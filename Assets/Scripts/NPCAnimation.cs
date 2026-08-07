using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCAnimation : MonoBehaviour
{
    private static readonly int IsWalkingParameter = Animator.StringToHash("IsWalking");
    private static readonly int GrabState = Animator.StringToHash("Grab");

    private NavMeshAgent agent;
    private Animator animator;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {

        bool isWalking =
        agent.velocity.sqrMagnitude > 0.01f &&
        !agent.pathPending &&
        !agent.isStopped;

        if (animator != null)
            animator.SetBool(IsWalkingParameter, isWalking);
    }

    public void Grab()
    {
        if (animator == null)
            return;

        // The controller's trigger transition is only available from Idle and
        // waits for exit time. Entering the state directly makes shelf grabs
        // start immediately, even if the NPC has only just stopped walking.
        animator.SetBool(IsWalkingParameter, false);
        animator.CrossFadeInFixedTime(GrabState, 0.1f, 0, 0f);
    }

    public void Sit()
    {
        animator.SetTrigger("Sit");
    }

        public void Stand()
    {
        animator.SetTrigger("Stand");
    }

    public void Look()
    {
        animator.SetTrigger("Look");
    }

}
