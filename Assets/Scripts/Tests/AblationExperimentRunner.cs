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
///   2. Generates M random GunshotHeard events at random positions.
///   3. For EACH event, runs NPCSelector.SelectBest FOUR times — once with each
///      ablation configuration:
///        • Full       — all four scoring terms enabled (baseline)
///        • NoLOS      — perception term disabled
///        • NoDistance — distance term disabled
///        • NoRole     — role-bonus term disabled
///   4. Records every decision to a CSV file in Application.persistentDataPath/Telemetry/
///   5. Prints a summary table to the Console.
///
/// What you do with the CSV:
///   • Compare WHICH NPC each ablation picked for the same event — if Full and
///     NoLOS pick the same NPC 99% of the time, LOS is doing nothing; if they
///     disagree 30% of the time, LOS materially shifts selection.
///   • Open in Excel or run the included Python snippet to get summary stats
///     for the report.
///
/// Setup (same as Module2TestRunner):
///   1. Open any scene (e.g. SampleScene or a fresh empty one).
///   2. Create Empty → add this component.
///   3. Press Play.
///   4. Right-click the component → "EXP → Run Ablation Experiment".
///   5. Console will print the output file path when done.
///
/// Defaults: 8 NPCs × 20 events × 4 modes = 80 decision rows.
/// </summary>
public class AblationExperimentRunner : MonoBehaviour
{
    [Header("Experiment parameters")]
    [Tooltip("Number of terrorist NPCs to spawn.")]
    public int  npcCount = 8;

    [Tooltip("Number of random gunshot events to evaluate.")]
    public int  eventCount = 20;

    [Tooltip("Side length of the square arena (NPCs spawn within ±arenaSize/2).")]
    public float arenaSize = 30f;

    [Tooltip("Seed for reproducible NPC placement and event positions.")]
    public int seed = 42;

    [Tooltip("If true, places one Cube wall at the centre to create LOS occlusion.")]
    public bool addCentreWall = true;

    [ContextMenu("EXP → Run Ablation Experiment")]
    void RunMenu() => StartCoroutine(RunExperiment());

    IEnumerator RunExperiment()
    {
        EnsureSingletons();
        Squad.ClearAll();
        NPCSelector.ResetWeights();

        Debug.Log($"[Ablation] Starting: {npcCount} NPCs × {eventCount} events × 4 ablations");

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

        // ── Run experiment ────────────────────────────────────────────────
        var csv = new StringBuilder();
        csv.AppendLine("event_id,ablation_mode,event_x,event_z,selected_npc,selected_role,distance_m,had_los,state_score");

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

        // Counters for the summary
        var picksPerMode = new Dictionary<string, Dictionary<string, int>>(); // mode → role → count
        foreach (var ab in ablations) picksPerMode[ab.name] = new Dictionary<string, int>();

        for (int ev = 0; ev < eventCount; ev++)
        {
            float ex = RandomFloat(rng, -arenaSize / 2f, arenaSize / 2f);
            float ez = RandomFloat(rng, -arenaSize / 2f, arenaSize / 2f);
            var origin = new Vector3(ex, 0f, ez);
            var e = new ScenarioEvent(ScenarioEventType.GunshotHeard, origin);

            foreach (var ab in ablations)
            {
                ab.apply();
                var winner = NPCSelector.SelectBest(allCandidates, e, log: false);
                if (winner == null)
                {
                    csv.AppendLine($"{ev},{ab.name},{ex:F2},{ez:F2},NONE,-,-,-,-");
                    continue;
                }

                float distance = Vector3.Distance(winner.Position, origin);
                bool  hasLos   = !Physics.Raycast(
                    winner.Position + Vector3.up * 1.5f,
                    (origin + Vector3.up * 1.5f - (winner.Position + Vector3.up * 1.5f)).normalized,
                    distance,
                    NPCSelector.LosObstacleLayers,
                    QueryTriggerInteraction.Ignore);

                csv.AppendLine($"{ev},{ab.name},{ex:F2},{ez:F2},{winner.NPCId},{winner.Role},{distance:F2},{(hasLos ? 1 : 0)},{winner.StateScore:F2}");

                // tally
                string roleKey = winner.Role.ToString();
                if (!picksPerMode[ab.name].ContainsKey(roleKey)) picksPerMode[ab.name][roleKey] = 0;
                picksPerMode[ab.name][roleKey]++;
            }

            if (ev % 5 == 0) yield return null; // keep editor responsive
        }

        // ── Write output ──────────────────────────────────────────────────
        NPCSelector.ResetWeights();

        string dir = Path.Combine(Application.persistentDataPath, "Telemetry");
        Directory.CreateDirectory(dir);
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string path  = Path.Combine(dir, $"AblationResults_{stamp}.csv");
        File.WriteAllText(path, csv.ToString());

        Debug.Log($"[Ablation] CSV written to: {path}");

        // ── Summary table ─────────────────────────────────────────────────
        var sb = new StringBuilder();
        sb.AppendLine("════════════════════════════════════════════════");
        sb.AppendLine($"  Ablation Summary  (n = {eventCount} events)");
        sb.AppendLine("════════════════════════════════════════════════");
        sb.AppendLine("Mode         | Guard | Roamer | Leader");
        sb.AppendLine("-------------+-------+--------+--------");
        foreach (var ab in ablations)
        {
            var counts = picksPerMode[ab.name];
            int g = counts.TryGetValue("Guard",  out var gg) ? gg : 0;
            int r = counts.TryGetValue("Roamer", out var rr) ? rr : 0;
            int l = counts.TryGetValue("Leader", out var ll) ? ll : 0;
            sb.AppendLine($"{ab.name,-12} | {g,5} | {r,6} | {l,6}");
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
