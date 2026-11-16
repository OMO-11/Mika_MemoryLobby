# 工程与素材验证

## 2026-10-14 Phase 3 最终技术结果

按批准方案完成 3A—3E，暂停后经用户明确恢复继续。最终 Unity 6000.3.7f1 EditMode **59/59**、PlayMode **10/10**：`Validation/Phase3/Final.EditMode.xml` / `Final.PlayMode.xml`。覆盖 META 任意分片 / 降级、v6 → v7、原文 / 正文投影与来源、Mood 完整事务及取消 / 存储失败、同时间游标分页、旧新轮取消收尾、UI 接管、字幕 / 设置 / BGM 及既有记忆 / 场景 / 素材回归。

最终 Windows Release / Mono / D3D11 构建错误 0，BuildReport 133,051,035 字节、16.28 秒。真实 EXE UI Write / NoKey 的全部 flags 通过：主场景 Alpha Shader、正式界面、输入 / 菜单捕获、META / 正文分离、无模拟口型 / BGM、历史、1024×768、唯一根 / 退出保存。真实记忆 Write / Restore / NoKey 全部 passed，跨模式、近期窗口外事实、纠错 / 遗忘 / 独立事实、原文保留和重启通过。使用随机隔离库，路径见 ui_run.json / player_run.json；Key 仅子进程环境使用。

发布版白色覆盖已通过显式 Straight Alpha 场景 Shader 修正；同 Idle 1.25 秒接近纯白像素 254,655 → 1,744，修正编辑器图为 1,632，HDR 保留。原素材 / 第三方加载器未修改，Unity 内部分支根因尚未完全隔离。修正发布图、中文历史 / 设置 / 字幕和缩放画面已查看；小按钮文字及低干扰时钟底色正常。视觉证据见 visual_review.json，人工明暗 / 口吻 / 真实 IME 选词与触摸体验待用户确认。

最终 Window/player_probe.json 与 window_composition.json 均 passed，600×500 物理客户区、透明 / 无边框 / 置顶，四角匹配临时纯色背景，返回 Home 恢复普通 1280×720；已查看本应用客户区截图，不采集其他窗口，也不宣称多屏 / 任意 DPI 全面通过。字体 OFL / Notice 与 Spine 许可随包一致，862 个冻结导入文件哈希一致，构建无 DPAPI / 项目测试 DLL，Core 无 Unity 引用。

综合报告 [acceptance.json](<../04_验收记录/Phase3/acceptance.json>) 将最终测试、报告、源码、EXE / Core / Runtime DLL、许可证和截图哈希关联。早期 FirstUI / PausedRendering / checkpoint 保留历史，不冒充最终证据。首次 ScreenCapture 编译失败与发布异常修正过程见开发日志。交付与操作见 [phase3_delivery.md](<../03_阶段记录/phase3/phase3_delivery.md>)；空 BGM、neutral 回退、action 仅 none 和 Phase 4 语音边界明确保留。

## 2026-10-13 Phase 2 最终结果

用户明确恢复后已完成 2D / 2E。最终 Unity 6000.3.7f1 EditMode **51 / 51**、PlayMode **7 / 7**，证据 `Validation/Phase2/2E.EditMode.xml` / `.log`、`2E.PlayMode.xml` / `.log`。覆盖摘要触发 / 取消 / 重开 / 来源、纠错与旧摘要竞态、遗忘的关联回复 / 原文排除 / 旧任务抑制、独立事实后续回答、在途请求收尾、v4 → v6 备份与数据保留，以及开发面板普通启动隐藏。

最终 Windows Mono / D3D11 干净构建错误 **0**，约 176.9 MB（精确字节数见 player_build.json）。实际 EXE 的 `player_write.json`、`player_restore.json`、`player_nokey.json` 均 passed：双向跨模式回忆、移出近期窗口的长期事实、纠错、遗忘后请求上下文排除、原文 / 独立事实保留、重启先到 Home、无 Key 本地记忆、唯一根与正常退出保存。

窗口复验 `Window/player_probe.json` / `window_composition.json` passed，物理客户区 600×500、透明 / 无边框 / 置顶、3 个角点匹配临时纯色背景，返回 Home 恢复普通 1280×720。已查看截图，不宣称最终美术、任意缩放或多显示器全面通过。Spine 过曝和 V7 偏粉保留后续美术对齐。

