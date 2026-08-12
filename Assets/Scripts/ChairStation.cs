using System.Collections.Generic;
using UnityEngine;
 
/// <summary>
/// Data describing a single seat: the box an NPC waits at before/after sitting, and the
/// exact seat transform. Mirrors ShelfStation's reservation and debug-marker pattern.
/// This component does not move or animate NPCs.
/// </summary>
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
 
    [Header("Debug View")]
    [Tooltip("Draw the approach/seat markers in the Scene view without rendering them in-game.")]
    [SerializeField] private bool showMarkers = true;
 
    [SerializeField] private Color approachAreaColour = new(0.1f, 0.8f, 1f, 0.25f);
    [SerializeField] private Color seatAreaColour = new(1f, 0.6f, 0.1f, 0.25f);
 
    private static readonly HashSet<ChairStation> ActiveChairs = new();
    private bool markerVisible;
    private NpcSitting reservedBy;
 
    public static IEnumerable<ChairStation> AllActive => ActiveChairs;
 
    public string SitTrigger => sitTrigger;
    public string StandTrigger => standTrigger;
    public Vector3 ApproachPosition => approachPoint != null ? approachPoint.position : transform.position;
    public Vector3 SeatPosition => seatPoint != null ? seatPoint.position : transform.position;
    public Quaternion SeatRotation => seatPoint != null ? seatPoint.rotation : transform.rotation;
    public Vector3 SeatForward => seatPoint != null ? seatPoint.forward : transform.forward;
 
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
 
    private void Update()
    {
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
    }
 
    private void OnDisable()
    {
        ActiveChairs.Remove(this);
        reservedBy = null;
    }
}