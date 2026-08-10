using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Opens a visual door for nearby NavMesh agents while keeping collision fixed
/// in the door's original closed position.
/// </summary>
[DisallowMultipleComponent]
public sealed class NpcAutomaticDoor : MonoBehaviour
{
    [SerializeField] private Transform doorVisual;
    [SerializeField, Min(0.1f)] private float detectionRadius = 3f;
    [SerializeField] private float openAngle = 95f;
    [SerializeField, Min(1f)] private float swingSpeed = 180f;
    [SerializeField, Min(0f)] private float closeDelay = 0.75f;
    [Tooltip("Switch this if the hinge should use the opposite edge of the door mesh.")]
    [SerializeField] private bool hingeOnPositiveSide;

    private Transform swingPivot;
    private Quaternion closedRotation;
    private Vector3 detectionCentre;
    private float lastNpcNearbyTime = float.NegativeInfinity;

    private void Awake()
    {
        if (doorVisual == null)
        {
            Debug.LogWarning($"{nameof(NpcAutomaticDoor)} on '{name}' needs a Door Visual.", this);
            enabled = false;
            return;
        }

        CreateStationaryColliderCopies();
        CreateHingePivot();
        closedRotation = swingPivot.localRotation;
    }

    private void Update()
    {
        if (HasNearbyNpc())
            lastNpcNearbyTime = Time.time;

        bool shouldOpen = Time.time <= lastNpcNearbyTime + closeDelay;
        Quaternion target = shouldOpen
            ? closedRotation * Quaternion.Euler(0f, openAngle, 0f)
            : closedRotation;

        swingPivot.localRotation = Quaternion.RotateTowards(
            swingPivot.localRotation, target, swingSpeed * Time.deltaTime);
    }

    private bool HasNearbyNpc()
    {
        NavMeshAgent[] agents = FindObjectsByType<NavMeshAgent>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        float radiusSquared = detectionRadius * detectionRadius;

        foreach (NavMeshAgent agent in agents)
        {
            if (agent == null)
                continue;

            Vector3 offset = agent.transform.position - detectionCentre;
            offset.y = 0f;
            if (offset.sqrMagnitude <= radiusSquared)
                return true;
        }

        return false;
    }

    private void CreateHingePivot()
    {
        MeshRenderer doorRenderer = FindDoorRenderer();
        if (doorRenderer == null)
        {
            swingPivot = doorVisual;
            detectionCentre = doorVisual.position;
            return;
        }

        Bounds localBounds = doorRenderer.localBounds;
        Vector3 hingeLocal = localBounds.center;
        float direction = hingeOnPositiveSide ? 1f : -1f;

        if (localBounds.size.x >= localBounds.size.z)
            hingeLocal.x += localBounds.extents.x * direction;
        else
            hingeLocal.z += localBounds.extents.z * direction;

        Vector3 hingeWorld = doorRenderer.transform.TransformPoint(hingeLocal);
        detectionCentre = doorRenderer.bounds.center;

        GameObject pivotObject = new("Door Hinge");
        swingPivot = pivotObject.transform;
        swingPivot.SetParent(doorVisual.parent, true);
        swingPivot.SetPositionAndRotation(hingeWorld, doorVisual.rotation);
        doorVisual.SetParent(swingPivot, true);
    }

    private MeshRenderer FindDoorRenderer()
    {
        foreach (MeshRenderer candidate in
                 doorVisual.GetComponentsInChildren<MeshRenderer>(true))
        {
            string objectName = candidate.gameObject.name.ToLowerInvariant();
            if (objectName.Contains("door") && !objectName.Contains("handle"))
                return candidate;
        }

        return doorVisual.GetComponentInChildren<MeshRenderer>(true);
    }

    private void CreateStationaryColliderCopies()
    {
        Collider[] movingColliders = doorVisual.GetComponentsInChildren<Collider>(true);
        GameObject collisionRoot = new("Stationary Door Collision");
        collisionRoot.transform.SetParent(transform, false);

        foreach (Collider source in movingColliders)
        {
            GameObject copyObject = new($"{source.name} Collider");
            copyObject.layer = source.gameObject.layer;
            copyObject.transform.SetParent(collisionRoot.transform, true);
            copyObject.transform.SetPositionAndRotation(
                source.transform.position, source.transform.rotation);
            copyObject.transform.localScale = DivideScale(
                source.transform.lossyScale, collisionRoot.transform.lossyScale);

            CopyCollider(source, copyObject);
            source.enabled = false;
        }
    }

    private static void CopyCollider(Collider source, GameObject destination)
    {
        if (source is MeshCollider sourceMesh)
        {
            MeshCollider copy = destination.AddComponent<MeshCollider>();
            copy.sharedMesh = sourceMesh.sharedMesh;
            copy.convex = sourceMesh.convex;
            copy.sharedMaterial = sourceMesh.sharedMaterial;
            return;
        }

        if (source is BoxCollider sourceBox)
        {
            BoxCollider copy = destination.AddComponent<BoxCollider>();
            copy.center = sourceBox.center;
            copy.size = sourceBox.size;
            copy.sharedMaterial = sourceBox.sharedMaterial;
            return;
        }

        if (source is CapsuleCollider sourceCapsule)
        {
            CapsuleCollider copy = destination.AddComponent<CapsuleCollider>();
            copy.center = sourceCapsule.center;
            copy.radius = sourceCapsule.radius;
            copy.height = sourceCapsule.height;
            copy.direction = sourceCapsule.direction;
            copy.sharedMaterial = sourceCapsule.sharedMaterial;
            return;
        }

        if (source is SphereCollider sourceSphere)
        {
            SphereCollider copy = destination.AddComponent<SphereCollider>();
            copy.center = sourceSphere.center;
            copy.radius = sourceSphere.radius;
            copy.sharedMaterial = sourceSphere.sharedMaterial;
        }
    }

    private static Vector3 DivideScale(Vector3 worldScale, Vector3 parentScale)
    {
        return new Vector3(
            SafeDivide(worldScale.x, parentScale.x),
            SafeDivide(worldScale.y, parentScale.y),
            SafeDivide(worldScale.z, parentScale.z));
    }

    private static float SafeDivide(float value, float divisor)
    {
        return Mathf.Abs(divisor) > 0.0001f ? value / divisor : value;
    }

    private void OnValidate()
    {
        detectionRadius = Mathf.Max(0.1f, detectionRadius);
        swingSpeed = Mathf.Max(1f, swingSpeed);
        closeDelay = Mathf.Max(0f, closeDelay);
    }
}
