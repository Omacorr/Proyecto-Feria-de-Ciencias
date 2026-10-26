using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Una silueta al fondo del pasillo que aparece (siempre fuera de vista, nunca
/// se la ve "aparecer") y desaparece de golpe cuando la miraste y apartaste la
/// vista, cuando te acercas, o si se queda mucho tiempo. Nunca toca la camara
/// y no usa efectos de luz.
/// </summary>
public class DistantWatcher : MonoBehaviour
{
    [Header("Figura")]
    [Tooltip("Objeto de la silueta. Tiene que arrancar inactivo. El modelo mira a +Z.")]
    [SerializeField] private GameObject _figure;

    [Tooltip("Gira solo en yaw para mirar al jugador.")]
    [SerializeField] private bool _faceCamera = true;

    [Header("Vista")]
    [Tooltip("'La estas mirando' si el angulo (grados) entre tu mirada y la figura (a 1.6 m de altura) es menor a esto.")]
    [SerializeField] private float _viewAngle = 28f;

    [Header("Desaparicion")]
    [Tooltip("Si te acercas a menos de esta distancia horizontal (m), desaparece.")]
    [SerializeField] private float _vanishDistance = 12f;

    [Tooltip("Segundos que tenes que mirarla para que despues, al apartar la vista, desaparezca.")]
    [SerializeField] private float _minSeenSeconds = 0.8f;

    [Tooltip("Tope de segundos activa: pasado esto, desaparece en cuanto este fuera de vista.")]
    [SerializeField] private float _maxVisibleSeconds = 25f;

    [SerializeField] private bool _playOnce = true;

    [Header("Eventos")]
    [SerializeField] private UnityEvent _onAppear;
    [SerializeField] private UnityEvent _onSeen;
    [SerializeField] private UnityEvent _onVanish;

    private Camera _cam;
    private Coroutine _routine;
    private bool _done;

    /// <summary>Espera a que la posicion este fuera de vista y activa la figura.</summary>
    public void Appear()
    {
        if (_routine != null) return;
        if (_playOnce && _done) return;
        if (_figure == null) { Debug.LogWarning("[DistantWatcher] Falta la figura."); return; }
        _routine = StartCoroutine(Run());
    }

    [ContextMenu("Probar (solo en Play)")]
    private void TestInPlay()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[DistantWatcher] Solo funciona en Play."); return; }
        _done = false;
        Appear();
    }

    private void OnDisable()
    {
        if (_routine != null) { StopCoroutine(_routine); _routine = null; }
    }

    private float AngleTo(Vector3 worldPoint)
    {
        Vector3 to = worldPoint - _cam.transform.position;
        return Vector3.Angle(_cam.transform.forward, to);
    }

    private IEnumerator Run()
    {
        Vector3 head = Vector3.up * 1.6f;

        // Esperar a que la posicion este fuera de vista.
        while (true)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam != null && AngleTo(_figure.transform.position + head) > _viewAngle + 15f) break;
            yield return null;
        }

        _figure.SetActive(true);
        _onAppear?.Invoke();
        Debug.Log("[DistantWatcher] Aparece.");

        float visibleTime = 0f;
        float seenTime = 0f;
        bool seenFired = false;

        while (true)
        {
            if (_cam == null) _cam = Camera.main;
            if (_cam == null) { yield return null; continue; }

            visibleTime += Time.deltaTime;

            Vector3 figPos = _figure.transform.position;
            Vector3 camPos = _cam.transform.position;

            if (_faceCamera)
            {
                Vector3 flat = camPos - figPos;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.0001f)
                    _figure.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
            }

            bool looking = AngleTo(figPos + head) < _viewAngle;
            if (looking)
            {
                seenTime += Time.deltaTime;
                if (!seenFired)
                {
                    seenFired = true;
                    _onSeen?.Invoke();
                }
            }

            Vector3 d = camPos - figPos;
            d.y = 0f;
            bool tooClose = d.magnitude < _vanishDistance;
            bool seenThenAway = seenTime >= _minSeenSeconds && !looking;
            bool timedOut = visibleTime >= _maxVisibleSeconds && !looking;

            if (tooClose || seenThenAway || timedOut)
                break;

            yield return null;
        }

        _figure.SetActive(false);
        _done = true;
        _routine = null;
        _onVanish?.Invoke();
        Debug.Log("[DistantWatcher] Desaparece.");
    }
}
