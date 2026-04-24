// =============================================================================
// ScenarioGenerator.cs
// Module 1 – Dynamic Scenario Generation
// Team Sentinels | University of Moratuwa | 2026
//
// Orchestrator class that drives the complete generation pipeline. Takes a
// ScenarioConfig as input, calls subsystems in order (layout → entity →
// role → navigation), and outputs a completed ScenarioData. All randomness
// flows through a single seeded System.Random instance for full seed-based
// reproducibility [22].
// =============================================================================

using System;
using System.Collections.Generic;
using TeamSentinels.ScenarioGeneration.DataModels;
using TeamSentinels.ScenarioGeneration.IO;
using UnityEngine;

namespace TeamSentinels.ScenarioGeneration.Generators
{
    /// <summary>
    /// Orchestrates the complete Module 1 generation pipeline. This is the
    /// primary entry point for scenario generation — call
    /// <see cref="Generate(ScenarioConfig)"/> to produce a complete
    /// <see cref="ScenarioData"/> from evaluator-defined parameters.
    ///
    /// <para><b>Pipeline stages (executed sequentially):</b></para>
    /// <list type="number">
    ///   <item>Layout Generation — produces room graph, corridors, doors</item>
    ///   <item>Entity Placement — positions trainee, hostage, terrorists</item>
    ///   <item>Role Assignment — derives NPC roles from spatial structure</item>
    ///   <item>Navigation Context — builds per-NPC movement data</item>
    ///   <item>Assembly — bundles the <see cref="ScenarioData"/> with
    ///         configuration metadata (validation is a Phase 3 follow-up).</item>
    /// </list>
    /// </summary>
    public class ScenarioGenerator
    {
        // ── Constants ────────────────────────────────────────────────────────

        /// <summary>Current generator version (semantic versioning).</summary>
        public const string GeneratorVersion = "1.0.0";

        /// <summary>
        /// Maximum number of retries when layout generation fails due to
        /// unreachable rooms. Each retry increments the seed by 1.
        /// </summary>
        private const int MaxLayoutRetries = 3;

        // ── Pipeline Stage Instances ─────────────────────────────────────────

        private readonly LayoutGenerator _layoutGenerator;
        private readonly EntityPlacer _entityPlacer;
        private readonly RoleAssigner _roleAssigner;
        private readonly NavigationContextBuilder _navigationContextBuilder;

        // ── Construction ─────────────────────────────────────────────────────

