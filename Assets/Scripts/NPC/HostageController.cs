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

    [Header("Animation")]
    [Tooltip("Planar move speed (m/s) that maps to a full walk animation. The hostage " +
             "animator's 'Speed' parameter is set to (actual speed / this), clamped 0-1, so " +
             "the hostage walks while following you and idles when still. ~1.0 suits the " +
             "default NavMeshAgent speed.")]
    public float walkAnimReferenceSpeed = 1.6f;

    [Tooltip("NavMesh move speed while following the trainee. Fast enough to keep up " +
             "with a walking player. Overrides the agent's 3.5 m/s default.")]
    public float followSpeed = 2.2f;

    [Header("Health (the hostage can be shot)")]
    [Tooltip("Hostage starting health. Friendly-fire from the trainee damages this.")]
    public float maxHealth = 100f;

    [Tooltip("At/below this health (but above 0) the hostage is 'injured' — it switches to a " +
             "limping/injured escort walk instead of the normal scared walk.")]
    public float injuredThreshold = 50f;

    [Header("Debug — read-only in Play mode")]
    public HostageState currentState = HostageState.Calm;

    /// <summary>Raised exactly once when this hostage dies — whether by trainee
    /// friendly-fire (TakeHit) or a guardian execution (Execute). Module4SessionController
    /// subscribes to end the mission as an immediate FAIL and tag the cause.</summary>
    public event System.Action<HostageController> OnKilled;

    /// <summary>True when a terrorist executed this hostage (vs. trainee friendly-fire).
    /// Lets the session tag the outcome as "hostage_executed" for the AAR.</summary>
    public bool WasExecuted { get; private set; }

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
        if (currentState == HostageState.Down)  return false;
        // Held at gunpoint — the guardian fully controls this hostage. It ignores
        // ambient stimuli; only ReleaseFromHold()/Execute() (driven by the guardian)
        // or its death move it out of this state.
        if (currentState == HostageState.Held)  return false;

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
                // The rescuer reached the hostage — follow from any non-terminal,
                // non-already-following state (incl. Panic: the rescuer's arrival
                // calms them enough to be led out).
                if (currentState == HostageState.Calm ||
                    currentState == HostageState.Fearful ||
                    currentState == HostageState.Freeze ||
                    currentState == HostageState.Panic)
                {
                    // e.Instigator is the trainee GameObject — captured as a fallback
                    // follow target (FollowRoutine prefers the live Camera.main).
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

    // ── Damage / death ────────────────────────────────────────────────────────

    /// <summary>
    /// Apply damage to the hostage (e.g. trainee friendly-fire). Below
    /// injuredThreshold the hostage limps (injured escort walk); at 0 it dies.
    /// Called by HostageHitBox when a bullet hits.
    /// </summary>
    public void TakeHit(float damage)
    {
        if (currentState == HostageState.Down || currentState == HostageState.Freed) return;

        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        Debug.LogWarning($"[HostageController] {NPCId} HIT — HP {_currentHealth:F0} " +
                         $"(friendly fire is a training failure).");

        if (_currentHealth <= 0f)
        {
            TransitionTo(HostageState.Down, null);
            return;
        }

        if (_currentHealth <= injuredThreshold)
            _injured = true; // Update() pushes this to the animator → injured walk
    }

    // ── Leverage (guardian control) ─────────────────────────────────────────────

    /// <summary>A guardian terrorist seizes this hostage as leverage: it kneels at
    /// gunpoint (Held) and stops reacting to ambient stimuli until released, executed,
    /// or its captor dies. No-op if the hostage is already terminal.</summary>
    public void SeizeAsLeverage(TerroristController captor)
    {
        if (currentState == HostageState.Down || currentState == HostageState.Freed) return;
        if (currentState == HostageState.Held && _captor == captor) return;
        _captor = captor;
        TransitionTo(HostageState.Held, null);
    }

    /// <summary>Release a held hostage (e.g. the captor was killed). Drops it back to
    /// Fearful so the trainee can then make contact and escort it out. No-op unless Held.</summary>
    public void ReleaseFromHold()
    {
        if (currentState != HostageState.Held) return;
        _captor = null;
        TransitionTo(HostageState.Fearful, null);
    }

    /// <summary>The guardian executes this hostage — an instant, unavoidable kill used
    /// as the leverage payoff. Terrorist weapons otherwise can't harm hostages, so this
    /// is the only terrorist→hostage lethal path. Ends in Down + OnKilled(executed).</summary>
    public void Execute(TerroristController by)
    {
        if (currentState == HostageState.Down || currentState == HostageState.Freed) return;
        WasExecuted = true;
        _currentHealth = 0f;
        Debug.LogWarning($"[HostageController] {NPCId} EXECUTED by " +
                         $"{(by != null ? by.gameObject.name : "guardian")} — mission failure.");
        TransitionTo(HostageState.Down, null);
    }

    /// <summary>True while a live guardian is holding this hostage at gunpoint.</summary>
    public bool IsHeld => currentState == HostageState.Held;

    /// <summary>World position of the hostage's head. The guardian aims its barrel here
    /// during the warning — pointing at the ROOT would aim at the floor, and aiming level
    /// would sail straight over a kneeling hostage. Falls back to a sensible offset if the
    /// rig isn't humanoid.</summary>
    public Vector3 HeadPosition
    {
        get
        {
            if (_animator != null && _animator.isHuman)
            {
                var head = _animator.GetBoneTransform(HumanBodyBones.Head);
                if (head != null) return head.position;
            }
            return transform.position + Vector3.up * 1.1f;
        }
    }

    /// <summary>Guardian is actively threatening this hostage (barrel on its head). Switches
    /// it to the cowering/looking-up kneel (Kneeling Inspecting) instead of the calm kneel.
    /// Only meaningful while Held.</summary>
    public void SetThreatened(bool on)
    {
        _threatened = on && currentState == HostageState.Held;
        if (_hasThreatenedParam) _animator?.SetBool(_animThreatened, _threatened);
    }

    // ── Private state ─────────────────────────────────────────────────────────

    float       _lastResponseTime = -99f;
    NavMeshAgent _agent;
    Animator    _animator;
    Transform   _followTarget;
    Coroutine   _followRoutine;
    Coroutine   _freezeCheckRoutine;
    Vector3     _lastAnimPos;
    float       _currentHealth;
    bool        _injured;
    bool        _killedFired;              // guards OnKilled against double-firing
    bool        _threatened;               // captor currently has the barrel on this hostage's head
    TerroristController _captor;           // guardian holding this hostage as leverage (null if free)
    bool        _hasSpeedParam;
    bool        _hasInjuredParam;
    bool        _hasHeldParam;
    bool        _hasThreatenedParam;
    static readonly int _animSpeed      = Animator.StringToHash("Speed");
    static readonly int _animInjured    = Animator.StringToHash("Injured");
    static readonly int _animHeld       = Animator.StringToHash("Held");
    static readonly int _animScared     = Animator.StringToHash("Scared");
    static readonly int _animThreatened = Animator.StringToHash("Threatened");

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>(); // optional
        _animator = GetComponentInChildren<Animator>(); // optional — drives walk/idle blend
        _lastAnimPos = transform.position;
        _currentHealth = maxHealth;

        // Movement comes from the NavMeshAgent, not the animation's root curve —
        // turn root motion off so the walk clip can't drag the hostage around.
        if (_animator != null)
        {
            _animator.applyRootMotion = false;

            // Only drive parameters the assigned controller actually has — otherwise
            // SetFloat/SetBool spam "parameter does not exist" every frame (e.g. if a
            // hostage's Animator Controller isn't the rebuilt HostageAnimator).
            foreach (var p in _animator.parameters)
            {
                if (p.nameHash == _animSpeed)      _hasSpeedParam      = true;
                if (p.nameHash == _animInjured)    _hasInjuredParam    = true;
                if (p.nameHash == _animHeld)       _hasHeldParam       = true;
                if (p.nameHash == _animThreatened) _hasThreatenedParam = true;
            }
            if (!_hasSpeedParam)
                Debug.LogWarning($"[HostageController] {NPCId}: Animator has no 'Speed' param — " +
                                 "its Animator Controller is probably not HostageAnimator. " +
                                 "Walk animation won't blend until that's fixed.");
        }

        // Keep-up speed: fast enough to follow a walking trainee, not so fast it
        // slides badly. Overrides the NavMeshAgent's 3.5 m/s default.
        if (_agent != null) _agent.speed = followSpeed;

        NPCRegistry.Register(this);
    }

    void OnDestroy() => NPCRegistry.Unregister(this);

    void Update()
    {
        // While held at gunpoint, keep TURNING TO FACE the captor. A one-shot snap on
        // entering Held goes stale the moment the guardian repositions (human-shield
        // tracking), which left the hostage kneeling with its back to the gun.
        if (currentState == HostageState.Held && _captor != null)
        {
            Vector3 toCaptor = _captor.transform.position - transform.position;
            toCaptor.y = 0f;
            if (toCaptor.sqrMagnitude > 0.01f)
            {
                Quaternion look = Quaternion.LookRotation(toCaptor);
                transform.rotation = Quaternion.Slerp(transform.rotation, look, Time.deltaTime * 6f);
            }
        }

        // Velocity-driven locomotion blend: walk while moving (e.g. following the
        // trainee), idle when still. Measures real movement so it never glides.
        if (_animator != null)
        {
            Vector3 d = transform.position - _lastAnimPos;
            d.y = 0f;
            float speed = Time.deltaTime > 0f ? d.magnitude / Time.deltaTime : 0f;
            float norm  = Mathf.Clamp01(speed / Mathf.Max(0.01f, walkAnimReferenceSpeed));
            if (_hasSpeedParam)   _animator.SetFloat(_animSpeed, norm, 0.12f, Time.deltaTime);
            if (_hasInjuredParam) _animator.SetBool(_animInjured, _injured);
        }
        _lastAnimPos = transform.position;

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
        if (prev == HostageState.Held)
        {
            _threatened = false;
            if (_hasThreatenedParam) _animator?.SetBool(_animThreatened, false);
            if (next != HostageState.Down)
            {
                if (_hasHeldParam) _animator?.SetBool(_animHeld, false);
                else               scareController?.SetScared(false);
                if (_agent != null && _agent.isActiveAndEnabled) _agent.isStopped = false;
            }
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

            case HostageState.Held:
                // Seized as leverage — kneel at gunpoint, frozen in place.
                if (_agent != null && _agent.isActiveAndEnabled)
                {
                    _agent.isStopped = true;
                    _agent.ResetPath();
                }
                if (_hasHeldParam)
                {
                    _animator?.SetBool(_animHeld, true);
                    // CRITICAL: drop the Scared flag. Coming from Fearful (a gunshot) leaves
                    // Scared=true, and the animator's AnyState→Scared then keeps yanking him
                    // out of the kneel while AnyState→Held pulls him back — the kneel/stand
                    // "blinking". Held owns the pose now, so Scared must be off.
                    scareController?.SetScared(false);
                    _animator?.SetBool(_animScared, false);
                }
                else scareController?.SetScared(true);
                // Face the captor if we have one, so the "at gunpoint" read is clear.
                if (_captor != null)
                {
                    Vector3 look = _captor.transform.position; look.y = transform.position.y;
                    if ((look - transform.position).sqrMagnitude > 0.01f)
                        transform.rotation = Quaternion.LookRotation(look - transform.position);
                }
                break;

            case HostageState.Calm:
                scareController?.SetScared(false);
                runawayController?.OnGunfire(false);
                break;

            case HostageState.Freed:
                // Runaway controller has already settled at the hiding spot.
                break;

            case HostageState.Down:
                // Shot dead (training failure). Stop everything, play death, and
                // turn the body into a non-blocking corpse.
                scareController?.SetScared(false);
                if (_hasHeldParam) _animator?.SetBool(_animHeld, false);
                if (_freezeCheckRoutine != null) { StopCoroutine(_freezeCheckRoutine); _freezeCheckRoutine = null; }
                if (_agent != null && _agent.isActiveAndEnabled)
                {
                    _agent.isStopped = true;
                    _agent.ResetPath();
                    _agent.enabled = false;
                }
                _animator?.SetTrigger("Death");
                foreach (var col in GetComponentsInChildren<Collider>())
                    if (col != null && !col.isTrigger) col.enabled = false;
                // Notify listeners once (Module4SessionController → immediate FAIL).
                if (!_killedFired)
                {
                    _killedFired = true;
                    OnKilled?.Invoke(this);
                }
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
            // Follow the player's ACTUAL position. In VR the head/camera moves with
            // the player while the rig root often stays at the world origin — so we
            // track Camera.main (the head) when available, and only fall back to the
            // stored instigator transform if there's no main camera. This is the fix
            // for "the hostage walks off instead of following me."
            Transform tgt = Camera.main != null ? Camera.main.transform : _followTarget;
            if (tgt != null)
            {
                Vector3 targetPos = tgt.position;
                // Re-path whenever you've moved ~0.5 m so it keeps up closely.
                if (Vector3.Distance(targetPos, lastDestination) > 0.5f)
                {
                    _agent.SetDestination(targetPos);
                    lastDestination = targetPos;
                }
            }
            yield return new WaitForSeconds(0.2f);
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
