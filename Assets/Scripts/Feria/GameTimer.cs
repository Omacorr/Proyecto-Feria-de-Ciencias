using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Cronometro de la feria (invisible para el jugador). Se crea solo al
/// arrancar el juego (RuntimeInitializeOnLoadMethod) y sobrevive los cambios
/// de escena (DontDestroyOnLoad): no hay que ponerlo en ninguna escena.
///
/// - Arranca con GameTimer.StartRun() (lo llama MainMenuController al Empezar).
/// - Mide con Time.realtimeSinceStartup y DESCUENTA las cargas: el reloj queda
///   congelado entre FaintOverlay.LoadBegin y la activacion de la escena nueva
///   (FaintOverlay.SceneActivated o, de respaldo, SceneManager.sceneLoaded).
/// - En cada desmayo (FaintOverlay.FaintStarted) guarda un parcial de la escena
///   que se deja.
/// - Termina cuando el desmayo sale de "Parte Final" hacia "Menu Principal".
///   Si se llega al Menu de cualquier otra forma con la partida corriendo, la
///   partida se cancela (abort).
/// - Loguea lineas "[CRONO] ..." (sin stack trace) que lee la app del operador
///   por adb logcat, y guarda un JSON por partida en
///   Application.persistentDataPath/partidas/.
///
/// Todos los segundos que se loguean son el tiempo ACTIVO acumulado desde el
/// start (sin las cargas), con 2 decimales y punto decimal.
/// </summary>
public class GameTimer : MonoBehaviour
{
    public const string MenuSceneName = "Menu Principal";
    public const string FinalSceneName = "Parte Final";
    private const float TickInterval = 5f;

    private static GameTimer s_instance;

    // Estado de la partida.
    private bool _running;
    private string _runId = string.Empty;
    private string _playerName = string.Empty;
    private float _startReal;
    private float _pausedTotal;      // segundos congelados por cargas
    private bool _frozen;
    private float _frozenSince;
    private float _finalElapsed;     // valor fijo despues de end/abort
    private float _lastSplitAt;      // segundos acumulados en el ultimo parcial
    private float _nextTickReal;
    private string _currentScene = string.Empty;
    private bool _expectingScene;    // hubo LoadBegin: la proxima activacion se loguea aunque sea la misma escena
    private RunRecord _record;

    // ---------------- JSON por partida ----------------

    [Serializable]
    private class SplitRecord
    {
        public string scene;
        public float seconds;   // duracion en esa escena (tiempo activo)
        public float atSeconds; // acumulado al dejarla
    }

    [Serializable]
    private class HintRecord
    {
        public string scene;
        public int step;
        public float atSeconds;
    }

    [Serializable]
    private class RunRecord
    {
        public string runId;
        public string playerName;
        public string startedAt; // fecha/hora local ISO 8601
        public bool finished;
        public bool aborted;
        public float totalSeconds;
        public List<SplitRecord> splits = new List<SplitRecord>();
        public List<HintRecord> hints = new List<HintRecord>();
    }

    // ---------------- API publica ----------------

    /// <summary>true mientras hay una partida corriendo.</summary>
    public static bool IsRunning => s_instance != null && s_instance._running;

    /// <summary>Segundos activos (sin cargas). Despues de end/abort queda fijo en el valor final.</summary>
    public static float ElapsedSeconds => s_instance != null ? s_instance.Elapsed() : 0f;

    /// <summary>Escena activa segun el cronometro.</summary>
    public static string CurrentScene => s_instance != null ? s_instance._currentScene : SceneManager.GetActiveScene().name;

    /// <summary>Nombre del jugador (lo carga el operador).</summary>
    public static string PlayerName => s_instance != null ? s_instance._playerName : string.Empty;

    /// <summary>Empieza una partida nueva. Si habia una corriendo, la cancela primero.</summary>
    public static void StartRun() => GetOrCreate().DoStart();

    /// <summary>Termina la partida (completa) y guarda el JSON.</summary>
    public static void EndRun() => GetOrCreate().DoEnd();

    /// <summary>Cancela la partida (no cuenta para el ranking) y guarda el JSON.</summary>
    public static void AbortRun() => GetOrCreate().DoAbort();

    /// <summary>Nombre del jugador actual (se puede cargar antes o durante la partida).</summary>
    public static void SetPlayerName(string playerName)
    {
        GameTimer t = GetOrCreate();
        t._playerName = (playerName ?? string.Empty).Trim();
        if (t._record != null)
        {
            t._record.playerName = t._playerName;
        }
        Log("name " + t._playerName);
    }

