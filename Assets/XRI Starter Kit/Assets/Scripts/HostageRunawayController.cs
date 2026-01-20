using UnityEngine;
using UnityEngine.AI;

public class HostageRunawayController : MonoBehaviour
{
    [Header("Refs")]
    public NavMeshAgent agent;
    public Animator animator;

    [Header("Targets (IMPORTANT: these must NOT be children of the hostage)")]
    public Transform doorPoint;
    public Transform doorThroughPoint;
    public Transform hideSpot;

    [Header("Door (optional)")]
    public MonoBehaviour door;                 // drag your SimpleDoorOpener here
    public float openDoorDistance = 1.2f;

    [Header("Animator Params")]
    public string runningBool = "Running";
    public string scaredBool = "Scared";
    public string getDownTrigger = "GetDown";

    [Header("Arrive Settings")]
    public float arriveDistance = 0.6f;
    public float maxSnapDistance = 0.5f;

    [Header("Behavior")]
    public bool keepScaredUntilGunfireStops = true;

    private enum State
    {
        Idle,
        ToDoorPoint,
        ToDoorThroughPoint,
        ToHideSpot,
        Hiding
    }

    private State state = State.Idle;
    private bool isGunfireActive;

    void Awake()
    {
        if (agent == null) agent = GetComponent<NavMeshAgent>();
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (animator != null) animator.applyRootMotion = false;
        if (agent != null)
        {
            agent.updateRotation = true;
            agent.updatePosition = true;
            agent.autoBraking = false;
        }
    }

    void Update()
    {
        if (agent == null || animator == null) return;

        // Always drive running animation from REAL velocity
        animator.SetBool(runningBool, agent.velocity.magnitude > 0.1f);

        switch (state)
        {
            case State.ToDoorPoint:
                // open door when close enough
                if (door != null && doorPoint != null)
                {
                    float d = Vector3.Distance(transform.position, doorPoint.position);
                    if (d <= openDoorDistance) TryOpenDoor();
                }

                if (ArrivedStrict())
                {
                    Log("Reached DoorPoint");
                    GoToDoorThroughPoint();
                }
                break;

            case State.ToDoorThroughPoint:
                if (ArrivedStrict())
                {
                    Log("Reached DoorThroughPoint");
                    GoToHideSpot();
                }
                break;

            case State.ToHideSpot:
                if (ArrivedStrict())
                {
                    Log("Reached HideSpot -> GetDown + Scared");
                    ReachedHideSpot();
                }
                break;

            case State.Hiding:
                // If you want him to calm down when gunfire stops
                if (!isGunfireActive && !keepScaredUntilGunfireStops)
                {
                    animator.SetBool(scaredBool, false);
                }
                break;
        }
    }

    // Called from your shooter script: runawayHostage.OnGunfire(true/false)
    public void OnGunfire(bool active)
    {
        isGunfireActive = active;

        if (active)
        {
            StartRunSequence();
        }
        else
        {
            // if your design = calm down when gunfire stops
            if (!keepScaredUntilGunfireStops && state == State.Hiding)
                animator.SetBool(scaredBool, false);
        }
    }

    private void StartRunSequence()
    {
        if (doorPoint == null || doorThroughPoint == null || hideSpot == null)
        {
            Debug.LogWarning("[HostageRunaway] Missing targets. Assign DoorPoint, DoorThroughPoint, HideSpot.");
            return;
        }

        // IMPORTANT: Do NOT set scared at start, or Animator will crouch while moving
        animator.ResetTrigger(getDownTrigger);
        animator.SetBool(scaredBool, false);

        state = State.ToDoorPoint;
        TrySetDestination(doorPoint.position, "DoorPoint");
        Log("StartRunSequence -> DoorPoint");
    }

    private void GoToDoorThroughPoint()
    {
        state = State.ToDoorThroughPoint;
        TrySetDestination(doorThroughPoint.position, "DoorThroughPoint");
    }

    private void GoToHideSpot()
    {
        state = State.ToHideSpot;
        TrySetDestination(hideSpot.position, "HideSpot");
    }

    private void ReachedHideSpot()
    {
        state = State.Hiding;

        // stop movement cleanly
        agent.ResetPath();
        agent.velocity = Vector3.zero;

        // now crouch + scared
        animator.SetBool(runningBool, false);
        animator.SetBool(scaredBool, true);
        animator.SetTrigger(getDownTrigger);
    }

    // -------- Arrival logic (prevents fake crouch / wrong room crouch) ----------
    private bool ArrivedStrict()
    {
        if (agent.pathPending) return false;

        if (agent.pathStatus != NavMeshPathStatus.PathComplete)
            return false;

        if (agent.remainingDistance > Mathf.Max(agent.stoppingDistance, arriveDistance) + 0.05f)
            return false;

        if (agent.velocity.sqrMagnitude > 0.05f)
            return false;

        return true;
    }

    // -------- Destination safety (snap to navmesh) ----------
    private void TrySetDestination(Vector3 rawPos, string label)
    {
        Vector3 pos = rawPos;

        if (NavMesh.SamplePosition(rawPos, out NavMeshHit hit, maxSnapDistance, NavMesh.AllAreas))
        {
            pos = hit.position;
        }
        else
        {
            Debug.LogWarning($"[HostageRunaway] {label} is NOT on NavMesh. Move it onto the floor/NavMesh.");
            return;
        }

        agent.SetDestination(pos);
        Log($"SetDestination -> {label} (snapped: {pos})");
    }

    // -------- Door open without compile error ----------
    private void TryOpenDoor()
    {
        // We don't call door.Open() directly (your SimpleDoorOpener doesn't have that method)
        // so we use SendMessage safely.
        door.SendMessage("Open", SendMessageOptions.DontRequireReceiver);
        door.SendMessage("OpenDoor", SendMessageOptions.DontRequireReceiver);
        door.SendMessage("TriggerOpen", SendMessageOptions.DontRequireReceiver);

        Log($"Door open attempted (component: {door.GetType().Name})");
    }

    private void Log(string msg)
    {
        Debug.Log($"[HostageRunaway] {msg}");
    }
}
