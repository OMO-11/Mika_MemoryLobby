# CHDDD 恢复资产

## 当前交接入口（2026-10-11）

用户已结束表情实验，资源冻结留存。本轮整理不修改模型、动画或正式 GiftCompanion 工程。

- [主程序资源接入清单](../../Docs/Production/CHDDD/主程序资源接入清单.md)：可用版本、主宪法映射、调用约定和接入剩余事项。
- [动作与表情详解](../../Docs/Production/CHDDD/动作与表情详解.md)：18 个身体片段、动作专属表情、9 个基础预设及 `><` / `-ω-`。
- [机器可读清单](Integration/resource_manifest_v1.json)：Clip、时长、循环、别名、资源路径与哈希。
- [Unity 新表情参考工程](Integration/UnityExpressionReference/README.md)：已验证测试资产的持久副本；[实际 Unity 预览](Reviews/UnityExpressionV17/Preview/index.html)。

最新制作源 `CHDDD_actions_v2.blend` 为修订 17；完整 `CHDDD_Actions_v2.unitypackage` 仍为修订 7。`><` 冻结 V15、`-ω-` 冻结 V17，均完成独立 Unity 渲染验证，尚未并入正式运行时。主程序目前仍使用占位资源。下文为历史制作记录，“当前”“待审查”等字样属于各轮当时状态，以本节与 Integration 清单为当前交接入口。

<!-- omega-v17 -->
当前 Blender 制作版本：V17（2026-10-11）。V15 的 `>o<` 按用户指示暂时保留；第二个表情实验 `-ω-` 的眼线增厚至 1.55 倍，嘴线增厚至 1.65 倍，保留原来的轮廓与过渡。主文件停在第 37 帧。最新预览：`Reviews/V17_OmegaThickerReview/Preview/index.html`，等待人工审查。Unity 包仍为 V7。备份：`Checkpoints/20261011_before_omega_v17/`。
<!-- /omega-v17 -->


<!-- omega-v16 -->
当前 Blender 制作版本：V16（2026-10-11）。V15 的 `>o<` 按用户指示暂时保留；新增第二个表情实验 `-ω-`，双眼复用原眉毛几何，嘴型为对称圆弧。主文件停在第 37 帧。最新预览：`Reviews/V16_OmegaFirstReview/Preview/index.html`，等待人工审查。Unity 包仍为 V7。备份：`Checkpoints/20261011_before_omega_v16/`。
<!-- /omega-v16 -->


<!-- chibi-eye-v15 -->
当前 Blender 制作版本：V15（2026-10-11）。`><` 眼线略增粗并以原中心缩至 94%，保留左右对称与原有角度。主文件保存于第 37 帧；最新预览：`Reviews/V15_ChibiEyeReview/Preview/index.html`。当前等待人工审查，Unity 包仍为 V7。修改前备份：`Checkpoints/20261011_before_chibi_eye_v15/`。
<!-- /chibi-eye-v15 -->


<!-- chibi-eye-v14 -->
当前 Blender 制作版本：V14（2026-10-11）。`><` 改为 Q 版镜像矢量眼线，使用平滑透明边缘与对称平面，并清理旧闭眼亮边。主文件保存于第 37 帧；最新预览：`Reviews/V14_ChibiEyeReview/Preview/index.html`。当前等待人工审查，Unity 包仍为 V7。修改前备份：`Checkpoints/20261011_before_chibi_eye_v14/`。
<!-- /chibi-eye-v14 -->


<!-- eye-angle-v13 -->
当前 Blender 制作版本：V13（2026-10-11）。根据反馈将 `><` 夹角收小约四分之一，继续使用平面贴图。主文件已保存，动作与时序保持 V12，最新预览：`Reviews/V13_EyeAngleReview/Preview/index.html`。Unity 包仍为 V7，待同步。本轮前备份：`Checkpoints/20261011_before_eye_angle_v13/`。
<!-- /eye-angle-v13 -->


<!-- eye-angle-v12 -->
当前 Blender 制作版本：V12（2026-10-11）。按用户橙色标线调整 `><` 上下眼线夹角，继续使用平面贴图。主文件已保存，动作与时序保持 V11，最新预览：`Reviews/V12_EyeAngleReview/Preview/index.html`。Unity 包仍为 V7，待同步。本轮前备份：`Checkpoints/20261011_before_eye_angle_v12/`。
<!-- /eye-angle-v12 -->


