using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Maneja el "weight" de un Volume con transiciones suaves. Pensado para un
/// Volume global aparte (perfil propio de la escena, NO el compartido) que se
/// va sumando de a poco: por ejemplo el pasillo que se "cierra" con cada
/// vuelta del loop. Todos los metodos publicos se pueden llamar desde un
/// UnityEvent. Costo despreciable: solo escribe Volume.weight.
/// </summary>
public class VolumeWeightFade : MonoBehaviour
{
    [Tooltip("Volume a manejar. Si queda vacio toma el del mismo GameObject.")]
    [SerializeField] private Volume _volume;

    [Range(0f, 1f)]
    [Tooltip("Weight con el que arranca la escena.")]
    [SerializeField] private float _startWeight = 0f;

    [Tooltip("Segundos que tarda cada transicion.")]
    [SerializeField] private float _fadeSeconds = 2f;

    [Range(0f, 1f)]
    [Tooltip("Cuanto suma StepUp() al objetivo.")]
    [SerializeField] private float _stepSize = 0.2f;

    [Tooltip("Usar tiempo real (ignora Time.timeScale).")]
    [SerializeField] private bool _useUnscaledTime = false;

    private float _target;
    private float _speed;

    /// <summary>Weight objetivo actual.</summary>
    public float TargetWeight { get { return _target; } }

    private void Reset()
    {
        _volume = GetComponent<Volume>();
    }

    private void Awake()
    {
        if (_volume == null)
        {
            _volume = GetComponent<Volume>();
        }
        _target = Mathf.Clamp01(_startWeight);
        _speed = 0f;
        if (_volume != null)
        {
            _volume.weight = _target;
        }
    }

    /// <summary>Lleva el weight a 1.</summary>
    public void FadeIn() { FadeTo(1f); }

    /// <summary>Lleva el weight a 0.</summary>
    public void FadeOut() { FadeTo(0f); }

    /// <summary>Lleva el weight a "weight" (0..1) en Fade Seconds.</summary>
    public void FadeTo(float weight)
    {
        _target = Mathf.Clamp01(weight);
        if (_volume == null)
        {
            return;
        }
        float distance = Mathf.Abs(_target - _volume.weight);
        _speed = _fadeSeconds > 0f ? distance / _fadeSeconds : float.PositiveInfinity;
    }

    /// <summary>Suma Step Size al objetivo (tope 1) y va hacia ahi.</summary>
    public void StepUp() { FadeTo(_target + _stepSize); }

    /// <summary>Cambia la duracion de las proximas transiciones.</summary>
    public void SetFadeSeconds(float seconds) { _fadeSeconds = Mathf.Max(0f, seconds); }

    /// <summary>Fija el weight al instante, sin transicion.</summary>
    public void SetInstant(float weight)
    {
        _target = Mathf.Clamp01(weight);
        _speed = 0f;
        if (_volume != null)
        {
            _volume.weight = _target;
        }
    }

    private void Update()
    {
        if (_volume == null || Mathf.Approximately(_volume.weight, _target))
        {
            return;
        }

        float dt = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        float current = _volume.weight;
        float remaining = Mathf.Abs(_target - current);

        // Ease: la velocidad baja cerca del objetivo (minimo 25 % para que llegue).
        float ease = Mathf.Lerp(0.25f, 1.5f, Mathf.Clamp01(remaining * 2f));
        _volume.weight = Mathf.MoveTowards(current, _target, _speed * ease * dt);
    }
}
