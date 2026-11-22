using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Calidad gráfica que se puede cambiar en vivo desde el panel del operador
/// (comandos "bloom", "post", "hdr", "scale"). La maneja GameSettings: la guarda
/// en PlayerPrefs y la vuelve a aplicar en cada escena.
///
/// Por qué existe: medido en el A54 (2026-11-07) con el Profiler conectado, el
/// Bloom se lleva ~33 ms de ~56 ms por frame (RG_BloomDownsample solo, 28 ms).
/// Dibujar la escena cuesta menos de 3 ms. Así que lo que más pesa es el
/// post-procesado, que en Cardboard multi-pass se hace dos veces (una por ojo).
///
/// No toca los assets: trabaja sobre copias de los perfiles (Volume.profile),
/// así en el Editor no quedan cambios guardados.
///
/// Ojo (2026-11-08): el perfil por defecto del pipeline móvil
/// (SampleSceneProfile) trae Bloom en calidad alta, a media resolución y con 6
/// pasadas, y se aplica en toda escena donde ningún Volume lo pise (por ejemplo
/// el Menú). Por eso, además de las copias de los Volumes de la escena, se usa
/// un Volume global "base" propio (prioridad muy baja, sobrevive a las escenas)
/// que pisa al perfil por defecto.
/// </summary>
public static class GraphicsQuality
{
    public enum BloomMode { Off = 0, Low = 1, Full = 2 }

    private static Volume s_baseVolume;
    private static Bloom s_baseBloom;

    /// <summary>Volume global de prioridad mínima que pisa al perfil por defecto del pipeline.</summary>
    public static Volume BaseVolume
    {
        get
        {
            if (s_baseVolume == null)
            {
                GameObject go = new GameObject("Calidad (volume base)");
                Object.DontDestroyOnLoad(go);
                go.hideFlags = HideFlags.DontSave;
                s_baseVolume = go.AddComponent<Volume>();
                s_baseVolume.isGlobal = true;
                s_baseVolume.priority = -1000f;
                VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.hideFlags = HideFlags.DontSave;
                s_baseVolume.sharedProfile = profile;
                s_baseBloom = profile.Add<Bloom>();
            }
            return s_baseVolume;
        }
    }

    /// <summary>
    /// Aplica la calidad a la escena cargada. Se llama al cargar cada escena y
    /// cada vez que cambia una opción.
    /// </summary>
    public static void Apply(BloomMode bloom, bool post, bool hdr, float renderScale)
    {
        ApplyBaseBloom(bloom);

        foreach (Volume vol in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (vol == s_baseVolume)
            {
                continue;
            }
            VolumeProfile shared = vol.sharedProfile;
            if (shared == null || !shared.TryGet(out Bloom original))
            {
                continue;
            }

            // vol.profile crea (una vez) una copia propia de este Volume.
            if (!vol.profile.TryGet(out Bloom b))
            {
                continue;
            }

            switch (bloom)
            {
                case BloomMode.Off:
                    b.active = false;
                    break;

                case BloomMode.Low:
                    // Un cuarto de resolución y menos pasadas: el halo se ve
                    // casi igual en el visor y cuesta una fracción.
                    b.active = original.active;
                    b.downscale.Override(BloomDownscaleMode.Quarter);
                    b.maxIterations.Override(Mathf.Min(original.maxIterations.value, 3));
                    b.highQualityFiltering.Override(false);
                    break;

                default:
                    b.active = original.active;
                    b.downscale.overrideState = original.downscale.overrideState;
                    b.downscale.value = original.downscale.value;
                    b.maxIterations.overrideState = original.maxIterations.overrideState;
                    b.maxIterations.value = original.maxIterations.value;
                    b.highQualityFiltering.overrideState = original.highQualityFiltering.overrideState;
                    b.highQualityFiltering.value = original.highQualityFiltering.value;
                    break;
            }
        }

        foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
        {
            // Las cámaras fuera de XR (como la de la grabación de Parte 2) se dejan como están.
            if (!cam.stereoEnabled && cam != Camera.main)
            {
                continue;
            }
            cam.allowHDR = hdr;
            if (cam.TryGetComponent(out UniversalAdditionalCameraData data))
            {
                data.renderPostProcessing = post;
            }
        }

        if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && !Application.isEditor)
        {
            // En el Editor no se toca: cambiaría el asset guardado.
            urp.renderScale = renderScale;
        }
    }

    /// <summary>
    /// Pisa al Bloom del perfil por defecto del pipeline. Los Volumes de la
    /// escena tienen más prioridad, así que donde haya uno manda su copia.
    /// </summary>
    private static void ApplyBaseBloom(BloomMode bloom)
    {
        _ = BaseVolume;
        Bloom b = s_baseBloom;
        b.active = true;
        b.intensity.overrideState = bloom == BloomMode.Off;
        b.intensity.value = 0f;
        bool low = bloom == BloomMode.Low;
        b.downscale.Override(BloomDownscaleMode.Quarter);
        b.downscale.overrideState = low;
        b.maxIterations.Override(3);
        b.maxIterations.overrideState = low;
        b.highQualityFiltering.Override(false);
        b.highQualityFiltering.overrideState = low;
    }
}
