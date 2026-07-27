// =============================================================================
// BatchEvaluationRunner.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Drives the Module 1 evaluation study. Each [ContextMenu] entry runs one
// experiment: it generates a batch of scenarios from a controlled set of
// configs, measures every one with ScenarioMetrics, and writes a CSV under
// Assets/Module1_DataModels_and_IO/Output/EvaluationResults/ for statistical
// analysis.
//
//   A  — variability at production settings (randomness = medium)
//   A2 — variability ceiling (randomness = high)
//   B  — differentiation across the six evaluator-facing form parameters (PRIMARY)
//   C  — randomness sensitivity            (secondary, RQ4 clause 2)
//   D  — difficulty differentiation        (secondary, RQ4 clause 2)
//   E  — validation reliability over the full pipeline parameter space
//
// Usage: attach to an empty GameObject, then right-click the component header
// and pick an experiment. Runs are synchronous — the editor is unresponsive
// until the batch finishes (roughly a minute for the full 2000-scenario suite).
//
// Design notes:
//   * Every scenario is given an EXPLICIT seed rather than letting the pipeline
//     auto-seed. ScenarioGenerator falls back to Environment.TickCount, whose
//     resolution (~15 ms on Windows) is coarser than one generation, so a tight
//     loop would hand many scenarios the SAME seed and collapse the very
//     variability these experiments measure. Seeds come from a master
//     System.Random, so each batch is unique-per-scenario AND reproducible.
//   * Generator logging is silenced during batches by default: the pipeline
//     emits ~16 log lines per scenario, which would put ~32,000 entries in the
//     console over a full suite and stall the editor. Nothing is lost — the
//     validation result and any exception are captured into the CSV.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using TeamSentinels.ScenarioGeneration.DataModels;
using TeamSentinels.ScenarioGeneration.Generators;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TeamSentinels.ScenarioGeneration.Evaluation
{
    /// <summary>
    /// Batch runner for the Module 1 evaluation experiments. Each experiment is
    /// exposed as a <see cref="ContextMenu"/> entry and writes one CSV whose
    /// columns are the <see cref="ScenarioMetrics"/> metric set plus the
    /// grouping columns that experiment's analysis needs.
    /// </summary>
    public class BatchEvaluationRunner : MonoBehaviour
    {
        // =====================================================================
        // Inspector Configuration
        // =====================================================================

        [Header("Output")]
        [SerializeField]
        [Tooltip("CSV output directory, relative to the Assets folder.")]
        private string outputDirectory = "Module1_DataModels_and_IO/Output/EvaluationResults";

        [Header("Sample Sizes")]
        [SerializeField]
        [Tooltip("Scenarios per variability experiment (A and A2).")]
        private int variabilitySamples = 100;

        [SerializeField]
        [Tooltip("Scenarios per parameter level in experiments B, C, and D.")]
        private int samplesPerLevel = 50;

        [SerializeField]
        [Tooltip("Scenarios in the validation reliability experiment (E).")]
        private int validationSamples = 500;

        [Header("Reproducibility")]
        [SerializeField]
        [Tooltip("Seeds the per-scenario seed stream. Change it to draw a fresh " +
                 "independent batch; keep it to reproduce a previous run exactly.")]
        private int masterSeed = 20260501;

        [SerializeField]
        [Tooltip("Silence the generation pipeline's own logging during a batch. " +
                 "Validation results and exceptions are still recorded in the CSV.")]
        private bool suppressGeneratorLogs = true;

        // =====================================================================
        // Constants
        // =====================================================================

        /// <summary>
        /// Seed for Experiment E's PARAMETER sampler (distinct from the seeds
        /// handed to the generator), fixed so the sampled parameter space is
        /// identical on every re-run.
        /// </summary>
        private const int ExperimentEParameterSeed = 42;

        /// <summary>Status columns appended to every experiment's CSV.</summary>
        private const string StatusColumns = "validationPassed,validationWarnings,errorMessage";

        /// <summary>Scenario ID written when generation threw before producing a scenario.</summary>
        private const string FailedScenarioId = "FAILED";

        /// <summary>
        /// Per-experiment offsets applied to <see cref="masterSeed"/> so each
        /// experiment draws an independent seed stream. Without them every
        /// experiment would start from the same seed, and the batches that share
        /// the baseline config (Experiment A, Experiment C's medium batch,
        /// Experiment D's difficulty-3 batch) would be identical repeats of one
        /// another rather than independent samples.
        /// </summary>
        private const int SeedStreamA = 1;
        private const int SeedStreamA2 = 2;
        private const int SeedStreamB = 3;
        private const int SeedStreamC = 4;
        private const int SeedStreamD = 5;
        private const int SeedStreamE = 6;

        // Experiment level sets. Declared explicitly rather than cast from ints
        // so a future enum member cannot silently change what gets sampled.
        private static readonly LayoutType[] AllLayoutTypes =
        {
            LayoutType.Linear, LayoutType.Branching, LayoutType.HubAndSpoke, LayoutType.Loop
        };

        private static readonly RoomSizeCategory[] AllRoomSizes =
        {
            RoomSizeCategory.Small, RoomSizeCategory.Medium, RoomSizeCategory.Large
        };

        private static readonly EntryType[] AllEntryTypes =
        {
            EntryType.Single, EntryType.Multiple
        };

        private static readonly PlacementStrategy[] AllPlacementStrategies =
        {
            PlacementStrategy.Clustered, PlacementStrategy.Dispersed,
            PlacementStrategy.FrontLoaded, PlacementStrategy.Deep
        };

        private static readonly HostageRiskLevel[] AllRiskLevels =
        {
            HostageRiskLevel.Low, HostageRiskLevel.Medium, HostageRiskLevel.High
        };

        private static readonly RandomnessLevel[] AllRandomnessLevels =
        {
            RandomnessLevel.Low, RandomnessLevel.Medium, RandomnessLevel.High
        };

        // =====================================================================
        // Context Menu Entry Points
        // =====================================================================

        /// <summary>
        /// Experiment A — variability at production settings. Generates
        /// <see cref="variabilitySamples"/> scenarios from the unmodified
        /// baseline config, each with its own seed, to measure how much the
        /// generator varies at the settings evaluators actually use.
        /// </summary>
        [ContextMenu("Run Experiment A - Variability (Production)")]
        public void RunExperimentA()
        {
            LogSummary(ExecuteVariabilityExperiment(
                "Experiment A", RandomnessLevel.Medium,
                "ExperimentA_Variability_Medium.csv", SeedStreamA,
                "ExperimentA_PairwiseDiversity_Medium.csv"));
        }

        /// <summary>
        /// Experiment A2 — variability ceiling. Identical to Experiment A but
        /// with randomness forced to high, establishing the maximum diversity
        /// achievable if the randomness control were re-exposed on the form.
        /// </summary>
        [ContextMenu("Run Experiment A2 - Variability (High Randomness)")]
        public void RunExperimentA2()
        {
            LogSummary(ExecuteVariabilityExperiment(
                "Experiment A2", RandomnessLevel.High,
                "ExperimentA2_Variability_High.csv", SeedStreamA2,
                "ExperimentA2_PairwiseDiversity_High.csv"));
        }

        /// <summary>
        /// Experiment B (PRIMARY) — form parameter differentiation. Varies each
        /// of the six evaluator-facing parameters across its levels, holding the
        /// rest at baseline, with <see cref="samplesPerLevel"/> scenarios per
        /// level. Every row is attributable to exactly one varied parameter.
        /// </summary>
        [ContextMenu("Run Experiment B - Form Parameter Differentiation")]
        public void RunExperimentB()
        {
            LogSummary(ExecuteExperimentB());
        }

        /// <summary>
        /// Experiment C (secondary) — randomness sensitivity. Three batches of
        /// <see cref="samplesPerLevel"/> scenarios at low, medium, and high
        /// randomness, all other parameters at baseline.
        /// </summary>
        [ContextMenu("Run Experiment C - Randomness Sensitivity")]
        public void RunExperimentC()
        {
            LogSummary(ExecuteExperimentC());
        }

        /// <summary>
        /// Experiment D (secondary) — difficulty differentiation. Three batches
        /// of <see cref="samplesPerLevel"/> scenarios at difficulty 1, 3, and 5
        /// with randomness fixed at medium.
        /// </summary>
        [ContextMenu("Run Experiment D - Difficulty Differentiation")]
        public void RunExperimentD()
        {
            LogSummary(ExecuteExperimentD());
        }

        /// <summary>
        /// Experiment E — validation reliability. Samples the FULL pipeline
        /// parameter space (deliberately including room and terrorist counts
        /// beyond what the dashboard exposes) and records the validation outcome
        /// of every scenario.
        /// </summary>
        [ContextMenu("Run Experiment E - Validation Reliability")]
        public void RunExperimentE()
        {
            LogSummary(ExecuteExperimentE());
        }

        /// <summary>
        /// Runs experiments A, A2, B, C, D, and E in order, timing each, and
        /// logs an aggregate summary for the whole suite.
        /// </summary>
        [ContextMenu("Run All Experiments")]
        public void RunAllExperiments()
        {
            Stopwatch suiteTimer = Stopwatch.StartNew();

            var summaries = new List<ExperimentSummary>
            {
                ExecuteVariabilityExperiment("Experiment A", RandomnessLevel.Medium,
                    "ExperimentA_Variability_Medium.csv", SeedStreamA,
                    "ExperimentA_PairwiseDiversity_Medium.csv"),
                ExecuteVariabilityExperiment("Experiment A2", RandomnessLevel.High,
                    "ExperimentA2_Variability_High.csv", SeedStreamA2,
                    "ExperimentA2_PairwiseDiversity_High.csv"),
                ExecuteExperimentB(),
                ExecuteExperimentC(),
                ExecuteExperimentD(),
                ExecuteExperimentE()
            };

            suiteTimer.Stop();

            int totalScenarios = 0;
            int totalPassed = 0;
            int totalErrored = 0;
            var report = new StringBuilder();

            foreach (ExperimentSummary summary in summaries)
            {
                totalScenarios += summary.total;
                totalPassed += summary.passed;
                totalErrored += summary.errored;
                report.AppendLine(
                    $"  {summary.name,-14} {summary.total,5} scenarios  " +
                    $"{summary.PassRate,6:F1}% pass  {summary.seconds,7:F1}s  → {summary.fileName}");
            }

            float overallPassRate = totalScenarios > 0 ? totalPassed * 100f / totalScenarios : 0f;

            Debug.Log(
                $"[Evaluation Suite] Complete: {totalScenarios} scenarios, " +
                $"{totalPassed} passed validation ({overallPassRate:F1}%), " +
                $"{totalScenarios - totalPassed} failed, " +
                $"{totalErrored} generation errors, " +
                $"{suiteTimer.Elapsed.TotalSeconds:F1}s elapsed.\n{report}");
        }

        // =====================================================================
        // Experiment Implementations
        // =====================================================================

        /// <summary>
        /// Shared body of Experiments A and A2: a fixed config repeated over
        /// many seeds, differing only in the randomness level under test.
        /// </summary>
        /// <param name="experimentName">Label used in progress logs.</param>
        /// <param name="randomness">Randomness level held for the whole batch.</param>
        /// <param name="fileName">Output CSV file name.</param>
        /// <param name="seedStream">Offset selecting this experiment's seed stream.</param>
        /// <param name="pairwiseFileName">
        /// Output file for the pairwise diversity comparison of this batch.
        /// </param>
        private ExperimentSummary ExecuteVariabilityExperiment(
            string experimentName, RandomnessLevel randomness, string fileName,
            int seedStream, string pairwiseFileName)
        {
            Stopwatch timer = Stopwatch.StartNew();
            var generator = new ScenarioGenerator();
            var seeds = new SeedSequence(masterSeed + seedStream);
            var rows = new List<string>(variabilitySamples);

            // The variability experiments are the only ones that keep whole
            // scenarios: per-scenario metrics show how far the metric
            // DISTRIBUTION spreads, but only a head-to-head comparison shows
            // whether individual scenarios actually differ from one another.
            var scenarios = new List<ScenarioData>(variabilitySamples);

            int passed = 0;
            int errored = 0;

            for (int i = 0; i < variabilitySamples; i++)
            {
                ScenarioConfig config = MakeBaselineConfig();
                config.executionControls.randomnessLevel = randomness;

                GenerationOutcome outcome = GenerateOne(generator, config, seeds.Next());
                if (outcome.validationPassed) passed++;
                if (!outcome.succeeded) errored++;
                if (outcome.scenario != null) scenarios.Add(outcome.scenario);

                rows.Add(ComposeRow(null, outcome, null));
                ReportProgress(experimentName, i + 1, variabilitySamples, passed, 10);
            }

            ExportPairwiseDiversity(experimentName, pairwiseFileName, scenarios);

            timer.Stop();
            return Finish(experimentName, fileName, BuildHeader(null, null), rows,
                variabilitySamples, passed, errored, timer);
        }

        /// <summary>
        /// Compares every unique pair in a batch and writes the results to CSV.
        /// Skipped with a warning when fewer than two scenarios generated
        /// successfully, since a single scenario has nothing to compare against.
        /// </summary>
        private void ExportPairwiseDiversity(
            string experimentName, string fileName, List<ScenarioData> scenarios)
        {
            if (scenarios == null || scenarios.Count < 2)
            {
                Debug.LogWarning($"[{experimentName}] Pairwise diversity skipped: " +
                                 $"{scenarios?.Count ?? 0} scenarios available, need at least 2.");
                return;
            }

            List<PairwiseDiversityResult> pairs = DiversityAnalyser.CompareAll(scenarios);

            var rows = new List<string>(pairs.Count);
            double editDistanceSum = 0;
            double jaccardSum = 0;
            foreach (PairwiseDiversityResult pair in pairs)
            {
                rows.Add(pair.ToCsvRow());
                editDistanceSum += pair.graphEditDistance;
                jaccardSum += pair.jaccardRoomConnectivity;
            }

            WriteCsv(fileName, DiversityAnalyser.ToCsvHeader(), rows);

            float meanEditDistance = pairs.Count > 0 ? (float)(editDistanceSum / pairs.Count) : 0f;
            float meanJaccard = pairs.Count > 0 ? (float)(jaccardSum / pairs.Count) : 0f;

            Debug.Log($"[{experimentName}] Pairwise diversity: {pairs.Count} pairs, " +
                      $"mean GED={meanEditDistance:F3}, mean Jaccard={meanJaccard:F3} → {fileName}");
        }

        /// <summary>
        /// Experiment B: for each evaluator-facing parameter, sweeps its levels
        /// while holding everything else at baseline.
        /// </summary>
        private ExperimentSummary ExecuteExperimentB()
        {
            const string experimentName = "Experiment B";

            Stopwatch timer = Stopwatch.StartNew();
            var generator = new ScenarioGenerator();
            var seeds = new SeedSequence(masterSeed + SeedStreamB);
            var rows = new List<string>();

            List<ParameterSweep> sweeps = BuildFormParameterSweeps();
            int total = 0;
            foreach (ParameterSweep sweep in sweeps) total += sweep.levels.Count * samplesPerLevel;

            int completed = 0;
            int passed = 0;
            int errored = 0;

            foreach (ParameterSweep sweep in sweeps)
            {
                int blockTotal = sweep.levels.Count * samplesPerLevel;
                int blockCompleted = 0;

                foreach (ParameterLevel level in sweep.levels)
                {
                    for (int i = 0; i < samplesPerLevel; i++)
                    {
                        ScenarioConfig config = MakeBaselineConfig();
                        level.apply(config);

                        GenerationOutcome outcome = GenerateOne(generator, config, seeds.Next());
                        if (outcome.validationPassed) passed++;
                        if (!outcome.succeeded) errored++;

                        string leading = string.Join(",", new[]
                        {
                            CsvField(sweep.parameterName),
                            CsvField(level.value)
                        });
                        rows.Add(ComposeRow(leading, outcome, null));

                        completed++;
                        blockCompleted++;
                        ReportProgress(experimentName, completed, total, passed, 100);
                    }
                }

                Debug.Log($"[{experimentName}] {sweep.parameterName}: " +
                          $"{blockCompleted}/{blockTotal} complete");
            }

            timer.Stop();
            string header = BuildHeader("variedParameter,parameterValue", null);
            return Finish(experimentName, "ExperimentB_FormParameters.csv", header, rows,
                total, passed, errored, timer);
        }

        /// <summary>
        /// Experiment C: baseline config across the three randomness levels.
        /// </summary>
        private ExperimentSummary ExecuteExperimentC()
        {
            const string experimentName = "Experiment C";

            Stopwatch timer = Stopwatch.StartNew();
            var generator = new ScenarioGenerator();
            var seeds = new SeedSequence(masterSeed + SeedStreamC);
            var rows = new List<string>();

            int total = AllRandomnessLevels.Length * samplesPerLevel;
            int completed = 0;
            int passed = 0;
            int errored = 0;

            foreach (RandomnessLevel randomness in AllRandomnessLevels)
            {
                for (int i = 0; i < samplesPerLevel; i++)
                {
                    ScenarioConfig config = MakeBaselineConfig();
                    config.executionControls.randomnessLevel = randomness;

                    GenerationOutcome outcome = GenerateOne(generator, config, seeds.Next());
                    if (outcome.validationPassed) passed++;
                    if (!outcome.succeeded) errored++;

                    rows.Add(ComposeRow(CsvField(ScenarioMetrics.EnumToString(randomness)),
                        outcome, null));

                    completed++;
                    ReportProgress(experimentName, completed, total, passed, 50);
                }
            }

            timer.Stop();
            string header = BuildHeader("randomnessBatch", null);
            return Finish(experimentName, "ExperimentC_RandomnessSensitivity.csv", header, rows,
                total, passed, errored, timer);
        }

        /// <summary>
        /// Experiment D: baseline config at difficulty 1, 3, and 5 with
        /// randomness held at medium.
        /// </summary>
        private ExperimentSummary ExecuteExperimentD()
        {
            const string experimentName = "Experiment D";
            int[] difficultyLevels = { 1, 3, 5 };

            Stopwatch timer = Stopwatch.StartNew();
            var generator = new ScenarioGenerator();
            var seeds = new SeedSequence(masterSeed + SeedStreamD);
            var rows = new List<string>();

            int total = difficultyLevels.Length * samplesPerLevel;
            int completed = 0;
            int passed = 0;
            int errored = 0;

            foreach (int difficulty in difficultyLevels)
            {
                for (int i = 0; i < samplesPerLevel; i++)
                {
                    ScenarioConfig config = MakeBaselineConfig();
                    config.executionControls.difficultyLevel = difficulty;
                    config.executionControls.randomnessLevel = RandomnessLevel.Medium;

                    GenerationOutcome outcome = GenerateOne(generator, config, seeds.Next());
                    if (outcome.validationPassed) passed++;
                    if (!outcome.succeeded) errored++;

                    rows.Add(ComposeRow(
                        difficulty.ToString(CultureInfo.InvariantCulture), outcome, null));

                    completed++;
                    ReportProgress(experimentName, completed, total, passed, 50);
                }
            }

            timer.Stop();
            string header = BuildHeader("difficultyBatch", null);
            return Finish(experimentName, "ExperimentD_DifficultyDifferentiation.csv", header, rows,
                total, passed, errored, timer);
        }

        /// <summary>
        /// Experiment E: uniform random sampling of the full pipeline parameter
        /// space, including room and terrorist counts beyond the dashboard's
        /// range, recording each scenario's validation outcome.
        /// </summary>
        private ExperimentSummary ExecuteExperimentE()
        {
            const string experimentName = "Experiment E";

            Stopwatch timer = Stopwatch.StartNew();
            var generator = new ScenarioGenerator();
            var seeds = new SeedSequence(masterSeed + SeedStreamE);

            // Parameter sampling is driven by its own fixed-seed RNG so the set
            // of configs tested is identical on every re-run, independent of the
            // per-scenario generation seeds.
            var parameterRng = new System.Random(ExperimentEParameterSeed);
            var rows = new List<string>(validationSamples);

            int passed = 0;
            int errored = 0;

            for (int i = 0; i < validationSamples; i++)
            {
                ScenarioConfig config = MakeBaselineConfig();

                int roomCount = parameterRng.Next(3, 16);       // 3–15 inclusive
                int terroristCount = parameterRng.Next(1, 9);   // 1–8 inclusive

                config.missionStructure.layoutType = Pick(AllLayoutTypes, parameterRng);
                config.missionStructure.roomCount = new RoomCountRange(roomCount, roomCount);
                config.missionStructure.roomSize = Pick(AllRoomSizes, parameterRng);
                config.missionStructure.entryType = Pick(AllEntryTypes, parameterRng);
                config.entityConfiguration.terroristCount = terroristCount;
                config.entityConfiguration.placementStrategy = Pick(AllPlacementStrategies, parameterRng);
                config.entityConfiguration.hostageRiskLevel = Pick(AllRiskLevels, parameterRng);
                config.executionControls.difficultyLevel = parameterRng.Next(1, 6);
                config.executionControls.randomnessLevel = Pick(AllRandomnessLevels, parameterRng);

                bool withinFormRange =
                    roomCount >= 3 && roomCount <= 5 &&
                    terroristCount >= 1 && terroristCount <= 4;

                int requestedSeed = seeds.Next();
                GenerationOutcome outcome = GenerateOne(generator, config, requestedSeed);
                if (outcome.validationPassed) passed++;
                if (!outcome.succeeded) errored++;

                // The pipeline retries a failed attempt with seed + 1, so the
                // gap between the requested and used seed is the retry count —
                // the difference between "passed first time" and "passed
                // eventually", which a bare pass rate would hide.
                int retries = Mathf.Max(0, outcome.metrics.seedUsed - requestedSeed);

                string trailing = string.Join(",", new[]
                {
                    CsvField(ClassifyFailure(outcome)),
                    retries.ToString(CultureInfo.InvariantCulture)
                });

                rows.Add(ComposeRow(withinFormRange ? "true" : "false", outcome, trailing));
                ReportProgress(experimentName, i + 1, validationSamples, passed, 50);
            }

            timer.Stop();
            string header = BuildHeader("withinFormRange", "failureCategory,seedRetries");
            return Finish(experimentName, "ExperimentE_ValidationReliability.csv", header, rows,
                validationSamples, passed, errored, timer);
        }

        // =====================================================================
        // Configuration
        // =====================================================================

        /// <summary>
        /// The shared production baseline every experiment starts from. Mirrors
        /// the dashboard's default form state: branching layout, 4 rooms,
        /// medium rooms, single entry, 3 terrorists, dispersed placement, with
        /// the form-hidden parameters at their fixed production values.
        /// </summary>
        /// <returns>A fresh config instance — callers may mutate it freely.</returns>
        private static ScenarioConfig MakeBaselineConfig()
        {
            return new ScenarioConfig
            {
                missionStructure = new MissionStructure
                {
                    missionType = MissionType.HostageRescue,
                    roomCount = new RoomCountRange(4, 4),
                    roomSize = RoomSizeCategory.Medium,
                    layoutType = LayoutType.Branching,
                    entryType = EntryType.Single
                },
                entityConfiguration = new EntityConfiguration
                {
                    hostageCount = 1,
                    terroristCount = 3,
                    placementStrategy = PlacementStrategy.Dispersed,
                    hostageRiskLevel = HostageRiskLevel.Medium
                },
                executionControls = new ExecutionControls
                {
                    difficultyLevel = 3,
                    randomnessLevel = RandomnessLevel.Medium,
                    seed = null,
                    timeLimit = null,
                    customLabel = null
                }
            };
        }

        /// <summary>
        /// The six evaluator-facing parameters and their levels, in the order
        /// they appear on the dashboard form.
        /// </summary>
        private static List<ParameterSweep> BuildFormParameterSweeps()
        {
            var sweeps = new List<ParameterSweep>();

            var layoutLevels = new List<ParameterLevel>();
            foreach (LayoutType layout in AllLayoutTypes)
            {
                LayoutType captured = layout;
                layoutLevels.Add(new ParameterLevel(
                    ScenarioMetrics.EnumToString(captured),
                    c => c.missionStructure.layoutType = captured));
            }
            sweeps.Add(new ParameterSweep("layoutType", layoutLevels));

            var roomCountLevels = new List<ParameterLevel>();
            foreach (int rooms in new[] { 3, 4, 5 })
            {
                int captured = rooms;
                roomCountLevels.Add(new ParameterLevel(
                    captured.ToString(CultureInfo.InvariantCulture),
                    c => c.missionStructure.roomCount = new RoomCountRange(captured, captured)));
            }
            sweeps.Add(new ParameterSweep("roomCount", roomCountLevels));

            var roomSizeLevels = new List<ParameterLevel>();
            foreach (RoomSizeCategory size in AllRoomSizes)
            {
                RoomSizeCategory captured = size;
                roomSizeLevels.Add(new ParameterLevel(
                    ScenarioMetrics.EnumToString(captured),
                    c => c.missionStructure.roomSize = captured));
            }
            sweeps.Add(new ParameterSweep("roomSize", roomSizeLevels));

            var placementLevels = new List<ParameterLevel>();
            foreach (PlacementStrategy strategy in AllPlacementStrategies)
            {
                PlacementStrategy captured = strategy;
                placementLevels.Add(new ParameterLevel(
                    ScenarioMetrics.EnumToString(captured),
                    c => c.entityConfiguration.placementStrategy = captured));
            }
            sweeps.Add(new ParameterSweep("placementStrategy", placementLevels));

            var terroristLevels = new List<ParameterLevel>();
            foreach (int count in new[] { 1, 2, 3, 4 })
            {
                int captured = count;
                terroristLevels.Add(new ParameterLevel(
                    captured.ToString(CultureInfo.InvariantCulture),
                    c => c.entityConfiguration.terroristCount = captured));
            }
            sweeps.Add(new ParameterSweep("terroristCount", terroristLevels));

            var entryLevels = new List<ParameterLevel>();
            foreach (EntryType entry in AllEntryTypes)
            {
                EntryType captured = entry;
                entryLevels.Add(new ParameterLevel(
                    ScenarioMetrics.EnumToString(captured),
                    c => c.missionStructure.entryType = captured));
            }
            sweeps.Add(new ParameterSweep("entryType", entryLevels));

            return sweeps;
        }

        // =====================================================================
        // Generation
        // =====================================================================

        /// <summary>
        /// Generates and measures one scenario. Never throws: a generation
        /// failure is captured into the returned outcome as a FAILED row that
        /// still carries the config echo, so the analysis can see which
        /// parameter combination broke.
        /// </summary>
        /// <param name="generator">Generator instance for this experiment.</param>
        /// <param name="config">Config to generate from; its seed is overwritten.</param>
        /// <param name="seed">Explicit seed for this scenario.</param>
        private GenerationOutcome GenerateOne(
            ScenarioGenerator generator, ScenarioConfig config, int seed)
        {
            config.executionControls.seed = seed;

            bool loggingWasEnabled = Debug.unityLogger.logEnabled;
            if (suppressGeneratorLogs) Debug.unityLogger.logEnabled = false;

            try
            {
                ScenarioData scenario = generator.Generate(config);
                ScenarioMetricsResult metrics = ScenarioMetrics.Extract(scenario);
                ValidationResult validation = scenario.configurationMetadata?.validationResult;

                return new GenerationOutcome
                {
                    metrics = metrics,
                    scenario = scenario,
                    succeeded = true,
                    validationPassed = validation?.passed ?? false,
                    warnings = validation?.warnings != null && validation.warnings.Count > 0
                        ? string.Join("; ", validation.warnings)
                        : string.Empty,
                    error = string.Empty
                };
            }
            catch (Exception ex)
            {
                Debug.unityLogger.logEnabled = loggingWasEnabled;
                Debug.LogWarning(
                    $"[BatchEvaluationRunner] Generation failed (seed {seed}, " +
                    $"{DescribeConfig(config)}): {ex.Message}");

                return new GenerationOutcome
                {
                    metrics = MakeFailedMetrics(config, seed),
                    scenario = null,
                    succeeded = false,
                    validationPassed = false,
                    warnings = string.Empty,
                    error = ex.Message
                };
            }
            finally
            {
                Debug.unityLogger.logEnabled = loggingWasEnabled;
            }
        }

        /// <summary>
        /// Builds a metrics result for a scenario that never generated: all
        /// measurements are zero and the scenario ID is
        /// <see cref="FailedScenarioId"/>, but the input parameter echo is
        /// filled in so the failed row is still attributable to its config.
        /// </summary>
        private static ScenarioMetricsResult MakeFailedMetrics(ScenarioConfig config, int seed)
        {
            return new ScenarioMetricsResult
            {
                scenarioId = FailedScenarioId,
                seedUsed = seed,
                layoutType = ScenarioMetrics.EnumToString(config.missionStructure.layoutType),
                roomSizeCategory = ScenarioMetrics.EnumToString(config.missionStructure.roomSize),
                entryType = ScenarioMetrics.EnumToString(config.missionStructure.entryType),
                configRoomCount = config.missionStructure.roomCount?.min ?? 0,
                configTerroristCount = config.entityConfiguration.terroristCount,
                placementStrategy = ScenarioMetrics.EnumToString(config.entityConfiguration.placementStrategy),
                hostageRiskLevel = ScenarioMetrics.EnumToString(config.entityConfiguration.hostageRiskLevel),
                difficultyLevel = config.executionControls.difficultyLevel,
                randomnessLevel = ScenarioMetrics.EnumToString(config.executionControls.randomnessLevel)
            };
        }

        /// <summary>
        /// Classifies a scenario's outcome for Experiment E. Returns "none" when
        /// validation passed, "generation_exception" when the pipeline threw, or
        /// the name of the first failed validation check (the validator prefixes
        /// each warning with its check name, e.g. "Reachability").
        /// </summary>
        private static string ClassifyFailure(GenerationOutcome outcome)
        {
            if (!outcome.succeeded) return "generation_exception";
            if (outcome.validationPassed) return "none";
            if (string.IsNullOrEmpty(outcome.warnings)) return "unknown";

            string firstWarning = outcome.warnings.Split(';')[0];
            int colon = firstWarning.IndexOf(':');
            return colon > 0 ? firstWarning.Substring(0, colon).Trim() : "unknown";
        }

        /// <summary>Compact one-line config description for failure logs.</summary>
        private static string DescribeConfig(ScenarioConfig config)
        {
            return $"layout={config.missionStructure.layoutType}, " +
                   $"rooms={config.missionStructure.roomCount?.min}, " +
                   $"size={config.missionStructure.roomSize}, " +
                   $"entry={config.missionStructure.entryType}, " +
                   $"terrorists={config.entityConfiguration.terroristCount}, " +
                   $"placement={config.entityConfiguration.placementStrategy}, " +
                   $"difficulty={config.executionControls.difficultyLevel}, " +
                   $"randomness={config.executionControls.randomnessLevel}";
        }

        /// <summary>Uniformly picks one element from a level set.</summary>
        private static T Pick<T>(T[] values, System.Random rng) => values[rng.Next(values.Length)];

        // =====================================================================
        // CSV Assembly and Output
        // =====================================================================

        /// <summary>
        /// Builds a header: optional grouping columns, then the full metric set,
        /// then the shared status columns, then any experiment-specific trailing
        /// columns.
        /// </summary>
        /// <param name="leadingColumns">Grouping columns, or null for none.</param>
        /// <param name="trailingColumns">Extra trailing columns, or null for none.</param>
        private static string BuildHeader(string leadingColumns, string trailingColumns)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(leadingColumns)) parts.Add(leadingColumns);
            parts.Add(ScenarioMetrics.ToCsvHeader());
            parts.Add(StatusColumns);
            if (!string.IsNullOrEmpty(trailingColumns)) parts.Add(trailingColumns);
            return string.Join(",", parts);
        }

        /// <summary>
        /// Assembles one CSV row in the same column order as
        /// <see cref="BuildHeader"/>. Leading and trailing values must already
        /// be CSV-escaped.
        /// </summary>
        private static string ComposeRow(
            string leadingValues, GenerationOutcome outcome, string trailingValues)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(leadingValues)) parts.Add(leadingValues);
            parts.Add(outcome.metrics.ToCsvRow());
            parts.Add(outcome.validationPassed ? "true" : "false");
            parts.Add(CsvField(outcome.warnings));
            parts.Add(CsvField(outcome.error));
            if (!string.IsNullOrEmpty(trailingValues)) parts.Add(trailingValues);
            return string.Join(",", parts);
        }

        /// <summary>
        /// Escapes a single CSV field, quoting it when it contains a comma,
        /// double quote, or line break.
        /// </summary>
        private static string CsvField(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        /// <summary>
        /// Writes the CSV, logs the completion line, and packages the summary.
        /// </summary>
        private ExperimentSummary Finish(
            string experimentName, string fileName, string header, List<string> rows,
            int total, int passed, int errored, Stopwatch timer)
        {
            string path = WriteCsv(fileName, header, rows);

            Debug.Log($"[{experimentName}] Complete: {total} scenarios, {passed} passed, " +
                      $"{total - passed} failed → {fileName}");

            return new ExperimentSummary
            {
                name = experimentName,
                fileName = fileName,
                filePath = path,
                total = total,
                passed = passed,
                errored = errored,
                seconds = timer.Elapsed.TotalSeconds
            };
        }

        /// <summary>
        /// Writes a header and rows to the output directory, creating it if
        /// needed. UTF-8 without a BOM, so pandas and R read the header cleanly.
        /// </summary>
        /// <returns>Absolute path of the written file.</returns>
        private string WriteCsv(string fileName, string header, List<string> rows)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, outputDirectory));
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, fileName);
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(false)))
            {
                writer.WriteLine(header);
                foreach (string row in rows) writer.WriteLine(row);
            }

