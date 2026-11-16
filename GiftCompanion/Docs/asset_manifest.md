# 资产 Manifest 与待接入清单

2026-10-11。角色交接来源为 `Material/Mika_MemoryLobby` 和 `Material/CHDDD`。正式 character_manifest 已记录本轮验证的播放资源，`status=phase0-resource-playback-verified`，LLM 表现指令仍关闭。identity / behavior 及全局八情绪映射继续保留待填写部分。

- Companion：完整 CH0069_home、Spine 4.2.33 导出，spine-unity 4.2.120 / spine-csharp 4.2.37；源码许可证保留，用户已确认授权。StreamingAssets 中 6 份源文件、7 个必需呈现脚本和 Behavior Profile 保留原 GUID。主程序禁用示例诊断 UI，Pat10 禁用。三个触摸预设不当作完整八情绪映射。
- Pet：Unity V7 完整包 73 项资源，位于 `Characters/MainCharacter/Pet/CHDDD`。Generic、无 Root Motion，18 身体 + 18 脸部过程 + 9 静态表情 Clip；动作脸部采样和嘴型材质配套。已接入显示和播放适配器，真实鼠标 Head/Body 区域尚未实现。当前 V7 材质在主工程偏粉，仍需人工审阅与色调对齐，不作为最终美术验收。
- Audio：中文及新日文 v4 角色权重和参考 WAV 分开预置，内置 CUDA 环境中 `/tts` 均实测通过；默认中文，正式 VoiceService 未连接，听感待试听；BGM 未提供。
- Character：配置文件中的称呼、Persona、Relationship、行为边界仍待填写。2026-10-13 用户确认采用 Mika 与老师的原作关系基调，称呼用户为“老师”；详细草案将在 Phase 3 整理。

2026-10-12 用户完成 Phase 1 整体效果验收，反馈 Spine 有些过曝。登记为 Phase 3 Companion 视觉对齐事项：届时在现有 Built-in / Gamma / D3D11 基线上核对观感与前后截图。当前未诊断具体原因，不将反馈直接归因为 Shader、贴图或颜色空间，也未修改冻结素材。

2026-10-13 用户完成 Phase 2 验收，并确认 Phase 3 允许小幅调整原大厅构图和界面布局；BGM 先使用空播放列表，待补充曲目。对应方案见 `phase3_plan.md`，仍待审批，当前未修改素材、字体或渲染配置。

已有上级 `Material/CH0069` 和审查成果保留。CHDDD Blender V8 / V9 后续修订未全部同步到 V7；Squeeze V15 与 Omega V17 只登记交接位置，不导入 Idle-only 测试 FBX 替换完整主模型。

最后来源复核发现 `CHDDD_actions_v2.blend` 在 2026-10-11 20:53:02 发生变更，旧清单哈希已不匹配；其余 70 / 71 交接源文件保持一致。该制作稿保留，不自动覆盖或重导；已导入的 73 项 Unity V7 负载哈希仍全部一致。详见 `Validation/Phase0/source_recheck.json`；后续重新交接时需要同步更新制作稿来源清单。

导入工具先核对交接 SHA-256、包与松散文件一致性和 GUID 冲突，拒绝覆盖已修改的目标。证据见 `Validation/Phase0/asset_import.json`。缺失动作回可用 Idle，内部 Enter / Exit 与未接入特殊表情不开放给 LLM；配置文件不是全部业务能力已经实现的声明。
