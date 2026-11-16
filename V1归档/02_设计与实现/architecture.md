# 当前架构

> 2026-10-19：Phase5共享Core/工具与schema v10已完成技术验收；Phase6新增应用内配置、资料快照恢复及独立目录装配，状态见[phase6_status.md](<../03_阶段记录/phase6/phase6_status.md>)。旧阶段架构说明保留为历史背景。

## 边界

Spine 与 3D 小人是同一角色的两个身体，CharacterCore 与共享 Memory 才是角色本身。Phase 3 提供 META、文字 UI、MomoTalk 与设置；3F 补充文件化音乐 / 空间 / 人物卡和长期回顾；Phase4 增加 Companion 语音，不向 AgentPet 增加 GPT-SoVITS，也不提前开放 Phase5 工具。

## 初始化

BootstrapEntry 实例化 AppRoot Prefab → 单实例检查 → Logger → ConfigService / ContentLibrary → DatabaseService 迁移 / 中断恢复 → AudioCache 回收孤立文件 / 创建 VoiceRuntimeManager（不启动） → LLMService → RelationshipDiary 导出恢复 → MemoryService → CharacterCore → Home。MemoryService 恢复未完成提取，配置可用时启动唯一后台处理器。日记不在启动时生成。启动失败停用交互，显示简短反馈；异常类型留在日志。启动期间退出先取消并等待初始化收尾，避免初始化继续使用已关闭服务。

AppRoot 使用 DontDestroyOnLoad 保留。AppController 是同一对象上的组件；Config、Database、Character、LLM、Memory、Logger 是由该根对象唯一持有的普通 C# 服务，没有为了呈现树形目录创建额外服务 GameObject。场景控制器只取得这个根对象并调用导航接口。

单实例 Mutex 在 Windows Player 启用；编辑器不锁住独立测试进程。显式退出 / 系统正常退出先取消模式和应用任务、等待切换与请求收尾、保存状态，再关闭数据库 / HTTP / 日志并释放 Mutex。编辑器停止或直接销毁根对象执行有界尽力保存；强制杀进程不保证最后内存变化。重复加载 Bootstrap 复用已存在根对象。

## 程序集

当前编辑器基线为 Unity 6000.3.7f1；uGUI（含 TMP）2.0.0、Unity Test Framework 1.6.0、Newtonsoft JSON 3.2.2。工程初始化使用该版本支持的 AssetDatabase.ImportPackage，并等待 TMP 导入完成。Physics 是胶囊体占位使用的 Unity 基础模块。

- `GiftCompanion.Core`：纯 C# 角色模型、配置、日志、SQLite、LLM、Memory 提取 / 检索与 PromptBuilder，不引用 UnityEngine。JSON 统一使用 Newtonsoft JSON。
- `GiftCompanion.Runtime`：Bootstrap、应用根对象、模式生命周期、中文 Home、Companion 输入 / 字幕 / 面板 / BGM，引用现有 Input System 和 Spine 表现程序集。
- `GiftCompanion.Editor`：仅编辑器使用的工程初始化工具。
- 两个测试程序集：少量核心逻辑测试与场景生命周期测试，不引入大型测试体系。

本轮增加交付的 `spine-unity` / `spine-csharp` 和 Input System 1.18.0，使用 Both 输入模式兼容现有 uGUI 导航；补齐 Animation / ImageConversion / JsonSerialize / Physics2D 基础模块。继续使用 Built-in / Gamma / D3D11，不导入示例工程的 URP、导航、Visual Scripting 或 MCP 等包。

`GiftCompanion.CHDDD` 包含冻结的 3D 交付控制器，`GiftCompanion.SpinePresentation` 包含必需的 Spine 本地表现脚本。场景中的 PetPresentation / CompanionPresentation 适配器只持有表现资源，不新建共享服务。Spine 示例加载器的 Update / OnGUI 禁用，由适配器初始化和清理交互。

## 模式与状态

一个 CharacterCore 和一个 CharacterState 对象供全部场景使用。Home 是唯一模式入口，Companion 与 AgentPet 间必须经过 Home。重复 UI 点击不排队，只允许一个场景切换执行。

