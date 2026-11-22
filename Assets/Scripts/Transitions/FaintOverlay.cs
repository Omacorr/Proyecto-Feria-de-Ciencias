using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Pantalla de "desmayo" que SOBREVIVE el cambio de escena
/// (DontDestroyOnLoad): espera el Pre Delay (la escena sigue visible: puerta
/// abriendose, caida, derrumbe), funde a un color (negro, o blanco-amarillo
/// para el final de Parte 4), carga la escena siguiente y recien ahi "abre los
/// ojos" ya adentro de la escena nueva. La escena destino no necesita tener
/// nada especial puesto - sirve igual para escenas del compañero (Parte 2).
///
/// Por que una esfera y no un Canvas: un Canvas Screen Space Overlay NO se ve
/// en estereo Cardboard (gotcha #7 de CLAUDE.md), y un Canvas World Space
/// pegado a la camara no puede ser hijo de la Main Camera de otra escena
/// (se destruiria al descargarla). Una esfera chica centrada en la camara
/// tapa TODAS las direcciones sin importar hacia donde mire el jugador, asi
/// que solo hace falta seguir la POSICION de la camara. Se reposiciona en
/// LateUpdate, en Application.onBeforeRender y en
/// RenderPipelineManager.beginCameraRendering (con la camara que se esta por
/// dibujar): asi tapa aunque la camara de la escena nueva no tenga el tag
/// MainCamera o aunque Camera.main cambie a mitad de la transicion.
///
/// Material, en orden de preferencia (robusto para el build de Android, donde
/// un shader que ningun material de las escenas incluidas referencia se
/// stripea y Shader.Find devuelve null):
///  1. El Overlay Material de FaintTransition (Mat_ReticleAlwaysOnTop). Como
///     queda referenciado desde la escena, su shader SIEMPRE entra al build.
///  2. Shader.Find("UI/AlwaysOnTop") (anda en el Editor; en el build solo si
///     alguna escena cargada lo referencia).
///  3. Shader.Find("UI/Default") forzado a ZTest Always. UI/Default esta en
///     Always Included Shaders (Project Settings > Graphics): existe siempre.
///  4. Shader.Find("Hidden/Internal-Colored") forzado a ZTest Always.
///  5. Nada: se loguea un error pero la escena se carga igual (nunca deja al
///     jugador trabado).
///
/// Texto de nivel (opcional, apagado por defecto - decision abierta #6): un
/// TextMesh 3D con la fuente interna de Unity (su shader GUI/Text ya dibuja con
/// ZTest Always y viene en los recursos internos, siempre esta en el build),
/// pegado adelante de la camara solo mientras la pantalla esta tapada.
///
/// Respaldo (Settings.StayCovered): si la escena destino no esta en Build
/// Settings, FaintTransition lo pide igual pero sin cargar nada: funde, muestra
/// el texto y la pantalla queda tapada (pantalla de "fin" en vez de trabarse).
///
/// No se pone en ninguna escena a mano: lo crea FaintTransition.
/// </summary>
public class FaintOverlay : MonoBehaviour
{
    /// <summary>
    /// true desde que se dispara un desmayo (incluye el Pre Delay) hasta que el
    /// jugador termina de abrir los ojos en la escena nueva. Mientras sea true,
    /// cualquier otro pedido de desmayo se ignora.
    /// </summary>
    public static bool IsRunning => s_active != null;

    // ---------------- Eventos de transicion (para GameTimer, feria) ----------------
    // Estaticos para que el cronometro no tenga que conocer a cada overlay.
    // SceneTransitionOverlay y MainMenuController los disparan con los Notify*
    // de abajo, asi todo cambio de escena "con carga" pasa por el mismo lugar.
    // Un listener que tire excepcion nunca corta la transicion.

    /// <summary>Se disparo un desmayo/transicion. Parametro: escena destino.</summary>
    public static event Action<string> FaintStarted;

    /// <summary>Justo antes de LoadSceneAsync. Parametro: escena que se carga.</summary>
    public static event Action<string> LoadBegin;

