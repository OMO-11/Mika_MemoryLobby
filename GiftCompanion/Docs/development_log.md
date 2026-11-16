# 开发日志

## 2026-10-07：工程骨架

依据总宪法第 53 节，使用本机 Unity 6000.6.4f1 建立 GiftCompanion 工程。创建共享核心、服务接口与最小实现、AppRoot 生命周期、四场景初始化工具、导航与取消边界；添加角色资产配置占位、少量逻辑测试与场景 Smoke Test。

Unity 初始化与 C# 编译通过；EditMode 5 / 5、PlayMode 2 / 2 全部通过。修复首次场景生成中缺失的 AppRoot Prefab 引用及异步 TMP 资源导入时序，初始化工具现在等待资源导入结束并补齐缺失引用。

本轮未进入 Phase 0 的外部服务接入。保留上级现有模型审查与素材。实际验证范围与证据在 `validation.md` 记录。

## 2026-10-08：适配 Unity 6000.3.7f1

按用户已安装的版本调整工程基线，更新 ProjectVersion、uGUI/TMP 2.0.0、Test Framework 1.6.0 和包锁，JSON 库继续使用 3.2.2。编辑器初始化改用 AssetDatabase.ImportPackage，保留异步导入完成检测；胶囊体使用的 Physics 模块改为显式依赖。

运行脚本通过共同的 Resolve-UnityEditor.ps1 读取工程版本、定位安装目录并校验可执行文件版本，避免继续调用已经移除的旧编辑器。适配前源文件与包锁保留在 Docs/Migrations/6000.3.7f1，旧验证记录归档到 Docs/Validation/6000.6.4f1；旧 Library 移至 .MigrationCache/6000.6.4f1-Library，新版本从空缓存重新导入。

新版本实测 EditMode 5 / 5、PlayMode 2 / 2 通过，两个测试进程正常退出。首次初始化退出时曾出现原生崩溃，保留日志后复查初始化成功并正常退出，详见 validation.md。当前未更改产品设计或接入素材、外部服务。

## 2026-10-11：第一版设计与 Phase 0 资源接入（进行中）

### 资源核对

收到用户提供的 `Material/Mika_MemoryLobby` 与 `Material/CHDDD`，按总宪法继续推进 Phase 0，不跨过验收门槛一次实现全部阶段。已阅读 Spine 主程序清单与 CHDDD 交接清单、动作及表情说明。

Spine 交付为 CH0069_home 完整记忆大厅骨架，已有本地待机、思考、触摸和 PCM 口型代码；原始动画数量不能等同于公开动作数量。3D 交付为 Generic 骨架、18 段身体动画及配套脸部采样，经过验证的是 Unity V7；Blender 后续修订和两个特殊表情另有交接资料，尚未全部同步到同一运行时模型。

当前工作：核对交接文件与依赖、编写 V1 设计、准备可重复的资源接入与播放验证。主工程继续使用 Unity 6000.3.7f1、Built-in 渲染管线；Spine 授权状态与外部服务配置另行确认。此记录不是资源已通过主程序验收的声明。

### 接入准备

71 个交接引用文件（包含 Spine 配套 .meta）SHA-256 与清单一致。CHDDD V7 unitypackage 中 73 项资源与松散源文件一致，已按宪法目录复制并保留 GUID。V1 方案写入 `v1_design.md`。

用户确认已有 Spine 授权，指定使用 spine-unity；已将交付 Runtime 和必需加载资源接入主工程，保留许可证声明，Pat10 仍禁用。补充实际需要的 Input System 1.18.0、Animation、ImageConversion、JsonSerialize、Physics2D 依赖，保留 Built-in。正在创建场景绑定和验证，尚未宣称播放成功。

用户要求 GPT-SoVITS 随项目内置并尽量精简。发现当前提供的是约 7.62 GiB 的 v2pro 整合压缩包，以及两个字节完全相同的语音模型 ZIP；模型目录实际为 v4，不能只凭整合包文件名假定版本兼容。下一步检查推理入口和必需权重，先制定运行时裁剪边界，不删除原始包。DeepSeek 按云 API 接口设计，尚未提供私有 Key。

### 播放与接口验证

主工程导入与编译完成。首次新增素材测试未等待 Home 切换锁释放，导航被防重复机制忽略；修正测试等待条件后，4 / 4 PlayMode 通过，涵盖既有生命周期和新增实际素材播放。Spine 完整骨架及网格加载、Thinking 不重启 Idle、触摸取消和卸载通过；3D 18 身体 / 18 面部状态及身体时长核对通过，原 Walk 实际驱动骨骼，摸头 Enter 中释放走 Exit 并回 Idle。补齐陪伴 / 桌宠摄像机 AudioListener。实机预览发现 3D 朝向背面，已将相机置于模型 +Z 正面并重跑图形检查。

DeepSeek 云端接口按官方当前协议实现 `deepseek-flash` SSE，包括心跳、Unicode 增量、DONE、HTTP 错误脱敏、截断失败、30 秒超时和取消；未接入完整对话 / 记忆业务。新增四个离线协议测试，EditMode 共 9 / 9、无图形 PlayMode 4 / 4 通过。环境中无 `GIFT_DEEPSEEK_API_KEY`，真实 API Probe 尚未运行。

用户确认先用中文。已恢复 ZIP 中的 GBK 中文文件名，预置约 220.8 MiB 角色模型和 7.62 秒 / 44.1 kHz 单声道参考 WAV，生成 v4 配置、启动 / `/tts` 验证脚本与 `voice_runtime.md`。整合包共 50,612 个文件，解压合计约 13.16 GiB；首轮保守推理选择约 9.04 GiB（含角色权重），仍需实际依赖验证后继续精简。该估算不是已打包产物，TTS 未启动、未推理通过。SQLite 和透明窗口仍未实施。

### 本轮收尾

最新 D3D11 图形 PlayMode 4 / 4 通过并正常退出，已检查更新后的正面 `pet.png` 与完整大厅 `companion.png`。当前 3D V7 配套材质偏粉，已登记为后续人工审阅和色调对齐事项，未修改冻结的源材质。71 份交接源文件哈希复查均未变化，正式角色 JSON 解析通过。验证日志未检出 C# / Shader 编译错误或运行异常。

README、架构、资产清单、正式角色 Manifest、Phase 0 状态和验证文档已同步。本轮完成第一版方案及素材播放接入；其余 Phase 0 项目保持明确未验收，不进入完整 Core / Memory 和最终 UI。可在 Unity 6000.3.7f1 打开 GiftCompanion，运行 Bootstrap 经 Home 检查两种表现身体。

## 2026-10-11：审批后继续第一部分（进行中）

用户已阅读第一版方案及日志并批准开始工作，同时补充日文语音训练包。按当前宪法阶段，先继续 Phase 0：复核新日文包、装配内置语音推理环境，推进 SQLite 和单窗口透明验证；真实 DeepSeek 请求仍需要本机私有 Key。中文默认不自动变更，日文作为新增资源单独登记。

当前正在检查归档内容、推理依赖与官方 SQLite / UniWindowController 接入方式。原始素材与制作稿继续保留，进度按实际执行结果更新。

### 日文资源与本地技术链准备

新日文 ZIP 与中文包的 SHA-256 已不同，包含独立 GPT / SoVITS 权重和 3.2 秒日语参考 WAV。已预置 `models/mika-ja` 与独立日文配置，中文默认继续保留。旧包相同的记录保留为当时状态，不套用于本次补充包。

从官方来源引入 SQLite 3.54.0 Windows x64 原生库（核对官方 SHA3）和 UniWindowController 0.9.8 固定提交，来源与哈希写入 `native_dependencies.json`。SQLite Phase 0 独立存储已实现版本化建表、WAL、串行写入、UTF-8 参数绑定和 FTS5，正在运行真实原生库测试；应用内原 RAM 服务暂未扩展成完整 Memory。Windows 单窗口原型与 EXE 验证脚本正在准备，实测前不标记通过。

用户明确稍后配置 DeepSeek Key，本轮继续其他验证。已准备本机 DPAPI 加密配置脚本；Unity .NET Standard 不提供 `ProtectedData`，改用 Windows 原生 DPAPI 解密，保持 Core 无 Unity 引用。首轮编译失败已定位并修复，等待重跑。

语音环境首轮解压完成主体，但 tar 将两个 Gradio 前端文件名中的方括号识别为模式，返回非零；这两项不属于推理依赖。正在补齐 API 间接引用的三个模块，并以真实启动和 `/tts` 结果判定可用性。原始 7z / ZIP 未修改。

### SQLite、中文语音与 Windows 透明窗口实测

修复 DPAPI 编译后，EditMode 11 / 11、PlayMode 4 / 4 通过。新增 SQLite 测试覆盖关闭重开后的中日文 / emoji 原文保持、FTS5 搜索、24 个并发写入串行完成和已取消请求不落库。Windows Player 再次使用同一原生库关闭重开并搜索成功，版本 3.54.0。

内置 Python 3.9 / Torch 2.0.0 + CUDA 11.8 能识别 RTX 4060；补齐 AP_BWE 的导入代码后，中文 v4 底模、角色 LoRA、vocoder、BERT / HuBERT 全部本地加载，`POST /tts` 返回 HTTP 200 和 318,764 字节 WAV。音频为 48 kHz 单声道、3.32 秒，峰值 20,816，RMS 约 3,006.53，非静音；首次请求约 11.87 秒。这是技术链验证，声音相似度和台词听感仍需试听。日文配置已另行启动验证，中文默认配置未改。

Windows Mono / D3D11 Development EXE 构建成功，关闭 Flip Model Swapchain。发现 Unity 的帧末分辨率设置与原生无边框切换存在时序冲突，已将样式切换延后并按实际客户区修正窗口尺寸。最终测试 `player_probe.json` 全部通过：同一 Unity 窗口 / 唯一 AppRoot，桌宠 600×500、Alpha 0、无边框、置顶；返回 Home 后 1280×720、Alpha 1、有边框、取消置顶。`window_composition.png` 仅截取测试程序客户区，后方临时纯色窗口的颜色真实透出，三个角点与背景 RGB 完全一致。临时测试程序和背景窗口已退出。

目前五项技术链已有实际验证，DeepSeek Streaming 等待用户稍后配置 Key；仍遵守 Phase 0 未全过不进入下一阶段。正在补充日文推理、可重复工具与语音包裁剪证据。

### 双语裁剪复测与本轮收尾

新日文独立模型真实 `/tts` 返回 314,924 字节、48 kHz 单声道、3.28 秒 WAV。引入 API 外层 import 记录器，保持交付 `api_v2.py` 不变；根据源码用途和实际加载情况移出 CMake / Gradio / ASR / 韩文词典 / 训练数据 / C++ 头文件等，共 701,299,013 字节（约 669 MiB）。PyAV 及 DLL、wordfreq、ipadic 实际加载，故保留。Torch / CUDA、G2PW、ONNX Runtime、BERT / HuBERT、v4 底模与 vocoder 未盲目裁剪。

裁剪后中日文分别干净启动、再次真实推理通过：中文约 10.70 秒、3.32 秒音频；日文约 8.38 秒、3.28 秒音频。两个 WAV 均非静音，六份角色权重 / 参考 WAV 哈希与源交付一致。当前 Runtime 32,197 文件、9,301,279,365 字节，约 8.66 GiB；与原整合包约 13.16 GiB 的原始文件合计分别记录，不把压缩体积当分发体积。双语验证通过后清理临时裁剪备份，原始素材和整合包保留。

启动 / 停止工具新增端口监听检查、就绪等待、UTF-8 日志、本地离线模型标志和进程身份核对。实测修复 PowerShell JSON 自动日期转换造成的启动时间比较问题，停止后等待进程真正退出，避免 GPU 清理期间端口仍占用。测试服务均已退出。PowerShell 工具语法检查与新增 Python 工具编译检查通过。

本机 Key 的 PowerShell DPAPI 加密 → Core 原生解密使用独立合成值验证通过，未读取或请求真实 Key。DeepSeek 用户明确稍后配置，本轮未发起真实云请求。新增命令行 `Test-DeepSeek.ps1` 与既有编辑器 Probe 都可在配置后完成最后验收。

Phase 0 状态、验证说明、架构、第一版设计、资产 Manifest 和两级 README 已同步。当前五项技术链通过，剩余真实 DeepSeek Streaming 待用户配置；没有进入 Phase 1 或提前实现完整对话 / Memory / VoiceService。中日文 WAV 和透明窗口截图可供审阅；声音听感与 3D 偏粉材质不视为已完成人工美术验收。

### 末次制作稿差异登记

最后针对旧交接记录的只读复核为 70 / 71 源文件未变化。唯一差异是 `Material/CHDDD/CHDDD_actions_v2.blend`，当前修改时间 2026-10-11 20:53:02，当前 SHA-256 与初次导入时不同。本轮工具未写入该路径；不能据此推断具体修改内容或来源。已保留现文件，未自动回滚、替换或重导；差异及新旧哈希写入 `source_recheck.json`。主工程已导入的 73 项 Unity V7 负载逐项复核仍全部与原始导入哈希一致，本轮技术链验证继续以冻结交付为准。SQLite 原生 DLL 哈希亦与官方下载记录一致。

## 2026-10-12：DeepSeek 联网验收与 Phase 1 方案

用户确认本机 Key 已配置，授权先联网验收，再提供 Phase 1 阶段方案。检查仅确认 DPAPI 文件存在，没有在聊天、日志或报告中输出 Key。为编辑器探针补充结构化验收报告、首段和完整请求计时，记录文本计数，不保存请求头或回复正文。

使用 Unity 6000.3.7f1 执行 `Tools/Test-DeepSeek.ps1`，解密本机配置并请求官方 `https://api.deepseek.com/chat/completions`，模型保持 `deepseek-flash`。真实 HTTPS / SSE 验收通过：3 个文本片段、5 个字符，首段 1712 ms，完整请求 2668 ms，正常收到 `[DONE]`；Unity 编译和探针进程退出码为 0。报告时间为 2026-10-12 04:31:53 UTC（北京时间 12:31:53），证据为 `Validation/Phase0/deepseek_probe.json` / `.txt` / `.log`。耗时仅代表本次短请求，不作为长期性能保证。

Phase 0 六项技术链至此全部实测通过。本轮没有重跑此前已通过的素材、双语 TTS、SQLite 和透明窗口检查，也未把真实接口探针通过等同于正式 LLMService / CharacterCore 对话已实现。DatabaseService 仍为 RAM，业务 LLM 与 Memory 仍为占位。

