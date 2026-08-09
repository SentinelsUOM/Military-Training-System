using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Generates evaluation data for the "context-aware multi-factor scoring" research
/// claim on the Module 2 slide.
///
/// What it does:
///   1. Spawns N terrorists with random roles in a square area.
///   2. Generates M random event ORIGIN POSITIONS, reused IDENTICALLY across all
///      three tested event types — so the only thing that varies between event
///      types is which role's bonus applies, never the geometry the candidates
///      are being scored against.
///   3. For EACH (event type × event position), runs NPCSelector.SelectBest FIVE
///      times — once per ablation configuration:
///        • Full       — all four scoring terms enabled (baseline)
///        • NoDistance — distance term disabled
///        • NoRole     — role-bonus term disabled
///        • NoState    — state-readiness term disabled
///        • NoLOS      — perception term disabled
///   4. Records every decision to a CSV file in Application.persistentDataPath/Telemetry/
///   5. Prints a per-event-type summary table to the Console.
///
/// Event types tested (one per entry in NPCSelector.GetRoleBonus):
///   GunshotHeard  → favours Roamer (+2.0)
///   RoomBreached  → favours Guard  (+3.0)
///   AllyDownSeen  → favours Leader (+1.5)
///
/// WHY ALL THREE, NOT JUST GunshotHeard:
///   Earlier versions of this runner only fired GunshotHeard events. Since
///   Guard's bonus only applies to RoomBreached and Leader's only to
///   AllyDownSeen, a GunshotHeard-only test can NEVER exercise those two
///   bonuses — every Guard/Leader candidate scores 0 role bonus on every
///   single test event, identical to "no role match at all". That run could
///   only ever produce evidence for the Roamer/GunshotHeard=2.0 bonus, none
///   for Guard=3.0 or Leader=1.5. Testing all three event types closes that
///   gap and lets each role's own bonus be measured on its own event type.
///
/// What you do with the CSV:
///   • Compare WHICH NPC each ablation picked for the same event — if Full and
///     NoLOS pick the same NPC 99% of the time, LOS is doing nothing; if they
///     disagree 30% of the time, LOS materially shifts selection.
///   • Group by event_type to see whether each role's bonus produces the
///     intended dominance for ITS OWN event type, and whether the RELATIVE
///     dominance (Guard's 3.0 vs Roamer's 2.0 vs Leader's 1.5) tracks the
///     relative bonus magnitude — evidence for the ordering, even without a
///     literature source for the exact numbers.
///   • Open in Excel or run generate_ablation_charts.py for print-ready figures.
///
/// Setup (same as Module2TestRunner):
///   1. Open any scene (e.g. SampleScene or a fresh empty one).
///   2. Create Empty → add this component.
///   3. Press Play.
///   4. Right-click the component → "EXP → Run Ablation Experiment".
///   5. Console will print the output file path when done.
///
/// Defaults: 8 NPCs × 50 events × 3 event types × 5 modes = 750 decision rows.
/// </summary>
public class AblationExperimentRunner : MonoBehaviour
{
    [Header("Experiment parameters")]
    [Tooltip("Number of terrorist NPCs to spawn.")]
    public int  npcCount = 8;

    [Tooltip("Number of random event positions to evaluate PER event type " +
             "(the same positions are reused across all event types).")]
    public int  eventCount = 50;

    [Tooltip("Side length of the square arena (NPCs spawn within ±arenaSize/2).")]
    public float arenaSize = 30f;

    [Tooltip("Seed for reproducible NPC placement and event positions.")]
    public int seed = 42;

    [Tooltip("If true, places one Cube wall at the centre to create LOS occlusion.")]
    public bool addCentreWall = true;

    // The three event types under test — one per role-bonus entry in
    // NPCSelector.GetRoleBonus. Order here is also the order printed/logged.
    static readonly ScenarioEventType[] EventTypesTested =
    {
        ScenarioEventType.GunshotHeard,   // favours Roamer (+2.0)
        ScenarioEventType.RoomBreached,   // favours Guard  (+3.0)
        ScenarioEventType.AllyDownSeen,   // favours Leader (+1.5)
    };

    [ContextMenu("EXP → Run Ablation Experiment")]
    void RunMenu() => StartCoroutine(RunExperiment());