    /// <summary>La escena nueva ya quedo activa (op.isDone). Parametro: su nombre.</summary>
    public static event Action<string> SceneActivated;

    /// <summary>El jugador termino de abrir los ojos en la escena nueva. Parametro: su nombre.</summary>
    public static event Action<string> EyesOpened;

    internal static void NotifyFaintStarted(string scene) => SafeRaise(FaintStarted, scene);
    internal static void NotifyLoadBegin(string scene) => SafeRaise(LoadBegin, scene);
    internal static void NotifySceneActivated(string scene) => SafeRaise(SceneActivated, scene);
    internal static void NotifyEyesOpened(string scene) => SafeRaise(EyesOpened, scene);

    private static void SafeRaise(Action<string> evt, string scene)
    {
        if (evt == null)
        {
            return;
        }
        foreach (Delegate d in evt.GetInvocationList())
        {
            try
            {
                ((Action<string>)d)(scene);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }
    }

    public struct Settings
    {
        public string SceneName;
        public Color Color;
        public float FadeOutDuration;
        public float HoldSeconds;
        public float WakeDuration;
        public bool BlinkOnWake;
        public AudioClip ImpactClip;
        public AudioClip WakeClip;
        public Material Material;

        // Agregados despues (opcionales: 0 / false / null = comportamiento anterior).
        /// <summary>Segundos con la escena todavia visible antes del fundido.</summary>
        public float PreDelay;
        /// <summary>Mostrar texto de nivel mientras la pantalla esta tapada.</summary>
        public bool ShowText;
        public string Title;
        public string Subtitle;
        /// <summary>Fuente del texto. null = fuente interna de Unity.</summary>
        public Font Font;

        /// <summary>
        /// Respaldo: NO cargar ninguna escena. Funde al color, muestra el texto
        /// (si hay) y se queda asi, con la mirada bloqueada. Lo usa
        /// FaintTransition cuando la escena destino no esta en Build Settings
        /// (por ejemplo, un build de prueba sin el Menu): mejor una pantalla de
        /// "fin" que dejar al jugador trabado a medias (ojos cerrados, cayendo,
        /// encandilado). Si otra cosa cambia de escena, el overlay se va solo.
        /// false = comportamiento de siempre (carga SceneName).
        /// </summary>
        public bool StayCovered;
    }

    // Esfera en 4999 y texto en 5000: los dos por encima del reticle y del
    // FadeImage (UI/AlwaysOnTop = Overlay = 4000), y el texto encima de la esfera.
    private const int SphereQueue = 4999;
    private const int TextQueue = 5000;

    private const float TextDistance = 3f;       // metros adelante de la camara
    private const float TextBlockHeight = 0.45f; // alto total del bloque de texto a esa distancia
    private const int TitleFontSize = 64;
    private const int SubtitleFontSize = 34;
    private const float TextFadeSeconds = 0.4f;

    private static FaintOverlay s_active;

    private Settings _s;
    private Material _mat;
    private Mesh _mesh;
    private Transform _sphere;
    private MeshRenderer _sphereRenderer;
    private AudioSource _audio;

    private Transform _textTransform;
    private TextMesh _text;
    private MeshRenderer _textRenderer;
    private Material _textMat;
    private Font _font;
    private Color _textColor = Color.white;
    private bool _textCalibrated;

    private readonly List<GazeController> _blockedGaze = new List<GazeController>();

    // Por si el Editor tiene "Enter Play Mode Options" sin domain reload: que
    // un desmayo cortado al salir de Play no deje IsRunning trabado en true.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_active = null;
    }

    public static void Play(Settings settings)
    {
        if (IsRunning)
        {
            Debug.Log("[FaintOverlay] Ya hay un desmayo en curso, se ignora el pedido.");
            return;
        }

        NotifyFaintStarted(settings.SceneName);

        GameObject go = new GameObject("FaintOverlay");
        DontDestroyOnLoad(go);
        FaintOverlay o = go.AddComponent<FaintOverlay>();
        s_active = o;
        o._s = settings;
        try
        {
            o.Build();
        }
        catch (Exception e)
        {
            // Si falla algo del armado visual, igual se carga la escena: lo
            // peor que puede pasar es quedarse trabado con la mirada bloqueada.
            Debug.LogException(e);
        }
        o.StartCoroutine(o.Run());
    }