    /// <summary>Escribe una linea "[CRONO] ..." en el log, sin stack trace.</summary>
    public static void Log(string message)
    {
        Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "{0}", "[CRONO] " + message);
    }

    /// <summary>Segundos con 2 decimales y punto decimal (siempre, sin importar el idioma del celular).</summary>
    public static string Fmt(float seconds)
    {
        return seconds.ToString("F2", CultureInfo.InvariantCulture);
    }

    // ---------------- Creacion ----------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        GetOrCreate();
    }

    private static GameTimer GetOrCreate()
    {
        if (s_instance == null)
        {
            GameObject go = new GameObject("GameTimer (feria)");
            DontDestroyOnLoad(go);
            s_instance = go.AddComponent<GameTimer>();
        }
        return s_instance;
    }

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }
        s_instance = this;
        _currentScene = SceneManager.GetActiveScene().name;

        FaintOverlay.FaintStarted += OnFaintStarted;
        FaintOverlay.LoadBegin += OnLoadBegin;
        FaintOverlay.SceneActivated += OnSceneActivated;
        SceneManager.sceneLoaded += OnSceneLoaded;
        FeriaEvents.HintGiven += OnHintGiven;
    }

    private void OnDestroy()
    {
        if (s_instance != this)
        {
            return;
        }
        FaintOverlay.FaintStarted -= OnFaintStarted;
        FaintOverlay.LoadBegin -= OnLoadBegin;
        FaintOverlay.SceneActivated -= OnSceneActivated;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        FeriaEvents.HintGiven -= OnHintGiven;
        s_instance = null;
    }

    private void Update()
    {
        if (_running && Time.realtimeSinceStartup >= _nextTickReal)
        {
            _nextTickReal = Time.realtimeSinceStartup + TickInterval;
            Log("tick " + Fmt(Elapsed()) + " " + _currentScene);
        }
    }

    // ---------------- Tiempo ----------------

    private float Elapsed()
    {
        if (!_running)
        {
            return _finalElapsed;
        }
        float now = _frozen ? _frozenSince : Time.realtimeSinceStartup;
        return Mathf.Max(0f, now - _startReal - _pausedTotal);
    }

    private void DoStart()
    {
        if (_running)
        {
            DoAbort();
        }
        _running = true;
        _startReal = Time.realtimeSinceStartup;
        _pausedTotal = 0f;
        _frozen = false;
        _finalElapsed = 0f;
        _lastSplitAt = 0f;
        _expectingScene = false;
        _currentScene = SceneManager.GetActiveScene().name;
        _nextTickReal = _startReal + TickInterval;
        _runId = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        _record = new RunRecord
        {
            runId = _runId,
            playerName = _playerName,
            startedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
        };
        Log("start " + _runId);
    }

    private void DoEnd()
    {
        if (!_running)
        {
            return;
        }
        float t = Elapsed();
        Finish(t, false);
        Log("end " + Fmt(t));
    }

    private void DoAbort()
    {
        if (!_running)
        {
            return;
        }
        float t = Elapsed();
        Finish(t, true);
        Log("abort " + Fmt(t));
    }

    private void Finish(float t, bool aborted)
    {
        _finalElapsed = t;
        _running = false;
        _frozen = false;
        if (_record != null)
        {
            _record.finished = !aborted;
            _record.aborted = aborted;
            _record.totalSeconds = t;
            _record.playerName = _playerName;
            SaveRecord(_record);
        }
        _record = null;
        // El proximo jugador no hereda el nombre.
        _playerName = string.Empty;
    }

    private void AddSplit(string scene, float at)
    {
        Log("split " + Fmt(at) + " " + scene);
        if (_record != null)
        {
            _record.splits.Add(new SplitRecord
            {
                scene = scene,
                seconds = Mathf.Max(0f, at - _lastSplitAt),
                atSeconds = at,
            });
        }
        _lastSplitAt = at;
    }

    // ---------------- Eventos ----------------

    private void OnFaintStarted(string destination)
    {
        if (!_running)
        {
            return;
        }
        string leaving = SceneManager.GetActiveScene().name;
        float t = Elapsed();
        AddSplit(leaving, t);

        if (leaving == FinalSceneName && destination == MenuSceneName)
        {
            DoEnd();
        }
    }

    private void OnLoadBegin(string scene)
    {
        if (!_running || _frozen)
        {
            return;
        }
        _frozen = true;
        _frozenSince = Time.realtimeSinceStartup;
        _expectingScene = true;
        Log("loadstart " + Fmt(Elapsed()));
    }

    private void OnSceneActivated(string scene)
    {
        HandleSceneActive(scene);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Single)
        {
            HandleSceneActive(scene.name);
        }
    }

    // Llega dos veces por carga (sceneLoaded y SceneActivated): es idempotente.
    private void HandleSceneActive(string scene)
    {
        if (string.IsNullOrEmpty(scene))
        {
            return;
        }
        if (_frozen)
        {
            _pausedTotal += Time.realtimeSinceStartup - _frozenSince;
            _frozen = false;
            if (_running)
            {
                Log("loadend " + Fmt(Elapsed()));
            }
        }

        bool changed = scene != _currentScene || _expectingScene;
        _expectingScene = false;
        _currentScene = scene;
        if (!changed || !_running)
        {
            return;
        }

        Log("scene " + Fmt(Elapsed()) + " " + scene);
        if (scene == MenuSceneName)
        {
            // Se volvio al Menu sin terminar la Parte Final.
            DoAbort();
        }
    }

    private void OnHintGiven(string scene, int step)
    {
        if (!_running)
        {
            return;
        }
        float t = Elapsed();
        string s = string.IsNullOrEmpty(scene) ? _currentScene : scene;
        Log("hint " + step.ToString(CultureInfo.InvariantCulture) + " " + Fmt(t) + " " + s);
        if (_record != null)
        {
            _record.hints.Add(new HintRecord { scene = s, step = step, atSeconds = t });
        }
    }

    // ---------------- Guardado ----------------

    private static void SaveRecord(RunRecord record)
    {
        try
        {
            string dir = Path.Combine(Application.persistentDataPath, "partidas");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "partida-" + record.runId + ".json");
            File.WriteAllText(file, JsonUtility.ToJson(record, true));
        }
        catch (Exception e)
        {
            // Que un problema de disco nunca corte el juego.
            Debug.LogWarning("[GameTimer] No pude guardar la partida: " + e.Message);
        }
    }
}
