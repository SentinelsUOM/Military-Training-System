using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Integration test harness for Module 2 (NPC Behaviour and Coordination).
///
/// Setup (do this ONCE per evaluation/demo):
///   1. Open any scene (e.g. SampleScene or a fresh empty one).
///   2. Right-click in Hierarchy → Create Empty → name it "Module2TestRunner".
///   3. Add Component → search "Module2TestRunner" → add this script.
///   4. Press Play.
///   5. In the Inspector, right-click the Module2TestRunner component header
///      → "TEST → Run All" (the ContextMenu entry).
///   6. Watch the Console — green "✅" lines = pass, red "❌" lines = fail.
///   7. Final summary line shows pass/fail counts.
///
/// What the tests cover:
///   • Terrorist FSM transitions (Idle→Suspicious→Alert→Engage→Down)
///   • Terrorist Retreat — low-health hit during Engage falls back (once per life)
///   • Hostage FSM transitions (Calm→Fearful→Panic, Fearful→Freeze)
///   • Squad coordination — Leader-Alert triggers a Converge directive
///   • Squad coordination — Leader-Engage triggers a Flank directive
///   • PatrolLine multi-waypoint — route traversal + loop wrap-around
///   • NPCSelector LOS term — clear-LOS NPC outscores blocked-LOS NPC
///   • NPCSelector ablation — disabling distance still returns a valid winner
///
/// Why MonoBehaviour and not Unity Test Framework:
///   The Module 1 tests already use this pattern (no asmdef gymnastics needed,
///   no special package). Beginners can run these without any setup beyond
///   attaching the script and right-clicking.
/// </summary>
public class Module2TestRunner : MonoBehaviour
{
    int _pass, _fail;
    bool _currentTestFailed;
    readonly List<string> _failLog = new List<string>();

    [Header("Settings")]
    [Tooltip("Frames to wait between Raise() and the assertion — gives Update() time to process state changes.")]
    public int waitFramesBetweenSteps = 2;

    [ContextMenu("TEST → Run All")]
    void RunAllMenu() => StartCoroutine(RunAll());

    IEnumerator RunAll()
    {
        _pass = 0;
        _fail = 0;
        _failLog.Clear();

        EnsureSingletons();
        NPCSelector.ResetWeights();

        Debug.Log("════════════════════════════════════════════════");
        Debug.Log("  Module 2 Integration Tests — START");
        Debug.Log("════════════════════════════════════════════════");

        yield return RunTest("Terrorist: Idle → Suspicious on GunshotHeard",
                             TestTerroristIdleToSuspicious());

        yield return RunTest("Terrorist: Suspicious → Alert on PlayerSeen",
                             TestTerroristSuspiciousToAlert());

        yield return RunTest("Terrorist: Alert → Engage on TargetConfirmed",
                             TestTerroristAlertToEngage());

        yield return RunTest("Terrorist: fatal hit → Down",
                             TestTerroristTakeHitDown());

        yield return RunTest("Terrorist: low-health hit during Engage → Retreat (once)",
                             TestTerroristRetreat());

        yield return RunTest("Terrorist: Alert with no re-acquire → gives up → Idle",
                             TestTerroristAlertGiveUp());

        yield return RunTest("Hostage: Calm → Panic on close gunshot",
                             TestHostageCalmToPanic());

        yield return RunTest("Hostage: Fearful → Freeze on very-close gunshot",
                             TestHostageFearfulToFreeze());

        yield return RunTest("Squad: Leader Alert → squad receives Converge directive",
                             TestLeaderDirectiveIssued());

        yield return RunTest("Squad: Leader Engage → squad receives Flank directive",
                             TestLeaderFlankDirective());

        yield return RunTest("PatrolLine: multi-waypoint route traversal + loop wrap",
                             TestPatrolLineMultiWaypoint());

        yield return RunTest("NPCSelector: clear LOS wins over blocked LOS",
                             TestNPCSelectorLOS());

        yield return RunTest("NPCSelector: ablation (distance=0) still returns a winner",
                             TestNPCSelectorAblation());

        Debug.Log("════════════════════════════════════════════════");
        Debug.Log($"  RESULTS:  ✅ PASS {_pass}   ❌ FAIL {_fail}");
        Debug.Log("════════════════════════════════════════════════");

        foreach (var f in _failLog) Debug.LogError(f);
    }

