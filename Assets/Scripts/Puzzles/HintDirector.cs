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
        if (!_running || _stepsGiven >= 3 || !HasObjective(_index))
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
    /// Pista directa YA (la pide el operador con FeriaEvents.RequestHint, o
    /// un boton). Da la voz (si faltaba) y la directa del objetivo actual. Si
    /// la directa ya se habia dado, la repite (vuelve a mostrar el texto y a
    /// disparar onDirect).
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
            ShowVoice(_objectives[_index]);
            Fire(3);
            return;
        }
        if (_stepsGiven < 2)
        {
            GiveStep(2);
        }
        GiveStep(3);
    }

    /// <summary>Detiene el reloj (no dispara onCleared).</summary>
    public void Stop()
    {
        _running = false;
    }

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
        if (step == 2)
        {
            ShowVoice(_objectives[_index]);
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
        if (_hint != null && !string.IsNullOrEmpty(o.voiceText))
        {
            _hint.Show(o.voiceText);
        }
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
