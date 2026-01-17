using UnityEngine;
using UnityEngine.AI;

public class HostageRunawayController : MonoBehaviour
{
    [Header("Refs")]
    public NavMeshAgent agent;
    public Animator animator;
    public Transform hideSpot;

    [Header("Animator Params")]
    public string runningBool = "Running";
    public string scaredBool = "Scared";
    public string getDownTrigger = "GetDown";

    [Header("Behavior")]
    public float arriveDistance = 0.6f;
    public bool keepScaredUntilGunfireStops = true;

    private bool isRunning = false;
    private bool gunfireOn = false;
    private bool reachedHide = false;

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
    }

    void Update()
    {
        if (!isRunning || agent == null || hideSpot == null) return;

        // Check arrival
        if (!agent.pathPending && agent.remainingDistance <= Mathf.Max(arriveDistance, agent.stoppingDistance))
        {
            // Arrived at hide spot
            isRunning = false;
            reachedHide = true;

            agent.isStopped = true;

            if (animator != null)
            {
                animator.SetBool(runningBool, false);

                // play get-down once (optional)
                if (!string.IsNullOrEmpty(getDownTrigger))
                    animator.SetTrigger(getDownTrigger);

                animator.SetBool(scaredBool, true);
            }
        }
    }

    // Call this when shooting starts/stops (from your shooter script)
    public void OnGunfire(bool isOn)
    {
        gunfireOn = isOn;

        if (gunfireOn)
        {
            StartRunToHide();
        }
        else
        {
            // Gunfire stopped
            if (keepScaredUntilGunfireStops)
            {
                // Calm down ONLY if you want
                if (reachedHide && animator != null)
                    animator.SetBool(scaredBool, false);
            }
        }
    }

    void StartRunToHide()
    {
        if (hideSpot == null)
        {
            Debug.LogWarning("HostageRunawayController: hideSpot not assigned!");
            return;
        }

        if (agent == null)
        {
            Debug.LogWarning("HostageRunawayController: NavMeshAgent missing!");
            return;
        }

        // If already reached hide and still scared, just stay scared
        if (reachedHide)
        {
            if (animator != null) animator.SetBool(scaredBool, true);
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(hideSpot.position);

        isRunning = true;

        if (animator != null)
        {
            animator.SetBool(scaredBool, false);
            animator.SetBool(runningBool, true);
        }
    }
}
