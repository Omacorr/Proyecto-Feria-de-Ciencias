using UnityEngine;

/// <summary>
/// Polvo flotando en el haz de una linterna. Va en un GameObject hijo de la
/// Spot Light con un ParticleSystem: mientras la luz esta prendida deja nacer
/// particulas; si se apaga (o la linterna se guarda) dejan de nacer y las que
/// quedan se disuelven solas.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class FlashlightDust : MonoBehaviour
{
    [Tooltip("Luz de la linterna. Vacio = busca en los padres.")]
    [SerializeField] private Light _light;

    [Tooltip("Intensidad minima para considerar la luz prendida.")]
    [SerializeField] private float _minIntensity = 0.05f;

    private ParticleSystem _particles;

    private void Awake()
    {
        _particles = GetComponent<ParticleSystem>();
        if (_light == null)
        {
            _light = GetComponentInParent<Light>();
        }
    }

    private void Update()
    {
        bool lit = _light != null
            && _light.enabled
            && _light.gameObject.activeInHierarchy
            && _light.intensity > _minIntensity;

        ParticleSystem.EmissionModule emission = _particles.emission;
        if (emission.enabled != lit)
        {
            emission.enabled = lit;
        }
    }
}
