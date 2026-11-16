using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Speech-only PCM envelope at the actual AudioSource playback position; no phoneme claims.</summary>
[DefaultExecutionOrder(-20)]
public sealed class CompanionSpeechDriver : MonoBehaviour {
    public AudioSource voice;
    public CompanionExpressionOverlay expression;
    public CompanionLocalMotion motion;
    public int Generation { get; private set; }
    public bool Paused { get; private set; }
    public bool MouthOpen { get; private set; }
    public int OpenFrames { get; private set; }
    public int ClosedFrames { get; private set; }
    public float Energy { get; private set; }
    public float Openness { get; private set; }
    public float Duration { get { return current==null?0:current.length; } }
    sealed class Envelope { public float[] values; public int window; public float normalizer; }
    readonly Dictionary<AudioClip,Envelope> cache=new Dictionary<AudioClip,Envelope>();
    AudioClip current;
    Envelope envelope;
    float smoothed,changeAt;
    bool playing;
    Envelope Analyze(AudioClip clip) {
        Envelope result;
        if(cache.TryGetValue(clip,out result))return result;
        var pcm=new float[clip.samples*clip.channels];
        if(!clip.GetData(pcm,0))throw new InvalidOperationException("Voice PCM unavailable; use Decompress On Load.");
        int window=Mathf.Max(1,Mathf.RoundToInt(clip.frequency*.025f));
        var levels=new float[(clip.samples+window-1)/window];
        for(int i=0;i<levels.Length;i++) {
            int start=i*window*clip.channels,end=Mathf.Min(pcm.Length,start+window*clip.channels);
            double sum=0;for(int j=start;j<end;j++)sum+=(double)pcm[j]*pcm[j];
            levels[i]=(float)Math.Sqrt(sum/Math.Max(1,end-start));
        }
        var sorted=(float[])levels.Clone();Array.Sort(sorted);
        result=new Envelope {values=levels,window=window,normalizer=Mathf.Max(.025f,sorted[(int)((sorted.Length-1)*.95f)])};
        cache.Add(clip,result);return result;
    }
    public void Play(AudioClip clip) {
        Stop();if(clip==null)return;
        try {envelope=Analyze(clip);}catch(Exception e){Debug.LogWarning(e.Message);return;}
        current=clip;voice.clip=clip;voice.loop=false;voice.Play();playing=true;Paused=false;
        motion.SetState(CompanionLocalMotion.PresentationState.Speaking);
        expression.SetSpeech(true,false);
    }
    public void Pause() {if(playing&&!Paused){voice.Pause();Paused=true;MouthOpen=false;Openness=0;smoothed=0;expression.SetSpeech(true,0f);}}
    public void Resume() {if(playing&&Paused){voice.UnPause();Paused=false;}}
    public void Stop() {
        Generation++;if(voice!=null){voice.Stop();voice.clip=null;}
        current=null;envelope=null;playing=false;Paused=false;MouthOpen=false;Openness=0;smoothed=0;Energy=0;
        if(expression!=null)expression.SetSpeech(false,false);
        if(motion!=null&&motion.State==CompanionLocalMotion.PresentationState.Speaking)
            motion.SetState(CompanionLocalMotion.PresentationState.Idle);
    }
    void Update() {
        if(!playing)return;
        if(!Paused&&!voice.isPlaying){Stop();return;}
        if(Paused || voice.mute || voice.volume<=0){MouthOpen=false;Openness=0;smoothed=0;expression.SetSpeech(true,0f);ClosedFrames++;return;}
        float position=(float)voice.timeSamples/envelope.window;
        int index=Mathf.Clamp((int)position,0,envelope.values.Length-1);
        float rms=Mathf.Lerp(envelope.values[index],envelope.values[Mathf.Min(index+1,envelope.values.Length-1)],position-index);
        float target=rms<.004f?0:Mathf.Clamp01(rms/envelope.normalizer);
        smoothed=Mathf.Lerp(smoothed,target,1-Mathf.Exp(-Time.unscaledDeltaTime/(target>smoothed?.045f:.10f)));
        Energy=smoothed;
        bool next=MouthOpen?smoothed>.10f:smoothed>.16f;
        if(next!=MouthOpen&&Time.unscaledTime-changeAt>.065f){MouthOpen=next;changeAt=Time.unscaledTime;}
        Openness=MouthOpen?Mathf.Max(.03f,Mathf.InverseLerp(.08f,.85f,smoothed)):0;
        expression.SetSpeech(true,Openness);
        if(MouthOpen)OpenFrames++;else ClosedFrames++;
    }
    void OnDisable(){Stop();}
}
