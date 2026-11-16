using System;
using System.IO;
using System.Net.Http;
using System.Diagnostics;
using GiftCompanion.Core.LLM;
using GiftCompanion.Core.Config;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GiftCompanion.Editor
{
    public static class DeepSeekPhase0Probe
    {
        [MenuItem("Gift Companion/Phase 0/Probe DeepSeek API")]
        public static async void Run()
        {
            string report = "Docs/Validation/Phase0/deepseek_probe.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(report));
            string key;
            try { key = LocalSecrets.ReadDeepSeekKey(Path.GetFullPath("UserData")); }
            catch (Exception error)
            {
                File.WriteAllText(report, $"NOT RUN: Local key could not be read ({error.GetType().Name}).\n");
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }
            if (string.IsNullOrWhiteSpace(key))
            {
                File.WriteAllText(report, "NOT RUN: No local DeepSeek API Key. Run Tools/Configure-DeepSeek.ps1.\n");
                Debug.Log("DeepSeek Phase 0 probe needs a locally configured API Key.");
                if (Application.isBatchMode) EditorApplication.Exit(2);
                return;
            }
            try
            {
                using var http = new HttpClient();
                var api = new DeepSeekApiClient(http, key);
                var elapsed = Stopwatch.StartNew();
                long? firstDeltaMs = null;
                int chunks = 0, characters = 0;
                await foreach (string text in api.StreamAsync("请仅回复：连接成功。"))
                {
                    if (!firstDeltaMs.HasValue) firstDeltaMs = elapsed.ElapsedMilliseconds;
                    chunks++;
                    characters += text.Length;
                }
                elapsed.Stop();
                if (characters == 0) throw new InvalidDataException("DeepSeek returned no text.");
                File.WriteAllText(report, $"PASSED: model=deepseek-flash chunks={chunks} characters={characters} firstDeltaMs={firstDeltaMs} elapsedMs={elapsed.ElapsedMilliseconds}\n");
                File.WriteAllText(Path.ChangeExtension(report, ".json"), JsonConvert.SerializeObject(new {
                    passed = true, verifiedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                    model = "deepseek-flash", endpoint = DeepSeekApiClient.Endpoint.AbsoluteUri,
                    chunks, characters, firstDeltaMs, elapsedMs = elapsed.ElapsedMilliseconds,
                    streamCompletedOnDone = true, unityVersion = Application.unityVersion,
                    scope = "Real authenticated HTTPS request and SSE completion; response text and API Key are not recorded."
                }, Formatting.Indented));
                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                File.WriteAllText(report, $"FAILED: {error.GetType().Name}\n");
                File.WriteAllText(Path.ChangeExtension(report, ".json"), JsonConvert.SerializeObject(new {
                    passed = false, verifiedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                    model = "deepseek-flash", errorType = error.GetType().Name,
                    scope = "Real API probe failed; no response body or credential recorded."
                }, Formatting.Indented));
                Debug.LogError($"DeepSeek probe failed: {error.GetType().Name}");
                if (Application.isBatchMode) EditorApplication.Exit(1);
            }
        }
    }
}