已依据宪法整理 `phase1_plan.md`，按 1A 配置 / 日志 / 正式数据库、1B 共享 LLM 与最小文字链、1C 场景切换 / 取消 / 退出、1D 集成验收划分检查点。明确正式库先落短期状态，Raw Messages / Session / FTS5 / PromptBuilder 留到 Phase 2；最终对话 UI、VoiceService 与 Agent Tools 留待对应阶段。另登记 Editor 与 Player 的数据 / DPAPI 目录不同，需要在 Phase 1 提供明确配置目标并验收 Windows EXE。

当前 Phase 1 仅提交方案供审批，没有开始实现；建议审批后先执行 1A。Phase 0 状态、验证说明、架构、记忆协议、第一版设计与两级 README 已同步，历史日志按当时结果保留。最终检查确认真实验收 JSON 字段完整、文档编码正常、无冲突 / 补丁标记，当前状态文档没有残留“等待 Key / 五项通过”的旧结论；Unity 探针进程已退出。

## 2026-10-12：按审批方案实施 Phase 1（进行中）

用户已阅读并批准 Phase 1 方案，要求按顺序逐步开始。先进入 1A，核对现有 RAM 数据库、配置 / 日志、测试和运行工具，准备正式短期状态库及非秘密配置持久化。检查点通过后更新状态与证据，再推进 1B / 1C / 1D；不重复索取同一阶段批准，不跨到 Phase 2。

### 1A：基础服务验证通过

正式 DatabaseService 改为原生 SQLite，schema v1 仅包含 app_state；异步串行执行事务、绑定参数、独立状态快照、版本检查和迁移前原生备份均已实现。ConfigService 支持非秘密 settings.json 校验 / 加载 / 原子替换；损坏原文在回退与重新保存后保留。Logger 增加幂等释放，Key 工具显式支持 Editor / Player，未复制真实 Key。

Unity 6000.3.7f1 EditMode 15 / 15 通过，证据归档为 `Validation/Phase1/1A.EditMode.xml` / `.log`。新增四项测试覆盖配置重开、损坏保留与非法值、SQLite 重开保持中日文 / emoji / 时间及独立快照、取消不写入、迁移冲突回滚及备份、更高版本拒绝、损坏库和不可用路径失败。配置脚本语法检查通过。开始推进 1B；Memory / 消息表尚未实施。

### 1B：共享文字链验证通过

统一 LLMService 已接入 DeepSeekApiClient，支持配置超时、角色只读身份输入、稳定错误码和客户端释放。CharacterCore 提供增量片段及最终结果，只有完整回复更新交互时间；请求互斥，暂停模式会取消并等待旧请求结束，不引入全局事件总线或完整 PromptBuilder。

EditMode 22 / 22 通过，涵盖 401 / 429 / 503、截断、取消、忙碌、暂停恢复和配置超时等；归档 `1B.EditMode.xml` / `.log`。正式 Core → LLMService → DeepSeek 真实短请求通过：3 片段 / 5 字符，首段 967 ms、完整 970 ms，交互时间提交成功。证据 `core_probe.json` / `.log`，没有记录 Key 或正文。这是本次短请求耗时，不代表长期性能。开始 1C，补齐模式提交边界与统一退出。

### 1C：生命周期验证通过

切换先暂停 / 取消旧请求，保存含目标 LastMode 的独立快照，再提交模式并完成 Unity 场景加载；持久化提交后不再用取消撤回已保存的状态。场景加载失败尝试恢复 Home 并同步数据库，恢复失败停止交互。退出统一收尾请求、保存状态、释放数据库 / HTTP / 日志 / Mutex；编辑器强制停止使用有界的尽力保存。启动异常只显示简短失败反馈，技术类型留在日志。

EditMode 23 / 23、PlayMode 5 / 5 通过；新增提交后取消竞态和实际场景加载故障 / Home 恢复 / 退出重开验证。既有三轮完整导航及实际素材播放继续通过。证据归档 `1C.EditMode.xml`、`1C.PlayMode.xml`。开始 1D：构建 Windows EXE，使用显式隔离验收目录，不覆盖正式用户状态；真实 Key 只通过测试子进程环境传入，不复制到构建或测试数据目录。

### 1D：首轮集成问题与修复（进行中）

最终集成 EditMode 24 / 24、PlayMode 6 / 6 通过，新增真实模式令牌取消请求与数据路径隔离测试。外层命令误读未设置的 LASTEXITCODE，曾在测试成功后阻止构建；已核对 XML 并单独启动构建，测试未误记失败。

首轮 Windows 构建虽返回 Succeeded，但报告 30 条 Texture2D → MonoScript 缓存资源引用错误，不能作为干净构建通过。首轮 Player 的正式状态库、唯一核心、真实 LLM 请求（首段 928 ms、完整 1028 ms）和退出保存通过，窗口检查失败。首轮报告保留为 `first_player_build.json` / `.log`、`first_player_write.json`。现将 Phase 1 构建改为 CleanBuildCache，并要求零构建错误；窗口探针由固定 350 ms 等待改为实际窗口状态检查，最多等待 8 秒，记录每次尺寸和原生标志，不放宽正确条件。修复后重建 / 复测前不标记 1D 完成。

### 1D：最终验收与 Phase 1 收尾

CleanBuildCache 后资源引用错误消失，最终 Windows Mono / D3D11 构建错误为 0，176,703,785 字节，未复制第二份语音 Runtime。收尾补齐启动期间退出时等待初始化收尾、先恢复状态再装配 LLM / Core 的顺序；相应最终代码重新通过 EditMode 24 / 24、PlayMode 6 / 6 与零错误构建。

最终 EXE 使用正式 AppRoot / CharacterCore / DatabaseService / LLMService，在 Player 数据根下随机隔离目录执行三轮 Home → Companion → Home → AgentPet → Home，唯一 Core / State、透明 / 无边框 / 置顶桌宠及普通 Home 恢复均通过。真实 DeepSeek 短请求首段 742 ms、完整 912 ms，3 个文本片段 / 5 字符并正常完成；关闭重启恢复状态且进入 Home，无 Key 时正常进入并播放两套素材，文字请求返回不可用结果。Write / Restore / NoKey 报告全部 passed，Key 只存在于联网测试子进程内，不复制到测试目录或 EXE。

透明合成复验首次发现截图脚本在 150% DPI 下将 600×500 物理客户区读成 400×333；新增线程 DPI 上下文并在结束时恢复。最终 EXE 再次复验通过，实际物理客户区 600×500，3 个角点匹配后方临时背景，Home 恢复通过。已查看更新截图，证据 `Validation/Phase1/Window/window_composition.json` / `.png` 与 `player_probe.json`；未扩大为任意缩放 / 多显示器 / 最终美术通过。测试窗口与临时背景均已退出。

SQLite 持久化后，编辑器场景回归也改为每项测试独立的 Validation 数据目录，避免未来回归覆盖真实开发状态；该入口只在 UNITY_EDITOR 编译，不改变已验收 Player 逻辑。受影响 PlayMode 6 / 6 再次通过。最初 Smoke Test 自行创建的 Editor memory.db 经只读核对仅含已知测试话题、无真实交互时间后，移到 UserData/Validation 留存；未删除数据库，未移动或修改 Secrets。之后普通编辑器首次运行会建立正式状态库。

全部 Phase 1 PowerShell 工具语法通过；Core 无 UnityEngine 引用，构建中没有 DPAPI 秘密文件或测试程序集。综合证据校验后写入 `Validation/Phase1/acceptance.json`，记录实际测试结果及交付 Core / Runtime DLL 的 SHA-256。架构、两级 README、阶段方案 / 状态、验证说明、V1 设计与记忆协议已同步。**Phase 1 按批准方案完成，Phase 2 尚未开始。** 完整消息 / 记忆、正式对话 UI、VoiceService 与 Agent Tools 仍留待对应阶段。

## 2026-10-12：Phase 1 用户验收与 Phase 2 方案

用户确认当前阶段已验收，整体效果符合预期，并反馈 Spine 有些过曝。该问题登记到资产清单和 Phase 3 Companion 视觉对齐事项，当前未诊断原因，不预判 Shader / 贴图 / 颜色空间，也未修改素材或既定渲染基线。

本轮对照宪法第 21—27、40、49、51 节与实际 MemoryService / DatabaseService / LLMService、Phase 1 状态，整理 `phase2_plan.md` 和待审批的 `phase2_status.md`。现有 schema v1 仅有 app_state、Memory 仍为占位，不能将方案当成功能已经完成。

方案按 2A 原始消息 / 会话 / 迁移 → 2B 后台提取 / 事实 / 未完成事项 → 2C FTS5 / PromptBuilder → 2D 摘要 / 纠错 / 遗忘 → 2E 跨模式与重启验收推进。强调复用数据库串行写队列、完整回复后异步提取、中文检索实测、受限上下文，以及纠错 / 遗忘后旧原文、摘要与待处理任务不能重新注入过期内容。立即切换模式时使用受限近期对话衔接，不等待云提取完成。

两级 README、当前架构、V1 设计与记忆协议同步指向本方案；Phase 1 实测证据继续保留。本轮只更新文档，未调用 DeepSeek、未迁移正式数据库、未实现 Phase 2 或重新导入角色资源。方案等待用户审批，批准后从 2A 开始。

文档静态核对通过：UTF-8 正常，无冲突 / 补丁残留，代码围栏闭合，新方案和状态路径存在，各入口的待审批表述一致。代码与工程版本仍保持 Phase 1 基线；本轮文档变更不重跑 Unity 测试或构建，不新增功能通过声明。

## 2026-10-13：按审批方案实施 Phase 2（进行中）

用户已阅读并批准 Phase 2 方案，授权按 2A → 2E 顺序推进。本轮先实现 2A：消息 / 会话、schema v1 → v2 迁移和完整轮次事务；通过隔离数据库及 Core 故障测试后再进入记忆提取，不重复索取本阶段批准。原始素材、渲染设置与语音 Runtime 保持现有基线。

### 2A：原始对话与会话通过

DatabaseService 增加 schema v2、messages / sessions 和唯一 request_id + role 约束，复用单连接串行事务。CharacterCore 先保存用户原文，再发 HTTP；完整回复、轮次状态、提取 pending 和 LastInteractionAt 原子提交，存储失败不冒充对话成功。取消 / 截断保留用户原文及状态，不保存不完整 assistant；离开模式和退出关闭会话，启动识别中断轮次，不重发云请求。

Unity 6000.3.7f1 EditMode 29 / 29、PlayMode 6 / 6 通过，证据 `Validation/Phase2/2A.EditMode.xml` / `.log`、`2A.PlayMode.xml` / `.log`。新增测试覆盖 v1 状态 / 原生备份保留、两种来源原文重开、重复完成、失败事务回滚、先落库后 HTTP、截断、取消和升级冲突。首次编译因测试程序集缺少 Newtonsoft 预编译引用失败；修正后首轮测试 28 / 29，剩余为取消子类型断言问题，已修正并完整复测通过；失败日志 / XML 保留。

2A 验收仅使用 Validation 隔离库，没有读取或迁移用户正式聊天库，也未调用真实 API。开始 2B：正式后台提取、事实 / 事项与共享 MemoryService。

### 2B：后台记忆提取通过

schema v3 增加 memories / user_facts / open_loops、来源关联、版本与排除元数据，继续复用同一连接和写队列。共享 MemoryService 在完整轮次后后台提取，每项来源须指向本轮 user，evidence 必须逐字存在于用户原文，记录内容 / 值必须在证据中；JSON 多余字段、非法范围、未知事项 ID 均拒绝。明确事实去重，纠错保留 superseded 历史，事项只能按已知候选完成。

LLMService 增加低输出预算非流式 JSON 请求，模型保持 deepseek-flash，校验正常 finish_reason；SSE 也拒绝已声明 length 等非正常结束。格式与字段依据官方 Chat Completions 文档（https://api-docs.deepseek.com/api/create-chat-completion/），不采用新的云服务。后台最多一项，前台开始可取消后台并保留 pending；失败须显式重试，关闭程序可收尾，不持有 Unity 场景对象。

Unity EditMode 33 / 33 通过，证据 `Validation/Phase2/2B.EditMode.xml` / `.log`；新增四项测试覆盖来源 / 证据 / 置信度拒绝、重复事实与纠错历史、事项解决、后台取消和失败重试。尚未做本阶段真实云请求或最终 Windows 构建。开始 2C：中文 / 日文 FTS5 检索、受限上下文和 PromptBuilder。

### 2C：共享检索与 PromptBuilder 通过，按用户要求暂停

用户在本检查点进行中明确要求“进行完 2C 阶段后暂时停止，保存当前进度”。已将后续边界收缩到 2C 完成与验证，不进入 2D / 2E。

schema v4 新增共享 FTS5 索引，并提供原有活跃记录回填逻辑。使用内置 unicode61，加中日文单字 / 双字预处理和独立英文标识符；查询采用有界引用词 OR 表达式及参数绑定。评分保留 0.60 / 0.25 / 0.15，bm25 取负并按候选最大值归一化，recency 使用 30 天指数衰减，按时间与 ID 稳定处理并列。实现依据官方 SQLite FTS5 文档（https://www.sqlite.org/fts5.html）并以实际原生库检索结果验证。

PromptBuilder 已接入正式 CharacterCore / LLMService：按宪法顺序组装只读身份、关系、最多 20 条事实 / 5 条事件 / 5 条事项、摘要插槽、6 个完整近期轮次和当前消息；总上限 12,000 字符，历史保持真实 user / assistant 角色，当前输入一次。来源依赖及上下文版本随完整回复事务核对 / 保存。提取未完成时，两个模式仍可使用共享近期对话；原始消息仍为事实来源。

2C 首次 EditMode 40 / 40、PlayMode 6 / 6 通过；收尾修正连续中英文句子中英文代号的分词，并补充上下文版本改变时拒绝过期回复的事务测试。最终 Unity 6000.3.7f1 EditMode **41 / 41**、PlayMode **6 / 6** 全部通过，证据 `Validation/Phase2/2C.EditMode.xml` / `.log`、`2C.PlayMode.xml` / `.log`。验证自然中文、日文、单字与混合代号搜索、长期事件移出近期窗口后的命中 / 重开、评分与 active 过滤、上下文预算和跨模式衔接，回归保留既有实际素材检查。

代码与 Unity 生成的 .meta 已保存在工程中；新增 `phase2_checkpoint.md` 保存续接入口、数据库版本、实测边界和下一步事项，阶段方案 / 状态、架构、记忆协议、V1 设计、验证说明与两级 README 已同步。当前没有 Git 仓库，保存以工作区文件和 `Validation/Phase2/checkpoint.json` 源码 / 测试哈希为依据，不伪造提交记录。

