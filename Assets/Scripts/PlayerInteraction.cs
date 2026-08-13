// -----------------------------------------------------------------------------
// File: PlayerInteraction.cs
// Project: WAWD Integrated Studio Project
// Purpose: Raycasts to target and activate world interactions.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private Camera playerCamera;
    [SerializeField, Min(0.5f)] private float interactionDistance = 4f;
    [SerializeField] private LayerMask interactionLayers = ~0;

    [Header("Crosshair")]
    [SerializeField, Min(1f)] private float crosshairSize = 5f;
    [SerializeField] private Color crosshairColour = Color.white;
    [SerializeField, Min(12)] private int promptFontSize = 20;

    private CashierInteractable targetedCashier;
    private WorkstationInteractable targetedWorkstation;
    private CashierInteractable conversingCashier;
    private PlayerController playerController;
    private Texture2D crosshairTexture;
    private GUIStyle promptStyle;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        if (CctvSystem.IsActive)
        {
            ClearTargets();
            return;
        }
        CashierInteractable newTarget = FindCashierTarget();
        if (newTarget != targetedCashier)
        {
            targetedCashier?.SetTargeted(false);
            targetedCashier = newTarget;
            targetedCashier?.SetTargeted(true);
        }

        WorkstationInteractable newWorkstation = FindWorkstationTarget();
        if (newWorkstation != targetedWorkstation)
        {
            targetedWorkstation?.SetTargeted(false);
            targetedWorkstation = newWorkstation;
            targetedWorkstation?.SetTargeted(true);
        }

        bool interactPressed = Keyboard.current != null
            && Keyboard.current.eKey.wasPressedThisFrame;
        bool dialogueContinuePressed = interactPressed;
        dialogueContinuePressed |= Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame;
        if (dialogueContinuePressed
            && conversingCashier != null && conversingCashier.DialogueOpen)
            conversingCashier.AdvanceText();
        else if (interactPressed)
        {
            if (targetedCashier != null)
            {
                conversingCashier = targetedCashier;
                conversingCashier.BeginConversation(transform);
                playerController?.SetInteractionLocked(true);
            }
            else if (targetedWorkstation != null && targetedWorkstation.CanUse)
                targetedWorkstation.Interact();
        }

        if (conversingCashier != null && !conversingCashier.DialogueOpen)
        {
            conversingCashier = null;
            playerController?.SetInteractionLocked(false);
        }

        if (conversingCashier != null && conversingCashier.DialogueOpen)
            playerController?.LookAtPoint(conversingCashier.transform.position
                + Vector3.up * 1.8f);
    }

    private CashierInteractable FindCashierTarget()
    {
        if (playerCamera == null)
            return null;
        Ray ray = new(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit[] hits = Physics.RaycastAll(ray, interactionDistance,
            interactionLayers, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit hit in hits)
        {
            CashierInteractable cashier =
                hit.collider.GetComponentInParent<CashierInteractable>();
            if (cashier != null)
                return cashier;
        }
        return null;
    }

    private WorkstationInteractable FindWorkstationTarget()
    {
        if (playerCamera == null) return null;
        Ray ray = new(playerCamera.transform.position, playerCamera.transform.forward);
        return Physics.Raycast(ray, out RaycastHit hit, interactionDistance,
                interactionLayers, QueryTriggerInteraction.Ignore)
            ? hit.collider.GetComponentInParent<WorkstationInteractable>()
            : null;
    }

    private void OnDisable()
    {
        ClearTargets();
        conversingCashier?.CloseDialogue();
        conversingCashier = null;
        playerController?.SetInteractionLocked(false);
    }

    private void ClearTargets()
    {
        targetedCashier?.SetTargeted(false);
        targetedCashier = null;
        targetedWorkstation?.SetTargeted(false);
        targetedWorkstation = null;
    }

    private void OnGUI()
    {
        if (CctvSystem.IsActive)
            return;
        EnsureStyles();
        float x = (Screen.width - crosshairSize) * 0.5f;
        float y = (Screen.height - crosshairSize) * 0.5f;
        GUI.color = crosshairColour;
        GUI.DrawTexture(new Rect(x, y, crosshairSize, crosshairSize), crosshairTexture);
        GUI.color = Color.white;

        if (targetedCashier != null
            && (conversingCashier == null || !conversingCashier.DialogueOpen))
            GUI.Label(new Rect(0f, y + 25f, Screen.width, 35f),
                targetedCashier.Prompt, promptStyle);
        else if (targetedWorkstation != null && targetedWorkstation.CanUse)
            GUI.Label(new Rect(0f, y + 25f, Screen.width, 35f),
                targetedWorkstation.Prompt, promptStyle);
    }

    private void EnsureStyles()
    {
        if (crosshairTexture == null)
        {
            crosshairTexture = new Texture2D(1, 1);
            crosshairTexture.SetPixel(0, 0, Color.white);
            crosshairTexture.Apply();
        }
        promptStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = promptFontSize,
            fontStyle = FontStyle.Bold
        };
        promptStyle.normal.textColor = Color.white;
    }

    private void OnDestroy()
    {
        if (crosshairTexture != null)
            Destroy(crosshairTexture);
    }
}
