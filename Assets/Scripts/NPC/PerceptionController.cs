using UnityEngine;

/// <summary>
/// Handles visual and auditory perception for a terrorist NPC.
///
/// Visual (runs on a 0.2 s tick — NOT every frame):
///   1. Distance check  — player beyond detectionRange → skip
///   2. FOV check       — dot product against NPC forward vs fovHalfAngle
///   3. Raycast         — from eyePosition to player's chest, against obstacleLayers
///   4. PlayerSeen      — raised on first positive result (direct call to TerroristController)
///   5. TargetConfirmed — raised after targetConfirmTime of sustained LOS (Alert → Engage)
///   6. PlayerLost      — raised after lostSightDelay (1.5 s) when LOS breaks
///
/// Auditory:
///   Handled by EventManager + HostageController/TerroristController CanRespond() range check.
///   PerceptionController adds no extra auditory code — the hearing radius is enforced in
///   TerroristController.CanRespond for GunshotHeard events.
///
/// Telemetry:
///   BehaviorSnapshot is emitted every snapshotInterval seconds.
///
/// Setup:
///   1. Add to the same GameObject as TerroristController.
///   2. Assign playerTarget to the XR Main Camera (or player head transform).
///   3. Assign eyePosition to a child transform at the NPC's eye level.
///   4. Set obstacleLayers to the layer mask for walls / doors.
/// </summary>
[RequireComponent(typeof(TerroristController))]
public class PerceptionController : MonoBehaviour
{
    // ── Inspector ─────────────────────────────────────────────────────────────

    [Header("Player Reference")]
    [Tooltip("Assign the XR Main Camera or player head transform here.")]
    public Transform playerTarget;

    [Header("Eye Position")]
    [Tooltip("Child transform at the NPC's eye level. If null, uses this transform.")]
    public Transform eyePosition;

    [Header("Visual Detection")]
    [Tooltip("Maximum detection range in metres.")]
    public float detectionRange = 20f;

    [Tooltip("Half-angle of the NPC's field of view in degrees. Default 60° → a 120° total " +
             "horizontal cone, which corresponds to the human BINOCULAR visual field (the " +
             "overlap region of both eyes, ~114–120° in vision-science literature). Beyond this " +
             "lies monocular peripheral vision (~200° total), but that is motion-only and not " +
             "modelled here. 120° is the realistic upper bound for reliable target detection; " +
             "lower it (90–110°) if you want easier stealth/flanking gameplay.")]
    [Range(30f, 110f)]
    public float fovHalfAngle = 60f;

    [Tooltip("Seconds of sustained LOS before TargetConfirmed is raised.")]
    public float targetConfirmTime = 0.5f;

    [Tooltip("Seconds of broken LOS before PlayerLost is raised.")]
    public float lostSightDelay = 1.5f;

    [Header("Obstruction")]
    [Tooltip("OPTIONAL override of which physics layers block line of sight. Leave as " +
             "'Nothing' (the default) to block on EVERYTHING solid — walls, doors, props — " +
             "which is the robust choice and needs no per-scene layer setup. Only set this " +
             "if you have a dedicated 'Walls' layer and want to ignore everything else.")]
    public LayerMask obstacleLayers;

    [Tooltip("The line-of-sight ray stops this many metres short of the player so the player's " +
             "own body never counts as an obstacle that blocks sight of themselves.")]
    public float playerClearance = 0.5f;

    [Header("Telemetry")]
    [Tooltip("Emit a BehaviorSnapshot every N seconds (0 = disable).")]
    public float snapshotInterval = 5f;

    // ── Private state ─────────────────────────────────────────────────────────

    TerroristController _controller;

    bool  _playerVisible;         // current tick result
    float _continuousLosTime;     // seconds of unbroken LOS — for TargetConfirmed
    float _lostSightTimer;        // counts up when LOS is broken — for PlayerLost
    bool  _targetConfirmedRaised; // true once TargetConfirmed has fired this engagement

    readonly RaycastHit[] _losHits = new RaycastHit[16]; // reused LOS raycast buffer