**已按用户要求暂停在 2C。** 2D 的实际摘要任务、显式遗忘及完整纠错 / 原文 / 摘要 / 旧任务回流治理未实现；当前仅有相关字段和来源基础。2E 的真实 DeepSeek 记忆联调、开发输入入口与新 Windows 构建未开始。本轮只测试隔离数据库，没有迁移用户正式库、修改 Key / 素材 / 渲染设置或启动语音 Runtime；现有 EXE 仍为 Phase 1。用户明确恢复后从 2D 开始。

### 2026-10-13：用户明确恢复，从 2D 继续

核对 2C XML，EditMode 41 / 41、PlayMode 6 / 6 均通过，当前无 Unity 测试进程占用。上次中断发生在保存收尾：checkpoint.json 尚未生成，已在修改 2D 代码前补存 2C 源码 / 测试哈希；标明本次补存时间，不伪称中断前已生成。用户本次继续指令撤销此前暂停，先完成摘要、显式遗忘及纠错回流治理，验证后再进入 2E。

### 2D：摘要、纠错与遗忘通过

schema v5 增加摘要来源 / 记忆版本和来源查询索引。模式退出、累计 10 轮或 8,000 字符触发后台摘要；原文分批、完整轮次有界取样，保存覆盖位置及来源，不等待云摘要才退出。取消的任务恢复 pending，失败显式重试，旧版本摘要拒绝提交。

Core 提供明确记录 ID / 轮次 ID 的遗忘接口，先取消并收尾前台请求，再执行同连接事务。遗忘保留原文，将目标事实历史 / 关联记录禁用并移出索引；沿 prompt_dependencies 排除角色复述，抑制旧提取，失效摘要并用允许的原文重建。原文同条消息含独立事实时保留其结构化内容，不再发送整段证据。基础纠错也复用此来源治理，旧摘要 / 复述不覆盖新值。

Unity 6000.3.7f1 EditMode 47 / 47、PlayMode 6 / 6 通过，证据 `Validation/Phase2/2D.EditMode.xml` / `.log`、`2D.PlayMode.xml` / `.log`。新增六项测试覆盖触发 / 增量 / 来源 / 重开、原文保留与独立事实、遗忘 pending 轮次后防复活 / 新陈述可重新记住、纠错与旧摘要竞态、离开模式不等网络及摘要取消恢复、摘要格式拒绝。开始 2E，准备少量真实云调用、显式开发入口与新版 Windows 验收。

### 2E：真实 Core 前置验收与最终构建准备

增加共享验收 Runner，编辑器与 Player 都通过正式 CharacterCore / MemoryService / DatabaseService 执行；随机识别码只存在隔离数据库，不写入报告。验证双向回忆、移出近期窗口的长期事实、明确纠错、遗忘上下文排除、原文保留与独立事实，再关闭重开验证持久事实，无 Key 仍能读取本地库。

首轮验收入口缺少 ICharacterCore.Memory 属性导致编译失败，已补齐并保留 `core_first_compile.log`。随后真实 Write / Restore 均通过，NoKey 因上一轮 Restore 回复重新进入近期上下文，outsideRecent 条件为 false；这是测试上下文准备问题，原始 `first_core_*` 报告留存。修正为无 Key 检查前在隔离库填充不触发云提取的普通轮次，不放宽验收条件。真实 Core Write / Restore / NoKey 全部通过，证据 `core_acceptance.json` / `core_write.json` / `core_restore.json` / `core_nokey.json`；该轮记录 schema v5。

新增显式开发面板：编辑器 Play 时菜单 `Gift Companion > Phase 2 > Open Development Panel`，Player 使用 `-phase2-dev`；普通启动隐藏，UI 仅调用 Core / MemoryService，不直接访问 SQLite / HTTP。可发送文字、查看事实 / 事件 / 事项、按确定记录遗忘、标记上一轮不记忆、显式重试。完整产品对话界面仍留到 Phase 3。

收尾复核发现同一原文含独立事实时，按原始消息追踪结构化事实回答会过度连带排除；细化 schema v6 的 prompt_record_dependencies，原文 / 摘要使用消息来源，结构化记忆使用具体记录引用。遗忘目标可追溯关联回复，独立事实的新回复不被旧来源连带排除。最终 EditMode **50 / 50**、PlayMode **7 / 7** 通过，含 v4 → v6 备份 / 数据保留、在途请求遗忘及后续请求恢复、独立事实后续回复、开发面板默认隐藏 / 显式启用。正在干净构建 Phase 2 Windows，最终 EXE 将再次做真实验收；未将前置 Core 结果冒充最终 Player 通过。

### 2E：最终验收与 Phase 2 收尾

最后补充大小写敏感事实值更正：只归一化事实键，事实值按原文精确比较，Moon7 → MOON7 保留旧版本并创建新的 active；避免标识符被大小写归一化吞掉。最终 EditMode **51 / 51**、PlayMode **7 / 7** 通过。修正已纳入最终干净 Windows Mono / D3D11 构建，错误 **0**，176,876,462 字节（约 176.9 MB），语音 Runtime 未复制第二份。

最终 EXE 再次使用随机 Validation 数据目录实测 Write / Restore / NoKey，三项 passed。双向跨模式、近期窗口外长期事实、明确纠错、遗忘后请求上下文排除、原文 / 独立事实保留、重开先到 Home、无 Key 本地记忆、唯一根和退出保存均通过；报告为 `player_write.json`、`player_restore.json`、`player_nokey.json`，实际隔离位置记于 `player_run.json`。Key 仅在联网子进程环境中使用，未复制到验收目录或构建。

对最终构建复验真实窗口合成，`Window/player_probe.json` / `window_composition.json` 均 passed。桌宠物理客户区 600×500，透明 / 无边框 / 置顶，3 个角点匹配临时纯色背景；返回 Home 恢复 1280×720 普通窗口。已查看最终截图，保持 V7 现有偏粉材质，不宣称最终美术或多显示器全面通过；测试程序和背景窗口正常退出。

综合证据核对后生成 `Validation/Phase2/acceptance.json`：记录最终 51 / 51、7 / 7、六份构建 / Player / 窗口报告、源码及 EXE / Core / Runtime DLL 的 SHA-256，schema v6。构建中 DPAPI 文件和项目测试 DLL 数量均为 0；Core 无 Unity 引用，PowerShell 五个 Phase 2 工具语法和文档静态检查通过。前置 Core 联网报告保留实际 schema v5，最终 schema v6 以后续 EXE 验收为准；不覆盖 2C 历史暂停快照。

新增 `Tools/Start-Phase2Dev.ps1` 与 `Docs/phase2_delivery.md`：可沿用用户已配置 Editor Key 到交互 EXE 的子进程环境，不显示 Key、不复制秘密文件，无需为本次体验重复输入。显式 `-UsePlayerKey` 使用独立 Player 配置；普通启动隐藏开发面板。启动脚本仅保存并通过语法核对，本轮没有额外启动正式数据目录中的交互实例。

阶段方案 / 状态、当前架构、记忆协议、V1 设计、验证说明与两级 README 已同步。**Phase 2 已按审批顺序完成开发和技术验收，供用户人工体验。** 未进入 Phase 3，Spine 过曝继续留到该阶段；正式 UI / 角色表现、语音、Agent Tools 和最终分发仍保持各自边界。本轮自动验收未迁移用户正式库、未修改原始素材、渲染设置或用户 Key 文件，也未启动 TTS。

## 2026-10-13：Phase 2 用户验收与 Phase 3 方案

用户确认当前阶段验收完成、整体效果符合预期，并要求先提供 Phase 3 阶段方案。本轮核对项目总宪法、阶段交接、最终验收证据与实际 Spine / Core / 配置 / 历史接口；Phase 2 的 51 / 51、7 / 7 及最终 Player 结果继续作为已验收基线，原始证据与 2C 快照不改写。

设计对齐已收到明确回复：按 Mika 与老师的原作关系基调设计；允许小幅调整原大厅构图与界面布局；BGM 先保留空播放列表，稍后补充素材。这些决定写入方案与状态，但 Identity / Relationship 资产尚未填写，实际程序仍为 Phase 2。

新增 `phase3_plan.md` 与 `phase3_status.md`，按 3A 视觉 / 人设 / 映射 → 3B META / Core → 3C 状态 / 输入 / 字幕 / Clock → 3D MomoTalk / 设置 / BGM → 3E 集成与 Windows 验收安排。过曝在第一步基于现有 Built-in / Gamma / D3D11 排查，不预判原因；三个 pat 预设和 52 个原作动画不当作完整全局情绪 / 动作能力。正式语音队列、口型同步、Ducking 和历史复听仍属于 Phase 4。

方案明确首行 META 的有界流式解析、缺失 / 非法头降级与原始模型回复保留；拟定 schema v7 表现记录保存正文投影，复用现有提交事务、来源和遗忘规则，旧 v6 历史不改写。新增消息替换须取消并等待旧轮收尾，UI 不直接访问 HTTP / SQLite，MomoTalk 分批读取共享历史。

同步 Phase 2 用户验收状态、两级 README、当前架构、V1 设计、角色协议和素材清单。角色协议原有“始终返回 LLMNotConfigured”的占位描述已过时，现按实际 Phase 2 对话能力更正，并明确区分 Phase 3 计划。**本轮只提交方案，等待审批后从 3A 开始。** 未修改代码 / 场景 / 素材，未调用 API、运行 Unity 或迁移数据库。

文档静态检查通过：11 份更新文档 UTF-8 正常、围栏闭合、无冲突标记，19 个本地 Markdown 链接有效。对 Phase 2 验收清单复核，65 份源码、3 份构建文件与 6 份报告 SHA-256 全部一致。本轮为纯文档变更，没有重跑 Unity 测试，也没有增加 Phase 3 功能通过声明。

## 2026-10-14：按批准方案实施 Phase 3（进行中）

用户明确授权先按 phase3_plan 实现，再依据效果精修。本轮按 3A → 3E 推进，不重复申请阶段批准。首先核对实际代码和资源边界，保留 Phase 2 交付与证据；实现与验收使用隔离数据，不操作用户正在制作的 Blender 文件。

3A 已整理 Mika / 老师的只读初版配置和明确的八情绪 fallback，未将三个 pat 预设冒充完整情绪映射。取得官方 Noto Sans CJK SC 字体和 OFL 许可。检查到加载器缺少运行时 Multiply / Screen 材质装配，已增加受控前后截图入口；是否为实际过曝原因等待同帧统计和截图，尚不标记视觉通过。

### 3A / 3B：视觉基线与协议检查通过

3A 同帧核对确认骨架只有 Normal 284 / Additive 10，无 Multiply / Screen；对应试验没有改变画面，已撤回加载器改动。改为 Companion 相机独立曝光 / 高光压缩，接近纯白像素 22,452 → 1,632，平均亮度约 0.630 → 0.589；已查看前后图。原始资源、Gamma 和 D3D11 不变，最终观感保留人工精修。官方中文字体检查通过。

3B 实现有界 META 增量解析、元信息 / 正文事件和 schema v7 assistant_presentations。messages.content 保留模型原文，历史 / 近期上下文 / 后台提取 / 摘要使用正文投影，旧 v6 原文不猜测标签。完整正文、表现与 Mood 同一事务保存；失败轮次不更新情绪。初轮测试因接口替身和旧 v4 夹具未去掉新增表、旧测试把元信息当文本片段而失败，修正测试准备和事件分类后完整回归 EditMode 55 / 55、PlayMode 7 / 7 通过，失败报告留存。开始 3C 正式交互。

### 3C：正式文字交互通过；进入 3D

正式 Home / Companion 界面使用中文字体。Enter 显示输入，流式正文驱动字幕，Esc 菜单收纳导航，Clock 常驻；TMP 正文关闭富文本。加入 IME 组合与结束帧保护，发送新消息先取消并等待上一轮，场景退出取消旧回调，触摸和 UI 接管相互协调。无语音时保持静态嘴部，未启动 GPT-SoVITS。

EditMode 56 / 56、PlayMode 9 / 9 通过；新增输入保护、无 Key 草稿 / 无伪回复、旧轮取消和迟到污染、返回 Home 清理检查。真实操作系统中文输入法与最终观感仍留人工体验，不用逻辑测试冒充完整 IME 实机验收。

3D 已实现纯 C# 历史分页服务、双模式历史正文、明确轮次 / 记录遗忘入口、非秘密音量 / 字号设置、空列表安全静音和有限 BGM 播放器，正在验证同时间游标、持久设置与合成音频的循环 / 释放。播放列表仍为空，不交付测试声音作正式素材。

### 3D / 3E：集成进展与发布画面问题

3D EditMode 57 / 57、PlayMode 10 / 10 通过，覆盖同时间游标分页、共享正文投影、遗忘后历史保留、设置恢复和 BGM 合成音频循环 / 释放。随后增加临时情绪在取消 / 存储失败时不保存的检查，最后完整回归为 EditMode 59 / 59、PlayMode 10 / 10。

首次 Player 编译因未启用 ScreenCapture 模块失败，改用已有 Texture2D.ReadPixels，不增加依赖；失败报告保留。较早 Release 构建零错误、133,037,395 字节，真实 UI Write / NoKey 和记忆 Write / Restore / NoKey 全部 passed，分别使用 ui_run.json / player_run.json 记录的随机隔离库。没有将 Key 写入报告或复制到目录。

查看真实 EXE 图像发现发布版 Spine 严重高光异常、历史小按钮文字因高度不足不可见，功能 flags 通过不能冒充画面通过。旧画面 / 构建证据另存 FirstUI。已增加按钮字号自动缩放，以及显式 Straight Alpha 候选 Shader 和不联网 Visual 对比模式；主程序仍使用原 Shader。原材质 runtime keyword 为 _STRAIGHT_ALPHA_INPUT、参数为 1，候选 Shader 可用，但不能据此确定根因或宣称修复。

最新检查 EXE 零错误、133,048,987 字节、14.491 秒，包含上述后续改动；只运行 Visual 模式，生成 release_original_shader.png / release_explicit_alpha.png / release_ldr.png 与 release_shader_audit.json。用户暂停前尚未查看这三个新对比图，未重跑该 EXE 的普通 UI / 记忆 / 窗口验收。最后完整测试发生在这些 UI / Shader / 探针修改前，没有再改 Core。Phase 3 尚未生成综合 acceptance.json 或最终 delivery 文档。

### 2026-10-14：按用户明确要求暂停并保存