AppController 预检目标，暂停新请求并取消 / 等待旧请求，然后 CharacterCore 保存含目标 LastMode 的独立快照，保存成功才更新可见字段；共享 State 引用不替换。取消只在持久化提交前生效，提交后完成 Unity 场景加载；失败尝试回 Home 并同步持久状态，恢复失败停止交互。旧模式令牌取消，表现资源随场景释放。启动 CurrentMode 始终 Home，LastMode 只作最近成功提交记录。

DatabaseService 使用 SQLite 3.54.0 原生库，初始化完成后 `IsPersistent=true`、`SchemaVersion=9`。memory.db 支持 app_state、messages、sessions、assistant_presentations、memories、user_facts、open_loops、FTS5、摘要覆盖 / 来源 / 版本、消息 / 具体记忆两级回复引用，以及 relationship_journals / journal_sources；存储接口复用单连接串行事务、WAL、参数绑定与独立快照。已有库迁移前原生备份，更高版本 / 损坏 / 不可写明确失败，不回退 RAM。Phase 0 独立验证库不导入正式库；v9 的 audio_records 索引实际分段 WAV，不改写消息原文。

CharacterCore 先提交用户原文再发 HTTP；完整 assistant 原文、正文投影 / 表现字段、Mood / 状态时间和提取 pending 同一事务提交。Companion 增量解析首行 META，AgentPet 保留普通正文接口；中断原文保留，截断片段不伪装完整回复，临时情绪不落库。第一次受理消息开始会话，离开活动模式 / 退出时关闭会话。MemoryService 后台最多一项云提取，前台可取消它，待办持久化；失败显式重试。MemoryWriteQueue 职责由现有 DatabaseService 承担，不另开竞争写连接。

Retriever 使用 FTS5 unicode61 加 CJK 单字 / 双字索引词，参数化有界查询并过滤 active。相关度归一化、重要度和时间衰减按 0.60 / 0.25 / 0.15 合成。PromptBuilder 以只读身份 / 关系、事实、事件、事项、当前会话摘要、完整近期历史和当前消息构建请求，总字符上限 12,000。提取即使未完成，近期完整对话仍可跨模式衔接；来源 / 记录引用与版本在回复提交时核对 / 保存。

模式退出、10 个新完整轮次或 8,000 字符触发后台摘要，分批取受限原文并保存覆盖位置及来源，退出不等云端。摘要与提取共用最多一个后台云任务，前台可取消、待办保留；旧版本结果拒绝提交。Core 遗忘命令先收尾前台请求，再将明确目标禁用、移出索引并排除原文片段 / 关联复述，失效摘要、抑制旧提取。独立结构化事实按记录引用追踪，避免同条消息里的其他事实连带失效；原始聊天保留。

## 配置与资源

identity.json 是 AppRoot Prefab 引用的只读种子；首次创建用户 Config/character_card.json 后，以用户卡片为准。玩家显式保存 / 重读，经 AppRoot.PlayerWorkAsync 取消并等待前台请求、让后台记忆让路后，在原 Core 替换同 ID 的不可变卡片；LLM / Memory / 日记没有写卡片接口。Mika / 老师是初始关系，不虚构共同经历。character_manifest 保留受限 CompanionDialoguePolicy；动作白名单只有 none，八情绪均可解析但只有 neutral 持久视觉，其余回退。旧 BGM Resources 字段兼容保留，正式播放改由独立歌单负责。

ContentLibrary 独立读取 Config/bgm_playlist.json / companion_spaces.json。bundle: 路径指向 StreamingAssets，其余相对资料根；导入复制允许文件，不执行包内代码。提交在写锁内追加最新清单，选择未变化不写盘，损坏文件回退后自动初始化不覆盖原件。显式修改保留上一版，普通文件系统原子替换；当前加密 / 重定向文件层报告错误 17 时，使用校验和写前副本恢复中断提交，不宣称该覆盖分支原子。接口细节见 [player_content_contracts.md](<player_content_contracts.md>)。音乐 / 空间 UI 可替换，类型化文件接口不依赖 UI。相同角色切换空间仅替换表现对象，预解析版本 / 图集 / Idle 后加载，成功保存才显示，失败保留原空间。未适配新包只播放基础 Idle；原作互动只对内置配置启用。

