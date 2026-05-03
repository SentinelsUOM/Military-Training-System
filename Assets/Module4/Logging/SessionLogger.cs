// Module 4 | Sentinels | University of Moratuwa | 2026

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TeamSentinels.Module4.Analysis;
using TeamSentinels.Module4.Data;
using UnityEngine;

namespace TeamSentinels.Module4.Logging
{
    /// <summary>
    /// Singleton MonoBehaviour. Central hub for all Module 4 data collection.
    /// Other modules call the public API to log events, state changes, and cognitive data.
    /// On EndSession() it calculates scores, extracts incidents, and writes session JSON.
    /// </summary>
    public class SessionLogger : MonoBehaviour
    {
        #region Singleton

        public static SessionLogger Instance { get; private set; }

        /// <summary>True between StartSession() and EndSession(). Used by Module4Bridge to gate listener calls.</summary>
        public bool IsSessionActive => _sessionActive;

        /// <summary>Time.time captured at the most recent StartSession(). Used by Module4SessionController for elapsed checks.</summary>
        public float SessionStartTime => _sessionStartTime;

        #endregion

        #region Data

        private string _sessionId;
        private string _scenarioId;
        private float  _sessionStartTime;

        private readonly List<MissionEvent>      _events          = new List<MissionEvent>();
        private readonly List<NPCStateChange>    _npcStateChanges = new List<NPCStateChange>();
        private readonly List<HostageStateEntry> _hostageHistory  = new List<HostageStateEntry>();
        private readonly List<ReplayFrame>       _replayFrames    = new List<ReplayFrame>();

        // Cognitive accumulators
        private readonly List<float> _reactionTimes      = new List<float>();
        private readonly List<float> _movementInitScores = new List<float>();
        private readonly List<float> _stabilityScores    = new List<float>();
        private readonly List<float> _attentionScores    = new List<float>();

        private bool _sessionActive;

        #endregion

        #region Events

        /// <summary>Fired when EndSession() has finished building and saving the SessionSummary.</summary>
        public static event Action<SessionSummary> OnSessionComplete;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        #endregion

        #region Public API

        /// <summary>Begins a new logging session tied to the given scenario.</summary>
        public void StartSession(string scenarioId)
        {
            _sessionId        = Guid.NewGuid().ToString();
            _scenarioId       = scenarioId;
            _sessionStartTime = Time.time;
            _sessionActive    = true;

            _events.Clear();
            _npcStateChanges.Clear();
            _hostageHistory.Clear();
            _replayFrames.Clear();
            _reactionTimes.Clear();
            _movementInitScores.Clear();
            _stabilityScores.Clear();
            _attentionScores.Clear();

            Debug.Log($"[SessionLogger] Session started. id={_sessionId} scenario={_scenarioId}");
        }

        /// <summary>Logs a discrete mission event (e.g. ShotFired, DoorOpened).</summary>
        public void LogEvent(MissionEvent e)
        {
            if (!_sessionActive) return;
            _events.Add(e);
        }

        /// <summary>Logs an NPC FSM state transition.</summary>
        public void LogNPCStateChange(NPCStateChange change)
        {
            if (!_sessionActive) return;
            _npcStateChanges.Add(change);
        }

        /// <summary>Logs a hostage emotional-state transition and computes distress score.</summary>
        public void LogHostageStateChange(string hostageId, string newState,
            string triggerEvent, float timestamp)
        {
            if (!_sessionActive) return;
            _hostageHistory.Add(HostageStateEntry.Create(hostageId, timestamp, newState, triggerEvent));
        }

        /// <summary>
        /// Accumulates a cognitive measurement sample from Module 3.
        /// All values should be normalised 0–1 except reactionTime (seconds).
        /// </summary>
        public void LogCognitiveUpdate(float reactionTime, float movementInit,
            float stability, float attention)
        {
            if (!_sessionActive) return;
            _reactionTimes.Add(reactionTime);
            _movementInitScores.Add(movementInit);
            _stabilityScores.Add(stability);
            _attentionScores.Add(attention);
        }

        /// <summary>Accepts a pre-built ReplayFrame from ReplayRecorder.</summary>
        public void AddReplayFrames(List<ReplayFrame> frames)
        {
            if (!_sessionActive || frames == null) return;
            _replayFrames.AddRange(frames);
        }

