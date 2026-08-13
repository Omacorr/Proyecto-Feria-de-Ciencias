using UnityEngine;

/// <summary>
/// Layer names used by the gaze interaction system.
/// Configure these in Edit &gt; Project Settings &gt; Tags and Layers.
/// </summary>
public static class GazeLayers
{
    public const string Interactable = "Interactable";
    public const string TeleportNode = "TeleportNode";

    public static int InteractableMask => LayerMask.GetMask(Interactable);

    public static int TeleportNodeMask => LayerMask.GetMask(TeleportNode);

    public static int GazeMask => LayerMask.GetMask(Interactable, TeleportNode);
}
