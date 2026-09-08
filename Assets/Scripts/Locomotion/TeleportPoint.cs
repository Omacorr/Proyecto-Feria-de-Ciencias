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

    [Header("Destino (opcional)")]
    [Tooltip("Si se asigna, mirar este punto teletransporta a la posicion de ESTE Transform en vez de a la posicion propia. Util para puertas: la puerta se queda quieta donde se ve bien, pero el destino real es otro punto (por ejemplo, un Cube vacio puesto del otro lado de la puerta). Dejalo vacio para el comportamiento normal (moverse a la posicion de este mismo objeto).")]
    [SerializeField] private Transform _destinationOverride;

    [Header("Eventos (opcional)")]
    [Tooltip("Se dispara cada vez que el jugador llega parado a este punto (lo llama TeleportManager apenas termina de moverlo). Util para marcar que el jugador 'ya vio' algo puesto en este lugar - por ejemplo, tp14 (frente a las notas con el codigo) puede cablear esto a CodeClueTracker.MarkSeen().")]
    [SerializeField] private UnityEvent _onPlayerArrived;

    public bool OverridesPlayerHeight => _overridePlayerHeight;
    public float TargetPlayerHeight => _targetPlayerHeight;

    /// <summary>
    /// True si este punto redirige a otro objetivo (Destination Override
    /// asignado) en vez de llevar al jugador a su propia posicion. TeleportManager
    /// usa esto para forzar el fade a negro en estos casos puntuales (puertas,
    /// etc.), sin importar si "Use Walk Animation" esta tildado para el resto
    /// de la escena - caminar hacia un destino que no es visualmente el mismo
    /// punto que se esta mirando queda raro.
    /// </summary>
    public bool UsesFadeTransition => _destinationOverride != null;

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

        Vector3 destination = _destinationOverride != null ? _destinationOverride.position : transform.position;
        _teleportManager.RequestTeleport(destination, this);
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
