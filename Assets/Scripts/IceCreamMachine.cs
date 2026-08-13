// -----------------------------------------------------------------------------
// File: IceCreamMachine.cs
// Project: WAWD Integrated Studio Project
// Purpose: Defines ice-cream products, occupancy, and doors.
// -----------------------------------------------------------------------------

using System.Collections.Generic;
using UnityEngine;

/// <summary>Shares occupancy and controls the two sliding doors of an ice-cream freezer.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class IceCreamMachine : MonoBehaviour
{
    public enum Side { Left, Right }

    [Header("Products")]
    [Tooltip("Products NPCs can collect from pos_L.")]
    [SerializeField] private List<string> leftProducts = new();
    [Tooltip("Products NPCs can collect from pos_R.")]
    [SerializeField] private List<string> rightProducts = new();

    [Header("Sliding Doors")]
    [SerializeField] private Transform rightDoor;
    [SerializeField] private Transform leftDoor;
    [SerializeField, Min(0f)] private float slideDistance = 0.65f;
    [SerializeField, Min(0.01f)] private float slideSpeed = 100f;

    [Header("Editor Preview")]
    [SerializeField] private bool keepRightDoorOpenInEditor;
    [SerializeField] private bool keepLeftDoorOpenInEditor;

    private ShelfStation reservedBy;
    private ShelfStation activeStation;
    private Vector3 rightClosedPosition;
    private Vector3 leftClosedPosition;
    private bool positionsCached;

    public bool IsOccupiedByOther(ShelfStation station) =>
        reservedBy != null && reservedBy != station;

    public IReadOnlyList<string> GetProducts(Side side) =>
        side == Side.Left ? leftProducts : rightProducts;

    public bool TryReserve(ShelfStation station)
    {
        if (station == null || IsOccupiedByOther(station))
            return false;
        reservedBy = station;
        return true;
    }

    public void Release(ShelfStation station)
    {
        if (reservedBy == station)
            reservedBy = null;
    }

    public void BeginUse(ShelfStation station)
    {
        if (reservedBy == station)
            activeStation = station;
    }

    public void EndUse(ShelfStation station)
    {
        if (activeStation == station)
            activeStation = null;
    }

    private void OnEnable()
    {
        FindDoors();
        CacheClosedPositions();
    }

    private void Update()
    {
        FindDoors();
        CacheClosedPositions();
        bool openRight = Application.isPlaying
            ? activeStation != null && activeStation.MachineSide == Side.Right
            : keepRightDoorOpenInEditor;
        bool openLeft = Application.isPlaying
            ? activeStation != null && activeStation.MachineSide == Side.Left
            : keepLeftDoorOpenInEditor;

        MoveDoor(rightDoor, rightClosedPosition, openRight ? -slideDistance : 0f);
        MoveDoor(leftDoor, leftClosedPosition, openLeft ? slideDistance : 0f);
    }

    private void MoveDoor(Transform target, Vector3 closed, float zOffset)
    {
        if (target == null)
            return;
        Vector3 destination = closed + Vector3.forward * zOffset;
        target.localPosition = Application.isPlaying
            ? Vector3.MoveTowards(target.localPosition, destination, slideSpeed * Time.deltaTime)
            : destination;
    }

    private void FindDoors()
    {
        if (rightDoor != null && leftDoor != null)
            return;
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "icecream_fridgedoor4") rightDoor = child;
            else if (child.name == "icecream_fridgedoor5") leftDoor = child;
        }
    }

    private void CacheClosedPositions()
    {
        if (positionsCached || rightDoor == null || leftDoor == null)
            return;
        rightClosedPosition = rightDoor.localPosition;
        leftClosedPosition = leftDoor.localPosition;
        positionsCached = true;
    }

    private void OnDisable()
    {
        reservedBy = null;
        activeStation = null;
    }
}
