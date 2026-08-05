using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class NPCAnimation : MonoBehaviour
{
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

        animator.SetBool("IsWalking", isWalking);
    }

    public void Grab()
    {
        animator.SetTrigger("Grab");
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