using UnityEngine;

/// <summary>
/// Lets the door be posed for a NavMesh bake outside Play mode and opens it
/// automatically for approaching NPCs at runtime.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class NpcAutomaticDoor : MonoBehaviour
{
    [Header("Door Pose")]
    [SerializeField] private bool open;
    [SerializeField] private Transform doorVisual;
    [SerializeField] private Transform handleVisual;
    [SerializeField] private float openAngle = 95f;
    [Tooltip("Switch this if the door swings around the wrong edge.")]
    [SerializeField] private bool hingeOnPositiveSide;

    [Header("Runtime Automatic Opening")]
    [SerializeField] private bool automaticallyOpenForNpcs = true;
    [SerializeField] private bool automaticallyOpenForPlayer;
    [SerializeField, Min(0.1f)] private float detectionRadius = 3f;
    [SerializeField, Min(1f)] private float swingSpeed = 180f;
    [SerializeField, Min(0f)] private float closeDelay = 0.75f;
    [Tooltip("Disable only the moving door panel's colliders while it is open.")]
    [SerializeField] private bool disableDoorCollisionWhileOpen;

    [SerializeField, HideInInspector] private bool closedPoseStored;
    [SerializeField, HideInInspector] private Vector3 closedLocalPosition;
    [SerializeField, HideInInspector] private Quaternion closedLocalRotation;
    [SerializeField, HideInInspector] private Vector3 handleClosedLocalPosition;
    [SerializeField, HideInInspector] private Quaternion handleClosedLocalRotation;
    private float lastNpcNearbyTime = float.NegativeInfinity;

    private void OnEnable()
    {
        FindParts();
        StoreClosedPoseIfNeeded();
        ApplyPose(open);
    }

    private void OnValidate()
    {
        FindParts();
        StoreClosedPoseIfNeeded();
        detectionRadius = Mathf.Max(0.1f, detectionRadius);
        swingSpeed = Mathf.Max(1f, swingSpeed);
        closeDelay = Mathf.Max(0f, closeDelay);
        ApplyPose(open);
    }

    [ContextMenu("Store Current Pose As Closed")]
    private void StoreCurrentPoseAsClosed()
    {
        FindParts();
        if (doorVisual == null)
            return;

        closedLocalPosition = doorVisual.localPosition;
        closedLocalRotation = doorVisual.localRotation;
        if (handleVisual != null)
        {
            handleClosedLocalPosition = handleVisual.localPosition;
            handleClosedLocalRotation = handleVisual.localRotation;
        }
        closedPoseStored = true;
        open = false;
        ApplyPose(false);
    }

    private void StoreClosedPoseIfNeeded()
    {
        if (closedPoseStored || doorVisual == null)
            return;

        closedLocalPosition = doorVisual.localPosition;
        closedLocalRotation = doorVisual.localRotation;
        if (handleVisual != null)
        {
            handleClosedLocalPosition = handleVisual.localPosition;
            handleClosedLocalRotation = handleVisual.localRotation;
        }
        closedPoseStored = true;
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        bool automaticOpeningEnabled = automaticallyOpenForNpcs
            || automaticallyOpenForPlayer;
        if (automaticOpeningEnabled && HasNearbyOpener())
            lastNpcNearbyTime = Time.time;

        bool shouldOpen = open || (automaticOpeningEnabled
            && Time.time <= lastNpcNearbyTime + closeDelay);
        ApplyRuntimePose(shouldOpen);
    }

    private bool HasNearbyOpener()
    {
        Vector3 centre = GetDetectionCentre();
        Collider[] nearby = Physics.OverlapSphere(centre, detectionRadius,
            Physics.AllLayers, QueryTriggerInteraction.Ignore);
        foreach (Collider candidate in nearby)
        {
            if (candidate == null)
                continue;
            if (automaticallyOpenForNpcs
                && candidate.GetComponentInParent<NpcNavigation>() != null)
                return true;
            if (automaticallyOpenForPlayer
                && candidate.GetComponentInParent<PlayerController>() != null)
                return true;
        }
        return false;
    }

    private void ApplyRuntimePose(bool openPose)
    {
        if (doorVisual == null)
            return;

        Vector3 currentPosition = doorVisual.position;
        Quaternion currentRotation = doorVisual.rotation;
        Vector3 handlePosition = handleVisual != null ? handleVisual.position : Vector3.zero;
        Quaternion handleRotation = handleVisual != null ? handleVisual.rotation : Quaternion.identity;

        ApplyPose(openPose);
        Vector3 targetPosition = doorVisual.position;
        Quaternion targetRotation = doorVisual.rotation;
        Vector3 targetHandlePosition = handleVisual != null ? handleVisual.position : Vector3.zero;
        Quaternion targetHandleRotation = handleVisual != null ? handleVisual.rotation : Quaternion.identity;

        doorVisual.SetPositionAndRotation(currentPosition, currentRotation);
        float step = swingSpeed * Time.deltaTime;
        doorVisual.position = Vector3.MoveTowards(
            currentPosition, targetPosition, step * Mathf.Deg2Rad);
        doorVisual.rotation = Quaternion.RotateTowards(
            currentRotation, targetRotation, step);

        if (handleVisual != null && !handleVisual.IsChildOf(doorVisual))
        {
            handleVisual.SetPositionAndRotation(handlePosition, handleRotation);
            handleVisual.position = Vector3.MoveTowards(
                handlePosition, targetHandlePosition, step * Mathf.Deg2Rad);
            handleVisual.rotation = Quaternion.RotateTowards(
                handleRotation, targetHandleRotation, step);
        }
    }

    private void ApplyPose(bool openPose)
    {
        if (!closedPoseStored || doorVisual == null || doorVisual.parent == null)
            return;

        SetDoorPanelCollision(!openPose);

        doorVisual.SetLocalPositionAndRotation(closedLocalPosition, closedLocalRotation);
        if (handleVisual != null)
            handleVisual.SetLocalPositionAndRotation(
                handleClosedLocalPosition, handleClosedLocalRotation);
        if (!openPose)
            return;

        MeshRenderer renderer = doorVisual.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = doorVisual.GetComponentInChildren<MeshRenderer>();
        if (renderer == null)
            return;

        Bounds bounds = renderer.localBounds;
        Vector3 hingeLocal = bounds.center;
        float direction = hingeOnPositiveSide ? 1f : -1f;
        if (bounds.size.x >= bounds.size.z)
            hingeLocal.x += bounds.extents.x * direction;
        else
            hingeLocal.z += bounds.extents.z * direction;

        Vector3 hingeWorld = renderer.transform.TransformPoint(hingeLocal);
        Vector3 axis = doorVisual.parent.up;
        Quaternion swing = Quaternion.AngleAxis(openAngle, axis);
        RotateAround(doorVisual, hingeWorld, swing);
        if (handleVisual != null && !handleVisual.IsChildOf(doorVisual))
            RotateAround(handleVisual, hingeWorld, swing);
    }

    private void SetDoorPanelCollision(bool enabled)
    {
        if (!disableDoorCollisionWhileOpen || doorVisual == null)
            return;
        foreach (Collider doorCollider in
                 doorVisual.GetComponentsInChildren<Collider>(true))
            doorCollider.enabled = enabled;
    }

    private static void RotateAround(Transform target, Vector3 pivot, Quaternion rotation)
    {
        target.position = pivot + rotation * (target.position - pivot);
        target.rotation = rotation * target.rotation;
    }

    private void FindParts()
    {
        if (doorVisual == null)
        {
            MeshRenderer doorRenderer = FindNamedRenderer(
                "door", "handle", "frame");
            if (doorRenderer == null)
            {
                // Some imported FBX parts have generic names. Preserve the
                // fallback that allowed the original automatic door to work.
                foreach (MeshRenderer candidate in
                         GetComponentsInChildren<MeshRenderer>(true))
                {
                    string candidateName = candidate.name.ToLowerInvariant();
                    if (!candidateName.Contains("handle")
                        && !candidateName.Contains("frame"))
                    {
                        doorRenderer = candidate;
                        break;
                    }
                }
            }
            doorVisual = doorRenderer?.transform;
        }
        if (handleVisual == null)
            handleVisual = FindNamedRenderer("handle")?.transform;
    }

    private Vector3 GetDetectionCentre()
    {
        if (doorVisual == null)
            return transform.position;

        MeshRenderer renderer = doorVisual.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = doorVisual.GetComponentInChildren<MeshRenderer>();
        return renderer != null ? renderer.bounds.center : doorVisual.position;
    }

    private void OnDrawGizmosSelected()
    {
        FindParts();
        Gizmos.color = new Color(0.2f, 1f, 0.35f, 0.8f);
        Gizmos.DrawWireSphere(GetDetectionCentre(), detectionRadius);
    }

    private MeshRenderer FindNamedRenderer(string required, params string[] excluded)
    {
        foreach (MeshRenderer candidate in GetComponentsInChildren<MeshRenderer>(true))
        {
            string objectName = candidate.name.ToLowerInvariant();
            if (!objectName.Contains(required))
                continue;

            bool rejected = false;
            foreach (string exclusion in excluded)
                rejected |= objectName.Contains(exclusion);
            if (!rejected)
                return candidate;
        }
        return null;
    }
}