前置编辑器 Core 真实 Write / Restore / NoKey 通过，记录 schema v5；最终记忆引用细化为 schema v6 后经 51 / 51、7 / 7 及最终 EXE 实际请求再次验证。首轮 Core 编译问题与 NoKey 测试近期窗口污染均保留记录，修正测试准备后全部通过，不放宽通过条件。验收数据全部隔离；Key 仅用于本机读取和测试子进程环境，不写入构建或报告。交付入口见 `phase2_delivery.md`，综合证据与哈希见 `Validation/Phase2/acceptance.json`。

## 2026-10-13 Phase 2C 历史暂停结果

2A EditMode 29 / 29、PlayMode 6 / 6；2B EditMode 33 / 33；2C 最终 EditMode **41 / 41**、PlayMode **6 / 6** 全部通过，证据 `Validation/Phase2/2C.EditMode.xml` / `.log` 与 `2C.PlayMode.xml` / `.log`。覆盖 schema 升级 / 备份、原文重开、完整轮次事务、提取证据校验、去重 / 基础事实版本、事项解决、后台前台优先 / 失败重试、CJK / 混合代号 FTS5、长期检索、受限 PromptBuilder、跨模式近期对话与上下文版本提交边界，含既有场景 / 素材回归。

测试使用独立 Validation 数据，Core 保持纯 C#；本轮未发起真实 DeepSeek 记忆请求、未迁移用户正式数据或构建 Phase 2 EXE。2D 摘要 / 显式遗忘及完整回流治理尚未实施，2E 真实联调与 Windows 验收未开始。用户要求完成 2C 后暂时停止，当前状态与续接见 `phase2_status.md` / `phase2_checkpoint.md`；源码 / 测试哈希见 `Validation/Phase2/checkpoint.json`。

2A 首次测试编译缺少 Newtonsoft 预编译引用，修正后首轮 28 / 29，剩余为 TaskCanceledException 子类型断言问题；失败日志 / XML 留存，最终已复测通过。2C 初次 40 / 40 后补充混合标识符与版本变化检查，最终结果为 41 / 41。

## 2026-10-12 Phase 1 历史验收结果

已按审批方案完成 1A → 1B → 1C → 1D。最终 Unity 6000.3.7f1 EditMode **24 / 24**、PlayMode **6 / 6** 通过，证据为 `Validation/Phase1/EditMode.xml` / `PlayMode.xml`。覆盖正式 SQLite 状态持久化与故障、设置保存 / 损坏保留、统一文字链及错误 / 超时 / 取消、提交后取消竞态、三轮导航、实际素材播放、加载失败回 Home、退出重开与模式令牌取消。

编辑器正式 CharacterCore → LLMService → DeepSeek 真实 Streaming 通过，首段 967 ms、完整 970 ms；最终 Windows 正式 AppRoot 同一链通过，短请求首段 742 ms、完整 912 ms。报告只保存计数、耗时和状态，不保存 Key 或回复正文；这些耗时仅代表短请求。完整对话 UI / 协议和 Memory 尚未实现。

Windows Mono / D3D11 干净构建错误为 **0**，约 176.7 MB，不重复复制 8.66 GiB 语音 Runtime。实际 EXE 在 Player 数据根下隔离 Validation 子目录验证三轮切换、唯一核心 / 状态、正式数据库、重启恢复并进入 Home、无 Key 仍可播放两套素材，以及退出保存。普通 Player 默认路径与验收目录边界另有纯逻辑测试。Key 仅在联网测试子进程环境中使用，不写入构建或验收目录。

新 EXE 的真实桌面透明合成复验通过，物理客户区 600×500，3 个角点匹配后方临时纯色背景，返回 Home 恢复普通 1280×720 窗口。已查看 `Validation/Phase1/Window/window_composition.png`。截图脚本新增线程 DPI 上下文，修复 150% 缩放下将客户区读成 400×333 的坐标虚拟化；不据此宣称任意缩放或多显示器完全通过。

首轮构建缓存产生的 30 条资源引用错误与窗口判定失败保留为 `first_player_build.*`、`first_player_write.json`；CleanBuildCache 与按实际原生状态等待后复测通过。阶段汇总见 `phase1_status.md` 与 `Validation/Phase1/acceptance.json`，完整日志见 `development_log.md`。

