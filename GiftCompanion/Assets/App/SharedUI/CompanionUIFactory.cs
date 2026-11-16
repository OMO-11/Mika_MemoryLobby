using System;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace GiftCompanion.SharedUI
{
    public static class CompanionUIFactory
    {
        private static TMP_FontAsset chinese;
        public static TMP_FontAsset Chinese
        {
            get
            {
                if (chinese != null) return chinese;
                var source = Resources.Load<Font>("Fonts/NotoSansCJKsc-Regular");
                if (source == null) throw new InvalidOperationException("Chinese font missing.");
                chinese = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                chinese.name = "Mika Chinese Dynamic";
                return chinese;
            }
        }
        public static readonly Color Ink = new Color(.055f, .075f, .13f, .95f);
        public static readonly Color Accent = new Color(.64f, .80f, 1);
        public static Canvas Canvas(string name)
        {
            foreach (var old in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)) old.gameObject.SetActive(false);
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            return canvas;
        }
        public static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
        }
        public static Image Panel(Transform parent, string name, Vector2 position, Vector2 size, Color? color = null, bool raycast = true)
        {
            var image = Rect(parent, name, position, size).gameObject.AddComponent<Image>();
            image.color = color ?? Ink; image.raycastTarget = raycast; return image;
        }
        public static TMP_Text Text(Transform parent, string name, string value, Vector2 position, Vector2 size, float fontSize = 24, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var text = Rect(parent, name, position, size).gameObject.AddComponent<TextMeshProUGUI>();
            text.font = Chinese; text.fontSize = fontSize; text.text = value; text.color = Color.white;
            text.alignment = alignment; text.richText = false; text.raycastTarget = false;
            text.overflowMode = TextOverflowModes.Ellipsis; return text;
        }
        public static Button Button(Transform parent, string name, string caption, Vector2 position, Vector2 size, Action clicked)
        {
            var image = Panel(parent, name, position, size, new Color(.16f, .22f, .34f, .96f));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(.76f, .88f, 1); colors.pressedColor = new Color(.5f, .7f, 1); colors.disabledColor = new Color(.5f, .55f, .65f, .55f); button.colors = colors;
            var label = Text(button.transform, "Caption", caption, Vector2.zero, size - new Vector2(12, 6), 22);
            label.enableAutoSizing = true; label.fontSizeMin = 14; label.fontSizeMax = 22;
            button.onClick.AddListener(() => clicked()); return button;
        }
        public static RectTransform Stretch(RectTransform rect, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom); rect.offsetMax = new Vector2(-right, -top); return rect;
        }
    }
}