普通设置位于 Config/settings.json，缺失时使用默认值；校验模型、超时、快捷键、BGM 音量 / 静音及字幕字号 20—36。预览基于配置副本，显式保存才持久化，关闭未保存面板恢复原值。保存使用临时文件与替换；损坏内容保留，不将秘密写入普通设置。Editor 使用工程 UserData，Player 默认使用本机应用数据 GiftCompanion；仅显式验收标志允许在 Player Validation 子目录隔离数据，普通启动忽略覆盖参数。

3D 已进入 Characters/MainCharacter/Pet；Spine 原始流式资源与加载器保留交接契约路径。原始 Material 及现有审查数据保留在上级目录。语音权重位于 Unity Assets 外的 Runtime/GPT-SoVITS，推理环境装配与分发见 `voice_runtime.md`。

## Phase 0 外部能力

Windows Player 的 AppRoot 附加唯一 DesktopWindowPrototype，使用本地固定版本 UniWindowController 操作当前 Unity 窗口。场景加载时切换透明 / 置顶和客户区尺寸；Unity 帧末分辨率应用后再改变原生样式。仅 Windows Player 启用，编辑器窗口不受控制。明确命令行标志启用的 Phase0PlayerProbe 自动验证窗口状态、SQLite 和返回 Home，普通启动时不运行该探针。

内置 GPT-SoVITS API 由独立 Python 进程验证，代码和权重位于 Assets 外，中文 / 日文各自配置，同一时刻一个测试进程。启动 / 停止、import 记录、裁剪及双语实测工具见 voice_runtime.md，正式 VoiceService 尚未接管进程或音频队列。

DeepSeekApiClient 支持带取消 / 超时的 SSE 和有界输出 JSON，由同一 LLMService 接管 HTTP 生命周期。JSON transport 允许 64—8192 tokens；提取 / 摘要仍采用原来的小预算，日记滚动 / 最终请求使用 2800 / 5000。CharacterCore 单请求互斥，历史按真实 user / assistant 角色传输，完整且落库成功才更新交互时间；取消 / 截断不提交成功结果，显式非正常 finish_reason 被拒绝。401、429、网络、超时与协议失败映射稳定错误码，不默认自动重发对话。JSON 提取校验来源与原文证据，不直接信任模型结构。

LocalSecrets 读取环境变量或 Windows 用户级 DPAPI 文件。Configure-DeepSeek.ps1 显式支持 Editor / Player 目标，不自动复制 Key。Phase2PlayerProbe 只按显式标志执行隔离验收，普通启动不发送探针。开发面板仅显式启用，UI 调用 Core / MemoryService；开发启动脚本可将既有 Editor Key 仅传给子进程环境，普通 Player 数据目录与配置目标保持独立。

## Companion 表现与 UI

ModeSceneController 为 Home / Companion 添加场景 UI。CompanionScreen 汇合独立的对话进度、面板和触摸状态，计算八状态；模型元信息先临时反馈，完整事务成功后共享 Mood 才生效。新消息取消并等待旧轮收尾，generation 丢弃旧回调。Enter 输入 / 发送，Shift+Enter 换行，IME 组合及结束帧保护；Esc 按输入、子面板、菜单层级收起。语音准备 / 关闭 / 不可用时字幕流式显示并为长文本分页；就绪后按实际播放段推进字幕 / Speaking，文字完成与声音完成分别管理。

CompanionPresentation 复用原加载器与完整 CH0069_home，绑定场景自有运行时材质至显式 Straight Alpha Shader；固定 Built-in / Gamma，保留 PMA 顶点色、Normal / Additive 和原 HDR 设置。CompanionTone 对该相机做轻量高光调整，原素材、第三方 Shader 和加载器不改写。只有头部实际网格命中开放；UI / 失焦 / 离场释放捕获，触摸结束恢复仍有效的对话状态。

