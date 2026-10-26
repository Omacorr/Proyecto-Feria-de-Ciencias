using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// "Jumpscare" VR-safe: el monstruo aparece de golpe a menos de un metro de la
/// cara del jugador, con un grito, se echa un poco encima durante una fraccion
/// de segundo y desaparece. Se dispara con Trigger() desde un UnityEvent.
///
/// VR Cardboard: NUNCA se rota ni se mueve la camara (el TrackedPoseDriver
/// manda en la rotacion y mover la cabeza por codigo marea). Todo el susto lo
/// hace el monstruo: se coloca delante de la mirada y la sigue con un Lerp
/// rapido para que el jugador no pueda esquivarlo girando la cabeza. El
/// temblor es del monstruo, no de la camara.
///
/// Fotosensibilidad: no hay flashes, ni cambios de luz, ni parpadeos. Solo
/// aparece una figura oscura y suena el grito.
///
/// Si el visual tiene ProceduralWalk, se lo apaga mientras dura el susto (si
/// no, al moverse el monstruo caminaria en el lugar) y se lo vuelve a prender
/// al terminar. Usa tiempo escalado (Time.deltaTime).
/// </summary>
public class Screamer : MonoBehaviour
{
    [Header("Monstruo")]
    [Tooltip("Instancia de Monstruo_Rig que arranca INACTIVA. Se activa en Trigger() y se apaga al terminar.")]
    [SerializeField] private GameObject _visual;

    [Tooltip("Punto de la cara dentro del visual (por ejemplo el hueso Head). Vacio = posicion del visual + 'Face Height'.")]
    [SerializeField] private Transform _faceAnchor;

    [Tooltip("Altura (m) de la cara sobre los pies del visual. Solo se usa si 'Face Anchor' esta vacio.")]
    [SerializeField] private float _faceHeight = 2.2f;

    [Header("Camara y posicion")]
    [Tooltip("Camara del jugador (Main Camera). Vacio = Camera.main.")]
    [SerializeField] private Transform _camera;

    [Tooltip("Distancia (m) delante de la camara a la que queda la cara del monstruo.")]
    [SerializeField] private float _distance = 0.75f;

    [Tooltip("Segundos que dura el susto (con el monstruo delante de la cara).")]
    [SerializeField] private float _holdSeconds = 0.5f;

    [Tooltip("Cuanto (m) se acerca a la camara durante el susto. Rapido al principio y mas lento al final.")]
    [SerializeField] private float _lungeDistance = 0.2f;

    [Tooltip("Temblor leve del monstruo (grados de rotacion). NO se mueve la camara.")]
    [SerializeField] private float _shakeDegrees = 3f;

    [Tooltip("Que tan rapido (1/s) sigue a la mirada si el jugador gira la cabeza. Mas alto = mas pegado.")]
    [SerializeField] private float _followSharpness = 18f;

