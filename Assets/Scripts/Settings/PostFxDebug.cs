using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Diagnóstico del costo del post-procesado en el celular (comando "fx" del panel
/// del operador). No se guarda: sirve para apagar cada pieza por separado y medir.
///
/// Medido en el A54 (2026-11-08), Menú: con post-procesado ~19 FPS, sin él ~59,
/// y apagar solo el Bloom o el HDR no cambia nada. Este comando aísla qué parte
/// del camino del post-procesado es la cara.
///
///   fx dither on|off     dithering de la cámara
///   fx vignette on|off   viñeta (escena + perfil por defecto)
///   fx tonemap on|off    tonemapping (escena + perfil por defecto)
///   fx color on|off      grading: Color Adjustments, Split Toning, etc.
///   fx grain on|off      grano
///   fx nrp on|off        Native RenderPass del renderer
///   fx inter auto|always textura intermedia del renderer
/// </summary>
public static class PostFxDebug
{
    private static Vignette s_baseVignette;
    private static Tonemapping s_baseTonemap;

    public static bool Set(string what, string value)
    {
        bool on = value == "on";
        if (what == "bfilter")
        {
            return SetBloomFilter(value);
        }
        if (value != "on" && value != "off" && what != "inter")
        {
            return false;
        }

        switch (what)
        {
            case "dither":
                foreach (Camera cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
                {
                    if (cam.TryGetComponent(out UniversalAdditionalCameraData d))
                    {
                        d.dithering = on;
                    }
                }
                return true;

            case "vignette":
                SetSceneActive(on, typeof(Vignette));
                Base<Vignette>(ref s_baseVignette).intensity.Override(0f);
                s_baseVignette.intensity.overrideState = !on;
                return true;

            case "tonemap":
                SetSceneActive(on, typeof(Tonemapping));
                Base<Tonemapping>(ref s_baseTonemap).mode.Override(TonemappingMode.None);
                s_baseTonemap.mode.overrideState = !on;
                return true;

            case "color":
                SetSceneActive(on, typeof(ColorAdjustments), typeof(SplitToning), typeof(WhiteBalance),
                    typeof(ChannelMixer), typeof(LiftGammaGain), typeof(ShadowsMidtonesHighlights), typeof(ColorCurves));
                return true;

            case "grain":
                SetSceneActive(on, typeof(FilmGrain));
                return true;

            case "nrp":
                Renderer().useNativeRenderPass = on;
                return true;

            case "inter":
                if (value != "auto" && value != "always")
                {
                    return false;
                }
                Renderer().intermediateTextureMode = value == "always" ? IntermediateTextureMode.Always : IntermediateTextureMode.Auto;
                return true;
        }
        return false;
    }

    /// <summary>fx bfilter gaussian|dual|kawase: filtro del Bloom en la base y en las copias de la escena.</summary>
    private static bool SetBloomFilter(string value)
    {
        BloomFilterMode mode;
        if (value == "gaussian") mode = BloomFilterMode.Gaussian;
        else if (value == "dual") mode = BloomFilterMode.Dual;
        else if (value == "kawase") mode = BloomFilterMode.Kawase;
        else return false;

        foreach (Volume vol in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            VolumeProfile p = vol == GraphicsQuality.BaseVolume ? vol.sharedProfile : (vol.sharedProfile != null ? vol.profile : null);
            if (p != null && p.TryGet(out Bloom b))
            {
                b.filter.Override(mode);
            }
        }
        return true;
    }

    private static UniversalRendererData Renderer()
    {
        var urp = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
        return (UniversalRendererData)urp.rendererDataList[0];
    }

    private static T Base<T>(ref T cached) where T : VolumeComponent
    {
        if (cached == null)
        {
            VolumeProfile p = GraphicsQuality.BaseVolume.sharedProfile;
            if (!p.TryGet(out cached))
            {
                cached = p.Add<T>();
            }
        }
        cached.active = true;
        return cached;
    }

    // Recuerda el estado original de cada componente de las copias de los Volumes.
    private static readonly Dictionary<VolumeComponent, bool> s_original = new Dictionary<VolumeComponent, bool>();

    private static void SetSceneActive(bool on, params System.Type[] types)
    {
        foreach (Volume vol in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (vol == GraphicsQuality.BaseVolume || vol.sharedProfile == null)
            {
                continue;
            }
            foreach (VolumeComponent c in vol.profile.components)
            {
                if (System.Array.IndexOf(types, c.GetType()) < 0)
                {
                    continue;
                }
                if (!s_original.ContainsKey(c))
                {
                    s_original[c] = c.active;
                }
                c.active = on && s_original[c];
            }
        }
    }
}