MomoTalk 经纯 C# ConversationHistoryService / IConversationHistoryStore 读取单库，按 UTC created_at 与 user ID 游标每页 30 轮，展示两个模式的正文投影；不直接访问 SQLite。遗忘调用既有 Core 的明确 ID 接口，历史原文保留。打开面板为快照，重新打开刷新；旧查询取消后不能写回已销毁 UI。Noto Sans CJK SC 使用动态 TMP 图集，正文 richText=false。

BgmFilePlayer 从文件歌单流式加载本地 OGG / WAV / MP3，BgmManager 随场景释放，当前曲目原生单曲循环，只有手动操作更换。双源线性交叉淡化用于切歌，首尾音量包络缓和循环边缘，音量总和不超过玩家设置。离场先取消加载再淡出；两首随包完整长音轨约 168.4 MB，不复制视频。合成测试音频不随包。Phase4 在独立语音源真正发声时，以独立临时系数压低 BGM，并在结束 / 打断后平滑恢复；不保存压低后的音量，不解除静音。

RelationshipDiary 经同一 AppRoot 前台工作门控，分批读取两个模式全部已完成且未排除聊天，按稳定水位 / revision 生成滚动摘要和第一人称回顾，不限最新轮次或当天。数据库先提交正文 / 来源 / export pending，串行队列内通过上述文件服务导出 Markdown；失败重试、缺失文件补回，成功且存在的文件日常读取不改写。遗忘 / 更正清空旧回顾正文、收起导出且不留正文备份。日记不进入事实检索、不写人物卡。退出等该工作收尾后才关闭数据库。

## 正式语音（Phase4）

AppRoot 唯一持有 VoiceRuntimeManager / AudioCache，场景持有 CompanionVoicePlayer / AudioSource。进入 Companion 按需启动内置 Python，绑定本机并校验实例 / PID 的健康响应；退出先停止播放 / 队列、结束自有进程并等待任务，随后关闭数据库。父进程意外退出时 host 的监视线程也会退出。其他监听程序不接管、不停止。

VoiceRuntimeSettings 读取独立 Config/voice_runtime.json，引用原有 voice.json / YAML，不将参数写进角色提示或人物卡。模型准备期间本轮先显示文字；Ready 后新轮使用 SpeechSegmenter → 单并发推理 → WAV 文件 / 生成队列 → 场景 AudioSource。只读正文增量，朗读清理不修改原文 / 正文投影。队列超界、推理失败仍可继续接收正文。取消时立即停声 / 丢弃迟到结果，实际推理门控保留至响应结束；超时先退役该确切自有进程。

已适配大厅按真实语音 PCM 驱动口型，每段短生命周期复用既有口型组件，避免长期字典保留所有片段；BGM 不驱动嘴部。未适配空间只播放声音 / 字幕，保留基础 Idle。语音与 BGM 分属不同 AudioSource 对象。

AudioCache 把 WAV 保存至用户相对路径；assistant 完整提交后整组 audio_records 才发布。启动回收无索引的程序命名文件，缺段不完整复听、不自动重生成。MomoTalk Replay 经同一播放仲裁，可无 Key / 无模型离线复听，实时对话优先；清缓存只删索引与校验范围内的 WAV，聊天 / 记忆保留。

## 尚未实施

桌宠最终输入、拖动、全局热键和工具属于 Phase5；完整跨机分发属于 Phase6。Phase 2 开发面板仅显式诊断启用，不承担正式 Companion 操作。


## 双语语音扩展

主链继续使用同一个 CharacterCore，以中文流式更新前端 / 历史 / 记忆。场景语音 Session 固定其 runtime / localizer / language 快照；日语分支通过独立有界 SpeechLocalizer（廉价 Flash 非思考调用，无对话历史）才进入原 TTS 单并发门控。声音准备及翻译等待期间立即显示中文；实际播放时按中文片段推进字幕。翻译失败不向日语模型传中文，不改共享 Mood / 原文。

语言选择与音量同属 settings.json；切换先取消正文 / 翻译 / 排队 / 声音，等待旧任务与自有 Python 退出，再按 zh / ja 配对配置创建唯一 manager。Python / 权重不复制，无双模型并驻。speech_localization.json 独立、重启加载；有界短句缓存仅在内存。

