using System.Collections;
using UnityEngine;

/// <summary>
/// Sonidos de tension mientras el jugador ingresa el codigo del candado
/// (Parte 1, pedido de Omar 2026-09-24): pasos que se acercan por detras y
/// susurros pegados al oido. Se juega con auriculares, asi que el 3D se nota.
///
/// Cableado (en la escena, sin codigo):
/// - ExamineTrigger (BaseLP) > On Examine Start > StartScares()
/// - ExamineTrigger (BaseLP) > On Examine End   > StopScares()
/// On Examine End ya se dispara al resolver el candado (CodeLock > On Unlocked
/// llama ReturnToPreviousPoint) y al salir con el boton Volver, asi que los
/// sustos se cortan solos en los dos casos.
///
/// Los pasos salen de un tramo al azar de un clip largo de pasos (por defecto
/// pasos_concreto.wav), desde un punto a espaldas del jugador que se va
/// acercando mientras suena. Los susurros son clips cortos (generados por
/// Herramientas > Generar susurros) que suenan a ~35 cm de un oido,
/// alternando izquierda/derecha.
/// </summary>
public class ExamineScareSounds : MonoBehaviour
{
    [Tooltip("Cabeza del jugador (Main Camera). Vacio = Camera.main.")]
    [SerializeField] private Transform _listener;

    [Header("Pasos")]
    [Tooltip("Clip largo de pasos; se usa un tramo al azar cada vez.")]
    [SerializeField] private AudioClip _footstepsClip;
    [SerializeField] private float _footstepsVolume = 0.85f;
    [Tooltip("Duracion de cada tanda de pasos (segundos, min/max).")]
    [SerializeField] private Vector2 _footstepsSeconds = new Vector2(2.5f, 4f);
    [Tooltip("Distancia a espaldas del jugador donde arrancan los pasos (metros, min/max).")]
    [SerializeField] private Vector2 _footstepsDistance = new Vector2(4f, 7f);
    [Tooltip("Cuantos metros se acercan mientras suenan.")]
    [SerializeField] private float _footstepsApproach = 1.8f;
    [Tooltip("Pitch de los pasos: menos de 1 = mas lentos y pesados.")]
    [SerializeField] private float _footstepsPitch = 0.85f;

    [Header("Susurros")]
    [SerializeField] private AudioClip[] _whisperClips;
    [SerializeField] private float _whisperVolume = 0.9f;
    [Tooltip("Distancia al oido (metros).")]
    [SerializeField] private float _whisperEarDistance = 0.35f;

    [Header("Sonidos lejanos (opcional)")]
    [Tooltip("Crujidos, golpes, etc. que suenan en 3D en una direccion al azar alrededor del jugador (no pegados al oido).")]
    [SerializeField] private AudioClip[] _distantClips;
    [SerializeField] private float _distantVolume = 0.8f;
    [Tooltip("Distancia al jugador de los sonidos lejanos (metros, min/max).")]
    [SerializeField] private Vector2 _distantDistance = new Vector2(6f, 12f);

    [Header("Arranque")]
    [Tooltip("Tildado: los sustos arrancan solos al cargar la escena (ambiente mientras se recorre el nivel). Destildado: solo con StartScares() desde un evento (como el candado de Parte 1).")]
    [SerializeField] private bool _startOnPlay;

    [Header("Ritmo")]
    [Tooltip("Espera antes del primer sonido (segundos, min/max).")]
    [SerializeField] private Vector2 _firstDelay = new Vector2(3f, 5f);
    [Tooltip("Espera entre sonidos (segundos, min/max).")]
    [SerializeField] private Vector2 _gap = new Vector2(5f, 9f);
    [Tooltip("Probabilidad de que el siguiente sonido sea un susurro (si no, pasos). Nunca repite el mismo tipo tres veces seguidas.")]
    [Range(0f, 1f)]
    [SerializeField] private float _whisperChance = 0.5f;

    [Tooltip("Máximo de sustos por StartScares (0 = sin límite). Con 1 suena una sola vez mientras se examina.")]
    [SerializeField] private int _maxSounds = 0;

    private AudioSource _steps;
    private AudioSource _whisper;
    private AudioSource _distant;
    private Coroutine _loop;
    private Coroutine _stepsMove;
    private int _lastWhisper = -1;
    // Sube con cada sonido nuevo; FadeOut lo usa para no cortar un sonido
    // pedido despues de StopScares.
    private int _soundToken;
    private float _whisperSide = 1f;
    private int _sameKindStreak;
    private bool _lastWasWhisper;
    // Sonidos reproducidos por el loop desde el ultimo StartScares (los PlayXNow no cuentan).
    private int _soundsPlayed;

    public bool IsRunning => _loop != null;

