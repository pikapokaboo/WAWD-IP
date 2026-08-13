// -----------------------------------------------------------------------------
// File: NpcNavigation.cs
// Project: WAWD Integrated Studio Project
// Purpose: Runs the NPC navigation and interaction state flow.
// -----------------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Runs the NPC's simple shopping journey. Traits remain data-only; this component
/// reads their names and decides how to move and interact.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent), typeof(NpcTraits))]
public sealed class NpcNavigation : MonoBehaviour
{
    [Header("Route")]
    [Tooltip("Destination used when the NPC leaves. If empty, the scene's despawning pad is used.")]
    [SerializeField] private Transform homeTarget;
    [Tooltip("Chance that an NPC shops instead of going directly home.")]
    [SerializeField, Range(0f, 100f)] private float enterStoreChance = 50f;
    [SerializeField, Min(1)] private int maximumConcurrentShoppers = 6;

    [Header("Trait Speeds")]
    [SerializeField, Min(0.1f)] private float slowSpeed = 2f;
    [SerializeField, Min(0.1f)] private float normalSpeed = 3.5f;
    [SerializeField, Min(0.1f)] private float fastSpeed = 5f;

    [Header("Interaction")]
    [SerializeField, Min(0.02f)] private float arrivalDistance = 0.08f;
    [SerializeField, Min(1f)] private float turnSpeed = 360f;
    [Tooltip("Corrects models whose visible forward direction differs from Unity's blue Z axis.")]
    [SerializeField] private float shelfFacingYawOffset = 90f;
    [Tooltip("Model correction when matching a checkout marker's red X arrow.")]
    [SerializeField] private float checkoutMarkerYawOffset;
    [SerializeField, Min(0f)] private float lookDuration = 1.5f;
    [SerializeField, Min(0f)] private float grabDuration = 1.5f;
    [Tooltip("Temporary right-of-way used while an NPC is stationary at a shelf. Lower is stronger.")]
    [SerializeField, Range(0, 99)] private int shelfInteractionPriority;

    [Header("Browsing")]
    [SerializeField, Range(0f, 100f)] private float urgentBrowseChance = 15f;
    [SerializeField, Range(0f, 100f)] private float casualBrowseChance = 65f;
    [SerializeField, Range(0f, 100f)] private float defaultBrowseChance = 35f;
    [SerializeField] private Vector2 browseDurationRange = new(2.5f, 5f);

    [Header("Eating")]
    [Tooltip("Chance that an NPC with food chooses an available chair after shopping.")]
    [SerializeField, Range(0f, 100f)] private float eatInStoreChance = 35f;
    [Tooltip("Higher sitting chance when the NPC has food for the microwave or hot-water station.")]
    [SerializeField, Range(0f, 100f)] private float heatedFoodEatChance = 70f;

    [Header("Traffic Avoidance")]
    [Tooltip("Navigation-only personal space. Larger values make NPCs steer away sooner without enlarging their collider.")]
    [SerializeField, Min(0.1f)] private float personalSpaceRadius = 1f;
    [Tooltip("Urgent Shopper priority range. In Unity, lower numbers have right of way.")]
    [SerializeField] private Vector2Int urgentPriorityRange = new(15, 35);
    [Tooltip("Casual Shopper priority range. Higher numbers yield more readily.")]
    [SerializeField] private Vector2Int casualPriorityRange = new(55, 75);
    [Tooltip("How long an NPC can make almost no progress before briefly stepping aside.")]
    [SerializeField, Min(0.25f)] private float stuckCheckTime = 1.25f;
    [SerializeField, Min(0.1f)] private float minimumProgress = 0.12f;
    [SerializeField, Min(0.2f)] private float sidestepDistance = 0.9f;
    [Tooltip("Minimum delay before this NPC may perform another make-room manoeuvre.")]
    [SerializeField, Min(0f)] private float makeRoomCooldown = 3f;
    [SerializeField, Min(0.25f)] private float overtakeDetectionDistance = 2.25f;
    [SerializeField, Min(0.1f)] private float overtakeCheckInterval = 0.5f;
    [SerializeField, Min(0.25f)] private float clearanceProbeDistance = 1.5f;

