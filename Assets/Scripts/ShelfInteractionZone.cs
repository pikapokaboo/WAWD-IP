// -----------------------------------------------------------------------------
// File: ShelfInteractionZone.cs
// Project: WAWD Integrated Studio Project
// Purpose: Defines a visible, resizable square where an NPC stands to use a shelf.
// -----------------------------------------------------------------------------

using UnityEngine;

/// <summary>A square shelf interaction area whose centre is the NPC destination.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class ShelfInteractionZone : MonoBehaviour
{
    [SerializeField] private Color gizmoColour = new(0.1f, 0.75f, 1f, 0.25f);

    private BoxCollider zoneCollider;
    private MonoBehaviour reservedBy;

    /// <summary>Gets the world-space centre of the square interaction area.</summary>
    public Vector3 Centre
    {
        get
        {
            CacheCollider();
            return zoneCollider != null
                ? transform.TransformPoint(zoneCollider.center)
                : transform.position;
        }
    }

    /// <summary>Attempts to reserve this shopping position for one NPC.</summary>
    public bool TryReserve(MonoBehaviour user)
    {
        if (user == null)
            return false;

        if (reservedBy == user)
            return true;

        if (reservedBy == null || !reservedBy.isActiveAndEnabled)
        {
            reservedBy = user;
            return true;
        }

        return false;
    }

    /// <summary>Returns whether this zone can currently be used by this action.</summary>
    public bool IsAvailableFor(MonoBehaviour user)
    {
        return user != null && (reservedBy == null
            || !reservedBy.isActiveAndEnabled || reservedBy == user);
    }

    /// <summary>Releases the zone if it is owned by the supplied NPC action.</summary>
    public void Release(MonoBehaviour user)
    {
        if (reservedBy == user)
            reservedBy = null;

    }

    private void Awake()
    {
        CacheCollider();
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;
    }

    private void Reset()
    {
        CacheCollider();
        if (zoneCollider == null)
            return;

        zoneCollider.isTrigger = true;
        zoneCollider.size = new Vector3(1.5f, 0.05f, 1.5f);
    }

    private void CacheCollider()
    {
        if (zoneCollider == null)
            zoneCollider = GetComponent<BoxCollider>();
    }

    private void OnDrawGizmos()
    {
        CacheCollider();
        if (zoneCollider == null)
            return;

        Matrix4x4 oldMatrix = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = gizmoColour;
        Gizmos.DrawCube(zoneCollider.center, zoneCollider.size);
        Gizmos.color = new Color(gizmoColour.r, gizmoColour.g, gizmoColour.b, 1f);
        Gizmos.DrawWireCube(zoneCollider.center, zoneCollider.size);
        Gizmos.matrix = oldMatrix;
    }
}
