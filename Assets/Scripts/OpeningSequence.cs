using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class OpeningSequence : MonoBehaviour
{
    public enum OpeningStage { TalkToCashier, GoToWorkstation, DayStarted }

    public static OpeningSequence Instance { get; private set; }
    public OpeningStage Stage { get; private set; }
    public bool NeedsCashierBriefing => Stage == OpeningStage.TalkToCashier;

    private DayNightCycle dayCycle;
    private GUIStyle objectiveStyle;
    private Texture2D objectiveTexture;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateForScene()
    {
        if (SceneManager.GetActiveScene().name == "Home_Screen")
            return;

        DayNightCycle cycle = FindFirstObjectByType<DayNightCycle>();
        if (cycle == null || FindFirstObjectByType<OpeningSequence>() != null)
            return;
        GameObject host = new("Opening Sequence");
        host.AddComponent<OpeningSequence>().dayCycle = cycle;
    }

    private void Awake()
    {
        Instance = this;
        Stage = OpeningStage.TalkToCashier;
    }

    public void FinishCashierBriefing()
    {
        if (Stage == OpeningStage.TalkToCashier)
            Stage = OpeningStage.GoToWorkstation;
    }

    public void StartDayFromWorkstation()
    {
        if (Stage != OpeningStage.GoToWorkstation)
            return;
        Stage = OpeningStage.DayStarted;
        dayCycle ??= FindFirstObjectByType<DayNightCycle>();
        dayCycle?.BeginOpeningDay();
        CctvSystem.EnterFromWorkstation();
    }

    public void PrepareForNextDay()
    {
        Stage = OpeningStage.GoToWorkstation;
    }

    private void OnGUI()
    {
        if (Stage == OpeningStage.DayStarted)
            return;
        EnsureStyle();
        string objective = Stage == OpeningStage.TalkToCashier
            ? "OBJECTIVE: Talk to the cashier"
            : "OBJECTIVE: Go to the security room and use the workstation";
        GUI.Label(new Rect(24f, 24f, 530f, 46f), objective, objectiveStyle);
    }

    private void EnsureStyle()
    {
        if (objectiveStyle != null) return;
        objectiveTexture = new Texture2D(1, 1);
        objectiveTexture.SetPixel(0, 0, new Color(0.025f, 0.03f, 0.045f, 0.78f));
        objectiveTexture.Apply();
        objectiveStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(16, 10, 8, 8)
        };
        objectiveStyle.normal.background = objectiveTexture;
        objectiveStyle.normal.textColor = Color.white;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (objectiveTexture != null) Destroy(objectiveTexture);
    }
}