        /// <summary>
        /// Constructs a new orchestrator with fresh pipeline-stage instances.
        /// The orchestrator is stateless between <see cref="Generate"/> calls —
        /// pipeline stages carry no per-run state, so a single
        /// <see cref="ScenarioGenerator"/> can be reused for many generations.
        /// </summary>
        public ScenarioGenerator()
        {
            _layoutGenerator = new LayoutGenerator();
            _entityPlacer = new EntityPlacer();
            _roleAssigner = new RoleAssigner();
            _navigationContextBuilder = new NavigationContextBuilder();
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Generates a complete scenario from the given configuration. Runs
        /// the full pipeline (layout → entities → roles → navigation) and
        /// returns a populated <see cref="ScenarioData"/>. If layout generation
        /// produces an unreachable graph, the pipeline retries up to
        /// <see cref="MaxLayoutRetries"/> times with incremented seeds.
        /// </summary>
        /// <param name="config">Validated evaluator configuration.</param>
        /// <returns>A fully populated <see cref="ScenarioData"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
        /// <exception cref="Exception">
        /// Thrown when layout generation fails even after
        /// <see cref="MaxLayoutRetries"/> retries.
        /// </exception>
        public ScenarioData Generate(ScenarioConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            // ── Stage 0: Resolve seed ────────────────────────────────────────
            int resolvedSeed = config.executionControls.seed ?? Environment.TickCount;
            Debug.Log($"[ScenarioGenerator] Resolving seed: {resolvedSeed}");

            // ── Stage 1: Layout (with retry on unreachable rooms) ────────────
            LayoutData layout = null;
            System.Random rng = null;
            int seedInUse = resolvedSeed;

            for (int attempt = 0; attempt <= MaxLayoutRetries; attempt++)
            {
                rng = new System.Random(seedInUse);
                try
                {
                    layout = _layoutGenerator.Generate(config, rng);
                    break;
                }
                catch (Exception ex) when (attempt < MaxLayoutRetries)
                {
                    Debug.LogWarning(
                        $"[ScenarioGenerator] Layout generation failed on attempt " +
                        $"{attempt + 1}/{MaxLayoutRetries + 1} with seed {seedInUse}: " +
                        $"{ex.Message}. Retrying with seed {seedInUse + 1}.");
                    seedInUse++;
                }
            }

            if (layout == null)
            {
                throw new Exception(
                    $"Layout generation failed after {MaxLayoutRetries + 1} attempts " +
                    $"(seeds {resolvedSeed}..{seedInUse}).");
            }

            resolvedSeed = seedInUse;
            Debug.Log($"[ScenarioGenerator] Layout generated: " +
                $"{layout.rooms.Count} rooms, {layout.layoutMetadata.layoutType}");

            // ── Stage 2: Entity Placement ────────────────────────────────────
            EntityPlacementResult placement = _entityPlacer.Place(layout, config, rng);
            List<EntityRecord> entities = placement.entities;
            SpawnPointData spawnPoints = placement.spawnPoints;
            Debug.Log($"[ScenarioGenerator] Entities placed: " +
                $"{entities.Count} entities");

            // ── Stage 3: Role Assignment ─────────────────────────────────────
            List<RoleAssignment> roleAssignments = _roleAssigner.Assign(
                layout, entities, spawnPoints, config, rng);
            Debug.Log($"[ScenarioGenerator] Roles assigned: " +
                $"{roleAssignments.Count} assignments");

            // ── Stage 4: Navigation Context ──────────────────────────────────
            Dictionary<string, NavigationContextEntry> navigationContext =
                _navigationContextBuilder.Build(
                    layout, roleAssignments, entities, spawnPoints, rng);
            Debug.Log($"[ScenarioGenerator] Navigation context built: " +
                $"{navigationContext.Count} entries");

            // ── Stage 5: Assemble ScenarioData ───────────────────────────────
            var scenario = new ScenarioData
            {
                scenarioId = Guid.NewGuid().ToString(),
                missionType = config.missionStructure.missionType,
                layout = layout,
                spawnPoints = spawnPoints,
                entities = entities,
                roleAssignments = roleAssignments,
                navigationContext = navigationContext,
                configurationMetadata = new ConfigurationMetadata
                {
                    scenarioConfig = config,
                    generationTimestamp = DateTime.UtcNow.ToString("o"),
                    seedUsed = resolvedSeed,
                    generatorVersion = GeneratorVersion,
                    validationResult = ValidationResult.CreatePassed(0)
                }
            };

            Debug.Log($"[ScenarioGenerator] Scenario {scenario.scenarioId} " +
                $"assembled successfully");

            return scenario;
        }

        /// <summary>
        /// Convenience method: loads a config from a JSON file, generates a
        /// scenario, and exports it to disk.
        /// </summary>
        /// <param name="configPath">Path to ScenarioConfig.json.</param>
        /// <param name="outputPath">
        /// Path for output Scenario.json. If null, a default path under
        /// <see cref="ScenarioExporter.DefaultOutputDirectory"/> is used.
        /// </param>
        /// <returns>Absolute path of the exported Scenario.json file.</returns>
        public string GenerateAndExport(string configPath, string outputPath = null)
        {
            ScenarioConfig config = ScenarioConfigLoader.LoadFromFile(configPath);
            ScenarioData scenario = Generate(config);
            return ScenarioExporter.ExportToFile(scenario, outputPath);
        }

        /// <summary>
        /// Convenience method: parses a raw ScenarioConfig JSON string and
        /// generates a scenario. Useful for network-delivered configs or
        /// in-memory test fixtures.
        /// </summary>
        /// <param name="configJson">Raw ScenarioConfig JSON string.</param>
        /// <returns>A fully populated <see cref="ScenarioData"/>.</returns>
        public ScenarioData GenerateFromJson(string configJson)
        {
            ScenarioConfig config = ScenarioConfigLoader.LoadFromJson(configJson);
            return Generate(config);
        }
    }
}
