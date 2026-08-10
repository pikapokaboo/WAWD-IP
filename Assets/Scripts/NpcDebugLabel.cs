using System.Text;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Draws an editor-style status note above an NPC during play.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NpcTraits), typeof(NpcNavigation))]
public sealed class NpcDebugLabel : MonoBehaviour
{
    [SerializeField] private bool showDebugNote = true;
    [SerializeField, Min(0f)] private float height = 3.2f;
    [SerializeField, Min(8)] private int fontSize = 15;
    [SerializeField] private Color actionColour = new(0.25f, 0.9f, 1f);
    [SerializeField] private Color traitColour = new(1f, 0.8f, 0.2f);
    [SerializeField] private Color productColour = new(0.35f, 1f, 0.45f);

    private NpcTraits traits;
    private NpcNavigation navigation;
    private GUIStyle style;
    private GUIStyle shadowStyle;

    private void Awake()
    {
        traits = GetComponent<NpcTraits>();
        navigation = GetComponent<NpcNavigation>();
    }

    private void OnGUI()
    {
        if (!showDebugNote || Camera.main == null || traits == null || navigation == null)
            return;

        Vector3 screen = Camera.main.WorldToScreenPoint(
            transform.position + Vector3.up * height);
        if (screen.z <= 0f)
            return;

        EnsureStyles();
        string message = BuildMessage();
        Vector2 size = style.CalcSize(new GUIContent(message));
        Rect area = new(
            screen.x - size.x * 0.5f,
            Screen.height - screen.y - size.y,
            size.x,
            size.y);

        Rect shadow = area;
        shadow.position += new Vector2(2f, 2f);
        GUI.Label(shadow, message, shadowStyle);
        GUI.Label(area, message, style);
    }

    private string BuildMessage()
    {
        StringBuilder message = new();
        AppendColoured(message, navigation.CurrentAction, actionColour);
        message.Append("\n");
        AppendColoured(message, "Traits: ", traitColour);
        IReadOnlyList<NpcTrait> active = traits.ActiveTraits;
        for (int i = 0; i < active.Count; i++)
        {
            if (i > 0)
                AppendColoured(message, ", ", traitColour);
            AppendColoured(message, active[i].Name, GetTraitColour(active[i].Name));
        }

        if (navigation.WantedProducts.Count > 0)
        {
            message.Append("\n");
            AppendColoured(message, "Wants: ", productColour);
            for (int i = 0; i < navigation.WantedProducts.Count; i++)
            {
                if (i > 0)
                    AppendColoured(message, ", ", productColour);
                AppendColoured(message, navigation.WantedProducts[i], productColour);
            }
        }
        return message.ToString();
    }

    private Color GetTraitColour(string traitName)
    {
        if (traitName == "No Money" || traitName == "Slow Walker")
            return new Color(1f, 0.4f, 0.35f);
        if (traitName == "Heavy Spender" || traitName == "Fast Walker")
            return new Color(0.45f, 1f, 0.45f);
        return traitColour;
    }

    private static void AppendColoured(StringBuilder builder, string value, Color colour)
    {
        builder.Append("<color=#");
        builder.Append(ColorUtility.ToHtmlStringRGB(colour));
        builder.Append('>');
        builder.Append(value);
        builder.Append("</color>");
    }

    private void EnsureStyles()
    {
        if (style != null && style.fontSize == fontSize)
            return;

        style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.LowerCenter,
            fontSize = fontSize,
            fontStyle = FontStyle.Bold
        };
        style.richText = true;
        style.normal.textColor = Color.white;

        shadowStyle = new GUIStyle(style);
        shadowStyle.normal.textColor = Color.black;
    }
}
