using System;
using UnityEngine;

/// <summary>Body and facial performance playback; movement of the desktop window belongs to the host.</summary>
[DefaultExecutionOrder(100)]
public sealed class CHDDDBodyControllerV2 : MonoBehaviour
{
    [Serializable] public class ActionEntry {
        public string name,clip,faceClip,emotion,enterClip,exitClip,nextAction,logicalAction,source;
        public bool loop; public float durationSeconds,transitionSeconds;
    }
    [Serializable] public class ActionMap { public int revision; public string defaultAction; public float defaultTransitionSeconds; public ActionEntry[] actions; }
    public TextAsset actionMap;
    public Animator animator;
    public CHDDDActionFaceControllerV2 actionFaces;
    public CHDDDExpressionController expressions;
    public bool useActionExpressions=true;
    public string CurrentAction { get; private set; }
    ActionMap map;

    void Awake() {
        if(animator==null) animator=GetComponent<Animator>();
        if(actionFaces==null) actionFaces=GetComponent<CHDDDActionFaceControllerV2>();
        if(expressions==null) expressions=GetComponent<CHDDDExpressionController>();
        if(actionMap!=null) map=JsonUtility.FromJson<ActionMap>(actionMap.text);
        if(animator!=null) animator.applyRootMotion=false;
        if(expressions!=null) expressions.enabled=!useActionExpressions;
        if(map!=null) PlayAction(map.defaultAction,0);
    }
    ActionEntry Find(string name) { return map?.actions==null ? null : Array.Find(map.actions,a=>a.name==name || a.clip==name); }
    public bool PlayAction(string name,float seconds=-1) {
        var entry=Find(name) ?? Find(map?.defaultAction);if(entry==null || animator==null) return false;
        var first=string.IsNullOrEmpty(entry.enterClip) ? entry : Find(entry.enterClip);
        float defaultSeconds=entry.transitionSeconds>0 ? entry.transitionSeconds : map.defaultTransitionSeconds;
        return PlayEntry(first,seconds<0 ? defaultSeconds : Mathf.Max(0,seconds));
    }
    bool PlayEntry(ActionEntry entry,float seconds) {
        if(entry==null || !animator.HasState(0,Animator.StringToHash(entry.clip))) return false;
        CurrentAction=entry.logicalAction;
        if(expressions!=null) expressions.enabled=!useActionExpressions;
        if(useActionExpressions && actionFaces!=null) actionFaces.BeginTransition(seconds);
        if(seconds<=0) animator.Play(entry.clip,0,0);
        else animator.CrossFadeInFixedTime(entry.clip,seconds,0,0);
        if(useActionExpressions && animator.layerCount>1) {
            if(seconds<=0) animator.Play(entry.faceClip,1,0);
            else animator.CrossFadeInFixedTime(entry.faceClip,seconds,1,0);
        }
        return true;
    }
    public void ReturnToIdle() {
        var current=Find(CurrentAction);
        if(current!=null && !string.IsNullOrEmpty(current.exitClip)) PlayEntry(Find(current.exitClip),.15f);
        else if(map!=null) PlayAction(map.defaultAction);
    }
    public void SetManualEmotion(string emotion,float seconds=.5f) {
        useActionExpressions=false;
        if(expressions!=null) { expressions.enabled=true;expressions.SetEmotion(emotion,seconds); }
    }
    public void ResumeActionExpressions(float seconds=.28f) {
        useActionExpressions=true;
        if(expressions!=null) expressions.enabled=false;
        if(actionFaces!=null) actionFaces.BeginTransition(seconds);
        var state=animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        if(map?.actions!=null) foreach(var e in map.actions)
            if(state.shortNameHash==Animator.StringToHash(e.clip)) {
                animator.CrossFadeInFixedTime(e.faceClip,seconds,1,state.normalizedTime*e.durationSeconds);break;
            }
    }
    void Update() { SynchronizeState(); }
    public void SynchronizeState() {
        if(map?.actions==null || animator==null) return;
        var state=animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        foreach(var e in map.actions) if(state.shortNameHash==Animator.StringToHash(e.clip)) { CurrentAction=e.logicalAction;return; }
    }
}
