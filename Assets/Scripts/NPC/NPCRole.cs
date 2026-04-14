/// <summary>
/// Tactical role of an NPC.  Used by NPCSelector to award a role-match bonus
/// when the event type is particularly relevant to this NPC's purpose.
///
/// Guard   — covers doors and choke-points; prioritised for RoomBreached
/// Roamer  — patrols open areas; prioritised for GunshotHeard
/// Leader  — commands squad; prioritised for AllyDownSeen
/// Hostage — civilian; not scored against combat events
/// </summary>
public enum NPCRole
{
    Guard,
    Roamer,
    Leader,
    Hostage,
}

/// <summary>Extension helpers so call-sites can test NPC type without comparing against every role value.</summary>
public static class NPCRoleExtensions
{
    public static bool IsTerrorist(this NPCRole role) => role != NPCRole.Hostage;
    public static bool IsHostage(this NPCRole role)   => role == NPCRole.Hostage;
}
