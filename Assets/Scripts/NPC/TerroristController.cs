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
///   Retreat    — health low; falling back away from the threat (once per life)
///   Down       — terminal; health ≤ 0
///
/// Transitions (per design spec)
///   Idle       + GunshotHeard              → Suspicious
///   Suspicious + PlayerSeen                → Alert
///   Alert      + TargetConfirmed           → Engage
///   Engage     + PlayerLost                → Alert
///   Engage     + AllyDownSeen / TerroristDown (squad LOS) → Engage (squad override — no-op if already Engage)
///   Engage/Alert/TakeCover + TerroristHit health ≤ retreatHealthThreshold → Retreat (once per life)
///   Retreat    + arrived at fallback point → Alert (re-engages via perception)
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

    [Header("Retreat Tuning")]
    [Tooltip("When a hit drops health to/below this value (but above the Down threshold), " +
             "the NPC falls back away from the threat. Set to 0 to disable retreating.")]
    public float retreatHealthThreshold = 40f;

    [Tooltip("How far (metres) the NPC tries to fall back from the threat.")]
    public float retreatDistance = 10f;

    [Tooltip("NavMeshAgent speed multiplier while retreating (1 = normal walk).")]
    public float retreatSpeedMultiplier = 1.5f;

    [Tooltip("Seconds to hold at the fallback point (or in place when no NavMeshAgent " +
             "is available) before returning to Alert.")]
    public float retreatHoldTime = 4f;

    [Header("Leader Coordination")]
    [Tooltip("Flank directive only — lateral offset (metres) from the threat position " +
             "that squad members move to when the Leader orders a Flank.")]
    public float flankOffset = 5f;

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

    [Header("Aim (Engage state)")]
    [Tooltip("Optional. Drag the gun mesh (or its parent transform) here. Used to read the gun's " +
             "current barrel direction so the chest bone can be rotated to make the gun point at " +
             "the player. If left blank, falls back to shooter.firePoint automatically.")]
    public Transform weaponPivot;

    [Tooltip("How fast the body rotates to face the player when alerted/engaged. Higher = snappier.")]
    public float aimRotationSpeed = 8f;

    [Tooltip("Maximum degrees the chest bone can rotate per frame to aim the gun. Prevents " +
             "extreme spine twists when the player is in an awkward position. 45° is a good balance.")]
    public float maxAimAngle = 45f;

    [Tooltip("Which humanoid bone to rotate for aim. Chest gives the cleanest 'rifle stock' aim. " +
             "UpperChest is more subtle; Spine swings the hips too. Leave on Chest for most rigs.")]
    public HumanBodyBones aimBoneType = HumanBodyBones.Chest;

    [Header("Death / Cleanup")]
    [Tooltip("If true, automatically positions the body so the mesh bottom sits on the floor " +
             "regardless of where the prefab's transform origin is. Uses renderer bounds. " +
             "Turn this OFF and use deathFloorOffset manually if you need fine control.")]
    public bool autoCalculateDeathOffset = true;

    [Tooltip("If true, disables all non-trigger colliders on death so alive NPCs can walk through " +
             "the body instead of being blocked when investigating. Recommended on.")]
    public bool clearCollidersOnDeath = true;

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
        TerroristState.Retreat    => 0.1f,
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

        // Retreating NPCs are also committed — the combat noise that caused the
        // retreat (gunshots, breaches) must not yank them back to Alert mid-fall-back.
        // Only squad-loss events break through; re-engage happens via own perception
        // after the retreat completes.
        if (currentState == TerroristState.Retreat)
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
                // Squadmate down — go to Alert and investigate. Do NOT auto-engage:
                // even if this NPC personally saw the player before, they should NOT shoot blindly.
                // PerceptionController will trigger Engage only when player is actually visible now.
                if (currentState == TerroristState.Idle ||
                    currentState == TerroristState.Suspicious ||
                    currentState == TerroristState.Alert)
                    TransitionTo(TerroristState.Alert, e);
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
            return;
        }

        // Wounded but not down — fall back from the threat. One retreat per life,
        // and only from combat-adjacent states (a patrolling NPC who somehow takes
        // a non-fatal hit shouldn't sprint away from nothing).
        if (retreatHealthThreshold > 0f &&
            currentHealth <= retreatHealthThreshold &&
            !_hasRetreated &&
            (currentState == TerroristState.Engage ||
             currentState == TerroristState.Alert  ||
             currentState == TerroristState.TakeCover))
        {
            _hasRetreated = true;
            TransitionTo(TerroristState.Retreat, null);
        }
    }

    /// <summary>
    /// Assigns the Module 2 combat role and squad at spawn time. Called by
    /// SceneBuilder AFTER Instantiate (i.e. after Awake has already run), so it
    /// must register squad membership itself — writing the squadId field alone
    /// would do nothing because Awake's registration already happened.
    /// </summary>
    public void AssignRoleAndSquad(NPCRole combatRole, string squad)
    {
        role = combatRole;

        if (string.IsNullOrEmpty(squad) || squad == squadId) return;

        // Leave the old squad (if Awake registered one from prefab defaults).
        if (!string.IsNullOrEmpty(squadId))
            Squad.Get(squadId)?.RemoveMember(this);

        squadId = squad;
        Squad.GetOrCreate(squadId).AddMember(this);
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
    Coroutine _retreatRoutine;
    bool      _hasRetreated;   // one retreat per life — reset only on (re)spawn
    bool      _wasFiring;
    bool      _personallyConfirmedPlayer; // true only when own PerceptionController fired TargetConfirmed
    CoverPoint _claimedCover;
    Vector3   _spawnPosition;
    Quaternion _spawnRotation;
    float     _originalAgentSpeed;
    bool      _isInvestigating;
    LeaderDirective _activeDirective;   // last directive received from squad Leader; null when none active
    Transform _aimBone;                 // cached chest/spine bone used to aim the upper body at the player

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

        // Cache the aim bone (Chest by default) so LateUpdate can rotate it without per-frame lookup.
        // Falls back through UpperChest → Chest → Spine in case the rig is missing some bones.
        // Non-humanoid rigs return null; aim falls back to weapon-pivot-only mode.
        if (animator != null && animator.isHuman)
        {
            _aimBone = animator.GetBoneTransform(aimBoneType);
            if (_aimBone == null) _aimBone = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (_aimBone == null) _aimBone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (_aimBone == null) _aimBone = animator.GetBoneTransform(HumanBodyBones.Spine);
        }

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
        // Skip during retreat — the NavMeshAgent rotates the body toward the escape path.
        if (currentState != TerroristState.Idle &&
            currentState != TerroristState.Retreat &&
            _lookAnchor != null && !_isInvestigating)
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

    /// <summary>
    /// Runs AFTER the Animator so our rotation overrides any animation root rotation.
    /// Two responsibilities:
    ///   1) Rotate the BODY horizontally (Y-axis) toward the player when alerted/engaged.
    ///   2) Rotate the CHEST BONE in 3D so the gun barrel points at the player camera.
    ///      Because the chest is the parent of (shoulders → arms → hands → gun), rotating
    ///      the chest carries the whole upper-body chain together — hands stay attached
    ///      to the gun, the gun stays attached to the hands, everything moves as one unit.
    ///
    /// This is "additive aim" on top of the animation: the animator plays its idle/fire
    /// clip normally, and after that runs we adjust the chest rotation just enough that
    /// the gun's forward direction points at the player.
    /// </summary>
    void LateUpdate()
    {
        if (currentState == TerroristState.Down) return;
        if (_lastSeenPlayer == null) return;

        bool engagedOrAlert =
            currentState == TerroristState.Engage ||
            currentState == TerroristState.Alert  ||
            currentState == TerroristState.TakeCover;

        // ── 1) Body Y-rotation toward player (horizontal facing) ──────────────
        if (engagedOrAlert && !_isInvestigating)
        {
            Vector3 toPlayer = _lastSeenPlayer.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > 0.001f)
            {
                Quaternion target = Quaternion.LookRotation(toPlayer);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, target, Time.deltaTime * aimRotationSpeed);
            }
        }

        // ── 2) Chest bone aim — rotates whole arm chain so gun points at player ────
        // The gun is a descendant of the chest (chest → shoulder → arm → forearm → hand
        // → weaponPivot → gun mesh). Rotating the chest carries everything together.
        if (currentState == TerroristState.Engage && _aimBone != null)
        {
            // Find the gun's current forward direction in world space.
            // Prefer the actual fire point (most accurate barrel direction); fall back
            // to weaponPivot, then to the aim bone's own forward as last resort.
            Transform gunRef = null;
            if (shooter != null && shooter.firePoint != null) gunRef = shooter.firePoint;
            else if (weaponPivot != null)                     gunRef = weaponPivot;

            Vector3 currentForward = gunRef != null ? gunRef.forward : _aimBone.forward;
            Vector3 desiredForward = (_lastSeenPlayer.position - _aimBone.position).normalized;

            if (currentForward.sqrMagnitude > 0.001f && desiredForward.sqrMagnitude > 0.001f)
            {
                // Rotation that maps current gun-forward → desired (player) direction.
                Quaternion delta = Quaternion.FromToRotation(currentForward, desiredForward);

                // Clamp magnitude so an awkward player position can't cause an extreme
                // spine twist that breaks the silhouette.
                delta = Quaternion.RotateTowards(Quaternion.identity, delta, maxAimAngle);

                // Apply additively to the chest's animator-driven rotation.
                _aimBone.rotation = delta * _aimBone.rotation;
            }
        }
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

        // ── Stop firing when leaving Engage ───────────────────────────────────
        // NPCs should ONLY shoot while they actually see the player. The moment
        // state leaves Engage (e.g. PlayerLost), kill the firing loop so they
        // don't keep shooting at a position where the player isn't.
        if (prev == TerroristState.Engage && next != TerroristState.Engage)
        {
            shooter?.StopFiring();
        }

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
            animator?.SetBool("Investigating", false);
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

        // ── Cancel retreat when leaving Retreat ──────────────────────────────
        if (prev == TerroristState.Retreat && _retreatRoutine != null)
        {
            StopCoroutine(_retreatRoutine);
            _retreatRoutine = null;
            if (agent != null && agent.isActiveAndEnabled)
            {
                agent.speed = _originalAgentSpeed;
                agent.ResetPath();
            }
        }

        // ── Release cover if leaving TakeCover / Retreat ─────────────────────
        if ((prev == TerroristState.TakeCover || prev == TerroristState.Retreat) &&
            next != TerroristState.TakeCover && next != TerroristState.Retreat)
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
                // Leader coordination: issue Converge directive to squad, then
                // follow any directive we currently hold (no-op for Leaders unless
                // another Leader exists in the same squad).
                IssueDirectiveIfLeader(trigger, LeaderDirectiveType.Converge);
                FollowActiveDirective();
                break;

            case TerroristState.TakeCover:
                patrolLine?.StopAndLook(_lookAnchor);
                StartCoroutine(MoveTocover(trigger));
                break;

            case TerroristState.Retreat:
                // Stop patrol movement but pass NO look target — while fleeing,
                // the NavMeshAgent steers the body toward the escape path; a look
                // target would twist the body backwards toward the threat.
                patrolLine?.StopAndLook(null);
                animator?.SetBool("Alert", true);
                _retreatRoutine = StartCoroutine(RetreatRoutine(trigger));
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
                // Leader coordination: an engaging Leader orders the squad to
                // FLANK (spread to the threat's sides) while it holds the front.
                IssueDirectiveIfLeader(trigger, LeaderDirectiveType.Flank);
                FollowActiveDirective();
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
        _retreatRoutine = null;
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

        // Disable all non-trigger colliders so the corpse doesn't block alive NPCs.
        // Investigating teammates use NavMeshAgent path-finding which respects the
        // capsule collider — without this, bodies become walls in the middle of the room.
        // Triggers (zone detectors, hitboxes already off) are preserved.
        if (clearCollidersOnDeath)
        {
            int disabledCount = 0;
            foreach (var col in GetComponentsInChildren<Collider>())
            {
                if (col == null) continue;
                if (col.isTrigger) continue;
                if (!col.enabled)  continue;
                col.enabled = false;
                disabledCount++;
            }
            if (disabledCount > 0)
                Debug.Log($"[TerroristController] {gameObject.name}: disabled {disabledCount} colliders so corpse doesn't block NavMesh paths.");
        }

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
    /// Snaps the NPC's transform so the mesh BOTTOM sits on the floor. Excludes own
    /// colliders during the raycast so we don't hit ourselves.
    ///
    /// When autoCalculateDeathOffset is true: reads renderer bounds to compute how far
    /// the mesh extends below the transform origin, then offsets accordingly. This works
    /// even if the prefab's transform pivot is at the hips, chest, or head instead of feet.
    ///
    /// When false: uses the manual deathFloorOffset field.
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

        // Raycast from well above the NPC straight down to find the actual floor surface.
        Vector3 origin = transform.position + Vector3.up * 2f;
        float floorY;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, ~0,
                            QueryTriggerInteraction.Ignore))
        {
            floorY = hit.point.y;
        }
        else
        {
            // Fallback — single-floor project, snap to Y=0
            floorY = 0f;
        }

        // Calculate how far the mesh extends BELOW the transform origin so we can put
        // the mesh's lowest point on the floor (rather than the transform itself).
        float belowTransform = 0f;
        if (autoCalculateDeathOffset)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            float minY = float.MaxValue;
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled) continue;
                if (r.bounds.min.y < minY) minY = r.bounds.min.y;
            }
            if (minY < float.MaxValue)
                belowTransform = transform.position.y - minY; // positive when mesh extends below transform
        }

        transform.position = new Vector3(
            transform.position.x,
            floorY + belowTransform + deathFloorOffset,
            transform.position.z);

        // Restore collider states (hitboxes/non-triggers are disabled separately in EnterDownState).
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
        float dur = Mathf.Clamp(clipLength, 0.5f, 5f);

        // Re-snap periodically DURING the death animation so root motion / aim curves
        // can't lift the body off the floor. Without this, animations that translate
        // the root upward (or NPCs that never had a death clip at all) leave the body
        // floating in mid-air.
        float elapsed     = 0f;
        float reSnapEvery = 0.15f;
        float nextSnap    = reSnapEvery;
        while (elapsed < dur)
        {
            elapsed += Time.deltaTime;
            if (elapsed >= nextSnap)
            {
                SnapToFloor();
                nextSnap += reSnapEvery;
            }
            yield return null;
        }

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

    // ── Retreat (low health fallback) ─────────────────────────────────────────

    /// <summary>
    /// Falls back AWAY from the threat when health drops below retreatHealthThreshold.
    /// Prefers a CoverPoint that is farther from the threat than the current position;
    /// otherwise picks a NavMesh point roughly retreatDistance metres directly away.
    /// Holds at the fallback point for retreatHoldTime, then returns to Alert so
    /// PerceptionController can re-trigger Engage if the player pursues.
    /// Without a NavMeshAgent the NPC can't move — it holds in place instead.
    /// </summary>
    IEnumerator RetreatRoutine(ScenarioEvent trigger)
    {
        // Threat = live player position when known, else the trigger's origin.
        Vector3 threatPos = _lastSeenPlayer != null
            ? _lastSeenPlayer.position
            : (trigger?.Origin ?? transform.position + transform.forward);

        bool canMove = agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;

        if (canMove)
        {
            Vector3 away = transform.position - threatPos;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = -transform.forward;
            away.Normalize();

            // 1st choice: a cover point near the fallback area that shields from the threat
            //             AND is farther from the threat than where we stand now.
            Vector3 fallbackArea = transform.position + away * retreatDistance;
            var cover = CoverPoint.SelectBest(fallbackArea, threatPos - fallbackArea);

            Vector3 dest;
            if (cover != null &&
                Vector3.Distance(cover.transform.position, threatPos) >
                Vector3.Distance(transform.position, threatPos))
            {
                // Release any cover still claimed from a previous TakeCover —
                // otherwise that point stays occupied forever.
                _claimedCover?.Release();
                cover.Claim();
                _claimedCover = cover;
                dest = cover.transform.position;
            }
            else
            {
                // 2nd choice: sample the NavMesh straight away from the threat,
                // shrinking the distance until a reachable point is found.
                dest = transform.position; // worst case: hold where we are
                for (float d = retreatDistance; d >= 2f; d -= 2f)
                {
                    if (NavMesh.SamplePosition(transform.position + away * d,
                                               out NavMeshHit hit, 3f, NavMesh.AllAreas))
                    {
                        dest = hit.position;
                        break;
                    }
                }
            }

            Debug.Log($"[TerroristController] {gameObject.name}: retreating to {dest} " +
                      $"(health {currentHealth:F0})");

            agent.speed = _originalAgentSpeed * retreatSpeedMultiplier;
            agent.SetDestination(dest);

            // Move until arrival — with a hard travel deadline so an unreachable
            // destination (disconnected NavMesh island) can't trap the NPC in
            // Retreat forever.
            float travelDeadline = Time.time + 8f;
            while (currentState == TerroristState.Retreat &&
                   Time.time < travelDeadline &&
                   (agent.pathPending ||
                    agent.remainingDistance > agent.stoppingDistance + 0.3f))
                yield return null;

            if (currentState != TerroristState.Retreat) yield break;

            agent.speed = _originalAgentSpeed;
            agent.ResetPath();
        }
        else
        {
            Debug.Log($"[TerroristController] {gameObject.name}: retreat requested but no " +
                      $"NavMeshAgent — holding position (health {currentHealth:F0})");
        }

        // Hold at the fallback point, weapon up, watching the threat direction.
        SetLookTarget(threatPos);
        float waited = 0f;
        while (waited < retreatHoldTime && currentState == TerroristState.Retreat)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        // Back to Alert — perception re-escalates to Engage if the player is visible.
        // Re-arm TargetConfirmed first: if the player kept us in continuous view
        // through the retreat, the original confirm flag is still set and would
        // otherwise never fire again (it only resets when LOS breaks).
        if (currentState == TerroristState.Retreat)
        {
            GetComponent<PerceptionController>()?.RearmTargetConfirmation();
            TransitionTo(TerroristState.Alert, trigger);
        }

        _retreatRoutine = null;
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

        // Perception events are raised BY this NPC (Instigator = self), so the
        // instigator fallback below would make the NPC target ITSELF. The real
        // player reference is the PerceptionController's target / main camera.
        if (e?.Instigator == gameObject || e?.Instigator == null)
        {
            var pc = GetComponent<PerceptionController>();
            if (pc != null && pc.playerTarget != null) return pc.playerTarget;
            var main = Camera.main;
            return main != null ? main.transform : null;
        }

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

    [Tooltip("Maximum search waypoints before giving up. Set high (e.g. 30) so the NPC keeps " +
             "searching the area until they actually see the player. The search will end early " +
             "if the player is spotted (state escalates to Engage).")]
    public int searchWaypointCount = 30;

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
        animator?.SetBool("Investigating", true);

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

            // Pick a random point on the NavMesh. Direction is random; distance is uniform
            // between min and max radius. As the search continues, expand the max radius
            // so the NPC progressively searches a wider area instead of pacing the same spot.
            Vector2 dir2D = Random.insideUnitCircle.normalized;
            if (dir2D.sqrMagnitude < 0.01f) dir2D = Vector2.up; // safety
            float expandedMax = searchWaypointMaxRadius + (i * 0.5f); // grow by 0.5m per waypoint
            float distance = Random.Range(searchWaypointMinRadius, expandedMax);
            Vector3 candidate = soundPos + new Vector3(dir2D.x, 0f, dir2D.y) * distance;

            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, expandedMax, NavMesh.AllAreas))
                continue;

            Debug.Log($"[TerroristController] {gameObject.name}: searching waypoint {i + 1}/{searchWaypointCount} at {hit.position} (radius {expandedMax:F1}m)");

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
        animator?.SetBool("Investigating", false);
        if (agent != null && agent.isActiveAndEnabled)
            agent.speed = _originalAgentSpeed;
    }

    // ── Leader coordination (squad directives) ────────────────────────────────

    /// <summary>
    /// Called by Squad.IssueDirective on every non-engaged, non-down member of the
    /// Leader's squad. Stores the directive and routes the NavMeshAgent toward
    /// TargetPosition. Members in Idle/Suspicious are escalated to Alert.
    /// </summary>
    public void OnDirective(LeaderDirective directive)
    {
        if (directive == null) return;
        if (currentState == TerroristState.Down || currentState == TerroristState.Engage) return;

        _activeDirective = directive;
        SetLookTarget(directive.TargetPosition);

        Debug.Log($"[TerroristController] {gameObject.name} received directive: {directive}");

        // Escalate to Alert so wander/patrol routines stop and the NPC focuses
        // on the directive. If already Alert/TakeCover, just refresh the destination.
        if (currentState == TerroristState.Idle || currentState == TerroristState.Suspicious)
        {
            TransitionTo(TerroristState.Alert, null);
            // FollowActiveDirective() runs inside the Alert case in TransitionTo
            return;
        }

        FollowActiveDirective();
    }

    /// <summary>
    /// If this NPC is a Leader with a valid squad, broadcast a directive.
    /// Alert leaders order Converge (close on the threat); engaging leaders order
    /// Flank (spread to the threat's sides while the leader holds the front).
    /// Target is the trigger's origin (the threat location); falls back to leader's
    /// own position when no trigger is supplied.
    /// </summary>
    void IssueDirectiveIfLeader(ScenarioEvent trigger, LeaderDirectiveType type)
    {
        if (role != NPCRole.Leader) return;
        if (string.IsNullOrEmpty(squadId)) return;

        Vector3 target = trigger?.Origin ?? transform.position;
        string  reason = trigger?.Type.ToString() ?? "manual";

        var directive = new LeaderDirective(type, target, this, reason);

        Squad.Get(squadId)?.IssueDirective(directive);
    }

    /// <summary>
    /// If a directive is currently held, route the NavMeshAgent toward its target.
    /// Called when entering Alert (post-AlertPropagator) and when a new directive arrives.
    ///
    /// Flank — instead of walking straight at the threat, each member moves to a
    /// point offset flankOffset metres to the threat's side. The side is whichever
    /// is already closer to the member, so a squad naturally splits left/right
    /// without explicit slot assignment.
    /// </summary>
    void FollowActiveDirective()
    {
        if (_activeDirective == null) return;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        switch (_activeDirective.Type)
        {
            case LeaderDirectiveType.Converge:
                agent.SetDestination(_activeDirective.TargetPosition);
                break;

            case LeaderDirectiveType.Flank:
                agent.SetDestination(ComputeFlankPoint(_activeDirective.TargetPosition));
                break;

            case LeaderDirectiveType.Hold:
                agent.ResetPath();
                break;
        }
    }

    /// <summary>
    /// Picks the flank destination for this member: threat position offset
    /// perpendicular to this member's approach direction, on whichever side the
    /// member already leans toward. Falls back to the raw threat position when
    /// no valid NavMesh point exists near the flank offset.
    /// </summary>
    Vector3 ComputeFlankPoint(Vector3 threatPos)
    {
        Vector3 toThreat = threatPos - transform.position;
        toThreat.y = 0f;
        if (toThreat.sqrMagnitude < 0.01f) return threatPos;

        // Perpendicular to the approach direction (horizontal plane).
        Vector3 perp = Vector3.Cross(Vector3.up, toThreat.normalized);

        // Choose the side this member is already offset toward — members on the
        // leader's left flank left, members on the right flank right.
        if (Vector3.Dot(perp, transform.position - threatPos) < 0f)
            perp = -perp;

        Vector3 candidate = threatPos + perp * flankOffset;

        if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, flankOffset, NavMesh.AllAreas))
            return hit.position;

        return threatPos; // no reachable flank point — degrade to Converge
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
