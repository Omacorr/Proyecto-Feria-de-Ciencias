using UnityEngine;

/// <summary>
/// Mecanica de llave y puerta (1/3). Guarda si el jugador ya recogio la
/// llave. No hace falta un script nuevo para la llave en si: se le pone el
/// componente Collectable de siempre (el mismo que usa TriSphere) y su
/// evento OnCollected se conecta desde el Inspector a CollectKey() aca.
/// Despues, Door consulta HasKey para decidir si se abre o no.
/// </summary>
public class KeyInventory : MonoBehaviour
{
    public bool HasKey { get; private set; }

    /// <summary>
    /// Llamar desde el evento OnCollected del Collectable de la llave.
    /// </summary>
    public void CollectKey()
    {
        if (HasKey)
        {
            return;
        }

        HasKey = true;
        Debug.Log("[KeyInventory] Llave recogida.");
    }
}
