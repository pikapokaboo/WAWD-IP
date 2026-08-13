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
    [SerializeField] private Vector2 seatedDurationRange = new(5f, 15f);
 
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
        if (!TryReserveChair(chair))
            return false;
        return BeginReservedSitSequence(chair);
    }

    public bool TryReserveChair(ChairStation chair)
    {
        if (chair == null || reservedChair != null || sitRoutine != null
            || IsSitting || !chair.TryReserve(this))
            return false;
        reservedChair = chair;
        return true;
    }

    public bool BeginReservedSitSequence(ChairStation chair)
    {
        if (chair == null || reservedChair != chair || sitRoutine != null || IsSitting)
            return false;
        sitRoutine = StartCoroutine(SitRoutine());
        return true;
    }
 
    private IEnumerator SitRoutine()
    {
        ChairStation chair = reservedChair;
        IsSitting = true;
 
        // 1. Walk to the box beside the chair.
        animator?.SetBool("IsWalking", true);
        yield return MoveTo(chair.ApproachPosition);
        animator?.SetBool("IsWalking", false);
 
        // 2. Match the side marker arrow, then slide into the chair while sitting.
        yield return FaceDirection(chair.ApproachForward);
        animator?.SetBool("IsWalking", false);
        animator?.SetTrigger(chair.SitTrigger);
        agent.enabled = false;
        yield return SlideTo(chair.SeatPosition, chair.SeatRotation, sitTransitionDuration);

        // 3. Stay seated.
        yield return new WaitForSeconds(Random.Range(
            Mathf.Min(seatedDurationRange.x, seatedDurationRange.y),
            Mathf.Max(seatedDurationRange.x, seatedDurationRange.y)));
 
        // 4. Turn towards the side marker, then slide out while standing.
        yield return FaceDirection(chair.ApproachPosition - transform.position);
        animator?.ResetTrigger(chair.SitTrigger);
        animator?.SetTrigger(chair.StandTrigger);
        // Let the Animator consume the trigger before movement begins so the
        // entire outward slide visibly uses the stand-up animation.
        yield return null;
        yield return SlideTo(chair.ApproachPosition, chair.ApproachRotation,
            standTransitionDuration);
        agent.enabled = true;
        agent.Warp(chair.ApproachPosition);
        animator?.SetBool("IsWalking", false);
        // The NPC must be fully upright on arrival, even if its controller has
        // a long exit transition from the Stand state.
        animator?.Play("Idle", 0, 0f);
 
        chair.Release(this);
        reservedChair = null;
        IsSitting = false;
        sitRoutine = null;
    }

    private IEnumerator SlideTo(Vector3 destination, Quaternion rotation, float duration)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        if (duration <= 0f)
        {
            transform.SetPositionAndRotation(destination, rotation);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float amount = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01(elapsed / duration));
            transform.SetPositionAndRotation(
                Vector3.Lerp(startPosition, destination, amount),
                Quaternion.Slerp(startRotation, rotation, amount));
            yield return null;
        }
        transform.SetPositionAndRotation(destination, rotation);
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

    private void OnDisable()
    {
        if (reservedChair != null)
            reservedChair.Release(this);
        reservedChair = null;
        sitRoutine = null;
        IsSitting = false;
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
