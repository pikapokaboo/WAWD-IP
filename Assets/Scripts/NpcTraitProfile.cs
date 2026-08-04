// -----------------------------------------------------------------------------
// File: NpcTraitProfile.cs
// Project: WAWD Integrated Studio Project
// Purpose: Assigns modular traits, either/or choices, and required companion
//          traits whenever an NPC spawns.
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
    [Tooltip("Unique name used by Comes With entries and gameplay queries.")]
    [SerializeField] private string traitName = "New Trait";

    [SerializeField] private NpcTraitType traitType;

    [Tooltip("Chance to obtain this trait. For an Either/Or group, the group rolls once using this chance; keep it identical on every member.")]
    [SerializeField, Range(0f, 100f)] private float selectionChance = 50f;

    [Tooltip("Higher values take priority. Only used by navigational traits.")]
    [SerializeField] private int navigationPriority;

    [Header("Either / Or")]
    [Tooltip("Traits with the same non-empty group name are mutually exclusive. The group can contain any number of traits.")]
    [SerializeField] private string eitherOrGroup;

    [Tooltip("Relative chance of choosing this trait after its Either/Or group succeeds. Equal weights give equal odds.")]
    [SerializeField, Min(0f)] private float eitherOrWeight = 1f;

    [Header("Required Traits")]
    [Tooltip("Trait names automatically granted whenever this trait is selected. Their own percentage rolls are ignored.")]
    [SerializeField] private List<string> comesWithTraitNames = new();

    [Tooltip("Optional modular action component enabled when this trait is selected.")]
    [SerializeField] private NpcTraitAction action;

    [NonSerialized] private bool isSelected;

    /// <summary>Gets the unique display/query name of this trait.</summary>
    public string TraitName => traitName;

    /// <summary>Gets whether this is a behavioural or navigational trait.</summary>
    public NpcTraitType TraitType => traitType;

    /// <summary>Gets this trait or its group's spawn percentage.</summary>
    public float SelectionChance => selectionChance;

    /// <summary>Gets the priority used to order active navigational traits.</summary>
    public int NavigationPriority => navigationPriority;

    /// <summary>Gets the mutually exclusive group name, or an empty string.</summary>
    public string EitherOrGroup => eitherOrGroup?.Trim() ?? string.Empty;

    /// <summary>Gets this choice's relative weight within its group.</summary>
    public float EitherOrWeight => Mathf.Max(0f, eitherOrWeight);

    /// <summary>Gets the names of traits automatically granted with this one.</summary>
    public IReadOnlyList<string> ComesWithTraitNames => comesWithTraitNames;

    /// <summary>Gets the modular action component linked to this trait.</summary>
    public NpcTraitAction Action => action;

    /// <summary>Gets whether this trait was selected for the current NPC.</summary>
    public bool IsSelected => isSelected;

    internal void SetSelected(bool selected)
    {
        isSelected = selected;
    }
}

