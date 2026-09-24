using UnityEngine;

/// <summary>
/// Hace parpadear una Light para dar clima de terror: varia la intensidad con
/// ruido Perlin (parpadeo suave, tipo tubo fluorescente gastado) y cada tanto
/// mete un apagon corto aleatorio. Para momentos de guion: Burst(segundos)
/// (parpadeo violento, lo usa FaintTransition), BurstThenOff(segundos),
/// TurnOff() y TurnOn(), todos llamables desde un UnityEvent.
///
/// Para Backrooms (modelo UNLIT, las Light no le hacen nada) usar
/// FluorescentFlicker, que ademas hace parpadear materiales.
///
/// El menu en si usa materiales Unlit, asi que se lee bien aunque esta luz este
/// casi apagada; el parpadeo es puramente ambiental sobre la habitacion.
/// </summary>
[RequireComponent(typeof(Light))]
public class FlickeringLight : MonoBehaviour
{
    [Tooltip("Intensidad base. El parpadeo va desde (base * (1 - Flicker Amount)) hasta base. Si se deja en 0, toma la intensidad que ya tenga la Light.")]
    [SerializeField] private float _baseIntensity = 1.5f;

    [Range(0f, 1f)]
    [Tooltip("Cuanto baja la intensidad en el parpadeo suave. 0 = fija, 1 = llega hasta apagarse.")]
    [SerializeField] private float _flickerAmount = 0.35f;

    [Tooltip("Velocidad del parpadeo suave.")]
    [SerializeField] private float _flickerSpeed = 8f;

    [Tooltip("Probabilidad por segundo de que arranque un apagon corto.")]
    [SerializeField] private float _blackoutChancePerSecond = 0.25f;

    [Tooltip("Duracion maxima de cada apagon, en segundos.")]
    [SerializeField] private float _blackoutMaxDuration = 0.08f;

    [Tooltip("Durante Burst(): segundos minimos que dura cada estado prendido/apagado (cada uno dura entre este valor y el doble). Asi el parpadeo violento no depende de los FPS. 0 = cambia en cada frame (comportamiento viejo, ~15-30 destellos por segundo). Para bajar el riesgo de fotosensibilidad (menos de 3 destellos/seg) usar 0.17 o mas.")]
    [SerializeField] private float _burstStepSeconds = 0.05f;

    private Light _light;
    private float _noiseSeed;
    private float _blackoutUntil;
    private float _burstUntil;
    private float _burstNextStepAt;
    private float _burstIntensity;
    private bool _forcedOff;
    private bool _offAfterBurst;

    /// <summary>
    /// Parpadeo violento forzado (prende/apaga rapido y al azar) durante
    /// "seconds" segundos. Pensado para momentos de guion, por ejemplo justo
    /// antes de un desmayo (FaintTransition lo llama). Tambien se puede
    /// cablear desde cualquier UnityEvent.
    /// </summary>
    public void Burst(float seconds)
    {
        _burstUntil = Time.time + Mathf.Max(0f, seconds);
        _burstNextStepAt = 0f; // el primer cambio es inmediato
    }

    /// <summary>
    /// Parpadeo violento y despues la luz queda MUERTA (apagada) hasta que se
    /// llame TurnOn(). Para "la luz parpadea y se apaga" (monstruo cerca, final).
    /// </summary>
    public void BurstThenOff(float seconds)
    {
        Burst(seconds);
        _offAfterBurst = true;
    }

    /// <summary>Apaga la luz y la deja apagada (sin parpadeo) hasta TurnOn().</summary>
    public void TurnOff()
    {
        _forcedOff = true;
        _offAfterBurst = false;
        _burstUntil = 0f;
        if (_light != null)
        {
            _light.intensity = 0f;
        }
    }

    /// <summary>Vuelve al parpadeo normal despues de TurnOff()/BurstThenOff().</summary>
    public void TurnOn()
    {
        _forcedOff = false;
        _offAfterBurst = false;
    }

    private void Awake()
    {
        _light = GetComponent<Light>();

        if (_baseIntensity <= 0f)
        {
            _baseIntensity = _light.intensity;
        }

        _noiseSeed = Random.value * 100f;
    }

    private void Update()
    {
        // Parpadeo forzado (Burst) en curso: gana sobre todo lo demas.
        if (Time.time < _burstUntil)
        {
            if (_burstStepSeconds <= 0f || Time.time >= _burstNextStepAt)
            {
                _burstIntensity = Random.value < 0.45f ? 0f : _baseIntensity * Random.Range(0.6f, 1.6f);
                _burstNextStepAt = Time.time + Random.Range(_burstStepSeconds, _burstStepSeconds * 2f);
            }
            _light.intensity = _burstIntensity;
            return;
        }

        // Termino un BurstThenOff(): la luz queda muerta.
        if (_offAfterBurst)
        {
            _offAfterBurst = false;
            _forcedOff = true;
        }

        // Apagada a proposito (TurnOff / BurstThenOff).
        if (_forcedOff)
        {
            _light.intensity = 0f;
            return;
        }

        // Apagon en curso.
        if (Time.time < _blackoutUntil)
        {
            _light.intensity = 0f;
            return;
        }

        // Arranca un apagon nuevo (probabilidad escalada por deltaTime).
        if (Random.value < _blackoutChancePerSecond * Time.deltaTime)
        {
            _blackoutUntil = Time.time + Random.Range(0.02f, _blackoutMaxDuration);
            _light.intensity = 0f;
            return;
        }

        // Parpadeo suave.
        float noise = Mathf.PerlinNoise(_noiseSeed, Time.time * _flickerSpeed); // 0..1
        float factor = Mathf.Lerp(1f - _flickerAmount, 1f, noise);
        _light.intensity = _baseIntensity * factor;
    }
}
