// Module 4 Integration | Sentinels | University of Moratuwa | 2026
//
// Lives in Assembly-CSharp (no asmdef in this folder) so it can reference
// both Module 2 (also Assembly-CSharp) and Module 4 (autoReferenced asmdef).

using System;
using System.Collections.Generic;
using TeamSentinels.Module4.Data;
using TeamSentinels.Module4.Logging;
using UnityEngine;

/// <summary>
/// Subscribes to Module 2's gameplay event hooks and translates each
/// signal into the SessionLogger calls that the dashboard expects.
///
/// Module 2 → Module 4 mapping
///   EventManager.OnEventRaised        → SessionLogger.LogEvent
///   TelemetryLogger.OnStateChangeLogged
///       actorType "Hostage"           → SessionLogger.LogHostageStateChange
///       any other actorType           → SessionLogger.LogNPCStateChange
///
/// Lifecycle
///   - Subscribes on OnEnable, unsubscribes on OnDisable.
///   - Does NOT call StartSession or EndSession itself —
///     Module4SessionController owns that.
///
/// Setup
///   - Place this component on the same GameObject as SessionLogger
///     (typically the "Module4Manager" GameObject).
/// </summary>
public class Module4Bridge : MonoBehaviour
{
    [Tooltip("Print every translated event to the Console. Useful for debugging.")]
    [SerializeField] private bool echoToConsole = false;

    [Tooltip("Lazily call SessionLogger.StartSession on the first event if no session is active. " +
             "Leave on for ad-hoc Play mode tests; turn off for real missions where " +
             "Module4SessionController explicitly opens the session.")]
    [SerializeField] private bool autoStartOnFirstEvent = true;

    [Tooltip("Scenario id used when auto-starting a session.")]
    [SerializeField] private string autoStartScenarioId = "RUNTIME_SESSION";

    private void OnEnable()
    {
        EventManager.OnEventRaised             += HandleScenarioEvent;
        TelemetryLogger.OnStateChangeLogged    += HandleStateChange;
    }

    private void OnDisable()
    {
        EventManager.OnEventRaised             -= HandleScenarioEvent;
        TelemetryLogger.OnStateChangeLogged    -= HandleStateChange;
    }

    private void HandleScenarioEvent(ScenarioEvent e)
    {
        if (e == null) return;
        if (!EnsureSessionActive()) return;

        // ScenarioEventType enum name (e.g. "DoorOpened") matches the
        // string literals SimulateTestSession used, so a direct ToString()
        // is the correct mapping.
        string eventType  = e.Type.ToString();
        string sourceId   = e.Instigator != null ? e.Instigator.name : null;
        string targetId   = e.TargetActorId;
        string roomId     = e.RoomId;

        // Most gameplay events are raised without a room, which left every incident saying
        // "in room unknown". Derive it from where the event happened so the AAR can say where.
        if (string.IsNullOrEmpty(roomId))
            roomId = RoomLocator.RoomLabelAt(e.Origin);

        // Convert absolute Time.time → elapsed-since-session-start so the
        // dashboard's timeline can plot events between 0 and missionDuration.
        // MissionEnded uses the same convention, so timestamps are consistent.
        float elapsed = Mathf.Max(0f, e.Timestamp - SessionLogger.Instance.SessionStartTime);

        // Tag hostage hits as friendly fire. PerformanceCalculator's safety penalty counts
        // events tagged "friendly_fire" — and until now NOTHING ever applied that tag, so
        // friendlyFireCount was permanently 0 and shooting the hostage was free.
        List<string> tags = e.Type == ScenarioEventType.HostageHit
            ? new List<string> { "friendly_fire" }
            : null;

        // Merge in any detector-supplied tags (e.g. GunFireDetector's "no_target_in_los").
        if (e.Tags != null && e.Tags.Count > 0)
        {
            tags ??= new List<string>();
            tags.AddRange(e.Tags);
        }

        var record = MissionEvent.Create(
            eventType,
            elapsed,
            sourceId,
            targetId,
            roomId,
            e.Origin,
            tags,
            e.Distance
        );

        SessionLogger.Instance.LogEvent(record);

        if (echoToConsole)
            Debug.Log($"[Module4Bridge] event → {eventType} t={elapsed:F2}s by={sourceId ?? "—"} room={roomId ?? "—"}");
    }

