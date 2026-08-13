using UnityEngine;

/// <summary>
/// Floor waypoint the player can gaze at to teleport. Requires a Collider and the TeleportNode layer.
/// </summary>
[DisallowMultipleComponent]
public class TeleportNode : GazeTargetBase
{
    [SerializeField]
    [Tooltip("Where the player rig will move to. Defaults to this object's position.")]
    Transform standPoint;

    [SerializeField]
    PlayerLocomotion playerLocomotion;

    [SerializeField]
    [Tooltip("Optional mesh renderer used for visual feedback while gazing.")]
    Renderer nodeRenderer;

    [SerializeField]
    Color idleNodeColor = new Color(0.35f, 0.65f, 1f, 0.35f);

    [SerializeField]
    Color activeNodeColor = new Color(0.35f, 0.65f, 1f, 0.9f);

    Material _runtimeMaterial;
    Color _cachedColor;
    bool _hasCachedColor;

    public Vector3 Destination => standPoint != null ? standPoint.position : transform.position;

    void Reset()
    {
        gameObject.layer = LayerMask.NameToLayer(GazeLayers.TeleportNode);
        dwellDuration = 1.5f;
        nodeRenderer = GetComponent<Renderer>();
    }

    void Awake()
    {
        if (playerLocomotion == null)
        {
            playerLocomotion = FindFirstObjectByType<PlayerLocomotion>();
        }

        CacheNodeColor();
        ApplyNodeColor(idleNodeColor);
    }

    void OnDestroy()
    {
        if (_runtimeMaterial != null)
        {
            Destroy(_runtimeMaterial);
        }
    }

    public override void OnGazeStart()
    {
        ApplyNodeColor(activeNodeColor);
    }

    public override void OnGazeProgress(float normalizedProgress)
    {
        ApplyNodeColor(Color.Lerp(idleNodeColor, activeNodeColor, normalizedProgress));
    }

    public override void OnGazeCancel()
    {
        ApplyNodeColor(idleNodeColor);
    }

    public override void OnGazeComplete()
    {
        ApplyNodeColor(idleNodeColor);

        if (playerLocomotion == null)
        {
            Debug.LogWarning($"TeleportNode '{name}' has no PlayerLocomotion assigned.", this);
            return;
        }

        playerLocomotion.TeleportTo(Destination);
    }

    void CacheNodeColor()
    {
        if (nodeRenderer == null)
        {
            return;
        }

        _runtimeMaterial = nodeRenderer.material;
        _cachedColor = _runtimeMaterial.color;
        _hasCachedColor = true;
    }

    void ApplyNodeColor(Color color)
    {
        if (nodeRenderer == null)
        {
            return;
        }

        if (!_hasCachedColor)
        {
            CacheNodeColor();
        }

        if (_runtimeMaterial != null)
        {
            _runtimeMaterial.color = color;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.35f, 0.65f, 1f, 0.8f);
        Gizmos.DrawWireSphere(Destination, 0.25f);
        Gizmos.DrawLine(transform.position, Destination);
    }
}
