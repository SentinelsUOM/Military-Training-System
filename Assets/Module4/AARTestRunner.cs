// Module 4 | Sentinels | University of Moratuwa | 2026

using System.Collections.Generic;
using TeamSentinels.Module4.Data;
using TeamSentinels.Module4.Logging;
using UnityEngine;

namespace TeamSentinels.Module4
{
    /// <summary>
    /// Developer test harness. Attach to any GameObject in the scene,
    /// then right-click the component header → "Run AAR Test" to exercise
    /// the full Module 4 pipeline without running the VR mission.
    /// Output JSON is written to Assets/Module4/Resources/.
    /// </summary>
    public class AARTestRunner : MonoBehaviour
    {
        #region Unity Lifecycle

        private void Start()
        {
            // Auto-ensure a SessionLogger exists in the scene during testing
            if (SessionLogger.Instance == null)
            {
                var go = new GameObject("[SessionLogger]");
                go.AddComponent<SessionLogger>();
                DontDestroyOnLoad(go);
            }
        }

        #endregion

        #region Public API

        [ContextMenu("Run AAR Test")]
        public void RunAARTest()
        {
            if (SessionLogger.Instance == null)
            {
                Debug.LogError("[AARTestRunner] SessionLogger.Instance is null. " +
                               "Make sure a SessionLogger is in the scene or press Play first.");
                return;
            }

            SessionLogger.Instance.StartSession("TEST_SCENARIO_001");

            LogTestEvents();
            LogTestNPCStateChanges();
            LogTestHostageStates();
            LogTestCognitiveSamples();

            SessionLogger.Instance.EndSession();

            Debug.Log("[AARTestRunner] AAR Test Complete. Check Module4/Resources/ for output JSON.");
        }

        #endregion

        #region Private

        private void LogTestEvents()
        {
            var events = new List<(string type, float time, string src, string tgt, string room, string[] tags)>
            {
                ("DoorOpened",            8f,   "trainee_01",   null,           "room_01", null),
                ("RoomBreached",          15f,  "trainee_01",   null,           "room_02", null),
                ("GunshotHeard",          22f,  "terrorist_01", null,           "room_02", null),
                ("PlayerSeen",            27f,  "terrorist_01", "trainee_01",   "room_02", null),
                ("ShotFired",             35f,  "trainee_01",   null,           "room_02", null),
                ("TerroristHit",          35f,  "trainee_01",   "terrorist_01", "room_02", null),
                ("ShotFired",             42f,  "trainee_01",   null,           "room_02", null),
                ("TerroristHit",          42f,  "trainee_01",   "terrorist_01", "room_02", null),
                ("TerroristDown",         43f,  "trainee_01",   "terrorist_01", "room_02", null),
                ("DoorOpened",            50f,  "trainee_01",   null,           "room_03", null),
                ("RoomBreached",          52f,  "trainee_01",   null,           "room_03", null),
                ("PlayerSeen",            58f,  "terrorist_02", "trainee_01",   "room_03", null),
                ("ShotFired",             65f,  "trainee_01",   null,           "room_03", null),
                ("TerroristDown",         66f,  "trainee_01",   "terrorist_02", "room_03", null),
                ("HostageContactStarted", 75f,  "trainee_01",   "hostage_01",   "room_03", null),
                ("HostageFreed",          85f,  "trainee_01",   "hostage_01",   "room_03", null),
                ("AllyDownSeen",          90f,  "trainee_01",   null,           "room_03",
                    new[] { "friendly_fire" }),  // friendly fire incident
            };

            foreach (var (type, time, src, tgt, room, tags) in events)
            {
                var e = MissionEvent.Create(type, time, src, tgt, room);
                if (tags != null) e.tags.AddRange(tags);
                SessionLogger.Instance.LogEvent(e);
            }
        }

        private void LogTestNPCStateChanges()
        {
            // Terrorist 01: Idle → Suspicious → Alert → Engage
            SessionLogger.Instance.LogNPCStateChange(
                NPCStateChange.Create("terrorist_01", "Terrorist", "Idle",        "Suspicious", "GunshotHeard", 20f));
            SessionLogger.Instance.LogNPCStateChange(
                NPCStateChange.Create("terrorist_01", "Terrorist", "Suspicious",  "Alert",      "PlayerSeen",   27f));
            SessionLogger.Instance.LogNPCStateChange(
                NPCStateChange.Create("terrorist_01", "Terrorist", "Alert",       "Engage",     "PlayerNear",   32f));

            // Terrorist 02: Idle → Alert → Engage (alert cascade with terrorist_01)
            SessionLogger.Instance.LogNPCStateChange(
                NPCStateChange.Create("terrorist_02", "Terrorist", "Idle",        "Alert",      "GunshotHeard", 28f));
            SessionLogger.Instance.LogNPCStateChange(
                NPCStateChange.Create("terrorist_02", "Terrorist", "Alert",       "Engage",     "PlayerSeen",   58f));

            // Terrorist 03 (roamer): also alerts in the cascade window
            SessionLogger.Instance.LogNPCStateChange(
                NPCStateChange.Create("terrorist_03", "Terrorist", "Idle",        "Alert",      "RadioAlert",   30f));

            // Hostage FSM: Calm → Fearful → Follow (logged via LogNPCStateChange for FSM tracing)
            SessionLogger.Instance.LogNPCStateChange(
                NPCStateChange.Create("hostage_01", "Hostage", "Calm",    "Fearful", "GunshotHeard", 22f));
            SessionLogger.Instance.LogNPCStateChange(
                NPCStateChange.Create("hostage_01", "Hostage", "Fearful", "Follow",  "HostageFreed", 85f));
        }

        private void LogTestHostageStates()
        {
            SessionLogger.Instance.LogHostageStateChange("hostage_01", "Calm",    "SessionStart",         0f);
            SessionLogger.Instance.LogHostageStateChange("hostage_01", "Fearful", "GunshotHeard",        22f);
            SessionLogger.Instance.LogHostageStateChange("hostage_01", "Panic",   "NearbyTerroristDown", 43f);
            SessionLogger.Instance.LogHostageStateChange("hostage_01", "Follow",  "HostageFreed",        85f);
        }

        private void LogTestCognitiveSamples()
        {
            // Simulated Module 3 outputs sampled every ~30 seconds
            SessionLogger.Instance.LogCognitiveUpdate(0.45f, 0.70f, 0.82f, 0.75f);
            SessionLogger.Instance.LogCognitiveUpdate(0.38f, 0.78f, 0.79f, 0.80f);
            SessionLogger.Instance.LogCognitiveUpdate(0.60f, 0.55f, 0.62f, 0.58f);
            SessionLogger.Instance.LogCognitiveUpdate(0.33f, 0.85f, 0.88f, 0.84f);
        }

        #endregion
    }
}