    private void Awake()
    {
        _steps = CreateSource("Pasos (susto)", 1f, 25f, AudioRolloffMode.Logarithmic);
        _whisper = CreateSource("Susurro (susto)", 0.1f, 3f, AudioRolloffMode.Linear);
        _distant = CreateSource("Lejano (susto)", 2f, 30f, AudioRolloffMode.Logarithmic);
    }

    private void Start()
    {
        if (_startOnPlay)
        {
            StartScares();
        }
    }

    /// <summary>Un susurro al oido ya (para cablear a un evento puntual, por ejemplo el loop de Parte 4).</summary>
    public void PlayWhisperNow()
    {
        if (EnsureListener() && _whisperClips != null && _whisperClips.Length > 0)
        {
            PlayWhisper();
        }
    }

    /// <summary>Pasos a espaldas del jugador ya.</summary>
    public void PlayFootstepsNow()
    {
        if (EnsureListener() && _footstepsClip != null)
        {
            PlayFootsteps();
        }
    }

    /// <summary>Un sonido lejano (crujido, golpe) ya.</summary>
    public void PlayDistantNow()
    {
        if (EnsureListener() && _distantClips != null && _distantClips.Length > 0)
        {
            PlayDistant();
        }
    }

    private bool EnsureListener()
    {
        if (_listener == null && Camera.main != null)
        {
            _listener = Camera.main.transform;
        }
        return _listener != null && isActiveAndEnabled;
    }

    private float PlayDistant()
    {
        _soundToken++;
        AudioClip clip = _distantClips[Random.Range(0, _distantClips.Length)];
        Vector3 dir = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * Vector3.forward;
        Vector3 pos = _listener.position + dir * Random.Range(_distantDistance.x, _distantDistance.y);
        pos.y = _listener.position.y + Random.Range(-1f, 0.5f);
        _distant.transform.SetParent(transform, true);
        _distant.transform.position = pos;
        _distant.pitch = Random.Range(0.85f, 1.1f);
        _distant.volume = _distantVolume;
        // Play() (no PlayOneShot): asi StopScares lo corta con el fundido igual
        // que a los pasos y susurros (el apagon de Parte 5 llama StopScares).
        _distant.clip = clip;
        _distant.Play();
        return clip.length / Mathf.Max(0.1f, _distant.pitch);
    }

