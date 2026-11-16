# V1 程序设计与本轮边界

更新：2026-10-16。依据项目总宪法、现有工程和交付清单。本文描述 V1 目标与阶段边界；功能是否可用以各阶段状态文档和实测报告为准。

## 当前判断

同一个 Mika 使用两个表现身体。主工程保留唯一 AppRoot / CharacterCore，共享身份、关系、短期状态、历史、记忆和模型配置。Companion 与 AgentPet 通过 Home 切换，表现组件只消费角色结果和本地交互事件，不创建另一套角色服务。

Phase 0 / Phase 1 / Phase 2 与基础 Phase 3 已通过技术及用户验收。按批准的 [Phase 3 方案](<../03_阶段记录/phase3/phase3_plan.md>)，提供文字 Companion、META、中文输入 / 字幕 / Clock、MomoTalk 和设置；Phase 3F 按补充要求扩展到 schema v8、独立歌单 / 空间 / 人物卡和全历史日记，69/69、12/12 逻辑回归及最终发布版 Write / Restore / NoKey 通过。最新结果见 [phase3plus_status.md](<../03_阶段记录/phase3plus/phase3plus_status.md>)，[体验入口](<../03_阶段记录/phase3plus/phase3plus_delivery.md>)；正式 Voice / AgentPet 工具仍按对应阶段推进。

## 用户流程

启动 → Home → 陪伴空间或桌面陪伴 → 返回 Home。切换前保存共享状态，取消旧模式的工作，释放其音频、输入捕获和视觉资源。两个模式只有一个活动场景。

陪伴空间以完整记忆大厅为画面主体，不拆掉背景或持续显示技术状态。Enter 显示 / 提交输入，Shift+Enter 换行，Esc 打开菜单 / 逐层返回；聊天记录、设置、音量等使用临时面板。模型等待表现为思考，正文流进入文字呈现；此阶段口型静态，Phase 4 才由实际语音驱动。未配置服务给出简短可理解的反馈。

桌面陪伴使用同一个 Unity 窗口，目标约 600×500，透明、置顶、点击穿透按命中区域决定。Phase 0 Windows EXE 已验证真实桌面透明合成和返回 Home 后的窗口恢复；编辑器仍使用普通 Game 视图。最终命中策略、拖动及 Ctrl+Alt+Space 无尾气泡留待 AgentPet 阶段。

Home 已使用中文导航和随包中文字体。用户确认的 Mika / 老师关系作为初始种子，之后以用户独立人物卡为准，只有玩家主动保存可更改；允许小幅调整大厅布局。音乐 / 空间 / 卡片 / 长期回顾使用临时管理面板，主体维持大厅。人设口吻、字幕节奏和触摸手感留待用户体验精修。

## 资源与版本选择

| 部分 | 本轮选择 | 限制 |
| --- | --- | --- |
| 编辑器 | Unity 6000.3.7f1 | 不升级到其他 Unity 6 版本 |
| 渲染 | Built-in / Gamma / D3D11 | 不直接搬入 Spine 示例的 URP 工程设置 |
| 3D | CHDDD_Actions_v2 Unity V7 完整交付 | 18 身体 + 18 面部过程 + 9 静态表情 Clip；不是最新 Blender 的完整重导出 |
| Spine | CH0069_home 4.2.33 与交付 4.2 Runtime 配套 | 用户已确认授权，直接采用 spine-unity；保留许可证声明 |
| 特殊表情 | 保留 Squeeze V15、Omega V17 交接引用 | 参考 FBX 只有测试 Idle，不替换 18 动作主模型；未开放运行时 API |

原始 `Material/` 不修改。接入时保留 .meta 与 GUID，生成文件哈希及来源记录；只导入交付依赖，不整包搬入审查工程。3D 放入 `Characters/MainCharacter/Pet`。Spine 二进制及 atlas PNG 的最终路径遵守其加载器契约，表现代码与第三方 Runtime 独立于纯 C# Core。

## 角色结果与表现契约

共享情绪固定为 neutral、happy、shy、concern、sad、surprised、annoyed、sleepy。动作状态和情绪分别管理；素材内部 confused 可用于 Confused 动作，但不扩展共享枚举。模型不能直接指定骨骼、素材路径或内部 Enter / Exit Clip。

| 业务 / 交互 | 3D 表现 | Spine 表现 |
| --- | --- | --- |
| 空闲 | Idle，保留原身体和脸部过程 | Idle_01 循环一次设定 |
| 等待回复 | Thinking | 本地 Thinking 组合状态 |
| 实际工具执行 | Working | 暂无专门动作，使用安全回退 |
| 工具实际成功 / 失败 | 低频 Success / Confused 单次，结束恢复仍有效状态 | 没有交付专门动作，维持 Idle / 当前合法状态 |
| 真实音频播放 | 后续语音口型适配 | SpeechDriver 基于 PCM 能量；结束后宿主重算状态 |
| 头部短点 / 身体短点 | HeadClick / BodyClick 单次 | 使用交付的头部网格命中与触摸反馈 |
| 持续摸头 | HeadPat_Enter → HeadPat → HeadPat_Exit | Pat_01_M 为零时长保持姿态，释放走 PatEnd |
| 真实拖动 | Dragging_Enter → Dragging → Dragging_Exit | 不将摸头误判为窗口拖动 |
| 入睡 / 唤醒 | Sleep_Enter → Sleep → Sleep_Exit | 当前交付未声明，使用 Idle 回退 |