音频旁 .speech.json 存实际中日文本与来源，继续用 v9 audio_records 的 profile_id 标识原音色，不升级旧库。MomoTalk 标注中文 / 日语，复听只读原 WAV；旧无 sidecar 的中文缓存保留。恢复 / 清理覆盖自有 sidecar。测试与交付证据独立在 Docs/Validation/Bilingual，原 Phase4 证据保持不变。


## 玩家听感反馈后的语音节奏修订

默认日语、一次性VoicePreferencesVersion=2升级。接收中文与显示中文分离：正文/历史仍即时接收和提交，语音就绪时字幕仅由SegmentPlaying推动，避免先预览全文后倒回分句；文字回退路径保留。SpeechSession接收控制头情绪，IExpressiveSpeechLocalizer将其作为本地化参考；IStyledVoiceSynthesizer在同一个GPU门控内选择同一模型的短参考/语速。VoiceDelivery配置与VoiceReferences独立，内容纳入音色指纹和sidecar来源，不改变角色事实/共享Mood的提交规则。

音频结束后0.45秒/短分句0.18秒最小间隔，等待合成的时间计入间隔，取消不等停顿，句间BGM保持压低。日记策略含MaximumKaomoji，narration-v2与策略参与生成指纹；仅玩家再次主动生成才按新规则出新回顾，原MD不覆盖。完整交付见voice_polish_delivery.md。

## 表达目标与段落排版扩展（2026-10-18）

DialoguePresentation新增可选VoiceIntent，META voice经白名单传到场景Session、本地化与同模型参考/原生语速；8种共享Mood与v9数据库不变。新增恼火/伤心/犹豫/平直为有限表达近似，复用原参考/害羞参考，无新模型、WAV或依赖。voice_delivery v2、speech_localization v3只升级内置旧默认，保留自定义。

为避免多轮模仿已去头的正文而省略META，PromptBuilder仅在Companion请求中给近期assistant重建白名单格式头，预算包含新增字符；原中文正文不改。已投影记录仅解析原文前256字符，未投影旧记录不猜控制词，AgentPet请求仍用纯正文。不会整段重放未过滤原始传输文本。

日记最终提示返回paragraphs数组，DiaryTextLayout将自然段统一后提交原DB/MD流程；超长段仅补空行，旧diary响应兼容，不额外调用模型。策略v2默认颜文字上限4（0–12）、目标段长160/最大260，narration-v3指纹让玩家下次生成使用新规则，不自动改写旧MD。原理与能力边界见[实现说明](<../03_阶段记录/声音与日记扩展/expression_diary_implementation.md>)。

## Phase6资料保护与独立运行

启动单实例锁后，先检查pending_restore/restore_transaction，再打开Logger、Config和SQLite；固定数据根事务替换，未提交操作回滚，Secrets与安装Runtime不动。AppRoot仍是唯一Core和服务所有者。维护操作通过PlayerWork暂停角色，再获取MemoryService.PauseAndDrain租约、停止语音、保存状态；AppController在此期间拒绝切换。后台取消收尾结束后才允许一致快照。

PlayerBackup复用串行数据库队列中的SQLite Backup API，副本转DELETE日志模式后独立校验。ZIP清单含格式/程序/数据库/角色/时间/范围/大小/SHA256；分块读写和严格解压路径/数量/大小限制，不把整个备份放入内存。恢复暂存与哈希复核在线程池中，UI仅显示预览并请求AppRoot操作。确认后生成当前资料恢复点，关闭连接并重启；启动事务保留旧目录，恢复后重置外部工具权限。MD手工批注作为文件原样保留。

PlayerMaintenance是Home临时覆盖层，各模式经统一导航回Home打开。联网测试委托AppRoot的短时独立连接检查，不提交角色对话或记忆；Key保存采用当前用户DPAPI，受控重启后统一加载已有客户端，避免第二套角色实例。正常Player忽略开发Key环境注入，发布标记使语音忽略工程Runtime覆盖。语音host所需可写YAML与临时/库缓存统一落在用户RuntimeCache，安装目录仅用于读取。构建和发布脚本采用明确目录清单，测试/Secret/个人数据库不进入目录包。
