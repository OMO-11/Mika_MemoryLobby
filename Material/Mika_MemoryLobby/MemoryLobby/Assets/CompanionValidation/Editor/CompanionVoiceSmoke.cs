using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>Real-time voice checks in the isolated scene, using the two supplied recordings.</summary>
public static class CompanionVoiceSmoke {
    [MenuItem("Companion Validation/Run Voice Smoke (Play Mode)")]
    static void RunFromMenu() {Debug.Log(Start());}
    [Serializable] public sealed class ClipResult {
        public string name;
        public float duration;
        public int samples, openFrames, closedFrames;
        public bool completed;
    }
    [Serializable] public sealed class Report {
        public string createdAt;
        public ClipResult[] clips;
        public string[] passed, failed;
        public bool allPassed;
    }
    static CompanionValidationBootstrap boot;
    static CompanionPointerInteraction pointer;
    static readonly List<string> passed=new List<string>(), failed=new List<string>();
    static readonly List<ClipResult> clips=new List<ClipResult>();
    static int stage, initialOpen, initialClosed, pauseSample, generation;
    static double stageAt;
    static bool oldPointer, oldGaze;
    static GameObject otherAudio;
    public static bool Running { get { return boot!=null; } }
    public static string Start() {
        if(Running)throw new InvalidOperationException("Voice checks are already running.");
        boot=UnityEngine.Object.FindFirstObjectByType<CompanionValidationBootstrap>();
        if(!EditorApplication.isPlaying || boot==null || boot.Speech==null || boot.sampleVoices.Length<2) {
            boot=null;throw new InvalidOperationException("Play the configured validation scene first.");
        }
        boot.SkipIntro();
        pointer=boot.Character.GetComponent<CompanionPointerInteraction>();
        oldPointer=pointer.enabled;oldGaze=boot.Motion.allowPointer;
        pointer.enabled=false;boot.Motion.allowPointer=false;boot.Expression.CancelTouch();
        passed.Clear();failed.Clear();clips.Clear();
        stage=0;PlayClip(0);
        EditorApplication.update+=Tick;
        return "Real-time voice checks started; report will be saved after approximately 40 seconds.";
    }
    static void Enter(int next) {stage=next;stageAt=EditorApplication.timeSinceStartup;}
    static void Check(bool ok,string name) {if(ok)passed.Add(name);else failed.Add(name);}
    static void PlayClip(int index) {
        initialOpen=boot.Speech.OpenFrames;initialClosed=boot.Speech.ClosedFrames;
        boot.Speech.Play(boot.sampleVoices[index]);stageAt=EditorApplication.timeSinceStartup;
    }
    static void Tick() {
        if(!EditorApplication.isPlaying || boot==null) {Finish(false);return;}
        try {
            var speech=boot.Speech;
            double age=EditorApplication.timeSinceStartup-stageAt;
            if(stage<2) {
                var clip=boot.sampleVoices[stage];
                if(age>clip.length+5) {Check(false,"Voice playback timed out: "+clip.name);Finish(false);return;}
                // EditorApplication.update can run before the driver's next Update.
                // Wait for its end-of-playback cleanup, within the explicit timeout.
                if(age>.5 && !speech.voice.isPlaying && speech.Duration==0) {
                    var result=new ClipResult {name=clip.name,duration=clip.length,samples=clip.samples,
                        openFrames=speech.OpenFrames-initialOpen,closedFrames=speech.ClosedFrames-initialClosed,
                        completed=speech.Duration==0 && !speech.MouthOpen};
                    clips.Add(result);
                    Check(result.completed && result.openFrames>0 && result.closedFrames>0,"Real voice opens, closes and finishes: "+clip.name);
                    if(stage==0){Enter(1);PlayClip(1);}else {Enter(2);speech.Play(boot.sampleVoices[0]);}
                }
            } else if(stage==2 && age>.4) {
                speech.Pause();pauseSample=speech.voice.timeSamples;Enter(3);
            } else if(stage==3 && age>.25) {
                Check(speech.Paused && speech.voice.timeSamples==pauseSample && !speech.MouthOpen &&
                    boot.Motion.State==CompanionLocalMotion.PresentationState.Speaking,"Pause freezes playback and closes mouth while retaining Speaking");
                speech.Resume();Enter(4);
            } else if(stage==4 && age>.35) {
                Check(!speech.Paused && speech.voice.isPlaying && speech.voice.timeSamples>pauseSample,"Resume continues from paused playback position");
                speech.voice.mute=true;Enter(5);
            } else if(stage==5 && age>.25) {
                Check(speech.voice.isPlaying && !speech.MouthOpen && boot.Motion.State==CompanionLocalMotion.PresentationState.Speaking,"Explicit mute closes mouth while voice time continues");
                speech.voice.mute=false;speech.voice.volume=.01f;
                speech.voice.timeSamples=FindStrongSample(boot.sampleVoices[0]);initialOpen=speech.OpenFrames;Enter(6);
            } else if(stage==6 && age>.5) {
                Check(speech.voice.isPlaying && speech.OpenFrames>initialOpen,"Low nonzero volume retains PCM-driven mouth movement");
                speech.voice.volume=1;generation=speech.Generation;speech.Play(boot.sampleVoices[1]);Enter(7);
            } else if(stage==7 && age>.3) {
                Check(speech.voice.clip==boot.sampleVoices[1] && speech.Generation>generation && speech.voice.isPlaying,"Replacing voice binds mouth to the new clip and generation");
                generation=speech.Generation;speech.Stop();Enter(8);
            } else if(stage==8 && age>.3) {
                Check(speech.Generation>generation && speech.voice.clip==null && !speech.MouthOpen && speech.Duration==0,"Cancellation clears old voice and mouth driver");
                otherAudio=new GameObject("Temporary independent audio smoke");
                var other=otherAudio.AddComponent<AudioSource>();other.clip=boot.sampleVoices[0];other.mute=true;other.Play();
                initialOpen=speech.OpenFrames;Enter(9);
            } else if(stage==9 && age>.3) {
                Check(otherAudio.GetComponent<AudioSource>().isPlaying && speech.OpenFrames==initialOpen && !speech.MouthOpen,"Independent AudioSource cannot drive character mouth");
                Finish(true);
            }
        } catch(Exception exception) {failed.Add(exception.ToString());Finish(false);}
    }
    static int FindStrongSample(AudioClip clip) {
        int count=Mathf.Min(clip.samples,clip.frequency*4),window=Mathf.Max(1,clip.frequency/50);
        var pcm=new float[count*clip.channels];clip.GetData(pcm,0);
        double maximum=-1;int best=0;
        for(int i=0;i+window<count;i+=window) {
            double sum=0;for(int j=i*clip.channels;j<(i+window)*clip.channels;j++)sum+=(double)pcm[j]*pcm[j];
            if(sum>maximum){maximum=sum;best=i;}
        }
        return best;
    }
    static void Finish(bool complete) {
        EditorApplication.update-=Tick;
        if(otherAudio!=null)UnityEngine.Object.DestroyImmediate(otherAudio);
        if(boot!=null) {
            boot.Speech.Stop();boot.Speech.voice.mute=false;boot.Speech.voice.volume=1;
            boot.Motion.allowPointer=oldGaze;if(pointer!=null)pointer.enabled=oldPointer;
        }
        var report=new Report {createdAt=DateTime.UtcNow.ToString("o"),clips=clips.ToArray(),
            passed=passed.ToArray(),failed=failed.ToArray(),allPassed=complete&&failed.Count==0};
        Directory.CreateDirectory(CompanionValidationTools.OutputDirectory);
        File.WriteAllText(Path.Combine(CompanionValidationTools.OutputDirectory,"v02_voice_report.json"),JsonUtility.ToJson(report,true));
        boot=null;pointer=null;otherAudio=null;
    }
}
