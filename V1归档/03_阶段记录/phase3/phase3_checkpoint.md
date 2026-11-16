# Phase 3 暂停交接记录

> 历史记录：用户已于 2026-10-14 明确恢复任务。下文保留暂停时的事实与待办；最新状态以 [phase3_status.md](<phase3_status.md>)、开发日志及后续验收报告为准。`checkpoint.json` 是暂停快照，不作为恢复后代码的哈希清单。

2026-10-14，Asia/Shanghai。用户明确要求“当前先暂时停止任务，写好当前进度与交接记录方便任务恢复”。**当前已暂停，不继续实现 / 构建 / 联网 / 画面诊断。等待用户明确恢复。** 无 Git 仓库，保存以工作区文件及 `Validation/Phase3/checkpoint.json` 哈希为准。

## 恢复时先做什么

1. 阅读本记录、`phase3_status.md`、`phase3_plan.md` 和《项目总宪法.md》。3A—3D 已实现，不从头重做，不进入 Phase 4。
2. **先查看尚未审阅的三个发布版对比截图**：`Validation/Phase3/release_original_shader.png`、`release_explicit_alpha.png`、`release_ldr.png`，同时读 `release_shader_audit.json`。这些文件由最后一次不联网的 Visual 探针生成，用户暂停前尚未查看图像；不能先宣称 Alpha Shader 或关闭 HDR 已修复问题。
3. 根据截图决定最小渲染修正，重新核对编辑器 / EXE 的同帧效果。当前主程序没有绑定新 Shader。检查历史小按钮的自动字号修正效果，以及明亮背景下时钟 / 提示的可读性。
4. 对最终采用的代码运行适当回归、干净构建，再验收最新 EXE。已有真实云端功能报告属于较早构建，不能冒充最后构建的完整通过。
5. 用最终 EXE 完成 Windows 透明窗口 / Home 恢复回归，补齐许可随包文件、最终文档和综合验收清单，再交用户体验精修。

## 授权与设计边界

- 用户已批准按 Phase 3 plan 先实现，后续根据效果精修；本次暂停优先，恢复后按原方案继续。
- 人设：Mika 与老师的原作关系基调，称呼用户为“老师”；允许小幅调整大厅构图与布局；BGM 暂空，待用户补充。
- Unity **6000.3.7f1**，本机编辑器 `W:/Machine/Unity/Unity Library/Unity 6000.3.7f1/Editor/Unity.exe`；Built-in / Gamma / D3D11，模型 deepseek-flash，不升级依赖。
- 同一 AppRoot / CharacterCore / State / Memory，Home 唯一切换入口。Core 无 Unity 引用，UI 不直接访问 HTTP / SQLite。
- 原始 Material、冻结 CHDDD V7 与用户 Blender 制作文件保持原样。本轮复核 Phase 0 copiedFiles 的 **862 个导入文件全部哈希一致**，包含交付的 Spine 加载器；没有修改第三方加载器 / 原始图集。
- 正式语音队列、真实口型同步、Ducking、音频缓存和复听属于 Phase 4；桌宠最终输入 / 拖动 / 工具属于 Phase 5。未启动 GPT-SoVITS，也未复制第二份 Runtime。
- 自动验收全部使用 Validation 隔离数据。Key 仅从既有本机 DPAPI 读取后传入测试子进程环境，不输出、不复制到构建或验证目录。没有迁移正式用户聊天库。

## 已实现的内容

| 检查点 | 实际结果 | 证据 / 限制 |
| --- | --- | --- |
| 3A | 只读 Mika / 老师初稿、官方中文字体、明确情绪回退、相机轻量高光调整 | 编辑器图像 / 字体通过；发布版视觉在 3E 暴露新问题，尚未最终通过 |
| 3B | META 增量解析、元信息 / 正文流、schema v7 原文 / 正文分离、事务提交 Mood | EditMode 55 / 55、PlayMode 7 / 7 |
| 3C | 中文 Home、Enter 输入 / IME 保护、流式字幕 / 分页、Clock、Esc 菜单、UI 接管触摸、新轮替换旧轮 | EditMode 56 / 56、PlayMode 9 / 9；实际操作系统 IME 选词仍需人工确认 |
| 3D | 共享 MomoTalk 游标分页、明确轮次 / 记忆遗忘、持久音量 / 字号、有限 BGM 播放器 | EditMode 57 / 57、PlayMode 10 / 10；BGM 空列表静音，合成测试声音不随包 |
| 3E | 最后完整回归 59 / 59、10 / 10；首版真实 UI 与记忆验证通过；已建立发布渲染 A/B 探针 | **暂停，未完成最终验收；最后完整测试后又改了 UI / Shader / 探针** |

