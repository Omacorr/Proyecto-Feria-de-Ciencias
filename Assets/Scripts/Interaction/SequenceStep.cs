using UnityEngine;

/// <summary>
/// Mecanica de secuencia (3/3, tras llave y puerta). Un paso individual del
/// puzzle (por ejemplo, uno de 4 pedestales/botones). Se le asigna un
/// Step Index (0 = primero de la secuencia correcta, 1 = segundo, etc.) y
/// al seleccionarlo con la mirada le avisa al SequenceManager.
///
/// Igual que TeleportPoint, se registra solo en el manager al activarse.
/// Necesita Collider + estar en la layer Interactive para que el
/// GazeController lo detecte.
/// </summary>
public class SequenceStep : MonoBehaviour, IGazeInteractable
{
    [Tooltip("Posicion de este paso dentro de la secuencia correcta. El primero de todos es 0.")]
    [SerializeField] private int _stepIndex;

    [SerializeField] private SequenceManager _sequenceManager;

    [Header("Materiales")]
    [Tooltip("Material normal, cuando no se esta mirando y no fue activado.")]
    [SerializeField] private Material _inactiveMaterial;

    [Tooltip("Material mientras se sostiene la mirada sobre el paso.")]
    [SerializeField] private Material _gazedAtMaterial;

    [Tooltip("Material que se queda puesto una vez que este paso fue seleccionado correctamente (hasta que se reinicia la secuencia).")]
    [SerializeField] private Material _activatedMaterial;

    private Renderer _renderer;
    private bool _isActivated;
    private bool _hasReportedThisGaze;

    private void Awake()
    {
        _renderer = GetComponent<Renderer>();
        SetGazed(false);
    }

    private void OnEnable()
    {
        _sequenceManager?.RegisterStep(this);

        // Este objeto vive dentro de GameplayRoot, que GameManager
        // activa/desactiva en cada cambio de estado - o sea que esto se
        // ejecuta de nuevo cada vez que arranca una partida. Nos aseguramos
        // de arrancar "sin marcar", por si esta partida es un reintento
        // despues de haber ganado o perdido antes (si no, un paso que ya
        // habias acertado en la partida anterior quedaba trabado como
        // activado para siempre y dejaba de responder a la mirada).
        _isActivated = false;
        _hasReportedThisGaze = false;
        SetGazed(false);
    }

    private void OnDisable()
    {
        _sequenceManager?.UnregisterStep(this);
    }

    public void OnGazeEnter()
    {
        if (!_isActivated)
        {
            SetGazed(true);
        }
    }

    public void OnGazeStay(float progress)
    {
        // El feedback de progreso ya lo muestra GazeReticle.
    }

    public void OnGazeExit()
    {
        // Al dejar de mirarlo se "rearma" el gatillo: la proxima vez que lo
        // vuelva a mirar y sostener, va a poder reportar de nuevo. Esto es
        // lo que evita el bug de abajo.
        _hasReportedThisGaze = false;

        if (!_isActivated)
        {
            SetGazed(false);
        }
    }

    public void OnGazeSelect()
    {
        // Sin este chequeo, sostener la mirada sigue disparando
        // OnGazeSelect cada vez que se cumple la duracion de seleccion
        // (GazeController resetea su timer y sigue contando mientras el
        // objeto siga siendo el mirado). Eso hacia que, tras elegir mal, con
        // solo mantener la mirada en el mismo paso se siguiera reportando
        // una y otra vez. Ahora cada "mirada sostenida" cuenta una sola vez;
        // hay que apartar la vista y volver a mirar para que cuente otra.
        if (_hasReportedThisGaze)
        {
            return;
        }

        // Un paso ya activado (correcto, en su turno) tampoco debe poder
        // volver a reportarse si el jugador pasa la mirada por el de nuevo
        // mas adelante en la secuencia - evita un "fallo" falso.
        if (_isActivated)
        {
            return;
        }

        if (_sequenceManager == null)
        {
            Debug.LogWarning($"[SequenceStep] {gameObject.name} no tiene SequenceManager asignado.");
            return;
        }

        _hasReportedThisGaze = true;
        _sequenceManager.ReportStep(this, _stepIndex);
    }

    /// <summary>
    /// Llamado por SequenceManager cuando este paso fue el correcto.
    /// </summary>
    public void MarkActivated()
    {
        _isActivated = true;

        if (_renderer != null && _activatedMaterial != null)
        {
            _renderer.material = _activatedMaterial;
        }
    }

    /// <summary>
    /// Llamado por SequenceManager al reiniciar la secuencia (ya sea por un
    /// error o manualmente).
    /// </summary>
    public void ResetVisual()
    {
        _isActivated = false;
        SetGazed(false);
    }

    private void SetGazed(bool gazedAt)
    {
        if (_renderer != null && _inactiveMaterial != null && _gazedAtMaterial != null)
        {
            _renderer.material = gazedAt ? _gazedAtMaterial : _inactiveMaterial;
        }
    }
}
