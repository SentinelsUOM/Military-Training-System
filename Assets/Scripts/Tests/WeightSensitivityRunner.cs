using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Sensitivity / generalization sweep for NPCSelector's four scoring weights.
///
/// Unlike AblationExperimentRunner (which only tests each term fully ON vs. fully
/// OFF), this sweeps each weight across a range of values — 0, 0.5, 1, 1.5, 2, 3, 4
/// — one weight at a time, holding the other three at their project defaults, and
/// measures how much the selection changes relative to the all-defaults baseline.
/// This answers "is 2.0/1.0/1.0/1.0 a fragile magic number, or is the system robust
/// across a reasonable range around it?" — which the binary ablation study cannot.
///
/// It also varies the "world" the sweep runs in — 3 event types (GunshotHeard,
/// RoomBreached, AllyDownSeen, i.e. the three rows of NPCSelector's role-bonus
/// table) x 3 random seeds (independent NPC layouts) — so the result is not
/// reported for one lucky/unlucky spatial arrangement or one event type alone.
///
/// Design (the "lighter" tier — see MODULE2_ABLATION_STUDY_REPORT.md):
///   3 event types x 3 seeds = 9 independent worlds
///   x 4 weights x 7 sweep values x 100 events per point
///   = 25,200 selection calls total.
///
/// Setup: same as AblationExperimentRunner — Create Empty in Play mode, add this
/// component, right-click -> "EXP -> Run Weight Sensitivity Sweep".
/// </summary>
public class WeightSensitivityRunner : MonoBehaviour
{
    [Header("Experiment parameters")]
    [Tooltip("Number of terrorist NPCs per world.")]
    public int npcCount = 8;

    [Tooltip("Number of gunshot-style events per (weight, value) data point.")]
    public int eventsPerPoint = 100;

    [Tooltip("Side length of the square arena.")]
    public float arenaSize = 30f;

    [Tooltip("Independent random-layout replicates per event type.")]
    public int[] seeds = { 42, 43, 44 };

    [Tooltip("If true, places one Cube wall at the centre to create LOS occlusion.")]
    public bool addCentreWall = true;

    static readonly float[] SweepValues = { 0f, 0.5f, 1f, 1.5f, 2f, 3f, 4f };

    static readonly ScenarioEventType[] EventTypes =
    {
        ScenarioEventType.GunshotHeard,   // role bonus favors Roamer
        ScenarioEventType.RoomBreached,   // role bonus favors Guard
        ScenarioEventType.AllyDownSeen,   // role bonus favors Leader
    };

    static readonly (string name, System.Action<float> setter)[] Weights =
    {
        ("Distance", v => { NPCSelector.ResetWeights(); NPCSelector.DistanceWeight = v; }),
        ("Role",     v => { NPCSelector.ResetWeights(); NPCSelector.RoleWeight     = v; }),
        ("State",    v => { NPCSelector.ResetWeights(); NPCSelector.StateWeight    = v; }),
        ("LOS",      v => { NPCSelector.ResetWeights(); NPCSelector.LosWeight      = v; }),
    };

    [ContextMenu("EXP → Run Weight Sensitivity Sweep")]
    void RunMenu() => StartCoroutine(RunExperiment());

