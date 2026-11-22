using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Contenedor persistente de las opciones que el jugador elige en el panel de
/// configuracion de la escena "Menu Principal": velocidad de carga del gaze y
/// tamaño del reticulo, cada una con tres presets (Bajo / Medio / Alto).
///
/// - Guarda todo en PlayerPrefs -> se mantiene entre escenas y entre sesiones.
/// - Sobrevive los cambios de escena (DontDestroyOnLoad).
/// - Se crea solo al arrancar el juego (RuntimeInitializeOnLoadMethod), asi que
///   no hay que ponerlo en ninguna escena a mano. Igual el generador de la
///   escena de menu deja uno puesto para que se vea claro en el Inspector.
/// - Cada vez que se carga una escena (o cambia un preset) se auto-aplica a los
///   GazeController / GazeReticle que haya en esa escena. No hace falta ningun
///   componente "applier" por escena.
/// </summary>
public class GameSettings : MonoBehaviour
{
    /// <summary>Los tres presets disponibles para cada opcion.</summary>
    public enum Level
    {
        Bajo = 0,
        Medio = 1,
        Alto = 2
    }

    // Claves de PlayerPrefs. No renombrar sin migrar los datos ya guardados en
    // los dispositivos.
    private const string GazeSpeedKey = "cfg.gazeSpeed";
    private const string ReticleSizeKey = "cfg.reticleSize";
    private const string FairModeKey = "cfg.fairMode";
    private const string PortalsVisibleKey = "cfg.portalsVisible";
    private const string TargetFpsKey = "cfg.targetFps";
    private const string BloomKey = "cfg.bloom";
    private const string PostKey = "cfg.post";
    private const string HdrKey = "cfg.hdr";
    private const string ScaleKey = "cfg.renderScale";

    // Con modo feria la mirada queda fija en este valor, sin importar el preset
    // de GazeSpeed: en la feria pasan muchas personas que nunca usaron VR y no
    // hay tiempo de explicarles el menu de configuracion.
    private const float FairModeGazeSeconds = 1.2f;

    // Multiplicador de los tiempos de HintDirector. Con modo feria las pistas
    // llegan antes (cada visitante tiene pocos minutos).
    private const float FairModeHintTimeScale = 0.6f;

    // "Velocidad de carga del gaze" = segundos de mirada sostenida para
    // seleccionar. Menos segundos = mas rapido. El indice del array es (int)Level.
    private static readonly float[] GazeSelectSecondsByLevel = { 3f, 2f, 1.2f };

    // "Tamaño del reticulo" = multiplicador sobre el tamaño original del reticulo
    // en cada escena. El indice del array es (int)Level.
    private static readonly float[] ReticleScaleByLevel = { 0.7f, 1f, 1.5f };

    public static GameSettings Instance { get; private set; }

    public Level GazeSpeed { get; private set; } = Level.Medio;
    public Level ReticleSize { get; private set; } = Level.Medio;

    /// <summary>
    /// Modo feria (prendido por defecto): mirada fija de 1.2 s y pistas mas
    /// rapidas (HintTimeScale 0.6). Apagado vuelve al preset de GazeSpeed.
    /// </summary>
    public bool FairMode { get; private set; } = true;

    /// <summary>
    /// Portales visibles (true, por defecto) o sutiles (false). Los portales
    /// escuchan FeriaEvents.PortalVisibilityChanged o leen este valor al arrancar.
    /// </summary>
    public bool PortalsVisible { get; private set; } = true;

    /// <summary>Cuadros por segundo objetivo (30 o 60, default 60).</summary>
    public int TargetFps { get; private set; } = 60;

    // Calidad gráfica (ver GraphicsQuality). Se cambia en vivo desde el panel del operador.
    public GraphicsQuality.BloomMode Bloom { get; private set; } = GraphicsQuality.BloomMode.Low;
    public bool PostProcessing { get; private set; } = true;
    public bool Hdr { get; private set; } = true;
    public float RenderScale { get; private set; } = 0.8f;

    /// <summary>Multiplicador de los tiempos de pista: 0.6 con modo feria, 1 sin.</summary>
    public float HintTimeScale => FairMode ? FairModeHintTimeScale : 1f;

    /// <summary>
    /// Segundos de mirada sostenida para seleccionar. Con modo feria es fijo
    /// (1.2 s); sin modo feria sale del preset de GazeSpeed.
    /// </summary>
    public float GazeSelectDuration => FairMode
        ? FairModeGazeSeconds
        : GazeSelectSecondsByLevel[Mathf.Clamp((int)GazeSpeed, 0, 2)];

    /// <summary>Multiplicador de tamaño del reticulo, segun el preset actual.</summary>
    public float ReticleScale => ReticleScaleByLevel[Mathf.Clamp((int)ReticleSize, 0, 2)];

