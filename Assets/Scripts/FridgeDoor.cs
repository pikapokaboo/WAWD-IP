using UnityEngine;

/// <summary>Controls an interactable fridge door in Edit mode and at runtime.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class FridgeDoor : MonoBehaviour
{
    [Header("Door")]
    [SerializeField] private Transform door;
    [SerializeField] private Vector3 closedLocalEulerAngles;
    [Tooltip("The door's hinge axis in its own local space.")]
    [SerializeField] private Vector3 localHingeAxis = Vector3.up;
    [SerializeField] private float openAngle = 90f;
    [SerializeField, Min(1f)] private float rotationSpeed = 180f;

    [Header("Editor Preview")]
    [Tooltip("Opens the door in Edit mode without pressing Play.")]
    [SerializeField] private bool keepDoorOpenInEditor;

    private int activeUsers;
    private float currentAngle;

    public void BeginUse()
    {
        activeUsers++;
    }

    public void EndUse()
    {
        activeUsers = Mathf.Max(0, activeUsers - 1);
    }

    private void Update()
    {
        if (door == null)
            return;

        bool shouldOpen = Application.isPlaying
            ? activeUsers > 0
            : keepDoorOpenInEditor;
        float targetAngle = shouldOpen ? openAngle : 0f;

        if (Application.isPlaying)
            currentAngle = Mathf.MoveTowards(
                currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
        else
            currentAngle = targetAngle;

        Vector3 axis = localHingeAxis.sqrMagnitude > 0.001f
            ? localHingeAxis.normalized
            : Vector3.up;
        door.localRotation = Quaternion.Euler(closedLocalEulerAngles)
            * Quaternion.AngleAxis(currentAngle, axis);
    }

    private void OnDisable()
    {
        activeUsers = 0;
    }

    private void OnValidate()
    {
        rotationSpeed = Mathf.Max(1f, rotationSpeed);
        if (localHingeAxis.sqrMagnitude < 0.001f)
            localHingeAxis = Vector3.up;
    }
}
