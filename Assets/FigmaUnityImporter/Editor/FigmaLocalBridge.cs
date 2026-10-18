using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Stanislaw.FigmaUnityImporter.Editor
{
    [InitializeOnLoad]
    internal static class FigmaLocalBridge
    {
        private static readonly ConcurrentQueue<string> PendingImports = new ConcurrentQueue<string>();
        private static HttpListener listener;
        private static Thread listenerThread;
        private static bool autoStartEnabled = true;
        private static bool isStarting;
        private static double nextStartAttempt;
        private static int importInProgress;

        public static bool IsRunning => listener?.IsListening == true;
        public static bool IsImporting => Volatile.Read(ref importInProgress) != 0;
        public static int PendingCount => PendingImports.Count;
        public static float Progress { get; private set; }
        public static string LastMessage { get; private set; } = "Waiting for a Frame from Figma.";
        public static event Action Changed;

        static FigmaLocalBridge()
        {
            InstallLifecycleHooks();
        }

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            InstallLifecycleHooks();
        }

        private static void InstallLifecycleHooks()
        {
            autoStartEnabled = true;
            EditorApplication.update -= EnsureRunning;
            EditorApplication.update += EnsureRunning;
            EditorApplication.update -= DrainQueue;
            EditorApplication.update += DrainQueue;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting -= Shutdown;
            EditorApplication.quitting += Shutdown;
            EditorApplication.delayCall -= Start;
            EditorApplication.delayCall += Start;
        }

        public static void Start()
        {
            autoStartEnabled = true;
            if (IsRunning || isStarting) return;
            isStarting = true;
            try
            {
                ShutdownListener();
                listener = new HttpListener();
                listener.Prefixes.Add($"http://localhost:{FigmaBridgeSettings.Port}/");
                listener.Start();
                listenerThread = new Thread(ListenLoop) { IsBackground = true, Name = "Figma Unity Bridge" };
                listenerThread.Start();
                SetMessage($"Listening on localhost:{FigmaBridgeSettings.Port}");
                Debug.Log($"[Figma Importer] Local bridge listening on http://localhost:{FigmaBridgeSettings.Port}/");
            }
            catch (Exception exception)
            {
                ShutdownListener();
                nextStartAttempt = EditorApplication.timeSinceStartup + 2d;
                SetMessage("Bridge could not start: " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                isStarting = false;
            }
        }

        public static void Stop()
        {
            autoStartEnabled = false;
            ShutdownListener();
            SetMessage("Local bridge stopped.");
        }

        private static void Shutdown()
        {
            ShutdownListener();
        }

        private static void ShutdownListener()
        {
            try { listener?.Stop(); } catch { /* ignored during domain reload */ }
            try { listener?.Close(); } catch { /* ignored during domain reload */ }
            listener = null;
            listenerThread = null;
        }

        private static void EnsureRunning()
        {
            if (!autoStartEnabled || IsRunning || isStarting || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            if (EditorApplication.timeSinceStartup < nextStartAttempt) return;
            Start();
        }

        private static void ListenLoop()
        {
            while (IsRunning)
            {
                try
                {
                    var context = listener.GetContext();
                    Handle(context);
                }
                catch (HttpListenerException) { }
                catch (ObjectDisposedException) { }
                catch (Exception exception) { Debug.LogException(exception); }
            }
        }

        private static void Handle(HttpListenerContext context)
        {
            AddCorsHeaders(context.Response);
            if (context.Request.HttpMethod == "OPTIONS")
            {
                context.Response.StatusCode = 204;
                context.Response.Close();
                return;
            }

            if (context.Request.HttpMethod == "GET" && context.Request.Url?.AbsolutePath == "/status")
            {
                var busy = IsImporting || !PendingImports.IsEmpty;
                WriteResponse(context.Response, 200, JsonConvert.SerializeObject(new
                {
                    ok = true,
                    busy,
                    progress = busy ? Progress : 0f,
                    message = LastMessage
                }));
                return;
            }

            if (context.Request.HttpMethod != "POST" || context.Request.Url?.AbsolutePath != "/import")
            {
                WriteResponse(context.Response, 404, "{\"ok\":false,\"message\":\"Use POST /import\"}");
                return;
            }

            // Apply backpressure before reading another large base64 request.
            // Figma retries this response while Unity imports the previous Frame.
            if (IsImporting || !PendingImports.IsEmpty)
            {
                context.Response.Headers["Retry-After"] = "1";
                WriteResponse(context.Response, 429,
                    "{\"ok\":false,\"busy\":true,\"message\":\"Unity is importing the previous Frame\"}");
                return;
            }

            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding ?? Encoding.UTF8);
            var json = reader.ReadToEnd();
            if (string.IsNullOrWhiteSpace(json))
            {
                WriteResponse(context.Response, 400, "{\"ok\":false,\"message\":\"Empty payload\"}");
                return;
            }

            PendingImports.Enqueue(json);
            WriteResponse(context.Response, 202, "{\"ok\":true,\"message\":\"Queued in Unity\"}");
        }

        private static void DrainQueue()
        {
            if (IsImporting || !PendingImports.TryDequeue(out var json)) return;
            Interlocked.Exchange(ref importInProgress, 1);
            Progress = 0f;
            try
            {
                var diagnosticsDirectory = Path.Combine("Library", "FigmaUnityImporter");
                Directory.CreateDirectory(diagnosticsDirectory);
                File.WriteAllText(Path.Combine(diagnosticsDirectory, "last-import.json"), json);
                ReportProgress(0.01f, "Reading queued Frame");
                var payload = JsonConvert.DeserializeObject<FigmaImportPayload>(json);
                json = null;
                if (payload == null || payload.protocolVersion != 9)
                    throw new InvalidDataException(
                        $"Unsupported Figma plugin protocol {payload?.protocolVersion}. " +
                        "Reopen the registered Tools/FigmaUnityImporter development plugin (expected v9).");
                var roots = FigmaSceneBuilder.BuildMany(payload, ReportProgress);
                var names = string.Join("', '", roots.Select(root => root.name));
                var removedScripts = FigmaScriptCleanup.RemoveAllFromActiveSceneUi();
                SetMessage($"Imported {roots.Count} frame(s) and removed {removedScripts} Figma script(s) " +
                           $"at {DateTime.Now:T}. Waiting for the next Frame.");
                Debug.Log($"[Figma Importer] Imported {roots.Count} frame(s): '{names}'. " +
                          $"Removed {removedScripts} Figma script(s) from the scene.");
            }
            catch (Exception exception)
            {
                SetMessage("Import failed: " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                Progress = 0f;
                Interlocked.Exchange(ref importInProgress, 0);
            }
        }

        private static void ReportProgress(float progress, string stage)
        {
            Progress = Mathf.Clamp01(progress);
            LastMessage = $"Importing one Frame — {Mathf.RoundToInt(Progress * 100f)}% — {stage}";
            if (EditorUtility.DisplayCancelableProgressBar("Figma Frame → Unity", LastMessage, Progress))
                throw new OperationCanceledException("Figma Frame import cancelled.");
        }

        private static void AddCorsHeaders(HttpListenerResponse response)
        {
            response.Headers["Access-Control-Allow-Origin"] = "*";
            response.Headers["Access-Control-Allow-Methods"] = "POST, OPTIONS";
            response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
            response.Headers["Access-Control-Allow-Private-Network"] = "true";
            response.Headers["Access-Control-Max-Age"] = "600";
        }

        private static void WriteResponse(HttpListenerResponse response, int status, string body)
        {
            var bytes = Encoding.UTF8.GetBytes(body);
            response.StatusCode = status;
            response.ContentType = "application/json; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            response.OutputStream.Write(bytes, 0, bytes.Length);
            response.Close();
        }

        private static void SetMessage(string message)
        {
            LastMessage = message;
            EditorApplication.delayCall += () => Changed?.Invoke();
        }
    }
}
