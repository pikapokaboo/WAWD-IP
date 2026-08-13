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
    [SerializeField] private Transform counterCupboard;
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

    [Header("Cigarette Purchase")]
    [SerializeField, Range(0f, 100f)] private float childCigaretteChance;
    [SerializeField, Range(0f, 100f)] private float teenCigaretteChance = 4f;
    [SerializeField, Range(0f, 100f)] private float youngAdultCigaretteChance = 22f;
    [SerializeField, Range(0f, 100f)] private float adultCigaretteChance = 18f;
    [SerializeField, Range(0f, 100f)] private float elderlyCigaretteChance = 7f;
    [SerializeField] private string[] cigaretteRequests =
        { "A pack of cigarettes too, please.", "Could I get a cigarette pack?", "And cigarettes, please." };
    [SerializeField] private string[] ageCheckLines =
        { "How old are you?", "Can I check your age?", "I'll need to verify your age." };
    [SerializeField] private string[] teenAgeReplies =
        { "I'm seventeen.", "Uh... old enough?", "Does almost eighteen count?" };
    [SerializeField] private string[] youngAdultAgeReplies =
        { "I'm twenty-one.", "Twenty-three.", "I'm over eighteen." };
    [SerializeField] private string[] adultAgeReplies =
        { "I'm an adult.", "Thirty-two.", "Definitely over eighteen." };
    [SerializeField] private string[] elderlyAgeReplies =
        { "Old enough to remember cheaper prices.", "Do I really look under eighteen?", "Seventy, dear." };
    [SerializeField] private string[] cigaretteCostLines =
        { "That'll be $12 extra.", "The pack is $12.", "That comes to $12 more." };
    [SerializeField] private string[] teenRefusalLines =
        { "Nice try. You're too young.", "I'll need an adult, sorry.", "Absolutely not, kid." };

    private readonly List<NpcNavigation> customers = new();
    private NpcNavigation paymentCustomer;
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

        // With an entirely empty checkout, approaching shoppers race for the
        // payment position. Tickets only begin once somebody has claimed it.
        if (paymentCustomer == null && customers.Count == 0)
        {
            customer.SetCheckoutQueueNumber(0);
            while (customer != null && paymentCustomer == null
                   && customers.Count == 0)
            {
                yield return customer.MoveToCheckoutMarker(
                    paymentPosition, "Going to available checkout");
                if (!customer.ReachedCheckoutMarker)
                    continue;

                if (paymentCustomer == null && customers.Count == 0)
                {
                    paymentCustomer = customer;
                    yield return RunPayment(customer);
                    paymentCustomer = null;
                    customer.SetCheckoutQueueNumber(0);
                    customer.LeaveCheckoutMarker();
                    yield break;
                }
            }
            customer?.LeaveCheckoutMarker();
        }

        if (customer == null)
            yield break;
        if (!customers.Contains(customer))
            customers.Add(customer);
        int lastPosition = int.MinValue;

        while (customer != null)
        {
            RemoveMissingCustomers();
            int queueNumber = customers.IndexOf(customer);
            if (queueNumber < 0)
                yield break;

            customer.SetCheckoutQueueNumber(queueNumber + 1);
            if (queueNumber == 0 && paymentCustomer == null)
            {
                customers.RemoveAt(0);
                paymentCustomer = customer;
                customer.SetCheckoutQueueNumber(0);
                do
                {
                    yield return customer.MoveToCheckoutMarker(
                        paymentPosition, "Going to payment position");
                }
                while (customer != null && !customer.ReachedCheckoutMarker);
                if (customer == null)
                {
                    paymentCustomer = null;
                    yield break;
                }
                yield return RunPayment(customer);
                paymentCustomer = null;
                break;
            }

            int slotIndex = queueNumber;
            if (slotIndex < queuePositions.Count && queuePositions[slotIndex] != null)
            {
                if (lastPosition != slotIndex)
                {
                    yield return customer.MoveToCheckoutMarker(
                        queuePositions[slotIndex], $"Waiting in queue #{queueNumber + 1}");
                    lastPosition = customer.ReachedCheckoutMarker
                        ? slotIndex
                        : int.MinValue;
                    if (customer.ReachedCheckoutMarker)
                        customer.CommentOnCheckoutQueue();
                }
                customer.SetCheckoutAction($"Waiting in queue #{queueNumber + 1}");
                yield return null;
            }
            else
            {
                customer.SetCheckoutAction($"Queue full - browsing (ticket #{queueNumber + 1})");
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
        bool wantsCigarettes = Random.value * 100f < GetCigaretteChance(customer);
        bool isTeen = customer.HasTrait("Teen");
        bool isOfAge = !customer.HasTrait("Child") && !isTeen;

        if (wantsCigarettes && isOfAge)
        {
            yield return RunCigarettePayment(customer);
            yield break;
        }

        if (wantsCigarettes && isTeen)
        {
            customer.SetCheckoutAction("Trying to buy cigarettes");
            yield return customer.FaceForCheckout(GetCashierLookPosition());
            customer.SpeakRandom(cigaretteRequests);
            yield return customer.PlayCheckoutAnimation("Grab");
            yield return TurnCashierTowards(customer.transform.position);
            cashierSpeech?.SayRandom(ageCheckLines);
            yield return PlayCashierInteraction();
            customer.SpeakRandom(teenAgeReplies);
            yield return customer.PlayCheckoutAnimation("Grab");
            cashierSpeech?.SayRandom(teenRefusalLines);
            yield return PlayCashierInteraction();
        }

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

    private IEnumerator RunAgeCheck(NpcNavigation customer)
    {
        yield return TurnCashierTowards(customer.transform.position);
        cashierSpeech?.SayRandom(ageCheckLines);
        yield return PlayCashierInteraction();

        if (customer.HasTrait("Young Adult"))
            customer.SpeakRandom(youngAdultAgeReplies);
        else if (customer.HasTrait("Elderly"))
            customer.SpeakRandom(elderlyAgeReplies);
        else
            customer.SpeakRandom(adultAgeReplies);
        yield return customer.PlayCheckoutAnimation("Grab");
    }

    private IEnumerator RunCigarettePayment(NpcNavigation customer)
    {
        customer.SetCheckoutAction("Buying cigarettes");
        yield return customer.FaceForCheckout(GetCashierLookPosition());

        // Place the normal shopping on the counter, then make the request.
        yield return customer.PlayCheckoutAnimation("Grab");
        customer.SpeakRandom(cigaretteRequests);
        yield return RunAgeCheck(customer);

        yield return TurnCashierTowards(counterCupboard != null
            ? counterCupboard.position
            : transform.position);
        cashierSpeech?.Say("One moment.");
        yield return PlayCashierInteraction();

        yield return TurnCashierTowards(customer.transform.position);
        yield return PlayCashierInteraction();
        cashierSpeech?.SayRandom(cigaretteCostLines);

        customer.SetCheckoutAction("Paying for cigarettes");
        yield return customer.PlayCheckoutAnimation("Grab");
        yield return PlayCashierInteraction();

        yield return TurnCashierTowards(monitor != null
            ? monitor.position
            : transform.position);
        yield return PlayCashierInteraction();

        yield return TurnCashierTowards(customer.transform.position);
        cashierSpeech?.SayRandom(cashierGoodbyes);
        yield return PlayCashierInteraction();
        PlayCashierState(idleState);
    }

    private float GetCigaretteChance(NpcNavigation customer)
    {
        if (customer.HasTrait("Child")) return childCigaretteChance;
        if (customer.HasTrait("Teen")) return teenCigaretteChance;
        if (customer.HasTrait("Young Adult")) return youngAdultCigaretteChance;
        if (customer.HasTrait("Elderly")) return elderlyCigaretteChance;
        return adultCigaretteChance;
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
