using System;
using System.IO;

namespace GiftCompanion.Core.Config
{
    public static class UserDataPaths
    {
        public static string Resolve(bool editor, string projectPath, string[] arguments)
        {
            if (editor) return Path.GetFullPath(Path.Combine(projectPath, "UserData"));
            string normal = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GiftCompanion");
            if (Array.IndexOf(arguments, "-phase5-probe") >= 0 || Array.IndexOf(arguments, "-phase6-probe") >= 0 || Array.IndexOf(arguments, "-v1-closeout-probe") >= 0 || Array.IndexOf(arguments,"-v2-window-probe")>=0 || Array.IndexOf(arguments,"-v2-reading-probe")>=0 || Array.IndexOf(arguments,"-v2-maintenance-probe")>=0 || Array.IndexOf(arguments,"-v2-resize-probe")>=0 || Array.IndexOf(arguments,"-v2-final-probe")>=0 || Array.IndexOf(arguments,"-seaside-probe")>=0)
            {
                int marker = Array.IndexOf(arguments, "-validation-data");
                if (marker < 0 || marker + 1 >= arguments.Length) throw new InvalidDataException("This probe requires isolated validation data.");
                string isolated = Path.GetFullPath(arguments[marker + 1]);
                if (!isolated.StartsWith(Path.GetFullPath(Path.Combine(normal, "Validation")) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Validation data path is outside its boundary.");
                return isolated;
            }
            // Test isolation is opt-in, under the Player data root, and never changes normal startup.
            if (Array.IndexOf(arguments, "-phase1-probe") >= 0 || Array.IndexOf(arguments, "-phase0-window-probe") >= 0 || Array.IndexOf(arguments,"-phase2-probe") >= 0 || Array.IndexOf(arguments,"-phase3-probe") >= 0 || Array.IndexOf(arguments,"-phase3plus-probe") >= 0 || Array.IndexOf(arguments,"-phase4-probe") >= 0 || Array.IndexOf(arguments,"-bilingual-probe") >= 0 || Array.IndexOf(arguments,"-voicepolish-probe") >= 0 || Array.IndexOf(arguments,"-expressiondiary-probe") >= 0)
            {
                int index = Array.IndexOf(arguments, "-validation-data");
                if (index < 0 && (Array.IndexOf(arguments, "-phase3plus-probe") >= 0 || Array.IndexOf(arguments, "-phase4-probe") >= 0 || Array.IndexOf(arguments, "-bilingual-probe") >= 0 || Array.IndexOf(arguments, "-voicepolish-probe") >= 0 || Array.IndexOf(arguments, "-expressiondiary-probe") >= 0)) throw new InvalidDataException("This probe requires isolated validation data.");
                if (index >= 0)
                {
                    if (index + 1 >= arguments.Length) throw new InvalidDataException("Validation data path is missing.");
                    string path = Path.GetFullPath(arguments[index + 1]);
                    string boundary = Path.GetFullPath(Path.Combine(normal, "Validation")) + Path.DirectorySeparatorChar;
                    if (!path.StartsWith(boundary, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Validation data path is outside its boundary.");
                    return path;
                }
            }
            return normal;
        }
    }
}
