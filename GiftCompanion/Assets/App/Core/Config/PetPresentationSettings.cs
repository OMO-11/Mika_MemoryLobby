using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace GiftCompanion.Core.Config
{
    public sealed class PetPresentationSettings
    {
        public int Version { get; set; } = 1;
        public string HeadBone { get; set; } = "Bip001 Head";
        public float HeadRadiusHeightRatio { get; set; } = .13f;
        public float LookYaw { get; set; } = 16;
        public float LookPitch { get; set; } = 9;
        public float LookSpeed { get; set; } = 4;
        public Dictionary<string, string> Actions { get; set; } = new Dictionary<string, string>
        { ["Idle"]="Idle", ["Thinking"]="Thinking", ["Working"]="Working", ["HeadPat"]="HeadPat", ["Dragging"]="Dragging", ["Success"]="Success", ["Confused"]="Confused", ["HeadClick"]="HeadClick", ["BodyClick"]="BodyClick" };
        public static PetPresentationSettings Load(string root, CancellationToken ct)
        {
            string path = Path.Combine(root, "Config", "pet_presentation.json"); AtomicDocument.Recover(path);
            if (!File.Exists(path)) AtomicDocument.WriteJson(path, new PetPresentationSettings(), ct);
            var value = AtomicDocument.Read<PetPresentationSettings>(path);
            string[] allowed = { "Idle", "Walk", "Thinking", "Working", "HeadPat", "Dragging", "Sleep", "Wave", "Success", "Confused", "HeadClick", "BodyClick" };
            if (value == null || value.Version != 1 || string.IsNullOrWhiteSpace(value.HeadBone) || value.HeadBone.Length > 100 ||
                !Finite(value.HeadRadiusHeightRatio,.05f,.25f) || !Finite(value.LookYaw,0,25) || !Finite(value.LookPitch,0,15) || !Finite(value.LookSpeed,.5f,12) ||
                value.Actions == null || value.Actions.Count > 20 || value.Actions.Any(p => !allowed.Contains(p.Key) || !allowed.Contains(p.Value))) throw new InvalidDataException("Pet presentation invalid.");
            return value;
        }
        private static bool Finite(float v, float min, float max) => !float.IsNaN(v) && v >= min && v <= max;
    }
}
