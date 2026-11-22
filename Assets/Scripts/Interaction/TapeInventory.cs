using UnityEngine;

/// <summary>
/// Parte 2: guarda si el jugador ya recogio la cinta de video. Mismo patron
/// que KeyInventory: el Collectable de la cinta cablea OnCollected a
/// CollectTape() y VCRSlot consulta HasTape. No es singleton: cada escena
/// necesita el suyo y VCRSlot tiene que apuntarle a ESE.
/// </summary>
public class TapeInventory : MonoBehaviour
{
    public bool HasTape { get; private set; }

    /// <summary>Llamar desde el evento OnCollected del Collectable de la cinta.</summary>
    public void CollectTape()
    {
        if (HasTape) return;
        HasTape = true;
        Debug.Log("[TapeInventory] Cinta recogida.");
    }
}
