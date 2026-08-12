using System.Collections.Generic;
using UnityEngine;

/// <summary>Displays short, gameplay-facing dialogue above a character.</summary>
[DisallowMultipleComponent]
public sealed class NpcSpeechBubble : MonoBehaviour
{
    private static readonly HashSet<NpcSpeechBubble> ActiveSpeakers = new();

    [Header("Display")]
    [SerializeField, Min(0f)] private float height = 3.65f;
    [SerializeField, Min(0.25f)] private float defaultDuration = 2.75f;
    [SerializeField, Min(8)] private int fontSize = 17;
    [SerializeField, Min(100f)] private float maximumDistance = 35f;
    [SerializeField] private Color bubbleColour = new(0.05f, 0.06f, 0.08f, 0.82f);
    [SerializeField] private Color textColour = Color.white;

    [Header("Reactions")]
    [SerializeField, Range(0f, 100f)] private float slowWalkerReactionChance = 40f;
    [SerializeField, Range(0f, 100f)] private float crowdingReactionChance = 55f;
    [SerializeField, Min(0f)] private float reactionCooldown = 8f;
    [SerializeField] private string[] slowWalkerLines =
    {
        "Excuse me...", "Can I get past?", "Bit of a hurry here!", "Coming through!"
    };
    [SerializeField] private string[] crowdingLines =
    {
        "Hey!", "Watch it!", "A little space, please!", "Oof!",
        "Personal space is still free!", "This isn't bumper cars!"
    };
    [SerializeField] private string[] bulldozedLines =
    {
        "I'm walking here!", "Was that really necessary?", "Okay, you win!",
        "Apparently I'm part of the trolley now.", "Please stop pushing me!"
    };
    [SerializeField] private string[] pushingPastLines =
    {
        "Sorry, coming through!", "Pardon me!", "Urgent shopping business!",
        "Beep beep!", "The aisle waits for no one!"
    };
    [SerializeField] private string[] bumpLines =
    {
        "Oops, sorry!", "Excuse me!", "Didn't see you there!", "Bonk!",
        "Aisle collision!", "We need indicators on these things."
    };

    [Header("Shopping Thoughts")]
    [SerializeField, Range(0f, 100f)] private float shoppingThoughtChance = 45f;
    [SerializeField, Min(0f)] private float thoughtCooldown = 5f;
    [SerializeField] private string[] productThoughts =
    {
        "Now, where is the {0}?", "I only came in for {0}...",
        "Can't forget the {0}.", "Mission: find {0}."
    };
    [SerializeField] private string[] browsingThoughts =
    {
        "Ooh, what's this?", "Just looking...", "Do I need this? Probably not.",
        "Window shopping, but indoors.", "I have completely forgotten my list."
    };
    [SerializeField] private string[] finishedBrowsingThoughts =
    {
        "Back to the list.", "I should probably keep moving.",
        "Nope, don't need it.", "My wallet survives another aisle."
    };
    [SerializeField] private string[] queueThoughts =
    {
        "This queue isn't too bad.", "I always choose the slow line.",
        "Nearly there...", "Should've used self-checkout.", "Time to inspect the receipt sweets."
    };
    [SerializeField] private string[] foundProductThoughts =
    {
        "There it is!", "Into the basket you go.", "Excellent choice, me.",
        "That's one thing off the list."
    };
    [SerializeField] private string[] stealingThoughts =
    {
        "Nobody saw that...", "Free sample, right?", "Act natural.",
        "This is between me and the shelf."
    };

    private string message;
    private float visibleUntil;
    private float nextReactionTime;
    private float nextThoughtTime;
    private readonly HashSet<int> touchingNpcs = new();
    private CapsuleCollider capsule;
    private GUIStyle textStyle;
    private GUIStyle bubbleStyle;
    private Texture2D bubbleTexture;

    public bool IsSpeaking => Time.unscaledTime < visibleUntil;

