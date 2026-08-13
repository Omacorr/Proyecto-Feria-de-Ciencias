using System.Collections;
using UnityEngine;

/// <summary>
/// Moves the player rig smoothly between positions. Attach to the parent of the VR camera.
/// </summary>
[DisallowMultipleComponent]
public class PlayerLocomotion : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Seconds it takes to move between teleport nodes.")]
    float moveDuration = 0.85f;

    [SerializeField]
    AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    public bool IsMoving { get; private set; }

    public void TeleportTo(Vector3 worldPosition)
    {
        if (IsMoving)
        {
            return;
        }

        StartCoroutine(MoveRoutine(worldPosition));
    }

    IEnumerator MoveRoutine(Vector3 targetPosition)
    {
        IsMoving = true;

        Vector3 startPosition = transform.position;
        float elapsed = 0f;

        while (elapsed < moveDuration)
        {
            elapsed += Time.deltaTime;
            float t = moveCurve.Evaluate(Mathf.Clamp01(elapsed / moveDuration));
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            yield return null;
        }

        transform.position = targetPosition;
        IsMoving = false;
    }
}
