// -----------------------------------------------------------------------------
// File: StartMenuController.cs
// Project: WAWD Integrated Studio Project
// Purpose: Builds and controls the start menu, settings placeholder, scene
//          navigation, desktop exit, and optional menu background music.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Builds and controls the start menu. The UI is intentionally generated from a
/// few inspector settings so it is easy to restyle or replace without touching
/// the menu behaviour.
/// </summary>
[DisallowMultipleComponent]
public sealed class StartMenuController : MonoBehaviour
{
    private const string GameplaySceneName = "Main_Scene";

    [Header("Text")]
    [SerializeField] private string gameTitle = "CheckOut LookOut";

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new(560f, 640f);
    [SerializeField] private Vector2 buttonSize = new(420f, 64f);
    [SerializeField] private Vector2 panelOffset = new(70f, 0f);
    [SerializeField, Min(0f)] private float spacing = 18f;

    [Header("Style")]
    [SerializeField] private Color panelColor = new(0.055f, 0.065f, 0.09f, 0.88f);
    [SerializeField] private Color buttonColor = new(0.11f, 0.15f, 0.29f, 0.98f);
    [SerializeField] private Color buttonHighlightColor = new(0.17f, 0.22f, 0.40f, 1f);
    [SerializeField] private Color accentColor = new(0.68f, 0.18f, 0.07f, 1f);
    [SerializeField] private Color textColor = new(1f, 0.98f, 0.86f, 1f);
    [SerializeField] private Font font;

    [Header("Button Artwork")]
    [SerializeField] private Texture2D logoTexture;
    [Tooltip("Optional artwork for each menu action. Clear a field to use the plain text fallback button.")]
    [SerializeField] private Texture2D playButtonTexture;
    [SerializeField] private Texture2D settingsButtonTexture;
    [SerializeField] private Texture2D exitButtonTexture;
    [Tooltip("Used as the Back button on the temporary settings screen.")]
    [SerializeField] private Texture2D backButtonTexture;
    [Tooltip("Crops the transparent square padding around the supplied button artwork.")]
    [SerializeField] private Rect buttonArtworkUv = new(0.123f, 0.404f, 0.754f, 0.192f);
    [SerializeField] private Rect logoArtworkUv = new(0.071f, 0.394f, 0.858f, 0.242f);

    [Header("Music")]
    [Tooltip("Drop a music AudioClip here. Leaving this empty keeps the menu silent.")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;

    private GameObject mainPanel;
    private GameObject settingsPanel;
    private AudioSource menuMusicSource;
    private float menuMusicBaseVolume = 1f;
    private static Sprite roundedUiSprite;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        BuildInterface();
        ConfigureMusic();
    }

    /// <summary>
    /// Loads the gameplay scene configured in the Inspector.
    /// </summary>
    public void Play()
    {
        SceneManager.LoadScene(GameplaySceneName, LoadSceneMode.Single);
    }

    /// <summary>
    /// Replaces the main navigation panel with the settings placeholder.
    /// </summary>
    public void OpenSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    /// <summary>
    /// Returns from the settings placeholder to the main navigation panel.
    /// </summary>
    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