    private NavMeshAgent agent;
    private NpcTraits traits;
    private Animator animator;
    private NpcSpeechBubble speech;
    private NpcSitting sitting;
    private readonly List<ShelfStation> shoppingRoute = new();
    private readonly List<string> wantedProducts = new();
    private static int activeShopperCount;
    private static readonly HashSet<NpcNavigation> ActiveNpcs = new();
    private bool holdsShoppingSlot;
    private float nextMakeRoomAllowedTime;
    private bool visuallyAtCheckoutMarker;
    private bool? forcedShoppingIntent;

    public IReadOnlyList<ShelfStation> ShoppingRoute => shoppingRoute;
    public IReadOnlyList<string> WantedProducts => wantedProducts;
    public string CurrentAction { get; private set; } = "Starting";
    public int AvoidancePriority => agent != null ? agent.avoidancePriority : -1;
    public static int ActiveShopperCount => activeShopperCount;
    public int CheckoutQueueNumber { get; private set; }
    public bool ReachedCheckoutMarker { get; private set; }

    public bool HasTrait(string traitName) =>
        traits != null && traits.HasTrait(traitName);

    public void ForceShoppingIntent(bool shouldShop) =>
        forcedShoppingIntent = shouldShop;

    public void SetHomeTarget(Transform target) => homeTarget = target;

