// =============================================================================
// ScenarioHttpServer.cs
// Module 1 - Dynamic Scenario Generation (Web Bridge)
// Team Sentinels | University of Moratuwa | 2026
//
// Lightweight in-process HTTP listener that lets an external client (the AAR
// web dashboard running on a PC browser) POST a ScenarioConfig and have Unity
// run the generation + scene build pipeline. Mirrors what EvaluatorConfigPanel
// does from the in-VR canvas, but driven from outside the headset over the LAN.
//
// LOCATION NOTE:
//   Lives at Assets/Module1_DataModels_and_IO/Scripts/SceneBuilder/, same as
//   SceneBuilder.cs - i.e. outside the TeamSentinels.ScenarioGeneration asmdef
//   so it compiles into Assembly-CSharp. Required because it references
//   SceneBuilder (Assembly-CSharp) and ScenarioGenerator (asmdef). The asmdef
//   is auto-referenced into Assembly-CSharp via autoReferenced: true.
//
// THREADING MODEL:
//   - HttpListener runs requests on a background thread.
//   - JSON parse + validation happen on that thread (no Unity API calls).
//   - The actual ScenarioGenerator.Generate / SceneBuilder.BuildScene calls
//     are queued onto the main thread (drained in Update) because Unity APIs
//     are not thread-safe.
//   - The listener thread blocks on a ManualResetEventSlim until the main
//     thread finishes the job, then writes the HTTP response.
//
// ENDPOINTS:
//   GET  /health             -> {"ok":true,"port":8080,"state":"idle"}
//   GET  /status             -> {state, scenarioId, lastError}
//   POST /scenario/generate  body = ScenarioConfig JSON
//                            -> runs generator, exports Scenario.json,
//                               returns {scenarioId, rooms, entities, seedUsed}
//   POST /scenario/start     body = ScenarioConfig JSON
//                            -> generate + SceneBuilder.BuildScene
//   OPTIONS /*               -> 204 with CORS headers (preflight)
// =============================================================================

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using TeamSentinels.ScenarioGeneration.DataModels;
using TeamSentinels.ScenarioGeneration.Generators;
using TeamSentinels.ScenarioGeneration.IO;
using UnityEngine;

namespace TeamSentinels.ScenarioGeneration.Scene
{
    /// <summary>
    /// In-process HTTP server that exposes the Module 1 generation pipeline
    /// to an external evaluator dashboard. Drop this MonoBehaviour into the
    /// bootstrap scene alongside <see cref="SceneBuilder"/>.
    /// </summary>
    public class ScenarioHttpServer : MonoBehaviour
    {
        // ── Inspector ────────────────────────────────────────────────────────

        [Header("Listener")]
        [Tooltip("HTTP port to listen on. Make sure the device firewall and " +
                 "Wi-Fi router (no client isolation) allow this port. NOTE: do NOT use 8080 — " +
                 "the MCP-for-Unity editor bridge owns 8080 on this machine, so the bind would " +
                 "collide and the dashboard can't reach the server.")]
        public int port = 8000;

        [Tooltip("Sent back as Access-Control-Allow-Origin. '*' lets any page " +
                 "reach the server; lock down to your dashboard URL for prod.")]
        public string allowedOrigin = "*";

        [Tooltip("Seconds the listener thread waits for the main-thread pipeline " +
                 "to finish before returning 504. Includes scene build time.")]
        public float pipelineTimeoutSeconds = 30f;

        [Header("Pipeline")]
        [Tooltip("SceneBuilder used by /scenario/start. Auto-found at Start() " +
                 "if left unassigned. /scenario/start returns 503 when missing.")]
        public SceneBuilder sceneBuilder;

        [Tooltip("If true, every successful generation also writes " +
                 "Scenario_<id>.json under Module1_DataModels_and_IO/Output/" +
                 "GeneratedScenarios so the evaluator and Module 4 can replay it.")]
        public bool exportScenarioJson = true;

        // ── State (read by /status from the listener thread) ────────────────

        private volatile string _state = STATE_IDLE; // idle | generating | building | live | error
        private volatile string _lastScenarioId;
        private volatile string _lastError;

        private const string STATE_IDLE       = "idle";
        private const string STATE_GENERATING = "generating";
        private const string STATE_BUILDING   = "building";
        private const string STATE_LIVE       = "live";
        private const string STATE_ERROR      = "error";

