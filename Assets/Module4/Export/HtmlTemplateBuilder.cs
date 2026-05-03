// Module 4 | Sentinels | University of Moratuwa | 2026

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TeamSentinels.Module4.Data;

namespace TeamSentinels.Module4.Export
{
    /// <summary>
    /// Pure static class. Converts a SessionSummary into a fully self-contained HTML report.
    /// No Unity UI, no external URLs, no CDN — everything is inline CSS and inline data.
    /// </summary>
    public static class HtmlTemplateBuilder
    {
        #region Public API

        /// <summary>
        /// Builds and returns the complete HTML string for the given session.
        /// All section builders are called in scroll order; the result is a single
        /// printable page with no JavaScript dependencies.
        /// </summary>
        public static string Build(SessionSummary s)
        {
            if (s == null) return BuildErrorPage("Null SessionSummary passed to HtmlTemplateBuilder.");

            var perf = s.performance     ?? new PerformanceSummary();
            var cog  = s.cognitiveSummary ?? new CognitiveSummary();
            var events    = s.events          ?? new List<MissionEvent>();
            var incidents = s.incidents       ?? new List<IncidentRecord>();
            var hostage   = s.hostageHistory  ?? new List<HostageStateEntry>();
            var npcChgs   = s.npcStateChanges ?? new List<NPCStateChange>();

            var sb = new StringBuilder(32768);

            sb.Append(BuildDocumentOpen(s.sessionId ?? "unknown"));
            sb.Append(BuildStyles());
            sb.Append("</head><body>\n<div class=\"page-wrap\">\n");

            sb.Append(BuildHeader(s, perf));
            sb.Append(BuildScoreCards(perf));
            sb.Append(BuildPerformanceBars(perf));
            sb.Append(BuildCombatStats(perf, events, npcChgs, hostage));
            sb.Append(BuildCognitiveSection(cog));
            sb.Append(BuildScenarioConfig(s, perf, hostage, npcChgs, events));
            sb.Append(BuildEventTimeline(events));
            sb.Append(BuildIncidentTable(incidents));
            sb.Append(BuildHostagePerspective(hostage));
            sb.Append(BuildFooter());

            sb.Append("</div>\n</body>\n</html>");

            return sb.ToString();
        }

        #endregion

        #region HTML Sections

        // ── Document shell ───────────────────────────────────────────────────

        private static string BuildDocumentOpen(string sessionId)
        {
            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
<meta charset=""UTF-8"">
<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
<title>AAR Report — {Esc(sessionId)}</title>
";
        }

        // ── Inline CSS ───────────────────────────────────────────────────────

