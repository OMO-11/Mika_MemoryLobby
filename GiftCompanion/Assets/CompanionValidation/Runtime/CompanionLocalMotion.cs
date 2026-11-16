using Spine;
using Spine.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>Local offsets on top of original animation; never changes source SkeletonData.</summary>
[DefaultExecutionOrder(-40)]
public sealed class CompanionLocalMotion : MonoBehaviour {
    public enum PresentationState { Idle, Thinking, Speaking }
    public PresentationState State { get; private set; }
    public CompanionBehaviorProfile profile;
    public Camera renderCamera;
    public bool allowPointer = true;
    public bool wander = true;
    public Vector2? ReviewGaze { get; set; }
    public Vector2 AppliedGaze { get; private set; }
    public float AppliedHead { get; private set; }
    public bool PointerInside { get; private set; }
    public SkeletonAnimation Character { get; private set; }
    SkeletonAnimation attachedCharacter;
    CompanionExpressionOverlay expression;
    Bone gaze, head;
    Bone touchPoint;
    TransformConstraint gazeCoupling;
    float touchBaseX,touchBaseY,couplingBaseX,couplingBaseY;
    Vector2 smoothTouch;
    float gazeBaseX, gazeBaseY, headBase;
    bool wroteBones;
    float elapsed, dwell;
    Vector2 target, smoothGaze, previousPointer;
    float smoothHead;
    float thinkingClock, thinkingPeriod=7.2f, lookBackAt=1.9f, lookBackHold=.7f;
    int thinkingCycle;
    public void Bind(SkeletonAnimation character) {
        Unbind();
        attachedCharacter=character;
        Character = character;
        gaze = Character.Skeleton.FindBone(profile.gazeBone);
        head = Character.Skeleton.FindBone(profile.headBone);
        touchPoint=string.IsNullOrEmpty(profile.touchPointBone)?null:Character.Skeleton.FindBone(profile.touchPointBone);
        gazeCoupling=string.IsNullOrEmpty(profile.gazeCouplingConstraint)?null:Character.Skeleton.FindTransformConstraint(profile.gazeCouplingConstraint);
        if (gaze == null || head == null) {
            Debug.LogWarning("Local motion disabled: required profile bones are missing.");
            enabled = false;
            return;
        }
        Character.BeforeApply += Restore;
        Character.UpdateLocal += Apply;
        Character.OnAnimationRebuild += Rebuild;
        expression=GetComponent<CompanionExpressionOverlay>();
    }
    void Update() {
        Advance(Time.unscaledDeltaTime);
    }
    public void Advance(float dt) {
        if (Character == null) return;
        if (expression == null) expression=GetComponent<CompanionExpressionOverlay>();
        elapsed += dt;
        // Slow wandering changes only the eye target; the original Idle remains underneath.
        Vector2 desired = wander ? new Vector2(Mathf.Sin(elapsed*.37f)*.25f,Mathf.Sin(elapsed*.23f)*.15f) : Vector2.zero;
        var mouse = Mouse.current;
        PointerInside = false;
        if (allowPointer && mouse != null && Application.isFocused &&
            (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())) {
            Vector2 p = mouse.position.ReadValue();
            PointerInside = p.x>=0 && p.x<Screen.width && p.y>=0 && p.y<Screen.height;
            if (PointerInside) {
                Ray ray = renderCamera.ScreenPointToRay(p);
                Plane plane = new Plane(transform.forward,transform.position);
                float distance;
                if (plane.Raycast(ray,out distance)) {
                    Vector3 local = transform.InverseTransformPoint(ray.GetPoint(distance));
                    desired = new Vector2(local.x-gaze.WorldX, local.y-gaze.WorldY)*.08f;
                }
                dwell = (p-previousPointer).magnitude < Mathf.Max(1,30*dt) ? dwell+dt : 0;
                previousPointer = p;
            }
        }
        if (!PointerInside) dwell = 0;
        if(State==PresentationState.Idle && wander && PointerInside && dwell>=profile.pointerAttentionSeconds)
            desired=new Vector2(Mathf.Sin(elapsed*.37f)*.25f,Mathf.Sin(elapsed*.23f)*.15f);
        if (State==PresentationState.Thinking) {
            thinkingClock+=dt;
            while(thinkingClock>=thinkingPeriod) {
                thinkingClock-=thinkingPeriod;thinkingCycle++;
                // Change the pauses between cycles, without sampling noise each frame.
                thinkingPeriod=7.2f+Mathf.Sin(thinkingCycle*1.73f)*1.15f;
                lookBackAt=1.9f+Mathf.Sin(thinkingCycle*2.13f)*.4f;
                lookBackHold=.7f+Mathf.Sin(thinkingCycle*1.37f)*.15f;
            }
            float t=thinkingClock, returning=lookBackAt+.45f, looking=returning+lookBackHold;
            float side=t<lookBackAt?1:t<returning?1-Mathf.SmoothStep(0,1,(t-lookBackAt)/.45f):t<looking?0:t<looking+.5f?Mathf.SmoothStep(0,1,(t-looking)/.5f):1;
            desired=new Vector2(-.38f,.27f)*side;
            dwell=1;
        } else if (State==PresentationState.Speaking) {
            desired=Vector2.zero; dwell=0;
        }
        if (expression != null && expression.TouchActive) {
            desired=expression.TouchGaze; dwell=0;
        }
        if (ReviewGaze.HasValue) { desired=ReviewGaze.Value; dwell=1; }
        desired.x /= Mathf.Max(.001f,profile.gazeLimit.x);
        desired.y /= Mathf.Max(.001f,profile.gazeLimit.y);
        desired = Vector2.ClampMagnitude(desired,1);
        target = Vector2.Scale(desired,profile.gazeLimit);
        smoothGaze = Vector2.Lerp(smoothGaze,target,1-Mathf.Exp(-dt/ .2f));
        float headTarget = profile.headFollowEnabled && dwell>.55f ? -desired.x*profile.headLimit :
            profile.headFollowEnabled && wander && State==PresentationState.Idle && !PointerInside ? Mathf.Sin(elapsed*.17f)*.18f : 0;
        if (State==PresentationState.Thinking && profile.headFollowEnabled) headTarget=.9f;
        if (expression != null && expression.TouchActive) headTarget=0;
        if(profile.eyesOnlyIdle && State==PresentationState.Idle)headTarget=0;
        smoothHead = Mathf.Lerp(smoothHead,headTarget,1-Mathf.Exp(-dt/1.05f));
        Vector2 touchTarget=expression!=null&&expression.TouchActive?expression.TouchDirection*.14f:Vector2.zero;
        smoothTouch=Vector2.Lerp(smoothTouch,touchTarget,1-Mathf.Exp(-dt/.14f));
    }
    public void SetState(PresentationState state) {
        if (State==state) return;
        State=state;
        if(state==PresentationState.Thinking) {
            thinkingClock=0;thinkingCycle=0;thinkingPeriod=7.2f;lookBackAt=1.9f;lookBackHold=.7f;
        }
    }
    void Restore(ISkeletonAnimation _) {
        if (!wroteBones) return;
        gaze.X=gazeBaseX; gaze.Y=gazeBaseY; head.Rotation=headBase;
        if(touchPoint!=null){touchPoint.X=touchBaseX;touchPoint.Y=touchBaseY;}
        if(gazeCoupling!=null){gazeCoupling.MixX=couplingBaseX;gazeCoupling.MixY=couplingBaseY;}
        wroteBones=false;
    }
    void Apply(ISkeletonAnimation _) {
        gazeBaseX=gaze.X; gazeBaseY=gaze.Y; headBase=head.Rotation;
        if(touchPoint!=null){touchBaseX=touchPoint.X;touchBaseY=touchPoint.Y;}
        if(gazeCoupling!=null){couplingBaseX=gazeCoupling.MixX;couplingBaseY=gazeCoupling.MixY;}
        // Convert screen-aligned skeleton displacement through the original parent matrix.
        var p=gaze.Parent;
        float det=p.A*p.D-p.B*p.C;
        if (Mathf.Abs(det)<.00001f) return;
        gaze.X += (p.D*smoothGaze.x-p.B*smoothGaze.y)/det;
        gaze.Y += (-p.C*smoothGaze.x+p.A*smoothGaze.y)/det;
        head.Rotation += smoothHead;
        // Cancel only the added gaze displacement, preserving the source constraint's neutral pose.
        if(touchPoint!=null) {
            touchPoint.X+=smoothTouch.x;touchPoint.Y+=smoothTouch.y;
            if(gazeCoupling!=null&&profile.eyesOnlyIdle) {
                float mx=Mathf.Clamp(gazeCoupling.MixX,0,.95f),my=Mathf.Clamp(gazeCoupling.MixY,0,.95f);
                float dx=-smoothGaze.x*mx/(1-mx),dy=-smoothGaze.y*my/(1-my);
                var parent=touchPoint.Parent;float determinant=parent.A*parent.D-parent.B*parent.C;
                if(Mathf.Abs(determinant)>.00001f) {
                    touchPoint.X+=(parent.D*dx-parent.B*dy)/determinant;
                    touchPoint.Y+=(-parent.C*dx+parent.A*dy)/determinant;
                }
            }
        }
        AppliedGaze=smoothGaze; AppliedHead=smoothHead;
        wroteBones=true;
    }
    public void Unbind() {
        Restore(null);
        if (Character != null) {
            Character.BeforeApply -= Restore;
            Character.UpdateLocal -= Apply;
            Character.OnAnimationRebuild -= Rebuild;
        }
        Character=null; gaze=null; head=null;touchPoint=null;gazeCoupling=null;
        smoothGaze=Vector2.zero; smoothHead=0;
        smoothTouch=Vector2.zero;
    }
    void Rebuild(ISkeletonAnimation _) { if (attachedCharacter != null) Bind(attachedCharacter); }
    void OnEnable() { if (attachedCharacter != null && Character == null) Bind(attachedCharacter); }
    public void SetReviewPose(Vector2 offset, float headDegrees) {
        allowPointer=false; wander=false; ReviewGaze=offset;
        Vector2 unit=new Vector2(offset.x/profile.gazeLimit.x,offset.y/profile.gazeLimit.y);
        smoothGaze=Vector2.Scale(Vector2.ClampMagnitude(unit,1),profile.gazeLimit);
        smoothHead=Mathf.Clamp(headDegrees,-profile.headLimit,profile.headLimit);
        Character.timeScale=0;
        Character.AnimationState.GetCurrent(0).TrackTime=0;
        Character.Update(0);
        Character.LateUpdate();
    }
    void OnDisable() { Unbind(); }
    void OnApplicationFocus(bool focused) {
        if (!focused) { dwell=0; PointerInside=false; target=Vector2.zero; }
    }
}