收到用户“当前先暂时停止任务，写好当前进度与交接记录”的指令后，停止开发、构建、联网和画面诊断，仅整理文档 / 文件哈希。新增 phase3_checkpoint.md，详细记录已实现内容、报告与二进制对应关系、未审阅的渲染证据、许可 / 文档收尾项及恢复顺序；phase3_status、方案、视觉说明、两级 README 和架构入口同步暂停状态。恢复须先查看发布对比图，不从头重做 3A—3D，也不进入 Phase 4。

Phase 0 copiedFiles 862 个导入文件哈希复核全部一致，原始 Material / 用户 Blender 文件未修改。当前构建只已复制字体 OFL；字体版权 Notice 与 Spine Runtime 许可证已在 Validation/Phase3 保存，仍需恢复后补入稳定打包流程。暂停时未发现本任务遗留的 Unity / GiftCompanion 进程，没有待等待的执行命令。未迁移正式用户数据库，Phase 2 历史证据保持原样；等待用户明确恢复。

暂停快照已保存到 Validation/Phase3/checkpoint.json：233 个源码 / 工程 / 工具文件、3 个构建文件、70 个证据文件，306 个 SHA-256 读回一致。8 份文档、13 个本地链接、5 个 PowerShell 工具静态语法检查通过。暂停后只执行文件保存与只读校验，没有运行 Unity、构建或联网任务。

### 2026-10-14：用户恢复任务，继续 3E 发布视觉修正

已按最新指令恢复，不重做 3A—3D，不进入 Phase 4。首先查看暂停的三张对比图：原 Shader 存在大面积异常白色覆盖，显式 Straight Alpha 后角色 / 背景恢复，关闭 HDR 只带来较小差异。原 Spine/Skeleton 已在 Always Included 中；构建日志 Normal pass 的 Full variant space 为 1，尚不能精确断言 Unity 内部根因。

采用场景专用、显式 RGB × Alpha Shader，绑定加载器创建的运行时材质；保留冻结源文件、原图集 / 骨架、Gamma、HDR 与第三方加载器。Shader 使用浮点颜色，保持 PMA 顶点色与 Normal / Additive 混合。对比探针改为同一 Idle 1.25 秒，原暂停渲染证据另存 PausedRendering。时钟 / 临时提示增加低干扰底色，字体及 Spine 许可加入稳定打包流程。上述变更待最终测试与发布图复核，尚不宣称阶段完成。

### 3E：恢复后最终技术验收与交付

恢复后回归 EditMode 59/59、PlayMode 10/10 全部通过；Core / Runtime 的渲染适配及 UI 变更已在测试前保存，后续仅增加编辑器视觉取证 / 离线汇总工具与文档，没有再改运行时代码。最终 Windows Release / Mono / D3D11 构建零错误，133,051,035 字节，16.2769579 秒；该 EXE 真实 UI Write / NoKey、记忆 Write / Restore / NoKey、Windows 窗口合成 / Home 恢复全部通过。Key 不打印、不复制，数据全部隔离，未打开正式聊天库。

已查看修正后的发布同帧、正常字幕 / 历史 / 缩放图与桌宠透明合成。原 Shader / 显式 Alpha / LDR 同 Idle 1.25 秒接近纯白像素分别 254,655 / 1,744 / 112；修正编辑器图 1,632。主程序保留 HDR，显式 Alpha 后白色覆盖消失，小按钮文字与时钟 / 提示底色正常；不宣称精确定位 Unity 内部根因或替用户完成观感验收。

透明客户区 600×500 四角匹配背景，返回 Home 恢复普通 1280×720；真实 UI 核对 1280×720 / 1024×768。字体 OFL、版权 Notice、Spine Runtime 许可已随包且哈希一致。冻结导入的 862 个文件全部一致，构建中 DPAPI 与项目测试 DLL 为 0，Core 不引用 Unity。新增离线 Save-Phase3Acceptance.py 汇总 XML / JSON / 程序 / 源码 / 许可 / 截图哈希，不运行网络或用户数据库。

更新 phase3_delivery、方案 / 状态、architecture、角色 / 记忆协议、v1_design、visual、validation 和两级 README；历史暂停交接仅加恢复说明，checkpoint.json 原样保留。启动入口 Tools/Start-Phase3.ps1 已保存，沿用 Editor Key 到子进程环境，普通界面不显示开发面板；本轮未额外启动正式数据的交互实例。

离线图像统计没有新增依赖：本机未安装 PIL，使用标准库解码 PNG。两次临时 .NET Bitmap 编译引用尝试未生成报告或改动程序；最终标准库统计与编辑器同帧结果一致。Phase 3 初版开发 / 技术验收完成，等待用户体验明暗、Mika 口吻、字幕节奏、实际 IME 与触摸后精修；BGM 暂空、八情绪明确回退、模型 action 仅 none。未启动 GPT-SoVITS，未进入 Phase 4 / Phase 5。

最终文档静态核对：13 份文档 UTF-8 / 冲突 / 围栏检查、29 个本地 Markdown 链接、6 个 Phase 3 PowerShell 工具 AST 语法通过。acceptance.json 区分 phase3TechnicalComplete 与 userAcceptance=pending；保存最终源码 / 程序 / 证据 / 许可证 / 截图 / 文档哈希，供后续体验精修恢复。所有任务测试进程已退出，不保留运行中的构建、云请求或 TTS。

## 2026-10-15：按用户补充要求实施 Phase 3F（资料文件与长期回顾）

用户确认当前最低限度 UI / 普通对话可用，后续再美化。现授权补充：文件化歌单与单曲循环 / 手动切歌 / 淡入淡出 / 玩家添加歌曲，外部 Spine 空间接口与选择，玩家可修改的人物卡，以及从首次使用到现在的关系记忆回顾，数据库与 Markdown 双份保存。日记是长期总结的沉浸表达，由玩家主动生成，不限于当天；只依据实际对话，不自动改写人格或虚构关系。用户已退出旧版以便新版本验证。

本次作为 Phase 3F 补充，保留 Phase3 已验收构建，输出独立 Phase3Plus；不接入 Phase4 TTS / Ducking / 复听、Phase5 工具或 Phase6 跨机分发。普通玩家通过菜单管理文件，无需编辑 JSON 或使用 Unity；保留独立 JSON / Markdown、备份、类型化后端接口、资料目录和可读维护报告。人物卡仅允许玩家主动修改，Memory / LLM 仍无写入权限，固定角色 ID 保护同一记忆身份。

BGM 来源 Material/Spine_Memory_BGM，当前为 Daily Routine 247.mp4 与 You are my princess.mp4。使用已有内置 ffmpeg 提取音轨及控制音量，不复制视频到运行包，不新增音频框架；正式播放器加载本地文件。Spine 新资源尚未提供，先完成兼容 4.2 的文件清单 / 导入校验 / 切换入口与基础 Idle 安全回退；高级表情 / 互动须后续资源适配，不开放任意动画给模型。

### Phase 3F 当前实现与测试记录（暂停保存）

已保存 AtomicDocument / ContentLibrary 独立文件服务、相对路径歌单 / 空间注册表、人物卡主动保存 / 重读与同 ID Core 替换、BgmFilePlayer 流式本地读取及双源手动淡化 / 同曲循环包络、动态空间加载、schema v8 日记 / 来源 / 待导出状态、全历史滚动回顾、MD 导出与遗忘 / 更正收起。新增资料管理、音乐、空间、人物卡、日记与维护面板，Windows 原生文件选择及说明入口。后续 Phase 4 / 5 / 6 内容未提前实施。

两份视频实际为 4051.65 / 5984.02 秒，提取完整 OGG 后为 68,363,302 / 100,029,009 字节，按 -18 LUFS / -2 dBTP 处理，不擅自截短，不复制视频或 ffmpeg 到计划构建。新增两个 Unity 自带 WebRequest 音频模块，没有第三方框架。原始素材 / 冻结制作文件保持原样，正式用户库未迁移，未读取本机 Key 执行云验收。

First 编译失败为 ILogger 类型冲突，已修正；Second EditMode 65/66，空空间清单首个导入未设置 SelectedSpaceId，已修正。Third EditMode 66/66、PlayMode 10/12：固定 15 帧不足以验证真实音频循环，以及音乐加载完成可能重新触发正在淡出的播放，导致快速 Home 导航超时。改为实际时钟断言，并在离场取消 pending load / 防止空源预升音量，Fourth PlayMode 12/12 通过。

Fourth 之后又增加 CharacterCardIssue 提示、维护页配置 / 程序 SHA-256、Spine metadata / 完整数据与待机预校验，尚未重新编译或回归。发现但尚未修正的边界包括初始化不必要保存可能覆盖损坏配置、选择当前项早退未修复持久选择、外部删除已导出 MD 不会自动重建、后端并发导入待审查。没有构建 Phase3Plus EXE，没有编写 PlayerProbe 或 -phase3plus-probe 隔离允许项，已保存的 Player 脚本暂不可运行；真实云日记、发布画面 / 窗口 / 文件选择器仍待验收。

### 2026-10-15：用户明确暂停，保存 checkpoints

收到“现在请暂时停止任务，根据当前进度写好 checkpoints”后停止开发 / 测试 / 构建 / 联网 / 音频处理，仅保存 phase3plus_checkpoint.md、phase3plus_status.md、方案暂停标注和导航。交接明确 66/66、12/12 与最新源码不是完整对应，未把未构建版本当作交付。旧 Phase3 EXE 与旧验收报告保留，当前没有本任务残留 Unity / GiftCompanion / ffmpeg 进程。

本次暂停不修复已知待办，不执行保存的工具脚本，只写哈希并做静态校验。等待用户明确恢复，从最新交接继续；不从头重做已完成内容，不进入 Phase4。

### 2026-10-16：用户恢复 Phase 3F

从 10-15 checkpoint 恢复，保留原快照。修正 BGM / 空间仅选择改变时保存，明确选择当前项可修复后端选项；导入条目在写锁内从最新清单追加，避免并发覆盖；日记检查缺失 Markdown 并补回，已存在成功导出不在普通读取时重写，遗忘仍覆盖收起正文且不留导出备份。导出文件名采用 UTC + ID，避免系统时区改变产生副本。增加相关回归断言。

已补齐 Phase3PlusPlayerProbe、AppRoot 挂载及 UserDataPaths 隔离允许项；新探针缺少或越界 validation-data 时拒绝启动。计划用随机 Validation 数据验证真实音频 / DeepSeek / 日记，不使用正式历史；最新测试与构建仍待执行。

恢复首轮 Resume EditMode 编译发现暂停前 Spine Atlas 被写成 using 声明，但当前 Runtime 未实现 IDisposable；改为显式 finally Dispose。Resume2 EditMode 67/67、PlayMode 12/12 全通过，含人物卡错误提示、Spine 完整预解析、并发导入、无变化选择保留损坏文件、Markdown 删除补回及普通同步保留批注。发布探针为 Windows Player 条件编译，其完整验证将随构建执行；仅补等待实际音频开始播放避免探针帧时序误报。玩家说明增加导出副本与时长范围。

独立 Windows 构建零错误（301,562,986 字节）后，第一次真实 Player 验收在选歌保存阶段发现加密 AppData 目录的 File.Replace 拒绝访问；未到联网步骤。在同一隔离目录的命名测试文件复现：ReplaceFile 元数据合并失败，忽略元数据亦不能删除替换文件，而同目录覆盖重命名成功。AtomicDocument 的 Windows 路径改为同目录 MoveFileExW 原子替换，先原子更新上一版备份，再提交当前文件；日记无备份策略不变。settings.json 也统一使用同一保存服务。没有删除正式资料或改 ACL / 关闭目录加密。需重新回归、构建和 Player 验证。

第二次 Player 仍在选歌保存阶段失败，原生错误码为 ERROR_NOT_SAME_DEVICE (17)，同目录 MoveFileEx 的两组标志均复现；不能仅归因为普通 EFS 元数据。当前执行环境的加密 / 重定向文件层不支持该同盘原子重命名。仅针对错误 17 增加带 SHA-256 校验的 .pending 写前副本：先完整落盘 / flush，再覆盖当前文件，读取 / 重启可恢复完整待提交内容；其他权限错误继续失败，不更改权限或文件加密。普通 NTFS 仍走原子替换，特殊文件层的覆盖阶段不宣称原子。新增模拟中断 / 校验损坏恢复测试，再回归与发布验证。

Final3 EditMode 68/68、PlayMode 12/12 通过。另在先前失败的同一 AppData 验收目录直接调用最新纯 C# Core 文件服务，确认二次保存 / 上一版备份及无备份导出成功，测试文件已清理。file_provider_review.json 记录错误 17 和恢复分支的实际限制；无需改 ACL 或取消加密。重新构建后继续发布验收。

第三次发布验证已通过两首完整 OGG 解码、真实长轨末尾同曲循环、导入 WAV 不自动切换 / 播放循环、人物卡保存与同 Core 应用、通用空间副本导入与切回、真实 DeepSeek 对话及原文 / 正文投影。日记未生成的根因是共用 JSON transport 仍限制 max_tokens≤2048，而日记请求为 2800 / 5000，在 HTTP 前就被拒绝；模拟日记服务测试未覆盖此跨层约束。将 JSON 请求上限有界提高到 8192，既有提取 / 摘要预算不变，新增 Diary→LLMService→HTTP 的离线集成回归，并在隐藏日志记录安全错误码。需再回归与构建，不把前一次 UI 的空日记结果当作通过。

Final4 EditMode 69/69、PlayMode 12/12 与构建零错误。第四次发布已真实完成日记双份保存、相同资料复用、外部删除 Markdown 补回、人物卡不变、遗忘后 DB / MD 收起且原文保留。最后维护步骤失败为探针错误：Canvas 是独立对象，不是 CompanionScreen 子对象，GetComponentsInChildren 找不到 Check 按钮。仅修正 Windows 条件编译的探针，按当前活动 UI 查找按钮；产品代码不改，重新构建并完成发布版剩余验收。该探针不在 Editor 逻辑回归中执行。

截图审阅发现第四次真实日记自加了“晴”的天气标题，原验收对话没有天气证据。收紧最终日记提示：真实日期由界面显示，不另加天气 / 日期标题，不补写未知地点 / 时间段 / 行为；本次回望的新感想明确标作现在的感想。此为提示文字修订，逻辑回归仍以 Final4 69/69、12/12 为准；修订后的 Windows 探针与日记文字必须经最终 Player 实测。主面板、音乐、空间、人物卡与日记基本排版可读，日记长文支持滚动；普通文件选择和真实 IME 仍需人工确认。

### 2026-10-16：Phase 3F 最终发布验收完成

