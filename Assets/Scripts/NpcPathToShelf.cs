// -----------------------------------------------------------------------------
// File: NpcPathToShelf.cs
// Project: WAWD Integrated Studio Project
// Purpose: Navigates an NPC to the nearest reachable shelf carrying a requested
//          product.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Modular navigational trait action that finds and paths to stocked shelves.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NpcTraitProfile))]
public sealed class NpcPathToShelf : NpcTraitAction
{
    private const float StoppingDistance = 0.05f;
    private const float RepathInterval = 1f;
    private const float NavMeshSampleRadius = 2f;
    private const float WanderRadius = 8f;
    private const float WanderInterval = 4f;
    private const int WanderAttempts = 8;

    [Header("Shopping Request")]
    [Tooltip("Product name to find. It must match a ShelfStock entry.")]
    [SerializeField] private string requestedProduct;

    [Header("Shelf Interaction")]
    [Tooltip("Time allowed for the existing Grab animation before this task completes.")]
    [SerializeField, Min(0f)] private float grabDuration = 2.32f;
    [Tooltip("Yaw correction for models whose visual forward axis is not Unity's +Z.")]
    [SerializeField] private float shelfFacingOffset = 90f;
    [Tooltip("How quickly the NPC turns toward the shelf, in degrees per second.")]
    [SerializeField, Min(1f)] private float shelfTurnSpeed = 180f;

    private NavMeshAgent agent;
    private NpcTraitProfile traitProfile;
    private NPCAnimation npcAnimation;
    private ShelfStock targetShelf;
    private ShelfSide targetSide;
    private ShelfInteractionZone reservedZone;
    private Vector3 targetPosition;
    private float nextRepathTime;
    private bool warned;
    private bool hasDestination;
    private bool isGrabbing;
    private float grabFinishedTime;
    private bool agentUpdatedRotationBeforeGrab;
    private float nextWaitingWanderTime;

    /// <summary>Gets a short live description for debug UI.</summary>
    public string CurrentStatus { get; private set; } = "Idle";