        private static string BuildStyles()
        {
            return @"<style>
/* ── Reset & base ── */
*, *::before, *::after { box-sizing: border-box; margin: 0; padding: 0; }
body {
  background: #0d1117;
  color: #c9d1d9;
  font-family: system-ui, -apple-system, 'Segoe UI', sans-serif;
  font-size: 14px;
  line-height: 1.6;
}
.page-wrap { max-width: 1100px; margin: 0 auto; padding: 24px 16px 48px; }

/* ── Typography ── */
h1, h2, h3 { font-family: 'Courier New', 'Lucida Console', monospace; letter-spacing: 0.05em; }
h1 { font-size: 2rem;  color: #e6edf3; }
h2 { font-size: 1.2rem; color: #8b949e; text-transform: uppercase;
     border-bottom: 1px solid #21262d; padding-bottom: 6px; margin: 32px 0 14px; }
h3 { font-size: 1rem; color: #c9d1d9; margin-bottom: 8px; }

/* ── Cards & layout ── */
.card { background: #161b22; border: 1px solid #21262d; border-radius: 8px; padding: 20px; }
.row  { display: flex; gap: 16px; flex-wrap: wrap; }
.row > * { flex: 1 1 200px; }

/* ── Header banner ── */
.header-banner {
  background: linear-gradient(135deg, #161b22 0%, #0d1117 100%);
  border: 1px solid #1f6feb;
  border-radius: 10px;
  padding: 28px 32px;
  margin-bottom: 24px;
}
.header-banner h1 { margin-bottom: 12px; }
.header-meta { display: flex; gap: 32px; flex-wrap: wrap; margin-top: 14px; font-size: 13px; color: #8b949e; }
.header-meta span strong { color: #c9d1d9; }

/* ── Mission badge ── */
.badge {
  display: inline-block;
  padding: 6px 18px;
  border-radius: 4px;
  font-family: 'Courier New', monospace;
  font-weight: bold;
  font-size: 0.9rem;
  letter-spacing: 0.1em;
  margin-top: 14px;
}
.badge-success { background: #1a3a24; color: #3fb950; border: 1px solid #3fb950; }
.badge-danger  { background: #3a1a1a; color: #f85149; border: 1px solid #f85149; }

/* ── Score cards ── */
.score-card {
  background: #161b22;
  border: 1px solid #21262d;
  border-radius: 8px;
  padding: 20px 16px;
  text-align: center;
}
.score-card .label { font-size: 11px; text-transform: uppercase; letter-spacing: 0.1em; color: #8b949e; margin-bottom: 8px; }
.score-card .value { font-family: 'Courier New', monospace; font-size: 2.4rem; font-weight: bold; }
.score-card.overall .value { color: #1f6feb; }
.score-card.safety  .value { color: #3fb950; }
.score-card.accuracy .value { color: #d29922; }
.score-card.speed   .value { color: #a371f7; }

/* ── CSS bar chart ── */
.bar-row { margin-bottom: 12px; }
.bar-label { display: flex; justify-content: space-between; margin-bottom: 4px; font-size: 12px; color: #8b949e; }
.bar-track { background: #21262d; border-radius: 4px; height: 18px; overflow: hidden; }
.bar-fill  { height: 100%; border-radius: 4px; transition: width 0.3s; }
.bar-safety   { background: #3fb950; }
.bar-accuracy { background: #d29922; }
.bar-speed    { background: #a371f7; }
.bar-overall  { background: #1f6feb; }

/* ── Tables ── */
table { width: 100%; border-collapse: collapse; font-size: 13px; }
th { background: #21262d; color: #8b949e; text-transform: uppercase;
     font-size: 11px; letter-spacing: 0.06em; padding: 8px 12px; text-align: left; }
td { padding: 8px 12px; border-bottom: 1px solid #21262d; vertical-align: top; }
tr:last-child td { border-bottom: none; }
tr:hover td { background: #1c2128; }

/* ── Event row tints ── */
.ev-combat  { background: rgba(248, 81, 73,  0.08); }
.ev-door    { background: rgba(210, 153, 34, 0.08); }
.ev-hostage { background: rgba(63,  185, 80, 0.08); }
.ev-npc     { background: rgba(31,  111, 235,0.08); }
.ev-other   { background: transparent; }

/* ── Severity badges ── */
.sev { display: inline-block; padding: 2px 10px; border-radius: 3px;
       font-size: 11px; font-weight: bold; letter-spacing: 0.06em; text-transform: uppercase; }
.sev-low    { background: #1a2a3a; color: #1f6feb; border: 1px solid #1f6feb; }
.sev-medium { background: #2e2208; color: #d29922; border: 1px solid #d29922; }
.sev-high   { background: #3a1a1a; color: #f85149; border: 1px solid #f85149; }

/* ── Hostage states ── */
.state-pill {
  display: inline-block;
  padding: 2px 10px;
  border-radius: 12px;
  font-size: 12px;
  font-weight: bold;
  margin: 2px 3px;
}
.state-calm    { background: #1a3a24; color: #3fb950; }
.state-fearful { background: #2e2208; color: #d29922; }
.state-panic   { background: #3a1a1a; color: #f85149; }
.state-freeze  { background: #1a2234; color: #8b949e; }
.state-follow  { background: #1a2a3a; color: #58a6ff; }
.state-unknown { background: #21262d; color: #8b949e; }

.distress-track { background: #21262d; border-radius: 4px; height: 8px; margin: 6px 0 10px; overflow: hidden; }
.distress-fill  { height: 100%; border-radius: 4px; background: linear-gradient(90deg, #3fb950, #d29922, #f85149); }

.trigger-list { font-size: 12px; color: #8b949e; padding: 4px 0; }
.trigger-list li { list-style: none; padding: 2px 0; }
.trigger-list li::before { content: '▸ '; color: #1f6feb; }

/* ── Two-column stat table ── */
.stat-grid { display: grid; grid-template-columns: 1fr 1fr; gap: 0; }
.stat-grid td:first-child { color: #8b949e; width: 55%; }
.stat-grid td:last-child  { color: #e6edf3; font-family: 'Courier New', monospace; font-weight: bold; }

/* ── Cognitive table ── */
.cog-table td:first-child { color: #8b949e; }
.cog-table td:last-child  { color: #e6edf3; font-family: 'Courier New', monospace; }

/* ── Footer ── */
.footer {
  margin-top: 48px;
  padding-top: 20px;
  border-top: 1px solid #21262d;
  text-align: center;
  font-size: 12px;
  color: #484f58;
  font-family: 'Courier New', monospace;
}

@media print {
  body { background: #fff; color: #000; }
  .header-banner, .card, .score-card { border: 1px solid #ccc; background: #f9f9f9; }
}
</style>
";
        }

        // ── Section 1: Header ────────────────────────────────────────────────

        private static string BuildHeader(SessionSummary s, PerformanceSummary perf)
        {
            string dateStr     = s.timestamp.ToString("yyyy-MM-dd HH:mm:ss") + " UTC";
            string durationStr = FormatTime(perf.missionDuration);
            string badgeClass  = perf.missionSuccess ? "badge-success" : "badge-danger";
            string badgeText   = perf.missionSuccess ? "MISSION SUCCESS" : "MISSION FAILED";

            return $@"
<div class=""header-banner"">
  <h1>&#9670; AFTER ACTION REVIEW</h1>
  <div class=""header-meta"">
    <span><strong>Session ID:</strong> {Esc(s.sessionId ?? "N/A")}</span>
    <span><strong>Scenario ID:</strong> {Esc(s.scenarioId ?? "N/A")}</span>
    <span><strong>Date:</strong> {Esc(dateStr)}</span>
    <span><strong>Duration:</strong> {Esc(durationStr)}</span>
  </div>
  <div><span class=""badge {badgeClass}"">{badgeText}</span></div>
</div>
";
        }

        // ── Section 2: Score cards ───────────────────────────────────────────

        private static string BuildScoreCards(PerformanceSummary perf)
        {
            return $@"
<h2>Performance Overview</h2>
<div class=""row"" style=""margin-bottom:24px"">
  {ScoreCard("overall",  "Overall Score",  Pct(perf.overallScore))}
  {ScoreCard("safety",   "Safety Score",   Pct(perf.safetyScore))}
  {ScoreCard("accuracy", "Accuracy Score", Pct(perf.accuracyScore))}
  {ScoreCard("speed",    "Speed Score",    Pct(perf.speedScore))}
</div>
";
        }

        private static string ScoreCard(string cls, string label, string value)
        {
            return $@"<div class=""score-card {cls}"">
  <div class=""label"">{label}</div>
  <div class=""value"">{value}</div>
</div>";
        }

        // ── Section 3: Performance bars ──────────────────────────────────────

        private static string BuildPerformanceBars(PerformanceSummary perf)
        {
            var sb = new StringBuilder();
            sb.Append("<h2>Performance Breakdown</h2>\n<div class=\"card\">\n");
            sb.Append(BarRow("Safety",   perf.safetyScore,   "safety"));
            sb.Append(BarRow("Accuracy", perf.accuracyScore, "accuracy"));
            sb.Append(BarRow("Speed",    perf.speedScore,    "speed"));
            sb.Append(BarRow("Overall",  perf.overallScore,  "overall"));
            sb.Append("</div>\n");
            return sb.ToString();
        }

        private static string BarRow(string label, float value, string barClass)
        {
            int pct = (int)Math.Round(Math.Clamp(value, 0f, 1f) * 100);
            return $@"<div class=""bar-row"">
  <div class=""bar-label""><span>{label}</span><span>{pct}%</span></div>
  <div class=""bar-track""><div class=""bar-fill bar-{barClass}"" style=""width:{pct}%""></div></div>
</div>
";
        }

        // ── Section 4: Combat statistics ─────────────────────────────────────

        private static string BuildCombatStats(PerformanceSummary perf,
            List<MissionEvent> events, List<NPCStateChange> npcChgs,
            List<HostageStateEntry> hostage)
        {
            int rooms      = events.Where(e => e.roomId != null).Select(e => e.roomId).Distinct().Count();
            int terrorists = npcChgs.Where(c => c.actorType == "Terrorist")
                                    .Select(c => c.actorId).Distinct().Count();
            int terDown    = events.Count(e => e.eventType == "TerroristDown");

            float accuracy = perf.totalShots > 0
                ? (float)perf.hits / perf.totalShots * 100f : 0f;

            return $@"
<h2>Combat Statistics</h2>
<div class=""card"">
  <table class=""stat-grid"">
    <tr><td>Shots Fired</td><td>{perf.totalShots}</td></tr>
    <tr><td>Hits</td><td>{perf.hits}</td></tr>
    <tr><td>Misses</td><td>{perf.misses}</td></tr>
    <tr><td>Accuracy</td><td>{accuracy.ToString("F1")}%</td></tr>
    <tr><td>Friendly Fire Incidents</td><td>{perf.friendlyFireCount}</td></tr>
    <tr><td>Terrorists Neutralized</td><td>{terDown} / {Math.Max(terrorists, terDown)}</td></tr>
    <tr><td>Hostages Saved</td><td>{perf.hostagesSaved} / {perf.hostagesTotal}</td></tr>
    <tr><td>Mission Duration</td><td>{FormatTime(perf.missionDuration)}</td></tr>
    <tr><td>Rooms Entered</td><td>{rooms}</td></tr>
  </table>
</div>
";
        }

        // ── Section 5: Cognitive analysis ────────────────────────────────────

        private static string BuildCognitiveSection(CognitiveSummary cog)
        {
            return $@"
<h2>Cognitive Analysis</h2>
<div class=""card"">
  <table class=""cog-table"">
    <tr>
      <th>Metric</th>
      <th>Value</th>
    </tr>
    <tr><td>Average Reaction Time</td><td>{cog.averageReactionTime.ToString("F2")} s</td></tr>
    <tr><td>Peak Reaction Time</td><td>{cog.peakReactionTime.ToString("F2")} s</td></tr>
    <tr><td>Movement Initiation Score</td><td>{Pct(cog.movementInitiationScore)}</td></tr>
    <tr><td>Stability Score</td><td>{Pct(cog.stabilityScore)}</td></tr>
    <tr><td>Attention Score</td><td>{Pct(cog.attentionScore)}</td></tr>
    <tr><td>Estimated Cognitive Load</td><td>{CogLabel(cog.estimatedCognitiveLoad)}</td></tr>
    <tr><td>Estimated Stress Level</td><td>{CogLabel(cog.estimatedStressLevel)}</td></tr>
  </table>
</div>
";
        }

        private static string CogLabel(string level)
        {
            if (string.IsNullOrEmpty(level)) return "<span class=\"sev sev-low\">N/A</span>";
            switch (level.ToLower())
            {
                case "low":    return $"<span class=\"sev sev-low\">{level.ToUpper()}</span>";
                case "medium": return $"<span class=\"sev sev-medium\">{level.ToUpper()}</span>";
                case "high":   return $"<span class=\"sev sev-high\">{level.ToUpper()}</span>";
                default:       return Esc(level);
            }
        }

        // ── Section 6: Scenario configuration ────────────────────────────────

        private static string BuildScenarioConfig(SessionSummary s, PerformanceSummary perf,
            List<HostageStateEntry> hostage, List<NPCStateChange> npcChgs,
            List<MissionEvent> events)
        {
            int uniqueRooms     = events.Where(e => e.roomId != null).Select(e => e.roomId).Distinct().Count();
            int uniqueTerrorists = npcChgs.Where(c => c.actorType == "Terrorist")
                                          .Select(c => c.actorId).Distinct().Count();
            int uniqueHostages  = hostage.Select(h => h.hostageId).Distinct().Count();

            return $@"
<h2>Scenario Configuration</h2>
<div class=""card"">
  <table class=""stat-grid"">
    <tr><td>Scenario ID</td><td>{Esc(s.scenarioId ?? "N/A")}</td></tr>
    <tr><td>Session ID</td><td>{Esc(s.sessionId  ?? "N/A")}</td></tr>
    <tr><td>Floors</td><td>1 (single-floor constraint)</td></tr>
    <tr><td>Rooms Visited</td><td>{uniqueRooms}</td></tr>
    <tr><td>Hostages</td><td>{Math.Max(uniqueHostages, perf.hostagesTotal)}</td></tr>
    <tr><td>Terrorists Tracked</td><td>{uniqueTerrorists}</td></tr>
    <tr><td>Session Timestamp</td><td>{s.timestamp:yyyy-MM-dd HH:mm:ss} UTC</td></tr>
    <tr><td>Report Generated</td><td>{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</td></tr>
  </table>
</div>
";
        }

        // ── Section 7: Event timeline ─────────────────────────────────────────

        private static string BuildEventTimeline(List<MissionEvent> events)
        {
            if (events == null || events.Count == 0)
                return EmptySection("Event Timeline", "No events recorded.");

            var sb = new StringBuilder();
            sb.Append("<h2>Event Timeline</h2>\n<div class=\"card\" style=\"padding:0;overflow:hidden\">\n");
            sb.Append("<table>\n<thead><tr>");
            sb.Append("<th>Time</th><th>Event Type</th><th>Room</th><th>Source</th><th>Target</th><th>Tags</th>");
            sb.Append("</tr></thead>\n<tbody>\n");

            foreach (var e in events.OrderBy(ev => ev.timestamp))
            {
                string rowClass = EventRowClass(e.eventType);
                string tags     = e.tags != null && e.tags.Count > 0
                    ? Esc(string.Join(", ", e.tags)) : "—";

                sb.Append($@"<tr class=""{rowClass}"">
  <td style=""font-family:'Courier New',monospace;white-space:nowrap"">{FormatTime(e.timestamp)}</td>
  <td><strong>{Esc(e.eventType ?? "")}</strong></td>
  <td>{Esc(e.roomId        ?? "—")}</td>
  <td>{Esc(e.sourceActorId ?? "—")}</td>
  <td>{Esc(e.targetActorId ?? "—")}</td>
  <td style=""font-size:12px;color:#8b949e"">{tags}</td>
</tr>
");
            }

            sb.Append("</tbody>\n</table>\n</div>\n");
            return sb.ToString();
        }

        private static string EventRowClass(string eventType)
        {
            if (string.IsNullOrEmpty(eventType)) return "ev-other";
            switch (eventType)
            {
                case "ShotFired":
                case "TerroristHit":
                case "TerroristDown":
                case "GunshotHeard":
                case "AllyDownSeen":
                    return "ev-combat";
                case "DoorOpened":
                case "RoomBreached":
                    return "ev-door";
                case "HostageContactStarted":
                case "HostageFreed":
                    return "ev-hostage";
                case "PlayerSeen":
                    return "ev-npc";
                default:
                    return "ev-other";
            }
        }

        // ── Section 8: Key incidents ──────────────────────────────────────────

        private static string BuildIncidentTable(List<IncidentRecord> incidents)
        {
            if (incidents == null || incidents.Count == 0)
                return EmptySection("Key Incidents", "No incidents detected.");

            var sb = new StringBuilder();
            sb.Append("<h2>Key Incidents</h2>\n<div class=\"card\" style=\"padding:0;overflow:hidden\">\n");
            sb.Append("<table>\n<thead><tr>");
            sb.Append("<th>Severity</th><th>Type</th><th>Description</th><th>Time</th><th>Actors</th>");
            sb.Append("</tr></thead>\n<tbody>\n");

            foreach (var inc in incidents.OrderBy(i => i.timestamp))
            {
                string sevBadge = SeverityBadge(inc.severity);
                string actors   = inc.involvedActors != null && inc.involvedActors.Count > 0
                    ? Esc(string.Join(", ", inc.involvedActors)) : "—";

                sb.Append($@"<tr>
  <td>{sevBadge}</td>
  <td style=""white-space:nowrap""><strong>{Esc(inc.incidentType ?? "")}</strong></td>
  <td>{Esc(inc.description ?? "")}</td>
  <td style=""font-family:'Courier New',monospace;white-space:nowrap"">{FormatTime(inc.timestamp)}</td>
  <td style=""font-size:12px;color:#8b949e"">{actors}</td>
</tr>
");
            }

            sb.Append("</tbody>\n</table>\n</div>\n");
            return sb.ToString();
        }

        private static string SeverityBadge(string severity)
        {
            string level = (severity ?? "low").ToLower();
            string cls   = level == "high" ? "sev-high" : level == "medium" ? "sev-medium" : "sev-low";
            return $"<span class=\"sev {cls}\">{level.ToUpper()}</span>";
        }

        // ── Section 9: Hostage perspective ───────────────────────────────────

        private static string BuildHostagePerspective(List<HostageStateEntry> history)
        {
            if (history == null || history.Count == 0)
                return EmptySection("Hostage Perspective", "No hostage data recorded.");

            var sb = new StringBuilder();
            sb.Append("<h2>Hostage Perspective</h2>\n");

            var byHostage = history
                .GroupBy(h => h.hostageId ?? "unknown")
                .OrderBy(g => g.Key);

            foreach (var group in byHostage)
            {
                var entries = group.OrderBy(e => e.timestamp).ToList();
                float finalDistress = entries.Last().distressScore;
                int   distressPct   = (int)Math.Round(Math.Clamp(finalDistress, 0f, 1f) * 100);

                sb.Append($"<div class=\"card\" style=\"margin-bottom:16px\">\n");
                sb.Append($"<h3>&#9679; {Esc(group.Key)}</h3>\n");

                // State pills
                sb.Append("<p style=\"margin:10px 0 6px\"><strong style=\"color:#8b949e;font-size:12px\">STATE JOURNEY:</strong><br>");
                foreach (var entry in entries)
                {
                    string pillClass = StateClass(entry.state);
                    sb.Append($"<span class=\"state-pill {pillClass}\">{Esc(entry.state ?? "?")}</span>");
                    sb.Append($"<span style=\"font-size:11px;color:#484f58\"> {FormatTime(entry.timestamp)} </span>");
                    if (entries.IndexOf(entry) < entries.Count - 1)
                        sb.Append("<span style=\"color:#484f58\">&#8594;</span> ");
                }
                sb.Append("</p>\n");

                // Distress bar
                sb.Append($"<p style=\"font-size:12px;color:#8b949e;margin-top:10px\">Final Distress Score: <strong style=\"color:#e6edf3\">{finalDistress.ToString("F2")} ({distressPct}%)</strong></p>\n");
                sb.Append($"<div class=\"distress-track\"><div class=\"distress-fill\" style=\"width:{distressPct}%\"></div></div>\n");

                // Trigger events
                var triggers = entries.Where(e => !string.IsNullOrEmpty(e.triggerEvent)).ToList();
                if (triggers.Count > 0)
                {
                    sb.Append("<ul class=\"trigger-list\">\n");
                    foreach (var t in triggers)
                        sb.Append($"  <li><span style=\"font-family:'Courier New',monospace;color:#8b949e\">{FormatTime(t.timestamp)}</span> — {Esc(t.state)} &larr; {Esc(t.triggerEvent)}</li>\n");
                    sb.Append("</ul>\n");
                }

                sb.Append("</div>\n");
            }

            return sb.ToString();
        }

        private static string StateClass(string state)
        {
            switch (state)
            {
                case "Calm":    return "state-calm";
                case "Fearful": return "state-fearful";
                case "Panic":   return "state-panic";
                case "Freeze":  return "state-freeze";
                case "Follow":  return "state-follow";
                default:        return "state-unknown";
            }
        }

        // ── Section 10: Footer ───────────────────────────────────────────────

        private static string BuildFooter()
        {
            return $@"
<div class=""footer"">
  <p>Generated by Sentinels Module 4 &nbsp;|&nbsp; University of Moratuwa &nbsp;|&nbsp; 2026</p>
  <p style=""margin-top:4px"">{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
</div>
";
        }

        #endregion

        #region Utilities

        /// <summary>Escapes HTML special characters to prevent injection in data fields.</summary>
        private static string Esc(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            return raw
                .Replace("&",  "&amp;")
                .Replace("<",  "&lt;")
                .Replace(">",  "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'",  "&#39;");
        }

        /// <summary>Formats a float seconds value as MM:SS.</summary>
        private static string FormatTime(float seconds)
        {
            int m = (int)(seconds / 60f);
            int s = (int)(seconds % 60f);
            return m + ":" + s.ToString("D2");
        }

        /// <summary>Formats a 0–1 float as a percentage string, e.g. "87%".</summary>
        private static string Pct(float value)
        {
            return ((int)Math.Round(Math.Clamp(value, 0f, 1f) * 100f)) + "%";
        }

        private static string EmptySection(string title, string message)
        {
            return $"<h2>{title}</h2>\n<div class=\"card\" style=\"color:#484f58;text-align:center;padding:32px\">{message}</div>\n";
        }

        private static string BuildErrorPage(string message)
        {
            return $"<!DOCTYPE html><html><head><title>AAR Error</title></head>" +
                   $"<body style=\"background:#0d1117;color:#f85149;font-family:monospace;padding:40px\">" +
                   $"<h1>Report Error</h1><p>{Esc(message)}</p></body></html>";
        }

        #endregion
    }
}
