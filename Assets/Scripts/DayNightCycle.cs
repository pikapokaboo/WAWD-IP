// -----------------------------------------------------------------------------
// File: DayNightCycle.cs
// Project: WAWD Integrated Studio Project
// Purpose: Drives the game-day clock, sunlight, and end-of-day event.
// -----------------------------------------------------------------------------

using System;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class DayNightCycle : MonoBehaviour
{
    [Header("Day Timing")]
    [Tooltip("Real-world minutes from the start time until the end time.")]
    [SerializeField, Min(0.1f)] private float dayLengthMinutes = 15f;
    [SerializeField, Range(0f, 24f)] private float startHour = 8f;
    [SerializeField, Range(0f, 24f)] private float endHour = 22f;
    [SerializeField] private bool pauseAtEndOfDay = true;

    [Header("Day Progression")]
    [SerializeField, Min(1)] private int startingDay = 1;
    [SerializeField, Min(0f)] private float endOfDayPauseSeconds = 3f;
    [SerializeField] private bool automaticallyStartNextDay = true;

    [Header("Player Day Start")]
    [Tooltip("The position and rotation used for the player at the start of every day.")]
    [SerializeField] private Transform playerSpawnPosition;
    [SerializeField] private PlayerController player;

    [Header("Day Start Sequence")]
    [SerializeField] private bool showDayStartSequence = true;
    [SerializeField, Min(0f)] private float dayTitleHoldSeconds = 1.25f;
    [SerializeField, Min(0.01f)] private float dayTitleFadeSeconds = 0.75f;
    [SerializeField, Min(12)] private int dayTitleFontSize = 52;

    [Header("Sun")]
    [SerializeField] private Light sun;
    [SerializeField] private float sunYaw = -30f;
    [SerializeField, Min(0f)] private float maximumSunIntensity = 1.15f;
    [SerializeField] private Color sunriseColour = new(1f, 0.55f, 0.3f);
    [SerializeField] private Color middayColour = new(1f, 0.96f, 0.84f);
    [SerializeField] private Color sunsetColour = new(1f, 0.38f, 0.22f);
    [SerializeField] private Color nightAmbientColour = new(0.035f, 0.045f, 0.09f);
    [SerializeField] private Color dayAmbientColour = new(0.65f, 0.68f, 0.72f);

    [Header("Clock Display")]
    [SerializeField] private bool showClock = true;
    [SerializeField] private bool useTwelveHourClock = true;
    [SerializeField, Min(12)] private int clockFontSize = 24;
    [SerializeField] private Vector2 clockSize = new(190f, 58f);
    [SerializeField] private Vector2 screenMargin = new(22f, 18f);
    [SerializeField] private Color panelColour = new(0.035f, 0.045f, 0.06f, 0.72f);
    [SerializeField] private Color textColour = Color.white;

    [Header("End Of Day")]
    [SerializeField] private UnityEvent onDayEnded = new();

    private float elapsedGameHours;
    private bool dayEnded;
    private GUIStyle clockStyle;
    private Texture2D panelTexture;
    private GUIStyle dayStyle;
    private GUIStyle dayTitleStyle;
    private float dayStartOverlayAlpha;
    private Coroutine dayStartSequence;

    public float CurrentHour => Mathf.Clamp(startHour + elapsedGameHours,
        startHour, Mathf.Max(startHour, endHour));
    public bool DayEnded => dayEnded;
    public float DayProgress => Mathf.InverseLerp(startHour, endHour, CurrentHour);
    public int CurrentDay { get; private set; }
    public event Action DayEndedEvent;

    private void Awake()
    {
        CurrentDay = Mathf.Max(1, startingDay);
        if (sun == null)
            sun = GetComponent<Light>();
        if (sun != null)
            RenderSettings.sun = sun;
        ApplyLighting();
    }

    private void Start()
    {
        TeleportPlayerToDayStart();
        BeginDayStartSequence();
    }

    private void Update()
    {
        if (dayEnded || endHour <= startHour)
            return;

        float realSeconds = Mathf.Max(0.1f, dayLengthMinutes * 60f);
        elapsedGameHours += (endHour - startHour) * Time.deltaTime / realSeconds;
        if (CurrentHour >= endHour)
            EndDay();
        ApplyLighting();
    }

    private void ApplyLighting()
    {
        float hour = CurrentHour;
        float daylight = Mathf.Clamp01(Mathf.Sin((hour - 6f) / 12f * Mathf.PI));
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler((hour / 24f) * 360f - 90f,
                sunYaw, 0f);
            sun.intensity = daylight * maximumSunIntensity;
            float colourPhase = Mathf.InverseLerp(6f, 18f, hour);
            sun.color = colourPhase < 0.5f
                ? Color.Lerp(sunriseColour, middayColour, colourPhase * 2f)
                : Color.Lerp(middayColour, sunsetColour, (colourPhase - 0.5f) * 2f);
        }
        RenderSettings.ambientLight = Color.Lerp(nightAmbientColour,
            dayAmbientColour, daylight);
    }

    private void EndDay()
    {
        elapsedGameHours = endHour - startHour;
        dayEnded = true;
        DayEndedEvent?.Invoke();
        onDayEnded.Invoke();
        if (pauseAtEndOfDay)
            Time.timeScale = 0f;
        if (automaticallyStartNextDay)
            StartCoroutine(StartNextDayAfterPause());
    }

    private System.Collections.IEnumerator StartNextDayAfterPause()
    {
        yield return new WaitForSecondsRealtime(endOfDayPauseSeconds);
        CurrentDay++;
        RestartDay();
    }

    [ContextMenu("Restart Day")]
    public void RestartDay()
    {
        elapsedGameHours = 0f;
        dayEnded = false;
        Time.timeScale = 1f;
        TeleportPlayerToDayStart();
        BeginDayStartSequence();
        ApplyLighting();
    }

    private void BeginDayStartSequence()
    {
        if (dayStartSequence != null)
            StopCoroutine(dayStartSequence);

        if (!showDayStartSequence)
        {
            dayStartOverlayAlpha = 0f;
            dayStartSequence = null;
            return;
        }

        dayStartSequence = StartCoroutine(PlayDayStartSequence());
    }

    private System.Collections.IEnumerator PlayDayStartSequence()
    {
        dayStartOverlayAlpha = 1f;
        float holdUntil = Time.realtimeSinceStartup + dayTitleHoldSeconds;
        while (Time.realtimeSinceStartup < holdUntil)
            yield return null;

        float fadeStart = Time.realtimeSinceStartup;
        while (dayStartOverlayAlpha > 0f)
        {
            dayStartOverlayAlpha = 1f - Mathf.Clamp01(
                (Time.realtimeSinceStartup - fadeStart) / dayTitleFadeSeconds);
            yield return null;
        }

        dayStartOverlayAlpha = 0f;
        dayStartSequence = null;
    }

    private void TeleportPlayerToDayStart()
    {
        if (playerSpawnPosition == null)
        {
            GameObject spawnObject = GameObject.Find("Player_Spawn_pos");
            if (spawnObject != null)
                playerSpawnPosition = spawnObject.transform;
        }

        if (player == null)
            player = FindFirstObjectByType<PlayerController>();
        if (player == null || playerSpawnPosition == null)
            return;

        CharacterController controller = player.GetComponent<CharacterController>();
        bool controllerWasEnabled = controller != null && controller.enabled;
        if (controllerWasEnabled)
            controller.enabled = false;

        player.transform.SetPositionAndRotation(
            playerSpawnPosition.position, playerSpawnPosition.rotation);

        if (controllerWasEnabled)
            controller.enabled = true;
    }

    private void OnGUI()
    {
        if (dayStartOverlayAlpha > 0f)
        {
            DrawDayStartOverlay();
            return;
        }

        if (!showClock)
            return;
        EnsureStyle();
        Rect clockRect = new(Screen.width - clockSize.x - screenMargin.x,
            screenMargin.y, clockSize.x, clockSize.y);
        GUI.Label(clockRect, FormatTime(CurrentHour), clockStyle);
        EnsureDayStyle();
        GUI.Label(new Rect(Screen.width - 220f, screenMargin.y + clockSize.y + 2f,
            198f, 32f), dayEnded ? $"DAY {CurrentDay} COMPLETE" : $"DAY {CurrentDay}",
            dayStyle);
    }

    private void DrawDayStartOverlay()
    {
        if (dayStartOverlayAlpha <= 0f)
            return;

        Color previousColour = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, dayStartOverlayAlpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height),
            Texture2D.whiteTexture);

        if (dayTitleStyle == null)
        {
            dayTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = dayTitleFontSize,
                fontStyle = FontStyle.Bold
            };
            dayTitleStyle.normal.textColor = Color.white;
        }

        GUI.color = new Color(1f, 1f, 1f, dayStartOverlayAlpha);
        GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height),
            $"DAY {CurrentDay}", dayTitleStyle);
        GUI.color = previousColour;
    }

    private void EnsureDayStyle()
    {
        if (dayStyle != null) return;
        dayStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.UpperRight,
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        dayStyle.normal.textColor = textColour;
    }

    private string FormatTime(float hour)
    {
        int totalMinutes = Mathf.RoundToInt(hour * 60f);
        int hours = (totalMinutes / 60) % 24;
        int minutes = totalMinutes % 60;
        if (!useTwelveHourClock)
            return $"{hours:00}:{minutes:00}";
        string period = hours >= 12 ? "PM" : "AM";
        int displayHour = hours % 12;
        if (displayHour == 0) displayHour = 12;
        return $"{displayHour}:{minutes:00} {period}";
    }

    private void EnsureStyle()
    {
        if (clockStyle != null)
            return;
        panelTexture = new Texture2D(1, 1);
        panelTexture.SetPixel(0, 0, panelColour);
        panelTexture.Apply();
        clockStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = clockFontSize,
            fontStyle = FontStyle.Bold
        };
        clockStyle.normal.background = panelTexture;
        clockStyle.normal.textColor = textColour;
    }

    private void OnDestroy()
    {
        if (panelTexture != null)
            Destroy(panelTexture);
    }
}