    // ---------------- Armado ----------------

    private void Build()
    {
        // El audio primero: si despues falla el material, el golpe seco suena igual.
        _audio = gameObject.AddComponent<AudioSource>();
        _audio.playOnAwake = false;
        _audio.spatialBlend = 0f;

        _mat = OverlayUtil.CreateOverlayMaterial(_s.Material, "[FaintOverlay]", SphereQueue);
        if (_mat != null)
        {
            _sphereRenderer = OverlayUtil.CreateSphere("FaintSphere", transform, _mat, true, out _mesh);
            _sphere = _sphereRenderer.transform;
        }

        if (_s.ShowText)
        {
            BuildText();
        }

        Application.onBeforeRender += FollowCamera;
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;

        FollowCamera();
        SetAlpha(0f);
        SetTextAlpha(0f);
    }

    private void BuildText()
    {
        string title = _s.Title ?? string.Empty;
        string sub = _s.Subtitle ?? string.Empty;
        if (title.Length == 0 && sub.Length == 0)
        {
            return;
        }

        // Una fuente importada (.ttf) usa el mismo shader GUI/Text que la interna,
        // asi que el resto del armado (copia del material, cola, ZTest) no cambia.
        _font = _s.Font != null && _s.Font.material != null
            ? _s.Font
            : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (_font == null || _font.material == null)
        {
            Debug.LogWarning("[FaintOverlay] No encontre la fuente interna LegacyRuntime.ttf: el desmayo sigue sin texto.");
            _font = null;
            return;
        }

        // Estimacion inicial (una linea de TextMesh mide ~ fontSize * characterSize / 10);
        // despues CalibrateText() lo corrige midiendo el mesh real.
        _text = OverlayUtil.CreateText("FaintText", transform, _font, TitleFontSize,
            (TextBlockHeight * 0.65f) * 10f / TitleFontSize, TextQueue, out _textRenderer, out _textMat);
        _textTransform = _text.transform;

        string content = title.Length > 0 ? "<b>" + title + "</b>" : string.Empty;
        if (sub.Length > 0)
        {
            content += (content.Length > 0 ? "\n" : string.Empty) + "<size=" + SubtitleFontSize + ">" + sub + "</size>";
        }
        _text.text = content;
        Font.textureRebuilt += OnFontTextureRebuilt;

        // Letra negra sobre fundido claro (Parte 4, blanco-amarillo), blanca sobre oscuro.
        float lum = 0.299f * _s.Color.r + 0.587f * _s.Color.g + 0.114f * _s.Color.b;
        _textColor = lum > 0.5f ? Color.black : Color.white;
    }

    private void OnFontTextureRebuilt(Font f) => OverlayUtil.RefreshFontTexture(f, _font, _textMat);

    private void CalibrateText()
    {
        if (_textCalibrated || _textRenderer == null || _textTransform == null)
        {
            return;
        }
        float h = _textRenderer.localBounds.size.y;
        if (h <= 0.0001f)
        {
            return; // el mesh todavia no se genero; se reintenta el frame siguiente
        }
        float k = Mathf.Clamp(TextBlockHeight / h, 0.01f, 100f);
        _textTransform.localScale = new Vector3(k, k, k);
        _textCalibrated = true;
    }

    // ---------------- Seguir a la camara ----------------

    private void LateUpdate()
    {
        FollowCamera();
        CalibrateText();
    }

    // Se llama en LateUpdate Y justo antes de renderizar, porque el
    // TrackedPoseDriver/Cardboard puede actualizar la camara despues del LateUpdate.
    private void FollowCamera()
    {
        Camera cam = OverlayUtil.ResolveCamera();
        if (cam != null)
        {
            PlaceAt(cam);
        }
    }

