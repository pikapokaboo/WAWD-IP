// -----------------------------------------------------------------------------
// File: CctvSystem.cs
// Project: WAWD Integrated Studio Project
// Purpose: Controls CCTV views, NPC reporting, and feedback.
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CctvSystem : MonoBehaviour
{
    public static bool IsActive { get; private set; }
    public static Camera ActiveCamera => IsActive && instance != null
        && instance.playerCamera != null
        ? instance.playerCamera
        : null;

    public static Camera GetGameplayCamera()
    {
        Camera cctvCamera = ActiveCamera;
        return cctvCamera != null ? cctvCamera : Camera.main;
    }
    private static CctvSystem instance;

    private readonly List<Camera> cameras = new();
    private Camera playerCamera;
    private PlayerController player;
    private DayNightCycle dayCycle;
    private Transform originalCameraParent;
    private Vector3 originalCameraLocalPosition;
    private Quaternion originalCameraLocalRotation;
    private int cameraIndex;
    private NpcTraits hoveredNpc;
    private Outline hoveredOutline;
    private bool reporting;
    private string resultMessage;
    private float fadeAlpha;
    private Texture2D solidTexture;
    private GUIStyle hudStyle;
    private GUIStyle centreStyle;
    private bool enteringCctv;

    public static void EnterFromWorkstation()
    {
        if (instance == null)
        {
            GameObject host = new("CCTV System");
            instance = host.AddComponent<CctvSystem>();
        }
        if (!instance.enteringCctv && !IsActive)
            instance.StartCoroutine(instance.EnterSequence());
    }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        player = FindFirstObjectByType<PlayerController>();
        playerCamera = player != null ? player.GetComponentInChildren<Camera>() : Camera.main;
        if (playerCamera != null)
        {
            originalCameraParent = playerCamera.transform.parent;
            originalCameraLocalPosition = playerCamera.transform.localPosition;
            originalCameraLocalRotation = playerCamera.transform.localRotation;
        }
        dayCycle = FindFirstObjectByType<DayNightCycle>();
        RefreshCameraList();
    }

    private IEnumerator EnterSequence()
    {
        enteringCctv = true;
        fadeAlpha = 0f;
        RefreshCameraList();
        if (cameras.Count == 0)
        {
            Debug.LogError("CCTV mode could not find any Camera_View cameras.", this);
            enteringCctv = false;
            yield break;
        }

        player?.SetInteractionLocked(true);
        HideCursorForBlackScreen();
        yield return Fade(0f, 1f, 0.5f);
        IsActive = true;
        SelectCamera(0);
        yield return null;
        yield return Fade(1f, 0f, 0.5f);
        fadeAlpha = 0f;
        enteringCctv = false;
        RestoreCctvCursor();
    }

    private void RefreshCameraList()
    {
        cameras.Clear();
        foreach (Camera camera in Resources.FindObjectsOfTypeAll<Camera>())
        {
            if (camera == null || camera.gameObject.name != "Camera_View"
                || !camera.gameObject.scene.IsValid())
                continue;
            camera.gameObject.SetActive(true);
            camera.targetTexture = null;
            camera.targetDisplay = 0;
            cameras.Add(camera);
        }
    }

    private void Update()
    {
        if (!IsActive || cameras.Count == 0) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = fadeAlpha <= 0f;
        if (DeveloperConsole.AnyConsoleOpen)
        {
            ClearHover();
            return;
        }
        if (Keyboard.current != null)
        {
            if (Keyboard.current.rightArrowKey.wasPressedThisFrame
                || Keyboard.current.dKey.wasPressedThisFrame) SelectCamera(cameraIndex + 1);
            if (Keyboard.current.leftArrowKey.wasPressedThisFrame
                || Keyboard.current.aKey.wasPressedThisFrame) SelectCamera(cameraIndex - 1);
        }
        UpdateNpcHover();
        if (!reporting && hoveredNpc != null && Mouse.current != null
            && Mouse.current.leftButton.wasPressedThisFrame)
            StartCoroutine(ReportNpc(hoveredNpc));
    }

    private void SelectCamera(int index)
    {
        if (cameras.Count == 0 || playerCamera == null) return;
        cameraIndex = (index % cameras.Count + cameras.Count) % cameras.Count;
        for (int i = 0; i < cameras.Count; i++)
        {
            if (cameras[i] == null) continue;
            // Camera_View components act as view markers. Keeping them disabled
            // prevents competing render cameras and permanent black frames.
            cameras[i].enabled = false;
        }
        Transform marker = cameras[cameraIndex].transform;
        playerCamera.transform.SetParent(null, true);
        playerCamera.transform.SetPositionAndRotation(marker.position, marker.rotation);
        playerCamera.enabled = true;
        ClearHover();
    }

    private void UpdateNpcHover()
    {
        Camera active = playerCamera;
        Ray ray = active.ScreenPointToRay(Mouse.current.position.ReadValue());
        NpcTraits npc = Physics.Raycast(ray, out RaycastHit hit, 300f,
                Physics.AllLayers, QueryTriggerInteraction.Ignore)
            ? hit.collider.GetComponentInParent<NpcTraits>() : null;
        if (npc == hoveredNpc) return;
        ClearHover();
        hoveredNpc = npc;
        if (hoveredNpc == null) return;
        hoveredOutline = hoveredNpc.GetComponent<Outline>();
        if (hoveredOutline == null) hoveredOutline = hoveredNpc.gameObject.AddComponent<Outline>();
        hoveredOutline.OutlineMode = Outline.Mode.OutlineVisible;
        hoveredOutline.OutlineColor = new Color(0.15f, 1f, 0.35f);
        hoveredOutline.OutlineWidth = 4f;
        hoveredOutline.enabled = true;
    }

    private void ClearHover()
    {
        if (hoveredOutline != null) hoveredOutline.enabled = false;
        hoveredOutline = null;
        hoveredNpc = null;
    }

    private IEnumerator ReportNpc(NpcTraits npc)
    {
        reporting = true;
        yield return new WaitForSecondsRealtime(2.5f);
        bool shoplifter = npc != null && npc.HasTrait("No Money");
        resultMessage = shoplifter ? "REMOVING SHOPLIFTER" : "NO SHOP THEFT DETECTED";
        HideCursorForBlackScreen();
        yield return Fade(0f, 1f, 0.25f);
        float sirenDuration = shoplifter
            ? DayNightCycle.Instance?.PlayShoplifterRemovalSound() ?? 0f
            : 0f;
        if (shoplifter && npc != null)
        {
            DayNightCycle.Instance?.ReportCaughtShoplifter();
            NpcNavigation navigation = npc.GetComponent<NpcNavigation>();
            navigation?.ReleaseAllOccupancy();
            Destroy(npc.gameObject);
            yield return null;
            NpcAutomaticDoor.RefreshAllAfterNpcRemoval();
        }
        yield return new WaitForSecondsRealtime(Mathf.Max(1f, sirenDuration));
        resultMessage = null;
        yield return Fade(1f, 0f, 0.35f);
        reporting = false;
        RestoreCctvCursor();
    }

    public static void ExitForDayEnd()
    {
        if (instance == null || !IsActive) return;
        instance.ClearHover();
        foreach (Camera camera in instance.cameras) camera.enabled = false;
        if (instance.playerCamera != null)
        {
            instance.playerCamera.transform.SetParent(instance.originalCameraParent, false);
            instance.playerCamera.transform.localPosition = instance.originalCameraLocalPosition;
            instance.playerCamera.transform.localRotation = instance.originalCameraLocalRotation;
            instance.playerCamera.enabled = true;
        }
        IsActive = false;
        instance.reporting = false;
        instance.resultMessage = null;
        instance.fadeAlpha = 0f;
        instance.enteringCctv = false;
        instance.player?.SetInteractionLocked(false);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float start = Time.unscaledTime;
        do
        {
            fadeAlpha = Mathf.Lerp(from, to,
                Mathf.Clamp01((Time.unscaledTime - start) / Mathf.Max(0.01f, duration)));
            yield return null;
        } while (Time.unscaledTime - start < duration);
        fadeAlpha = to;
    }

    private static void HideCursorForBlackScreen()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
    }

    private static void RestoreCctvCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnGUI()
    {
        if (!IsActive && fadeAlpha <= 0f) return;
        EnsureStyles();
        if (IsActive)
        {
            DrawCctvBorder();
            GUI.Label(new Rect(20f, 18f, 300f, 38f),
                $"â— REC   CAM {cameraIndex + 1:00}", hudStyle);
            string time = dayCycle != null ? dayCycle.CurrentTimeText : "--:--";
            string day = dayCycle != null ? $"DAY {dayCycle.CurrentDay}" : "DAY --";
            GUI.Label(new Rect(Screen.width - 300f, 18f, 275f, 38f),
                $"{day}   {time}", hudStyle);
            GUI.Label(new Rect(20f, Screen.height - 62f, 470f, 38f),
                reporting ? "REPORTING... SYSTEM TEMPORARILY LOCKED"
                    : "LMB REPORT   A/D OR â†/â†’ CHANGE CAMERA", hudStyle);
        }
        if (fadeAlpha > 0f)
        {
            GUI.color = new Color(0f, 0f, 0f, fadeAlpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), solidTexture);
            GUI.color = new Color(1f, 1f, 1f, fadeAlpha);
            if (!string.IsNullOrEmpty(resultMessage))
                GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), resultMessage, centreStyle);
            GUI.color = Color.white;
        }
    }

    private void DrawCctvBorder()
    {
        Color previousColour = GUI.color;
        GUI.color = new Color(0.35f, 1f, 0.48f, 0.8f);
        const float edge = 3f;
        GUI.DrawTexture(new Rect(8f, 8f, Screen.width - 16f, edge), solidTexture);
        GUI.DrawTexture(new Rect(8f, Screen.height - 11f,
            Screen.width - 16f, edge), solidTexture);
        GUI.DrawTexture(new Rect(8f, 8f, edge, Screen.height - 16f), solidTexture);
        GUI.DrawTexture(new Rect(Screen.width - 11f, 8f,
            edge, Screen.height - 16f), solidTexture);
        GUI.color = previousColour;
    }

    private void EnsureStyles()
    {
        if (solidTexture == null)
        {
            solidTexture = new Texture2D(1, 1);
            solidTexture.SetPixel(0, 0, Color.white); solidTexture.Apply();
        }
        hudStyle ??= new GUIStyle(GUI.skin.box)
        { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        hudStyle.normal.textColor = new Color(0.55f, 1f, 0.65f);
        centreStyle ??= new GUIStyle(GUI.skin.label)
        { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        centreStyle.normal.textColor = Color.white;
    }
}
