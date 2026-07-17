using System.Collections.Generic;
using UnityEngine;

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

    // ── Leader directives (squad coordination) ────────────────────────────────

    /// Last directive issued to this squad by its Leader. Null if no directive active.
    public LeaderDirective CurrentDirective { get; private set; }

    /// <summary>
    /// Broadcast a directive to every non-engaged, non-down squad member.
    /// Only callable by a Leader-role member of THIS squad (silently ignored otherwise).
    /// Logged to TelemetryLogger so Module 4 can replay it on the AAR timeline.
    /// </summary>
    public void IssueDirective(LeaderDirective directive)
    {
        if (directive == null || directive.Source == null) return;
        if (directive.Source.role != NPCRole.Leader)      return;
        if (!_members.Contains(directive.Source))         return; // not our leader

        CurrentDirective = directive;
        TelemetryLogger.Instance?.LogDirective(SquadId, directive);

        foreach (var member in _members)
        {
            if (member == null || member == directive.Source) continue;
            if (member.currentState == TerroristState.Down)   continue;
            if (member.currentState == TerroristState.Engage) continue; // committed
            member.OnDirective(directive);
        }
    }

    /// Clear the active directive (call at scenario reset or when the leader dies).
    public void ClearDirective() => CurrentDirective = null;

    // ── Coordinated fan-out search ────────────────────────────────────────────

    // Rooms swept recently, keyed by a quantised centre, so successive fan-outs rotate to
    // FRESH rooms instead of re-checking the one just cleared. Entries older than this decay.
    const float RoomFreshnessSeconds = 15f;
    // Minimum gap between fan-outs, so several members reporting "empty" at once don't thrash
    // the whole squad's destinations. One call already re-tasks everyone.
    const float FanOutCooldownSeconds = 5f;

    readonly Dictionary<long, float> _roomSweptAt = new Dictionary<long, float>();
    float _nextFanOutAllowedAt = -999f;

    static long RoomKey(Vector3 p) => (long)Mathf.Round(p.x) * 100000L + (long)Mathf.Round(p.z);

    /// <summary>
    /// Kept for the existing call site (first investigator found nothing). Now spreads the squad
    /// OUT rather than piling everyone onto the same point — see <see cref="FanOutSearch"/>.
    /// </summary>
    public void EscalateInvestigation(Vector3 area, TerroristController requester)
        => FanOutSearch(area, requester);

    /// <summary>
    /// Split the squad up to HUNT the trainee: assign each available searcher a DISTINCT room so
    /// they cover ground and cut off escape, instead of everyone converging on one spot. The
    /// candidate rooms are the ones nearest the focus (last-known position) that haven't just been
    /// swept, so a re-split after a lost contact rotates onto fresh rooms.
    ///
    /// The HOSTAGE GUARDIAN is never included — it must never leave the hostage room, for any
    /// reason. Members already Engaging are left to fight; downed members are skipped.
    ///
    /// Convergence is handled elsewhere: the instant any searcher gets LOS it enters Engage and
    /// broadcasts GunshotHeard, which re-tasks the others onto that contact. If that contact is
    /// then lost, an empty sweep calls back here and the squad splits again.
    /// </summary>
    public void FanOutSearch(Vector3 focus, TerroristController caller)
    {
        if (Time.time < _nextFanOutAllowedAt) return;

        var searchers = new List<TerroristController>();
        foreach (var m in _members)
        {
            if (m == null)                                 continue;
            if (m.isHostageGuardian)                       continue; // NEVER leaves the hostage room
            if (m.currentState == TerroristState.Down)     continue;
            if (m.currentState == TerroristState.Engage)   continue; // already in the fight
            searchers.Add(m);
        }
        if (searchers.Count == 0) return;

        var rooms = new List<Vector3>(TerroristController.GetSceneRoomCenters());
        if (rooms.Count == 0)
        {
            // No room data (unbuilt/legacy scene): fall back to the old same-point sweep so the
            // squad still responds rather than freezing.
            foreach (var m in searchers) m.DispatchToInvestigate(focus);
            _nextFanOutAllowedAt = Time.time + FanOutCooldownSeconds;
            return;
        }

        // Rank rooms: freshly-swept rooms sink to the bottom; otherwise nearest-to-focus first.
        rooms.Sort((a, b) => RoomCost(a, focus).CompareTo(RoomCost(b, focus)));

        // Consider a few more rooms than searchers so, with distinct assignment, they genuinely
        // spread rather than all crowding the single closest room.
        int candidateCount = Mathf.Min(rooms.Count, searchers.Count + 2);

        var taken = new bool[candidateCount];
        foreach (var m in searchers)
        {
            // Each searcher claims the nearest UNTAKEN candidate room to its own position — this
            // is what makes them split (distinct rooms) while still each walking the short way.
            int best = -1; float bestD = float.MaxValue;
            for (int i = 0; i < candidateCount; i++)
            {
                if (taken[i]) continue;
                float d = (rooms[i] - m.transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best < 0) best = 0; // more searchers than candidate rooms → double up on the closest

            taken[best] = true;
            _roomSweptAt[RoomKey(rooms[best])] = Time.time;
            m.DispatchToInvestigate(rooms[best]);
        }

        _nextFanOutAllowedAt = Time.time + FanOutCooldownSeconds;
    }

    /// <summary>Sort cost for a room during fan-out: distance to the focus, with a large penalty
    /// for rooms swept within the last few seconds so re-splits move onto fresh ground.</summary>
    float RoomCost(Vector3 room, Vector3 focus)
    {
        float cost = Vector3.Distance(room, focus);
        if (_roomSweptAt.TryGetValue(RoomKey(room), out float t) &&
            Time.time - t < RoomFreshnessSeconds)
            cost += 1000f; // recently searched — try somewhere else first
        return cost;
    }
}
