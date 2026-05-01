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

        [SerializeField] private float recordInterval = 0.1f;

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
                yield return wait;
            }
        }

        private void CaptureFrame()
        {
            float now = Time.time;
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