最终构建零错误，301,565,220 字节。Write / Restore / NoKey 三次独立进程全部通过，真实日记与音乐、文件持久化、同 Core / State 空间、原文 / 正文、缺失 MD 修复、遗忘 DB / MD 同步、NoKey 本地查看、维护报告、缩放 / 退出均完成。最终日记不再自加天气标题，明确区分角色当时推测与老师明确表达。Windows 窗口回归亦通过桌面透明合成、置顶 / 无边框、Home 恢复及 SQLite / FTS5 重开。

交付 / 当前状态 / 文件接口、架构、角色与记忆协议、V1 边界、README 已同步。旧 checkpoint 与失败尝试保留为历史；旧 Phase3 构建不改，正式资料未用于测试，首次用户正常启动才备份 / 升级至 v8。没有自动启动正式新版本。人工选择器 / IME / 听感 / 口吻及第二份 Spine 尚需用户体验 / 提供，Phase 4 / 5 / 6 不提前启动。收尾只做文档链接 / 工具静态核对、冻结素材 / 旧构建不变和新交付哈希，不继续添加功能。

### 2026-10-17：玩家审阅反馈与下一轮清单

用户确认新增功能中除日记外均已使用审阅，当前没有特别严重的问题。据此将非日记新增功能记录为已通过本次玩家审阅；日记仅保持待体验验收，不标成失败或通过。未由这一概括反馈推定所有输入法、设备组合或未提供的第二份 Spine 均已专项验收。

按总宪法和现有代码 / 运行时基线整理 [phase4_plan.md](phase4_plan.md)：4A 独立配置及 VoiceRuntimeManager，4B 正文增量断句 / 单并发 TTS / 分段 WAV，4C 打断 / 实际语音口型字幕 / BGM Ducking，4D 文件音频缓存 / 索引迁移 / 历史复听，4E 发布回归与玩家试听。默认中文，保留既有日文模型与独立配置；不要求重新安装环境。日记体验可单独继续，不阻塞 4A。

此次仅更新规划和状态文档，未实施 Phase4、运行 Unity / 语音服务、调用 DeepSeek 或检查正式聊天库。2026-10-16 acceptance.json 及其哈希保留为历史技术证据，不因本次玩家反馈而重新生成。待实施时从 4A 顺序推进，UI 整体美化 / 第二份空间适配依反馈与素材另排，Phase5 / 6 保留原边界。


### 2026-10-17：用户批准 Phase4，开始正式语音链

按 4A→4E 顺序实施，结束后交玩家测试反馈。已写入独立 voice_runtime 配置类型、自有进程管理器、流式断句 / WAV 校验、schema v9 音频索引及文件缓存基础服务，并开始接入场景播放、口型 / Ducking 与临时声音面板。GPT-SoVITS 原始 API / 模型和冻结素材不修改，新增受控 host 调用交付 API。消费者取消不释放尚未结束的实际推理；只有完整提交的 assistant 消息可拥有正式缓存。此时仍在集成，尚未编译或宣称 4A—4D 通过；不启动正式用户数据。

4A—4D 首轮代码与测试已接入。首次沙箱内 Unity 卡在授权，终止该已记录测试实例；首次提权审批超时未执行，窄化为明确 Unity 批处理命令后重试成功。Compile2 真正进入编译，报告 CompanionVoicePlayer 的 ILogger 名称歧义，已改为显式 Core 接口别名。当前正在补齐发布探针与使用说明，尚无通过报告。用户在独立 Material/MemoryLobby_Seaside 工程的 Editor 未操作或关闭。

Integrated1 EditMode 77/77 通过；PlayMode 15/16，新增语音用例通过，旧音乐测试把同对象语音源计入音量总和。将语音源放到独立子对象而非改低测试要求，并补正式界面双完成 / 新输入立即停止测试，Integrated2 PlayMode 17/17 通过。发布探针改为独立观测 LLM 任务完成时刻，不能用等待首音后的人工标记冒充真实重叠；口型按1.5秒实际时间观察。下一步独立 Windows 构建与真实服务验收。

首版 Phase4 Release 构建零错误，301,670,776 字节。真实 Write / Restore / NoKey 全通过，48kHz 中文音频、实际 GPU 取消门控、DeepSeek / PCM 口型 / Ducking / 整组缓存、无服务 / 无 Key 复听、缺段禁用与清缓存保留历史均完成。已查看 speaking / voice_settings 截图，中文可读。该次启动约34.835秒，已准备后短回复文字约1.358秒完成、首音约5.082秒，未观察真实短回复重叠，不能宣称比实际更快。

最后补充发布故障探针（运行时缺失、端口占用、自有服务崩溃 / 菜单重试、启动中离开与快速重入），并优化准备期间先显示当前轮文字，服务就绪后后续新轮才语音；新增对应正式界面测试。代码因此有后续变动，原首版证据将另存，不把前次构建冒充最终交付。下一步重跑必要测试、最终构建及四模式发布探针。

Final1 EditMode 77/77、PlayMode 18/18 通过；后续构建零错误，301,676,459字节，含故障探针。收尾审阅发现 Process.Start 在分配 PID 前失败时，清理路径会再次访问 HasExited，掩盖原异常并残留 Starting 状态；已先置 Unavailable、识别未关联原生进程的对象并释放，补无效解释器用例。该修订需再次验证，尚未计入 Final1 通过结论。

Final2 EditMode 78/78、PlayMode 18/18 与零错误构建通过，第二轮 Write/Restore/NoKey/Faults 全通过；故障组真实验证缺运行时、外来端口保留、自有服务崩溃/菜单重试、1024×768声音面板、启动中离开与快速重入。最后处理不可朗读但合法的符号回复（如☆）：没有生成片段时回到正文显示，而非留下空字幕；另给损坏缓存复听增加自然反馈。新增正式界面符号用例，只需复验受影响的 PlayMode，再重建并完成最终发布 / 文档哈希。核心78项源码未再改变。

### 2026-10-17：Phase4 最终发布与交接

最终逻辑证据为Final2 EditMode78/78、Final3 PlayMode19/19；78项之后Core未改变，仅Runtime符号回复文字回退/坏缓存自然提示及相应用例修订，最终Player亦覆盖。最终Release/Mono/D3D11零错误构建301,676,459字节，15.4364936秒。该构建Write/Restore/NoKey/Faults全部通过，发布报告均晚于最终构建。真实中文/DeepSeek/取消推理门控/PCM/Ducking/完整缓存，以及离线复听、缺段不补合成、清缓存保留历史、缺运行时/端口保留、自有进程崩溃重试、1024×768、准备中离开/快速重入均完成。

已查看最终五张自有客户区画面，新增声音面板、正文字幕与历史复听可读。最后本机模型加载21.104秒、文本1.637秒、首音5.910秒，短回复没有实际重叠，不能把架构允许重叠冒充实测更快。原始API/模型不改，开发构建不复制8.66GiB运行时；正式聊天库未用于测试，Key只沿用子进程环境且不传给模型。Phase4状态/交付/接口/架构/两级README/恢复记录已同步，收尾仅离线静态核对、冻结/旧构建与新交付哈希，不再增加功能。待玩家试听反馈，日记仍待体验，Phase5/6未开始。


## 2026-10-17 双语语音扩展（进行中）

用户新增日语克隆试听需求。保持中文主回复/前端/历史/记忆；新增独立廉价 Flash 本地化调用后再送日语 TTS。复用现有中日权重与同一 Python Runtime，切换先停止/卸载旧进程；无新增大模型依赖。独立 speech_localization.json 与每段 speech.json 可审计，沿用 v9 音频索引避免迁库。开发与测试使用 Bilingual 独立构建/证据目录，保留 Phase4 最终证据；正在补齐自动化和真实双语验收。官方模型依据：https://api-docs.deepseek.com/zh-cn/quick_start/pricing/ （2026-10-17核对）。

### 双语首轮验证

87/87 EditMode、22/22 PlayMode 已通过；首轮 Bilingual Release 零错误，301,707,894字节，比原Phase4增加31,435字节，未打入共享Runtime。隔离 Write / NoKey 全通过：中文不调用翻译；菜单中文→日语先退出旧进程；日语译文生成真实48kHz音频；中文先显示且原文落库；中日音频离线复听与sidecar清理保留历史。初次中文冷加载42.101秒、日语加载21.062秒、日语新轮首音6.382秒。已查看声音面板/中文字幕/中日历史标签截图，不代替用户主观听感验收。随后补查进入场景的自动预热边界、重试离场保护和资料检查路径，正在最终复跑；首轮报告归档 FirstPublished。

### 双语最终交付

最终 Final2 EditMode87/87、PlayMode22/22；WindowsRelease301,708,981字节/零错误，比Phase4增加32,522字节，共享Runtime不复制。最终Write15项、NoKey10项全通过，覆盖进入场景自动准备/菜单切换/真实译文日语克隆/中文先显示与落库/来源文件/无Key两种缓存复听/清理保留历史/Home退出释放。最终加载22.246/22.211秒，日语首音6.327秒。已复核15个原Runtime及旧Phase4冻结文件哈希，104个C#源码/证据hash，项目自有服务与Player退出。技术完成，用户主观双语反馈待定；无正式资料测试或人物卡/记忆/日记写入。入口Tools/Start-Bilingual.cmd，交接Docs/bilingual_voice_checkpoint.md。


## 2026-10-17 双语听感与字幕微调（进行中）

按玩家反馈：日语默认与一次性设置升级；语音模式中文字幕只随实际发声显示；句间0.45秒、分句0.18秒可独立配置，已有等待计入呼吸；日语常体活泼、减少敬语并传入同一语义情绪；纠正主提示把neutral写成必填的歧义。已用新提供中日参考OGG与对应文本导出4条3–10秒WAV，约2.77MB，无权重/环境复制。中文柔和参考、日语明朗/害羞参考及语速分档，原API/素材不改；新voice_delivery.json/VoiceReferences独立文件。日记每篇最多1个颜文字（可0、配置0–2），只作用于后续玩家主动生成，不自动重写旧日记。正在补测试和独立VoicePolish构建/隔离真实验证，原Bilingual与Phase4证据保留。

### 语音微调最终交付

Final EditMode92/92、PlayMode23/23；最终Release304,508,014字节/零错误，增加2,799,033字节（参考音频2,766,598）。最终Write15项与NoKey7项通过，日语默认/字幕无提前全文/参考情绪/呼吸/中文柔和/日记DB与MD/离线复听全部实测。首次译文时间条件偏移已补规则复验，最终使用お仕事が終わったら。最终加载17.475秒/首音10.677秒/现场间隔3.334秒；首轮0.503秒，最小配置0.45不等于所有现场间隔。连续样例是缓存加0.45秒呼吸，非现场录音。27个原始及旧发布冻结文件、108个C#和15份证据核验通过，自有进程退出。主观音色/发音/语气与实际等待继续交玩家体验；正式资料未使用，Phase5/6未开始。交接Docs/voice_polish_checkpoint.md。


## 2026-10-18 表达目标与日记排版（进行中）

按用户最新要求检查素材与实现。现有参考无独立恼火/伤心录音，计划只作明确标注的语气/节奏近似；犹豫与平直发声作为表现意图，保持8种共享Mood与v9库不变。日记改自然分段、增加有界排版兜底，放宽颜文字并按篇幅使用。用户额度低7%停止要求已记录，当前工具无账户余额百分比接口，已告知无法可靠监测；持续写checkpoint。

### 2026-10-18 00:38：按用户要求暂停，保存表达与日记checkpoint

已实现独立VoiceIntent（annoyed/sad/hesitant/flat）、同模型参考与原生速度、v2预设/v3翻译保守升级、结构化自然段日记及超长段纯排版兜底、颜文字默认4（0–12）。无新增权重/参考音频，8种Mood与schema v9不变。首次真实Write中恼火/伤心通过，犹豫/平直因主模型省略META而回退；失败报告和样例完整归档FirstPublished，未掩盖。已强化每轮控制头提示，重新Final EditMode102/102、PlayMode24/24通过，最新Release零错误304,532,383字节。用户此时要求暂停，因此最新构建未做真实复验、NoKey未执行、综合acceptance未生成。

本项目自有Runtime Python、ExpressionDiary Player和构建进程均为0；其他用户项目未动，正式资料未用于测试。Docs/expression_diary_checkpoint.md保存实现文件、失败原因、证据、能力边界与可直接执行的恢复顺序；pause_process_review与pause_manifest记录暂停状态。额度百分比工具不可见，无法监测7%阈值；本次停止来自明确暂停请求。学习说明已保存，最终交付/README切换待恢复后验收完成。不进入Phase5/6。

### 2026-10-18：恢复任务，表达与日记扩展最终技术验收

恢复时133项暂停快照全部一致，先沿用原构建实测；仍出现第3/4轮省略META，已保留SecondPublished失败证据。仅强化文字提示无效，改为PromptBuilder在Companion请求历史中重建白名单格式头作为一致角色示例，只解析已投影原文前256字符、旧raw==body不猜标签，新增头计入12000字符预算。数据库/字幕/记忆正文不变，AgentPet仍纯正文。补2项历史与预算边界测试后，Final EditMode104/104、PlayMode24/24通过，之后C#未改。

最新独立Release304,532,895字节、零错误、42.645秒，比VoicePolish仅加24,881字节，没有新增模型/参考WAV/依赖或复制Runtime。最新EXE真实Write18项、NoKey7项全部通过：四种目标正确贯通中文原文→日语本地化→参考/速度→音频缓存，Mood枚举仍8、schema仍9、单GPU。日记实测3段124/122/130字、2个颜文字，DB/MD一致；无Key/无翻译/无Runtime复听及日记恢复、Home释放和退出保存通过。

31个冻结文件、4个参考及原素材哈希核验，112份C#与15份证据写入ExpressionDiary/acceptance.json；发布无测试DLL/DPAPI、许可证齐全。全部测试为随机Validation资料，未读取正式聊天/人物卡。本项目Player与语音Python退出，其他用户程序未动。交付/学习说明/最新checkpoint/协议/文件接口/架构/两级README已同步，原暂停与恢复过程保存checkpoint_history.md。主观情绪效果交玩家判断，恼火/伤心为表达近似，flat不保证恒定音高。额度百分比仍不可读取，无低额度提示，Phase5/6未开始。

### 2026-10-18：玩家验收通过，提交Phase5阶段方案

用户确认当前阶段整体效果符合预期，记录表达/日记扩展通过本次阶段玩家验收；不回写原acceptance或样例qualityAccepted字段，不将概括反馈推定为全部硬件/未来资源专项验收。

