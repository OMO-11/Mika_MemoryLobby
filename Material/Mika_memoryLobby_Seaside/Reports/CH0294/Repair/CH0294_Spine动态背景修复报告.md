# CH0294 Spine 动态背景修复报告

日期：2026-10-17（Asia/Shanghai）  
范围：Spine 角色、骨架内的星空/海面/浪层/光效、Unity 展示与动画播放。按用户要求不处理语音，不补做独立漂浮粒子。

## 1. 修复结果

**现有 CH0294 Spine 动态大厅已经接入可运行场景。已修复透明混合、颜色空间、Canvas 缺失、多页图集渲染和背景取景问题，并验证入场、待机、对话及交互轨道能正常返回待机。**

- 场景已保存并保持在 Unity 中打开，当前已退出 Play Mode；点击 Play 即可运行。
- 四页图集均正常生成 CanvasRenderer，不再只使用单页 UI 渲染路径。
- 原先明显的透明白边、白块和画面泛白问题已修正。
- 星空、海面和五层浪保留为真实 Spine 动画，未用静态背景图替换。
- 参考图比例、16:9、21:9 和竖屏均采用等比覆盖与裁切；抽样截图未出现背景空洞。
- 完整 12 秒待机周期输出 120 帧，所有采样帧的画幅覆盖检查均通过。
- Play Mode 内 11 组定步长测试共 11,501 帧全部通过，并另确认实际自动播放约 58 秒。
- 六个骨架/图集/贴图文件内容均未改写，与最初校验值一致。

本次完成的是现有 Spine 动态背景修复。原游戏独立粒子、精确后处理与镜头调度没有源数据可核对，不将其表述为完全复刻。

## 2. 项目、场景和交付位置

| 项目 | 路径 |
| --- | --- |
| 实际修复的 Unity 项目 | `W:\Machine\VSCode\Mika_Gift\Material\Mika_MemoryLobby\MemoryLobby_Seaside` |
| 修复场景 | `Assets/Seaside/Scenes/CH0294_Seaside_Repaired.unity` |
| 用户提供的参考图 | `W:\Machine\VSCode\Mika_Gift\Material\Mika_memoryLobby_Seaside\1792225055763.jpeg` |
| 报告与验证文件 | `W:\Machine\VSCode\Mika_Gift\Material\Mika_memoryLobby_Seaside\Reports\CH0294\Repair` |
| 原始 Spine 资源 | `W:\Machine\VSCode\Mika_Gift\Material\Mika_MemoryLobby_Spine\spine\CH0294_home` |

用户指定的 `Mika_memoryLobby_Seaside` 用于参考图和报告；MCP 连接的 Unity 项目仍是上表所列的嵌套目录。没有另外复制一套 Unity 项目。

环境：Unity 6000.3.7f1、URP 17.3.0、Spine runtime 4.2.120、Linear 颜色空间。没有改动 Spine runtime 或其 shader 源码，也没有改动项目全局颜色空间。

## 3. 问题原因与实际改动

### 3.1 透明白边、白块和异常加亮

源 PNG 使用直通 Alpha，但原来的四份 `Spine/Skeleton` 材质没有启用对应输入转换。透明像素中保存的 RGB 颜色被带入画面，产生明显白块和边缘污染。

处理：

- 四份图集材质统一设置 `_StraightAlphaInput=1` 并启用 `_STRAIGHT_ALPHA_INPUT`。
- 新增专用于该大厅的 UI 材质 `CH0294_SkeletonGraphic_Straight.mat`，使用项目已有 `Spine/SkeletonGraphic` shader，并启用相同转换。
- UI mesh 使用 PMA vertex colors；本场景不使用 CanvasGroup 淡入淡出，因此材质和 mesh 两侧的 CanvasGroup compatible 均关闭，保持一致。
- 保留骨架内 Additive 插槽，避免为了消除白块而删除原有光带或光效。

### 3.2 透明修正后仍然泛白

实际运行场景使用 Linear 颜色空间，而四张彩色 PNG 原先被作为非 sRGB 纹理导入。实际 Game View/相机输出仍明显偏亮，仅启用 Alpha 转换不能完全解决。