    IEnumerator RunExperiment()
    {
        EnsureSingletons();

        Debug.Log($"[WeightSensitivity] Starting: {EventTypes.Length} event types x " +
                  $"{seeds.Length} seeds x {Weights.Length} weights x {SweepValues.Length} values x " +
                  $"{eventsPerPoint} events = " +
                  $"{EventTypes.Length * seeds.Length * Weights.Length * SweepValues.Length * eventsPerPoint} calls");

        var csv = new StringBuilder();
        csv.AppendLine("event_type,seed,swept_weight,weight_value,event_id,selected_npc,selected_role," +
                        "distance_m,had_los,state_score,agree_with_baseline");

        var stateCycle = new[]
        {
            TerroristState.Idle, TerroristState.Suspicious, TerroristState.Alert,
            TerroristState.TakeCover, TerroristState.Retreat,
        };

        foreach (var evType in EventTypes)
        {
            foreach (var seed in seeds)
            {
                Squad.ClearAll();
                var rng     = new System.Random(seed);
                var npcs    = new List<TerroristController>();
                var spawned = new List<GameObject>();

                for (int i = 0; i < npcCount; i++)
                {
                    float x = RandomFloat(rng, -arenaSize / 2f, arenaSize / 2f);
                    float z = RandomFloat(rng, -arenaSize / 2f, arenaSize / 2f);
                    var role = (NPCRole)(i % 3);
                    var t = SpawnTerrorist($"WSNPC_{i:D2}", new Vector3(x, 0f, z), role);
                    t.currentState = stateCycle[i % stateCycle.Length];
                    npcs.Add(t);
                    spawned.Add(t.gameObject);
                }

                if (addCentreWall)
                {
                    var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    wall.name = "[WS] CentreWall";
                    wall.transform.position   = new Vector3(0f, 1f, 0f);
                    wall.transform.localScale = new Vector3(10f, 3f, 1f);
                    spawned.Add(wall);
                }

                yield return null;
                yield return null; // let Awake/registration settle

                // Fixed set of events for this world — SHARED across every sweep point below,
                // so a difference in outcome is attributable to the weight, not to different events.
                var events = new List<ScenarioEvent>(eventsPerPoint);
                for (int e = 0; e < eventsPerPoint; e++)
                {
                    float ex = RandomFloat(rng, -arenaSize / 2f, arenaSize / 2f);
                    float ez = RandomFloat(rng, -arenaSize / 2f, arenaSize / 2f);
                    events.Add(new ScenarioEvent(evType, new Vector3(ex, 0f, ez)));
                }

                var allCandidates = new List<INPCResponder>(npcs.Count);
                foreach (var n in npcs) allCandidates.Add(n);

                // Baseline: all four weights at the project's shipped defaults.
                NPCSelector.ResetWeights();
                var baseline = new string[events.Count];
                for (int e = 0; e < events.Count; e++)
                {
                    var w = NPCSelector.SelectBest(allCandidates, events[e], log: false);
                    baseline[e] = w != null ? w.NPCId : "NONE";
                }

                foreach (var weight in Weights)
                {
                    foreach (var val in SweepValues)
                    {
                        weight.setter(val);

                        for (int e = 0; e < events.Count; e++)
                        {
                            var ev     = events[e];
                            var winner = NPCSelector.SelectBest(allCandidates, ev, log: false);

                            string npcId = winner != null ? winner.NPCId : "NONE";
                            string role  = winner != null ? winner.Role.ToString() : "-";
                            float  dist  = winner != null ? Vector3.Distance(winner.Position, ev.Origin) : -1f;
                            float  state = winner != null ? winner.StateScore : -1f;
                            bool   hasLos = false;
                            if (winner != null)
                            {
                                Vector3 from = winner.Position + Vector3.up * 1.5f;
                                Vector3 to   = ev.Origin        + Vector3.up * 1.5f;
                                hasLos = !Physics.Raycast(from, (to - from).normalized, dist,
                                                          NPCSelector.LosObstacleLayers,
                                                          QueryTriggerInteraction.Ignore);
                            }
                            bool agree = npcId == baseline[e];

                            csv.AppendLine($"{evType},{seed},{weight.name},{val:F1},{e},{npcId},{role}," +
                                            $"{dist:F2},{(hasLos ? 1 : 0)},{state:F2},{(agree ? 1 : 0)}");
                        }
                        yield return null; // keep editor responsive between sweep points
                    }
                }

                NPCSelector.ResetWeights();
                foreach (var go in spawned) if (go != null) Destroy(go);
                Squad.ClearAll();
            }
        }

        string dir = Path.Combine(Application.persistentDataPath, "Telemetry");
        Directory.CreateDirectory(dir);
        string stamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string path  = Path.Combine(dir, $"WeightSensitivity_{stamp}.csv");
        File.WriteAllText(path, csv.ToString());

        Debug.Log($"[WeightSensitivity] CSV written to: {path}");
    }

    // ── helpers (mirrors AblationExperimentRunner) ────────────────────────────

    void EnsureSingletons()
    {
        if (EventManager.Instance == null)
        {
            var go = new GameObject("[WS] EventManager");
            go.AddComponent<EventManager>().logEvents = false;
        }
        if (AlertPropagator.Instance == null)
        {
            var go = new GameObject("[WS] AlertPropagator");
            go.AddComponent<AlertPropagator>();
        }
        if (TelemetryLogger.Instance == null)
        {
            var go = new GameObject("[WS] TelemetryLogger");
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
