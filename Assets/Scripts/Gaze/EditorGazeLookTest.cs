using UnityEngine;

/// <summary>
/// Allows looking around with the mouse while testing in the Unity Editor without VR hardware.
/// Disable or remove this component on mobile builds.
/// </summary>
[DisallowMultipleComponent]
public class EditorGazeLookTest : MonoBehaviour
{
    [SerializeField]
    float lookSensitivity = 2.5f;

    [SerializeField]
    bool requireRightMouseButton = true;

    float _rotationX;
    float _rotationY;

    void Start()
    {
        Vector3 euler = transform.localEulerAngles;
        _rotationX = euler.x;
        _rotationY = euler.y;
    }

    void Update()
    {
#if !UNITY_EDITOR
        enabled = false;
        return;
#else
        if (requireRightMouseButton && !Input.GetMouseButton(1))
        {
            return;
        }

        _rotationY += Input.GetAxis("Mouse X") * lookSensitivity;
        _rotationX -= Input.GetAxis("Mouse Y") * lookSensitivity;
        _rotationX = Mathf.Clamp(_rotationX, -85f, 85f);
        transform.localRotation = Quaternion.Euler(_rotationX, _rotationY, 0f);
#endif
    }
}
