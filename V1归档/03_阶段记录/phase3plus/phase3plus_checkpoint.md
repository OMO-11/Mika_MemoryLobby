# Phase 3F / Phase3Plus 暂停交接

> 2026-10-16 已按用户要求恢复并完成 Phase 3F 技术验收。下文保留为 10-15 的历史暂停快照；当前入口见 [phase3plus_delivery.md](<phase3plus_delivery.md>) / [phase3plus_status.md](<phase3plus_status.md>)，最新证据为 Validation/Phase3Plus/acceptance.json。旧 checkpoint.json 保留不改，不再据其 paused / pending 判断当前进度。

2026-10-15，Asia/Shanghai。用户明确要求「现在请暂时停止任务，根据当前进度写好 checkpoints」。**已停止开发、Unity、构建、联网和音频处理，仅保存交接与哈希。用户明确恢复后再继续。** 无 Git 仓库，当前工作以本文件和 `Validation/Phase3Plus/checkpoint.json` 为准。

## 当前结论

基础 Phase 3 仍是已验收可运行版本：`Builds/Phase3/GiftCompanion.exe`。本轮按用户补充要求开发 Phase 3F，计划输出独立 `Builds/Phase3Plus/`，**尚未构建任何 Phase3Plus EXE，也没有最终 Player / 网络 / 视觉 / 窗口验收或 acceptance.json。** 不覆盖旧版，不将当前源码当作已交付。

最后已通过：Third EditMode **66/66**、Fourth PlayMode **12/12**。**这两份通过报告与最新源码不完全对应**：Fourth 之后又增加人物卡错误提示、维护报告指纹、Spine 导入的完整数据 / 待机预校验；这些后续改动尚未编译 / 复测。恢复后先回归，不直接发布。

## 用户要求及对齐

- 当前 UI / 普通对话达到最低功能即可，后续再美化。整体应可扩展，贯彻「一切皆文件」与沉浸优先；普通玩家通过菜单管理，不要求编辑 JSON / Unity / 代码。
- BGM 来源 `W:/Machine/VSCode/Mika_Gift/Material/Spine_Memory_BGM`。当前歌曲持续单曲循环，只有手动选歌 / 上下首才切换；淡入淡出、循环边缘缓和、允许玩家添加歌曲，独立路径清单供后端读取。
- 增加文件化 Spine 空间接口与选择权，为将来的第二份资源铺垫；同一角色 / Core / State / 历史，当前没有第二份新美术。未经适配空间只展示基础 Idle，不虚构摸头 / 嘴部 / 八情绪。
- 人物卡独立文件，玩家主动编辑 / 保存 / 重新读取。固定角色 ID 保护原记忆身份；Memory / LLM / 日记均无自动改写人物卡权限。
- 用户补充：日记是**从首次使用到现在的长期记忆回顾**，让玩家了解聊过什么、角色表达过什么感受 / 关系变化，不是当天自动流水账。是否生成由玩家自己决定。数据库与 Markdown 双份管理，不预设恋爱 / 排他承诺，不让生成感想变成新的事实。
- 用户已确认退出旧版，方便后续验证；暂停时没有任务遗留 Unity / GiftCompanion / ffmpeg 进程。
- 后续 Phase 4 的语音 / Ducking / 复听、Phase 5 的桌宠工具、Phase 6 的完整安装 / 分发 / Key 引导继续跳过。本轮没有启动 TTS。

## 已保存实现

