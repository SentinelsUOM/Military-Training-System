// Dry-run entry point. Builds a default ScenarioConfig, runs the full Module 1
// pipeline, serialises the result, and prints the first 200 lines.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamSentinels.ScenarioGeneration.DataModels;
using TeamSentinels.ScenarioGeneration.Generators;
using TeamSentinels.ScenarioGeneration.IO;

internal static class DryRun
{
    private static int Main()
    {
        try
        {
            var config = new ScenarioConfig();
            // Pin a deterministic seed so the output is reproducible run-to-run.
            config.executionControls.seed = 20260419;

            Console.WriteLine(string.Format(
                "=== Config: layout={0}, rooms=[{1}-{2}], terrorists={3}, difficulty={4}, seed={5} ===",
                config.missionStructure.layoutType,
                config.missionStructure.roomCount.min,
                config.missionStructure.roomCount.max,
                config.entityConfiguration.terroristCount,
                config.executionControls.difficultyLevel,
                config.executionControls.seed));

            var generator = new ScenarioGenerator();
            ScenarioData scenario = generator.Generate(config);

            Console.WriteLine("=== Generation complete ===");
            Console.WriteLine(string.Format(
                "    scenarioId    : {0}", scenario.scenarioId));
            Console.WriteLine(string.Format(
                "    rooms         : {0}", scenario.layout.rooms.Count));
            Console.WriteLine(string.Format(
                "    entities      : {0}", scenario.entities.Count));
            Console.WriteLine(string.Format(
                "    roleAssignments: {0}", scenario.roleAssignments.Count));
            var vr = scenario.configurationMetadata.validationResult;
            Console.WriteLine(string.Format(
                "    validation    : passed={0} ({1}/{2}); warnings={3}",
                vr.passed, vr.checksPassed, vr.checksRun,
                (vr.warnings == null || vr.warnings.Count == 0) ? "(none)" : string.Join("; ", vr.warnings)));

            string json = ScenarioExporter.SerialiseToJson(scenario, compact: false);
            string[] lines = json.Replace("\r\n", "\n").Split('\n');
            Console.WriteLine(string.Format(
                "=== JSON length: {0} chars across {1} lines ===", json.Length, lines.Length));

            // Dump the full JSON alongside the exe so we can inspect later sections.
            string fullPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(typeof(DryRun).Assembly.Location), "Scenario_dryrun.json");
            System.IO.File.WriteAllText(fullPath, json);
            Console.WriteLine("=== Wrote full JSON to " + fullPath + " ===");

            int take = Math.Min(200, lines.Length);
            Console.WriteLine("=== First " + take + " lines ===");
            for (int i = 0; i < take; i++)
            {
                Console.WriteLine(lines[i]);
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("DRY-RUN FAILED:");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