    const float PerceptionTickRate = 0.2f;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        _controller = GetComponent<TerroristController>();
    }

    void Start()
    {
        // Use InvokeRepeating for 0.2 s perception tick (not Update)
        InvokeRepeating(nameof(PerceptionTick), PerceptionTickRate, PerceptionTickRate);

        if (snapshotInterval > 0f)
            InvokeRepeating(nameof(EmitSnapshot), snapshotInterval, snapshotInterval);

        // Auto-find player if not assigned
        if (playerTarget == null)
        {
            var cam = Camera.main;
            if (cam != null) playerTarget = cam.transform;
            else Debug.LogWarning($"[PerceptionController] {gameObject.name}: playerTarget not assigned " +
                                  "and Camera.main not found. Assign manually in Inspector.");
        }
    }

    void OnDestroy()
    {
        CancelInvoke(nameof(PerceptionTick));
        CancelInvoke(nameof(EmitSnapshot));
    }

    /// <summary>
    /// Re-arms TargetConfirmed so it can fire again WITHOUT requiring LOS to break
    /// first. Called by TerroristController when a retreat ends: if the player kept
    /// the NPC in continuous view through the whole retreat, the original confirm
    /// flag is still set and the NPC would otherwise never re-escalate to Engage.
    /// The confirm timer restarts, so re-engage still takes targetConfirmTime.
    /// </summary>
    public void RearmTargetConfirmation()
    {
        _targetConfirmedRaised = false;
        _continuousLosTime     = 0f;
    }

    // ── Perception tick ───────────────────────────────────────────────────────

    void PerceptionTick()
    {
        // Skip if NPC is down or player target is missing
        if (_controller.currentState == TerroristState.Down) return;
        if (playerTarget == null) return;

        bool canSeeNow = CheckVisibility();

        if (canSeeNow)
        {
            _lostSightTimer = 0f;

            if (!_playerVisible)
            {
                // First frame of LOS — raise PlayerSeen
                _playerVisible       = true;
                _continuousLosTime   = 0f;
                _targetConfirmedRaised = false;

                var e = new ScenarioEvent(
                    ScenarioEventType.PlayerSeen,
                    playerTarget.position,
                    gameObject);

                // Direct call to owning controller (bypasses CanRespond self-filter)
                _controller.HandleDetection(e);

                // Also raise globally for other systems (Module 3, hostages, etc.)
                EventManager.Instance?.Raise(e);
            }

            // Accumulate continuous LOS time
            _continuousLosTime += PerceptionTickRate;

            if (!_targetConfirmedRaised && _continuousLosTime >= targetConfirmTime)
            {
                _targetConfirmedRaised = true;

                var e = new ScenarioEvent(
                    ScenarioEventType.TargetConfirmed,
                    playerTarget.position,
                    gameObject);

                _controller.HandleDetection(e);
                EventManager.Instance?.Raise(e);
            }
        }
        else
        {
            _continuousLosTime = 0f;

            if (_playerVisible)
            {
                // LOS just broke — start confirmation timer
                _lostSightTimer += PerceptionTickRate;

                if (_lostSightTimer >= lostSightDelay)
                {
                    _playerVisible         = false;
                    _targetConfirmedRaised = false;
                    _lostSightTimer        = 0f;

                    var e = new ScenarioEvent(
                        ScenarioEventType.PlayerLost,
                        transform.position,
                        gameObject);

                    _controller.HandleDetection(e);
                    EventManager.Instance?.Raise(e);
                }
            }
        }
    }

    // ── Visibility check ──────────────────────────────────────────────────────

    bool CheckVisibility()
    {
        if (playerTarget == null) return false;

        // Eye origin: use the assigned eye transform, else estimate head height
        // (~1.6 m) so the LOS ray runs at eye level rather than from the feet.
        Vector3 eye    = eyePosition != null ? eyePosition.position
                                             : transform.position + Vector3.up * 1.6f;
        Vector3 target = playerTarget.position;
        Vector3 dir    = target - eye;
        float   dist   = dir.magnitude;

        // 1. Distance
        if (dist > detectionRange) return false;

        // 2. FOV — dot product of NPC forward vs direction to player
        float dot = Vector3.Dot(transform.forward, dir.normalized);
        if (dot < Mathf.Cos(fovHalfAngle * Mathf.Deg2Rad)) return false;

        // 3. Line of sight — is anything SOLID between the eye and the player?
        //
        // Cast toward the player's chest (slightly below the camera). We stop the
        // ray 'playerClearance' metres short so the player's own body never counts
        // as a blocker, and we skip any collider that belongs to THIS NPC (its own
        // body/weapon at the muzzle of the ray). If anything else is hit — a wall,
        // a door, a prop, another NPC — sight is blocked.
        //
        // The mask defaults to Everything (~0) when obstacleLayers is left unset,
        // so walls block sight WITHOUT needing a hand-configured layer per scene.
        // This is what stops NPCs "seeing"/shooting through walls.
        int mask = obstacleLayers.value != 0 ? obstacleLayers.value : ~0;

        Vector3 chest    = target + Vector3.down * 0.2f;
        Vector3 toChest  = chest - eye;
        float   losDist  = toChest.magnitude;
        Vector3 losDir   = toChest / Mathf.Max(0.0001f, losDist);
        float   checkLen = losDist - playerClearance;

        if (checkLen > 0f)
        {
            int n = Physics.RaycastNonAlloc(eye, losDir, _losHits, checkLen, mask,
                                            QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider col = _losHits[i].collider;
                if (col == null) continue;
                if (col.transform.IsChildOf(transform)) continue; // our own body/weapon
                return false;                                      // something is in the way
            }
        }

        return true;
    }

    // ── Telemetry snapshot ────────────────────────────────────────────────────

    void EmitSnapshot()
    {
        if (_controller.currentState == TerroristState.Down) return;

        TelemetryLogger.Instance?.LogSnapshot(
            _controller.NPCId,
            _controller.currentState.ToString(),
            transform.position,
            $"visible={_playerVisible} los={_continuousLosTime:F1}s");
    }

    // ── Gizmos ────────────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;

        Vector3 eye = eyePosition != null ? eyePosition.position : transform.position;

        // Detection range sphere
        Gizmos.color = _playerVisible ? Color.red : Color.yellow;
        Gizmos.DrawWireSphere(eye, detectionRange);

        // FOV lines
        float angle   = fovHalfAngle * Mathf.Deg2Rad;
        Vector3 left  = Quaternion.Euler(0, -fovHalfAngle, 0) * transform.forward * detectionRange;
        Vector3 right = Quaternion.Euler(0,  fovHalfAngle, 0) * transform.forward * detectionRange;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(eye, left);
        Gizmos.DrawRay(eye, right);

        // Line to player when visible
        if (_playerVisible && playerTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(eye, playerTarget.position);
        }
    }
}
