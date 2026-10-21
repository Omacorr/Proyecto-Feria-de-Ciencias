using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Una lista de pasos con demora, armable desde el Inspector: cada paso espera
/// sus segundos (contados desde el paso anterior) y dispara su evento. Sirve
/// para coreografiar momentos del guion sin escribir un script por cada uno,
/// por ejemplo el final de Parte 5: se cae la mascara -> parpadeo rapido ->
/// pitido de monitor -> desmayo al Menu.
///
/// Play() la arranca (una sola vez salvo Allow Restart). Play On Start la
/// arranca sola al cargar la escena. Stop() la corta (tambien si se llama
/// desde un paso de la propia secuencia: no corre ningun paso mas).
///
/// Detalles de robustez:
///  - Las demoras usan tiempo escalado (Time.timeScale), igual que el resto
///    de las animaciones del juego.
///  - Un paso cuyo evento tira una excepcion se loguea y la secuencia sigue
///    (si no, un error en un paso cortaba todos los siguientes, por ejemplo el
///    desmayo final).
///  - Apagar el objeto o el componente corta la secuencia (no se reanuda).
/// </summary>
public class TimedSequence : MonoBehaviour
{
    [Serializable]
    public class Step
    {
        [Tooltip("Nombre solo para ubicarse en el Inspector.")]
        public string label;
        [Tooltip("Segundos de espera desde el paso anterior (o desde Play() si es el primero).")]
        public float delay;
        public UnityEvent action;
    }

    [SerializeField] private bool _playOnStart;
    [SerializeField] private bool _allowRestart;
    [SerializeField] private Step[] _steps = new Step[0];

    private Coroutine _running;
    private bool _isRunning;
    private bool _played;
    // Cambia en cada Play()/Stop(): una corrida vieja que ve otro numero se corta.
    private int _runId;

    /// <summary>true mientras la secuencia esta corriendo.</summary>
    public bool IsRunning => _isRunning;

    private void Start()
    {
        if (_playOnStart)
        {
            Play();
        }
    }

    public void Play()
    {
        if (_isRunning || (_played && !_allowRestart))
        {
            return;
        }
        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[TimedSequence] " + name + ": Play() con el objeto apagado, no se puede arrancar.");
            return;
        }
        _played = true;
        _isRunning = true;
        int id = ++_runId;
        // Los pasos con demora 0 corren adentro de StartCoroutine: si la
        // secuencia ya termino (o se freno) ahi mismo, no se guarda el handle.
        Coroutine c = StartCoroutine(Run(id));
        if (_isRunning && id == _runId)
        {
            _running = c;
        }
    }

    public void Stop()
    {
        _runId++;
        _isRunning = false;
        if (_running != null)
        {
            StopCoroutine(_running);
            _running = null;
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    private IEnumerator Run(int id)
    {
        foreach (Step step in _steps)
        {
            if (step == null)
            {
                continue;
            }
            if (step.delay > 0f)
            {
                yield return new WaitForSeconds(step.delay);
            }
            if (id != _runId)
            {
                yield break;
            }
            Debug.Log("[TimedSequence] " + name + ": " + (string.IsNullOrEmpty(step.label) ? "(paso)" : step.label));
            try
            {
                step.action?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
            if (id != _runId)
            {
                yield break; // un paso llamo Stop() (o Play() de nuevo)
            }
        }
        _isRunning = false;
        _running = null;
    }
}