    IEnumerator RunTest(string name, IEnumerator body)
    {
        Debug.Log($"▶ {name}");
        _currentTestFailed = false;

        // Run the test body — failures are recorded via Fail() not exceptions
        while (body.MoveNext())
            yield return body.Current;

        // Settle frame so any Destroy()s complete before next test
        yield return null;
        yield return null;
        Squad.ClearAll();

        if (_currentTestFailed)
        {
            _fail++;
            Debug.LogError($"❌ FAIL: {name}");
        }
        else
        {
            _pass++;
            Debug.Log($"✅ PASS: {name}");
        }
    }

    void Fail(string message)
    {
        _currentTestFailed = true;
        _failLog.Add($"❌ {message}");
        Debug.LogError($"   assertion failed: {message}");
    }

    void AssertEqual<T>(T expected, T actual, string what)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            Fail($"{what}: expected {expected}, got {actual}");
    }

    void AssertTrue(bool cond, string what)
    {
        if (!cond) Fail($"{what} was false");
    }

    // ── Test setup helpers ────────────────────────────────────────────────

    void EnsureSingletons()
    {
        if (EventManager.Instance == null)
        {
            var go = new GameObject("[TestRunner] EventManager");
            var em = go.AddComponent<EventManager>();
            em.logEvents = false;
        }
        if (AlertPropagator.Instance == null)
        {
            var go = new GameObject("[TestRunner] AlertPropagator");
            go.AddComponent<AlertPropagator>();
        }
        if (TelemetryLogger.Instance == null)
        {
            var go = new GameObject("[TestRunner] TelemetryLogger");
            var tl = go.AddComponent<TelemetryLogger>();
            tl.echoToConsole = false;
        }
    }

    IEnumerator WaitFrames(int n)
    {
        for (int i = 0; i < n; i++) yield return null;
    }

    TerroristController SpawnTerrorist(string id, Vector3 pos, NPCRole role, string squad = "")
    {
        var go = new GameObject(id);
        go.transform.position = pos;
        var t = go.AddComponent<TerroristController>();
        t.role             = role;
        t.squadId          = squad;
        t.idleMode         = IdleMode.Static;   // no wander/patrol mechanics in tests
        t.responseCooldown = 0f;                // no cooldown so back-to-back events work
        t.hearingRange     = 100f;              // hear everything in tests
        return t;
    }

    HostageController SpawnHostage(string id, Vector3 pos)
    {
        var go = new GameObject(id);
        go.transform.position = pos;
        var h = go.AddComponent<HostageController>();
        h.responseCooldown = 0f;

        // Pin the personality profile so these DISTANCE-threshold tests stay deterministic.
        //
        // Hostages now default to a research-weighted RANDOM profile (see HostageProfile.cs),
        // and profiles differ in how reliably a non-threat is recognised. A Weak hostage can
        // legitimately read a FAR gunshot as point-blank and go to Panic instead of Fearful —
        // which would make "Fearful → Freeze on very-close gunshot" fail ~10% of runs for a
        // reason that is correct behaviour, not a bug.
        //
        // Brave = discrimination 1.0 = never misreads = the pre-profile behaviour these two
        // tests were written against, so they keep testing the distance thresholds only.
        // Profile behaviour itself is covered separately by HostageProfileTestRunner.
        h.assignRandomProfile = false;
        h.profile = HostageProfile.Brave;

        return h;
    }

    void Destroy(params Object[] objs)
    {
        foreach (var o in objs) if (o != null) Object.Destroy(o);
    }

    // ── Terrorist FSM tests ───────────────────────────────────────────────

    IEnumerator TestTerroristIdleToSuspicious()
    {
        var t = SpawnTerrorist("T_IdleSusp", Vector3.zero, NPCRole.Roamer);
        yield return WaitFrames(1);

        AssertEqual(TerroristState.Idle, t.currentState, "initial state");

        EventManager.Instance.Raise(new ScenarioEvent(
            ScenarioEventType.GunshotHeard, Vector3.forward * 5f));
        yield return WaitFrames(waitFramesBetweenSteps);

        AssertEqual(TerroristState.Suspicious, t.currentState, "after GunshotHeard");

        Destroy(t.gameObject);
    }

    IEnumerator TestTerroristSuspiciousToAlert()
    {
        var t = SpawnTerrorist("T_SuspAlert", Vector3.zero, NPCRole.Guard);
        yield return WaitFrames(1);

        EventManager.Instance.Raise(new ScenarioEvent(
            ScenarioEventType.GunshotHeard, Vector3.forward * 5f));
        yield return WaitFrames(waitFramesBetweenSteps);

        // Perception is personal — deliver PlayerSeen via the NPC's own
        // detection path (HandleDetection), the same call PerceptionController
        // makes. (It is intentionally NOT routed over the event bus anymore.)
        t.HandleDetection(new ScenarioEvent(
            ScenarioEventType.PlayerSeen, Vector3.forward * 5f, t.gameObject));
        yield return WaitFrames(waitFramesBetweenSteps);

        AssertEqual(TerroristState.Alert, t.currentState, "after PlayerSeen");

        Destroy(t.gameObject);
    }

    IEnumerator TestTerroristAlertToEngage()
    {
        var t = SpawnTerrorist("T_AlertEngage", Vector3.zero, NPCRole.Guard);
        yield return WaitFrames(1);

        t.HandleDetection(new ScenarioEvent(
            ScenarioEventType.PlayerSeen, Vector3.forward * 5f, t.gameObject));
        yield return WaitFrames(waitFramesBetweenSteps);

        t.HandleDetection(new ScenarioEvent(
            ScenarioEventType.TargetConfirmed, Vector3.forward * 5f, t.gameObject));
        yield return WaitFrames(waitFramesBetweenSteps);

        AssertEqual(TerroristState.Engage, t.currentState, "after TargetConfirmed");

        Destroy(t.gameObject);
    }

    IEnumerator TestTerroristTakeHitDown()
    {
        var t = SpawnTerrorist("T_TakeHit", Vector3.zero, NPCRole.Guard);
        yield return WaitFrames(1);

        t.TakeHit(9999f);
        yield return WaitFrames(waitFramesBetweenSteps);

        AssertEqual(TerroristState.Down, t.currentState, "after fatal TakeHit");

        Destroy(t.gameObject);
    }

    IEnumerator TestTerroristRetreat()
    {
        var t = SpawnTerrorist("T_Retreat", Vector3.zero, NPCRole.Guard);
        yield return WaitFrames(1);

        // Drive to Engage via the NPC's own perception: PlayerSeen → Alert,
        // TargetConfirmed → Engage.
        t.HandleDetection(new ScenarioEvent(
            ScenarioEventType.PlayerSeen, Vector3.forward * 5f, t.gameObject));
        yield return WaitFrames(waitFramesBetweenSteps);

        t.HandleDetection(new ScenarioEvent(
            ScenarioEventType.TargetConfirmed, Vector3.forward * 5f, t.gameObject));
        yield return WaitFrames(waitFramesBetweenSteps);

        AssertEqual(TerroristState.Engage, t.currentState, "pre-condition: Engage");

        // Wounding hit: 100 → 30 — at/below retreatHealthThreshold (40), above Down (20)
        t.TakeHit(70f);
        yield return WaitFrames(waitFramesBetweenSteps);

        AssertEqual(TerroristState.Retreat, t.currentState, "after low-health hit");

        // Second wounding hit must NOT re-trigger Retreat (one per life) and
        // 30 → 25 is still above the Down threshold, so the state is unchanged.
        t.TakeHit(5f);
        yield return WaitFrames(waitFramesBetweenSteps);
        AssertEqual(TerroristState.Retreat, t.currentState,
                    "state after second non-fatal hit (no re-trigger, no Down)");

        Destroy(t.gameObject);
    }

    IEnumerator TestTerroristAlertGiveUp()
    {
        var t = SpawnTerrorist("T_GiveUp", Vector3.zero, NPCRole.Guard);
        t.alertGiveUpTime = 0.3f;   // short so the test is fast
        yield return WaitFrames(1);

        // Personal sighting drives Idle → Alert.
        t.HandleDetection(new ScenarioEvent(
            ScenarioEventType.PlayerSeen, Vector3.forward * 5f, t.gameObject));
        yield return WaitFrames(waitFramesBetweenSteps);
        AssertEqual(TerroristState.Alert, t.currentState, "after PlayerSeen");

        // No further contact — after alertGiveUpTime it should resume patrol (Idle).
        // (No NavMeshAgent here, so no search runs; the give-up timer is the path.)
        float deadline = Time.time + 1.5f;
        while (Time.time < deadline && t.currentState == TerroristState.Alert)
            yield return null;

        AssertEqual(TerroristState.Idle, t.currentState, "after give-up timeout");

        Destroy(t.gameObject);
    }

    // ── Hostage FSM tests ─────────────────────────────────────────────────

    IEnumerator TestHostageCalmToPanic()
    {
        var h = SpawnHostage("H_CalmPanic", Vector3.zero);
        yield return WaitFrames(1);

        AssertEqual(HostageState.Calm, h.currentState, "initial state");

        // Close gunshot — within panicDistance (default 6m)
        EventManager.Instance.Raise(new ScenarioEvent(
            ScenarioEventType.GunshotHeard, Vector3.forward * 2f));
        yield return WaitFrames(waitFramesBetweenSteps);

        AssertEqual(HostageState.Panic, h.currentState, "after close gunshot");

        Destroy(h.gameObject);
    }

    IEnumerator TestHostageFearfulToFreeze()
    {
        var h = SpawnHostage("H_FearFreeze", Vector3.zero);
        yield return WaitFrames(1);

        // Far gunshot → Fearful (outside panicDistance 6m)
        EventManager.Instance.Raise(new ScenarioEvent(
            ScenarioEventType.GunshotHeard, Vector3.forward * 10f));
        yield return WaitFrames(waitFramesBetweenSteps);
        AssertEqual(HostageState.Fearful, h.currentState, "after far gunshot");

        // Close gunshot while Fearful → Freeze (within freezeRange 5m)
        EventManager.Instance.Raise(new ScenarioEvent(
            ScenarioEventType.GunshotHeard, Vector3.forward * 2f));
        yield return WaitFrames(waitFramesBetweenSteps);
        AssertEqual(HostageState.Freeze, h.currentState, "after close gunshot while Fearful");

        Destroy(h.gameObject);
    }

    // ── Coordination tests ────────────────────────────────────────────────

    IEnumerator TestLeaderDirectiveIssued()
    {
        var leader = SpawnTerrorist("LeaderAlpha", Vector3.zero, NPCRole.Leader, "alpha");
        var m1     = SpawnTerrorist("MemberA",     Vector3.right * 5f, NPCRole.Guard,  "alpha");
        var m2     = SpawnTerrorist("MemberB",     Vector3.right * 8f, NPCRole.Guard,  "alpha");
        yield return WaitFrames(1);

        AssertTrue(Squad.Get("alpha")?.CurrentDirective == null,
                   "Squad should have no directive before Leader is alerted");

        // RoomBreached is a TARGETED event — NPCSelector may pick a Guard member
        // (role bonus 3.0) rather than the Leader. The Leader is then alerted via
        // AlertPropagator Ring 1, which has a 0.3 s delay — so wait until the
        // Leader escalates (up to ~2 s) instead of a fixed 2 frames.
        EventManager.Instance.Raise(new ScenarioEvent(
            ScenarioEventType.RoomBreached, Vector3.forward * 3f));

        float deadline = Time.time + 2f;
        while (Time.time < deadline &&
               leader.currentState != TerroristState.Alert &&
               leader.currentState != TerroristState.Engage)
            yield return null;

        AssertTrue(leader.currentState == TerroristState.Alert ||
                   leader.currentState == TerroristState.Engage,
                   $"Leader should be Alert/Engage, got {leader.currentState}");

        var squad = Squad.Get("alpha");
        AssertTrue(squad != null, "Squad alpha should exist");
        AssertTrue(squad?.CurrentDirective != null,
                   "Squad should have a directive after Leader entered Alert");

        if (squad?.CurrentDirective != null)
        {
            AssertEqual("Converge", squad.CurrentDirective.Type.ToString(), "directive type");
            AssertTrue(squad.CurrentDirective.Source == leader,
                       "directive source should be the Leader");
        }

        Destroy(leader.gameObject, m1.gameObject, m2.gameObject);
    }

    IEnumerator TestLeaderFlankDirective()
    {
        var leader = SpawnTerrorist("LeaderBravo", Vector3.zero,       NPCRole.Leader, "bravo");
        var member = SpawnTerrorist("MemberC",     Vector3.right * 5f, NPCRole.Guard,  "bravo");
        yield return WaitFrames(1);

        // Drive the Leader's OWN perception (HandleDetection bypasses NPCSelector,
        // so this test is deterministic — no routing or Ring-1 delays involved).
        // PlayerSeen: Idle → Alert (issues Converge).
        leader.HandleDetection(new ScenarioEvent(
            ScenarioEventType.PlayerSeen, Vector3.forward * 3f));
        yield return WaitFrames(waitFramesBetweenSteps);

        AssertEqual(TerroristState.Alert, leader.currentState, "Leader after PlayerSeen");

        // TargetConfirmed: Alert → Engage → Flank directive replaces Converge.
        leader.HandleDetection(new ScenarioEvent(
            ScenarioEventType.TargetConfirmed, Vector3.forward * 3f));
        yield return WaitFrames(waitFramesBetweenSteps);

        AssertEqual(TerroristState.Engage, leader.currentState, "Leader state");

        var squad = Squad.Get("bravo");
        AssertTrue(squad?.CurrentDirective != null,
                   "Squad should hold a directive after Leader entered Engage");

        if (squad?.CurrentDirective != null)
        {
            AssertEqual("Flank", squad.CurrentDirective.Type.ToString(),
                        "directive type after Leader Engage");
            AssertTrue(squad.CurrentDirective.Source == leader,
                       "directive source should be the Leader");
        }

        Destroy(leader.gameObject, member.gameObject);
    }

    // ── PatrolLine tests ──────────────────────────────────────────────────

    IEnumerator TestPatrolLineMultiWaypoint()
    {
        var npc = new GameObject("P_MultiWaypoint");
        npc.transform.position = Vector3.zero;
        var patrol = npc.AddComponent<PatrolLine>();
        patrol.moveSpeed    = 10f;   // fast so the test completes in a few frames
        patrol.stopDistance = 0.3f;

        var w0 = new GameObject("wp0").transform; w0.position = Vector3.zero;
        var w1 = new GameObject("wp1").transform; w1.position = new Vector3(0f, 0f, 1.5f);
        var w2 = new GameObject("wp2").transform; w2.position = new Vector3(1.5f, 0f, 1.5f);

        patrol.SetWaypoints(new[] { w0, w1, w2 }, looping: true);

        AssertEqual(3, patrol.RouteLength, "route length");
        AssertEqual(1, patrol.CurrentTargetIndex, "initial target index");

        // Walk: should reach wp1 and advance to wp2…
        int frames = 0;
        while (patrol.CurrentTargetIndex != 2 && frames++ < 300) yield return null;
        AssertTrue(patrol.CurrentTargetIndex == 2,
                   $"route should advance to waypoint 2, index={patrol.CurrentTargetIndex}");

        // …then loop mode should wrap back to wp0 after reaching wp2.
        frames = 0;
        while (patrol.CurrentTargetIndex != 0 && frames++ < 300) yield return null;
        AssertTrue(patrol.CurrentTargetIndex == 0,
                   $"looping route should wrap to waypoint 0, index={patrol.CurrentTargetIndex}");

        Destroy(npc, w0.gameObject, w1.gameObject, w2.gameObject);
    }

    // ── NPCSelector tests ─────────────────────────────────────────────────

    IEnumerator TestNPCSelectorLOS()
    {
        // Wall between origin and 'blocked' NPC; 'clear' NPC is on the opposite side.
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "[Test] Wall";
        wall.transform.position   = new Vector3(2.5f, 1f, 0f);
        wall.transform.localScale = new Vector3(0.5f, 3f, 5f);

        var clearLOS   = SpawnTerrorist("Clear",   new Vector3(-5f, 0f, 0f), NPCRole.Roamer);
        var blockedLOS = SpawnTerrorist("Blocked", new Vector3( 5f, 0f, 0f), NPCRole.Roamer);
        yield return WaitFrames(1);

        NPCSelector.ResetWeights();
        NPCSelector.LosObstacleLayers = ~0;

        var e = new ScenarioEvent(ScenarioEventType.GunshotHeard, Vector3.zero);
        var winner = NPCSelector.SelectBest(
            new List<INPCResponder> { clearLOS, blockedLOS }, e, log: false);

        AssertTrue(winner == clearLOS,
                   $"Expected NPC with clear LOS to win, got {winner?.NPCId ?? "null"}");

        Destroy(clearLOS.gameObject, blockedLOS.gameObject, wall);
    }

    IEnumerator TestNPCSelectorAblation()
    {
        var near = SpawnTerrorist("Near", Vector3.forward * 2f,  NPCRole.Roamer);
        var far  = SpawnTerrorist("Far",  Vector3.forward * 20f, NPCRole.Roamer);
        yield return WaitFrames(1);

        // First confirm defaults: near should win on distance
        NPCSelector.ResetWeights();
        var e = new ScenarioEvent(ScenarioEventType.GunshotHeard, Vector3.zero);
        var winnerDefault = NPCSelector.SelectBest(
            new List<INPCResponder> { near, far }, e, log: false);
        AssertTrue(winnerDefault == near,
                   "With defaults, the near NPC should win on distance term");

        // Now ablate distance — call should still return a valid winner
        NPCSelector.SetAblation(distance: false);
        var winnerAblated = NPCSelector.SelectBest(
            new List<INPCResponder> { near, far }, e, log: false);
        AssertTrue(winnerAblated != null,
                   "Ablated SelectBest should still return a winner");

        NPCSelector.ResetWeights();
        Destroy(near.gameObject, far.gameObject);
    }
}