    private AudioSource CreateSource(string name, float minDistance, float maxDistance, AudioRolloffMode rolloff)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        AudioSource s = go.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = false;
        s.spatialBlend = 1f;
        s.dopplerLevel = 0f;
        s.spread = 0f;
        s.rolloffMode = rolloff;
        s.minDistance = minDistance;
        s.maxDistance = maxDistance;
        s.priority = 64;
        return s;
    }

    /// <summary>Empieza la secuencia de sustos (desde On Examine Start).</summary>
    public void StartScares()
    {
        if (_loop != null || !isActiveAndEnabled)
        {
            return;
        }
        if (_listener == null && Camera.main != null)
        {
            _listener = Camera.main.transform;
        }
        if (_listener == null)
        {
            Debug.LogWarning("[ExamineScareSounds] No hay camara: sin sustos.");
            return;
        }
        _sameKindStreak = 0;
        _soundsPlayed = 0;
        _loop = StartCoroutine(Loop());
    }

    /// <summary>Corta los sustos con un fundido corto (desde On Examine End).</summary>
    public void StopScares()
    {
        if (_loop != null)
        {
            StopCoroutine(_loop);
            _loop = null;
        }
        if (_stepsMove != null)
        {
            StopCoroutine(_stepsMove);
            _stepsMove = null;
        }
        if (isActiveAndEnabled)
        {
            StartCoroutine(FadeOut(_steps, 0.3f));
            StartCoroutine(FadeOut(_whisper, 0.3f));
            StartCoroutine(FadeOut(_distant, 0.3f));
        }
        else
        {
            _steps.Stop();
            _whisper.Stop();
            _distant.Stop();
        }
    }

    private void OnDisable()
    {
        // Unity corta las corrutinas si se apaga el GameObject, pero NO si solo
        // se destilda el componente: sin esto quedaba un loop "huerfano" y
        // StartScares() arrancaba un segundo loop en paralelo.
        StopAllCoroutines();
        _loop = null;
        _stepsMove = null;
        if (_steps != null)
        {
            _steps.Stop();
        }
    }

    private IEnumerator Loop()
    {
        yield return new WaitForSeconds(Random.Range(_firstDelay.x, _firstDelay.y));
        while (true)
        {
            bool canWhisper = _whisperClips != null && _whisperClips.Length > 0;
            bool canSteps = _footstepsClip != null;
            bool canDistant = _distantClips != null && _distantClips.Length > 0;
            if (!canWhisper && !canSteps && !canDistant)
            {
                _loop = null;
                yield break;
            }

            bool whisper = canWhisper && (!canSteps || Random.value < _whisperChance);
            // Nunca tres del mismo tipo seguidos.
            if (_sameKindStreak >= 2 && whisper == _lastWasWhisper && canWhisper && canSteps)
            {
                whisper = !whisper;
            }
            _sameKindStreak = whisper == _lastWasWhisper ? _sameKindStreak + 1 : 1;
            _lastWasWhisper = whisper;

            // Si hay sonidos lejanos, uno de cada tres turnos (aprox.) es uno de esos.
            bool distant = canDistant && ((!canWhisper && !canSteps) || Random.value < 0.34f);
            float duration = distant ? PlayDistant() : (whisper ? PlayWhisper() : PlayFootsteps());
            _soundsPlayed++;
            if (_maxSounds > 0 && _soundsPlayed >= _maxSounds)
            {
                // Llegó al tope: espera a que termine el sonido y no programa más.
                yield return new WaitForSeconds(duration);
                _loop = null;
                yield break;
            }
            yield return new WaitForSeconds(duration + Random.Range(_gap.x, _gap.y));
        }
    }

    private float PlayWhisper()
    {
        _soundToken++;
        int i = Random.Range(0, _whisperClips.Length);
        if (_whisperClips.Length > 1 && i == _lastWhisper)
        {
            i = (i + 1) % _whisperClips.Length;
        }
        _lastWhisper = i;
        AudioClip clip = _whisperClips[i];

        // Pegado a un oido (alternando lados), un poquito por detras.
        _whisperSide = -_whisperSide;
        _whisper.transform.SetParent(_listener, false);
        _whisper.transform.localPosition = new Vector3(_whisperSide * _whisperEarDistance, 0f, -0.08f);
        _whisper.clip = clip;
        _whisper.volume = _whisperVolume;
        _whisper.pitch = Random.Range(0.92f, 1.05f);
        _whisper.Play();
        return clip.length / Mathf.Max(0.1f, _whisper.pitch);
    }

    private float PlayFootsteps()
    {
        _soundToken++;
        float duration = Random.Range(_footstepsSeconds.x, _footstepsSeconds.y);
        float maxStart = Mathf.Max(0f, _footstepsClip.length - duration * _footstepsPitch - 0.1f);

        // A espaldas del jugador (+-50 grados), en el plano horizontal.
        Vector3 back = -Vector3.ProjectOnPlane(_listener.forward, Vector3.up).normalized;
        if (back.sqrMagnitude < 0.01f)
        {
            back = -Vector3.forward;
        }
        back = Quaternion.AngleAxis(Random.Range(-50f, 50f), Vector3.up) * back;
        float distance = Random.Range(_footstepsDistance.x, _footstepsDistance.y);
        Vector3 start = _listener.position + back * distance;
        start.y = _listener.position.y - 1.2f; // a la altura del piso, no de la cabeza

        _steps.transform.SetParent(transform, true);
        _steps.transform.position = start;
        _steps.clip = _footstepsClip;
        _steps.pitch = _footstepsPitch;
        _steps.time = Random.Range(0f, maxStart);
        _steps.volume = 0f;
        _steps.Play();

        if (_stepsMove != null)
        {
            StopCoroutine(_stepsMove);
        }
        _stepsMove = StartCoroutine(MoveSteps(start, -back, duration));
        return duration;
    }

    private IEnumerator MoveSteps(Vector3 start, Vector3 towardPlayer, float duration)
    {
        const float fade = 0.35f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            _steps.transform.position = start + towardPlayer * (_footstepsApproach * Mathf.Clamp01(t / duration));
            float fadeIn = Mathf.Clamp01(t / fade);
            float fadeOut = Mathf.Clamp01((duration - t) / fade);
            _steps.volume = _footstepsVolume * Mathf.Min(fadeIn, fadeOut);
            yield return null;
        }
        _steps.Stop();
        _stepsMove = null;
    }

    private IEnumerator FadeOut(AudioSource source, float seconds)
    {
        if (source == null || !source.isPlaying)
        {
            yield break;
        }
        // Si mientras tanto se pidio un sonido nuevo (PlayWhisperNow, etc.), el
        // fundido se abandona: antes lo bajaba a 0 y lo cortaba.
        int token = _soundToken;
        float start = source.volume;
        float t = 0f;
        while (t < seconds && source.isPlaying)
        {
            if (token != _soundToken)
            {
                yield break;
            }
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(start, 0f, t / seconds);
            yield return null;
        }
        if (token == _soundToken)
        {
            source.Stop();
        }
    }
}