3D 对外公开 12 个逻辑动作，6 个过渡片段由宿主控制，不作为 LLM action。先命中资源白名单，再播放；未知动作回 Idle，未知情绪回 neutral。V1 业务调度优先级为拖动 > 直接触摸 > 执行状态 / 结果 > 主动问候 / 待机，结果演出结束后恢复仍有效的状态；事件去重，不逐帧重播。

Spine 的三个 pat 预设是触摸局部反馈，不等于完整八情绪映射；本轮不虚构该能力。Pat10 实验保持禁用，不引入 10.png 或拆分 region 图片。触摸捕获在失焦、UI 接管、取消和场景退出时释放；触摸结束不停止正在进行的对话。

3D 面部骨骼、逐帧形变和嘴型权重必须同步身体 Animator 时间。手动情绪独占脸部后，应显式恢复动作脸部过程。特殊表情将来采用另一局部控制器，并保证任意中断都撤销遮挡、覆盖材质和临时形变；本轮不在冻结资产上重新制作。

## 共享服务设计（Phase 0 后实施）

LLMService 按宪法默认 `deepseek-flash`，走一次请求的流式回复；把协议解析、呈现文字、允许的动作、工具执行结果分开。API Key 仅从本机私有配置读取，正式存储用 DPAPI，不进版本资源、聊天日志或交接清单。模型与官方 endpoint 已通过 Phase 0 真实请求，正式共享 LLMService 集成在 Phase 1。

DatabaseService 使用单 SQLite、FTS5、版本化迁移和串行写入。MemoryService 在完整回复后后台提取有原文证据的事实 / 事件 / 事项，供受限 PromptBuilder 取回；摘要、明确对象遗忘和纠错引用治理已实现并验证，不增加向量库或多代理框架。

VoiceService 连接本机 GPT-SoVITS `/tts`；分句生成 WAV 队列，最多一个 TTS 请求，播放完成才消费下一段。取消、切换场景、关闭应用时停止旧队列。失败保留文字并给出简短反馈。交付的游戏台词仅用于本地音频测试，不作为动态 TTS 或 BGM。

WindowsService 负责 UniWindowController / Win32 的单窗口、命中区域、透明与快捷键；这些能力不放进角色模型。AgentExecutor 只执行 Level 1 白名单工具，成功依赖实际返回值；不开放任意 Shell、任意文件改写或删除。每个异步流程绑定应用 / 模式 / 请求取消令牌。

日志记录时间、阶段、资源来源、版本、耗时、错误类型和验收结果；不记录 Key、完整对话或剪贴板正文。技术信息保留在日志和隐藏诊断入口。

## 验收顺序

1. 校验两份交接清单、依赖、GUID；记录 V7 与最新制作稿差异。
2. 在主工程验证 3D 原动作、脸部过程、过渡和返回 Home 后释放；在授权明确后验证 Spine 播放及触摸清理。
3. 使用真实配置验证 DeepSeek SSE、SQLite 消息落盘 / 重开、内置 GPT-SoVITS `/tts` 和实际透明窗口。语音采用用户确认的中文资源，精简分发方案见 `voice_runtime.md`。
4. 六项 Phase 0 全部通过才进入 Phase 1；每项必须有实测记录，源工程历史报告只作参考。

## 需要对齐的事项

- Spine Runtime 授权已确认；DeepSeek 走云 API，本机 Key 已配置并通过真实 Streaming 验收。GPT-SoVITS 已在项目内置并通过中日文推理，中文仍为默认，不要求另行安装 Python；声音听感与完整分发待后续验收。
- Phase 1 / Phase 2 已获用户整体效果验收；Phase 3 已按批准方案实施，当前状态见 `phase3_status.md`，用户观感验收独立于技术检查。
- Phase 3 发布版异常白色覆盖通过场景专用显式 Straight Alpha Shader 修正，原素材 / 第三方加载器不变；相机轻量高光调整独立保留，不切换全局渲染设置。Unity 原 Shader 分支异常的内部根因不作定论。
- 3D V7 作为本轮稳定基线；后续是否将 V8 / V9 身体及笑眉修改重新导出，单独确认，不替换已冻结制作文件。
- 关系采用 Mika 与老师的原作基调，称呼用户为“老师”；只读 Persona、行为边界、中文字体和 Home 文案已有初版，不虚构私人经历。八情绪只有 neutral 持久视觉，模型动作仅 none；BGM 按用户决定保持空列表。

接口设计依据：[DeepSeek Chat Completions](https://api-docs.deepseek.com/api/create-chat-completion/)；Runtime 许可证依据：[Spine Runtimes License Agreement](https://esotericsoftware.com/spine-runtimes-license)。本轮 GPT-SoVITS 路径与参数依据用户交付整合包内的实际 API / TTS 源码。