<!-- recovery-v11 -->
## 当前工作文件（2026-10-11）

`CHDDD_actions_v2.blend` 已找回星期五后续审查稿中的完整动作，当前 Blender 制作版本为 V11；18 个身体动作及面部过程与最后保存的 V10 相同。`>o<` 平面贴图为本轮待审查试验，打开主文件停在第 37 帧。最新页面：`Reviews/V11_FlatEyeRecovery/Preview/index.html`。

Blender 控制脚本读取 `Blender/` 下的动作配置。Unity 文件夹及 `CHDDD_Actions_v2.unitypackage` 保留 V7，尚未导出本轮修改。下文的 V7 Unity 验证报告属于历史交付，不是 V11 已通过 Unity 的证明。详情及备份见 `恢复记录_20261011.md`。

侧栏未出现时，运行内嵌 `CHDDD_Controls.py` 和 `CHDDD_BodyControls_v2.py`。主文件已经打包眼贴图；本次没有修改 Blender 的脚本安全选项。
<!-- /recovery-v11 -->


本目录是 CH0069 的恢复与制作副本，原始 CH0069 素材保持不变。制作日期：2026-10-07。

已恢复光环几何、UV 与发光材质，并将光环绑定到头部；重建 Blender Eevee 卡通材质；分离嘴部材质并恢复 8×8 嘴型图集切换；新增 9 套基础情绪预设、5 个眼睑与眉毛形变，保留全部 33 个原动画。

## Blender 使用

打开 `CHDDD.blend`，活动场景为 `CHDDD_Restoration`，30 FPS，贴图已打包。侧栏 **CHDDD → CHDDD Expressions** 的表情名按钮用于定格，旁边的「播放过渡」按钮用于预览一次过渡，并停在目标表情。默认从 Neutral 开始，时长 0.5 秒；取消「从 Neutral 开始」可从当前表情接续。

重启 Blender 后，如侧栏没有出现，请在 Text Editor 中选择文件内的 `CHDDD_Controls.py` 并运行。它同步切换骨骼动作、嘴型和眼部形变。单独在 Action 下拉框中选择动作不会同步嘴部材质和形变，因此表情预览优先使用侧栏按钮。

原始动作在 NLA 中保留，新情绪动作以 `Pet_Emotion_` 开头。为避免多个动作叠加，编辑文件默认把 NLA 轨道静音，并显示 Neutral 表情。新表情的 `Pet_Blink` 驱动上下眼睑、眼眶皮肤与睫毛闭合，保留眼球和瞳孔原尺寸；眉形使用 `Pet_BrowSad`、`Pet_BrowAnnoyed`、`Pet_BrowSleepy`。`Pet_EyeWide` 暂留兼容接口，权重为零。保留的原始动画仍可能包含原素材自身的夸张眼部动作。

## Unity 使用

优先导入 `CHDDD.unitypackage`，再将 `Assets/CHDDD/Unity/CHDDD.prefab` 拖入场景。资源包包含 FBX、贴图、材质、Shader、Generic Avatar、Animator Controller、面部 Avatar Mask 和表情控制脚本。

当前 Shader 面向 **Unity Built-in Render Pipeline**，不是 URP Shader。若正式工程采用 URP，需要转换 Shader；Blender 的 Shader to RGB 节点也不能直接作为 Unity 材质使用。当前重建是基于现有贴图与材质参数的卡通近似效果，未宣称与原游戏渲染逐像素一致。

调用 `CHDDDExpressionController.SetEmotion("happy")`，默认以 0.5 秒过渡切换表情；也可调用 `SetEmotion("happy", 0.8f)` 指定时长。`SetEmotionImmediate("happy")` 用于立即定格。控制器以 Smoothstep 插值眼睑与眉形，混合嘴型图集，并使用固定秒数交叉淡入淡出切换骨骼姿态。切换途中再次调用时，从当前形变和嘴型混合权重接续；未知表情回退 Neutral。原事件可接入 `SetMouthTile(int)`，本次没有自动恢复原战斗特效脚本与粒子特效。

资源包提供两个 Animator 层：Base（原 Idle / Walk）和 Emotion（九种基础表情）。Emotion 使用面部与头部蒙版，完整骨骼继续按 Generic Rig 使用。LookAt、触摸与拖拽由项目运行时接入。

## 表情清单

