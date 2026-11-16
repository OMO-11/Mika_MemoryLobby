using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.AgentPet.Window;
using GiftCompanion.Bootstrap;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using GiftCompanion.Core.Agent;
using GiftCompanion.SharedUI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UI = GiftCompanion.SharedUI.CompanionUIFactory;

namespace GiftCompanion.AgentPet
{
    [DefaultExecutionOrder(500)]
    public sealed partial class PetScreen : MonoBehaviour
    {
        private AppRoot root;
        private PetPresentation visual;
        private DesktopWindowPrototype host;
        private Canvas canvas;
        private RectTransform bubble;
        private CanvasGroup fade;
        private TMP_Text title, output;
        private TMP_InputField input;
        private readonly CompanionInputGuard guard = new CompanionInputGuard();
        private Keyboard ime;
        private string composition = "", panel = "chat", text = "";
        private Task turn = Task.CompletedTask;
        private CancellationTokenSource cancellation;
        private long generation;
        private int openedFrame;
        private float showUntil, lastActivity;
        private bool open, editing, leaving;
        private bool allowActions;
        private string taskAction = "Idle";
        private string toolOutcome = "";
        private int previousFrameRate;
        private int previousVsync, lastScreenWidth, lastScreenHeight;
        private Button allowButton;
        private Vector3 baseCamera;
        public bool Ready => input != null && visual?.Body != null;
        public bool IsOpen => open;
        public bool IsEditing => editing;
        public string OutputText => text;
        public string InputDraft => input == null ? "" : input.text;
        public Task ActiveTurn => turn;
        public DialogueResult LastResult { get; private set; }
        public RectTransform Bubble => bubble;
        public bool Dragging { get; private set; }
        public string PanelName => panel;
        partial void BuildPetInteraction();
        partial void UpdatePetInteraction();
        partial void ReleasePetInteraction();
        partial void OpenTools();

