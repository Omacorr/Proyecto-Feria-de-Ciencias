using System.Collections;
using UnityEngine;

/// <summary>
/// Un solo cilindro de letras de un CodeLock (por ejemplo, cada rueda del
/// candado de A a F). Al mirarlo y mantener la mirada, gira al siguiente
/// simbolo (en loop) y le avisa al CodeLock para que revise si la
/// combinacion completa ya es correcta. Este script no sabe cual es la
/// combinacion correcta - esa logica vive entera en CodeLock. Aca solo se
/// muestra un simbolo a la vez y se gira.
/// </summary>
public class CodeDigit : MonoBehaviour, IGazeInteractable
{
    [Tooltip("El CodeLock dueno de este cilindro. Le avisa cada vez que cambia de simbolo.")]
    [SerializeField] private CodeLock _codeLock;

    [Header("Simbolos")]
    [Tooltip("Los simbolos posibles, en el orden en que aparecen al girar (por ejemplo 'ABCDEF'). El primero es el que arranca mostrando.")]
    [SerializeField] private string _symbols = "ABCDEF";

    [Header("Rotacion")]
    [Tooltip("Eje LOCAL sobre el que gira el cilindro. Probalo en el editor: si gira 'acostado' en vez de sobre su propio eje, es que este vector esta mal - cambialo (por ejemplo a Vector3.up o Vector3.forward) hasta que gire como corresponde.")]
    [SerializeField] private Vector3 _rotationAxis = Vector3.right;

    [Tooltip("Cuantos grados gira por cada simbolo. Normalmente 360 dividido la cantidad de simbolos visibles en el modelo (6 simbolos = 60).")]
    [SerializeField] private float _degreesPerStep = 60f;

    [Tooltip("Cuantos segundos tarda en girar de un simbolo al siguiente. Dejalo en 0 para que sea instantaneo.")]
    [SerializeField] private float _rotationDuration = 0.15f;

    [Header("Feedback de mirada (opcional)")]
    [SerializeField] private Material _inactiveMaterial;
    [SerializeField] private Material _gazedAtMaterial;

    [Header("Indicador sutil al mirar")]
    [Tooltip("Al mirar el disco: brilla apenas y hace un amague de giro, para que se entienda que el codigo se ingresa girando cada disco con la mirada.")]
    [SerializeField] private bool _gazeHint = true;
    [Tooltip("Brillo (emision) del disco mientras se lo mira. Bajo a proposito: tiene que notarse, no encandilar.")]
    [SerializeField] private Color _hintEmission = new Color(0.30f, 0.20f, 0.08f);
    [Tooltip("Grados del amague de giro al empezar a mirarlo (en el mismo sentido en que gira).")]
    [SerializeField] private float _hintNudgeDegrees = 9f;
    [Tooltip("Duracion del amague (ida y vuelta), en segundos.")]
    [SerializeField] private float _hintNudgeSeconds = 0.45f;

    [Header("Giro sostenido (issue #32: candado mas rapido)")]
    [Tooltip("Si esta tildado, mientras se sigue mirando el disco avanza solo un numero cada 'Hold Step Seconds', sin esperar a que el aro de la mira se vuelva a llenar.")]
    [SerializeField] private bool _holdToSpin = true;
    [Tooltip("Si esta tildado, el PRIMER paso de cada mirada lo da la seleccion normal (aro lleno). Recien despues gira solo. Evita que un disco gire por pasarle la mirada por encima.")]
    [SerializeField] private bool _waitForFirstSelect = true;
    [Tooltip("Segundos entre paso y paso mientras se sigue mirando el disco.")]
    [SerializeField] private float _holdStepSeconds = 0.6f;
    [Tooltip("Pausa EXTRA al caer en el numero correcto de este disco, para que se alcance a soltar la mirada ahi.")]
    [SerializeField] private float _pauseOnCorrectSeconds = 0.8f;
    [Tooltip("Una seleccion de GazeController que llega menos de estos segundos despues de un paso se ignora (si no, el giro sostenido y la seleccion normal darian dos pasos juntos).")]
    [SerializeField] private float _selectDebounceSeconds = 0.3f;

    /// <summary>Simbolo que esta mostrando este cilindro ahora mismo.</summary>
    public char CurrentSymbol => !string.IsNullOrEmpty(_symbols) ? _symbols[_currentIndex] : '\0';

    private static readonly int EmissiveFactorId = Shader.PropertyToID("emissiveFactor");

    private Renderer _renderer;
    private Renderer[] _hintRenderers;
    private MaterialPropertyBlock _mpb;
    private Quaternion _baseLocalRotation;
    private int _currentIndex;
    private bool _isRotating;
    private Coroutine _nudge;

