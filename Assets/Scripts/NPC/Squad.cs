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
    /// Delegates alert propagation to AlertPropagator (Ring 1 squad broadcast), then re-elects a
    /// Leader if the one who died was it.
    /// </summary>
    public void NotifyMemberDown(TerroristController downed, ScenarioEvent trigger)
    {
        AlertPropagator.Instance?.HandleMemberDown(this, downed, trigger);
        PromoteNewLeaderIfNeeded(downed);
    }

    /// <summary>
    /// If the member who just died was the squad Leader, promote a surviving member so the squad
    /// keeps coordinating (Converge / Flank). Without this, killing the leader first left the squad
    /// leaderless for the whole rest of the mission — an easy exploit. The hostage guardian is never
    /// eligible (it must never leave the hostage to run directives); a Roamer is preferred, else any
    /// alive member.
    /// </summary>
    void PromoteNewLeaderIfNeeded(TerroristController downed)
    {
        if (downed == null || downed.role != NPCRole.Leader) return;

        // The dead leader's standing order is void.
        ClearDirective();

        // Guard against double-promotion: if a leader somehow still lives, leave it be.
        foreach (var m in _members)
            if (m != null && m != downed && m.role == NPCRole.Leader &&
                m.currentState != TerroristState.Down) return;

        TerroristController pick = null;
        foreach (var m in _members)
        {
            if (m == null || m == downed)            continue;
            if (m.currentState == TerroristState.Down) continue;
            if (m.isHostageGuardian)                 continue; // never pulls off the hostage
            if (pick == null) pick = m;               // first eligible = fallback
            if (m.role == NPCRole.Roamer) { pick = m; break; } // prefer a roamer, like spawn-time election
        }

        if (pick != null)
        {
            Debug.Log($"[Squad {SquadId}] Leader {downed.NPCId} is down — promoting {pick.NPCId} to Leader.");
            pick.AssumeLeadership();
        }
        else
        {
            Debug.Log($"[Squad {SquadId}] Leader {downed.NPCId} down — no eligible successor (only guardian/dead left).");
        }
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
    const float WidenPerWave   = 3f;   // each empty wave pushes the search this much farther out
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
            if (haveDir && _searchWave == 0)
            {
                // WAVE 0 — CHASE. They saw which way you ran, so everyone sweeps FORWARD along the
                // escape line, line-abreast, covering its width. Nobody goes backward yet.
                float lat  = LaneLateral(i) * LateralStep;
                float lead = LeadBase + Mathf.Abs(LaneLateral(i)) * LeadPerLane;
                desired = focus + dir * lead + perp * lat;
            }
            else if (haveDir)
            {
                // WAVE 1+ — DIVIDE. The forward chase didn't find you, so now they split to cover
                // EVERY approach: each searcher takes a DISTINCT bearing fanned around the escape
                // direction (straight on, then the sides, then behind), widening each wave, until
                // someone reacquires or a shot is heard.
                float baseDeg = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                float ang     = (baseDeg + LaneAngle(i)) * Mathf.Deg2Rad;
                Vector3 sectorDir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
                desired = focus + sectorDir * (LeadBase + widen);
            }
            else
            {
                // No escape direction at all (a blind gunshot) — spread radially around the point.
                float ang = (360f / searchers.Count) * i * Mathf.Deg2Rad;
                desired = focus + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (RadialDistance + widen);
            }

            searchers[i].DispatchToInvestigate(SampleReachable(desired, focus));
        }

        _searchWave++;
        _nextFanOutAllowedAt = Time.time + FanOutCooldownSeconds;
    }

    // ── Shared confirmed-contact blackboard + persistent hunt ─────────────────
    //
    // The core doctrine fix (see Literature_Review_Module2.docx §2–4): a confirmed
    // sighting is SHARED, PERSISTENT squad knowledge — not private to the one who saw
    // it — and it triggers a coordinated hunt that only ends on a DELIBERATE GROUP
    // decision, never a per-NPC give-up timer. Previously "confirmed" lived on each
    // TerroristController (_personallyConfirmedPlayer) and every searcher decided,
    // alone, to walk home when its own sweep came up empty — so the mate who never
    // got his own line of sight quit while a comrade was still in contact.

    /// Where the trainee was last CONFIRMED (own eyes of any member). Shared anchor.
    public Vector3 PointLastSeen    { get; private set; }
    /// Horizontal direction the trainee was moving when last seen (for chase bias).
    public Vector3 LastSeenHeading  { get; private set; }
    /// Time.time of the most recent confirmation. Resets the hunt's persistence clock.
    public float   LastConfirmTime  { get; private set; } = -999f;
    /// True while the squad is actively hunting a lost/known contact. No member stands
    /// down while this is set — stand-down is the squad's EndHunt(), taken together.
    public bool    HuntActive       { get; private set; }

    float _huntStartTime = -999f;
    // How long the squad PRESSES a lost contact before collectively standing down.
    // Doctrine gives no exact number for a lost contact (open question in the review),
    // so this is a deliberately LONG, area-based budget for a semi-trained force —
    // not the few-second clock that produced the bug. Re-armed on every fresh sighting.
    const float HuntBudgetSeconds  = 45f;
    // Search radius grows with elapsed hunt time (PLS model: radius ≈ speed × time),
    // capped so searchers don't wander the whole level.
    const float SearchGrowthPerSec = 0.6f;
    const float SearchRadiusMin    = 5f;
    const float SearchRadiusMax    = 18f;
    int _searchTick;                    // rotates individual re-task bearings over time

    /// <summary>
    /// Any member who CONFIRMS the trainee with its own eyes calls this. Writes the
    /// shared anchor, (re)starts the hunt, RESETS the persistence clock and collapses
    /// the search back to a fresh converge (wave 0 + immediate fan-out). This is what
    /// makes a new sighting authoritative for the WHOLE squad and re-converges searchers
    /// who had fanned out.
    /// </summary>
    public void ReportConfirmedContact(Vector3 pos, Vector3 heading, TerroristController reporter)
    {
        PointLastSeen   = pos;
        heading.y       = 0f;
        if (heading.sqrMagnitude > 0.01f) LastSeenHeading = heading.normalized;
        LastConfirmTime = Time.time;
        _huntStartTime  = Time.time;     // fresh sighting re-arms the full persistence budget
        HuntActive      = true;
        // Collapse the radius and re-converge everyone onto the fresh anchor.
        SetEscapeContext(pos, LastSeenHeading);
    }

    /// <summary>
    /// Begin (or refresh) a persistent hunt from a LOST contact — the member had eyes on
    /// the trainee and just lost them. Marks the hunt active and kicks the first fan-out.
    /// </summary>
    public void BeginHunt(Vector3 pos, Vector3 heading, TerroristController caller)
    {
        PointLastSeen = pos;
        heading.y     = 0f;
        if (heading.sqrMagnitude > 0.01f) LastSeenHeading = heading.normalized;
        if (!HuntActive) { HuntActive = true; _huntStartTime = Time.time; }
        LastConfirmTime = Time.time;
        SetEscapeContext(pos, LastSeenHeading);
        FanOutSearch(pos, LastSeenHeading, caller);
    }

    /// <summary>True while the squad should keep pressing the hunt — a long, time-based
    /// (area) budget rather than a few-second clock. Re-armed by every fresh sighting.</summary>
    public bool HuntShouldContinue() =>
        HuntActive && (Time.time - _huntStartTime) < HuntBudgetSeconds && HasEligibleSearcher();

    /// <summary>
    /// A searcher's sweep came up empty. The squad — not the searcher — decides what
    /// happens next: if the hunt budget still holds, RE-TASK that searcher to a fresh,
    /// wider point around the shared anchor (keeping it committed) and return true; if the
    /// area is genuinely searched out / the budget is spent, END the hunt for EVERYONE and
    /// return false so the caller stands down together with the rest of the squad.
    /// </summary>
    public bool NotifySearcherExhausted(TerroristController searcher)
    {
        if (!HuntActive) return false;
        if (!HuntShouldContinue()) { EndHunt("area searched / time budget spent"); return false; }

        // Keep this searcher on the hunt — send it to a fresh sector at a radius that grows
        // with elapsed hunt time (PLS expanding-area model). Individual re-task, so it works
        // even while the squad-wide fan-out is on cooldown (no searcher goes idle mid-hunt).
        if (searcher != null)
        {
            Vector3 p = NextSearchPoint(searcher);
            searcher.DispatchToInvestigate(p);
        }
        return true;
    }

    /// <summary>
    /// The DELIBERATE, squad-wide stand-down. Clears the hunt and sends every non-guardian
    /// member home together (and clears their personal "I saw him" latch), so nobody is left
    /// hunting alone and nobody quits early. This is the ONLY sanctioned way out of a hunt.
    /// </summary>
    public void EndHunt(string reason)
    {
        if (!HuntActive) return;
        HuntActive  = false;             // must clear BEFORE StandDown so members don't re-bounce
        _searchWave = 0;
        Debug.Log($"[Squad {SquadId}] hunt ended ({reason}) — squad standing down together.");
        foreach (var m in _members)
            if (m != null) m.StandDown();
    }

    /// Reset the hunt (scenario reset). Does not command members.
    public void ClearHunt()
    {
        HuntActive = false;
        _searchWave = 0;
        LastConfirmTime = -999f;
        _huntStartTime  = -999f;
    }

    bool HasEligibleSearcher()
    {
        foreach (var m in _members)
        {
            if (m == null || m.isHostageGuardian)          continue;
            if (m.currentState == TerroristState.Down)     continue;
            return true;
        }
        return false;
    }

    /// A fresh point around the shared anchor for one searcher: a distinct bearing per
    /// member, rotated over time, biased along the escape direction, at a time-expanding
    /// radius. Always pulled back onto the NavMesh by SampleReachable.
    Vector3 NextSearchPoint(TerroristController searcher)
    {
        float elapsed = Time.time - _huntStartTime;
        float radius  = Mathf.Clamp(SearchRadiusMin + elapsed * SearchGrowthPerSec,
                                    SearchRadiusMin, SearchRadiusMax);

        int idx = _members.IndexOf(searcher);
        if (idx < 0) idx = 0;
        float ang = (idx * 73f + _searchTick * 37f) * Mathf.Deg2Rad;
        _searchTick++;

        Vector3 dir = new Vector3(Mathf.Sin(ang), 0f, Mathf.Cos(ang));
        if (_sharedEscapeDir != Vector3.zero) dir = (dir + _sharedEscapeDir).normalized;

        return SampleReachable(PointLastSeen + dir * radius, PointLastSeen);
    }

    // 0, +1, -1, +2, -2, … — spreads searchers alternately to either side of the escape line.
    static int LaneLateral(int lane)
    {
        if (lane == 0) return 0;
        int mag = (lane + 1) / 2;
        return (lane % 2 == 1) ? mag : -mag;
    }

    // 0°, +60°, -60°, +120°, -120°, +180° … — DIVIDE bearings fanned around the escape direction,
    // sweeping out to the sides and eventually behind, so the squad covers every approach.
    static float LaneAngle(int lane)
    {
        if (lane == 0) return 0f;
        int mag = (lane + 1) / 2;              // 1,1,2,2,3,3
        float deg = Mathf.Min(mag * 60f, 180f); // 60,120,180 — never past a full reversal
        return (lane % 2 == 1) ? deg : -deg;
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
