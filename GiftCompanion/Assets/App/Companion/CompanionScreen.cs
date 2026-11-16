using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Core.Character;
using GiftCompanion.SharedUI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UI = GiftCompanion.SharedUI.CompanionUIFactory;

namespace GiftCompanion.Companion
{
    public sealed partial class CompanionScreen : MonoBehaviour
    {
        private AppRoot root;
        private CompanionPresentation visual;
        private Canvas canvas;
        private RectTransform inputPanel, menuPanel;
        private CanvasGroup inputFade, subtitleFade;
        private TMP_InputField input;
        private TMP_Text subtitle, clock, hint, speaker;
        private Button historyButton, settingsButton;
        private Action openHistory, openSettings;
        private CancellationTokenSource turnCancellation;
        private Task currentTurn = Task.CompletedTask;
        private long generation;
        private readonly CompanionInputGuard inputGuard = new CompanionInputGuard();
        private CharacterVisualState dialogueState = CharacterVisualState.Idle;
        private CharacterEmotion emotion;
        private bool inputOpen, menuOpen, leaving;
        private string panelName;
        private float subtitleUntil, nextPage, hintUntil;
        private int inputOpenedFrame;
        private Keyboard imeKeyboard;
        private string imeComposition = "";
        public bool IsInputOpen => inputOpen;
        public bool IsMenuOpen => menuOpen;
        public string ActivePanel => panelName;
        public CharacterVisualState State => visual == null ? CharacterVisualState.Idle : visual.CurrentState;
        public string SubtitleText => subtitle == null ? "" : subtitle.text;
        public Task ActiveTurn => currentTurn;
        public string InputDraft => input == null ? "" : input.text;
        public DialogueResult LastDialogueResult { get; private set; }
        public bool IsReady => music != null && subtitle != null && libraryMusic?.Ready == true && visual?.Ready == true;
        partial void BuildAdditionalPanels();
        partial void CloseAdditionalPanels();
        partial void ReleaseAdditionalPanels();
        private void Start()
        {
            root = AppRoot.Instance; if (root == null || !root.IsReady) return;
            visual = FindFirstObjectByType<CompanionPresentation>();
            emotion = root.Character.State.Mood;
            canvas = UI.Canvas("Companion Interface");
            var clockPanel = UI.Panel(canvas.transform, "ClockBackdrop", new Vector2(-68, -32), new Vector2(110, 43), new Color(.025f, .04f, .08f, .55f), false);
            clockPanel.rectTransform.anchorMin = clockPanel.rectTransform.anchorMax = Vector2.one;
            clock = UI.Text(clockPanel.transform, "Clock", "", Vector2.zero, new Vector2(96, 39), 24);
            var hintPanel = UI.Panel(canvas.transform, "HintBackdrop", new Vector2(0, -328), new Vector2(360, 32), new Color(.025f, .04f, .08f, .6f), false);
            hint = UI.Text(hintPanel.transform, "Hint", "Enter 与老师说话  ·  Esc 菜单", Vector2.zero, new Vector2(350, 30), 17);
            hintUntil = Time.unscaledTime + 7;
            var subtitlePanel = UI.Panel(canvas.transform, "Subtitle", new Vector2(0, -243), new Vector2(930, 150), new Color(.025f, .04f, .08f, .78f), false);
            subtitleFade = subtitlePanel.gameObject.AddComponent<CanvasGroup>(); subtitleFade.alpha = 0; subtitleFade.blocksRaycasts = false;
            speaker = UI.Text(subtitlePanel.transform, "Speaker", root.Character.Identity.DisplayName, new Vector2(0, 46), new Vector2(860, 27), 18, TextAlignmentOptions.Left); speaker.color = UI.Accent;
            subtitle = UI.Text(subtitlePanel.transform, "Body", "", new Vector2(0, -12), new Vector2(860, 90), 26, TextAlignmentOptions.TopLeft);
            subtitle.overflowMode = TextOverflowModes.Page;
            inputPanel = UI.Panel(canvas.transform, "Input", new Vector2(0, -247), new Vector2(950, 132)).rectTransform;
            inputFade = inputPanel.gameObject.AddComponent<CanvasGroup>();
            var viewport = UI.Rect(inputPanel, "Text Viewport", new Vector2(-57, 8), new Vector2(780, 73)); viewport.gameObject.AddComponent<RectMask2D>();
            var inputText = UI.Text(viewport, "Text", "", Vector2.zero, new Vector2(780, 73), 25, TextAlignmentOptions.TopLeft);
            var placeholder = UI.Text(viewport, "Placeholder", "老师，想说些什么？", Vector2.zero, new Vector2(780, 73), 25, TextAlignmentOptions.TopLeft); placeholder.color = new Color(.65f, .7f, .8f);
            input = inputPanel.gameObject.AddComponent<TMP_InputField>(); input.textViewport = viewport; input.textComponent = (TextMeshProUGUI)inputText;
            input.placeholder = placeholder; input.fontAsset = UI.Chinese; input.richText = false; input.lineType = TMP_InputField.LineType.MultiLineNewline; input.characterLimit = 12000;
            UI.Button(inputPanel, "Send", "发送", new Vector2(390, 8), new Vector2(100, 55), () => { if (inputGuard.CanSubmit(Time.frameCount)) _ = SendAsync(input.text.TrimEnd('\r','\n')); });
            UI.Text(inputPanel, "InputHint", "Enter 发送  ·  Shift+Enter 换行  ·  Esc 收起", new Vector2(0, -46), new Vector2(870, 25), 15);
            inputPanel.gameObject.SetActive(false);
            menuPanel = UI.Panel(canvas.transform, "Menu", Vector2.zero, new Vector2(430, 500)).rectTransform;
            UI.Text(menuPanel, "Title", "陪伴空间", new Vector2(0, 195), new Vector2(350, 45), 29);
            historyButton = UI.Button(menuPanel, "MomoTalk", "MomoTalk", new Vector2(0, 115), new Vector2(330, 52), () => openHistory?.Invoke()); historyButton.interactable = false;
            settingsButton = UI.Button(menuPanel, "Settings", "设置", new Vector2(0, 50), new Vector2(330, 52), () => openSettings?.Invoke()); settingsButton.interactable = false;
            UI.Button(menuPanel, "Library", "资料与回顾", new Vector2(0, -15), new Vector2(330, 52), OpenLibrary);
            var navigation = GetComponent<ModeSceneController>();
            UI.Button(menuPanel, "ReturnHome", "返回 Home", new Vector2(0, -80), new Vector2(330, 52), navigation.ReturnHome);
            UI.Button(menuPanel, "Exit", "退出", new Vector2(0, -145), new Vector2(330, 52), navigation.Quit);
            menuPanel.gameObject.SetActive(false);
            BuildAdditionalPanels();
            BuildImmersiveUI();
            BuildMaintenanceUI();
            imeKeyboard = Keyboard.current;
            if (imeKeyboard != null) imeKeyboard.onIMECompositionChange += OnComposition;
            ResolveVisual();
        }
        private void OnComposition(UnityEngine.InputSystem.LowLevel.IMECompositionString composition)
        { imeComposition = composition.ToString(); inputGuard.Observe(imeComposition, Time.frameCount); }
        private void Update()
        {
            if (root == null || subtitle == null) return;
            UpdateWindowView();
            UpdateImmersiveUI();
            UpdateMaintenanceUI();
            clock.text = DateTime.Now.ToString("HH:mm");
            if(seasideClock!=null)seasideClock.text=clock.text;
            speaker.text = root.Character.Identity.DisplayName;
            hint.transform.parent.gameObject.SetActive(WindowView==null && Time.unscaledTime < hintUntil && !inputOpen && !menuOpen && panelName == null);
            inputGuard.Observe(string.IsNullOrEmpty(imeComposition) ? Input.compositionString : imeComposition, Time.frameCount);
            var keyboard = Keyboard.current;
            if (keyboard != null && Application.isFocused && root.IsReady && !root.Controller.IsSwitching && !leaving)
            {
                if (keyboard.escapeKey.wasPressedThisFrame && inputGuard.CanSubmit(Time.frameCount)) Back();
                else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                {
                    if (!inputOpen && panelName == null && !HasSelectedControl()) OpenInput();
                    else if (inputOpen && input.isFocused && Time.frameCount > inputOpenedFrame && inputGuard.CanSubmit(Time.frameCount) && !keyboard.shiftKey.isPressed)
                        _ = SendAsync(input.text.TrimEnd('\r','\n'));
                }
            }
            if (inputOpen) inputFade.alpha = Mathf.MoveTowards(inputFade.alpha, 1, Time.unscaledDeltaTime * 8);
            UpdateVoicePresentation();
            bool show = Time.unscaledTime < subtitleUntil && !inputOpen && !menuOpen && panelName == null && (WindowView==null||WindowView.AtCharacter);
            subtitleFade.alpha = Mathf.MoveTowards(subtitleFade.alpha, show ? 1 : 0, Time.unscaledDeltaTime * 5);
            if (dialogueState == CharacterVisualState.Idle && show && Time.unscaledTime >= nextPage && subtitle.pageToDisplay < subtitle.textInfo.pageCount)
            { subtitle.pageToDisplay++; nextPage = Time.unscaledTime + 5; }
            ResolveVisual();
        }
        public void OpenInput()
        {
            if(WindowView!=null&&!WindowView.AtCharacter&&panelName==null){queuedWindowInput=true;WindowView.GoCharacter();return;}
            if (input == null || menuOpen || panelName != null || leaving) return;
            visual?.Loader?.SkipIntro();
            visual?.Loader?.Character?.GetComponent<SeasideSpaceView>()?.FinishEntrance();
            inputOpen = true; inputOpenedFrame = Time.frameCount; inputFade.alpha = 0;
            inputRevision++;inputFade.blocksRaycasts=true;inputFade.interactable=true;
            inputPanel.gameObject.SetActive(true); input.ActivateInputField();
            if (imeKeyboard != null) imeKeyboard.SetIMEEnabled(true);
            ResolveVisual();
        }
        private void CloseInput()
        {
            inputOpen = false; input.DeactivateInputField();inputFade.blocksRaycasts=false;inputFade.interactable=false;
            StartCoroutine(FadeInputAway(++inputRevision));
            if (imeKeyboard != null) imeKeyboard.SetIMEEnabled(false);
        }
        public void OpenMenu()
        {
            if(!TryNavigate(OpenMenu))return;
            ReleaseMusicFocus();
            if (inputOpen) CloseInput(); CloseAdditionalPanels(); panelName = null;
            if(WindowView!=null){menuOpen=true;menuPanel.gameObject.SetActive(false);WindowView.PanelOpen=false;WindowView.GoWindow();ResolveVisual();return;}
            menuOpen = true; menuPanel.gameObject.SetActive(true); ResolveVisual();
        }
        public void Back()
        {
            if(ConfirmationVisible){CancelConfirmation();return;}
            if(panelName=="Music"||panelName=="Memories"||panelName=="Maintenance"){OpenDataSettings();return;}
            if(WindowView!=null)
            {
                queuedWindowInput=false;
                if(inputOpen)CloseInput();
                else if(panelName!=null){OpenMenu();}
                else if(WindowView.AtCharacter)OpenMenu();
                else {WindowView.GoCharacter();menuOpen=false;}
                ResolveVisual();return;
            }
            if (inputOpen) CloseInput();
            else if (panelName != null) OpenMenu();
            else { menuOpen = !menuOpen; menuPanel.gameObject.SetActive(menuOpen); }
            ResolveVisual();
        }
        public Task SendAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || root == null || !root.IsReady || root.Controller.IsSwitching || root.PlayerWorkBusy || voiceUiBusy || leaving) return Task.CompletedTask;
            string draft = message;
            voicePlayer?.StopSpeech(); voiceForTurn = false; currentBody = "";
            long id = ++generation; turnCancellation?.Cancel(); var previous = currentTurn;
            var scope = CancellationTokenSource.CreateLinkedTokenSource(root.ApplicationToken, root.Controller.ActiveModeToken, destroyCancellationToken);
            turnCancellation = scope;
            if (inputOpen) CloseInput(); input.text = "";
            dialogueState = CharacterVisualState.Thinking; subtitle.text = ""; subtitleUntil = 0; emotion = root.Character.State.Mood;
            LastDialogueResult = null;
            currentTurn = RunTurnAsync(draft, id, previous, scope); ResolveVisual(); return currentTurn;
        }
        private async Task RunTurnAsync(string message, long id, Task previous, CancellationTokenSource scope)
        {
            var latency = System.Diagnostics.Stopwatch.StartNew(); bool firstChinese = true;
            try
            {
                try { await previous; } catch (OperationCanceledException) { }
                scope.Token.ThrowIfCancellationRequested(); var body = new StringBuilder();
                BeginVoice(id);
                await foreach (var chunk in root.Character.StreamMessageAsync(message, InteractionSource.Companion, scope.Token))
                {
                    if (this == null || id != generation || scope.IsCancellationRequested) return;
                    if (chunk.Presentation != null)
                    {
                        emotion = chunk.Presentation.Emotion; voicePlayer.SetEmotion(id, emotion.ToString().ToLowerInvariant());
                        voicePlayer.SetVoiceIntent(id, chunk.Presentation.VoiceIntent); visual.PlayAction(chunk.Presentation.Action);
                    }
                    if (!chunk.IsFinal && chunk.Text.Length > 0)
                    {
                        if (firstChinese) { firstChinese = false; root.Logger?.Info("dialogue chinese_first_ms=" + latency.ElapsedMilliseconds); }
                        body.Append(chunk.Text); currentBody = body.ToString();
                        if (voiceForTurn) await voicePlayer.AppendAsync(id, chunk.Text);
                        if (this == null || id != generation || scope.IsCancellationRequested) return;
                        // Receive and persist Chinese immediately; voice-mode subtitles are revealed once,
                        // by actual playback. Translation/synthesis waits cannot flash the full reply first.
                        if (!voiceForTurn) ShowBodyText(currentBody, false);
                        if (!voiceForTurn) dialogueState = CharacterVisualState.Speaking;
                    }
                    if (chunk.IsFinal)
                    {
                        LastDialogueResult = chunk.Result;
                        if (chunk.Result.Success)
                        {
                            currentBody = chunk.Result.Text;
                            if (voiceForTurn) await voicePlayer.AppendAsync(id, "", true, chunk.Result.RequestId);
                            if (this == null || id != generation || scope.IsCancellationRequested) return;
                            if (!voiceForTurn) { ShowBodyText(chunk.Result.Text, true); dialogueState = CharacterVisualState.Idle; }
                            emotion = root.Character.State.Mood;
                        }
                        else
                        {
                            voiceForTurn = false; voicePlayer.StopSpeech(); dialogueState = CharacterVisualState.Idle;
                            input.text = message;
                            if (chunk.Result.ErrorCode == "Canceled") { subtitleUntil = 0; emotion = root.Character.State.Mood; }
                            else ShowFeedback(chunk.Result.ErrorCode == "LLMNotConfigured" ? "老师，还需要先设置连接。" : "刚才没能说完，老师愿意再试一次吗？");
                        }
                    }
                }
            }
            catch (OperationCanceledException) { if (this != null && id == generation) { voiceForTurn = false; voicePlayer?.StopSpeech(); } }
            catch (Exception error)
            { root.Logger?.Error("companion dialogue failed", error); if (this != null && id == generation) { voiceForTurn = false; voicePlayer?.StopSpeech(); input.text = message; ShowFeedback("刚才没能说完，老师愿意再试一次吗？"); } }
            finally
            {
                if (this != null && id == generation) { dialogueState = voiceForTurn ? CharacterVisualState.Thinking : CharacterVisualState.Idle; if (scope.IsCancellationRequested) { voiceForTurn = false; voicePlayer?.StopSpeech(); subtitleUntil = 0; emotion = root.Character.State.Mood; } ResolveVisual(); }
                if (ReferenceEquals(turnCancellation, scope)) turnCancellation = null;
                scope.Dispose();
            }
        }
        private void ShowFeedback(string text)
        { subtitle.text = text; subtitle.pageToDisplay = 1; subtitleUntil = Time.unscaledTime + 5; emotion = root.Character.State.Mood; }
        private void ResolveVisual()
        {
            if (visual == null) return;
            bool captured = inputOpen || menuOpen || panelName != null || (WindowView!=null&&!WindowView.AtCharacter);
            visual.SetUiCapture(captured); visual.SetEmotion(emotion);
            var state = panelName == "MomoTalk" ? CharacterVisualState.MomoTalk : menuOpen || panelName != null ? CharacterVisualState.Menu :
                inputOpen ? CharacterVisualState.Listening : visual.Loader.Expression != null && visual.Loader.Expression.TouchActive ? CharacterVisualState.Touched : dialogueState;
            visual.SetState(state);
        }
        private void OnApplicationFocus(bool focused) { if (!focused) visual?.CancelInteraction(); }
        private void OnDestroy()
        {
            generation++; turnCancellation?.Cancel(); ReleaseAdditionalPanels();
            if(spacePreviewTexture!=null)Destroy(spacePreviewTexture);
            voiceForTurn = false; voicePlayer?.StopSpeech();
            if (root != null && root.ActiveVoice == voicePlayer) root.ActiveVoice = null;
            if (imeKeyboard != null) { imeKeyboard.onIMECompositionChange -= OnComposition; imeKeyboard.SetIMEEnabled(false); }
        }
    }
}