按总宪法28—40、45—46、49—51节核对现有V7动作、600×500透明窗口、共享Core与正文传输。发现PetHotkey虽有字段但校验写成固定组合，AgentPet正式UI和tool_calls循环尚未实现；已在Docs/phase5_plan.md列入5A窗口/热键→5B气泡对话→5C互动/LookAt→5D最小工具闭环→5E其余Level 1/文件设置→5F独立发布回归。计划保持AgentPet无TTS、同一角色与记忆；工具经过白名单/参数/权限/执行结果，敏感确认来自宪法37节，默认不授予任意Shell/文件写入等能力。应用清单/工具策略/桌宠参数独立文件，普通玩家通过菜单维护。

已查阅DeepSeek官方Tool Calls/Chat Completions与Microsoft RegisterHotKey/SetForegroundWindow说明，方案保留现有非思考Flash及直接HTTP接口，不新增框架或迁移Beta。5D计划按需以备份迁移扩充同库执行记录，副作用失败/取消不自动重放。Phase5状态为方案待审批、各段未实施；两级README、最新交接/交付和阶段状态同步。

此次仅规划与文档，不运行Unity、DeepSeek、GPT-SoVITS或桌面工具，不读取正式聊天/剪贴板/个人文档，不迁移数据库；原程序与技术证据保持。下一步在方案批准后从5A开始。

### 2026-10-18：提交Phase6与之后的可选路线

用户要求继续提供Phase6及之后的阶段方案，本次只规划，不将其解释为批准或完成Phase5。按总宪法正式阶段止于6，已写Docs/phase6_plan.md与phase6_status.md：6A集成范围核对→6B首次配置/Key/故障回退→6C一致性资料快照与恢复→6D单份内置Runtime/体积/独立目录包→6E多屏DPI/兼容/稳定/占用→6F V1候选交付与玩家验收。保留单角色/单Core/同库/桌宠无TTS、默认日语与文件服务边界。

核对当前开发启动器仍沿用Editor Key和共享Runtime注入、已有安装根相对路径回退、数据库仅有迁移原生备份，普通玩家Key/完整恢复与独立交付仍待补。方案明确正式包不带个人资料或Key，资料仍在AppData，换机重新配置DPAPI Key；恢复为明确选择的快照，不自动重放工具，语音缓存可选且缺失不自动补合成。Runtime裁剪只在暂存副本按实际依赖与双语推理验证，不删原模型或承诺无依据大小。查阅Microsoft DPAPI/高DPI与SQLite Backup官方资料，引用在方案内。

另写post_phase6_roadmap.md：7沉浸视觉/交互精修、8新素材与羁绊表达、9长期运行与维护，均为建议编号、可选且未批准，不伪称原宪法要求或V1全必做。5/6实际实施顺序不变，影响使用的缺陷不得拖到可选阶段。阶段5状态、两级README与最新交接已链接新规划。

本次仅文档，无Unity/LLM/TTS/打包、无复制运行环境、无正式库/剪贴板/个人文件操作，原112份C#与15份验收证据待离线核对保持。下一步仍在Phase5方案获准后从5A开始，Phase6以Phase5实际交付为前置。

离线收尾核对通过：新规划及索引链接有效，6个Phase6检查点与3个可选后续阶段齐全；原112份C#源码及15份验收证据哈希全部未变，无需为文档改动重跑Unity测试。

### 2026-10-18：用户批准实施Phase5

开始按5A→5F顺序工作，先窗口/热键/恢复，再气泡、互动和受控工具。已冻结35个原模型/旧发布证据，保留共享Runtime和正式资料。进度持续写phase5_status.md与phase5_checkpoint.md，尚未宣称新增功能验收通过。

### 2026-10-18：Phase5桌宠与工具集成

5ABC2 EditMode114/114，5ABC3 PlayMode26/26通过。桌宠以同窗口圆角气泡替换原型常驻导航，菜单/草稿/分页/历史/日记共享原服务；窗口注册热键、位置恢复、头身触摸、拖动优先和受限LookAt已接入，原生发布验证待做。PlayMode初次失败为旧测试未经过正式菜单寻找返回按钮，已纠正路径。

用户选择默认流式、按需允许桌面操作，采用单条消息开关。正在接入DeepSeek原生SSE tool_calls、后端白名单与逐次敏感确认，同Core请求门控与共享数据库。新增schema v10执行台账并使用既有迁移备份；执行未知状态不自动重放，派生审计不保存参数、聊天或剪贴板。工具轮次保留聊天历史，自动记忆/日记排除其临时资料。13工具执行层与玩家登记/选择范围/启停/记录界面已写入，正在编译与回归，尚不宣称通过。

### 2026-10-18：Phase5最终技术交付

5A→5F已按批准方案完成，等待玩家体验反馈。Final EditMode136/136、PlayMode35/35，独立Windows Release/Mono/D3D11构建错误0、304674581字节（比ExpressionDiary增加141686字节）。最终同一构建Live39项/NoKey30项、600×500物理合成/透明角落穿透、实际热键、头身短点击/摸头/拖动/释放、Home恢复1280×720均通过，发布截图已目检。返回Companion实际日语静音合成后确认自有服务释放，桌宠阶段OwnedVoicePID为0。35个原素材/Runtime/旧发布及证据哈希不变，原验收文件不回写。

真实鼠标验证补出跨屏DPI问题：短点曾误拖动、透明命中不一致；PetNative统一物理坐标作用域，透明单像素采样与手势同坐标，不修改第三方包。另修复拖回原点仍继续移动、等价热键不误报冲突、窗口ID复核进程、迟到面板回调与停止后实际操作反馈。vSync临时关闭后30/60fps生效并在退出桌宠恢复；短样本空闲进程CPU占全机算力约0.90—0.94%、工作集297—323MiB，活动约1.68—1.80%，GPU 3D活动计数均值4.19%。

默认正常流式，桌面操作单条消息开关，原生SSE完整调用后再校验/确认/执行；13函数、应用/范围文件和普通玩家维护界面完成。同库schema v10记录调用/权限/结果，启动未知不重放，日志不含原参数/路径/剪贴板/Key；工具轮次保留历史但排除自动记忆和日记。执行网络预算120秒，确认单独180秒。真工具仅受控程序/目录，敏感写入未用正式剪贴板或音量测试；音量COM只读连接已过，写入由玩家应用内本次确认体验。

Tools/Start-Phase5.cmd与交付、实现、文件接口、最新checkpoints及两级README已同步。acceptance.json记录实际源码/构建hash与晚于构建的Live/NoKey证据，playerAcceptance=pending。正常用户库未用于测试；正式首次使用自动备份升级v10后应继续Phase5，旧程序拒绝新库。共享8.66GiB Runtime不复制，Phase6及以后仍仅规划。验收Player已退出，未操作用户其他Unity/制作/Python进程；下一步仅按玩家反馈定向修复。

## 2026-10-19 Phase6授权与6A—6C实现

用户暂缓Phase5玩家体验测试，授权继续Phase6。核对唯一Core与schema v10，冻结39项旧资产/构建/验收；旧Phase5证据不覆盖。应用内掩码Key输入、显式测试、当前用户DPAPI保存、受控重启统一加载；UI仅委托AppRoot执行网络调用。Home、陪伴设置和桌宠设置提供“资料与连接”。新增资料说明、单项配置原件保全与重置、脱敏诊断、启动失败恢复入口。

维护锁阻止模式切换，暂停Core后等待后台记忆任务取消收尾、停止语音、保存状态。备份走SQLite原生Backup API，ZIP包含固定数据根和SHA256清单，可选历史语音，排除Secrets/Logs/Validation/Runtime/既有备份。恢复先严格路径/数量/大小/哈希/SQLite/角色/版本校验，预览后保留当前快照，启动前事务替换固定数据集；失败回滚，外部路径授权重置。MD批注按文件保留，旧备份的遗忘时间回溯在预览说明。

首次编译发现WriteJson接口缺少可选保留旧件参数，已补默认兼容。隔离测试一处async lambda断言阻塞Unity同步上下文，已仅结束该批处理进程并改直接返回Task；不是正式资料恢复失败。最新技术检查进行中。语音上游回写YAML与缓存已转到用户RuntimeCache，待独立发布副本双语加载验证。源模型/Runtime不裁剪，打包脚本仅允许明确构建根并在副本排除可再生字节码/日志。

### Phase6原生隔离恢复检查

完整默认配置原生预览发现多行本地化提示词触发Windows路径字符异常，改为仅识别盘符/UNC前缀，不向路径API传正文，并补回归。153项纯逻辑检查已覆盖新路径和恢复点重复回滚，36项场景检查通过；快照.tmp遗留排除已验证。

StandaloneAttempt2中首次配置、快照、预览、当前恢复点与关闭保存8项通过；实际重启暴露本机加密AppData目录拒绝Directory.Move，恢复已自动回滚、旧资料保留。通过全新受控目录复现AccessDenied（非正式玩家资料）。现改为先完整复制原资料、Flush并核对哈希，再发布CopySnapshotComplete事务，复制安装数据；回滚不消耗副本，支持再次中断回滚。成功提交后清理暂存大副本，保留玩家快照与原资料恢复点。最终独立流程继续执行，不能把前两次失败写成通过。

### Phase6候选封存（2026-10-19 23:14）

最终153 EditMode+36 PlayMode、独立Offline8/Restored12/移动目录双语9/连续观察6共35项通过，Release构建0错误。真实重启适配加密目录，模型自有进程均退出；安装树27783份文件元数据无变化，源资产与旧交付39项冻结通过。观察为12次往返后Home空闲600.46秒，不能代替长时真实对话；CPU归一整机容量1.071%。Unity内存0无效，外部中间采样564.95→566.17MiB，不作无泄漏承诺。

候选载荷8.886GiB，9541379576 bytes，Runtime9236606178 bytes，27782条SHA256和Python依赖版本已封存，manifest本身另验。仅裁剪副本可再生字节码/日志64732362 bytes，29份无源码字节码保留。Validation/Phase6/acceptance.json及状态/交付/文件接口/维护说明/宪法核对/README/checkpoint已更新。Phase5体验暂缓，Phase6正式验收、其他硬件及1—2小时使用仍待反馈，不自动开始Phase7。

## 2026-10-23 V1完善方案获准，开始A

用户批准v1_refinement_plan，按A→F处理，Phase8/9保留项不提前实现。配置重置改为可取消的明确确认状态，切换项目/页面不会继续执行旧选择。新增后端PlayerConfigRecovery，主文件与previous/pending副本先完整保留、Flush并核对哈希，再移除原件；复制/锁定失败不开始删除，删除失败尝试从保留副本恢复。UI通过AppRoot维护锁调用，不直接执行文件重置。已补选择切换、三件保留、锁文件失败、取消/缺失原件回归，隔离EditMode正在运行。旧候选、旧证据和正式玩家资料未修改。

### V1完善 A—E 验证进展

A隔离EditMode157/157通过。B新增一次性准备提示与本轮“先看文字”，只取消语音并展示已收到中文，不重发LLM请求；晚到音频不改字幕，下一轮恢复语音偏好。首段中文、本地化、合成和首音分别记录耗时，不加入正文。B隔离PlayMode37/37通过。

C使用中文配置名称，支持人物/歌单/空间上一版的类型化校验和显式确认恢复，当前原件另存，其他项目继续完整备份路径。D解包前、恢复确认和下次启动检查资料盘空间，区分Backups、RestorePoints、ConfigRecovery；估算预留副本/压缩备份/数据库临时件/写入余量，磁盘状态未知时停止。CD隔离EditMode163/163通过，包含模拟空间不足与未知状态，无需填满磁盘。

E新增独立Config/companion_display.json作为日记字号/行距与后续画面参数来源，保留非法文件并回退。按篇目ID和状态记住本场景会话阅读位置；重开同篇或返回上一页可续读，新的/失效篇目不会沿用旧篇位置。不重生成日记、不改MD批注。E隔离PlayMode38/38通过。现创建单独V1Refinement构建和受控截图入口，准备F前后对照；不覆盖Phase6交付。

### V1完善 F与原生检查

F修正设置页声音/维护入口重叠，新增显示设置与原版画面对照，未保存返回恢复已保存值；独立JSON校验后保存。小窗口用Expand缩放，主场景保持字幕与临时面板。首次最终回归163/164，发现非法显示值的回退漏接InvalidDataException，修复后Final2逻辑164/164、场景38/38。之后仅改新显示标签高度与原生可见性探针，业务逻辑未改。

精修前Before实际EXE7项通过。第一次After自动12项虽通过，人工截图发现四个新参数标签过矮导致不可见；保留VisualAfterAttempt1，修正为40高度并加入字符真实可见检查。最终After13项全部通过，截图已查看，设置无重叠、标签可读、未保存预览恢复、900×700恢复预览完整。单独Release重建0错误，304785570 bytes，旧V1不覆盖。

新完整候选Releases/Gift-V1-Refined-Candidate已装配，内置Runtime从旧已验证副本复制，原件不改。原生Offline8/Restored12全部通过，实际关闭重启恢复设置、导入内容、MD批注，重置桌面授权，仍同一Core且桌宠无TTS。最后双语Voice检查进行中，待安装写入审计和全文件哈希封存；不能提前宣布交付与玩家验收完成。

### V1完善最终封存

双语Voice9项通过（JA预热91890ms/推理10561ms，CN23895ms/13375ms），自有模型及测试程序退出。最终原生42项通过；双语结束后27784份安装元数据无增删改。新清单27783份文件SHA256，约8.886GiB；旧候选27782份文件及其清单全部校验不变，冻结39项通过，Runtime逐文件等于旧便携副本。acceptance.json passed=true，正式playerAcceptance仍pending。Final2逻辑164/164与场景38/38后仅显示标签高度/原生探针调整，已由最终原生可见性检查和截图复核。

交付/玩家说明/维护/界面对照/方案状态/两级README/checkpoint同步完成，旧Phase6证据不改，未自动进入Phase8/9。测试未使用正式玩家资料，不宣称冷启动提速、全天稳定或跨设备全部通过。本轮没有待轮询exec或未退出的自有测试/模型进程；下一步由玩家实际使用给出反馈。

## 2026-11-02 V1收尾：默认陪伴，暂关桌宠

用户授权直接修改入口并整理V1全流程总结。新增独立Config/app_entry.json，默认StartupMode=companion、PetEntryEnabled=false；Bootstrap按后端配置选择默认场景，Home隐藏桌宠与找回位置，AppController拦截关闭状态的桌宠模式切换。Home作为维护/恢复入口保留，代码/素材/共享数据库不删除，不迁移记忆。

新默认与损坏配置回退逻辑通过165项EditMode，默认直达/入口隐藏/模式拦截与既有共享场景通过39项PlayMode。历史多模式测试在自身隔离文件显式开启Home/桌宠，不修改普通运行默认。新增独立Builds/V1Closeout与Releases/Gift-V1-Closeout构建/装配工具；旧候选不覆盖。Runtime副本准备与构建进行中，原生启动验收未完成。

