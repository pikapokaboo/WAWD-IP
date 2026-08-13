// -----------------------------------------------------------------------------
// File: NpcAppearance.cs
// Project: WAWD Integrated Studio Project
// Purpose: Gives each spawned NPC a unique colour without recolouring its eyes.
// -----------------------------------------------------------------------------

using UnityEngine;

[DisallowMultipleComponent]
public sealed class NpcAppearance : MonoBehaviour
{
    [Header("Random NPC Colour")]
    [SerializeField] private bool randomiseOnAwake = true;
    [SerializeField, Range(0f, 1f)] private float minimumSaturation = 0.35f;
    [SerializeField, Range(0f, 1f)] private float maximumSaturation = 0.8f;
    [SerializeField, Range(0f, 1f)] private float minimumBrightness = 0.65f;
    [SerializeField, Range(0f, 1f)] private float maximumBrightness = 1f;
    [Tooltip("Renderer or material names containing this text keep their original colour.")]
    [SerializeField] private string excludedName = "eye";

    private static readonly int BaseColourId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColourId = Shader.PropertyToID("_Color");
    private MaterialPropertyBlock propertyBlock;

    public Color CurrentColour { get; private set; } = Color.white;

    private void Awake()
    {
        if (randomiseOnAwake)
            ApplyRandomColour();
    }

    [ContextMenu("Apply Random Colour")]
    public void ApplyRandomColour()
    {
        CurrentColour = Random.ColorHSV(0f, 1f,
            minimumSaturation, maximumSaturation,
            minimumBrightness, maximumBrightness);
        ApplyColour(CurrentColour);
    }

    public void ApplyColour(Color colour)
    {
        CurrentColour = colour;
        propertyBlock ??= new MaterialPropertyBlock();

        foreach (Renderer npcRenderer in GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = npcRenderer.sharedMaterials;
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                Material material = materials[materialIndex];
                if (ShouldKeepOriginalColour(npcRenderer, material))
                    continue;

                npcRenderer.GetPropertyBlock(propertyBlock, materialIndex);
                if (material != null && material.HasProperty(BaseColourId))
                    propertyBlock.SetColor(BaseColourId, colour);
                if (material != null && material.HasProperty(ColourId))
                    propertyBlock.SetColor(ColourId, colour);
                npcRenderer.SetPropertyBlock(propertyBlock, materialIndex);
                propertyBlock.Clear();
            }
        }
    }

    private bool ShouldKeepOriginalColour(Renderer npcRenderer, Material material)
    {
        if (string.IsNullOrWhiteSpace(excludedName))
            return false;

        return npcRenderer.name.IndexOf(excludedName,
                   System.StringComparison.OrdinalIgnoreCase) >= 0
               || (material != null && material.name.IndexOf(excludedName,
                   System.StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private void OnValidate()
    {
        maximumSaturation = Mathf.Max(minimumSaturation, maximumSaturation);
        maximumBrightness = Mathf.Max(minimumBrightness, maximumBrightness);
    }
}
