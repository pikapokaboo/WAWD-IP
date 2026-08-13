// -----------------------------------------------------------------------------
// File: ShelfStation.cs
// Project: WAWD Integrated Studio Project
// Purpose: Defines shelf products, positions, and occupancy.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Data describing where an NPC uses a shelf and which products it contains.
/// This component does not move or animate NPCs.
/// </summary>
[DisallowMultipleComponent]
public sealed class ShelfStation : MonoBehaviour
{
    [Header("Products")]
    [Tooltip("Product names available from this shelf.")]
    [SerializeField] private List<string> products = new();

    [Header("NPC Interaction")]
    [Tooltip("NPCs should navigate to the centre of this marker.")]
    [SerializeField] private Transform standingPosition;

    [Tooltip("NPCs should face this object after reaching the standing position.")]
    [SerializeField] private Transform lookTarget;

    [Tooltip("Animator trigger an NPC behaviour should play at this shelf.")]
    [SerializeField] private string interactionTrigger = "Grab";

    [Tooltip("Model-facing correction for this station. Use 0 for direct blue-Z facing.")]
    [SerializeField] private float facingYawOffset = 90f;

    [Header("Shared Machine (Optional)")]
    [SerializeField] private IceCreamMachine.Side machineSide;

    [Header("Debug View")]
    [Tooltip("Draw the standing area in the Scene view without rendering the cube in-game.")]
    [SerializeField] private bool showStandingArea = true;

    [SerializeField] private Color standingAreaColour = new(0.1f, 0.8f, 1f, 0.25f);

    private static readonly HashSet<ShelfStation> ActiveShelves = new();
    private int approachingShopperCount;
    private bool markerVisible;
    private FridgeDoor fridgeDoor;
    private IceCreamMachine iceCreamMachine;
    private OpenFridge openFridge;
    private NpcNavigation reservedBy;

    public static IEnumerable<ShelfStation> AllActive => ActiveShelves;
    public IReadOnlyList<string> Products => iceCreamMachine != null
        ? iceCreamMachine.GetProducts(machineSide)
        : openFridge != null ? openFridge.Products : products;
    public string InteractionTrigger => interactionTrigger;
    public float FacingYawOffset => facingYawOffset;
    public Vector3 StandPosition => standingPosition != null
        ? standingPosition.position
        : transform.position;
    public Vector3 LookPosition => lookTarget != null
        ? lookTarget.position
        : transform.position;
    public bool HasApproachingShopper => approachingShopperCount > 0
        || (iceCreamMachine != null && iceCreamMachine.IsOccupiedByOther(this));
    public IceCreamMachine.Side MachineSide => machineSide;
    public OpenFridge SharedOpenFridge => openFridge;

    public bool IsAvailableFor(NpcNavigation npc) =>
        (reservedBy == null || reservedBy == npc)
        && (iceCreamMachine == null || !iceCreamMachine.IsOccupiedByOther(this));

    public bool TryReserve(NpcNavigation npc)
    {
        if (npc == null || !IsAvailableFor(npc)
            || (iceCreamMachine != null && !iceCreamMachine.TryReserve(this)))
            return false;
        reservedBy = npc;
        approachingShopperCount = 1;
        return true;
    }

    public ShelfStation FindAvailableSharedPosition(NpcNavigation npc)
    {
        if (openFridge == null)
            return this;

        foreach (ShelfStation station in ActiveShelves)
        {
            if (station != null && station.openFridge == openFridge
                && station.IsAvailableFor(npc))
                return station;
        }
        return this;
    }

    public void Release(NpcNavigation npc)
    {
        if (reservedBy != npc)
            return;
        reservedBy = null;
        approachingShopperCount = 0;
        iceCreamMachine?.Release(this);
    }

    public static void ReleaseAllFor(NpcNavigation npc)
    {
        if (npc == null) return;
        foreach (ShelfStation shelf in ActiveShelves)
        {
            if (shelf == null || shelf.reservedBy != npc) continue;
            shelf.EndInteraction();
            shelf.Release(npc);
        }
    }

    public void RegisterApproachingShopper()
    {
        approachingShopperCount++;
    }

    public void UnregisterApproachingShopper()
    {
        approachingShopperCount = Mathf.Max(0, approachingShopperCount - 1);
    }

    public void BeginInteraction()
    {
        if (fridgeDoor == null)
            fridgeDoor = GetComponent<FridgeDoor>();
        fridgeDoor?.BeginUse();
        iceCreamMachine?.BeginUse(this);
    }

    public void EndInteraction()
    {
        if (fridgeDoor == null)
            fridgeDoor = GetComponent<FridgeDoor>();
        fridgeDoor?.EndUse();
        iceCreamMachine?.EndUse(this);
    }

    private void Awake()
    {
        fridgeDoor = GetComponent<FridgeDoor>();
        iceCreamMachine = GetComponentInParent<IceCreamMachine>();
        openFridge = GetComponentInParent<OpenFridge>();
        if (standingPosition == null)
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
        if (standingPosition == null)
            return;

        markerVisible = visible;
        foreach (Renderer markerRenderer in
                 standingPosition.GetComponentsInChildren<Renderer>(true))
            markerRenderer.enabled = visible;

        // Debug markers must never affect navigation or physics.
        foreach (Collider markerCollider in
                 standingPosition.GetComponentsInChildren<Collider>(true))
            markerCollider.enabled = false;
    }

    private void OnDrawGizmos()
    {
        if (!DeveloperConsole.ShowInteractionMarkers || !showStandingArea
            || standingPosition == null)
            return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColour = Gizmos.color;

        Gizmos.matrix = standingPosition.localToWorldMatrix;
        Gizmos.color = standingAreaColour;
        Gizmos.DrawCube(Vector3.zero, Vector3.one);
        Gizmos.color = new Color(
            standingAreaColour.r,
            standingAreaColour.g,
            standingAreaColour.b,
            1f);
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.DrawLine(StandPosition, LookPosition);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColour;
    }

    private void OnEnable()
    {
        ActiveShelves.Add(this);
    }

    private void OnDisable()
    {
        ActiveShelves.Remove(this);
        approachingShopperCount = 0;
        reservedBy = null;
        iceCreamMachine?.Release(this);
    }

    public bool HasProduct(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return false;

        foreach (string product in Products)
        {
            if (string.Equals(product?.Trim(), productName.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