## Phase 0 历史验收结果（2026-10-11—12）

2026-10-11，Unity 6000.3.7f1 导入两套交付素材并编译通过。EditMode **11 / 11**、PlayMode **4 / 4** 通过；素材图形测试使用 Direct3D 11 并正常退出，报告为 `Validation/Phase0/PlayMode.xml` / `.log`。核心与无图形回归报告保留在 `Validation/EditMode.xml`、`Validation/PlayMode.xml`。本轮只新增 DeepSeek 真实验收，未重复运行这些既有检查。

新增测试验证原 Walk 实际改变骨骼、18 身体 / 18 面部状态及身体时长、摸头进入过程中释放走 Exit / Idle、内部动作名安全回退、Spine 743 骨骼 / 294 槽 / 52 源动画和非空网格、Idle 轨道持续、触摸取消及离开场景释放。既有模式导航与唯一核心测试现已使用真实素材运行。

DeepSeek 四个离线协议测试覆盖心跳 / 角色增量 / 中文正文 / DONE、HTTP 错误脱敏、截断失败、取消时不发送。2026-10-12 用户配置 Key 后，使用 `Tools/Test-DeepSeek.ps1` 启动 Unity 6000.3.7f1，读取本机 DPAPI Key 并向官方 HTTPS 接口真实请求 `deepseek-flash`。SSE 收到 3 个文本片段、5 个字符，首段 **1712 ms**、完整结束 **2668 ms**，正常收到 `[DONE]`，探针进程退出码 0。报告为 `Validation/Phase0/deepseek_probe.json` / `.txt` / `.log`，不保存 Key 或回复正文。Phase 0 当时尚未接入正式业务 LLMService，后续 Phase 1 已接通。

新增两项 SQLite 测试使用真实 x64 原生库，覆盖落盘重开、UTF-8 原文、FTS5、24 并发写入及取消。Windows EXE 也再次关闭重开独立验证库并搜索成功。Phase 0 当时正式 DatabaseService 为 RAM 骨架；后续 Phase 1 已持久化短期状态，但独立验证库仍不能当成完整 Memory。

Windows Mono / D3D11 Development 构建成功。`player_probe.json` 与 `window_composition.json` 均通过：唯一 AppRoot、桌宠 600×500、透明 / 无边框 / 置顶，三个角点真实显示后方临时背景；返回 Home 恢复 1280×720、普通边框和非置顶。已查看 `window_composition.png`；截图仅覆盖测试程序客户区。初轮发现的帧末分辨率 / 原生样式时序问题已修复。

内置 GPT-SoVITS 中日文真实 `/tts` 都返回 HTTP 200、48 kHz 非静音 WAV；约 669 MiB 裁剪后两种语言重新启动并再次推理通过。六份角色资源哈希通过，最终 Runtime 约 8.66 GiB。结果见 `voice_assembly.json` 与双语 `tts_probe_*.json`，音质仍需试听。2026-10-11 的独立 DPAPI 加解密测试使用合成值；2026-10-12 的 DeepSeek 探针另外验证真实 Key 读取和真实 API 请求。

已查看主工程输出 `Validation/Phase0/companion.png`、`pet.png`。Spine 大厅完整显示；3D 相机已从背面改为正面。V7 材质偏粉，保留交付资产并登记后续人工色调审阅；不宣称最终美术验收。最终中文 UI、真实鼠标停留、拖动、快捷键、长期运行和多显示器缩放仍未验收。Phase 0 六项技术链全部通过；后续 Phase 1 的交付见 `phase1_status.md`。

以下为 2026-10-08 骨架阶段历史记录；表中数量与胶囊体描述仅对应当时状态。

---

当前目标 Unity：6000.3.7f1。适配日期：2026-10-08。

当前结果已在本机 **Unity 6000.3.7f1（696ec25a53d1）** 下重新取得，不能与旧版本结果混用。2026-10-07 在 6000.6.4f1 下的历史报告保留在 `Validation/6000.6.4f1/`。

## 已通过

| 检查 | 结果 | 证据 |
| --- | --- | --- |
| 编辑器初始化与 C# 编译 | 通过，生成四场景、AppRoot Prefab、TMP 基础资源和构建配置 | `Validation/setup.log` |
| EditMode 核心逻辑 | 5 / 5 通过 | `Validation/EditMode.xml` |
| PlayMode 场景生命周期 | 2 / 2 通过 | `Validation/PlayMode.xml` |

