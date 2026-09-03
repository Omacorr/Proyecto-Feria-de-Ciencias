using UnityEngine;

/// <summary>
/// Guarda si el jugador ya encontro la pista con el codigo del candado (por
/// ejemplo, piso el TeleportPoint de enfrente de las notas). Objeto simple
/// y separado para que cualquier script (por ahora, ExamineTrigger) pueda
/// consultarlo sin acoplarse a TeleportManager para esto en particular.
///
/// Cableo tipico:
///  - En el TeleportPoint de las notas (tp14), agregarle una fila a su
///    evento On Player Arrived -> arrastrar el objeto que tenga este
///    script -> Code Clue Tracker -> MarkSeen().
///  - En BaseLP, arrastrar ese mismo objeto al campo Clue Tracker de
///    Examine Trigger, para que no deje acercarse al candado hasta que
///    HasSeenCode sea true.
/// </summary>
public class CodeClueTracker : MonoBehaviour
{
    public bool HasSeenCode { get; private set; }

    /// <summary>Cablear desde un UnityEvent (por ejemplo TeleportPoint.OnPlayerArrived).</summary>
    public void MarkSeen()
    {
        HasSeenCode = true;
    }
}
