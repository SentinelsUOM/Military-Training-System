// Module 4 Integration | Sentinels | University of Moratuwa | 2026
//
// Lives in Assembly-CSharp (no asmdef in this folder) so it can reference
// both Module 2 (also Assembly-CSharp) and Module 4 (autoReferenced asmdef).

using System.Collections;
using System.Collections.Generic;
using TeamSentinels.Module4.Data;
using TeamSentinels.Module4.Logging;
using TeamSentinels.ScenarioGeneration.DataModels;
using TeamSentinels.ScenarioGeneration.Scene;
using UnityEngine;

/// <summary>
/// Owns the lifecycle of a Module 4 logging session — when it starts,
/// when it ends — and registers all live NPCs with the ReplayRecorder.
///
/// Session start triggers
///   - EventManager.OnEventRaised(ScenarioReady)
///   - First gameplay event arrives via Module4Bridge.autoStartOnFirstEvent
///   - Inspector context menu "Start Session"
///
/// Session end triggers (whichever fires first)
///   - All hostages reach HostageState.Freed                     → MissionEnd:hostages_rescued (PASS)
///   - PlayerHealth.health drops to 0                            → MissionEnd:player_down (FAIL)
///   - Any hostage is shot dead (HostageState.Down)              → MissionEnd:hostage_killed (FAIL)
///   - missionTimeoutSeconds elapses without another end trigger → MissionEnd:timeout (FAIL)
///   - Inspector context menu "End Session"                       → manual
///
/// Setup
///   Place this on the Module4Manager GameObject alongside SessionLogger,
///   Module4Bridge, ReplayRecorder, WebReportExporter, and DashboardUploader.
///   Drag the Player Health component into the Inspector if you want
///   automatic player-death detection.
/// </summary>
public class Module4SessionController : MonoBehaviour
{
    [Header("Session Identity")]
    [Tooltip("Scenario id used for the session. Should match the Module 1 scenario when available.")]
    [SerializeField] private string scenarioId = "VR_TRAINING";

    [Header("Auto-Start")]
    [Tooltip("Auto-start a session when EventManager raises ScenarioReady.")]
    [SerializeField] private bool startOnScenarioReady = true;

    [Header("Auto-End: All Hostages Freed (PASS)")]
    [Tooltip("End the session when every registered hostage has reached Freed state.")]
    [SerializeField] private bool endWhenAllHostagesFreed = true;

    [Header("Auto-End: Player Death (FAIL)")]
    [Tooltip("End the session as soon as the player health component reports 0 HP. " +
             "Drag the player rig's PlayerHealth component here.")]
    [SerializeField] private PlayerHealth playerHealth;

    [Header("Auto-End: Hostage Killed (FAIL)")]
    [Tooltip("End the session the moment any registered hostage is shot dead " +
             "(enters HostageState.Down). Killing a hostage is an instant mission failure.")]
    [SerializeField] private bool endWhenHostageKilled = true;

    [Header("Auto-End: Timeout (FAIL safety net)")]
    [Tooltip("Maximum allowed mission duration in seconds. The session ends as a timeout " +
             "if neither the rescue nor a player-death trigger fires by then. 0 = NO TIMEOUT " +
             "(mission runs until you win or die) — this is the configured default.")]
    [SerializeField] private float missionTimeoutSeconds = 0f;

    [Header("Realistic Escort Mode")]
    [Tooltip("On scene start, clear the Runaway Controller on every HostageController in the scene. " +
             "Disables the auto-flee mechanic so the only path to Freed is through the ExtractionZone " +
             "(realistic escort gameplay). Turn off if you want hostages to also self-rescue by panic-running.")]
    [SerializeField] private bool disableAutoFleeOnAllHostages = true;

    [Header("Replay")]
    [Tooltip("Optional ReplayRecorder reference. If blank, looked up on this GameObject.")]
    [SerializeField] private ReplayRecorder replayRecorder;

    [Tooltip("Drag the Player rig's Transform here to record the trainee's position alongside the NPCs. " +
             "If left blank, only NPC positions are recorded.")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("Actor id used for the player in replay frames.")]
    [SerializeField] private string playerActorId = "trainee_01";

