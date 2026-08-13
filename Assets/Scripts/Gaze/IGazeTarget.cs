/// <summary>
/// Implemented by objects that respond to sustained gaze (dwell time).
/// </summary>
public interface IGazeTarget
{
    /// <summary>Seconds the player must look at this target to trigger it.</summary>
    float DwellDuration { get; }

    /// <summary>Whether this target can currently be selected.</summary>
    bool IsAvailable { get; }

    /// <summary>Called when gaze first lands on this target.</summary>
    void OnGazeStart();

    /// <summary>Called every frame while gazing. Normalized progress is in [0, 1].</summary>
    void OnGazeProgress(float normalizedProgress);

    /// <summary>Called when gaze leaves this target before completion.</summary>
    void OnGazeCancel();

    /// <summary>Called when dwell time completes.</summary>
    void OnGazeComplete();
}