    public void ReleaseAllOccupancy()
    {
        ShelfStation.ReleaseAllFor(this);
        CheckoutStation.ReleaseCustomerFromAll(this);
        CookingStation.ReleaseUserFromAll(this);
        NpcSitting npcSitting = sitting != null ? sitting : GetComponent<NpcSitting>();
        if (npcSitting != null) npcSitting.ReleaseReservation();
        ReleaseShoppingSlot();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetShopperCount()
    {
        activeShopperCount = 0;
        ActiveNpcs.Clear();
    }

    private IEnumerator Start()
    {
        agent = GetComponent<NavMeshAgent>();
        traits = GetComponent<NpcTraits>();
        animator = GetComponentInChildren<Animator>();
        speech = GetComponent<NpcSpeechBubble>();
        sitting = GetComponent<NpcSitting>();

        CapsuleCollider bodyCollider = GetComponent<CapsuleCollider>();
        agent.radius = bodyCollider != null ? bodyCollider.radius : personalSpaceRadius;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;

        // Start runs after Awake, where NpcTraits normally rolls its result.
        if (!traits.HasRolled)
            traits.RollTraits();

        ApplyMovementTrait();
        ApplyAvoidanceTrait();
        FindHomeTarget();

        bool rolledShopping = forcedShoppingIntent
            ?? Random.value * 100f < enterStoreChance;
        bool receivedShoppingSlot = rolledShopping && TryTakeShoppingSlot();
        ChairStation eatingChair = null;
        if (receivedShoppingSlot)
        {
            BuildShoppingRoute();
            for (int i = 0; i < shoppingRoute.Count; i++)
            {
                PreferAvailableProduct(i);
                speech?.ThinkAboutProduct(wantedProducts[i]);
                yield return MaybeBrowse(shoppingRoute[i]);
                // The shelf may have become occupied while this NPC browsed.
                PreferAvailableProduct(i);
                yield return VisitShelf(shoppingRoute[i], wantedProducts[i]);
            }
            bool hasFood = wantedProducts.Count > 0;
            CookingStation cooking = FindFirstObjectByType<CookingStation>();
            if (hasFood)
            {
                bool needsHeating = cooking != null
                    && cooking.HasFoodNeedingPreparation(this);
                eatingChair = ReserveEatingChair(needsHeating
                    ? heatedFoodEatChance
                    : eatInStoreChance);
            }

            if (!traits.HasTrait("No Money") && hasFood)
            {
                CheckoutStation checkout = FindFirstObjectByType<CheckoutStation>();
                if (checkout != null)
                {
                    yield return checkout.Checkout(this);
                    if (cooking != null)
                        yield return cooking.PrepareFood(this, eatingChair != null);
                }
            }
            else if (traits.HasTrait("No Money") && hasFood && cooking != null)
            {
                yield return cooking.PrepareFood(this, eatingChair != null);
            }
        }

        // Shopping includes collecting, queueing, and paying. The slot is only
        // freed once this NPC actually changes state to going home.
        ReleaseShoppingSlot();
        if (eatingChair != null)
            yield return EatAtChair(eatingChair);
        yield return GoHome(rolledShopping && !receivedShoppingSlot
            ? "Store full - going home"
            : "Going home");
    }

    private void ApplyMovementTrait()
    {
        if (traits.HasTrait("Slow Walker"))
            agent.speed = slowSpeed;
        else if (traits.HasTrait("Fast Walker"))
            agent.speed = fastSpeed;
        else
            agent.speed = normalSpeed;
    }

    private void BuildShoppingRoute()
    {
        shoppingRoute.Clear();
        wantedProducts.Clear();

        int wantedCount = traits.HasTrait("Heavy Spender")
            ? Random.Range(3, 6)
            : Random.Range(1, 4);

        List<ProductLocation> available = new();
        foreach (ShelfStation shelf in ShelfStation.AllActive)
        {
            if (shelf == null)
                continue;

            foreach (string product in shelf.Products)
            {
                if (!string.IsNullOrWhiteSpace(product))
                    available.Add(new ProductLocation(shelf, product.Trim()));
            }
        }

        Shuffle(available);
        HashSet<string> selectedNames = new(System.StringComparer.OrdinalIgnoreCase);
        foreach (ProductLocation location in available)
        {
            if (!selectedNames.Add(location.Product))
                continue;

            shoppingRoute.Add(location.Shelf);
            wantedProducts.Add(location.Product);
            if (shoppingRoute.Count >= wantedCount)
                break;
        }
    }

    private void PreferAvailableProduct(int currentIndex)
    {
        if (currentIndex < 0 || currentIndex >= shoppingRoute.Count)
            return;

        ShelfStation currentShelf = shoppingRoute[currentIndex];
        if (currentShelf == null || !currentShelf.HasApproachingShopper)
            return;

        for (int i = currentIndex + 1; i < shoppingRoute.Count; i++)
        {
            ShelfStation alternative = shoppingRoute[i];
            if (alternative == null || alternative.HasApproachingShopper
                || alternative == currentShelf)
                continue;

            (shoppingRoute[currentIndex], shoppingRoute[i]) =
                (shoppingRoute[i], shoppingRoute[currentIndex]);
            (wantedProducts[currentIndex], wantedProducts[i]) =
                (wantedProducts[i], wantedProducts[currentIndex]);
            return;
        }
    }

    private IEnumerator VisitShelf(ShelfStation shelf, string product)
    {
        if (shelf == null)
            yield break;

        shelf = shelf.FindAvailableSharedPosition(this);
        while (shelf != null && !shelf.TryReserve(this))
        {
            CurrentAction = $"Waiting for {product}";
            yield return new WaitForSeconds(0.25f);
            if (shelf != null)
                shelf = shelf.FindAvailableSharedPosition(this);
        }
        if (shelf == null)
            yield break;
        CurrentAction = $"Going to {product}";
        yield return MoveTo(shelf.StandPosition);
        if (shelf == null || !agent.isOnNavMesh)
        {
            if (shelf != null)
                shelf.Release(this);
            yield break;
        }

        agent.isStopped = true;
        agent.updateRotation = false;
        shelf.BeginInteraction();
        int normalPriority = agent.avoidancePriority;
        agent.avoidancePriority = shelfInteractionPriority;
        SetWalking(false);
        CurrentAction = $"Looking at {product}";
        yield return Face(shelf.LookPosition, shelf.FacingYawOffset);

        if (traits.HasTrait("No Money"))
        {
            speech?.CommentOnFoundProduct(product, true);
            yield return PlayAnimationToCompletion("Look", lookDuration + 5f);
        }
        else
        {
            speech?.CommentOnFoundProduct(product, false);
        }

        CurrentAction = $"Grabbing {product}";
        PlayAnimation(string.IsNullOrWhiteSpace(shelf.InteractionTrigger)
            ? "Grab"
            : shelf.InteractionTrigger);
        yield return new WaitForSeconds(grabDuration);

        if (agent.isOnNavMesh)
        {
            agent.avoidancePriority = normalPriority;
            agent.updateRotation = true;
            agent.isStopped = false;
        }
        shelf.Release(this);
        shelf.EndInteraction();
    }

    private IEnumerator MaybeBrowse(ShelfStation wantedShelf, bool force = false)
    {
        float chance = traits.HasTrait("Urgent Shopper")
            ? urgentBrowseChance
            : traits.HasTrait("Casual Shopper")
                ? casualBrowseChance
                : defaultBrowseChance;

        if (!force && Random.value * 100f >= chance)
            yield break;

        List<ShelfStation> choices = new();
        foreach (ShelfStation shelf in ShelfStation.AllActive)
        {
            if (shelf != null && shelf != wantedShelf && !shelf.HasApproachingShopper)
                choices.Add(shelf);
        }
        if (choices.Count == 0)
            yield break;

        ShelfStation browseShelf = choices[Random.Range(0, choices.Count)];
        if (!browseShelf.TryReserve(this))
            yield break;
        int normalPriority = agent.avoidancePriority;
        agent.avoidancePriority = 90;
        CurrentAction = $"Browsing {browseShelf.name}";
        string browsedProduct = browseShelf.Products.Count > 0
            ? browseShelf.Products[Random.Range(0, browseShelf.Products.Count)]
            : null;
        speech?.CommentOnBrowsing(browsedProduct);
        yield return MoveTo(browseShelf.StandPosition);

        if (browseShelf == null || browseShelf.HasApproachingShopper || !agent.isOnNavMesh)
        {
            browseShelf.Release(this);
            agent.avoidancePriority = normalPriority;
            yield break;
        }

        agent.isStopped = true;
        agent.updateRotation = false;
        browseShelf.BeginInteraction();
        agent.avoidancePriority = shelfInteractionPriority;
        SetWalking(false);
        yield return Face(browseShelf.LookPosition, browseShelf.FacingYawOffset);

        float browseUntil = Time.time + Random.Range(
            Mathf.Min(browseDurationRange.x, browseDurationRange.y),
            Mathf.Max(browseDurationRange.x, browseDurationRange.y));
        while (Time.time < browseUntil && !browseShelf.HasApproachingShopper)
        {
            HoldFacing(browseShelf.LookPosition);
            yield return null;
        }

        CurrentAction = browseShelf.HasApproachingShopper
            ? "Giving way to a shopper"
            : "Finished browsing";
        speech?.CommentOnFinishedBrowsing();
        browseShelf.EndInteraction();
        browseShelf.Release(this);
        agent.updateRotation = true;
        agent.isStopped = false;
        agent.avoidancePriority = normalPriority;
        yield return null;
    }

    private ChairStation ReserveEatingChair(float chance)
    {
        ChairStation chair = FindNearestAvailableChair();
        if (sitting == null || chair == null
            || Random.value * 100f >= chance
            || !sitting.TryReserveChair(chair))
            return null;
        return chair;
    }

    private IEnumerator EatAtChair(ChairStation chair)
    {
        CurrentAction = "Going to sit and eat";
        if (!sitting.BeginReservedSitSequence(chair))
            yield break;
        while (sitting.IsSitting)
        {
            CurrentAction = "Sitting and eating";
            yield return null;
        }
        CurrentAction = "Finished eating";
    }

    private ChairStation FindNearestAvailableChair()
    {
        ChairStation closest = null;
        float closestSqrDistance = float.PositiveInfinity;

        foreach (ChairStation chair in ChairStation.AllActive)
        {
            if (chair == null || !chair.IsAvailableFor(sitting))
                continue;

            float sqrDistance = (chair.ApproachPosition - transform.position).sqrMagnitude;
            if (sqrDistance < closestSqrDistance)
            {
                closestSqrDistance = sqrDistance;
                closest = chair;
            }
        }

        return closest;
    }

    private IEnumerator GoHome(string action)
    {
        FindHomeTarget();
        if (homeTarget == null)
        {
            CurrentAction = "No home target";
            Debug.LogWarning($"{name} cannot find an NPC despawning pad.", this);
            yield break;
        }

        CurrentAction = action;
        yield return MoveTo(homeTarget.position);
        CurrentAction = "Home";
    }

    private bool TryTakeShoppingSlot()
    {
        if (activeShopperCount >= maximumConcurrentShoppers)
            return false;

        activeShopperCount++;
        holdsShoppingSlot = true;
        return true;
    }

    private void ReleaseShoppingSlot()
    {
        if (!holdsShoppingSlot)
            return;

        holdsShoppingSlot = false;
        activeShopperCount = Mathf.Max(0, activeShopperCount - 1);
    }

    private void OnDisable()
    {
        ReleaseAllOccupancy();
        ActiveNpcs.Remove(this);
    }

    private void OnEnable()
    {
        ActiveNpcs.Add(this);
    }

    private IEnumerator MoveTo(Vector3 destination, float reevaluateAfter = 0f)
    {
        LeaveCheckoutMarker();
        if (!agent.isOnNavMesh)
        {
            CurrentAction = "Not on NavMesh";
            Debug.LogWarning($"{name} spawned away from the baked NavMesh.", this);
            yield break;
        }

        agent.isStopped = false;
        Vector3 reachableDestination = destination;
        if (NavMesh.SamplePosition(destination, out NavMeshHit destinationHit,
                Mathf.Max(0.75f, agent.radius), agent.areaMask))
            reachableDestination = destinationHit.position;

        if (!agent.SetDestination(reachableDestination))
            yield break;

        SetWalking(true);
        float reevaluateAt = reevaluateAfter > 0f
            ? Time.time + reevaluateAfter
            : float.PositiveInfinity;
        Vector3 progressPosition = transform.position;
        float nextOvertakeCheck = Time.time + Random.Range(0f, overtakeCheckInterval);
        float nextProgressCheck = Time.time + stuckCheckTime
            + agent.avoidancePriority * 0.005f;
        while (agent.pathPending
               || agent.remainingDistance > agent.stoppingDistance + arrivalDistance)
        {
            if (!agent.isOnNavMesh || agent.pathStatus == NavMeshPathStatus.PathInvalid)
                break;
            if (Time.time >= reevaluateAt)
                break;

            if (Time.time >= nextOvertakeCheck)
            {
                if (TryGetSlowerNpcAhead(out NpcNavigation slowerNpc))
                    yield return OvertakeMovingNpc(reachableDestination, slowerNpc);
                nextOvertakeCheck = Time.time + overtakeCheckInterval;
            }

            if (Time.time >= nextProgressCheck)
            {
                float progress = Vector3.Distance(transform.position, progressPosition);
                if (progress < minimumProgress
                    && Time.time >= nextMakeRoomAllowedTime
                    && HasNearbyBlockingNpc(reachableDestination, out NpcNavigation blocker))
                {
                    if (blocker != null && blocker.AvoidancePriority < AvoidancePriority)
                    {
                        speech?.ReactToBeingBulldozed();
                        blocker.speech?.ReactToPushingPast();
                    }
                    yield return MakeRoom(reachableDestination, "Making room for another NPC");
                }

                progressPosition = transform.position;
                nextProgressCheck = Time.time + stuckCheckTime
                    + agent.avoidancePriority * 0.005f;
            }
            SetWalking(agent.pathPending || agent.velocity.sqrMagnitude > 0.01f);
            yield return null;
        }
        SetWalking(false);
    }

    public IEnumerator MoveToCheckoutMarker(Transform marker, string action)
    {
        ReachedCheckoutMarker = false;
        if (marker == null)
            yield break;

        CurrentAction = action;
        yield return MoveTo(marker.position, 3f);
        if (!agent.isOnNavMesh)
            yield break;
        if (agent.pathPending
            || agent.remainingDistance > agent.stoppingDistance + arrivalDistance)
            yield break;

        agent.isStopped = true;
        agent.ResetPath();
        agent.updateRotation = false;
        SetWalking(false);
        agent.nextPosition = transform.position;
        agent.updatePosition = false;
        Vector3 exact = marker.position;
        exact.y = transform.position.y;
        transform.position = exact;
        visuallyAtCheckoutMarker = true;
        ReachedCheckoutMarker = true;

        Vector3 markerDirection = marker.right;
        markerDirection.y = 0f;
        if (markerDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(
                markerDirection.normalized, Vector3.up)
                * Quaternion.Euler(0f, checkoutMarkerYawOffset, 0f);
            while (Quaternion.Angle(transform.rotation, targetRotation) > 1f)
            {
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
                yield return null;
            }
            transform.rotation = targetRotation;
        }
    }

    public void LeaveCheckoutMarker()
    {
        if (!visuallyAtCheckoutMarker || agent == null)
            return;

        transform.position = agent.nextPosition;
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.isStopped = false;
        visuallyAtCheckoutMarker = false;
    }

    public IEnumerator FaceForCheckout(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            yield break;

        Quaternion targetRotation = Quaternion.LookRotation(
            direction.normalized, Vector3.up);
        while (Quaternion.Angle(transform.rotation, targetRotation) > 1f)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            yield return null;
        }
        transform.rotation = targetRotation;
    }

