/// <summary>
/// All scenario event types the system can raise and respond to.
/// Add new types here as the system grows — detectors and responders
/// reference this enum to stay in sync.
/// </summary>
public enum ScenarioEventType
{
    // ── Weapon ────────────────────────────────────────────────────────────────
    ShotFired,           // A weapon was discharged (player or NPC)
    GunshotHeard,        // Derived from ShotFired; broadcast to all NPCs in hearing range

    // ── Spatial ───────────────────────────────────────────────────────────────
    DoorOpened,          // A door transitioned to the open state
    RoomBreached,        // The player entered a new trigger zone / room
    RoomCleared,         // All active threats in a room were eliminated
    TerroristEnteredRoom,// A terrorist NPC entered the hostage's room

    // ── Detection ─────────────────────────────────────────────────────────────
    PlayerSeen,          // NPC first established line of sight to the player
    PlayerLost,          // NPC lost LOS (after 1.5 s confirmation delay)
    TargetConfirmed,     // NPC sustained LOS long enough to commit to Engage

    // ── Combat ────────────────────────────────────────────────────────────────
    TerroristHit,        // A bullet damaged a terrorist (per-impact, fires before TerroristDown)
    TerroristDown,       // Terrorist health reached 0 — broadcast for squad reaction
    AllyDownSeen,        // NPC saw a squad-mate go Down with confirmed LOS

    // ── Hostage ───────────────────────────────────────────────────────────────
    HostageContactStarted, // Trainee entered safe-contact radius of a hostage
    HostageFreed,          // Hostage reached the safe hiding spot

    // ── Escalation ────────────────────────────────────────────────────────────
    StressSpike,         // 3+ ShotFired events within 2 s — escalates hostages to Panic
    DelayExceeded,       // Trainee took too long on an objective
    TimerExpired,        // Background scenario timer ran out
    NoProgressWindow,    // Background: no meaningful action within a time window

    // ── Lifecycle ─────────────────────────────────────────────────────────────
    ScenarioReady,       // NavMesh baked + all NPCs spawned — safe to start simulation

    // NOTE: append new members HERE, at the end — never in the middle.
    // TriggerZoneDetector.eventType is a serialized ScenarioEventType, which Unity
    // stores in scenes/prefabs as an INTEGER ordinal. Inserting a member higher up
    // silently renumbers everything below it and repoints every existing detector at
    // the wrong event.
    HostageHit,          // A bullet damaged a hostage but did NOT kill them (trainee friendly fire)
}
