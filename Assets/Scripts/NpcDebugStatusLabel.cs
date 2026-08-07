// -----------------------------------------------------------------------------
// File: NpcDebugStatusLabel.cs
// Project: WAWD Integrated Studio Project
// Purpose: Displays the NPC's current navigational objective above its head.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using System.Text;

/// <summary>
/// Creates a camera-facing world-space label showing what an NPC is trying to do.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NpcTraitProfile))]
public sealed class NpcDebugStatusLabel : MonoBehaviour
{
    [Header("Position")]
    [SerializeField, Min(0f)] private float heightAboveNpc = 2.6f;
    [SerializeField, Min(0.0001f)] private float worldScale = 0.005f;

    [Header("Appearance")]
    [SerializeField] private string idleMessage = "Idle";
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color outlineColor = new(0f, 0f, 0f, 0.9f);
    [SerializeField, Min(1)] private int fontSize = 32;

    private NpcTraitProfile traitProfile;
    private Transform labelTransform;
    private Text statusText;
    private Camera viewingCamera;
    private NpcPathToShelf shelfAction;
    private string lastDisplayedText;

    private void Awake()
    {
        traitProfile = GetComponent<NpcTraitProfile>();
        shelfAction = GetComponent<NpcPathToShelf>();
        CreateLabel();
        RefreshText();
    }

    private void LateUpdate()
    {
        string displayText = BuildDisplayText();
        if (displayText != lastDisplayedText)
        {
            lastDisplayedText = displayText;
            statusText.text = displayText;
        }

        if (viewingCamera == null || !viewingCamera.isActiveAndEnabled)
            viewingCamera = Camera.main;

        if (viewingCamera != null)
            labelTransform.rotation = viewingCamera.transform.rotation;
    }

    private void CreateLabel()
    {
        GameObject canvasObject = new("NPC Debug Status", typeof(Canvas));
        labelTransform = canvasObject.transform;
        labelTransform.SetParent(transform, false);
        labelTransform.localPosition = Vector3.up * heightAboveNpc;
        labelTransform.localScale = Vector3.one * worldScale;

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(700f, 190f);

        GameObject textObject = new("Text", typeof(RectTransform), typeof(Text),
            typeof(Outline));
        textObject.transform.SetParent(labelTransform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        statusText = textObject.GetComponent<Text>();
        statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        statusText.fontSize = fontSize;
        statusText.color = textColor;
        statusText.alignment = TextAnchor.MiddleCenter;
        statusText.resizeTextForBestFit = true;
        statusText.resizeTextMinSize = 14;
        statusText.resizeTextMaxSize = fontSize;

        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = outlineColor;
        outline.effectDistance = new Vector2(2f, -2f);
    }

    private void RefreshText()
    {
        lastDisplayedText = BuildDisplayText();
        statusText.text = lastDisplayedText;
    }

    private string BuildDisplayText()
    {
        NpcTraitOption current = traitProfile.CurrentNavigationTrait;
        string goal = current != null ? current.TraitName : idleMessage;
        string activity = GetCurrentActivity(current);
        StringBuilder traits = new();

        foreach (NpcTraitOption trait in traitProfile.ActiveTraits)
        {
            if (traits.Length > 0)
                traits.Append(" | ");
            traits.Append(trait.TraitName);
        }

        return $"Goal: {goal}\nStatus: {activity}\nTraits: "
            + (traits.Length > 0 ? traits.ToString() : "None");
    }

    private string GetCurrentActivity(NpcTraitOption current)
    {
        if (current == null)
            return idleMessage;

        if (current.Action == shelfAction && shelfAction != null)
            return shelfAction.CurrentStatus;

        if (current.Action is NpcBrowseStore)
            return "Browsing around the store";

        if (current.Action is NpcPathToHome)
            return "Leaving the store";

        return current.TraitName;
    }

    private void OnValidate()
    {
        heightAboveNpc = Mathf.Max(0f, heightAboveNpc);
        worldScale = Mathf.Max(0.0001f, worldScale);
        fontSize = Mathf.Max(1, fontSize);
    }
}
