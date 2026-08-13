using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorkstationInteractable : MonoBehaviour
{
    [SerializeField] private string prompt = "[E] Prepare for the day";
    [SerializeField] private Color outlineColour = new(0.35f, 0.8f, 1f, 1f);
    [SerializeField, Range(0f, 10f)] private float outlineWidth = 4f;

    private Outline outline;
    public string Prompt => prompt;
    public bool CanUse => OpeningSequence.Instance != null
        && OpeningSequence.Instance.Stage == OpeningSequence.OpeningStage.GoToWorkstation;

    private void Awake()
    {
        outline = GetComponent<Outline>();
        if (outline == null) outline = gameObject.AddComponent<Outline>();
        outline.OutlineMode = Outline.Mode.OutlineVisible;
        outline.OutlineColor = outlineColour;
        outline.OutlineWidth = outlineWidth;
        outline.enabled = false;
    }

    public void SetTargeted(bool targeted)
    {
        if (outline != null) outline.enabled = targeted && CanUse;
    }

    public void Interact()
    {
        if (CanUse) OpeningSequence.Instance.StartDayFromWorkstation();
        SetTargeted(false);
    }

    private void OnDisable()
    {
        if (outline != null) outline.enabled = false;
    }
}
