using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Mecanica de secuencia (2/3, tras llave y puerta). Varios SequenceStep se
/// registran aca solos (OnEnable/OnDisable, igual que TeleportPoint con
/// TeleportManager) y avisan cuando el jugador los selecciona con la
/// mirada. Si se van seleccionando en el orden correcto (Step Index 0, 1,
/// 2...) la secuencia avanza; al llegar al ultimo paso dispara
/// _onSequenceComplete. Si se selecciona un paso fuera de orden, TODA la
/// secuencia se reinicia (visualmente tambien, via ResetVisual en cada
/// paso) y dispara _onSequenceFailed.
///
/// _onSequenceComplete y _onSequenceFailed son UnityEvent: conectalos desde
/// el Inspector a lo que necesites (abrir algo, activar un cartel, llamar
/// GameManager.TriggerVictory(), etc.) sin escribir mas scripts.
/// </summary>
public class SequenceManager : MonoBehaviour
{
    [Tooltip("Cantidad total de pasos de la secuencia (tiene que coincidir con cuantos SequenceStep apuntan a este manager).")]
    [SerializeField] private int _stepCount = 4;

    [Header("Eventos")]
    [SerializeField] private UnityEvent _onSequenceComplete;
    [SerializeField] private UnityEvent _onSequenceFailed;

    public bool IsComplete { get; private set; }

    private readonly List<SequenceStep> _registeredSteps = new List<SequenceStep>();
    private int _nextExpectedStep;

    private void OnEnable()
    {
        // GameManager activa/desactiva GameplayRoot (padre de este objeto)
        // en cada cambio de estado. O sea que esto se ejecuta solo cada vez
        // que arranca una partida nueva. Sin esto, despues de ganar una vez
        // IsComplete se quedaba en true para siempre y ReportStep ignoraba
        // todo lo que pasara despues (ni victoria ni derrota volvian a
        // dispararse). No llamamos ResetSequence() aca porque esa recorre
        // _registeredSteps, y el orden en que Unity activa este objeto y
        // sus SequenceStep hijos no esta garantizado - podrian no estar
        // registrados todavia en este instante. Por eso cada SequenceStep
        // resetea su propio estado visual en su propio OnEnable.
        _nextExpectedStep = 0;
        IsComplete = false;
    }

    public void RegisterStep(SequenceStep step)
    {
        if (!_registeredSteps.Contains(step))
        {
            _registeredSteps.Add(step);
        }
    }

    public void UnregisterStep(SequenceStep step)
    {
        _registeredSteps.Remove(step);
    }

    /// <summary>
    /// Llamado por cada SequenceStep cuando el jugador lo selecciona.
    /// </summary>
    public void ReportStep(SequenceStep step, int stepIndex)
    {
        if (IsComplete)
        {
            return;
        }

        if (stepIndex == _nextExpectedStep)
        {
            step.MarkActivated();
            _nextExpectedStep++;

            if (_nextExpectedStep >= _stepCount)
            {
                IsComplete = true;
                Debug.Log("[SequenceManager] Secuencia completa.");
                _onSequenceComplete?.Invoke();
            }
        }
        else
        {
            Debug.Log($"[SequenceManager] Paso incorrecto (se esperaba {_nextExpectedStep}, llego {stepIndex}). Reiniciando secuencia.");
            ResetSequence();
            _onSequenceFailed?.Invoke();
        }
    }

    /// <summary>
    /// Reinicia el progreso y el material de todos los pasos registrados.
    /// Se puede llamar tambien manualmente (por ejemplo, desde otro
    /// UnityEvent) si en algun momento queres resetear el puzzle a mano.
    /// </summary>
    public void ResetSequence()
    {
        _nextExpectedStep = 0;
        IsComplete = false;

        foreach (SequenceStep step in _registeredSteps)
        {
            step.ResetVisual();
        }
    }
}
