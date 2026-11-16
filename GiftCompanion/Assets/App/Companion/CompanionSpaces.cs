using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GiftCompanion.Bootstrap;
using GiftCompanion.Core.Config;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GiftCompanion.Companion
{
    public sealed partial class CompanionPresentation
    {
        private CompanionBehaviorProfile defaultProfile, ownedProfile;
        private bool spaceChanging;
        public string ActiveSpaceId { get; private set; }
        public string SpaceError { get; private set; }
        public bool Ready { get; private set; }
        public static void ValidateSpaceRuntime(SpaceDefinition definition, string folder)
        {
            string file = Path.Combine(folder, definition.Skeleton);
            string version;
            if (definition.Skeleton.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) version = (string)JObject.Parse(File.ReadAllText(file))["skeleton"]?["spine"];
            else { using var stream = File.OpenRead(file); version = Spine.SkeletonBinary.GetVersionString(stream); }
            if (version == null || !version.StartsWith("4.2.", StringComparison.Ordinal)) throw new InvalidDataException("此资源需要导出为 Spine 4.2，原空间会保留。");
            try
            {
                using var reader = new StringReader(File.ReadAllText(Path.Combine(folder, definition.Atlas)));
                var atlas = new Spine.Atlas(reader, folder, new MetadataTextures());
                try
                {
                var data = definition.Skeleton.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                    ? new Spine.SkeletonJson(atlas).ReadSkeletonData(file) : new Spine.SkeletonBinary(atlas).ReadSkeletonData(file);
                if (data.FindAnimation(definition.Idle) == null && !File.Exists(Path.Combine(folder, "space.json")))
                    definition.Idle = data.Animations.FirstOrDefault(a => a.Name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) >= 0)?.Name;
                if (string.IsNullOrEmpty(definition.Idle) || data.FindAnimation(definition.Idle) == null) throw new InvalidDataException("待机动画未找到，请资源提供者补齐 space.json。");
                }
                finally { atlas.Dispose(); }
            }
            catch (InvalidDataException) { throw; }
            catch (Exception error) { throw new InvalidDataException("资源包暂不能使用，请检查骨架、图集与待机动画。", error); }
        }
        private sealed class MetadataTextures : Spine.TextureLoader
        {
            public void Load(Spine.AtlasPage page, string path)
            {
                using var input = File.OpenRead(path); var header = new byte[24];
                if (input.Read(header, 0, header.Length) != header.Length || header[0] != 137 || header[1] != 80 || header[2] != 78 || header[3] != 71) throw new InvalidDataException("图集 PNG 文件不可用。");
                page.width = header[16] << 24 | header[17] << 16 | header[18] << 8 | header[19];
                page.height = header[20] << 24 | header[21] << 16 | header[22] << 8 | header[23];
            }
            public void Unload(object texture) { }
        }
        public async Task SwitchSpaceAsync(SpaceEntry entry, CancellationToken ct, bool force = false)
        {
            if (spaceChanging) throw new InvalidOperationException("Space selection busy.");
            if (!force && entry.Id == ActiveSpaceId)
            { await AppRoot.Instance.Library.SelectSpaceAsync(entry.Id, ct); return; }
            spaceChanging = true;
            var camera = loader.renderCamera; var previousPosition = camera.transform.position; float previousSize = camera.orthographicSize;
            CompanionValidationBootstrap candidate = null; CompanionBehaviorProfile profile = null;
            try
            {
                var library = AppRoot.Instance.Library;
                string manifest = library.Resolve(entry.Definition); string folder = Path.GetDirectoryName(manifest);
                var definition = library.ReadSpace(entry); ValidateSpaceRuntime(definition, folder); ct.ThrowIfCancellationRequested();
                profile = entry.Id == "mika-memory" ? Instantiate(defaultProfile) : ScriptableObject.CreateInstance<CompanionBehaviorProfile>();
                profile.resourceFolder = folder; profile.skeletonFile = definition.Skeleton; profile.atlasFile = definition.Atlas;
                profile.textureFiles = definition.Textures; profile.idleAnimation = definition.Idle; profile.skeletonScale = definition.Scale; profile.enablePat10Trial = false;
                var go = new GameObject("Companion Space Runtime"); go.transform.SetParent(transform, false); go.SetActive(false);
                candidate = go.AddComponent<CompanionValidationBootstrap>(); candidate.enabled = false; candidate.profile = profile;
                candidate.renderCamera = camera; candidate.enableLocalBehavior = entry.Id == "mika-memory"; candidate.Initialize();
                var shader = Shader.Find("GiftCompanion/SpineStraightAlpha"); if (shader == null) throw new InvalidDataException("Space shader unavailable.");
                foreach (var atlas in candidate.Character.SkeletonDataAsset.atlasAssets)
                    foreach (var material in atlas.Materials) { material.shader = shader; material.SetFloat("_InputIsStraight", definition.StraightAlpha ? 1 : 0); }
                candidate.Character.Update(0); candidate.Character.LateUpdate();
                Vector3 position = new Vector3(definition.CameraX, definition.CameraY, -100); float size = definition.CameraSize;
                if (definition.FitToBounds)
                {
                    candidate.Character.Skeleton.GetBounds(out float x, out float y, out float width, out float height, ref boundsScratch);
                    position = new Vector3(x + width * .5f, y + height * .5f, -100);
                    size = Mathf.Max(.1f, Mathf.Max(height * .5f, width * .5f / camera.aspect) * 1.03f);
                }
                if(entry.Id=="mika-seaside") {
                    var sea=candidate.Character.gameObject.AddComponent<SeasideSpaceView>();
                    sea.Bind(candidate,AppRoot.Instance.VisualSettings.Value.ReduceMotion);
                }
                camera.transform.position = previousPosition; camera.orthographicSize = previousSize;
                await library.SelectSpaceAsync(entry.Id, ct);
                // Commit barrier: selection is saved. Finish the visible swap without a late cancellation.
                CancelInteraction(); var old = loader; var oldProfile = ownedProfile;
                old.Character.gameObject.SetActive(false); loader = candidate; ownedProfile = profile;
                candidate = null; profile = null; loader.gameObject.SetActive(true);
                camera.transform.position = position; camera.orthographicSize = size;
                Destroy(old.Character.gameObject);
                if (old.gameObject.name == "Companion Space Runtime") Destroy(old.gameObject); else Destroy(old);
                if (oldProfile != null) Destroy(oldProfile);
                var tone=camera.GetComponent<CompanionTone>();if(tone!=null)tone.enabled=true;
                ActiveSpaceId = entry.Id; SpaceError = null;
                if(entry.Id=="mika-memory"&&!AppRoot.Instance.VisualSettings.Value.ReduceMotion)loader.PlayIntro();
                SetState(CurrentState);
            }
            catch
            {
                if (camera != null) { camera.transform.position = previousPosition; camera.orthographicSize = previousSize; }
                if (candidate != null) Destroy(candidate.gameObject); if (profile != null) Destroy(profile);
                throw;
            }
            finally { spaceChanging = false; }
        }
        private float[] boundsScratch;
        private void OnDestroy() { if (ownedProfile != null) Destroy(ownedProfile); }
    }
}