| 部分 | 当前源码实现 | 证据 / 未完成 |
| --- | --- | --- |
| 文件服务 | AtomicDocument 原子写 / 上一版备份；ContentLibrary 歌单、空间清单、相对路径、导入 / 校验 / 维护报告 | 3 项新增文件测试通过；后端并发导入待审查，UI 已限制单操作 |
| 人物卡 | ConfigService 从用户 character_card.json 读取；玩家保存 / 重读；Core 在暂停并收尾后替换不可变卡片，同 ID / 同历史 | 文件往返与 ID 保护、场景应用通过；最新错误提示属性未复测 |
| BGM | 本地 OGG / WAV / MP3 流式读取；双 AudioSource 手动交叉淡化；当前源原生单曲循环 + 首尾音量包络；选歌持久化 | 12/12 中含循环 / 手动切换 / 音量上界；真实 EXE 长音乐未验证 |
| 离场 | 离开前取消加载，防止淡出期间加载完成重新播放；无片段时不预升音量 | Fourth PlayMode 全通过，修复了快速返回 Home 超时 |
| 空间 | 独立 registry / space.json、动态 profile、资源预装成功后再切换、失败保留原空间、基础 Idle | 已用现有大厅副本验证通用入口与共享 State；最新 metadata preflight 未复测 |
| 关系回顾 | 分批读取全部已完成且未排除的两模式聊天，滚动总结 / 最终第一人称日记；来源 / 水位 / 版本；相同资料复用 | 4 项日记测试通过；真实云生成未执行 |
| 双份存储 | schema **v8** relationship_journals / journal_sources；数据库先提交 export pending，MD 原子投影；失败重试；遗忘 / 更正收起正文并更新 MD，无遗忘导出备份 | 导出失败恢复、遗忘竞态 / 原文保留、v7→v8 测试通过；外部删除 MD 的修复缺口见下 |
| 普通玩家 UI | Esc →「资料与回顾」→音乐 / 陪伴空间 / 人物卡 / 相处日记 / 资料维护；Windows 文件 / 文件夹选择；打开文件夹 / 说明 / 检查报告 | 场景逻辑通过；新页面截图、实际文件选择器 / IME 未实机验收 |
| 工具与说明 | Prepare-DefaultMusic、Test/Build/Start-Phase3Plus、双击 CMD、PlayerGuide、phase3_file_plan | 准备 / 测试工具已运行，构建 / 启动 / Player 工具未运行；Player 探针未实现，禁止现在运行 Player 脚本 |

## 代码和文件入口

- `Assets/App/Core/Config/LibraryFiles.cs`：MusicCatalog / SpaceCatalog / SpaceDefinition，ContentLibrary 与 AtomicDocument。JSON 默认大小 ≤1 MiB；路径只在 bundle / 用户资料根内解析；导入只复制允许资源，不执行包内代码。
- `Core/Config/ConfigService.cs`：CharacterCardPath、SaveCharacterCardAsync / ReloadCharacterCardAsync；最新 CharacterCardIssue 给普通玩家错误说明。
- `Core/Character/CharacterCore.cs`：UsePlayerCharacterCard 要求同 ID、已暂停且无请求；增加 memoryChanged 回调供遗忘后同步导出。
- `Bootstrap/AppRoot.cs`：Library / Diary 唯一服务，PlayerWorkAsync 与普通对话互斥、让后台任务让路，退出等待该任务收尾；初始化不自动生成日记。
- `Core/Memory/RelationshipDiary.cs`：DiaryPolicy / IDiaryStore、全历史分批 / 长消息分段、JSON 总结、取消、指纹复用、Markdown 投影。日记不进入事实检索，不写 Identity。
- `Core/Persistence/DiaryStore.cs`：schema v8 表、来源、快照水位 / revision、事务提交、导出待办；SessionSummaryStore.InvalidateAllSummaries 同时使旧日记失效。MemoryService 提取成功与 Core 遗忘后同步导出。
- `Companion/BGM/BgmManager.cs`：当前源 loop=true，循环不自动换歌，边缘包络；双源只用于手动切歌，线性混合保持音量上界。`BgmFilePlayer.cs`：本地文件、streamAudio、代次 / 取消、离场 CancelPendingLoad。
- `Companion/CompanionSpaces.cs`：动态加载、配置相机 / 边界、Straight Alpha / PMA uniform；新加入 MetadataTextures + SkeletonBinary / SkeletonJson 预解析，检查版本 / 图集 / 待机。未附 space.json 时尝试找含 idle 的动画，找不到拒绝，不播放任意原作动画。
- `Companion/CompanionLibraryPanels.cs`：新增管理面板；`SharedUI/PlayerFiles.cs`：原生选择器及打开自有资料文件；CompanionScreen / Panels 接入。
- `SpineStraightAlpha.shader` 新增 `_InputIsStraight` uniform，避免 shader_feature；Built-in / Gamma 基线不改。原始加载器 / 资源不改写。
- `Assets/StreamingAssets/Defaults/`：bgm_playlist.json、companion_spaces.json、PlayerGuide.md；`CH0069_Source/space.json` 描述已有大厅；`DefaultMusic/` 两首 OGG。

