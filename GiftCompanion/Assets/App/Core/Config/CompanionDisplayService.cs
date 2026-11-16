using System;
using System.IO;
using System.Threading;
using Newtonsoft.Json;

namespace GiftCompanion.Core.Config
{
    public sealed class CompanionDisplaySettings
    {
        public int Version { get; set; } = 1;
        public float Exposure { get; set; } = .93f;
        public float HighlightCompression { get; set; } = .45f;
        public float DiaryFontSize { get; set; } = 24;
        public float DiaryLineSpacing { get; set; } = 10;
        public CompanionDisplaySettings Copy() => (CompanionDisplaySettings)MemberwiseClone();
        public void Validate()
        {
            if (Version != 1 || !Finite(Exposure, .75f, 1.1f) || !Finite(HighlightCompression, 0, 1) ||
                !Finite(DiaryFontSize, 20, 30) || !Finite(DiaryLineSpacing, 0, 20)) throw new InvalidDataException("显示设置无效；原文件保留，可通过菜单恢复默认显示。");
        }
        private static bool Finite(float n, float min, float max) => !float.IsNaN(n) && !float.IsInfinity(n) && n >= min && n <= max;
    }
    public sealed class CompanionDisplayService
    {
        public string FilePath { get; }
        public CompanionDisplaySettings Value { get; private set; } = new CompanionDisplaySettings();
        public string Issue { get; private set; }
        public CompanionDisplayService(string root) => FilePath = Path.Combine(root, "Config", "companion_display.json");
        public void Load(CancellationToken ct)
        {
            if (!File.Exists(FilePath)) { AtomicDocument.WriteJson(FilePath, Value, ct); return; }
            try { var next = AtomicDocument.Read<CompanionDisplaySettings>(FilePath); if (next == null) throw new InvalidDataException(); next.Validate(); Value = next; Issue = null; }
            catch (Exception e) when (e is JsonException || e is InvalidDataException || e is IOException || e is UnauthorizedAccessException) { Issue = "显示配置暂不可用，原件保留，暂用可用显示设置。"; }
        }
        public void Save(CompanionDisplaySettings settings, CancellationToken ct)
        { settings.Validate(); var copy = settings.Copy(); AtomicDocument.WriteJson(FilePath, copy, ct); Value = copy; Issue = null; }
    }
}
