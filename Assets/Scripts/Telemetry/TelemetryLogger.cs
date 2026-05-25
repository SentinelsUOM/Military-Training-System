using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// Serialisable telemetry records
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Emitted every time an NPC's FSM state changes.</summary>
[Serializable]
public class NPCStateChangeRecord
{
    public string actorId;
    public string actorType;      // "Terrorist" or "Hostage"
    public string previousState;
    public string newState;
    public int    triggerEventId; // EventId of the ScenarioEvent that caused this change
    public string triggerEventType;
    public float  timestamp;
}

/// <summary>Emitted every time EventManager routes an event to a single best responder.</summary>
[Serializable]
public class ResponderDecisionRecord
{
    public int    eventId;
    public string eventType;
    public string[] candidateIds;
    public string   selectedResponder;  // null if no valid candidate
    public string   scoreSummary;       // compact human-readable score breakdown
    public float    timestamp;
}

/// <summary>Periodic snapshot of an NPC's live state (emitted by PerceptionController).</summary>
[Serializable]
public class BehaviorSnapshotRecord
{
    public float  timestamp;
    public string actorId;
    public string state;
    public float  posX, posY, posZ;
    public string notes;
}

/// <summary>Emitted when a Leader-role terrorist issues a coordination directive to its squad.</summary>
[Serializable]
public class LeaderDirectiveRecord
{
    public float  timestamp;
    public string squadId;
    public string leaderId;
    public string directiveType;   // "Converge" / "Hold"
    public float  targetX, targetY, targetZ;
    public string reason;          // event type that triggered the directive
}

// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Central telemetry sink for Module 2.
///
/// Every NPC state change and every responder selection must call the
/// corresponding Log method here.  On session end (OnDestroy) the accumulated
/// records are serialised to JSON inside Application.persistentDataPath.
///
/// Module 4 (AAR / Replay) reads these files.
///
/// Setup: place one instance on the ScenarioManager GameObject.
/// </summary>
public class TelemetryLogger : MonoBehaviour
{
    public static TelemetryLogger Instance { get; private set; }

    // ── Module 4 integration hook (additive, does not affect storage) ────────
    /// <summary>Fires after every LogStateChange(). Subscribed by Module 4 (AAR / dashboard).</summary>
    public static event Action<NPCStateChangeRecord> OnStateChangeLogged;

    [Header("Output")]
    [Tooltip("Sub-folder inside Application.persistentDataPath where session files are written.")]
    public string outputFolder = "Telemetry";

    [Tooltip("Print every log call to the Unity Console (useful during development).")]
    public bool echoToConsole = true;

    // ── Storage ───────────────────────────────────────────────────────────────

