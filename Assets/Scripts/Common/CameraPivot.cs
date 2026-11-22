using UnityEngine;

/// <summary>
/// Pivote temporal padre de la camara. El TrackedPoseDriver pisa la rotacion
/// LOCAL de la camara en cada frame, asi que la inclinacion/caida/sacudida se
/// aplica a este padre vacio (la camara conserva su pose mundial al
/// reparentarse). Lo usan AbyssFall y ScriptedChase.
/// </summary>
public sealed class CameraPivot
{
    public Transform Pivot { get; private set; }
    private readonly Transform _cam;
    private readonly Transform _originalParent;

    public CameraPivot(Transform cam, string name)
    {
        _cam = cam;
        _originalParent = cam.parent;
        Pivot = new GameObject(name).transform;
        Pivot.SetParent(_originalParent, true);
        Pivot.position = cam.position;
        Pivot.localRotation = Quaternion.identity; // igual que el padre original: la rotacion local de la camara sigue valiendo
        cam.SetParent(Pivot, true);
    }

    /// <summary>
    /// Inclina el pivote sobre el eje "derecha" horizontal segun hacia donde
    /// mira la cabeza (si no, mirando de costado seria un roll).
    /// </summary>
    public void Tilt(float degrees)
    {
        Quaternion yaw = Quaternion.Euler(0f, _cam.localEulerAngles.y, 0f);
        Pivot.localRotation = Quaternion.AngleAxis(degrees, yaw * Vector3.right);
    }

    /// <summary>Devuelve la camara a su padre (si sigue viva) y destruye el pivote. Seguro al descargar la escena.</summary>
    public void Release()
    {
        if (Pivot == null)
        {
            return;
        }
        if (_cam != null && _cam.parent == Pivot)
        {
            _cam.SetParent(_originalParent, true);
        }
        Object.Destroy(Pivot.gameObject);
        Pivot = null;
    }
}