处理：四张图集贴图的 TextureImporter `sRGBTexture` 改为 true。没有重绘、压缩或改写 PNG，也没有启用 Alpha Is Transparency、增加 mipmap 或更换纹理分辨率。

本次同时对比了临时预览、实际场景 RenderTexture 和运行中的相机截图。预览渲染目标的颜色转换与实际输出不同，因此最终以实际场景输出为依据；上一份调查中的临时材质预览只用于定位 Alpha 问题，不能单独作为最终颜色设置依据。

对比：

- [颜色修复前的运行截图](gameview_before_color_fix.png)
- [颜色修复后的运行截图](gameview_after_color_fix.png)

### 3.3 缺少 Canvas 和多纹理支持

原 `New SkeletonGraphic` 是场景根对象，没有 Canvas 祖先；同时 `allowMultipleCanvasRenderers=false`，与四页图集不匹配。

处理后的主要层级：

```text
Main Camera
Directional Light
Global Volume（禁用默认样例后处理）
Seaside Canvas（Screen Space - Camera，绑定 Main Camera）
└── Seaside Viewport（RectMask2D，全屏）
    └── CH0294 Seaside Lobby
        ├── SkeletonGraphic
        ├── SeasideLobbyController
        └── 四个运行时/编辑器生成的 CanvasRenderer 子对象
```

启用 Multiple CanvasRenderers，已实测生成四个渲染器。采用 CanvasScaler，参考分辨率为原图的 1652 × 1080。大厅图形不拦截 UI raycast。

原 SkeletonGraphic 上没有实际用途的 SkeletonRootMotion 与 SkeletonUtility 在修复场景内移除，避免 root motion 改变固定大厅构图；原场景快照保留这些组件。

### 3.4 背景边缘露黑与构图不匹配

骨架 setup 包围盒并不是完整矩形背景。直接缩放到整个包围盒会暴露星空贴片外沿、海浪边缘和未覆盖区域。

处理：沿用项目原有 `SeasideLobbyController`，把取景范围调整为：

```text
x = -15.5
y = -1.27
width = 32
height = 20.92
portraitFocusX = 3.5
```

这些数值使用 SkeletonDataAsset scale=0.01 后的骨架单位。控制器根据画幅进行等比 cover，保留上方构图，窄屏向角色焦点裁切，不拉伸图像。

新增加载首帧的 Canvas 布局同步，以及 `Canvas.preWillRenderCanvases` 时的尺寸复核，减少初始化和改变窗口尺寸时出现旧布局的机会。保留原有 LateUpdate 尺寸检查。

SkeletonGraphic 的位置/旋转物理继承系数设为 0，使窗口适配造成的 UI 坐标变化不再被当成外力注入发丝等物理约束。骨架本身的 37 个 Physics 约束仍然保留并参与动画。

### 3.5 动画分层与返回待机

没有把 A/M 当作两种配音版本。实际沿用并接入现有控制器的分层播放方式：

| 轨道 | 用途 |
| --- | --- |
| 0 | `Idle_01` 循环；入场时使用 `Start_Idle_01`，结束后自动接回 Idle |
| 1 | Talk/Pat/LookEnd 等 M 动作轨 |
| 2 | 对应 A 附件/口型/表情轨 |

对话及交互结束后，上层轨道通过 empty animation 混出。该逻辑原已存在于控制脚本，本次将它接入实际场景并逐项验证，没有声称它是本次新增的系统。

验证中五组对话、抚摸、抚摸结束、视线结束、对话中断替换均能清除轨道 1/2，附件恢复到待机基线。语音绑定保持空，没有新增音频播放或语音资源需求。

## 4. 验证结果

### 4.1 Play Mode 中的播放与状态恢复

以实际场景的 SkeletonGraphic 和控制器进行 60 Hz 定步长测试：

| 场景 | 检查结果 |
| --- | --- |
| Idle 连续三个周期，36 秒 | 数值有效；星空/海面/浪层持续变化 |
| Talk 01–05，各自播放完整时长并等待混出 | 五组均回到 Idle，上层轨道清除，附件残留 0 |
| Start_Idle → Idle | 自动衔接成功，附件残留 0 |
| Pat / PatEnd / LookEnd | 三组均正常混出并恢复待机 |
| 对话 1 播放中切到对话 4，再回待机 | 没有残留轨道或附件 |

