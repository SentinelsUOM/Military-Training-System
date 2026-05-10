using System.Collections;
using UnityEngine;
using UnityEngine.AI;

public enum IdleMode { Patrol, Wander, Static }

/// <summary>
/// Full terrorist FSM controller with all Module 2 transitions.
///
/// States
///   Idle       — patrolling normally
///   Suspicious — heard something; stopped, looking toward sound source
///   Alert      — confirmed threat; holding position, scanning
///   TakeCover  — moving to / sheltering at a CoverPoint
///   Engage     — actively firing at the player
///   Down       — terminal; health ≤ 0
///
/// Transitions (per design spec)
///   Idle       + GunshotHeard              → Suspicious
///   Suspicious + PlayerSeen                → Alert
///   Alert      + TargetConfirmed           → Engage
///   Engage     + PlayerLost                → Alert
///   Engage     + AllyDownSeen / TerroristDown (squad LOS) → Engage (squad override — no-op if already Engage)
///   Any active + TerroristHit health ≤ 0   → Down  (via TakeHit())
///   Any active + RoomBreached / DoorOpened → Alert
///
/// Alert propagation is delegated to AlertPropagator (3-ring model).
/// Perception (vision + auditory) is handled by PerceptionController on this same GameObject.
/// Telemetry is emitted on every state transition via TelemetryLogger.
///
/// Setup
///   1. Add to the terrorist NPC GameObject.
///   2. Assign patrolLine, shooter, and optionally agent + animator in the Inspector.
///   3. Set role (Guard / Roamer / Leader) and squadId.
/// </summary>
public class TerroristController : MonoBehaviour, INPCResponder
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Existing NPC Components (drag from same GameObject)")]
    public PatrolLine        patrolLine;
    public NpcShooterRaycast shooter;

    [Tooltip("NavMeshAgent used for moving to cover. Optional — TakeCover is skipped if absent.")]
    public NavMeshAgent agent;

    [Header("Idle Behaviour")]
    [Tooltip("Patrol  — follows PatrolLine (Guard)\n" +
             "Wander  — picks random NavMesh points (Roamer)\n" +
             "Static  — stands at spawn position (Leader)")]
    public IdleMode idleMode = IdleMode.Patrol;

    [Tooltip("Wander only — max distance from spawn that the NPC will wander.")]
    public float wanderRadius = 12f;

    [Tooltip("Wander only — seconds to pause at each waypoint before picking the next one.")]
    public float wanderPauseTime = 2f;

    [Tooltip("Static only — the transform this NPC faces while standing still.\n" +
             "Leave empty to keep spawn rotation.")]
    public Transform staticFaceTarget;

    [Tooltip("Animator on this NPC (used to trigger death animation). Optional.")]
    public Animator animator;

    [Tooltip("Vertical offset applied after death to make the body lie flat on the floor.\n" +
             "If the body floats above the floor → set this to a NEGATIVE number (e.g. -0.3).\n" +
             "If the body sinks into the floor → set this to a POSITIVE number (e.g. 0.1).\n" +
             "Adjust until the body sits visually correctly on the ground.")]
    public float deathFloorOffset = 0f;

    [Header("Role & Squad")]
    [Tooltip("Tactical role — drives roleBonus in NPCSelector.")]
    public NPCRole role = NPCRole.Guard;

    [Tooltip("Squad identifier assigned from Scenario JSON at spawn time. Leave blank = no squad.")]
    public string squadId = "";

    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Selection Tuning")]
    [Tooltip("Seconds before this NPC can be selected again for any event.")]
    public float responseCooldown = 4f;

    [Header("Engage Tuning")]
    [Tooltip("PlayerSeen events closer than this escalate directly to Engage. 0 = disabled.")]
    public float engageDistance = 5f;

    [Tooltip("XR Main Camera — used as shoot target when Engage is triggered by PlayerSeen.\n" +
             "If left empty the event instigator's Camera is used instead.")]
    public Transform playerCamera;

    [Header("Suspicious Timeout")]
    [Tooltip("Seconds in Suspicious state before auto-returning to Idle if nothing escalates.")]
    public float suspiciousTimeout = 6f;

    [Header("Hearing")]
    [Tooltip("Radius within which this NPC can hear a GunshotHeard event.")]
    public float hearingRange = 20f;

    [Header("Debug — read-only in Play mode")]
    public TerroristState currentState = TerroristState.Idle;
    public float          currentHealth;

    // ── INPCResponder ─────────────────────────────────────────────────────────

    public string  NPCId            => gameObject.name;
    public NPCRole Role             => role;
    public Vector3 Position         => transform.position;
    public float   LastResponseTime => _lastResponseTime;

    /// State readiness per design spec (0.0–1.0). Down is hard-filtered in CanRespond.
    public float StateScore => currentState switch
    {
        TerroristState.Idle       => 1.0f,
        TerroristState.Suspicious => 0.8f,
        TerroristState.Alert      => 0.5f,
        TerroristState.TakeCover  => 0.2f,
        TerroristState.Engage     => 0.0f,
        _                          => 0.0f,
    };

    public bool CanRespond(ScenarioEvent e)
    {
        // Terminal — no response ever
        if (currentState == TerroristState.Down) return false;

        // Don't respond to own weapon fire
        if (e.Instigator == gameObject &&
            e.Type != ScenarioEventType.PlayerSeen &&
            e.Type != ScenarioEventType.TargetConfirmed &&
            e.Type != ScenarioEventType.PlayerLost)
            return false;

        // Engaged NPCs are committed — only squad-loss override is allowed
        if (currentState == TerroristState.Engage)
        {
            return e.Type == ScenarioEventType.AllyDownSeen ||
                   e.Type == ScenarioEventType.TerroristDown;
        }

        // Squad-death events always bypass cooldown — every active NPC must react
        if (e.Type == ScenarioEventType.TerroristDown) return true;

        // Cooldown applies to every other event
        if (Time.time - _lastResponseTime < responseCooldown) return false;

        // Filter by event type
        switch (e.Type)
        {
            case ScenarioEventType.GunshotHeard:
                // Auditory range check
                return Vector3.Distance(transform.position, e.Origin) <= hearingRange;

            case ScenarioEventType.RoomBreached:
            case ScenarioEventType.DoorOpened:
            case ScenarioEventType.PlayerSeen:
            case ScenarioEventType.TargetConfirmed:
            case ScenarioEventType.PlayerLost:
            case ScenarioEventType.AllyDownSeen:
            case ScenarioEventType.TerroristDown:
                return true;

            default:
                return false;
        }
    }

    public void RespondTo(ScenarioEvent e)
    {
        _lastResponseTime = Time.time;

        switch (e.Type)
        {
            // ── Auditory ──────────────────────────────────────────────────────
            case ScenarioEventType.GunshotHeard:
                SetLookTarget(e.Origin);
                if (currentState == TerroristState.Idle)
                    TransitionTo(TerroristState.Suspicious, e);
                else if (currentState != TerroristState.Engage)
                    TransitionTo(TerroristState.Alert, e);
                break;

            // ── Spatial ───────────────────────────────────────────────────────
            case ScenarioEventType.RoomBreached:
            case ScenarioEventType.DoorOpened:
                SetLookTarget(e.Origin);
                if (currentState != TerroristState.Engage)
                    TransitionTo(TerroristState.Alert, e);
                break;

            // ── Vision: first sight ───────────────────────────────────────────
            case ScenarioEventType.PlayerSeen:
            {
                var cam = ResolvePlayerCamera(e);
                if (cam != null) { _lastSeenPlayer = cam; SetLookTarget(cam.position); }
                else             { SetLookTarget(e.Origin); }

                if (currentState == TerroristState.Idle ||
                    currentState == TerroristState.Suspicious)
                    TransitionTo(TerroristState.Alert, e);
                break;
            }

            // ── Vision: sustained contact → commit to Engage ──────────────────
            case ScenarioEventType.TargetConfirmed:
            {
                var cam = ResolvePlayerCamera(e);
                if (cam != null) { _lastSeenPlayer = cam; SetLookTarget(cam.position); }
                else             { SetLookTarget(e.Origin); }

                if (currentState == TerroristState.Alert ||
                    currentState == TerroristState.Suspicious)
                    TransitionTo(TerroristState.Engage, e);
                break;
            }

            // ── Vision lost ────────────────────────────────────────────────────
            case ScenarioEventType.PlayerLost:
                if (currentState == TerroristState.Engage)
                    TransitionTo(TerroristState.Alert, e);
                break;

            // ── Squad alert ────────────────────────────────────────────────────
            case ScenarioEventType.AllyDownSeen:
                // Squadmate started shooting — raise readiness but wait for personal LOS
                if (currentState == TerroristState.Idle ||
                    currentState == TerroristState.Suspicious)
                    TransitionTo(TerroristState.Alert, e);
                break;

            case ScenarioEventType.TerroristDown:
                if (currentState == TerroristState.Down) break;
                Debug.Log($"[TerroristController] {gameObject.name} received TerroristDown | " +
                          $"currentState={currentState} | personallyConfirmed={_personallyConfirmedPlayer}");
                if (_personallyConfirmedPlayer)
                {
                    // This NPC personally saw and confirmed the player — engage immediately
                    if (currentState != TerroristState.Engage)
                        TransitionTo(TerroristState.Engage, e);
                }
                else
                {
                    // Never personally saw the player — go to Alert so EventManager sends one to investigate
                    if (currentState == TerroristState.Idle ||
                        currentState == TerroristState.Suspicious ||
                        currentState == TerroristState.Alert)
                        TransitionTo(TerroristState.Alert, e);
                }
                break;
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Apply damage.  When health reaches 0, transitions to Down.
    /// Call this from your damage/hit detection code (e.g. HitBox.cs).
    /// </summary>
    public void TakeHit(float damage)
    {
        if (currentState == TerroristState.Down) return;

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        if (currentHealth <= 20f)
        {
            TransitionTo(TerroristState.Down, null);
        }
    }

    /// <summary>
    /// Called by PerceptionController when it confirms the player's position.
    /// Bypasses CanRespond so the owning NPC always reacts to its own perception.
    /// </summary>
    public void HandleDetection(ScenarioEvent e)
    {
        if (currentState == TerroristState.Down) return;
        _lastResponseTime = Time.time;

        switch (e.Type)
        {
            case ScenarioEventType.PlayerSeen:
            {
                var cam = ResolvePlayerCamera(e);
                if (cam != null) { _lastSeenPlayer = cam; SetLookTarget(cam.position); }
                else             { SetLookTarget(e.Origin); }

                if (currentState == TerroristState.Idle ||
                    currentState == TerroristState.Suspicious)
                    TransitionTo(TerroristState.Alert, e);
                break;
            }
            case ScenarioEventType.TargetConfirmed:
            {
                var cam = ResolvePlayerCamera(e);
                if (cam != null) { _lastSeenPlayer = cam; SetLookTarget(cam.position); }
                _personallyConfirmedPlayer = true; // own eyes confirmed the target
                if (currentState == TerroristState.Alert ||
                    currentState == TerroristState.Suspicious)
                    TransitionTo(TerroristState.Engage, e);
                break;
            }
            case ScenarioEventType.PlayerLost:
                if (currentState == TerroristState.Engage)
                    TransitionTo(TerroristState.Alert, e);
                break;
        }
    }

    // ── Private state ─────────────────────────────────────────────────────────

    float     _lastResponseTime = -99f;
    Transform _lookAnchor;
    Transform _lastSeenPlayer;
    Coroutine _suspiciousRoutine;
    Coroutine _investigateRoutine;
    Coroutine _wanderRoutine;
    bool      _wasFiring;
    bool      _personallyConfirmedPlayer; // true only when own PerceptionController fired TargetConfirmed
    CoverPoint _claimedCover;
    Vector3   _spawnPosition;
    Quaternion _spawnRotation;
    float     _originalAgentSpeed;
    bool      _isInvestigating;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        currentHealth = maxHealth;
        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;

        // Reusable world-space anchor so PatrolLine.StopAndLook() always gets a valid target.
        var go = new GameObject($"[LookAnchor] {gameObject.name}");
        _lookAnchor = go.transform;

        if (agent != null) _originalAgentSpeed = agent.speed;

        NPCRegistry.Register(this);

        // Register with squad if an ID was provided.
        if (!string.IsNullOrEmpty(squadId))
            Squad.GetOrCreate(squadId).AddMember(this);
    }

    void Start()
    {
        // Disable PatrolLine entirely for non-Patrol NPCs so it never moves the transform.
        if (idleMode != IdleMode.Patrol && patrolLine != null)
            patrolLine.enabled = false;

        // Kick off the correct initial idle behaviour.
        StartIdleBehaviour();
    }

    void StartIdleBehaviour()
    {
        switch (idleMode)
        {
            case IdleMode.Patrol:
                patrolLine?.ResumePatrol();
                break;

            case IdleMode.Wander:
                if (_wanderRoutine != null) StopCoroutine(_wanderRoutine);
                _wanderRoutine = StartCoroutine(WanderRoutine());
                break;

            case IdleMode.Static:
                if (agent != null && agent.isActiveAndEnabled) agent.ResetPath();
                if (staticFaceTarget != null)
                    transform.LookAt(new Vector3(
                        staticFaceTarget.position.x,
                        transform.position.y,
                        staticFaceTarget.position.z));
                break;
        }
    }

    void OnDestroy()
    {
        NPCRegistry.Unregister(this);

        if (!string.IsNullOrEmpty(squadId))
            Squad.Get(squadId)?.RemoveMember(this);

        _claimedCover?.Release();
        _claimedCover = null;

        if (_lookAnchor != null) Destroy(_lookAnchor.gameObject);
    }

    void Update()
    {
        if (currentState == TerroristState.Down) return;

        // Keep look anchor tracking the player's live position every frame.
        if (_lastSeenPlayer != null && currentState != TerroristState.Idle && _lookAnchor != null)
            _lookAnchor.position = new Vector3(
                _lastSeenPlayer.position.x, transform.position.y, _lastSeenPlayer.position.z);

        // Wander/Static NPCs have PatrolLine disabled so we rotate them ourselves.
        // Patrol NPCs use PatrolLine.StopAndLook which reads _lookAnchor, but we also
        // handle rotation here so all three idle modes track the player smoothly.
        // Skip during investigation — the InvestigateRoutine controls rotation for searching.
        if (currentState != TerroristState.Idle && _lookAnchor != null && !_isInvestigating)
        {
            Vector3 dir = _lookAnchor.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion tgt = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, tgt, Time.deltaTime * 6f);
            }
        }

        if (shooter == null) return;

        bool isFiring = shooter.IsFiring;

        // Keep Engage state in sync with the physical shooter component.
        if (isFiring && !_wasFiring && currentState != TerroristState.Engage)
        {
            currentState = TerroristState.Engage;
            Debug.Log($"[TerroristController] {gameObject.name}: → Engage (shooter started)");
        }
        else if (!isFiring && _wasFiring && currentState == TerroristState.Engage)
        {
            currentState = TerroristState.Alert;
            Debug.Log($"[TerroristController] {gameObject.name}: → Alert (shooter stopped)");
        }

        _wasFiring = isFiring;
    }

    // ── State machine ─────────────────────────────────────────────────────────

    void TransitionTo(TerroristState next, ScenarioEvent trigger)
    {
        if (next == currentState) return;

        var prev = currentState;
        currentState = next;

        // ── Telemetry (must be first) ─────────────────────────────────────────
        TelemetryLogger.Instance?.LogStateChange(
            NPCId, "Terrorist", prev.ToString(), next.ToString(), trigger);

        Debug.Log($"[TerroristController] {gameObject.name}: {prev} → {next}");

        // ── Cancel pending Suspicious timeout ─────────────────────────────────
        if (_suspiciousRoutine != null)
        {
            StopCoroutine(_suspiciousRoutine);
            _suspiciousRoutine = null;
        }

        // ── Cancel any active investigation walk ──────────────────────────────
        if (_investigateRoutine != null)
        {
            StopCoroutine(_investigateRoutine);
            _investigateRoutine = null;
            _isInvestigating = false;
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.speed = _originalAgentSpeed;
                agent.ResetPath();
            }
        }

        // ── Cancel wander when leaving Idle ──────────────────────────────────
        if (prev == TerroristState.Idle && _wanderRoutine != null)
        {
            StopCoroutine(_wanderRoutine);
            _wanderRoutine = null;
            if (agent != null && agent.isActiveAndEnabled) agent.ResetPath();
        }

        // ── Release cover if leaving TakeCover ───────────────────────────────
        if (prev == TerroristState.TakeCover && next != TerroristState.TakeCover)
        {
            _claimedCover?.Release();
            _claimedCover = null;
        }

        // ── Behaviour for new state ────────────────────────────────────────────
        switch (next)
        {
            case TerroristState.Suspicious:
                patrolLine?.StopAndLook(_lookAnchor);
                animator?.SetBool("Alert", false);
                _suspiciousRoutine = StartCoroutine(SuspiciousTimeout());
                break;

            case TerroristState.Alert:
                patrolLine?.StopAndLook(_lookAnchor);
                animator?.SetBool("Alert", true);
                // Trigger Ring 1+2 alert propagation
                AlertPropagator.Instance?.Broadcast(this, trigger);
                break;

            case TerroristState.TakeCover:
                patrolLine?.StopAndLook(_lookAnchor);
                StartCoroutine(MoveTocover(trigger));
                break;

            case TerroristState.Engage:
                patrolLine?.StopAndLook(_lookAnchor);
                if (shooter != null)
                {
                    Debug.Log($"[TerroristController] {gameObject.name} entering Engage | " +
                              $"_lastSeenPlayer={((_lastSeenPlayer != null) ? _lastSeenPlayer.name : "NULL")} | " +
                              $"shooter.target={(shooter.target != null ? shooter.target.name : "NULL")} | " +
                              $"trigger={trigger?.Type.ToString() ?? "none"}");
                    if (_lastSeenPlayer != null) shooter.target = _lastSeenPlayer;
                    shooter.StartFiring();
                }
                // Trigger Ring 1+2 alert propagation
                AlertPropagator.Instance?.Broadcast(this, trigger);
                break;

            case TerroristState.Idle:
                switch (idleMode)
                {
                    case IdleMode.Patrol:
                        patrolLine?.ResumePatrol();
                        break;

                    case IdleMode.Wander:
                        animator?.SetBool("Alert", false);
                        if (_wanderRoutine != null) StopCoroutine(_wanderRoutine);
                        _wanderRoutine = StartCoroutine(WanderRoutine());
                        break;

                    case IdleMode.Static:
                        animator?.SetBool("Alert", false);
                        if (agent != null && agent.isActiveAndEnabled) agent.ResetPath();
                        if (staticFaceTarget != null)
                            transform.LookAt(new Vector3(
                                staticFaceTarget.position.x,
                                transform.position.y,
                                staticFaceTarget.position.z));
                        break;
                }
                break;

            case TerroristState.Down:
                EnterDownState(trigger);
                break;
        }
    }

    // ── Down state ────────────────────────────────────────────────────────────

    void EnterDownState(ScenarioEvent trigger)
    {
        // Stop all combat
        shooter?.StopFiring();

        // Stop ALL active routines so the body doesn't keep wandering / patrolling / investigating.
        StopAllCoroutines();
        _wanderRoutine = null;
        _investigateRoutine = null;
        _suspiciousRoutine = null;
        _isInvestigating = false;

        // Fully disable PatrolLine — StopAndLook only pauses it, the component can still drive movement.
        if (patrolLine != null)
        {
            patrolLine.StopAndLook(_lookAnchor);
            patrolLine.enabled = false;
        }

        // Disable NavMeshAgent and correct the base-offset elevation it was applying.
        // Without this, the root sits above the floor and the death animation plays in mid-air.
        if (agent != null && agent.isActiveAndEnabled)
        {
            float baseOffset = agent.baseOffset;
            agent.isStopped = true;
            agent.ResetPath();
            agent.enabled   = false;
            // Drop the root to true floor height now that the agent is no longer lifting it.
            transform.position -= new Vector3(0f, baseOffset, 0f);
        }

        // Snap immediately to the floor so the body doesn't appear to die in mid-air.
        // Project is single-floor (Y=0 per CLAUDE.md), but we raycast first to handle any geometry.
        SnapToFloor();

        // Disable only hitbox components so bullets no longer register.
        // TakeHit() already guards Down state, but this removes the overhead.
        foreach (var hb in GetComponentsInChildren<NPCHitBox>())
            hb.enabled = false;

        // Trigger death animation then freeze in final pose.
        // applyRootMotion stays true so the animation's own root curve carries
        // the body to the floor — no need for a Rigidbody.
        if (animator != null)
        {
            animator.SetBool("Alert", false);
            animator.SetTrigger("Death");
            StartCoroutine(FreezeAfterDeath());
        }

        // Raise TerroristDown event for squad propagation
        var downEvent = new ScenarioEvent(
            ScenarioEventType.TerroristDown,
            transform.position,
            gameObject,
            targetActorId: NPCId);

        EventManager.Instance?.Raise(downEvent);

        // Notify squad — AlertPropagator handles Ring 1 Engage
        if (!string.IsNullOrEmpty(squadId))
            Squad.Get(squadId)?.NotifyMemberDown(this, downEvent);
    }

    /// <summary>
    /// Snaps the NPC's transform down to the floor. Excludes own colliders so the raycast
    /// doesn't hit the NPC's own body and stop short.
    /// </summary>
    void SnapToFloor()
    {
        // Disable own colliders for the raycast so we don't hit ourselves.
        var ownColliders = GetComponentsInChildren<Collider>();
        var savedStates = new bool[ownColliders.Length];
        for (int i = 0; i < ownColliders.Length; i++)
        {
            savedStates[i] = ownColliders[i].enabled;
            ownColliders[i].enabled = false;
        }

        Vector3 origin = transform.position + Vector3.up * 1.5f;
        float floorY;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 5f, ~0,
                            QueryTriggerInteraction.Ignore))
        {
            floorY = hit.point.y;
        }
        else
        {
            // Fallback — single-floor project, snap to Y=0
            floorY = 0f;
        }
        transform.position = new Vector3(transform.position.x, floorY + deathFloorOffset, transform.position.z);

        // Restore collider states (hitboxes are disabled separately later, but other colliders stay on).
        for (int i = 0; i < ownColliders.Length; i++)
            ownColliders[i].enabled = savedStates[i];
    }

    IEnumerator FreezeAfterDeath()
    {
        // Wait one frame for the Death trigger to register, then wait until
        // the animator enters the Death state before measuring its length.
        yield return null;
        float waited = 0f;
        while (waited < 0.3f && !animator.GetCurrentAnimatorStateInfo(0).IsTag("Death"))
        {
            waited += Time.deltaTime;
            yield return null;
        }
        float clipLength = animator.GetCurrentAnimatorStateInfo(0).length;
        // Clamp: some death clips are set to loop — treat anything > 5 s as 3 s
        yield return new WaitForSeconds(Mathf.Clamp(clipLength, 0.5f, 5f));

        // Disable animator FIRST so the pose is frozen — then snap. Otherwise the animator
        // can still drive root motion this frame and offset our snap.
        animator.enabled = false;
        yield return null; // wait one frame for the disable to fully take effect

        SnapToFloor();
    }

    // ── Cover movement ────────────────────────────────────────────────────────

    IEnumerator MoveTocover(ScenarioEvent trigger)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            // No NavMesh — fall back to Alert
            TransitionTo(TerroristState.Alert, trigger);
            yield break;
        }

        // Determine threat direction from the last known player position.
        Vector3 threatDir = _lastSeenPlayer != null
            ? (_lastSeenPlayer.position - transform.position)
            : (trigger?.Origin ?? transform.position) - transform.position;

        var cover = CoverPoint.SelectBest(transform.position, threatDir);
        if (cover == null)
        {
            TransitionTo(TerroristState.Alert, trigger);
            yield break;
        }

        cover.Claim();
        _claimedCover = cover;
        agent.SetDestination(cover.transform.position);

        // Wait until the agent arrives (or state changes)
        while (currentState == TerroristState.TakeCover &&
               (!agent.pathPending) &&
               agent.remainingDistance > agent.stoppingDistance)
        {
            yield return null;
        }

        // Arrived — enable peek / scan behaviour (Alert while in cover)
        if (currentState == TerroristState.TakeCover)
            TransitionTo(TerroristState.Alert, trigger);
    }

    // ── Suspicious timeout ────────────────────────────────────────────────────

    IEnumerator SuspiciousTimeout()
    {
        yield return new WaitForSeconds(suspiciousTimeout);
        if (currentState == TerroristState.Suspicious)
            TransitionTo(TerroristState.Idle, null);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    void SetLookTarget(Vector3 worldPos)
    {
        if (_lookAnchor != null)
            _lookAnchor.position = new Vector3(worldPos.x, transform.position.y, worldPos.z);
    }

    Transform ResolvePlayerCamera(ScenarioEvent e)
    {
        if (playerCamera != null) return playerCamera;
        if (e?.Instigator == null) return null;
        var cam = e.Instigator.GetComponentInChildren<Camera>();
        return cam != null ? cam.transform : e.Instigator.transform;
    }

    // ── Wander ────────────────────────────────────────────────────────────────

    IEnumerator WanderRoutine()
    {
        while (currentState == TerroristState.Idle)
        {
            // Pick a random point on the NavMesh within wanderRadius of spawn
            Vector3 randomDir = Random.insideUnitSphere * wanderRadius;
            randomDir.y = 0f;
            Vector3 target = _spawnPosition + randomDir;

            if (NavMesh.SamplePosition(target, out NavMeshHit hit, wanderRadius, NavMesh.AllAreas))
            {
                if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                {
                    agent.SetDestination(hit.position);

                    // Wait until arrived
                    while (currentState == TerroristState.Idle &&
                           (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.3f))
                        yield return null;
                }
            }

            // Pause at this spot
            float pause = wanderPauseTime + Random.Range(-0.5f, 0.5f);
            yield return new WaitForSeconds(pause);
        }

        _wanderRoutine = null;
    }

    // ── Investigate ───────────────────────────────────────────────────────────

    [Header("Investigation Tuning")]
    [Tooltip("Walk speed while approaching the gunshot location (normal speed is used otherwise).")]
    public float investigateSpeed = 1.5f;

    [Tooltip("Distance from destination at which the NPC raises its weapon (gun-ready stance).")]
    public float gunReadyDistance = 4f;

    [Tooltip("Number of directions to look at on arrival (rotation scan).")]
    public int searchCheckPoints = 4;

    [Tooltip("Seconds to pause and look at each search direction.")]
    public float searchPausePerDirection = 1.0f;

    [Tooltip("Degrees of rotation per second while turning to check a direction.")]
    public float searchTurnSpeed = 90f;

    [Tooltip("Number of nearby points to walk to and check after the initial scan (other rooms / areas).")]
    public int searchWaypointCount = 3;

    [Tooltip("Minimum distance from the gunshot location for a search waypoint (so the NPC actually moves).")]
    public float searchWaypointMinRadius = 4f;

    [Tooltip("Maximum distance from the gunshot location for a search waypoint.")]
    public float searchWaypointMaxRadius = 10f;

    [Tooltip("Seconds to scan each search waypoint before moving to the next.")]
    public float searchWaypointPauseTime = 2f;

    /// <summary>
    /// Walk to soundPos and scan. Called by EventManager on the selected responder.
    /// Only acts if the NPC has a NavMeshAgent and is currently Suspicious.
    /// </summary>
    public void InvestigatePosition(Vector3 soundPos)
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        // Allow both Suspicious (gunshot) and Alert (squad-member down) investigations
        if (currentState != TerroristState.Suspicious && currentState != TerroristState.Alert) return;

        if (_investigateRoutine != null) StopCoroutine(_investigateRoutine);
        _investigateRoutine = StartCoroutine(InvestigateRoutine(soundPos));
    }

    IEnumerator InvestigateRoutine(Vector3 soundPos)
    {
        Debug.Log($"[TerroristController] {gameObject.name}: investigating position at {soundPos}");

        var startState = currentState;
        _isInvestigating = true;

        // ── Phase 1: Cautious walk toward the location (no gun) ───────────────
        // They only know a gunshot came from there. Normal walk, weapon lowered.
        agent.speed = investigateSpeed;
        animator?.SetBool("Alert", false);
        agent.SetDestination(soundPos);

        bool gunRaised = false;
        while (currentState == startState)
        {
            // Reached the gunshot location
            if (!agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.5f)
                break;

            // ── Phase 1b: Approaching the area — raise weapon to gun-ready ────
            // Player could be in this room, get ready before stepping in.
            if (!gunRaised && !agent.pathPending && agent.remainingDistance <= gunReadyDistance)
            {
                animator?.SetBool("Alert", true);
                gunRaised = true;
                Debug.Log($"[TerroristController] {gameObject.name}: nearing investigation area — weapon ready");
            }

            yield return null;
        }

        if (currentState != startState) { EndInvestigation(); yield break; }

        // Make sure the gun is up by the time we start searching
        if (!gunRaised) animator?.SetBool("Alert", true);
        agent.ResetPath();

        // ── Phase 2: Quick rotation scan at the gunshot location ──────────────
        Debug.Log($"[TerroristController] {gameObject.name}: arrived — scanning area with weapon ready");

        yield return StartCoroutine(ScanRotation(startState));
        if (currentState != startState) { EndInvestigation(); yield break; }

        // ── Phase 3: Search nearby points (other rooms, doorways, behind cover)
        Debug.Log($"[TerroristController] {gameObject.name}: searching surrounding area");

        for (int i = 0; i < searchWaypointCount; i++)
        {
            if (currentState != startState) { EndInvestigation(); yield break; }

            // Pick a random point on the NavMesh AT LEAST searchWaypointMinRadius away from the
            // gunshot location, so the NPC actually moves to a different area instead of pacing
            // in place. Direction is random; distance is uniform between min and max radius.
            Vector2 dir2D = Random.insideUnitCircle.normalized;
            if (dir2D.sqrMagnitude < 0.01f) dir2D = Vector2.up; // safety
            float distance = Random.Range(searchWaypointMinRadius, searchWaypointMaxRadius);
            Vector3 candidate = soundPos + new Vector3(dir2D.x, 0f, dir2D.y) * distance;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, searchWaypointMaxRadius, NavMesh.AllAreas))
                continue;

            Debug.Log($"[TerroristController] {gameObject.name}: searching waypoint {i + 1}/{searchWaypointCount} at {hit.position}");

            // Walk slowly to this search point — gun stays ready
            agent.SetDestination(hit.position);
            while (currentState == startState &&
                   (agent.pathPending || agent.remainingDistance > agent.stoppingDistance + 0.5f))
                yield return null;

            if (currentState != startState) { EndInvestigation(); yield break; }

            // Pause and look around at this checkpoint
            agent.ResetPath();
            float waited = 0f;
            while (waited < searchWaypointPauseTime && currentState == startState)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }

        // ── Phase 4: Nothing found — de-escalate ──────────────────────────────
        if (currentState == startState && startState == TerroristState.Suspicious)
        {
            Debug.Log($"[TerroristController] {gameObject.name}: area clear, returning to Idle");
            EndInvestigation();
            TransitionTo(TerroristState.Idle, null);
            yield break;
        }

        EndInvestigation();
    }

    /// <summary>
    /// Rotates the NPC to look at several directions around it, pausing at each.
    /// Returns false if the state changed (player detected) during the scan.
    /// </summary>
    IEnumerator ScanRotation(TerroristState startState)
    {
        float baseAngle = transform.eulerAngles.y;
        float angleStep = 360f / searchCheckPoints;

        for (int i = 0; i < searchCheckPoints; i++)
        {
            if (currentState != startState) yield break;

            float targetAngle = baseAngle + (angleStep * i) + Random.Range(-15f, 15f);
            Quaternion targetRot = Quaternion.Euler(0f, targetAngle, 0f);

            while (Quaternion.Angle(transform.rotation, targetRot) > 2f)
            {
                if (currentState != startState) yield break;
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, targetRot, searchTurnSpeed * Time.deltaTime);
                yield return null;
            }

            float pause = searchPausePerDirection + Random.Range(-0.2f, 0.2f);
            float waited = 0f;
            while (waited < pause)
            {
                if (currentState != startState) yield break;
                waited += Time.deltaTime;
                yield return null;
            }
        }
    }

    /// <summary>
    /// Restores original agent speed and clears the investigation flag.
    /// Called when investigation ends normally or is interrupted by state change.
    /// </summary>
    void EndInvestigation()
    {
        _isInvestigating = false;
        _investigateRoutine = null;
        if (agent != null && agent.isActiveAndEnabled)
            agent.speed = _originalAgentSpeed;
    }

    // ── Inspector test helpers ────────────────────────────────────────────────

    [ContextMenu("TEST → GunshotHeard")]
    void TestGunshotHeard()
    {
        EventManager.Instance?.Raise(new ScenarioEvent(
            ScenarioEventType.GunshotHeard, transform.position + Vector3.forward * 8f));
    }

    [ContextMenu("TEST → PlayerSeen (far)")]
    void TestPlayerSeenFar()
    {
        EventManager.Instance?.Raise(new ScenarioEvent(
            ScenarioEventType.PlayerSeen, transform.position + Vector3.forward * 10f, gameObject));
    }

    [ContextMenu("TEST → TargetConfirmed")]
    void TestTargetConfirmed()
    {
        EventManager.Instance?.Raise(new ScenarioEvent(
            ScenarioEventType.TargetConfirmed, transform.position + Vector3.forward * 5f, gameObject));
    }

    [ContextMenu("TEST → PlayerLost")]
    void TestPlayerLost()
    {
        EventManager.Instance?.Raise(new ScenarioEvent(
            ScenarioEventType.PlayerLost, transform.position));
    }

    [ContextMenu("TEST → RoomBreached")]
    void TestRoomBreached()
    {
        EventManager.Instance?.Raise(new ScenarioEvent(
            ScenarioEventType.RoomBreached, transform.position));
    }

    [ContextMenu("TEST → TakeHit (50 damage)")]
    void TestTakeHit() => TakeHit(50f);

    [ContextMenu("TEST → TakeHit (fatal)")]
    void TestTakeFatalHit() => TakeHit(9999f);
}
