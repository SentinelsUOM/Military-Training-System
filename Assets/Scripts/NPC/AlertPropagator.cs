using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Implements the 3-ring alert propagation model.
///
/// Ring 1 — Squad broadcast (0.3 s delay, no distance/LOS requirement)
///   When any squad member enters Alert or Engage → all other members transition to Alert
///   if they are currently Idle or Suspicious.
///   Special rule: when the source is entering Down → Ring 1 members with LOS → Engage immediately (no delay).
///
/// Ring 2 — Proximity broadcast (1.0 s delay, within alertPropagationRadius, requires LOS)
///   Non-squad NPCs nearby → transition to Suspicious.
///
/// Ring 3 — No propagation
///   NPCs in separate rooms with no proximity overlap → unaffected.
///
/// Setup: place one instance on the ScenarioManager GameObject alongside EventManager.
/// </summary>
public class AlertPropagator : MonoBehaviour
{
    public static AlertPropagator Instance { get; private set; }

    [Header("Ring 2 Settings")]
    [Tooltip("Radius for non-squad proximity broadcast.")]
    public float alertPropagationRadius = 15f;

    [Tooltip("Layer mask for LOS obstruction check (walls, doors, obstacles).")]
    public LayerMask obstacleLayers;

    // ── Singleton ─────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called by TerroristController.TransitionTo() when entering Alert or Engage.
    /// Triggers Ring 1 (squad) and Ring 2 (proximity) propagation.
    /// </summary>
    public void Broadcast(TerroristController source, ScenarioEvent trigger)
    {
        if (source == null) return;

        // Ring 1 — delayed squad broadcast
        if (!string.IsNullOrEmpty(source.squadId))
            StartCoroutine(Ring1Broadcast(source, trigger, delaySeconds: 0.3f, forceEngage: false));

        // Ring 2 — delayed proximity broadcast
        StartCoroutine(Ring2Broadcast(source, trigger, delaySeconds: 1.0f));
    }

    /// <summary>
    /// Called by Squad.NotifyMemberDown() when a squad member's health reaches 0.
    /// Ring 1 squad members with LOS to the downed NPC → Engage immediately (0 delay).
    /// </summary>
    public void HandleMemberDown(Squad squad, TerroristController downed, ScenarioEvent trigger)
    {
        if (squad == null || downed == null) return;

        // Immediate Ring 1 with LOS check — override to Engage
        StartCoroutine(Ring1Broadcast(downed, trigger, delaySeconds: 0f, forceEngage: true));

        // Ring 2 — nearby non-squad become Suspicious
        StartCoroutine(Ring2Broadcast(downed, trigger, delaySeconds: 1.0f));
    }

    // ── Ring coroutines ───────────────────────────────────────────────────────

    IEnumerator Ring1Broadcast(TerroristController source, ScenarioEvent trigger,
                               float delaySeconds, bool forceEngage)
    {
        if (delaySeconds > 0f) yield return new WaitForSeconds(delaySeconds);

        var squad = Squad.Get(source.squadId);
        if (squad == null) yield break;

        // Build a snapshot so mutations mid-loop don't cause issues
        var members = new List<TerroristController>(squad.Members);

        foreach (var member in members)
        {
            if (member == null || member == source) continue;
            if (member.currentState == TerroristState.Down) continue;

            if (forceEngage)
            {
                // TerroristDown override: only engage if this member has LOS to the downed NPC
                if (HasLOS(member.transform.position, source.transform.position))
                {
                    // Raise AllyDownSeen so the controller's full RespondTo logic runs
                    var allyDownEvent = new ScenarioEvent(
                        ScenarioEventType.AllyDownSeen,
                        source.transform.position,
                        source.gameObject,
                        targetActorId: source.NPCId);

                    member.RespondTo(allyDownEvent);
                }
            }
            else
            {
                // Standard Alert propagation — only escalate Idle/Suspicious
                if (member.currentState == TerroristState.Idle ||
                    member.currentState == TerroristState.Suspicious)
                {
                    var propagated = new ScenarioEvent(
                        ScenarioEventType.AllyDownSeen,
                        source.transform.position,
                        source.gameObject);
                    member.RespondTo(propagated);
                }
            }
        }
    }

    IEnumerator Ring2Broadcast(TerroristController source, ScenarioEvent trigger, float delaySeconds)
    {
        yield return new WaitForSeconds(delaySeconds);

        // Find all registered terrorist NPCs NOT in the same squad as source
        foreach (var responder in NPCRegistry.GetAll())
        {
            var tc = responder as TerroristController;
            if (tc == null || tc == source) continue;
            if (tc.currentState == TerroristState.Down) continue;

            // Skip squad members — Ring 1 handles them
            bool sameSquad = !string.IsNullOrEmpty(source.squadId) &&
                             source.squadId == tc.squadId;
            if (sameSquad) continue;

            float dist = Vector3.Distance(source.transform.position, tc.transform.position);
            if (dist > alertPropagationRadius) continue;

            // LOS check
            if (!HasLOS(source.transform.position, tc.transform.position)) continue;

            // Escalate to Suspicious only
            if (tc.currentState == TerroristState.Idle)
            {
                var propagated = new ScenarioEvent(
                    ScenarioEventType.GunshotHeard,
                    source.transform.position,
                    source.gameObject);

                if (tc.CanRespond(propagated))
                    tc.RespondTo(propagated);
            }
        }
    }

    // ── LOS helper ────────────────────────────────────────────────────────────

    bool HasLOS(Vector3 from, Vector3 to)
    {
        Vector3 dir  = to - from;
        float   dist = dir.magnitude;
        return !Physics.Raycast(from, dir.normalized, dist, obstacleLayers, QueryTriggerInteraction.Ignore);
    }
}
