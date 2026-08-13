using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Lets paid shoppers prepare suitable food before leaving.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class CookingStation : MonoBehaviour
{
    [System.Serializable]
    private sealed class CookingPosition
    {
        public Transform standingPosition = null;
        public Transform lookTarget = null;
        [System.NonSerialized] public NpcNavigation user;
    }

    [Header("Use Chance")]
    [SerializeField, Range(0f, 100f)] private float useChance = 75f;
    [SerializeField] private List<string> microwaveProducts = new()
        { "Microwavable Food", "Burgers" };
    [SerializeField] private List<string> hotWaterProducts = new()
        { "Instant Noodle", "Instant Ramen" };

    [Header("Microwaves")]
    [SerializeField] private List<CookingPosition> microwavePositions = new();
    [SerializeField, Min(0f)] private float microwaveWait = 5f;

    [Header("Hot Water")]
    [SerializeField] private List<CookingPosition> hotWaterPositions = new();
    [SerializeField, Min(0f)] private float hotWaterWait = 4f;

    private bool markersVisible;

    public IEnumerator PrepareFood(NpcNavigation npc)
    {
        if (npc == null || Random.value * 100f >= useChance)
            yield break;

        bool needsMicrowave = ContainsAny(npc.WantedProducts, microwaveProducts);
        bool needsHotWater = ContainsAny(npc.WantedProducts, hotWaterProducts);
        if (!needsMicrowave && !needsHotWater)
            yield break;

        if (needsMicrowave)
            yield return UseAvailablePosition(npc, microwavePositions,
                "Waiting for a microwave", "Heating food", microwaveWait);
        if (needsHotWater)
            yield return UseAvailablePosition(npc, hotWaterPositions,
                "Waiting for hot water", "Adding hot water", hotWaterWait);
    }

    private IEnumerator UseAvailablePosition(NpcNavigation npc,
        List<CookingPosition> positions, string waitingAction,
        string usingAction, float waitDuration)
    {
        if (positions == null || positions.Count == 0)
            yield break;

        CookingPosition position = null;
        while (npc != null && position == null)
        {
            foreach (CookingPosition candidate in positions)
            {
                if (candidate != null && candidate.standingPosition != null
                    && candidate.user == null)
                {
                    position = candidate;
                    position.user = npc;
                    break;
                }
            }
            if (position == null)
            {
                npc.SetCheckoutAction(waitingAction);
                yield return new WaitForSeconds(0.25f);
            }
        }
        if (npc == null || position == null)
            yield break;

        npc.SetCheckoutAction(usingAction);
        do
        {
            yield return npc.MoveToCheckoutMarker(
                position.standingPosition, usingAction);
        }
        while (npc != null && !npc.ReachedCheckoutMarker);
        if (npc == null)
        {
            position.user = null;
            yield break;
        }

        Transform target = position.lookTarget != null
            ? position.lookTarget
            : transform;
        yield return npc.FaceForCheckout(GetVisualCentre(target));
        npc.Speak(usingAction == "Heating food"
            ? "Let's warm this up."
            : "Just add hot water...");
        yield return npc.PlayCheckoutAnimation("Grab");
        npc.SetCheckoutAction(usingAction + " - waiting");
        yield return new WaitForSeconds(waitDuration);
        npc.SetCheckoutAction("Taking prepared food");
        yield return npc.PlayCheckoutAnimation("Grab");
        npc.LeaveCheckoutMarker();
        position.user = null;
    }

    private static Vector3 GetVisualCentre(Transform target)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
            return target.position;

        Bounds visibleBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            visibleBounds.Encapsulate(renderers[i].bounds);
        return visibleBounds.center;
    }

    private static bool ContainsAny(IReadOnlyList<string> bought,
        List<string> suitable)
    {
        foreach (string item in bought)
            foreach (string candidate in suitable)
                if (string.Equals(item, candidate,
                    System.StringComparison.OrdinalIgnoreCase))
                    return true;
        return false;
    }

    private void Awake() => SetMarkers(false);
    private void OnEnable() => SetMarkers(false);
    private void OnValidate() => SetMarkers(false);

    private void Update()
    {
        if (!Application.isPlaying
            || markersVisible == DeveloperConsole.ShowInteractionMarkers)
            return;
        SetMarkers(DeveloperConsole.ShowInteractionMarkers);
    }

    private void SetMarkers(bool visible)
    {
        markersVisible = visible;
        SetMarkers(microwavePositions, visible);
        SetMarkers(hotWaterPositions, visible);
    }

    private static void SetMarkers(List<CookingPosition> positions, bool visible)
    {
        if (positions == null) return;
        foreach (CookingPosition position in positions)
        {
            if (position?.standingPosition == null) continue;
            foreach (Renderer renderer in position.standingPosition
                .GetComponentsInChildren<Renderer>(true)) renderer.enabled = visible;
            foreach (Collider collider in position.standingPosition
                .GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }
    }
}
