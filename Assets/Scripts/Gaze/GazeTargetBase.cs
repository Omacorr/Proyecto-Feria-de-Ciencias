using UnityEngine;

/// <summary>
/// Base component for gaze targets. Requires a Collider on the same object or a child.
/// </summary>
public abstract class GazeTargetBase : MonoBehaviour, IGazeTarget
{
    [SerializeField]
    [Tooltip("Seconds the player must look at this object to trigger it.")]
    protected float dwellDuration = 1.75f;

    [SerializeField]
    [Tooltip("Uncheck to temporarily disable gaze interaction.")]
    protected bool isAvailable = true;

    public float DwellDuration => dwellDuration;

    public bool IsAvailable => isAvailable && enabled && gameObject.activeInHierarchy;

    public virtual void OnGazeStart() { }

    public virtual void OnGazeProgress(float normalizedProgress) { }

    public virtual void OnGazeCancel() { }

    public abstract void OnGazeComplete();
}
