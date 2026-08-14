// -----------------------------------------------------------------------------
// File: NpcDespawningPad.cs
// Project: WAWD Integrated Studio Project
// Purpose: Removes NPCs that enter the despawning pad trigger.
// -----------------------------------------------------------------------------

using UnityEngine;

/// <summary>
/// Destroys marked NPCs when one of their colliders enters this trigger.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class NpcDespawningPad : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("NPC"))
        {
            NpcNavigation navigation = other.GetComponent<NpcNavigation>();
            if (navigation != null && navigation.IsReportableShoplifter)
                DayNightCycle.Instance?.ReportEscapedShoplifter();
            Destroy(other.gameObject);
        }
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }
}
