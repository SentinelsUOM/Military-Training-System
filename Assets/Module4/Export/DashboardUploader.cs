// Module 4 | Sentinels | University of Moratuwa | 2026

using System;
using System.Collections;
using System.IO;
using System.IO.Compression;
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
    /// SessionSummary to the Sentinels AAR Next.js dashboard.
    ///
    /// Endpoint contract (matches app/api/sessions/route.js):
    ///   POST {dashboardUrl}/api/sessions
    ///   Content-Type: application/json
    ///   Content-Encoding: gzip           (body is gzip-compressed)
    ///   Body: SessionSummary serialised by Newtonsoft.Json, then gzipped
    ///
    /// Reliability (added for the Module-2 evaluation study):
    ///   • gzip — a full session is ~300-500 KB of JSON; Unity's UnityWebRequest is
    ///     unreliable pushing that much over HTTPS, so we compress it ~10-25x first.
    ///     The dashboard route transparently gunzips (and still accepts plain JSON).
    ///   • retry — each upload is retried a few times with backoff on network error.
    ///   • offline queue — the gzipped body is written to PendingUploads/ BEFORE the
    ///     first attempt and deleted only on success, so a session is never lost even
    ///     if the app is quit mid-upload or the network is down. On the next launch
    ///     any queued sessions are re-sent.
    ///
    /// The dashboard uses findOneAndUpdate({sessionId}, ..., {upsert:true}), so
    /// re-uploading the same sessionId (live retry + queue flush) is idempotent.
    /// </summary>
    public class DashboardUploader : MonoBehaviour
    {
        #region Inspector

        [Tooltip("Base URL of the Next.js dashboard. Examples:\n" +
                 "  Deployed:   https://military-training-system.vercel.app\n" +
                 "  PC editor:  http://localhost:3000\n" +
                 "  LAN/Quest:  http://192.168.1.42:3000  (your laptop's LAN IP)")]
        [SerializeField] private string dashboardBaseUrl = "https://military-training-system.vercel.app";

        [Tooltip("Toggle uploads off without removing the component. Useful when offline.")]
        [SerializeField] private bool enableUpload = true;

        [Tooltip("HTTP request timeout in seconds.")]
        [SerializeField] private int timeoutSeconds = 20;

        [Tooltip("How many times to retry a failed upload before queuing it for next launch.")]
        [SerializeField] private int maxAttempts = 4;

        [Tooltip("Log the full JSON payload before sending. Verbose — disable for builds.")]
        [SerializeField] private bool logPayload = false;

        #endregion

        #region Constants

        private const string SessionsRoute = "/api/sessions";
        private const string QueueFolder   = "PendingUploads";
        private const string QueueExt      = ".json.gz";

        #endregion

        #region Events

        /// <summary>Fired after each upload finishes. true = stored (HTTP 200), false = queued for retry.</summary>
        public event Action<bool, string> OnUploadCompleted;

        #endregion

        private string QueueDir => Path.Combine(Application.persistentDataPath, QueueFolder);

        #region Unity Lifecycle

        private void Start()
        {
            SessionLogger.OnSessionComplete += OnSessionComplete;
            // Re-send anything that didn't make it out last run (crash / quit / offline).
            if (enableUpload) StartCoroutine(FlushQueue());
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

            SessionLogger.Instance.SimulateTestSession();
        }

        #endregion

        #region Private — dispatch

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
                return;
            }

            if (logPayload)
                Debug.Log("[DashboardUploader] Payload (" + json.Length + " chars):\n" + json);

            byte[] gz = Gzip(Encoding.UTF8.GetBytes(json));

            // Persist to the offline queue BEFORE the first attempt so the session
            // survives an app quit / crash mid-upload.
            string queueFile = SaveToQueue(summary.sessionId, gz);

            StartCoroutine(UploadWithRetry(summary.sessionId, gz, queueFile));
        }

        #endregion

        #region Private — upload

        private IEnumerator UploadWithRetry(string sessionId, byte[] gz, string queueFile)
        {
            for (int attempt = 1; attempt <= Mathf.Max(1, maxAttempts); attempt++)
            {
                bool ok = false;
                string detail = null;
                yield return Post(gz, sessionId, (o, d) => { ok = o; detail = d; });

                if (ok)
                {
                    Debug.Log($"[DashboardUploader] Upload OK (attempt {attempt}) — sid={sessionId}");
                    DeleteFromQueue(queueFile);
                    OnUploadCompleted?.Invoke(true, "HTTP 200");
                    yield break;
                }

                Debug.LogWarning($"[DashboardUploader] Upload attempt {attempt}/{maxAttempts} failed — {detail}");

                if (attempt < maxAttempts)
                    yield return new WaitForSecondsRealtime(attempt * 2.5f); // 2.5s, 5s, 7.5s backoff
            }

            Debug.LogError($"[DashboardUploader] Upload FAILED after {maxAttempts} attempts — sid={sessionId}. " +
                           "Kept in the offline queue; it will be re-sent on the next launch.");
            OnUploadCompleted?.Invoke(false, "queued for next launch");
        }

        /// <summary>Single POST attempt. Reports (success, detail) via the callback.</summary>
        private IEnumerator Post(byte[] gz, string sessionId, Action<bool, string> done)
        {
            string url = dashboardBaseUrl.TrimEnd('/') + SessionsRoute;

            using (UnityWebRequest req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler   = new UploadHandlerRaw(gz) { contentType = "application/json" };
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type",     "application/json");
                req.SetRequestHeader("Content-Encoding", "gzip");
                req.SetRequestHeader("Accept",           "application/json");
                req.timeout = Mathf.Max(1, timeoutSeconds);

                Debug.Log("[DashboardUploader] POST " + url + "  (sid=" + sessionId + ", " + gz.Length + " gz-bytes)");

                yield return req.SendWebRequest();

                bool ok =
#if UNITY_2020_1_OR_NEWER
                    req.result == UnityWebRequest.Result.Success && req.responseCode == 200;
#else
                    !req.isNetworkError && !req.isHttpError && req.responseCode == 200;
#endif
                if (ok)
                {
                    done(true, "HTTP " + req.responseCode);
                }
                else
                {
                    string body = req.downloadHandler != null ? req.downloadHandler.text : "<none>";
                    done(false, "HTTP " + req.responseCode + " error=" + req.error + " body=" + body);
                }
            }
        }

        #endregion

        #region Private — offline queue

        /// <summary>Re-send every session left in the queue from a previous run.</summary>
        private IEnumerator FlushQueue()
        {
            string[] files;
            try
            {
                if (!Directory.Exists(QueueDir)) yield break;
                files = Directory.GetFiles(QueueDir, "*" + QueueExt);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DashboardUploader] Could not read upload queue: " + ex.Message);
                yield break;
            }

            if (files.Length == 0) yield break;
            Debug.Log($"[DashboardUploader] Flushing {files.Length} queued session(s) from a previous run…");

            foreach (string file in files)
            {
                byte[] gz;
                string sid;
                try
                {
                    gz  = File.ReadAllBytes(file);
                    sid = Path.GetFileName(file);
                    if (sid.EndsWith(QueueExt)) sid = sid.Substring(0, sid.Length - QueueExt.Length);
                }
                catch { continue; }

                bool ok = false;
                yield return Post(gz, sid, (o, d) => { ok = o; });
                if (!ok)
                {
                    yield return new WaitForSecondsRealtime(3f);
                    yield return Post(gz, sid, (o, d) => { ok = o; });
                }

                if (ok)
                {
                    Debug.Log("[DashboardUploader] Flushed queued session " + sid);
                    DeleteFromQueue(file);
                }
                else
                {
                    Debug.LogWarning("[DashboardUploader] Queued session " + sid +
                                     " still could not upload; leaving it for the next launch.");
                }
            }
        }

        private string SaveToQueue(string sessionId, byte[] gz)
        {
            try
            {
                Directory.CreateDirectory(QueueDir);
                string path = Path.Combine(QueueDir, SafeName(sessionId) + QueueExt);
                File.WriteAllBytes(path, gz);
                return path;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[DashboardUploader] Could not queue session to disk: " + ex.Message);
                return null;
            }
        }

        private static void DeleteFromQueue(string path)
        {
            if (string.IsNullOrEmpty(path)) return;
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex) { Debug.LogWarning("[DashboardUploader] Could not delete queued file: " + ex.Message); }
        }

        private static string SafeName(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId)) return "session_" + DateTime.UtcNow.Ticks;
            foreach (char c in Path.GetInvalidFileNameChars())
                sessionId = sessionId.Replace(c, '_');
            return sessionId;
        }

        private static byte[] Gzip(byte[] data)
        {
            using (var ms = new MemoryStream())
            {
                using (var gz = new GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal, leaveOpen: true))
                    gz.Write(data, 0, data.Length);
                return ms.ToArray();
            }
        }

        #endregion
    }
}