    [Header("Audio")]
    [Tooltip("Fuente 2D del grito. Vacio = se crea una en este mismo objeto (spatialBlend 0).")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _screamClip;
    [Range(0f, 1f)]
    [SerializeField] private float _volume = 0.85f;

    [Header("Comportamiento")]
    [Tooltip("true = el susto se juega una sola vez por escena.")]
    [SerializeField] private bool _playOnce = true;

    [Tooltip("Bloquea la mirada (gaze) mientras dura, para que no se seleccione nada por accidente.")]
    [SerializeField] private bool _blockGaze = true;

    [Header("Eventos")]
    [Tooltip("Al empezar el grito (por ejemplo para apagar luces o sonidos de ambiente).")]
    [SerializeField] private UnityEvent _onScream;

    [Tooltip("Cuando el monstruo ya desaparecio y se libero la mirada.")]
    [SerializeField] private UnityEvent _onFinished;

    private bool _running;
    private bool _played;
    private Coroutine _routine;
    private ProceduralWalk _walk;
    private bool _walkWasEnabled;

    public bool IsRunning => _running;

    /// <summary>Dispara el susto. Si ya esta corriendo, o ya se jugo y 'Play Once', no hace nada.</summary>
    public void Trigger()
    {
        if (_running || (_playOnce && _played))
        {
            return;
        }
        if (_visual == null)
        {
            Debug.LogWarning("[Screamer] Falta asignar 'Visual'.", this);
            return;
        }
        Transform cam = _camera != null ? _camera : (Camera.main != null ? Camera.main.transform : null);
        if (cam == null)
        {
            Debug.LogWarning("[Screamer] No hay camara.", this);
            return;
        }
        _played = true;
        _routine = StartCoroutine(Run(cam));
    }

    [ContextMenu("Probar screamer (solo en Play)")]
    private void TestInPlay()
    {
        if (!Application.isPlaying)
        {
            Debug.Log("[Screamer] Solo se puede probar en Play.", this);
            return;
        }
        _played = false;
        Trigger();
    }

    private IEnumerator Run(Transform cam)
    {
        _running = true;

        if (_blockGaze)
        {
            GazeController.AddBlocker(this);
            ResetGazeNow();
        }

        _walk = _visual.GetComponentInChildren<ProceduralWalk>(true);
        if (_walk != null)
        {
            _walkWasEnabled = _walk.enabled;
        }

        // Posicion inicial: cara justo delante de la mirada.
        _visual.SetActive(true);
        if (_walk != null)
        {
            _walk.enabled = false;
        }
        Transform root = _visual.transform;
        Vector3 fwd = cam.forward;
        PlaceFace(root, cam.position + fwd * _distance, cam.position);

        PlayScream();
        _onScream?.Invoke();

        float t = 0f;
        float duration = Mathf.Max(0.05f, _holdSeconds);
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            // Ease-out: se echa encima rapido al principio y frena.
            float lunge = _lungeDistance * (1f - (1f - k) * (1f - k));
            float dist = Mathf.Max(0.2f, _distance - lunge);

            Vector3 target = cam.position + cam.forward * dist;
            Vector3 current = FacePosition(root);
            float f = 1f - Mathf.Exp(-_followSharpness * Time.deltaTime);
            Vector3 next = Vector3.Lerp(current, target, f);
            PlaceFace(root, next, cam.position);

            // Temblor leve del monstruo (sobre su rotacion, nunca la camara).
            if (_shakeDegrees > 0f)
            {
                root.rotation *= Quaternion.Euler(
                    Random.Range(-_shakeDegrees, _shakeDegrees),
                    Random.Range(-_shakeDegrees, _shakeDegrees),
                    Random.Range(-_shakeDegrees, _shakeDegrees));
            }
            yield return null;
        }

        Finish();
    }

    // Posicion actual de la cara (mundo).
    private Vector3 FacePosition(Transform root)
    {
        return _faceAnchor != null ? _faceAnchor.position : root.position + Vector3.up * _faceHeight;
    }

    // Gira el root solo en yaw para mirar a la camara y lo mueve para que la
    // cara quede en 'facePoint'.
    private void PlaceFace(Transform root, Vector3 facePoint, Vector3 camPos)
    {
        Vector3 toCam = camPos - facePoint;
        toCam.y = 0f;
        if (toCam.sqrMagnitude > 1e-6f)
        {
            // El modelo mira a +Z.
            root.rotation = Quaternion.LookRotation(toCam.normalized, Vector3.up);
        }
        Vector3 offset = facePoint - FacePosition(root);
        root.position += offset;
    }

    private void PlayScream()
    {
        if (_screamClip == null)
        {
            return;
        }
        if (_audioSource == null)
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
            }
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
        }
        _audioSource.PlayOneShot(_screamClip, _volume);
    }

    private void Finish()
    {
        Cleanup();
        // El clip dura mas que el pantallazo: se corta junto con la imagen
        // (fundido cortisimo para que no haga "clic").
        if (_audioSource != null && _audioSource.isPlaying && isActiveAndEnabled)
        {
            StartCoroutine(CutSound(0.06f));
        }
        _onFinished?.Invoke();
    }

    private IEnumerator CutSound(float seconds)
    {
        float start = _audioSource.volume;
        float t = 0f;
        while (t < seconds)
        {
            t += Time.unscaledDeltaTime;
            _audioSource.volume = Mathf.Lerp(start, 0f, t / seconds);
            yield return null;
        }
        _audioSource.Stop();
        _audioSource.volume = start;
    }

    private void Cleanup()
    {
        _running = false;
        _routine = null;
        if (_visual != null)
        {
            _visual.SetActive(false);
        }
        if (_walk != null)
        {
            _walk.enabled = _walkWasEnabled;
            _walk = null;
        }
        if (_blockGaze)
        {
            GazeController.RemoveBlocker(this);
            ResetGazeNow();
        }
    }

    private static void ResetGazeNow()
    {
        GazeController gaze = FindFirstObjectByType<GazeController>();
        if (gaze != null)
        {
            gaze.ResetGaze();
        }
    }

    private void OnDisable()
    {
        if (_running)
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
            }
            Cleanup();
        }
        else
        {
            GazeController.RemoveBlocker(this);
        }
    }
}