    /// <summary>
    /// Closes a standalone build, or exits Play Mode when testing in Unity.
    /// </summary>
    public void QuitToDesktop()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BuildInterface()
    {
        EnsureEventSystem();

        GameObject canvasObject = new("Start Menu Canvas", typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        mainPanel = CreatePanel(canvasObject.transform, "Main Menu");
        if (logoTexture != null)
            AddLogo(mainPanel.transform, logoTexture);
        else
            AddLabel(mainPanel.transform, gameTitle, 48, 210f);
        AddButton(mainPanel.transform, "PLAY", playButtonTexture, Play);
        AddButton(mainPanel.transform, "SETTINGS", settingsButtonTexture, OpenSettings);
        AddButton(mainPanel.transform, "LEAVE TO DESKTOP", exitButtonTexture, QuitToDesktop);

        settingsPanel = CreatePanel(canvasObject.transform, "Audio Settings");
        StyleSettingsPanel(settingsPanel);
        AddSettingsHeader(settingsPanel.transform);
        AddVolumeSlider(settingsPanel.transform, "MASTER", AudioVolumeSettings.Master,
            AudioVolumeSettings.SetMaster);
        AddVolumeSlider(settingsPanel.transform, "BGM", AudioVolumeSettings.Bgm, value =>
        {
            AudioVolumeSettings.SetBgm(value);
            RefreshMenuMusicVolume();
        });
        AddVolumeSlider(settingsPanel.transform, "SOUND EFFECTS",
            AudioVolumeSettings.SoundEffects, AudioVolumeSettings.SetSoundEffects);
        AddButton(settingsPanel.transform, "BACK", backButtonTexture, CloseSettings);
        settingsPanel.SetActive(false);
    }

    private GameObject CreatePanel(Transform parent, string panelName)
    {
        GameObject panel = new(panelName, typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = panelSize;
        rect.anchoredPosition = panelOffset;

        Image panelImage = panel.GetComponent<Image>();
        panelImage.color = Color.clear;
        panelImage.raycastTarget = false;

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 32, 32);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        return panel;
    }

    private void StyleSettingsPanel(GameObject panel)
    {
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(620f, 700f);

        Image background = panel.GetComponent<Image>();
        background.sprite = GetRoundedUiSprite();
        background.type = Image.Type.Sliced;
        background.color = new Color(0.035f, 0.045f, 0.09f, 0.97f);
        background.raycastTarget = true;

        Shadow shadow = panel.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.42f);
        shadow.effectDistance = new Vector2(10f, -10f);

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(60, 60, 40, 40);
        layout.childAlignment = TextAnchor.MiddleCenter;
    }

    private void AddSettingsHeader(Transform parent)
    {
        GameObject header = new("Settings Header", typeof(RectTransform), typeof(Image),
            typeof(LayoutElement));
        header.transform.SetParent(parent, false);
        header.GetComponent<RectTransform>().sizeDelta = new Vector2(500f, 86f);
        LayoutElement layout = header.GetComponent<LayoutElement>();
        layout.preferredWidth = 500f;
        layout.preferredHeight = 86f;

        Image background = header.GetComponent<Image>();
        background.sprite = GetRoundedUiSprite();
        background.type = Image.Type.Sliced;
        background.color = textColor;
        background.raycastTarget = false;

        Text title = CreateText(header.transform, "Title", "AUDIO SETTINGS", 31,
            TextAnchor.MiddleLeft);
        title.color = new Color(0.11f, 0.15f, 0.29f, 1f);
        SetRect(title.rectTransform, Vector2.zero, Vector2.one,
            new Vector2(34f, 0f), new Vector2(-95f, 0f));

        AddAccentTab(header.transform, 42);
    }

