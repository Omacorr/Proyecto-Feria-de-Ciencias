using UnityEngine;

/// <summary>
/// Hace parpadear una Light para dar clima de terror: varia la intensidad con
/// ruido Perlin (parpadeo suave, tipo tubo fluorescente gastado) y cada tanto
/// mete un apagon corto aleatorio. No hace nada mas.
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

    private Light _light;
    private float _noiseSeed;
    private float _blackoutUntil;

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
