// -----------------------------------------------------------------------------
// File: NpcTraitProfile.cs
// Project: WAWD Integrated Studio Project
// Purpose: Randomly assigns compatible modular traits whenever an NPC spawns.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Identifies whether a trait describes a destination or general NPC behaviour.
/// </summary>
public enum NpcTraitType
{
    Behavioural,
    Navigational
}

/// <summary>
/// Inspector configuration and runtime state for one possible NPC trait.
/// </summary>
[Serializable]
public sealed class NpcTraitOption
{
    [Tooltip("Unique name used by compatibility checks and gameplay queries.")]
    [SerializeField] private string traitName = "New Trait";

    [SerializeField] private NpcTraitType traitType;

    [Tooltip("Independent chance that this trait is rolled when the NPC spawns.")]
    [SerializeField, Range(0f, 100f)] private float selectionChance = 50f;

    [Tooltip("Higher values take priority. Only used by navigational traits.")]
    [SerializeField] private int navigationPriority;

    [Tooltip("Turn this off if selecting this trait should prevent every other trait.")]
    [SerializeField] private bool canCombineWithOtherTraits = true;

    [Tooltip("Trait names that cannot be selected together with this trait.")]
    [SerializeField] private List<string> incompatibleTraitNames = new();

    [Tooltip("Optional modular action component enabled when this trait is selected.")]
    [SerializeField] private NpcTraitAction action;

    [NonSerialized] private bool isSelected;

    /// <summary>Gets the unique display/query name of this trait.</summary>
    public string TraitName => traitName;

    /// <summary>Gets whether this is a behavioural or navigational trait.</summary>
    public NpcTraitType TraitType => traitType;

    /// <summary>Gets this trait's independent spawn percentage.</summary>
    public float SelectionChance => selectionChance;

    /// <summary>Gets the priority used to order active navigational traits.</summary>
    public int NavigationPriority => navigationPriority;

    /// <summary>Gets whether this trait may coexist with other traits.</summary>
    public bool CanCombineWithOtherTraits => canCombineWithOtherTraits;

    /// <summary>Gets the modular action component linked to this trait.</summary>
    public NpcTraitAction Action => action;

    /// <summary>Gets whether this trait was selected for the current NPC.</summary>
    public bool IsSelected => isSelected;

    internal void SetSelected(bool selected)
    {
        isSelected = selected;
    }

    internal bool ExplicitlyConflictsWith(NpcTraitOption other)
    {
        return ContainsName(incompatibleTraitNames, other.traitName)
            || ContainsName(other.incompatibleTraitNames, traitName);
    }

    private static bool ContainsName(List<string> names, string candidate)
    {
        if (names == null || string.IsNullOrWhiteSpace(candidate))
            return false;

        foreach (string value in names)
        {
            if (string.Equals(value?.Trim(), candidate.Trim(),
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

/// <summary>
/// Rolls an NPC's traits on spawn, resolves incompatibilities, enables linked
/// action components, and exposes the resulting traits to other systems.
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class NpcTraitProfile : MonoBehaviour
{
    [Tooltip("All traits this type of NPC is allowed to roll.")]
    [SerializeField] private List<NpcTraitOption> possibleTraits = new();

    [Tooltip("Print selected traits to the Console when this NPC spawns.")]
    [SerializeField] private bool logSelectedTraits;

    private readonly List<NpcTraitOption> activeTraits = new();
    private readonly List<NpcTraitOption> activeNavigationalTraits = new();

    /// <summary>Gets every trait selected for this NPC.</summary>
    public IReadOnlyList<NpcTraitOption> ActiveTraits => activeTraits;

    /// <summary>
    /// Gets selected navigational traits ordered from highest to lowest priority.
    /// </summary>
    public IReadOnlyList<NpcTraitOption> ActiveNavigationalTraits =>
        activeNavigationalTraits;

    private void Awake()
    {
        RollTraits();
    }

    /// <summary>
    /// Clears the current selection and performs fresh percentage rolls.
    /// </summary>
    [ContextMenu("Reroll Traits")]
    public void RollTraits()
    {
        DisableAllActions();
        activeTraits.Clear();
        activeNavigationalTraits.Clear();

        List<NpcTraitOption> candidates = new(possibleTraits);
        Shuffle(candidates);

        foreach (NpcTraitOption candidate in candidates)
        {
            if (!IsValid(candidate) || HasDuplicateActiveName(candidate.TraitName))
                continue;

            if (UnityEngine.Random.value * 100f > candidate.SelectionChance)
                continue;

            if (!IsCompatibleWithActiveTraits(candidate))
                continue;

            candidate.SetSelected(true);
            activeTraits.Add(candidate);

            if (candidate.TraitType == NpcTraitType.Navigational)
                activeNavigationalTraits.Add(candidate);
        }

        activeNavigationalTraits.Sort((left, right) =>
            right.NavigationPriority.CompareTo(left.NavigationPriority));

        foreach (NpcTraitOption trait in activeTraits)
            trait.Action?.SetTraitActive(true);

        if (logSelectedTraits)
            Debug.Log($"{name} traits: {BuildSelectedTraitText()}", this);
    }

    /// <summary>
    /// Returns whether this NPC currently owns the named trait.
    /// </summary>
    public bool HasTrait(string traitName)
    {
        foreach (NpcTraitOption trait in activeTraits)
        {
            if (string.Equals(trait.TraitName, traitName,
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the highest-priority active navigation trait, if one exists.
    /// </summary>
    public bool TryGetHighestPriorityNavigationTrait(out NpcTraitOption trait)
    {
        if (activeNavigationalTraits.Count > 0)
        {
            trait = activeNavigationalTraits[0];
            return true;
        }

        trait = null;
        return false;
    }

    private void DisableAllActions()
    {
        HashSet<NpcTraitAction> handledActions = new();

        foreach (NpcTraitOption trait in possibleTraits)
        {
            trait?.SetSelected(false);

            if (trait?.Action != null && handledActions.Add(trait.Action))
                trait.Action.SetTraitActive(false);
        }
    }

    private bool IsCompatibleWithActiveTraits(NpcTraitOption candidate)
    {
        if (!candidate.CanCombineWithOtherTraits && activeTraits.Count > 0)
            return false;

        foreach (NpcTraitOption selected in activeTraits)
        {
            if (!selected.CanCombineWithOtherTraits
                || candidate.ExplicitlyConflictsWith(selected))
                return false;
        }

        return true;
    }

    private bool HasDuplicateActiveName(string traitName)
    {
        foreach (NpcTraitOption selected in activeTraits)
        {
            if (string.Equals(selected.TraitName, traitName,
                    StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool IsValid(NpcTraitOption trait)
    {
        return trait != null && !string.IsNullOrWhiteSpace(trait.TraitName);
    }

    private static void Shuffle(List<NpcTraitOption> traits)
    {
        for (int i = traits.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            (traits[i], traits[swapIndex]) = (traits[swapIndex], traits[i]);
        }
    }

    private string BuildSelectedTraitText()
    {
        if (activeTraits.Count == 0)
            return "(none)";

        List<string> names = new(activeTraits.Count);
        foreach (NpcTraitOption trait in activeTraits)
            names.Add(trait.TraitName);
        return string.Join(", ", names);
    }
}