运行时文件（首次运行新版本时建立；本轮未写入正式 Player / Editor 资料）：

```text
UserData 或 %LOCALAPPDATA%/GiftCompanion/
  Config/bgm_playlist.json
  Config/companion_spaces.json
  Config/character_card.json
  Config/diary_policy.json
  Config/settings.json
  Content/Music/<id>.ogg|wav|mp3
  Content/Spaces/<id>/space.json + 资源
  Diary/<生成时间>_<id>.md
  使用说明.md
  资料检查.md
  memory.db
```

人物卡种子仍是 `Assets/Characters/MainCharacter/Config/identity.json`，新版本实际可修改卡片位于上述 Config/character_card.json。bundle: 路径相对 StreamingAssets，其余相对用户资料根，不使用开发机器 W: 路径。包内说明首次复制，不覆盖玩家已有说明。

## 素材处理

两份 MP4 实际长 **4051.65 秒 / 5984.02 秒**（约 67.5 / 99.7 分钟）。已用项目已有 `Runtime/GPT-SoVITS/runtime/ffmpeg.exe` 提取完整音轨、-18 LUFS / -2 dBTP / LRA 11，48 kHz 双声道 OGG q5。**未擅自截短**，未启动 Python TTS 服务。

输出：daily-routine-247.ogg **68,363,302 字节**；you-are-my-princess.ogg **100,029,009 字节**，共 168,392,311 字节。来源 / 输出哈希见 `Validation/Phase3Plus/default_music.json`。原始 MP4 保留，新构建只拟带 OGG，不复制视频或 ffmpeg。歌单 MaximumDurationMinutes 默认 180，可文件配置（上限 1440）；导入文件上限 200 MiB。原始长期音轨的精简留待与用户确认 / Phase 6，不凭推测截成某个循环。

新增 Unity 自带模块 `com.unity.modules.unitywebrequest` / `unitywebrequestaudio` 1.0.0，packages-lock 已由 Unity 更新；没有新第三方框架。Unity 固定 **6000.3.7f1**，Built-in / Gamma / D3D11、deepseek-flash。

## 测试过程与准确对应关系

1. First EditMode 编译失败：BgmFilePlayer ILogger 与 UnityEngine.ILogger 重名；已用别名修正，日志保留。
2. Second EditMode **65/66**：空空间清单首次导入未填 SelectedSpaceId；已补首项选择，不放宽条件。
3. Third EditMode **66/66 passed**；Third PlayMode **10/12**：15 帧不足以验证短音频实际循环；快速 Home 导航遇到音乐加载 / 淡出竞态。
4. 修正为实际时间检查，并在 LeaveAsync 取消 pending load、防止 PlayClip 重启淡出；Fourth PlayMode **12/12 passed**，2026-10-15 14:54:37（Asia/Shanghai）结束。
5. Fourth 后新增 ConfigService.CharacterCardIssue、维护报告 SHA-256、Spine MetadataTextures / 完整预校验，**尚未编译 / 复测**。当前不能说 latestSource 全通过。

