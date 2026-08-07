// -----------------------------------------------------------------------------
// File: SpawnedNpc.cs
// Project: WAWD Integrated Studio Project
// Purpose: Identifies NPC objects that can be removed by a despawning pad.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

/// <summary>
/// Marker component used to distinguish NPCs from players and other colliders.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class SpawnedNpc : MonoBehaviour
{
    private static readonly HashSet<SpawnedNpc> ActiveNpcs = new();

    [Header("Crowd Avoidance")]
    [Tooltip("Navigation avoidance radius. Set to match the character's visible width.")]
    [SerializeField, Min(0.1f)] private float avoidanceRadius = 1.15f;
    [SerializeField, Range(0, 99)] private int minimumAvoidancePriority = 40;
    [SerializeField, Range(0, 99)] private int maximumAvoidancePriority = 60;
    [Tooltip("NPCs begin slowing when another customer is this far ahead.")]
    [SerializeField, Min(0.5f)] private float slowDownDistance = 3f;
    [Tooltip("Desired minimum spacing between moving customers.")]
    [SerializeField, Min(0.1f)] private float stopDistance = 1.5f;
    [SerializeField, Min(0.1f)] private float speedChangeRate = 5f;

    private NavMeshAgent agent;
    private float preferredSpeed = 3.5f;
    private bool preferredSpeedAssigned;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        if (!preferredSpeedAssigned)
            preferredSpeed = agent.speed;
        agent.radius = avoidanceRadius;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        int minimum = Mathf.Min(minimumAvoidancePriority, maximumAvoidancePriority);
        int maximum = Mathf.Max(minimumAvoidancePriority, maximumAvoidancePriority);
        agent.avoidancePriority = Random.Range(minimum, maximum + 1);
    }

    private void Update()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        float targetSpeed = preferredSpeed * GetTrafficSpeedFactor();
        agent.speed = Mathf.MoveTowards(agent.speed, targetSpeed,
            speedChangeRate * Time.deltaTime);
    }

    /// <summary>Sets the uncongested speed selected by movement traits.</summary>
    public void SetPreferredSpeed(float speed)
    {
        preferredSpeed = Mathf.Max(0f, speed);
        preferredSpeedAssigned = true;
    }

    private void OnEnable()
    {
        foreach (SpawnedNpc other in ActiveNpcs)
        {
            if (other != null)
                IgnorePhysicalCollisionWith(other);
        }

        ActiveNpcs.Add(this);
    }

    private void OnDisable()
    {
        ActiveNpcs.Remove(this);
    }

    private void IgnorePhysicalCollisionWith(SpawnedNpc other)
    {
        Collider[] ownColliders = GetComponentsInChildren<Collider>();
        Collider[] otherColliders = other.GetComponentsInChildren<Collider>();

        foreach (Collider ownCollider in ownColliders)
        {
            foreach (Collider otherCollider in otherColliders)
                Physics.IgnoreCollision(ownCollider, otherCollider, true);
        }
    }

    private float GetTrafficSpeedFactor()
    {
        Vector3 travelDirection = agent.desiredVelocity;
        travelDirection.y = 0f;
        if (travelDirection.sqrMagnitude < 0.01f)
            return 1f;

        travelDirection.Normalize();
        float closestAhead = float.PositiveInfinity;

        foreach (SpawnedNpc other in ActiveNpcs)
        {
            if (other == null || other == this || other.agent == null)
                continue;

            Vector3 offset = other.transform.position - transform.position;
            offset.y = 0f;
            float distance = offset.magnitude;
            if (distance <= 0.001f || distance > slowDownDistance
                || Vector3.Dot(travelDirection, offset / distance) < 0.55f)
                continue;

            Vector3 otherDirection = other.agent.desiredVelocity;
            otherDirection.y = 0f;
            bool approachingHeadOn = otherDirection.sqrMagnitude > 0.01f
                && Vector3.Dot(travelDirection, otherDirection.normalized) < -0.25f;

            // At head-on meetings only one agent yields, avoiding a mutual stop.
            if (approachingHeadOn
                && agent.avoidancePriority < other.agent.avoidancePriority)
                continue;

            closestAhead = Mathf.Min(closestAhead, distance);
        }

        if (float.IsPositiveInfinity(closestAhead))
            return 1f;

        return Mathf.InverseLerp(stopDistance, slowDownDistance, closestAhead);
    }

    private void OnValidate()
    {
        avoidanceRadius = Mathf.Max(0.1f, avoidanceRadius);
        minimumAvoidancePriority = Mathf.Clamp(minimumAvoidancePriority, 0, 99);
        maximumAvoidancePriority = Mathf.Clamp(maximumAvoidancePriority, 0, 99);
        slowDownDistance = Mathf.Max(0.5f, slowDownDistance);
        stopDistance = Mathf.Clamp(stopDistance, 0.1f, slowDownDistance);
        speedChangeRate = Mathf.Max(0.1f, speedChangeRate);
    }
}
