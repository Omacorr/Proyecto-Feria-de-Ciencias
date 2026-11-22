using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Medidor de rendimiento de la feria. Se crea solo (DontDestroyOnLoad) y cada
/// 5 s reales loguea "[CRONO] perf fpsProm fps1%bajo msPeorFrame escena", haya o
/// no partida. Guarda las ultimas ~600 muestras de unscaledDeltaTime en un
/// array fijo (sin basura por frame).
/// </summary>
public class PerfMeter : MonoBehaviour
{
    private const int SampleCount = 600;
    private const float LogInterval = 5f;

    private static PerfMeter s_instance;

    private readonly float[] _samples = new float[SampleCount];
    private readonly float[] _sorted = new float[SampleCount];
    private int _count;
    private int _head;
    private float _nextLog;
    private float _currentFps;

    // Tiempos de CPU y GPU por frame (FrameTimingManager, necesita "Frame Timing
    // Stats" prendido en Player Settings). Sirven para saber si el limite es la GPU
    // (lo mas probable con el post-procesado) o la CPU.
    private readonly FrameTiming[] _timings = new FrameTiming[1];
    private double _cpuSum;
    private double _gpuSum;
    private int _timingCount;

    /// <summary>FPS promedio de la ultima ventana de 5 s (0 hasta que haya una).</summary>
    public static float CurrentFps => s_instance != null ? s_instance._currentFps : 0f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (s_instance != null)
        {
            return;
        }
        GameObject go = new GameObject("PerfMeter (feria)");
        DontDestroyOnLoad(go);
        s_instance = go.AddComponent<PerfMeter>();
    }

    private void Awake()
    {
        if (s_instance != null && s_instance != this)
        {
            Destroy(gameObject);
            return;
        }
        s_instance = this;
        _nextLog = Time.realtimeSinceStartup + LogInterval;
    }

    private void OnDestroy()
    {
        if (s_instance == this)
        {
            s_instance = null;
        }
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (dt > 0f)
        {
            _samples[_head] = dt;
            _head = (_head + 1) % SampleCount;
            if (_count < SampleCount)
            {
                _count++;
            }
        }

        FrameTimingManager.CaptureFrameTimings();
        if (FrameTimingManager.GetLatestTimings(1, _timings) > 0 && _timings[0].gpuFrameTime > 0)
        {
            _cpuSum += _timings[0].cpuFrameTime;
            _gpuSum += _timings[0].gpuFrameTime;
            _timingCount++;
        }

        if (Time.realtimeSinceStartup < _nextLog || _count == 0)
        {
            return;
        }
        _nextLog = Time.realtimeSinceStartup + LogInterval;
        Report();
    }

    private void Report()
    {
        float sum = 0f;
        float worst = 0f;
        for (int i = 0; i < _count; i++)
        {
            float v = _samples[i];
            _sorted[i] = v;
            sum += v;
            if (v > worst)
            {
                worst = v;
            }
        }
        System.Array.Sort(_sorted, 0, _count);

        float avgFps = _count / sum;
        // 1% bajo: FPS promedio del 1% de frames mas lentos (minimo 1 frame).
        int n = Mathf.Max(1, _count / 100);
        float slowSum = 0f;
        for (int i = _count - n; i < _count; i++)
        {
            slowSum += _sorted[i];
        }
        float low1 = n / slowSum;

        _currentFps = avgFps;
        string timing = "";
        if (_timingCount > 0)
        {
            timing = "cpu=" + F1((float)(_cpuSum / _timingCount)) + " gpu=" + F1((float)(_gpuSum / _timingCount)) + " ";
        }
        _cpuSum = _gpuSum = 0;
        _timingCount = 0;
        // Formato: perf fpsProm fps1%bajo msPeorFrame [cpu=ms gpu=ms] escena
        GameTimer.Log("perf " + F1(avgFps) + " " + F1(low1) + " " + F1(worst * 1000f) + " " + timing + SceneManager.GetActiveScene().name);
    }

    private static string F1(float v)
    {
        return v.ToString("F1", CultureInfo.InvariantCulture);
    }
}
