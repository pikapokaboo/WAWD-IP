using System.Collections.Generic;
using UnityEngine;
 
/// <summary>
/// Data describing a single seat: the box an NPC waits at before/after sitting, and the
/// exact seat transform. Mirrors ShelfStation's reservation and debug-marker pattern.
/// This component does not move or animate NPCs.
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class ChairStation : MonoBehaviour
{
    [Header("NPC Interaction")]
    [Tooltip("NPCs navigate here before and after sitting (the box beside the chair).")]
    [SerializeField] private Transform approachPoint;
 
    [Tooltip("Exact seat position/rotation the NPC snaps to while seated.")]
    [SerializeField] private Transform seatPoint;
 
    [Tooltip("Animator trigger played when sitting down.")]
    [SerializeField] private string sitTrigger = "Sit";
 
    [Tooltip("Animator trigger played when standing up.")]
    [SerializeField] private string standTrigger = "Stand";

    [Header("Seated Pose")]
    [Tooltip("Fine adjustment from Sit_pos, using the seated NPC's local axes.")]
    [SerializeField] private Vector3 seatedPositionOffset;
    [Tooltip("Fine rotation adjustment applied after the Sit_pos red-arrow direction.")]
    [SerializeField] private Vector3 seatedRotationOffset;

    [Header("Editor Preview")]
    [Tooltip("Show an NPC in its seated animation without entering Play mode.")]
    [SerializeField] private bool previewSeatedNpc;
    [SerializeField] private GameObject seatedNpcPreviewPrefab;
 
    [Header("Debug View")]
    [Tooltip("Draw the approach/seat markers in the Scene view without rendering them in-game.")]
    [SerializeField] private bool showMarkers = true;
 
    [SerializeField] private Color approachAreaColour = new(0.1f, 0.8f, 1f, 0.25f);
    [SerializeField] private Color seatAreaColour = new(1f, 0.6f, 0.1f, 0.25f);
 
    private static readonly HashSet<ChairStation> ActiveChairs = new();
    private bool markerVisible;
    private NpcSitting reservedBy;
    [System.NonSerialized] private GameObject previewInstance;
 
    public static IEnumerable<ChairStation> AllActive => ActiveChairs;
 
    public string SitTrigger => sitTrigger;
    public string StandTrigger => standTrigger;
    public Vector3 ApproachPosition => approachPoint != null ? approachPoint.position : transform.position;
    public Vector3 SeatPosition
    {
        get
        {
            Vector3 origin = seatPoint != null ? seatPoint.position : transform.position;
            return origin + BaseSeatRotation * seatedPositionOffset;
        }
    }
    // Interaction markers use their red X arrow as the NPC-facing direction,
    // matching checkout and cooking position markers.
    public Vector3 SeatForward => seatPoint != null ? seatPoint.right : transform.right;
    public Vector3 ApproachForward => approachPoint != null ? approachPoint.right : transform.right;
    public Quaternion SeatRotation => BaseSeatRotation
        * Quaternion.Euler(seatedRotationOffset);
    public Quaternion ApproachRotation => FlatRotation(ApproachForward, transform.rotation);
    private Quaternion BaseSeatRotation => FlatRotation(SeatForward, transform.rotation);

    private static Quaternion FlatRotation(Vector3 direction, Quaternion fallback)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction.normalized, Vector3.up)
            : fallback;
    }
 
    public bool IsAvailableFor(NpcSitting npc) => reservedBy == null || reservedBy == npc;
 
    public bool TryReserve(NpcSitting npc)
    {
        if (npc == null || !IsAvailableFor(npc))
            return false;
        reservedBy = npc;
        return true;
    }
 
    public void Release(NpcSitting npc)
    {
        if (reservedBy != npc)
            return;
        reservedBy = null;
    }
 
    private void Awake()
    {
        if (approachPoint == null && seatPoint == null)
            return;
 
        SetRuntimeMarkerVisibility(false);
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            SetRuntimeMarkerVisibility(false);
            RefreshPreview();
        }
    }
 
    private void Update()
    {
        if (!Application.isPlaying)
        {
            // Preview posing is the only editor-time work this component needs.
            // Never scan or update anything while preview mode is disabled.
            if (previewSeatedNpc)
                RefreshPreview();
            return;
        }
        if (markerVisible != DeveloperConsole.ShowInteractionMarkers)
            SetRuntimeMarkerVisibility(DeveloperConsole.ShowInteractionMarkers);
    }
 
    private void SetRuntimeMarkerVisibility(bool visible)
    {
        markerVisible = visible;
        SetRenderersEnabled(approachPoint, visible);
        SetRenderersEnabled(seatPoint, visible);
    }
 
    private static void SetRenderersEnabled(Transform marker, bool visible)
    {
        if (marker == null)
            return;
 
        foreach (Renderer markerRenderer in marker.GetComponentsInChildren<Renderer>(true))
            markerRenderer.enabled = visible;
 
        // Debug markers must never affect navigation or physics.
        foreach (Collider markerCollider in marker.GetComponentsInChildren<Collider>(true))
            markerCollider.enabled = false;
    }
 
    private void OnDrawGizmos()
    {
        if (!DeveloperConsole.ShowInteractionMarkers || !showMarkers)
            return;
 
        DrawMarker(approachPoint, approachAreaColour);
        DrawMarker(seatPoint, seatAreaColour);
 
        if (approachPoint != null && seatPoint != null)
        {
            Color previousColour = Gizmos.color;
            Gizmos.color = Color.white;
            Gizmos.DrawLine(approachPoint.position, seatPoint.position);
            Gizmos.color = previousColour;
        }
    }
 
    private static void DrawMarker(Transform marker, Color colour)
    {
        if (marker == null)
            return;
 
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColour = Gizmos.color;
 
        Gizmos.matrix = marker.localToWorldMatrix;
        Gizmos.color = colour;
        Gizmos.DrawCube(Vector3.zero, Vector3.one * 0.4f);
        Gizmos.color = new Color(colour.r, colour.g, colour.b, 1f);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one * 0.4f);
 
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColour;
    }
 
    private void OnEnable()
    {
        ActiveChairs.Add(this);
        SetRuntimeMarkerVisibility(Application.isPlaying
            && DeveloperConsole.ShowInteractionMarkers);
        if (!Application.isPlaying)
            RefreshPreview();
    }
 
    private void OnDisable()
    {
        ActiveChairs.Remove(this);
        reservedBy = null;
        DestroyPreview();
    }

    private void RefreshPreview()
    {
        if (Application.isPlaying || !previewSeatedNpc
            || seatedNpcPreviewPrefab == null || seatPoint == null)
        {
            DestroyPreview();
            return;
        }

        if (previewInstance == null)
        {
            RecoverPreviewReference();
        }

        if (previewInstance == null)
        {
            previewInstance = Instantiate(seatedNpcPreviewPrefab);
            previewInstance.name = PreviewObjectName;
            previewInstance.hideFlags = HideFlags.HideAndDontSave;
            previewInstance.transform.SetParent(transform, true);
            previewInstance.SetActive(false);
            foreach (MonoBehaviour behaviour in previewInstance
                .GetComponentsInChildren<MonoBehaviour>(true))
                behaviour.enabled = false;
            foreach (Collider previewCollider in previewInstance
                .GetComponentsInChildren<Collider>(true))
                previewCollider.enabled = false;
            UnityEngine.AI.NavMeshAgent previewAgent = previewInstance
                .GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (previewAgent != null)
                previewAgent.enabled = false;
            previewInstance.SetActive(true);
        }

        previewInstance.transform.SetPositionAndRotation(SeatPosition, SeatRotation);
        Animator previewAnimator = previewInstance.GetComponentInChildren<Animator>();
        if (previewAnimator != null)
        {
            previewAnimator.Play(sitTrigger, 0, 0.95f);
            previewAnimator.Update(0f);
        }
    }

    private string PreviewObjectName =>
        $"Seated NPC Preview (Editor Only) [{GetInstanceID()}]";

    private void RecoverPreviewReference()
    {
        if (Application.isPlaying || previewInstance != null)
            return;

        // Preview objects are parented to their chair, so recovery after a script
        // reload only checks this chair's direct children instead of scanning every
        // loaded GameObject once per chair per editor frame.
        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject candidate = transform.GetChild(i).gameObject;
            if (candidate.name == PreviewObjectName
                || candidate.name == "Seated NPC Preview (Editor Only)")
            {
                previewInstance = candidate;
                return;
            }
        }
    }

    private void DestroyPreview()
    {
        if (previewInstance == null)
        {
            // This cheap child lookup is only needed during validation/disable,
            // never continuously while the editor is idle.
            RecoverPreviewReference();
        }
        if (previewInstance == null)
            return;
        GameObject objectToDestroy = previewInstance;
        previewInstance = null;
        DestroyPreviewObject(objectToDestroy);
    }

    private static void DestroyPreviewObject(GameObject objectToDestroy)
    {
        if (objectToDestroy == null)
            return;
        if (Application.isPlaying)
        {
            Destroy(objectToDestroy);
            return;
        }

        // OnValidate cannot destroy immediately. Hide and rename it now so it
        // cannot be rediscovered, then remove it after Unity finishes validation.
        objectToDestroy.SetActive(false);
        objectToDestroy.name = "Seated NPC Preview (Pending Delete)";
#if UNITY_EDITOR
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (objectToDestroy != null)
                DestroyImmediate(objectToDestroy);
        };
#endif
    }
}