V1完整设计/实现回顾已写Docs/v1_closeout_summary.md，明确环境骨架、Phase0—6、A—F精修与本次收尾；说明中文主LLM→日语本地化→本地声音、来源受限记忆/日记、文件契约、独立备份恢复、曾发现的问题和验证边界。暂不启动后续阶段，不将技术收尾当成玩家最终验收。

### 2026-11-02 收尾封存

入口165项EditMode/39项PlayMode通过。首轮Player探针引用未启用的ScreenCapture模块，改用工程已有帧读取方案，最终Release构建0错误、304792610 bytes。构建EXE与完整收尾包的默认直达、按钮隐藏、路由拒绝、维护可用、同Core、schema及保存退出10项检查均通过，最终截图已查看；测试为静音隔离资料，不重新宣称语音/外部API整体验收。

独立目录Releases/Gift-V1-Closeout封存，27782条SHA256，约8.886GiB；Runtime27585份文件复制后逐份与旧清单核对，最终尺寸/时间未改，旧完善候选的清单与程序3份指纹不变。综合acceptance.json passed=true、playerAcceptance=pending，封包与受验构建程序指纹一致。V1总结与使用说明随包提供，两级README和checkpoint同步。正式历史/Key未用于测试，旧候选/素材/代码保留，没有待结束构建或自有探针进程。

### 2026-10-23 Mika视觉布局提案（待审阅）

用户要求先出视觉方案、不附加具体限制。以“星夜茶会”为方向，保留现有蓝紫场景，加入暖白信笺、淡粉、深玫瑰和柔金细线。菜单/日记优先从左侧展开，让开未花面部；同套样式覆盖对话、设置、音乐与入口，桌宠只规划气泡与菜单统一。新增mika_visual_plan.md、独立HTML七状态原型及七张PNG，基础色对比已计算，截图已检查。ui-ux-pro-max用于辅助布局与可读性，未引入新框架或字体。仅方案文件，不修改程序、模型、人设、真实日记或封存包；等待用户批准后实施。接续见mika_visual_checkpoint.md。

### 2026-10-30 Mika视觉稿第二版（待审阅）

按玩家三张参考图修订独立HTML：曲名取代左上Mika，去副标题；悬停音乐控制集中为上一首/暂停继续/下一首/音量，图标可点击切换重叠星星与瑞士卷。删去独立音乐页、菜单音乐项、左下音乐入口；时钟透明并由26增至32。对话改为直接叠在左下场景中的白色描边字幕，无姓名纸页、输入框或底板。

保留第一版原型和PNG，新版为Docs/Design/MikaVisual/revision2.html与r2-*图片。使用原有无UI截图，未修改源图。实际Edge鼠标/键盘14项通过，已核对截图与小窗口；修复点击后悬停控件不收起及SVG/场景容器ID重名。仅原型交互，无真实音频、联网或资料写入。Unity源码及已封存V1均未修改，后续输入方式与真实音频接入仍属实际实现环节。说明与checkpoint已同步，等待视觉审阅。

### 2026-10-30 Mika视觉稿第三版（待审阅）

按本轮两张标注图调整：时间从平面角落移入窗外天空，斜向投影、局部柔焦与淡紫明暗，并以静态头部/头饰轮廓示意遮挡。音乐四按钮及音量展开区删除椭圆底板，透明平铺。菜单入口顺右侧墙纸倾斜，常态暗色透明、靠近高亮、点击打开原目录；展开目录未重做。双星按标准几何五角星重绘，瑞士卷仍可切换。

新增revision3.html、r3-*截图、说明及交接。保留前两版原型、图片、Unity程序及封存包。隐藏Edge实际交互18项通过；音乐显隐、切歌暂停音量、图标、菜单高亮/恢复/点击及键盘/小窗口核对完成。天空景深与遮挡仅静态视觉示意，实际跟随Spine的遮挡和音频后端接入待Unity实现。没有写入玩家数据，等待视觉反馈。

### 2026-10-30 Mika视觉稿第四版（待审阅）

用户进一步指定大型方块时间及菜单墙面位置。新增revision4.html与r4-*图：时间改为本机Impact粗方块数字，字高440、投影宽650，铺开窗外并裁切在天空窗面，保留人物静态遮挡和柔焦。菜单放大至34字/28图标，入口206×72，向左下移入墙纸，常态约22%不透明度。透明音乐按钮等沿用前版，修正Home叠层隐藏。

隐藏Edge实际交互20项通过，预览常态与菜单高亮已查看；没有导入新字体、改Unity、播放真实音频或写玩家数据。景深与遮挡仍为静态原型示意。前三版保留，主方案入口和checkpoint同步第四版，等待用户审阅。

### 2026-10-30 Mika视觉稿第五版：Unity实测窗内排版（待审阅）

第四版超大时间依靠裁切，用户指出严重溢出。本轮新增仅编辑器WindowClockVisualProbe，在Unity6000.3.7f1临时场景隐藏人物/窗帘取景，导出真实窗框顶点与前景透明层；两正式场景和行为配置哈希不变。按Window_01/02内沿求交及20像素内缩区域，完整字形拟合窗内，包围框净距25.20像素，替换手绘头部遮罩为Unity真实前景。

新增revision5.html与无遮挡校准页、独立window_clock_layout.json、校准/像素脚本、r5-*图。浏览器20项通过；1280×720实际SVG独立像素审计39,849变化像素，窗外0、安全区外0，检出像素净距32.43。直接审阅整页比较的抗锯齿干扰与测试DOM残留回调已定位处理，最终报告通过。仅测量工具/证据/原型更新，未改正式运行界面或玩家资料，主方案和checkpoint同步。

### 2026-10-31 Mika视觉稿第六版（待审阅）

时钟删除冒号，仅2106四位数字。按既有虚线五边形做两段透视拉伸，实际墨迹边界去除字体上下留白，长宽填入虚线区域、约1像素保护，不扩大Unity测得的安全边界。复用第五版取景前景，未再改场景。新增revision6.html、布局v6、r6-*、说明及交接，保留前版。浏览器22项通过，实际像素审计88,364变化像素，窗外0/安全区外0，最小净距20.23。正常/无遮挡图已看，正式运行界面未改，等待反馈。

### 2026-10-31 Mika视觉稿第七版：仅水平调整、恢复直角（待审阅）

根据玩家反馈取消两段透视，2106整组使用同一26.8166°二维旋转和水平缩放，恢复第五版统一字高218.47、横向适配383.50。上下等宽、四角90°，6不再单独歪斜；不强行填满五边形每个角，保留实测窗沿及真实前景。新增revision7.html、独立布局v7、r7-*及说明，更新方案与checkpoint。

检查发现SVG等宽数字与Canvas默认宽度不一致，改由SVG getBBox度量横向宽度；补做移除窗沿裁切的实际字形审计，确认完整6没有依赖裁切截断。最终浏览器23项通过，未裁切像素59,509、窗外/安全区外0、最小净距24.08。截图已查看、浏览器已退出，正式运行界面、候选包及玩家资料未改。

### 2026-10-31 Mika视觉稿第八版：竖向加高5%（待审阅）

按玩家要求在第七版统一直角字形基础上加高5%，高度218.469→229.393，宽383.501与26.8166°倾角保持。在既有安全区内微调中心，保留无冒号、真实前景遮挡与原20像素窗沿安全区，不引入透视。新增revision8.html、独立布局v8、r8-*及说明，同步主方案与checkpoint。

浏览器23项通过，移除窗口裁切后像素审计60,635变化像素，窗外/安全区外0，最小净距20.92；校准图已看，临时浏览器退出。正式Unity运行界面、候选包与玩家资料未变。

### 2026-10-31 Mika视觉稿第九版：按红线向上拉长（待审阅）

玩家标注红线要求继续加高。固定第八版底边、宽383.501及26.8166°倾角，局部竖向增加58，高度287.393，顶部右移26.17/上移51.76；四位2106保留上下等宽和90°字形，无分段透视。新增revision9.html、独立布局v9、r9-*与说明，同步方案及checkpoint。

裁切使用真实窗面，旧20像素虚线保留作参考；排版矩形空白角净距2.73，实际墨迹不同于排版矩形。浏览器23项通过，额外去掉裁切审计75,774实际像素，窗外/原内缩区域外0，最小墨迹净距20.39。正常及校准截图已看，临时浏览器已退出，正式Unity界面、玩家资料和候选包未改。

### 2026-11-02 窗边视角运镜第一版参考GIF（待审阅）

按玩家要求先制作点击窗后时间、经过人物、到窗边显示时间/菜单并有流星的参考。新增仅编辑器WindowCameraMotionProbe，Unity临时场景导出room/character/curtains分层，三个正式场景/行为配置哈希一致。Canvas分层景深位移与缓入缓出完成2.5D运镜，附点击前进/返回和完整演示的本地HTML，参数motion.json独立存放并嵌入页面。

输出11.6秒960×540 GIF：3.2秒前进、窗边停留两次流星、2.4秒演示返回；原始174帧/15fps，合并157帧，约13.06MB。关键帧发现终点人物未离场，修正额外近景位移；修正旋转取景露出画布边缘与流星不在可见范围，最终渲染无异常，GIF解码/时长校验通过，已查看关键帧和解码画面。临时Unity/浏览器已退出，中间帧清理，分层/成品/代码/报告保留。未改正式运行UI、玩家资料或候选发布包；静态第九版保持，等待运镜方向反馈后逐步精修。

### 2026-11-02 窗边运镜第二版：退后、月光纱帘与常驻场景UI（待审阅）

按玩家反馈保留路线方向，将终点从zoom2.85退至1.95，center(920,280)、rotation-10°，保留窗台、窗框、花瓶、墙面与斜向空间。帘层depth1.05，增加程序化轻纱/微摆与柔和月光。删除旧宽大块状时间与终点另现的屏幕UI，改为同一套场景固定时间/两排菜单，始终使用背景镜头矩阵，alpha恒0.76/0.78，前进和返回不淡出，人物自然遮挡。交互点击区域逆变换场景坐标。

新增CameraMotionV2独立参数、HTML、GIF与说明，直接复用V1原分层、不再次启动Unity。完整174帧渲染无异常，6时点alpha验证通过；GIF11.6秒960×540、15fps、16.34MB，解码时长/关键帧核验通过。已查看人物、途中、窗边、返回及GIF解码图，原三个场景/行为配置hash一致，浏览器退出，中间帧清理。正式运行界面/玩家数据/候选包未变。主方案/checkpoint同步，等待参考GIF反馈。

### 2026-11-02 窗边运镜第三版：上移取景、楷体单列菜单与细线时间（待审阅）

依据玩家标图，终点由(920,280)上移至(920,244)，保留zoom1.95/-10°，减少底部墙纸但留窗台/月光纱帘。菜单移左，17px楷体、六项单列、行距27；时间改细线Segoe UI Light“21 06”，原比例无冒号/加宽。保留同一场景矩阵和alpha恒0.76/0.78，不做运镜淡出，更新返回点击区。

新增CameraMotionV3参数/HTML/GIF及说明，继续复用V1图层，无Unity启动或字体文件复制。完整174帧无异常、六时点alpha检查通过；GIF11.6秒960×540/15fps、16.16MB，重新解码/时长及窗边帧检查通过，三个场景/配置hash一致。已查看人物和终点关键帧，浏览器退出，中间Frames清理；正式运行界面、玩家数据和候选包未改，方案/checkpoint同步待审阅。

### 2026-11-02 窗边运镜第四版：纠正为第一版正面方向、实际萝莉体与新指针钟（待审阅）

玩家否定V2/V3越来越斜的方向，回归V1转正角-26.8166°，终点zoom从2.85稍退至2.5、center(980,282)。菜单时钟在终点正向，不继续斜排。菜单16px萝莉体、行距18.5紧凑单列，时钟重做透明指针钟，原有数字样式不继承；场景常驻、遮挡、不淡出规则保留。

用户提供Lolita Regular，注册表定位实际用户Fonts/Lolita.ttf，复制一次至共享参考Fonts，@font-face明确加载并验证loaded。新增V4独立参数/HTML/GIF/说明，复用V1图层，未启动Unity。完整174帧无异常、六时点alpha恒定、真实字体载入通过；11.6秒960×540/15fps GIF约15.03MB，解码时长/窗边帧核验通过。三个正式场景/配置hash未变；浏览器退出，中间帧清理，方案/checkpoint同步待审阅，正式程序/玩家数据/候选包未改。

### 2026-11-02 窗边运镜第五版：左菜单、右上24小时时间与日期（待审阅）

按玩家要求菜单移至左侧world(900,185)，右上world(1060,190)改32px萝莉体HH : mm，12px日期排下方，删除V4指针钟，仅简单两行。GIF固定21 : 06/2026.11.02，交互预览Asia/Shanghai+h23实时时间并补零。保持正面终点、紧凑萝莉体菜单及场景常驻不淡出，更新点击区域。

新增CameraMotionV5参数/HTML/GIF及说明，复用共享字体与V1原层，无Unity启动。完整174帧无异常、六时点alpha/字体载入/时钟日期格式检查通过；11.6秒960×540/15fps GIF约15.02MB，重新解码/时长/窗边帧核验通过。三个场景/配置hash不变，浏览器退出、中间帧清理；正式程序/玩家数据/候选包未改，主方案/checkpoint同步待反馈。

### 2026-11-02 窗边运镜第六版：界面缩小15%增加夜空留白（待审阅）

菜单、时间与日期统一scale0.85，镜头/锚点/正面构图/字体/时间日期规则保持，点击及返回提示同步缩放。新增CameraMotionV6参数/HTML/GIF与说明，复用原层与共享字体，无Unity启动。174帧无异常，alpha/字体/时间日期检查通过；GIF11.6秒960×540/15fps、14.91MB，解码/时长与关键帧核对通过，三个场景/配置hash不变。浏览器退出，中间Frames清理，主方案/checkpoint同步；正式程序/玩家数据/候选包未改。

### 2026-11-02 窗边运镜第七版：纠正为整体退镜上抬、菜单左下（待审阅）

上一版将“缩小”误解为只缩UI。依据玩家纠正，改整个终点camera zoom2.5→2.3、center(980,282)→(955,268)，保持正面角度，露出更多上方夜空星星。撤销独立UI0.85，恢复scale1；菜单world(845,195)向实际左下移。其余萝莉体、24小时时间日期与常驻场景规则保持。

