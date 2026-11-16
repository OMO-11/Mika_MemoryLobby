using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace GiftCompanion.Core.Config
{
    public readonly struct PetHotkey
    {
        public uint Modifiers { get; }
        public uint Key { get; }
        public PetHotkey(uint modifiers, uint key) { Modifiers = modifiers; Key = key; }
        public static bool TryParse(string value, out PetHotkey hotkey)
        {
            hotkey = default; if (string.IsNullOrWhiteSpace(value) || value.Length > 64) return false;
            var words = value.Split('+').Select(s => s.Trim().ToUpperInvariant()).ToArray();
            if (words.Length < 2 || words.Length > 4) return false;
            uint modifiers = 0;
            foreach (string word in words.Take(words.Length - 1))
            {
                uint flag = word == "CTRL" ? 2u : word == "ALT" ? 1u : word == "SHIFT" ? 4u : 0;
                if (flag == 0 || (modifiers & flag) != 0) return false; modifiers |= flag;
            }
            if ((modifiers & 3) == 0) return false;
            string last = words.Last(); uint key = last == "SPACE" ? 0x20u : last.Length == 1 && last[0] >= 'A' && last[0] <= 'Z' ? last[0] : 0u;
            if (key == 0 && last.StartsWith("F") && int.TryParse(last.Substring(1), out int number) && number >= 1 && number <= 11) key = (uint)(0x70 + number - 1);
            if (key == 0 || key == 0x20 && modifiers == 1) return false;
            hotkey = new PetHotkey(modifiers, key); return true;
        }
    }

    public sealed class PetSettings
    {
        public int Version { get; set; } = 1;
        public int Width { get; set; } = 600;
        public int Height { get; set; } = 500;
        public bool HasPosition { get; set; }
        public int Left { get; set; }
        public int Top { get; set; }
        public bool Topmost { get; set; } = true;
        public float ReplySeconds { get; set; } = 18;
        public float LongPressSeconds { get; set; } = .55f;
        public float DragPixels { get; set; } = 9;
        public int IdleFrameRate { get; set; } = 30;
        public int ActiveFrameRate { get; set; } = 60;
        public PetSettings Copy() => (PetSettings)MemberwiseClone();
        public void Validate()
        {
            if (Version != 1 || Width < 500 || Width > 1000 || Height < 400 || Height > 800 ||
                Math.Abs((long)Left) > 100000 || Math.Abs((long)Top) > 100000 || float.IsNaN(ReplySeconds) || ReplySeconds < 5 || ReplySeconds > 300 ||
                float.IsNaN(LongPressSeconds) || LongPressSeconds < .25f || LongPressSeconds > 2 || float.IsNaN(DragPixels) || DragPixels < 3 || DragPixels > 40 ||
                IdleFrameRate < 15 || IdleFrameRate > 60 || ActiveFrameRate < IdleFrameRate || ActiveFrameRate > 120)
                throw new InvalidDataException("Pet settings invalid.");
        }
    }
    public sealed class PetSettingsService
    {
        public PetSettings Value { get; private set; } = new PetSettings();
        public string Issue { get; private set; }
        public string FilePath { get; }
        public PetSettingsService(string root) { FilePath = Path.Combine(root, "Config", "pet_settings.json"); }
        public void Load(CancellationToken ct)
        {
            AtomicDocument.Recover(FilePath);
            if (!File.Exists(FilePath)) AtomicDocument.WriteJson(FilePath, new PetSettings(), ct);
            try { var next = AtomicDocument.Read<PetSettings>(FilePath); if (next == null) throw new InvalidDataException(); next.Validate(); Value = next; Issue = null; }
            catch (Exception e) when (e is IOException || e is InvalidDataException || e is Newtonsoft.Json.JsonException) { Issue = "桌面设置文件暂时不可用，已保留原文件。"; }
        }
        public void Save(PetSettings value, CancellationToken ct)
        { value.Validate(); AtomicDocument.WriteJson(FilePath, value, ct); Value = value.Copy(); Issue = null; }
    }
}
