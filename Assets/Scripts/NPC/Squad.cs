using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

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

    // ── Coordinated directional search ────────────────────────────────────────

    // Minimum gap between fan-outs so several members reporting "empty" at once don't thrash the
    // whole squad's destinations. One call already re-tasks everyone.
    const float FanOutCooldownSeconds = 4f;
    // How long a reported escape direction stays the squad's shared search bias.
    const float EscapeContextTTL = 12f;

    // Forward-sweep formation.
    const float LeadBase       = 5f;   // how far along the escape line the near searcher goes
    const float LeadPerLane    = 1.5f; // extra depth per lateral lane, so it reads as a cone, not a wall
    const float LateralStep    = 3.5f; // sideways gap between adjacent searchers across the escape line
    const float DoubleBackLead = 6f;   // how far the plank-checker goes the OPPOSITE way (later waves only)
    const float WidenPerWave   = 3f;   // each empty wave pushes the sweep this much farther out
    const float RadialDistance = 6f;   // fallback spread radius when no escape direction is known

    float   _nextFanOutAllowedAt = -999f;
    Vector3 _sharedFocus;
    Vector3 _sharedEscapeDir;
    float   _sharedContextAt = -999f;
    int     _searchWave;               // 0 = first forward sweep; grows each time a sweep comes up empty

    /// <summary>
    /// Reported by whoever last had eyes on the trainee as they broke line of sight. Gives the WHOLE
    /// squad a shared "they went THAT way" — so a fan-out triggered by a member who only heard the
    /// gunfire still pushes in the right direction instead of guessing. Resets the search to wave 0
    /// (a fresh forward chase) and clears the cooldown so the chase starts immediately.
    /// </summary>
    public void SetEscapeContext(Vector3 lastPos, Vector3 escapeDir)
    {
        _sharedFocus = lastPos;
        escapeDir.y  = 0f;
        _sharedEscapeDir = escapeDir.sqrMagnitude > 0.01f ? escapeDir.normalized : Vector3.zero;
        _sharedContextAt = Time.time;
        _searchWave      = 0;
        _nextFanOutAllowedAt = Time.time; // allow an immediate fan-out on the fresh contact-lost
    }

    /// <summary>Kept for the gunshot-investigation call site. A gunshot gives a POINT but no travel
    /// direction, so this fans the squad out radially around it.</summary>
    public void EscalateInvestigation(Vector3 area, TerroristController requester)
        => FanOutSearch(area, Vector3.zero, requester);

    /// <summary>
    /// Send the squad to HUNT the trainee, spread out. When an escape direction is known (someone
    /// saw which way they ran) the searchers sweep FORWARD along it, line-abreast, covering the
    /// width of the escape — this is the whole point: they chase where you went, not where you were.
    /// Each empty sweep widens the net (WidenPerWave) and, from the second wave on, peels ONE
    /// searcher off to check the DOUBLING-BACK in case the trainee planked — matching "chase first,
    /// check the opposite a little only if I'm not found". With no direction at all (a blind
    /// gunshot) they spread radially around the point.
    ///
    /// The HOSTAGE GUARDIAN is never included. Engaged members are left to fight. Convergence is
    /// unchanged: the first to get LOS re-engages and pulls the others onto the contact.
    /// </summary>
    public void FanOutSearch(Vector3 focus, Vector3 escapeDir, TerroristController caller)
    {
        if (Time.time < _nextFanOutAllowedAt) return;

        var searchers = new List<TerroristController>();
        foreach (var m in _members)
        {
            if (m == null)                               continue;
            if (m.isHostageGuardian)                     continue; // NEVER leaves the hostage room
            if (m.currentState == TerroristState.Down)   continue;
            if (m.currentState == TerroristState.Engage) continue; // already in the fight
            searchers.Add(m);
        }
        if (searchers.Count == 0) return;

        // Prefer the squad-shared escape context (the freshest sighting) over a caller with no
        // heading of its own — otherwise a squadmate who only HEARD the shots would search blind.
        escapeDir.y = 0f;
        Vector3 dir = escapeDir.sqrMagnitude > 0.01f ? escapeDir.normalized : Vector3.zero;
        if (dir == Vector3.zero && Time.time - _sharedContextAt < EscapeContextTTL &&
            _sharedEscapeDir != Vector3.zero)
        {
            dir   = _sharedEscapeDir;
            focus = _sharedFocus;
        }

        bool    haveDir = dir != Vector3.zero;
        Vector3 perp    = haveDir ? Vector3.Cross(Vector3.up, dir).normalized : Vector3.right;
        float   widen   = _searchWave * WidenPerWave;

        // Nearest searcher to the focus takes the direct line; the rest fan out around them.
        searchers.Sort((a, b) =>
            (a.transform.position - focus).sqrMagnitude.CompareTo((b.transform.position - focus).sqrMagnitude));

        for (int i = 0; i < searchers.Count; i++)
        {
            Vector3 desired;
            if (haveDir)
            {
                bool plankChecker = _searchWave >= 1 && searchers.Count >= 3 && i == searchers.Count - 1;
                if (plankChecker)
                {
                    // From the second wave on, ONE searcher covers the trainee doubling back.
                    desired = focus - dir * (DoubleBackLead + widen);
                }
                else
                {
                    float lat  = LaneLateral(i) * LateralStep;
                    float lead = LeadBase + widen + Mathf.Abs(LaneLateral(i)) * LeadPerLane;
                    desired = focus + dir * lead + perp * lat;
                }
            }
            else
            {
                float ang = (360f / searchers.Count) * i * Mathf.Deg2Rad;
                desired = focus + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (RadialDistance + widen);
            }

            searchers[i].DispatchToInvestigate(SampleReachable(desired, focus));
        }

        _searchWave++;
        _nextFanOutAllowedAt = Time.time + FanOutCooldownSeconds;
    }

    // 0, +1, -1, +2, -2, … — spreads searchers alternately to either side of the escape line.
    static int LaneLateral(int lane)
    {
        if (lane == 0) return 0;
        int mag = (lane + 1) / 2;
        return (lane % 2 == 1) ? mag : -mag;
    }

    /// <summary>Nearest navigable point to <paramref name="desired"/>; if that spot is off the mesh
    /// (through a wall / outside the building) it pulls back toward the focus so the searcher still
    /// gets a reachable destination instead of freezing and staring at a wall.</summary>
    static Vector3 SampleReachable(Vector3 desired, Vector3 focus)
    {
        if (NavMesh.SamplePosition(desired, out var h, 6f, NavMesh.AllAreas)) return h.position;
        Vector3 mid = Vector3.Lerp(desired, focus, 0.5f);
        if (NavMesh.SamplePosition(mid, out var h2, 8f, NavMesh.AllAreas)) return h2.position;
        if (NavMesh.SamplePosition(focus, out var h3, 8f, NavMesh.AllAreas)) return h3.position;
        return focus;
    }
}
