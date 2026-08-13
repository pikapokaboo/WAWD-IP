// -----------------------------------------------------------------------------
// File: CashierInteractable.cs
// Project: WAWD Integrated Studio Project
// Purpose: Controls player dialogue and interaction with the cashier.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class CashierInteractable : MonoBehaviour
{
    [Header("Interaction")]
    [SerializeField] private string prompt = "[E] Talk";
    [SerializeField] private string[] dialogue =
    {
        "Welcome! Let me know if you need anything.",
        "Long day, huh?",
        "Please don't ask me to price-check the entire shop.",
        "The register and I are in a committed relationship."
    };
    [Header("Visual Novel Display")]
    [SerializeField] private string speakerName = "Cashier";
    [SerializeField, Min(0f)] private float charactersPerSecond = 45f;
    [SerializeField] private Color panelColour = new(0.025f, 0.03f, 0.045f, 0.92f);
    [SerializeField] private Color nameColour = new(0.35f, 0.8f, 1f, 1f);
    [SerializeField] private Color dialogueColour = Color.white;
    [SerializeField] private string greeting = "Hello! What can I help you with?";
    [SerializeField] private string[] dialogueOptions =
    {
        "How is your day going?",
        "Any shopping advice?",
        "Goodbye"
    };
    [SerializeField] private string[] optionResponses =
    {
        "Not bad! The register has only yelled at me twice today.",
        "Never shop hungry. That's how you leave with twelve snacks.",
        "See you around!"
    };
    [SerializeField] private string[] casualDialogueOptions =
    {
        "How's your shift going?",
        "Anything interesting happen today?",
        "What do you think of the customers?",
        "Any advice for surviving this place?",
        "I'd better get back to work."
    };
    [SerializeField] private string[] shiftResponses =
    {
        "Pretty normal. The till beeped at me in a judgmental tone, though.",
        "Quiet so far. Saying that out loud has probably doomed us.",
        "I've been standing here so long I think the counter considers me furniture."
    };
    [SerializeField] private string[] interestingResponses =
    {
        "Someone spent ten minutes choosing milk and left with crisps.",
        "A customer apologised to the automatic door. Very polite.",
        "I watched someone check their pockets for a phone they were holding."
    };
    [SerializeField] private string[] customerResponses =
    {
        "Most are lovely. Some navigate aisles like lost shopping trolleys.",
        "They're unpredictable. That's why you're watching the cameras, right?",
        "I like them. I just wish they knew queues are lines, not abstract art."
    };
    [SerializeField] private string[] adviceResponses =
    {
        "Coffee, comfortable shoes, and never trust an unusually innocent shopper.",
        "Keep an eye on quiet corners. Also keep snacks nearby. Mostly the snacks.",
        "If something looks suspicious, check twice before reporting grandma."
    };

    [Header("Highlight")]
    [SerializeField] private Color outlineColour = Color.white;
    [SerializeField, Range(0f, 10f)] private float outlineWidth = 4f;

    private Outline outline;
    private string currentLine;
    private float lineStartedAt;
    private bool dialogueOpen;
    private GUIStyle panelStyle;
    private GUIStyle nameStyle;
    private GUIStyle dialogueStyle;
    private GUIStyle hintStyle;
    private GUIStyle optionButtonStyle;
    private Texture2D panelTexture;
    private Transform conversationPartner;
    private bool showOptions;
    private bool closeAfterLine;
    private bool openingBriefing;

    public string Prompt => prompt;
    public bool DialogueOpen => dialogueOpen;

    private void Awake()
    {
        outline = GetComponent<Outline>();
        if (outline == null)
            outline = gameObject.AddComponent<Outline>();
        outline.OutlineMode = Outline.Mode.OutlineVisible;
        outline.OutlineColor = outlineColour;
        outline.OutlineWidth = outlineWidth;
        outline.enabled = false;
    }

    public void SetTargeted(bool targeted)
    {
        if (outline != null)
            outline.enabled = targeted;
    }

    public void BeginConversation(Transform partner)
    {
        conversationPartner = partner;
        openingBriefing = OpeningSequence.Instance != null
            && OpeningSequence.Instance.NeedsCashierBriefing;
        currentLine = openingBriefing
            ? "Hey, you must be the new security guard. Welcome aboard. Head to the security room and get the workstation ready before we open."
            : GetCasualGreeting();
        lineStartedAt = Time.unscaledTime;
        dialogueOpen = true;
        showOptions = false;
        closeAfterLine = false;
    }

    public void AdvanceText()
    {
        if (!dialogueOpen || string.IsNullOrEmpty(currentLine))
            return;
        if (VisibleCharacterCount < currentLine.Length)
            lineStartedAt = Time.unscaledTime - currentLine.Length
                / Mathf.Max(1f, charactersPerSecond);
    }

    public void CloseDialogue() => dialogueOpen = false;

    private void OnDisable()
    {
        if (outline != null)
            outline.enabled = false;
        dialogueOpen = false;
    }

    private int VisibleCharacterCount => string.IsNullOrEmpty(currentLine)
        ? 0
        : charactersPerSecond <= 0f
            ? currentLine.Length
            : Mathf.Clamp(Mathf.FloorToInt(
                (Time.unscaledTime - lineStartedAt) * charactersPerSecond),
                0, currentLine.Length);

    private void Update()
    {
        if (dialogueOpen && Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseDialogue();

        if (!dialogueOpen || conversationPartner == null)
            return;
        Vector3 direction = conversationPartner.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(direction), 360f * Time.unscaledDeltaTime);
    }

    private void OnGUI()
    {
        if (!dialogueOpen || string.IsNullOrEmpty(currentLine))
            return;

        EnsureDialogueStyles();
        float margin = Mathf.Clamp(Screen.width * 0.06f, 18f, 90f);
        float panelHeight = Mathf.Clamp(Screen.height * 0.34f, 140f, 310f);
        float bottomMargin = Mathf.Clamp(Screen.height * 0.035f, 10f, 32f);
        Rect panelRect = new(margin, Screen.height - panelHeight - bottomMargin,
            Screen.width - margin * 2f, panelHeight);
        GUI.Box(panelRect, GUIContent.none, panelStyle);

        Rect nameRect = new(panelRect.x + 24f, panelRect.y + 12f,
            panelRect.width - 48f, 38f);
        GUI.Label(nameRect, speakerName, nameStyle);

        string visibleText = currentLine.Substring(0, VisibleCharacterCount);
        Rect textRect = new(panelRect.x + 24f, panelRect.y + 52f,
            panelRect.width - 48f, panelRect.height - 88f);
        GUI.Label(textRect, visibleText, dialogueStyle);

        string hint = VisibleCharacterCount < currentLine.Length
            ? "E  Show full text     Esc  Close"
            : "E  Continue     Esc  Close";
        GUI.Label(new Rect(panelRect.x + 24f, panelRect.yMax - 32f,
            panelRect.width - 48f, 24f), hint, hintStyle);

        if (VisibleCharacterCount >= currentLine.Length)
        {
            if (closeAfterLine)
            {
                if (GUI.Button(new Rect(panelRect.xMax - 210f,
                    panelRect.yMax - 86f, 180f, 48f), "Close",
                    optionButtonStyle))
                    CloseDialogue();
            }
            else
            {
                showOptions = true;
                DrawOptions(panelRect);
            }
        }
    }

    private void DrawOptions(Rect panelRect)
    {
        if (openingBriefing)
        {
            DrawOpeningOptions(panelRect);
            return;
        }
        DayNightCycle cycle = FindFirstObjectByType<DayNightCycle>();
        if (cycle != null && cycle.CurrentDay >= 2)
        {
            DrawCasualOptions(panelRect);
            return;
        }
        if (!showOptions || dialogueOptions == null)
            return;
        float width = Mathf.Min(520f, Screen.width - 36f);
        float x = (Screen.width - width) * 0.5f;
        const float optionHeight = 58f;
        float y = Mathf.Max(10f,
            panelRect.y - dialogueOptions.Length * (optionHeight + 6f) - 10f);
        for (int i = 0; i < dialogueOptions.Length; i++)
        {
            if (!GUI.Button(new Rect(x, y + i * (optionHeight + 6f), width, optionHeight),
                    dialogueOptions[i], optionButtonStyle))
                continue;
            bool goodbye = dialogueOptions[i].IndexOf("goodbye",
                System.StringComparison.OrdinalIgnoreCase) >= 0;
            currentLine = i < optionResponses.Length
                ? optionResponses[i]
                : dialogue[Random.Range(0, dialogue.Length)];
            lineStartedAt = Time.unscaledTime;
            showOptions = false;
            closeAfterLine = goodbye;
            break;
        }
    }

    private string GetCasualGreeting()
    {
        DayNightCycle cycle = FindFirstObjectByType<DayNightCycle>();
        if (cycle == null || cycle.CurrentDay < 2)
            return greeting;
        string[] greetings =
        {
            $"Morning! Ready for day {cycle.CurrentDay}?",
            "Hey again. Survived another shift, I see.",
            "Welcome back! The shop hasn't exploded yet, so we're doing well.",
            "Good to see you. Fancy a quick chat before things get busy?"
        };
        return greetings[Random.Range(0, greetings.Length)];
    }

    private void DrawCasualOptions(Rect panelRect)
    {
        if (!showOptions || casualDialogueOptions == null) return;
        float width = Mathf.Min(560f, Screen.width - 36f);
        float x = (Screen.width - width) * 0.5f;
        float optionHeight = Mathf.Clamp((panelRect.y - 20f)
            / Mathf.Max(1, casualDialogueOptions.Length) - 5f, 36f, 54f);
        float y = Mathf.Max(10f,
            panelRect.y - casualDialogueOptions.Length * (optionHeight + 5f) - 8f);
        for (int i = 0; i < casualDialogueOptions.Length; i++)
        {
            if (!GUI.Button(new Rect(x, y + i * (optionHeight + 5f), width, optionHeight),
                    casualDialogueOptions[i], optionButtonStyle)) continue;
            bool goodbye = i == casualDialogueOptions.Length - 1;
            currentLine = i switch
            {
                0 => PickRandom(shiftResponses),
                1 => PickRandom(interestingResponses),
                2 => PickRandom(customerResponses),
                3 => PickRandom(adviceResponses),
                _ => "Alright. Try not to catch anyone stealing the entire shelf."
            };
            lineStartedAt = Time.unscaledTime;
            showOptions = false;
            closeAfterLine = goodbye;
            break;
        }
    }

    private static string PickRandom(string[] lines) => lines != null && lines.Length > 0
        ? lines[Random.Range(0, lines.Length)]
        : "Not much to say about that yet.";

    private void DrawOpeningOptions(Rect panelRect)
    {
        string[] options =
        {
            "What am I supposed to do as the new security guard?",
            "Where is the security room?",
            "Thanks. I'll get ready."
        };
        float width = Mathf.Min(620f, Screen.width - 36f);
        float x = (Screen.width - width) * 0.5f;
        const float optionHeight = 62f;
        float y = Mathf.Max(10f,
            panelRect.y - options.Length * (optionHeight + 6f) - 10f);
        for (int i = 0; i < options.Length; i++)
        {
            if (!GUI.Button(new Rect(x, y + i * (optionHeight + 6f), width, optionHeight), options[i],
                    optionButtonStyle))
                continue;
            if (i == 0)
                currentLine = "Watch the customers through the security cameras and catch shoplifters. Look for tells like someone checking whether anybody is watching, acting suspicious around products, or saying suspicious things out loud. Hover over a customer and report them when you're confidentâ€”but be careful, because reporting an innocent shopper will count as a false report.";
            else if (i == 1)
                currentLine = "Go into the storage room behind me and use the door in there. You can also enter through the door beside the shelves.";
            else
            {
                currentLine = "Thanks. Once you use the workstation, we'll open the store for the day.";
                OpeningSequence.Instance?.FinishCashierBriefing();
                closeAfterLine = true;
            }
            lineStartedAt = Time.unscaledTime;
            showOptions = false;
            break;
        }
    }

    private void EnsureDialogueStyles()
    {
        if (panelTexture == null)
        {
            panelTexture = new Texture2D(1, 1);
            panelTexture.SetPixel(0, 0, panelColour);
            panelTexture.Apply();
        }
        panelStyle ??= new GUIStyle(GUI.skin.box);
        panelStyle.normal.background = panelTexture;
        nameStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 30,
            fontStyle = FontStyle.Bold
        };
        nameStyle.normal.textColor = nameColour;
        dialogueStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 27,
            wordWrap = true,
            alignment = TextAnchor.UpperLeft
        };
        dialogueStyle.normal.textColor = dialogueColour;
        hintStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            alignment = TextAnchor.MiddleRight
        };
        hintStyle.normal.textColor = new Color(0.75f, 0.8f, 0.88f, 1f);
        optionButtonStyle ??= new GUIStyle(GUI.skin.button)
        {
            fontSize = 21,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            padding = new RectOffset(14, 14, 6, 6)
        };
    }

    private void OnDestroy()
    {
        if (panelTexture != null)
            Destroy(panelTexture);
    }
}
