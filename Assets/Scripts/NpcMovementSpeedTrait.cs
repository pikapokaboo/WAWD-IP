// -----------------------------------------------------------------------------
// File: NpcMovementSpeedTrait.cs
// Project: WAWD Integrated Studio Project
// Purpose: Applies a trait-defined movement speed to an NPC NavMeshAgent.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Modular trait action that changes the owning NPC's NavMesh movement speed.
/// Multiple configured copies can represent different speed traits.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public sealed class NpcMovementSpeedTrait : NpcTraitAction
{
    [Tooltip("NavMeshAgent movement speed used while this trait is active.")]
    [SerializeField, Min(0f)] private float movementSpeed = 3.5f;

    private NavMeshAgent agent;
    private SpawnedNpc spawnedNpc;
    private float originalSpeed;
    private bool hasCachedOriginalSpeed;

    /// <inheritdoc />
    protected override void OnTraitActivated()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();
        if (spawnedNpc == null)
            spawnedNpc = GetComponent<SpawnedNpc>();

        if (!hasCachedOriginalSpeed)
        {
            originalSpeed = agent.speed;
            hasCachedOriginalSpeed = true;
        }

        if (spawnedNpc != null)
            spawnedNpc.SetPreferredSpeed(movementSpeed);
        else
            agent.speed = movementSpeed;
    }

    /// <inheritdoc />
    protected override void OnTraitDeactivated()
    {
        if (agent != null && hasCachedOriginalSpeed)
        {
            if (spawnedNpc != null)
                spawnedNpc.SetPreferredSpeed(originalSpeed);
            else
                agent.speed = originalSpeed;
        }
    }

    private void OnValidate()
    {
        movementSpeed = Mathf.Max(0f, movementSpeed);
    }
}
