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

    [Tooltip("Half-angle of the NPC's field of view in degrees.")]
    public float fovHalfAngle = 60f;

    [Tooltip("Seconds of sustained LOS before TargetConfirmed is raised.")]
    public float targetConfirmTime = 0.5f;

    [Tooltip("Seconds of broken LOS before PlayerLost is raised.")]
    public float lostSightDelay = 1.5f;

    [Header("Obstruction")]
    [Tooltip("Physics layers that block line of sight (walls, doors, obstacles).")]
    public LayerMask obstacleLayers;

    [Header("Telemetry")]
    [Tooltip("Emit a BehaviorSnapshot every N seconds (0 = disable).")]
    public float snapshotInterval = 5f;

    // ── Private state ─────────────────────────────────────────────────────────

    TerroristController _controller;

    bool  _playerVisible;         // current tick result
    float _continuousLosTime;     // seconds of unbroken LOS — for TargetConfirmed
    float _lostSightTimer;        // counts up when LOS is broken — for PlayerLost
    bool  _targetConfirmedRaised; // true once TargetConfirmed has fired this engagement

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

        Vector3 eye    = eyePosition != null ? eyePosition.position : transform.position;
        Vector3 target = playerTarget.position;
        Vector3 dir    = target - eye;
        float   dist   = dir.magnitude;

        // 1. Distance
        if (dist > detectionRange) return false;

        // 2. FOV — dot product of NPC forward vs direction to player
        float dot = Vector3.Dot(transform.forward, dir.normalized);
        if (dot < Mathf.Cos(fovHalfAngle * Mathf.Deg2Rad)) return false;

        // 3. Raycast (from eye to player chest — slightly below camera)
        Vector3 chest = target + Vector3.down * 0.2f;
        if (Physics.Raycast(eye, (chest - eye).normalized, dist, obstacleLayers,
                            QueryTriggerInteraction.Ignore))
            return false;

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
