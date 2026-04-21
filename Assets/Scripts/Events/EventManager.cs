using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central event bus for the scenario system.
///
/// Flow per Raise() call:
///   1. Log the raw event to the console (if enabled).
///   2. Route to NPCs — two modes:
///        Broadcast events  → every eligible NPC reacts (GunshotHeard, TerroristDown, etc.)
///        Targeted events   → NPCSelector picks the single best responder
///   3. Derive secondary events:
///        ShotFired  → GunshotHeard  (always)
///        ShotFired  → StressSpike   (if 3+ shots within 2 s)
///
/// Broadcast events (all eligible NPCs in range react simultaneously):
///   GunshotHeard, TerroristDown, RoomCleared, StressSpike
///
/// Targeted events (NPCSelector picks one winner):
///   Everything else
///
/// Attach to the ScenarioManager GameObject in the scene.
/// </summary>
public class EventManager : MonoBehaviour
{
    public static EventManager Instance { get; private set; }

    [Header("Debug")]
    [Tooltip("Print every event, candidate scores, and selection result to the Console.")]
    public bool logEvents = true;

    [Header("Stress Spike Detection")]
    [Tooltip("How many ShotFired events within the window trigger a StressSpike.")]
    public int   spikeShotCount  = 3;
    [Tooltip("Time window in seconds for the spike counter.")]
    public float spikeShotWindow = 2f;

    // Event types that go to ALL eligible NPCs rather than just the best one.
    static readonly HashSet<ScenarioEventType> BroadcastTypes = new HashSet<ScenarioEventType>
    {
        ScenarioEventType.GunshotHeard,   // every NPC in hearing range reacts
        ScenarioEventType.TerroristDown,  // squad/nearby NPCs respond via AlertPropagator
        ScenarioEventType.RoomCleared,    // all NPCs in room can de-escalate
        ScenarioEventType.StressSpike,    // all hostages escalate to Panic
    };

    // ── Singleton ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Core ──────────────────────────────────────────────────────────────────

    public void Raise(ScenarioEvent e)
    {
        if (logEvents)
            Debug.Log($"[EventManager] {e}");

        RouteToNPCs(e);

        // Derived events
        if (e.Type == ScenarioEventType.ShotFired)
        {
            Raise(new ScenarioEvent(ScenarioEventType.GunshotHeard, e.Origin, e.Instigator,
                                    e.RoomId, e.TargetActorId));
            TrackShotAndCheckSpike(e);
        }
    }

    // ── NPC routing ───────────────────────────────────────────────────────────

    void RouteToNPCs(ScenarioEvent e)
    {
        var registry = NPCRegistry.GetAll();
        if (registry.Count == 0) return;

        if (BroadcastTypes.Contains(e.Type))
        {
            // Deliver to every NPC that can respond — no selection scoring needed.
            var responders = new List<INPCResponder>(registry.Count);
            foreach (var npc in registry)
            {
                if (npc.CanRespond(e))
                {
                    npc.RespondTo(e);
                    responders.Add(npc);
                }
            }

            if (logEvents && responders.Count == 0)
                Debug.Log($"[EventManager] Broadcast {e.Type} → 0 / {registry.Count} responded");

            // GunshotHeard: best-scored TERRORIST walks to investigate (never a hostage)
            if (e.Type == ScenarioEventType.GunshotHeard && responders.Count > 0)
            {
                var terrorists = responders.FindAll(r => r is TerroristController);
                if (terrorists.Count == 0) return;
                var investigator = NPCSelector.SelectBest(terrorists, e, false);
                if (investigator is TerroristController tc)
                {
                    tc.InvestigatePosition(e.Origin);
                    if (logEvents)
                        Debug.Log($"[EventManager] GunshotHeard investigator → {tc.NPCId}");
                }
            }
        }
        else
        {
            // Targeted: collect candidates → score → route to winner.
            var candidates = new List<INPCResponder>(registry.Count);
            foreach (var npc in registry)
                if (npc.CanRespond(e))
                    candidates.Add(npc);

            if (candidates.Count > 0)
            {
                var selected = NPCSelector.SelectBest(candidates, e, logEvents);
                selected?.RespondTo(e);
            }
            else if (logEvents)
            {
                Debug.Log($"[EventManager] {e.Type} (E{e.EventId}) → 0 / {registry.Count} eligible");
            }
        }
    }

    // ── StressSpike detection ─────────────────────────────────────────────────

    readonly Queue<float> _recentShots = new Queue<float>();

    void TrackShotAndCheckSpike(ScenarioEvent shotEvent)
    {
        float now = shotEvent.Timestamp;
        _recentShots.Enqueue(now);

        while (_recentShots.Count > 0 && now - _recentShots.Peek() > spikeShotWindow)
            _recentShots.Dequeue();

        if (_recentShots.Count >= spikeShotCount)
        {
            _recentShots.Clear();
            Raise(new ScenarioEvent(ScenarioEventType.StressSpike, shotEvent.Origin, shotEvent.Instigator));
        }
    }

    // ── Scenario lifecycle ────────────────────────────────────────────────────

    /// <summary>
    /// Call this after Module 1 finishes baking the NavMesh and spawning all NPCs.
    /// Raises ScenarioReady so PerceptionControllers and other late-init components
    /// know it is safe to start.
    /// </summary>
    public void NotifyScenarioReady()
    {
        Raise(new ScenarioEvent(ScenarioEventType.ScenarioReady, Vector3.zero));
        Debug.Log("[EventManager] ScenarioReady raised — simulation is live.");
    }
}