    public IEnumerator PlayCheckoutAnimation(string stateName)
    {
        yield return PlayAnimationToCompletion(stateName, grabDuration + 5f);
    }

    public IEnumerator BrowseWhileWaitingForCheckout()
    {
        yield return MaybeBrowse(null, true);
        yield return new WaitForSeconds(0.5f);
    }

    public void SetCheckoutAction(string action)
    {
        CurrentAction = action;
    }

    public void SetCheckoutQueueNumber(int number)
    {
        CheckoutQueueNumber = number;
    }

    public void CommentOnCheckoutQueue()
    {
        if (speech == null)
            speech = GetComponent<NpcSpeechBubble>();
        speech?.CommentOnQueue();
    }

    public void Speak(string line, float duration = -1f)
    {
        if (speech == null)
            speech = GetComponent<NpcSpeechBubble>();
        speech?.Say(line, duration);
    }

    public void SpeakRandom(string[] lines, float duration = -1f)
    {
        if (speech == null)
            speech = GetComponent<NpcSpeechBubble>();
        speech?.SayRandom(lines, duration);
    }

    private IEnumerator MakeRoom(Vector3 originalDestination, string avoidanceAction)
    {
        if (!agent.isOnNavMesh)
            yield break;

        Vector3 routeDirection = originalDestination - transform.position;
        routeDirection.y = 0f;
        if (routeDirection.sqrMagnitude < 0.01f)
            yield break;

        string previousAction = CurrentAction;
        CurrentAction = avoidanceAction;
        nextMakeRoomAllowedTime = Time.time + makeRoomCooldown;
        speech?.ReactToCrowding();

        routeDirection.Normalize();
        Vector3 side = Vector3.Cross(Vector3.up, routeDirection);
        // Alternate the preferred side and try the opposite side if blocked.
        if ((GetInstanceID() & 1) == 0)
            side = -side;

        bool foundA = TryGetSidestep(side, out Vector3 pointA, out float scoreA);
        bool foundB = TryGetSidestep(-side, out Vector3 pointB, out float scoreB);
        bool found = foundA || foundB;
        Vector3 bestPoint = !foundB || (foundA && scoreA >= scoreB) ? pointA : pointB;

        if (found && agent.SetDestination(bestPoint))
        {
            float giveUpTime = Time.time + 1.25f;
            while (agent.pathPending || agent.remainingDistance > arrivalDistance)
            {
                if (!agent.isOnNavMesh || Time.time >= giveUpTime)
                    break;
                SetWalking(agent.pathPending || agent.velocity.sqrMagnitude > 0.01f);
                yield return null;
            }
        }

        if (agent.isOnNavMesh)
        {
            agent.SetDestination(originalDestination);
            SetWalking(true);
        }
        CurrentAction = previousAction;
    }

