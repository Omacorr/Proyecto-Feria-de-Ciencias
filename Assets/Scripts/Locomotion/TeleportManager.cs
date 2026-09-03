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

    private readonly List<TeleportPoint> _registeredPoints = new List<TeleportPoint>();
    private TeleportPoint _currentOccupiedPoint;

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

        if (_cameraTransform == null)
        {
            Debug.LogWarning("[TeleportManager] Falta asignar Camera Transform en el Inspector (tiene que ser Main Camera, no Player).");
            return;
        }

        if (_useWalkAnimation)
        {
            StartCoroutine(DoWalk(destination, sourcePoint));
        }
        else
        {
            StartCoroutine(DoFadeTeleport(destination, sourcePoint));
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

        sourcePoint.SetVisible(false);
        _currentOccupiedPoint = sourcePoint;
        sourcePoint.NotifyArrived();

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
        if (_currentOccupiedPoint != null)
        {
            _currentOccupiedPoint.SetVisible(true);
        }

        Vector3 current = _cameraTransform.position;
        float targetY = sourcePoint.OverridesPlayerHeight ? sourcePoint.TargetPlayerHeight : current.y;
        _cameraTransform.position = new Vector3(destination.x, targetY, destination.z);

        Debug.Log($"[TeleportManager] Teletransportado a {_cameraTransform.position}");

        sourcePoint.SetVisible(false);
        _currentOccupiedPoint = sourcePoint;
        sourcePoint.NotifyArrived();
    }
}
