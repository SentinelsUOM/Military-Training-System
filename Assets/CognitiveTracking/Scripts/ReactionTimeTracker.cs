using System.Collections.Generic;
using TeamSentinels.Module4.Data;
using TeamSentinels.Module4.Logging;
using UnityEngine;

namespace TeamSentinels.CognitiveTracking
{
    /// <summary>
    /// Measures the trainee's reaction time to threat stimuli.
    ///
    /// Concept
    ///   A *stimulus* is an enemy-caused scenario event the trainee should react to:
    ///     • PlayerSeen       — an enemy established line of sight on the trainee
    ///     • GunshotHeard     — enemy gunfire (own-weapon shots are filtered out)
    ///     • TargetConfirmed  — an enemy committed to engaging the trainee
    ///   When one arrives, a response window (default 3 s) opens and the tracker
    ///   watches four *response channels* derived from the tracked body:
    ///     • head     — head snaps toward the threat (angular speed over threshold
    ///                  while the gaze angle to the threat is closing, or the threat
    ///                  enters the ~25° gaze cone from outside it)
    ///     • hands    — either hand accelerates above threshold (weapon raise)
    ///     • movement — horizontal body speed rises above threshold (going to cover)
    ///     • trigger  — the trainee fires their weapon
    ///   Reaction time = first channel to fire − stimulus time.
    ///
    /// Validity rules
    ///   • A channel already above its threshold AT the stimulus is disqualified for
    ///     that stimulus — motion that was already underway is not a reaction.
    ///   • The first 80 ms after the stimulus are ignored (faster than human RT).
    ///   • While a window is open, new stimuli are ignored; after it closes a short
    ///     cooldown suppresses bursts (e.g. automatic fire) from spamming samples.
    ///   • No response within the window → recorded as a miss (reactionTime = -1).
    ///
    /// Output
    ///   Each measurement is appended to the session's movementTrack (reactions +
    ///   incrementally-updated reactionStats) and each valid RT is also fed to
    ///   SessionLogger.LogReactionTime, which drives cognitiveSummary
    ///   averageReactionTime / peakReactionTime on the Summary tab and home stats.
    /// </summary>
    [DefaultExecutionOrder(30002)] // after the HUD and recorder have updated
    public class ReactionTimeTracker : MonoBehaviour
    {
        [Header("Response window")]
        [Tooltip("Seconds after a stimulus in which a response counts.")]
        [SerializeField] float responseWindow = 3.0f;
        [Tooltip("Seconds after a completed measurement before a new stimulus is accepted.")]
        [SerializeField] float cooldown = 2.0f;

        [Header("Channel thresholds")]
        [Tooltip("Head angular speed (deg/s) that counts as an orienting response.")]
        [SerializeField] float headTurnThreshold = 90f;
        [Tooltip("Gaze cone half-angle (deg): threat entering it counts as acquired.")]
        [SerializeField] float gazeAcquireAngle = 25f;
        [Tooltip("Hand speed (m/s) that counts as a weapon-raise response.")]
        [SerializeField] float handSpeedThreshold = 1.0f;
        [Tooltip("Horizontal body speed (m/s) that counts as a locomotion response.")]
        [SerializeField] float moveSpeedThreshold = 0.6f;

        const float MinValidRt = 0.08f;

        CognitiveMovementRecorder _recorder;

        // Smoothed motion state
        float _yawSpeed, _handSpeed, _bodySpeed;
        float _prevYaw;
        Vector3 _prevHead, _prevLHand, _prevRHand;
        bool _hasPrev;

        // Open stimulus window
        bool _windowOpen;
        float _stimTime;                  // Time.time at stimulus
        Vector3 _stimOrigin;
        string _stimType, _stimActor;
        float _angleAtStimulus;
        bool _headEligible, _handsEligible, _moveEligible;
        float _cooldownUntil;

        readonly List<float> _validRts = new List<float>();

        void Awake()
        {
            _recorder = GetComponent<CognitiveMovementRecorder>();
        }

