// -----------------------------------------------------------------------------
// File: NpcSpawningPad.cs
// Project: WAWD Integrated Studio Project
// Purpose: Periodically creates NPC prefab instances above a spawning pad.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns NPCs at a configurable interval while enforcing a live NPC limit.
/// </summary>
[DisallowMultipleComponent]
public sealed class NpcSpawningPad : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("NPC prefab created by this pad.")]
    [SerializeField] private GameObject npcPrefab;

    [Tooltip("Optional transform used as the exact spawn position and rotation.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("Vertical offset used when no custom spawn point is assigned.")]
    [SerializeField, Min(0f)] private float spawnHeight = 0.05f;

    [Header("Spawn Timing")]
    [SerializeField, Min(0.1f)] private float minimumSpawnInterval = 2f;
    [SerializeField, Min(0.1f)] private float maximumSpawnInterval = 5f;
    [SerializeField, Min(1)] private int maximumLivingNpcs = 5;
    [SerializeField] private bool spawnImmediately = true;

    private readonly List<GameObject> livingNpcs = new();
    private float nextSpawnTime;

    private void OnEnable()
    {
        nextSpawnTime = Time.time + (spawnImmediately ? 0f : GetRandomSpawnDelay());
    }

    private void Update()
    {
        if (Time.time < nextSpawnTime)
            return;

        RemoveDestroyedNpcs();

        if (livingNpcs.Count < maximumLivingNpcs)
            SpawnNpc();

        nextSpawnTime = Time.time + GetRandomSpawnDelay();
    }

    /// <summary>
    /// Creates one NPC if a prefab is assigned and the live limit has not been reached.
    /// </summary>
    public void SpawnNpc()
    {
        if (npcPrefab == null)
        {
            Debug.LogWarning($"{nameof(NpcSpawningPad)} on '{name}' needs an NPC prefab.", this);
            enabled = false;
            return;
        }

        RemoveDestroyedNpcs();
        if (livingNpcs.Count >= maximumLivingNpcs)
            return;

        Vector3 position = spawnPoint != null
            ? spawnPoint.position
            : transform.position + transform.up * spawnHeight;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        GameObject spawnedNpc = Instantiate(npcPrefab, position, rotation);
        livingNpcs.Add(spawnedNpc);
    }

    private void RemoveDestroyedNpcs()
    {
        livingNpcs.RemoveAll(npc => npc == null);
    }

    private float GetRandomSpawnDelay()
    {
        return Random.Range(minimumSpawnInterval, maximumSpawnInterval);
    }

    private void OnValidate()
    {
        minimumSpawnInterval = Mathf.Max(0.1f, minimumSpawnInterval);
        maximumSpawnInterval = Mathf.Max(minimumSpawnInterval, maximumSpawnInterval);
        maximumLivingNpcs = Mathf.Max(1, maximumLivingNpcs);
        spawnHeight = Mathf.Max(0f, spawnHeight);
    }
}
