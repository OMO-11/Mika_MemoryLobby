"""Freeze the existing CHDDD handoff without editing art or the main Unity project."""
from pathlib import Path
import hashlib
import json
import re
import shutil

ROOT = Path(__file__).resolve().parents[2]
ASSET = ROOT / "Material/CHDDD"
OUT = ASSET / "Integration"
DATE = "2026-10-11"
CORE_EMOTIONS = ["neutral", "happy", "shy", "concern", "sad", "surprised", "annoyed", "sleepy"]


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def rel(path):
    return path.relative_to(ROOT).as_posix()


def sha(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def file_ref(path, role, **extra):
    path = ROOT / path
    assert path.is_file(), path
    return {"path": rel(path), "role": role, "bytes": path.stat().st_size, "sha256": sha(path), **extra}


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    body = read(ASSET / "Blender/body_action_map_v2.json")
    unity = read(ASSET / "Unity/body_action_map_v2.json")
    faces = read(ASSET / "Blender/action_face_profiles_v2.json")
    unity_faces = read(ASSET / "Unity/action_face_profiles_v2.json")
    expressions = read(ASSET / "Blender/expression_map_v2.json")["expressions"]
    unity_expressions = read(ASSET / "Unity/expression_map_v2.json")["expressions"]
    legacy = read(ASSET / "character_manifest.json")
    proof = read(ASSET / "Reviews/UnityExpressionV17/unity_validation.json")
    blend_sha = sha(ASSET / "CHDDD_actions_v2.blend")
    assert body["revision"] == 17 and unity["revision"] == 7
    assert proof["passed"] and proof["sourceSHA256"] == blend_sha

    # Keep the verified reference outside Temp, including Unity GUIDs and project settings.
    reference_source = ROOT / "Temp/UnityExpressionReviewV17"
    reference = OUT / "UnityExpressionReference"
    for directory in ("Assets", "Packages", "ProjectSettings"):
        shutil.copytree(reference_source / directory, reference / directory, dirs_exist_ok=True)
    source_files = sorted(
        p for directory in ("Assets", "Packages", "ProjectSettings")
        for p in (reference_source / directory).rglob("*") if p.is_file()
    )
    copied = []
    for source in source_files:
        target = reference / source.relative_to(reference_source)
        assert sha(source) == sha(target), target
        copied.append({"path": target.relative_to(reference).as_posix(), "sha256": sha(target), "bytes": target.stat().st_size})
    write(OUT / "unity_expression_reference_files.json", {
        "schemaVersion": 1, "date": DATE, "pathBase": "Material/CHDDD/Integration/UnityExpressionReference",
        "sourceProject": rel(reference_source), "excluded": ["Library", "Logs", "UserSettings"],
        "copiedFileCount": len(copied), "files": copied,
    })
    (reference / "README.md").write_text(
        "# Unity 表情参考工程（冻结）\n\n"
        "2026-10-11：从已完成渲染验证的 Temp/UnityExpressionReviewV17 原样复制 Assets、Packages、ProjectSettings，保留全部 .meta。未复制 Library、Logs、UserSettings。\n\n"
        "使用 Unity 6000.3.7f1 独立打开本目录，然后打开 Assets/Review/UnityExpressions.unity。"
        "包含 >< V15 和 -ω- V17 的几何、8 个形变、遮罩、材质与实际渲染用参数。FBX 仅含测试用 Idle 参照，不能替代完整 18 动作模型。\n\n"
        "Editor/CHDDDUnityExpressionReview.cs 为编辑器渲染工具，非 Player 运行时控制器；输出路径仍指向原工作区 Reviews/UnityExpressionV17。"
        "正式移植请按 Docs/Production/CHDDD/动作与表情详解.md 的遮罩、透明和复位约定实现。不要整目录混入正式工程导致 GUID 冲突。\n\n"
        "验证记录与预览保留在 Material/CHDDD/Reviews/UnityExpressionV17；副本逐文件哈希见上级 unity_expression_reference_files.json。\n",
        encoding="utf-8",
    )

    # Semantic descriptions are for the host, not new animation instructions.
    semantics = {
        "Idle": ("自然待机", "默认或无其他状态", ["放松站姿", "轻呼吸与重心微调", "自然循环"], "neutral 与短眨眼", "state"),
        "Walk": ("原地行走", "宿主移动期间", ["来源 Café 步态", "自然发衣跟随", "宿主决定停止"], "neutral 与自然眨眼", "state"),
        "Thinking": ("思考", "等待 LLM / 解析意图", ["轻托下巴", "头肩和视线配合", "低强度循环"], "思考、关切与轻微眼眉嘴变化", "state"),
        "Working": ("专注工作", "工具执行期间", ["稍前倾", "小幅规律手部动作", "专注循环"], "专注与自然眼眉嘴变化", "state"),
        "HeadPat": ("持续摸头", "头部长按成立", ["Enter 接受触摸", "低头放松与轻摇", "释放走 Exit"], "接受触摸、舒服、放松", "touch"),
        "Dragging": ("原版举起", "真实拖动开始", ["Enter 拿起", "原动画 29 循环", "释放走 Exit"], "举起专属过程；排除原眼部缩放", "drag"),
        "Sleep": ("原地打盹", "宿主进入休息状态", ["Enter 呵欠渐闭眼", "低头慢呼吸", "唤醒走 Exit"], "闭眼与柔和困倦眉", "state"),
        "Wave": ("挥手问候", "低频问候 / 欢迎回来", ["收身预备", "探身单手招手两次并扇翼", "直接收手、发衣延迟收稳"], "注意用户、明快微笑、短眨眼、放松", "greeting"),
        "Success": ("欢呼庆祝", "工具成功 / 明确夸奖", ["收手下沉蓄势", "挺胸踮脚、双臂向头顶斜上展开 Y 形", "关键姿态停留、缓冲落回"], "起举迅速闭眼，约 0.65–0.67 秒闭合；上弧笑眉欢呼后恢复", "outcome"),
        "Confused": ("疑惑挠头", "工具失败 / 疑惑回应", ["停下稍后缩", "歪头约 23 度、掌心朝自己挠头两次", "可读停留后连续收手恢复"], "疑问、略担心、缓和", "outcome"),
        "HeadClick": ("头部短点受惊与佯怒", "头部短点击", ["缩身后撤、双手护头", "放下手并认出用户", "抿嘴假生气后恢复"], "闭眼三角嘴 -A- 受惊，随后皱眉抿嘴；不使用笑嘴", "touch"),
        "BodyClick": ("身体点击害羞回应", "身体短点击", ["收肩蜷身、手近胸、脚尖内扣", "后缩停留、移开视线再回望", "尾段轻抖翅膀并收稳"], "惊觉、害羞、回望、放松", "touch"),
    }
    internal_descriptions = {
        "HeadPat_Enter": "从站姿进入接受摸头，脸部同步放松",
        "HeadPat_Exit": "释放后收回低头与轻摇，回到待机",
        "Dragging_Enter": "进入被举起姿态，配合短暂惊觉",
        "Dragging_Exit": "从举起缓冲放下，发衣收稳",
        "Sleep_Enter": "掩嘴呵欠、低头、渐闭眼",
        "Sleep_Exit": "唤醒、恢复站姿与睁眼",
    }
    u_actions = {a["name"]: a for a in unity["actions"]}
    profiles = {p["name"]: p for p in faces["profiles"]}
    u_profiles = {p["name"]: p for p in unity_faces["profiles"]}
    actions = []
    for entry in body["actions"]:
        name = entry["name"]
        item = dict(entry)
        item["publicLogicalAction"] = name in semantics
        item["stage"] = "enter" if name.endswith("_Enter") else "exit" if name.endswith("_Exit") else "loop" if entry["loop"] else "one-shot"
        item["intentEmotion"] = item.pop("emotion")
        item["intentEmotionIsSharedEnum"] = item["intentEmotion"] in CORE_EMOTIONS
        if name in semantics:
            label, trigger, stages, face_description, trigger_kind = semantics[name]
            item.update(displayName=label, trigger=trigger, bodyPhases=stages, faceSequence=face_description, triggerKind=trigger_kind)
        else:
            item.update(displayName=internal_descriptions[name], trigger="内部流转，不直接暴露给业务 / LLM", triggerKind="internal")
        aliases = [name.lower()] if name in semantics and name.lower() != name else []
        if name == "Success":
            aliases += ["succeed", "Succeed", "success"]
        item["aliases"] = list(dict.fromkeys(aliases))
        item["runtime"] = {
            "unityDeliveryRevision": 7, "clipPresent": name in u_actions,
            "profilePresent": name in u_profiles, "integratedInMainProgram": False,
            "hasKnownBodyChangesAfterUnity7": name in ("Success", "HeadClick"),
            "hasKnownFaceChangesAfterUnity7": profiles[name] != u_profiles.get(name),
            "latestPackageFullySynchronized": False,
        }
        item["faceData"] = {
            "profileName": name, "fps": faces["fps"], "sampleCount": len(profiles[name]["samples"]),
            "sampleFields": sorted(profiles[name]["samples"][0]),
            "authoringProfilePath": "Material/CHDDD/Blender/action_face_profiles_v2.json",
            "deliveredProfilePath": "Material/CHDDD/Unity/action_face_profiles_v2.json",
            "samplingClock": "body Animator normalizedTime", "manualPresetOverridesActionFace": True,
        }
        actions.append(item)

    emotions = []
    for expr in expressions:
        emotions.append({**expr, "sharedEmotionEnum": expr["name"] in CORE_EMOTIONS,
                         "publicEmotion": expr["name"] in CORE_EMOTIONS,
                         "fallbackSharedEmotion": "concern" if expr["name"] == "confused" else expr["name"],
                         "unity7Available": True, "integratedInMainProgram": False})

    sample_path = "Material/CHDDD/Integration/UnityExpressionReference/Assets/Review/expression_samples.json"
    samples = read(ROOT / sample_path)
    special = [
        {
            "id": "Squeeze", "aliases": ["><", ">o<"], "revision": 15,
            "status": "provisionally-retained-and-frozen-at-user-request",
            "humanReview": "用户暂时保留 V15", "fallbackEmotion": "happy",
            "objects": ["CHDDD_TestSqueeze_EyeLines"],
            "texturePath": "Material/CHDDD/Textures/mika_squeeze_eyes_review.png",
            "vectorSourcePath": "Material/CHDDD/Textures/mika_squeeze_eyes_review.svg",
            "shapeAction": "Review9_TestSqueeze_Shapes",
            "materialActions": {"CHDDD_MouthAtlas": "Review9_TestSqueeze_Mouth", "CHDDD_Face": "Review9_TestSqueeze_FaceMaterial",
                                "CHDDD_EyeMouth": "Review9_TestSqueeze_OldLashMaterial", "Review9_SqueezeEyeLine": "Review9_TestSqueeze_EyeLineMaterial"},
            "blenderFunction": "preview_review9_expression", "blenderOperator": "chddd.review9_expression",
            "enabledExtraShapes": ["Pet_BrowHappy_Review", "Pet_TestRoundMouth_Review"],
            "description": "Head 绑定镜像平面贴图，真实闭眼后显示 ><，原图集 605 加窄圆口形变；随后复原",
        },
        {
            "id": "Omega", "aliases": ["-ω-"], "revision": 17,
            "status": "frozen-at-user-request-after-unity-render-test",
            "humanReview": "用户结束实验，未单独记录 V17 美术通过", "fallbackEmotion": "sleepy",
            "objects": ["CHDDD_TestOmega_Eyes", "CHDDD_TestOmega_Mouth"],
            "shapeAction": "Review16_TestOmega_Shapes",
            "materialActions": {"Review16_OmegaEyeLine": "Review16_TestOmega_Eyes", "Review16_OmegaMouth": "Review16_TestOmega_MouthLine",
                                "CHDDD_Face": "Review16_TestOmega_Face", "CHDDD_EyeMouth": "Review16_TestOmega_OldLashes", "CHDDD_MouthAtlas": "Review16_TestOmega_MouthAtlas"},
            "blenderFunction": "preview_review16_omega", "blenderOperator": "chddd.review16_omega",
            "eyeStrokeMultiplierVs16": 1.55, "mouthStrokeMultiplierVs16": 1.65,
            "description": "复制原眉毛几何与 UV 作为镜像眼线，独立圆弧 ω 嘴网格；真实闭眼，旧嘴淡出后复原",
        },
    ]
    for expr in special:
        expr.update(durationSeconds=2.4, authoringFps=30, frames=[1, 73], loop=False, sharedEmotionEnum=False,
                    keyFrame=37, headBone="Bip001 Head", unityRenderVerified=True,
                    includedInUnity7Package=False, integratedInMainProgram=False, runtimeEnabled=False,
                    unitySamplePath=sample_path, unitySampleName=expr["id"], unitySampleFps=24,
                    sampleCount=len(next(e for e in samples["expressions"] if e["name"] == expr["id"])["frames"]),
                    sourceKeyNamesRetainedForCompatibility=True)

    bindings = [
        ("default", "Idle", None, "§28/29", "状态驱动"),
        ("move.started", "Walk", None, "§29", "宿主负责移动"),
        ("llm.waiting", "Thinking", "concern", "§1/39", "与共享情绪分离"),
        ("tool.executing", "Working", None, "§39", "持续到实际结果或取消"),
        ("tool.succeeded", "Success", "happy", "§39", "只触发一次，动作内表情优先"),
        ("tool.failed", "Confused", "concern", "§39", "不把 confused 写入共享枚举"),
        ("head.click", "HeadClick", None, "§46", "短点、长按、拖动互斥"),
        ("head.longPress", "HeadPat", None, "§46", "释放调用 ReturnToIdle 一次"),
        ("body.click", "BodyClick", None, "§46 + 制作补充", "明确 Body Collider"),
        ("drag.started", "Dragging", None, "§46", "抢占 HeadPat；窗口移动由宿主处理"),
        ("rest.entered", "Sleep", "sleepy", "§28 + 制作方案", "宿主决定进入睡眠"),
        ("greeting", "Wave", "happy", "用户设计方向", "低频，按一次实际事件触发"),
    ]

    assets = [
        ("项目总宪法.md", "normativeProjectManual"),
        ("Material/CH0069/Model/CH0069.glb", "immutableSourceModel"),
        ("Material/CH0069/Animations/animation_manifest.json", "sourceAnimationInventory"),
        ("Material/CHDDD/SourceMetadata/source_provenance.json", "sourceProvenance"),
        ("Material/CHDDD/CHDDD_actions_v2.blend", "latestAuthoringModel"),
        ("Material/CHDDD/Blender/body_action_map_v2.json", "latestAuthoringActionMap"),
        ("Material/CHDDD/Blender/action_face_profiles_v2.json", "latestAuthoringFaceProfiles"),
        ("Material/CHDDD/Blender/expression_map_v2.json", "latestAuthoringStaticEmotions"),
        ("Material/CHDDD/CHDDD_BodyControls_v2.py", "blenderBodyAndSpecialExpressionPreview"),
        ("Material/CHDDD/CHDDD_Controls.py", "blenderStandardEmotionPreview"),
        ("Material/CHDDD/CHDDD_Actions_v2.unitypackage", "verifiedFullUnityPackageRevision7"),
        ("Material/CHDDD/Unity/CHDDD_Actions_v2.fbx", "unity7Model"),
        ("Material/CHDDD/Unity/CHDDD_Actions_v2.prefab", "unity7Prefab"),
        ("Material/CHDDD/Unity/CHDDD_Actions_v2.controller", "unity7Animator"),
        ("Material/CHDDD/Unity/CHDDD_Face_v2.mask", "unity7FaceMaskExcludingHead"),
        ("Material/CHDDD/Unity/body_action_map_v2.json", "unity7ActionMap"),
        ("Material/CHDDD/Unity/action_face_profiles_v2.json", "unity7FaceProfiles"),
        ("Material/CHDDD/Unity/expression_map_v2.json", "unity7StaticEmotions"),
        ("Material/CHDDD/Unity/CHDDDBodyControllerV2.cs", "unityBodyPlaybackController"),
        ("Material/CHDDD/Unity/CHDDDActionFaceControllerV2.cs", "unityActionFaceController"),
        ("Material/CHDDD/Unity/CHDDDExpressionController.cs", "unityManualEmotionController"),
        ("Material/CHDDD/Textures/mika_squeeze_eyes_review.png", "squeeze15Texture"),
        ("Material/CHDDD/Textures/mika_squeeze_eyes_review.svg", "squeeze15VectorSource"),
        ("Material/CHDDD/Integration/UnityExpressionReference/ProjectSettings/ProjectVersion.txt", "expressionReferenceEditorVersion"),
        ("Material/CHDDD/Integration/UnityExpressionReference/Assets/Review/UnityExpressions.unity", "expressionReferenceScene"),
        ("Material/CHDDD/Integration/UnityExpressionReference/Assets/Review/Model/CHDDD_ExpressionReview.fbx", "expressionReferenceModelNotFullBodyDelivery"),
        (sample_path, "unitySpecialExpressionSamples"),
        ("Material/CHDDD/Integration/UnityExpressionReference/Assets/Review/Editor/CHDDDUnityExpressionReview.cs", "editorOnlyExpressionReviewDriver"),
        ("Material/CHDDD/Reviews/UnityExpressionV17/unity_validation.json", "specialExpressionRenderValidation"),
        ("Material/CHDDD/Reviews/UnityExpressionV17/Preview/index.html", "specialExpressionUnityPreview"),
        ("Material/CHDDD/Reviews/V9_SecondReview/Preview/index.html", "latestBodyRefinementBlenderPreview"),
        ("Material/CHDDD/Previews/ActionsV2/readability.html", "unity7BodyReadabilityPreview"),
        ("Material/CHDDD/body_v2_unity_validation.json", "unity7FullPlaybackValidation"),
        ("Material/CHDDD/body_v2_revision7_unity_local_validation.json", "unity7LocalFaceValidation"),
        ("Material/CHDDD/recovery_validation.json", "blender17RecoveryValidation"),
        ("Docs/Production/CHDDD/主程序资源接入清单.md", "handoffGuide"),
        ("Docs/Production/CHDDD/动作与表情详解.md", "actionAndExpressionDetails"),
    ]
    manifest = {
        "schemaVersion": 1, "characterId": "CHDDD", "sourceCharacter": "CH0069",
        "scope": "AgentPet-3D", "date": DATE, "pathBase": "workspace-root",
        "purpose": "交接资源清单；不是当前控制器可直接反序列化的 ActionMap，也不是已经接入主程序的声明",
        "freeze": {"experimentsEndedByUser": True, "artModifiedByConsolidation": False, "blenderRevision": 17,
                   "latestKnownBodyRefinementRevision": 8, "latestKnownSuccessFaceTimingRevision": 9,
                   "unityDeliveryRevision": 7, "fullySynchronized": False, "integratedInMainProgram": False,
                   "specialExpressionVersions": {"Squeeze": 15, "Omega": 17}, "sourceBlendSHA256": blend_sha},
        "counts": {"bodyClips": 18, "publicLogicalActions": 12, "loopClips": 7, "oneShotResponses": 5,
                   "enterExitClips": 6, "actionFaceProfiles": 18, "actionFaceBoneClips": 18,
                   "staticExpressionAssets": 9, "sharedEmotionEnum": 8, "specialExpressionAssets": 2,
                   "legacyNeutralTransitionExamples": 8, "unity7ExportedClips": 45, "sourceAnimatedClips": 33},
        "renderContract": {"rig": "Generic", "authoringFps": 30, "rootMotion": False, "weaponsVisible": False,
                           "unityVersion": "6000.3.7f1", "verifiedPipeline": "Built-in", "verifiedColorSpace": "Gamma",
                           "verifiedGraphicsAPI": "Direct3D11", "secondaryMotion": "baked-bone-animation",
                           "frameDurationFormula": "(lastFrame-firstFrame)/fps", "shapeDataRange": [0, 1],
                           "unityBlendShapeWeightRange": [0, 100], "timeUnit": "seconds"},
        "layers": [{"role": "BaseAnimation", "unityLayer": 0, "unityName": "Body", "ownsHead": True},
                   {"role": "Emotion", "unityLayer": 1, "unityName": "Emotion", "ownsHead": False,
                    "requiresActionFaceProfiles": True},
                   {"role": "ProceduralRig", "implemented": False, "purpose": "鼠标 LookAt 与微小修正"}],
        "sharedEmotionAllowlist": CORE_EMOTIONS,
        "actions": actions, "staticExpressions": emotions,
        "legacyNeutralTransitions": {"scope": "legacy-restoration-not-v2-45-clips", "defaultSeconds": 0.5,
                                     "presets": legacy["transitions"]["presets"]},
        "specialExpressions": special,
        "mouthAtlas": {"texturePath": "Material/CHDDD/Textures/Character_Mouth.png", "tiles": faces["mouthTiles"],
                       "slotCount": 8, "triangleTile": 200, "normalizeWeights": True,
                       "properties": ["_MouthBlendEnabled", "_MouthTiles0", "_MouthTiles1", "_MouthWeights0", "_MouthWeights1"]},
        "sourceReuse": {"Idle": "CH0069_Cafe_Idle", "Walk": "CH0069_Cafe_Walk",
                        "Dragging": {"oneBasedGLBIndex": 29, "clip": "CH0069_Formation_Pickup",
                                     "originalEyeScalingExcluded": True}},
        "eventBindings": [{"event": event, "actionId": action, "suggestedSharedEmotion": emotion,
                           "basis": basis, "note": note, "bindingImplementedInMainProgram": False}
                          for event, action, emotion, basis, note in bindings],
        "hostPolicy": {"implemented": False, "requiredPriorityRelations": [["Dragging", "HeadPat"]],
                       "suggestedPriority": "拖动 > 直接触摸 > 工具状态和结果 > 主动问候 / 待机",
                       "specialPerformanceScheduling": "事件驱动、低频、同一事件去重；不按每帧重播，不无限排队",
                       "unknownActionFallback": "Idle", "unknownEmotionFallback": "neutral",
                       "allowLLMBoneControl": False, "exposeInternalTransitionClipsToLLM": False,
                       "exposeUnintegratedSpecialExpressionsToLLM": False,
                       "fixedOneShotNextAction": "Idle", "restoreStillActiveHostStateAfterFeedback": True,
                       "resolvedActionAliasesAreHostResponsibility": True,
                       "requiresCancellationTokenForHostAsyncFlows": True},
        "controllerContract": {
            "body": {"type": "CHDDDBodyControllerV2", "methods": ["PlayAction(string,float=-1)", "ReturnToIdle()", "SetManualEmotion(string,float=.5)", "ResumeActionExpressions(float=.28)"],
                     "currentActionIsLogicalNotStage": True, "defaultBlendSeconds": 0.28, "exitBlendSeconds": 0.15,
                     "manualModePersistsAcrossPlayAction": True, "jsonDoesNotRebuildAnimatorTransitions": True},
            "face": {"type": "CHDDDActionFaceControllerV2", "sampledByBodyNormalizedTime": True,
                     "directFields": ["blink", "sad", "annoyed", "sleepy", "mouthWeights"],
                     "fieldsNotDirectlyWrittenByCurrentCSharp": ["gazeX", "gazeY", "browLift", "happy"], "eyeWideWrittenAs": 0,
                     "latestOptionalHappyField": {"shape": "Pet_BrowHappy_Review", "defaultValue": 0, "requiresCSharpSampleAndBlendExtension": True},
                     "newHappyBrowShapeSupportedByUnity7Controller": False},
            "manualEmotion": {"type": "CHDDDExpressionController", "defaultBlendSeconds": 0.5,
                              "easing": "smoothstep", "unknownEmotion": "neutral"},
            "specialExpressionRuntimeControllerImplemented": False,
        },
        "specialExpressionPorting": {
            "samplePath": sample_path, "sampleFps": 24, "sampleIncludesExactEndPoint": False,
            "explicitResetOnCompletionAndInterruption": True,
            "resetFieldsToZero": ["eyeSkinMask", "oldEyeMask", "squeeze", "omegaEye", "omegaMouth", "hideMouth"],
            "alwaysZeroShapes": ["Pet_TestSqueeze_Review"],
            "vertexMask": {"name": "UnityReviewMasks", "encoding": "LINEAR RGBA CORNER",
                           "R": "closed-eye skin", "G": "closed-lid seam cleanup", "B": "old eye inserts", "A": 1},
            "shaderNames": ["CHDDD/ReviewFace", "CHDDD/ReviewEyeMouth", "CHDDD/ReviewOverlay", "CHDDD/ReviewMouthAtlas"],
            "updateFaceNormalProperty": "_ReviewFaceNormal", "editorReviewDriverIsPlayerAPI": False,
            "referenceFBXContainsFull18BodyClips": False,
        },
        "mainProgram": {"projectPath": "GiftCompanion", "manifestPath": "GiftCompanion/Assets/Characters/MainCharacter/Config/character_manifest.json",
                        "currentAssetStatus": "awaiting-assets", "characterStillPlaceholder": True,
                        "targetManifestSection": "pet", "sharedEnumCodePath": "GiftCompanion/Assets/App/Core/Character/CharacterModels.cs",
                        "spineCompanionUsesSeparateManifestAndAdapter": True},
        "excludedFromUse": ["持枪 / 战斗来源动画", "Pet_TestSqueeze_Review 失败形变", "V8-V14 被后续替代的眼线实验", "旧 V1 作为当前默认交付", "将测试参考 FBX 替代全动作模型"],
        "assets": [file_ref(path, role) for path, role in assets],
    }

    checks = {}
    def check(name, result):
        checks[name] = bool(result)
        assert result, name
    names = {a["name"] for a in actions}
    check("18_body_entries_and_12_public_actions", len(actions) == 18 and sum(a["publicLogicalAction"] for a in actions) == 12)
    check("7_loops_5_responses_6_transitions", sum(a["loop"] for a in actions) == 7 and sum(a["stage"] == "one-shot" for a in actions) == 5 and len(internal_descriptions) == 6)
    check("18_face_profiles_match_body_ids", names == set(profiles) == set(u_profiles) == set(u_actions))
    check("known_face_differences_match_current_samples", {a["name"] for a in actions if a["runtime"]["hasKnownFaceChangesAfterUnity7"]} == {"Success", "HeadClick"})
    check("fps_frames_durations_and_profile_counts", all(
        abs((a["frames"][1] - a["frames"][0]) / body["fps"] - a["durationSeconds"]) < 1e-7
        and len(profiles[a["name"]]["samples"]) == a["frames"][1] - a["frames"][0] + 1
        and profiles[a["name"]]["loop"] == a["loop"] for a in actions))
    check("9_assets_8_shared_emotions", len(emotions) == 9 and {e["name"] for e in emotions if e["sharedEmotionEnum"]} == set(CORE_EMOTIONS))
    check("static_emotion_maps_match_unity7", expressions == unity_expressions)
    check("all_declared_unity7_clips_exist", all((ASSET / "Unity/AnimationsV2" / (name + ".anim")).is_file()
        for name in [a[key] for a in unity["actions"] for key in ("clip", "faceClip")] + [e["clip"] for e in unity_expressions]))
    check("unity7_has_exact_45_animation_files", len(list((ASSET / "Unity/AnimationsV2").glob("*.anim"))) == 45)
    check("all_mouth_samples_normalized_8_slots", all(len(s["mouthWeights"]) == 8 and abs(sum(s["mouthWeights"]) - 1) < 1e-5 and min(s["mouthWeights"]) >= 0
        for data in (faces, unity_faces) for p in data["profiles"] for s in p["samples"]))
    check("aliases_unambiguous", len([alias for a in actions for alias in a["aliases"]]) == len(set(alias for a in actions for alias in a["aliases"])))
    check("event_bindings_reference_public_actions", all(b["actionId"] in semantics and (b["suggestedSharedEmotion"] is None or b["suggestedSharedEmotion"] in CORE_EMOTIONS) for b in manifest["eventBindings"]))
    check("special_samples_58_frames_each_and_versions_match", all(e["sampleCount"] == 58 and next(s["revision"] for s in samples["expressions"] if s["name"] == e["id"]) == e["revision"] for e in special))
    check("special_exact_end_point_is_not_assumed", all(e["frames"][-1]["time"] < 2.4 for e in samples["expressions"]))
    check("source_blend_matches_unity_render_receipt", blend_sha == proof["sourceSHA256"])
    check("reference_project_copy_matches_each_source_file", bool(copied))
    check("formal_main_manifest_remains_awaiting_assets", read(ROOT / manifest["mainProgram"]["manifestPath"])["status"] == "awaiting-assets")

    # Validate local documentation links, catching old audit-directory spelling mistakes.
    docs = [ROOT / path for path, role in assets if role in ("handoffGuide", "actionAndExpressionDetails")]
    planned_outputs = {(OUT / name).resolve() for name in ("resource_manifest_v1.json", "handoff_validation.json")}
    for doc in docs:
        for target in re.findall(r"\]\(([^)]+)\)", doc.read_text(encoding="utf-8")):
            if "://" not in target and not target.startswith("#"):
                resolved = (doc.parent / target.split("#")[0].strip("<>")).resolve()
                assert resolved.exists() or resolved in planned_outputs, (doc, target)
    check("all_document_links_exist", True)
    write(OUT / "resource_manifest_v1.json", manifest)

    # End experiments in current status only; keep historical reviews and V7 delivery records intact.
    status = read(ASSET / "body_v2_current_delivery_status.json")
    status.update(expressionReview="experiments-ended-frozen-for-handoff", handoff="Integration/resource_manifest_v1.json", experimentEndDate=DATE)
    status["omegaExpressionReview"] = {"revision": 17, "status": "frozen-at-user-request", "explicitVisualApprovalRecorded": False}
    write(ASSET / "body_v2_current_delivery_status.json", status)
    progress = read(ASSET / "body_v2_progress.json")
    progress.update(status="experiments-ended-resource-handoff-ready", handoff="Integration/resource_manifest_v1.json", experimentEndDate=DATE)
    write(ASSET / "body_v2_progress.json", progress)
    legacy["blenderWorkingCopy"].update(pendingExpression=None, experimentStatus="ended-frozen-for-handoff",
                                      handoff="Integration/resource_manifest_v1.json", experimentEndDate=DATE,
                                      retainedSpecialExpressions={"Squeeze": 15, "Omega": 17})
    write(ASSET / "character_manifest.json", legacy)
    check("art_source_unchanged_after_consolidation", sha(ASSET / "CHDDD_actions_v2.blend") == blend_sha)
    write(OUT / "handoff_validation.json", {"date": DATE, "passed": all(checks.values()), "scope": "inventory-and-handoff-consistency-not-new-animation-runtime-test",
        "checks": checks, "assetReferenceCount": len(manifest["assets"]), "referenceFilesCopied": len(copied),
        "sourceBlendSHA256": blend_sha, "artSourceModified": False, "formalProjectModified": False,
        "fullUnitySynchronization": False, "specialExpressionRenderProof": proof})

    # Preserve the existing checksum list and refresh entries whose metadata was updated.
    checksum_file = ASSET / "body_v2_checksums.sha256"
    rows = []
    for row in checksum_file.read_text(encoding="utf-8-sig").splitlines():
        if not row.strip():
            continue
        _, name = row.split("  ", 1)
        path = ASSET / name
        assert path.is_file(), path
        rows.append(f"{sha(path)}  {name}")
    checksum_file.write_text("\n".join(rows) + "\n", encoding="utf-8")
    handoff_files = [OUT / "resource_manifest_v1.json", OUT / "handoff_validation.json", OUT / "unity_expression_reference_files.json", *docs]
    (OUT / "handoff_checksums.sha256").write_text("\n".join(f"{sha(p)}  {rel(p)}" for p in handoff_files) + "\n", encoding="utf-8")
    print(json.dumps({"passed": True, "actions": len(actions), "publicActions": len(semantics), "staticExpressions": len(emotions),
                      "specialExpressions": len(special), "checks": len(checks), "referenceFilesCopied": len(copied)}, ensure_ascii=True))


if __name__ == "__main__":
    main()