当前包锁确认 uGUI/TMP 2.0.0、Unity Test Framework 1.6.0、Newtonsoft JSON 3.2.2；Unity 自带 NUnit 最终解析为 2.0.5。运行脚本成功自动定位 6000.3.7f1，并校验实际编辑器版本与 ProjectVersion.txt 一致。

核心测试覆盖切换前保存分离快照、保存失败不更新模式、活动模式必须经过 Home、取消不改变状态，以及 LLM 未配置时不伪造对话。

场景测试覆盖 Home → Companion → Home → AgentPet → Home 连续三轮、按钮真实事件绑定、重复加载 Bootstrap、主动实例化重复 AppRoot、快速连续切换请求、旧模式任务令牌被取消，以及直接从 AgentPet 场景 Play 时安全回到 Bootstrap/Home。每次正常切换后都检查唯一 AppRoot、唯一场景控制器、相同 CharacterCore / Database / Memory / LLM / CharacterState 引用及短期话题连续性。

## 首次导入异常记录

首次使用新版本重建 Library 时，C# 编译与场景设置已完成，但编辑器在退出清理后出现一次原生 GetTimeSinceStartup / Ticker 堆栈崩溃。原始日志保留在 `Migrations/6000.3.7f1/setup-first-import.log`。

随后 EditMode 与 PlayMode 两个进程均全部通过并以 0 正常退出；再次执行初始化工具也以 0 正常退出，当前 setup.log 不含该崩溃。尚不能从一次记录确定原生异常的原因，因此不把它归因于角色代码，也不声称它永远不会再次出现。

## 验证边界

测试在 Unity 编辑器后台模式执行，验证的是编译、对象生命周期和场景导航；未验收最终视觉效果或打包后的 EXE。当前两个角色场景使用胶囊体与临时导航按钮，AgentPet 是普通窗口。

真实 SQLite、网络 Streaming、Spine、3D 原角色动画、GPT-SoVITS 和透明桌宠窗口都未在此工程接入。此记录只表示骨架通过，不表示 Phase 0 或 V1 完成。DatabaseService 是 RAM 占位，重启后没有记忆持久化。

## 2026-10-16 Phase 3F / Phase3Plus

Final4 EditMode 69/69、PlayMode 12/12，含全部基线与新增文件 / 卡片 / 空间 / BGM / 日记逻辑回归。随后仅日记提示收紧和 Windows 条件编译探针按钮定位修订，由最终构建与真实 Player 复验；不将 Editor 测试冒充 Windows 探针执行。发布构建 301,565,220 字节、零错误，保持 Unity 6000.3.7f1 / Mono / D3D11。新程序在 Builds/Phase3Plus，不覆盖 Phase3。

最终 Write / Restore / NoKey 全通过：两首 >4000 / >5900 秒音轨解码，真实长轨末尾循环不切歌，导入 WAV 不自动切换及单曲播放，人物卡 / 空间同 Core / State 应用与重启恢复，真实 DeepSeek 对话 / 全历史日记，数据库 + Markdown、相同资料复用、外部删除补回、遗忘收起正文 / 无导出备份 / 原文保留，NoKey 本地读取与自然失败，资料维护报告、1024×768 缩放、退出保存。单独窗口回归通过桌面 alpha 合成、无边框 / 置顶和返回 Home 恢复、SQLite / FTS5 重开。

前四次发布尝试记录于 Attempt1—4：文件层错误 17、日记 JSON 预算超限、探针按钮定位，均非最终通过证据。错误 17 兼容分支为有校验的可恢复写入，覆盖阶段不宣称原子；直接在同一失败目录调用当前 Core 的保存 / 备份 / 无备份投影已通过。截图发现的无来源天气标题已收紧提示，最终生成自然区分原话与推测。

所有网络验证使用随机 Validation 资料和受控验收对话，Key 只传子进程环境，正式聊天库未读取或迁移。新空间接口以原大厅副本验证，没有第二套新美术。原生选择器、真实 IME 候选、音乐听感、审美 / 口吻、全面多屏 DPI 仍待人工体验。Phase 4 / 5 / 6 未提前实现。最终 SHA-256 与证据见 Validation/Phase3Plus/acceptance.json，交付见 phase3plus_delivery.md。
