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

    [Header("Ritmo")]
    [Tooltip("Espera antes del primer sonido (segundos, min/max).")]
    [SerializeField] private Vector2 _firstDelay = new Vector2(3f, 5f);
    [Tooltip("Espera entre sonidos (segundos, min/max).")]
    [SerializeField] private Vector2 _gap = new Vector2(5f, 9f);
    [Tooltip("Probabilidad de que el siguiente sonido sea un susurro (si no, pasos). Nunca repite el mismo tipo tres veces seguidas.")]
    [Range(0f, 1f)]
    [SerializeField] private float _whisperChance = 0.5f;

    private AudioSource _steps;
    private AudioSource _whisper;
    private Coroutine _loop;
    private Coroutine _stepsMove;
    private int _lastWhisper = -1;
    private float _whisperSide = 1f;
    private int _sameKindStreak;
    private bool _lastWasWhisper;

    public bool IsRunning => _loop != null;

    private void Awake()
    {
        _steps = CreateSource("Pasos (susto)", 1f, 25f, AudioRolloffMode.Logarithmic);
        _whisper = CreateSource("Susurro (susto)", 0.1f, 3f, AudioRolloffMode.Linear);
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
        }
        else
        {
            _steps.Stop();
            _whisper.Stop();
        }
    }

    private void OnDisable()
    {
        _loop = null;
        _stepsMove = null;
    }

    private IEnumerator Loop()
    {
        yield return new WaitForSeconds(Random.Range(_firstDelay.x, _firstDelay.y));
        while (true)
        {
            bool canWhisper = _whisperClips != null && _whisperClips.Length > 0;
            bool canSteps = _footstepsClip != null;
            if (!canWhisper && !canSteps)
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

            float duration = whisper ? PlayWhisper() : PlayFootsteps();
            yield return new WaitForSeconds(duration + Random.Range(_gap.x, _gap.y));
        }
    }

    private float PlayWhisper()
    {
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

    private static IEnumerator FadeOut(AudioSource source, float seconds)
    {
        if (source == null || !source.isPlaying)
        {
            yield break;
        }
        float start = source.volume;
        float t = 0f;
        while (t < seconds && source.isPlaying)
        {
            t += Time.deltaTime;
            source.volume = Mathf.Lerp(start, 0f, t / seconds);
            yield return null;
        }
        source.Stop();
    }
}
