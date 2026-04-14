using System.Collections.Generic;

/// <summary>
/// Runtime squad record — created at scenario load time from the Scenario JSON
/// role assignments (Module 1 output).
///
/// Usage (called by TerroristController.Awake once squadId is set):
///   Squad.GetOrCreate("alpha").AddMember(this);
///
/// When a member goes Down, TerroristController calls NotifyMemberDown() so the
/// squad can drive alert propagation via AlertPropagator.
/// </summary>
public class Squad
{
    public readonly string SquadId;

    readonly List<TerroristController> _members = new List<TerroristController>();

    // ── Static registry ───────────────────────────────────────────────────────

    static readonly Dictionary<string, Squad> _all = new Dictionary<string, Squad>();

    /// Returns the existing squad or creates a new one.
    public static Squad GetOrCreate(string id)
    {
        if (!_all.TryGetValue(id, out var squad))
        {
            squad = new Squad(id);
            _all[id] = squad;
        }
        return squad;
    }

    /// Returns an existing squad, or null if none with that id exists yet.
    public static Squad Get(string id)
    {
        _all.TryGetValue(id, out var squad);
        return squad;
    }

    /// Remove all squads — call at scenario reset / scene unload.
    public static void ClearAll() => _all.Clear();

    // ── Instance ──────────────────────────────────────────────────────────────

    Squad(string id) { SquadId = id; }

    public void AddMember(TerroristController t)
    {
        if (!_members.Contains(t))
            _members.Add(t);
    }

    public void RemoveMember(TerroristController t) => _members.Remove(t);

    public IReadOnlyList<TerroristController> Members => _members;

    /// <summary>
    /// Called when a member's health reaches 0.
    /// Delegates alert propagation to AlertPropagator (Ring 1 squad broadcast).
    /// </summary>
    public void NotifyMemberDown(TerroristController downed, ScenarioEvent trigger)
    {
        AlertPropagator.Instance?.HandleMemberDown(this, downed, trigger);
    }
}