新增测试为 PlayerFilesTests（3 项）、RelationshipDiaryTests（4 项）、PlayerContentTests（2 项）。既有 v6 / v4 迁移 fixture 删除新日记表后再模拟旧版本，避免假旧库；schema v8 对照测试验证旧对话保留。PlayMode 全部在隔离目录运行，没有真实 API 请求，没有迁移正式聊天库。

## 恢复后的顺序

1. 先读本记录和 `phase3plus_status.md`，核对暂停哈希；不重做已通过阶段，不进入 Phase 4。
2. 完整回归最新源码，尤其新 MetadataTextures 预解析（空 rendererObject）、人物卡错误提示 / 文件维护页；检查新增 UI 的布局和实际文件选择器。
3. 审查以下**已识别但尚未修改**的边界：
   - BgmFilePlayer.SelectAsync 在初始化时即使 SelectedTrackId 相同也会 SaveMusicAsync；Spine SwitchSpaceAsync 同 ID force 初始化也会 SaveSpacesAsync。损坏配置回退后，自动初始化可能把原路径覆盖为默认 JSON（上一版备份仍在）。应仅在玩家确实改变选择时保存，保持损坏原文件不被自动改写。
   - 同当前歌曲 / 当前空间的早退可能未把之前失败的后端选择改回当前可用项；明确选择当前项也应能修复持久选择。
   - Diary 导出当前只处理 export pending。玩家在外部删除一个 export succeeded 的 MD 后，复用日记不会自动重建文件；需补缺失文件修复，同时避免日常读取覆盖玩家保留的可读文件。
   - ContentLibrary 保存有锁，整个并发导入未串行；UI 已单操作，后端未来并发调用是否丢更新需审查。
4. **先实现 Phase3PlusPlayerProbe、AppRoot 挂载与 UserDataPaths 的 -phase3plus-probe 隔离允许项，再运行 Player 工具。当前 Test-Phase3PlusPlayer.ps1 只是已保存的计划入口，缺上述实现；直接跑将不能提供正确隔离验收。**
5. 构建 `Builds/Phase3Plus/`（旧 Phase3 不覆盖），用新 EXE 在随机 Validation 数据中验证两首长音轨、手动切歌 / 同曲循环、添加本地 WAV、人物卡应用 / 重启、已有大厅副本的通用空间切换、真实 DeepSeek 日记及来源遗忘后 MD / DB 同步、NoKey 本地查看。不得用正式用户历史验收。
6. 查看新菜单 / 音乐 / 空间 / 卡片 / 日记 / 维护 / 缩放截图，完成窗口回归，记录听感 / 原生文件选择 / IME 仍需人工确认的边界。
7. 最终验收 / 403 类哈希报告独立保存 Phase3Plus，补交付与说明、同步架构 / 协议 / README。当前文档主体仍保留旧 Phase3 基线，实际扩展状态以本交接为准。

可用工具（仅用户恢复后）：

```powershell
& 'W:\Machine\VSCode\Mika_Gift\GiftCompanion\Tools\Test-Phase3Plus.ps1' -Checkpoint Resume
& 'W:\Machine\VSCode\Mika_Gift\GiftCompanion\Tools\Build-Phase3Plus.ps1'
# 先补齐 Probe 和隔离路径允许项，才可执行 Test-Phase3PlusPlayer.ps1。
```

Start-Phase3Plus.ps1 / .cmd 已保存但未运行，且 EXE 尚不存在；不能把它们视作可体验交付。真实 DeepSeek Key 已配置，后续验收沿用本机 DPAPI 到子进程环境，不打印、不复制、不重复索取。

## 暂停保存

没有在本次暂停后继续修复上述问题、执行 Unity / 构建 / 网络 / 音频处理。暂停只写本文、状态、开发日志、导航和哈希清单，静态解析工具不执行脚本。保存数量及校验结果见 checkpoint.json / save_review.json。