    IEnumerator RunExperiment()
    {
        EnsureSingletons();
        Squad.ClearAll();
        NPCSelector.ResetWeights();

        Debug.Log($"[Ablation] Starting: {npcCount} NPCs × {eventCount} events × " +
                  $"{EventTypesTested.Length} event types × 5 ablations = " +
                  $"{eventCount * EventTypesTested.Length * 5} decisions");

        // ── Setup: spawn NPCs ─────────────────────────────────────────────
        var rng     = new System.Random(seed);
        var npcs    = new List<TerroristController>();
        var spawned = new List<GameObject>();

        // Round-robin through every non-terminal FSM state so StateScore actually varies
        // across candidates. Without this every spawned NPC defaults to Idle (score 1.0)
        // and the NoState ablation is inert by construction — it can never move the
        // selection because every candidate loses the same constant amount.
        var stateCycle = new[]
        {
            TerroristState.Idle, TerroristState.Suspicious, TerroristState.Alert,
            TerroristState.TakeCover, TerroristState.Retreat,
        };

        for (int i = 0; i < npcCount; i++)
        {
            float x = RandomFloat(rng, -arenaSize / 2f, arenaSize / 2f);
            float z = RandomFloat(rng, -arenaSize / 2f, arenaSize / 2f);
            var role = (NPCRole)(i % 3); // round-robin: Guard / Roamer / Leader
            var t = SpawnTerrorist($"AblationNPC_{i:D2}", new Vector3(x, 0f, z), role);
            t.currentState = stateCycle[i % stateCycle.Length];
            npcs.Add(t);
            spawned.Add(t.gameObject);
        }

        GameObject wall = null;
        if (addCentreWall)
        {
            wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "[Ablation] CentreWall";
            wall.transform.position   = new Vector3(0f, 1f, 0f);
            wall.transform.localScale = new Vector3(10f, 3f, 1f);
            spawned.Add(wall);
        }

        yield return null;
        yield return null; // let Awake/registration settle

        // ── Pre-generate the shared event positions ──────────────────────
        // Drawn ONCE and reused for every event type below, so a difference in
        // outcome between e.g. GunshotHeard and RoomBreached can only be caused
        // by which role gets the bonus for that event type — never by the two
        // event types having been tested against different points in space.
        var eventOrigins = new List<Vector3>(eventCount);
        for (int ev = 0; ev < eventCount; ev++)
        {
            float ex = RandomFloat(rng, -arenaSize / 2f, arenaSize / 2f);
            float ez = RandomFloat(rng, -arenaSize / 2f, arenaSize / 2f);
            eventOrigins.Add(new Vector3(ex, 0f, ez));
        }

        // ── Run experiment ────────────────────────────────────────────────
        var csv = new StringBuilder();
        csv.AppendLine("event_type,event_id,ablation_mode,event_x,event_z,selected_npc,selected_role,distance_m,had_los,state_score");

        var ablations = new (string name, System.Action apply)[]
        {
            ("Full",       () => NPCSelector.SetAblation(distance: true,  role: true,  state: true,  los: true )),
            ("NoLOS",      () => NPCSelector.SetAblation(distance: true,  role: true,  state: true,  los: false)),
            ("NoDistance", () => NPCSelector.SetAblation(distance: false, role: true,  state: true,  los: true )),
            ("NoRole",     () => NPCSelector.SetAblation(distance: true,  role: false, state: true,  los: true )),
            ("NoState",    () => NPCSelector.SetAblation(distance: true,  role: true,  state: false, los: true )),
        };

        var allCandidates = new List<INPCResponder>(npcs.Count);
        foreach (var n in npcs) allCandidates.Add(n);

        // Counters for the console summary: eventType → mode → role → count
        var picksPerTypeMode = new Dictionary<string, Dictionary<string, Dictionary<string, int>>>();
        foreach (var et in EventTypesTested)
        {
            var perMode = new Dictionary<string, Dictionary<string, int>>();
            foreach (var ab in ablations) perMode[ab.name] = new Dictionary<string, int>();
            picksPerTypeMode[et.ToString()] = perMode;
        }

        foreach (var eventType in EventTypesTested)
        {
            for (int ev = 0; ev < eventCount; ev++)
            {
                Vector3 origin = eventOrigins[ev];
                var e = new ScenarioEvent(eventType, origin);

                foreach (var ab in ablations)
                {
                    ab.apply();
                    var winner = NPCSelector.SelectBest(allCandidates, e, log: false);
                    if (winner == null)
                    {
                        csv.AppendLine($"{eventType},{ev},{ab.name},{origin.x:F2},{origin.z:F2},NONE,-,-,-,-");
                        continue;
                    }

                    float distance = Vector3.Distance(winner.Position, origin);
                    bool  hasLos   = !Physics.Raycast(
                        winner.Position + Vector3.up * 1.5f,
                        (origin + Vector3.up * 1.5f - (winner.Position + Vector3.up * 1.5f)).normalized,
                        distance,
                        NPCSelector.LosObstacleLayers,
                        QueryTriggerInteraction.Ignore);

                    csv.AppendLine($"{eventType},{ev},{ab.name},{origin.x:F2},{origin.z:F2},{winner.NPCId},{winner.Role},{distance:F2},{(hasLos ? 1 : 0)},{winner.StateScore:F2}");

                    // tally
                    string roleKey = winner.Role.ToString();
                    var counts = picksPerTypeMode[eventType.ToString()][ab.name];
                    if (!counts.ContainsKey(roleKey)) counts[roleKey] = 0;
                    counts[roleKey]++;
                }

                if (ev % 5 == 0) yield return null; // keep editor responsive
            }
        }

        // ── Write output ──────────────────────────────────────────────────
        NPCSelector.ResetWeights();

        string dir = Path.Combine(Application.persistentDataPath, "Telemetry");
        Directory.CreateDirectory(dir);
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string path  = Path.Combine(dir, $"AblationResults_{stamp}.csv");
        File.WriteAllText(path, csv.ToString());

        Debug.Log($"[Ablation] CSV written to: {path}");

        // ── Summary table (per event type) ────────────────────────────────
        var sb = new StringBuilder();
        foreach (var eventType in EventTypesTested)
        {
            sb.AppendLine("════════════════════════════════════════════════");
            sb.AppendLine($"  Ablation Summary — {eventType}  (n = {eventCount} events)");
            sb.AppendLine("════════════════════════════════════════════════");
            sb.AppendLine("Mode         | Guard | Roamer | Leader");
            sb.AppendLine("-------------+-------+--------+--------");
            foreach (var ab in ablations)
            {
                var counts = picksPerTypeMode[eventType.ToString()][ab.name];
                int g = counts.TryGetValue("Guard",  out var gg) ? gg : 0;
                int r = counts.TryGetValue("Roamer", out var rr) ? rr : 0;
                int l = counts.TryGetValue("Leader", out var ll) ? ll : 0;
                sb.AppendLine($"{ab.name,-12} | {g,5} | {r,6} | {l,6}");
            }
        }
        sb.AppendLine("════════════════════════════════════════════════");
        sb.AppendLine("If Full and NoX show similar distributions, term X is not");
        sb.AppendLine("strongly influencing selection. Big differences = term matters.");
        Debug.Log(sb.ToString());

        // ── Cleanup ───────────────────────────────────────────────────────
        foreach (var go in spawned) if (go != null) Destroy(go);
        Squad.ClearAll();
    }

    // ── helpers ───────────────────────────────────────────────────────────

    void EnsureSingletons()
    {
        if (EventManager.Instance == null)
        {
            var go = new GameObject("[Ablation] EventManager");
            go.AddComponent<EventManager>().logEvents = false;
        }
        if (AlertPropagator.Instance == null)
        {
            var go = new GameObject("[Ablation] AlertPropagator");
            go.AddComponent<AlertPropagator>();
        }
        if (TelemetryLogger.Instance == null)
        {
            var go = new GameObject("[Ablation] TelemetryLogger");
            var tl = go.AddComponent<TelemetryLogger>();
            tl.echoToConsole = false;
        }
    }

    TerroristController SpawnTerrorist(string id, Vector3 pos, NPCRole role)
    {
        var go = new GameObject(id);
        go.transform.position = pos;
        var t = go.AddComponent<TerroristController>();
        t.role             = role;
        t.idleMode         = IdleMode.Static;
        t.responseCooldown = 0f;
        t.hearingRange     = 1000f;
        return t;
    }

    float RandomFloat(System.Random rng, float min, float max)
        => min + (float)rng.NextDouble() * (max - min);
}