新增V7参数/HTML/GIF/说明，完整174帧无异常，字体/alpha/日期格式通过，终点视口逆投影完整在源图范围内，非空画布扩充星空。GIF11.6秒960×540/15fps、15.12MB，解码时长及窗边帧核验通过。三个场景/配置hash不变；浏览器退出、中间帧清理，正式程序/玩家数据/候选包未改，主方案/checkpoint同步待反馈。

### 2026-11-02 第一版视觉确定稿分类归档与设计报告（完成）

玩家认可运镜第七版，要求总结、分类保存确定素材、详细设计报告。新建Docs/Design/MikaVisual_Baseline_V1_2026-11-02，冻结为首套视觉基准，非历史运镜V1。归档63文件约43.90MiB：GIF/可移植预览/关键帧、Spine源文件和真实分层、萝莉体、SVG/组件规则、参数/代码/来源/测量验证。大BGM仅清单与原位索引，不复制用户数据/运行环境。原版本保留。

新增16部分设计介绍报告MD+可视HTML、普通玩家“开始阅读”入口、分类素材页及SHA清单。报告完整总结音乐中枢、无框字幕、正面退镜上抬、常驻窗边菜单/时间日期、夜景与纱帘流星，区分确定规则/原型覆盖/生产未接入。独立预览已调整为所有依赖都在归档内。

验证：隐藏Edge阻断联网后预览/萝莉体加载通过，归档终点与原V7截图逐像素一致；报告/入口排版已看，114条HTML链接锚点、7个SVG解析通过；37项来源源hash核对一致，60个有效载荷文件SHA校验通过。源场景/行为配置未改，浏览器退出，无待轮询任务。主方案重写为确定稿总览，原计划保存历史，checkpoint同步；没有自动开始Unity生产实现。

### 2026-11-02 窗边内页六张视觉稿与日记A/B对照（待审阅）

按用户要求完成设置、日记A窗景浮字/B深蓝薄信笺、聊天记录、人物设定、陪伴空间六主稿，另补设置画面阅读/资料子页，附总览与可点击预览。保留基准窗景/时钟和萝莉体，透明细线控件、文字导航；日记同样内容/字体/位置，仅改变底层，分段可滚动并有继续阅读，保留手动回望全部历史。

新建MikaVisual_InnerPages_Draft1，独立设计JSON及HTML/CSS/JS，仅引用已冻结图层字体。原生Edge字体/页签滑杆/A-B一致/滚动/历史筛选/示例取回核对通过，六主稿/两子页/对照已看。未读取真实资料，业务操作仅演示，无服务调用或生产修改，浏览器退出。README/草稿说明/主方案/checkpoint同步待反馈，未选择日记最终方案，第一版归档保持。

### 2026-11-02 内页初版确认并追加原视觉归档：日记采用浮字（完成）

玩家认可整体内页并明确选择浮字日记。将设置（含两子页）、浮字日记、历史、人物设定、空间五页及参数/HTML/CSS/JS/总结追加到原素材目录08_内页确定稿。默认日记只进入air，去除paper导航和底板，旧paper哈希转air；薄信笺和旧对照仅归入未采用历史。原草稿保留，正式业务未接入。

总入口/总报告新增第17部分/README/规则与索引更新；追加前文档清单另存历史。原主场景图/GIF/Spine/字体不改，旧60有效载荷53项原样、7项文档授权更新。归档99文件约51.41MiB，96有效载荷SHA及199条HTML链接通过；离线原生浏览器实际字体/五页/默认air/旧路由重定向/本归档内依赖通过，浮字截图逐像素一致。浏览器退出，无待轮询exec；主方案/checkpoint同步，不自动进入生产实现。

### 2026-11-03 V2-1 视觉资源与配置基础（完成）

按 V2-0 定案接入 Lolita + Noto fallback、七枚 SVG 转透明 Sprite、唯一默认视觉 JSON 与每空间锚点/安全区；新增 CompanionVisualService，AppRoot 加载但默认不写玩家文件。提供主题控件、共享投影与逆映射、等比留白与未知空间菜单回退。

Unity 6000.3.7f1 最终编译通过，基础探针 31 项、隔离启动场景 1/1 通过；三张静态截图已查看并校准 TMP 基线。96 项冻结资源 SHA256 一致。修复版本异常捕获、fallback 表初始化和 Sprite 导入模式。未运行真实 API/TTS，未重建 V1 包；字体再分发许可尚无明确凭据，记录到交付前核对。动态 Spine 与运镜继续在 V2-2 实现。见[V2-1 checkpoint](Design/V2_Visual_Implementation/02_执行记录/V2-1/checkpoint.md)。

### 2026-11-03 V2-2 真实动态分层与窗边运镜（完成）

同一 Spine 通过原生 Separator 产生 8 段网格，前景与背景按 V7 参数不同深度运动，菜单/时间为持续世界 UI。接入时钟逆投影、前景网格/UV 透明度过滤、世界菜单业务路由、反向/重复/失焦/窗口 Fit、减少动态与内页进出过渡。新增独立资源 Material 保留环境 Shader；月光/纱帘/流星/模糊可分别配置。主场景完整字幕音乐、浮字内页仍留后续阶段。

最终 PlayMode 40/40，专项运镜 20 项和独立 D3D11 程序 12 项通过；Native 使用隔离静音资料正常保存退出，53 帧实际截图按采集间隔编码约 8.09 秒 GIF。构建零错误，图形切片 BuildReport 327,024,754 bytes，无完整语音 Runtime；96 项冻结哈希和 V1 关键指纹一致。真实 API/TTS、全设备/DPI及长时间体验不写成通过。已复核往返、内页与 1100×800 留白截图。见[V2-2 checkpoint](Design/V2_Visual_Implementation/02_执行记录/V2-2/checkpoint.md)。

### 2026-11-03 V2-2 终点持续环境动态（完成）

按用户反馈分离 ambientTime 与相机/失焦暂停，终点、内页、人物往返继续同一环境时间线；星点增加低幅度明暗变化。修复首版录制停留段不采帧造成的 GIF 定格，新增 10 秒持续终点录制。新版 GIF 151 帧约 16.13 秒，停留窗景 ROI 变化已核对；专项 24 项、独立 Windows 15 项通过，正常保存退出。新 NativeContinuous/、continuous_acceptance.json 与二进制清单记录当前结果，首轮 Native/ 和演示保留历史；未推进 V2-3。


### 2026-11-03 V2-3 主场景与 V2-4 浮字阅读（完成）

按用户安排联合实施：临时透明输入保留草稿，左下无框细描边字幕沿用分段时序；左上实际曲名与装饰切换、鼠标/键盘展开控制、真实暂停续播、音量保存和设置内音乐文件管理入口。历史纵向布局保留分页、时间/称呼、缓存复听与离页停止；浮字日记保留手动全程回望、取消、DB/MD 保存、批注、来源失效、可调字号/行距和逐篇阅读位置，新增继续阅读及当前 MD 入口。修正未激活 TMP 初始化、字幕采帧过早、输入全选及底部按钮重叠。

Unity 6000.3.7f1 构建零错误，BuildReport 327,064,178 bytes；EditMode 165/165、PlayMode 41/41、独立 D3D11 程序 31 项通过，程序正常保存退出。截图已复核，覆盖输入、实际可见字幕、音乐、真实缓存复听、长日记、空态、来源失效、960×540/30 字号阅读。Native 使用隔离样本与模拟 LLM/声源，真实 UI/SQLite/缓存/导出路径运行；不读取正式资料、不启动真实模型，未宣称真实音色或所有设备复验。96 项冻结素材及 V1 关键程序/清单指纹一致。

[联合交付与截图](Design/V2_Visual_Implementation/02_执行记录/V2-4/联合交付.html)、[V2-3 报告](Design/V2_Visual_Implementation/02_执行记录/V2-3/实现与验证报告.md)、[V2-4 checkpoint](Design/V2_Visual_Implementation/02_执行记录/V2-4/checkpoint.md)已保存。Builds/V2Reading 不含完整语音 Runtime，V1 完整收尾版保持不变。V2-5/6 待安排。按用户明确指令，在结果落盘且自己的测试进程退出后休眠本机，实际请求记录见阶段目录 hibernate.json；不强制关闭其他应用。


### 2026-11-03 V2-5 设置、人物与空间维护（完成）

完成声音/画面与阅读/资料三组设置、细线滑杆、滚动表单、默认草稿与取回、未保存确认及预览撤回。人物卡按原字段显示，名字校验、保存应用、取回覆盖确认和上一版原件入口保留。空间使用实际取景缩略图和清单导入/切换，未适配空间使用回退菜单与阅读遮暗；音乐、记忆、语音缓存、Key、备份恢复、恢复点、原件/上一版、诊断、维护 Home 和退出均可达，敏感动作明确对象确认。

修复减少动态预览污染配置对象、无图形设备相机 Render 崩溃、新离页保护阻碍日记停止整理重开，以及验证脚本缩窗过早被模式窗口恢复覆盖。正常日记离页仍取消生成；系统关闭也检查未保存编辑。

最终 EditMode 165/165、PlayMode 43/43、独立维护 53 项和主场景/阅读回归 33 项通过。Windows 实际执行卡片、设置、空间与音乐、记忆确认移除、备份创建/校验/预览、三维护页往返、脱敏诊断和正常保存退出，960×540 实测截图已核对。所有资料隔离，阅读 LLM/WAV 为模拟样本，未新验真实 API/TTS、提交恢复/重启或完整设备/DPI。正式私聊、Key、V1 收尾版与冻结 96 项素材未改；27,782 项发布文件存在/尺寸及关键清单/程序哈希通过。

当前 Builds/V2Maintenance 图形切片构建零错误，BuildReport 327,111,506 bytes，190 项文件 SHA256 保存，不含完整语音 Runtime。[交付与截图](Design/V2_Visual_Implementation/02_执行记录/V2-5/交付与截图.html)、[阶段 checkpoint](Design/V2_Visual_Implementation/02_执行记录/V2-5/checkpoint.md)、两级 README、架构和阶段计划已同步。V2-6 待安排，承担完整装配、字体凭据、全设备与长期体验验收。


### 2026-11-03 V2-5 放大窗口黑边修正（完成）

按玩家截图将场景从 16:9 固定视口改为全客户区等比铺满，移除黑色清屏相机，按基准正交视野计算裁切；时钟逆映射同步，屏幕 UI 保持完整适配，确认和回退阅读遮罩拉满。43/43 场景、49 项原生检查通过，覆盖四种尺寸和实际最大化 2560×1334，15 张截图边缘检查，最大化人物/窗边/设置已复核。环境时间和同一 Core 保留，测试正常保存退出。Builds/V2Maintenance 更新，V1 与冻结 96 项哈希未变；不启动 V2-6、不新验真实模型。见[修正记录](Design/V2_Visual_Implementation/02_执行记录/V2-5/黑边修正/修正说明.md)。


### 2026-11-03 V2-6 精修回归与本机完整候选（完成）

生成 V2Final 2.0.0 新构建，保留既有浮字与连续运镜及铺满修正；将 V1 完整 Runtime 27,585 项独立复制并逐项 SHA256 校验，新包 27,784 项 9,563,765,223 bytes，V1 与冻结视觉不变。补齐组件/素材来源记录，Lolita 无明确再分发凭据，交付限定本机候选，未外发。

重新通过 EditMode 165、PlayMode 43、独立 11 流程共 233 项：入口、维护、阅读、含 1920×1080 与 2560×1440 无边框的窗口矩阵、连续终点与往返、跨进程保存/DPAPI 样例、实际隔离备份恢复、中日随包 TTS、六次往返后 60 秒实际帧间隔。均值人物56.8/窗边54.8/设置53.1 FPS；内存 API 返回0不可用，不宣称稳定60或长时稳定。声音日语冷启91.3秒、中文31.2秒，短句13秒左右，保留模型和文字回退。

真实 DPI150%，100/125%、其他设备、真实对话 API 与个人听感未新验；恢复通过受控新进程执行，未让测试正常重启进入正式资料。完整候选 Releases/Gift-V2-Candidate，新的连续录制、截图、清单、玩家说明/体验清单/兼容范围与 checkpoint 已保存，两级 README 和阶段状态同步。见[交付总览](Design/V2_Visual_Implementation/02_执行记录/V2-6/交付总览.html)。后续等待具体玩家反馈，不自动新增功能阶段。


## 2026-11-06：海边大厅资源补充

按用户进一步指示简化菜单为过渡占位，使用既有空间接口接入 CH0294 原始六个文件，SHA-256 与交付源一致。主工程保持 Unity 6000.3.7f1、Built-in/Gamma、一个角色核心及数据库；海边菜单 Esc 直接开合，实时背景继续，旧大厅运镜保留。

补齐已有配置的内置空间增量合并、海边构图与背景延展、入场白光、抚摸/待机表情、实际语音分段口型与暂停闭嘴。修正初次专项截图中菜单与曲名重合的问题，仅将回退菜单下移。

最终逻辑 166/166、场景 43/43、海边专项 42、阅读回归 33、完整包重开恢复 3 项通过；新候选独立放在 Releases/Gift-V2-Seaside，原 V1/V2 收尾包保留。未新验真实外部 LLM、语音模型冷启动、其他设备与长时性能。证据位于 Docs/Design/CH0294_Seaside_Integration_2026-11-06。


## 2026-11-06：2.0.2 三项审阅修正

按用户审阅修正大厅切换后菜单自动出现、补齐 CH0069 原作 Start_Idle_01 入场、海边透明背景萝莉体时钟。移交旧视图后再清理 UI，避免把旧大厅相机缩放写入新大厅；时钟换字体时同步字形材质，避免旧字体图集残留。

逻辑 166/166、场景 43/43；独立程序证据和 5/10 秒阁楼特写、入场结束及海边时钟实图归档至 Docs/Design/CH0294_Seaside_Integration_2026-11-06/Review1。新构建为 Builds/V2SeasideReview，完整体验包仍为 Releases/Gift-V2-Seaside，保留原语音 Runtime 与用户资料位置。


## 2026-11-14：2.0.2 安装包与清理（完成）

交付约 6.18 GB 的单个离线安装 ZIP，包含两个大厅与完整语音环境。27,790 项发布文件安装前后 SHA-256 通过；55 项大厅、3 项重开、快捷方式、登记、卸载及正式资料保留全部通过。ZIP 全量 CRC 与成员哈希通过。验证目录预期修正和一次并发归档期间的动画时序失败保留日志，最终同一安装包完整复验通过。

清理两个旧完整包和五个旧构建，56,525 文件、19.33 GiB；保留最新完整目录及构建、源码素材与资料。报告归类到 Docs/Delivery/Installer-2.0.2-2026-11-14，两级 README、架构和 V1 归档索引同步更新。
