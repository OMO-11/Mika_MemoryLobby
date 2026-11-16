using System;
using System.Diagnostics;
using System.IO;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.LLM;
using GiftCompanion.Core.Memory;
using GiftCompanion.Core.Persistence;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Logger = GiftCompanion.Core.Logging.Logger;

namespace GiftCompanion.Editor
{
    public static class Phase1CoreProbe
    {
        [MenuItem("Gift Companion/Phase 1/Probe Shared Core")]
        public static async void Run()
        {
            string reports = Path.GetFullPath("Docs/Validation/Phase1");
            Directory.CreateDirectory(reports);
            bool passed = false;
            try
            {
                string userData = Path.GetFullPath("UserData");
                string identity = File.ReadAllText("Assets/Characters/MainCharacter/Config/identity.json");
                using var logger = new Logger(Path.Combine(reports, "CoreProbeLogs"));
                var config = new ConfigService(userData, identity, logger);
                await config.InitializeAsync(default);
                using var database = new DatabaseService(Path.Combine(reports, "core_probe.db"), config.Identity.Id, logger);
                await database.InitializeAsync(default); await database.MigrateAsync(default);
                using var llm = new LLMService(LocalSecrets.ReadDeepSeekKey(userData), config.Configuration.Model, config.Configuration.NetworkTimeoutSeconds);
                var core = new CharacterCore(config.Identity, new CharacterState(), database, llm, new MemoryService(), logger);
                int chunks = 0, characters = 0;
                long? firstDeltaMs = null;
                var watch = Stopwatch.StartNew();
                DialogueResult result = null;
                await foreach (var chunk in core.StreamMessageAsync("请仅回复：连接成功。", InteractionSource.Companion, default))
                {
                    if (chunk.IsFinal) result = chunk.Result;
                    else { firstDeltaMs ??= watch.ElapsedMilliseconds; chunks++; characters += chunk.Text.Length; }
                }
                passed = result?.Success == true && characters > 0 && core.State.LastInteractionAt.HasValue;
                File.WriteAllText(Path.Combine(reports, "core_probe.json"), JsonConvert.SerializeObject(new {
                    passed, model = config.Configuration.Model, chunks, characters, firstDeltaMs, elapsedMs = watch.ElapsedMilliseconds,
                    stateCommitted = core.State.LastInteractionAt.HasValue, errorCode = result?.ErrorCode,
                    verifiedAtUtc = DateTimeOffset.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                    scope = "Real CharacterCore -> LLMService -> DeepSeek transport; no response text or credential recorded."
                }, Formatting.Indented));
            }
            catch (Exception error)
            {
                File.WriteAllText(Path.Combine(reports, "core_probe.json"), JsonConvert.SerializeObject(new { passed = false, errorType = error.GetType().Name }));
            }
            if (Application.isBatchMode) EditorApplication.Exit(passed ? 0 : 1);
        }
    }
}