/// <summary>
/// Rolls an NPC's independent traits and either/or groups, grants required
/// companion traits, enables linked actions, and exposes the result.
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class NpcTraitProfile : MonoBehaviour
{
    [Tooltip("All traits this type of NPC is allowed to receive.")]
    [SerializeField] private List<NpcTraitOption> possibleTraits = new();

    [Tooltip("Print selected traits to the Console when this NPC spawns.")]
    [SerializeField] private bool logSelectedTraits;

    private readonly List<NpcTraitOption> activeTraits = new();
    private readonly List<NpcTraitOption> activeNavigationalTraits = new();
    private readonly HashSet<string> selectedNames =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> selectedEitherOrGroups =
        new(StringComparer.OrdinalIgnoreCase);

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
    /// Clears the current selection and performs fresh trait and group rolls.
    /// </summary>
    [ContextMenu("Reroll Traits")]
    public void RollTraits()
    {
        ResetSelection();

        Dictionary<string, List<NpcTraitOption>> eitherOrGroups =
            new(StringComparer.OrdinalIgnoreCase);
        List<NpcTraitOption> independentTraits = new();

        foreach (NpcTraitOption trait in possibleTraits)
        {
            if (!IsValid(trait))
                continue;

            if (string.IsNullOrWhiteSpace(trait.EitherOrGroup))
            {
                independentTraits.Add(trait);
                continue;
            }

            if (!eitherOrGroups.TryGetValue(trait.EitherOrGroup, out var group))
            {
                group = new List<NpcTraitOption>();
                eitherOrGroups.Add(trait.EitherOrGroup, group);
            }

            group.Add(trait);
        }

        Shuffle(independentTraits);
        foreach (NpcTraitOption trait in independentTraits)
        {
            if (RollSucceeded(trait.SelectionChance))
                SelectTraitAndDependencies(trait, new HashSet<string>(
                    StringComparer.OrdinalIgnoreCase));
        }

        List<List<NpcTraitOption>> shuffledGroups = new(eitherOrGroups.Values);
        Shuffle(shuffledGroups);
        foreach (List<NpcTraitOption> group in shuffledGroups)
            RollEitherOrGroup(group);

        activeNavigationalTraits.Sort((left, right) =>
            right.NavigationPriority.CompareTo(left.NavigationPriority));

        foreach (NpcTraitOption trait in activeTraits)
            trait.Action?.SetTraitActive(true);

        if (logSelectedTraits)
            Debug.Log($"{name} traits: {BuildSelectedTraitText()}", this);
    }

    /// <summary>Returns whether this NPC currently owns the named trait.</summary>
    public bool HasTrait(string traitName)
    {
        return !string.IsNullOrWhiteSpace(traitName)
            && selectedNames.Contains(traitName.Trim());
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

    private void RollEitherOrGroup(List<NpcTraitOption> group)
    {
        if (group.Count == 0
            || selectedEitherOrGroups.Contains(group[0].EitherOrGroup)
            || !RollSucceeded(group[0].SelectionChance))
            return;

        NpcTraitOption choice = ChooseWeighted(group);
        if (choice != null)
        {
            SelectTraitAndDependencies(choice, new HashSet<string>(
                StringComparer.OrdinalIgnoreCase));
        }
    }

    private void SelectTraitAndDependencies(NpcTraitOption trait,
        HashSet<string> dependencyChain)
    {
        if (!IsValid(trait) || selectedNames.Contains(trait.TraitName))
            return;

        if (!dependencyChain.Add(trait.TraitName))
        {
            Debug.LogWarning(
                $"A Comes With cycle involving '{trait.TraitName}' was ignored.", this);
            return;
        }

        string groupName = trait.EitherOrGroup;
        if (!string.IsNullOrWhiteSpace(groupName)
            && selectedEitherOrGroups.Contains(groupName))
        {
            Debug.LogWarning(
                $"Trait '{trait.TraitName}' could not be granted because another trait "
                + $"from Either/Or group '{groupName}' is already selected.", this);
            dependencyChain.Remove(trait.TraitName);
            return;
        }

        trait.SetSelected(true);
        activeTraits.Add(trait);
        selectedNames.Add(trait.TraitName.Trim());

        if (!string.IsNullOrWhiteSpace(groupName))
            selectedEitherOrGroups.Add(groupName);

        if (trait.TraitType == NpcTraitType.Navigational)
            activeNavigationalTraits.Add(trait);

        foreach (string requiredName in trait.ComesWithTraitNames)
        {
            NpcTraitOption requiredTrait = FindTrait(requiredName);
            if (requiredTrait == null)
            {
                Debug.LogWarning(
                    $"Trait '{trait.TraitName}' requires unknown trait '{requiredName}'.",
                    this);
                continue;
            }

            SelectTraitAndDependencies(requiredTrait, dependencyChain);
        }

        dependencyChain.Remove(trait.TraitName);
    }

    private void ResetSelection()
    {
        HashSet<NpcTraitAction> handledActions = new();

        foreach (NpcTraitOption trait in possibleTraits)
        {
            trait?.SetSelected(false);

            if (trait?.Action != null && handledActions.Add(trait.Action))
                trait.Action.SetTraitActive(false);
        }

        activeTraits.Clear();
        activeNavigationalTraits.Clear();
        selectedNames.Clear();
        selectedEitherOrGroups.Clear();
    }

    private NpcTraitOption FindTrait(string traitName)
    {
        if (string.IsNullOrWhiteSpace(traitName))
            return null;

        foreach (NpcTraitOption trait in possibleTraits)
        {
            if (IsValid(trait) && string.Equals(trait.TraitName.Trim(),
                    traitName.Trim(), StringComparison.OrdinalIgnoreCase))
                return trait;
        }

        return null;
    }

    private static NpcTraitOption ChooseWeighted(List<NpcTraitOption> choices)
    {
        float totalWeight = 0f;
        foreach (NpcTraitOption choice in choices)
            totalWeight += choice.EitherOrWeight;

        if (totalWeight <= 0f)
            return choices[UnityEngine.Random.Range(0, choices.Count)];

        float roll = UnityEngine.Random.value * totalWeight;
        foreach (NpcTraitOption choice in choices)
        {
            roll -= choice.EitherOrWeight;
            if (roll <= 0f)
                return choice;
        }

        return choices[^1];
    }

    private static bool RollSucceeded(float percentage)
    {
        return UnityEngine.Random.value * 100f < percentage;
    }

    private static bool IsValid(NpcTraitOption trait)
    {
        return trait != null && !string.IsNullOrWhiteSpace(trait.TraitName);
    }

    private static void Shuffle<T>(List<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
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