        // ── Internals ───────────────────────────────────────────────────────

        private HttpListener _listener;
        private Thread _listenerThread;
        private readonly ConcurrentQueue<Action> _mainThreadJobs = new ConcurrentQueue<Action>();
        private ScenarioGenerator _generator;

        // ── Lifecycle ───────────────────────────────────────────────────────

        private void Start()
        {
            _generator = new ScenarioGenerator();

            if (sceneBuilder == null)
                sceneBuilder = FindObjectOfType<SceneBuilder>();

            if (sceneBuilder != null)
            {
                sceneBuilder.OnSceneBuildComplete += OnSceneBuildComplete;
                sceneBuilder.OnSceneBuildFailed   += OnSceneBuildFailed;
            }

            StartListener();
        }

        private void OnDestroy()
        {
            if (sceneBuilder != null)
            {
                sceneBuilder.OnSceneBuildComplete -= OnSceneBuildComplete;
                sceneBuilder.OnSceneBuildFailed   -= OnSceneBuildFailed;
            }
            StopListener();
        }

        private void OnApplicationQuit() => StopListener();

        private void Update()
        {
            // Drain jobs queued by the listener thread onto the Unity main thread.
            while (_mainThreadJobs.TryDequeue(out Action job))
            {
                try { job(); }
                catch (Exception ex)
                {
                    Debug.LogError($"[ScenarioHttpServer] Main-thread job failed: {ex}");
                }
            }
        }

        // ── SceneBuilder callbacks (main thread) ────────────────────────────

        private void OnSceneBuildComplete(ScenarioData scenario)
        {
            _state = STATE_LIVE;
            _lastScenarioId = scenario.scenarioId;
        }

        private void OnSceneBuildFailed(string reason)
        {
            _state = STATE_ERROR;
            _lastError = reason;
        }

        // ── Listener lifecycle ──────────────────────────────────────────────

        private void StartListener()
        {
            try
            {
                _listener = new HttpListener();
                // http://+:<port>/ binds to all interfaces. On Windows this may
                // require either running as admin or a urlacl entry:
                //   netsh http add urlacl url=http://+:8080/ user=Everyone
                // On Android (Quest) the binding works without elevation.
                _listener.Prefixes.Add($"http://+:{port}/");
                _listener.Start();

                _listenerThread = new Thread(ListenLoop)
                {
                    IsBackground = true,
                    Name = "ScenarioHttpServer",
                };
                _listenerThread.Start();

                Debug.Log($"[ScenarioHttpServer] Listening on port {port}.");
            }
            catch (HttpListenerException ex)
            {
                Debug.LogError($"[ScenarioHttpServer] Could not bind port {port}: " +
                    $"{ex.Message}\nOn Windows run once as admin: " +
                    $"netsh http add urlacl url=http://+:{port}/ user=Everyone");
                _listener = null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ScenarioHttpServer] Listener start failed: {ex}");
                _listener = null;
            }
        }

        private void StopListener()
        {
            try { _listener?.Stop();  } catch { /* swallow */ }
            try { _listener?.Close(); } catch { /* swallow */ }
            _listener = null;

            try { _listenerThread?.Join(500); } catch { /* swallow */ }
            _listenerThread = null;
        }

        private void ListenLoop()
        {
            while (_listener != null && _listener.IsListening)
            {
                HttpListenerContext ctx;
                try
                {
                    ctx = _listener.GetContext();
                }
                catch (HttpListenerException) { return; }   // listener stopped
                catch (ObjectDisposedException) { return; } // listener disposed
                catch (Exception ex)
                {
                    Debug.LogWarning($"[ScenarioHttpServer] GetContext failed: {ex.Message}");
                    continue;
                }

                try
                {
                    HandleRequest(ctx);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[ScenarioHttpServer] Handler threw: {ex}");
                    TryWriteError(ctx, 500, ex.Message);
                }
            }
        }

        // ── Routing ─────────────────────────────────────────────────────────

        private void HandleRequest(HttpListenerContext ctx)
        {
            HttpListenerRequest  req = ctx.Request;
            HttpListenerResponse res = ctx.Response;
            ApplyCors(res);

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 204;
                res.Close();
                return;
            }