        private void Start()
        {
            root = AppRoot.Instance; if (root == null || !root.IsReady) return;
            visual = FindFirstObjectByType<PetPresentation>(); host = root.GetComponent<DesktopWindowPrototype>();
            if (host == null) host = root.gameObject.AddComponent<DesktopWindowPrototype>();
            host.InputRequested += OpenInput;
            previousFrameRate = Application.targetFrameRate;
            previousVsync = QualitySettings.vSyncCount; QualitySettings.vSyncCount = 0;
            canvas = UI.Canvas("Pet Bubble"); var scaler = canvas.GetComponent<CanvasScaler>(); scaler.referenceResolution = new Vector2(600, 500);
            bubble = UI.Panel(canvas.transform, "Bubble", new Vector2(-130, 0), new Vector2(330, 350), new Color(.045f, .065f, .11f, .95f)).rectTransform;
            var image = bubble.GetComponent<Image>(); image.sprite = Rounded(); image.type = Image.Type.Sliced;
            fade = bubble.gameObject.AddComponent<CanvasGroup>(); fade.alpha = 0; fade.blocksRaycasts = false;
            title = UI.Text(bubble, "Name", root.Character.Identity.DisplayName, new Vector2(-18, 143), new Vector2(260, 30), 19, TextAlignmentOptions.Left); title.color = UI.Accent;
            UI.Button(bubble, "Menu", "···", new Vector2(136, 143), new Vector2(34, 28), OpenMenu);
            allowButton = UI.Button(bubble, "AllowActions", "允许桌面操作：关（仅本次）", new Vector2(0, 111), new Vector2(296, 32), () => SetAllowActions(!allowActions));
            var allowLabel = allowButton.GetComponentInChildren<TMP_Text>(); allowLabel.fontSizeMin = 12; allowLabel.fontSizeMax = 16;
            output = UI.Text(bubble, "Reply", "", new Vector2(0, 20), new Vector2(296, 150), 20, TextAlignmentOptions.TopLeft); output.overflowMode = TextOverflowModes.Page;
            var viewport = UI.Rect(bubble, "Input View", new Vector2(0, -87), new Vector2(296, 65)); viewport.gameObject.AddComponent<RectMask2D>();
            var content = UI.Text(viewport, "Input Text", "", Vector2.zero, new Vector2(292, 65), 20, TextAlignmentOptions.TopLeft);
            var placeholder = UI.Text(viewport, "Placeholder", "老师，想说些什么？", Vector2.zero, new Vector2(292, 65), 19, TextAlignmentOptions.TopLeft); placeholder.color = new Color(.7f, .75f, .85f);
            input = viewport.gameObject.AddComponent<TMP_InputField>(); input.textViewport = viewport; input.textComponent = (TextMeshProUGUI)content; input.placeholder = placeholder; input.fontAsset = UI.Chinese;
            input.richText = false; input.lineType = TMP_InputField.LineType.MultiLineNewline; input.characterLimit = 12000;
            input.onSelect.AddListener(_ => { if (!editing && panel == "chat") OpenInput(); });
            UI.Button(bubble, "Send", "发送", new Vector2(102, -145), new Vector2(78, 34), () => { if (guard.CanSubmit(Time.frameCount)) _ = SendAsync(input.text.TrimEnd('\r', '\n')); });
            UI.Button(bubble, "Close", "收起", new Vector2(-102, -145), new Vector2(78, 34), Hide);
            UI.Button(bubble, "Previous", "‹", new Vector2(-26, -145), new Vector2(38, 34), () => output.pageToDisplay = Mathf.Max(1, output.pageToDisplay - 1));
            UI.Button(bubble, "Next", "›", new Vector2(26, -145), new Vector2(38, 34), () => output.pageToDisplay = Mathf.Min(output.textInfo.pageCount, output.pageToDisplay + 1));
            BuildMenu(); baseCamera = Camera.main.transform.position; Reposition(); BuildPetInteraction();
            if (FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length == 0) Camera.main.gameObject.AddComponent<AudioListener>();
            ime = Keyboard.current; if (ime != null) ime.onIMECompositionChange += OnComposition;
            Say("老师，我在这里。\n" + root.Config.Configuration.PetHotkey + " 可以叫我。", 8); lastActivity = Time.unscaledTime;
        }
        private static Sprite rounded;
        private static Sprite Rounded()
        {
            if (rounded != null) return rounded;
            var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false); texture.name = "Pet rounded panel";
            for (int y = 0; y < 32; y++) for (int x = 0; x < 32; x++)
            { float dx = Mathf.Max(0, Mathf.Abs(x - 15.5f) - 7.5f), dy = Mathf.Max(0, Mathf.Abs(y - 15.5f) - 7.5f); texture.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(8 - Mathf.Sqrt(dx * dx + dy * dy)))); }
            texture.Apply(); rounded = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect, new Vector4(10, 10, 10, 10)); return rounded;
        }
        private void OnComposition(UnityEngine.InputSystem.LowLevel.IMECompositionString value) { composition = value.ToString(); guard.Observe(composition, Time.frameCount); }
        private void Update()
        {
            if (!Ready) return;
            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight) Reposition();
            guard.Observe(string.IsNullOrEmpty(composition) ? UnityEngine.Input.compositionString : composition, Time.frameCount);
            var keys = Keyboard.current;
            if (keys != null && Application.isFocused && !root.Controller.IsSwitching)
            {
                if (keys.escapeKey.wasPressedThisFrame) { if (panel != "chat") BackToChat(); else if (editing) EndEditing(); else if (open) Hide(); else OpenMenu(); }
                if (editing && input.isFocused && Time.frameCount > openedFrame && guard.CanSubmit(Time.frameCount) && !keys.shiftKey.isPressed && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame)) _ = SendAsync(input.text.TrimEnd('\r', '\n'));
            }
            if (open && panel == "chat" && !editing && turn.IsCompleted && Time.unscaledTime > showUntil && !PointerOnBubble()) Hide();
            fade.alpha = Mathf.MoveTowards(fade.alpha, open && !Dragging ? 1 : 0, Time.unscaledDeltaTime * 8); fade.blocksRaycasts = open && !Dragging;
            title.text = root.Character.Identity.DisplayName;
            Application.targetFrameRate = Time.unscaledTime - lastActivity < 3 || open || !turn.IsCompleted || Dragging ? root.PetSettings.Value.ActiveFrameRate : root.PetSettings.Value.IdleFrameRate;
            UpdatePetInteraction();
        }
        public bool PointerOnBubble() => open && !Dragging && RectTransformUtility.RectangleContainsScreenPoint(bubble, CursorPoint(), null);
        public void Reposition()
        {
            lastScreenWidth = Screen.width; lastScreenHeight = Screen.height;
            bool right = true;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var bounds = PetNative.Bounds; var work = PetNative.WorkArea; right = bounds.Left + bounds.Width / 2 > work.Left + work.Width / 2;
#endif
            bubble.anchoredPosition = new Vector2(right ? -130 : 130, 0);
            var camera = Camera.main;
            if (camera != null && camera.orthographic) camera.transform.position = baseCamera - camera.transform.right * (right ? .27f : -.27f) * (2 * camera.orthographicSize * camera.aspect);
        }
        public void OpenInput()
        {
            if (!Ready || leaving || root.Controller.IsSwitching) return;
            BackToChat(); open = true; editing = true; openedFrame = Time.frameCount; host.SetInputFocus(true);
            input.ActivateInputField(); ime?.SetIMEEnabled(true); lastActivity = Time.unscaledTime;
            if (string.IsNullOrEmpty(text)) Say("老师，今天需要我做什么？", 30);
        }
        public void Hide() { panelGeneration++; EndEditing(); open = false; ClosePetPanels(); }
        private void EndEditing() { editing = false; input.DeactivateInputField(); ime?.SetIMEEnabled(false); host.SetInputFocus(false); showUntil = Time.unscaledTime + root.PetSettings.Value.ReplySeconds; }
        private void Say(string value, float seconds = -1)
        { text = value ?? ""; output.text = text; output.pageToDisplay = 1; open = true; showUntil = Time.unscaledTime + (seconds < 0 ? root.PetSettings.Value.ReplySeconds : seconds); lastActivity = Time.unscaledTime; }
        public Task SendAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || !Ready || leaving || !root.IsReady || root.PlayerWorkBusy || root.Controller.IsSwitching) return Task.CompletedTask;
            long id = ++generation; cancellation?.Cancel(); Task previous = turn;
            var scope = CancellationTokenSource.CreateLinkedTokenSource(root.ApplicationToken, root.Controller.ActiveModeToken, destroyCancellationToken); cancellation = scope;
            EndEditing(); input.text = ""; BackToChat(); Say(""); visual.PlayAction("Thinking"); LastResult = null;
            taskAction = "Thinking";
            toolOutcome = "";
            AgentTurnContext tools = allowActions ? new AgentTurnContext { Confirm = ConfirmToolAsync, OutcomeObserved = value => { if (this != null && id == generation) toolOutcome = value; }, StateChanged = state => { if (this != null && id == generation) { taskAction = state; if (!Dragging && !gesture.IsHeld) visual.PlayAction(state); } } } : null;
            SetAllowActions(false);
            turn = RunAsync(message, id, previous, scope, tools); return turn;
        }
        public void SetAllowActions(bool value) { allowActions = value; if (allowButton != null) allowButton.GetComponentInChildren<TMP_Text>().text = value ? "允许桌面操作：开（仅本次）" : "允许桌面操作：关（仅本次）"; }
        private async Task RunAsync(string message, long id, Task previous, CancellationTokenSource scope, AgentTurnContext tools)
        {
            try
            {
                try { await previous; } catch (OperationCanceledException) { }
                scope.Token.ThrowIfCancellationRequested(); var body = new StringBuilder();
                await foreach (var chunk in root.Character.StreamMessageAsync(message, InteractionSource.AgentPet, scope.Token, tools))
                {
                    if (this == null || id != generation || scope.IsCancellationRequested) return;
                    if (!chunk.IsFinal) { body.Append(chunk.Text); text = body.ToString(); output.text = text; }
                    else
                    {
                        LastResult = chunk.Result;
                        if (chunk.Result.Success) { text = chunk.Result.Text; output.text = text; }
                        else { input.text = message; Say(tools != null && toolOutcome.Length > 0 ? toolOutcome + "\n后面的回复没能说完；已发起的操作不会重做。" : chunk.Result.ErrorCode == "LLMNotConfigured" ? "老师，还需要在陪伴空间设置好连接。" : "刚才没能说完，老师愿意再试一次吗？"); }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { root.Logger?.Error("pet dialogue failed", error); if (this != null && id == generation) { input.text = message; Say("刚才没能说完，老师愿意再试一次吗？"); } }
            finally
            {
                if (this != null && id == generation)
                {
                    showUntil = Time.unscaledTime + root.PetSettings.Value.ReplySeconds;
                    bool feedback = tools != null && (taskAction == "Success" || taskAction == "Confused");
                    if (!Dragging && !gesture.IsHeld) { if (feedback) { Play(taskAction); reactionUntil = Time.unscaledTime + 1.6f; } else visual.ReleaseInteraction(); }
                    taskAction = "Idle";
                }
                if (ReferenceEquals(cancellation, scope)) cancellation = null; scope.Dispose();
            }
        }
        public void StopCurrent() { generation++; cancellation?.Cancel(); CloseToolsPanel(); visual.ReleaseInteraction(); Say(toolOutcome.Length > 0 ? toolOutcome + "\n好，后面的先停在这里；已发起的操作不会重做。" : "好，先停在这里。"); }
        public void SetDragging(bool value) { Dragging = value; lastActivity = Time.unscaledTime; if (!value && !leaving) { Reposition(); host.SavePosition(); } }
        private void OnApplicationFocus(bool focused) { if (!focused && editing) EndEditing(); if (!focused) ReleasePetInteraction(); }
        private void OnDestroy()
        {
            leaving = true; generation++; cancellation?.Cancel(); ReleasePetInteraction(); if (host != null) { host.InputRequested -= OpenInput; host.SetInputFocus(false); }
            if (ime != null) { ime.onIMECompositionChange -= OnComposition; ime.SetIMEEnabled(false); }
            Application.runInBackground = previousBackground; InputSystem.settings.backgroundBehavior = previousInputBackground;
            Application.targetFrameRate = previousFrameRate;
            QualitySettings.vSyncCount = previousVsync;
        }
    }
}