    readonly List<NPCStateChangeRecord>   _stateChanges = new List<NPCStateChangeRecord>();
    readonly List<ResponderDecisionRecord> _decisions   = new List<ResponderDecisionRecord>();
    readonly List<BehaviorSnapshotRecord>  _snapshots   = new List<BehaviorSnapshotRecord>();
    readonly List<LeaderDirectiveRecord>   _directives  = new List<LeaderDirectiveRecord>();

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) WriteSessionFiles();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Call this inside every NPC FSM TransitionTo() — before returning.
    /// </summary>
    public void LogStateChange(
        string        actorId,
        string        actorType,
        string        previousState,
        string        newState,
        ScenarioEvent trigger)
    {
        var record = new NPCStateChangeRecord
        {
            actorId         = actorId,
            actorType       = actorType,
            previousState   = previousState,
            newState        = newState,
            triggerEventId  = trigger?.EventId ?? -1,
            triggerEventType = trigger?.Type.ToString() ?? "—",
            timestamp       = Time.time,
        };
        _stateChanges.Add(record);

        if (echoToConsole)
            Debug.Log($"[Telemetry|StateChange] {actorId} ({actorType}): {previousState} → {newState} " +
                      $"(event E{record.triggerEventId}:{record.triggerEventType})");

        // Module 4 integration hook (additive — does not affect storage)
        OnStateChangeLogged?.Invoke(record);
    }

    /// <summary>
    /// Call this in EventManager after SelectBest() resolves a targeted event.
    /// </summary>
    public void LogDecision(
        int      eventId,
        string   eventType,
        List<string> candidateIds,
        string   selectedResponder,
        string   scoreSummary)
    {
        var record = new ResponderDecisionRecord
        {
            eventId           = eventId,
            eventType         = eventType,
            candidateIds      = candidateIds?.ToArray() ?? Array.Empty<string>(),
            selectedResponder = selectedResponder,
            scoreSummary      = scoreSummary,
            timestamp         = Time.time,
        };
        _decisions.Add(record);

        if (echoToConsole)
            Debug.Log($"[Telemetry|Decision] E{eventId} {eventType} → {selectedResponder ?? "none"} | {scoreSummary}");
    }

    /// <summary>
    /// Call from PerceptionController or periodic snapshot hooks.
    /// </summary>
    public void LogSnapshot(string actorId, string state, Vector3 position, string notes = "")
    {
        _snapshots.Add(new BehaviorSnapshotRecord
        {
            timestamp = Time.time,
            actorId   = actorId,
            state     = state,
            posX      = position.x,
            posY      = position.y,
            posZ      = position.z,
            notes     = notes,
        });
    }

    /// <summary>
    /// Called by Squad.IssueDirective when a Leader broadcasts a coordination command.
    /// Captured separately from state changes so Module 4 AAR can render directives
    /// as their own timeline events ("Leader α → Converge on Room 3").
    /// </summary>
    public void LogDirective(string squadId, LeaderDirective directive)
    {
        if (directive == null) return;

        var record = new LeaderDirectiveRecord
        {
            timestamp     = directive.Timestamp,
            squadId       = squadId,
            leaderId      = directive.Source != null ? directive.Source.NPCId : "?",
            directiveType = directive.Type.ToString(),
            targetX       = directive.TargetPosition.x,
            targetY       = directive.TargetPosition.y,
            targetZ       = directive.TargetPosition.z,
            reason        = directive.Reason,
        };
        _directives.Add(record);

        if (echoToConsole)
            Debug.Log($"[Telemetry|Directive] {squadId} ({record.leaderId}): " +
                      $"{record.directiveType} @ ({record.targetX:F1},{record.targetY:F1},{record.targetZ:F1}) " +
                      $"reason={record.reason}");
    }

    // ── File I/O ──────────────────────────────────────────────────────────────

    [Serializable] class StateChangeList { public List<NPCStateChangeRecord>   records; }
    [Serializable] class DecisionList    { public List<ResponderDecisionRecord> records; }
    [Serializable] class SnapshotList    { public List<BehaviorSnapshotRecord>  records; }
    [Serializable] class DirectiveList   { public List<LeaderDirectiveRecord>   records; }

    void WriteSessionFiles()
    {
        try
        {
            string dir = Path.Combine(Application.persistentDataPath, outputFolder);
            Directory.CreateDirectory(dir);

            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            File.WriteAllText(
                Path.Combine(dir, $"StateChanges_{stamp}.json"),
                JsonUtility.ToJson(new StateChangeList { records = _stateChanges }, prettyPrint: true));

            File.WriteAllText(
                Path.Combine(dir, $"Decisions_{stamp}.json"),
                JsonUtility.ToJson(new DecisionList { records = _decisions }, prettyPrint: true));

            File.WriteAllText(
                Path.Combine(dir, $"Snapshots_{stamp}.json"),
                JsonUtility.ToJson(new SnapshotList { records = _snapshots }, prettyPrint: true));

            File.WriteAllText(
                Path.Combine(dir, $"Directives_{stamp}.json"),
                JsonUtility.ToJson(new DirectiveList { records = _directives }, prettyPrint: true));

            Debug.Log($"[TelemetryLogger] Session files written to: {dir}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[TelemetryLogger] Failed to write session files: {ex.Message}");
        }
    }
}
