using System.Collections;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Full hostage FSM controller with all Module 2 transitions.
///
/// States
///   Calm    — idle, no reaction
///   Fearful — scared in place (HostageScareController active)
///   Freeze  — total paralysis from sustained close-range threat
///   Panic   — actively fleeing (HostageRunawayController active)
///   Follow  — escorting with the player via NavMeshAgent
///   Freed   — terminal; reached hiding spot
///
/// Transitions (per design spec)
///   Calm    + GunshotHeard                          → Fearful
///   Fearful + TerroristEnteredRoom                  → Panic
///   Fearful + GunshotHeard (dist < freezeRange)     → Freeze
///   Panic   + sustained close threat > freezeThreshold → Freeze  (via SustainedThreatCheck)
///   Any     + HostageContactStarted (trainee nearby, safe) → Follow
///   Follow  + GunshotHeard (during escort)          → Fearful  [regression — flagged in telemetry]
///   Any     + RoomCleared + no nearby threat        → Calm
///   Freed   — terminal; no further reactions
///
/// NavMesh dependency
///   NavMeshAgent is required for Follow state.
///   If absent or not on a NavMesh, Follow falls back to Fearful.
///
/// Setup
///   1. Add this component to a hostage NPC GameObject.
///   2. Drag scareController and runawayController into the Inspector (both optional).
///   3. Set panicDistance and freezeRange as needed.
/// </summary>
public class HostageController : MonoBehaviour, INPCResponder
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Existing Hostage Components (both optional)")]
    public HostageScareController   scareController;
    public HostageRunawayController runawayController;

    [Header("Selection Tuning")]
    [Tooltip("Seconds before this hostage can be selected again for the same event.")]
    public float responseCooldown = 3f;

    [Header("Distance Thresholds")]
    [Tooltip("Shots within this distance escalate Calm → Panic directly (skipping Fearful).")]
    public float panicDistance = 6f;

    [Tooltip("Shots within this distance trigger Freeze when the hostage is already Fearful.")]
    public float freezeRange = 5f;

    [Tooltip("Seconds of continuous close-range threat in Panic state before entering Freeze.")]
    public float freezeThreshold = 3f;

    [Header("Debug — read-only in Play mode")]
    public HostageState currentState = HostageState.Calm;

    // ── INPCResponder ─────────────────────────────────────────────────────────

    public string  NPCId            => gameObject.name;
    public NPCRole Role             => NPCRole.Hostage;
    public Vector3 Position         => transform.position;
    public float   LastResponseTime => _lastResponseTime;

    /// State readiness 0.0–1.0 (Calm = most available).
    public float StateScore => currentState switch
    {
        HostageState.Calm    => 1.0f,
        HostageState.Fearful => 0.8f,
        HostageState.Follow  => 0.5f,
        HostageState.Freeze  => 0.2f,
        HostageState.Panic   => 0.0f,
        HostageState.Freed   => 0.0f,
        _                    => 0.0f,
    };

    public bool CanRespond(ScenarioEvent e)
    {
        // Terminal states — no further reactions
        if (currentState == HostageState.Freed) return false;

        switch (e.Type)
        {
            case ScenarioEventType.GunshotHeard:
            case ScenarioEventType.StressSpike:
            case ScenarioEventType.TerroristDown:
            case ScenarioEventType.TerroristEnteredRoom:
            case ScenarioEventType.HostageContactStarted:
            case ScenarioEventType.HostageFreed:
            case ScenarioEventType.RoomCleared:
                break;
            default:
                return false;
        }

        if (Time.time - _lastResponseTime < responseCooldown) return false;
        return true;
    }

    public void RespondTo(ScenarioEvent e)
    {
        _lastResponseTime = Time.time;

        switch (e.Type)
        {
            // ── Gunshot ───────────────────────────────────────────────────────
            case ScenarioEventType.GunshotHeard:
            {
                float dist = Vector3.Distance(transform.position, e.Origin);

                if (currentState == HostageState.Follow)
                {
                    // Regression — hostile sound during escort
                    TransitionTo(HostageState.Fearful, e, regression: true);
                }
                else if (currentState == HostageState.Fearful && dist <= freezeRange)
                {
                    TransitionTo(HostageState.Freeze, e);
                }
                else if (currentState == HostageState.Calm)
                {
                    TransitionTo(dist <= panicDistance
                        ? HostageState.Panic
                        : HostageState.Fearful, e);
                }
                break;
            }

            // ── Stress spike (rapid fire) ─────────────────────────────────────
            case ScenarioEventType.StressSpike:
                if (currentState == HostageState.Calm || currentState == HostageState.Fearful)
                    TransitionTo(HostageState.Panic, e);
                break;

            // ── Terrorist entered the room ────────────────────────────────────
            case ScenarioEventType.TerroristEnteredRoom:
                if (currentState == HostageState.Fearful)
                    TransitionTo(HostageState.Panic, e);
                break;

            // ── Threat eliminated ─────────────────────────────────────────────
            case ScenarioEventType.TerroristDown:
                // Threat removed — panic may resolve, but do not override Freed
                if (currentState == HostageState.Panic)
                    TransitionTo(HostageState.Fearful, e);
                break;

            case ScenarioEventType.RoomCleared:
                if (currentState != HostageState.Freed)
                    TransitionTo(HostageState.Calm, e);
                break;

            // ── Trainee contact ───────────────────────────────────────────────
            case ScenarioEventType.HostageContactStarted:
                if (currentState == HostageState.Calm ||
                    currentState == HostageState.Fearful ||
                    currentState == HostageState.Freeze)
                {
                    // e.Instigator is the trainee GameObject — capture for follow target
                    _followTarget = e.Instigator != null ? e.Instigator.transform : null;
                    TransitionTo(HostageState.Follow, e);
                }
                break;

            // ── Terminal ──────────────────────────────────────────────────────
            case ScenarioEventType.HostageFreed:
                TransitionTo(HostageState.Freed, e);
                break;
        }
    }

    // ── Private state ─────────────────────────────────────────────────────────

    float       _lastResponseTime = -99f;
    NavMeshAgent _agent;
    Transform   _followTarget;
    Coroutine   _followRoutine;
    Coroutine   _freezeCheckRoutine;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>(); // optional
        NPCRegistry.Register(this);
    }

    void OnDestroy() => NPCRegistry.Unregister(this);

    void Update()
    {
        // Auto-detect when runaway sequence completes → raise HostageFreed
        if (currentState == HostageState.Panic &&
            runawayController != null          &&
            runawayController.IsHiding)
        {
            TransitionTo(HostageState.Freed, null);
            EventManager.Instance?.Raise(new ScenarioEvent(
                ScenarioEventType.HostageFreed, transform.position, gameObject));
        }
    }

    // ── State machine ─────────────────────────────────────────────────────────

    void TransitionTo(HostageState next, ScenarioEvent trigger, bool regression = false)
    {
        if (next == currentState) return;

        var prev = currentState;
        currentState = next;

        // ── Telemetry ─────────────────────────────────────────────────────────
        string notes = regression ? "[REGRESSION]" : "";
        TelemetryLogger.Instance?.LogStateChange(
            NPCId, "Hostage", prev.ToString(), next.ToString(), trigger);

        if (regression)
            Debug.LogWarning($"[HostageController] {gameObject.name}: REGRESSION {prev} → {next} " +
                             $"(gunshot during escort)");
        else
            Debug.Log($"[HostageController] {gameObject.name}: {prev} → {next}");

        // ── Stop follow / freeze routines when leaving those states ───────────
        if (prev == HostageState.Follow)
        {
            if (_followRoutine != null) { StopCoroutine(_followRoutine); _followRoutine = null; }
            if (_agent != null && _agent.isActiveAndEnabled) _agent.ResetPath();
        }
        if (prev == HostageState.Panic || prev == HostageState.Freeze)
        {
            if (_freezeCheckRoutine != null) { StopCoroutine(_freezeCheckRoutine); _freezeCheckRoutine = null; }
        }

        // ── Behaviours for new state ──────────────────────────────────────────
        switch (next)
        {
            case HostageState.Fearful:
                scareController?.SetScared(true);
                break;

            case HostageState.Freeze:
                scareController?.SetScared(true);
                // Halt all movement
                if (_agent != null && _agent.isActiveAndEnabled) _agent.ResetPath();
                break;

            case HostageState.Panic:
                scareController?.SetScared(true);
                if (runawayController != null && !runawayController.IsFleeing)
                    runawayController.OnGunfire(true);
                // Start a timer: if panic persists with a close threat, enter Freeze
                _freezeCheckRoutine = StartCoroutine(SustainedThreatCheck(trigger));
                break;

            case HostageState.Follow:
                scareController?.SetScared(false);
                _followRoutine = StartCoroutine(FollowRoutine());
                break;

            case HostageState.Calm:
                scareController?.SetScared(false);
                runawayController?.OnGunfire(false);
                break;

            case HostageState.Freed:
                // Runaway controller has already settled at the hiding spot.
                break;
        }
    }

    // ── Follow (NavMesh) ──────────────────────────────────────────────────────

    IEnumerator FollowRoutine()
    {
        // Validate NavMesh availability
        if (_agent == null)
        {
            Debug.LogWarning($"[HostageController] {NPCId}: NavMeshAgent missing — falling back to Fearful");
            TransitionTo(HostageState.Fearful, null);
            yield break;
        }

        if (!_agent.isOnNavMesh)
        {
            Debug.LogWarning($"[HostageController] {NPCId}: Not on NavMesh — falling back to Fearful");
            TransitionTo(HostageState.Fearful, null);
            yield break;
        }

        _agent.stoppingDistance = 1.5f;
        Vector3 lastDestination = transform.position;

        while (currentState == HostageState.Follow)
        {
            if (_followTarget != null)
            {
                Vector3 targetPos = _followTarget.position;
                if (Vector3.Distance(targetPos, lastDestination) > 1.0f)
                {
                    _agent.SetDestination(targetPos);
                    lastDestination = targetPos;
                }
            }
            yield return new WaitForSeconds(0.3f);
        }

        if (_agent.isActiveAndEnabled) _agent.ResetPath();
    }

    // ── Sustained threat check (Panic → Freeze) ───────────────────────────────

    IEnumerator SustainedThreatCheck(ScenarioEvent trigger)
    {
        float elapsed = 0f;
        while (currentState == HostageState.Panic && elapsed < freezeThreshold)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (currentState == HostageState.Panic)
            TransitionTo(HostageState.Freeze, trigger);
    }

    // ── Inspector test helpers ────────────────────────────────────────────────

    [ContextMenu("TEST → GunshotHeard (far)")]
    void TestGunshotFar()
    {
        EventManager.Instance?.Raise(new ScenarioEvent(
            ScenarioEventType.GunshotHeard, transform.position + Vector3.forward * 15f, gameObject));
    }

    [ContextMenu("TEST → GunshotHeard (close — may Freeze)")]
    void TestGunshotClose()
    {
        EventManager.Instance?.Raise(new ScenarioEvent(
            ScenarioEventType.GunshotHeard, transform.position + Vector3.forward * 2f, gameObject));
    }

    [ContextMenu("TEST → StressSpike")]
    void TestStressSpike()
    {
        EventManager.Instance?.Raise(new ScenarioEvent(
            ScenarioEventType.StressSpike, transform.position, gameObject));
    }

    [ContextMenu("TEST → TerroristEnteredRoom")]
    void TestTerroristEntered()
    {
        EventManager.Instance?.Raise(new ScenarioEvent(
            ScenarioEventType.TerroristEnteredRoom, transform.position));
    }

    [ContextMenu("TEST → HostageContactStarted")]
    void TestHostageContact()
    {
        EventManager.Instance?.Raise(new ScenarioEvent(
            ScenarioEventType.HostageContactStarted, transform.position, gameObject));
    }

    [ContextMenu("TEST → RoomCleared")]
    void TestRoomCleared()
    {
        EventManager.Instance?.Raise(new ScenarioEvent(
            ScenarioEventType.RoomCleared, transform.position));
    }
}
