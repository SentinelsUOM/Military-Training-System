// Module 4 | Sentinels | University of Moratuwa | 2026

using System;
using System.IO;
using TeamSentinels.Module4.Data;
using TeamSentinels.Module4.Logging;
using UnityEngine;

namespace TeamSentinels.Module4.Export
{
    /// <summary>
    /// MonoBehaviour. Subscribes to SessionLogger.OnSessionComplete and writes a
    /// self-contained HTML after-action report to persistent storage.
    /// Output: Application.persistentDataPath/AARReports/report_{sessionId}.html
    /// </summary>
    public class WebReportExporter : MonoBehaviour
    {
        #region Data

        private const string ReportSubfolder = "AARReports";

        #endregion

        #region Events

        /// <summary>Fired after the HTML file has been successfully written to disk.</summary>
        public event Action<string> OnReportExported;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            SessionLogger.OnSessionComplete += OnSessionComplete;
        }

        private void OnDestroy()
        {
            SessionLogger.OnSessionComplete -= OnSessionComplete;
        }

        #endregion

        #region Public API

        /// <summary>
        /// Triggers a simulated session via SessionLogger and immediately exports the report.
        /// Use this from the Inspector context menu for testing without running the full mission.
        /// </summary>
        [ContextMenu("Export Test Report")]
        public void ExportTestReport()
        {
            if (SessionLogger.Instance == null)
            {
                Debug.LogError("[WebReportExporter] SessionLogger.Instance is null. " +
                               "Ensure a SessionLogger is present in the scene and the game is playing.");
                return;
            }

            // SimulateTestSession calls EndSession internally, which fires OnSessionComplete,
            // which triggers OnSessionComplete below — the export happens automatically.
            SessionLogger.Instance.SimulateTestSession();
        }

        #endregion

        #region Private

        private void OnSessionComplete(SessionSummary summary)
        {
            if (summary == null)
            {
                Debug.LogWarning("[WebReportExporter] Received null SessionSummary — skipping export.");
                return;
            }

            try
            {
                string html     = HtmlTemplateBuilder.Build(summary);
                string fullPath = WriteReport(summary.sessionId, html);

                Debug.Log("AAR Report saved to: " + fullPath);
                OnReportExported?.Invoke(fullPath);
            }
            catch (Exception ex)
            {
                Debug.LogError("[WebReportExporter] Failed to write report: " + ex.Message);
            }
        }

        private static string WriteReport(string sessionId, string html)
        {
            string dir = Path.Combine(Application.persistentDataPath, ReportSubfolder);
            Directory.CreateDirectory(dir);

            string safeId = string.IsNullOrEmpty(sessionId) ? "unknown" : sessionId;
            string path   = Path.Combine(dir, $"report_{safeId}.html");

            File.WriteAllText(path, html, System.Text.Encoding.UTF8);
            return path;
        }

        #endregion
    }
}