    /// <summary>Gets the product this action is currently seeking.</summary>
    private bool HasArrived => hasDestination && agent != null && agent.isOnNavMesh
        && !agent.pathPending
        && agent.remainingDistance <= agent.stoppingDistance;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        traitProfile = GetComponent<NpcTraitProfile>();
        npcAnimation = GetComponent<NPCAnimation>();

    }

    private void Update()
    {
        if (!IsTraitActive)
            return;

        if (isGrabbing)
        {
            if (Time.time >= grabFinishedTime)
                traitProfile.CompleteNavigationAction(this);
            return;
        }

        if (HasArrived)
        {
            BeginGrabInteraction();
            return;
        }

        if (Time.time < nextRepathTime)
            return;

        FindShelfAndSetPath();
        nextRepathTime = Time.time + RepathInterval;
    }

    private void LateUpdate()
    {
        // Keep this authoritative after the NavMeshAgent and Animator have
        // updated. Applying the rotation only on the arrival frame can leave
        // the character displaying its final walking direction instead.
        if (isGrabbing)
            FaceSelectedShelf();
    }

    /// <inheritdoc />
    protected override void OnTraitActivated()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        agent.updateRotation = true;
        agent.stoppingDistance = StoppingDistance;
        agent.isStopped = false;
        agentUpdatedRotationBeforeGrab = true;
        nextRepathTime = 0f;
        nextWaitingWanderTime = 0f;
        CurrentStatus = "Finding shelf";
        hasDestination = false;
        isGrabbing = false;
        FindShelfAndSetPath();
    }

    /// <inheritdoc />
    protected override void OnTraitDeactivated()
    {
        hasDestination = false;
        isGrabbing = false;
        CurrentStatus = "Idle";
        ReleaseReservedZone();

        if (agent != null && agent.isOnNavMesh)
        {
            agent.updateRotation = agentUpdatedRotationBeforeGrab;
            agent.isStopped = false;
            agent.ResetPath();
        }
    }

    private void FindShelfAndSetPath()
    {
        if (string.IsNullOrWhiteSpace(requestedProduct))
        {
            WarnOnce($"{nameof(NpcPathToShelf)} on '{name}' needs a requested product.");
            return;
        }

        if (!PlaceAgentOnNavMesh())
            return;

        bool found = ShelfStock.TryFindNearestReachable(requestedProduct,
            agent.nextPosition, agent.areaMask, out targetShelf, out targetSide,
            out targetPosition);

        if (!found)
        {
            WarnOnce($"No reachable shelf stocks '{requestedProduct}'. Check shelf stock names, interaction zones, and the baked NavMesh.");
            return;
        }

        ShelfInteractionZone selectedZone = targetSide?.InteractionZone;
        if (selectedZone != null && !selectedZone.TryReserve(this))
        {
            // Another shopper is using this side. Wander on reachable NavMesh
            // around the store while retrying instead of queuing at the spawn.
            hasDestination = false;
            CurrentStatus = "Wandering while shelf is occupied";
            WanderWhileWaiting();
            return;
        }

        if (reservedZone != selectedZone)
        {
            ReleaseReservedZone();
            reservedZone = selectedZone;
        }

        agent.stoppingDistance = StoppingDistance;
        agent.isStopped = false;
        if (agent.SetDestination(targetPosition))
        {
            hasDestination = true;
            warned = false;
            CurrentStatus = "Going to shelf";
        }
        else
            WarnOnce($"'{name}' could not calculate a path to shelf '{targetShelf.name}'.");
    }

    private bool PlaceAgentOnNavMesh()
    {
        if (agent.isOnNavMesh)
            return true;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit startHit,
                NavMeshSampleRadius, agent.areaMask) && agent.Warp(startHit.position))
            return true;

        WarnOnce($"NPC '{name}' is not close enough to a baked NavMesh.");
        return false;
    }

    private void BeginGrabInteraction()
    {
        hasDestination = false;
        isGrabbing = true;
        CurrentStatus = $"Grabbing {requestedProduct}";
        agent.isStopped = true;
        agent.ResetPath();

        agent.updateRotation = false;

        npcAnimation?.Grab();
        grabFinishedTime = Time.time + grabDuration;
    }

    private void FaceSelectedShelf()
    {
        if (targetShelf == null)
            return;

        Vector3 lookPosition = targetSide != null && targetSide.HasShelfTarget
            ? targetSide.ShelfTargetPosition
            : targetShelf.transform.position;
        Vector3 direction = lookPosition - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.0001f)
        {
            Quaternion shelfRotation = Quaternion.LookRotation(
                direction.normalized, Vector3.up);
            Quaternion desiredRotation = shelfRotation
                * Quaternion.Euler(0f, shelfFacingOffset, 0f);
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                desiredRotation, shelfTurnSpeed * Time.deltaTime);
        }
    }

    private void ReleaseReservedZone()
    {
        if (reservedZone == null)
            return;

        reservedZone.Release(this);
        reservedZone = null;
    }

    private void WanderWhileWaiting()
    {
        if (targetShelf == null || Time.time < nextWaitingWanderTime)
            return;

        nextWaitingWanderTime = Time.time + WanderInterval;
        Vector3 centre = targetShelf.transform.position;
        NavMeshPath path = new();

        for (int attempt = 0; attempt < WanderAttempts; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle * WanderRadius;
            Vector3 candidate = centre + new Vector3(offset.x, 0f, offset.y);

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2f,
                    agent.areaMask)
                || !NavMesh.CalculatePath(agent.nextPosition, hit.position,
                    agent.areaMask, path)
                || path.status != NavMeshPathStatus.PathComplete)
                continue;

            agent.isStopped = false;
            agent.stoppingDistance = 0.3f;
            agent.SetDestination(hit.position);
            return;
        }
    }

    private void WarnOnce(string message)
    {
        if (warned)
            return;

        Debug.LogWarning(message, this);
        warned = true;
    }

    private void OnValidate()
    {
        grabDuration = Mathf.Max(0f, grabDuration);
        shelfTurnSpeed = Mathf.Max(1f, shelfTurnSpeed);
    }
}
