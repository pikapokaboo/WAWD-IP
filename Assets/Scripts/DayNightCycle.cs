// -----------------------------------------------------------------------------
// File: DayNightCycle.cs
// Project: WAWD Integrated Studio Project
// Purpose: Drives the game-day clock, sunlight, and end-of-day event.
// -----------------------------------------------------------------------------

using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class DayNightCycle : MonoBehaviour
{
    public static DayNightCycle Instance { get; private set; }
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
    [SerializeField] private bool prepareBeforeDayOne = true;
    [SerializeField, Range(0f, 24f)] private float preparationHour = 5.5f;

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
    [Tooltip("Legacy option. Leave disabled to keep the scene lighting fixed.")]
    [SerializeField] private bool animateSunAndLighting;
    [SerializeField] private Light sun;
    [SerializeField] private float sunYaw = -30f;
    [SerializeField, Range(0f, 12f)] private float sunriseHour = 6f;
    [SerializeField, Range(10f, 15f)] private float solarNoonHour = 12f;
    [SerializeField, Range(15f, 24f)] private float sunsetHour = 19f;
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

    [Header("Shoplifter Failure")]
    [SerializeField, Min(0)] private int dayOneShoplifters = 3;
    [SerializeField, Min(0)] private int shoplifterIncreasePerDay = 1;
    [SerializeField, Range(0f, 1f)] private float allowedEscapeFraction = 0.4f;
    [SerializeField, Min(0)] private int minimumAllowedEscapes = 1;

    [Header("Sound Effects")]
    [SerializeField] private AudioClip cashRegisterSound;
    [SerializeField] private AudioClip dayCompleteSound;
    [SerializeField] private AudioClip shoplifterRemovalSound;
    [SerializeField, Range(0f, 1f)] private float soundEffectVolume = 0.75f;

    private float elapsedGameHours;
    private bool dayEnded;
    private GUIStyle clockStyle;
    private Texture2D panelTexture;
    private GUIStyle dayStyle;
    private GUIStyle dayTitleStyle;
    private GUIStyle dayCompleteTitleStyle;
    private GUIStyle dayCompleteSubtitleStyle;
    private float dayStartOverlayAlpha;
    private Coroutine dayStartSequence;
    private AudioSource soundEffectSource;
    private bool dayFailed;
    private int escapedShoplifters;
    private int totalEscapedShoplifters;
    private int caughtShoplifters;
    private GUIStyle failureTitleStyle;
    private GUIStyle failureBodyStyle;
    private GUIStyle failureButtonStyle;

    public float CurrentHour => PreparingToOpen ? preparationHour
        : Mathf.Clamp(startHour + elapsedGameHours,
            startHour, Mathf.Max(startHour, endHour));
    public bool DayEnded => dayEnded;
    public float DayProgress => Mathf.InverseLerp(startHour, endHour, CurrentHour);
    public int CurrentDay { get; private set; }
    public bool PreparingToOpen { get; private set; }
    public bool DayActive => !PreparingToOpen && !dayEnded && !dayFailed;
    public int EscapedShoplifters => escapedShoplifters;
    public int AllowedShoplifterEscapes => Mathf.Max(minimumAllowedEscapes,
        Mathf.FloorToInt(ExpectedShopliftersForDay * allowedEscapeFraction));
    public int ExpectedShopliftersForDay => dayOneShoplifters
        + Mathf.Max(0, CurrentDay - 1) * shoplifterIncreasePerDay;
    public int DaysSurvived => Mathf.Max(0, CurrentDay - 1);
    public event Action DayEndedEvent;

    private void Awake()
    {
        Instance = this;
        CurrentDay = Mathf.Max(1, startingDay);
        PreparingToOpen = prepareBeforeDayOne && CurrentDay == 1;
        if (sun == null)
            sun = GetComponent<Light>();
        if (sun != null)
            RenderSettings.sun = sun;
        ApplyLighting();
        EnsureSoundEffectSource();
    }

    private void Start()
    {
        TeleportPlayerToDayStart();
        BeginDayStartSequence();
    }

    private void Update()
    {
        if (dayFailed)
        {
            HandleFailureInput();
            return;
        }
        if (PreparingToOpen || dayEnded || endHour <= startHour)
            return;

        float realSeconds = Mathf.Max(0.1f, dayLengthMinutes * 60f);
        elapsedGameHours += (endHour - startHour) * Time.deltaTime
            / realSeconds;
        if (CurrentHour >= endHour)
            EndDay();
        ApplyLighting();
    }

    private void ApplyLighting()
    {
        if (!animateSunAndLighting)
            return;

        float hour = CurrentHour;
        float daylight;
        float sunPitch;
        if (hour <= sunriseHour)
        {
            daylight = 0f;
            sunPitch = Mathf.Lerp(-18f, 0f,
                Mathf.InverseLerp(0f, sunriseHour, hour));
        }
        else if (hour <= solarNoonHour)
        {
            float morning = Mathf.InverseLerp(sunriseHour, solarNoonHour, hour);
            daylight = Mathf.Sin(morning * Mathf.PI * 0.5f);
            sunPitch = Mathf.Lerp(0f, 90f, morning);
        }
        else if (hour < sunsetHour)
        {
            float afternoon = Mathf.InverseLerp(solarNoonHour, sunsetHour, hour);
            daylight = Mathf.Cos(afternoon * Mathf.PI * 0.5f);
            sunPitch = Mathf.Lerp(90f, 180f, afternoon);
        }
        else
        {
            daylight = 0f;
            sunPitch = Mathf.Lerp(180f, 198f,
                Mathf.InverseLerp(sunsetHour, 24f, hour));
        }
        if (sun != null)
        {
            // In Unity, an X rotation of 90 degrees points a directional
            // light straight down: sunrise=0, noon=90, sunset=180.
            sun.transform.rotation = Quaternion.Euler(sunPitch, sunYaw, 0f);
            sun.intensity = daylight * maximumSunIntensity;
            float colourPhase = Mathf.InverseLerp(sunriseHour, sunsetHour, hour);
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
        CctvSystem.ExitForDayEnd();
        RemoveAllNpcs();
        if (dayCompleteSound != null)
            soundEffectSource.PlayOneShot(dayCompleteSound, soundEffectVolume);
        DayEndedEvent?.Invoke();
        onDayEnded.Invoke();
        if (pauseAtEndOfDay)
            Time.timeScale = 0f;
        if (automaticallyStartNextDay)
            StartCoroutine(StartNextDayAfterPause());
    }

    private System.Collections.IEnumerator StartNextDayAfterPause()
    {
        float soundDuration = dayCompleteSound != null ? dayCompleteSound.length : 0f;
        yield return new WaitForSecondsRealtime(Mathf.Max(
            endOfDayPauseSeconds, soundDuration));
        CurrentDay++;
        RestartDay();
    }

    [ContextMenu("Restart Day")]
    public void RestartDay()
    {
        elapsedGameHours = 0f;
        dayEnded = false;
        dayFailed = false;
        escapedShoplifters = 0;
        PreparingToOpen = true;
        Time.timeScale = 1f;
        TeleportPlayerToDayStart();
        OpeningSequence.Instance?.PrepareForNextDay();
        BeginDayStartSequence();
        ApplyLighting();
    }

    public void SkipToEndOfDay()
    {
        if (dayEnded) return;
        EndDay();
    }

    private static void RemoveAllNpcs()
    {
        NpcNavigation[] npcs = FindObjectsByType<NpcNavigation>(
            FindObjectsSortMode.None);
        foreach (NpcNavigation npc in npcs)
        {
            if (npc == null) continue;
            npc.ReleaseAllOccupancy();
            Destroy(npc.gameObject);
        }
        NpcAutomaticDoor.RefreshAllAfterNpcRemoval();
    }

    public void BeginOpeningDay()
    {
        if (!PreparingToOpen)
            return;
        PreparingToOpen = false;
        elapsedGameHours = 0f;
        escapedShoplifters = 0;
        ApplyLighting();
    }

    public void ReportEscapedShoplifter()
    {
        if (!DayActive) return;
        escapedShoplifters++;
        totalEscapedShoplifters++;
        if (escapedShoplifters > AllowedShoplifterEscapes)
            FailDay();
    }

    public void ReportCaughtShoplifter()
    {
        if (DayActive) caughtShoplifters++;
    }

    private void FailDay()
    {
        dayFailed = true;
        CctvSystem.ExitForDayEnd();
        RemoveAllNpcs();
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void HandleFailureInput()
    {
        if (Keyboard.current == null) return;
        if (Keyboard.current.rKey.wasPressedThisFrame)
            RetryFromDayOne();
        else if (Keyboard.current.escapeKey.wasPressedThisFrame)
            SceneManager.LoadScene("Home_Screen");
    }

    private static void RetryFromDayOne()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public string CurrentTimeText => FormatTime(CurrentHour);

    public void PlayRegisterSound()
    {
        EnsureSoundEffectSource();
        if (cashRegisterSound != null)
            soundEffectSource.PlayOneShot(cashRegisterSound, soundEffectVolume);
    }

    public float PlayShoplifterRemovalSound()
    {
        if (shoplifterRemovalSound == null) return 0f;
        EnsureSoundEffectSource();
        soundEffectSource.PlayOneShot(shoplifterRemovalSound, soundEffectVolume);
        return shoplifterRemovalSound.length;
    }

    private void EnsureSoundEffectSource()
    {
        if (soundEffectSource != null) return;
        soundEffectSource = gameObject.AddComponent<AudioSource>();
        ConfigureEffectSource(soundEffectSource, false);
    }

    private void ConfigureEffectSource(AudioSource audioSource, bool loop)
    {
        audioSource.playOnAwake = false;
        audioSource.loop = loop;
        audioSource.spatialBlend = 0f;
        audioSource.volume = AudioVolumeSettings.SoundEffects;
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
        HideCursorForBlackScreen();
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
        RestoreCursorAfterBlackScreen();
    }

    private static void HideCursorForBlackScreen()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
    }

    private static void RestoreCursorAfterBlackScreen()
    {
        Cursor.lockState = CctvSystem.IsActive
            ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = CctvSystem.IsActive;
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
        if (dayFailed)
        {
            DrawFailureScreen();
            return;
        }
        if (dayEnded)
        {
            DrawDayCompleteScreen();
            return;
        }

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

    private void DrawFailureScreen()
    {
        HideCursorForBlackScreen();
        GUI.color = new Color(0.025f, 0.01f, 0.015f, 1f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height),
            Texture2D.whiteTexture);
        GUI.color = Color.white;
        EnsureFailureStyles();

        GUI.Label(new Rect(0f, Screen.height * 0.5f - 165f,
            Screen.width, 70f), "SHIFT FAILED", failureTitleStyle);
        GUI.Label(new Rect(Screen.width * 0.15f, Screen.height * 0.5f - 80f,
            Screen.width * 0.7f, 140f),
            $"DAYS SURVIVED: {DaysSurvived}\n"
            + $"DAY REACHED: {CurrentDay}\n"
            + $"SHOPLIFTERS CAUGHT: {caughtShoplifters}\n"
            + $"TOTAL SHOPLIFTERS MISSED: {totalEscapedShoplifters}\n"
            + $"FINAL SHIFT: {escapedShoplifters} escaped / "
            + $"{AllowedShoplifterEscapes} allowed", failureBodyStyle);

        float buttonWidth = Mathf.Min(310f, Screen.width * 0.36f);
        if (GUI.Button(new Rect(Screen.width * 0.5f - buttonWidth - 10f,
                Screen.height * 0.5f + 90f, buttonWidth, 58f),
                "RETRY FROM DAY 1  [R]", failureButtonStyle))
            RetryFromDayOne();
        if (GUI.Button(new Rect(Screen.width * 0.5f + 10f,
                Screen.height * 0.5f + 90f, buttonWidth, 58f),
                "RETURN TO TITLE  [ESC]", failureButtonStyle))
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Home_Screen");
        }
    }

    private void EnsureFailureStyles()
    {
        failureTitleStyle ??= new GUIStyle(GUI.skin.label)
        { alignment = TextAnchor.MiddleCenter, fontSize = 54, fontStyle = FontStyle.Bold };
        failureTitleStyle.normal.textColor = new Color(1f, 0.22f, 0.2f);
        failureBodyStyle ??= new GUIStyle(GUI.skin.label)
        { alignment = TextAnchor.MiddleCenter, fontSize = 23, wordWrap = true };
        failureBodyStyle.normal.textColor = Color.white;
        failureButtonStyle ??= new GUIStyle(GUI.skin.button)
        { alignment = TextAnchor.MiddleCenter, fontSize = 19, fontStyle = FontStyle.Bold };
    }

    private void DrawDayCompleteScreen()
    {
        HideCursorForBlackScreen();
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height),
            Texture2D.whiteTexture);
        GUI.color = Color.white;

        dayCompleteTitleStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 52,
            fontStyle = FontStyle.Bold
        };
        dayCompleteTitleStyle.normal.textColor = Color.white;
        dayCompleteSubtitleStyle ??= new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 24
        };
        dayCompleteSubtitleStyle.normal.textColor = new Color(0.72f, 0.86f, 1f);

        GUI.Label(new Rect(0f, Screen.height * 0.5f - 85f,
            Screen.width, 70f), $"DAY {CurrentDay} COMPLETE!",
            dayCompleteTitleStyle);
        GUI.Label(new Rect(0f, Screen.height * 0.5f - 10f,
            Screen.width, 45f), "Great work. Preparing the next shift...",
            dayCompleteSubtitleStyle);
        GUI.Label(new Rect(0f, Screen.height * 0.5f + 35f,
            Screen.width, 40f), $"DAY {CurrentDay + 1} STARTING SOON",
            dayCompleteSubtitleStyle);
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
        if (Instance == this) Instance = null;
        if (panelTexture != null)
            Destroy(panelTexture);
    }
}
