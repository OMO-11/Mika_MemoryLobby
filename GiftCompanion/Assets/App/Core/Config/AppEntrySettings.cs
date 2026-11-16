using System;
using System.IO;
using System.Threading;
using GiftCompanion.Core.Character;
using Newtonsoft.Json;
namespace GiftCompanion.Core.Config
{
    public sealed class AppEntrySettings
    {
        public int Version { get; set; } = 1;
        public string StartupMode { get; set; } = "companion";
        public bool PetEntryEnabled { get; set; }
        public CharacterMode InitialMode => StartupMode == "home" ? CharacterMode.Home : CharacterMode.Companion;
        public static AppEntrySettings Load(string root, CancellationToken ct)
        {
            string path = Path.Combine(root, "Config", "app_entry.json");
            if (!File.Exists(path)) { var defaults = new AppEntrySettings(); AtomicDocument.WriteJson(path, defaults, ct); return defaults; }
            try
            {
                var value = AtomicDocument.Read<AppEntrySettings>(path);
                if (value == null || value.Version != 1 || value.StartupMode != "companion" && value.StartupMode != "home") throw new InvalidDataException();
                return value;
            }
            catch (Exception e) when (e is JsonException || e is InvalidDataException || e is IOException || e is UnauthorizedAccessException) { return new AppEntrySettings(); }
        }
    }
}