        void OnEnable()
        {
            EventManager.OnEventRaised += OnScenarioEvent;
        }

        void OnDisable()
        {
            EventManager.OnEventRaised -= OnScenarioEvent;
        }

        // ── Stimulus intake ──────────────────────────────────────────────────

        void OnScenarioEvent(ScenarioEvent e)
        {
            var logger = SessionLogger.Instance;
            if (logger == null || !logger.IsSessionActive) return;

            bool enemySourced = e.Instigator != null &&
                e.Instigator.name.ToLowerInvariant().Contains("terrorist");

            // Trainee fired — that's a response if a window is open, never a stimulus.
            if (e.Type == ScenarioEventType.ShotFired && !enemySourced)
            {
                if (_windowOpen && Time.time - _stimTime >= MinValidRt)
                    CloseWindow(Time.time - _stimTime, "trigger");
                return;
            }

            if (!enemySourced) return;
            if (e.Type != ScenarioEventType.PlayerSeen &&
                e.Type != ScenarioEventType.GunshotHeard &&
                e.Type != ScenarioEventType.TargetConfirmed) return;

            if (_windowOpen || Time.time < _cooldownUntil) return;

            Transform head = HeadTransform();
            if (head == null) return;

            _windowOpen = true;
            _stimTime = Time.time;
            _stimOrigin = e.Origin;
            _stimType = e.Type.ToString();
            _stimActor = e.Instigator.name;
            _angleAtStimulus = AngleToThreat(head);

            // Channels already in motion at the stimulus are not valid responses.
            _headEligible = _yawSpeed < headTurnThreshold;
            _handsEligible = _handSpeed < handSpeedThreshold;
            _moveEligible = _bodySpeed < moveSpeedThreshold;
        }

        // ── Per-frame motion tracking + window evaluation ────────────────────

        void Update()
        {
            Transform head = HeadTransform();
            if (head == null) return;

            UpdateMotionState(head);

            if (!_windowOpen) return;

            var logger = SessionLogger.Instance;
            if (logger == null || !logger.IsSessionActive)
            {
                _windowOpen = false; // session ended mid-window — discard
                return;
            }

            float elapsed = Time.time - _stimTime;
            if (elapsed > responseWindow)
            {
                CloseWindow(-1f, "none");
                return;
            }
            if (elapsed < MinValidRt) return;

            float angleNow = AngleToThreat(head);

            // Head: turning fast while closing on the threat, or threat acquired
            // in the gaze cone from clearly outside it.
            if (_headEligible &&
                ((_yawSpeed > headTurnThreshold && angleNow < _angleAtStimulus - 5f) ||
                 (angleNow < gazeAcquireAngle && _angleAtStimulus > gazeAcquireAngle + 10f)))
            {
                CloseWindow(elapsed, "head");
                return;
            }

            if (_handsEligible && _handSpeed > handSpeedThreshold)
            {
                CloseWindow(elapsed, "hands");
                return;
            }

            if (_moveEligible && _bodySpeed > moveSpeedThreshold)
            {
                CloseWindow(elapsed, "movement");
            }
        }

        void UpdateMotionState(Transform head)
        {
            float dt = Time.deltaTime;
            if (dt <= 1e-5f) return;

            var hud = CognitiveMirrorHud.Instance;
            Transform lHand = hud != null ? hud.LeftHandTransform : null;
            Transform rHand = hud != null ? hud.RightHandTransform : null;

            Vector3 headPos = head.position;
            Vector3 f = head.forward;
            Vector3 fFlat = new Vector3(f.x, 0f, f.z);
            float yaw = fFlat.sqrMagnitude > 1e-4f
                ? Mathf.Atan2(fFlat.x, fFlat.z) * Mathf.Rad2Deg
                : _prevYaw;

            if (_hasPrev)
            {
                // Light exponential smoothing keeps single-frame tracking jitter
                // from spiking a channel over threshold.
                float k = 1f - Mathf.Exp(-dt / 0.08f);

                float yawSpeed = Mathf.Abs(Mathf.DeltaAngle(_prevYaw, yaw)) / dt;
                _yawSpeed = Mathf.Lerp(_yawSpeed, yawSpeed, k);

                Vector3 flat = headPos - _prevHead;
                flat.y = 0f;
                _bodySpeed = Mathf.Lerp(_bodySpeed, flat.magnitude / dt, k);

                float handSpeed = 0f;
                if (lHand != null) handSpeed = (lHand.position - _prevLHand).magnitude / dt;
                if (rHand != null) handSpeed = Mathf.Max(handSpeed, (rHand.position - _prevRHand).magnitude / dt);
                _handSpeed = Mathf.Lerp(_handSpeed, handSpeed, k);
            }

            _prevYaw = yaw;
            _prevHead = headPos;
            if (lHand != null) _prevLHand = lHand.position;
            if (rHand != null) _prevRHand = rHand.position;
            _hasPrev = true;
        }

