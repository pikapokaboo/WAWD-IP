// -----------------------------------------------------------------------------
// File: NpcSpeechBubble.cs
// Project: WAWD Integrated Studio Project
// Purpose: Displays context-sensitive world-space NPC speech.
// -----------------------------------------------------------------------------

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

    [Header("Shoplifter Thoughts")]
    [SerializeField] private string[] shoplifterSearchThoughts =
    {
        "Where's the {0}? Asking for absolutely no receipt.",
        "Target acquired: {0}. Wallet status: decorative.",
        "I need {0}. Payment is more of a suggestion.",
        "Today's special: five-finger discount on {0}."
    };
    [SerializeField] private string[] shoplifterBrowsingThoughts =
    {
        "Just browsing... very suspiciously.",
        "Where are the cameras? Purely academic question.",
        "Act natural. Nobody acts this natural.",
        "I am an innocent customer with an empty wallet.",
        "Would this fit under my shirt? Hypothetically."
    };
    [SerializeField] private string[] shoplifterFinishedBrowsingThoughts =
    {
        "Too many witnesses. Next aisle.",
        "Abort snackquisition.",
        "Nothing to stealâ€”I mean, buyâ€”here.",
        "That shelf was giving me suspicious looks."
    };

    private string message;
    private float visibleUntil;
    private float nextReactionTime;
    private float nextThoughtTime;
    private readonly HashSet<int> touchingNpcs = new();
    private CapsuleCollider capsule;
    private NpcNavigation navigation;
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
        if (IsShoplifter)
        {
            TryThought(shoplifterSearchThoughts, product);
            return;
        }
        TryThought(GetProductLines(product, ProductMoment.Searching), product);
    }

    public void CommentOnBrowsing(string product)
    {
        if (IsShoplifter)
        {
            TryThought(shoplifterBrowsingThoughts, product, false, 85f);
            return;
        }
        if (string.IsNullOrWhiteSpace(product))
            TryThought(browsingThoughts, null, false, 70f);
        else
            TryThought(GetProductLines(product, ProductMoment.Browsing), product,
                false, 70f);
    }

    public void CommentOnFinishedBrowsing()
    {
        TryThought(IsShoplifter
            ? shoplifterFinishedBrowsingThoughts
            : finishedBrowsingThoughts, null, false, IsShoplifter ? 75f : 55f);
    }

    public void CommentOnQueue()
    {
        TryThought(queueThoughts, null, false, 60f);
    }

    public void CommentOnFoundProduct(string product, bool stealing)
    {
        string[] lines = GetProductLines(product,
            stealing ? ProductMoment.Stealing : ProductMoment.Grabbing);
        TryThought(lines, product, true);
    }

    private enum ProductMoment { Searching, Browsing, Grabbing, Stealing }

    private bool IsShoplifter => navigation != null
        && navigation.HasTrait("No Money");

    private string[] GetProductLines(string product, ProductMoment moment)
    {
        string key = product?.ToLowerInvariant() ?? string.Empty;

        if (key.Contains("milk"))
            return moment switch
            {
                ProductMoment.Searching => new[] { "Where's the {0}? My bones demand it.", "Operation: locate {0}. Moo-ve out." },
                ProductMoment.Browsing => new[] { "Milk has flavours now? What a timeline.", "Do I trust a cow with this much creativity?" },
                ProductMoment.Grabbing => new[] { "{0}: calcium with character.", "This is going straight in my emotional-support cereal." },
                _ => new[] { "Consider this milk... liberated.", "The cow wanted me to have this." }
            };

        if (key.Contains("chip") || key.Contains("snack"))
            return moment switch
            {
                ProductMoment.Searching => new[] { "I can already hear the {0} crunching.", "Where are the emergency {0}?" },
                ProductMoment.Browsing => new[] { "Mostly air, but premium air.", "A balanced diet needs both chip colours." },
                ProductMoment.Grabbing => new[] { "{0}. Dinner is saved.", "Crunch acquired. Dignity optional." },
                _ => new[] { "Silent snack. Extremely loud packet.", "Crime, but make it crunchy." }
            };

        if (key.Contains("noodle") || key.Contains("ramen"))
            return moment switch
            {
                ProductMoment.Searching => new[] { "Tonight we dine on {0} and poor decisions.", "Find {0}. Add water. Become chef." },
                ProductMoment.Browsing => new[] { "Three minutes to cook, three days of sodium.", "The seasoning packet fears no doctor." },
                ProductMoment.Grabbing => new[] { "{0}: cuisine of champions and students.", "My kettle has been training for this." },
                _ => new[] { "Instant noodles, delayed consequences.", "I'll pay the sodium tax later." }
            };

        if (key.Contains("ice cream"))
            return moment switch
            {
                ProductMoment.Searching => new[] { "Where is the {0}? This is medically necessary.", "Emergency dessert search underway." },
                ProductMoment.Browsing => new[] { "It's never too cold for bad decisions.", "I am just checking the emotional-support flavours." },
                ProductMoment.Grabbing => new[] { "{0}! Future me can handle the brain freeze.", "A responsible adult purchase." },
                _ => new[] { "A cold case begins.", "Ice cream has no witnesses." }
            };

        if (key.Contains("burger") || key.Contains("sandwich") || key.Contains("wrap")
            || key.Contains("microwavable"))
            return moment switch
            {
                ProductMoment.Searching => new[] { "Where's the {0}? Cooking is cancelled.", "I need {0}, preferably with no washing up." },
                ProductMoment.Browsing => new[] { "The picture says gourmet. The plastic says otherwise.", "Chef Microwave may approve." },
                ProductMoment.Grabbing => new[] { "{0}: five-star convenience.", "Tonight's chef has a Start button." },
                _ => new[] { "Fast food just got faster.", "No receipt, no recipe." }
            };

        if (key.Contains("water"))
            return moment switch
            {
                ProductMoment.Searching => new[] { "Searching for {0}. Hydration arc begins.", "Water: the original energy drink." },
                ProductMoment.Browsing => new[] { "Vintage: approximately dinosaur-aged.", "Bold flavour. Notes of... water." },
                ProductMoment.Grabbing => new[] { "Hydration acquired.", "My organs are going to love this." },
                _ => new[] { "Hydro-heist complete.", "They can't charge me for clouds." }
            };

        if (key.Contains("soju"))
            return moment switch
            {
                ProductMoment.Searching => new[] { "Where's the {0}? Asking for future me.", "A tiny green bottle of confidence, please." },
                ProductMoment.Browsing => new[] { "This bottle contains tomorrow's headache.", "Responsible decisions are on another shelf." },
                ProductMoment.Grabbing => new[] { "Weekend plans: secured.", "One {0}. What could possibly go wrong?" },
                _ => new[] { "Liquid courage, acquired quietly.", "This plan improves with every bad decision." }
            };

        if (key.Contains("drink") || key.Contains("bepis") || key.Contains("bepsi")
            || key.Contains("conk") || key.Contains("dew") || key.Contains("tea")
            || key.Contains("can"))
            return moment switch
            {
                ProductMoment.Searching => new[] { "I require {0} for maximum refreshment.", "Where is the fizzy motivation?" },
                ProductMoment.Browsing => new[] { "Is this a drink or a side quest?", "The label promises at least three emotions." },
                ProductMoment.Grabbing => new[] { "{0}! Carbonated confidence.", "My dentist just felt a disturbance." },
                _ => new[] { "Stealth level: carbonated.", "If it fizzes, it legally counts as excitement." }
            };

        if (key.Contains("soy"))
            return moment switch
            {
                ProductMoment.Searching => new[] { "Soy where is the {0}?", "Bean-based objective detected." },
                ProductMoment.Browsing => new[] { "A bean with ambition.", "Soy many choices." },
                ProductMoment.Grabbing => new[] { "{0}. Soy far, soy good.", "The beans have chosen me." },
                _ => new[] { "Soy long, shelf.", "Bean there, stole that." }
            };

        return moment switch
        {
            ProductMoment.Searching => productThoughts,
            ProductMoment.Browsing => new[] { "Do I need {0}? My heart says yes. My list says nothing.", "Inspecting {0} for scientific reasons." },
            ProductMoment.Grabbing => new[] { "{0}, welcome to the team.", "Yoinkâ€”legally, after checkout.", "A fine addition to my pile of decisions." },
            _ => new[] { "{0} has mysteriously changed ownership.", "This is between me and the security camera.", "Yoink, but quietly." }
        };
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
        navigation = GetComponent<NpcNavigation>();
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
        Camera camera = CctvSystem.GetGameplayCamera();
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
