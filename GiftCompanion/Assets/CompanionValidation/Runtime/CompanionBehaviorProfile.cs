using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Companion/Behavior Profile")]
public sealed class CompanionBehaviorProfile : ScriptableObject {
    public string resourceFolder;
    public string skeletonFile;
    public string atlasFile;
    public string[] textureFiles;
    public string idleAnimation;
    public string introAnimation="Start_Idle_01";
    public string gazeBone;
    public string headBone;
    public string mouthSlot;
    public string closedMouth;
    public string openMouth;
    public string gazeCouplingConstraint;
    public string touchPointBone;
    public string touchMotionAnimation;
    public string touchReleaseAnimation;
    public string[] headHitSlots = Array.Empty<string>();
    public float expressionBlendSeconds = .16f;
    public float mouthBlendSeconds = .065f;
    public bool eyesOnlyIdle = true;
    public float pointerAttentionSeconds = 2.2f;
    public bool enablePat10Trial = false;
    public AttachmentSetting[] blinkAttachments = Array.Empty<AttachmentSetting>();
    public float skeletonScale = .01f;
    public Vector2 gazeLimit = new Vector2(.48f, .34f);
    public float headLimit = 1.5f;
    public Vector2 headHitCenter = new Vector2(0, 0);
    public Vector2 headHitRadius = new Vector2(1.6f, 1.25f);
    public bool headFollowEnabled;
    public ExpressionSetting[] touchExpressions = Array.Empty<ExpressionSetting>();
    public ExpressionSetting[] thinkingExpressions = Array.Empty<ExpressionSetting>();
    [Serializable] public sealed class AttachmentSetting {
        public string slot;
        public string attachment;
    }
    [Serializable] public sealed class ExpressionSetting {
        public string id;
        public string sourceAnimation;
        public float sourceTime;
        public bool enabled;
        public bool closedEyes;
        public string openMouth;
        public AttachmentSetting[] attachments = Array.Empty<AttachmentSetting>();
        public Vector2 gazeBias;
    }
}
