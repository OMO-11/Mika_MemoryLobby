using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Companion;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Audio;
using GiftCompanion.Core.LLM;
using GiftCompanion.Core.Memory;
using GiftCompanion.Core.Persistence;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace GiftCompanion.Tests
{
    public sealed class CompanionScreenTests
    {
        [SetUp] public void Isolate() => PlayTestData.Begin();
        private static IEnumerator Boot()
        {
            yield return SceneManager.LoadSceneAsync("Assets/Scenes/Bootstrap.unity");
            float end = Time.realtimeSinceStartup + 20;
            while (AppRoot.Instance == null || !AppRoot.Instance.IsReady || AppRoot.Instance.Controller.IsSwitching || SceneManager.GetActiveScene().name != "Home")
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(end)); yield return null; }
            yield return Await(AppRoot.Instance.Controller.SwitchModeAsync(CharacterMode.Companion));
            while (Object.FindFirstObjectByType<CompanionScreen>()?.IsReady != true)
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(end + 30)); yield return null; }
        }
        [UnityTest] public IEnumerator InputAndMenuCaptureTouchAndNoKeyRetainsDraftWithoutSavingFakeReply()
        {
            yield return Boot(); var screen = Object.FindFirstObjectByType<CompanionScreen>();
            Assert.That(screen, Is.Not.Null); Assert.That(screen.IsMenuOpen, Is.False); Assert.That(screen.IsInputOpen, Is.False);
            var pointer = Object.FindFirstObjectByType<CompanionPresentation>().Loader.Character.GetComponent<CompanionPointerInteraction>();
            screen.OpenInput(); Assert.That(pointer.enabled, Is.False); Assert.That(screen.State, Is.EqualTo(CharacterVisualState.Listening));
            screen.Back(); Assert.That(pointer.enabled, Is.True);
            screen.OpenMenu(); Assert.That(pointer.enabled, Is.False); screen.Back(); Assert.That(pointer.enabled, Is.True);
            yield return Await(screen.SendAsync("本地无 Key 草稿"));
            Assert.That(screen.InputDraft, Is.EqualTo("本地无 Key 草稿")); Assert.That(screen.SubtitleText, Does.Not.Contain("LLMNotConfigured"));
            var turns = ((IConversationStore)AppRoot.Instance.Database).LoadTurnsAsync(10, false, default); yield return Await(turns); Assert.That(turns.Result, Is.Empty);
        }
        [UnityTest] public IEnumerator ReplacementDrainsOldTurnAndLateCallbacksCannotReplaceNewTextOrMood()
        {
            yield return Boot(); var root = AppRoot.Instance; var screen = Object.FindFirstObjectByType<CompanionScreen>(); var llm = new ReplacementReply();
            var core = new CharacterCore(root.Character.Identity, root.Character.State, root.Database, llm, root.Memory, root.Logger, root.ApplicationToken);
            typeof(AppRoot).GetField("<Character>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(root, core);
            var old = screen.SendAsync("old"); yield return Await(llm.Started.Task);
            var next = screen.SendAsync("new"); yield return Await(next); yield return Await(old);
            Assert.That(llm.MaximumActive, Is.EqualTo(1)); Assert.That(screen.SubtitleText, Is.EqualTo("新的完整回复")); Assert.That(core.State.Mood, Is.EqualTo(CharacterEmotion.Happy));
            var turns = ((IConversationStore)root.Database).LoadTurnsAsync(10, false, default); yield return Await(turns);
            Assert.That(turns.Result.Count, Is.EqualTo(2)); Assert.That(turns.Result[0].Status, Is.EqualTo("canceled")); Assert.That(turns.Result[0].AssistantText, Is.Empty);
            Assert.That(turns.Result[1].AssistantText, Is.EqualTo("新的完整回复"));
            yield return Await(root.Controller.SwitchModeAsync(CharacterMode.Home)); yield return null;
            Assert.That(screen == null, Is.True); Assert.That(Object.FindObjectsByType<CompanionPointerInteraction>(FindObjectsSortMode.None), Is.Empty);
        }
        [UnityTest] public IEnumerator HistoryReadsSharedBodyAndSceneMusicHandlesEmptyAndSyntheticPlaylistAndRelease()
        {
            yield return Boot(); var root = AppRoot.Instance; var screen = Object.FindFirstObjectByType<CompanionScreen>();
            Assert.That(root.Library.Music.Tracks.Count, Is.EqualTo(2));
            var conversations = (IConversationStore)root.Database;
            var begin = conversations.BeginTurnAsync("history-ui", "老师原文", InteractionSource.AgentPet, default); yield return Await(begin);
            yield return Await(conversations.CompleteTurnAsync(begin.Result, "<META emotion=\"happy\" action=\"none\">\n记录正文", root.Character.State, default, presentation:new DialoguePresentation("记录正文", CharacterEmotion.Happy)));
            yield return Await(screen.OpenHistoryAsync()); Assert.That(screen.VisibleHistoryCount, Is.EqualTo(1)); Assert.That(screen.ActivePanel, Is.EqualTo("MomoTalk"));
            screen.OpenSettings(); Assert.That(screen.ActivePanel, Is.EqualTo("Voice")); screen.Back(); screen.Back();
            var manager = Object.FindFirstObjectByType<GiftCompanion.Companion.Bgm.BgmManager>();
            var clip = AudioClip.Create("Validation tone", 4800, 1, 48000, false); var samples = new float[4800];
            for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(i * Mathf.PI * 2 * 440 / 48000) * .01f; clip.SetData(samples, 0);
            manager.Configure(new[] {clip}, .2f, false);
            float loopCheckUntil = Time.realtimeSinceStartup + .6f; while (Time.realtimeSinceStartup < loopCheckUntil) yield return null;
            Assert.That(manager.Source.clip, Is.SameAs(clip)); Assert.That(manager.CurrentTrackIndex, Is.EqualTo(0)); Assert.That(manager.RepeatCount, Is.GreaterThan(0)); Assert.That(manager.Source.volume, Is.GreaterThan(0));
            yield return Await(manager.FadeOutAsync(default)); Assert.That(manager.Source.isPlaying, Is.False);
            yield return Await(root.Controller.SwitchModeAsync(CharacterMode.Home)); yield return null;
            Assert.That(manager == null, Is.True); Object.Destroy(clip);
        }
        private sealed class ReplacementReply : ILLMService
        {
            public bool IsConfigured => true;
            public int MaximumActive, active;
            public readonly TaskCompletionSource<bool> Started = new TaskCompletionSource<bool>();
            public async IAsyncEnumerable<LLMChunk> StreamDialogueAsync(DialogueRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
            {
                active++; MaximumActive = Math.Max(MaximumActive, active);
                try
                {
                    if (request.Message == "old") { Started.TrySetResult(true); await Task.Delay(Timeout.Infinite, ct); }
                    yield return new LLMChunk("<META emotion=\"happy\" action=\"none\">\n新的完整回复");
                }
                finally { active--; }
            }
        }
        [UnityTest] public IEnumerator RealScreenKeepsSpeechAfterTextCommitAndSendingAgainStopsOldSoundImmediately()
        {
            yield return Boot(); var root = AppRoot.Instance; var screen = Object.FindFirstObjectByType<CompanionScreen>();
            var visual = Object.FindFirstObjectByType<CompanionPresentation>(); var music = Object.FindFirstObjectByType<GiftCompanion.Companion.Bgm.BgmManager>();
            var core = new CharacterCore(root.Character.Identity, root.Character.State, root.Database, new SpokenReply(), root.Memory, root.Logger, root.ApplicationToken);
            typeof(AppRoot).GetField("<Character>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(root, core);
            var cfg = root.Config.Configuration.Copy(); cfg.VoiceEnabled = true; yield return Await(root.Config.SaveAsync(cfg, default));
            screen.VoicePlayer.Configure(new LocalVoice(), root.AudioCache, root.VoiceSettings, music, () => visual.Loader.Speech, root.Logger, .1f);
            yield return Await(screen.SendAsync("old voice"));
            float end = Time.realtimeSinceStartup + 4;
            while (!screen.VoicePlayer.IsPlaying) { Assert.That(Time.realtimeSinceStartup, Is.LessThan(end)); yield return null; }
            yield return null;
            Assert.That(screen.ActiveTurn.IsCompleted, Is.True); Assert.That(screen.VoicePlayer.Completion.IsCompleted, Is.False);
            Assert.That(screen.State, Is.EqualTo(CharacterVisualState.Speaking)); Assert.That(screen.SubtitleText, Does.Not.Contain("<META"));
            var next = screen.SendAsync("new voice"); Assert.That(screen.VoicePlayer.IsPlaying, Is.False);
            yield return Await(next); yield return Await(screen.VoicePlayer.Completion); yield return Await(screen.VoicePlayer.DrainAsync());
            Assert.That(screen.LastDialogueResult.Success, Is.True); Assert.That(screen.SubtitleText, Does.Contain("新的"));
        }
        [UnityTest] public IEnumerator UnreadyVoiceKeepsCurrentTurnStreamingAsTextWithoutWaitingForModel()
        {
            yield return Boot(); var root = AppRoot.Instance; var screen = Object.FindFirstObjectByType<CompanionScreen>();
            var core = new CharacterCore(root.Character.Identity, root.Character.State, root.Database, new SpokenReply(), root.Memory, root.Logger, root.ApplicationToken);
            typeof(AppRoot).GetField("<Character>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(root, core);
            var cfg = root.Config.Configuration.Copy(); cfg.VoiceEnabled = true; yield return Await(root.Config.SaveAsync(cfg, default));
            Assert.That(root.Voice.State, Is.EqualTo(VoiceRuntimeState.Stopped));
            yield return Await(screen.SendAsync("unready voice"));
            Assert.That(screen.LastDialogueResult.Success, Is.True); Assert.That(screen.SubtitleText, Does.Contain("新的"));
            Assert.That(screen.VoicePlayer.HasLiveSession, Is.False); Assert.That(root.Voice.OwnedProcessId, Is.EqualTo(0));
        }
        [UnityTest] public IEnumerator SymbolOnlyReplyRemainsVisibleWhenThereIsNothingToSynthesize()
        {
            yield return Boot(); var root = AppRoot.Instance; var screen = Object.FindFirstObjectByType<CompanionScreen>();
            var visual = Object.FindFirstObjectByType<CompanionPresentation>(); var music = Object.FindFirstObjectByType<GiftCompanion.Companion.Bgm.BgmManager>();
            var core = new CharacterCore(root.Character.Identity, root.Character.State, root.Database, new SpokenReply(), root.Memory, root.Logger, root.ApplicationToken);
            typeof(AppRoot).GetField("<Character>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(root, core);
            var cfg = root.Config.Configuration.Copy(); cfg.VoiceEnabled = true; yield return Await(root.Config.SaveAsync(cfg, default));
            screen.VoicePlayer.Configure(new LocalVoice(), root.AudioCache, root.VoiceSettings, music, () => visual.Loader.Speech, root.Logger, .1f);
            yield return Await(screen.SendAsync("symbols")); yield return Await(screen.VoicePlayer.Completion);
            Assert.That(screen.LastDialogueResult.Success, Is.True); Assert.That(screen.SubtitleText, Is.EqualTo("☆")); Assert.That(screen.VoicePlayer.PlayedSegments, Is.EqualTo(0));
        }
        private sealed class SpokenReply : ILLMService
        {
            public bool IsConfigured => true;
            public async IAsyncEnumerable<LLMChunk> StreamDialogueAsync(DialogueRequest request, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
            { await Task.Yield(); ct.ThrowIfCancellationRequested(); yield return new LLMChunk("<META emotion=\"happy\" action=\"none\">\n" + (request.Message == "symbols" ? "☆" : "老师，" + (request.Message == "old voice" ? "这是旧的一段温柔回复。" : "这是新的一段完整回复。"))); }
        }
        [UnityTest] public IEnumerator ChineseReplyIsStoredDuringTranslationAndVoiceSubtitlesDoNotPreviewFullReply()
        {
            yield return Boot(); var root = AppRoot.Instance; var screen = Object.FindFirstObjectByType<CompanionScreen>();
            var core = new CharacterCore(root.Character.Identity, root.Character.State, root.Database, new SpokenReply(), root.Memory, root.Logger, root.ApplicationToken);
            typeof(AppRoot).GetField("<Character>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(root, core);
            var cfg = root.Config.Configuration.Copy(); cfg.VoiceEnabled = true; yield return Await(root.Config.SaveAsync(cfg, default));
            var translator = new HeldTranslation();
            screen.VoicePlayer.Configure(new LocalVoice(), root.AudioCache, root.VoiceSettings, null, null, root.Logger, .1f, translator, "ja");
            try
            {
                yield return Await(screen.SendAsync("日语也先看中文"));
                Assert.That(translator.Started, Is.True); Assert.That(screen.VoicePlayer.IsPlaying, Is.False);
                Assert.That(screen.SubtitleText, Is.Empty);
                Assert.That(screen.LastDialogueResult.Text, Does.Contain("新的")); Assert.That(screen.ActiveTurn.IsCompleted, Is.True);
                var turns = ((IConversationStore)root.Database).LoadTurnsAsync(10, true, default); yield return Await(turns);
                Assert.That(turns.Result.Single().AssistantText, Is.EqualTo(screen.LastDialogueResult.Text));
                screen.VoicePlayer.StopSpeech(); translator.Release.TrySetResult(true); yield return Await(screen.VoicePlayer.DrainAsync());
                var paths = ((IAudioRecordStore)root.Database).ReadAllAudioPathsAsync(default); yield return Await(paths); Assert.That(paths.Result, Is.Empty);
            }
            finally { translator.Release.TrySetResult(true); }
        }
        [UnityTest] public IEnumerator ChoosingTextDuringTranslationKeepsOneChineseTurnAndIgnoresLateSpeech()
        {
            yield return Boot(); var root = AppRoot.Instance; var screen = Object.FindFirstObjectByType<CompanionScreen>();
            var core = new CharacterCore(root.Character.Identity, root.Character.State, root.Database, new SpokenReply(), root.Memory, root.Logger, root.ApplicationToken);
            typeof(AppRoot).GetField("<Character>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(root, core);
            var cfg = root.Config.Configuration.Copy(); cfg.VoiceEnabled = true; yield return Await(root.Config.SaveAsync(cfg, default));
            var translator = new HeldTranslation(); int plays = 0;
            screen.VoicePlayer.SegmentPlaying += (_, __, ___, ____) => plays++;
            screen.VoicePlayer.Configure(new LocalVoice(), root.AudioCache, root.VoiceSettings, null, null, root.Logger, .1f, translator, "ja");
            try
            {
                yield return Await(screen.SendAsync("先看文字")); Assert.That(translator.Started, Is.True);
                screen.UseTextForCurrentReply(); string body = screen.LastDialogueResult.Text;
                Assert.That(screen.SubtitleText, Is.EqualTo(body)); Assert.That(root.Config.Configuration.VoiceEnabled, Is.True);
                translator.Release.TrySetResult(true); yield return Await(screen.VoicePlayer.DrainAsync()); yield return null;
                Assert.That(plays, Is.EqualTo(0)); Assert.That(screen.SubtitleText, Is.EqualTo(body));
                var turns = ((IConversationStore)root.Database).LoadTurnsAsync(10, true, default); yield return Await(turns);
                Assert.That(turns.Result.Count, Is.EqualTo(1)); Assert.That(turns.Result[0].AssistantText, Is.EqualTo(body));
                // The next turn uses voice again without changing the persisted preference.
                yield return Await(screen.SendAsync("下一轮")); yield return Await(screen.VoicePlayer.Completion);
                Assert.That(plays, Is.GreaterThan(0));
            }
            finally { translator.Release.TrySetResult(true); }
        }
        [UnityTest] public IEnumerator DiaryRemembersEachEntryAndReturnPositionWithoutChangingExportedNotes()
        {
            yield return Boot(); var root = AppRoot.Instance; var screen = Object.FindFirstObjectByType<CompanionScreen>();
            var store = (IDiaryStore)root.Database; var conversations = (IConversationStore)root.Database;
            var start = conversations.BeginTurnAsync("diary-reading", "测试回忆", InteractionSource.Companion, default); yield return Await(start);
            yield return Await(conversations.CompleteTurnAsync(start.Result, "测试回复", root.Character.State, default));
            var turns = conversations.LoadTurnsAsync(10, true, default); yield return Await(turns);
            var snapshot = store.SnapshotDiaryAsync(default); yield return Await(snapshot);
            for (int i = 0; i < 2; i++)
            {
                var entry = new DiaryEntry { Id = Guid.NewGuid().ToString("N"), Fingerprint = "reading-" + i, Status = "active", CreatedAt = DateTimeOffset.UtcNow.AddSeconds(i),
                    FirstAt = snapshot.Result.FirstAt.Value, LastAt = snapshot.Result.LastAt.Value, TurnCount = 1,
                    Body = string.Join("\n\n", Enumerable.Repeat("这是用于验证阅读位置的长段落，保留自然换行，让老师可以慢慢翻看之前聊过的话题。", 35)) };
                var saved = store.CommitDiaryAsync(snapshot.Result, entry, new[] { turns.Result[0].UserMessageId, turns.Result[0].AssistantMessageId }, default); yield return Await(saved); Assert.That(saved.Result, Is.True);
            }
            yield return Await(screen.OpenDiaryAsync()); yield return null;
            var scroll = (UnityEngine.UI.ScrollRect)typeof(CompanionScreen).GetField("diaryScroll", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(screen);
            scroll.verticalNormalizedPosition = .32f; screen.OpenMenu(); yield return Await(screen.OpenDiaryAsync());
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(.32f).Within(.03f));
            var buttons = scroll.transform.parent.GetComponentsInChildren<UnityEngine.UI.Button>();
            buttons.Single(b => b.name == "Previous").onClick.Invoke(); Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1).Within(.03f));
            scroll.verticalNormalizedPosition = .71f; buttons.Single(b => b.name == "Next").onClick.Invoke();
            Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(.32f).Within(.03f));
        }
        private sealed class HeldTranslation : ISpeechLocalizer
        {
            public bool Started; public readonly TaskCompletionSource<bool> Release = new TaskCompletionSource<bool>();
            public async Task<LocalizedSpeech> LocalizeAsync(string chinese, CancellationToken ct)
            { Started = true; await Release.Task; return new LocalizedSpeech { Text = "先生、一緒に過ごそうね。", Language = "ja" }; }
        }
        private sealed class LocalVoice : IVoiceSynthesizer
        {
            public string ProfileId => "zh:screen-test";
            public async Task<byte[]> SynthesizeAsync(string text, CancellationToken ct) { await Task.Delay(50, ct); return VoicePlaybackTests.Wav(); }
        }
        private static IEnumerator Await(Task task)
        { while (!task.IsCompleted) yield return null; if (task.IsFaulted) throw task.Exception; Assert.That(task.IsCanceled, Is.False); }
        [UnityTearDown] public IEnumerator End()
        { if (AppRoot.Instance != null) Object.Destroy(AppRoot.Instance.gameObject); yield return null; PlayTestData.End(); }
    }
}
