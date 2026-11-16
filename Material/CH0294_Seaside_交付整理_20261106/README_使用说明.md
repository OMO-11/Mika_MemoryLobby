# CH0294 海边记忆大厅：可复用资源交付

整理日期：2026-11-06。此目录是当前工作区已修复内容的独立副本，源工程与原始素材保持原位。

## 从这里开始

推荐通过 Unity Hub 添加本目录下的 `01_UnityProject`，使用 **Unity 6000.3.7f1** 打开。
打开 `Assets/Seaside/Scenes/CH0294_Seaside_Repaired.unity`，按 Play。
首次打开需要 Unity 解析 Packages 并重新生成 Library，可能需要访问 Unity 包仓库。

## 目录说明

| 目录 | 内容与用途 |
| --- | --- |
| `01_UnityProject` | 完整的 Assets、Packages、ProjectSettings，作为可打开的 Unity 工程使用 |
| `01_UnityProject/Assets/CH0294` | 修复后的导入资源：骨骼、图集、四张贴图、材质、Atlas 与 SkeletonData 资产及其 .meta |
| `01_UnityProject/Assets/Seaside` | 当前修复场景、UI 材质及三个播放／互动控制脚本 |
| `01_UnityProject/Assets/Spine` | 原工程附带的 Spine 4.2 运行时与编辑器支持，保留原许可声明 |
| `01_UnityProject/Assets/Settings` | 原工程渲染配置；移动场景到其他工程时也要检查这些依赖 |
| `02_SpineOriginal` | 原始 .skel、.atlas 与四张 PNG，共六个文件，适合交给其他支持 Spine 4.2 的环境继续适配 |
| `03_Previews` | 最新入场、白光、抚摸与表情预览；构图和表情截图 |
| `04_Reports` | 分阶段调查／修复报告及当时的验证数据 |

## 已具备的效果

- 正确的贴图颜色与透明混合、动态海面及星空背景。
- 修复后的纵向视野与宽屏适配。
- 自动入场特写；最终方案为 1.25 倍速，并在原时间轴 16.6667 秒侧脸转正面的位置，以白光接入待机，取消末尾约五秒正面停留。历史验收总时长约 14.07 秒，10 FPS 验收约 14.20 秒。
- 额头至头顶区域抚摸判定、反馈保持、松手收尾。
- 轻微看向玩家、闭眼微笑和思考表情。

## 复用到已有 Unity 工程

建议先直接打开交付工程确认效果，再迁移。原工程资源目录 `Assets/New Folder` 在交付副本中整理为 `Assets/CH0294`，文件内容及所有 .meta GUID 保留。
迁移时保留 `.meta`，并一并带入 `Assets/CH0294`、`Assets/Seaside`、Spine 4.2 支持与相关 URP Settings；也可以在打开交付工程后通过 Unity 的 Export Package（Include dependencies）导出修复场景。
已有工程若安装 Spine，先核对版本与 GUID，避免重复导入两套运行时。目标工程还需支持 URP 17.3.0、Unity UI 与 Input System 1.18.0，并维持原工程的 Linear 色彩空间和输入设置。只复制原始六个 Spine 文件不会自动包含修复材质与互动逻辑。

## 整理范围与验证边界

本次只整理复制，不修改已完成的动画和互动行为。交付副本移除了不参与播放的 Unity MCP 包依赖，保留其余包及版本锁定；没有复制 Library、Temp、Logs、旧修复场景备份和数百张过程帧图。
历史报告中的绝对路径及截图引用保留为原始记录，使用本目录的工程和预览为准。抚摸／表情 GIF 是前一阶段的互动展示，最新入场以 MP4 和白光 GIF 为准。

本次完成了复制文件 SHA-256 对照、GUID 引用静态核对及交付文件清单生成；本次会话没有可用 Unity MCP，未重新启动整理后的工程进行播放测试。历史 Play 模式验收数据随报告交付，代表先前验证结果。

复制核对没有发现源文件内容差异或工程内资源引用遗漏。静态扫描另记录了 12 个与源工程一致、在本地包缓存中未解析的 GUID，集中于原有默认 Volume 配置、渲染器调试资源和教程图标；这些历史配置保留原样，仍需由 Unity 重新导入确认，详情见 `DELIVERY_VALIDATION.json`。

语音未补齐；原游戏独立漂浮光点所需 Effect/Prefab/Timeline 仍未提供。本目录保留现有 Spine 动态背景，不包含额外补绘或音频资源。

`DELIVERY_VALIDATION.json` 记录本次整理核对结果，`FILE_MANIFEST.json` 记录各文件大小、SHA-256 与来源。

## 2026-11-06 补充：发梢已知问题

放大后可见长发末端的直线截断，现有原始导出包也可复现。此次已定位 L_Hair_08 分片并完成独立渲染核对，尚未还原原貌；需要原始角色 AssetBundle 或完整未裁切素材继续修复。详见 `04_Reports/HairTipFix/发梢截断调查与修复需求.md`。当前工程贴图和网格保留原样。
