using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Audio;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.LLM;
using GiftCompanion.Core.Agent;
using ILogger = GiftCompanion.Core.Logging.ILogger;
using Logger = GiftCompanion.Core.Logging.Logger;
using GiftCompanion.Core.Memory;
using GiftCompanion.Core.Persistence;
using UnityEngine;

namespace GiftCompanion.Bootstrap
{
    [RequireComponent(typeof(AppController))]
    public sealed class AppRoot : MonoBehaviour
    {
        [SerializeField] private TextAsset identityAsset;
        [SerializeField] private TextAsset characterManifestAsset;
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private SingleInstanceGuard guard;
        private Task shutdownTask;
        private bool resourcesDisposed, shutdownComplete;
        public CancellationToken ApplicationToken => lifetime.Token;
        public string FailureCode { get; private set; }
        public bool ShutdownSucceeded { get; private set; }
#if UNITY_EDITOR
        internal static string EditorValidationDataPath;
#endif
        public static AppRoot Instance { get; private set; }
        public Task Initialization { get; private set; }
        public bool IsReady { get; private set; }
        public IConfigService Config { get; private set; }
        public AppEntrySettings EntrySettings { get; private set; } = new AppEntrySettings();
        public PetSettingsService PetSettings { get; private set; }
        public AgentSettingsService AgentSettings { get; private set; }
        public AgentConversation Agent { get; private set; }
        public ILogger Logger { get; private set; }
        public IDatabaseService Database { get; private set; }
        public ILLMService LLM { get; private set; }
        public IMemoryService Memory { get; private set; }
        public ConversationHistoryService History { get; private set; }
        public ContentLibrary Library { get; private set; }
        public RelationshipDiary Diary { get; private set; }
        public VoiceRuntimeManager Voice { get; private set; }
        public VoiceRuntimeSettings VoiceSettings { get; private set; } = new VoiceRuntimeSettings();
        public AudioCache AudioCache { get; private set; }
        public ISpeechLocalizer SpeechLocalizer { get; private set; }
        public string LocalizationIssue { get; private set; }
        public Companion.Voice.CompanionVoicePlayer ActiveVoice { get; set; }
        public string VoiceConfigurationIssue { get; private set; }
        public bool PlayerWorkBusy { get; private set; }
        private TaskCompletionSource<bool> playerWorkEnded;
        public ICharacterCore Character { get; private set; }
        public AppController Controller { get; private set; }
        public PlayerBackup Backups { get; private set; }
        public PlayerConfigRecovery ConfigRecovery { get; private set; }
        public CompanionDisplayService Display { get; private set; }
        public CompanionVisualService VisualSettings { get; private set; }
        public string RestoreNotice { get; private set; }
        public void AcknowledgeRestoreNotice() => RestoreNotice = null;
        public string DataDirectory { get; private set; }
        public string FixedCharacterId { get; private set; }
        public int DataSchemaVersion => Database?.SchemaVersion ?? 0;
        public async Task TestConnectionAsync(string candidate, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(candidate)) candidate = LocalSecrets.ReadDeepSeekKey(Config.UserDataPath, false);
            if (string.IsNullOrWhiteSpace(candidate)) throw new ArgumentException("请先输入Key。");
            using var client = new LLMService(candidate.Trim(), Config.Configuration.Model, 20);
            await client.CompleteJsonAsync("Reply with JSON only: {\"ok\":true}", "Connection check", 32, ct);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Controller = GetComponent<AppController>();
            Application.wantsToQuit += WantsToQuit;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            gameObject.AddComponent<AgentPet.Window.DesktopWindowPrototype>();
            gameObject.AddComponent<Validation.Phase0PlayerProbe>();
            gameObject.AddComponent<Validation.Phase1PlayerProbe>();
            gameObject.AddComponent<Validation.Phase2PlayerProbe>();
            gameObject.AddComponent<Validation.Phase3PlayerProbe>();
            gameObject.AddComponent<Validation.Phase3PlusPlayerProbe>();
            gameObject.AddComponent<Validation.Phase4PlayerProbe>();
            gameObject.AddComponent<Validation.BilingualPlayerProbe>();
            gameObject.AddComponent<Validation.VoicePolishPlayerProbe>();
            gameObject.AddComponent<Validation.ExpressionDiaryPlayerProbe>();
            gameObject.AddComponent<Validation.Phase5PlayerProbe>();
            gameObject.AddComponent<Validation.Phase6PlayerProbe>();
            gameObject.AddComponent<Validation.V1CloseoutProbe>();
            gameObject.AddComponent<Validation.V2WindowProbe>();
            gameObject.AddComponent<Validation.V2ReadingProbe>();
            gameObject.AddComponent<Validation.V2MaintenanceProbe>();
            gameObject.AddComponent<Validation.V2ResizeProbe>();
            gameObject.AddComponent<Validation.V2FinalProbe>();
            gameObject.AddComponent<Validation.SeasideProbe>();
#endif
            gameObject.AddComponent<Validation.Phase2DevelopmentPanel>();
            Initialization = InitializeAsync(lifetime.Token);
        }

