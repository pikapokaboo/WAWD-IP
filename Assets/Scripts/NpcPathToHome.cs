// -----------------------------------------------------------------------------
// File: NpcPathToHome.cs
// Project: WAWD Integrated Studio Project
// Purpose: Uses a NavMeshAgent to send an NPC to a despawning pad.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Modular navigational trait action that follows an already baked NavMesh to
/// a selected despawning pad.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class NpcPathToHome : NpcTraitAction
{
    [Header("Destination")]
    [Tooltip("Scene despawning pad this NPC should walk toward.")]
    [SerializeField] private NpcDespawningPad despawningPad;

    [Tooltip("Find the first despawning pad in the scene when none is assigned.")]
    [SerializeField] private bool findPadAutomatically = true;

    [Header("NavMesh")]
    [Tooltip("Search radius used to locate the nearest baked NavMesh point.")]
    [SerializeField, Min(0.1f)] private float navMeshSampleRadius = 2f;

    [Tooltip("How often the destination is refreshed if the pad moves.")]
    [SerializeField, Min(0.1f)] private float repathInterval = 0.5f;

    private NavMeshAgent agent;
    private float nextRepathTime;
    private bool warnedAboutMissingPad;
    private bool warnedAboutNavMesh;

    /// <summary>Gets the currently assigned despawning pad.</summary>
    public NpcDespawningPad DespawningPad => despawningPad;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    private void Update()
    {
        if (!IsTraitActive || Time.time < nextRepathTime)
            return;

        TrySetPath();
        nextRepathTime = Time.time + repathInterval;
    }

    /// <summary>
    /// Assigns the scene pad this NPC should navigate toward.
    /// </summary>
    public void SetDestination(NpcDespawningPad pad)
    {
        despawningPad = pad;
        warnedAboutMissingPad = false;

        if (IsTraitActive)
            TrySetPath();
    }

    /// <inheritdoc />
    protected override void OnTraitActivated()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        nextRepathTime = 0f;
        TrySetPath();
    }

    /// <inheritdoc />
    protected override void OnTraitDeactivated()
    {
        if (agent != null && agent.isOnNavMesh)
            agent.ResetPath();
    }

    private void TrySetPath()
    {
        if (!ResolveDestination())
            return;

        if (!PlaceAgentOnNavMesh())
            return;

        if (!NavMesh.SamplePosition(despawningPad.transform.position,
                out NavMeshHit destinationHit, navMeshSampleRadius, agent.areaMask))
        {
            WarnAboutNavMesh(
                $"No baked NavMesh was found near despawning pad '{despawningPad.name}'.");
            return;
        }

        warnedAboutNavMesh = false;
        if (!agent.SetDestination(destinationHit.position))
            WarnAboutNavMesh("The NavMeshAgent could not calculate a path to the despawning pad.");
    }

    private bool ResolveDestination()
    {
        if (despawningPad == null && findPadAutomatically)
            despawningPad = FindFirstObjectByType<NpcDespawningPad>();

        if (despawningPad != null)
            return true;

        if (!warnedAboutMissingPad)
        {
            Debug.LogWarning(
                $"{nameof(NpcPathToHome)} on '{name}' needs a despawning pad.", this);
            warnedAboutMissingPad = true;
        }

        return false;
    }

    private bool PlaceAgentOnNavMesh()
    {
        if (agent.isOnNavMesh)
            return true;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit startHit,
                navMeshSampleRadius, agent.areaMask) && agent.Warp(startHit.position))
        {
            warnedAboutNavMesh = false;
            return true;
        }

        WarnAboutNavMesh(
            $"NPC '{name}' is not close enough to a baked NavMesh. Bake the scene's NavMesh "
            + "and ensure the spawning pad sits on it.");
        return false;
    }

    private void WarnAboutNavMesh(string message)
    {
        if (warnedAboutNavMesh)
            return;

        Debug.LogWarning(message, this);
        warnedAboutNavMesh = true;
    }

    private void OnValidate()
    {
        navMeshSampleRadius = Mathf.Max(0.1f, navMeshSampleRadius);
        repathInterval = Mathf.Max(0.1f, repathInterval);
    }
}