        /// <summary>
        /// Ends the active session: calculates scores, extracts incidents,
        /// writes JSON to Module4/Resources/, and fires OnSessionComplete.
        /// </summary>
        public void EndSession()
        {
            if (!_sessionActive)
            {
                Debug.LogWarning("[SessionLogger] EndSession called with no active session.");
                return;
            }
            _sessionActive = false;

            float duration = Time.time - _sessionStartTime;

            // Determine hostage count from history (unique hostage IDs)
            int hostagesTotal = _hostageHistory
                .Select(h => h.hostageId)
                .Distinct()
                .Count();
            hostagesTotal = Mathf.Max(hostagesTotal, 1);

            PerformanceSummary perf = PerformanceCalculator.Calculate(
                _events, _hostageHistory, duration, hostagesTotal);

            List<IncidentRecord> incidents = IncidentExtractor.Extract(
                _events, _npcStateChanges, _hostageHistory);

            CognitiveSummary cog = BuildCognitiveSummary();

            var summary = new SessionSummary
            {
                sessionId       = _sessionId,
                scenarioId      = _scenarioId,
                timestamp       = DateTime.UtcNow,
                performance     = perf,
                cognitiveSummary = cog,
                events          = new List<MissionEvent>(_events),
                npcStateChanges = new List<NPCStateChange>(_npcStateChanges),
                hostageHistory  = new List<HostageStateEntry>(_hostageHistory),
                incidents       = incidents,
                replayFrames    = new List<ReplayFrame>(_replayFrames)
            };

            SaveToJson(summary);

            Debug.Log($"[SessionLogger] Session ended. Duration={duration:F1}s  " +
                      $"Score={perf.overallScore:P0}  Incidents={incidents.Count}");

            OnSessionComplete?.Invoke(summary);
        }

        #endregion

        #region Context Menu

        [ContextMenu("Simulate Test Session")]
        public void SimulateTestSession()
        {
            StartSession("SIMULATED_SCENARIO");

            float[] times = { 5f, 12f, 18f, 25f, 34f, 45f, 52f, 63f, 78f, 95f };
            string[] rooms = { "room_01", "room_02", "room_03" };

            // Events
            LogEvent(MissionEvent.Create("DoorOpened",            times[0], "trainee_01", null,        rooms[0]));
            LogEvent(MissionEvent.Create("RoomBreached",          times[1], "trainee_01", null,        rooms[1]));
            LogEvent(MissionEvent.Create("PlayerSeen",            times[2], "terrorist_01", "trainee_01", rooms[1]));
            LogEvent(MissionEvent.Create("GunshotHeard",          times[3], "terrorist_01", null,      rooms[1]));
            LogEvent(MissionEvent.Create("ShotFired",             times[4], "trainee_01", null,        rooms[1]));
            LogEvent(MissionEvent.Create("TerroristHit",          times[5], "trainee_01", "terrorist_01", rooms[1]));
            LogEvent(MissionEvent.Create("ShotFired",             times[6], "trainee_01", null,        rooms[2]));
            LogEvent(MissionEvent.Create("TerroristDown",         times[7], "trainee_01", "terrorist_02", rooms[2]));
            LogEvent(MissionEvent.Create("HostageContactStarted", times[8], "trainee_01", "hostage_01", rooms[2]));
            LogEvent(MissionEvent.Create("HostageFreed",          times[9], "trainee_01", "hostage_01", rooms[2]));

            // NPC state changes
            LogNPCStateChange(NPCStateChange.Create("terrorist_01", "Terrorist", "Idle",        "Suspicious", "GunshotHeard", 28f));
            LogNPCStateChange(NPCStateChange.Create("terrorist_01", "Terrorist", "Suspicious",  "Alert",      "PlayerSeen",   34f));
            LogNPCStateChange(NPCStateChange.Create("terrorist_02", "Terrorist", "Idle",        "Alert",      "GunshotHeard", 50f));
            LogNPCStateChange(NPCStateChange.Create("terrorist_02", "Terrorist", "Alert",       "Engage",     "PlayerSeen",   55f));

            // Hostage states
            LogHostageStateChange("hostage_01", "Calm",    "SessionStart",        5f);
            LogHostageStateChange("hostage_01", "Fearful", "GunshotHeard",        33f);
            LogHostageStateChange("hostage_01", "Panic",   "NearbyTerroristHit",  45f);
            LogHostageStateChange("hostage_01", "Follow",  "HostageFreed",        95f);

            // Cognitive samples
            LogCognitiveUpdate(0.42f, 0.75f, 0.80f, 0.70f);
            LogCognitiveUpdate(0.38f, 0.80f, 0.78f, 0.72f);
            LogCognitiveUpdate(0.55f, 0.60f, 0.65f, 0.55f);

            EndSession();
        }

        #endregion

        #region Private

        private CognitiveSummary BuildCognitiveSummary()
        {
            float Avg(List<float> list) => list.Count == 0 ? 0f : list.Average();
            float Max(List<float> list) => list.Count == 0 ? 0f : list.Max();

            var cog = new CognitiveSummary
            {
                averageReactionTime     = Avg(_reactionTimes),
                peakReactionTime        = Max(_reactionTimes),
                movementInitiationScore = Avg(_movementInitScores),
                stabilityScore          = Avg(_stabilityScores),
                attentionScore          = Avg(_attentionScores)
            };
            cog.DeriveLabels();
            return cog;
        }

        private static void SaveToJson(SessionSummary summary)
        {
            string dir = Path.Combine(Application.dataPath, "Module4", "Resources");
            Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, $"session_{summary.sessionId}.json");
            string json = JsonConvert.SerializeObject(summary, Formatting.Indented,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Include });

            File.WriteAllText(path, json);
            Debug.Log($"[SessionLogger] Saved → {path}");
        }

        #endregion
    }
}