    // La camara que REALMENTE se esta por dibujar: cubre el caso de una escena
    // nueva cuya camara no tenga el tag MainCamera, o de varias camaras.
    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera cam)
    {
        if (cam != null && (cam.cameraType == CameraType.Game || cam.cameraType == CameraType.VR))
        {
            PlaceAt(cam);
        }
    }

    private void PlaceAt(Camera cam)
    {
        if (_sphere != null)
        {
            OverlayUtil.PlaceSphere(_sphere, cam);
        }
        if (_textTransform != null)
        {
            Transform ct = cam.transform;
            float dist = Mathf.Max(TextDistance, cam.nearClipPlane * 4f);
            _textTransform.SetPositionAndRotation(ct.position + ct.forward * dist, ct.rotation);
        }
    }

    // ---------------- Fundidos ----------------

    private void SetAlpha(float a)
    {
        if (_mat != null)
        {
            _mat.color = new Color(_s.Color.r, _s.Color.g, _s.Color.b, a);
        }
        // Apagado mientras es transparente: no gasta fill-rate en el celular
        // durante el Pre Delay.
        if (_sphereRenderer != null)
        {
            _sphereRenderer.enabled = a > 0.001f;
        }
    }

    private void SetTextAlpha(float a)
    {
        if (_text != null)
        {
            _text.color = new Color(_textColor.r, _textColor.g, _textColor.b, a);
        }
        if (_textRenderer != null)
        {
            _textRenderer.enabled = a > 0.001f;
        }
    }

    private IEnumerator Fade(float from, float to, float duration) => FadeWith(SetAlpha, from, to, duration);

    private IEnumerator FadeText(float from, float to, float duration) => FadeWith(SetTextAlpha, from, to, duration);

    private static IEnumerator FadeWith(Action<float> set, float from, float to, float duration)
    {
        float e = 0f;
        while (e < duration)
        {
            e += Time.unscaledDeltaTime;
            set(Mathf.Lerp(from, to, Mathf.Clamp01(e / duration)));
            yield return null;
        }
        set(to);
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip != null && _audio != null)
        {
            _audio.PlayOneShot(clip);
        }
    }

    // ---------------- Secuencia ----------------

    private IEnumerator Run()
    {
        // 0. La escena sigue visible (puerta abriendose, caida, derrumbe...).
        //    Tiempo escalado, igual que las animaciones de AbyssFall/WallCollapse.
        if (_s.PreDelay > 0f)
        {
            yield return new WaitForSeconds(_s.PreDelay);
        }

        // 1. Colapso: golpe seco + fundido al color. El golpe arranca junto con
        //    el fundido; con Fade Out Duration corto (0.12) coincide con el corte.
        PlayClip(_s.ImpactClip);
        yield return Fade(0f, 1f, Mathf.Max(0.01f, _s.FadeOutDuration));

        // Respaldo sin escena destino: quedarse tapado con el texto (ver StayCovered).
        if (_s.StayCovered)
        {
            yield return HoldCovered();
            yield break;
        }

        // 2. Cargar la escena siguiente en segundo plano, con la pantalla tapada.
        AsyncOperation op = null;
        NotifyLoadBegin(_s.SceneName);
        try
        {
            op = SceneManager.LoadSceneAsync(_s.SceneName);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
        }
        if (op == null)
        {
            Debug.LogError("[FaintOverlay] No pude cargar '" + _s.SceneName + "'. Esta agregada y tildada en File > Build Settings, con el nombre EXACTO?");
            // Se avisa igual (con la escena que sigue activa) para que el
            // cronometro no quede congelado por una carga que nunca empezo.
            NotifySceneActivated(SceneManager.GetActiveScene().name);
            yield return Fade(1f, 0f, 0.5f);
            // FaintTransition habia bloqueado la mirada: devolverla para no dejar al jugador trabado.
            foreach (GazeController gaze in FindObjectsByType<GazeController>(FindObjectsSortMode.None))
            {
                gaze.enabled = true;
            }
            Release();
            Destroy(gameObject);
            yield break;
        }
        op.allowSceneActivation = false;

        float held = 0f;
        bool hasText = _text != null;
        if (hasText)
        {
            yield return FadeText(0f, 1f, TextFadeSeconds);
            held += TextFadeSeconds;
        }
        float textOut = hasText ? TextFadeSeconds : 0f;
        // Con allowSceneActivation = false, progress se clava en 0.9 al terminar de cargar.
        while (held < _s.HoldSeconds - textOut || op.progress < 0.9f)
        {
            held += Time.unscaledDeltaTime;
            yield return null;
        }
        if (hasText)
        {
            yield return FadeText(1f, 0f, TextFadeSeconds);
        }

        op.allowSceneActivation = true;
        while (!op.isDone)
        {
            yield return null;
        }
        NotifySceneActivated(SceneManager.GetActiveScene().name);

        // 3. Ya en la escena nueva: "abrir los ojos de golpe". La mirada queda
        //    bloqueada hasta tener los ojos abiertos (que no se pueda elegir un
        //    teletransporte a ciegas).
        yield return null;
        BlockGazeInNewScene();

        PlayClip(_s.WakeClip);
        float wake = Mathf.Max(0.05f, _s.WakeDuration);
        if (_s.BlinkOnWake)
        {
            // Parpadeo: abre a medias, vuelve a cerrar, y abre del todo.
            yield return Fade(1f, 0.35f, 0.18f);
            yield return Fade(0.35f, 1f, 0.12f);
            yield return new WaitForSecondsRealtime(0.25f);
        }
        yield return Fade(1f, 0f, wake);

        UnblockGaze();
        Release(); // desde aca ya se puede disparar otro desmayo
        NotifyEyesOpened(SceneManager.GetActiveScene().name);

        // Dejar terminar los sonidos antes de destruir el objeto (con tope,
        // por si el audio queda pausado y isPlaying no baja nunca).
        float maxWait = 0.5f + Mathf.Max(
            _s.WakeClip != null ? _s.WakeClip.length : 0f,
            _s.ImpactClip != null ? _s.ImpactClip.length : 0f);
        float waited = 0f;
        while (_audio != null && _audio.isPlaying && waited < maxWait)
        {
            waited += Time.unscaledDeltaTime;
            yield return null;
        }
        Destroy(gameObject);
    }

    /// <summary>
    /// Pantalla tapada "para siempre": muestra el texto y espera. La mirada
    /// sigue bloqueada (la bloqueo FaintTransition al disparar) e IsRunning
    /// sigue en true, asi ningun otro desmayo se superpone. Si algo externo
    /// cambia la escena activa, se libera y se destruye para no tapar la nueva.
    /// </summary>
    private IEnumerator HoldCovered()
    {
        if (_text != null)
        {
            yield return FadeText(0f, 1f, TextFadeSeconds);
        }
        Debug.Log("[FaintOverlay] Sin escena destino: la pantalla queda tapada (respaldo).");
        Scene start = SceneManager.GetActiveScene();
        while (SceneManager.GetActiveScene() == start)
        {
            yield return null;
        }
        Release();
        Destroy(gameObject);
    }

    private void BlockGazeInNewScene() => OverlayUtil.DisableGaze(_blockedGaze);

    private void UnblockGaze() => OverlayUtil.RestoreGaze(_blockedGaze);

    private void Release()
    {
        if (s_active == this)
        {
            s_active = null;
        }
    }

    private void OnDestroy()
    {
        Application.onBeforeRender -= FollowCamera;
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        Font.textureRebuilt -= OnFontTextureRebuilt;

        UnblockGaze();
        Release();

        // Material y mesh creados en runtime: no se liberan solos con el GameObject.
        if (_mat != null) Destroy(_mat);
        if (_textMat != null) Destroy(_textMat);
        if (_mesh != null) Destroy(_mesh);
    }
}
