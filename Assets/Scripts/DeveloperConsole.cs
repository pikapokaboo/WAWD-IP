// -----------------------------------------------------------------------------
// File: DeveloperConsole.cs
// Project: WAWD Integrated Studio Project
// Purpose: Provides a lightweight in-game command console toggled with F6.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class DeveloperConsole : MonoBehaviour
{
    public static bool ShowNpcDebug { get; private set; }
    public static bool ShowInteractionMarkers { get; private set; }
    public static bool AnyConsoleOpen { get; private set; }

    [Header("Controls")]
    [SerializeField] private Key toggleKey = Key.F6;
    [SerializeField] private PlayerController playerController;

    [Header("Appearance")]
    [SerializeField, Range(0.25f, 1f)] private float screenHeight = 0.55f;
    [SerializeField, Range(12, 28)] private int fontSize = 16;
    [SerializeField] private Color panelColour = new Color(0.035f, 0.045f, 0.06f, 0.72f);
    [SerializeField] private Color accentColour = new Color(0.22f, 0.72f, 0.95f, 1f);
    [SerializeField] private Color textColour = new Color(0.88f, 0.92f, 0.96f, 1f);

    private const string InputControlName = "DeveloperConsoleInput";
    private readonly List<string> output = new List<string>();
    private readonly List<string> commandHistory = new List<string>();
    private string input = string.Empty;
    private Vector2 scrollPosition;
    private int historyIndex;
    private bool isOpen;
    private bool refocusInput;
    private bool playerWasEnabled;
    private GUIStyle panelStyle;
    private GUIStyle headerStyle;
    private GUIStyle outputStyle;
    private GUIStyle inputStyle;
    private Texture2D panelTexture;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        output.Add("Developer console ready. Type 'help' for commands.");
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard[toggleKey].wasPressedThisFrame)
        {
            SetOpen(!isOpen);
            return;
        }

        if (!isOpen)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame)
            SetOpen(false);
        else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
            SubmitCommand();
        else if (keyboard.upArrowKey.wasPressedThisFrame)
            BrowseHistory(-1);
        else if (keyboard.downArrowKey.wasPressedThisFrame)
            BrowseHistory(1);
    }

    private void SetOpen(bool open)
    {
        isOpen = open;
        AnyConsoleOpen = open;
        if (open)
        {
            playerWasEnabled = playerController != null && playerController.enabled;
            if (playerController != null)
                playerController.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            refocusInput = true;
        }
        else
        {
            if (playerController != null && playerWasEnabled)
                playerController.enabled = true;
            Cursor.lockState = CctvSystem.IsActive
                ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = CctvSystem.IsActive;
        }
    }

    private void OnGUI()
    {
        if (!isOpen)
            return;

        EnsureStyles();
        float height = Mathf.Clamp(Screen.height * screenHeight, 260f, Screen.height);
        Rect panel = new Rect(0f, 0f, Screen.width, height);
        GUI.Box(panel, GUIContent.none, panelStyle);

        GUILayout.BeginArea(new Rect(24f, 16f, Screen.width - 48f, height - 28f));
        GUILayout.Label("DEVELOPER CONSOLE", headerStyle);
        GUILayout.Space(8f);
        scrollPosition = GUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandHeight(true));
        foreach (string line in output)
            GUILayout.Label(line, outputStyle);
        GUILayout.EndScrollView();
        GUILayout.Space(8f);

        GUI.SetNextControlName(InputControlName);
        input = GUILayout.TextField(input, inputStyle, GUILayout.Height(36f));
        GUILayout.Label("ENTER  run command     UP/DOWN  history     F6 or ESC  close", outputStyle);
        GUILayout.EndArea();

        if (refocusInput)
        {
            GUI.FocusControl(InputControlName);
            refocusInput = false;
        }
    }

    private void SubmitCommand()
    {
        string commandLine = input.Trim();
        input = string.Empty;
        refocusInput = true;
        if (commandLine.Length == 0)
            return;

        output.Add("> " + commandLine);
        commandHistory.Add(commandLine);
        historyIndex = commandHistory.Count;
        Execute(commandLine);
        scrollPosition.y = float.MaxValue;
    }

    private void Execute(string commandLine)
    {
        string[] parts = commandLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        string command = parts[0].ToLowerInvariant();
        switch (command)
        {
            case "help":
                output.Add("help | clear | fps | timescale <number> | playerpos | npcdebug | markers | debugstatus | skipday | quit");
                break;
            case "clear":
                output.Clear();
                break;
            case "fps":
                float fps = Time.unscaledDeltaTime > 0f ? 1f / Time.unscaledDeltaTime : 0f;
                output.Add($"Current FPS: {fps:0.0}");
                break;
            case "timescale":
                if (parts.Length > 1 && float.TryParse(parts[1], out float scale))
                {
                    Time.timeScale = Mathf.Clamp(scale, 0f, 10f);
                    output.Add($"Time scale set to {Time.timeScale:0.##}");
                }
                else output.Add("Usage: timescale <0-10>");
                break;
            case "playerpos":
                Vector3 position = transform.position;
                output.Add($"Player position: {position.x:0.00}, {position.y:0.00}, {position.z:0.00}");
                break;
            case "npcdebug":
                ShowNpcDebug = !ShowNpcDebug;
                output.Add($"NPC debug labels: {OnOff(ShowNpcDebug)}");
                break;
            case "markers":
                ShowInteractionMarkers = !ShowInteractionMarkers;
                output.Add($"Shelf and checkout markers: {OnOff(ShowInteractionMarkers)}");
                break;
            case "debugstatus":
                output.Add($"NPC labels: {OnOff(ShowNpcDebug)} | Interaction markers: {OnOff(ShowInteractionMarkers)}");
                break;
            case "skipday":
                DayNightCycle cycle = FindFirstObjectByType<DayNightCycle>();
                if (cycle == null) output.Add("No day cycle found.");
                else
                {
                    cycle.SkipToEndOfDay();
                    output.Add("Skipped to the end of the current day.");
                }
                break;
            case "quit":
                output.Add("Closing application...");
                Application.Quit();
                break;
            default:
                output.Add($"Unknown command '{parts[0]}'. Type 'help'.");
                break;
        }
    }

    private static string OnOff(bool value)
    {
        return value ? "ON" : "OFF";
    }

    private void BrowseHistory(int direction)
    {
        if (commandHistory.Count == 0)
            return;
        historyIndex = Mathf.Clamp(historyIndex + direction, 0, commandHistory.Count);
        input = historyIndex < commandHistory.Count ? commandHistory[historyIndex] : string.Empty;
        refocusInput = true;
    }

    private void EnsureStyles()
    {
        if (panelStyle != null)
            return;
        panelTexture = new Texture2D(1, 1);
        panelTexture.SetPixel(0, 0, panelColour);
        panelTexture.Apply();
        panelStyle = new GUIStyle(GUI.skin.box) { normal = { background = panelTexture } };
        headerStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize + 4, fontStyle = FontStyle.Bold };
        headerStyle.normal.textColor = accentColour;
        outputStyle = new GUIStyle(GUI.skin.label) { fontSize = fontSize, wordWrap = true };
        outputStyle.normal.textColor = textColour;
        inputStyle = new GUIStyle(GUI.skin.textField) { fontSize = fontSize, padding = new RectOffset(12, 12, 7, 7) };
    }

    private void OnDestroy()
    {
        if (isOpen) AnyConsoleOpen = false;
        if (panelTexture != null)
            Destroy(panelTexture);
    }
}
