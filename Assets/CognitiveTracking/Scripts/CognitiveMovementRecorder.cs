using TeamSentinels.Module4.Data;
using TeamSentinels.Module4.Logging;
using UnityEngine;

namespace TeamSentinels.CognitiveTracking
{
    /// <summary>
    /// Records the trainee's body movement (head + hands, crouch, scanning,
    /// weapon-in-hand, health) at a fixed rate while a Module 4 session is
    /// active, and registers the track with SessionLogger so it is serialised
    /// into the session JSON and uploaded to the AAR dashboard with everything
    /// else at EndSession().
    ///
    /// Timestamps use the same clock as mission events and replay frames
    /// (seconds since StartSession), and positions are world-space so the
    /// dashboard can draw the movement path on top of the scenario layout.
    ///
    /// Tracked transforms come from CognitiveMirrorHud when present (single
    /// point of rig discovery); falls back to Camera.main head-only otherwise.
    /// </summary>
    [DefaultExecutionOrder(30001)] // sample after the HUD has refreshed its state
    public class CognitiveMovementRecorder : MonoBehaviour
    {
        [Tooltip("Samples per second. 5 Hz ≈ 3k samples for a 10-minute mission.")]
        [Range(1f, 20f)]
        [SerializeField] float sampleRateHz = 5f;

        [Tooltip("Speeds above this count as 'moving' in the aggregate stats (m/s).")]
        [SerializeField] float movingSpeedThreshold = 0.15f;

        [Tooltip("Crouch fraction above this counts as crouching in the stats.")]
        [SerializeField] float crouchThreshold = 0.35f;

        MovementTrack _track;
        bool _recording;

        /// <summary>The live track for the active session (null when not recording).
        /// ReactionTimeTracker appends its reaction measurements here so they ride
        /// along in the same movementTrack upload.</summary>
        public MovementTrack CurrentTrack => _recording ? _track : null;
        float _nextSampleTime;

        // Per-session accumulators / previous-sample state
        float _prevT;
        Vector3 _prevHead, _prevLHand, _prevRHand;
        float _prevYaw;
        bool _hasPrev;
        bool _wasCrouched;
        float _standingEyeY;
        float _speedSum, _angSpeedSum, _headHeightSum;
        int _statSamples;

        void Update()
        {
            var logger = SessionLogger.Instance;
            bool sessionActive = logger != null && logger.IsSessionActive;

            if (sessionActive && !_recording)
                BeginRecording(logger);
            else if (!sessionActive && _recording)
                _recording = false; // summary already captured the shared track instance

            if (!_recording) return;

            if (Time.time >= _nextSampleTime)
            {
                _nextSampleTime = Time.time + 1f / Mathf.Max(1f, sampleRateHz);
                TakeSample(logger);
            }
        }

        void BeginRecording(SessionLogger logger)
        {
            _track = new MovementTrack();
            _track.stats.sampleRateHz = sampleRateHz;
            _track.stats.minHeadHeight = float.MaxValue;
            logger.SetMovementTrack(_track);

            _recording = true;
            _hasPrev = false;
            _wasCrouched = false;
            _standingEyeY = 1.55f;
            _speedSum = _angSpeedSum = _headHeightSum = 0f;
            _statSamples = 0;
            _nextSampleTime = Time.time;
        }

