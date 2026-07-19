// Module 4 | Sentinels | University of Moratuwa | 2026

using System.Collections.Generic;
using System.Linq;
using TeamSentinels.Module4.Data;

namespace TeamSentinels.Module4.Analysis
{
    /// <summary>
    /// Pure static class. Scans raw session logs and auto-detects notable incidents
    /// for display in the AAR dashboard incident list.
    /// No MonoBehaviour; safe to call from any context.
    /// </summary>
    public static class IncidentExtractor
    {
        #region Public API

        /// <summary>
        /// Analyses session logs and returns a chronologically sorted list of IncidentRecords.
        /// </summary>
        /// <param name="events">All MissionEvents recorded during the session.</param>
        /// <param name="npcChanges">All NPC FSM state transitions.</param>
        /// <param name="hostageHistory">All hostage emotional-state entries.</param>
        public static List<IncidentRecord> Extract(
            List<MissionEvent>      events,
            List<NPCStateChange>    npcChanges,
            List<HostageStateEntry> hostageHistory)
        {
            events         ??= new List<MissionEvent>();
            npcChanges     ??= new List<NPCStateChange>();
            hostageHistory ??= new List<HostageStateEntry>();

            var incidents = new List<IncidentRecord>();

            ExtractFirstContact(events, incidents);
            ExtractFirstShot(events, incidents);
            ExtractHostageEndangered(hostageHistory, incidents);
            ExtractTerroristNeutralized(events, incidents);
            ExtractAlertCascade(npcChanges, incidents);
            ExtractHostageRescued(hostageHistory, incidents);
            ExtractMissionEnd(events, incidents);

            incidents.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));
            return incidents;
        }

        #endregion

        #region Private — Extractors

        private static void ExtractFirstContact(List<MissionEvent> events,
            List<IncidentRecord> incidents)
        {
            var e = events.FirstOrDefault(ev => ev.eventType == "PlayerSeen");
            if (e == null) return;

            incidents.Add(IncidentRecord.Create(
                "FirstContact",
                e.timestamp,
                $"Enemy first detected trainee in {Where(e.roomId)}.",
                Actors(e.sourceActorId, e.targetActorId),
                "low"));
        }

        private static void ExtractFirstShot(List<MissionEvent> events,
            List<IncidentRecord> incidents)
        {
            var e = events.FirstOrDefault(ev => ev.eventType == "ShotFired");
            if (e == null) return;

            incidents.Add(IncidentRecord.Create(
                "FirstShot",
                e.timestamp,
                $"First shot fired by {e.sourceActorId ?? "unknown"} in {Where(e.roomId)}.",
                Actors(e.sourceActorId),
                "low"));
        }

        private static void ExtractHostageEndangered(List<HostageStateEntry> history,
            List<IncidentRecord> incidents)
        {
            foreach (var entry in history.Where(h => h.state == "Panic" || h.state == "Freeze"))
            {
                incidents.Add(IncidentRecord.Create(
                    "HostageEndangered",
                    entry.timestamp,
                    $"{entry.hostageId} entered {entry.state} state " +
                    $"(trigger: {entry.triggerEvent ?? "unknown"}). Distress: {entry.distressScore:P0}.",
                    Actors(entry.hostageId),
                    "high"));
            }
        }

        private static void ExtractTerroristNeutralized(List<MissionEvent> events,
            List<IncidentRecord> incidents)
        {
            foreach (var e in events.Where(ev => ev.eventType == "TerroristDown"))
            {
                incidents.Add(IncidentRecord.Create(
                    "TerroristNeutralized",
                    e.timestamp,
                    $"{e.targetActorId ?? "Terrorist"} neutralized in {Where(e.roomId)}.",
                    Actors(e.sourceActorId, e.targetActorId),
                    "low"));
            }
        }

        private static void ExtractAlertCascade(List<NPCStateChange> npcChanges,
            List<IncidentRecord> incidents)
        {
            // Collect all transitions to "Alert" state
            var alertTransitions = npcChanges
                .Where(c => c.newState == "Alert")
                .OrderBy(c => c.timestamp)
                .ToList();

            // Sliding window: find any group of 3+ alerts within a 5s window
            for (int i = 0; i < alertTransitions.Count; i++)
            {
                float windowStart = alertTransitions[i].timestamp;
                float windowEnd   = windowStart + 5f;

                var group = alertTransitions
                    .Where(c => c.timestamp >= windowStart && c.timestamp <= windowEnd)
                    .ToList();

                if (group.Count >= 3)
                {
                    var actorIds = group.Select(c => c.actorId).Distinct().ToList();
                    incidents.Add(IncidentRecord.Create(
                        "AlertCascade",
                        windowStart,
                        $"{group.Count} NPCs entered Alert state within 5 seconds " +
                        $"(t={windowStart:F1}s–{windowEnd:F1}s).",
                        actorIds,
                        "medium"));

                    // Skip past this window to avoid overlapping cascades
                    while (i < alertTransitions.Count - 1 &&
                           alertTransitions[i + 1].timestamp <= windowEnd)
                        i++;
                }
            }
        }

        private static void ExtractHostageRescued(List<HostageStateEntry> history,
            List<IncidentRecord> incidents)
        {
            foreach (var entry in history.Where(h => h.state == "Follow"))
            {
                incidents.Add(IncidentRecord.Create(
                    "HostageRescued",
                    entry.timestamp,
                    $"{entry.hostageId} is following the trainee — rescue in progress.",
                    Actors(entry.hostageId),
                    "low"));
            }
        }

        private static void ExtractMissionEnd(List<MissionEvent> events,
            List<IncidentRecord> incidents)
        {
            if (events.Count == 0) return;

            var last = events[events.Count - 1];
            incidents.Add(IncidentRecord.Create(
                "MissionEnd",
                last.timestamp,
                $"Mission concluded at t={last.timestamp:F1}s. Final event: {last.eventType}.",
                Actors(last.sourceActorId),
                "low"));
        }

        #endregion

        #region Private — Helpers

        private static List<string> Actors(params string[] ids)
        {
            return ids.Where(id => !string.IsNullOrEmpty(id)).ToList();
        }

        // Readable location phrase. Module4Bridge fills roomId (e.g. "the corridor") from the
        // event position; if it's still missing the event happened outside any known room.
        private static string Where(string roomId)
        {
            return string.IsNullOrEmpty(roomId) ? "an unknown area" : roomId;
        }

        #endregion
    }
}
