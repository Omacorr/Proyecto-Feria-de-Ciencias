using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Centraliza el movimiento del jugador entre puntos de teletransporte.
/// Soporta dos modos, elegibles desde el Inspector:
///  - Caminata (por defecto): se desplaza gradualmente hacia el destino a
///    velocidad constante, con sonido de pasos en loop mientras camina.
///  - Fade: fundido a negro, salto instantaneo, fundido de vuelta (mas comodo
///    para VR, queda disponible como alternativa si el caminar marea a
///    alguien probando la app).
/// En ambos casos, todos los TeleportPoint quedan visibles siempre, excepto
/// el que el jugador esta pisando en este momento.
/// </summary>
public class TeleportManager : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Transform de Main Camera (NO el objeto 'Player'). Desde que Main Camera se separo de ser hijo de Player (fix del profesor para el drift del punto de mira), es la camara la que hay que mover para que el jugador vea que se desplazo - moverla a ella es lo unico que hace que la vista cambie de lugar. Player deja de moverse desde aca: en cambio, sigue solo por su cuenta a la camara (ver PlayerFollowsCamera, puesto en el objeto Player). ARRASTRA MAIN CAMERA ACA, no Player.")]
    [SerializeField] private Transform _cameraTransform;

    [Header("Modo de movimiento")]
    [Tooltip("Tildado: camina gradualmente hacia el destino. Destildado: usa el fade (instantaneo, mas comodo).")]
    [SerializeField] private bool _useWalkAnimation = true;

    [Header("Caminata")]
    [Tooltip("Velocidad de desplazamiento, en metros por segundo.")]
    [SerializeField] private float _walkSpeed = 2f;

    [Tooltip("Opcional. AudioSource que reproduce el sonido de pasos en loop mientras camina.")]
    [SerializeField] private AudioSource _footstepsAudioSource;

    [Header("Fade (alternativa)")]
    [Tooltip("Opcional. Se usa solo si 'Use Walk Animation' esta destildado.")]
    [SerializeField] private VRFadeController _fadeController;

    public bool IsTeleporting { get; private set; }
    public Vector3 PlayerPosition => _cameraTransform != null ? _cameraTransform.position : Vector3.zero;

    /// <summary>
    /// El TeleportPoint que el jugador esta pisando ahora mismo (o null si
    /// nunca piso ninguno). Pensado para que otros scripts (por ejemplo
    /// ExamineTrigger) puedan guardarlo antes de mandar al jugador a otro
    /// lado, y volver ahi despues.
    /// </summary>
    public TeleportPoint CurrentOccupiedPoint => _currentOccupiedPoint;

    /// <summary>
    /// True mientras el movimiento este bloqueado con LockMovement() (caida al
    /// abismo, monstruo que te agarra, desmayo...). Mientras tanto se ignoran
    /// los pedidos de teletransporte y se corta una caminata en curso.
    /// </summary>
    public bool IsMovementLocked => _movementLocked;

    private readonly List<TeleportPoint> _registeredPoints = new List<TeleportPoint>();
    private TeleportPoint _currentOccupiedPoint;
    private bool _movementLocked;

    /// <summary>
    /// Bloquea el teletransporte: se ignoran pedidos nuevos y, si el jugador
    /// esta caminando, se frena ahi mismo (sin disparar On Player Arrived). NO
    /// corta un fade que ya este en curso (terminaria dejando la pantalla en
    /// negro). Llamable desde un UnityEvent. Lo usan AbyssFall y
    /// MonsterController para que ninguna caminata pise el movimiento de la
    /// camara que hacen ellos.
    /// </summary>
    public void LockMovement()
    {
        _movementLocked = true;
    }

    /// <summary>Deshace LockMovement(). Llamable desde un UnityEvent.</summary>
    public void UnlockMovement()
    {
        _movementLocked = false;
    }

    public void RegisterPoint(TeleportPoint point)
    {
        if (!_registeredPoints.Contains(point))
        {
            _registeredPoints.Add(point);
        }

        // Por defecto, todo punto que se registra arranca visible (salvo que
        // resulte ser el que el jugador ya esta pisando).
        point.SetVisible(point != _currentOccupiedPoint);
    }

    public void UnregisterPoint(TeleportPoint point)
    {
        _registeredPoints.Remove(point);
    }

    public void RequestTeleport(Vector3 destination, TeleportPoint sourcePoint)
    {
        if (IsTeleporting)
        {
            Debug.Log("[TeleportManager] Solicitud ignorada, ya se esta moviendo.");
            return;
        }

        if (_movementLocked)
        {
            Debug.Log("[TeleportManager] Solicitud ignorada, el movimiento esta bloqueado (LockMovement).");
            return;
        }

        if (sourcePoint == null)
        {
            Debug.LogWarning("[TeleportManager] RequestTeleport sin TeleportPoint de origen, se ignora.");
            return;
        }

        if (_cameraTransform == null)
        {
            Debug.LogWarning("[TeleportManager] Falta asignar Camera Transform en el Inspector (tiene que ser Main Camera, no Player).");
            return;
        }

        // Los puntos que redirigen a otro objetivo (Destination Override, por
        // ejemplo puertas) siempre usan fade, sin importar el modo general de
        // la escena: caminar hacia un destino que no es el punto que se esta
        // mirando queda confuso. El resto de los puntos respeta "Use Walk
        // Animation" como siempre. Excepcion: los puntos con Seamless Loop
        // (UsesFadeTransition da false) caminan hasta si mismos y saltan sin
        // fade al llegar (ver CompleteArrival).
        bool useFade = !_useWalkAnimation || sourcePoint.UsesFadeTransition;

        if (useFade)
        {
            StartCoroutine(DoFadeTeleport(destination, sourcePoint));
        }
        else
        {
            StartCoroutine(DoWalk(destination, sourcePoint));
        }
    }

    private IEnumerator DoWalk(Vector3 destination, TeleportPoint sourcePoint)
    {
        IsTeleporting = true;

        // Libera el punto anterior apenas arranca a caminar (asi se puede
        // volver a mirar mientras te alejas).
        if (_currentOccupiedPoint != null)
        {
            _currentOccupiedPoint.SetVisible(true);
        }

        Vector3 startPos = _cameraTransform.position;
        // Por defecto solo cambia X/Z y mantiene la altura de ojos actual.
        // Si este punto en particular pide una altura especifica (agacharse
        // para pasar por un hueco, o pararse de nuevo del otro lado), se usa
        // esa en cambio.
        float targetY = sourcePoint.OverridesPlayerHeight ? sourcePoint.TargetPlayerHeight : startPos.y;
        Vector3 targetPos = new Vector3(destination.x, targetY, destination.z);

        float distance = Vector3.Distance(startPos, targetPos);
        float duration = _walkSpeed > 0f ? distance / _walkSpeed : 0f;

        if (_footstepsAudioSource != null && duration > 0f)
        {
            _footstepsAudioSource.loop = true;
            _footstepsAudioSource.Play();
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (_movementLocked)
            {
                // Otro sistema (caida, monstruo) tomo el control de la camara:
                // frenar aca, sin pisarle la posicion ni marcar llegada.
                if (_footstepsAudioSource != null)
                {
                    _footstepsAudioSource.Stop();
                }
                _currentOccupiedPoint = null;
                Debug.Log("[TeleportManager] Caminata cortada por LockMovement.");
                IsTeleporting = false;
                yield break;
            }

            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _cameraTransform.position = Vector3.Lerp(startPos, targetPos, t);
            yield return null;
        }

        _cameraTransform.position = targetPos;

        if (_footstepsAudioSource != null)
        {
            _footstepsAudioSource.Stop();
        }

        Debug.Log($"[TeleportManager] Llego caminando a {_cameraTransform.position}");

        CompleteArrival(sourcePoint, startPos);

        IsTeleporting = false;
    }

    private IEnumerator DoFadeTeleport(Vector3 destination, TeleportPoint sourcePoint)
    {
        IsTeleporting = true;

        if (_fadeController != null)
        {
            yield return StartCoroutine(_fadeController.FadeOutAndIn(() => MoveInstant(destination, sourcePoint)));
        }
        else
        {
            MoveInstant(destination, sourcePoint);
        }

        IsTeleporting = false;
    }

    private void MoveInstant(Vector3 destination, TeleportPoint sourcePoint)
    {
        if (_movementLocked)
        {
            // Se bloqueo el movimiento durante el fundido: no mover la camara
            // (el fundido igual termina, para no dejar la pantalla en negro).
            return;
        }

        if (_currentOccupiedPoint != null)
        {
            _currentOccupiedPoint.SetVisible(true);
        }

        Vector3 current = _cameraTransform.position;
        float targetY = sourcePoint.OverridesPlayerHeight ? sourcePoint.TargetPlayerHeight : current.y;
        _cameraTransform.position = new Vector3(destination.x, targetY, destination.z);

        Debug.Log($"[TeleportManager] Teletransportado a {_cameraTransform.position}");

        CompleteArrival(sourcePoint, current);
    }

    /// <summary>
    /// Cierre comun de caminata y fade: marca el punto como ocupado y dispara
    /// su On Player Arrived. Si el punto es un loop sin fade (Seamless Loop) y
    /// el jugador llego avanzando en el sentido del loop, en cambio lo pasa de
    /// golpe a Destination Override (ver DoSeamlessLoop).
    /// </summary>
    private void CompleteArrival(TeleportPoint sourcePoint, Vector3 startPos)
    {
        if (sourcePoint.IsSeamlessLoop && ShouldLoop(sourcePoint, startPos))
        {
            DoSeamlessLoop(sourcePoint);
            return;
        }

        sourcePoint.SetVisible(false);
        _currentOccupiedPoint = sourcePoint;
        sourcePoint.NotifyArrived();
    }

    private static bool ShouldLoop(TeleportPoint sourcePoint, Vector3 startPos)
    {
        if (!sourcePoint.LoopOnlyWhenMovingAway)
        {
            return true;
        }

        // Solo en XZ: la altura de ojos no cuenta como "avanzar".
        Vector3 travel = sourcePoint.transform.position - startPos;
        Vector3 loopDirection = sourcePoint.transform.position - sourcePoint.LoopDestination.position;
        travel.y = 0f;
        loopDirection.y = 0f;
        if (travel.sqrMagnitude < 0.0001f || loopDirection.sqrMagnitude < 0.0001f)
        {
            return false;
        }

        // Positivo = el jugador venia alejandose del destino del loop, o sea
        // "avanzando hacia adelante": ese es el caso en que lo devolvemos.
        return Vector3.Dot(travel, loopDirection) > 0f;
    }

    /// <summary>
    /// Loop de Parte 4 ("caminar hacia adelante te devuelve al mismo punto"):
    /// el jugador ya llego caminando a sourcePoint; se lo pasa en el mismo
    /// frame, sin fade, a la posicion X/Z de su Destination Override. La altura
    /// de ojos no cambia (ya quedo resuelta por la caminata). La rotacion
    /// tampoco (la maneja el casco), por eso el destino tiene que mirar en la
    /// misma direccion que el origen para que el salto no se note.
    /// </summary>
    private void DoSeamlessLoop(TeleportPoint sourcePoint)
    {
        Transform destination = sourcePoint.LoopDestination;
        Vector3 current = _cameraTransform.position;
        _cameraTransform.position = new Vector3(destination.position.x, current.y, destination.position.z);

        Debug.Log($"[TeleportManager] Loop sin fade: {sourcePoint.name} -> {destination.name} ({_cameraTransform.position})");

        // El punto del loop vuelve a quedar "adelante" y seleccionable.
        sourcePoint.SetVisible(true);

        // Si el destino es otro TeleportPoint, el jugador queda parado ahi.
        TeleportPoint arrivalPoint = destination.GetComponent<TeleportPoint>();
        if (arrivalPoint != null && arrivalPoint.isActiveAndEnabled)
        {
            arrivalPoint.SetVisible(false);
            _currentOccupiedPoint = arrivalPoint;
        }
        else
        {
            _currentOccupiedPoint = null;
        }

        // El jugador probablemente sigue mirando el mismo punto del loop: sin
        // esto, el timer de la mirada seguia corriendo desde la caminata y lo
        // volvia a seleccionar solo.
        foreach (GazeController gaze in FindObjectsByType<GazeController>(FindObjectsSortMode.None))
        {
            gaze.ResetGaze();
        }

        sourcePoint.NotifyLooped();
        if (_currentOccupiedPoint != null)
        {
            _currentOccupiedPoint.NotifyArrived();
        }
    }
}
