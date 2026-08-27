using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Etapa 6/7: centraliza el movimiento del jugador entre puntos de
/// teletransporte, con fundido a negro (VRFadeController) para evitar el
/// salto brusco. Todos los TeleportPoint registrados quedan visibles siempre,
/// excepto el que el jugador esta pisando en este momento (para que no se
/// vea la camara metida adentro del marcador).
/// </summary>
public class TeleportManager : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Transform raiz del jugador (el objeto 'Player'), no la camara.")]
    [SerializeField] private Transform _playerTransform;

    [Tooltip("Opcional. Si se asigna, se usa para el fundido a negro durante el teletransporte.")]
    [SerializeField] private VRFadeController _fadeController;

    public bool IsTeleporting { get; private set; }
    public Vector3 PlayerPosition => _playerTransform != null ? _playerTransform.position : Vector3.zero;

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
            Debug.Log("[TeleportManager] Solicitud ignorada, ya se esta teletransportando.");
            return;
        }

        if (_playerTransform == null)
        {
            Debug.LogWarning("[TeleportManager] Falta asignar Player Transform en el Inspector.");
            return;
        }

        StartCoroutine(DoTeleport(destination, sourcePoint));
    }

    private IEnumerator DoTeleport(Vector3 destination, TeleportPoint sourcePoint)
    {
        IsTeleporting = true;

        if (_fadeController != null)
        {
            yield return StartCoroutine(_fadeController.FadeOutAndIn(() => MovePlayer(destination, sourcePoint)));
        }
        else
        {
            MovePlayer(destination, sourcePoint);
        }

        IsTeleporting = false;
    }

    private void MovePlayer(Vector3 destination, TeleportPoint sourcePoint)
    {
        // Vuelve a habilitar el punto anterior (si habia uno) - ahora se
        // puede volver a mirar y seleccionar para teletransportarse de nuevo.
        if (_currentOccupiedPoint != null)
        {
            _currentOccupiedPoint.SetVisible(true);
        }

        // Solo cambia X/Z. La altura de los ojos del jugador no depende de
        // a que altura este puesto el marcador de destino.
        Vector3 current = _playerTransform.position;
        _playerTransform.position = new Vector3(destination.x, current.y, destination.z);

        Debug.Log($"[TeleportManager] Teletransportado a {_playerTransform.position}");

        // Oculta el punto nuevo, porque el jugador esta parado ahi.
        sourcePoint.SetVisible(false);
        _currentOccupiedPoint = sourcePoint;
    }
}
