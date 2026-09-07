using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Etapa 6/7: punto de teletransporte predeterminado. Al sostener la mirada
/// el tiempo suficiente, le pide a TeleportManager que mueva al jugador
/// hasta la posicion de este objeto. Debe tener un Collider y estar en la
/// layer Teleport (ademas de Interactive en el LayerMask del GazeController).
///
/// La visibilidad/interactuabilidad de este punto (si esta al alcance, si es
/// el mas cercano, si el jugador esta parado encima) la decide TeleportManager
/// llamando a SetVisible - este script no se auto-oculta por su cuenta.
/// </summary>
public class TeleportPoint : MonoBehaviour, IGazeInteractable
{
    [Tooltip("Manager que ejecuta el movimiento real del jugador.")]
    [SerializeField] private TeleportManager _teleportManager;

    [Tooltip("Material cuando NO se esta mirando el punto.")]
    [SerializeField] private Material _inactiveMaterial;

    [Tooltip("Material cuando SI se esta mirando el punto.")]
    [SerializeField] private Material _gazedAtMaterial;

    [Header("Altura del jugador (opcional)")]
    [Tooltip("Tildado: este punto lleva al jugador a una altura de ojos especifica en vez de mantener la altura actual. Sirve para agacharse y pasar por huecos bajos (o para volver a pararse del otro lado).")]
    [SerializeField] private bool _overridePlayerHeight;

    [Tooltip("Posicion Y (mundial) a la que queda el jugador si 'Override Player Height' esta tildado. Ajustala a ojo probando en Play/Build hasta que pase justo por el hueco.")]
    [SerializeField] private float _targetPlayerHeight;

    [Header("Eventos (opcional)")]
    [Tooltip("Se dispara cada vez que el jugador llega parado a este punto (lo llama TeleportManager apenas termina de moverlo). Util para marcar que el jugador 'ya vio' algo puesto en este lugar - por ejemplo, tp14 (frente a las notas con el codigo) puede cablear esto a CodeClueTracker.MarkSeen().")]
    [SerializeField] private UnityEvent _onPlayerArrived;

    public bool OverridesPlayerHeight => _overridePlayerHeight;
    public float TargetPlayerHeight => _targetPlayerHeight;

    private Renderer _renderer;
    private Collider _collider;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _collider = GetComponent<Collider>();
        SetGazed(false);
    }

    private void OnEnable()
    {
        _teleportManager?.RegisterPoint(this);
    }

    private void OnDisable()
    {
        _teleportManager?.UnregisterPoint(this);
    }

    public void OnGazeEnter()
    {
        SetGazed(true);
    }

    public void OnGazeStay(float progress)
    {
        // El feedback de progreso ya lo muestra GazeReticle.
    }

    public void OnGazeExit()
    {
        SetGazed(false);
    }

    public void OnGazeSelect()
    {
        if (_teleportManager == null)
        {
            Debug.LogWarning($"[TeleportPoint] {gameObject.name} no tiene TeleportManager asignado.");
            return;
        }

        _teleportManager.RequestTeleport(transform.position, this);
    }

    /// <summary>
    /// La llama TeleportManager apenas el jugador termina de llegar parado
    /// a este punto (caminando o con fade). No hace falta llamarla a mano.
    /// </summary>
    public void NotifyArrived()
    {
        _onPlayerArrived?.Invoke();
    }

    /// <summary>
    /// Controla si este punto se ve y se puede seleccionar. Lo decide
    /// TeleportManager en base a distancia y solapamiento con otros puntos.
    /// </summary>
    public void SetVisible(bool visible)
    {
        if (_renderer != null)
        {
            _renderer.enabled = visible;
        }

        if (_collider != null)
        {
            _collider.enabled = visible;
        }

        if (!visible)
        {
            SetGazed(false);
        }
    }

    private void SetGazed(bool gazedAt)
    {
        if (_renderer != null && _inactiveMaterial != null && _gazedAtMaterial != null)
        {
            _renderer.material = gazedAt ? _gazedAtMaterial : _inactiveMaterial;
        }
    }
}
