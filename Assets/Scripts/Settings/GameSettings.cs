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

    // "Velocidad de carga del gaze" = segundos de mirada sostenida para
    // seleccionar. Menos segundos = mas rapido. El indice del array es (int)Level.
    private static readonly float[] GazeSelectSecondsByLevel = { 3f, 2f, 1.2f };

    // "Tamaño del reticulo" = multiplicador sobre el tamaño original del reticulo
    // en cada escena. El indice del array es (int)Level.
    private static readonly float[] ReticleScaleByLevel = { 0.7f, 1f, 1.5f };

    public static GameSettings Instance { get; private set; }

    public Level GazeSpeed { get; private set; } = Level.Medio;
    public Level ReticleSize { get; private set; } = Level.Medio;

    /// <summary>Segundos de mirada sostenida para seleccionar, segun el preset actual.</summary>
    public float GazeSelectDuration => GazeSelectSecondsByLevel[Mathf.Clamp((int)GazeSpeed, 0, 2)];

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