    // Se ejecuta una vez al arrancar el juego, despues de cargar la primera
    // escena. Garantiza que GameSettings exista siempre (aunque se pruebe una
    // escena de gameplay directo sin pasar por el menu) y aplica los presets
    // guardados a esa primera escena.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        GetOrCreate().Apply();
    }

    private void Awake()
    {
        // Singleton persistente: si ya hay una instancia (por ejemplo, volviste
        // al Menu Principal desde otra escena que tambien tiene un GameSettings
        // puesto a mano), esta de mas.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        GazeSpeed = (Level)PlayerPrefs.GetInt(GazeSpeedKey, (int)Level.Medio);
        ReticleSize = (Level)PlayerPrefs.GetInt(ReticleSizeKey, (int)Level.Medio);
        FairMode = PlayerPrefs.GetInt(FairModeKey, 1) != 0;
        PortalsVisible = PlayerPrefs.GetInt(PortalsVisibleKey, 1) != 0;
        TargetFps = PlayerPrefs.GetInt(TargetFpsKey, 60) == 30 ? 30 : 60;
        ApplyFrameRate();
        Bloom = (GraphicsQuality.BloomMode)Mathf.Clamp(PlayerPrefs.GetInt(BloomKey, (int)GraphicsQuality.BloomMode.Low), 0, 2);
        PostProcessing = PlayerPrefs.GetInt(PostKey, 1) != 0;
        Hdr = PlayerPrefs.GetInt(HdrKey, 1) != 0;
        RenderScale = Mathf.Clamp(PlayerPrefs.GetFloat(ScaleKey, 0.8f), 0.5f, 1f);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply();
        if (Bloom != GraphicsQuality.BloomMode.Off)
        {
            StartCoroutine(RearmBloom());
        }
    }

    /// <summary>
    /// Medido en el A54 (2026-11-08): en el Tutorial, el Bloom armado al cargar la
    /// escena costaba ~4–10 FPS más que después de apagarlo y prenderlo (en otras
    /// escenas no cambia nada). No se encontró la causa; rearmarlo unos frames
    /// después de cargar lo deja en el estado barato. Pasa durante el fundido de
    /// carga, así que no se ve.
    /// </summary>
    private System.Collections.IEnumerator RearmBloom()
    {
        GraphicsQuality.Apply(GraphicsQuality.BloomMode.Off, PostProcessing, Hdr, RenderScale);
        for (int i = 0; i < 3; i++)
        {
            yield return null;
        }
        GraphicsQuality.Apply(Bloom, PostProcessing, Hdr, RenderScale);
    }

    public void SetGazeSpeed(Level level)
    {
        GazeSpeed = level;
        PlayerPrefs.SetInt(GazeSpeedKey, (int)level);
        PlayerPrefs.Save();
        Apply();
    }

    public void SetReticleSize(Level level)
    {
        ReticleSize = level;
        PlayerPrefs.SetInt(ReticleSizeKey, (int)level);
        PlayerPrefs.Save();
        Apply();
    }

    /// <summary>Prende o apaga el modo feria, lo guarda y lo aplica a la escena.</summary>
    public void SetFairMode(bool on)
    {
        FairMode = on;
        PlayerPrefs.SetInt(FairModeKey, on ? 1 : 0);
        PlayerPrefs.Save();
        Apply();
    }

    /// <summary>Fija 30 o 60 fps (otro valor se ignora), lo guarda y lo aplica.</summary>
    public void SetTargetFps(int fps)
    {
        if (fps != 30 && fps != 60)
        {
            return;
        }
        TargetFps = fps;
        PlayerPrefs.SetInt(TargetFpsKey, fps);
        PlayerPrefs.Save();
        ApplyFrameRate();
    }

    /// <summary>Cambia la calidad gráfica, la guarda y la aplica a la escena actual.</summary>
    public void SetGraphics(GraphicsQuality.BloomMode bloom, bool post, bool hdr, float renderScale)
    {
        Bloom = bloom;
        PostProcessing = post;
        Hdr = hdr;
        RenderScale = Mathf.Clamp(renderScale, 0.5f, 1f);
        PlayerPrefs.SetInt(BloomKey, (int)bloom);
        PlayerPrefs.SetInt(PostKey, post ? 1 : 0);
        PlayerPrefs.SetInt(HdrKey, hdr ? 1 : 0);
        PlayerPrefs.SetFloat(ScaleKey, RenderScale);
        PlayerPrefs.Save();
        GraphicsQuality.Apply(Bloom, PostProcessing, Hdr, RenderScale);
    }

    private void ApplyFrameRate()
    {
        Application.targetFrameRate = TargetFps;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }

    /// <summary>
    /// Portales visibles (true) o sutiles (false). Lo guarda, lo aplica y avisa
    /// por FeriaEvents.PortalVisibilityChanged a los portales de la escena.
    /// </summary>
    public void SetPortalsVisible(bool visible)
    {
        PortalsVisible = visible;
        PlayerPrefs.SetInt(PortalsVisibleKey, visible ? 1 : 0);
        PlayerPrefs.Save();
        Apply();
        FeriaEvents.NotifyPortalVisibility(visible);
    }

    /// <summary>
    /// Empuja los valores actuales al GazeController y al GazeReticle de la
    /// escena activa. Se llama solo al cargar cada escena y cada vez que cambia
    /// un preset. Si la escena no tiene esos componentes, no hace nada.
    /// </summary>
    public void Apply()
    {
        GazeController gaze = FindFirstObjectByType<GazeController>();
        if (gaze != null)
        {
            gaze.SetSelectDuration(GazeSelectDuration);
        }

        GazeReticle reticle = FindFirstObjectByType<GazeReticle>();
        if (reticle != null)
        {
            reticle.SetSizeMultiplier(ReticleScale);
        }

        GraphicsQuality.Apply(Bloom, PostProcessing, Hdr, RenderScale);
    }

    /// <summary>
    /// Devuelve la instancia, creandola si todavia no existe. Asi ningun script
    /// tiene que preocuparse por el orden de carga.
    /// </summary>
    public static GameSettings GetOrCreate()
    {
        if (Instance == null)
        {
            new GameObject("GameSettings").AddComponent<GameSettings>();
        }
        return Instance;
    }
}