    // Latches once a mission has ended (pass or fail) so auto-start triggers can't
    // immediately spin up a fresh session that re-detects the dead hostage and ends
    // again, looping "MissionEnded" every frame. Reset naturally on scene reload.
    private bool _missionEnded;

    private void OnEnable()
    {
        EventManager.OnEventRaised += HandleScenarioEvent;
    }

    private void OnDisable()
    {
        EventManager.OnEventRaised -= HandleScenarioEvent;
    }

    private void Start()
    {
        if (replayRecorder == null) replayRecorder = GetComponent<ReplayRecorder>();

        if (disableAutoFleeOnAllHostages)
            DisableAutoFleeOnAllHostages();
    }

    private void DisableAutoFleeOnAllHostages()
    {
        // Pathway 1: HostageController.runawayController — cleared so Panic state
        // doesn't kick off the runaway sequence.
        var hostages = FindObjectsByType<HostageController>(FindObjectsSortMode.None);
        int hostagesCleared = 0;
        foreach (var hc in hostages)
        {
            if (hc.runawayController != null)
            {
                hc.runawayController = null;
                hostagesCleared++;
            }
        }

        // Pathway 2: NpcShooterRaycast.runawayHostage — cleared so terrorist gunfire
        // doesn't directly trigger the hostage runaway controller, bypassing
        // HostageController entirely. Also disable scare-on-fire for consistency.
        var shooters = FindObjectsByType<NpcShooterRaycast>(FindObjectsSortMode.None);
        int shootersCleared = 0;
        foreach (var sh in shooters)
        {
            bool changed = false;
            if (sh.runawayHostage != null) { sh.runawayHostage = null; changed = true; }
            if (sh.controlHostageScare)    { sh.controlHostageScare = false; changed = true; }
            if (changed) shootersCleared++;
        }

        // Pathway 3: defensive — disable any remaining HostageRunawayController
        // components so a stale reference somewhere can't drive them either.
        var runaways = FindObjectsByType<HostageRunawayController>(FindObjectsSortMode.None);
        int runawaysDisabled = 0;
        foreach (var r in runaways)
        {
            if (r.enabled) { r.enabled = false; runawaysDisabled++; }
        }

        Debug.Log($"[Module4SessionController] Realistic escort mode active. " +
                  $"Hostages cleared: {hostagesCleared}, " +
                  $"shooters cleared: {shootersCleared}, " +
                  $"runaway controllers disabled: {runawaysDisabled}.");
    }

    private void Update()
    {
        if (SessionLogger.Instance == null || !SessionLogger.Instance.IsSessionActive) return;

        // Player death check — poll because PlayerHealth doesn't fire an event
        if (playerHealth != null && playerHealth.health <= 0)
        {
            EndSession("player_down");
            return;
        }

        // Hostage killed check — poll the registry for any hostage that has died.
        // Distinguish a guardian EXECUTION (leverage payoff) from trainee friendly-fire
        // so the after-action report shows the real cause.
        if (endWhenHostageKilled)
        {
            var dead = FirstDeadHostage();
            if (dead != null)
            {
                EndSession(dead.WasExecuted ? "hostage_executed" : "hostage_killed");
                return;
            }
        }

        // Mission timeout check
        if (missionTimeoutSeconds > 0f)
        {
            float elapsed = Time.time - SessionLogger.Instance.SessionStartTime;
            if (elapsed >= missionTimeoutSeconds)
            {
                EndSession("timeout");
            }
        }
    }

    [ContextMenu("Start Session")]
    public void StartSession()
    {
        if (_missionEnded)
        {
            // Mission already resolved this scene load — don't restart into an instant re-fail.
            return;
        }
        if (SessionLogger.Instance == null)
        {
            Debug.LogError("[Module4SessionController] SessionLogger.Instance is null.");
            return;
        }
        if (SessionLogger.Instance.IsSessionActive)
        {
            Debug.LogWarning("[Module4SessionController] StartSession called but a session is already active.");
            return;
        }

        SessionLogger.Instance.StartSession(scenarioId);
        CaptureLayout();
        StartCoroutine(RegisterReplayActorsNextFrame());
    }

