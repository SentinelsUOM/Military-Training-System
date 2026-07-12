// Module 4 | Sentinels | University of Moratuwa | 2026

using System;
using System.Collections;
using System.Collections.Generic;
using TeamSentinels.Module4.Data;
using UnityEngine;

namespace TeamSentinels.Module4.Logging
{
    /// <summary>
    /// MonoBehaviour that samples registered actor transforms and FSM states
    /// at a fixed interval and builds a list of ReplayFrames for data-visualisation replay.
    /// Attach to any persistent GameObject in the scene (e.g. the SessionManager).
    /// </summary>
    public class ReplayRecorder : MonoBehaviour
    {
        #region Data

        [Tooltip("Seconds between replay samples. 0.1s x 5 actors = 50 frames/sec, which blew the " +
                 "session JSON up to 11 MB and made the dashboard POST take 16 minutes. 0.5s is " +
                 "ample for a replay path.")]
        [SerializeField] private float recordInterval = 0.5f;

        [Tooltip("Hard cap on replay frames. On reaching it the recorder halves the data and the " +
                 "sample rate instead of growing forever, so the uploaded payload stays bounded.")]
        [SerializeField] private int maxFrames = 4000;

        private readonly List<ReplayFrame> _frames = new List<ReplayFrame>();

        private readonly List<ActorEntry> _actors = new List<ActorEntry>();

        private Coroutine _recordingCoroutine;
        private bool      _recording;

        private class ActorEntry
        {
            public string         actorId;
            public Transform      transform;
            public Func<string>   getState;
        }

        #endregion

        #region Unity Lifecycle

        private void OnDestroy()
        {
            if (_recordingCoroutine != null)
                StopCoroutine(_recordingCoroutine);
        }

        #endregion

        #region Public API

        /// <summary>
        /// Registers an actor for position/state capture each recording tick.
        /// <paramref name="getState"/> is a delegate that returns the actor's current FSM state string.
        /// </summary>
        public void RegisterActor(string actorId, Transform t, Func<string> getState)
        {
            if (string.IsNullOrEmpty(actorId) || t == null || getState == null)
            {
                Debug.LogWarning("[ReplayRecorder] RegisterActor received null/empty argument.");
                return;
            }

            // Prevent duplicate registration
            foreach (var existing in _actors)
            {
                if (existing.actorId == actorId)
                {
                    existing.transform = t;
                    existing.getState  = getState;
                    return;
                }
            }

            _actors.Add(new ActorEntry { actorId = actorId, transform = t, getState = getState });
            Debug.Log($"[ReplayRecorder] Registered actor: {actorId}");
        }

        /// <summary>Unregisters a previously registered actor.</summary>
        public void UnregisterActor(string actorId)
        {
            _actors.RemoveAll(a => a.actorId == actorId);
        }

        /// <summary>Starts capturing frames. Clears any previously recorded data.</summary>
        public void StartRecording()
        {
            if (_recording)
            {
                Debug.LogWarning("[ReplayRecorder] Already recording.");
                return;
            }

            _frames.Clear();
            _recording = true;
            _recordingCoroutine = StartCoroutine(RecordLoop());
            Debug.Log("[ReplayRecorder] Recording started.");
        }

        /// <summary>
        /// Stops capturing and returns the full list of recorded ReplayFrames.
        /// Also passes the frames to SessionLogger if one exists.
        /// </summary>
        public List<ReplayFrame> StopRecording()
        {
            _recording = false;
            if (_recordingCoroutine != null)
            {
                StopCoroutine(_recordingCoroutine);
                _recordingCoroutine = null;
            }

            Debug.Log($"[ReplayRecorder] Recording stopped. {_frames.Count} frames captured.");

            if (SessionLogger.Instance != null)
                SessionLogger.Instance.AddReplayFrames(_frames);

            return new List<ReplayFrame>(_frames);
        }

        #endregion

        #region Private

        private IEnumerator RecordLoop()
        {
            var wait = new WaitForSeconds(recordInterval);
            while (_recording)
            {
                CaptureFrame();
                TrimIfTooLarge();
                yield return wait;
            }
        }

        /// <summary>
        /// Hard ceiling on replay size. At 0.1s x 5 actors this recorded 50 frames/second —
        /// one real session produced 34,465 frames and an 11.3 MB session JSON, which took
        /// the dashboard 16 MINUTES to POST and then died with ECONNRESET. A replay scatter
        /// plot needs nothing like that resolution. When we hit the cap we halve the data
        /// (keep every 2nd frame) and halve the sample rate, so the recording keeps running
        /// at lower resolution instead of growing without bound. Payload stays bounded no
        /// matter how long the mission runs.
        /// </summary>
        private void TrimIfTooLarge()
        {
            if (_frames.Count < maxFrames) return;

            for (int i = _frames.Count - 1; i >= 0; i--)
                if (i % 2 == 1) _frames.RemoveAt(i);      // drop every other frame

            recordInterval *= 2f;                          // and sample half as often from now on
            Debug.Log($"[ReplayRecorder] Replay hit {maxFrames} frames — downsampled to " +
                      $"{_frames.Count} and dropped the sample rate to {recordInterval:F2}s. " +
                      "(Keeps the uploaded session payload bounded.)");
        }

        private void CaptureFrame()
        {
            // Use elapsed-since-session-start so frames align with event/state-change
            // timestamps. Falls back to Time.time if no session is active.
            float now = SessionLogger.Instance != null && SessionLogger.Instance.IsSessionActive
                ? Time.time - SessionLogger.Instance.SessionStartTime
                : Time.time;
            foreach (var actor in _actors)
            {
                if (actor.transform == null) continue;

                string state;
                try   { state = actor.getState?.Invoke() ?? "Unknown"; }
                catch { state = "Unknown"; }

                Vector3 euler = actor.transform.eulerAngles;
                _frames.Add(ReplayFrame.Create(
                    now,
                    actor.actorId,
                    actor.transform.position,
                    new Vector3Serializable(euler.x, euler.y, euler.z),
                    state));
            }
        }

        #endregion
    }
}
