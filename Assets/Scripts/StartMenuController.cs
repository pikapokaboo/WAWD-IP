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
    [Header("Navigation")]
    [SerializeField] private string gameplaySceneName = "Main";

    [Header("Text")]
    [SerializeField] private string gameTitle = "What ever goofy thing we bouta name dis";
    [SerializeField] private string settingsPlaceholder = "Will insert settings later";

    [Header("Layout")]
    [SerializeField] private Vector2 panelSize = new(560f, 640f);
    [SerializeField] private Vector2 buttonSize = new(420f, 64f);
    [SerializeField, Min(0f)] private float spacing = 18f;

    [Header("Style")]
    [SerializeField] private Color panelColor = new(0.12f, 0.12f, 0.12f, 0.88f);
    [SerializeField] private Color buttonColor = new(0.38f, 0.38f, 0.38f, 1f);
    [SerializeField] private Color buttonHighlightColor = new(0.55f, 0.55f, 0.55f, 1f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Font font;

    [Header("Music")]
    [Tooltip("Drop a music AudioClip here. Leaving this empty keeps the menu silent.")]
    [SerializeField] private AudioClip backgroundMusic;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;

    private GameObject mainPanel;
    private GameObject settingsPanel;

    private void Awake()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        BuildInterface();
        ConfigureMusic();
    }

    public void Play()
    {
        if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            Debug.LogError(
                $"Cannot load scene '{gameplaySceneName}'. Check its name and add it to Build Settings.",
                this);
            return;
        }

        SceneManager.LoadScene(gameplaySceneName);
    }

    public void OpenSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void CloseSettings()
    {
        settingsPanel.SetActive(false);
        mainPanel.SetActive(true);
    }

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
        AddLabel(mainPanel.transform, gameTitle, 48, 210f);
        AddButton(mainPanel.transform, "PLAY", Play);
        AddButton(mainPanel.transform, "SETTINGS", OpenSettings);
        AddButton(mainPanel.transform, "LEAVE TO DESKTOP", QuitToDesktop);

        settingsPanel = CreatePanel(canvasObject.transform, "Settings Placeholder");
        AddLabel(settingsPanel.transform, settingsPlaceholder, 32, 180f);
        AddButton(settingsPanel.transform, "BACK", CloseSettings);
        settingsPanel.SetActive(false);
    }

    private GameObject CreatePanel(Transform parent, string panelName)
    {
        GameObject panel = new(panelName, typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = panelSize;

        panel.GetComponent<Image>().color = panelColor;

        VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(50, 50, 45, 45);
        layout.spacing = spacing;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panel.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        return panel;
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

    private void AddButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new(label, typeof(RectTransform), typeof(Image), typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);

        buttonObject.GetComponent<RectTransform>().sizeDelta = buttonSize;
        LayoutElement layout = buttonObject.GetComponent<LayoutElement>();
        layout.preferredWidth = buttonSize.x;
        layout.preferredHeight = buttonSize.y;

        Image image = buttonObject.GetComponent<Image>();
        image.color = buttonColor;

        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = buttonColor;
        colors.highlightedColor = buttonHighlightColor;
        colors.selectedColor = buttonHighlightColor;
        colors.pressedColor = buttonHighlightColor * 0.8f;
        button.colors = colors;
        button.targetGraphic = image;
        button.onClick.AddListener(action);

        GameObject textObject = new("Text", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;

        Text text = textObject.GetComponent<Text>();
        text.text = label;
        text.font = ResolveFont();
        text.fontSize = 26;
        text.color = textColor;
        text.alignment = TextAnchor.MiddleCenter;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 14;
        text.resizeTextMaxSize = 26;
    }

    private Font ResolveFont()
    {
        if (font == null)
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font;
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
            return;

        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = backgroundMusic;
        source.volume = musicVolume;
        source.loop = true;
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        source.Play();
    }
}
