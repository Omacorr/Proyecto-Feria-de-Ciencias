using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Late como un corazon sobre el "weight" de un Volume de post-procesado.
/// Pensado para un Volume global aparte (perfil propio de la escena, NO el
/// "Global Volume Profile" compartido) con, por ejemplo, Vignette mas fuerte
/// y rojiza + ColorAdjustments desaturado: cada latido "cierra" un poco la
/// vision y la suelta. Sirve para momentos de tension (despertar con latidos,
/// agarrar la llave, vuelta del loop, aparicion del monstruo).
///
/// Costo: casi cero. Solo cambia Volume.weight; Vignette y ColorAdjustments
/// ya corren en el pase final de post-procesado (no agrega pases). No dibuja
/// nada por su cuenta, asi que funciona igual en multi-pass y single-pass.
///
/// Metodos para UnityEvent: Pulse(segundos), StartPulse(), StopPulse(),
/// SetBpm(float). Fotosensibilidad: es un cambio lento (1-2 Hz, suave), no
/// un destello.
/// </summary>
public class VolumePulse : MonoBehaviour
{
    [Tooltip("Volume a modular. Tiene que ser Global y arrancar con Weight 0.")]
    [SerializeField] private Volume _volume;

    [Tooltip("Latidos por minuto (60-80 calmo, 110-140 panico).")]
    [SerializeField] private float _bpm = 110f;

    [Range(0f, 1f)]
    [Tooltip("Weight maximo en el pico del latido.")]
    [SerializeField] private float _maxWeight = 1f;

    [Range(0f, 1f)]
    [Tooltip("Weight que queda entre latidos mientras dura el pulso (0 = vuelve a normal entre latidos).")]
    [SerializeField] private float _restWeight = 0.25f;

    [Range(0f, 1f)]
    [Tooltip("Fuerza del segundo golpe (\"dub\") respecto del primero.")]
    [SerializeField] private float _secondBeat = 0.6f;

    [Tooltip("Onda suave (seno) en vez de latido: para respiraciones lentas, por ejemplo el brillo de los Backrooms que sube y baja con el zumbido.")]
    [SerializeField] private bool _smoothWave;

    [Tooltip("Segundos de fundido al empezar y al terminar.")]
    [SerializeField] private float _fadeSeconds = 0.8f;

    [Tooltip("Arranca a latir al iniciar la escena (por ejemplo, con los latidos del despertar).")]
    [SerializeField] private bool _playOnStart;

    [Tooltip("Si Play On Start esta tildado: segundos que dura (0 = hasta StopPulse).")]
    [SerializeField] private float _startSeconds = 7f;

    private float _phase;
    private float _envelope;
    private bool _active;
    private float _stopAt = -1f;

    /// <summary>Late durante "seconds" segundos y se apaga solo.</summary>
    public void Pulse(float seconds)
    {
        _active = true;
        _stopAt = seconds > 0f ? Time.time + seconds : -1f;
    }

    /// <summary>Late hasta que se llame StopPulse().</summary>
    public void StartPulse()
    {
        _active = true;
        _stopAt = -1f;
    }

    /// <summary>Deja de latir (con fundido).</summary>
    public void StopPulse()
    {
        _active = false;
    }

    /// <summary>Cambia el ritmo (por ejemplo, subirlo cuando el monstruo se acerca).</summary>
    public void SetBpm(float bpm)
    {
        _bpm = Mathf.Max(20f, bpm);
    }

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
        if (_volume != null)
        {
            _volume.weight = 0f;
        }
    }

    private void Start()
    {
        if (_playOnStart)
        {
            Pulse(_startSeconds);
        }
    }

    private void OnDisable()
    {
        _active = false;
        _envelope = 0f;
        if (_volume != null)
        {
            _volume.weight = 0f;
        }
    }

    private void Update()
    {
        if (_volume == null)
        {
            return;
        }

        if (_active && _stopAt > 0f && Time.time >= _stopAt)
        {
            _active = false;
        }

        float fadeRate = _fadeSeconds > 0f ? Time.deltaTime / _fadeSeconds : 1f;
        _envelope = Mathf.MoveTowards(_envelope, _active ? 1f : 0f, fadeRate);

        if (_envelope <= 0f)
        {
            if (_volume.weight != 0f)
            {
                _volume.weight = 0f;
            }
            _phase = 0f;
            return;
        }

        _phase = (_phase + Time.deltaTime * _bpm / 60f) % 1f;

        // "lub" en 0 y "dub" en 0.28 del latido: subida rapida, caida suave.
        float beat = _smoothWave
            ? 0.5f - 0.5f * Mathf.Cos(_phase * Mathf.PI * 2f)
            : Mathf.Max(Spike(_phase, 0f), Spike(_phase, 0.28f) * _secondBeat);
        float shape = Mathf.Lerp(_restWeight, 1f, beat);
        _volume.weight = Mathf.Clamp01(shape * _maxWeight * _envelope);
    }

    private static float Spike(float phase, float at)
    {
        float t = phase - at;
        if (t < 0f)
        {
            t += 1f;
        }
        const float attack = 0.04f;
        const float decay = 0.22f;
        if (t < attack)
        {
            return t / attack;
        }
        if (t < attack + decay)
        {
            float d = (t - attack) / decay;
            return 1f - d * d * (3f - 2f * d);
        }
        return 0f;
    }
}
