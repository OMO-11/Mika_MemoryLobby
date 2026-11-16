using System;
using GiftCompanion.Core.Config;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UI = GiftCompanion.SharedUI.CompanionUIFactory;

namespace GiftCompanion.Companion
{
    public sealed partial class CompanionScreen
    {
        private RectTransform displayPanel;
        private CompanionDisplaySettings displayDraft;
        private TMP_Text displayNote, exposureLabel, compressionLabel, diarySizeLabel, diarySpacingLabel;
        private void BuildDisplayPanel()
        {
            displayPanel = BuildPanel("Display", "画面与阅读");
            exposureLabel = UI.Text(displayPanel, "ExposureLabel", "", new Vector2(0, 208), new Vector2(780, 40), 21);
            AddSlider(displayPanel, "Exposure", new Vector2(0, 164), .75f, 1.1f, v => { if (displayDraft == null) return; displayDraft.Exposure = v; PreviewDisplay(); });
            compressionLabel = UI.Text(displayPanel, "CompressionLabel", "", new Vector2(0, 112), new Vector2(780, 40), 21);
            AddSlider(displayPanel, "Compression", new Vector2(0, 68), 0, 1, v => { if (displayDraft == null) return; displayDraft.HighlightCompression = v; PreviewDisplay(); });
            diarySizeLabel = UI.Text(displayPanel, "DiarySizeLabel", "", new Vector2(0, 16), new Vector2(780, 40), 21);
            AddSlider(displayPanel, "DiarySize", new Vector2(0, -28), 20, 30, v => { if (displayDraft == null) return; displayDraft.DiaryFontSize = Mathf.Round(v); PreviewDisplay(); });
            diarySpacingLabel = UI.Text(displayPanel, "DiarySpacingLabel", "", new Vector2(0, -80), new Vector2(780, 40), 21);
            AddSlider(displayPanel, "DiarySpacing", new Vector2(0, -124), 0, 20, v => { if (displayDraft == null) return; displayDraft.DiaryLineSpacing = Mathf.Round(v); PreviewDisplay(); });
            UI.Button(displayPanel, "SaveDisplay", "保存并应用", new Vector2(-280, -194), new Vector2(250, 44), () =>
            {
                if (displayDraft == null || panelCancellation == null) return;
                try { root.Display.Save(displayDraft, panelCancellation.Token); displayNote.text = "已保存。画面立即生效，日记下次打开应用字号；返回不会丢失已保存设置。"; }
                catch (Exception e) { root.Logger?.Info("display save failed type=" + e.GetType().Name); displayNote.text = "暂时无法保存，当前文件已保留，请检查权限后重试。"; }
            });
            UI.Button(displayPanel, "DefaultDisplay", "预览默认显示", new Vector2(0, -194), new Vector2(250, 44), () => { displayDraft = new CompanionDisplaySettings(); RefreshDisplaySliders(); });
            UI.Button(displayPanel, "OriginalTone", "预览原版画面", new Vector2(280, -194), new Vector2(250, 44), () =>
            { if (displayDraft == null) return; displayDraft.Exposure = .95f; displayDraft.HighlightCompression = .35f; RefreshDisplaySliders(); });
            displayNote = UI.Text(displayPanel, "Note", "", new Vector2(0, -260), new Vector2(830, 54), 17);
            ApplyDisplay(root.Display.Value);
        }
        public void OpenDisplaySettings()
        {
            if(!TryNavigate(OpenDisplaySettings))return;
            ShowPanel("Display", displayPanel); displayDraft = root.Display.Value.Copy(); settingsDraft=root.Config.Configuration.Copy(); motionDraft=root.VisualSettings.Value.ReduceMotion; RefreshDisplaySliders();RefreshReducedMotionLabel();
            displayNote.text = root.Display.Issue ?? "拖动可预览，保存后保留。未保存离页时会询问是否放弃修改。";
        }
        private void RefreshDisplaySliders()
        {
            FindDisplaySlider("Exposure").SetValueWithoutNotify(displayDraft.Exposure);
            FindDisplaySlider("Compression").SetValueWithoutNotify(displayDraft.HighlightCompression);
            FindDisplaySlider("DiarySize").SetValueWithoutNotify(displayDraft.DiaryFontSize);
            FindDisplaySlider("DiarySpacing").SetValueWithoutNotify(displayDraft.DiaryLineSpacing);
            if(settingsDraft!=null)FindDisplaySlider("SubtitleSize").SetValueWithoutNotify(settingsDraft.SubtitleFontSize);
            PreviewDisplay();
        }
        private Slider FindDisplaySlider(string name)=>System.Array.Find(displayPanel.GetComponentsInChildren<Slider>(true),slider=>slider.name==name);
        private void PreviewDisplay()
        {
            ApplyDisplay(displayDraft);
            if(settingsDraft!=null){subtitle.fontSize=settingsDraft.SubtitleFontSize;sizeLabel.text="字幕文字大小  "+settingsDraft.SubtitleFontSize;}
            exposureLabel.text = "画面亮度  " + Mathf.RoundToInt(displayDraft.Exposure * 100) + "%";
            compressionLabel.text = "高光柔和程度  " + Mathf.RoundToInt(displayDraft.HighlightCompression * 100) + "%";
            diarySizeLabel.text = "日记文字大小  " + displayDraft.DiaryFontSize;
            diarySpacingLabel.text = "日记行距  " + displayDraft.DiaryLineSpacing;
        }
        private static void ApplyDisplay(CompanionDisplaySettings settings)
        {
            var tone = FindFirstObjectByType<CompanionTone>();
            if (tone != null) { tone.exposure = settings.Exposure; tone.highlightCompression = settings.HighlightCompression; }
        }
    }
}
