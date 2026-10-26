using System.Collections;
using UnityEngine;

/// <summary>
/// Monitor cardiaco del final (hospital): un bip cada "Interval" segundos y la
/// pantalla (y opcionalmente una luz) destella con cada bip. Usa tiempo
/// escalado. Funciona con materiales URP Lit/Unlit (_BaseColor/_EmissionColor)
/// y con el shader glTF (emissiveFactor), via MaterialPropertyBlock.
///
/// Se maneja desde UnityEvents: SetInterval, SpeedUp (acelera el ritmo),
/// Flatline (tono continuo, pantalla apagada) y StopBeeping.
/// </summary>
public class MonitorBeepDisplay : MonoBehaviour
{
    [Header("Sonido")]
    [Tooltip("AudioSource 3D desde donde suena el bip.")]
    [SerializeField] private AudioSource _audioSource;

    [Tooltip("Bip corto (un solo pitido).")]
    [SerializeField] private AudioClip _beepClip;

    [Tooltip("Tono continuo de linea plana (opcional, puede ser un loop).")]
    [SerializeField] private AudioClip _flatlineClip;

    [Header("Ritmo")]
    [Tooltip("Segundos entre bips.")]
    [SerializeField] private float _interval = 1f;

    [Tooltip("Arranca solo al activarse el objeto.")]
    [SerializeField] private bool _startOnEnable = true;

    [Header("Pantalla")]
    [Tooltip("Renderer de la pantalla del monitor (destella en cada bip).")]
    [SerializeField] private Renderer _screen;

    [Tooltip("Color de emision en el destello (HDR).")]
    [ColorUsage(true, true)]
    [SerializeField] private Color _flashColor = new Color(0.2f, 2.2f, 0.4f);

    [Tooltip("Color de emision entre bips.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color _idleColor = new Color(0.02f, 0.15f, 0.04f);

    [Tooltip("Color cuando esta en linea plana.")]
    [ColorUsage(true, true)]
    [SerializeField] private Color _flatlineColor = new Color(0.01f, 0.03f, 0.01f);

    [Tooltip("Segundos que tarda el destello en apagarse.")]
    [SerializeField] private float _flashDecay = 0.25f;

    [Header("Luz (opcional)")]
    [Tooltip("Luz puntual chica que late con el bip.")]
    [SerializeField] private Light _light;

    [Tooltip("Intensidad de la luz en el pico del destello.")]
    [SerializeField] private float _lightPeak = 1.5f;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
    private static readonly int EmissiveFactorId = Shader.PropertyToID("emissiveFactor");

    private MaterialPropertyBlock _block;
    private Coroutine _loop;
    private Coroutine _speedUp;
    private float _flash; // 0..1
    private bool _flat;

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        _flat = false;
        ApplyVisual(_idleColor, 0f);
        if (_startOnEnable)
        {
            StartBeeping();
        }
    }

    private void OnDisable()
    {
        _loop = null;
        _speedUp = null;
    }

    private void Update()
    {
        if (_flash > 0f)
        {
            _flash = Mathf.Max(0f, _flash - Time.deltaTime / Mathf.Max(0.01f, _flashDecay));
            ApplyVisual(Color.Lerp(_idleColor, _flashColor, _flash), _flash);
        }
    }

    /// <summary>Empieza (o reanuda) los bips con el intervalo actual.</summary>
    public void StartBeeping()
    {
        _flat = false;
        if (_loop == null)
        {
            _loop = StartCoroutine(BeepLoop());
        }
    }

    /// <summary>Cambia el intervalo entre bips (segundos).</summary>
    public void SetInterval(float seconds)
    {
        _interval = Mathf.Max(0.1f, seconds);
    }

    /// <summary>Lleva el intervalo hasta targetInterval en overSeconds (acelerar o calmar el pulso).</summary>
    public void SpeedUp(float targetInterval, float overSeconds)
    {
        if (_speedUp != null)
        {
            StopCoroutine(_speedUp);
        }
        _speedUp = StartCoroutine(RampInterval(Mathf.Max(0.1f, targetInterval), Mathf.Max(0.01f, overSeconds)));
    }

    /// <summary>Linea plana: corta los bips, tono continuo y pantalla casi apagada.</summary>
    public void Flatline()
    {
        StopBeeping();
        _flat = true;
        _flash = 0f;
        ApplyVisual(_flatlineColor, 0f);

        if (_audioSource != null && _flatlineClip != null)
        {
            _audioSource.Stop();
            _audioSource.clip = _flatlineClip;
            _audioSource.Play();
        }
    }

    /// <summary>Corta los bips (y la rampa de velocidad). No toca el sonido de linea plana.</summary>
    public void StopBeeping()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
        if (_speedUp != null)
        {
            StopCoroutine(_speedUp);
            _speedUp = null;
        }
    }

    private IEnumerator BeepLoop()
    {
        while (true)
        {
            Beep();
            yield return new WaitForSeconds(_interval);
        }
    }

    private IEnumerator RampInterval(float target, float seconds)
    {
        float from = _interval;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.deltaTime;
            _interval = Mathf.Lerp(from, target, Mathf.Clamp01(t / seconds));
            yield return null;
        }
        _interval = target;
        _speedUp = null;
    }

    private void Beep()
    {
        if (_audioSource != null && _beepClip != null)
        {
            _audioSource.PlayOneShot(_beepClip);
        }
        _flash = 1f;
        ApplyVisual(_flashColor, 1f);
    }

    private void ApplyVisual(Color emission, float lightK)
    {
        if (_screen != null)
        {
            _screen.GetPropertyBlock(_block);
            _block.SetColor(EmissionColorId, emission);
            _block.SetColor(EmissiveFactorId, emission);
            _screen.SetPropertyBlock(_block);
        }

        if (_light != null)
        {
            _light.intensity = _flat ? 0f : _lightPeak * lightK;
        }
    }
}
