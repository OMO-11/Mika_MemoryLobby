using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GiftCompanion.Core.Config
{
    public sealed class AppConfiguration
    {
        public string Model { get; set; } = "deepseek-flash";
        public int NetworkTimeoutSeconds { get; set; } = 30;
        public string PetHotkey { get; set; } = "Ctrl+Alt+Space";
        public float BgmVolume { get; set; } = .35f;
        public bool BgmMuted { get; set; }
        public float SubtitleFontSize { get; set; } = 26;
        public bool VoiceEnabled { get; set; } = true;
        public float VoiceVolume { get; set; } = .75f;
        public string VoiceLanguage { get; set; } = "ja";
        public int VoicePreferencesVersion { get; set; } = 2;
        public AppConfiguration Copy() => (AppConfiguration)MemberwiseClone();
        // API keys stay outside the public configuration; LocalSecrets reads the local DPAPI file.
    }

    public interface IConfigService
    {
        AppConfiguration Configuration { get; }
        CharacterIdentity Identity { get; }
        CompanionDialoguePolicy CompanionPolicy { get; }
        IReadOnlyList<string> BgmResources { get; }
        string UserDataPath { get; }
        Task InitializeAsync(CancellationToken ct);
        Task SaveAsync(AppConfiguration configuration, CancellationToken ct);
        Task SaveCharacterCardAsync(CharacterIdentity identity, CancellationToken ct);
        Task ReloadCharacterCardAsync(CancellationToken ct);
        string CharacterCardPath { get; }
        string CharacterCardIssue { get; }
    }

    public sealed class ConfigService : IConfigService
    {
        private readonly string identityJson;
        private readonly ILogger logger;
        private readonly SemaphoreSlim saving = new SemaphoreSlim(1, 1);
        public AppConfiguration Configuration { get; private set; } = new AppConfiguration();
        public bool UsedDefaultsAfterInvalidFile { get; private set; }
        public CharacterIdentity Identity { get; private set; }
        public CompanionDialoguePolicy CompanionPolicy { get; }
        public IReadOnlyList<string> BgmResources { get; }
        public string UserDataPath { get; }
        public string CharacterCardPath => Path.Combine(UserDataPath, "Config", "character_card.json");
        public string CharacterCardIssue { get; private set; }

        public ConfigService(string userDataPath, string identityJson, ILogger logger = null, string characterManifest = null)
        {
            UserDataPath = userDataPath;
            this.identityJson = identityJson;
            this.logger = logger;
            var manifest = string.IsNullOrEmpty(characterManifest) ? new JObject() : JsonConvert.DeserializeObject<JObject>(characterManifest, new JsonSerializerSettings {MaxDepth=32});
            var actions = manifest?["companion"]?["actions"] as JObject;
            CompanionPolicy = new CompanionDialoguePolicy(actions?.Properties().Where(p => p.Value.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)p.Value)).Select(p => p.Name));
            BgmResources = (manifest?["bgmPlaylist"] as JArray ?? new JArray()).Take(16).Select(x => x.Type == JTokenType.String ? (string)x : (x as JObject)?.Value<string>("resource"))
                .Where(x => !string.IsNullOrWhiteSpace(x) && System.Text.RegularExpressions.Regex.IsMatch(x, "^[A-Za-z0-9_/-]{1,120}$")).ToArray();
        }

        public Task InitializeAsync(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            foreach (string folder in new[] { "Config", "AudioCache", "Logs" })
                Directory.CreateDirectory(Path.Combine(UserDataPath, folder));

            var identity = JsonConvert.DeserializeObject<CharacterIdentity>(identityJson);
            if (identity == null || string.IsNullOrWhiteSpace(identity.Id) ||
                string.IsNullOrWhiteSpace(identity.DisplayName))
                throw new InvalidDataException("Character identity asset is missing required fields.");
            Identity = identity;
            AtomicDocument.Recover(CharacterCardPath);
            if (!File.Exists(CharacterCardPath)) AtomicDocument.WriteJson(CharacterCardPath, identity, ct);
            else LoadCharacterCard();
            string settingsPath = Path.Combine(UserDataPath, "Config", "settings.json");
            AtomicDocument.Recover(settingsPath);
            if (File.Exists(settingsPath))
            {
                try
                {
                    var configuration = JsonConvert.DeserializeObject<AppConfiguration>(File.ReadAllText(settingsPath),
                        new JsonSerializerSettings { MaxDepth = 16 });
                    Validate(configuration);
                    var document = Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(settingsPath));
                    if (!document.TryGetValue("VoicePreferencesVersion", StringComparison.OrdinalIgnoreCase, out var version) || version.Value<int>() < 2)
                    {
                        // The player requested a Japanese default for this release. Apply once;
                        // subsequent explicit language choices persist normally.
                        configuration.VoiceLanguage = "ja"; configuration.VoicePreferencesVersion = 2;
                        AtomicDocument.WriteJson(settingsPath, configuration, ct);
                    }
                    Configuration = configuration;
                }
                catch (Exception error) when (error is JsonException || error is InvalidDataException)
                {
                    UsedDefaultsAfterInvalidFile = true;
                    logger?.Error("config invalid defaults=true original_preserved=true", error);
                }
            }
            logger?.Info("config initialized");
            return Task.CompletedTask;
        }

        public async Task SaveAsync(AppConfiguration configuration, CancellationToken ct)
        {
            Validate(configuration);
            var copy = configuration.Copy();
            await saving.WaitAsync(ct);
            string path = Path.Combine(UserDataPath, "Config", "settings.json");
            try
            {
                AtomicDocument.WriteJson(path, copy, ct);
                Configuration = copy;
                UsedDefaultsAfterInvalidFile = false;
                logger?.Info("config saved");
            }
            finally { saving.Release(); }
        }

        private static void Validate(AppConfiguration configuration)
        {
            if (configuration == null || configuration.Model != "deepseek-flash" ||
                configuration.NetworkTimeoutSeconds < 1 || configuration.NetworkTimeoutSeconds > 120 ||
                !PetHotkey.TryParse(configuration.PetHotkey, out _) || float.IsNaN(configuration.BgmVolume) || configuration.BgmVolume < 0 || configuration.BgmVolume > 1 ||
                float.IsNaN(configuration.SubtitleFontSize) || configuration.SubtitleFontSize < 20 || configuration.SubtitleFontSize > 36 ||
                float.IsNaN(configuration.VoiceVolume) || configuration.VoiceVolume < 0 || configuration.VoiceVolume > 1 ||
                configuration.VoiceLanguage != "zh" && configuration.VoiceLanguage != "ja" || configuration.VoicePreferencesVersion < 1 || configuration.VoicePreferencesVersion > 2)
                throw new InvalidDataException("Configuration values are unsupported.");
        }
        public static void ValidateCharacterCard(CharacterIdentity value, string expectedId)
        {
            if (value == null || value.Id != expectedId || string.IsNullOrWhiteSpace(value.DisplayName) || value.DisplayName.Length > 64 ||
                value.Persona == null || value.Persona.Length > 8000 || value.Relationship == null || value.Relationship.Length > 4000)
                throw new InvalidDataException("人物卡内容无效；角色 ID 不可更改。");
        }
        private void LoadCharacterCard()
        {
            CharacterCardIssue = null;
            try { var value = AtomicDocument.Read<CharacterIdentity>(CharacterCardPath); ValidateCharacterCard(value, Identity.Id); Identity = value; }
            catch (Exception error) when (error is JsonException || error is InvalidDataException)
            { CharacterCardIssue = "人物卡文件有误，原文件已保留，继续使用可用人物卡。"; logger?.Error("character card invalid original_preserved=true", error); }
        }
        public Task ReloadCharacterCardAsync(CancellationToken ct) { ct.ThrowIfCancellationRequested(); LoadCharacterCard(); return Task.CompletedTask; }
        public async Task SaveCharacterCardAsync(CharacterIdentity identity, CancellationToken ct)
        {
            ValidateCharacterCard(identity, Identity.Id); await saving.WaitAsync(ct);
            try { AtomicDocument.WriteJson(CharacterCardPath, identity, ct); Identity = identity; CharacterCardIssue = null; }
            finally { saving.Release(); }
        }
    }
}
