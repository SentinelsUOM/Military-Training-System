// Module 4 | Sentinels | University of Moratuwa | 2026

using System;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using TeamSentinels.Module4.Data;
using TeamSentinels.Module4.Logging;
using UnityEngine;
using UnityEngine.Networking;

namespace TeamSentinels.Module4.Export
{
    /// <summary>
    /// MonoBehaviour. Subscribes to SessionLogger.OnSessionComplete and POSTs the
    /// SessionSummary as JSON to the Sentinels AAR Next.js dashboard.
    ///
    /// Endpoint contract (matches app/api/sessions/route.js):
    ///   POST {dashboardUrl}/api/sessions
    ///   Content-Type: application/json
    ///   Body: SessionSummary serialised by Newtonsoft.Json
    ///
    /// The dashboard uses findOneAndUpdate({sessionId}, ..., {upsert:true}),
    /// so re-uploading the same sessionId is safe and idempotent.
    /// </summary>
    public class DashboardUploader : MonoBehaviour
    {
        #region Inspector

        [Tooltip("Base URL of the Next.js dashboard. Examples:\n" +
                 "  PC editor:  http://localhost:3000\n" +
                 "  Quest 3:    http://192.168.1.42:3000  (your laptop's LAN IP)")]
        [SerializeField] private string dashboardBaseUrl = "http://localhost:3000";

        [Tooltip("Toggle uploads off without removing the component. Useful when offline.")]
        [SerializeField] private bool enableUpload = true;

        [Tooltip("HTTP request timeout in seconds.")]
        [SerializeField] private int timeoutSeconds = 15;

        [Tooltip("Log the full JSON payload before sending. Verbose — disable for builds.")]
        [SerializeField] private bool logPayload = false;

        #endregion

        #region Constants

        private const string SessionsRoute = "/api/sessions";

        #endregion

        #region Events

        /// <summary>Fired after each upload attempt. true = HTTP 2xx, false = network/server error.</summary>
        public event Action<bool, string> OnUploadCompleted;

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
        /// Triggers a simulated session and uploads the resulting SessionSummary.
        /// Use from the Inspector context menu to verify the dashboard wiring without a full mission.
        /// </summary>
        [ContextMenu("Upload Test Session")]
        public void UploadTestSession()
        {
            if (SessionLogger.Instance == null)
            {
                Debug.LogError("[DashboardUploader] SessionLogger.Instance is null. " +
                               "Ensure a SessionLogger is present in the scene and the game is playing.");
                return;
            }

            // SimulateTestSession calls EndSession, which fires OnSessionComplete,
            // which calls OnSessionComplete below — the upload happens automatically.
            SessionLogger.Instance.SimulateTestSession();
        }

        #endregion

        #region Private

        private void OnSessionComplete(SessionSummary summary)
        {
            if (!enableUpload)
            {
                Debug.Log("[DashboardUploader] Upload disabled via Inspector. Skipping.");
                return;
            }

            if (summary == null)
            {
                Debug.LogWarning("[DashboardUploader] Received null SessionSummary — skipping upload.");
                return;
            }

            if (string.IsNullOrWhiteSpace(dashboardBaseUrl))
            {
                Debug.LogError("[DashboardUploader] dashboardBaseUrl is empty. Set it in the Inspector.");
                return;
            }

            StartCoroutine(UploadCoroutine(summary));
        }

        private IEnumerator UploadCoroutine(SessionSummary summary)
        {
            string url = dashboardBaseUrl.TrimEnd('/') + SessionsRoute;
            string json;

            try
            {
                json = JsonConvert.SerializeObject(summary, Formatting.None, new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Include,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                });
            }
            catch (Exception ex)
            {
                Debug.LogError("[DashboardUploader] JSON serialisation failed: " + ex.Message);
                OnUploadCompleted?.Invoke(false, "serialisation failed: " + ex.Message);
                yield break;
            }

            if (logPayload)
            {
                Debug.Log("[DashboardUploader] Payload (" + json.Length + " chars):\n" + json);
            }

            byte[] body = Encoding.UTF8.GetBytes(json);

            using (UnityWebRequest req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler   = new UploadHandlerRaw(body) { contentType = "application/json" };
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Accept",       "application/json");
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = Mathf.Max(1, timeoutSeconds);

                Debug.Log("[DashboardUploader] POST " + url + "  (sessionId=" + summary.sessionId + ", " + body.Length + " bytes)");

                yield return req.SendWebRequest();

                bool ok =
#if UNITY_2020_1_OR_NEWER
                    req.result == UnityWebRequest.Result.Success;
#else
                    !req.isNetworkError && !req.isHttpError;
#endif

                if (ok)
                {
                    Debug.Log("[DashboardUploader] Upload OK — HTTP " + req.responseCode +
                              "  response: " + (req.downloadHandler != null ? req.downloadHandler.text : "<none>"));
                    OnUploadCompleted?.Invoke(true, "HTTP " + req.responseCode);
                }
                else
                {
                    string detail =
                        "HTTP " + req.responseCode +
                        "  error: " + req.error +
                        "  body: "  + (req.downloadHandler != null ? req.downloadHandler.text : "<none>");
                    Debug.LogError("[DashboardUploader] Upload FAILED — " + detail);
                    OnUploadCompleted?.Invoke(false, detail);
                }
            }
        }

        #endregion
    }
}
