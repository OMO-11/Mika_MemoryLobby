using GiftCompanion.Core.Config;
using GiftCompanion.SharedUI;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;

namespace GiftCompanion.Companion
{
    public static class CompanionVisualFoundation
    {
        static TMP_FontAsset font;
        public static TMP_FontAsset Font
        {
            get
            {
                if (font != null) return font;
                var source = Resources.Load<Font>("MikaVisual/Lolita");
                if (source == null) return CompanionUIFactory.Chinese;
                font = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
                font.name = "Mika Lolita Dynamic";
                font.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset> { CompanionUIFactory.Chinese };
                return font;
            }
        }
        public static string DefaultJson => Resources.Load<TextAsset>("MikaVisual/visual_defaults").text;
        public static Color Color(string hex, float alpha = 1)
        { ColorUtility.TryParseHtmlString(hex, out var color); color.a = alpha; return color; }
        // Reference coordinates: top-left origin, y down. Unity vectors are returned in the same logical convention.
        public static Vector2 Project(Vector2 point, VisualSpace space, float progress, float depth = 1)
        {
            float p = Mathf.Clamp01(progress), scale = Mathf.Lerp(1, space.Zoom, p) * (1 + (depth - 1) * p);
            Vector2 delta = point - Vector2.Lerp(new Vector2(640,360), new Vector2(space.CenterX,space.CenterY), p);
            float angle = space.Rotation * p * Mathf.Deg2Rad;
            return new Vector2(640,360) + new Vector2(Mathf.Cos(angle)*delta.x-Mathf.Sin(angle)*delta.y, Mathf.Sin(angle)*delta.x+Mathf.Cos(angle)*delta.y)*scale;
        }
        public static Vector2 Unproject(Vector2 point, VisualSpace space, float progress)
        {
            float p = Mathf.Clamp01(progress), angle = -space.Rotation * p * Mathf.Deg2Rad;
            var d = (point-new Vector2(640,360))/Mathf.Lerp(1,space.Zoom,p);
            return Vector2.Lerp(new Vector2(640,360),new Vector2(space.CenterX,space.CenterY),p)+new Vector2(Mathf.Cos(angle)*d.x-Mathf.Sin(angle)*d.y,Mathf.Sin(angle)*d.x+Mathf.Cos(angle)*d.y);
        }
        // Fit the complete logical UI inside the window, keeping every control reachable.
        public static Rect Viewport(float width, float height)
        { float scale = Mathf.Min(width/1280, height/720); return new Rect((width-1280*scale)/2,(height-720*scale)/2,1280*scale,720*scale); }
        // Cover the window with the scene. Negative offsets represent cropped scene edges.
        public static Rect SceneViewport(float width, float height)
        { float scale = Mathf.Max(width/1280, height/720); return new Rect((width-1280*scale)/2,(height-720*scale)/2,1280*scale,720*scale); }
        public static void Style(TMP_Text text, VisualTheme theme, float size)
        { text.font = Font; text.fontSize = size; text.color = Color(theme.Ink); text.richText = false; text.raycastTarget = false; }
        public static void Style(Button button, VisualTheme theme)
        {
            var colors = button.colors; colors.normalColor = new Color(1,1,1,theme.MenuAlpha); colors.highlightedColor = Color(theme.Focus);
            colors.selectedColor = Color(theme.Focus); colors.pressedColor = Color(theme.Accent); colors.disabledColor = Color(theme.Ink,theme.DisabledAlpha); button.colors = colors;
        }
        public static Sprite Icon(string name) => Resources.Load<Sprite>("MikaVisual/Icons/" + name);
        // Parent uses the centered 1280x720 logical canvas. Caller supplies existing business routes.
        public static RectTransform CreateMenu(Transform parent, CompanionVisualSettings settings, string spaceId, float progress, System.Action<int> clicked)
        {
            var space=settings.FindSpace(spaceId); var theme=settings.Theme;
            var origin=space==null ? new Vector2(60,150) : Project(new Vector2(space.MenuX,space.MenuY),space,progress);
            float scale=space==null?1:Mathf.Lerp(1,space.Zoom,Mathf.Clamp01(progress));
            var group=CompanionUIFactory.Rect(parent,"Visual menu",new Vector2(origin.x-640,360-origin.y),Vector2.zero);
            group.localRotation=Quaternion.Euler(0,0,space==null?0:-(space.UiAngle+space.Rotation*Mathf.Clamp01(progress)));
            var labels=new[]{"聊天记录","日记","陪伴空间","人物设定","设置","回到未花"};
            for(int i=0;i<labels.Length;i++)
            {
                int index=i;
                float row=space==null?40:theme.RowHeight*scale;
                var text=CompanionUIFactory.Text(group,labels[i],labels[i],new Vector2(0,theme.MenuSize*scale*1.1f-i*row),new Vector2(180*scale,row),theme.MenuSize*scale,TextAlignmentOptions.TopLeft);
                var rect=(RectTransform)text.transform; rect.pivot=new Vector2(0,1); Style(text,theme,theme.MenuSize*scale); text.overflowMode=TextOverflowModes.Overflow;text.raycastTarget=true;
                var button=text.gameObject.AddComponent<Button>();button.targetGraphic=text;Style(button,theme);button.onClick.AddListener(()=>clicked?.Invoke(index));
            }
            return group;
        }
    }
}
