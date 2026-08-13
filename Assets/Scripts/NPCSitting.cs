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
 
        // 1. Walk to the box beside the chair.
        yield return MoveTo(chair.ApproachPosition);
 
        // 2. Walk from the box onto the chair itself, then play the sit animation.
        yield return MoveTo(chair.SeatPosition);
        yield return FaceDirection(chair.SeatForward);

        animator?.SetBool("IsWalking", false);
        animator?.SetTrigger(chair.SitTrigger);
        Debug.Log($"Sit trigger fired. Current state: {animator.GetCurrentAnimatorStateInfo(0).fullPathHash}, has param: {System.Array.Exists(animator.parameters, p => p.name == chair.SitTrigger)}");
 
        // Hand the transform fully over to the seat point / animation instead of the agent,
        // since chairs usually sit slightly off the NavMesh.
        agent.enabled = false;
        transform.SetPositionAndRotation(chair.SeatPosition, chair.SeatRotation);
        yield return new WaitForSeconds(sitTransitionDuration);
 
        // 3. Stay seated.
        yield return new WaitForSeconds(seatedDuration);
 
        // 4. Stand up, then walk back to the box.
        animator?.SetTrigger(chair.StandTrigger);
        yield return new WaitForSeconds(standTransitionDuration);
 
        agent.enabled = true;
        agent.Warp(chair.SeatPosition);
        animator?.SetBool("IsWalking", true);
        yield return MoveTo(chair.ApproachPosition);
 
        chair.Release(this);
        reservedChair = null;
        IsSitting = false;
        sitRoutine = null;
    }
 
    private IEnumerator MoveTo(Vector3 destination)
    {
        agent.isStopped = false;
        agent.SetDestination(destination);
 
        // Wait a frame so pathPending has a chance to become true before we check it.
        yield return null;
 
        while (agent.pathPending || agent.remainingDistance > arrivalDistance)
            yield return null;
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

    #if UNITY_EDITOR
    [ContextMenu("DEBUG: Sit On Nearest Chair")]
    private void DebugSitNearestChair()
    {
        ChairStation nearest = null;
        float closestSqr = float.PositiveInfinity;
        foreach (var chair in ChairStation.AllActive)
        {
            if (chair == null || !chair.IsAvailableFor(this)) continue;
            float sqr = (chair.ApproachPosition - transform.position).sqrMagnitude;
            if (sqr < closestSqr) { closestSqr = sqr; nearest = chair; }
        }

        if (nearest == null)
            Debug.LogWarning("No available ChairStation found.", this);
        else if (!TryBeginSitSequence(nearest))
            Debug.LogWarning("TryBeginSitSequence failed (already reserved/sitting).", this);
    }
    #endif
}