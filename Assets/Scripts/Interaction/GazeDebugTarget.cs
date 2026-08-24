using UnityEngine;

/// <summary>
/// Implementacion minima de IGazeInteractable, solo para validar por consola
/// que el GazeController dispara bien los 4 estados (etapa 3). En la etapa 4
/// esto se reemplaza por InteractiveObject / Collectable con comportamiento real.
/// Poner este script en el mismo GameObject que ya tiene el Collider en layer
/// Interactive (por ejemplo, Icosahedron).
/// </summary>
public class GazeDebugTarget : MonoBehaviour, IGazeInteractable
{
    // Para no inundar el logcat, solo logueamos el progreso cada 25%.
    private int _lastLoggedStep = -1;

    public void OnGazeEnter()
    {
        _lastLoggedStep = -1;
        Debug.Log($"[GazeState] ENTER: {gameObject.name}");
    }

    public void OnGazeStay(float progress)
    {
        int step = Mathf.FloorToInt(progress * 4f); // 0,1,2,3,4 (25% cada uno)
        if (step != _lastLoggedStep)
        {
            _lastLoggedStep = step;
            Debug.Log($"[GazeState] STAY: {gameObject.name} progreso={progress:P0}");
        }
    }

    public void OnGazeExit()
    {
        Debug.Log($"[GazeState] EXIT: {gameObject.name}");
    }

    public void OnGazeSelect()
    {
        Debug.Log($"[GazeState] SELECT: {gameObject.name}");
    }
}
