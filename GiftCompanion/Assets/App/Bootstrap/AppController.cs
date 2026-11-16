using System;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GiftCompanion.Bootstrap
{
    public sealed class AppController : MonoBehaviour
    {
        private AppRoot root;
        private CancellationTokenSource modeLifetime = new CancellationTokenSource();
        private bool stopping;
        private Task transition = Task.CompletedTask;
        internal System.Func<string, Task> SceneLoaderOverride;
        public bool IsSwitching { get; private set; }
        public CancellationToken ActiveModeToken => modeLifetime.Token;

        public void Initialize(AppRoot appRoot) => root = appRoot;

        public Task SwitchModeAsync(CharacterMode mode, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            if (stopping || root == null || !root.IsReady)
                throw new InvalidOperationException("Application is not ready.");
            if (IsSwitching) return Task.CompletedTask; // Repeated UI clicks do not queue.
            if (mode == CharacterMode.AgentPet && !root.EntrySettings.PetEntryEnabled) throw new InvalidOperationException("桌宠入口已暂时关闭。");
            if (root.PlayerWorkBusy) throw new InvalidOperationException("资料处理期间请稍候再切换。");
            string scenePath = ScenePath(mode);
            if (root.Character.CurrentMode == mode && SceneManager.GetActiveScene().path == scenePath) return Task.CompletedTask;
            if (!Application.CanStreamedLevelBeLoaded(scenePath))
                throw new InvalidOperationException("Target scene is not in Build Settings.");

            IsSwitching = true;
            transition = SwitchCoreAsync(mode, scenePath, ct);
            return transition;
        }

        private async Task SwitchCoreAsync(CharacterMode mode, string scenePath, CancellationToken ct)
        {
            bool committed = false;
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, root.ApplicationToken);
            try
            {
                await root.Character.PauseAndCancelAsync(linked.Token);
                await root.StopVoiceAsync();
                await root.Character.SwitchModeAsync(mode, linked.Token);
                committed = true;
                modeLifetime.Cancel();
                modeLifetime.Dispose();
                modeLifetime = new CancellationTokenSource();
                root.Logger.Info($"scene switch target={mode}");
                // Unity scene activation cannot be canceled. After state commit, finish this
                // load to keep the character mode consistent with the active scene.
                await LoadSceneAsync(scenePath);
            }
            catch (Exception exception)
            {
                root.Logger.Error("scene switch failed", exception);
                if (committed && !stopping)
                {
                    try
                    {
                        await LoadSceneAsync(ScenePath(CharacterMode.Home));
                        await root.Character.RecoverHomeAsync();
                        root.Logger.Info("scene recovery result=home");
                    }
                    catch (Exception recovery)
                    {
                        stopping = true;
                        root.ReportRuntimeFailure("SceneRecoveryFailed", recovery);
                    }
                }
                throw;
            }
            finally { IsSwitching = false; if (!stopping) root.Character.ResumeRequests(); }
        }

        private async Task LoadSceneAsync(string path)
        {
            if (SceneLoaderOverride != null) { await SceneLoaderOverride(path); return; }
            var operation = SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
            if (operation == null) throw new InvalidOperationException("Scene load did not start.");
            while (!operation.isDone) await Task.Yield();
        }

        public static string ScenePath(CharacterMode mode)
        {
            switch (mode)
            {
                case CharacterMode.Home: return "Assets/Scenes/Home.unity";
                case CharacterMode.Companion: return "Assets/Scenes/Companion.unity";
                case CharacterMode.AgentPet: return "Assets/Scenes/AgentPet.unity";
                default: throw new ArgumentOutOfRangeException(nameof(mode));
            }
        }

        public void Shutdown()
        {
            if (stopping) return;
            stopping = true;
            modeLifetime.Cancel();
        }
        public void ReleaseLifetime() => modeLifetime.Dispose();
        public async Task DrainAsync()
        {
            try { await transition; }
            catch (Exception error) { root?.Logger?.Error("scene shutdown transition ended", error); }
        }
    }
}
