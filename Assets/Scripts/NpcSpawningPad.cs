// -----------------------------------------------------------------------------
// File: NpcSpawningPad.cs
// Project: WAWD Integrated Studio Project
// Purpose: Periodically creates NPC prefab instances above a spawning pad.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns NPCs at a configurable interval with an optional live NPC safety limit.
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
    [Tooltip("Optional safety limit. Set to 0 to allow continuous spawning.")]
    [SerializeField, Min(0)] private int maximumLivingNpcs;
    [SerializeField] private bool spawnImmediately = true;

    [Header("Daily Spawn Quotas")]
    [SerializeField] private bool useDailyQuotas = true;
    [SerializeField, Min(0)] private int dayOneLegitimateShoppers = 12;
    [SerializeField, Min(0)] private int dayOneShoplifters = 3;
    [SerializeField, Min(0)] private int legitimateIncreasePerDay = 3;
    [SerializeField, Min(0)] private int shoplifterIncreasePerDay = 1;
    [Tooltip("Small timing variation while keeping customers evenly spread through the day.")]
    [SerializeField, Range(0f, 0.4f)] private float scheduleJitter = 0.18f;

    private readonly List<GameObject> livingNpcs = new();
    private float nextSpawnTime;
    private DayNightCycle dayCycle;
    private int scheduledDay;
    private readonly List<ScheduledSpawn> dailySchedule = new();
    private int nextScheduledSpawn;

    private void OnEnable()
    {
        dayCycle = FindFirstObjectByType<DayNightCycle>();
        if (useDailyQuotas && dayCycle != null)
        {
            BuildDailySchedule();
            return;
        }
        nextSpawnTime = Time.time + (spawnImmediately ? 0f : GetRandomSpawnDelay());
    }

    private void Update()
    {
        if (useDailyQuotas && dayCycle != null)
        {
            UpdateDailySchedule();
            return;
        }
        if (Time.time < nextSpawnTime)
            return;

        RemoveDestroyedNpcs();

        if (maximumLivingNpcs == 0 || livingNpcs.Count < maximumLivingNpcs)
            SpawnNpc();

        nextSpawnTime = Time.time + GetRandomSpawnDelay();
    }

    /// <summary>
    /// Creates one NPC if a prefab is assigned and the live limit has not been reached.
    /// </summary>
    public void SpawnNpc()
    {
        SpawnNpc(false, false);
    }

    private void SpawnNpc(bool forceCustomerType, bool shoplifter)
    {
        if (npcPrefab == null)
        {
            Debug.LogWarning($"{nameof(NpcSpawningPad)} on '{name}' needs an NPC prefab.", this);
            enabled = false;
            return;
        }

        RemoveDestroyedNpcs();
        if (maximumLivingNpcs > 0 && livingNpcs.Count >= maximumLivingNpcs)
            return;

        Vector3 position = spawnPoint != null
            ? spawnPoint.position
            : transform.position + transform.up * spawnHeight;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        GameObject spawnedNpc = Instantiate(npcPrefab, position, rotation);
        if (forceCustomerType)
        {
            NpcTraits traits = spawnedNpc.GetComponent<NpcTraits>();
            traits?.ForcePoolChoice("Spending Type",
                shoplifter ? "No Money" : Random.value < 0.5f
                    ? "Light Spender" : "Heavy Spender");
            spawnedNpc.GetComponent<NpcNavigation>()?.ForceShoppingIntent(true);
        }
        livingNpcs.Add(spawnedNpc);
    }

    private void UpdateDailySchedule()
    {
        int currentDay = dayCycle != null ? dayCycle.CurrentDay : 1;
        if (currentDay != scheduledDay)
            BuildDailySchedule();
        if (dayCycle.DayEnded || nextScheduledSpawn >= dailySchedule.Count
            || dayCycle.DayProgress < dailySchedule[nextScheduledSpawn].progress)
            return;

        RemoveDestroyedNpcs();
        if (maximumLivingNpcs > 0 && livingNpcs.Count >= maximumLivingNpcs)
            return;
        SpawnNpc(true, dailySchedule[nextScheduledSpawn].shoplifter);
        nextScheduledSpawn++;
    }

    private void BuildDailySchedule()
    {
        scheduledDay = dayCycle != null ? dayCycle.CurrentDay : 1;
        int dayOffset = Mathf.Max(0, scheduledDay - 1);
        int legitimate = dayOneLegitimateShoppers
            + legitimateIncreasePerDay * dayOffset;
        int shoplifters = dayOneShoplifters + shoplifterIncreasePerDay * dayOffset;
        int total = legitimate + shoplifters;
        dailySchedule.Clear();
        nextScheduledSpawn = 0;
        if (total == 0) return;

        List<bool> types = new(total);
        for (int i = 0; i < legitimate; i++) types.Add(false);
        for (int i = 0; i < shoplifters; i++) types.Add(true);
        for (int i = types.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (types[i], types[swap]) = (types[swap], types[i]);
        }

        float slot = 1f / total;
        for (int i = 0; i < total; i++)
        {
            float progress = (i + 0.5f) * slot
                + Random.Range(-scheduleJitter, scheduleJitter) * slot;
            dailySchedule.Add(new ScheduledSpawn(
                Mathf.Clamp(progress, 0.01f, 0.99f), types[i]));
        }
        dailySchedule.Sort((a, b) => a.progress.CompareTo(b.progress));
    }

    private readonly struct ScheduledSpawn
    {
        public readonly float progress;
        public readonly bool shoplifter;
        public ScheduledSpawn(float progress, bool shoplifter)
        {
            this.progress = progress;
            this.shoplifter = shoplifter;
        }
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
        maximumLivingNpcs = Mathf.Max(0, maximumLivingNpcs);
        spawnHeight = Mathf.Max(0f, spawnHeight);
    }
}
