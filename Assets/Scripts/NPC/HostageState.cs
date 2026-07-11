/// <summary>
/// Finite states for a hostage NPC.
///
/// Calm    — no reaction; idle animation
/// Fearful — scared in place; scared animation via HostageScareController
/// Freeze  — total paralysis from close-range sustained threat (sub-state of fear)
/// Panic   — actively fleeing; full runaway sequence via HostageRunawayController
/// Follow  — player is nearby and the hostage feels safe enough to follow
/// Held    — seized by a guardian as leverage: kneeling at gunpoint / human shield
/// Freed   — terminal: hostage has reached the hiding spot; no further reactions
/// </summary>
public enum HostageState
{
    Calm,
    Fearful,
    Freeze,   // Close-range sustained threat; cannot move or react further
    Panic,
    Follow,
    Held,     // Seized by a guardian terrorist as leverage (kneeling, hands up, used as a shield)
    Freed,
    Down,     // Terminal: hostage was shot dead (training failure)
}
