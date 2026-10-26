using UnityEngine;

/// <summary>
/// Hace que la rotacion de la linterna se demore un poco en seguir hacia
/// donde mira el jugador, en vez de girar instantaneamente pegada a la
/// camara - efecto "sway" de sostenerla con la mano. La posicion sigue
/// rigida (este objeto sigue siendo hijo de Main Camera, se mueve con ella
/// normalmente); solo la ROTACION se suaviza en LateUpdate, despues de que
/// el TrackedPoseDriver ya roto la camara este mismo frame.
///
/// La rotacion LOCAL respecto al padre que tenia este objeto al arrancar
/// (Awake) queda guardada como el "offset de apuntado" fijo (normalmente
/// identidad, apuntando derecho hacia adelante igual que la camara) - a
/// partir de ahi, cada frame busca alcanzar esa misma orientation relativa
/// al padre actual, pero limitando cuantos grados por segundo puede girar.
/// </summary>
public class FlashlightSway : MonoBehaviour
{
    [Tooltip("Grados por segundo maximos a los que la luz gira para alcanzar la direccion real de la camara. Mas bajo = mas demora/sway visible, mas alto = se siente mas pegada a la cabeza otra vez.")]
    [SerializeField] private float _followSpeedDegrees = 220f;

    private Quaternion _localOffset;

    private void Awake()
    {
        _localOffset = transform.localRotation;
    }

    private void LateUpdate()
    {
        if (transform.parent == null)
        {
            return;
        }

        Quaternion targetWorldRotation = transform.parent.rotation * _localOffset;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, targetWorldRotation, _followSpeedDegrees * Time.deltaTime);
    }
}
