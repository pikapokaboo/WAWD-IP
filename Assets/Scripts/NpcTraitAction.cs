// -----------------------------------------------------------------------------
// File: NpcTraitAction.cs
// Project: WAWD Integrated Studio Project
// Purpose: Defines the base component for modular NPC trait action scripts.
// -----------------------------------------------------------------------------

using UnityEngine;

/// <summary>
/// Base class for a script that performs one NPC trait's behaviour.
/// Create a separate derived component for each implemented action.
/// </summary>
public abstract class NpcTraitAction : MonoBehaviour
{
    /// <summary>Gets whether the owning trait profile selected this action.</summary>
    public bool IsTraitActive { get; private set; }

    internal void SetTraitActive(bool active)
    {
        if (IsTraitActive == active)
        {
            enabled = active;
            return;
        }

        IsTraitActive = active;
        enabled = active;

        if (active)
            OnTraitActivated();
        else
            OnTraitDeactivated();
    }

    /// <summary>
    /// Called after this action's trait is selected and the component is enabled.
    /// </summary>
    protected virtual void OnTraitActivated()
    {
    }

    /// <summary>
    /// Called after this action's trait is removed and the component is disabled.
    /// </summary>
    protected virtual void OnTraitDeactivated()
    {
    }
}
