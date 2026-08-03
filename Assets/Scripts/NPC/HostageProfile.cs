using UnityEngine;

/// <summary>
/// Trait-differentiated hostage response profiles.
///
/// WHY THIS EXISTS
///   Without profiles every hostage reacts identically to the same stimulus, which is the
///   one clearly unrealistic simplification left in the hostage model. Real defensive
///   response is trait-differentiated, and two independent findings converge on HOW:
///
///     • Visual  — high "looming cognitive style" produces GENERALIZED freezing (freezing at
///                 anything approaching, including non-threats), vs SELECTIVE freezing in
///                 low-looming individuals. F(1,80)=6.50, p=0.01.
///                 (Frontiers in Psychology, 2016, art. 521)
///     • Auditory — high-anxiety individuals respond with EQUAL LATENCY to a quiet 60 dB and
///                 a loud 85 dB stimulus, while low-anxiety individuals discriminate between
///                 them. (Journal of Research in Personality)
///
///   Shared mechanism: TRAIT REACTIVITY FLATTENS THE INTENSITY-RESPONSE RELATIONSHIP.
///   The auditory result is the directly applicable one here, since the hostage model's
///   primary stimulus is a gunshot.
///
/// THE ONE DESIGN DECISION WORTH KNOWING
///   All three profiles deliberately keep IDENTICAL distance thresholds (panicDistance,
///   freezeRange, freezeThreshold). Profiles differ ONLY in threat discrimination.
///
///   This is a correctness requirement, not a simplification. The dB-to-distance chain
///   CANNOT separate profiles at CQB range: consecutive published thresholds map to
///   1 m -> 14 m -> 158 m -> 2.2 km, and gunfire exceeds the 137 dB Fear/Panic threshold at
///   EVERY in-building distance. Per-profile distances would therefore be invented numbers
///   presented as research-derived ones. Discrimination is the parameter the research
///   actually supports. See HOSTAGE_PROFILE_RESEARCH.md section 3.6 for the full arithmetic.
///
/// HONEST STATUS OF THE NUMBERS BELOW
///   The MECHANISM and its DIRECTION are research-grounded. The three discrimination values
///   themselves are design calibrations, not published values — the literature supplies no
///   numeric probability. Treat them as tunable, and prefer a sensitivity analysis (showing
///   the Weak > Normal > Brave ordering is stable across a range) over defending any exact
///   value. Same standing as the Speed coefficients in MODULE4_EVALUATION.md section 8.
///
/// Companion documents: HOSTAGE_PROFILE_RESEARCH.md (research + design),
///                      HOSTAGE_EMOTION_SOUND_RESEARCH.pdf (the stimulus side).
/// </summary>
public enum HostageProfile
{
    /// High neuroticism / trait anxiety / high looming. Cannot grade the threat: reacts to a
    /// distant shot as though it were point-blank, may stay frozen after the threat is gone,
    /// and may fail to recognise the approaching rescuer. Slow to extract.
    Weak,

    /// Population-typical. Grades the threat correctly MOST of the time, with occasional
    /// lapses.
    ///
    /// NOTE — this is deliberately NOT identical to the project's pre-profile behaviour.
    /// The original code never misread anything, which is equivalent to a discrimination of
    /// 1.0 (what Brave has). Introducing profiles necessarily changes hostage behaviour —
    /// that is the point of the feature — and there is no setting that is both
    /// "profiles enabled" and "bit-identical to before". Tests that need the old
    /// deterministic behaviour should pin the profile to Brave, which Module2TestRunner does.
    Normal,

    /// Low neuroticism / low looming. Grades the threat correctly, low peak distress,
    /// recognises and follows the rescuer quickly — but carries a small captor-defiance
    /// tendency (London Syndrome) that raises execution risk.
    Brave,
}

