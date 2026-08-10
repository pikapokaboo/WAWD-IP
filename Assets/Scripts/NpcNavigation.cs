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
    [SerializeField, Range(0f, 100f)] private float enterStoreChance = 45f;

    [Header("Trait Speeds")]
    [SerializeField, Min(0.1f)] private float slowSpeed = 2f;
    [SerializeField, Min(0.1f)] private float normalSpeed = 3.5f;
    [SerializeField, Min(0.1f)] private float fastSpeed = 5f;

    [Header("Interaction")]
    [SerializeField, Min(0.02f)] private float arrivalDistance = 0.08f;
    [SerializeField, Min(1f)] private float turnSpeed = 360f;
    [Tooltip("Corrects models whose visible forward direction differs from Unity's blue Z axis.")]
    [SerializeField] private float shelfFacingYawOffset = 90f;
    [SerializeField, Min(0f)] private float lookDuration = 1.5f;
    [SerializeField, Min(0f)] private float grabDuration = 1.5f;

    private NavMeshAgent agent;
    private NpcTraits traits;
    private Animator animator;
    private readonly List<ShelfStation> shoppingRoute = new();
    private readonly List<string> wantedProducts = new();

    public IReadOnlyList<ShelfStation> ShoppingRoute => shoppingRoute;
    public IReadOnlyList<string> WantedProducts => wantedProducts;
    public string CurrentAction { get; private set; } = "Starting";

    private IEnumerator Start()
    {
        agent = GetComponent<NavMeshAgent>();
        traits = GetComponent<NpcTraits>();
        animator = GetComponentInChildren<Animator>();

        // Start runs after Awake, where NpcTraits normally rolls its result.
        if (!traits.HasRolled)
            traits.RollTraits();

        ApplyMovementTrait();
        FindHomeTarget();

        if (Random.value * 100f < enterStoreChance)
        {
            BuildShoppingRoute();
            for (int i = 0; i < shoppingRoute.Count; i++)
                yield return VisitShelf(shoppingRoute[i], wantedProducts[i]);
        }

        yield return GoHome();
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

    private IEnumerator VisitShelf(ShelfStation shelf, string product)
    {
        if (shelf == null)
            yield break;

        CurrentAction = $"Going to {product}";
        yield return MoveTo(shelf.StandPosition);
        if (shelf == null || !agent.isOnNavMesh)
            yield break;

        agent.isStopped = true;
        agent.updateRotation = false;
        SetWalking(false);
        CurrentAction = $"Looking at {product}";
        yield return Face(shelf.LookPosition);

        if (traits.HasTrait("No Money"))
        {
            PlayAnimation("Look");
            yield return new WaitForSeconds(lookDuration);
        }

        CurrentAction = $"Grabbing {product}";
        PlayAnimation(string.IsNullOrWhiteSpace(shelf.InteractionTrigger)
            ? "Grab"
            : shelf.InteractionTrigger);
        yield return new WaitForSeconds(grabDuration);

        if (agent.isOnNavMesh)
        {
            agent.updateRotation = true;
            agent.isStopped = false;
        }
    }

    private IEnumerator GoHome()
    {
        FindHomeTarget();
        if (homeTarget == null)
        {
            CurrentAction = "No home target";
            Debug.LogWarning($"{name} cannot find an NPC despawning pad.", this);
            yield break;
        }

        CurrentAction = "Going home";
        yield return MoveTo(homeTarget.position);
        CurrentAction = "Home";
    }

    private IEnumerator MoveTo(Vector3 destination)
    {
        if (!agent.isOnNavMesh)
        {
            CurrentAction = "Not on NavMesh";
            Debug.LogWarning($"{name} spawned away from the baked NavMesh.", this);
            yield break;
        }

        agent.isStopped = false;
        if (!agent.SetDestination(destination))
            yield break;

        SetWalking(true);
        while (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + arrivalDistance)
        {
            if (!agent.isOnNavMesh || agent.pathStatus == NavMeshPathStatus.PathInvalid)
                break;
            yield return null;
        }
        SetWalking(false);
    }

    private IEnumerator Face(Vector3 target)
    {
        Vector3 direction = target - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.001f)
            yield break;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up)
            * Quaternion.Euler(0f, shelfFacingYawOffset, 0f);
        while (Quaternion.Angle(transform.rotation, targetRotation) > 1f)
        {
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, turnSpeed * Time.deltaTime);
            yield return null;
        }
        transform.rotation = targetRotation;
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
        if (animator != null)
            animator.SetBool("IsWalking", walking);
    }

    private void PlayAnimation(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return;

        animator.ResetTrigger("Look");
        animator.ResetTrigger("Grab");
        animator.CrossFadeInFixedTime(stateName, 0.1f, 0, 0f);
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
    }
}
