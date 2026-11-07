using UnityEngine;

/// <summary>
/// Guarda si el jugador ya recogio la llave. La llave en si solo lleva un
/// Collectable cuyo OnCollected se cablea a CollectKey(); Door/LevelDoor
/// consultan HasKey. No es singleton: cada escena necesita el suyo.
/// </summary>
public class KeyInventory : MonoBehaviour
{
    public bool HasKey { get; private set; }

    /// <summary>Llamar desde el evento OnCollected del Collectable de la llave.</summary>
    public void CollectKey()
    {
        if (HasKey) return;
        HasKey = true;
        Debug.Log("[KeyInventory] Llave recogida.");
    }
}
