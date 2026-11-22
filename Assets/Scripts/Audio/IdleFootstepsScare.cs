using UnityEngine;

/// <summary>
/// Susto por quedarse quieto: si el jugador lleva un rato parado en el mismo
/// TeleportPoint (sin teletransportarse), suenan pasos a sus espaldas via
/// ExamineScareSounds.PlayFootstepsNow(). Despues hay un enfriamiento antes de
/// poder repetirlo. El contador se reinicia cada vez que el jugador se mueve.
///
/// Se cablea en la escena sin codigo: asignar el TeleportManager y un
/// ExamineScareSounds (con el clip de pasos). Opcional: limitar a ciertos
/// puntos con Room Points (por ejemplo los de una habitacion).
/// </summary>
public class IdleFootstepsScare : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("TeleportManager del jugador. Vacio = se busca uno en la escena.")]
    [SerializeField] private TeleportManager _teleportManager;

    [Tooltip("ExamineScareSounds que reproduce los pasos (tiene que tener Footsteps Clip).")]
    [SerializeField] private ExamineScareSounds _scares;

    [Header("Donde aplica")]
    [Tooltip("Puntos donde puede dispararse el susto. Vacio = en cualquier punto.")]
    [SerializeField] private TeleportPoint[] _roomPoints;

    [Header("Ritmo")]
    [Tooltip("Segundos quieto antes del susto (min/max, al azar).")]
    [SerializeField] private Vector2 _idleSeconds = new Vector2(12f, 20f);

    [Tooltip("Enfriamiento tras el susto, antes de volver a armarse (min/max, al azar).")]
    [SerializeField] private Vector2 _cooldownSeconds = new Vector2(18f, 30f);

    [Header("Estado")]
    [Tooltip("Tildado: arranca activo al cargar la escena. Destildado: hay que llamar a Enable() desde un evento.")]
    [SerializeField] private bool _enabledAtStart = true;

    private bool _active;
    private TeleportPoint _lastPoint;
    private float _idleTimer;
    private float _idleTarget;
    private float _cooldownTimer;

    private void Start()
    {
        if (_teleportManager == null)
        {
            _teleportManager = FindFirstObjectByType<TeleportManager>();
        }
        _active = _enabledAtStart;
        ResetIdle();
    }

    /// <summary>Activa el susto (llamable desde un UnityEvent).</summary>
    public void Enable()
    {
        _active = true;
        ResetIdle();
    }

    /// <summary>Desactiva el susto (llamable desde un UnityEvent).</summary>
    public void Disable()
    {
        _active = false;
    }

    private void ResetIdle()
    {
        _idleTimer = 0f;
        _idleTarget = Random.Range(_idleSeconds.x, Mathf.Max(_idleSeconds.x, _idleSeconds.y));
        _lastPoint = _teleportManager != null ? _teleportManager.CurrentOccupiedPoint : null;
    }

    private void Update()
    {
        if (!_active || _teleportManager == null || _scares == null)
        {
            return;
        }

        float dt = Time.deltaTime;
        if (_cooldownTimer > 0f)
        {
            _cooldownTimer -= dt;
        }

        TeleportPoint current = _teleportManager.CurrentOccupiedPoint;
        if (_teleportManager.IsTeleporting || current != _lastPoint)
        {
            // Se movio: vuelve a contar desde cero.
            ResetIdle();
            return;
        }

        if (!IsRoomPoint(current))
        {
            _idleTimer = 0f;
            return;
        }

        _idleTimer += dt;
        if (_idleTimer >= _idleTarget && _cooldownTimer <= 0f)
        {
            _scares.PlayFootstepsNow();
            _cooldownTimer = Random.Range(_cooldownSeconds.x, Mathf.Max(_cooldownSeconds.x, _cooldownSeconds.y));
            ResetIdle();
        }
    }

    private bool IsRoomPoint(TeleportPoint p)
    {
        if (_roomPoints == null || _roomPoints.Length == 0)
        {
            return true;
        }
        if (p == null)
        {
            return false;
        }
        foreach (TeleportPoint rp in _roomPoints)
        {
            if (rp == p)
            {
                return true;
            }
        }
        return false;
    }
}
