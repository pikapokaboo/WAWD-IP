// -----------------------------------------------------------------------------
// File: NpcSpawningPad.cs
// Project: WAWD Integrated Studio Project
// Purpose: Periodically creates NPC prefab instances above a spawning pad.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Spawns NPCs at a configurable interval with an optional live NPC safety limit.
/// </summary>
[DisallowMultipleComponent]
public sealed class NpcSpawningPad : MonoBehaviour
{
    private static readonly List<NpcSpawningPad> SpawnPads = new();
    private static readonly List<GameObject> LivingNpcs = new();
    private static NpcSpawningPad coordinator;
    private static int nextPadIndex;

    [Header("Spawn Settings")]
    [Tooltip("NPC prefab created by this pad.")]
    [SerializeField] private GameObject npcPrefab;

    [Tooltip("Optional transform used as the exact spawn position and rotation.")]
    [SerializeField] private Transform spawnPoint;

    [Tooltip("NPCs created by this pad will leave through this despawning pad.")]
    [SerializeField] private Transform pairedDespawningPad;

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
    [Tooltip("Keep spawning passers-by between scheduled customers. These NPCs only go home.")]
    [SerializeField] private bool spawnHomeboundNpcs = true;

    private float nextSpawnTime;
    private DayNightCycle dayCycle;
    private int scheduledDay;
    private readonly List<ScheduledSpawn> dailySchedule = new();
    private int nextScheduledSpawn;

    private void OnEnable()
    {
        if (!SpawnPads.Contains(this))
            SpawnPads.Add(this);
        if (coordinator == null)
            coordinator = this;
        if (coordinator != this)
            return;

        dayCycle = IsHomeMenuScene() ? null : FindFirstObjectByType<DayNightCycle>();
        if (useDailyQuotas && dayCycle != null)
        {
            BuildDailySchedule();
            nextSpawnTime = Time.time + (spawnImmediately ? 0f : GetRandomSpawnDelay());
            return;
        }
        nextSpawnTime = Time.time + (spawnImmediately ? 0f : GetRandomSpawnDelay());
    }

    private void Update()
    {
        if (coordinator != this)
            return;

        if (useDailyQuotas && dayCycle != null)
        {
            UpdateDailySchedule();
            return;
        }
        if (Time.time < nextSpawnTime)
            return;

        RemoveDestroyedNpcs();

        if (maximumLivingNpcs == 0 || LivingNpcs.Count < maximumLivingNpcs)
            SpawnFromNextPad(false, false, IsHomeMenuScene());

        nextSpawnTime = Time.time + GetRandomSpawnDelay();
    }

    /// <summary>
    /// Creates one NPC if a prefab is assigned and the live limit has not been reached.
    /// </summary>
    public void SpawnNpc()
    {
        SpawnNpc(false, false, false);
    }

    private void SpawnNpc(bool forceCustomerType, bool shoplifter,
        bool forceHomebound)
    {
        if (npcPrefab == null)
        {
            Debug.LogWarning($"{nameof(NpcSpawningPad)} on '{name}' needs an NPC prefab.", this);
            enabled = false;
            return;
        }

        RemoveDestroyedNpcs();
        if (maximumLivingNpcs > 0 && LivingNpcs.Count >= maximumLivingNpcs)
            return;

        Vector3 position = spawnPoint != null
            ? spawnPoint.position
            : transform.position + transform.up * spawnHeight;
        Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        GameObject spawnedNpc = Instantiate(npcPrefab, position, rotation);
        NpcNavigation navigation = spawnedNpc.GetComponent<NpcNavigation>();
        if (pairedDespawningPad != null)
            navigation?.SetHomeTarget(pairedDespawningPad);
        if (forceCustomerType)
        {
            NpcTraits traits = spawnedNpc.GetComponent<NpcTraits>();
            traits?.ForcePoolChoice("Spending Type",
                shoplifter ? "No Money" : Random.value < 0.5f
                    ? "Light Spender" : "Heavy Spender");
            navigation?.ForceShoppingIntent(true);
        }
        else if (forceHomebound)
            navigation?.ForceShoppingIntent(false);
        LivingNpcs.Add(spawnedNpc);
    }

