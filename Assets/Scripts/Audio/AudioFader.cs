using System.Collections;
using UnityEngine;

/// <summary>
/// Fundidos de volumen y "distorsion" de pitch para UN AudioSource, todo
/// llamable desde cualquier UnityEvent del Inspector (sin codigo). Existe
/// porque un UnityEvent solo puede hacer Play()/Stop() secos o fijar un
/// valor de golpe; varios momentos del guion necesitan que el sonido CAMBIE
/// en el tiempo:
///  - Latidos del despertar (Parte 1): Play On Start + Auto Fade Out After.
///  - Musica de cumpleaños que se distorsiona (Parte 2): Distort() baja el
///    pitch de a poco (efecto "cinta que se frena") y, si hay un clip ya
///    distorsionado, salta a ese clip.
///  - Zumbido de Backrooms que se apaga en la revelacion (Parte 5): FadeOut()
///    deja silencio real antes del pitido final.
///  - Cualquier ambiente que no deba arrancar de golpe al despertar: Play On
///    Start con Fade In.
///
/// Armado: en el MISMO GameObject que el AudioSource. En el AudioSource:
/// poner el clip (campo AudioClip del propio AudioSource - aca SI va ahi,
/// porque este componente usa Play() del AudioSource), tildar Loop si es un
/// ambiente/musica, y DESTILDAR Play On Awake (el arranque lo maneja este
/// componente con Play On Start).
///
/// Metodos para UnityEvent: Play(), FadeIn(), FadeOut(), FadeOutOver(seg),
/// StopNow(), Distort(), RestorePitch(), SetPitch(valor).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioFader : MonoBehaviour
{
    [Header("Al arrancar la escena")]
    [Tooltip("Tildado: empieza a sonar solo al cargar la escena, con fundido de entrada (Fade In Seconds). Destildar Play On Awake en el AudioSource.")]
    [SerializeField] private bool _playOnStart;

    [Tooltip("Segundos de espera antes de arrancar (solo con Play On Start).")]
    [SerializeField] private float _startDelay;

    [Tooltip("Si es mayor que 0: despues de sonar estos segundos hace FadeOut solo (por ejemplo, los latidos del despertar se van calmando). 0 = suena hasta que algo llame FadeOut/StopNow (o hasta que termine el clip, si no es loop).")]
    [SerializeField] private float _autoFadeOutAfter;

    [Header("Volumen")]
    [Range(0f, 1f)]
    [Tooltip("Volumen al que llega con Play()/FadeIn(). Si queda en 1, se usa el Volume que ya tenia el AudioSource.")]
    [SerializeField] private float _targetVolume = 1f;

    [Tooltip("Segundos del fundido de entrada. 0 = arranca de golpe.")]
    [SerializeField] private float _fadeInSeconds = 1.5f;

    [Tooltip("Segundos del fundido de salida (FadeOut). 0 = se corta de golpe.")]
    [SerializeField] private float _fadeOutSeconds = 1.5f;

    [Header("Distorsion (Distort)")]
    [Tooltip("Pitch final de Distort(). Menos de 1 = mas lento y grave (cinta que se frena). 1 = no cambia el pitch (sirve si solo se quiere saltar al clip distorsionado).")]
    [SerializeField] private float _distortPitch = 0.55f;

    [Tooltip("Segundos que tarda en llegar al pitch final.")]
    [SerializeField] private float _distortSeconds = 2.5f;

    [Tooltip("Opcional: version YA distorsionada del mismo clip (hecha aparte, por ejemplo en Audacity). Distort() salta a este clip en la misma posicion relativa. Vacio = solo cambia el pitch.")]
    [SerializeField] private AudioClip _distortedClip;

    private AudioSource _source;
    private float _baseVolume = 1f;
    private float _basePitch = 1f;
    private Coroutine _volumeRoutine;
    private Coroutine _pitchRoutine;
    private Coroutine _autoFadeRoutine;

    private void Awake()
    {
        _source = GetComponent<AudioSource>();
        _baseVolume = _source.volume;
        _basePitch = _source.pitch;
    }

    private void Start()
    {
        if (!_playOnStart)
        {
            return;
        }
        // Por si quedo tildado Play On Awake en el AudioSource: se corta y se
        // vuelve a arrancar con fundido, en vez de sonar dos veces.
        _source.Stop();
        _source.volume = 0f;
        StartCoroutine(PlayAfterDelay());
    }

    private IEnumerator PlayAfterDelay()
    {
        if (_startDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(_startDelay);
        }
        Play();
    }

    /// <summary>Arranca desde el principio con fundido de entrada.</summary>
    public void Play()
    {
        if (_source.clip == null)
        {
            Debug.LogWarning("[AudioFader] " + name + ": el AudioSource no tiene clip (va en el campo AudioClip del propio AudioSource).");
            return;
        }
        _source.volume = _fadeInSeconds > 0f ? 0f : TargetVolume;
        _source.Play();
        FadeVolumeTo(TargetVolume, _fadeInSeconds, false);
        ScheduleAutoFadeOut();
    }

    /// <summary>Si no esta sonando, arranca; en cualquier caso sube al volumen objetivo.</summary>
    public void FadeIn()
    {
        if (!_source.isPlaying)
        {
            Play();
            return;
        }
        FadeVolumeTo(TargetVolume, _fadeInSeconds, false);
    }

    /// <summary>Baja a 0 en Fade Out Seconds y frena el AudioSource.</summary>
    public void FadeOut()
    {
        FadeOutOver(_fadeOutSeconds);
    }

    /// <summary>Igual que FadeOut() pero con otra duracion (UnityEvent con parametro float).</summary>
    public void FadeOutOver(float seconds)
    {
        CancelAutoFadeOut();
        if (!_source.isPlaying)
        {
            return;
        }
        FadeVolumeTo(0f, seconds, true);
    }

    /// <summary>Corta en seco, sin fundido.</summary>
    public void StopNow()
    {
        CancelAutoFadeOut();
        StopRoutine(ref _volumeRoutine);
        _source.Stop();
    }

    /// <summary>
    /// "Se distorsiona": salta al Distorted Clip (si hay) y baja el pitch de a
    /// poco hasta Distort Pitch.
    /// </summary>
    public void Distort()
    {
        if (_distortedClip != null && _source.clip != _distortedClip)
        {
            SwapClipKeepingPosition(_distortedClip);
        }
        PitchTo(_distortPitch, _distortSeconds);
    }

    /// <summary>Vuelve al pitch original de a poco (mismo tiempo que Distort).</summary>
    public void RestorePitch()
    {
        PitchTo(_basePitch, _distortSeconds);
    }

    /// <summary>Fija el pitch de golpe (UnityEvent con parametro float).</summary>
    public void SetPitch(float pitch)
    {
        StopRoutine(ref _pitchRoutine);
        _source.pitch = pitch;
    }

    // ---------------- Internos ----------------

    private float TargetVolume => _targetVolume < 1f ? _targetVolume : _baseVolume;

    private void ScheduleAutoFadeOut()
    {
        CancelAutoFadeOut();
        if (_autoFadeOutAfter > 0f && isActiveAndEnabled)
        {
            _autoFadeRoutine = StartCoroutine(AutoFadeOut());
        }
    }

    private IEnumerator AutoFadeOut()
    {
        yield return new WaitForSecondsRealtime(_autoFadeOutAfter);
        _autoFadeRoutine = null;
        FadeOutOver(_fadeOutSeconds);
    }

    private void CancelAutoFadeOut()
    {
        StopRoutine(ref _autoFadeRoutine);
    }

    private void FadeVolumeTo(float target, float seconds, bool stopAtEnd)
    {
        StopRoutine(ref _volumeRoutine);
        if (seconds <= 0f || !isActiveAndEnabled)
        {
            _source.volume = target;
            if (stopAtEnd)
            {
                _source.Stop();
            }
            return;
        }
        _volumeRoutine = StartCoroutine(VolumeRoutine(target, seconds, stopAtEnd));
    }

    private IEnumerator VolumeRoutine(float target, float seconds, bool stopAtEnd)
    {
        float from = _source.volume;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            _source.volume = Mathf.Lerp(from, target, Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }
        _source.volume = target;
        if (stopAtEnd)
        {
            _source.Stop();
        }
        _volumeRoutine = null;
    }

    private void PitchTo(float target, float seconds)
    {
        StopRoutine(ref _pitchRoutine);
        if (seconds <= 0f || !isActiveAndEnabled)
        {
            _source.pitch = target;
            return;
        }
        _pitchRoutine = StartCoroutine(PitchRoutine(target, seconds));
    }

    private IEnumerator PitchRoutine(float target, float seconds)
    {
        float from = _source.pitch;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(elapsed / seconds);
            // Arranca lento y se "frena" cada vez mas rapido, como una cinta.
            _source.pitch = Mathf.Lerp(from, target, k * k);
            yield return null;
        }
        _source.pitch = target;
        _pitchRoutine = null;
    }

    private void SwapClipKeepingPosition(AudioClip newClip)
    {
        AudioClip oldClip = _source.clip;
        bool wasPlaying = _source.isPlaying;
        float relative = 0f;
        if (oldClip != null && oldClip.length > 0f)
        {
            relative = Mathf.Repeat(_source.time / oldClip.length, 1f);
        }
        _source.clip = newClip;
        if (wasPlaying)
        {
            _source.Play();
            // Posicion relativa, sin pasarse del final del clip nuevo.
            _source.time = Mathf.Min(relative * newClip.length, Mathf.Max(0f, newClip.length - 0.05f));
        }
    }

    private void StopRoutine(ref Coroutine routine)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }
}
