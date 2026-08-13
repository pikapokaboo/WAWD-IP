using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PauseMenuController : MonoBehaviour
{
    private static readonly Color Navy = new(0.11f, 0.15f, 0.29f, 1f);
    private static readonly Color NavyHover = new(0.17f, 0.22f, 0.40f, 1f);
    private static readonly Color Cream = new(1f, 0.98f, 0.86f, 1f);
    private static readonly Color Rust = new(0.68f, 0.18f, 0.07f, 1f);
    private static Sprite roundedSprite;

    private GameObject pausePanel;
    private GameObject settingsPanel;
    private GameObject dimmer;
    private float previousTimeScale = 1f;
    private bool paused;
    private Font font;
    private PlayerController player;
    private bool playerWasEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForMainScene()
    {
        if (SceneManager.GetActiveScene().name != "Main_Scene"
            || FindFirstObjectByType<PauseMenuController>() != null)
            return;

        new GameObject("Pause Menu").AddComponent<PauseMenuController>();
    }

    private void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        player = FindFirstObjectByType<PlayerController>();
        BuildInterface();
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame
            || DeveloperConsole.AnyConsoleOpen)
            return;

        if (!paused) OpenPauseMenu();
        else if (settingsPanel.activeSelf) ShowPausePanel();
        else ContinueGame();
    }

    private void OpenPauseMenu()
    {
        paused = true;
        previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        playerWasEnabled = player != null && player.enabled;
        if (player != null)
            player.enabled = false;
        Time.timeScale = 0f;
        pausePanel.SetActive(true);
        settingsPanel.SetActive(false);
        dimmer.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void ContinueGame()
    {
        paused = false;
        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        dimmer.SetActive(false);
        Time.timeScale = previousTimeScale;
        if (player != null && playerWasEnabled)
            player.enabled = true;
        Cursor.lockState = CctvSystem.IsActive ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = CctvSystem.IsActive;
    }

    private void ShowSettings()
    {
        pausePanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    private void ShowPausePanel()
    {
        settingsPanel.SetActive(false);
        pausePanel.SetActive(true);
    }

    private void ReturnToTitle()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SceneManager.LoadScene("Home_Screen", LoadSceneMode.Single);
    }

    private void BuildInterface()
    {
        EnsureEventSystem();
        GameObject canvasObject = new("Pause Menu Canvas", typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        dimmer = CreateDimmer(canvasObject.transform);
        pausePanel = CreateCard(canvasObject.transform, "Paused", new Vector2(620f, 590f));
        AddHeader(pausePanel.transform, "PAUSED");
        AddButton(pausePanel.transform, "CONTINUE", ContinueGame);
        AddButton(pausePanel.transform, "SETTINGS", ShowSettings);
        AddButton(pausePanel.transform, "RETURN TO TITLE", ReturnToTitle);

        settingsPanel = CreateCard(canvasObject.transform, "Pause Settings", new Vector2(620f, 700f));
        AddHeader(settingsPanel.transform, "AUDIO SETTINGS");
        AddSlider(settingsPanel.transform, "MASTER", AudioVolumeSettings.Master,
            AudioVolumeSettings.SetMaster);
        AddSlider(settingsPanel.transform, "BGM", AudioVolumeSettings.Bgm,
            AudioVolumeSettings.SetBgm);
        AddSlider(settingsPanel.transform, "SOUND EFFECTS", AudioVolumeSettings.SoundEffects,
            AudioVolumeSettings.SetSoundEffects);
        AddButton(settingsPanel.transform, "BACK", ShowPausePanel);

        pausePanel.SetActive(false);
        settingsPanel.SetActive(false);
        dimmer.SetActive(false);
    }

    private static GameObject CreateDimmer(Transform parent)
    {
        GameObject dimmer = new("Dimmer", typeof(RectTransform), typeof(Image));
        dimmer.transform.SetParent(parent, false);
        RectTransform rect = dimmer.GetComponent<RectTransform>();
        Stretch(rect);
        Image image = dimmer.GetComponent<Image>();
        image.color = new Color(0.01f, 0.015f, 0.035f, 0.42f);
        image.raycastTarget = false;
        return dimmer;
    }

    private GameObject CreateCard(Transform parent, string name, Vector2 size)
    {
        GameObject card = new(name, typeof(RectTransform), typeof(Image),
            typeof(VerticalLayoutGroup));
        card.transform.SetParent(parent, false);
        RectTransform rect = card.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        Image image = card.GetComponent<Image>();
        image.sprite = RoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = new Color(0.035f, 0.045f, 0.09f, 0.97f);
        VerticalLayoutGroup layout = card.GetComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(60, 60, 45, 45);
        layout.spacing = 20f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        Shadow shadow = card.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(10f, -10f);
        return card;
    }

    private void AddHeader(Transform parent, string label)
    {
        GameObject header = CreateBox(parent, label, new Vector2(500f, 86f), Cream);
        AddText(header.transform, label, 31, new Color(0.11f, 0.15f, 0.29f, 1f),
            TextAnchor.MiddleLeft, new Vector2(32f, 0f), new Vector2(-100f, 0f));
        AddAccent(header.transform, 86f);
    }

    private void AddButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        GameObject box = CreateBox(parent, label, new Vector2(500f, 92f), Navy);
        Button button = box.AddComponent<Button>();
        button.targetGraphic = box.GetComponent<Image>();
        ColorBlock colours = button.colors;
        colours.normalColor = Navy;
        colours.highlightedColor = NavyHover;
        colours.selectedColor = NavyHover;
        colours.pressedColor = Rust;
        colours.fadeDuration = 0.08f;
        button.colors = colours;
        button.onClick.AddListener(action);
        AddText(box.transform, label, 28, Cream, TextAnchor.MiddleLeft,
            new Vector2(32f, 0f), new Vector2(-100f, 0f));
        AddAccent(box.transform, 92f);
    }

    private void AddSlider(Transform parent, string label, float value,
        UnityEngine.Events.UnityAction<float> callback)
    {
        GameObject row = CreateBox(parent, label, new Vector2(500f, 112f),
            new Color(0.11f, 0.15f, 0.29f, 0.92f));
        Text title = AddText(row.transform, label, 24, Cream, TextAnchor.UpperLeft,
            new Vector2(22f, 0f), new Vector2(-110f, 0f));
        SetRect(title.rectTransform, new Vector2(0f, 0.53f), Vector2.one,
            new Vector2(22f, 0f), new Vector2(-110f, 0f));
        Text amount = AddText(row.transform, "", 22, Cream, TextAnchor.UpperRight,
            Vector2.zero, new Vector2(-22f, 0f));
        SetRect(amount.rectTransform, new Vector2(0.75f, 0.53f), Vector2.one,
            Vector2.zero, new Vector2(-22f, 0f));

        GameObject sliderObject = new("Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(row.transform, false);
        SetRect(sliderObject.GetComponent<RectTransform>(), new Vector2(0f, 0.08f),
            new Vector2(1f, 0.44f), new Vector2(22f, 0f), new Vector2(-22f, 0f));
        Image track = AddImage(sliderObject.transform, "Track", NavyHover);
        SetRect(track.rectTransform, new Vector2(0f, 0.38f), new Vector2(1f, 0.62f),
            Vector2.zero, Vector2.zero);
        track.raycastTarget = true;
        GameObject fillArea = new("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        SetRect(fillArea.GetComponent<RectTransform>(), new Vector2(0f, 0.38f),
            new Vector2(1f, 0.62f), Vector2.zero, Vector2.zero);
        Image fill = AddImage(fillArea.transform, "Fill", Rust);
        Stretch(fill.rectTransform);
        GameObject handleArea = new("Handle Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObject.transform, false);
        SetRect(handleArea.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
            new Vector2(15f, 0f), new Vector2(-15f, 0f));
        Image handle = AddImage(handleArea.transform, "Handle", Cream);
        handle.rectTransform.sizeDelta = new Vector2(30f, 30f);
        handle.raycastTarget = true;
        Slider slider = sliderObject.GetComponent<Slider>();
        slider.fillRect = fill.rectTransform;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = value;
        slider.onValueChanged.AddListener(v =>
        {
            amount.text = $"{Mathf.RoundToInt(v * 100f)}%";
            callback(v);
        });
        amount.text = $"{Mathf.RoundToInt(value * 100f)}%";
    }

    private GameObject CreateBox(Transform parent, string name, Vector2 size, Color colour)
    {
        GameObject box = new(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        box.transform.SetParent(parent, false);
        box.GetComponent<RectTransform>().sizeDelta = size;
        LayoutElement element = box.GetComponent<LayoutElement>();
        element.preferredWidth = size.x;
        element.preferredHeight = size.y;
        Image image = box.GetComponent<Image>();
        image.sprite = RoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = colour;
        return box;
    }

    private Text AddText(Transform parent, string value, int size, Color colour,
        TextAnchor alignment, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject obj = new("Text", typeof(RectTransform), typeof(Text));
        obj.transform.SetParent(parent, false);
        Text text = obj.GetComponent<Text>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = colour;
        text.alignment = alignment;
        text.raycastTarget = false;
        SetRect(text.rectTransform, Vector2.zero, Vector2.one, offsetMin, offsetMax);
        return text;
    }

    private void AddAccent(Transform parent, float height)
    {
        Image accent = AddImage(parent, "Accent", Rust);
        SetRect(accent.rectTransform, new Vector2(0.84f, 0f), Vector2.one,
            Vector2.zero, Vector2.zero);
        AddText(accent.transform, ">", Mathf.RoundToInt(height * 0.45f), Cream,
            TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
    }

    private static Image AddImage(Transform parent, string name, Color colour)
    {
        GameObject obj = new(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.sprite = RoundedSprite();
        image.type = Image.Type.Sliced;
        image.color = colour;
        image.raycastTarget = false;
        return image;
    }

    private static Sprite RoundedSprite()
    {
        if (roundedSprite != null) return roundedSprite;
        const int size = 32;
        const int radius = 10;
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false);
        texture.hideFlags = HideFlags.HideAndDontSave;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = Mathf.Clamp(x + 0.5f, radius, size - radius);
            float ny = Mathf.Clamp(y + 0.5f, radius, size - radius);
            float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f),
                new Vector2(nx, ny));
            texture.SetPixel(x, y, distance <= radius ? Color.white : Color.clear);
        }
        texture.Apply();
        roundedSprite = Sprite.Create(texture, new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
        roundedSprite.hideFlags = HideFlags.HideAndDontSave;
        return roundedSprite;
    }

    private static void Stretch(RectTransform rect) =>
        SetRect(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

    private static void SetRect(RectTransform rect, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
    }

    private void OnDestroy()
    {
        if (!paused) return;
        Time.timeScale = previousTimeScale;
        if (player != null && playerWasEnabled)
            player.enabled = true;
    }
}
