using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Inspector data describing one trait that an NPC may roll.</summary>
[Serializable]
public sealed class NpcTrait
{
    [Tooltip("Unique name used by other scripts to query this trait.")]
    [SerializeField] private string traitName = "New Trait";

    [Tooltip("Show this trait in the NPC's overhead debug label.")]
    [SerializeField] private bool showInDebugLabel = true;

    [Tooltip("Independent chance, or the shared chance for an Either/Or pool.")]
    [SerializeField, Range(0f, 100f)] private float spawnChance = 50f;

    [Tooltip("Traits with the same non-empty pool name are mutually exclusive.")]
    [SerializeField] private string eitherOrPool;

    [Tooltip("Relative chance of this choice winning inside its pool.")]
    [SerializeField, Min(0f)] private float poolWeight = 1f;

    [Tooltip("Traits automatically added when this trait is selected.")]
    [SerializeField] private List<string> comesWith = new();

    public string Name => traitName?.Trim() ?? string.Empty;
    public bool ShowInDebugLabel => showInDebugLabel;
    public float SpawnChance => spawnChance;
    public string EitherOrPool => eitherOrPool?.Trim() ?? string.Empty;
    public float PoolWeight => Mathf.Max(0f, poolWeight);
    public IReadOnlyList<string> ComesWith => comesWith;
}

/// <summary>
/// Rolls and stores an NPC's traits. This component contains no behaviour;
/// other scripts decide what selected traits mean.
/// </summary>
[DisallowMultipleComponent]
public sealed class NpcTraits : MonoBehaviour
{
    [SerializeField] private List<NpcTrait> possibleTraits = new();
    [SerializeField] private bool rollOnAwake = true;
    [SerializeField] private bool logResult;

    private readonly List<NpcTrait> activeTraits = new();
    private readonly HashSet<string> activeNames =
        new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<NpcTrait> ActiveTraits => activeTraits;
    public bool HasRolled { get; private set; }

    /// <summary>Raised after a new trait selection has been completed.</summary>
    public event Action TraitsRolled;

    private void Awake()
    {
        if (rollOnAwake)
            RollTraits();
    }

    /// <summary>Clears the current result and performs all gacha rolls again.</summary>
    [ContextMenu("Roll Traits")]
    public void RollTraits()
    {
        activeTraits.Clear();
        activeNames.Clear();
        HasRolled = false;

        Dictionary<string, List<NpcTrait>> pools =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (NpcTrait trait in possibleTraits)
        {
            if (!IsValid(trait))
                continue;

            if (string.IsNullOrWhiteSpace(trait.EitherOrPool))
            {
                if (Roll(trait.SpawnChance))
                    SelectWithDependencies(trait, new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase));
                continue;
            }

            if (!pools.TryGetValue(trait.EitherOrPool, out List<NpcTrait> pool))
            {
                pool = new List<NpcTrait>();
                pools.Add(trait.EitherOrPool, pool);
            }

            pool.Add(trait);
        }

        foreach (List<NpcTrait> pool in pools.Values)
        {
            if (pool.Count > 0 && Roll(pool[0].SpawnChance))
                SelectWithDependencies(ChooseWeighted(pool), new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase));
        }

        HasRolled = true;
        TraitsRolled?.Invoke();

        if (logResult)
            Debug.Log($"{name} traits: {string.Join(", ", GetActiveNames())}", this);
    }

    /// <summary>Returns whether this NPC rolled the named trait.</summary>
    public bool HasTrait(string traitName)
    {
        return !string.IsNullOrWhiteSpace(traitName)
            && activeNames.Contains(traitName.Trim());
    }

    /// <summary>Copies the selected trait names into a new read-only result.</summary>
    public IReadOnlyList<string> GetActiveNames()
    {
        List<string> names = new(activeTraits.Count);
        foreach (NpcTrait trait in activeTraits)
            names.Add(trait.Name);
        return names;
    }

    private void SelectWithDependencies(NpcTrait trait, HashSet<string> chain)
    {
        if (!IsValid(trait) || activeNames.Contains(trait.Name) || !chain.Add(trait.Name))
            return;

        activeTraits.Add(trait);
        activeNames.Add(trait.Name);

        foreach (string requiredName in trait.ComesWith)
        {
            NpcTrait required = FindPossibleTrait(requiredName);
            if (required != null)
                SelectWithDependencies(required, chain);
            else
                Debug.LogWarning($"Trait '{trait.Name}' comes with unknown trait '{requiredName}'.", this);
        }

        chain.Remove(trait.Name);
    }

    private NpcTrait FindPossibleTrait(string traitName)
    {
        foreach (NpcTrait trait in possibleTraits)
        {
            if (IsValid(trait) && string.Equals(trait.Name, traitName?.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                return trait;
        }

        return null;
    }

    private static NpcTrait ChooseWeighted(List<NpcTrait> pool)
    {
        float totalWeight = 0f;
        foreach (NpcTrait trait in pool)
            totalWeight += trait.PoolWeight;

        if (totalWeight <= 0f)
            return pool[UnityEngine.Random.Range(0, pool.Count)];

        float result = UnityEngine.Random.value * totalWeight;
        foreach (NpcTrait trait in pool)
        {
            result -= trait.PoolWeight;
            if (result <= 0f)
                return trait;
        }

        return pool[^1];
    }

    private static bool IsValid(NpcTrait trait)
    {
        return trait != null && !string.IsNullOrWhiteSpace(trait.Name);
    }

    private static bool Roll(float chance)
    {
        return UnityEngine.Random.value * 100f < chance;
    }
}