        private async Task InitializeAsync(CancellationToken ct)
        {
            try
            {
                guard = new SingleInstanceGuard(!Application.isEditor);
                if (!guard.IsPrimary) { Application.Quit(); return; }
                string userDataPath = UserDataPaths.Resolve(Application.isEditor, Path.Combine(Application.dataPath, ".."), Environment.GetCommandLineArgs());
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(EditorValidationDataPath))
                {
                    string validation = Path.GetFullPath(EditorValidationDataPath);
                    string boundary = Path.GetFullPath(Path.Combine(userDataPath, "Validation")) + Path.DirectorySeparatorChar;
                    if (!validation.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Editor validation path is outside its boundary.");
                    userDataPath = validation;
                }
#endif
                DataDirectory = userDataPath;
                if (identityAsset == null) throw new InvalidDataException("AppRoot identity asset is not assigned.");
                string fixedId = Newtonsoft.Json.Linq.JObject.Parse(identityAsset.text).Value<string>("id");
                FixedCharacterId = fixedId;
                RestoreNotice = await Task.Run(() => PlayerRestore.ApplyPending(userDataPath, Path.GetFullPath(Path.Combine(Application.dataPath, "..")), fixedId), ct);
                Logger = new Logger(Path.Combine(userDataPath, "Logs"));
                Logger.Info("startup");
                if (identityAsset == null) throw new InvalidDataException("AppRoot identity asset is not assigned.");
                Config = new ConfigService(userDataPath, identityAsset.text, Logger, characterManifestAsset?.text);
                await Config.InitializeAsync(ct);
                EntrySettings = AppEntrySettings.Load(userDataPath, ct);
                Backups = new PlayerBackup(userDataPath, Path.GetFullPath(Path.Combine(Application.dataPath, "..")), Config.Identity.Id);
                ConfigRecovery = new PlayerConfigRecovery(userDataPath);
                Display = new CompanionDisplayService(userDataPath); Display.Load(ct);
                VisualSettings = new CompanionVisualService(userDataPath, Companion.CompanionVisualFoundation.DefaultJson); VisualSettings.Load(ct);
                string backupGuide = Path.Combine(userDataPath, "备份与迁移说明.md");
                if (!File.Exists(backupGuide)) AtomicDocument.Write(backupGuide, File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Defaults", "BackupGuide.md")), ct);
                PetSettings = new PetSettingsService(userDataPath); PetSettings.Load(ct);
                string petGuide = Path.Combine(userDataPath, "桌宠与工具说明.md");
                if (!File.Exists(petGuide)) AtomicDocument.Write(petGuide, File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Defaults", "PetGuide.md")), ct);
                Library = new ContentLibrary(userDataPath, Application.streamingAssetsPath);
                await Library.InitializeAsync(ct);
                VoiceDeliverySettings.Seed(userDataPath, Path.Combine(Application.streamingAssetsPath, "Voice"), ct);
                string voiceGuide = Path.Combine(userDataPath, "声音与复听.md");
                AtomicDocument.Recover(voiceGuide);
                if (!File.Exists(voiceGuide)) AtomicDocument.Write(voiceGuide, File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Defaults", "VoiceGuide.md")), ct);
                string bilingualGuide = Path.Combine(userDataPath, "双语声音说明.md");
                AtomicDocument.Recover(bilingualGuide);
                if (!File.Exists(bilingualGuide)) AtomicDocument.Write(bilingualGuide, File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Defaults", "BilingualVoiceGuide.md")), ct);
                string polishGuide = Path.Combine(userDataPath, "语音与日记微调.md");
                AtomicDocument.Recover(polishGuide);
                if (!File.Exists(polishGuide)) AtomicDocument.Write(polishGuide, File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Defaults", "VoicePolishGuide.md")), ct);
                string expressionGuide = Path.Combine(userDataPath, "表达与日记说明.md");
                AtomicDocument.Recover(expressionGuide);
                if (!File.Exists(expressionGuide)) AtomicDocument.Write(expressionGuide, File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Defaults", "ExpressionDiaryGuide.md")), ct);
                Database = new DatabaseService(Path.Combine(userDataPath, "memory.db"), Config.Identity.Id, Logger);
                await Database.InitializeAsync(ct);
                await Database.MigrateAsync(ct);
                AudioCache = new AudioCache((IAudioRecordStore)Database, userDataPath);
                await AudioCache.RecoverAsync(ct);
                CreateVoiceRuntime();
                History = new ConversationHistoryService((IConversationHistoryStore)Database);
                Logger.Info($"database persistent={Database.IsPersistent} schema={Database.SchemaVersion}");
                var state = await Database.LoadCharacterStateAsync(ct) ?? new CharacterState();
                await ((IConversationStore)Database).RecoverConversationsAsync(ct);
                string key = null;
                try { key = LocalSecrets.ReadDeepSeekKey(userDataPath, Application.isEditor || Array.Exists(Environment.GetCommandLineArgs(), a => a.EndsWith("-probe", StringComparison.Ordinal))); }
                catch (Exception error) { Logger.Error("llm key unavailable", error); }
                LLM = new LLMService(key, Config.Configuration.Model, Config.Configuration.NetworkTimeoutSeconds);
                try
                {
                    var translation = SpeechLocalizationSettings.Load(userDataPath, ct);
                    SpeechLocalizer = new SpeechLocalizer(new LLMService(key, translation.Model, translation.TimeoutSeconds), translation);
                }
                catch (Exception error)
                { LocalizationIssue = "日语朗读暂时不可用，可以先看中文或使用中文声音。"; Logger.Error("speech localization configuration unavailable original_preserved=true", error); }
                Diary = new RelationshipDiary((IDiaryStore)Database, LLM as IMemoryLLMService, () => Config.Identity, userDataPath);
                await Diary.InitializeAsync(ct);
                Memory = new MemoryService((IMemoryStore)Database, LLM, Logger, lifetime.Token, SyncDiaryExportsAsync);
                await Memory.InitializeAsync(ct);
                ct.ThrowIfCancellationRequested();
                AgentSettings = new AgentSettingsService(userDataPath); AgentSettings.Load(ct);
                await ((IAgentToolStore)Database).RecoverToolsAsync(ct);
                Agent = new AgentConversation(((LLMService)LLM).AgentModel, new AgentPet.Tools.WindowsAgentPlatform(AgentSettings), (IAgentToolStore)Database, AgentSettings, userDataPath, Logger);
                try { await Agent.ExportAuditAsync(ct); } catch (IOException) { Logger.Info("tool audit export pending retry=true"); }
                Character = new CharacterCore(Config.Identity, state, Database, LLM, Memory, Logger, lifetime.Token, Config.CompanionPolicy, SyncDiaryExportsAsync, Agent);
                Controller.Initialize(this);
                IsReady = true;
                Logger.Info("character core ready");
            }
            catch (Exception exception)
            {
                ReportRuntimeFailure("StartupFailed", exception);
                gameObject.AddComponent<SharedUI.StartupRecovery>();
                throw;
            }
        }

        public async Task SyncDiaryExportsAsync(CancellationToken ct)
        {
            try { if (Diary != null) await Diary.SyncExportsAsync(ct); }
            catch (OperationCanceledException) { }
            catch (Exception error) { Logger?.Error("diary export pending retry=true", error); }
        }
        public async Task<T> PlayerWorkAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
        {
            if (PlayerWorkBusy || !IsReady || Controller.IsSwitching) throw new InvalidOperationException("Player work unavailable.");
            PlayerWorkBusy = true; playerWorkEnded = new TaskCompletionSource<bool>();
            using var scope = CancellationTokenSource.CreateLinkedTokenSource(ct, ApplicationToken, Controller.ActiveModeToken);
            try
            {
                ActiveVoice?.StopSpeech();
                await Character.PauseAndCancelAsync(scope.Token);
                using var foreground = (Memory as ISharedMemoryService)?.EnterForeground();
                return await operation(scope.Token);
            }
            finally
            {
                if (IsReady && !Controller.IsSwitching && !ApplicationToken.IsCancellationRequested) Character.ResumeRequests();
                PlayerWorkBusy = false; playerWorkEnded.TrySetResult(true);
            }
        }
        public Task<bool> SaveCharacterCardAsync(CharacterIdentity value, CancellationToken ct) => PlayerWorkAsync(async token =>
        {
            await Config.SaveCharacterCardAsync(value, token);
            ((CharacterCore)Character).UsePlayerCharacterCard(Config.Identity);
            return true;
        }, ct);
        public Task<T> MaintenanceAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct) => PlayerWorkAsync(async token =>
        {
            using var drained = Memory is MemoryService worker ? await worker.PauseAndDrainAsync(token) : null;
            await StopVoiceAsync();
            await Database.SaveCharacterStateAsync(Character.State, token);
            return await operation(token);
        }, ct);
        public Task<BackupManifest> BackupAsync(string output, bool audio, CancellationToken ct) => MaintenanceAsync(token => Backups.CreateAsync(output, audio, (DatabaseService)Database, token), ct);
        public Task<string> ResetConfigAsync(string file, CancellationToken ct) => MaintenanceAsync(token => Task.Run(() => ConfigRecovery.Reset(file, token), token), ct);
        public Task<string> RestorePreviousConfigAsync(string file, CancellationToken ct) => MaintenanceAsync(token => Task.Run(() => ConfigRecovery.RestorePrevious(file, text =>
        {
            if (file == "character_card.json") ConfigService.ValidateCharacterCard(Newtonsoft.Json.JsonConvert.DeserializeObject<CharacterIdentity>(text), FixedCharacterId);
            else if (file == "bgm_playlist.json") Library.ValidateMusic(Newtonsoft.Json.JsonConvert.DeserializeObject<MusicCatalog>(text));
            else if (file == "companion_spaces.json") Library.ValidateSpaces(Newtonsoft.Json.JsonConvert.DeserializeObject<SpaceCatalog>(text));
            else throw new InvalidDataException("此配置不支持单项恢复。");
        }, token), token), ct);
        public Task<string> RestoreAsync(BackupPreview preview, CancellationToken ct) => MaintenanceAsync(async token =>
        {
            await Task.Run(() => Backups.EstimateRestore(preview, token).EnsureAvailable(), token);
            string point = Path.Combine(Config.UserDataPath, "Backups", "before-restore-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".mikabackup");
            await Backups.CreateAsync(point, true, (DatabaseService)Database, token);
            await Task.Run(() => Backups.Schedule(preview, token), token); return point;
        }, ct);
        public async Task<bool> RestartAsync()
        {
#if UNITY_EDITOR
            return false; // The Editor owns its Play lifecycle. Apply on the next Play session.
#else
            await ShutdownAsync();
            if (!ShutdownSucceeded) return false;
            string executable = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
            var start = new System.Diagnostics.ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable) };
            start.EnvironmentVariables.Remove("GIFT_DEEPSEEK_API_KEY"); start.EnvironmentVariables.Remove("GIFT_VOICE_RUNTIME_ROOT");
            System.Diagnostics.Process.Start(start); Application.Quit(); return true;
#endif
        }
        public Task<bool> ReloadCharacterCardAsync(CancellationToken ct) => PlayerWorkAsync(async token =>
        {
            await Config.ReloadCharacterCardAsync(token);
            ((CharacterCore)Character).UsePlayerCharacterCard(Config.Identity);
            return true;
        }, ct);
        public void ReportRuntimeFailure(string code, Exception error)
        {
            IsReady = false; FailureCode = code;
            Logger?.Error(code, error);
        }
        public bool CreateVoiceRuntime()
        {
            try
            {
                Voice?.Dispose(); Voice = null;
                VoiceSettings = VoiceRuntimeSettings.Load(Config.UserDataPath, ApplicationToken).ForLanguage(Config.Configuration.VoiceLanguage);
                Voice = new VoiceRuntimeManager(Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                    Path.Combine(Application.streamingAssetsPath, "Voice", "voice_host.py"), Config.UserDataPath, VoiceSettings, Logger);
                VoiceConfigurationIssue = null; return true;
            }
            catch (Exception error)
            { VoiceConfigurationIssue = "语音资料暂时不可用，可以继续文字聊天。"; Logger?.Error("voice configuration invalid original_preserved=true", error); return false; }
        }
        public async Task StopVoiceAsync()
        {
            ActiveVoice?.StopSpeech();
            if (Voice != null) await Voice.StopAsync();
            if (ActiveVoice != null) await ActiveVoice.DrainAsync();
        }

        public Task ShutdownAsync() => shutdownTask ??= ShutdownCoreAsync();

        private async Task ShutdownCoreAsync()
        {
            IsReady = false;
            Controller?.Shutdown();
            lifetime.Cancel();
            try
            {
                // A close during bootstrap must not dispose services while initialization still uses them.
                if (Initialization != null)
                {
                    try { await Initialization; }
                    catch (Exception error) { Logger?.Error("shutdown startup ended", error); }
                }
                if (Controller != null) await Controller.DrainAsync();
                if (playerWorkEnded != null) await playerWorkEnded.Task;
                await StopVoiceAsync();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                if (Character != null) await Character.PauseAndCancelAsync(timeout.Token);
                if (Memory is ISharedMemoryService sharedMemory) await sharedMemory.StopAsync(timeout.Token);
                if (Database?.IsPersistent == true && Character != null)
                    await ((IConversationStore)Database).EndSessionAndSaveStateAsync(Character.State, "closed", timeout.Token);
                ShutdownSucceeded = true;
            }
            catch (Exception error) { Logger?.Error("shutdown save failed", error); }
            finally
            {
                DisposeResources();
                shutdownComplete = true;
            }
        }

        public async void RequestQuit()
        {
            await ShutdownAsync();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private bool WantsToQuit()
        {
            if (shutdownComplete) return true;
            var companion=FindFirstObjectByType<GiftCompanion.Companion.CompanionScreen>();
            if(companion!=null&&companion.HandleWindowClose())return false;
            RequestQuit();
            return false;
        }

        private void OnGUI()
        {
            if (string.IsNullOrEmpty(FailureCode)) return;
            if (GetComponent<SharedUI.StartupRecovery>() != null) return;
            GUI.Box(new Rect(Screen.width / 2 - 180, Screen.height / 2 - 55, 360, 110), "Unable to continue. Please close and try again.");
            if (GUI.Button(new Rect(Screen.width / 2 - 50, Screen.height / 2, 100, 32), "Close")) RequestQuit();
        }

        private void DisposeResources()
        {
            if (resourcesDisposed) return;
            resourcesDisposed = true;
            Controller?.ReleaseLifetime();
            Voice?.Dispose();
            (SpeechLocalizer as IDisposable)?.Dispose();
            (Memory as IDisposable)?.Dispose();
            Database?.Dispose();
            (LLM as IDisposable)?.Dispose();
            Logger?.Info("shutdown");
            Logger?.Dispose();
            guard?.Dispose();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            IsReady = false;
            Application.wantsToQuit -= WantsToQuit;
            Controller?.Shutdown();
            lifetime.Cancel();
            // Editor stop / abrupt object destruction cannot await Unity continuations.
            // The persistence worker has no Unity dependency; give a final save a bounded wait.
            if (!resourcesDisposed && Database?.IsPersistent == true && Character != null)
            {
                try
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    if (!((IConversationStore)Database).EndSessionAndSaveStateAsync(Character.State, "interrupted", timeout.Token).Wait(2500))
                        Logger?.Info("shutdown fallback save timed_out=true");
                }
                catch (Exception error) { Logger?.Error("shutdown fallback save failed", error); }
            }
            DisposeResources();
            lifetime.Dispose();
            Instance = null;
        }
    }
}