| 名称 | Clip | 嘴型编号 | 闭眼权重 | 睁眼形变权重 |
| --- | --- | --- | --- | --- |
| neutral | Pet_Emotion_Neutral | 300 | 0% | 0% |
| happy | Pet_Emotion_Happy | 706 | 10% | 0% |
| shy | Pet_Emotion_Shy | 705 | 15% | 0% |
| concern | Pet_Emotion_Concern | 604 | 10% | 0% |
| sad | Pet_Emotion_Sad | 604 | 18% | 0% |
| surprised | Pet_Emotion_Surprised | 605 | 0% | 0% |
| annoyed | Pet_Emotion_Annoyed | 305 | 42% | 0% |
| sleepy | Pet_Emotion_Sleepy | 305 | 100% | 0% |
| confused | Pet_Emotion_Confused | 401 | 8% | 0% |

九个 Clip 是 2 秒的基础表情保持动作，用于情绪层与过渡，不是九个完整全身演出动作。手册中的 HeadPat、Wave、Thinking、Working、Success、Confused、Dragging、Sleep 全身动作仍需后续制作；不能把情绪 Confused 或 Sleepy 当作这些全身动作已完成。

## 验证与文件

Blender 5.2.2 LTS 已逐套渲染预览，见 `Previews/expressions_contact_sheet.png` 及各表情 PNG。Unity 导入和运行时检查的当前结果见 `restoration_validation.json`，包括 Generic Avatar、33 个原动画、9 个情绪动画、光环层级、5 个 BlendShape、Shader 编译、嘴型、眉形切换与眼球尺寸保持。

Unity 无界面测试验证导入、层级和接口，不代替正式桌宠场景的最终画面及交互验收。具体结果见 `restoration_validation.json`。

- `CHDDD.blend`：继续编辑的主文件；`.blend1` 是 Blender 自动备份。
- `CHDDD.unitypackage`：包含完整 Unity 资源引用的导入包。
- `Unity/CHDDD.fbx`：50 个动画（33 个原动画、9 个情绪定格、8 个骨骼过渡）与恢复后的模型。
- `Unity/CHDDD.prefab`、`CHDDD.controller`、`CHDDD_Face.mask`：已生成的 Unity 配置。
- `Unity/Materials/`：Unity 材质。
- `Unity/expression_map.json`：运行时表情映射。
- `character_manifest.json`：制作参数、表情骨骼姿态、嘴型与形变映射。
- `Textures/`：制作副本贴图。
- `SourceMetadata/`：原材质参数、光环几何记录与来源记录。
- `Previews/`：最终预览及制作过程中的诊断图。

原资源来源和权利信息沿用 CH0069 的记录，见 `SourceMetadata/source_provenance.json`。

## 眼部修复（2026-10-07）

sad 使用轻度垂眼、眉头上扬与眉尾下垂；annoyed 使用眼睑半闭与眉头下压；sleepy 使用完整闭眼与放松的下垂眉。形变改变原有上下眼睑皮肤、睫毛和眉毛网格，不再压缩眼球贴片。68 个眼球贴片顶点在全部形变中保持原坐标。眉毛部分会被原发型的刘海遮挡。

近景预览见 `Previews/eye_fix_contact_sheet.png`（三种表情、正面与左右 20°）和 `Previews/blink_transition_strip.png`（五档闭眼过渡）。本轮修复覆盖新情绪预设。修复前主文件另存为 `CHDDD_before_eye_fix.blend`。

## 柔和眉形与表情过渡（2026-10-07）

闭眼轮廓已细分为连续弧线；sleepy 眉形更平缓，眉毛条带增宽约 20%，加入 0.4 毫米浅厚度与柔和明暗。仅细分眼睑、睫毛和眉毛区域，保留眼球原坐标、UV 和完整骨架。刘海仍会遮住部分眉毛。

Blender 内保存八套 Neutral 到各目标表情的过渡。每套包含骨骼、形变和嘴部节点三个配套 Action，默认帧 1–16，对应 30 FPS 下 0.5 秒。单独切换骨骼 Action 无法同步另外两套数据，使用侧栏完整预览。

FBX 中八个 `Pet_Transition_NeutralTo*` 是骨骼过渡 Clip，按非循环导入；眼睑、眉毛与嘴型的运行时插值由 `CHDDDExpressionController` 同步执行。桌宠调用 `SetEmotion` 即可使用完整过渡，无需手动播放这些骨骼 Clip。默认使用真实时间，暂停游戏时间缩放时仍可响应表情。

