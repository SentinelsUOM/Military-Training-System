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

    [Tooltip("Base NavMesh move speed (m/s) for walking (patrol/converge/flank/investigate). " +
             "Kept modest so foot motion matches the walk clip instead of sliding. Retreat " +
             "multiplies this by Retreat Speed Multiplier.")]
    public float moveSpeed = 1.0f;

    [Tooltip("Wander only — max distance from spawn that the NPC will wander.")]
    public float wanderRadius = 12f;

    [Tooltip("Wander only — seconds to pause at each waypoint before picking the next one.")]
    public float wanderPauseTime = 2f;

    [Tooltip("Static only — the transform this NPC faces while standing still.\n" +
             "Leave empty to keep spawn rotation.")]
    public Transform staticFaceTarget;

    [Tooltip("Animator on this NPC (used to trigger death animation). Optional.")]
    public Animator animator;

    [Tooltip("Planar movement speed (m/s) that maps to a FULL walk animation. The locomotion " +
             "blend tree's 'Speed' parameter is set to (actual speed / this), clamped 0-1. " +
             "Lower it if the legs cycle too slowly for the travel speed (foot-sliding); raise " +
             "it if they cycle too fast. ~1.3 matches the default NavMeshAgent / PatrolLine speed.")]
    public float walkAnimReferenceSpeed = 1.0f;

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

    [Tooltip("Hostage guardian: stays on the hostage and NEVER leaves to investigate, " +
             "converge or flank. It only engages what it personally sees, then keeps " +
             "guarding. Set automatically by SceneBuilder for the HostageGuardian role.")]
    public bool isHostageGuardian = false;

    [Tooltip("The specific hostage this guardian seizes as leverage when it detects the trainee. " +
             "Set by SceneBuilder for the HostageGuardian role; if left null, a guardian auto-binds " +
             "to the nearest hostage at spawn.")]
    public HostageController guardedHostage;

    [Header("Hostage Leverage (guardian only)")]
    [Tooltip("Backstop: seconds of sustained standoff (guardian holding the hostage while it sees the " +
             "trainee) before it EXECUTES. Neutralise the guardian before this elapses to save the hostage.")]
    public float executionCountdown = 15f;

    [Tooltip("Once holding the hostage, if the trainee closes within this distance (m) the guardian turns " +
             "and AIMS AT THE HOSTAGE as a visible warning ('back off!'). Back away and it de-escalates.")]
    public float warningRange = 3.5f;

    [Tooltip("If the trainee keeps pushing in past the warning to within this distance (m) of the hostage, " +
             "the guardian shoots it. Set below warningRange so the warning always shows first.")]
    public float executeRange = 1.5f;

    [Tooltip("How far (m) behind the hostage — from the trainee's viewpoint — the guardian holds while " +
             "using it as a human shield.")]
    public float shieldStandoff = 1.0f;

    [Tooltip("The guardian only resorts to the human shield when the SITUATION is high-pressure: its " +
             "posture has escalated to CoverAndPeek, OR its health is at/below the retreat threshold, OR " +
             "the trainee closes within this range (m) of the hostage. Otherwise it just fights normally.")]
    public float shieldTriggerRange = 6f;

    [Tooltip("Relaxed guard-patrol radius (m) around the hostage while idle — the guardian wanders " +
             "gently within this of the hostage, then holds and watches.")]
    public float guardPatrolRadius = 2.5f;

    [Tooltip("Leash (m): the farthest the guardian may stray from the hostage while searching " +
             "suspiciously (after a gunshot / ally-down). It NEVER exceeds this — it never leaves the hostage.")]
    public float guardLeashRadius = 6f;

    [Header("Health")]
    public float maxHealth = 100f;

    [Header("Retreat Tuning")]
    [Tooltip("When a hit drops health to/below this value (but above the Down threshold), " +
             "the NPC falls back away from the threat. Set to 0 to disable retreating.")]
    public float retreatHealthThreshold = 40f;

    [Tooltip("How far (metres) the NPC tries to fall back from the threat.")]
    public float retreatDistance = 10f;

    [Tooltip("NavMeshAgent speed multiplier while retreating (1 = normal walk). ~1.7 reads as a " +
             "jog without badly outrunning the run clip.")]
    public float retreatSpeedMultiplier = 1.7f;

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

    [Header("Standoff (combat spacing)")]
    [Tooltip("Hard minimum distance (m) the terrorist keeps between itself and the player while " +
             "fighting. It never paths closer than this and steps back if the player closes in. " +
             "Grounded in the Tueller 'reactionary gap' idea; ~3 m suits tight indoor rooms.")]
    public float minStandoffDistance = 3f;

    [Tooltip("Preferred firing distance (m). When closing on the player the terrorist stops on this " +
             "ring instead of walking onto you. Should be >= minStandoffDistance.")]
    public float preferredStandoffDistance = 3.5f;

    [Header("Combat Escalation (posture)")]
    [Tooltip("Current squad-member behaviour. Starts at HoldAndShoot (stand at range and fire). As the " +
             "firefight escalates — allies killed, stress spikes, taking hits, time in combat — it flips " +
             "to CoverAndPeek (use cover, peek and fire). Read-only at runtime; mirrors real morale decay.")]
    public CombatPosture posture = CombatPosture.HoldAndShoot;

    [Tooltip("Escalation points needed to switch from HoldAndShoot to CoverAndPeek.")]
    public float escalationThreshold = 3f;

    [Tooltip("Escalation points added when this NPC sees / is told an ally went down (strongest cue).")]
    public float escalationPerAllyDown = 2f;

    [Tooltip("Escalation points added per StressSpike (rapid player gunfire nearby).")]
    public float escalationPerStressSpike = 1f;

    [Tooltip("Escalation points added each time the player hits this NPC.")]
    public float escalationPerHit = 1f;

    [Tooltip("Every this many seconds of continuous combat, escalation rises by 1 (sustained-fight caution).")]
    public float escalationCombatInterval = 8f;

    [Tooltip("While in CoverAndPeek, seconds spent firing from one cover spot before repositioning to new cover.")]
    public float coverHoldTime = 3.5f;

    [Tooltip("XR Main Camera — used as shoot target when Engage is triggered by PlayerSeen.\n" +
             "If left empty the event instigator's Camera is used instead.")]
    public Transform playerCamera;

    [Header("Suspicious Timeout")]
    [Tooltip("Seconds in Suspicious state before auto-returning to Idle if nothing escalates.")]
    public float suspiciousTimeout = 6f;

    [Header("Alert Give-Up")]
    [Tooltip("Seconds an NPC stays in Alert with no re-acquisition / active search before it " +
             "gives up and resumes its patrol/idle. Prevents NPCs from standing alert forever " +
             "after the player breaks contact. The last-known-position search (started on " +
             "PlayerLost) runs independently and also returns the NPC to patrol when it ends.")]
    public float alertGiveUpTime = 12f;

    [Header("Hearing")]
    [Tooltip("Radius within which this NPC can hear a GunshotHeard event.")]
    public float hearingRange = 20f;

    [Header("Aim (Engage state)")]
    [Tooltip("When ON, procedurally twists the spine so the barrel points exactly at the player. " +
             "This makes the SHOT land on target but visibly rolls/twists the two-handed pose and " +
             "can pull the support hand off the gun. When OFF (default), the terrorist just turns to " +
             "FACE the player and lets the clean 'firing rifle' clip pose the weapon — natural pose, " +
             "both hands on the gun; the shot still goes where the clip's barrel points (≈ forward).")]
    public bool useProceduralAim = false;

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

    [Tooltip("Head LookAt IK strength (0-1) — how strongly the NPC turns its head/eyes to look at " +
             "the player while alerted/engaged. Needs 'IK Pass' on the animator base layer (already set). " +
             "1 = fully tracks you.")]
    [Range(0f, 1f)] public float headLookWeight = 1f;

    [Tooltip("How much the upper body leans into the head LookAt (0-1). Small values keep it to the " +
             "head/neck; larger values turn the torso too. ~0.3 reads natural.")]
    [Range(0f, 1f)] public float headLookBodyWeight = 0.35f;

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

        // Hostage guardian: it REACTS to danger cues (gunshots, ally-down, breaches) by
        // getting Suspicious/Alert and searching around the hostage (GuardianRoamRoutine),
        // but it never relocates to investigate/converge/flank — those paths are separately
        // skipped for guardians — so it never leaves the hostage. It ignores vision events
        // here because those reach it through its own perception (HandleDetection).
        if (isHostageGuardian)
        {
            return e.Type == ScenarioEventType.GunshotHeard  ||
                   e.Type == ScenarioEventType.TerroristDown ||
                   e.Type == ScenarioEventType.AllyDownSeen  ||
                   e.Type == ScenarioEventType.StressSpike   ||
                   e.Type == ScenarioEventType.RoomBreached  ||
                   e.Type == ScenarioEventType.DoorOpened;
        }

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
            {
                // Auditory range check
                float d = Vector3.Distance(transform.position, e.Origin);
                bool heard = d <= hearingRange;
                // Throttle: auto-fire raises one GunshotHeard per bullet, so log this
                // per-NPC check at most ~once/second to keep the trace readable.
                if (Time.time - _lastHearingLogTime >= 1f)
                {
                    _lastHearingLogTime = Time.time;
                    Debug.Log($"[TerroristController] {gameObject.name}: GunshotHeard range check — " +
                              $"dist={d:F1}m vs hearingRange={hearingRange:F1}m → {(heard ? "HEARD" : "too far, ignored")} " +
                              $"(state={currentState})");
                }
                return heard;
            }

            // ── Perception is PERSONAL ──────────────────────────────────────
            // PlayerSeen / TargetConfirmed / PlayerLost are raised by an NPC's
            // OWN PerceptionController and delivered directly to that NPC via
            // HandleDetection(). They must NOT be routed over the event bus to
            // other NPCs — otherwise a terrorist who never saw the player would
            // receive a squadmate's TargetConfirmed and open fire blindly on the
            // player's last-known position ("shooting without seeing you").
            // Other NPCs gain awareness through squad directives + AlertPropagation,
            // and only Engage once their OWN perception confirms line of sight.
            case ScenarioEventType.PlayerSeen:
            case ScenarioEventType.TargetConfirmed:
            case ScenarioEventType.PlayerLost:
                return false;

            case ScenarioEventType.RoomBreached:
            case ScenarioEventType.DoorOpened:
            case ScenarioEventType.AllyDownSeen:
            case ScenarioEventType.TerroristDown:
            case ScenarioEventType.StressSpike: // rate-limited by the cooldown above; only escalates posture
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
                Debug.Log($"[TerroristController] {gameObject.name}: RespondTo GunshotHeard at {e.Origin:F1} " +
                          $"(state={currentState}) — turning to face sound" +
                          (currentState == TerroristState.Idle ? " → Suspicious" :
                           currentState != TerroristState.Engage ? " → Alert" : " (Engaged, holding)"));
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

            case ScenarioEventType.StressSpike:
                // Rapid player gunfire nearby — no state change, but it rattles them.
                AddEscalation(escalationPerStressSpike);
                break;

            case ScenarioEventType.TerroristDown:
                if (currentState == TerroristState.Down) break;
                // Learning a squad-mate is dead is the strongest cue to start using cover.
                AddEscalation(escalationPerAllyDown);
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
        // Guardians NEVER retreat — they never leave the hostage; when wounded they
        // reach for the hostage as a shield instead (handled in GuardianPressureTick).
        if (retreatHealthThreshold > 0f &&
            currentHealth <= retreatHealthThreshold &&
            !_hasRetreated &&
            !isHostageGuardian &&
            (currentState == TerroristState.Engage ||
             currentState == TerroristState.Alert  ||
             currentState == TerroristState.TakeCover))
        {
            _hasRetreated = true;
            TransitionTo(TerroristState.Retreat, null);
        }

        // Taking fire raises caution — feeds the posture escalation.
        if (currentState == TerroristState.Engage ||
            currentState == TerroristState.Alert  ||
            currentState == TerroristState.TakeCover)
            AddEscalation(escalationPerHit);
    }

    // ── Combat escalation (morale model) ───────────────────────────────────────

    /// <summary>
    /// Raises this NPC's combat-stress score and, once it crosses escalationThreshold,
    /// flips posture from HoldAndShoot to CoverAndPeek. Models mounting caution as
    /// casualties, stress and time accumulate — first contact is disciplined standoff
    /// fire; a grinding firefight pushes survivors into cover. Escalation only rises.
    /// </summary>
    void AddEscalation(float amount)
    {
        if (amount <= 0f) return;
        if (posture == CombatPosture.CoverAndPeek) return; // already at the cautious posture

        _escalation += amount;
        if (_escalation >= escalationThreshold)
        {
            posture = CombatPosture.CoverAndPeek;
            Debug.Log($"[TerroristController] {gameObject.name}: posture → CoverAndPeek " +
                      $"(escalation {_escalation:F1} ≥ {escalationThreshold}) — switching to cover & peek.");
            // If already in the fight, restart positioning so it adopts cover immediately.
            if (currentState == TerroristState.Engage) StartCombatPositioning();
        }
    }

    // ── Combat positioning (standoff hold / cover-peek) ────────────────────────

    void StartCombatPositioning()
    {
        StopCombatPositioning();
        if (isHostageGuardian) return; // guardian holds its post by the hostage, never repositions
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;
        _combatPosRoutine = StartCoroutine(CombatPositionRoutine());
    }

    void StopCombatPositioning()
    {
        if (_combatPosRoutine != null) { StopCoroutine(_combatPosRoutine); _combatPosRoutine = null; }
    }

    /// <summary>
    /// Steers the agent WHILE it fires, per posture:
    ///   HoldAndShoot — stand and fire; only move to restore the standoff gap if the
    ///                  player closes inside minStandoffDistance.
    ///   CoverAndPeek — relocate to cover that shields from the player, fire from there
    ///                  for coverHoldTime, then bound to fresh cover (peek & fire).
    /// Firing is driven by the shooter component; this only governs footing.
    /// </summary>
    IEnumerator CombatPositionRoutine()
    {
        while (currentState == TerroristState.Engage)
        {
            Vector3 threat = _lastSeenPlayer != null ? _lastSeenPlayer.position : _lastKnownPlayerPos;

            if (posture == CombatPosture.CoverAndPeek)
            {
                // Move to cover that shields from the threat, but never inside the standoff ring.
                var cover = CoverPoint.SelectBest(transform.position, threat - transform.position);
                if (cover != null)
                {
                    _claimedCover?.Release();
                    cover.Claim();
                    _claimedCover = cover;
                    agent.SetDestination(EnforceStandoff(cover.transform.position, threat));
                }
                else
                {
                    agent.SetDestination(StandoffPoint(threat)); // no cover available → just hold standoff
                }

                // Fire from here for a beat (peek), then loop to pick fresh cover (reposition).
                float held = 0f;
                while (held < coverHoldTime && currentState == TerroristState.Engage)
                {
                    MaintainStandoff(threat);
                    held += Time.deltaTime;
                    yield return null;
                }
            }
            else // HoldAndShoot
            {
                MaintainStandoff(threat);
                yield return null;
            }
        }
        _combatPosRoutine = null;
    }

    /// <summary>Per-frame spacing keeper: back off to the standoff ring if the player crowds
    /// in; otherwise (HoldAndShoot) stop and plant so the NPC fires from where it stands.</summary>
    void MaintainStandoff(Vector3 threat)
    {
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        float dist = Vector3.Distance(transform.position, threat);
        if (dist < minStandoffDistance - 0.05f)
        {
            agent.SetDestination(StandoffPoint(threat)); // too close — step back
        }
        else if (posture == CombatPosture.HoldAndShoot &&
                 !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.1f)
        {
            agent.ResetPath(); // at a safe gap and not already moving — plant and fire
        }
    }

    /// <summary>A NavMesh point preferredStandoffDistance straight back from the threat.</summary>
    Vector3 StandoffPoint(Vector3 threat)
    {
        Vector3 away = transform.position - threat; away.y = 0f;
        if (away.sqrMagnitude < 0.01f) away = -transform.forward;
        away.Normalize();
        Vector3 target = threat + away * preferredStandoffDistance;
        return NavMesh.SamplePosition(target, out NavMeshHit hit, 2f, NavMesh.AllAreas)
            ? hit.position : transform.position;
    }

    /// <summary>Pushes a candidate destination out of the standoff ring if it sits inside it.</summary>
    Vector3 EnforceStandoff(Vector3 point, Vector3 threat)
    {
        Vector3 flat = point; flat.y = threat.y;
        if (Vector3.Distance(flat, threat) >= minStandoffDistance) return point;

        Vector3 dir = point - threat; dir.y = 0f;
        if (dir.sqrMagnitude < 0.01f) dir = transform.position - threat;
        dir.y = 0f; dir.Normalize();
        Vector3 pushed = threat + dir * minStandoffDistance;
        return NavMesh.SamplePosition(pushed, out NavMeshHit hit, 2f, NavMesh.AllAreas)
            ? hit.position : point;
    }

    /// <summary>Approach destination on the standoff ring around a target (so squad members
    /// closing in stop at a firing distance instead of walking onto the player).</summary>
    Vector3 ApproachRingPoint(Vector3 target)
    {
        Vector3 from = transform.position - target; from.y = 0f;
        if (from.sqrMagnitude < 0.01f) from = -transform.forward;
        from.Normalize();
        Vector3 dest = target + from * preferredStandoffDistance;
        return NavMesh.SamplePosition(dest, out NavMeshHit hit, preferredStandoffDistance, NavMesh.AllAreas)
            ? hit.position : dest;
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
                if (cam != null) { _lastSeenPlayer = cam; SetLookTarget(cam.position); _lastKnownPlayerPos = cam.position; }
                else             { SetLookTarget(e.Origin); _lastKnownPlayerPos = e.Origin; }
                _playerVisible = true; // currently has eyes on the player

                if (currentState == TerroristState.Idle ||
                    currentState == TerroristState.Suspicious)
                    TransitionTo(TerroristState.Alert, e);
                break;
            }
            case ScenarioEventType.TargetConfirmed:
            {
                var cam = ResolvePlayerCamera(e);
                if (cam != null) { _lastSeenPlayer = cam; SetLookTarget(cam.position); _lastKnownPlayerPos = cam.position; }
                _personallyConfirmedPlayer = true; // own eyes confirmed the target
                _playerVisible = true;             // currently has eyes on the player
                if (currentState == TerroristState.Alert ||
                    currentState == TerroristState.Suspicious)
                    TransitionTo(TerroristState.Engage, e);
                break;
            }
            case ScenarioEventType.PlayerLost:
                _playerVisible = false; // no current line of sight
                // Lost sight of the player — actively HUNT: walk to where they were
                // last seen and run the search routine (scan + expanding sweep).
                // This now fires whether the NPC had fully committed (Engage) OR had
                // only just spotted the player (Alert): a guard who glimpses someone
                // and then loses them should still go check the last-known spot, not
                // stand in place and give up. PerceptionController only raises
                // PlayerLost for an NPC that PERSONALLY had eyes on the player, so
                // squad-alerted NPCs that never actually saw you are unaffected.
                if (currentState == TerroristState.Engage ||
                    currentState == TerroristState.Alert)
                {
                    if (currentState == TerroristState.Engage)
                        TransitionTo(TerroristState.Alert, e);
                    // Guardian holds its post — it does NOT chase/search; it keeps
                    // guarding the hostage and re-engages only if the player returns.
                    if (!isHostageGuardian)
                        InvestigatePosition(_lastKnownPlayerPos);
                }
                break;
        }
    }

    // ── Private state ─────────────────────────────────────────────────────────

    float     _lastResponseTime = -99f;
    float     _lastHearingLogTime = -99f;  // throttles the per-bullet GunshotHeard range-check log
    float     _stuckTimer;                 // accumulates time an agent wants to move but isn't (stuck diagnostic)
    Transform _lookAnchor;
    Transform _lastSeenPlayer;
    Coroutine _suspiciousRoutine;
    Coroutine _investigateRoutine;
    Coroutine _wanderRoutine;
    Coroutine _retreatRoutine;
    Coroutine _alertGiveUpRoutine;       // returns NPC to patrol if Alert never re-acquires
    bool      _hasRetreated;   // one retreat per life — reset only on (re)spawn
    bool      _wasFiring;
    bool      _personallyConfirmedPlayer; // true only when own PerceptionController fired TargetConfirmed
    Vector3   _lastKnownPlayerPos;        // where the player was last actually seen (for search-on-lost)
    bool      _pendingEscalation;         // this investigation may call for squad backup if it finds nothing
    CoverPoint _claimedCover;
    Vector3   _spawnPosition;
    Quaternion _spawnRotation;
    float     _originalAgentSpeed;
    bool      _isInvestigating;
    LeaderDirective _activeDirective;   // last directive received from squad Leader; null when none active
    Transform _aimBone;                 // cached chest/spine bone used to aim the upper body at the player
    Transform[] _spineChain;            // Spine→Chest→UpperChest, used to spread the aim rotation naturally
    Transform _leftUpperArm, _leftForeArm, _leftHand; // support-hand IK chain — keeps the left hand on the gun
    float     _escalation;              // accumulated combat-stress score; flips posture at escalationThreshold
    float     _nextCombatTickTime;      // next time the sustained-combat escalation tick may fire
    Coroutine _combatPosRoutine;        // active combat-positioning loop (standoff hold / cover-peek)
    Coroutine _leverageRoutine;         // active hostage-leverage loop (guardian only)
    Coroutine _guardRoamRoutine;        // leashed patrol/search loop around the hostage (guardian only)
    bool      _hostageWarning;          // guardian is in the WARNING beat: aiming AT the hostage to warn the trainee off
    bool      _playerVisible;           // TRUE only while this NPC currently has LOS on the player
                                        // (set on PlayerSeen/TargetConfirmed, cleared on PlayerLost).
                                        // NOTE: _lastSeenPlayer is the camera transform, so its .position
                                        // is always live — use _playerVisible for "can I see them NOW?".
    Vector3   _lastAnimPos;             // previous-frame position, for velocity-driven locomotion blend
    static readonly int _animSpeed  = Animator.StringToHash("Speed");
    static readonly int _animFiring = Animator.StringToHash("Firing");
    static readonly int _animCrouch = Animator.StringToHash("Crouch");
    static readonly int _animAlert  = Animator.StringToHash("Alert");

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        currentHealth = maxHealth;
        _spawnPosition = transform.position;
        _spawnRotation = transform.rotation;
        _lastAnimPos   = transform.position;

        // Reusable world-space anchor so PatrolLine.StopAndLook() always gets a valid target.
        var go = new GameObject($"[LookAnchor] {gameObject.name}");
        _lookAnchor = go.transform;

        // Enforce a sane walk speed regardless of the prefab's NavMeshAgent default
        // (Unity defaults agents to 3.5 m/s, which badly outruns the walk/run clip
        // cadence → foot-sliding). This keeps travel speed matched to the animation.
        if (agent != null)
        {
            agent.speed = moveSpeed;
            _originalAgentSpeed = moveSpeed;
        }

        // Cache the aim bone (Chest by default) so LateUpdate can rotate it without per-frame lookup.
        // Falls back through UpperChest → Chest → Spine in case the rig is missing some bones.
        // Non-humanoid rigs return null; aim falls back to weapon-pivot-only mode.
        if (animator != null && animator.isHuman)
        {
            _aimBone = animator.GetBoneTransform(aimBoneType);
            if (_aimBone == null) _aimBone = animator.GetBoneTransform(HumanBodyBones.Chest);
            if (_aimBone == null) _aimBone = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (_aimBone == null) _aimBone = animator.GetBoneTransform(HumanBodyBones.Spine);

            // Spine chain (low → high) used to AIM the weapon at the player. The
            // rotation needed to bring the gun onto the target is spread across
            // these bones so the torso bends naturally, instead of one bone
            // snapping ~90° (which tore the two-handed grip and pitched the head).
            _spineChain = new Transform[]
            {
                animator.GetBoneTransform(HumanBodyBones.Spine),
                animator.GetBoneTransform(HumanBodyBones.Chest),
                animator.GetBoneTransform(HumanBodyBones.UpperChest),
            };

            // Left (support) arm — after the spine aim moves the gun, a two-bone IK
            // pass re-locks this hand onto the weapon so it never floats off.
            _leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            _leftForeArm  = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            _leftHand     = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        }

        // Movement is driven by NavMeshAgent / PatrolLine / transform — NOT by the
        // animation's root curve. Force root motion off so the walk/aim clips can't
        // drag the body around independently of where the agent is steering it.
        if (animator != null) animator.applyRootMotion = false;

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

        // ── Stuck diagnostic ─────────────────────────────────────────────────
        // If the agent has a destination but isn't actually moving toward it,
        // report WHY after 2 s: the NavMesh path status is the smoking gun.
        //   pathStatus=PathPartial  → destination is across an unreachable gap
        //                             (NavMesh not connected — e.g. a door still
        //                             severing it, or a floor gap at the doorway).
        //   pathStatus=PathComplete → path exists but something else stops it
        //                             (isStopped, a physical obstruction, arrival).
        //   isOnNavMesh=false       → the agent never attached to the NavMesh.
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh &&
            !agent.isStopped && (agent.hasPath || agent.pathPending))
        {
            bool wantsToMove = agent.pathPending ||
                               agent.remainingDistance > agent.stoppingDistance + 0.3f;
            bool notMoving   = agent.velocity.sqrMagnitude < 0.0025f; // < 0.05 m/s
            if (wantsToMove && notMoving)
            {
                _stuckTimer += Time.deltaTime;
                if (_stuckTimer >= 2f)
                {
                    _stuckTimer = 0f;
                    Debug.LogWarning($"[TerroristController] {gameObject.name}: STUCK — " +
                        $"state={currentState} pathStatus={agent.pathStatus} " +
                        $"onNavMesh={agent.isOnNavMesh} remaining={agent.remainingDistance:F2} " +
                        $"dest={agent.destination:F1} pos={transform.position:F1}");
                }
            }
            else _stuckTimer = 0f;
        }
        else _stuckTimer = 0f;

        // ── Velocity-driven locomotion blend ─────────────────────────────────
        // Measure how fast we ACTUALLY moved this frame (covers NavMeshAgent
        // movement — wander/converge/flank/investigate/retreat — AND PatrolLine,
        // which drives the transform directly). Feeding real speed into the
        // animator's "Speed" param blends Aiming (still) ↔ Rifle Walk (moving),
        // so NPCs never glide in the aim pose while relocating.
        if (animator != null)
        {
            Vector3 d = transform.position - _lastAnimPos;
            d.y = 0f;
            float speed = Time.deltaTime > 0f ? d.magnitude / Time.deltaTime : 0f;
            // 0 = idle/aim, 1 = walk pace, 2 = run pace (retreat). Clamp to the
            // blend's 0-2 range so fast movement reaches the rifle-run clip.
            float norm  = Mathf.Clamp(speed / Mathf.Max(0.01f, walkAnimReferenceSpeed), 0f, 2f);
            // 0.12 s damping smooths the start/stop so legs ease in/out.
            animator.SetFloat(_animSpeed, norm, 0.12f, Time.deltaTime);

            // Firing clip plays whenever the shooter is actually firing (Engage) — and also
            // during the hostage WARNING beat, so the guardian holds the rifle aimed at the
            // hostage (it faces the hostage via the look-anchor override) instead of relaxing.
            animator.SetBool(_animFiring, (shooter != null && shooter.IsFiring) ||
                                          (isHostageGuardian && _hostageWarning));
            // Crouch clip while moving to / holding cover.
            animator.SetBool(_animCrouch, currentState == TerroristState.TakeCover);
            // Threat-aware flag drives the locomotion swap: relaxed patrol walk
            // (Remy@Rifle Walk) only while Idle/patrolling; any other state uses the
            // cautious tactical walk (Ch08@Walking) via the AlertMove state. Driven
            // here every frame so it stays correct even in states (e.g. Engage) that
            // don't set it on entry.
            animator.SetBool(_animAlert, currentState != TerroristState.Idle);
        }
        _lastAnimPos = transform.position;

        // While engaging we have live sight of the player, so keep recording where
        // they are. The instant we lose them (PlayerLost), this value freezes at the
        // last-seen spot and becomes the search target.
        if (currentState == TerroristState.Engage && _lastSeenPlayer != null)
            _lastKnownPlayerPos = _lastSeenPlayer.position;

        // Sustained-fight caution: a drawn-out engagement slowly raises escalation
        // even without fresh casualties, so a long firefight eventually pushes the
        // NPC into cover-and-peek.
        if (currentState == TerroristState.Engage &&
            escalationCombatInterval > 0f && Time.time >= _nextCombatTickTime)
        {
            _nextCombatTickTime = Time.time + escalationCombatInterval;
            AddEscalation(1f);
        }

        // Guardian: keep the leashed roam alive, and run the leverage decision
        // (fight normally / grab shield under pressure / execute on rush).
        if (isHostageGuardian)
        {
            if (_guardRoamRoutine == null && currentState != TerroristState.Down)
                _guardRoamRoutine = StartCoroutine(GuardianRoamRoutine());
            GuardianPressureTick();
        }

        // Keep look anchor tracking the player's live position every frame.
        if (_lastSeenPlayer != null && currentState != TerroristState.Idle && _lookAnchor != null)
            _lookAnchor.position = new Vector3(
                _lastSeenPlayer.position.x, transform.position.y, _lastSeenPlayer.position.z);

        // WARNING beat: a guardian threatening the hostage turns and aims AT it instead of
        // the trainee — the visible "back off or it dies" telegraph.
        if (isHostageGuardian && _hostageWarning && guardedHostage != null && _lookAnchor != null)
            _lookAnchor.position = new Vector3(
                guardedHostage.transform.position.x, transform.position.y, guardedHostage.transform.position.z);

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
                // Snap to face the player much faster while actually shooting so the
                // body clearly turns to look at you (not stuck side-on).
                float faceRate = currentState == TerroristState.Engage
                    ? Mathf.Max(aimRotationSpeed, 14f)
                    : aimRotationSpeed;
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, target, Time.deltaTime * faceRate);
            }
        }

        // ── 2) Spine-chain aim — bends the torso so the gun points at the player ───
        // The gun barrel (shooter.firePoint) is a descendant of the spine
        // (spine → chest → shoulder → arm → hand → gun). We spread the rotation
        // needed to bring the barrel onto the player across the spine bones
        // (low → high), re-measuring the barrel direction after each bone so the
        // chain converges on the target. Distributing the twist keeps the
        // two-handed grip intact and the head level — instead of one bone snapping
        // ~90° (which is what tore the hand off the gun and pitched the face down).
        if (useProceduralAim && currentState == TerroristState.Engage &&
            _spineChain != null && shooter != null && shooter.firePoint != null)
        {
            Transform barrel  = shooter.firePoint;
            Vector3   aimAt   = _lastSeenPlayer.position;
            float     perBone = Mathf.Max(1f, maxAimAngle); // max degrees each bone may add

            // Capture the animation's support-hand grip RELATIVE to the weapon BEFORE
            // we bend the spine (the firing clip poses the left hand on the gun). After
            // aiming we restore that same relative grip so the hand rides with the gun.
            bool haveGrip = _leftHand != null && _leftForeArm != null && _leftUpperArm != null;
            Vector3    gripLocalPos = Vector3.zero;
            Quaternion gripLocalRot = Quaternion.identity;
            if (haveGrip)
            {
                gripLocalPos = barrel.InverseTransformPoint(_leftHand.position);
                gripLocalRot = Quaternion.Inverse(barrel.rotation) * _leftHand.rotation;
            }

            foreach (Transform bone in _spineChain)
            {
                if (bone == null) continue;

                Vector3 currentForward = barrel.forward;
                Vector3 desiredForward = aimAt - barrel.position;
                if (currentForward.sqrMagnitude < 1e-4f || desiredForward.sqrMagnitude < 1e-4f)
                    continue;

                Quaternion delta = Quaternion.FromToRotation(currentForward, desiredForward.normalized);
                delta = Quaternion.RotateTowards(Quaternion.identity, delta, perBone);
                bone.rotation = delta * bone.rotation; // reading barrel.forward next loop reflects this
            }

            // Re-lock the support hand to the same spot on the weapon.
            if (haveGrip)
            {
                Vector3 gripWorld = barrel.TransformPoint(gripLocalPos);
                SolveTwoBoneIK(_leftUpperArm, _leftForeArm, _leftHand, gripWorld, _leftForeArm.position);
                _leftHand.rotation = barrel.rotation * gripLocalRot;
            }
        }
    }

    /// <summary>
    /// Analytic two-bone IK (canonical "two-joint" solution). Rotates <paramref name="upper"/>
    /// and <paramref name="mid"/> so <paramref name="tip"/> reaches <paramref name="target"/>,
    /// with <paramref name="hint"/> steering the elbow's pole direction. Used to keep the
    /// terrorist's support hand welded to the rifle after the spine aims it.
    /// </summary>
    static void SolveTwoBoneIK(Transform upper, Transform mid, Transform tip,
                               Vector3 target, Vector3 hint)
    {
        Vector3 a = upper.position, b = mid.position, c = tip.position, t = target;

        float lab = (b - a).magnitude;
        float lcb = (b - c).magnitude;
        float lat = Mathf.Clamp((t - a).magnitude, 0.001f, lab + lcb - 0.001f);
        if (lab < 1e-4f || lcb < 1e-4f) return;

        float ac_ab_0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((c - a).normalized, (b - a).normalized), -1f, 1f));
        float ba_bc_0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((a - b).normalized, (c - b).normalized), -1f, 1f));
        float ac_at_0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((c - a).normalized, (t - a).normalized), -1f, 1f));

        float ac_ab_1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
        float ba_bc_1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));

        Vector3 axis0 = Vector3.Cross(c - a, hint - a);
        if (axis0.sqrMagnitude < 1e-8f) axis0 = Vector3.Cross(c - a, b - a);
        axis0.Normalize();
        Vector3 axis1 = Vector3.Cross(c - a, t - a);
        if (axis1.sqrMagnitude < 1e-8f) axis1 = axis0; else axis1.Normalize();

        Quaternion r0 = Quaternion.AngleAxis((ac_ab_1 - ac_ab_0) * Mathf.Rad2Deg,
                                             Quaternion.Inverse(upper.rotation) * axis0);
        Quaternion r1 = Quaternion.AngleAxis((ba_bc_1 - ba_bc_0) * Mathf.Rad2Deg,
                                             Quaternion.Inverse(mid.rotation) * axis0);
        Quaternion r2 = Quaternion.AngleAxis(ac_at_0 * Mathf.Rad2Deg,
                                             Quaternion.Inverse(upper.rotation) * axis1);

        upper.localRotation = upper.localRotation * r0 * r2;
        mid.localRotation   = mid.localRotation * r1;
    }

    /// <summary>
    /// Humanoid head/eye LookAt IK — makes the NPC physically turn its head (and lean
    /// the torso a little) to look AT the player while alerted or fighting, instead of
    /// staring straight ahead / down. Requires "IK Pass" on the animator base layer.
    /// </summary>
    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || !animator.isHuman) return;

        bool watchPlayer =
            _lastSeenPlayer != null && !_isInvestigating &&
            (currentState == TerroristState.Engage ||
             currentState == TerroristState.Alert  ||
             currentState == TerroristState.TakeCover);

        if (watchPlayer)
        {
            animator.SetLookAtPosition(_lastSeenPlayer.position);
            // (overall, body, head, eyes, clamp) — head leads, torso follows a little.
            animator.SetLookAtWeight(headLookWeight, headLookBodyWeight, 1f, 1f, 0.6f);
        }
        else
        {
            animator.SetLookAtWeight(0f);
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
            StopCombatPositioning();
        }

        // ── Cancel pending Suspicious timeout ─────────────────────────────────
        if (_suspiciousRoutine != null)
        {
            StopCoroutine(_suspiciousRoutine);
            _suspiciousRoutine = null;
        }

        // ── Cancel the Alert give-up timer (re-armed below if entering Alert) ──
        if (_alertGiveUpRoutine != null)
        {
            StopCoroutine(_alertGiveUpRoutine);
            _alertGiveUpRoutine = null;
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
                // Backstop: if nothing re-acquires us and no search is running,
                // give up after alertGiveUpTime and resume patrol.
                _alertGiveUpRoutine = StartCoroutine(AlertGiveUpTimeout());
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
                // Govern OUR OWN footing while firing: hold the standoff ring
                // (HoldAndShoot) or relocate between cover (CoverAndPeek). This
                // replaces blindly following a flank directive onto the player.
                _nextCombatTickTime = Time.time + escalationCombatInterval;
                StartCombatPositioning();
                break;

            case TerroristState.Idle:
                // Returning to patrol — drop any stale leader directive so we don't
                // immediately re-route to an old threat position on the next alert.
                _activeDirective = null;
                // Guardians don't use the shared patrol/wander idle modes — their movement
                // is the leashed GuardianRoamRoutine so they never drift off the hostage.
                // Returning to patrol means it lost the trainee → let any held hostage go.
                if (isHostageGuardian)
                {
                    if (guardedHostage != null && guardedHostage.IsHeld)
                        guardedHostage.ReleaseFromHold();
                    break;
                }
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

    // ── Hostage leverage (guardian only) ────────────────────────────────────────

    /// <summary>Per-frame guardian decision while it's hostile: fight normally by default,
    /// but (a) execute instantly if the trainee rushes the hostage, and (b) fall back on the
    /// human shield only once the situation turns high-pressure (escalated posture, wounded,
    /// or the trainee closing in). Called from Update.</summary>
    void GuardianPressureTick()
    {
        if (!isHostageGuardian) return;
        // Only relevant while actively threatened.
        if (currentState != TerroristState.Engage && currentState != TerroristState.Alert) return;
        if (guardedHostage == null) guardedHostage = FindNearestHostage();
        if (guardedHostage == null) return;
        if (guardedHostage.currentState == HostageState.Down ||
            guardedHostage.currentState == HostageState.Freed) return;

        // ONLY decide to use the hostage while the guardian CURRENTLY sees the trainee.
        // _lastSeenPlayer is the camera transform (its .position is always live), so it
        // can't tell us LOS — _playerVisible does. No current sight → it just fights/searches,
        // never grabbing or executing through a wall.
        if (!_playerVisible || _lastSeenPlayer == null) return;
        if (_leverageRoutine != null || guardedHostage.IsHeld) return;

        Vector3 hostagePos = guardedHostage.transform.position;
        Vector3 p = _lastSeenPlayer.position;
        float distToHostage = Vector3.Distance(new Vector3(p.x, hostagePos.y, p.z), hostagePos);

        // Reach for the shield only when the situation is genuinely high-pressure.
        bool dangerClose  = distToHostage <= shieldTriggerRange;
        bool highPressure = posture == CombatPosture.CoverAndPeek ||
                            currentHealth <= retreatHealthThreshold ||
                            dangerClose;
        if (highPressure)
        {
            Debug.Log($"[TerroristController] {gameObject.name}: high-pressure (posture={posture}, " +
                      $"hp={currentHealth:F0}, dangerClose={dangerClose}) — grabbing the hostage as a shield.");
            StartHostageLeverage();
        }
    }

    /// <summary>Begin the leverage sequence: seize the guarded hostage as a human shield
    /// and start the execution countdown. Called when a guardian first becomes aware of the
    /// trainee (Alert/Engage). Idempotent — safe to call every time those states are entered.</summary>
    void StartHostageLeverage()
    {
        if (!isHostageGuardian) return;
        if (guardedHostage == null) guardedHostage = FindNearestHostage();  // lazy auto-bind
        if (guardedHostage == null) return;
        if (_leverageRoutine != null) return;                       // already running
        if (guardedHostage.currentState == HostageState.Down ||
            guardedHostage.currentState == HostageState.Freed) return;
        _leverageRoutine = StartCoroutine(HostageLeverageRoutine());
    }

    HostageController FindNearestHostage()
    {
        HostageController best = null;
        float bestSqr = float.MaxValue;
        foreach (var npc in NPCRegistry.GetAll())
        {
            if (npc is HostageController hc &&
                hc.currentState != HostageState.Down &&
                hc.currentState != HostageState.Freed)
            {
                float d = (hc.transform.position - transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = hc; }
            }
        }
        return best;
    }

    IEnumerator HostageLeverageRoutine()
    {
        // Seize the hostage: it kneels at gunpoint and stops reacting to ambient stimuli.
        guardedHostage.SeizeAsLeverage(this);
        Debug.Log($"[TerroristController] {gameObject.name}: seized {guardedHostage.NPCId} as a shield — " +
                  $"it will warn if you close in, and execute if you push past the warning (or after ~{executionCountdown:F0}s).");

        float threat = 0f;
        bool  warnedOnce = false;

        while (guardedHostage != null && guardedHostage.IsHeld && currentState != TerroristState.Down)
        {
            // Guardian disengaged (gave up, lost the trainee, back to patrol) → let the
            // hostage go. A guardian that isn't in an active standoff does not execute.
            if (currentState != TerroristState.Engage &&
                currentState != TerroristState.Alert  &&
                currentState != TerroristState.TakeCover)
            {
                guardedHostage.ReleaseFromHold();
                break;
            }

            Vector3 hostagePos = guardedHostage.transform.position;

            // The whole graduated threat runs ONLY while the guardian currently SEES the
            // trainee. No line of sight → warning drops, countdown pauses (it can't track).
            if (_playerVisible && _lastSeenPlayer != null)
            {
                Vector3 p = _lastSeenPlayer.position;
                float distPH = Vector3.Distance(new Vector3(p.x, hostagePos.y, p.z), hostagePos);

                if (distPH <= executeRange)
                {
                    // Trainee pushed past the warning into the kill range → shoot the hostage.
                    ExecuteHostage("trainee_pushed_in");
                    yield break;
                }
                else if (distPH <= warningRange)
                {
                    // WARNING beat — turn and aim AT the hostage so the trainee sees the threat.
                    if (!_hostageWarning)
                    {
                        _hostageWarning = true;
                        shooter?.StopFiring();  // stop shooting the trainee — threaten the hostage instead
                        if (!warnedOnce)
                        {
                            warnedOnce = true;
                            Debug.LogWarning($"[TerroristController] {gameObject.name}: WARNING — aiming at " +
                                             $"{guardedHostage.NPCId}. Trainee too close — back off or it dies.");
                        }
                    }
                }
                else
                {
                    // SHIELD beat — standoff distance: use the hostage as cover, keep firing at the trainee.
                    if (_hostageWarning)
                    {
                        _hostageWarning = false;
                        shooter?.StartFiring();
                    }
                    if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                    {
                        Vector3 away = hostagePos - p; away.y = 0f;
                        if (away.sqrMagnitude > 0.0001f)
                        {
                            Vector3 shieldPos = hostagePos + away.normalized * shieldStandoff;
                            if (Vector3.Distance(transform.position, shieldPos) > 0.5f)
                            {
                                agent.isStopped = false;
                                agent.SetDestination(shieldPos);
                            }
                        }
                    }
                }

                // Standoff backstop — held in view too long without being neutralised.
                threat += Time.deltaTime;
                if (threat >= executionCountdown)
                {
                    ExecuteHostage("standoff_expired");
                    yield break;
                }
            }
            else
            {
                // Lost sight — drop the warning beat; countdown paused (doesn't accrue).
                _hostageWarning = false;
            }

            yield return null;
        }

        _hostageWarning = false;
        _leverageRoutine = null;
    }

    void ExecuteHostage(string cause)
    {
        _leverageRoutine = null;
        _hostageWarning = false;
        if (guardedHostage == null) return;
        Debug.LogWarning($"[TerroristController] {gameObject.name}: EXECUTING {guardedHostage.NPCId} ({cause}).");
        shooter?.StopFiring();
        guardedHostage.Execute(this);
    }

    // ── Guardian leashed roam (patrol around hostage / suspicious search) ─────────

    /// <summary>Keeps a guardian moving realistically around its hostage WITHOUT ever
    /// leaving it: a gentle patrol within guardPatrolRadius while relaxed, and a tenser
    /// search within guardLeashRadius while Suspicious/Alert. Never runs during combat
    /// (Engage holds & fights) or while the hostage is seized as a shield.</summary>
    IEnumerator GuardianRoamRoutine()
    {
        while (guardedHostage == null)
        {
            guardedHostage = FindNearestHostage();
            if (guardedHostage == null) yield return new WaitForSeconds(0.5f);
        }

        while (currentState != TerroristState.Down)
        {
            bool relaxed    = currentState == TerroristState.Idle;
            bool suspicious = currentState == TerroristState.Suspicious ||
                              currentState == TerroristState.Alert;

            if ((!relaxed && !suspicious) ||
                guardedHostage == null ||
                guardedHostage.currentState == HostageState.Down ||
                guardedHostage.IsHeld ||
                agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            {
                yield return new WaitForSeconds(0.4f);
                continue;
            }

            Vector3 anchor = guardedHostage.transform.position;
            float radius = suspicious ? guardLeashRadius : guardPatrolRadius;

            if (TryPickPointAround(anchor, radius, out Vector3 target))
            {
                agent.isStopped = false;
                agent.SetDestination(target);

                float t = 0f;
                while (t < 6f &&
                       (currentState == TerroristState.Idle ||
                        currentState == TerroristState.Suspicious ||
                        currentState == TerroristState.Alert) &&
                       !guardedHostage.IsHeld &&
                       agent.isActiveAndEnabled && !agent.pathPending &&
                       agent.remainingDistance > agent.stoppingDistance + 0.25f)
                {
                    // Hard leash — never stray beyond the leash from the hostage.
                    if (Vector3.Distance(transform.position, guardedHostage.transform.position) > guardLeashRadius + 1f)
                        agent.SetDestination(guardedHostage.transform.position);
                    t += Time.deltaTime;
                    yield return null;
                }
                if (agent.isActiveAndEnabled && agent.isOnNavMesh) agent.ResetPath();
            }

            // Hold and watch between moves — brief/tense when suspicious, longer when relaxed.
            float dwell = suspicious ? Random.Range(1.5f, 3f) : Random.Range(3f, 7f);
            float waited = 0f;
            while (waited < dwell &&
                   (currentState == TerroristState.Idle ||
                    currentState == TerroristState.Suspicious ||
                    currentState == TerroristState.Alert) &&
                   !guardedHostage.IsHeld)
            {
                waited += Time.deltaTime;
                yield return null;
            }
        }
        _guardRoamRoutine = null;
    }

    bool TryPickPointAround(Vector3 center, float radius, out Vector3 result)
    {
        for (int i = 0; i < 8; i++)
        {
            Vector2 r = Random.insideUnitCircle * radius;
            Vector3 candidate = center + new Vector3(r.x, 0f, r.y);
            if (UnityEngine.AI.NavMesh.SamplePosition(candidate, out var hit, 2f, UnityEngine.AI.NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }
        }
        result = center;
        return false;
    }

    // ── Down state ────────────────────────────────────────────────────────────

    void EnterDownState(ScenarioEvent trigger)
    {
        // Stop all combat
        shooter?.StopFiring();

        // A dying guardian can no longer hold its hostage at gunpoint — release it so
        // the trainee can reach and escort it out. (StopAllCoroutines below kills the
        // leverage loop; this drops the hostage out of Held.)
        if (isHostageGuardian && guardedHostage != null && guardedHostage.IsHeld)
            guardedHostage.ReleaseFromHold();

        // Stop ALL active routines so the body doesn't keep wandering / patrolling / investigating.
        StopAllCoroutines();
        _wanderRoutine = null;
        _investigateRoutine = null;
        _suspiciousRoutine = null;
        _retreatRoutine = null;
        _alertGiveUpRoutine = null;
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
        // Don't time out while actively investigating — the investigation owns the
        // return-to-Idle when its search completes. This only catches a Suspicious
        // NPC that merely looked toward a sound and was never sent to search.
        if (currentState == TerroristState.Suspicious && !_isInvestigating)
            TransitionTo(TerroristState.Idle, null);
    }

    // ── Alert give-up ─────────────────────────────────────────────────────────

    /// <summary>
    /// Backstop that stops an NPC standing in Alert forever after the player breaks
    /// contact. After alertGiveUpTime, if the NPC is still Alert and is NOT actively
    /// searching (the last-known-position search owns its own return-to-patrol), it
    /// gives up and resumes its idle/patrol behaviour.
    /// </summary>
    IEnumerator AlertGiveUpTimeout()
    {
        yield return new WaitForSeconds(alertGiveUpTime);

        // Still alert and not mid-search → no contact regained, stand down.
        if (currentState == TerroristState.Alert && !_isInvestigating)
        {
            Debug.Log($"[TerroristController] {gameObject.name}: lost contact — giving up, resuming patrol.");
            TransitionTo(TerroristState.Idle, null);
        }

        _alertGiveUpRoutine = null;
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
    [Tooltip("On hearing a gunshot, the investigator first turns to face the sound and scans " +
             "for this many seconds BEFORE walking over. If it spots the player in that window " +
             "it engages instead of investigating.")]
    public float lookBeforeInvestigateTime = 1.5f;

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
    public void InvestigatePosition(Vector3 soundPos, bool allowEscalation = false)
    {
        // Guardian never leaves the hostage to investigate — its search is the leashed
        // GuardianRoamRoutine, so it just declines here (silently; this fires on every
        // gunshot and used to spam the console).
        if (isHostageGuardian)
            return;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
        {
            Debug.LogWarning($"[TerroristController] {gameObject.name}: InvestigatePosition ABORTED — NavMeshAgent unusable " +
                             $"(agent={(agent == null ? "null" : "present")}, " +
                             $"active={(agent != null && agent.isActiveAndEnabled)}, " +
                             $"onNavMesh={(agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)}). NPC cannot walk to investigate.");
            return;
        }
        // Allow both Suspicious (gunshot) and Alert (squad-member down) investigations
        if (currentState != TerroristState.Suspicious && currentState != TerroristState.Alert)
        {
            Debug.Log($"[TerroristController] {gameObject.name}: InvestigatePosition skipped — state is {currentState}, " +
                      "not Suspicious/Alert (only those investigate).");
            return;
        }

        Debug.Log($"[TerroristController] {gameObject.name}: InvestigatePosition ACCEPTED → walking to {soundPos:F1} " +
                  $"(state={currentState}, allowEscalation={allowEscalation}).");
        _pendingEscalation = allowEscalation;
        if (_investigateRoutine != null) StopCoroutine(_investigateRoutine);
        _investigateRoutine = StartCoroutine(InvestigateRoutine(soundPos));
    }

    /// <summary>
    /// Squad-backup entry point: a non-guardian member dispatched to sweep an area
    /// after the first investigator turned up nothing. Forces it into a searching
    /// state and runs the investigation (without re-escalating again).
    /// </summary>
    public void DispatchToInvestigate(Vector3 area)
    {
        if (isHostageGuardian) return;
        if (currentState == TerroristState.Down || currentState == TerroristState.Engage) return;
        if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return;

        SetLookTarget(area);
        if (currentState != TerroristState.Alert)
            TransitionTo(TerroristState.Alert, null);
        InvestigatePosition(area, allowEscalation: false);
    }

    IEnumerator InvestigateRoutine(Vector3 soundPos)
    {
        Debug.Log($"[TerroristController] {gameObject.name}: investigating position at {soundPos}");

        var startState = currentState;
        bool escalate  = _pendingEscalation; // capture; a later call may overwrite the field
        _isInvestigating = true;
        animator?.SetBool("Investigating", true);

        // ── Phase 0: Look first — turn to face the sound and scan in place ────
        // The NPC hears a shot but hasn't moved yet: face the origin, hold, and
        // let perception work. If the player is spotted in this window the state
        // leaves startState (→ Engage) and we abort the walk entirely.
        SetLookTarget(soundPos);
        animator?.SetBool("Alert", true);
        {
            float looked = 0f;
            while (looked < lookBeforeInvestigateTime && currentState == startState)
            {
                // Rotate to face the sound while scanning.
                Vector3 dir = soundPos - transform.position; dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation,
                        Quaternion.LookRotation(dir), Time.deltaTime * 6f);
                looked += Time.deltaTime;
                yield return null;
            }
        }
        if (currentState != startState) { EndInvestigation(); yield break; }

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
        // Whether the search began from Suspicious (heard a gunshot) or Alert
        // (lost sight of the player and hunted their last-known position), if the
        // sweep completes without re-acquiring, the area is clear → resume patrol.
        if (currentState == startState &&
            (startState == TerroristState.Suspicious || startState == TerroristState.Alert))
        {
            Debug.Log($"[TerroristController] {gameObject.name}: area clear, returning to Idle");

            // First investigator turned up nothing → call the rest of the squad to
            // sweep the same area (second wave). Backups don't escalate again, so
            // this is bounded to one extra wave.
            if (escalate && !string.IsNullOrEmpty(squadId))
                Squad.Get(squadId)?.EscalateInvestigation(soundPos, this);

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
        // Guardian never leaves the hostage to follow Converge/Flank orders.
        if (isHostageGuardian) return;

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

        Vector3 target = _activeDirective.TargetPosition;
        switch (_activeDirective.Type)
        {
            case LeaderDirectiveType.Converge:
                // Close on the threat but stop on the standoff ring — never on top of the player.
                agent.SetDestination(ApproachRingPoint(target));
                break;

            case LeaderDirectiveType.Flank:
                // Flank to the side, then ensure the chosen point isn't inside the standoff ring.
                agent.SetDestination(EnforceStandoff(ComputeFlankPoint(target), target));
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
