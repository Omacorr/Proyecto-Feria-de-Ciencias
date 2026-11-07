using UnityEngine;

/// <summary>
/// Guarda si el jugador ya encontro la pista del codigo del candado.
/// Cableado: TeleportPoint de las notas (tp14) -> On Player Arrived ->
/// MarkSeen(); y el mismo objeto en el campo Clue Tracker de ExamineTrigger
/// (BaseLP), que no deja acercarse al candado hasta que HasSeenCode sea true.
/// </summary>
public class CodeClueTracker : MonoBehaviour
{
    public bool HasSeenCode { get; private set; }

    /// <summary>Cablear desde un UnityEvent (por ejemplo TeleportPoint.OnPlayerArrived).</summary>
    public void MarkSeen() => HasSeenCode = true;
}
