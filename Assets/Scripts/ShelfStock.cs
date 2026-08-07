// -----------------------------------------------------------------------------
// File: ShelfStock.cs
// Project: WAWD Integrated Studio Project
// Purpose: Describes products and NavMesh interaction zones for each side of a
//          shelf, allowing NPCs to find the correct stocked side.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Inspector configuration for one independently stocked shelf side.</summary>
[Serializable]
public sealed class ShelfSide
{
    [SerializeField] private string sideName = "Shelf Side";

    [Tooltip("Product names stocked on this side. Names are case-insensitive.")]
    [SerializeField] private List<string> stockedProducts = new();

    [Tooltip("Square in the aisle whose centre is the NPC's destination.")]
    [SerializeField] private ShelfInteractionZone interactionZone;

    [Tooltip("The shelf half the NPC should face while grabbing.")]
    [SerializeField] private Transform shelfTarget;

    /// <summary>Gets the descriptive name of this shelf side.</summary>
    public string SideName => sideName;

    /// <summary>Gets the configured aisle interaction zone.</summary>
    public ShelfInteractionZone InteractionZone => interactionZone;

    /// <summary>Gets whether a shelf-facing target has been configured.</summary>
    public bool HasShelfTarget => shelfTarget != null;

    /// <summary>Gets the point the NPC should look toward while grabbing.</summary>
    public Vector3 ShelfTargetPosition => shelfTarget != null
        ? shelfTarget.position
        : interactionZone != null ? interactionZone.transform.parent.position : Vector3.zero;

    /// <summary>Returns whether this side stocks the requested product.</summary>
    public bool ContainsProduct(string productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return false;

        foreach (string stockedProduct in stockedProducts)
        {
            if (string.Equals(stockedProduct?.Trim(), productName.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

/// <summary>
/// Registers the independently stocked sides of a shelf and supplies reachable
/// navigation destinations for NPC shopping actions.
/// </summary>
[DisallowMultipleComponent]
public sealed class ShelfStock : MonoBehaviour
{
    [Tooltip("Independently stocked and approached sides of this shelf.")]
    [SerializeField] private List<ShelfSide> sides = new();

    [Tooltip("Radius used to find walkable NavMesh near each interaction zone.")]
    [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 3f;

    private static readonly HashSet<ShelfStock> ActiveShelves = new();

    /// <summary>Gets the configured sides for browsing and store systems.</summary>
    public IReadOnlyList<ShelfSide> Sides => sides;

    /// <summary>Gets all currently enabled shelf objects.</summary>
    public static IEnumerable<ShelfStock> EnabledShelves => ActiveShelves;

    private void OnEnable()
    {
        ActiveShelves.Add(this);
    }

    private void OnDisable()
    {
        ActiveShelves.Remove(this);
    }

    /// <summary>Returns whether either side contains the requested product.</summary>
    public bool ContainsProduct(string productName)
    {
        foreach (ShelfSide side in sides)
        {
            if (side != null && side.ContainsProduct(productName))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Finds this shelf's closest reachable side carrying the requested product.
    /// </summary>
    public bool TryGetReachableProductSide(string productName, Vector3 origin,
        int areaMask, out ShelfSide side, out Vector3 destination,
        out float pathLength)
    {
        side = null;
        destination = default;
        pathLength = float.PositiveInfinity;
        NavMeshPath path = new();

        foreach (ShelfSide candidateSide in sides)
        {
            if (candidateSide == null || !candidateSide.ContainsProduct(productName))
                continue;

            Vector3 rawPosition = candidateSide.InteractionZone != null
                ? candidateSide.InteractionZone.Centre
                : transform.position;

            if (!NavMesh.SamplePosition(rawPosition, out NavMeshHit hit,
                    navMeshSampleRadius, areaMask)
                || !NavMesh.CalculatePath(origin, hit.position, areaMask, path)
                || path.status != NavMeshPathStatus.PathComplete)
                continue;

            float candidateLength = CalculatePathLength(path);
            if (candidateLength >= pathLength)
                continue;

            side = candidateSide;
            destination = hit.position;
            pathLength = candidateLength;
        }

        return side != null;
    }

    /// <summary>
    /// Finds the shelf side carrying the product with the shortest complete path.
    /// </summary>
    public static bool TryFindNearestReachable(string productName, Vector3 origin,
        int areaMask, out ShelfStock shelf, out ShelfSide side,
        out Vector3 destination)
    {
        shelf = null;
        side = null;
        destination = default;
        float shortestPathLength = float.PositiveInfinity;

        foreach (ShelfStock candidateShelf in ActiveShelves)
        {
            if (candidateShelf == null || !candidateShelf.isActiveAndEnabled
                || !candidateShelf.TryGetReachableProductSide(productName, origin,
                    areaMask, out ShelfSide candidateSide,
                    out Vector3 candidateDestination, out float candidateLength)
                || candidateLength >= shortestPathLength)
                continue;

            shortestPathLength = candidateLength;
            shelf = candidateShelf;
            side = candidateSide;
            destination = candidateDestination;
        }

        return shelf != null;
    }

    private static float CalculatePathLength(NavMeshPath path)
    {
        float length = 0f;
        for (int i = 1; i < path.corners.Length; i++)
            length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return length;
    }

    private void OnValidate()
    {
        navMeshSampleRadius = Mathf.Max(0.1f, navMeshSampleRadius);
    }
}
