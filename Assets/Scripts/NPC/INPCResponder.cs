using UnityEngine;

/// <summary>
/// Implemented by any NPC controller that wants to participate in
/// the scenario event system.
///
/// NPCRegistry holds all active responders.
/// NPCSelector scores them against a ScenarioEvent.
/// EventManager calls RespondTo() on the winner.
/// </summary>
public interface INPCResponder
{
    /// Display name used in debug logs — typically gameObject.name.
    string NPCId { get; }

    /// Terrorist or Hostage — used for role-match scoring bonus.
    NPCRole Role { get; }

    /// Current world position — used for distance scoring.
    Vector3 Position { get; }

    /// How ready this NPC is to respond right now (0–20).
    /// Lower-energy states return higher values so idle NPCs are preferred.
    float StateScore { get; }

    /// Time.time of the last RespondTo() call — used for cooldown scoring.
    float LastResponseTime { get; }

    /// Return true if this NPC can meaningfully respond to the given event.
    /// Should account for event type, current state, and cooldown.
    bool CanRespond(ScenarioEvent e);

    /// Execute the response: update state, drive existing NPC components.
    void RespondTo(ScenarioEvent e);
}
