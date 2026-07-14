// =============================================================================
// EvaluatorCanvasBuilder.cs
// Module 1 - Dynamic Scenario Generation (Editor tooling)
// Team Sentinels | University of Moratuwa | 2026
//
// One-shot editor command that materialises the EvaluatorConfigPanel UI in
// the active scene: Canvas + EventSystem + a vertically stacked panel of
// labelled controls, all auto-wired to a freshly-added EvaluatorConfigPanel
// component. Lives in Assembly-CSharp-Editor (SceneBuilder/Editor/) so it
// can reach Module 2 / SceneBuilder types directly.
// =============================================================================

#if UNITY_EDITOR
using System.Collections.Generic;
using TeamSentinels.ScenarioGeneration.Scene;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace TeamSentinels.ScenarioGeneration.EditorTools
{
    /// <summary>
    /// Builds a working evaluator UI Canvas in the active scene with a single
    /// menu click. Useful for getting from "script exists" to "I can see the
    /// panel" without manually wiring 20+ Inspector fields.
    /// </summary>
    public static class EvaluatorCanvasBuilder
    {
        private const string MenuPath = "Tools/Scenario Generator/Build Evaluator Canvas";

        // Layout constants
        private const float RowHeight    = 36f;
        private const float HeaderHeight = 40f;
        private const float ButtonHeight = 48f;
        private const float StatusHeight = 64f;
        private const float LabelWidth   = 210f;
        private const float ValueWidth   = 60f;
        private const int   RowFontSize  = 15;
        private const int   HeaderFontSize = 17;

        // World-space sizing. Authored in pixels, scaled down to metres, so
        // 1000 x 640 px at 0.0013 becomes a 1.30 x 0.83 m panel - readable at the
        // placer's 1.4 m distance without being so tall the trainee must look up.
        //
        // The form is laid out in TWO COLUMNS with no ScrollView. That is deliberate:
        //  - A ScrollRect's Viewport needs a Mask, and Mask.IsRaycastLocationValid()
        //    does a screen-point test that does not hold up for a world-space canvas
        //    hit by a tracked-device ray, so EVERY control inside it becomes unclickable.
        //  - Anything scrolled out of the viewport is unreachable in VR anyway.
        // Two columns keep all 24 controls on-panel and directly hittable.
        private const float PanelWidth       = 1000f;
        private const float PanelHeight      = 640f;
        private const float WorldCanvasScale = 0.0013f;
        private const float ColumnGap        = 16f;

        private const string EvaluatorCanvasName = "EvaluatorCanvas";
        private const string EvaluatorPanelName  = "EvaluatorPanel";

        [MenuItem(MenuPath, priority = 50)]
        public static void BuildCanvas()
        {
            string sbStatus = BuildCanvasCore();
            EditorUtility.DisplayDialog(
                "Evaluator Canvas Built",
                "Canvas + EvaluatorPanel created and wired.\n\n" +
                sbStatus + "\n\n" +
                "The panel is world-space so it renders in VR. Point a controller at " +
                "it and pull the trigger to use it.",
                "OK");
        }

        /// <summary>
        /// Does the actual building and returns a status line. Split out from
        /// <see cref="BuildCanvas"/> so it can be driven from automation without a
        /// modal dialog blocking the editor.
        /// </summary>
        internal static string BuildCanvasCore()
        {
            // 0. Clean up any previous build (so re-running doesn't pile on duplicates).
            foreach (var existing in Object.FindObjectsOfType<EvaluatorConfigPanel>())
            {
                // Walk up to find the parent EvaluatorCanvas if the panel is inside one,
                // otherwise just destroy the panel itself.
                Transform t = existing.transform;
                while (t.parent != null && t.parent.name != EvaluatorCanvasName) t = t.parent;
                Object.DestroyImmediate(
                    (t.parent != null && t.parent.name == EvaluatorCanvasName)
                        ? t.parent.gameObject
                        : existing.gameObject);
            }
            var staleCanvas = GameObject.Find(EvaluatorCanvasName);
            if (staleCanvas != null) Object.DestroyImmediate(staleCanvas);

            // 1. Always create a dedicated canvas. Do not reuse any pre-existing
            //    canvas - that would nest the panel under unrelated UI.
            //
            //    The canvas MUST be world-space: Unity never renders a screen-space
            //    canvas to an HMD, so a ScreenSpaceOverlay panel is invisible in the
            //    Quest build even though it looks fine in the desktop Game view.
            //    TrackedDeviceGraphicRaycaster is what lets the XR controller ray
            //    click it; a plain GraphicRaycaster only understands a mouse.
            var canvasGo = new GameObject(EvaluatorCanvasName,
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster),
                typeof(TrackedDeviceGraphicRaycaster), typeof(WorldSpacePanelPlacer));
            Canvas canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode  = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;
            canvas.worldCamera = Camera.main;

            // Authored in pixels, scaled down to metres: 640 x 820 px at
            // WorldCanvasScale is a ~0.64 x 0.82 m panel, comfortable to read at
            // the placer's 1.4 m distance.
            var canvasRt = canvasGo.GetComponent<RectTransform>();
            canvasRt.sizeDelta  = new Vector2(PanelWidth, PanelHeight);
            canvasRt.localScale = Vector3.one * WorldCanvasScale;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode          = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor          = 1f;
            scaler.dynamicPixelsPerUnit = 3f; // keeps text crisp up close in VR

            if (Object.FindObjectOfType<EventSystem>() == null)
                EditorApplication.ExecuteMenuItem("GameObject/UI/Event System");

            // 2. Panel root + title + two columns (no ScrollView - see PanelWidth comment)
            GameObject panelGo = CreatePanel(canvas.transform);
            CreateTitle(panelGo, "Scenario Configuration");
            GameObject leftCol, rightCol;
            CreateColumns(panelGo, out leftCol, out rightCol);

            // 3. Build form. Rows are split across the two columns so everything fits
            //    on the panel at once and stays within arm's reach of the ray.
            var built = new BuiltControls();

            AddSectionHeader(leftCol, "Mission Structure");
            built.missionTypeDropdown = AddDropdownRow(leftCol, "Mission Type");
            built.roomCountMinSlider  = AddSliderRow  (leftCol, "Room Count Min", 3, 20, 5, out built.roomCountMinLabel);
            built.roomCountMaxSlider  = AddSliderRow  (leftCol, "Room Count Max", 3, 20, 8, out built.roomCountMaxLabel);
            built.roomSizeDropdown    = AddDropdownRow(leftCol, "Room Size");
            built.layoutTypeDropdown  = AddDropdownRow(leftCol, "Layout Type");
            built.entryTypeDropdown   = AddDropdownRow(leftCol, "Entry Type");

            AddSectionHeader(leftCol, "Entity Configuration");
            built.hostageCountLabel         = AddReadonlyRow(leftCol, "Hostage Count", "1");
            built.terroristCountSlider      = AddSliderRow  (leftCol, "Terrorist Count", 1, 8, 4, out built.terroristCountLabel);
            built.placementStrategyDropdown = AddDropdownRow(leftCol, "Placement Strategy");
            built.hostageRiskLevelDropdown  = AddDropdownRow(leftCol, "Hostage Risk Level");

            AddSectionHeader(rightCol, "Execution Controls");
            built.difficultySlider   = AddSliderRow  (rightCol, "Difficulty", 1, 5, 3, out built.difficultyLabel);
            built.randomnessDropdown = AddDropdownRow(rightCol, "Randomness");
            built.seedInput          = AddInputRow   (rightCol, "Seed (blank = random)");
            built.timeLimitInput     = AddInputRow   (rightCol, "Time Limit (60-1800 s)");
            built.customLabelInput   = AddInputRow   (rightCol, "Custom Label");

            AddSectionHeader(rightCol, "Actions");
            built.statusText         = AddStatusText(rightCol, "Ready");
            built.generateButton     = AddButton(rightCol, "Generate Scenario");
            built.startMissionButton = AddButton(rightCol, "Start Mission");

            // 4. Add controller and wire fields
            var panel = panelGo.AddComponent<EvaluatorConfigPanel>();
            WireFields(panel, panelGo, built);

            // 5. Try to find SceneBuilder in the scene
            var sceneBuilder = Object.FindObjectOfType<SceneBuilder>();
            if (sceneBuilder != null) panel.sceneBuilder = sceneBuilder;

            // 6. Force a layout rebuild now so rows sit correctly in the editor
            //    before Play is even pressed.
            Canvas.ForceUpdateCanvases();
            foreach (var col in new[] { leftCol, rightCol })
            {
                var colRt = col.transform as RectTransform;
                if (colRt != null) LayoutRebuilder.ForceRebuildLayoutImmediate(colRt);
            }

            // 7. Mark scene dirty so the work isn't lost on close
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Selection.activeGameObject = panelGo;
            EditorGUIUtility.PingObject(panelGo);

            return sceneBuilder != null
                ? $"SceneBuilder reference set to '{sceneBuilder.name}'."
                : "No SceneBuilder found in the scene - assign 'Scene Builder' on the panel manually before clicking Start Mission.";
        }

        // ── Construction helpers ──────────────────────────────────────────────

        // Title height is reserved at the top of the panel; ScrollView fills below it.
        private const float TitleAreaHeight = 56f;

        private static GameObject CreatePanel(Transform canvas)
        {
            // Fixed-size panel positioned by absolute anchors. Title and ScrollView
            // are anchored manually inside so we don't depend on a layout group
            // sizing them correctly across Unity versions.
            var go = new GameObject(EvaluatorPanelName,
                typeof(RectTransform), typeof(Image));
            go.transform.SetParent(canvas, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            rt.anchoredPosition = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = new Color(0.06f, 0.08f, 0.11f, 1f); // fully opaque dark

            return go;
        }

        /// <summary>
        /// Two side-by-side columns filling the panel below the title. Plain
        /// RectTransforms with a VerticalLayoutGroup - deliberately no ScrollRect,
        /// Viewport or Mask, since a Mask silently blocks tracked-device ray casts
        /// to everything beneath it on a world-space canvas.
        /// </summary>
        private static void CreateColumns(GameObject panel, out GameObject left, out GameObject right)
        {
            left  = CreateColumn(panel, "ColumnLeft",  0f,   0.5f, ColumnGap * 0.5f);
            right = CreateColumn(panel, "ColumnRight", 0.5f, 1f,   ColumnGap * 0.5f);
        }

        private static GameObject CreateColumn(GameObject panel, string name,
                                               float anchorXMin, float anchorXMax, float innerPad)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(panel.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(anchorXMin, 0f);
            rt.anchorMax = new Vector2(anchorXMax, 1f);
            rt.pivot     = new Vector2(0.5f, 1f);
            // Leave room for the title strip at the top and a margin all round.
            rt.offsetMin = new Vector2(12f + innerPad, 12f);
            rt.offsetMax = new Vector2(-(12f + innerPad), -TitleAreaHeight);

            var vlg = go.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(6, 6, 4, 4);
            vlg.spacing = 5;
            vlg.childAlignment         = TextAnchor.UpperCenter;
            vlg.childControlWidth      = true;
            vlg.childControlHeight     = false;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            return go;
        }

        private static void CreateTitle(GameObject panel, string title)
        {
            // Anchored to the top of the panel, full width minus padding.
            var go = new GameObject("Title", typeof(RectTransform));
            go.transform.SetParent(panel.transform, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot     = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2(0, -12);
            rt.sizeDelta = new Vector2(-24, 40);

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text      = title;
            text.fontSize  = 22;
            text.fontStyle = FontStyles.Bold;
            text.color     = new Color(0.95f, 0.95f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
        }

        private static void AddSectionHeader(GameObject parent, string title)
        {
            var go = new GameObject($"Header_{title}", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent.transform, false);
            go.GetComponent<LayoutElement>().minHeight = HeaderHeight;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text     = title;
            text.fontSize = HeaderFontSize;
            text.fontStyle = FontStyles.Bold;
            text.color    = new Color(0.55f, 0.78f, 1f, 1f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            // Static text must never swallow the controller ray - otherwise it sits
            // on top of the control next to it and eats the click.
            text.raycastTarget = false;
        }

        private static GameObject CreateRow(GameObject parent, string name)
        {
            var row = new GameObject(name,
                typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(parent.transform, false);

            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8;
            hlg.childControlWidth      = true;
            hlg.childControlHeight     = true;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            hlg.childAlignment         = TextAnchor.MiddleLeft;

            row.GetComponent<LayoutElement>().minHeight = RowHeight;
            return row;
        }

        private static TMP_Text AddRowLabel(GameObject row, string labelText)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(row.transform, false);
            go.GetComponent<LayoutElement>().preferredWidth = LabelWidth;
            go.GetComponent<LayoutElement>().minHeight      = RowHeight;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text     = labelText;
            text.fontSize = RowFontSize;
            text.color    = new Color(0.92f, 0.92f, 0.92f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;   // see AddSectionHeader
            return text;
        }

        // Run a UI menu command and ensure the created GameObject ends up as a
        // child of `expectedParent`. Unity's menu commands sometimes attach the
        // new UI element under the nearest Canvas instead of the selected object,
        // especially when the selected object lives under a Mask - this normalises
        // that behaviour.
        private static GameObject ExecuteMenuAndAttach(string menuPath, GameObject expectedParent)
        {
            // Snapshot existing children of the canvas root and the expected parent
            // so we can locate the genuinely new GameObject afterwards.
            Transform canvasRoot = expectedParent.GetComponentInParent<Canvas>()?.transform
                                   ?? expectedParent.transform.root;

            var preexisting = new HashSet<Transform>();
            CollectTransforms(canvasRoot, preexisting);

            Selection.activeGameObject = expectedParent;
            if (!EditorApplication.ExecuteMenuItem(menuPath))
            {
                Debug.LogError($"[EvaluatorCanvasBuilder] Menu '{menuPath}' not found.");
                return null;
            }

            // Find the first new transform under the canvas root.
            GameObject created = null;
            foreach (Transform t in canvasRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!preexisting.Contains(t))
                {
                    created = t.gameObject;
                    break;
                }
            }
            if (created == null) created = Selection.activeGameObject;
            if (created == null || created == expectedParent)
            {
                Debug.LogError($"[EvaluatorCanvasBuilder] '{menuPath}' did not produce a new GameObject.");
                return null;
            }

            // Force the new element to live under expectedParent.
            if (created.transform.parent != expectedParent.transform)
                created.transform.SetParent(expectedParent.transform, worldPositionStays: false);

            return created;
        }

        private static void CollectTransforms(Transform root, HashSet<Transform> set)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                set.Add(t);
        }

        // Slider with a value-display label on the right.
        private static Slider AddSliderRow(GameObject parent, string label,
                                           int min, int max, int defaultValue,
                                           out TMP_Text valueLabel)
        {
            GameObject row = CreateRow(parent, $"Row_{label}");
            AddRowLabel(row, label);

            GameObject sliderGo = ExecuteMenuAndAttach("GameObject/UI/Slider", row);
            if (sliderGo == null) { valueLabel = null; return null; }
            sliderGo.name = $"Slider_{label}";

            var sliderLe = sliderGo.GetComponent<LayoutElement>() ?? sliderGo.AddComponent<LayoutElement>();
            sliderLe.flexibleWidth  = 1;
            sliderLe.preferredHeight = RowHeight;
            sliderLe.minHeight       = RowHeight;

            var slider = sliderGo.GetComponent<Slider>();
            if (slider != null)
            {
                slider.wholeNumbers = true;
                slider.minValue = min;
                slider.maxValue = max;
                slider.value    = defaultValue;
            }

            // Value label
            var valGo = new GameObject($"Value_{label}", typeof(RectTransform), typeof(LayoutElement));
            valGo.transform.SetParent(row.transform, false);
            var valLe = valGo.GetComponent<LayoutElement>();
            valLe.preferredWidth = ValueWidth;
            valLe.minHeight      = RowHeight;

            valueLabel = valGo.AddComponent<TextMeshProUGUI>();
            valueLabel.text     = defaultValue.ToString();
            valueLabel.fontSize = RowFontSize;
            valueLabel.color    = Color.white;
            valueLabel.alignment = TextAlignmentOptions.MidlineRight;

            return slider;
        }

        private static TMP_Dropdown AddDropdownRow(GameObject parent, string label)
        {
            GameObject row = CreateRow(parent, $"Row_{label}");
            AddRowLabel(row, label);

            GameObject ddGo = ExecuteMenuAndAttach("GameObject/UI/Dropdown - TextMeshPro", row);
            if (ddGo == null) return null;
            ddGo.name = $"Dropdown_{label}";

            var le = ddGo.GetComponent<LayoutElement>() ?? ddGo.AddComponent<LayoutElement>();
            le.flexibleWidth   = 1;
            le.preferredHeight = RowHeight;
            le.minHeight       = RowHeight;
            le.minWidth        = 200;

            return ddGo.GetComponent<TMP_Dropdown>();
        }

        private static TMP_InputField AddInputRow(GameObject parent, string label)
        {
            GameObject row = CreateRow(parent, $"Row_{label}");
            AddRowLabel(row, label);

            GameObject inputGo = ExecuteMenuAndAttach("GameObject/UI/Input Field - TextMeshPro", row);
            if (inputGo == null) return null;
            inputGo.name = $"Input_{label}";

            var le = inputGo.GetComponent<LayoutElement>() ?? inputGo.AddComponent<LayoutElement>();
            le.flexibleWidth   = 1;
            le.preferredHeight = RowHeight;
            le.minHeight       = RowHeight;
            le.minWidth        = 200;

            return inputGo.GetComponent<TMP_InputField>();
        }

        private static TMP_Text AddReadonlyRow(GameObject parent, string label, string value)
        {
            GameObject row = CreateRow(parent, $"Row_{label}");
            AddRowLabel(row, label);

            var go = new GameObject($"Readonly_{label}", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(row.transform, false);
            var le = go.GetComponent<LayoutElement>();
            le.flexibleWidth = 1;
            le.minHeight     = RowHeight;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text     = value;
            text.fontSize = RowFontSize;
            text.color    = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            return text;
        }

        private static TMP_Text AddStatusText(GameObject parent, string initial)
        {
            var go = new GameObject("StatusText", typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(parent.transform, false);
            go.GetComponent<LayoutElement>().minHeight = StatusHeight;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.text     = initial;
            text.fontSize = RowFontSize;
            text.color    = Color.white;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableWordWrapping = true;
            return text;
        }

        private static Button AddButton(GameObject parent, string caption)
        {
            GameObject btnGo = ExecuteMenuAndAttach("GameObject/UI/Button - TextMeshPro", parent);
            if (btnGo == null) return null;
            btnGo.name = $"Button_{caption}";

            var le = btnGo.GetComponent<LayoutElement>() ?? btnGo.AddComponent<LayoutElement>();
            le.minHeight       = ButtonHeight;
            le.preferredHeight = ButtonHeight;
            le.flexibleWidth   = 1;

            var label = btnGo.GetComponentInChildren<TMP_Text>();
            if (label != null)
            {
                label.text      = caption;
                label.fontSize  = 16;
                label.fontStyle = FontStyles.Bold;
                label.color     = Color.black;
            }

            return btnGo.GetComponent<Button>();
        }

        // ── Wiring ────────────────────────────────────────────────────────────

        private struct BuiltControls
        {
            public TMP_Dropdown   missionTypeDropdown;
            public Slider         roomCountMinSlider;
            public TMP_Text       roomCountMinLabel;
            public Slider         roomCountMaxSlider;
            public TMP_Text       roomCountMaxLabel;
            public TMP_Dropdown   roomSizeDropdown;
            public TMP_Dropdown   layoutTypeDropdown;
            public TMP_Dropdown   entryTypeDropdown;

            public TMP_Text       hostageCountLabel;
            public Slider         terroristCountSlider;
            public TMP_Text       terroristCountLabel;
            public TMP_Dropdown   placementStrategyDropdown;
            public TMP_Dropdown   hostageRiskLevelDropdown;

            public Slider         difficultySlider;
            public TMP_Text       difficultyLabel;
            public TMP_Dropdown   randomnessDropdown;
            public TMP_InputField seedInput;
            public TMP_InputField timeLimitInput;
            public TMP_InputField customLabelInput;

            public TMP_Text       statusText;
            public Button         generateButton;
            public Button         startMissionButton;
        }

        private static void WireFields(EvaluatorConfigPanel panel,
                                       GameObject panelRoot,
                                       BuiltControls c)
        {
            panel.missionTypeDropdown       = c.missionTypeDropdown;
            panel.roomCountMinSlider        = c.roomCountMinSlider;
            panel.roomCountMaxSlider        = c.roomCountMaxSlider;
            panel.roomCountMinLabel         = c.roomCountMinLabel;
            panel.roomCountMaxLabel         = c.roomCountMaxLabel;
            panel.roomSizeDropdown          = c.roomSizeDropdown;
            panel.layoutTypeDropdown        = c.layoutTypeDropdown;
            panel.entryTypeDropdown         = c.entryTypeDropdown;

            panel.hostageCountLabel         = c.hostageCountLabel;
            panel.terroristCountSlider      = c.terroristCountSlider;
            panel.terroristCountLabel       = c.terroristCountLabel;
            panel.placementStrategyDropdown = c.placementStrategyDropdown;
            panel.hostageRiskLevelDropdown  = c.hostageRiskLevelDropdown;

            panel.difficultySlider          = c.difficultySlider;
            panel.difficultyLabel           = c.difficultyLabel;
            panel.randomnessDropdown        = c.randomnessDropdown;
            panel.seedInput                 = c.seedInput;
            panel.timeLimitInput            = c.timeLimitInput;
            panel.customLabelInput          = c.customLabelInput;

            panel.generateButton            = c.generateButton;
            panel.startMissionButton        = c.startMissionButton;
            panel.statusText                = c.statusText;

            // Intentionally NOT setting panel.panelRoot. When that field is
            // assigned, EvaluatorConfigPanel hides the panel after a successful
            // build, which makes the "Mission live" status invisible. Leaving it
            // null keeps the panel up so the evaluator can read the result.
            // panel.panelRoot = panelRoot;
        }
    }
}
#endif
