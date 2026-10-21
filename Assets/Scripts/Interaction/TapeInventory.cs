using UnityEngine;

/// <summary>
/// Cumpleaños (Parte 2): guarda si el jugador ya recogio la cinta de video.
/// Mismo patron que KeyInventory: la cinta en si solo necesita el componente
/// Collectable + Collider + Layer Interactive, y su evento OnCollected se
/// cablea desde el Inspector a CollectTape() de aca. Despues VCRSlot consulta
/// HasTape para dejar (o no) meterla en la tele.
///
/// No es singleton: cada escena que use cinta necesita su propio GameObject con
/// este componente, y VCRSlot tiene que apuntarle a ESE.
/// </summary>
public class TapeInventory : MonoBehaviour
{
    public bool HasTape { get; private set; }

    /// <summary>Llamar desde el evento OnCollected del Collectable de la cinta.</summary>
    public void CollectTape()
    {
        if (HasTape)
        {
            return;
        }
        HasTape = true;
        Debug.Log("[TapeInventory] Cinta recogida.");
    }
}
