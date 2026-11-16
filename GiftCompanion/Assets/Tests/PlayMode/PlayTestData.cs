using System;
using System.IO;
using GiftCompanion.Bootstrap;
using UnityEngine;

namespace GiftCompanion.Tests
{
    internal static class PlayTestData
    {
        public static void Begin()
        {
            AppRoot.EditorValidationDataPath = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../UserData/Validation", "play-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(Path.Combine(AppRoot.EditorValidationDataPath, "Config"));
            File.WriteAllText(Path.Combine(AppRoot.EditorValidationDataPath, "Config/settings.json"), "{\"VoiceEnabled\":false}");
            File.WriteAllText(Path.Combine(AppRoot.EditorValidationDataPath, "Config/app_entry.json"), "{\"StartupMode\":\"home\",\"PetEntryEnabled\":true}");
        }
        public static void End()
        {
            string path = AppRoot.EditorValidationDataPath;
            AppRoot.EditorValidationDataPath = null;
            if (path == null) return;
            string boundary = Path.GetFullPath(Path.Combine(Application.dataPath, "../UserData/Validation")) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected test cleanup path.");
            if (Directory.Exists(path)) Directory.Delete(path, true);
        }
    }
}
