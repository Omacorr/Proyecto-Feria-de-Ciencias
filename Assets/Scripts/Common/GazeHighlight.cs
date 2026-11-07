using UnityEngine;

/// <summary>
/// Cambio de material al mirar un objeto. Lo comparten todos los
/// IGazeInteractable que tienen "Inactive Material" / "Gazed At Material".
/// No hace nada si falta el renderer o alguno de los dos materiales.
/// </summary>
public static class GazeHighlight
{
    public static void Swap(Renderer renderer, Material inactive, Material gazedAt, bool isGazed)
    {
        if (renderer != null && inactive != null && gazedAt != null)
        {
            renderer.material = isGazed ? gazedAt : inactive;
        }
    }
}
