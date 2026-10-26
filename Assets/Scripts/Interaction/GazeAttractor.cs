using UnityEngine;

/// <summary>
/// Atrae la mirada hacia una pista si el jugador tarda demasiado: pasados
/// "Delay" segundos (desde Start o desde Arm()) sube de a poco la intensidad de
/// una Light, el brillo emisivo de un Renderer y el volumen de un AudioSource,
/// hasta llegar al maximo en "Ramp Seconds". Stop() corta todo y deja el
/// objeto como estaba (llamarlo cuando la pista ya se examino).
///
/// Generico: sirve para el reloj, la nota, el armario, etc. Todo opcional.
/// </summary>
public class GazeAttractor : MonoBehaviour
{
    [Tooltip("Segundos sin resolver antes de empezar a llamar la atencion.")]
    [SerializeField] private float _delay = 40f;

    [Tooltip("Segundos que tarda en llegar al maximo una vez empezado.")]
    [SerializeField] private float _rampSeconds = 30f;

    [Tooltip("Arranca la cuenta sola en Start. Si no, hay que llamar Arm().")]
    [SerializeField] private bool _armOnStart = true;

    [Header("Luz (opcional)")]
    [SerializeField] private Light _light;

    [Tooltip("Intensidad de la luz al inicio (se restaura con Stop).")]
    [SerializeField] private float _lightMin = 0f;

    [SerializeField] private float _lightMax = 1.5f;

    [Header("Brillo emisivo (opcional)")]
    [SerializeField] private Renderer _renderer;

    [ColorUsage(true, true)]
    [Tooltip("Emision maxima (HDR). Al inicio es negro.")]
    [SerializeField] private Color _emissionMax = new Color(0.5f, 0.4f, 0.15f);

    [Header("Sonido (opcional)")]
    [Tooltip("AudioSource (idealmente 3D, en loop) que se va haciendo mas audible. Si no esta sonando, se inicia al empezar.")]
    [SerializeField] private AudioSource _audio;

    [SerializeField] private float _volumeMax = 0.6f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissiveFactorId = Shader.PropertyToID("emissiveFactor");

    private MaterialPropertyBlock _block;
    private float _armedAt;
    private bool _armed;
    private bool _active;

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
    }

    private void Start()
    {
        ResetEffects();
        if (_armOnStart)
        {
            Arm();
        }
    }

    /// <summary>Reinicia la cuenta (por ejemplo al entrar a una zona nueva).</summary>
    public void Arm()
    {
        _armed = true;
        _active = false;
        _armedAt = Time.time;
        ResetEffects();
    }

    /// <summary>Corta la atraccion y restaura todo. Llamar cuando la pista ya se vio.</summary>
    public void Stop()
    {
        _armed = false;
        _active = false;
        ResetEffects();
    }

    private void Update()
    {
        if (!_armed)
        {
            return;
        }

        float elapsed = Time.time - _armedAt - _delay;
        if (elapsed < 0f)
        {
            return;
        }

        if (!_active)
        {
            _active = true;
            if (_audio != null)
            {
                _audio.volume = 0f;
                if (!_audio.isPlaying)
                {
                    _audio.Play();
                }
            }
        }

        float k = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, _rampSeconds));
        k = k * k * (3f - 2f * k); // suave
        Apply(k);
    }

    private void Apply(float k)
    {
        if (_light != null)
        {
            _light.intensity = Mathf.Lerp(_lightMin, _lightMax, k);
        }

        if (_renderer != null)
        {
            SetEmission(Color.Lerp(Color.black, _emissionMax, k));
        }

        if (_audio != null)
        {
            _audio.volume = _volumeMax * k;
        }
    }

    private void ResetEffects()
    {
        if (_light != null)
        {
            _light.intensity = _lightMin;
        }

        if (_renderer != null)
        {
            SetEmission(Color.black);
        }

        if (_audio != null)
        {
            _audio.volume = 0f;
            if (_audio.isPlaying)
            {
                _audio.Stop();
            }
        }
    }

    private void SetEmission(Color c)
    {
        _renderer.GetPropertyBlock(_block);
        _block.SetColor(EmissionColorId, c);
        _block.SetColor(EmissiveFactorId, c);
        _renderer.SetPropertyBlock(_block);
    }
}
