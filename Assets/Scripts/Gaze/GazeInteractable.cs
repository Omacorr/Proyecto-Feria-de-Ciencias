using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Marks an object as interactable via sustained gaze. Requires a Collider and the Interactable layer.
/// </summary>
[DisallowMultipleComponent]
public class GazeInteractable : GazeTargetBase
{
    [SerializeField]
    UnityEvent onInteract = new UnityEvent();

    [SerializeField]
    [Tooltip("If enabled, this object can only be activated once.")]
    bool interactOnce;

    [SerializeField]
    [Tooltip("Optional renderer to highlight while the player is looking at this object.")]
    Renderer highlightRenderer;

    [SerializeField]
    Color highlightColor = new Color(0.2f, 0.95f, 0.55f, 1f);

    bool _hasInteracted;
    Color _originalColor;
    Material _runtimeMaterial;
    bool _hasOriginalColor;

    public UnityEvent OnInteract => onInteract;

    void Reset()
    {
        gameObject.layer = LayerMask.NameToLayer(GazeLayers.Interactable);
        highlightRenderer = GetComponent<Renderer>();
    }

    void Awake()
    {
        CacheOriginalColor();
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
        ApplyHighlight(true);
    }

    public override void OnGazeProgress(float normalizedProgress)
    {
        ApplyHighlight(true);
    }

    public override void OnGazeCancel()
    {
        ApplyHighlight(false);
    }

    public override void OnGazeComplete()
    {
        if (interactOnce && _hasInteracted)
        {
            return;
        }

        _hasInteracted = true;
        ApplyHighlight(false);
        onInteract.Invoke();
    }

    void CacheOriginalColor()
    {
        if (highlightRenderer == null)
        {
            return;
        }

        _runtimeMaterial = highlightRenderer.material;
        _originalColor = _runtimeMaterial.color;
        _hasOriginalColor = true;
    }

    void ApplyHighlight(bool enabled)
    {
        if (highlightRenderer == null)
        {
            return;
        }

        if (!_hasOriginalColor)
        {
            CacheOriginalColor();
        }

        if (_runtimeMaterial == null)
        {
            return;
        }

        _runtimeMaterial.color = enabled ? highlightColor : _originalColor;
    }
}
