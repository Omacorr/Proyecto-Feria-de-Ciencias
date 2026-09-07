using UnityEngine;

/// <summary>
/// Mantiene a Player (X/Z) parado en el mismo lugar que Main Camera.
///
/// Desde que Main Camera se separo de ser hijo de Player (fix del profesor
/// para el drift del punto de mira), quedaron dos objetos independientes:
/// antes, mover Player tambien movia a la camara porque era su hijo - ahora
/// no. Se invirtio quien manda: TeleportManager mueve directamente a Main
/// Camera (es el unico cambio que hace que el jugador vea que se desplazo),
/// y este script hace que Player la siga, para que su posicion seguida
/// represente donde esta parado el jugador realmente (por si algun script
/// futuro necesita leer Player.position, por ejemplo para audio espacial).
///
/// Solo copia X/Z, no la altura (Y): Player mantiene su propia altura de
/// piso fija, mientras que la altura de Main Camera es la de los ojos y
/// puede cambiar sola (por ejemplo al agacharse, en un TeleportPoint con
/// Overrides Player Height tildado). Tampoco copia rotacion - Player no
/// necesita girar con la cabeza, ningun script depende de eso.
///
/// Va en el objeto "Player" (la raiz del prefab). Corre en LateUpdate para
/// asegurarse de que ya paso el Update de este mismo frame de
/// TrackedPoseDriver (el que mueve a Main Camera segun el sensor del
/// casco) y de TeleportManager (que la mueve al teletransportar) antes de
/// copiar su posicion - asi Player nunca queda un frame atrasado.
/// </summary>
public class PlayerFollowsCamera : MonoBehaviour
{
    [Tooltip("La Main Camera del jugador (la misma que ya usan GazeController y TeleportManager).")]
    [SerializeField] private Transform _mainCamera;

    private void LateUpdate()
    {
        if (_mainCamera == null)
        {
            return;
        }

        Vector3 position = transform.position;
        position.x = _mainCamera.position.x;
        position.z = _mainCamera.position.z;
        transform.position = position;
    }
}
