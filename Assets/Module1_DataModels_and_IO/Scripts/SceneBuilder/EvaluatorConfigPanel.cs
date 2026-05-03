// =============================================================================
// EvaluatorConfigPanel.cs
// Module 1 - Dynamic Scenario Generation (Evaluator UI)
// Team Sentinels | University of Moratuwa | 2026
//
// Unity UI controller that lets the evaluator configure every ScenarioConfig
// parameter, runs the Module 1 generation pipeline, and hands the result off
// to SceneBuilder for runtime instantiation.
//
// LOCATION NOTE:
//   Lives at Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/, same as
//   SceneBuilder.cs - i.e. outside the TeamSentinels.ScenarioGeneration asmdef
//   so it compiles into Assembly-CSharp. Required because it references both
//   SceneBuilder (Assembly-CSharp) and ScenarioGenerator (asmdef). The asmdef
//   is auto-referenced into Assembly-CSharp via autoReferenced: true.
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using TeamSentinels.ScenarioGeneration.DataModels;
using TeamSentinels.ScenarioGeneration.Generators;
using TeamSentinels.ScenarioGeneration.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeamSentinels.ScenarioGeneration.Scene
{
    /// <summary>
    /// Evaluator-facing configuration panel. Reads UI controls into a
    /// <see cref="ScenarioConfig"/>, runs <see cref="ScenarioGenerator"/>, and
    /// drives <see cref="SceneBuilder"/> to instantiate the chosen scenario.
    /// </summary>
    public class EvaluatorConfigPanel : MonoBehaviour
    {
        // ── Inspector: Mission Structure ─────────────────────────────────────

        [Header("Mission Structure")]
        public TMP_Dropdown missionTypeDropdown;
        public Slider       roomCountMinSlider;
        public Slider       roomCountMaxSlider;
        public TMP_Text     roomCountMinLabel;
        public TMP_Text     roomCountMaxLabel;
        public TMP_Dropdown roomSizeDropdown;
        public TMP_Dropdown layoutTypeDropdown;
        public TMP_Dropdown entryTypeDropdown;

        // ── Inspector: Entity Configuration ──────────────────────────────────

        [Header("Entity Configuration")]
        public TMP_Text     hostageCountLabel;
        public Slider       terroristCountSlider;
        public TMP_Text     terroristCountLabel;
        public TMP_Dropdown placementStrategyDropdown;
        public TMP_Dropdown hostageRiskLevelDropdown;

        // ── Inspector: Execution Controls ────────────────────────────────────

        [Header("Execution Controls")]
        public Slider         difficultySlider;
        public TMP_Text       difficultyLabel;
        public TMP_Dropdown   randomnessDropdown;
        public TMP_InputField seedInput;
        public TMP_InputField timeLimitInput;
        public TMP_InputField customLabelInput;

        // ── Inspector: Actions ───────────────────────────────────────────────

        [Header("Actions")]
        public Button   generateButton;
        public Button   startMissionButton;
        public TMP_Text statusText;

        // ── Inspector: References ────────────────────────────────────────────

        [Header("References")]
        public SceneBuilder sceneBuilder;

        [Tooltip("Optional: panel root GameObject to hide once the mission " +
                 "starts. Leave empty to keep the UI visible.")]
        public GameObject panelRoot;

        // ── PlayerPrefs key ──────────────────────────────────────────────────

        private const string PREFS_KEY = "LastScenarioConfig";

        // ── Enum dropdown maps (display name + enum value, parallel arrays) ──

        private static readonly RoomSizeCategory[]  RoomSizeValues   = { RoomSizeCategory.Small, RoomSizeCategory.Medium, RoomSizeCategory.Large };
        private static readonly string[]            RoomSizeNames    = { "Small (4x4 m)", "Medium (6x6 m)", "Large (8x8 m)" };

        private static readonly LayoutType[]        LayoutTypeValues = { LayoutType.Linear, LayoutType.Branching, LayoutType.HubAndSpoke, LayoutType.Loop };
        private static readonly string[]            LayoutTypeNames  = { "Linear", "Branching", "Hub & Spoke", "Loop" };

        private static readonly EntryType[]         EntryTypeValues  = { EntryType.Single, EntryType.Multiple };
        private static readonly string[]            EntryTypeNames   = { "Single", "Multiple" };

        private static readonly PlacementStrategy[] PlacementValues  = { PlacementStrategy.Clustered, PlacementStrategy.Dispersed, PlacementStrategy.FrontLoaded, PlacementStrategy.Deep };
        private static readonly string[]            PlacementNames   = { "Clustered", "Dispersed", "Front-Loaded", "Deep" };

        private static readonly HostageRiskLevel[]  RiskValues       = { HostageRiskLevel.Low, HostageRiskLevel.Medium, HostageRiskLevel.High };
        private static readonly string[]            RiskNames        = { "Low", "Medium", "High" };

        private static readonly RandomnessLevel[]   RandomnessValues = { RandomnessLevel.Low, RandomnessLevel.Medium, RandomnessLevel.High };
        private static readonly string[]            RandomnessNames  = { "Low", "Medium", "High" };

        private static readonly string[]            DifficultyLabels = { "1 (Easiest)", "2 (Easy)", "3 (Moderate)", "4 (Hard)", "5 (Hardest)" };

        // ── State ────────────────────────────────────────────────────────────

        private ScenarioGenerator _generator;
        private ScenarioData      _generatedScenario;
        private bool              _suspendCallbacks; // skip slider/dropdown callbacks while populating from JSON

        /// <summary>The most recently generated scenario, or null if none.</summary>
        public ScenarioData GeneratedScenario => _generatedScenario;

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Awake()
        {
            _generator = new ScenarioGenerator();
        }

        private void Start()
        {
            PopulateDropdowns();
            ConfigureSliders();
            WireCallbacks();
            SetButtonsEnabled(generateEnabled: true, startEnabled: false);
            SetStatus("Ready");

            // Restore previous config if available, else use defaults.
            if (!TryLoadConfigFromPrefs())
                ApplyConfigToUi(new ScenarioConfig());

            UpdateAllLabels();
        }

        // ── Dropdown population ──────────────────────────────────────────────

        private void PopulateDropdowns()
        {
            if (missionTypeDropdown != null)
                SetDropdownOptions(missionTypeDropdown, new[] { "Hostage Rescue" });

            SetDropdownOptions(roomSizeDropdown,          RoomSizeNames);
            SetDropdownOptions(layoutTypeDropdown,        LayoutTypeNames);
            SetDropdownOptions(entryTypeDropdown,         EntryTypeNames);
            SetDropdownOptions(placementStrategyDropdown, PlacementNames);
            SetDropdownOptions(hostageRiskLevelDropdown,  RiskNames);
            SetDropdownOptions(randomnessDropdown,        RandomnessNames);
        }

        private static void SetDropdownOptions(TMP_Dropdown dropdown, string[] names)
        {
            if (dropdown == null) return;
            dropdown.ClearOptions();
            var options = new List<TMP_Dropdown.OptionData>(names.Length);
            foreach (string n in names) options.Add(new TMP_Dropdown.OptionData(n));
            dropdown.AddOptions(options);
        }

        private void ConfigureSliders()
        {
            ConfigureSlider(roomCountMinSlider,  3, 20, 5);
            ConfigureSlider(roomCountMaxSlider,  3, 20, 8);
            ConfigureSlider(terroristCountSlider, 1, 8,  4);
            ConfigureSlider(difficultySlider,     1, 5,  3);
        }

        private static void ConfigureSlider(Slider s, int min, int max, int defaultValue)
        {
            if (s == null) return;
            s.wholeNumbers = true;
            s.minValue = min;
            s.maxValue = max;
            s.value    = defaultValue;
        }

        private void WireCallbacks()
        {
            if (roomCountMinSlider   != null) roomCountMinSlider.onValueChanged.AddListener(OnRoomCountMinChanged);
            if (roomCountMaxSlider   != null) roomCountMaxSlider.onValueChanged.AddListener(OnRoomCountMaxChanged);
            if (terroristCountSlider != null) terroristCountSlider.onValueChanged.AddListener(OnTerroristCountChanged);
            if (difficultySlider     != null) difficultySlider.onValueChanged.AddListener(OnDifficultyChanged);

            if (generateButton     != null) generateButton.onClick.AddListener(OnGenerateClicked);
            if (startMissionButton != null) startMissionButton.onClick.AddListener(OnStartMissionClicked);

            if (sceneBuilder != null)
            {
                sceneBuilder.OnSceneBuildComplete += OnSceneBuildComplete;
                sceneBuilder.OnSceneBuildFailed   += OnSceneBuildFailed;
            }
        }

        private void OnDestroy()
        {
            if (sceneBuilder != null)
            {
                sceneBuilder.OnSceneBuildComplete -= OnSceneBuildComplete;
                sceneBuilder.OnSceneBuildFailed   -= OnSceneBuildFailed;
            }
        }

        // ── Slider callbacks ─────────────────────────────────────────────────

        private void OnRoomCountMinChanged(float value)
        {
            if (_suspendCallbacks) return;
            int min = Mathf.RoundToInt(value);
            int max = Mathf.RoundToInt(roomCountMaxSlider != null ? roomCountMaxSlider.value : min);

            // Enforce min <= max by pushing max up if needed.
            if (min > max && roomCountMaxSlider != null)
            {
                _suspendCallbacks = true;
                roomCountMaxSlider.value = min;
                _suspendCallbacks = false;
            }

            UpdateRoomCountLabels();
            UpdateTerroristCountWarning();
        }

        private void OnRoomCountMaxChanged(float value)
        {
            if (_suspendCallbacks) return;
            int max = Mathf.RoundToInt(value);
            int min = Mathf.RoundToInt(roomCountMinSlider != null ? roomCountMinSlider.value : max);

            // Enforce min <= max by pulling min down if needed.
            if (min > max && roomCountMinSlider != null)
            {
                _suspendCallbacks = true;
                roomCountMinSlider.value = max;
                _suspendCallbacks = false;
            }

            UpdateRoomCountLabels();
            UpdateTerroristCountWarning();
        }

        private void OnTerroristCountChanged(float value)
        {
            if (_suspendCallbacks) return;
            UpdateTerroristCountLabel();
            UpdateTerroristCountWarning();
        }

        private void OnDifficultyChanged(float value)
        {
            if (_suspendCallbacks) return;
            UpdateDifficultyLabel();
        }

        // ── Label updates ────────────────────────────────────────────────────

        private void UpdateAllLabels()
        {
            UpdateRoomCountLabels();
            UpdateTerroristCountLabel();
            UpdateDifficultyLabel();
            if (hostageCountLabel != null) hostageCountLabel.text = "1";
        }

        private void UpdateRoomCountLabels()
        {
            if (roomCountMinLabel != null && roomCountMinSlider != null)
                roomCountMinLabel.text = Mathf.RoundToInt(roomCountMinSlider.value).ToString();
            if (roomCountMaxLabel != null && roomCountMaxSlider != null)
                roomCountMaxLabel.text = Mathf.RoundToInt(roomCountMaxSlider.value).ToString();
        }

        private void UpdateTerroristCountLabel()
        {
            if (terroristCountLabel != null && terroristCountSlider != null)
                terroristCountLabel.text = Mathf.RoundToInt(terroristCountSlider.value).ToString();
        }

        private void UpdateDifficultyLabel()
        {
            if (difficultyLabel == null || difficultySlider == null) return;
            int idx = Mathf.Clamp(Mathf.RoundToInt(difficultySlider.value) - 1,
                                  0, DifficultyLabels.Length - 1);
            difficultyLabel.text = DifficultyLabels[idx];
        }

        private void UpdateTerroristCountWarning()
        {
            if (terroristCountSlider == null || roomCountMaxSlider == null) return;
            int t   = Mathf.RoundToInt(terroristCountSlider.value);
            int max = Mathf.RoundToInt(roomCountMaxSlider.value);
            if (t > max * 2)
                SetStatus($"Warning: terroristCount ({t}) exceeds roomCount.max x 2 ({max * 2}).", warn: true);
            else if (statusText != null && statusText.text.StartsWith("Warning:"))
                SetStatus("Ready");
        }

        // ── Build config from UI ─────────────────────────────────────────────

        /// <summary>
        /// Reads every UI control and constructs a populated
        /// <see cref="ScenarioConfig"/>. Performs lightweight UI-side checks
        /// (min &lt;= max, terroristCount &lt;= max*2, timeLimit range) and
        /// surfaces the first failure through <paramref name="warning"/>.
        /// </summary>
        /// <param name="warning">Out: human-readable warning, or null if none.</param>
        /// <returns>The constructed config.</returns>
        public ScenarioConfig BuildConfig(out string warning)
        {
            warning = null;
            var config = new ScenarioConfig();

            // ── Mission Structure ───────────────────────────────────────────
            int minRooms = roomCountMinSlider != null ? Mathf.RoundToInt(roomCountMinSlider.value) : 5;
            int maxRooms = roomCountMaxSlider != null ? Mathf.RoundToInt(roomCountMaxSlider.value) : 8;
            if (minRooms > maxRooms)
            {
                warning = $"roomCountMin ({minRooms}) is greater than roomCountMax ({maxRooms}).";
                (minRooms, maxRooms) = (maxRooms, minRooms); // swap so generator doesn't choke
            }

            config.missionStructure.missionType = MissionType.HostageRescue;
            config.missionStructure.roomCount   = new RoomCountRange(minRooms, maxRooms);
            config.missionStructure.roomSize    = LookupEnum(roomSizeDropdown,   RoomSizeValues,   RoomSizeCategory.Medium);
            config.missionStructure.layoutType  = LookupEnum(layoutTypeDropdown, LayoutTypeValues, LayoutType.Branching);
            config.missionStructure.entryType   = LookupEnum(entryTypeDropdown,  EntryTypeValues,  EntryType.Single);

            // ── Entity Configuration ────────────────────────────────────────
            int terrorists = terroristCountSlider != null ? Mathf.RoundToInt(terroristCountSlider.value) : 4;
            if (terrorists > maxRooms * 2)
                warning ??= $"terroristCount ({terrorists}) exceeds roomCount.max x 2 ({maxRooms * 2}).";

            config.entityConfiguration.hostageCount      = 1; // const per schema
            config.entityConfiguration.terroristCount    = terrorists;
            config.entityConfiguration.placementStrategy = LookupEnum(placementStrategyDropdown, PlacementValues, PlacementStrategy.Dispersed);
            config.entityConfiguration.hostageRiskLevel  = LookupEnum(hostageRiskLevelDropdown,  RiskValues,      HostageRiskLevel.Medium);

            // ── Execution Controls ──────────────────────────────────────────
            config.executionControls.difficultyLevel = difficultySlider != null
                ? Mathf.Clamp(Mathf.RoundToInt(difficultySlider.value), 1, 5)
                : 3;
            config.executionControls.randomnessLevel = LookupEnum(randomnessDropdown, RandomnessValues, RandomnessLevel.Medium);
            config.executionControls.seed            = ParseOptionalInt(seedInput, "seed", null, null, ref warning);
            config.executionControls.timeLimit       = ParseOptionalInt(timeLimitInput, "timeLimit", 60, 1800, ref warning);
            config.executionControls.customLabel     = ParseOptionalString(customLabelInput, 128, ref warning);

            return config;
        }

        private static TEnum LookupEnum<TEnum>(TMP_Dropdown dropdown, TEnum[] values, TEnum fallback)
        {
            if (dropdown == null) return fallback;
            int idx = dropdown.value;
            return (idx >= 0 && idx < values.Length) ? values[idx] : fallback;
        }

        private static int? ParseOptionalInt(TMP_InputField field, string name,
                                             int? min, int? max, ref string warning)
        {
            if (field == null) return null;
            string raw = field.text?.Trim();
            if (string.IsNullOrEmpty(raw)) return null;
            if (!int.TryParse(raw, out int parsed))
            {
                warning ??= $"{name} '{raw}' is not a valid integer.";
                return null;
            }
            if (min.HasValue && parsed < min.Value)
            {
                warning ??= $"{name} ({parsed}) is below the minimum ({min.Value}).";
                return null;
            }
            if (max.HasValue && parsed > max.Value)
            {
                warning ??= $"{name} ({parsed}) is above the maximum ({max.Value}).";
                return null;
            }
            return parsed;
        }

        private static string ParseOptionalString(TMP_InputField field, int maxLen, ref string warning)
        {
            if (field == null) return null;
            string raw = field.text;
            if (string.IsNullOrEmpty(raw)) return null;
            if (raw.Length > maxLen)
            {
                warning ??= $"customLabel length ({raw.Length}) exceeds {maxLen} chars - truncated.";
                raw = raw.Substring(0, maxLen);
            }
            return raw;
        }

        // ── Generate ─────────────────────────────────────────────────────────

        /// <summary>
        /// Builds the config from the UI, runs the Module 1 pipeline, and
        /// stores the resulting scenario for the Start Mission button.
        /// </summary>
        public void OnGenerateClicked()
        {
            SetButtonsEnabled(generateEnabled: false, startEnabled: false);
            SetStatus("Generating...");

            ScenarioConfig config = BuildConfig(out string uiWarning);

            if (!ScenarioConfigLoader.IsValid(config, out List<string> errors))
            {
                SetStatus("Failed: " + string.Join("; ", errors), warn: true);
                SetButtonsEnabled(generateEnabled: true, startEnabled: false);
                return;
            }

            try
            {
                ScenarioData scenario = _generator.Generate(config);
                if (scenario?.configurationMetadata?.validationResult != null &&
                    !scenario.configurationMetadata.validationResult.passed)
                {
                    string warns = string.Join("; ",
                        scenario.configurationMetadata.validationResult.warnings ?? new List<string>());
                    SetStatus($"Failed: validation rejected after retries. {warns}", warn: true);
                    SetButtonsEnabled(generateEnabled: true, startEnabled: false);
                    return;
                }

                _generatedScenario = scenario;
                SaveConfigToPrefs(config);

                // Export the JSON to disk so the evaluator (and Module 4) can
                // inspect/replay it. Lands under the Assets-side output folder
                // so it shows up in Unity's Project window. AssetDatabase refresh
                // makes the new file visible immediately in the editor.
                string exportedAt = TryExportScenarioJson(scenario);

                int rooms    = scenario.layout?.rooms?.Count ?? 0;
                int entities = scenario.entities?.Count ?? 0;
                int seedUsed = scenario.configurationMetadata?.seedUsed ?? 0;

                string warnSuffix = string.IsNullOrEmpty(uiWarning) ? "" : $"  [{uiWarning}]";
                string fileSuffix = string.IsNullOrEmpty(exportedAt)
                    ? ""
                    : $"\nSaved: {System.IO.Path.GetFileName(exportedAt)}";
                SetStatus($"Success: {rooms} rooms, {entities} entities, seed={seedUsed}{warnSuffix}{fileSuffix}");

                SetButtonsEnabled(generateEnabled: true, startEnabled: true);
            }
            catch (Exception ex)
            {
                SetStatus($"Failed: {ex.Message}", warn: true);
                SetButtonsEnabled(generateEnabled: true, startEnabled: false);
            }
        }

        // ── Start mission (hand off to SceneBuilder) ─────────────────────────

        /// <summary>
        /// Hands the most recently generated scenario to <see cref="SceneBuilder"/>.
        /// Disables both buttons during build; re-enables them on failure.
        /// </summary>
        public void OnStartMissionClicked()
        {
            if (sceneBuilder == null)
            {
                SetStatus("Failed: sceneBuilder reference is not assigned.", warn: true);
                return;
            }
            if (_generatedScenario == null)
            {
                SetStatus("Failed: generate a scenario before starting the mission.", warn: true);
                return;
            }

            SetButtonsEnabled(generateEnabled: false, startEnabled: false);
            SetStatus("Building scene...");
            sceneBuilder.BuildScene(_generatedScenario);
        }

        private void OnSceneBuildComplete(ScenarioData scenario)
        {
            SetStatus($"Mission live: {scenario.scenarioId}");
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void OnSceneBuildFailed(string reason)
        {
            SetStatus($"Failed: {reason}", warn: true);
            SetButtonsEnabled(generateEnabled: true, startEnabled: true);
        }

        // ── Scenario JSON export ─────────────────────────────────────────────

        // Writes the generated scenario to disk under the Assets-side output
        // folder so it shows up in the Project window. Returns the absolute
        // path on success, or null on failure (failures are logged, not thrown,
        // so the success message is still surfaced).
        private static string TryExportScenarioJson(ScenarioData scenario)
        {
            try
            {
                string outDir = System.IO.Path.Combine(
                    Application.dataPath,
                    "Module1_DataModels_and_IO/Output/GeneratedScenarios");
                if (!System.IO.Directory.Exists(outDir))
                    System.IO.Directory.CreateDirectory(outDir);

                string outPath = System.IO.Path.Combine(
                    outDir, $"Scenario_{scenario.scenarioId}.json");
                string written = ScenarioExporter.ExportToFile(scenario, outPath);

#if UNITY_EDITOR
                // Make the new file visible in Unity's Project window immediately.
                UnityEditor.AssetDatabase.Refresh();
#endif
                return written;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EvaluatorConfigPanel] Could not export Scenario.json: {ex.Message}");
                return null;
            }
        }

        // ── PlayerPrefs save / load ──────────────────────────────────────────

        private void SaveConfigToPrefs(ScenarioConfig config)
        {
            try
            {
                string json = JsonConvert.SerializeObject(config,
                    ScenarioJsonSettings.WriterSettings);
                PlayerPrefs.SetString(PREFS_KEY, json);
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EvaluatorConfigPanel] Could not save config: {ex.Message}");
            }
        }

        private bool TryLoadConfigFromPrefs()
        {
            string json = PlayerPrefs.GetString(PREFS_KEY, null);
            if (string.IsNullOrEmpty(json)) return false;

            try
            {
                ScenarioConfig config = JsonConvert.DeserializeObject<ScenarioConfig>(
                    json, ScenarioJsonSettings.ReaderSettings);
                if (config == null) return false;
                ApplyConfigToUi(config);
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[EvaluatorConfigPanel] Could not load saved config: {ex.Message}");
                return false;
            }
        }

        // ── Apply config -> UI (used by load + defaults) ─────────────────────

        private void ApplyConfigToUi(ScenarioConfig config)
        {
            if (config == null) return;
            _suspendCallbacks = true;
            try
            {
                if (missionTypeDropdown != null) missionTypeDropdown.value = 0; // only one option

                if (roomCountMinSlider != null) roomCountMinSlider.value = config.missionStructure.roomCount.min;
                if (roomCountMaxSlider != null) roomCountMaxSlider.value = config.missionStructure.roomCount.max;

                SelectEnum(roomSizeDropdown,          RoomSizeValues,   config.missionStructure.roomSize);
                SelectEnum(layoutTypeDropdown,        LayoutTypeValues, config.missionStructure.layoutType);
                SelectEnum(entryTypeDropdown,         EntryTypeValues,  config.missionStructure.entryType);

                if (terroristCountSlider != null) terroristCountSlider.value = config.entityConfiguration.terroristCount;
                SelectEnum(placementStrategyDropdown, PlacementValues, config.entityConfiguration.placementStrategy);
                SelectEnum(hostageRiskLevelDropdown,  RiskValues,      config.entityConfiguration.hostageRiskLevel);

                if (difficultySlider != null) difficultySlider.value = config.executionControls.difficultyLevel;
                SelectEnum(randomnessDropdown, RandomnessValues, config.executionControls.randomnessLevel);

                if (seedInput      != null) seedInput.text      = config.executionControls.seed.HasValue      ? config.executionControls.seed.Value.ToString()      : "";
                if (timeLimitInput != null) timeLimitInput.text = config.executionControls.timeLimit.HasValue ? config.executionControls.timeLimit.Value.ToString() : "";
                if (customLabelInput != null) customLabelInput.text = config.executionControls.customLabel ?? "";
            }
            finally
            {
                _suspendCallbacks = false;
            }

            UpdateAllLabels();
        }

        private static void SelectEnum<TEnum>(TMP_Dropdown dropdown, TEnum[] values, TEnum target)
            where TEnum : struct
        {
            if (dropdown == null) return;
            for (int i = 0; i < values.Length; i++)
            {
                if (EqualityComparer<TEnum>.Default.Equals(values[i], target))
                {
                    dropdown.value = i;
                    return;
                }
            }
        }

        // ── UI helpers ───────────────────────────────────────────────────────

        private void SetButtonsEnabled(bool generateEnabled, bool startEnabled)
        {
            if (generateButton     != null) generateButton.interactable     = generateEnabled;
            if (startMissionButton != null) startMissionButton.interactable = startEnabled;
        }

        private void SetStatus(string text, bool warn = false)
        {
            if (statusText == null) return;
            statusText.text = text;
            statusText.color = warn ? new Color(0.85f, 0.35f, 0.35f) : Color.white;
        }
    }
}
