using System;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Core.Audio;
using GiftCompanion.Core.Character;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UI = GiftCompanion.SharedUI.CompanionUIFactory;

namespace GiftCompanion.Companion
{
    public sealed partial class CompanionScreen
    {
        private Voice.CompanionVoicePlayer voicePlayer;
        private RectTransform voicePanel;
        private TMP_Text voiceStatus, voiceVolumeLabel, voiceCacheLabel, voiceNote;
        private Button voiceEnabledButton, voiceLanguageButton;
        private Button readTextButton;
        private static bool startupHintShown;
        private bool voiceForTurn, voiceUiBusy;
        private string currentBody = "";
        private float spokenPageAt, spokenPageInterval;
        public Voice.CompanionVoicePlayer VoicePlayer => voicePlayer;
        private void BuildVoicePanel()
        {
            readTextButton = UI.Button(canvas.transform, "ReadText", "先看文字", new Vector2(395, -143), new Vector2(170, 36), UseTextForCurrentReply);
            readTextButton.gameObject.SetActive(false);
            voicePlayer = gameObject.AddComponent<Voice.CompanionVoicePlayer>(); root.ActiveVoice = voicePlayer;
            ConfigureVoicePlayer(); voicePlayer.SegmentPlaying += OnSpeechSegment; voicePlayer.Ended += OnSpeechEnded;
            voicePanel = BuildPanel("Voice", "声音与复听");
            voiceStatus = UI.Text(voicePanel, "Status", "", new Vector2(-110, 211), new Vector2(590, 36), 21);
            UI.Button(voicePanel, "Guide", "使用说明", new Vector2(335, 211), new Vector2(150, 38), () =>
                GiftCompanion.SharedUI.PlayerFiles.ReadText(System.IO.Path.Combine(root.Config.UserDataPath, "表达与日记说明.md")));
            voiceEnabledButton = UI.Button(voicePanel, "Enabled", "", new Vector2(-170, 140), new Vector2(270, 46), () =>
            { if (settingsDraft == null) return; settingsDraft.VoiceEnabled = !settingsDraft.VoiceEnabled; UpdateVoiceDraft(); });
            UI.Button(voicePanel, "Retry", "重试语音", new Vector2(170, 140), new Vector2(270, 46), () => _ = RetryVoiceAsync());
            voiceLanguageButton = UI.Button(voicePanel, "Language", "", new Vector2(0, 82), new Vector2(610, 42), () =>
            { if (settingsDraft == null || voiceUiBusy) return; settingsDraft.VoiceLanguage = settingsDraft.VoiceLanguage == "ja" ? "zh" : "ja"; UpdateVoiceDraft(); });
            voiceVolumeLabel = UI.Text(voicePanel, "VolumeLabel", "", new Vector2(0, 34), new Vector2(650, 32), 21);
            AddSlider(voicePanel, "VoiceVolume", new Vector2(0, -7), 0, 1, value =>
            { if (settingsDraft == null) return; settingsDraft.VoiceVolume = value; UpdateVoiceDraft(); });
            UI.Button(voicePanel, "Stop", "停止当前回复 / 复听", new Vector2(-170, -58), new Vector2(290, 48), () => _ = StopDialogueAsync());
            UI.Button(voicePanel, "Save", "保存声音设置", new Vector2(170, -58), new Vector2(290, 48), () => _ = SaveVoiceSettingsAsync());
            voiceCacheLabel = UI.Text(voicePanel, "Cache", "", new Vector2(0, -128), new Vector2(810, 36), 21);
            UI.Button(voicePanel, "ClearCache", "清理已缓存的语音", new Vector2(0, -190), new Vector2(340, 46), () => ConfirmAction("清理全部已缓存语音？历史文字和共同记忆仍保留，清理后旧记录将无法复听。",()=>_ = ClearAudioCacheAsync()));
            voiceNote = UI.Text(voicePanel, "Note", "", new Vector2(0, -254), new Vector2(830, 48), 17);
            UI.Button(settingsPanel, "Voice", "声音与复听", new Vector2(-165, -126), new Vector2(290, 42), OpenVoiceSettings);
            settingsNote.rectTransform.anchoredPosition = new Vector2(0, -258);
            if (root.Config.Configuration.VoiceEnabled) _ = StartVoiceQuietlyAsync();
        }
        private void ConfigureVoicePlayer()
        {
            var cfg = root.VoiceSettings;
            music.DuckFactor = cfg.DuckFactor; music.DuckAttackSeconds = cfg.DuckAttackSeconds;
            music.DuckReleaseSeconds = cfg.DuckReleaseSeconds; music.DuckHoldSeconds = cfg.DuckHoldSeconds;
            voicePlayer.Configure(root.Voice, root.AudioCache, cfg, music, () => visual?.Loader?.Speech, root.Logger,
                root.Config.Configuration.VoiceVolume, root.SpeechLocalizer, root.Config.Configuration.VoiceLanguage);
        }
        private async Task StartVoiceQuietlyAsync()
        {
            // Start also runs during Companion scene activation, before the transition flag clears.
            if (this == null || leaving || root.Character.CurrentMode != CharacterMode.Companion) return;
            try
            {
                if (root.Voice != null)
                {
                    if (!startupHintShown && root.Voice.State != VoiceRuntimeState.Ready)
                    { startupHintShown = true; ShowFeedback("声音还在准备，老师可以先和我文字聊天。"); }
                    await root.Voice.EnsureReadyAsync(root.Controller.ActiveModeToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { root.Logger.Error("voice startup text_fallback=true", error); }
        }
        private bool BeginVoice(long id)
        { voiceForTurn = root.Config.Configuration.VoiceEnabled && voicePlayer.Begin(id); return voiceForTurn; }
        public void UseTextForCurrentReply()
        {
            if (!voiceForTurn) return;
            // Cancel only speech: preserve the same Chinese request, accumulated text and saved history.
            voiceForTurn = false; voicePlayer.StopSpeech();
            ShowBodyText(currentBody, currentTurn.IsCompleted);
            dialogueState = currentTurn.IsCompleted ? CharacterVisualState.Idle : CharacterVisualState.Thinking;
            root.Logger?.Info("voice turn_text_selected=true");
        }
        private void OnSpeechSegment(long id, string text, double duration, bool replay)
        {
            if (!replay && (id != generation || !voiceForTurn)) return;
            subtitle.text = text; subtitle.ForceMeshUpdate(); subtitle.pageToDisplay = 1; subtitleUntil = float.PositiveInfinity;
            spokenPageInterval = Mathf.Max(.5f, (float)duration / Math.Max(1, subtitle.textInfo.pageCount));
            spokenPageAt = Time.unscaledTime + spokenPageInterval;
        }
        private void OnSpeechEnded(long id, bool failed, bool replay)
        {
            if (!replay && (id != generation || !voiceForTurn)) return;
            if (!replay) voiceForTurn = false;
            dialogueState = CharacterVisualState.Idle;
            if (failed && replay && panelName == "MomoTalk") historyNote.text = "这段声音暂时无法播放，可以先看文字。";
            if (failed && !replay) ShowBodyText(currentBody, true);
            else subtitleUntil = Time.unscaledTime + 4;
        }
        private void ShowBodyText(string text, bool final)
        {
            subtitle.text = text; subtitle.ForceMeshUpdate(); subtitle.pageToDisplay = final ? 1 : Math.Max(1, subtitle.textInfo.pageCount);
            subtitleUntil = final ? Time.unscaledTime + Mathf.Clamp(text.Length * .11f, 4, 10) + Math.Max(0, subtitle.textInfo.pageCount - 1) * 5 : float.PositiveInfinity;
            nextPage = Time.unscaledTime + 5;
        }
        private void UpdateVoicePresentation()
        {
            if (readTextButton != null) readTextButton.gameObject.SetActive(voiceForTurn && !inputOpen && !menuOpen && panelName == null && !leaving);
            if (voiceForTurn) dialogueState = voicePlayer.IsPlaying ? CharacterVisualState.Speaking : voicePlayer.HasLiveSession ? CharacterVisualState.Thinking : CharacterVisualState.Idle;
            else if (voicePlayer?.IsReplaying == true) dialogueState = voicePlayer.IsPlaying ? CharacterVisualState.Speaking : CharacterVisualState.Thinking;
            if (voicePlayer?.IsPlaying == true && Time.unscaledTime >= spokenPageAt && subtitle.pageToDisplay < subtitle.textInfo.pageCount)
            { subtitle.pageToDisplay++; spokenPageAt = Time.unscaledTime + spokenPageInterval; }
            if (panelName == "Voice")
                voiceStatus.text = root.VoiceConfigurationIssue ?? (root.Config.Configuration.VoiceLanguage == "ja" ? root.LocalizationIssue : null) ?? (!root.Config.Configuration.VoiceEnabled ? "当前使用文字陪伴" : root.Voice?.State == VoiceRuntimeState.Ready ? "声音已准备好" :
                    root.Voice?.State == VoiceRuntimeState.Starting ? "声音还在准备，可以先文字聊天" : "声音暂时不可用，可以继续文字聊天或重试");
        }
        public async Task StopDialogueAsync()
        {
            generation++; turnCancellation?.Cancel(); voiceForTurn = false; voicePlayer?.StopSpeech();
            dialogueState = CharacterVisualState.Idle; emotion = root.Character.State.Mood; subtitleUntil = Time.unscaledTime + 2;
            try { await currentTurn; } catch (OperationCanceledException) { }
            if (this != null) ResolveVisual();
        }
        public void OpenVoiceSettings()
        {
            if(!TryNavigate(OpenVoiceSettings))return;
            ShowPanel("Voice", voicePanel); settingsDraft = root.Config.Configuration.Copy();
            RefreshSoundSliders(); RefreshCacheSize();
            voiceNote.text = "语言保存后切换，文字始终为中文。日语需联网翻译；旧语音保留原语言。";
        }
        private void UpdateVoiceDraft()
        {
            voiceEnabledButton.GetComponentInChildren<TMP_Text>().text = settingsDraft.VoiceEnabled ? "语音：开启" : "语音：关闭";
            voiceLanguageButton.GetComponentInChildren<TMP_Text>().text = settingsDraft.VoiceLanguage == "ja" ? "朗读：日语  ·  点击切换为中文" : "朗读：中文  ·  点击切换为日语";
            voiceVolumeLabel.text = "角色声音音量  " + Mathf.RoundToInt(settingsDraft.VoiceVolume * 100) + "%";
            voicePlayer.SetVolume(settingsDraft.VoiceVolume);
        }
        private void RefreshCacheSize()
        {
            try { voiceCacheLabel.text = "已缓存语音  " + (root.AudioCache.SizeBytes() / 1048576d).ToString("F1") + " MB"; }
            catch (Exception error) { root.Logger.Error("audio cache size unavailable", error); voiceCacheLabel.text = "缓存大小暂时无法读取"; }
        }
        private async Task SaveVoiceSettingsAsync()
        {
            if (voiceUiBusy || panelCancellation == null || settingsDraft == null) return;
            voiceUiBusy = true; var ct = panelCancellation.Token;
            voiceNote.text = "正在应用声音设置，请稍等。";
            try
            {
                // Merge only this panel's fields so a volume save cannot replace unrelated player settings.
                var copy = root.Config.Configuration.Copy(); copy.VoiceEnabled = settingsDraft.VoiceEnabled; copy.VoiceVolume = settingsDraft.VoiceVolume;
                copy.BgmVolume=settingsDraft.BgmVolume;copy.BgmMuted=settingsDraft.BgmMuted;
                bool changedLanguage = copy.VoiceLanguage != settingsDraft.VoiceLanguage;
                copy.VoiceLanguage = settingsDraft.VoiceLanguage;
                await root.Config.SaveAsync(copy, ct);
                if (changedLanguage)
                {
                    await StopDialogueAsync(); await root.StopVoiceAsync();
                    if (this == null || leaving || root.Controller.IsSwitching || root.Character.CurrentMode != CharacterMode.Companion) return;
                    if (this != null && root.CreateVoiceRuntime()) ConfigureVoicePlayer();
                }
                if (!copy.VoiceEnabled) { await StopDialogueAsync(); await root.StopVoiceAsync(); }
                else _ = StartVoiceQuietlyAsync();
                if (this != null && !ct.IsCancellationRequested) voiceNote.text = changedLanguage ? "朗读语言已切换，声音准备好后用于新回复。" : "声音设置已保存。";
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { root.Logger.Error("voice settings failed", error); if (this != null && !ct.IsCancellationRequested) voiceNote.text = "设置暂时没能应用，请再试一次。"; }
            finally { voiceUiBusy = false; }
        }
        private async Task RetryVoiceAsync()
        {
            if (voiceUiBusy || !root.Config.Configuration.VoiceEnabled) { voiceNote.text = "先开启语音并保存，再重试。"; return; }
            voiceUiBusy = true;
            try
            {
                await StopDialogueAsync(); await root.StopVoiceAsync();
                if (this == null || leaving || root.Controller.IsSwitching || root.Character.CurrentMode != CharacterMode.Companion) return;
                if (root.CreateVoiceRuntime()) { ConfigureVoicePlayer(); await StartVoiceQuietlyAsync(); }
            }
            catch (Exception error) { root.Logger.Error("voice retry failed", error); }
            finally { voiceUiBusy = false; }
        }
        private async Task ClearAudioCacheAsync()
        {
            if (voiceUiBusy || panelCancellation == null) return;
            voiceUiBusy = true; var ct = panelCancellation.Token;
            voiceNote.text = "正在停止朗读并清理缓存，请稍等。";
            try
            {
                await StopDialogueAsync();
                // Drain in-flight synthesis before deleting staged files. Keep the process reusable.
                await voicePlayer.DrainAsync(); await root.AudioCache.ClearAsync(ct);
                if (this != null && !ct.IsCancellationRequested) { RefreshCacheSize(); voiceNote.text = "语音缓存已清理，聊天和共同记忆仍保留。"; }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { root.Logger.Error("audio clear failed", error); if (this != null && !ct.IsCancellationRequested) voiceNote.text = "缓存暂时没能清理，请再试一次。"; }
            finally { voiceUiBusy = false; }
        }
        private async Task ReplayHistoryAsync(string messageId, CancellationToken ct)
        {
            try
            {
                if (!currentTurn.IsCompleted || voiceForTurn || !await voicePlayer.ReplayAsync(messageId, ct))
                    if (this != null && !ct.IsCancellationRequested) historyNote.text = "先等当前回复说完；没有完整缓存的记录无法复听。";
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { root.Logger.Error("audio replay failed", error); if (this != null && !ct.IsCancellationRequested) historyNote.text = "这段声音暂时无法播放，可以先看文字。"; }
        }
    }
}
