/// <summary>
/// Finite states for a terrorist NPC.
/// The state influences both NPC behaviour and NPCSelector scoring:
/// lower-energy states (Idle, Suspicious) score higher so they are preferred
/// over already-engaged NPCs when selecting a responder.
/// </summary>
public enum TerroristState
{
    Idle,        // Patrolling normally — most responsive to new events
    Suspicious,  // Heard something — stopped, looking toward sound origin
    Alert,       // Confirmed threat nearby — holding position, scanning
    Engage,      // Actively firing — blocks further event responses
    TakeCover,   // Moving to / sheltering at a CoverPoint
    Down,        // Terminal — health reached 0; no further responses
}