#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
            return path;
        }

        // =====================================================================
        // Logging
        // =====================================================================

        /// <summary>
        /// Logs a progress line every <paramref name="interval"/> scenarios and
        /// always on the final one.
        /// </summary>
        private static void ReportProgress(
            string experimentName, int completed, int total, int passed, int interval)
        {
            if (completed != total && (interval <= 0 || completed % interval != 0)) return;

            float passRate = completed > 0 ? passed * 100f / completed : 0f;
            Debug.Log($"[{experimentName}] Progress: {completed}/{total} " +
                      $"({passRate:F1}% validation pass)");
        }

        /// <summary>Logs the one-line result of a single experiment run.</summary>
        private static void LogSummary(ExperimentSummary summary)
        {
            Debug.Log($"[{summary.name}] {summary.total} scenarios in " +
                      $"{summary.seconds:F1}s, {summary.PassRate:F1}% validation pass, " +
                      $"{summary.errored} generation errors → {summary.filePath}");
        }

        // =====================================================================
        // Support Types
        // =====================================================================

        /// <summary>Outcome of a single generate-and-measure attempt.</summary>
        private struct GenerationOutcome
        {
            /// <summary>Measured metrics, or a zeroed FAILED result when generation threw.</summary>
            public ScenarioMetricsResult metrics;

            /// <summary>
            /// The generated scenario, or null when generation threw. Retained
            /// only by experiments that need the full data afterwards (the
            /// variability experiments feed it to <see cref="DiversityAnalyser"/>);
            /// other experiments let it fall out of scope immediately.
            /// </summary>
            public ScenarioData scenario;

            /// <summary>False when the pipeline threw before producing a scenario.</summary>
            public bool succeeded;

            /// <summary>True when all validation checks passed.</summary>
            public bool validationPassed;

            /// <summary>Validation warnings joined with "; ", or empty.</summary>
            public string warnings;

            /// <summary>Exception message when generation threw, or empty.</summary>
            public string error;
        }

        /// <summary>Aggregate result of one experiment, used by the suite report.</summary>
        private struct ExperimentSummary
        {
            /// <summary>Experiment label, e.g. "Experiment B".</summary>
            public string name;

            /// <summary>Output CSV file name.</summary>
            public string fileName;

            /// <summary>Absolute path of the written CSV.</summary>
            public string filePath;

            /// <summary>Scenarios attempted.</summary>
            public int total;

            /// <summary>Scenarios that passed all validation checks.</summary>
            public int passed;

            /// <summary>Scenarios where the pipeline threw an exception.</summary>
            public int errored;

            /// <summary>Wall-clock duration in seconds.</summary>
            public double seconds;

            /// <summary>Validation pass rate as a percentage.</summary>
            public float PassRate => total > 0 ? passed * 100f / total : 0f;
        }

        /// <summary>One level of a swept parameter and the mutation that applies it.</summary>
        private readonly struct ParameterLevel
        {
            /// <summary>Level as written to the parameterValue column.</summary>
            public readonly string value;

            /// <summary>Applies this level to a baseline config.</summary>
            public readonly Action<ScenarioConfig> apply;

            /// <summary>Creates a level with its CSV label and mutation.</summary>
            public ParameterLevel(string value, Action<ScenarioConfig> apply)
            {
                this.value = value;
                this.apply = apply;
            }
        }

        /// <summary>One parameter of Experiment B together with all its levels.</summary>
        private readonly struct ParameterSweep
        {
            /// <summary>Parameter name as written to the variedParameter column.</summary>
            public readonly string parameterName;

            /// <summary>Levels to sweep for this parameter.</summary>
            public readonly List<ParameterLevel> levels;

            /// <summary>Creates a sweep over the given levels.</summary>
            public ParameterSweep(string parameterName, List<ParameterLevel> levels)
            {
                this.parameterName = parameterName;
                this.levels = levels;
            }
        }

        /// <summary>
        /// Supplies a distinct seed per scenario from a reproducible stream. The
        /// pipeline's own auto-seed (Environment.TickCount) is too coarse to
        /// distinguish scenarios generated milliseconds apart, which would make
        /// a variability batch silently measure repeats of the same scenario.
        /// </summary>
        private sealed class SeedSequence
        {
            private readonly System.Random _rng;
            private readonly HashSet<int> _issued = new HashSet<int>();

            /// <summary>Creates a stream reproducible from <paramref name="masterSeed"/>.</summary>
            public SeedSequence(int masterSeed)
            {
                _rng = new System.Random(masterSeed);
            }

            /// <summary>Returns the next seed, never repeating a previous one.</summary>
            public int Next()
            {
                for (int attempt = 0; attempt < 1000; attempt++)
                {
                    int seed = _rng.Next(1, int.MaxValue);
                    if (_issued.Add(seed)) return seed;
                }
                throw new InvalidOperationException(
                    "SeedSequence could not find an unused seed after 1000 attempts.");
            }
        }
    }
}