    private void AddLabel(Transform parent, string value, int fontSize, float height)
    {
        GameObject labelObject = new("Label", typeof(RectTransform), typeof(Text),
            typeof(LayoutElement));
        labelObject.transform.SetParent(parent, false);

        labelObject.GetComponent<RectTransform>().sizeDelta = new Vector2(buttonSize.x, height);
        LayoutElement layout = labelObject.GetComponent<LayoutElement>();
        layout.preferredWidth = buttonSize.x;
        layout.preferredHeight = height;

        Text label = labelObject.GetComponent<Text>();
        label.text = value;
        label.font = ResolveFont();
        label.fontSize = fontSize;
        label.color = textColor;
        label.alignment = TextAnchor.MiddleCenter;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 16;
        label.resizeTextMaxSize = fontSize;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private void AddButton(
        Transform parent,
        string label,
        Texture2D artwork,
        UnityEngine.Events.UnityAction action)
    {
        System.Type graphicType = artwork != null ? typeof(RawImage) : typeof(Image);
        GameObject buttonObject = new(label, typeof(RectTransform), graphicType, typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        Vector2 displaySize = artwork != null ? buttonSize : new Vector2(buttonSize.x, 86f);
        buttonObject.GetComponent<RectTransform>().sizeDelta = displaySize;
        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = displaySize.x;
        layout.preferredHeight = displaySize.y;

        Graphic background = buttonObject.GetComponent<Graphic>();
        background.color = artwork != null ? Color.white : buttonColor;
        if (artwork != null)
            ((RawImage)background).texture = artwork;
        else
        {
            Image fallback = (Image)background;
            fallback.sprite = GetRoundedUiSprite();
            fallback.type = Image.Type.Sliced;
        }

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = artwork != null ? Color.white : buttonColor;
        colors.highlightedColor = artwork != null
            ? new Color(1f, 1f, 1f, 0.82f)
            : buttonHighlightColor;
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = accentColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.targetGraphic = background;
        button.onClick.AddListener(action);

        if (artwork != null)
            return;

        GameObject textObject = new("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(34f, 0f);
        textRect.offsetMax = new Vector2(-90f, 0f);

        Text text = textObject.GetComponent<Text>();
        text.text = label;
        text.font = ResolveFont();
        text.fontSize = 28;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleLeft;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 14;
        text.resizeTextMaxSize = 28;

        AddAccentTab(buttonObject.transform, 38);

    }

    private void AddLogo(Transform parent, Texture2D texture)
    {
        Vector2 displaySize = new(620f, 175f);
        GameObject frame = new("Game Logo", typeof(RectTransform), typeof(RawImage),
            typeof(LayoutElement));
        frame.transform.SetParent(parent, false);
        frame.GetComponent<RectTransform>().sizeDelta = displaySize;

        LayoutElement layout = frame.GetComponent<LayoutElement>();
        layout.preferredWidth = displaySize.x;
        layout.preferredHeight = displaySize.y;

        RawImage image = frame.GetComponent<RawImage>();
        image.texture = texture;
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private Font ResolveFont()
    {
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font;
    }

    private void AddVolumeSlider(
        Transform parent,
        string label,
        float initialValue,
        UnityEngine.Events.UnityAction<float> onChanged)
    {
        GameObject row = new(label, typeof(RectTransform), typeof(Image),
            typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        row.GetComponent<RectTransform>().sizeDelta = new Vector2(500f, 112f);
        LayoutElement rowLayout = row.GetComponent<LayoutElement>();
        rowLayout.preferredWidth = 500f;
        rowLayout.preferredHeight = 112f;

        Image rowBackground = row.GetComponent<Image>();
        rowBackground.sprite = GetRoundedUiSprite();
        rowBackground.type = Image.Type.Sliced;
        rowBackground.color = new Color(0.11f, 0.15f, 0.29f, 0.78f);
        rowBackground.raycastTarget = false;

        Text title = CreateText(row.transform, "Label", label, 25, TextAnchor.UpperLeft);
        SetRect(title.rectTransform, new Vector2(0f, 0.54f), Vector2.one,
            new Vector2(22f, 0f), new Vector2(-100f, 0f));

        Text percentage = CreateText(row.transform, "Value", string.Empty, 22,
            TextAnchor.UpperRight);
        SetRect(percentage.rectTransform, new Vector2(0.78f, 0.54f), Vector2.one,
            Vector2.zero, new Vector2(-22f, 0f));

        GameObject sliderObject = new("Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(row.transform, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        SetRect(sliderRect, new Vector2(0f, 0.08f), new Vector2(1f, 0.44f),
            new Vector2(22f, 0f), new Vector2(-22f, 0f));

        Image track = CreateSliderImage(sliderObject.transform, "Track", buttonColor);
        SetRect(track.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 0.62f),
            Vector2.zero, Vector2.zero);
        track.raycastTarget = true;

        GameObject fillArea = new("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        SetRect(fillArea.GetComponent<RectTransform>(), new Vector2(0f, 0.38f),
            new Vector2(1f, 0.62f), Vector2.zero, Vector2.zero);
        Image fill = CreateSliderImage(fillArea.transform, "Fill", accentColor);
        SetRect(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        GameObject handleArea = new("Handle Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObject.transform, false);
        SetRect(handleArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            new Vector2(12f, 0f), new Vector2(-12f, 0f));
        Image handle = CreateSliderImage(handleArea.transform, "Handle", textColor);
        RectTransform handleRect = handle.rectTransform;
        handleRect.sizeDelta = new Vector2(30f, 30f);
        handle.raycastTarget = true;

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.value = initialValue;
        slider.onValueChanged.AddListener(value =>
        {
            percentage.text = $"{Mathf.RoundToInt(value * 100f)}%";
            onChanged(value);
        });
        percentage.text = $"{Mathf.RoundToInt(initialValue * 100f)}%";
    }

    private Text CreateText(Transform parent, string objectName, string value,
        int size, TextAnchor alignment)
    {
        GameObject textObject = new(objectName, typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(parent, false);
        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = ResolveFont();
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = textColor;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private void AddAccentTab(Transform parent, int arrowSize)
    {
        GameObject accent = new("Accent", typeof(RectTransform), typeof(Image));
        accent.transform.SetParent(parent, false);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        SetRect(accentRect, new Vector2(0.84f, 0f), Vector2.one,
            Vector2.zero, Vector2.zero);
        Image accentImage = accent.GetComponent<Image>();
        accentImage.sprite = GetRoundedUiSprite();
        accentImage.type = Image.Type.Sliced;
        accentImage.color = accentColor;
        accentImage.raycastTarget = false;

        Text arrow = CreateText(accent.transform, "Arrow", ">", arrowSize,
            TextAnchor.MiddleCenter);
        arrow.color = textColor;
        SetRect(arrow.rectTransform, Vector2.zero, Vector2.one,
            Vector2.zero, Vector2.zero);
    }

    private static Image CreateSliderImage(Transform parent, string objectName, Color color)
    {
        GameObject imageObject = new(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.GetComponent<Image>();
        image.sprite = GetRoundedUiSprite();
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Sprite GetRoundedUiSprite()
    {
        if (roundedUiSprite != null)
            return roundedUiSprite;

        const int size = 32;
        const float radius = 10f;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            name = "Rounded UI Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color clear = new(1f, 1f, 1f, 0f);
        Color solid = Color.white;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nearestX = Mathf.Clamp(x + 0.5f, radius, size - radius);
                float nearestY = Mathf.Clamp(y + 0.5f, radius, size - radius);
                float distance = Vector2.Distance(
                    new Vector2(x + 0.5f, y + 0.5f),
                    new Vector2(nearestX, nearestY));
                texture.SetPixel(x, y, distance <= radius ? solid : clear);
            }
        }
        texture.Apply();

        roundedUiSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        roundedUiSprite.name = "Rounded UI Sprite";
        roundedUiSprite.hideFlags = HideFlags.HideAndDontSave;
        return roundedUiSprite;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private void ConfigureMusic()
    {
        if (backgroundMusic == null)
        {
            AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (AudioSource candidate in sources)
            {
                if (candidate.clip == null || !candidate.loop)
                    continue;
                menuMusicSource = candidate;
                menuMusicBaseVolume = candidate.volume;
                RefreshMenuMusicVolume();
                return;
            }
            return;
        }

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = backgroundMusic;
        menuMusicSource = source;
        menuMusicBaseVolume = musicVolume;
        RefreshMenuMusicVolume();
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.Play();
    }

    private void RefreshMenuMusicVolume()
    {
        if (menuMusicSource != null)
            menuMusicSource.volume = menuMusicBaseVolume * AudioVolumeSettings.Bgm;
    }
}
