using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Sistema de pistas por escena (issue #31, modo feria). Va UNO por escena,
/// en un objeto vacio tipo "Pistas".
///
/// Tiene una lista de objetivos en el orden del nivel (por ejemplo, en la
/// Parte 1: "agarrar la linterna", "ver las notas", "abrir el candado"). Si
/// el jugador se queda trabado en el objetivo actual, las pistas escalan en
/// tres escalones, medidos desde que empezo el objetivo (o desde el ultimo
/// Progress()):
///   1. Ambiente (30 s): algo sutil del mundo (un sonido, una luz que parpadea).
///   2. Voz (60 s): el pensamiento del protagonista, con VRHintText.
///   3. Directa (90 s): lo obvio (HintGlow sobre el objeto, una flecha...).
/// Los tiempos se multiplican por GameSettings.HintTimeScale (0.6 con modo
/// feria: en la feria cada visitante tiene pocos minutos).
/// IMPORTANTE: por defecto el reloj esta APAGADO (HintDirector.AutoHints =
/// false): las pistas solo salen cuando el operador las pide, un escalon por
/// pedido.
///
/// Por que escalones y no un cartel fijo: el guion es de terror, y un cartel
/// desde el arranque rompe el clima. Asi el que explora no ve ninguna pista,
/// y el que se traba no se va frustrado de la feria.
///
/// El reloj NO avanza durante un desmayo (FaintOverlay.IsRunning) ni con la
/// mirada bloqueada (GazeController.IsBlocked: despertar del piso,
/// persecuciones, screamer...). Usa tiempo escalado, asi se puede probar con
/// Time.timeScale alto.
///
/// Cableado tipico: el evento que completa cada paso del nivel (On Collected,
/// On Unlocked, On Player Arrived...) llama a NextObjective(); un avance
/// parcial (por ejemplo un fragmento de tres) llama a Progress(). El operador
/// pide una pista ya con FeriaEvents.RequestHint() -> GiveDirectHint().
/// </summary>
public class HintDirector : MonoBehaviour
{
    /// <summary>Un paso del nivel con sus tres escalones de pista.</summary>
    [Serializable]
    public class Objective
    {
        [Tooltip("Nombre corto para el Inspector y los logs (por ejemplo 'Agarrar la linterna').")]
        public string label = "Objetivo";

        [Tooltip("Lo que 'piensa' el protagonista en el escalon de voz. Se muestra con el VRHintText de la escena.")]
        [TextArea(2, 4)]
        public string voiceText = "";

        [Tooltip("Pista suave (nivel 1 del operador, y escalon 1 si no esta vacia): una pista sutil, sin decir el objeto. Si esta vacia se usa Voice Text.")]
        [TextArea(2, 4)]
        public string voiceTextSoft = "";

        [Tooltip("Pista directa (nivel 3 del operador, y escalon 3 si no esta vacia): dice que hacer. Si esta vacia se usa Voice Text.")]
        [TextArea(2, 4)]
        public string voiceTextDirect = "";

        [Tooltip("Escalon 1 (ambiente): algo sutil del mundo. Por ejemplo FlickeringLight.Burst o un AudioSource.Play cerca del objetivo.")]
        public UnityEvent onAmbient = new UnityEvent();

        [Tooltip("Escalon 2 (voz): se suma a mostrar el Voice Text (por ejemplo un susurro).")]
        public UnityEvent onVoice = new UnityEvent();

        [Tooltip("Escalon 3 (directa): lo obvio. Por ejemplo HintGlow.On sobre el objeto a usar.")]
        public UnityEvent onDirect = new UnityEvent();

        [Tooltip("Se dispara al dejar este objetivo (NextObjective/SetObjective). Para apagar lo que prendio la pista, por ejemplo HintGlow.Off.")]
        public UnityEvent onCleared = new UnityEvent();
    }

    /// <summary>El HintDirector de la escena activa (null si no hay).</summary>
    public static HintDirector Current { get; private set; }

    private const string AutoHintsKey = "cfg.autoHints";

    /// <summary>
    /// Pistas automaticas por tiempo (30/60/90 s x HintTimeScale). APAGADO por
    /// defecto: las pistas solo las da el operador. Se guarda en PlayerPrefs
    /// (clave cfg.autoHints, default 0).
    /// </summary>
    public static bool AutoHints
    {
        get => PlayerPrefs.GetInt(AutoHintsKey, 0) != 0;
        set => PlayerPrefs.SetInt(AutoHintsKey, value ? 1 : 0);
    }

    [Header("Objetivos (en orden)")]
    [SerializeField] private List<Objective> _objectives = new List<Objective>();

    [Tooltip("Objetivo con el que arranca (0 = el primero).")]
    [SerializeField] private int _startObjective;

    [Tooltip("Arrancar el reloj solo al empezar la escena. Destildalo si el nivel arranca con una cinematica y preferis llamar SetObjective a mano.")]
    [SerializeField] private bool _autoStart = true;

    [Header("Tiempos base (segundos desde que empezo el objetivo)")]
    [Tooltip("Escalon 1, ambiente. Se multiplica por GameSettings.HintTimeScale.")]
    [SerializeField] private float _ambientSeconds = 30f;
    [Tooltip("Escalon 2, voz. Se multiplica por GameSettings.HintTimeScale.")]
    [SerializeField] private float _voiceSeconds = 60f;
    [Tooltip("Escalon 3, directa. Se multiplica por GameSettings.HintTimeScale.")]
    [SerializeField] private float _directSeconds = 90f;

    [Header("Texto en VR")]
    [Tooltip("VRHintText de la escena (Main Camera/Hint (VR)). Muestra el Voice Text del escalon 2.")]
    [SerializeField] private VRHintText _hint;

    private int _index = -1;
    private float _elapsed;
    private int _stepsGiven; // 0 = ninguno, 3 = ya se dio la directa
    private bool _running;

    private void OnEnable()
    {
        Current = this;
        FeriaEvents.HintRequested += GiveDirectHint;
    }

    private void OnDisable()
    {
        FeriaEvents.HintRequested -= GiveDirectHint;
        if (Current == this)
        {
            Current = null;
        }
    }

    private void Start()
    {
        if (_autoStart)
        {
            StartObjective(_startObjective);
        }
    }

    private void Update()
    {
        // Sin pistas automaticas: solo las da el operador (GiveDirectHint).
        if (!AutoHints || !_running || _stepsGiven >= 3 || !HasObjective(_index))
        {
            return;
        }

        // Pausas: desmayo en curso o mirada bloqueada (cinematicas).
        if (FaintOverlay.IsRunning || GazeController.IsBlocked)
        {
            return;
        }

        _elapsed += Time.deltaTime;

        float scale = GameSettings.Instance != null ? GameSettings.Instance.HintTimeScale : 1f;
        if (_elapsed >= ThresholdFor(_stepsGiven + 1) * scale)
        {
            GiveStep(_stepsGiven + 1);
        }
    }

    /// <summary>
    /// El jugador avanzo dentro del objetivo actual: reinicia el reloj (y los
    /// escalones vuelven a empezar desde el ambiente). Lo que haya prendido
    /// una pista directa sigue prendido hasta onCleared.
    /// </summary>
    public void Progress()
    {
        _elapsed = 0f;
        _stepsGiven = 0;
    }

    /// <summary>
    /// El objetivo actual se cumplio: dispara su onCleared, pasa al siguiente
    /// y reinicia el reloj. Si era el ultimo, el director se detiene.
    /// </summary>
    public void NextObjective()
    {
        ClearCurrent();
        int next = _index + 1;
        if (!HasObjective(next))
        {
            Debug.Log("[HintDirector] No quedan objetivos: pistas detenidas.");
            _running = false;
            _index = _objectives.Count;
            return;
        }
        StartObjective(next);
    }

    /// <summary>
    /// Salta a un objetivo por indice (dispara el onCleared del actual si
    /// cambia) y reinicia el reloj.
    /// </summary>
    public void SetObjective(int index)
    {
        if (!HasObjective(index))
        {
            Debug.LogWarning($"[HintDirector] SetObjective({index}): indice fuera de rango.");
            return;
        }
        if (index != _index)
        {
            ClearCurrent();
        }
        StartObjective(index);
    }

    /// <summary>
    /// Pista pedida por el operador (FeriaEvents.RequestHint, o un boton).
    /// Escala UN escalon por pedido en el objetivo actual: 1ª vez ambiente,
    /// 2ª voz (+ Voice Text), 3ª directa. A partir de la 4ª repite la directa
    /// (vuelve a mostrar el texto y a disparar onDirect). Al cambiar de
    /// objetivo (o con Progress) el conteo vuelve a cero.
    /// </summary>
    public void GiveDirectHint()
    {
        if (!HasObjective(_index))
        {
            Debug.Log("[HintDirector] Pista pedida, pero no hay objetivo actual.");
            return;
        }

        if (_stepsGiven >= 3)
        {
            Objective o = _objectives[_index];
            ShowText(FirstNonEmpty(o.voiceTextDirect, o.voiceText));
            Fire(3);
            return;
        }
        GiveStep(_stepsGiven + 1);
    }

    /// <summary>
    /// Pista de un nivel fijo pedida por el operador (1 suave, 2 media, 3
    /// directa). Nivel 1: ambiente + voiceTextSoft. Nivel 2: ambiente + voz +
    /// voiceText. Nivel 3: ambiente + voz + directa + voiceTextDirect. Los
    /// textos vacios caen en voiceText. Actualiza el conteo de escalones.
    /// </summary>
    public void GiveHintLevel(int level)
    {
        if (!HasObjective(_index))
        {
            Debug.Log("[HintDirector] Pista de nivel pedida, pero no hay objetivo actual.");
            return;
        }

        level = Mathf.Clamp(level, 1, 3);
        Objective o = _objectives[_index];

        o.onAmbient?.Invoke();
        if (level >= 2)
        {
            o.onVoice?.Invoke();
        }
        if (level == 3)
        {
            o.onDirect?.Invoke();
        }

        if (level == 1)
        {
            ShowText(FirstNonEmpty(o.voiceTextSoft, o.voiceText));
        }
        else if (level == 2)
        {
            ShowText(o.voiceText);
        }
        else
        {
            ShowText(FirstNonEmpty(o.voiceTextDirect, o.voiceText));
        }

        _stepsGiven = Mathf.Max(_stepsGiven, level);
        string scene = SceneManager.GetActiveScene().name;
        Debug.Log($"[HintDirector] {scene} / objetivo {_index} ({o.label}): nivel {level} (operador)");
        FeriaEvents.ReportHint(scene, level);
    }

    /// <summary>Detiene el reloj (no dispara onCleared).</summary>
    public void Stop()
    {
        _running = false;
    }

    /// <summary>Nombre del objetivo actual ("" si no hay).</summary>
    public string CurrentObjectiveName => HasObjective(_index) ? _objectives[_index].label : string.Empty;

    /// <summary>Escalon de pista ya dado para el objetivo actual (0 = ninguno, 3 = la directa).</summary>
    public int CurrentStep => _stepsGiven;

    // ------------------------------------------------------------------

    private void StartObjective(int index)
    {
        if (!HasObjective(index))
        {
            _running = false;
            return;
        }
        _index = index;
        Progress();
        _running = true;
        Debug.Log($"[HintDirector] Objetivo {index}: {_objectives[index].label}");
    }

    private void ClearCurrent()
    {
        if (HasObjective(_index))
        {
            _objectives[_index].onCleared?.Invoke();
        }
    }


    // Da el escalon n (1..3) y deja anotado que ya se dio.
    private void GiveStep(int step)
    {
        Objective o = _objectives[_index];
        if (step == 1)
        {
            // Solo si hay texto suave: sin el, el escalon 1 es solo ambiente.
            ShowText(o.voiceTextSoft);
        }
        else if (step == 2)
        {
            ShowVoice(o);
        }
        else if (step == 3)
        {
            ShowText(o.voiceTextDirect);
        }
        Fire(step);
        _stepsGiven = Mathf.Max(_stepsGiven, step);
    }

    private void Fire(int step)
    {
        Objective o = _objectives[_index];
        switch (step)
        {
            case 1: o.onAmbient?.Invoke(); break;
            case 2: o.onVoice?.Invoke(); break;
            case 3: o.onDirect?.Invoke(); break;
        }

        string scene = SceneManager.GetActiveScene().name;
        Debug.Log($"[HintDirector] {scene} / objetivo {_index} ({o.label}): escalon {step}");
        FeriaEvents.ReportHint(scene, step);
    }

    private void ShowVoice(Objective o)
    {
        ShowText(o.voiceText);
    }

    // Muestra un texto en VR (VRHintText de la escena) si hay texto y hay visor.
    private void ShowText(string text)
    {
        if (_hint != null && !string.IsNullOrEmpty(text))
        {
            _hint.Show(text);
        }
    }

    private static string FirstNonEmpty(string a, string b)
    {
        return !string.IsNullOrEmpty(a) ? a : b;
    }

    // Los escalones nunca se adelantan al anterior aunque se configuren al reves.
    private float ThresholdFor(int step)
    {
        float voice = Mathf.Max(_ambientSeconds, _voiceSeconds);
        return step == 1 ? _ambientSeconds : step == 2 ? voice : Mathf.Max(voice, _directSeconds);
    }

    private bool HasObjective(int index)
    {
        return index >= 0 && index < _objectives.Count && _objectives[index] != null;
    }
}