    // Giro sostenido: tiempo acumulado mirando desde el ultimo paso, cuanto
    // hay que esperar para el proximo (mas largo si cayo en el correcto),
    // cuantos pasos van en esta mirada y cuando fue el ultimo paso.
    private bool _gazed;
    private float _holdTimer;
    private float _nextStepDelay;
    private int _stepsThisGaze;
    private float _lastStepTime = float.NegativeInfinity;
    private int _lockIndex = -2; // -2 = todavia no se pregunto al CodeLock

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        // El mesh del disco suele ser un hijo (BaseLP.001 > BaseLP.001_PadLock_0).
        _hintRenderers = GetComponentsInChildren<Renderer>(true);
        _mpb = new MaterialPropertyBlock();
        // La rotacion con la que arranca es la del primer simbolo: todos los
        // giros se calculan desde aca, asi el amague nunca desalinea el numero.
        _baseLocalRotation = transform.localRotation;
        SetGazed(false);
    }

    private void OnEnable()
    {
        // Vuelve al primer simbolo cada vez que arranca una partida nueva
        // (este objeto vive bajo GameplayRoot, igual que SequenceStep).
        _currentIndex = 0;
        _isRotating = false;
        _nudge = null;
        transform.localRotation = _baseLocalRotation;
        ResetHold();
        _gazed = false;
        _lastStepTime = float.NegativeInfinity;
    }

    private void OnDisable()
    {
        SetHintGlow(0f);
    }

    public void OnGazeEnter()
    {
        _gazed = true;
        ResetHold();
        SetGazed(true);
        if (_gazeHint && !_isRotating && isActiveAndEnabled)
        {
            _nudge = StartCoroutine(Nudge());
        }
    }

    public void OnGazeStay(float progress)
    {
        // El progreso ya lo muestra GazeReticle; aca solo un pulso suave de brillo.
        if (_gazeHint)
        {
            SetHintGlow(0.75f + 0.25f * Mathf.Sin(Time.time * 5f));
        }

        // Giro sostenido: acumulador propio, independiente del timer de
        // GazeController (que no se toca). Con _waitForFirstSelect, arranca
        // recien despues del primer paso de esta mirada.
        if (!_holdToSpin || (_waitForFirstSelect && _stepsThisGaze == 0))
        {
            return;
        }

        _holdTimer += Time.deltaTime;
        if (_holdTimer >= _nextStepDelay && !_isRotating)
        {
            // Si estaba girando, el acumulador queda "lleno" y da el paso
            // apenas termina el giro (los pasos se ignoran mientras gira).
            TryStep();
        }
    }

    public void OnGazeExit()
    {
        _gazed = false;
        ResetHold();
        SetGazed(false);
        SetHintGlow(0f);
    }

    public void OnGazeSelect()
    {
        // Mientras el giro sostenido maneja el disco, las selecciones
        // periodicas de GazeController se ignoran: si no, caerian en fases
        // al azar respecto del acumulador y romperian la pausa del numero
        // correcto. Un select suelto (sin mirada en curso, o el primero de
        // una mirada) funciona como siempre.
        if (_holdToSpin && _gazed && _stepsThisGaze > 0)
        {
            return;
        }
        if (Time.time - _lastStepTime < _selectDebounceSeconds)
        {
            return;
        }

        TryStep();
    }

    /// <summary>
    /// Avanza un simbolo si no esta girando. Devuelve false si no pudo.
    /// </summary>
    private bool TryStep()
    {
        if (_isRotating || string.IsNullOrEmpty(_symbols))
        {
            return false;
        }

        if (_nudge != null)
        {
            StopCoroutine(_nudge);
            _nudge = null;
        }

        _currentIndex = (_currentIndex + 1) % _symbols.Length;
        StartCoroutine(RotateStep());

        _lastStepTime = Time.time;
        int steps = _stepsThisGaze + 1;
        ResetHold();
        _stepsThisGaze = steps;

        // Cayo en el numero correcto de este disco: pausa extra para que el
        // jugador alcance a sacar la mirada justo ahi.
        if (IsOnCorrectSymbol())
        {
            _nextStepDelay += Mathf.Max(0f, _pauseOnCorrectSeconds);
        }

        _codeLock?.ReportDigitChanged();
        return true;
    }

    private bool IsOnCorrectSymbol()
    {
        if (_codeLock == null)
        {
            return false;
        }
        if (_lockIndex == -2)
        {
            _lockIndex = _codeLock.GetDigitIndex(this);
        }
        if (_lockIndex < 0)
        {
            return false;
        }
        char target = _codeLock.GetTargetSymbol(_lockIndex);
        return target != '\0' && target == CurrentSymbol;
    }

    private void ResetHold()
    {
        _holdTimer = 0f;
        _stepsThisGaze = 0;
        _nextStepDelay = Mathf.Max(0.05f, _holdStepSeconds);
    }

    private Quaternion RotationForIndex(int index)
    {
        return _baseLocalRotation * Quaternion.AngleAxis(_degreesPerStep * index, _rotationAxis);
    }

    // Amague: gira unos grados hacia el proximo simbolo y vuelve, una sola vez
    // por mirada. Le dice al jugador "esto gira" sin agregar carteles.
    private IEnumerator Nudge()
    {
        Quaternion rest = RotationForIndex(_currentIndex);
        Quaternion peak = rest * Quaternion.AngleAxis(_hintNudgeDegrees, _rotationAxis);
        float duration = Mathf.Max(0.05f, _hintNudgeSeconds);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float k = Mathf.Sin(Mathf.Clamp01(elapsed / duration) * Mathf.PI);
            transform.localRotation = Quaternion.Slerp(rest, peak, k);
            yield return null;
        }
        transform.localRotation = rest;
        _nudge = null;
    }

    private void SetHintGlow(float amount)
    {
        if (_hintRenderers == null || _mpb == null)
        {
            return;
        }
        Color c = _hintEmission * Mathf.Max(0f, amount);
        c.a = 1f;
        foreach (Renderer r in _hintRenderers)
        {
            if (r == null)
            {
                continue;
            }
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(EmissiveFactorId, c);
            r.SetPropertyBlock(_mpb);
        }
    }

    private IEnumerator RotateStep()
    {
        _isRotating = true;

        Quaternion fromRot = transform.localRotation;
        Quaternion toRot = RotationForIndex(_currentIndex);
        float duration = Mathf.Max(0f, _rotationDuration);

        for (float elapsed = 0f; elapsed < duration;)
        {
            elapsed += Time.deltaTime;
            transform.localRotation = Quaternion.Slerp(fromRot, toRot, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        transform.localRotation = toRot;
        _isRotating = false;
    }

    private void SetGazed(bool gazedAt) => GazeHighlight.Swap(_renderer, _inactiveMaterial, _gazedAtMaterial, gazedAt);
}
