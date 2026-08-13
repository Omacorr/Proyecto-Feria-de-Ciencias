using UnityEngine;

/// <summary>
/// Simple test action for GazeInteractable. Useful while VR hardware is unavailable.
/// </summary>
[DisallowMultipleComponent]
public class GazeInteractableDebugAction : MonoBehaviour
{
    [SerializeField]
    string message = "Interaccion completada";

    [SerializeField]
    Light optionalLight;

    [SerializeField]
    bool toggleLight;

    public void Execute()
    {
        Debug.Log($"[GazeInteractable] {message}", this);

        if (toggleLight && optionalLight != null)
        {
            optionalLight.enabled = !optionalLight.enabled;
        }
    }
}
