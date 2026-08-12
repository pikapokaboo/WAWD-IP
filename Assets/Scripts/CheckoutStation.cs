using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Owns a cashier interaction and a first-in, first-out customer queue.</summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class CheckoutStation : MonoBehaviour
{
    [Header("Positions")]
    [SerializeField] private Transform paymentPosition;
    [SerializeField] private List<Transform> queuePositions = new();

    [Header("Cashier")]
    [SerializeField] private Transform cashier;
    [SerializeField] private Animator cashierAnimator;
    [SerializeField] private Transform monitor;
    [SerializeField] private float cashierFacingYawOffset;
    [SerializeField, Min(1f)] private float turnSpeed = 360f;

    [Header("Animations")]
    [Tooltip("The cashier's resting Animator state.")]
    [SerializeField] private string idleState = "Idle";
    [Tooltip("Cashier interaction state. Uses Grab until a separate cashier clip is supplied.")]
    [SerializeField] private string interactState = "Grab";
    [SerializeField, Min(0.1f)] private float interactionDuration = 1.5f;

    [Header("Conversation")]
    [SerializeField] private string[] customerGreetings =
        { "Hi!", "Hello!", "Just these, please.", "How's it going?" };
    [SerializeField] private string[] cashierGreetings =
        { "Hello!", "Welcome!", "Found everything okay?", "Hi there!" };
    [SerializeField] private string[] cashierScanningLines =
        { "Let me scan those.", "One moment...", "That'll be everything?", "Just ringing these up." };
    [SerializeField] private string[] customerGoodbyes =
        { "Thanks!", "Cheers!", "Have a good one!", "See you!" };
    [SerializeField] private string[] cashierGoodbyes =
        { "Thank you!", "Have a nice day!", "See you next time!" };
    [SerializeField] private string[] cashierIdleLines =
        { "Nice and quiet...", "Who's next?", "Another day at the till.", "I could use a break." };
    [SerializeField] private string[] cashierQueueLines =
        { "I'll be with you shortly!", "Thanks for waiting!", "Next, please!", "The queue is moving!" };
    [SerializeField, Range(0f, 100f)] private float funnyExchangeChance = 20f;
    [Tooltip("Keep this list aligned with Cashier Funny Replies.")]
    [SerializeField] private string[] customerFunnyQuestions =
    {
        "Do you accept good vibes?", "Is this the express lane? I ran here.",
        "Can I pay in exposure?", "The shelf made me buy it."
    };
    [Tooltip("Reply at each index answers the customer question at the same index.")]
    [SerializeField] private string[] cashierFunnyReplies =
    {
        "Only alongside actual money.", "That isn't how express lanes work.",
        "The till says no.", "The shelf has excellent taste."
    };
    [SerializeField] private Vector2 cashierRemarkInterval = new(18f, 32f);

    private readonly List<NpcNavigation> customers = new();
    private bool markersVisible;
    private NpcSpeechBubble cashierSpeech;
    private float nextCashierRemarkTime;

    private void Awake()
    {
        HideMarkerObjects();
    }

    private void OnEnable()
    {
        HideMarkerObjects();
    }

    private void OnValidate()
    {
        HideMarkerObjects();
    }

    private void Update()
    {
        if (!Application.isPlaying)
            return;

        if (markersVisible != DeveloperConsole.ShowInteractionMarkers)
        {
            markersVisible = DeveloperConsole.ShowInteractionMarkers;
            SetMarkerVisibility(paymentPosition, markersVisible);
            foreach (Transform position in queuePositions)
                SetMarkerVisibility(position, markersVisible);
        }

        UpdateCashierRemarks();
    }

    private void HideMarkerObjects()
    {
        markersVisible = false;
        SetMarkerVisibility(paymentPosition, false);
        foreach (Transform position in queuePositions)
            SetMarkerVisibility(position, false);
    }

    public IEnumerator Checkout(NpcNavigation customer)
    {
        if (customer == null || paymentPosition == null)
            yield break;

        customers.Add(customer);
        int lastPosition = int.MinValue;

        while (customer != null)
        {
            RemoveMissingCustomers();
            int queueNumber = customers.IndexOf(customer);
            if (queueNumber < 0)
                yield break;

            customer.SetCheckoutQueueNumber(queueNumber + 1);
            if (queueNumber == 0)
            {
                yield return customer.MoveToCheckoutMarker(
                    paymentPosition, "Going to payment position");
                if (!customer.ReachedCheckoutMarker)
                {
                    yield return null;
                    continue;
                }
                yield return RunPayment(customer);
                customers.Remove(customer);
                break;
            }

            int slotIndex = queueNumber - 1;
            if (slotIndex < queuePositions.Count && queuePositions[slotIndex] != null)
            {
                if (lastPosition != slotIndex)
                {
                    yield return customer.MoveToCheckoutMarker(
                        queuePositions[slotIndex], $"Waiting in queue #{queueNumber}");
                    lastPosition = customer.ReachedCheckoutMarker
                        ? slotIndex
                        : int.MinValue;
                    if (customer.ReachedCheckoutMarker)
                        customer.CommentOnCheckoutQueue();
                }
                customer.SetCheckoutAction($"Waiting in queue #{queueNumber}");
                yield return null;
            }
            else
            {
                customer.SetCheckoutAction($"Queue full - browsing (ticket #{queueNumber})");
                yield return customer.BrowseWhileWaitingForCheckout();
                lastPosition = int.MinValue;
            }
        }

        customers.Remove(customer);
        if (customer != null)
        {
            customer.SetCheckoutQueueNumber(0);
            customer.LeaveCheckoutMarker();
        }
    }

    private IEnumerator RunPayment(NpcNavigation customer)
    {
        PrepareCashierSpeech();
        int funnyIndex = -1;
        int funnyPairCount = Mathf.Min(customerFunnyQuestions?.Length ?? 0,
            cashierFunnyReplies?.Length ?? 0);
        if (funnyPairCount > 0 && Random.value * 100f < funnyExchangeChance)
            funnyIndex = Random.Range(0, funnyPairCount);

        customer.SetCheckoutAction("Paying cashier");
        yield return customer.FaceForCheckout(GetCashierLookPosition());
        if (funnyIndex >= 0)
            customer.Speak(customerFunnyQuestions[funnyIndex]);
        else
            customer.SpeakRandom(customerGreetings);
        yield return customer.PlayCheckoutAnimation("Grab");

        yield return TurnCashierTowards(customer.transform.position);
        if (funnyIndex >= 0)
            cashierSpeech?.Say(cashierFunnyReplies[funnyIndex]);
        else
            cashierSpeech?.SayRandom(cashierGreetings);
        yield return PlayCashierInteraction();

        yield return TurnCashierTowards(monitor != null ? monitor.position : transform.position);
        cashierSpeech?.SayRandom(cashierScanningLines);
        yield return PlayCashierInteraction();

        yield return TurnCashierTowards(customer.transform.position);
        cashierSpeech?.SayRandom(cashierGoodbyes);
        yield return PlayCashierInteraction();

        customer.SetCheckoutAction("Finishing payment");
        customer.SpeakRandom(customerGoodbyes);
        yield return customer.PlayCheckoutAnimation("Grab");
        PlayCashierState(idleState);
    }

    private void PrepareCashierSpeech()
    {
        if (cashier == null || cashierSpeech != null || !Application.isPlaying)
            return;
        cashierSpeech = cashier.GetComponent<NpcSpeechBubble>();
        if (cashierSpeech == null)
            cashierSpeech = cashier.gameObject.AddComponent<NpcSpeechBubble>();
    }

    private void UpdateCashierRemarks()
    {
        PrepareCashierSpeech();
        if (cashierSpeech == null || cashierSpeech.IsSpeaking
            || Time.time < nextCashierRemarkTime)
            return;

        cashierSpeech.SayRandom(customers.Count > 1
            ? cashierQueueLines
            : cashierIdleLines);
        float minimum = Mathf.Min(cashierRemarkInterval.x, cashierRemarkInterval.y);
        float maximum = Mathf.Max(cashierRemarkInterval.x, cashierRemarkInterval.y);
        nextCashierRemarkTime = Time.time + Random.Range(minimum, maximum);
    }

    private IEnumerator PlayCashierInteraction()
    {
        PlayCashierState(interactState);
        yield return new WaitForSeconds(interactionDuration);
        PlayCashierState(idleState);
    }

    private void PlayCashierState(string stateName)
    {
        if (cashierAnimator != null && !string.IsNullOrWhiteSpace(stateName))
            cashierAnimator.CrossFadeInFixedTime(stateName, 0.1f, 0, 0f);
    }

    private IEnumerator TurnCashierTowards(Vector3 target)
    {
        if (cashier == null)
            yield break;

        Vector3 direction = target - cashier.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            yield break;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized)
            * Quaternion.Euler(0f, cashierFacingYawOffset, 0f);
        while (Quaternion.Angle(cashier.rotation, targetRotation) > 1f)
        {
            cashier.rotation = Quaternion.RotateTowards(
                cashier.rotation, targetRotation, turnSpeed * Time.deltaTime);
            yield return null;
        }
        cashier.rotation = targetRotation;
    }

    private Vector3 GetCashierLookPosition()
    {
        return cashier != null ? cashier.position + Vector3.up : transform.position;
    }

    private void RemoveMissingCustomers()
    {
        customers.RemoveAll(customer => customer == null);
    }

    private void OnDrawGizmos()
    {
        if (!DeveloperConsole.ShowInteractionMarkers)
            return;

        DrawPosition(paymentPosition, Color.green);
        foreach (Transform position in queuePositions)
            DrawPosition(position, Color.yellow);
    }

    private static void DrawPosition(Transform position, Color colour)
    {
        if (position == null)
            return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColour = Gizmos.color;
        Gizmos.matrix = position.localToWorldMatrix;

        Color fill = colour;
        fill.a = 0.2f;
        Gizmos.color = fill;
        Gizmos.DrawCube(Vector3.zero, Vector3.one);
        colour.a = 1f;
        Gizmos.color = colour;
        Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColour;
    }

    private static void SetMarkerVisibility(Transform marker, bool visible)
    {
        if (marker == null)
            return;
        foreach (Renderer renderer in marker.GetComponentsInChildren<Renderer>(true))
            renderer.enabled = visible;
        foreach (Collider collider in marker.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;
    }
}
