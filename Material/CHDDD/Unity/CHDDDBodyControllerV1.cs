using System;
using UnityEngine;

/// <summary>Manifest-based playback helper. Window movement and interaction arbitration belong to the pet host.</summary>
public sealed class CHDDDBodyControllerV1 : MonoBehaviour
{
    [Serializable] public class ActionEntry {
        public string name,clip,emotion,enterClip,exitClip,nextAction,logicalAction;
        public bool loop; public float durationSeconds;
    }
    [Serializable] public class ActionMap { public string defaultAction; public float defaultTransitionSeconds; public ActionEntry[] actions; }
    public TextAsset actionMap;
    public Animator animator;
    public CHDDDExpressionController expressions;
    public bool followSuggestedEmotion=true;
    public string CurrentAction { get; private set; }
    ActionMap map; string lastObservedClip;

    void Awake() {
        if (animator==null) animator=GetComponent<Animator>();
        if (expressions==null) expressions=GetComponent<CHDDDExpressionController>();
        if (actionMap!=null) map=JsonUtility.FromJson<ActionMap>(actionMap.text);
        if (animator!=null) animator.applyRootMotion=false;
        if (map!=null) PlayAction(map.defaultAction,0);
    }
    ActionEntry Find(string name) { return map?.actions==null ? null : Array.Find(map.actions,a=>a.name==name); }
    public bool PlayAction(string name,float seconds=-1) {
        var entry=Find(name) ?? Find(map?.defaultAction);
        if (entry==null || animator==null) return false;
        string clip=string.IsNullOrEmpty(entry.enterClip) ? entry.clip : entry.enterClip;
        if (!animator.HasState(0,Animator.StringToHash(clip))) return false;
        CurrentAction=entry.logicalAction;lastObservedClip=clip;
        float duration=seconds<0 ? map.defaultTransitionSeconds : Mathf.Max(0,seconds);
        if (duration==0) animator.Play(clip,0,0);
        else animator.CrossFadeInFixedTime(clip,duration,0,0);
        if (followSuggestedEmotion && expressions!=null) expressions.SetEmotion(entry.emotion);
        return true;
    }
    public void ReturnToIdle() {
        var current=Find(CurrentAction);
        if (current!=null && !string.IsNullOrEmpty(current.exitClip)) {
            animator.CrossFadeInFixedTime(current.exitClip,.15f,0,0);
            lastObservedClip=current.exitClip;
            if (followSuggestedEmotion && expressions!=null) expressions.SetEmotion("neutral");
        } else if (map!=null) PlayAction(map.defaultAction);
    }
    void Update() { SynchronizeState(); }
    public void SynchronizeState() {
        if (map?.actions==null || animator==null || animator.IsInTransition(0)) return;
        var state=animator.GetCurrentAnimatorStateInfo(0);
        foreach (var entry in map.actions) if (state.shortNameHash==Animator.StringToHash(entry.clip)) {
            CurrentAction=entry.logicalAction;
            if (lastObservedClip!=entry.clip) {
                lastObservedClip=entry.clip;
                if (followSuggestedEmotion && expressions!=null) expressions.SetEmotion(entry.emotion);
            }
            return;
        }
    }
}