合计 **11 组、11,501 个采样帧**。所有采样帧的骨骼变换和附件世界顶点均未出现 NaN / Infinity。

另在未暂停的 Play Mode 中记录两次状态，实际游戏时间与 Idle 轨道时间均前进约 **58.35 秒**，确认自动更新在运行。本次没有进行 FPS 基准、长时间耐久或 Windows 打包测试。

### 4.2 动态背景确实在运动

在 Idle 测试中，下列九个背景插槽的世界顶点均发生变化：

```text
BG_03, BG_fx03, BG_02, BG_02_sha,
BG_Wave1, BG_Wave2, BG_Wave3, BG_Wave4, BG_Wave5
```

修复后 0.5 秒与 6 秒的同尺寸帧有 1,223,617 / 1,784,160 个像素不同。该指标只用于确认渲染随动画变化，不用作与原游戏的相似度分数。

### 4.3 画幅与覆盖

| 验证内容 | 分辨率 / 时刻 | 结果 |
| --- | --- | --- |
| 原参考比例 Idle | 1652 × 1080；0.5s、6s、11.9s | 三张均无未覆盖像素 |
| 常见横屏 | 1920 × 1080；Idle 6s | 无未覆盖像素 |
| 宽屏 | 2520 × 1080；Idle 6s | 无未覆盖像素 |
| 竖屏 | 1080 × 1920；Idle 6s | 无未覆盖像素，角色焦点裁切 |
| 分层对话 | 1652 × 1080；Talk 01 @ 2s | 无未覆盖像素 |
| 入场与衔接 | 1652 × 1080；0s、10s、21.7s | 三张均无未覆盖像素，最终进入 Idle |
| 完整 Idle 周期 | 826 × 540；0–11.9s，每 0.1s 一张 | 120/120 帧均无未覆盖像素 |

覆盖检查使用透明相机清屏，对实际渲染结果进行 Alpha 检查（阈值 250/255），不是用不透明底色遮住缺口。高分辨率截图共 10 张；完整周期使用额外 120 张较小帧。未对所有动画、所有时刻、所有极端画幅承诺穷举覆盖。

### 4.4 文件与控制台

- 六个源文件 SHA-256 与上一轮调查完全一致。
- Unity 导入的 `.skel.bytes`、`.atlas.txt` 和四张 PNG 仍与源文件逐字节一致。
- 修改集中在四份材质、四份贴图 `.meta`、现有控制器脚本及 Build Settings。
- 调查时已确认的 31 个动画、441 根骨骼和 215 个插槽没有被删减或重导出。
- 本轮最后查询 Unity Console 的 Error/Warning 返回 0 条；此前版本 API 弃用警告的源码并未纳入本次改造，不宣称已经清除了全部弃用代码。
- 最终修复场景已经保存，当前非 Play Mode。

## 5. 如何使用

1. 打开上表中的实际 Unity 项目。
2. 打开 `Assets/Seaside/Scenes/CH0294_Seaside_Repaired.unity`；当前编辑器已停留在这个场景。
3. 点击 Play，默认播放 `Idle_01`，星空、海浪和角色一起运动。
4. 运行时选中 `CH0294 Seaside Lobby`，在 `SeasideLobbyController` 的组件菜单中可调用：`Playback/Play Entrance`、`Playback/Play Dialogue 1–5`、`Playback/Play Pat`、`Playback/End Pat`、`Playback/End Look`、`Playback/Reset to Idle`。

Build Settings 已把修复场景设为首个启用场景，并禁用原 SampleScene 的构建入口；原场景文件保留。如果已有自己的入口场景，应按项目需要重新排列场景列表。

静态预览：

![修复后 Idle @ 0.5s](repaired_idle_t0.5_reference.png)

[查看完整 12 秒动态预览 GIF](CH0294_修复后动态预览.gif) · [查看原游戏参考图](../../../1792225055763.jpeg)

## 6. 修改清单与回退

新增：