    private void HandleStateChange(NPCStateChangeRecord rec)
    {
        if (rec == null) return;
        if (!EnsureSessionActive()) return;

        // TelemetryLogger records timestamps as absolute Time.time. Convert to
        // elapsed-since-session-start so dashboard charts plot correctly.
        float elapsed = Mathf.Max(0f, rec.timestamp - SessionLogger.Instance.SessionStartTime);

        if (string.Equals(rec.actorType, "Hostage", StringComparison.OrdinalIgnoreCase))
        {
            SessionLogger.Instance.LogHostageStateChange(
                rec.actorId,
                rec.newState,
                rec.triggerEventType,
                elapsed,
                ResolveHostageProfile(rec.actorId)
            );

            if (echoToConsole)
                Debug.Log($"[Module4Bridge] hostage → {rec.actorId}: {rec.previousState} → {rec.newState}");
        }
        else
        {
            var npcChange = NPCStateChange.Create(
                rec.actorId,
                rec.actorType,
                rec.previousState,
                rec.newState,
                rec.triggerEventType,
                elapsed
            );

            SessionLogger.Instance.LogNPCStateChange(npcChange);

            if (echoToConsole)
                Debug.Log($"[Module4Bridge] npc → {rec.actorId} ({rec.actorType}): {rec.previousState} → {rec.newState}");
        }
    }

    // Hostage personality profiles, resolved once per hostage and cached. Lives here rather
    // than being threaded through TelemetryLogger because Module4Bridge sits in
    // Assembly-CSharp and can see BOTH sides — HostageController (Assembly-CSharp) and
    // SessionLogger (TeamSentinels.Module4) — which is the whole reason this bridge exists.
    // Threading a profile parameter through Module 2's telemetry contract would have touched
    // four files and coupled Module 2's logger to a Module 4 concern.
    private readonly Dictionary<string, string> _hostageProfileCache = new Dictionary<string, string>();

    /// <summary>
    /// Looks up a hostage's personality profile by actor id ("Weak"/"Normal"/"Brave"), or null
    /// if no matching HostageController is in the scene. Cached because a profile is fixed for
    /// the whole session, so the scene scan happens at most once per hostage.
    /// </summary>
    private string ResolveHostageProfile(string actorId)
    {
        if (string.IsNullOrEmpty(actorId)) return null;
        if (_hostageProfileCache.TryGetValue(actorId, out string cached)) return cached;

        string resolved = null;
        foreach (var h in FindObjectsByType<HostageController>(FindObjectsSortMode.None))
        {
            if (h.NPCId == actorId) { resolved = h.ActiveProfile.ToString(); break; }
        }

        _hostageProfileCache[actorId] = resolved; // cache misses too — don't rescan every change
        return resolved;
    }

    private Module4SessionController _cachedController;

    private bool EnsureSessionActive()
    {
        if (SessionLogger.Instance == null)
        {
            Debug.LogWarning("[Module4Bridge] SessionLogger.Instance is null — drop the event.");
            return false;
        }

        if (SessionLogger.Instance.IsSessionActive) return true;

        if (!autoStartOnFirstEvent) return false;

        // Prefer Module4SessionController.StartSession() so it can register replay
        // actors and start the recording coroutine. Falls back to a direct
        // SessionLogger call if no controller is in the scene.
        if (_cachedController == null)
        {
            _cachedController = GetComponent<Module4SessionController>();
            if (_cachedController == null)
                _cachedController = FindFirstObjectByType<Module4SessionController>();
        }

        if (_cachedController != null)
        {
            _cachedController.StartSession();
            Debug.Log("[Module4Bridge] Auto-started session via Module4SessionController (replay setup included).");
        }
        else
        {
            SessionLogger.Instance.StartSession(autoStartScenarioId);
            Debug.Log("[Module4Bridge] Auto-started session via SessionLogger (no Module4SessionController found, replay disabled): " + autoStartScenarioId);
        }
        return true;
    }
}