    private bool HasNearbyBlockingNpc(Vector3 destination, out NpcNavigation blocker)
    {
        blocker = null;
        Vector3 routeDirection = destination - transform.position;
        routeDirection.y = 0f;
        if (routeDirection.sqrMagnitude < 0.01f)
            return false;
        routeDirection.Normalize();

        foreach (NpcNavigation other in ActiveNpcs)
        {
            if (other == null || other == this || other.agent == null
                || !other.agent.isOnNavMesh)
                continue;

            Vector3 offset = other.transform.position - transform.position;
            offset.y = 0f;
            float blockingDistance = agent.radius + other.agent.radius + 0.75f;
            if (offset.sqrMagnitude > blockingDistance * blockingDistance
                || offset.sqrMagnitude < 0.001f)
                continue;

            // Only an NPC in or near the forward travel corridor counts as a
            // blocker; nearby NPCs behind or well to the side are ignored.
            if (Vector3.Dot(routeDirection, offset.normalized) > 0.35f)
            {
                blocker = other;
                return true;
            }
        }
        return false;
    }

    private bool TryGetSidestep(Vector3 side, out Vector3 point, out float score)
    {
        return TryGetOpenNavMeshPoint(
            transform.position + side * sidestepDistance, out point, out score);
    }

    private bool TryGetOpenNavMeshPoint(Vector3 desired, out Vector3 point, out float score)
    {
        point = transform.position;
        score = float.NegativeInfinity;
        if (!NavMesh.SamplePosition(desired,
                out NavMeshHit sample, sidestepDistance, agent.areaMask))
            return false;

        NavMeshPath path = new();
        if (!agent.CalculatePath(sample.position, path)
            || path.status != NavMeshPathStatus.PathComplete)
            return false;

        point = sample.position;
        score = 0f;
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * Mathf.Deg2Rad;
            Vector3 direction = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            if (NavMesh.Raycast(point, point + direction * clearanceProbeDistance,
                    out NavMeshHit edge, agent.areaMask))
                score += Vector3.Distance(point, edge.position);
            else
                score += clearanceProbeDistance;
        }
        return true;
    }

    private IEnumerator OvertakeMovingNpc(
        Vector3 originalDestination, NpcNavigation slowerNpc)
    {
        if (slowerNpc == null || slowerNpc.agent == null)
            yield break;

        string previousAction = CurrentAction;
        CurrentAction = "Overtaking slower NPC";
        speech?.ReactToSlowWalker();

        Vector3 slowerForward = slowerNpc.agent.velocity.normalized;
        Vector3 side = Vector3.Cross(Vector3.up, slowerForward);
        Vector3 predictedLead = slowerNpc.transform.position
            + slowerForward * Mathf.Max(1.2f, slowerNpc.agent.radius * 2f);

        bool foundA = TryGetOpenNavMeshPoint(
            predictedLead + side * sidestepDistance, out _, out float scoreA);
        bool foundB = TryGetOpenNavMeshPoint(
            predictedLead - side * sidestepDistance, out _, out float scoreB);
        if (!foundA && !foundB)
        {
            CurrentAction = previousAction;
            yield break;
        }
        if (!foundA || (foundB && scoreB > scoreA))
            side = -side;

        float deadline = Time.time + 3f;
        while (Time.time < deadline && slowerNpc != null
               && slowerNpc.agent != null && slowerNpc.agent.isOnNavMesh)
        {
            Vector3 movingForward = slowerNpc.agent.velocity.sqrMagnitude > 0.04f
                ? slowerNpc.agent.velocity.normalized
                : slowerForward;
            Vector3 movingTarget = slowerNpc.transform.position
                + movingForward * Mathf.Max(1.2f, slowerNpc.agent.radius * 2f)
                + side * sidestepDistance;
            if (NavMesh.SamplePosition(movingTarget, out NavMeshHit hit,
                    sidestepDistance, agent.areaMask))
                agent.SetDestination(hit.position);

            Vector3 relative = transform.position - slowerNpc.transform.position;
            relative.y = 0f;
            if (Vector3.Dot(movingForward, relative) > slowerNpc.agent.radius * 1.5f)
                break;
            yield return null;
        }

        if (agent.isOnNavMesh)
            agent.SetDestination(originalDestination);
        CurrentAction = previousAction;
    }

    private IEnumerator Face(Vector3 target, float yawOffset)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            yield break;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up)
            * Quaternion.Euler(0f, yawOffset, 0f);
        while (Quaternion.Angle(transform.rotation, targetRotation) > 1f)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            yield return null;
        }
        transform.rotation = targetRotation;
    }

    private void HoldFacing(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(
            direction.normalized, Vector3.up)
            * Quaternion.Euler(0f, shelfFacingYawOffset, 0f);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    private void FindHomeTarget()
    {
        if (homeTarget != null)
            return;

        NpcDespawningPad pad = FindFirstObjectByType<NpcDespawningPad>();
        if (pad != null)
            homeTarget = pad.transform;
    }

    private void SetWalking(bool walking)
    {
        if (animator == null)
            return;

        animator.SetBool("IsWalking", walking);
        // Only scale locomotion. Shelf interactions and idle animations should
        // retain their authored playback speed.
        animator.speed = walking
            ? Mathf.Max(0.1f, agent.speed / normalSpeed)
            : 1f;
    }

    private void PlayAnimation(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        animator.ResetTrigger("Look");
        animator.ResetTrigger("Grab");
        animator.CrossFadeInFixedTime(stateName, 0.1f, 0, 0f);
    }

    private IEnumerator PlayAnimationToCompletion(string stateName, float timeout)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            yield break;

        PlayAnimation(stateName);
        float deadline = Time.time + Mathf.Max(1f, timeout);

        // Allow the cross-fade to enter the requested state.
        while (!animator.GetCurrentAnimatorStateInfo(0).IsName(stateName)
               && Time.time < deadline)
            yield return null;

        // Wait for the clip itself, rather than guessing its duration.
        while (Time.time < deadline)
        {
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (!state.IsName(stateName) || state.normalizedTime >= 1f)
                break;
            yield return null;
        }
    }

    private void ApplyAvoidanceTrait()
    {
        Vector2Int range = traits.HasTrait("Urgent Shopper")
            ? urgentPriorityRange
            : traits.HasTrait("Casual Shopper")
                ? casualPriorityRange
                : new Vector2Int(25, 75);

        int minimum = Mathf.Clamp(Mathf.Min(range.x, range.y), 0, 99);
        int maximum = Mathf.Clamp(Mathf.Max(range.x, range.y), minimum, 99);
        int movementOffset = traits.HasTrait("Slow Walker")
            ? -10
            : traits.HasTrait("Fast Walker") ? 10 : 0;
        agent.avoidancePriority = Mathf.Clamp(
            Random.Range(minimum, maximum + 1) + movementOffset, 0, 99);
    }

    private bool TryGetSlowerNpcAhead(out NpcNavigation slowerNpc)
    {
        slowerNpc = null;
        if (agent == null || agent.velocity.sqrMagnitude < 0.04f)
            return false;

        Vector3 forward = agent.velocity.normalized;
        float closestDistanceSquared = float.PositiveInfinity;
        foreach (NpcNavigation other in ActiveNpcs)
        {
            if (other == null || other == this || other.agent == null
                || !other.agent.isOnNavMesh || other.agent.speed >= agent.speed - 0.1f
                || other.agent.velocity.sqrMagnitude < 0.04f)
                continue;

            Vector3 otherDirection = other.agent.velocity.normalized;
            if (Vector3.Dot(forward, otherDirection) < 0.75f)
                continue;

            Vector3 offset = other.transform.position - transform.position;
            offset.y = 0f;
            float distanceSquared = offset.sqrMagnitude;
            if (distanceSquared > overtakeDetectionDistance * overtakeDetectionDistance
                || distanceSquared >= closestDistanceSquared)
                continue;

            if (Vector3.Dot(forward, offset.normalized) > 0.55f)
            {
                closestDistanceSquared = distanceSquared;
                slowerNpc = other;
            }
        }
        return slowerNpc != null;
    }

    private readonly struct ProductLocation
    {
        public ProductLocation(ShelfStation shelf, string product)
        {
            Shelf = shelf;
            Product = product;
        }

        public ShelfStation Shelf { get; }
        public string Product { get; }
    }

    private static void Shuffle<T>(IList<T> items)
    {
        for (int i = items.Count - 1; i > 0; i--)
        {
            int swapIndex = Random.Range(0, i + 1);
            (items[i], items[swapIndex]) = (items[swapIndex], items[i]);
        }
    }

    private void OnValidate()
    {
        slowSpeed = Mathf.Max(0.1f, slowSpeed);
        normalSpeed = Mathf.Max(0.1f, normalSpeed);
        fastSpeed = Mathf.Max(0.1f, fastSpeed);
        arrivalDistance = Mathf.Max(0.02f, arrivalDistance);
        turnSpeed = Mathf.Max(1f, turnSpeed);
        lookDuration = Mathf.Max(0f, lookDuration);
        grabDuration = Mathf.Max(0f, grabDuration);
        browseDurationRange.x = Mathf.Max(0f, browseDurationRange.x);
        browseDurationRange.y = Mathf.Max(0f, browseDurationRange.y);
        stuckCheckTime = Mathf.Max(0.25f, stuckCheckTime);
        minimumProgress = Mathf.Max(0.1f, minimumProgress);
        sidestepDistance = Mathf.Max(0.2f, sidestepDistance);
        makeRoomCooldown = Mathf.Max(0f, makeRoomCooldown);
        overtakeDetectionDistance = Mathf.Max(0.25f, overtakeDetectionDistance);
        overtakeCheckInterval = Mathf.Max(0.1f, overtakeCheckInterval);
        clearanceProbeDistance = Mathf.Max(0.25f, clearanceProbeDistance);
        maximumConcurrentShoppers = Mathf.Max(1, maximumConcurrentShoppers);
        urgentPriorityRange.x = Mathf.Clamp(urgentPriorityRange.x, 0, 99);
        urgentPriorityRange.y = Mathf.Clamp(urgentPriorityRange.y, 0, 99);
        casualPriorityRange.x = Mathf.Clamp(casualPriorityRange.x, 0, 99);
        casualPriorityRange.y = Mathf.Clamp(casualPriorityRange.y, 0, 99);
        personalSpaceRadius = Mathf.Max(0.1f, personalSpaceRadius);
    }
}