八情绪均能解析，但当前只有 neutral 持久视觉，其余明确回退 neutral；三个 pat 预设保持触摸用途。模型 action 白名单仅 none，不开放原作全部 52 个动画。字幕阶段不模拟发声，Speech 音频源不播放。

## 关键代码入口

- `Assets/App/Core/Character/DialoguePresentation.cs`：256 字符控制头缓存、首行 META、未知值降级、受限动作策略。
- `Core/Character/CharacterCore.cs`：正文 / 元信息流、原文保存、完整轮次与 Mood 事务、取消及原有记忆治理。
- `Core/Persistence/DatabaseService.cs` / `ConversationStore.cs`：schema **v7** 的 `assistant_presentations(message_id,protocol_version,body_text,emotion,action)`；messages.content 保留模型原文，读取投影保留原消息 ID / 来源；v6 历史不猜标签、不改写。
- `Core/Memory/ConversationHistoryService.cs` / `Persistence/ConversationHistoryStore.cs`：按 created_at / user ID 稳定游标，每页 30 轮，两个模式共享历史。
- `Core/Memory/PromptBuilder.cs` / `MemoryService.cs`：Companion 协议计入 12,000 字符预算，白名单来自 Config 的 Manifest；提取 / 摘要读取正文。
- `Core/Config/ConfigService.cs`：只读 Manifest 策略 / BGM Resources 名称，非秘密 BgmVolume / BgmMuted / SubtitleFontSize；原子保存保留既有字段。
- `Assets/App/Companion/CompanionScreen.cs`：场景对话与输入、generation 防迟到、IME 结束帧保护、字幕阅读、状态重算；`CompanionPanels.cs`：MomoTalk / 设置 / 遗忘 / 导航淡出。
- `CompanionPresentation.cs`：视觉 Adapter、情绪明确回退和 UI 捕获；`CompanionTone.cs` / `.shader`：曝光 0.95 / 高光压缩 0.35，只作用 Companion 相机。
- `Companion/SpineStraightAlpha.shader`：新增显式输入 Alpha 乘法的候选 Shader，**当前主程序未绑定**，只供 Visual 探针对比。
- `Companion/Bgm/BgmManager.cs`：最多 16 首、循环 / 切换 / 淡入淡出 / 静音；空播放列表安全静音。
- `Assets/App/SharedUI/HomeScreen.cs` / `CompanionUIFactory.cs`：运行时 uGUI / TMP 界面、动态中文图集；旧骨架 Canvas 停用但保留序列化文件。
- `Assets/App/Validation/Phase3PlayerProbe.cs`：显式 `-phase3-probe <报告目录> Write|NoKey|Visual`。Visual 不发云请求，逐次截图并退出。
- `Assets/Editor/Phase3Setup.cs`：Install 绑定 AppRoot 的 Manifest 与 Adapter 的 expression_map；VisualAudit 保存编辑器图像 / 字体并将候选 Shader 加入 Always Included。

新增脚本与资源的 .meta 已由 Unity 生成；AppRoot Prefab、Companion 场景配置引用已保存。

## 发布渲染问题：已知事实与未确定部分

最初编辑器同帧检查为 Normal 284 / Additive 10，无 Multiply / Screen。补齐后者没有改变画面，试验已撤回。相机高光调整后的编辑器接近纯白像素 22,452 → 1,632，平均亮度约 0.630 → 0.589，前后图已经查看。

较早真实 EXE 的 `reply_write.png` / `history_write.png` 等出现严重过亮、异常高光覆盖，明显不同于编辑器；功能 flags 全 true 不能代表画面通过。首版 UI 图像和构建报告另存 `Validation/Phase3/FirstUI/`。

最后一次 Visual 探针依次保存：原 Shader → 显式 Alpha Shader → 再关闭相机 HDR。材质报告显示原 Shader 为 Spine/Skeleton，四份材质都有 `_STRAIGHT_ALPHA_INPUT` keyword 且参数为 1；候选 Shader 可用。**这仅证明运行时标志存在，不证明发布变体确实执行了正确混合，也不证明异常已修复。三个新对比图尚未审阅。** 不预先断言根因是变体裁剪、HDR 或原始素材。

历史小按钮文字在首次 EXE 图中因高度不足不可见。UIFactory 已增加按钮字体自动缩放 14—22；最后构建包含此改动，但普通 MomoTalk 截图尚未重验。时钟 / 底部操作提示在明亮画面下也需核对对比度。

## 构建、测试与报告对应关系

