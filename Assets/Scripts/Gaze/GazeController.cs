using UnityEngine;

/// <summary>
/// Raycasts from the camera center, tracks dwell time on gaze targets, and drives the reticle UI.
/// Attach this to the Main Camera. The camera should be a child of a PlayerLocomotion object.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class GazeController : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Maximum distance for gaze selection.")]
    float maxRayDistance = 20f;

    [SerializeField]
    LayerMask gazeLayerMask;

    [SerializeField]
    GazeReticleUI reticleUI;

    [SerializeField]
    PlayerLocomotion playerLocomotion;

    [SerializeField]
    [Tooltip("Creates a PlayerRig parent with PlayerLocomotion if one does not exist.")]
    bool autoSetupPlayerRig = true;

    Camera _camera;
    IGazeTarget _currentTarget;
    Component _currentTargetComponent;
    float _dwellTimer;

    void Reset()
    {
        gazeLayerMask = GazeLayers.GazeMask;
    }

    void Awake()
    {
        _camera = GetComponent<Camera>();

        if (gazeLayerMask.value == 0)
        {
            gazeLayerMask = GazeLayers.GazeMask;
        }

        if (reticleUI == null)
        {
            reticleUI = GetComponent<GazeReticleUI>();
            if (reticleUI == null)
            {
                reticleUI = gameObject.AddComponent<GazeReticleUI>();
            }
        }

        if (autoSetupPlayerRig)
        {
            EnsurePlayerRig();
        }
        else if (playerLocomotion == null)
        {
            playerLocomotion = GetComponentInParent<PlayerLocomotion>();
        }
    }

    void Update()
    {
        if (playerLocomotion != null && playerLocomotion.IsMoving)
        {
            ClearTarget();
            reticleUI.SetMode(GazeReticleUI.ReticleMode.Idle);
            reticleUI.SetProgress(0f);
            return;
        }

        if (!TryGetFocusedTarget(out IGazeTarget target, out Component targetComponent))
        {
            ClearTarget();
            reticleUI.SetMode(GazeReticleUI.ReticleMode.Idle);
            reticleUI.SetProgress(0f);
            return;
        }

        if (_currentTargetComponent != targetComponent)
        {
            ClearTarget();
            _currentTarget = target;
            _currentTargetComponent = targetComponent;
            _currentTarget.OnGazeStart();
        }

        reticleUI.SetMode(GetReticleMode(target));
        _dwellTimer += Time.deltaTime;

        float duration = Mathf.Max(0.01f, target.DwellDuration);
        float progress = Mathf.Clamp01(_dwellTimer / duration);
        reticleUI.SetProgress(progress);
        target.OnGazeProgress(progress);

        if (_dwellTimer >= duration)
        {
            target.OnGazeComplete();
            ClearTarget();
            reticleUI.SetMode(GazeReticleUI.ReticleMode.Idle);
            reticleUI.SetProgress(0f);
        }
    }

    void EnsurePlayerRig()
    {
        playerLocomotion = GetComponentInParent<PlayerLocomotion>();
        if (playerLocomotion != null)
        {
            return;
        }

        Transform originalParent = transform.parent;
        Vector3 worldPosition = transform.position;
        Quaternion worldRotation = transform.rotation;

        var rigObject = new GameObject("PlayerRig");
        rigObject.transform.position = new Vector3(worldPosition.x, originalParent != null ? originalParent.position.y : 0f,
            worldPosition.z);
        rigObject.transform.rotation = Quaternion.identity;
        playerLocomotion = rigObject.AddComponent<PlayerLocomotion>();

        if (originalParent != null)
        {
            rigObject.transform.SetParent(originalParent, true);
        }

        transform.SetParent(rigObject.transform, true);
        transform.position = worldPosition;
        transform.rotation = worldRotation;
    }

    bool TryGetFocusedTarget(out IGazeTarget target, out Component targetComponent)
    {
        target = null;
        targetComponent = null;

        Ray ray = new Ray(_camera.transform.position, _camera.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance, gazeLayerMask, QueryTriggerInteraction.Collide);

        if (hits.Length == 0)
        {
            return false;
        }

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            IGazeTarget candidate = hits[i].collider.GetComponentInParent<IGazeTarget>();
            if (candidate == null || !candidate.IsAvailable)
            {
                continue;
            }

            target = candidate;
            targetComponent = candidate as Component;
            return true;
        }

        return false;
    }

    static GazeReticleUI.ReticleMode GetReticleMode(IGazeTarget target)
    {
        if (target is TeleportNode)
        {
            return GazeReticleUI.ReticleMode.Teleport;
        }

        return GazeReticleUI.ReticleMode.Interactable;
    }

    void ClearTarget()
    {
        if (_currentTarget != null)
        {
            _currentTarget.OnGazeCancel();
        }

        _currentTarget = null;
        _currentTargetComponent = null;
        _dwellTimer = 0f;
    }
}