        // ── Recording ────────────────────────────────────────────────────────

        void CloseWindow(float reactionTime, string channel)
        {
            _windowOpen = false;
            _cooldownUntil = Time.time + cooldown;

            var logger = SessionLogger.Instance;
            var track = _recorder != null ? _recorder.CurrentTrack : null;
            if (logger == null || track == null) return;

            var sample = new ReactionSample
            {
                t = _stimTime - logger.SessionStartTime,
                stimulus = _stimType,
                actor = _stimActor,
                angleToThreat = _angleAtStimulus,
                reactionTime = reactionTime,
                channel = channel
            };
            track.reactions.Add(sample);

            if (reactionTime >= 0f)
            {
                _validRts.Add(reactionTime);
                logger.LogReactionTime(reactionTime);
            }

            UpdateStats(track.reactionStats, sample);

            Debug.Log($"[ReactionTimeTracker] {_stimType} by {_stimActor} (offset {_angleAtStimulus:F0}°) → " +
                      (reactionTime >= 0f ? $"{channel} in {reactionTime:F2}s" : "no response"));
        }

        void UpdateStats(ReactionStats s, ReactionSample sample)
        {
            s.stimulusCount++;
            if (sample.reactionTime < 0f)
            {
                s.missedCount++;
            }
            else
            {
                s.respondedCount++;
                switch (sample.channel)
                {
                    case "head": s.headResponses++; break;
                    case "hands": s.handResponses++; break;
                    case "movement": s.moveResponses++; break;
                    case "trigger": s.triggerResponses++; break;
                }
            }

            // Angle average over all stimuli (how far off-gaze threats appeared).
            s.avgAngleToThreat += (sample.angleToThreat - s.avgAngleToThreat) / s.stimulusCount;

            if (_validRts.Count > 0)
            {
                float sum = 0f, best = float.MaxValue, worst = 0f;
                foreach (float rt in _validRts)
                {
                    sum += rt;
                    if (rt < best) best = rt;
                    if (rt > worst) worst = rt;
                }
                s.avgReactionTime = sum / _validRts.Count;
                s.bestReactionTime = best;
                s.worstReactionTime = worst;

                var sorted = new List<float>(_validRts);
                sorted.Sort();
                int mid = sorted.Count / 2;
                s.medianReactionTime = sorted.Count % 2 == 1
                    ? sorted[mid]
                    : (sorted[mid - 1] + sorted[mid]) * 0.5f;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        static Transform HeadTransform()
        {
            var hud = CognitiveMirrorHud.Instance;
            if (hud != null && hud.HeadTransform != null) return hud.HeadTransform;
            return Camera.main != null ? Camera.main.transform : null;
        }

        float AngleToThreat(Transform head)
        {
            Vector3 to = _stimOrigin - head.position;
            Vector3 toFlat = new Vector3(to.x, 0f, to.z);
            Vector3 f = head.forward;
            Vector3 fFlat = new Vector3(f.x, 0f, f.z);
            if (toFlat.sqrMagnitude < 1e-4f || fFlat.sqrMagnitude < 1e-4f) return 0f;
            return Vector3.Angle(fFlat, toFlat);
        }
    }
}