- `Assets/Seaside/Scenes/CH0294_Seaside_Repaired.unity` 及其 meta。
- `Assets/Seaside/Materials/CH0294_SkeletonGraphic_Straight.mat` 及其 meta。
- `Assets/Seaside/Backups/CH0294_RepairSession_Before.unity`：修复前活动场景的保存副本，包含当时尚未保存到原 SampleScene 文件的状态。
- 本报告目录中的帧、GIF、验证 JSON 和复现方法体。

修改：

- `Assets/New Folder/CH0294_home_CH0294_home*.mat`，共四份：直通 Alpha 输入。
- `Assets/New Folder/CH0294_home*.png.meta`，共四份：sRGB 导入标记。
- `Assets/Seaside/Scripts/SeasideLobbyController.cs`：参考取景和 Canvas 布局同步。
- `ProjectSettings/EditorBuildSettings.asset`：默认入口改为修复场景。

本报告目录 `Backup/Assets` 和 `Backup/ProjectSettings` 保存了上述既有文件的修改前内容。详细路径、备份路径和修改前后 SHA-256 见 [verification.json](verification.json)。原 SampleScene 磁盘文件没有被修复场景覆盖。

如需回退：退出 Play Mode，按 `verification.json` 的 `backupPath → path` 恢复对应材质、贴图 meta、控制器和 Build Settings，再让 Unity 刷新。需要恢复当时编辑器中的未保存场景内容时，打开 `Assets/Seaside/Backups/CH0294_RepairSession_Before.unity`。场景副本引用同一套资源，因此要获得原视觉状态，需要同时恢复材质和导入设置。

## 7. 与原游戏参考仍存在的差异

参考图用于校准上半身构图和星空/海面取景，现有 Spine 内容已完整保留并运行。以下项目没有足够原始数据确认，本次不伪造或补绘：

- 原图中的独立漂浮白色粒子：当前骨架外的 Effect/Prefab/Timeline 未提供；用户已确认本次不补充，先完成现有动态背景。
- 原游戏的精确 Bloom、颜色分级、粒子亮度和镜头参数：本次保留无额外调色的可靠基线，未以局部截图逆推一套未经核实的参数。
- 参考图的确切动画时刻与表演轨道未知，眼睑、姿态和发丝相位可能不同，不能据单张图证明逐像素一致。

**完成本次现有 Spine 动态背景修复不需要补充素材。** 语音和独立粒子均不构成本次结果的阻塞。

## 8. 验证文件

| 文件 | 内容 |
| --- | --- |
| [verification.json](verification.json) | 最终综合验证、源文件完整性、修改文件哈希 |
| [final_unity_state.json](final_unity_state.json) | 保存后的场景、四页材质、贴图导入和 Canvas 状态 |
| [console_final.json](console_final.json) | 最终 Unity Error/Warning 查询结果 |
| [playback_validation.json](playback_validation.json) | 11 组播放、混出、附件恢复和背景运动记录 |
| [scene_capture.json](scene_capture.json) | 10 张场景截图与画幅覆盖统计 |
| [loop_coverage.json](loop_coverage.json) | 完整 Idle 周期 120 帧覆盖统计 |
| [realtime_before.json](realtime_before.json) / [realtime_after.json](realtime_after.json) | 实际自动播放前后时间与轨道状态 |
| [RepairScene.cs.txt](RepairScene.cs.txt) | 场景与材质接入的 Unity MCP 方法体 |
| [CaptureScene.cs.txt](CaptureScene.cs.txt) / [CaptureIdleLoop.cs.txt](CaptureIdleLoop.cs.txt) | 截图与完整周期采样方法体 |
| [ValidatePlayback.cs.txt](ValidatePlayback.cs.txt) | Play Mode 定步长播放验证方法体 |
| [VerifyRepair.py](VerifyRepair.py) | 源文件、验证结果及输出文件一致性复核脚本 |

部分 MCP 的运行截图在编辑器未刷新画面时使用了 Main Camera 渲染回退；本场景采用 Screen Space - Camera，所以 Canvas 包含在该路径中。精确覆盖统计采用实际场景相机与 sRGB RenderTexture 的离屏渲染，不把它描述为 Windows 桌面截屏。

动态 GIF 是实际 Unity 渲染帧的媒体转换，826 × 540、10 fps、12 秒，约 13.57 MiB。它用于查看动作，颜色会受 GIF 调色板限制；静态 PNG 是画质核对依据。