- 最后完整 EditMode **59 / 59**、PlayMode **10 / 10**：`Validation/Phase3/3E.EditMode.xml`、`3E.PlayMode.xml`。包含临时情绪在取消 / 存储失败时不持久化。后续改动集中在 Player 截图实现、候选 Shader、按钮字号和 Visual 探针；没有再改 Core，但未重跑最终场景检查。
- 首次 Player 编译因项目未启用 ScreenCapture 模块失败，已改用已有 Texture2D.ReadPixels，不增加依赖。失败日志 / JSON 为 `first_player_build.*`。
- 较早零错误 Release 构建 **133,037,395 字节**通过真实 UI Write / NoKey 及记忆 Write / Restore / NoKey。全部 flags passed，但人工查看发布图发现上述异常。对应 UI / 记忆报告仍保留，不能将其绑定到最新 EXE。
- 最新检查构建 **133,048,987 字节**、错误 **0**、14.491 秒，见 `player_build.json`。无 Development 水印；包含候选 Shader、按钮缩放与 Visual 模式。仅运行不联网 Visual 渲染对比，尚未对该 EXE 重做普通 UI、记忆和窗口验收。
- **尚未生成 Phase 3 acceptance.json，尚未执行最终透明窗口回归，尚未编写最终 phase3_delivery.md。** 不标记 Phase 3 完成。
- Phase 2 最终 EXE 与验收证据继续保留，不覆盖。

隔离数据保留位置（不是正式用户数据）：

```text
UI:
C:/Users/fufu/AppData/Local/GiftCompanion/Validation/phase3-ui-e6d82c0e80484ca9a1faa627675ca18d

Memory:
C:/Users/fufu/AppData/Local/GiftCompanion/Validation/phase3-memory-f62ae3bfaa3245e29fe98047d0c95c69
```

路径由 `ui_run.json` / `player_run.json`记录。最后 Visual 数据为新随机 Validation 子目录，不含 Key，未登记为正式交付。

## 恢复后可用的工具

仅在用户明确恢复后执行，以下不是当前继续运行的指令：

```powershell
& 'W:\Machine\VSCode\Mika_Gift\GiftCompanion\Tools\Test-Phase3.ps1' -Checkpoint Resume
& 'W:\Machine\VSCode\Mika_Gift\GiftCompanion\Tools\Build-Phase3.ps1'
& 'W:\Machine\VSCode\Mika_Gift\GiftCompanion\Tools\Test-Phase3UIPlayer.ps1'
& 'W:\Machine\VSCode\Mika_Gift\GiftCompanion\Tools\Test-Phase3MemoryPlayer.ps1'
```

最终窗口检查使用现有 `Test-WindowRuntime.ps1`，显式传 Phase 3 EXE、`Docs/Validation/Phase3/Window` 报告目录和新的 Player Validation 数据目录，避免覆盖 Phase 0 / Phase 2 证据。工具均使用本机现有 Key，不能打印解密内容。

`Tools/Start-Phase3.ps1` 已保存：正式 UI、不传 -phase2-dev，沿用 Editor Key 到子进程环境；脚本尚未启动正式数据目录实例，**当前 EXE 仍为有已知画面问题的检查构建，不建议把此启动入口视作已验收交付。**

## 许可证、收尾与其他待办

字体为 Noto Sans CJK SC Regular 2.004，16,437,364 字节，SHA-256 为 `2c76254f6fc379fddfce0a7e84fb5385bb135d3e399294f6eeb6680d0365b74b`。原始 OTF / OFL 在 `Assets/App/SharedUI/Resources/Fonts/`，来源 / 字体版权见 `font_provenance.json`。没有安装 fontTools；使用只读 name 表解析取得版权。一次终端 GBK 打印 © 失败，但 UTF-8 的 provenance / notice 文件已经成功写入并读回。

当前构建已复制 `Licenses/NotoSansCJK-OFL.txt`。`Validation/Phase3/NotoSansCJK-Notice.txt` 和 `Spine-Runtimes-License.txt` 已生成，**尚未补入构建或 Build-Phase3.ps1 的稳定复制流程**。Spine 授权已有用户确认，不重复询问授权；保留原许可证。

恢复后还需同步 architecture、character_protocol、memory_protocol、v1_design、validation 与交付说明的 Phase 3 实际内容；部分正文仍为 Phase 2 / 方案历史，当前状态以本记录和 phase3_status 为准。可考虑在对话完成时刷新已打开的 MomoTalk；当前打开历史会读取快照，再次打开能看到后续完成记录，这项尚未改动。

没有使用 Unity MCP / Blender MCP，没有委派子代理。暂停时未发现本任务遗留的 Unity 或 GiftCompanion 进程；没有待继续等待的运行命令。只完成保存与交接，不擅自恢复阶段工作。

保存校验完成：checkpoint.json 收录 233 个源码 / 工程 / 工具文件、3 个构建文件和 70 个证据文件，306 个 SHA-256 全部读回一致。8 份暂停相关文档 UTF-8 / 围栏 / 冲突检查、13 个本地链接和 5 个 Phase 3 PowerShell 工具的静态语法检查通过；没有执行工具脚本、Unity 或云请求。
