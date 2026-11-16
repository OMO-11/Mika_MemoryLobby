using System;
using System.Collections;
using System.Linq;
using GiftCompanion.Companion.Bgm;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object=UnityEngine.Object;
namespace GiftCompanion.Tests
{
    public sealed class MusicHubTests
    {
        [UnityTest]public IEnumerator PauseKeepsPlaybackPositionAndMutedPreferenceAndCanLeave()
        {
            var host=new GameObject("Pause verification");var music=host.AddComponent<BgmManager>();var clip=AudioClip.Create("long loop",240000,1,48000,false);
            music.Configure(new[]{clip},.2f,false);yield return new WaitForSeconds(.3f);
            music.SetPaused(true);float at=music.Source.time;yield return new WaitForSeconds(.2f);
            Assert.That(music.Source.time,Is.EqualTo(at).Within(.03));
            music.SetVolume(.17f,true);music.SetDucked(true);music.SetPaused(false);yield return new WaitForSeconds(.15f);
            Assert.That(music.Source.time,Is.GreaterThan(at+.03f));Assert.That(music.Muted,Is.True);Assert.That(music.Volume,Is.EqualTo(.17f));
            music.SetPaused(true);var leave=music.FadeOutAsync(default);yield return null;Assert.That(leave.IsCompleted,Is.True);Assert.That(music.GetComponents<AudioSource>().All(source=>!source.isPlaying),Is.True);
            Object.Destroy(host);Object.Destroy(clip);
        }
    }
}