/// <summary>
/// Preset lookup for <see cref="HostageProfile"/>. A static table rather than a
/// ScriptableObject, mirroring how Module 2's <c>AILevel</c> is a plain enum — the values are
/// research-fixed rather than designer-tuned, so there is nothing to author per-scene.
/// </summary>
public static class HostageProfiles
{
    // ── The one differentiating parameter ────────────────────────────────────────
    // Probability that a NON-THREATENING stimulus is correctly recognised as non-threatening.
    //   1.0 = response GRADES with stimulus intensity/proximity  (low-anxiety pattern)
    //   low = response is FLAT — a distant shot is treated like a point-blank one
    // DESIGN CALIBRATIONS, not published values — see the class doc above.
    private const float DISCRIMINATION_WEAK   = 0.35f;
    private const float DISCRIMINATION_NORMAL = 0.85f;
    private const float DISCRIMINATION_BRAVE  = 1.00f;

    // ── Brave-only tail risk ─────────────────────────────────────────────────────
    // London Syndrome: hostages who become aggressively defiant toward captors are
    // historically the ones who get executed (Iranian Embassy siege is the origin case).
    // Folded into Brave rather than given its own profile, so "brave" is not naively "better".
    // Set to 0 to disable the behaviour entirely — but then record the omission, because the
    // model would be deliberately excluding a documented captivity phenomenon.
    private const float DEFIANCE_CHANCE_BRAVE = 0.15f;

    // ── Research-weighted spawn distribution ─────────────────────────────────────
    // Published trauma-trajectory prevalence (Galatzer-Levy, Huang & Bonanno 2018,
    // Clinical Psychology Review): resilience 65.7% / recovery 20.8% / chronicity 10.6%.
    // Those sum to 97.1%; the ~2.9% remainder is the DELAYED-ONSET trajectory, which is
    // deliberately not modelled (it describes symptoms emerging weeks-to-months later, which
    // has no counterpart inside a ~5-minute mission). Renormalised over the three modelled
    // profiles => 67.7 / 21.4 / 10.9. NOT equal thirds: a trainee should meet a composed
    // hostage far more often than a fragile one.
    private const float SPAWN_WEIGHT_BRAVE  = 0.677f;
    private const float SPAWN_WEIGHT_NORMAL = 0.214f;
    // Weak takes the remainder (0.109) so the weights always sum to exactly 1.

    /// <summary>
    /// How reliably this profile tells a real threat from a harmless one (0..1).
    /// Higher = better discrimination = response scales properly with the actual danger.
    /// </summary>
    public static float ThreatDiscrimination(HostageProfile profile) => profile switch
    {
        HostageProfile.Weak   => DISCRIMINATION_WEAK,
        HostageProfile.Brave  => DISCRIMINATION_BRAVE,
        _                     => DISCRIMINATION_NORMAL,
    };

    /// <summary>Chance this profile defies its captor when threatened (Brave only).</summary>
    public static float DefianceChance(HostageProfile profile) =>
        profile == HostageProfile.Brave ? DEFIANCE_CHANCE_BRAVE : 0f;

    /// <summary>
    /// Picks a profile using the research-weighted prevalence above. Used for normal training
    /// so hostage variety is realistic. EVALUATION runs should force a specific profile
    /// instead, so prevalence never confounds a controlled comparison.
    /// </summary>
    public static HostageProfile RandomWeighted()
    {
        float r = Random.value;
        if (r < SPAWN_WEIGHT_BRAVE)                        return HostageProfile.Brave;
        if (r < SPAWN_WEIGHT_BRAVE + SPAWN_WEIGHT_NORMAL)  return HostageProfile.Normal;
        return HostageProfile.Weak;
    }

    /// <summary>
    /// Parses a profile name from the dashboard mission form / HTTP scenario request.
    /// Accepts "weak" | "normal" | "brave" (case-insensitive). Anything else — including
    /// "random", null, or an unrecognised value — yields a research-weighted random pick,
    /// which is the intended default for training runs.
    /// </summary>
    public static HostageProfile Parse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return RandomWeighted();
        switch (raw.Trim().ToLowerInvariant())
        {
            case "weak":   return HostageProfile.Weak;
            case "normal": return HostageProfile.Normal;
            case "brave":  return HostageProfile.Brave;
            default:       return RandomWeighted();
        }
    }
}
