using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Una puerta que se entreabre SOLA cuando no la estas mirando, una sola vez.
/// Antes de abrir se corta de golpe el zumbido de fondo (silencio incomodo),
/// y despues vuelve despacio. Nunca toca la camara.
///
/// Flujo: Arm() -> esperar fuera de vista -> cortar zumbido (On Silence) ->
/// esperar el silencio -> si justo la miras, esperar a que vuelva a estar fuera
/// de vista -> abrir (crujido + luz opcional) -> volver el zumbido.
/// </summary>
public class OpenWhenUnseen : MonoBehaviour
{
    [Header("Puerta")]
    [Tooltip("Hoja de la puerta que se mueve.")]
    [SerializeField] private Transform _door;

    [Tooltip("Bisagra (opcional). Si esta vacia, gira alrededor de la posicion de la puerta.")]
    [SerializeField] private Transform _hinge;

    [Tooltip("Eje de giro en coordenadas de mundo.")]
    [SerializeField] private Vector3 _axis = Vector3.up;

    [Tooltip("Grados que se entreabre.")]
    [SerializeField] private float _openDegrees = 14f;

    [Tooltip("Duracion de la apertura, en segundos (ease in-out).")]
    [SerializeField] private float _openSeconds = 3f;

    [Header("Fuera de vista")]
    [Tooltip("Esta fuera de vista si el angulo entre hacia donde mira el jugador y la puerta supera esto (grados).")]
    [SerializeField] private float _unseenAngle = 75f;

    [Tooltip("Segundos continuos fuera de vista antes de empezar.")]
    [SerializeField] private float _unseenSeconds = 0.5f;

    [Header("Sonido")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _creakClip;

    [Tooltip("Zumbido de fondo (opcional). Se corta de golpe antes de abrir y vuelve con fundido.")]
    [SerializeField] private AudioSource _humSource;

    [Tooltip("Segundos de silencio entre que se corta el zumbido y empieza a abrir.")]
    [SerializeField] private float _silenceBefore = 2f;

    [Tooltip("Segundos despues de terminar de abrir hasta que empieza a volver el zumbido.")]
    [SerializeField] private float _humReturnDelay = 4f;

    [Tooltip("Duracion del fundido de vuelta del zumbido.")]
    [SerializeField] private float _humFadeInSeconds = 5f;

    [Header("Luz de revelado (opcional)")]
    [Tooltip("Luz que arranca apagada y sube mientras abre.")]
    [SerializeField] private Light _revealLight;
    [SerializeField] private float _revealIntensity = 0.8f;

    [Header("Comportamiento")]
    [SerializeField] private bool _playOnce = true;

    [Header("Eventos")]
    [SerializeField] private UnityEvent _onSilence;
    [SerializeField] private UnityEvent _onOpened;

    private Camera _cam;
    private Coroutine _routine;
    private bool _done;

    private void Awake()
    {
        if (_revealLight != null) _revealLight.intensity = 0f;
    }

    /// <summary>Empieza a esperar el momento de abrir. Llamable desde UnityEvent.</summary>
    public void Arm()
    {
        if (_routine != null) return;
        if (_playOnce && _done) return;
        _routine = StartCoroutine(Run());
    }

    [ContextMenu("Probar (solo en Play)")]
    private void TestInPlay()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[OpenWhenUnseen] Solo funciona en Play."); return; }
        _done = false;
        Arm();
    }

    private void OnDisable()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
    }

    private bool IsUnseen()
    {
        if (_door == null) return false;
        if (_cam == null) _cam = Camera.main;
        if (_cam == null) return false;
        Vector3 to = _door.position - _cam.transform.position;
        return Vector3.Angle(_cam.transform.forward, to) > _unseenAngle;
    }

    private IEnumerator Run()
    {
        if (_door == null) { Debug.LogWarning("[OpenWhenUnseen] Falta la puerta."); _routine = null; yield break; }

        // 1) Esperar fuera de vista de forma continua.
        float unseen = 0f;
        while (unseen < _unseenSeconds)
        {
            if (IsUnseen()) unseen += Time.deltaTime; else unseen = 0f;
            yield return null;
        }

        // 2) Cortar el zumbido de golpe.
        float humVolume = 1f;
        if (_humSource != null)
        {
            humVolume = _humSource.volume;
            _humSource.volume = 0f;
        }
        _onSilence?.Invoke();
        Debug.Log("[OpenWhenUnseen] Silencio.");

        yield return new WaitForSeconds(_silenceBefore);

        // 3) Si justo la miras, esperar a que vuelva a estar fuera de vista.
        while (!IsUnseen()) yield return null;

        // 4) Abrir.
        if (_audioSource != null && _creakClip != null) _audioSource.PlayOneShot(_creakClip);

        Vector3 pivot = _hinge != null ? _hinge.position : _door.position;
        Vector3 axis = _axis.sqrMagnitude > 0.0001f ? _axis.normalized : Vector3.up;
        float applied = 0f;
        float dur = Mathf.Max(0.01f, _openSeconds);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / dur;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            float target = _openDegrees * k;
            _door.RotateAround(pivot, axis, target - applied);
            applied = target;
            if (_revealLight != null) _revealLight.intensity = _revealIntensity * k;
            yield return null;
        }
        if (_revealLight != null) _revealLight.intensity = _revealIntensity;

        _done = true;
        _onOpened?.Invoke();
        Debug.Log("[OpenWhenUnseen] Puerta entreabierta.");

        // 5) Volver el zumbido.
        if (_humSource != null)
        {
            yield return new WaitForSeconds(_humReturnDelay);
            float fade = Mathf.Max(0.01f, _humFadeInSeconds);
            float f = 0f;
            while (f < 1f)
            {
                f += Time.deltaTime / fade;
                _humSource.volume = humVolume * Mathf.Clamp01(f);
                yield return null;
            }
            _humSource.volume = humVolume;
        }

        _routine = null;
    }
}