        void TakeSample(SessionLogger logger)
        {
            var hud = CognitiveMirrorHud.Instance;

            Transform head = hud != null && hud.HeadTransform != null
                ? hud.HeadTransform
                : (Camera.main != null ? Camera.main.transform : null);
            if (head == null) return; // no tracked head yet — try again next tick

            Transform lHand = hud != null ? hud.LeftHandTransform : null;
            Transform rHand = hud != null ? hud.RightHandTransform : null;

            float t = Time.time - logger.SessionStartTime;
            Vector3 headPos = head.position;
            Vector3 lPos = lHand != null ? lHand.position : headPos;
            Vector3 rPos = rHand != null ? rHand.position : headPos;

            Vector3 f = head.forward;
            Vector3 fFlat = new Vector3(f.x, 0f, f.z);
            float yaw = fFlat.sqrMagnitude > 1e-4f
                ? Mathf.Atan2(fFlat.x, fFlat.z) * Mathf.Rad2Deg
                : _prevYaw;
            float pitch = Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;

            // Crouch estimate from head height above the rig floor, against a
            // running standing-height calibration (same approach as the HUD dummy).
            float floorY = hud != null ? hud.RigFloorY : 0f;
            float headHeight = headPos.y - floorY;
            if (headHeight > 0.15f) // ignore pre-tracking frames at floor level
                _standingEyeY = Mathf.Clamp(Mathf.Max(_standingEyeY, headHeight), 1.15f, 2.10f);
            float crouch = Mathf.Clamp01((_standingEyeY - headHeight) / (0.45f * _standingEyeY));

            float speed = 0f, angSpeed = 0f;
            if (_hasPrev)
            {
                float dt = Mathf.Max(1e-3f, t - _prevT);
                Vector3 flat = headPos - _prevHead;
                flat.y = 0f;
                speed = flat.magnitude / dt;
                angSpeed = Mathf.Abs(Mathf.DeltaAngle(_prevYaw, yaw)) / dt;
            }

            int gun = 0;
            float hp = 1f;
            if (hud != null)
            {
                if (hud.LeftWeaponHeld) gun |= 1;
                if (hud.RightWeaponHeld) gun |= 2;
                hp = hud.Health01;
            }

            _track.samples.Add(new MovementSample
            {
                t = t,
                head = headPos,
                lHand = lPos,
                rHand = rPos,
                yaw = yaw,
                pitch = pitch,
                speed = speed,
                angSpeed = angSpeed,
                crouch = crouch,
                gun = gun,
                hp = hp
            });

            UpdateStats(t, headPos, lPos, rPos, yaw, speed, angSpeed, crouch, headHeight, gun);

            _prevT = t;
            _prevHead = headPos;
            _prevLHand = lPos;
            _prevRHand = rPos;
            _prevYaw = yaw;
            _hasPrev = true;
        }

        void UpdateStats(float t, Vector3 head, Vector3 lHand, Vector3 rHand,
                         float yaw, float speed, float angSpeed, float crouch,
                         float headHeight, int gun)
        {
            var s = _track.stats;

            if (_hasPrev)
            {
                float dt = Mathf.Max(1e-3f, t - _prevT);
                Vector3 flat = head - _prevHead;
                flat.y = 0f;
                s.totalDistance += flat.magnitude;
                s.leftHandDistance += (lHand - _prevLHand).magnitude;
                s.rightHandDistance += (rHand - _prevRHand).magnitude;
                s.totalHeadYawDeg += Mathf.Abs(Mathf.DeltaAngle(_prevYaw, yaw));

                if (speed > movingSpeedThreshold) s.timeMoving += dt;
                else s.timeStill += dt;
                if (crouch > crouchThreshold) s.timeCrouched += dt;
                if (gun != 0) s.timeWeaponHeld += dt;
            }

            bool crouched = crouch > crouchThreshold;
            if (crouched && !_wasCrouched) s.crouchCount++;
            _wasCrouched = crouched;

            s.maxSpeed = Mathf.Max(s.maxSpeed, speed);
            s.peakAngSpeed = Mathf.Max(s.peakAngSpeed, angSpeed);
            s.minHeadHeight = Mathf.Min(s.minHeadHeight, headHeight);

            _statSamples++;
            _speedSum += speed;
            _angSpeedSum += angSpeed;
            _headHeightSum += headHeight;
            s.avgSpeed = _speedSum / _statSamples;
            s.avgAngSpeed = _angSpeedSum / _statSamples;
            s.avgHeadHeight = _headHeightSum / _statSamples;
        }
    }
}
