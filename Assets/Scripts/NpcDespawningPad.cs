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
        NpcPathToHome npc = other.GetComponentInParent<NpcPathToHome>();
        if (npc != null)
            Destroy(npc.gameObject);
    }

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }
}