嘴型采用现有图集的七个独立单元按权重渐变，复用 MaterialPropertyBlock，避免改写共享材质；频繁切换时保留当前混合结果。暂时同时看到淡出的旧嘴型和淡入的新嘴型属于图集渐变，并非生成新的嘴部几何。

预览见 `Previews/neutral_to_sleepy.gif`、`Previews/neutral_transition_contact_sheet.png`；过渡起终点和中途接续验证见 `transition_validation.json`。本轮修改前备份为 `CHDDD_before_curve_transition.blend`。这些过渡用于情绪层，项目所需的完整全身演出动作仍待制作。


## 第一版身体动作（2026-10-08 交付）

第一版动作请打开 `CHDDD_actions_v1.blend`，共 18 个身体 Clip，隐藏武器并抬高光环。Unity 6000.3.7f1 独立工程验证已通过，本版请导入 `CHDDD_Actions_v1.unitypackage`，使用 `Assets/CHDDD/Unity/CHDDD_Actions_v1.prefab`。全部动作预览见 [动作审查页](Previews/ActionsV1/index.html)。原 `CHDDD.blend` 和旧表情资源包保留；正文此前“全身动作待制作”描述对应旧恢复版本。详细记录、调用方法和测试范围见 [动作第一版进度](动作第一版进度.md)。

## 动作优化 V2（2026-10-08）

当前优化版请打开 `CHDDD_actions_v2.blend`，Unity 导入 `CHDDD_Actions_v2.unitypackage`，使用本版同名预制体。Dragging 改用原 GLB 第 29 条 `Formation_Pickup`；特殊动作增加发衣跟随、动作节奏与独立表情过程。全部 18 个动作的 24 FPS 连续预览见 [V2 审查页](Previews/ActionsV2/index.html)，支持慢放、暂停和 V1 对比。制作记录、调用方式与验证范围见 [动作优化第二版](动作优化第二版.md)。V1 和原始素材保留；官方观感的最终判断仍需人工审查。

暂停任务恢复后完成 V2 修订 3：调整动作快慢、分层发衣延迟与肩部布料牵动，修复循环装饰的角速度接缝。修订前完整模型、资源包和制作脚本保存在 `Checkpoints/V2_before_resume/`。

设计方向确认后已制作 V2 修订 4：按用户选择的“接近游戏原版，互动更活泼”，加强挥手、庆祝、疑惑及两种点击反馈的手势和身体反应，同步独立表情与发衣跟随；缩短点击淡入以保留起势。当前预览页可对比修订 3，上一轮完整交付保存在 `Checkpoints/V2_revision3_before_lively/`。详见 V2 制作记录中的修订 4。

**V2 修订 5（2026-10-09）**：按小尺寸可读性重制五种反馈的全身姿态、停留与缓冲，并强化实际发衣网格的跟随和重力回落。优先查看 [小尺寸与轮廓同步审查](Previews/ActionsV2/readability.html)，再看全部动作页；三级设计区分低强度日常、情绪响应、低频特殊演出。详细节奏、验证与频率调度边界见 V2 制作记录末尾。修订 4 完整备份在 `Checkpoints/V2_revision4_before_readability/`。


**V2 修订 6（2026-10-09）**：Confused、Success、HeadClick、BodyClick 的整体姿态已获用户审查通过，本轮集中回修子动作衔接、重心与头手的先后、手腕转向及发衣收势。保持关键姿态、时长和全部表情过程，其他动作曲线保留。打开 [新旧 Unity 连续帧对比](Previews/ActionsV2/readability.html)，支持慢放与逐帧；源模型、Unity 包路径沿用 V2。修订 5 完整备份在 `Checkpoints/V2_revision5_before_flow/`，详细记录见 [动作优化第二版](动作优化第二版.md) 末尾。


**当前为 V2 修订 7（2026-10-09）**：Confused 改为掌心向内挠头，Success 双手举高并闭眼抬眉、局部改善肩衣穿插，Wave 一边挥手一边扇翅且连续收手，HeadClick 闭眼三角嘴受惊后佯装生气，BodyClick 尾段轻抖翅膀。总时长和已认可的身体幅度保留。打开 [修订7与修订6的Unity连续对比](Previews/ActionsV2/readability.html)，并使用本轮完整 Unity 资源包；三角嘴需要随包更新的八槽动作表情控制器。修订 6 备份在 `Checkpoints/V2_revision6_before_local/`，详见 V2 制作记录末尾。