            string path = req.Url?.AbsolutePath?.TrimEnd('/') ?? "";
            string route = req.HttpMethod + " " + path;

            switch (route)
            {
                case "GET /health":
                    WriteJson(res, 200, new { ok = true, port, state = _state });
                    return;

                case "GET /status":
                    WriteJson(res, 200, new
                    {
                        state      = _state,
                        scenarioId = _lastScenarioId,
                        lastError  = _lastError,
                    });
                    return;

                case "POST /scenario/generate":
                    HandleGenerate(req, res, alsoBuild: false);
                    return;

                case "POST /scenario/start":
                    HandleGenerate(req, res, alsoBuild: true);
                    return;

                default:
                    WriteJson(res, 404, new { error = "not_found", path });
                    return;
            }
        }

        // ── Generate / Start handler ────────────────────────────────────────

        /// <summary>Extract the evaluation tier ("npcLevel": "dumb|medium|full") from the raw
        /// request JSON. Defaults to Full if absent/unrecognised. Kept off the typed config model
        /// so the generator/validator are untouched.</summary>
        private static AILevel ParseNpcLevel(string body)
        {
            if (string.IsNullOrEmpty(body)) return AILevel.Advanced;
            var m = System.Text.RegularExpressions.Regex.Match(
                body, "\"npcLevel\"\\s*:\\s*\"(\\w+)\"",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!m.Success) return AILevel.Advanced;
            switch (m.Groups[1].Value.ToLowerInvariant())
            {
                // Old aliases (dumb/medium/full) kept so any cached form still resolves.
                case "basic":        case "dumb":   return AILevel.Basic;
                case "intermediate": case "medium": return AILevel.Intermediate;
                default:                            return AILevel.Advanced; // "advanced"/"full"
            }
        }

        /// <summary>Pull a top-level string field ("name":"value") out of the raw request JSON.
        /// Returns null if absent. Kept off the typed config model like ParseNpcLevel.</summary>
        private static string ParseStringField(string body, string field)
        {
            if (string.IsNullOrEmpty(body)) return null;
            var m = System.Text.RegularExpressions.Regex.Match(
                body, "\"" + System.Text.RegularExpressions.Regex.Escape(field) + "\"\\s*:\\s*\"([^\"]+)\"",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value : null;
        }

        private void HandleGenerate(
            HttpListenerRequest req, HttpListenerResponse res, bool alsoBuild)
        {
            // 1. Read body (off the main thread - safe, no Unity APIs).
            string body;
            using (var sr = new StreamReader(req.InputStream, req.ContentEncoding ?? Encoding.UTF8))
                body = sr.ReadToEnd();

            // Module-2 evaluation tags. Read straight from the request body so we don't have to
            // widen the strongly-typed ScenarioConfig model. npcLevel = ablation tier; playerId =
            // participant code that groups the same player's Dumb/Medium/Full plays.
            AILevel npcLevel = ParseNpcLevel(body);
            string  playerId = ParseStringField(body, "playerId");

            // 2. Parse + validate config.
            ScenarioConfig config;
            try
            {
                config = ScenarioConfigLoader.LoadFromJson(body);
            }
            catch (ScenarioConfigValidationException vex)
            {
                WriteJson(res, 400, new
                {
                    error   = "validation_failed",
                    details = vex.ValidationErrors,
                });
                return;
            }
            catch (Exception ex)
            {
                WriteJson(res, 400, new { error = "bad_json", message = ex.Message });
                return;
            }

            if (alsoBuild && sceneBuilder == null)
            {
                WriteJson(res, 503, new { error = "scene_builder_unassigned" });
                return;
            }

            // 3. Run the heavy work on the Unity main thread; block here until done.
            ScenarioData scenario = null;
            string failure = null;

            using (var done = new ManualResetEventSlim(false))
            {
                _mainThreadJobs.Enqueue(() =>
                {
                    try
                    {
                        _state = STATE_GENERATING;
                        scenario = _generator.Generate(config);

                        ValidationResult vr = scenario?.configurationMetadata?.validationResult;
                        if (vr != null && !vr.passed)
                        {
                            failure = "validation_rejected_after_retries: " +
                                string.Join("; ", vr.warnings ?? new List<string>());
                            _state = STATE_ERROR;
                            _lastError = failure;
                            return;
                        }

                        _lastScenarioId = scenario.scenarioId;

                        if (exportScenarioJson)
                            TryExportScenarioJson(scenario);

                        if (alsoBuild)
                        {
                            _state = STATE_BUILDING;
                            // Apply the chosen evaluation tier to every terrorist this build spawns.
                            sceneBuilder.npcAiLevel = npcLevel;
                            // Tag the evaluation context so the SessionSummary (built at mission end)
                            // records who played and at which AI tier — for the dashboard comparison.
                            EvaluationContext.NpcLevel = npcLevel.ToString().ToLowerInvariant();
                            EvaluationContext.PlayerId  = playerId;
                            Debug.Log($"[ScenarioHttpServer] Building scenario at NPC LEVEL = {npcLevel}" +
                                      $"  player={(string.IsNullOrEmpty(playerId) ? "(none)" : playerId)}. " +
                                      $"If NPC LEVEL is wrong, refresh localhost:3000 so the form sends npcLevel.");
                            // BuildScene is synchronous and fires
                            // OnSceneBuildComplete (-> _state = "live") before
                            // returning, so we don't need to touch _state here.
                            sceneBuilder.BuildScene(scenario);
                        }
                        else
                        {
                            _state = STATE_IDLE;
                        }
                    }
                    catch (Exception ex)
                    {
                        failure = ex.Message;
                        _state = STATE_ERROR;
                        _lastError = failure;
                    }
                    finally
                    {
                        done.Set();
                    }
                });

                if (!done.Wait(TimeSpan.FromSeconds(pipelineTimeoutSeconds)))
                {
                    WriteJson(res, 504, new
                    {
                        error   = "pipeline_timeout",
                        timeout = pipelineTimeoutSeconds,
                    });
                    return;
                }
            }

            if (failure != null)
            {
                WriteJson(res, 500, new { error = "pipeline_failed", message = failure });
                return;
            }

            int rooms    = scenario.layout?.rooms?.Count ?? 0;
            int entities = scenario.entities?.Count ?? 0;
            int seedUsed = scenario.configurationMetadata?.seedUsed ?? 0;

            WriteJson(res, 200, new
            {
                ok             = true,
                scenarioId     = scenario.scenarioId,
                rooms,
                entities,
                seedUsed,
                missionStarted = alsoBuild,
            });
        }

        // ── Response helpers ────────────────────────────────────────────────

        private void ApplyCors(HttpListenerResponse res)
        {
            res.Headers["Access-Control-Allow-Origin"]  = allowedOrigin;
            res.Headers["Access-Control-Allow-Methods"] = "GET, POST, OPTIONS";
            res.Headers["Access-Control-Allow-Headers"] = "Content-Type";
        }

        private static void WriteJson(HttpListenerResponse res, int status, object payload)
        {
            try
            {
                string json = JsonConvert.SerializeObject(payload, ScenarioJsonSettings.WriterSettings);
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                res.StatusCode      = status;
                res.ContentType     = "application/json; charset=utf-8";
                res.ContentLength64 = bytes.Length;
                res.OutputStream.Write(bytes, 0, bytes.Length);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ScenarioHttpServer] WriteJson failed: {ex.Message}");
            }
            finally
            {
                try { res.Close(); } catch { /* swallow */ }
            }
        }

        private void TryWriteError(HttpListenerContext ctx, int status, string message)
        {
            try
            {
                ApplyCors(ctx.Response);
                WriteJson(ctx.Response, status, new { error = "internal", message });
            }
            catch { /* best effort */ }
        }

        // Mirrors EvaluatorConfigPanel.TryExportScenarioJson. Runs on the main
        // thread (called from inside the queued lambda), so AssetDatabase.Refresh
        // is safe.
        private static void TryExportScenarioJson(ScenarioData scenario)
        {
            try
            {
                string outDir = Path.Combine(
                    Application.dataPath,
                    "Module1_DataModels_and_IO/Output/GeneratedScenarios");
                if (!Directory.Exists(outDir))
                    Directory.CreateDirectory(outDir);

                string outPath = Path.Combine(outDir, $"Scenario_{scenario.scenarioId}.json");
                ScenarioExporter.ExportToFile(scenario, outPath);

#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ScenarioHttpServer] Could not export Scenario.json: {ex.Message}");
            }
        }
    }
}
