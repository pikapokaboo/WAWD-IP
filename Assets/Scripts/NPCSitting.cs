using System.Collections;
using UnityEngine;
using UnityEngine.AI;
 
/// <summary>
/// Standalone sit/stand behaviour. Does not touch NpcNavigation — it shares the same
/// NavMeshAgent but only drives it while a sit sequence is actually running. Targets,
/// animator triggers, and reservation all live on the ChairStation it's given.
///
/// Flow: walk to the station's approach point (the box beside the chair) -> walk to the
/// seat point while playing the sit animation -> wait while seated -> play the stand
/// animation -> walk back to the approach point -> release the station.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class NpcSitting : MonoBehaviour
{
    [Header("Animation Timing")]
    [Tooltip("Time given for the sit-down animation to finish before the NPC is considered seated.")]
    [SerializeField, Min(0f)] private float sitTransitionDuration = 1f;
    [Tooltip("Time given for the stand-up animation to finish before the NPC starts walking away.")]
    [SerializeField, Min(0f)] private float standTransitionDuration = 1f;
    [SerializeField, Min(0f)] private float seatedDuration = 5f;
 
    [Header("Movement")]
    [SerializeField, Min(0.05f)] private float arrivalDistance = 0.1f;
    [SerializeField, Min(1f)] private float seatTurnSpeed = 360f;
    [Tooltip("Safety net: if a walk to the box or the chair takes longer than this, give up instead of hanging forever.")]
    [SerializeField, Min(1f)] private float moveTimeout = 10f;
    [Tooltip("How far to search for a valid NavMesh point near the (possibly off-mesh) seat when standing back up.")]
    [SerializeField, Min(0.1f)] private float seatNavMeshSampleRadius = 1.5f;
 
    private NavMeshAgent agent;
    private Animator animator;
    private Coroutine sitRoutine;
    private ChairStation reservedChair;
 
    public bool IsSitting { get; private set; }
    public ChairStation ReservedChair => reservedChair;
 
    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }
 
    /// <summary>
    /// Attempts to reserve the given chair and, if successful, starts the sit sequence.
    /// Returns false (and reserves nothing) if the chair is already taken.
    /// </summary>
    public bool TryBeginSitSequence(ChairStation chair)
    {
        if (chair == null || sitRoutine != null || IsSitting || !chair.TryReserve(this))
            return false;
 
        reservedChair = chair;
        sitRoutine = StartCoroutine(SitRoutine());
        return true;
    }
 
    private IEnumerator SitRoutine()
    {
        ChairStation chair = reservedChair;
        IsSitting = true;
        bool agentDisabled = false;
 
        // try/finally guarantees the chair is released and the agent is left in a usable
        // state even if a step below fails, times out, or this object gets destroyed.
        try
        {
            // 1. Walk to the box beside the chair.
            animator?.SetBool("IsWalking", true);
            yield return MoveTo(chair.ApproachPosition);
 
            // 2. Walk from the box onto the chair itself, then play the sit animation.
            yield return MoveTo(chair.SeatPosition);
            yield return FaceDirection(chair.SeatForward);
 
            animator?.SetBool("IsWalking", false);
            animator?.SetTrigger(chair.SitTrigger);
 
            // Hand the transform fully over to the seat point / animation instead of the
            // agent, since chairs usually sit slightly off the NavMesh.
            agent.enabled = false;
            agentDisabled = true;
            transform.SetPositionAndRotation(chair.SeatPosition, chair.SeatRotation);
            yield return new WaitForSeconds(sitTransitionDuration);
 
            // 3. Stay seated.
            yield return new WaitForSeconds(seatedDuration);
 
            // 4. Stand up, then walk back to the box.
            animator?.SetTrigger(chair.StandTrigger);
            yield return new WaitForSeconds(standTransitionDuration);
 
            // Warping straight to chair.SeatPosition can silently fail and leave the agent
            // off the NavMesh (SeatPosition is allowed to sit off-mesh), which would make
            // the walk back below bail out instantly. Snap to the nearest valid point instead.
            Vector3 standPosition = chair.ApproachPosition;
            if (NavMesh.SamplePosition(chair.SeatPosition, out NavMeshHit standHit,
                    seatNavMeshSampleRadius, agent.areaMask))
                standPosition = standHit.position;
 
            agent.enabled = true;
            agentDisabled = false;
            agent.Warp(standPosition);
            animator?.SetBool("IsWalking", true);
            yield return MoveTo(chair.ApproachPosition);
        }
        finally
        {
            if (agentDisabled && agent != null)
                agent.enabled = true;
 
            chair.Release(this);
            reservedChair = null;
            IsSitting = false;
            sitRoutine = null;
        }
    }
 
    private IEnumerator MoveTo(Vector3 destination)
    {
        if (!agent.isOnNavMesh)
        {
            Debug.LogWarning($"{name}: NpcSitting tried to move while not on a NavMesh.", this);
            yield break;
        }
 
        Vector3 reachableDestination = destination;
        if (NavMesh.SamplePosition(destination, out NavMeshHit hit,
                Mathf.Max(0.75f, agent.radius), agent.areaMask))
            reachableDestination = hit.position;
 
        agent.isStopped = false;
        if (!agent.SetDestination(reachableDestination))
        {
            Debug.LogWarning($"{name}: NpcSitting could not path to {reachableDestination}.", this);
            yield break;
        }
 
        // Hard safety net: if the agent takes too long to reach the destination, bail out instead of hanging forever.
        float deadline = Time.time + moveTimeout;
        while (agent.pathPending
               || agent.remainingDistance > agent.stoppingDistance + arrivalDistance)
        {
            if (!agent.isOnNavMesh || agent.pathStatus == NavMeshPathStatus.PathInvalid)
            {
                Debug.LogWarning($"{name}: NpcSitting's path to {reachableDestination} became invalid.", this);
                yield break;
            }
 
            if (Time.time >= deadline)
            {
                Debug.LogWarning($"{name}: NpcSitting timed out walking to {reachableDestination}.", this);
                yield break;
            }
 
            yield return null;
        }
    }
 
    private IEnumerator FaceDirection(Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            yield break;
 
        Quaternion targetRotation = Quaternion.LookRotation(forward);
        while (Quaternion.Angle(transform.rotation, targetRotation) > 1f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, seatTurnSpeed * Time.deltaTime);
            yield return null;
        }
        transform.rotation = targetRotation;
    }
}