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

    [Header("Debug View")]
    [Tooltip("Draw the standing area in the Scene view without rendering the cube in-game.")]
    [SerializeField] private bool showStandingArea = true;

    [SerializeField] private Color standingAreaColour = new(0.1f, 0.8f, 1f, 0.25f);

    private static readonly HashSet<ShelfStation> ActiveShelves = new();
    private int approachingShopperCount;

    public static IEnumerable<ShelfStation> AllActive => ActiveShelves;
    public IReadOnlyList<string> Products => products;
    public string InteractionTrigger => interactionTrigger;
    public Vector3 StandPosition => standingPosition != null
        ? standingPosition.position
        : transform.position;
    public Vector3 LookPosition => lookTarget != null
        ? lookTarget.position
        : transform.position;
    public bool HasApproachingShopper => approachingShopperCount > 0;

    public void RegisterApproachingShopper()
    {
        approachingShopperCount++;
    }

    public void UnregisterApproachingShopper()
    {
        approachingShopperCount = Mathf.Max(0, approachingShopperCount - 1);
    }

    private void Awake()
    {
        if (standingPosition == null)
            return;

        // The marker is editing data, not part of the visible game world.
        foreach (Renderer markerRenderer in
                 standingPosition.GetComponentsInChildren<Renderer>(true))
            markerRenderer.enabled = false;

        foreach (Collider markerCollider in
                 standingPosition.GetComponentsInChildren<Collider>(true))
            markerCollider.enabled = false;
    }

    private void OnDrawGizmos()
    {
        if (!showStandingArea || standingPosition == null)
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
    }

    public bool HasProduct(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return false;

        foreach (string product in products)
        {
            if (string.Equals(product?.Trim(), productName.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
