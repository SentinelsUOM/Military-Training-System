// Placed in the TeamSentinels.Module4 assembly on purpose: SessionLogger (this assembly)
// reads it, and ScenarioHttpServer (Assembly-CSharp) auto-references this assembly and
// writes it. An asmdef cannot reference Assembly-CSharp, so this shared holder must live
// here — not under Scripts/ — or SessionLogger would not be able to see it.
//
// Left in the GLOBAL namespace (no namespace block) so both sides reference it as plain
// `EvaluationContext`.

/// <summary>
/// Carries the current evaluation run's participant code and enemy-AI level from the
/// moment a scenario is started from the dashboard form (set by ScenarioHttpServer) to
/// the moment the SessionSummary is assembled at mission end (read by SessionLogger),
/// so the dashboard can group a player's Dumb/Medium/Full plays for comparison.
///
/// Static because "scenario start" and "mission end" are far apart in time and live in
/// different systems. Null when the run was not started as part of the evaluation study.
/// </summary>
public static class EvaluationContext
{
    /// Participant code, e.g. "P03". Null = not an evaluation run.
    public static string PlayerId;

    /// Enemy-AI tier: "dumb" | "medium" | "full".
    public static string NpcLevel;
}