    private void SpawnFromNextPad(bool forceCustomerType, bool shoplifter,
        bool forceHomebound)
    {
        NpcSpawningPad pad = GetNextSpawnPad();
        if (pad != null)
            pad.SpawnNpc(forceCustomerType, shoplifter, forceHomebound);
    }

    private static NpcSpawningPad GetNextSpawnPad()
    {
        SpawnPads.RemoveAll(pad => pad == null);
        if (SpawnPads.Count == 0)
            return null;

        for (int checkedPads = 0; checkedPads < SpawnPads.Count; checkedPads++)
        {
            int index = nextPadIndex % SpawnPads.Count;
            nextPadIndex = (index + 1) % SpawnPads.Count;
            NpcSpawningPad pad = SpawnPads[index];
            if (pad.isActiveAndEnabled && pad.npcPrefab != null)
                return pad;
        }
        return null;
    }

    private void UpdateDailySchedule()
    {
        if (!dayCycle.DayActive)
            return;
        int currentDay = dayCycle != null ? dayCycle.CurrentDay : 1;
        if (currentDay != scheduledDay)
        {
            BuildDailySchedule();
            nextSpawnTime = Time.time + GetRandomSpawnDelay();
        }
        if (dayCycle.DayEnded)
            return;

        RemoveDestroyedNpcs();
        bool hasRoom = maximumLivingNpcs == 0 || LivingNpcs.Count < maximumLivingNpcs;
        bool spawnedScheduledCustomer = false;

        if (hasRoom && nextScheduledSpawn < dailySchedule.Count
            && dayCycle.DayProgress >= dailySchedule[nextScheduledSpawn].progress)
        {
            SpawnFromNextPad(true, dailySchedule[nextScheduledSpawn].shoplifter, false);
            nextScheduledSpawn++;
            spawnedScheduledCustomer = true;
            hasRoom = maximumLivingNpcs == 0 || LivingNpcs.Count < maximumLivingNpcs;
        }

        if (spawnHomeboundNpcs && !spawnedScheduledCustomer
            && hasRoom && Time.time >= nextSpawnTime)
        {
            SpawnFromNextPad(false, false, true);
            nextSpawnTime = Time.time + GetRandomSpawnDelay();
        }
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
        LivingNpcs.RemoveAll(npc => npc == null);
    }

    private void OnDisable()
    {
        SpawnPads.Remove(this);
        if (coordinator != this)
            return;

        coordinator = SpawnPads.Count > 0 ? SpawnPads[0] : null;
        if (coordinator != null && coordinator.isActiveAndEnabled)
            coordinator.InitialiseCoordinator();
    }

    private void InitialiseCoordinator()
    {
        dayCycle = IsHomeMenuScene() ? null : FindFirstObjectByType<DayNightCycle>();
        if (useDailyQuotas && dayCycle != null)
            BuildDailySchedule();
        nextSpawnTime = Time.time + (spawnImmediately ? 0f : GetRandomSpawnDelay());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSpawnNetwork()
    {
        SpawnPads.Clear();
        LivingNpcs.Clear();
        coordinator = null;
        nextPadIndex = 0;
    }

    private float GetRandomSpawnDelay()
    {
        return Random.Range(minimumSpawnInterval, maximumSpawnInterval);
    }

    private static bool IsHomeMenuScene()
    {
        return SceneManager.GetActiveScene().name == "Home_Screen";
    }

    private void OnValidate()
    {
        minimumSpawnInterval = Mathf.Max(0.1f, minimumSpawnInterval);
        maximumSpawnInterval = Mathf.Max(minimumSpawnInterval, maximumSpawnInterval);
        maximumLivingNpcs = Mathf.Max(0, maximumLivingNpcs);
        spawnHeight = Mathf.Max(0f, spawnHeight);
    }
}