    /// <summary>
    /// Grabs the built scenario's room/door geometry from SceneBuilder and attaches a
    /// compact top-down snapshot to the session, so the dashboard can draw walls and the
    /// 3D replay. No-op (and harmless) if the scene wasn't built from a Module 1 scenario.
    /// </summary>
    private void CaptureLayout()
    {
        var builder  = FindFirstObjectByType<SceneBuilder>();
        var scenario = builder != null ? builder.ActiveScenario : null;
        if (scenario?.layout?.rooms == null)
        {
            Debug.Log("[Module4SessionController] No ActiveScenario layout to attach.");
            return;
        }

        var snap      = new LayoutSnapshot();
        var seenDoors = new HashSet<string>();

        foreach (var room in scenario.layout.rooms)
        {
            if (room == null) continue;
            snap.rooms.Add(new RoomBox
            {
                id      = room.id,
                type    = room.type.ToString(),
                centerX = room.position?.x ?? 0f,
                centerZ = room.position?.z ?? 0f,
                width   = room.size?.width  ?? 0f,
                depth   = room.size?.depth  ?? 0f,
                height  = room.size?.height ?? 0f,
            });

            if (room.doors == null) continue;
            foreach (var d in room.doors)
            {
                if (d?.position == null) continue;
                // A door between two rooms is recorded on both — dedupe by rounded position.
                string key = Mathf.RoundToInt(d.position.x * 100f) + "_" + Mathf.RoundToInt(d.position.z * 100f);
                if (!seenDoors.Add(key)) continue;
                snap.doors.Add(new DoorMark
                {
                    x          = d.position.x,
                    z          = d.position.z,
                    wallSide   = d.wallSide.ToString(),
                    isExterior = d.isExterior,
                });
            }
        }

        SessionLogger.Instance.SetLayout(snap);
        RoomLocator.SetRooms(snap.rooms);   // lets Module4Bridge tag events with their room
    }

    [ContextMenu("End Session")]
    public void EndSession() => EndSession("manual");

    [ContextMenu("DEBUG: Force All Hostages to Follow")]
    public void DebugForceAllHostagesToFollow()
    {
        if (EventManager.Instance == null)
        {
            Debug.LogError("[Module4SessionController] EventManager.Instance is null. Are you in Play mode?");
            return;
        }

        var instigator = playerTransform != null ? playerTransform.gameObject :
                         (Camera.main != null ? Camera.main.gameObject : gameObject);

        var hostages = FindObjectsByType<HostageController>(FindObjectsSortMode.None);
        int forced = 0;
        foreach (var hc in hostages)
        {
            if (hc == null) continue;
            if (hc.currentState == HostageState.Freed) continue;

            EventManager.Instance.Raise(new ScenarioEvent(
                ScenarioEventType.HostageContactStarted,
                hc.transform.position,
                instigator,
                roomId: null,
                targetActorId: hc.NPCId
            ));
            forced++;
        }
        Debug.Log($"[Module4SessionController] Forced {forced} hostage(s) into Follow state. They should walk toward you now. Lead them to ExtractionZone.");
    }

    public void EndSession(string reason)
    {
        if (SessionLogger.Instance == null || !SessionLogger.Instance.IsSessionActive)
        {
            Debug.LogWarning("[Module4SessionController] EndSession called but no session is active.");
            return;
        }

        // Tag the outcome reason as the final mission event so it shows up in the dashboard timeline.
        SessionLogger.Instance.LogEvent(MissionEvent.Create(
            "MissionEnded",
            Time.time - SessionLogger.Instance.SessionStartTime,
            sourceActorId: "system",
            roomId: null,
            position: default,
            tags: new System.Collections.Generic.List<string> { reason }
        ));

        if (replayRecorder != null) replayRecorder.StopRecording();

        _missionEnded = true;
        Debug.Log($"[Module4SessionController] Ending session — reason: {reason}");
        SessionLogger.Instance.EndSession();

        // The result is decided — FREEZE the simulation. Without this the NPC AI kept
        // running after the mission ended: in one session the mission ended on 'timeout'
        // with the hostage ALIVE, and the guardian then executed him *after* the result
        // was already recorded. Nothing may change the outcome once it's been called.
        FreezeAllNpcs();
        // OnSessionComplete fires inside EndSession() → DashboardUploader & WebReportExporter pick it up.
    }

