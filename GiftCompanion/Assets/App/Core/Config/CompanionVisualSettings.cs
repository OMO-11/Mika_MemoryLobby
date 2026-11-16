using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Newtonsoft.Json;

namespace GiftCompanion.Core.Config
{
    public sealed class VisualTheme
    {
        public string Ink, ClockInk, Accent, Focus;
        public float DisabledAlpha, MenuAlpha, ClockAlpha, MenuSize, ClockSize, DateSize, BodySize, RowHeight;
        public void Validate()
        {
            foreach (var color in new[] { Ink, ClockInk, Accent, Focus })
                if (color == null || !System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$")) throw new InvalidDataException("Invalid visual color.");
            foreach (float alpha in new[] { DisabledAlpha, MenuAlpha, ClockAlpha }) Check(alpha, 0, 1);
            foreach (float size in new[] { MenuSize, ClockSize, DateSize, BodySize, RowHeight }) Check(size, 10, 72);
        }
        internal static void Check(float value, float min, float max)
        { if (float.IsNaN(value) || float.IsInfinity(value) || value < min || value > max) throw new InvalidDataException("Invalid visual number."); }
    }
    public sealed class VisualSpace
    {
        public string Id;
        public float CenterX, CenterY, Zoom, Rotation, MenuX, MenuY, ClockX, ClockY, UiAngle;
        public float SafeLeft, SafeTop, SafeRight, SafeBottom;
        public float ForwardSeconds=3.2f, ReturnSeconds=2.4f, CurtainDepth=1.05f, CharacterDepth=2.5f, CharacterOffsetX=-700, CharacterOffsetY=300;
        public float[] Pane={907.1975f,-40.66349f,1266.30969f,133.05426f,1213.43616f,507.610626f,756.0764f,276.4155f};
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Id)) throw new InvalidDataException("Missing space ID.");
            VisualTheme.Check(CenterX, 0, 1280); VisualTheme.Check(CenterY, 0, 720);
            VisualTheme.Check(Zoom, 1, 4); VisualTheme.Check(Rotation, -90, 90); VisualTheme.Check(UiAngle, -90, 90);
            VisualTheme.Check(MenuX, 0, 1280); VisualTheme.Check(ClockX, 0, 1280);
            VisualTheme.Check(MenuY, 0, 720); VisualTheme.Check(ClockY, 0, 720);
            foreach (var margin in new[] { SafeLeft, SafeTop, SafeRight, SafeBottom }) VisualTheme.Check(margin, 0, 180);
            VisualTheme.Check(ForwardSeconds,.1f,10);VisualTheme.Check(ReturnSeconds,.1f,10);
            VisualTheme.Check(CurtainDepth,1,4);VisualTheme.Check(CharacterDepth,1,4);
            VisualTheme.Check(CharacterOffsetX,-2000,2000);VisualTheme.Check(CharacterOffsetY,-2000,2000);
            if(Pane==null||Pane.Length!=8)throw new InvalidDataException("Invalid window polygon.");foreach(var n in Pane)VisualTheme.Check(n,-1280,2560);
        }
    }
    public sealed class CompanionVisualSettings
    {
        public int Version;
        public bool ReduceMotion;
        public float EffectStrength = .6f;
        public bool Moonlight=true,Sheer=true,Meteors=true,NearBlur=true;
        public VisualTheme Theme;
        public List<VisualSpace> Spaces;
        public void Validate()
        {
            if (Version != 1 || Theme == null || Spaces == null || Spaces.Count > 128) throw new InvalidDataException("Invalid visual version or structure.");
            Theme.Validate(); var ids = new HashSet<string>(StringComparer.Ordinal);
            VisualTheme.Check(EffectStrength,0,1);
            foreach (var space in Spaces) { if (space == null) throw new InvalidDataException(); space.Validate(); if (!ids.Add(space.Id)) throw new InvalidDataException("Duplicate visual space."); }
        }
        public VisualSpace FindSpace(string id) => Spaces.Find(space => space.Id == id);
    }
    public sealed class CompanionVisualService
    {
        readonly string defaults;
        public string FilePath { get; }
        public CompanionVisualSettings Value { get; private set; }
        public string Issue { get; private set; }
        public CompanionVisualService(string userDataRoot, string defaultJson)
        {
            FilePath = Path.Combine(userDataRoot, "Config", "companion_visual.json"); defaults = defaultJson;
            Value = Parse(defaults);
        }
        static CompanionVisualSettings Parse(string text)
        { var value = JsonConvert.DeserializeObject<CompanionVisualSettings>(text, new JsonSerializerSettings { MaxDepth = 16 }); if (value == null) throw new InvalidDataException(); value.Validate(); return value; }
        public void Load(CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (!File.Exists(FilePath)) { Value = Parse(defaults); Issue = null; return; }
            try { var next = AtomicDocument.Read<CompanionVisualSettings>(FilePath); if (next == null) throw new InvalidDataException(); next.Validate(); Value = next; Issue = null; }
            catch (Exception e) when (e is JsonException || e is InvalidDataException || e is IOException || e is UnauthorizedAccessException)
            { Issue = "视觉配置暂不可用，保留原文件并沿用可用布局。"; }
        }
        public void Save(CompanionVisualSettings value, CancellationToken ct)
        { value.Validate(); var copy = Parse(JsonConvert.SerializeObject(value)); AtomicDocument.WriteJson(FilePath, copy, ct); Value = copy; Issue = null; }
    }
}