    public void Say(string line, float duration = -1f)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;
        message = line.Trim();
        visibleUntil = Time.unscaledTime + (duration > 0f ? duration : defaultDuration);
    }

    public void SayRandom(string[] lines, float duration = -1f)
    {
        if (lines != null && lines.Length > 0)
            Say(lines[Random.Range(0, lines.Length)], duration);
    }

    public void ReactToSlowWalker()
    {
        TryReaction(slowWalkerLines, slowWalkerReactionChance);
    }

    public void ReactToCrowding()
    {
        TryReaction(crowdingLines, crowdingReactionChance);
    }

    public void ReactToBeingBulldozed()
    {
        TryReaction(bulldozedLines, 100f);
    }

    public void ReactToPushingPast()
    {
        TryReaction(pushingPastLines, 65f);
    }

    public void ReactToBump()
    {
        SayRandom(bumpLines);
    }

    public void ThinkAboutProduct(string product)
    {
        TryThought(productThoughts, product);
    }

    public void CommentOnBrowsing()
    {
        TryThought(browsingThoughts, null, false, 70f);
    }

    public void CommentOnFinishedBrowsing()
    {
        TryThought(finishedBrowsingThoughts, null, false, 55f);
    }

    public void CommentOnQueue()
    {
        TryThought(queueThoughts, null, false, 60f);
    }

    public void CommentOnFoundProduct(bool stealing)
    {
        TryThought(stealing ? stealingThoughts : foundProductThoughts, null, true);
    }

    private void TryReaction(string[] lines, float chance)
    {
        if (Time.unscaledTime < nextReactionTime || Random.value * 100f >= chance)
            return;
        nextReactionTime = Time.unscaledTime + reactionCooldown;
        SayRandom(lines);
    }

    private void TryThought(string[] lines, string replacement = null,
        bool force = false, float chanceOverride = -1f)
    {
        float chance = chanceOverride >= 0f ? chanceOverride : shoppingThoughtChance;
        if (Time.unscaledTime < nextThoughtTime
            || (!force && Random.value * 100f >= chance)
            || lines == null || lines.Length == 0)
            return;

        string line = lines[Random.Range(0, lines.Length)];
        if (replacement != null)
            line = string.Format(line, replacement);
        nextThoughtTime = Time.unscaledTime + thoughtCooldown;
        Say(line);
    }

    private void Awake()
    {
        capsule = GetComponent<CapsuleCollider>();
    }

    private void OnEnable()
    {
        ActiveSpeakers.Add(this);
    }

    private void OnDisable()
    {
        ActiveSpeakers.Remove(this);
        touchingNpcs.Clear();
    }

    private void Update()
    {
        if (capsule == null)
            return;

        foreach (NpcSpeechBubble other in ActiveSpeakers)
        {
            if (other == null || other == this || other.capsule == null
                || GetInstanceID() > other.GetInstanceID())
                continue;

            int otherId = other.GetInstanceID();
            bool touching = CapsulesTouch(other);
            if (touching && touchingNpcs.Add(otherId))
            {
                other.touchingNpcs.Add(GetInstanceID());
                ReactToBump();
                other.ReactToBump();
            }
            else if (!touching)
            {
                touchingNpcs.Remove(otherId);
                other.touchingNpcs.Remove(GetInstanceID());
            }
        }
    }

    private bool CapsulesTouch(NpcSpeechBubble other)
    {
        Bounds a = capsule.bounds;
        Bounds b = other.capsule.bounds;
        if (a.max.y < b.min.y || b.max.y < a.min.y)
            return false;

        Vector2 delta = new(transform.position.x - other.transform.position.x,
            transform.position.z - other.transform.position.z);
        float radiusA = capsule.radius * Mathf.Max(
            Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));
        float radiusB = other.capsule.radius * Mathf.Max(
            Mathf.Abs(other.transform.lossyScale.x), Mathf.Abs(other.transform.lossyScale.z));
        float contactDistance = radiusA + radiusB + 0.03f;
        return delta.sqrMagnitude <= contactDistance * contactDistance;
    }

    private void OnGUI()
    {
        Camera camera = Camera.main;
        if (!IsSpeaking || camera == null)
            return;
        Vector3 worldPosition = transform.position + Vector3.up * height;
        if ((camera.transform.position - worldPosition).sqrMagnitude
            > maximumDistance * maximumDistance)
            return;
        Vector3 screen = camera.WorldToScreenPoint(worldPosition);
        if (screen.z <= 0f)
            return;
        if (IsHiddenByWorld(camera, worldPosition))
            return;

        EnsureStyles();
        GUIContent content = new(message);
        Vector2 textSize = textStyle.CalcSize(content);
        float width = Mathf.Clamp(textSize.x + 28f, 100f, 300f);
        float heightPixels = textStyle.CalcHeight(content, width - 24f) + 16f;
        Rect bubble = new(screen.x - width * 0.5f,
            Screen.height - screen.y - heightPixels, width, heightPixels);
        GUI.Box(bubble, GUIContent.none, bubbleStyle);
        GUI.Label(new Rect(bubble.x + 12f, bubble.y + 8f,
            bubble.width - 24f, bubble.height - 16f), content, textStyle);
    }

    private bool IsHiddenByWorld(Camera camera, Vector3 worldPosition)
    {
        Vector3 offset = worldPosition - camera.transform.position;
        float distance = offset.magnitude;
        if (distance <= 0.01f)
            return false;

        RaycastHit[] hits = Physics.RaycastAll(camera.transform.position,
            offset / distance, distance - 0.05f, ~0, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            // The character's own head/body must not hide its speech.
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                continue;
            return true;
        }
        return false;
    }

    private void EnsureStyles()
    {
        if (textStyle != null)
            return;
        bubbleTexture = new Texture2D(1, 1);
        bubbleTexture.SetPixel(0, 0, bubbleColour);
        bubbleTexture.Apply();
        bubbleStyle = new GUIStyle(GUI.skin.box);
        bubbleStyle.normal.background = bubbleTexture;
        textStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = fontSize,
            fontStyle = FontStyle.Bold,
            wordWrap = true
        };
        textStyle.normal.textColor = textColour;
    }

    private void OnDestroy()
    {
        if (bubbleTexture != null)
            Destroy(bubbleTexture);
    }
}