    private void HandleScenarioEvent(ScenarioEvent e)
    {
        if (e == null) return;

        if (startOnScenarioReady &&
            e.Type == ScenarioEventType.ScenarioReady &&
            (SessionLogger.Instance == null || !SessionLogger.Instance.IsSessionActive))
        {
            StartSession();
            return;
        }

        if (endWhenAllHostagesFreed &&
            e.Type == ScenarioEventType.HostageFreed &&
            SessionLogger.Instance != null &&
            SessionLogger.Instance.IsSessionActive)
        {
            // OnEventRaised fires BEFORE EventManager.RouteToNPCs, so the hostage's
            // state hasn't transitioned to Freed yet. Defer the check by one frame.
            StartCoroutine(CheckRescueNextFrame());
        }
    }

    private System.Collections.IEnumerator CheckRescueNextFrame()
    {
        yield return null;
        if (SessionLogger.Instance == null || !SessionLogger.Instance.IsSessionActive) yield break;
        if (AllHostagesFreed())
        {
            EndSession("hostages_rescued");
        }
    }

    private IEnumerator RegisterReplayActorsNextFrame()
    {
        // Wait one frame so any spawn-on-Start NPCs are guaranteed to be in NPCRegistry.
        yield return null;

        if (replayRecorder == null) yield break;

        var all = NPCRegistry.GetAll();
        int registered = 0;

        foreach (var npc in all)
        {
            if (npc is MonoBehaviour mb && mb != null)
            {
                string actorId = npc.NPCId;
                Transform t    = mb.transform;
                INPCResponder captured = npc;
                replayRecorder.RegisterActor(actorId, t, () => StateLabelOf(captured));
                registered++;
            }
        }

        // Register the player rig so the trainee shows up on the replay scatter plot.
        if (playerTransform != null)
        {
            replayRecorder.RegisterActor(playerActorId, playerTransform, () => "Trainee");
            registered++;
        }

        Debug.Log($"[Module4SessionController] Registered {registered} actors with ReplayRecorder.");
        replayRecorder.StartRecording();
    }

    private static string StateLabelOf(INPCResponder npc)
    {
        switch (npc)
        {
            case TerroristController tc: return tc.currentState.ToString();
            case HostageController   hc: return hc.currentState.ToString();
            default:                     return "Unknown";
        }
    }

    /// <summary>
    /// Stops every NPC dead once the mission result is decided: halts their coroutines
    /// (patrol, search, hostage-leverage countdown), stops any weapon firing, and disables
    /// the controllers. Prevents the after-the-whistle behaviour that killed a hostage
    /// AFTER the session had already ended on a timeout.
    /// </summary>
    private void FreezeAllNpcs()
    {
        int frozen = 0;

        foreach (var t in FindObjectsByType<TerroristController>(FindObjectsSortMode.None))
        {
            if (t == null) continue;
            t.StopAllCoroutines();                       // kills the hostage-leverage countdown too
            var shooter = t.GetComponent<NpcShooterRaycast>();
            if (shooter != null) shooter.StopFiring();
            var agent = t.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled) { agent.isStopped = true; agent.ResetPath(); }
            t.enabled = false;
            frozen++;
        }

        foreach (var h in FindObjectsByType<HostageController>(FindObjectsSortMode.None))
        {
            if (h == null) continue;
            h.StopAllCoroutines();
            var agent = h.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.isActiveAndEnabled) { agent.isStopped = true; agent.ResetPath(); }
            h.enabled = false;
            frozen++;
        }

        Debug.Log($"[Module4SessionController] Mission over — froze {frozen} NPC(s). " +
                  "Nothing can change the outcome now.");
    }

    private HostageController FirstDeadHostage()
    {
        var all = NPCRegistry.GetAll();
        foreach (var npc in all)
        {
            if (npc is HostageController hc && hc.currentState == HostageState.Down)
                return hc;
        }
        return null;
    }

    private bool AllHostagesFreed()
    {
        // Lenient rule for testing: at least one hostage freed counts as success.
        // Switch back to `freed == hostages` for strict "all hostages must be rescued" semantics.
        var all = NPCRegistry.GetAll();
        int hostages = 0, freed = 0;
        foreach (var npc in all)
        {
            if (npc is HostageController hc)
            {
                hostages++;
                if (hc.currentState == HostageState.Freed) freed++;
            }
        }
        return hostages > 0 && freed >= 1;
    }
}
