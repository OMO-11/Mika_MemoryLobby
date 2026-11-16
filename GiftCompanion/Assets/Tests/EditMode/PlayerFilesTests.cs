using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using GiftCompanion.Core.Character;
using GiftCompanion.Core.Config;
using NUnit.Framework;

namespace GiftCompanion.Tests
{
    public sealed class PlayerFilesTests
    {
        private string directory;
        [SetUp] public void SetUp() { directory = Path.Combine(Path.GetTempPath(), "GiftFiles-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory); }
        [TearDown] public void TearDown() { Directory.Delete(directory, true); }
        [Test] public async Task ShippedSpacesMergeIntoExistingCatalogWithoutReplacingPlayerSelectionOrImportedEntries()
        {
            string bundle=Path.Combine(directory,"bundle"),user=Path.Combine(directory,"user");
            Directory.CreateDirectory(Path.Combine(bundle,"Defaults"));Directory.CreateDirectory(Path.Combine(user,"Config"));
            var original=new SpaceEntry{Id="mika-memory",Title="记忆大厅",Definition="bundle:original/space.json",Builtin=true};
            var sea=new SpaceEntry{Id="mika-seaside",Title="海边记忆大厅",Definition="bundle:sea/space.json",Builtin=true};
            var imported=new SpaceEntry{Id="player-space",Title="我的空间",Definition="Content/space.json"};
            var defaults=new SpaceCatalog{SelectedSpaceId=original.Id,Spaces=new System.Collections.Generic.List<SpaceEntry>{original,sea}};
            AtomicDocument.WriteJson(Path.Combine(bundle,"Defaults","companion_spaces.json"),defaults,default);
            var previous=new SpaceCatalog{SelectedSpaceId=imported.Id,Spaces=new System.Collections.Generic.List<SpaceEntry>{original,imported}};
            AtomicDocument.WriteJson(Path.Combine(user,"Config","companion_spaces.json"),previous,default);
            var library=new ContentLibrary(user,bundle);await library.InitializeAsync(default);
            Assert.That(library.Spaces.SelectedSpaceId,Is.EqualTo(imported.Id));Assert.That(library.Spaces.Spaces.Count,Is.EqualTo(3));
            Assert.That(library.Spaces.Spaces.Single(s=>s.Id==imported.Id).Title,Is.EqualTo("我的空间"));
            library.Reload(default);Assert.That(library.Spaces.Spaces.Count,Is.EqualTo(3));
            Assert.That(File.Exists(library.SpacesFile+".previous.bak"),Is.True);
        }
        [Test] public void InterruptedFileProviderCommitRecoversOnlyAnIntactChecksummedDocument()
        {
            string path = Path.Combine(directory, "recover.json"); byte[] bytes = System.Text.Encoding.UTF8.GetBytes("{\"version\":1,\"tracks\":[]}");
            string digest; using (var hash = System.Security.Cryptography.SHA256.Create()) digest = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
            File.WriteAllText(path, "{partial");
            File.WriteAllText(path + ".pending", Newtonsoft.Json.JsonConvert.SerializeObject(new { Version = 1, BytesBase64 = Convert.ToBase64String(bytes), Sha256 = digest }));
            Assert.That(AtomicDocument.Read<MusicCatalog>(path).Version, Is.EqualTo(1)); Assert.That(File.Exists(path + ".pending"), Is.False);
            File.WriteAllText(path + ".pending", Newtonsoft.Json.JsonConvert.SerializeObject(new { Version = 1, BytesBase64 = Convert.ToBase64String(bytes), Sha256 = "damaged" }));
            AtomicDocument.Recover(path); Assert.That(File.ReadAllBytes(path), Is.EqualTo(bytes));
            AtomicDocument.WriteJson(path, new MusicCatalog(), default); Assert.That(AtomicDocument.Read<MusicCatalog>(path).Repeat, Is.EqualTo("one"));
            Assert.That(File.Exists(path + ".pending"), Is.False);
        }
        [Test] public async Task PlayerCardSavesAndReloadsWithoutChangingCharacterIdAndKeepsBackup()
        {
            const string seed = "{\"id\":\"main-character\",\"displayName\":\"Mika\",\"persona\":\"原始语气\",\"relationship\":\"老师\"}";
            var config = new ConfigService(Path.Combine(directory, "user"), seed); await config.InitializeAsync(default);
            await config.SaveCharacterCardAsync(new CharacterIdentity("main-character", "未花", "玩家修改的语气", "玩家确认的关系"), default);
            Assert.That(File.Exists(config.CharacterCardPath + ".previous.bak"), Is.True);
            var restored = new ConfigService(config.UserDataPath, seed); await restored.InitializeAsync(default);
            Assert.That(restored.Identity.Persona, Is.EqualTo("玩家修改的语气")); Assert.That(restored.Identity.DisplayName, Is.EqualTo("未花"));
            Assert.ThrowsAsync<InvalidDataException>(() => config.SaveCharacterCardAsync(new CharacterIdentity("other", "Other", "", ""), default));
            File.WriteAllText(restored.CharacterCardPath, "bad json"); await restored.ReloadCharacterCardAsync(default);
            Assert.That(restored.Identity.Persona, Is.EqualTo("玩家修改的语气")); Assert.That(File.ReadAllText(restored.CharacterCardPath), Is.EqualTo("bad json"));
            Assert.That(restored.CharacterCardIssue, Is.Not.Empty);
        }
        [Test] public async Task ConcurrentImportsKeepEveryEntryAndUnchangedSelectionsDoNotRewriteDamagedFiles()
        {
            string bundle = Path.Combine(directory, "bundle"); Directory.CreateDirectory(Path.Combine(bundle, "Defaults"));
            AtomicDocument.WriteJson(Path.Combine(bundle, "Defaults", "bgm_playlist.json"), new MusicCatalog { SelectedTrackId = "seed", Tracks = new System.Collections.Generic.List<MusicTrack> { new MusicTrack {Id="seed",Title="Seed",Path="bundle:seed.ogg"} } }, default);
            AtomicDocument.WriteJson(Path.Combine(bundle, "Defaults", "companion_spaces.json"), new SpaceCatalog { SelectedSpaceId = "seed", Spaces = new System.Collections.Generic.List<SpaceEntry> { new SpaceEntry {Id="seed",Title="Seed",Definition="bundle:space.json"} } }, default);
            var library = new ContentLibrary(Path.Combine(directory, "user"), bundle); await library.InitializeAsync(default);
            File.WriteAllText(library.MusicFile, "damaged music"); File.WriteAllText(library.SpacesFile, "damaged spaces"); library.Reload(default);
            await library.SelectMusicAsync("seed", default); await library.SelectSpaceAsync("seed", default);
            Assert.That(File.ReadAllText(library.MusicFile), Is.EqualTo("damaged music")); Assert.That(File.ReadAllText(library.SpacesFile), Is.EqualTo("damaged spaces"));
            string source = Path.Combine(directory, "new.wav"); File.WriteAllBytes(source, new byte[256 * 1024]);
            var tracks = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => library.ImportMusicAsync(source, default)));
            Assert.That(library.Music.Tracks.Count, Is.EqualTo(9)); Assert.That(tracks.Select(t => t.Id).Distinct().Count(), Is.EqualTo(8));
            await library.SelectMusicAsync(tracks[0].Id, default);
            Assert.That(library.Music.SelectedTrackId, Is.EqualTo(tracks[0].Id));
            Assert.That(AtomicDocument.Read<MusicCatalog>(library.MusicFile).Tracks.Count, Is.EqualTo(9));
        }
        [Test] public async Task MusicCatalogUsesRelativeFilesAndManualSelectionPersistsAndInvalidFileIsPreserved()
        {
            var library = new ContentLibrary(Path.Combine(directory, "user"), Path.Combine(directory, "bundle")); await library.InitializeAsync(default);
            string first = Path.Combine(directory, "first.wav"), second = Path.Combine(directory, "second.ogg"); File.WriteAllBytes(first, new byte[32]); File.WriteAllBytes(second, new byte[32]);
            var a = await library.ImportMusicAsync(first, default); var b = await library.ImportMusicAsync(second, default);
            Assert.That(library.Music.SelectedTrackId, Is.EqualTo(a.Id), "Adding does not switch songs.");
            var next = library.Music; next.SelectedTrackId = b.Id; next.FadeSeconds = 2; await library.SaveMusicAsync(next, default);
            var reopen = new ContentLibrary(library.DataDirectory, Path.Combine(directory, "bundle")); await reopen.InitializeAsync(default);
            Assert.That(reopen.Music.Repeat, Is.EqualTo("one")); Assert.That(reopen.Music.SelectedTrackId, Is.EqualTo(b.Id)); Assert.That(File.Exists(reopen.Resolve(b.Path)), Is.True);
            Assert.That(File.Exists(first) && File.Exists(second), Is.True); Assert.Throws<InvalidDataException>(() => reopen.Resolve("../outside.mp3"));
            File.WriteAllText(reopen.MusicFile, "bad json"); reopen.Reload(default);
            Assert.That(reopen.Warnings.Count, Is.EqualTo(1)); Assert.That(File.ReadAllText(reopen.MusicFile), Is.EqualTo("bad json"));
        }
        [Test] public async Task SpaceImportCopiesOnlyResourcePackageAndRejectsEscapingOrMissingPages()
        {
            var library = new ContentLibrary(Path.Combine(directory, "user"), Path.Combine(directory, "bundle")); await library.InitializeAsync(default);
            string package = Path.Combine(directory, "Space"); Directory.CreateDirectory(package);
            File.WriteAllBytes(Path.Combine(package, "character.skel"), new byte[32]); File.WriteAllText(Path.Combine(package, "character.atlas"), "page.png\nsize: 1,1\n"); File.WriteAllBytes(Path.Combine(package, "page.png"), new byte[32]);
            File.WriteAllText(Path.Combine(package, "ignored.dll"), "not copied");
            bool validated = false;
            var entry = await library.ImportSpaceAsync(package, (_,__) => validated = true, default);
            Assert.That(validated, Is.True); var definition = library.ReadSpace(entry); Assert.That(definition.Textures.Single(), Is.EqualTo("page.png"));
            Assert.That(File.Exists(Path.Combine(Path.GetDirectoryName(library.Resolve(entry.Definition)), "ignored.dll")), Is.False);
            definition.Textures = new[] {"../page.png"}; AtomicDocument.WriteJson(Path.Combine(package, "space.json"), definition, default);
            Assert.Throws<InvalidDataException>(() => ContentLibrary.InspectPackage(package));
        }
    }
}
