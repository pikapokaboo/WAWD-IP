// -----------------------------------------------------------------------------
// File: NpcBrowseStore.cs
// Project: WAWD Integrated Studio Project
// Purpose: Makes an NPC browse several reachable shelf sides without grabbing.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>A navigation action that visits random shelf interaction zones.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent), typeof(NpcTraitProfile))]
public sealed class NpcBrowseStore : NpcTraitAction
{
    [Tooltip("Chance that a non-rushed buyer browses first. Just Browsing NPCs always browse.")]
    [SerializeField, Range(0f, 100f)] private float nonRushBrowseChance = 30f;
    [SerializeField] private Vector2Int numberOfStops = new(2, 3);
    [Tooltip("How long the NPC looks at each shelf without grabbing anything.")]
    [SerializeField] private Vector2 pauseAtStop = new(3f, 5f);
    [SerializeField] private float shelfFacingOffset = 90f;
    [SerializeField, Min(1f)] private float shelfTurnSpeed = 180f;

    private NavMeshAgent agent;
    private NpcTraitProfile profile;
    private ShelfInteractionZone reservedZone;
    private ShelfSide targetSide;
    private int stopsRemaining;
    private float continueTime;
    private bool hasDestination;
    private bool atShelf;
    private bool skipBrowsing;
    private bool previousUpdateRotation;
    private bool capturedUpdateRotation;
    private readonly HashSet<ShelfInteractionZone> visitedShelves = new();

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        profile = GetComponent<NpcTraitProfile>();
    }

    private void Update()
    {
        if (!IsTraitActive || agent == null || !agent.isOnNavMesh)
            return;

        if (skipBrowsing)
        {
            profile.CompleteNavigationAction(this);
            return;
        }

        if (hasDestination)
        {
            if (agent.pathPending || agent.remainingDistance > agent.stoppingDistance)
                return;

            hasDestination = false;
            atShelf = true;
            agent.isStopped = true;
            agent.updateRotation = false;
            continueTime = Time.time + Random.Range(pauseAtStop.x, pauseAtStop.y);
            return;
        }

        if (Time.time < continueTime)
            return;

        if (atShelf)
        {
            atShelf = false;
            ReleaseShelf();
            stopsRemaining--;
            if (stopsRemaining <= 0)
            {
                profile.CompleteNavigationAction(this);
                return;
            }
        }

        ChooseNextShelf();
    }

    private void LateUpdate()
    {
        if (!IsTraitActive || hasDestination || reservedZone == null
            || targetSide == null || !targetSide.HasShelfTarget)
            return;

        Vector3 direction = targetSide.ShelfTargetPosition - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion desired = Quaternion.LookRotation(direction.normalized, Vector3.up)
            * Quaternion.Euler(0f, shelfFacingOffset, 0f);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, desired,
            shelfTurnSpeed * Time.deltaTime);
    }

    protected override void OnTraitActivated()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
        if (profile == null)
            profile = GetComponent<NpcTraitProfile>();

        agent.updateRotation = true;
        capturedUpdateRotation = false;
        skipBrowsing = profile.HasTrait("Just Passing By")
            || (!profile.HasTrait("Just Browsing")
                && Random.value * 100f >= nonRushBrowseChance);
        int minimum = Mathf.Max(1, Mathf.Min(numberOfStops.x, numberOfStops.y));
        int maximum = Mathf.Max(minimum, Mathf.Max(numberOfStops.x, numberOfStops.y));
        stopsRemaining = Random.Range(minimum, maximum + 1);
        continueTime = 0f;
        hasDestination = false;
        atShelf = false;
        visitedShelves.Clear();

        if (!skipBrowsing)
            ChooseNextShelf();
    }

    protected override void OnTraitDeactivated()
    {
        hasDestination = false;
        atShelf = false;
        skipBrowsing = false;
        ReleaseShelf();

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.ResetPath();
        }
    }

    private void ChooseNextShelf()
    {
        if (agent == null || !agent.isOnNavMesh)
            return;

        List<(ShelfSide side, ShelfInteractionZone zone)> candidates = new();
        foreach (ShelfStock shelf in ShelfStock.EnabledShelves)
        {
            if (shelf == null)
                continue;

            foreach (ShelfSide side in shelf.Sides)
            {
                if (side?.InteractionZone != null)
                    candidates.Add((side, side.InteractionZone));
            }
        }

        NavMeshPath path = new();
        ShelfSide nearestSide = null;
        ShelfInteractionZone nearestZone = null;
        Vector3 nearestDestination = default;
        float nearestDistance = float.PositiveInfinity;
        bool hasUnvisitedShelf = false;
        bool hasOccupiedShelf = false;

        foreach ((ShelfSide side, ShelfInteractionZone zone) in candidates)
        {
            if (visitedShelves.Contains(zone))
                continue;

            hasUnvisitedShelf = true;
            if (!zone.IsAvailableFor(this))
            {
                hasOccupiedShelf = true;
                continue;
            }

            if (!NavMesh.SamplePosition(zone.Centre, out NavMeshHit hit, 2f,
                    agent.areaMask)
                || !NavMesh.CalculatePath(agent.nextPosition, hit.position,
                    agent.areaMask, path)
                || path.status != NavMeshPathStatus.PathComplete)
                continue;

            float distance = PathLength(path);
            if (distance >= nearestDistance)
                continue;

            nearestDistance = distance;
            nearestSide = side;
            nearestZone = zone;
            nearestDestination = hit.position;
        }

        if (nearestZone != null && nearestZone.TryReserve(this))
        {
            reservedZone = nearestZone;
            targetSide = nearestSide;
            visitedShelves.Add(nearestZone);
            previousUpdateRotation = agent.updateRotation;
            capturedUpdateRotation = true;
            agent.stoppingDistance = 0.05f;
            agent.isStopped = false;
            hasDestination = agent.SetDestination(nearestDestination);
            if (!hasDestination)
            {
                visitedShelves.Remove(nearestZone);
                ReleaseShelf();
            }
            return;
        }

        if (!hasUnvisitedShelf || !hasOccupiedShelf)
            skipBrowsing = true;
        else
            continueTime = Time.time + 1f;
    }

    private static float PathLength(NavMeshPath path)
    {
        float length = 0f;
        for (int i = 1; i < path.corners.Length; i++)
            length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
        return length;
    }

    private void ReleaseShelf()
    {
        if (reservedZone != null)
            reservedZone.Release(this);

        reservedZone = null;
        targetSide = null;

        if (agent != null && capturedUpdateRotation)
        {
            agent.updateRotation = previousUpdateRotation;
            capturedUpdateRotation = false;
        }
    }

    private void OnValidate()
    {
        nonRushBrowseChance = Mathf.Clamp(nonRushBrowseChance, 0f, 100f);
        pauseAtStop.x = Mathf.Max(0f, pauseAtStop.x);
        pauseAtStop.y = Mathf.Max(pauseAtStop.x, pauseAtStop.y);
        shelfTurnSpeed = Mathf.Max(1f, shelfTurnSpeed);
    }
}
