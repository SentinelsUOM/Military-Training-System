using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Validation harness for trait-differentiated hostage personality profiles.
///
/// Setup (same pattern as Module2TestRunner — no asmdef or test package needed):
///   1. Open any scene.
///   2. Hierarchy → Create Empty → name it "HostageProfileTestRunner".
///   3. Add Component → "HostageProfileTestRunner".
///   4. Press Play.
///   5. Right-click the component header → "TEST → Run All".
///   6. Console: green "PASS" lines = pass, red "FAIL" = fail; summary at the end.
///
/// WHAT THIS VALIDATES, AND WHAT IT DELIBERATELY DOES NOT
///   These are VERIFICATION tests: they confirm the implementation does what the design
///   specifies. They are NOT evidence that profiles are realistic — the parameters are
///   authored, so "Weak panics more" is true by construction. See HOSTAGE_PROFILE_RESEARCH.md
///   section 8.1 for the verification-vs-discovery split; the genuine (non-circular) findings
///   are about trainee rescue time and mission outcome, which need real play data.
///
/// Because discrimination is STOCHASTIC, the behavioural tests run many trials and assert on
/// RATES with generous tolerances, not on single outcomes. A single run proving nothing is
/// exactly the failure mode these tests are shaped to avoid.
/// </summary>
public class HostageProfileTestRunner : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Trials per behavioural test. Higher = tighter rate estimates, slower run.")]
    public int trials = 400;

    [Tooltip("Frames to wait after raising an event before asserting.")]
    public int settleFrames = 1;

    int _pass, _fail;
    readonly List<string> _failLog = new List<string>();

    [ContextMenu("TEST → Run All")]
    public void RunAll() => StartCoroutine(RunAllRoutine());

    IEnumerator RunAllRoutine()
    {
        _pass = 0; _fail = 0; _failLog.Clear();
        Debug.Log("═══ Hostage Profile validation ═══");

        // ── A. Preset table ───────────────────────────────────────────────────
        Check("Weak discrimination is lowest",
              HostageProfiles.ThreatDiscrimination(HostageProfile.Weak) <
              HostageProfiles.ThreatDiscrimination(HostageProfile.Normal));
        Check("Normal discrimination is between Weak and Brave",
              HostageProfiles.ThreatDiscrimination(HostageProfile.Normal) <
              HostageProfiles.ThreatDiscrimination(HostageProfile.Brave));
        Check("Brave has perfect discrimination (1.0 = never misreads)",
              Mathf.Approximately(HostageProfiles.ThreatDiscrimination(HostageProfile.Brave), 1f));
        Check("Only Brave carries defiance risk",
              HostageProfiles.DefianceChance(HostageProfile.Brave) > 0f &&
              Mathf.Approximately(HostageProfiles.DefianceChance(HostageProfile.Weak),   0f) &&
              Mathf.Approximately(HostageProfiles.DefianceChance(HostageProfile.Normal), 0f));

        // ── B. Parsing (Phase 2 will feed this from the mission form) ────────
        Check("Parse('weak')  → Weak",   HostageProfiles.Parse("weak")   == HostageProfile.Weak);
        Check("Parse('BRAVE') → Brave (case-insensitive)",
              HostageProfiles.Parse("BRAVE") == HostageProfile.Brave);
        Check("Parse(' Normal ') → Normal (trims)",
              HostageProfiles.Parse(" Normal ") == HostageProfile.Normal);

        // ── C. Spawn distribution matches published prevalence ───────────────
        yield return RunSpawnDistributionTest();

        // ── D. The four discrimination gates ─────────────────────────────────
        yield return RunGate1_DistantGunshot();
        yield return RunGate4_RescuerApproach();

        // ── E. Regression guard — thresholds unchanged across profiles ────────
        yield return RunThresholdsAreIdenticalTest();

        // ── Summary ──────────────────────────────────────────────────────────
        Debug.Log($"═══ RESULT: {_pass} passed, {_fail} failed ═══");
        if (_fail > 0)
            foreach (var f in _failLog) Debug.LogError("  ✗ " + f);
        else
            Debug.Log("  ✓ All hostage-profile checks passed.");
    }

    // ── C. Spawn distribution ────────────────────────────────────────────────
    // Published trajectory prevalence, renormalised over the three modelled profiles:
    // Brave 67.7% / Normal 21.4% / Weak 10.9%. Sampling error at n=4000 is ~±1.5pp, so a
    // 5pp tolerance is comfortable without being so loose it would miss a real mistake
    // (e.g. equal thirds, or two profiles swapped).
    IEnumerator RunSpawnDistributionTest()
    {
        const int n = 4000;
        int brave = 0, normal = 0, weak = 0;
        for (int i = 0; i < n; i++)
        {
            switch (HostageProfiles.RandomWeighted())
            {
                case HostageProfile.Brave:  brave++;  break;
                case HostageProfile.Normal: normal++; break;
                default:                    weak++;   break;
            }
        }
        float pB = brave / (float)n, pN = normal / (float)n, pW = weak / (float)n;
        Debug.Log($"  spawn mix over {n}: Brave {pB:P1} · Normal {pN:P1} · Weak {pW:P1}");

        Check($"Brave ≈ 67.7% (got {pB:P1})",  Mathf.Abs(pB - 0.677f) < 0.05f);
        Check($"Normal ≈ 21.4% (got {pN:P1})", Mathf.Abs(pN - 0.214f) < 0.05f);
        Check($"Weak ≈ 10.9% (got {pW:P1})",   Mathf.Abs(pW - 0.109f) < 0.05f);
        Check("Brave is the modal profile (research: resilience is the modal response)",
              brave > normal && brave > weak);
        yield return null;
    }

    // ── D. Gate 1 — a DISTANT gunshot is the non-threat ──────────────────────
    // Brave should always read it correctly (Calm → Fearful).
    // Weak should frequently misread it as point-blank (Calm → Panic).
    IEnumerator RunGate1_DistantGunshot()
    {
        float bravePanicRate  = 0f, weakPanicRate = 0f;

        foreach (var profile in new[] { HostageProfile.Brave, HostageProfile.Weak })
        {
            int panics = 0;
            for (int i = 0; i < trials; i++)
            {
                var h = SpawnHostage($"G1_{profile}_{i}", profile);
                yield return WaitFrames(settleFrames);

                // Fire from well beyond panicDistance (6 m) — a genuinely distant shot.
                RaiseGunshot(h, distance: 20f);
                yield return WaitFrames(settleFrames);

                if (h.currentState == HostageState.Panic) panics++;
                Object.Destroy(h.gameObject);
            }
            float rate = panics / (float)trials;
            if (profile == HostageProfile.Brave) bravePanicRate = rate; else weakPanicRate = rate;
            Debug.Log($"  gate1 {profile}: panicked at a 20 m gunshot {rate:P1} of {trials} trials");
        }

        Check($"Brave NEVER misreads a distant shot (got {bravePanicRate:P1}, expect 0%)",
              Mathf.Approximately(bravePanicRate, 0f));
        Check($"Weak OFTEN misreads a distant shot (got {weakPanicRate:P1}, expect ~65%)",
              weakPanicRate > 0.45f);
        Check("Weak misreads distant shots far more than Brave (the core design claim)",
              weakPanicRate > bravePanicRate + 0.4f);
    }

    // ── D. Gate 4 — the APPROACHING RESCUER is the non-threat ────────────────
    // This is the consequential one: a hostage who misreads the rescuer FREEZES instead of
    // following, which is what makes a Weak hostage genuinely slower to extract.
    IEnumerator RunGate4_RescuerApproach()
    {
        float braveFollow = 0f, weakFollow = 0f;

        foreach (var profile in new[] { HostageProfile.Brave, HostageProfile.Weak })
        {
            int followed = 0;
            for (int i = 0; i < trials; i++)
            {
                var h = SpawnHostage($"G4_{profile}_{i}", profile);
                yield return WaitFrames(settleFrames);

                // Put the hostage in Fearful first (the realistic pre-rescue state), then
                // have the rescuer make contact.
                RaiseGunshot(h, distance: 20f);
                yield return WaitFrames(settleFrames);

                RaiseContact(h);
                yield return WaitFrames(settleFrames);

                if (h.currentState == HostageState.Follow) followed++;
                Object.Destroy(h.gameObject);
            }
            float rate = followed / (float)trials;
            if (profile == HostageProfile.Brave) braveFollow = rate; else weakFollow = rate;
            Debug.Log($"  gate4 {profile}: followed the rescuer on first contact {rate:P1}");
        }

        Check($"Brave ALWAYS follows the rescuer immediately (got {braveFollow:P1})",
              braveFollow > 0.99f);
        Check($"Weak OFTEN fails to follow on first contact (got {weakFollow:P1})",
              weakFollow < 0.55f);
        Check("Weak is measurably slower to extract than Brave — the emergent training effect",
              braveFollow > weakFollow + 0.3f);
    }

    // ── E. Regression guard ──────────────────────────────────────────────────
    // The design's central decision: profiles must NOT change distance thresholds, because
    // per-profile distances cannot be derived from any published dB threshold
    // (HOSTAGE_PROFILE_RESEARCH.md section 3.6). If someone later "tunes" these per profile,
    // this test fails and points at the reason.
    IEnumerator RunThresholdsAreIdenticalTest()
    {
        var w = SpawnHostage("TH_Weak",   HostageProfile.Weak);
        var n = SpawnHostage("TH_Normal", HostageProfile.Normal);
        var b = SpawnHostage("TH_Brave",  HostageProfile.Brave);
        yield return WaitFrames(1);

        Check("panicDistance identical across profiles",
              Mathf.Approximately(w.panicDistance, n.panicDistance) &&
              Mathf.Approximately(n.panicDistance, b.panicDistance));
        Check("freezeRange identical across profiles",
              Mathf.Approximately(w.freezeRange, n.freezeRange) &&
              Mathf.Approximately(n.freezeRange, b.freezeRange));
        Check("freezeThreshold identical across profiles",
              Mathf.Approximately(w.freezeThreshold, n.freezeThreshold) &&
              Mathf.Approximately(n.freezeThreshold, b.freezeThreshold));
        Check("ThreatDiscrimination is the ONLY thing that differs",
              !Mathf.Approximately(w.ThreatDiscrimination, b.ThreatDiscrimination));

        Object.Destroy(w.gameObject); Object.Destroy(n.gameObject); Object.Destroy(b.gameObject);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    HostageController SpawnHostage(string id, HostageProfile profile)
    {
        var go = new GameObject(id);
        go.transform.position = Vector3.zero;
        var h = go.AddComponent<HostageController>();
        h.responseCooldown = 0f;          // no cooldown gating inside a test
        h.assignRandomProfile = false;    // pin the profile under test
        h.profile = profile;
        return h;
    }

    /// Raises a gunshot at the given distance and routes it straight to this hostage,
    /// bypassing EventManager so the test doesn't depend on scene wiring or NPC selection.
    void RaiseGunshot(HostageController h, float distance)
    {
        var e = new ScenarioEvent(
            ScenarioEventType.GunshotHeard,
            h.transform.position + Vector3.right * distance,
            null);
        if (h.CanRespond(e)) h.RespondTo(e);
    }

    /// Raises a rescuer-contact event targeted at this hostage.
    void RaiseContact(HostageController h)
    {
        var e = new ScenarioEvent(
            ScenarioEventType.HostageContactStarted,
            h.transform.position,
            null,
            roomId: null,
            targetActorId: h.NPCId);
        if (h.CanRespond(e)) h.RespondTo(e);
    }

    IEnumerator WaitFrames(int n)
    {
        for (int i = 0; i < n; i++) yield return null;
    }

    void Check(string label, bool condition)
    {
        if (condition) { _pass++; Debug.Log($"<color=green>PASS</color> {label}"); }
        else           { _fail++; _failLog.Add(label); Debug.LogWarning($"<color=red>FAIL</color> {label}"); }
    }
}
