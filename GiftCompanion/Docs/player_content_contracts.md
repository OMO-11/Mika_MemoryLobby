# 玩家资料文件接口

2026-10-16。普通玩家通过 Esc → 资料与回顾操作，不需要编辑以下文件。本文供后续维护和资源交接使用；实现与发布状态见 [phase3plus_status.md](phase3plus_status.md)。

## 位置与保存

Player 资料根为 `%LOCALAPPDATA%/GiftCompanion/`，Editor 为工程 `UserData/`。菜单可直接打开。`bundle:` 相对 StreamingAssets，其余路径相对资料根，不记录本机 W: 素材路径。配置最大 1 MiB，JSON 大小写不敏感，保存为 UTF-8。首次启动只创建缺失文件，格式损坏保留原文件并提示。

`AtomicDocument` 在同目录写临时文件，取消前检查，普通 Windows 文件系统用 MoveFileExW 原子提交。当前执行环境的加密 / 重定向文件层报告跨设备（错误 17）时，改用带 SHA-256 校验的 `.pending` 写前副本，完整落盘后覆盖文件，下一次读写 / 重启恢复中断提交；此兼容分支的覆盖阶段不宣称原子。其他权限错误继续失败，不改 ACL 或加密设置。歌单、空间清单、人物卡和普通设置保存上一版 `.previous.bak`。导入原素材不移动，清单删除不删除原音乐。备份整个资料目录前正常退出程序。Key 单独保管，不随报告分享。

## 音乐

`Config/bgm_playlist.json` 由 `ContentLibrary` 读取，`BgmFilePlayer.SelectAsync` 调用播放：

```json
{
  "version": 1,
  "repeat": "one",
  "fadeSeconds": 1.5,
  "maximumDurationMinutes": 180,
  "selectedTrackId": "example",
  "tracks": [
    { "id": "example", "title": "一首音乐", "path": "Content/Music/example.ogg" }
  ]
}
```

最多 128 首；ID 唯一，标题最多 120 字符。支持 OGG / WAV / MP3；导入单文件最多 200 MiB，解码时检查时长不超过歌单限制（1—1440 分钟）。过渡 0.2—8 秒，短片段按长度收缩。曲目不自动顺序播放，添加也不自动切歌。循环首尾做音量包络，不承诺任意非循环歌曲能做到音乐结构无缝；手动切歌双源线性混合，音量总和受设置限制。离场取消加载再淡出。

后端 `ImportMusicAsync` 在复制后于写锁内追加最新清单；`SelectMusicAsync` 只在选择改变时写盘。空列表静音。未来可以替换界面或音频实现，清单结构不依赖按钮名称。

## 陪伴空间

`Config/companion_spaces.json`：

```json
{
  "version": 1,
  "selectedSpaceId": "example-space",
  "spaces": [
    { "id": "example-space", "title": "另一个陪伴空间", "definition": "Content/Spaces/example/space.json", "builtin": false }
  ]
}
```

最多 64 项。每包 `space.json` 示例：

```json
{
  "version": 1,
  "runtime": "4.2",
  "skeleton": "character.skel",
  "atlas": "character.atlas",
  "textures": ["character.png"],
  "idle": "Idle_01",
  "scale": 0.01,
  "straightAlpha": true,
  "fitToBounds": true,
  "cameraX": 0,
  "cameraY": 9.7,
  "cameraSize": 9.5
}
```

文件名必须为包内直接文件，不跨目录；骨架可为 4.2 二进制或带清单的 JSON，图集使用 PNG（最多 16 页，每文件最多 128 MiB）。图集页面、完整骨架数据及 Idle 必须通过预解析。未附清单时，只接受唯一 skel 与 atlas 并寻找含 idle 的待机动画；找不到则请资源提供者补清单。仅复制骨架、图集、纹理和生成的清单，不执行包内程序。

`builtin` 用于已适配的原作大厅；新包保持 false，只播放基础待机。`fitToBounds=true` 自动构图，false 使用相机坐标 / 大小。导入由 `ContentLibrary.ImportSpaceAsync` 调用 `CompanionPresentation.ValidateSpaceRuntime`；切换由 `SwitchSpaceAsync` 预装成功后提交。角色 ID、Core、State、历史和记忆不换。新表情 / 触摸 / PCM 口型要另行适配，不靠修改动画名称假装已支持。

## 人物卡

`Config/character_card.json` 的字段为 id、displayName、persona、relationship。种子为 `Assets/Characters/MainCharacter/Config/identity.json`；启动后修改实际用户文件或通过人物卡面板保存。ID 必须与种子一致；名字最多 64 字符，Persona 最多 8000、Relationship 最多 4000 字符。

`ConfigService.SaveCharacterCardAsync / ReloadCharacterCardAsync` 负责文件。正式调用经 `AppRoot.SaveCharacterCardAsync / ReloadCharacterCardAsync` 门控：等前台请求收尾，让后台记忆让路，再由原 `CharacterCore.UsePlayerCharacterCard` 应用。两个模式和下一轮对话使用同一卡片。自动记忆 / 日记没有修改权限；保存卡片不清空旧聊天。

## 长期回顾

`Config/diary_policy.json` 独立设置 Version=1、BatchCharacters（默认 6000，1000—12000）、RollingSummaryCharacters（2400，500—4000）、DiaryCharacters（4000，500—6000）。手动点击回顾才使用共享 DeepSeek JSON 服务；长历史分批、超长单轮分段，可取消。

数据库 schema v8 的 `relationship_journals` 保存正文、状态、版本 / 水位指纹和导出状态；`journal_sources` 关联实际消息 ID。`RelationshipDiary.GenerateAsync` 在同一来源版本 / 人物卡时复用，`SyncExportsAsync` 负责可读 Markdown。文件名为 UTC 时间 + ID，正文日期为本地时间。导出失败重试、缺失补回，已有成功文件普通查看不重写。

遗忘 / 更正保守收起该角色旧回顾，清空数据库正文并更新 MD，不留正文备份；原始聊天按已有规则保留。外部修改 MD 不写回数据库；重要批注另存副本。日记不参与事实检索，不改人物卡，不推断未说出的心理，不生成好感度数值。

## 语音与音频文件（Phase4）

`Config/voice_runtime.json` 为独立后端配置，由 `VoiceRuntimeSettings.Load` 创建 / 读取，错误文件保留并让语音降级。主要字段如下，JSON 字段名读取时不区分大小写：

```json
{
  "Version": 1,
  "RuntimeDirectory": "Runtime/GPT-SoVITS",
  "Profile": "voice.json",
  "InferenceConfig": "tts_infer.yaml",
  "Port": 9880,
  "StartupTimeoutSeconds": 120,
  "RequestTimeoutSeconds": 120,
  "MaximumQueuedSegments": 32,
  "MaximumBufferedAudio": 3,
  "MinimumSegmentCharacters": 10,
  "PreferredSegmentCharacters": 45,
  "MaximumSegmentCharacters": 70,
  "DuckFactor": 0.5,
  "DuckAttackSeconds": 0.25,
  "DuckReleaseSeconds": 0.8,
  "DuckHoldSeconds": 0.4
}
```

`RuntimeDirectory` 相对安装根目录，开发启动器以子进程环境 `GIFT_VOICE_RUNTIME_ROOT` 指向现有共享 Runtime，避免每份构建复制 8.66 GiB。普通资料文件不保存开发机 W: 绝对路径。双语扩展后，settings.json 的 VoiceLanguage（zh / ja）决定实际 Profile / InferenceConfig 配对；voice_runtime.json 的原字段保留兼容读取，不再需要玩家手工改两个文件。通过声音面板选择语言并保存，程序先停止并卸载旧音色，再准备另一种音色。Profile 仍引用原有参考音频、转写和语言，不复制成新人物卡。

语音开关 `VoiceEnabled` 与音量 `VoiceVolume` 保存在现有 Config/settings.json，由设置服务读写。音乐压低只使用播放器临时系数，不保存成 BgmVolume。声音与复听面板支持开关 / 音量保存、重试、停止当前回复、占用查看及缓存清理；《声音与复听.md》是独立玩家说明，不覆盖玩家已有《使用说明.md》。

`VoiceRuntimeManager` 只启动项目内 Python，绑定本机，核对健康响应中的实例标识 / PID；不采用其他监听程序。`StreamingAssets/Voice/voice_host.py` 调用交付 API，保持原 Runtime 源码 / 模型不改，并对推理加串行门控。请求取消后继续占住实际推理门控直至完成；超时先退出该自有服务再允许后续请求，避免 GPU 推理重叠。推理排队超界会停止本轮语音，正文继续接收。

`AudioCache/<会话随机ID>/<四位片段序号>.wav` 保存逐段实际音频，`AudioCache` 服务校验路径 / WAV，再由同一个 DatabaseService 的 schema v9 `audio_records` 索引到已完整提交的 assistant 消息。字段为 message_id、segment_index、segment_count、path、duration、display_text、profile_id、created_at；profile_id 记录语言与语音配置指纹。数据库消息原文和正文投影不因朗读清理而修改。

音频文件完整落盘后才提交整组索引。失败 / 取消轮次不发布半组缓存；中断遗留的无索引音频下次启动回收。缺段即不可完整复听，不调用 TTS 补齐。`ReadPlayableAsync` / `ReplayAsync` 只读本地缓存，无 Key / 无服务仍可用；实时对话优先，点击新消息立即停止旧声音。清理通过 `ClearAsync` 先去索引再删除程序命名的 WAV，不删除聊天 / 记忆 / 玩家其他文件；删除前校验最终路径，不递归删除整个资料目录。

## 维护与后续

《使用说明.md》面向玩家，《资料检查.md》列出文件状态、路径、程序 / 配置指纹。指纹用于核对交接版本，不能证明程序安全或替代源代码审计；玩家遇到问题可以把去除个人信息后的报告交给维护者。新增界面、播放实现和特定空间适配可以继续扩展，保持文件服务与角色核心分离。

Phase4 的实际验证进度见 [phase4_status.md](phase4_status.md)；Phase5 处理桌宠工具；Phase6 处理安装分发、备份恢复引导、进一步体积优化与多屏 DPI。新增资料管理不授予模型任意 Shell 或文件写入权。


## 双语声音扩展（2026-10-17）

中文输入 → 主 LLM 中文流式回复 → 前端与既有中文历史 / 记忆。仅日语朗读分支经过独立 SpeechLocalizer，再提交日语文本给已有日语克隆模型；中文模式不产生翻译请求。翻译无历史 / 记忆 / 工具写权限，不覆盖人物卡。详细说明见 [双语玩家说明](../Assets/StreamingAssets/Defaults/BilingualVoiceGuide.md)。

Config/speech_localization.json 为独立后端文件：Version=1、Model=deepseek-flash、TimeoutSeconds=15、MaximumOutputTokens=384、CacheEntries=64、Instruction=日语本地化规则。复用本机 DPAPI / 子进程环境 Key，Key 不入 JSON。模型名可独立调整；接口沿用 DeepSeek。禁止思考，无自动重试，单并发，最多256条短句内存缓存，无本地翻译模型。文件修改后重启生效；非法文件保留，日语转文字回退，中文正常工作。

每段新 WAV 旁边有同名 .speech.json，字段 Version / DisplayText / SpokenText / Language / ProfileId / TranslationModel / InstructionHash。先写文件再完整发布整组 v9 索引，译文不进消息或记忆数据库。旧 WAV 无此文件仍可复听。清缓存或恢复孤立文件时，同步清理程序命名的 sidecar，不递归删除其他资料。每条回复保留当时的一种声音；切换影响新回复，不重译旧聊天，不重复缓存同一回复的双份音频。


## 听感与字幕微调（2026-10-17 历史基线）

本节取代前文双语首版“中文先预览”的显示节奏：语音可用时，前端接收完整中文但字幕随实际音频逐句显示。默认VoiceLanguage=ja，VoicePreferencesVersion=2一次升级后保留玩家显式选择。SpeechLocalizationSettings v2为常体与情绪本地化，只迁移未改过的旧默认说明。voice_runtime.json新SentencePauseSeconds=.45、ClausePauseSeconds=.18，有界0–2/0–1秒。voice_delivery.json/VoiceReferences经VoiceDeliverySettings读取，单模型内用短参考与语速分档；格式示例即Assets/StreamingAssets/Voice/voice_delivery.json。高级文件3–10秒WAV且与转写配套，路径限自有VoiceReferences；配置和参考hash加入ProfileId，sidecar记Emotion/ReferenceId/SpeedFactor。新音频使用新规则，旧缓存保留声音，仅复听间隔按当前节奏。diary_policy.json新增MaximumKaomoji默认1、支持0–2，玩家下次生成时使用新指纹，新MD独立保存。

## 表达目标与日记排版（2026-10-18 当前）

上述默认日语、逐句字幕、停顿和缓存规则继续适用。`Config/voice_delivery.json` 的Version=2，补`annoyed / sad / hesitant / flat`日语表达目标；不扩充数据库8种Mood，旧v1自定义映射保留。`Config/speech_localization.json`的默认规则Version=3，仅升级未改过的v1/v2默认规则。新sidecar增加`VoiceIntent`，原`Emotion`仍保存共享心情，`ReferenceId / SpeedFactor`记录实际声音配置。恼火/伤心使用原参考与轻微速度调整，犹豫复用害羞参考，平直为克制演绎；未训练强怒声/哭腔或保证恒定音高。

`Config/diary_policy.json`的Version=2，`MaximumKaomoji`默认4、可0–12；`PreferredParagraphCharacters`默认160（至少80），`MaximumParagraphCharacters`默认260（不小于前者、最多500）。原有批处理和整篇字数配置保持。旧v1的上限1升级为4，0或其他自定义上限保留；只在玩家下次主动生成时生效。最终LLM返回`paragraphs`字符串数组（兼容旧`diary`字符串），统一排版后提交数据库/MD；超长段仅补空行，不改写正文、不增加修复请求。新叙事指纹为narration-v3，旧MD不自动覆盖。普通玩家从声音面板打开《表达与日记说明.md》，原理和完整示例见[实现说明](expression_diary_implementation.md)。

## Phase5：桌宠与工具文件（2026-10-18）

新增文件仍在同一UserData/Config，菜单编辑经AtomicDocument校验保存并保留上一版，手工修改后重启读取。AgentPet不另建人物卡、聊天库或TTS；普通聊天默认流式，“允许桌面操作”是单条消息的临时开关，不保存永久执行许可。

| 文件 | 数据与默认值 | 实际读取者 |
|---|---|---|
| settings.json | 原PetHotkey默认Ctrl+Alt+Space，支持Ctrl/Alt搭配字母、Space、F1—F11 | ConfigService、DesktopWindowPrototype |
| pet_settings.json | Version=1；Width600/Height500、HasPosition/Left/Top、Topmost、ReplySeconds18、LongPressSeconds0.55、DragPixels9、IdleFrameRate30/ActiveFrameRate60 | PetSettingsService、PetScreen、窗口适配器 |
| pet_presentation.json | Version=1；HeadBone=Bip001 Head、HeadRadiusHeightRatio0.13、LookYaw16/LookPitch9/LookSpeed4、Actions逻辑映射 | PetScreen交互层，映射已有V7白名单 |
| known_apps.json | 初始[]；每项Name、Path，通过文件选择器登记真实exe或解析后目标，不传自由启动参数 | AgentSettingsService、WindowsAgentPlatform |
| agent_tools.json | Version=1；Enabled固定13函数开关、SearchRoots初始[]、MaxCalls4/MaxRounds3、TimeoutSeconds120、ConfirmationSeconds180 | AgentConversation、后端参数校验/执行层 |
| memory.db | 当前schema v10新增agent_tool_runs，同库队列/迁移原生备份 | DatabaseService |
| Logs/tool_audit.jsonl | 最近200条派生审计：请求/调用ID、类型、状态、权限决定、错误码、耗时、时间；不存原参数、路径、聊天或剪贴板 | AgentConversation导出，菜单读同库记录 |
| 桌宠与工具说明.md | 随新版本首次补齐的玩家说明，不覆盖玩家已有同名文档 | 菜单使用说明 |

13函数为get_current_time/open_application/open_folder/search_file/open_url/get_running_apps/focus_application/open_system_setting/open_file/get_clipboard/set_clipboard/set_volume/mute_volume。策略文件只能启停后端已有函数，不能增加Shell或降级敏感确认。打开文档、剪贴板读写和系统音量/静音逐次确认；搜索只选定目录、文件名，不读正文，不扫整盘或链接目录。快捷方式有启动参数则拒绝，文件打开还限常见文档/媒体扩展名。

agent_tool_runs字段：character_id、request_id、call_id、tool、status、permission、error_code、elapsed_ms、created_at，主键request_id+call_id。status为started/succeeded/failed/denied/interrupted；permission记录not_required/requested/allowed/denied/reused_result。启动把started恢复为结果不明，不自动重放。系统副作用与数据库不承诺跨系统原子提交；工具中间消息不成为角色台词，完整最终正文才提交共享历史。工具轮次两条消息以desktop_tool理由排除自动提取、检索上下文和日记，原历史仍可阅读。

坏工具配置保留原件、禁用工具并提示；菜单可恢复工具默认开关。应用登记、重命名、范围添加/移除、工具启停、热键/置顶/重置和记录查看均不要求玩家编辑文件或使用终端。旧v9聊天、日记、音频索引原样保留；升级v10之后继续使用Phase5，旧程序会拒绝新库，不自动回滚。完整备份恢复与独立运行环境分发属于Phase6。

## Phase6备份、配置与分发接口（2026-10-19）

“资料与连接”维护页经Home统一进入。Config/backup_policy.json在首次打开备份页创建，格式为Version=1、IncludeAudio布尔值，玩家菜单编辑后后端读取；没有自动备份/云同步。Secrets/deepseek.key.dpapi继续兼容旧原生密文，新写入为DPAPI1头加Base64密文，仍为当前Windows用户DPAPI，不是明文编码。普通启动不读取开发Key环境；保存后受控重启统一加载。

.mikabackup是ZIP数据包，backup_manifest.json记录Version=1、Application、Schema、CharacterId、CreatedAt、IncludesAudio、Files(Path/Bytes/Sha256)与固定安装依赖。只有memory.db、Config、Content、VoiceReferences、Diary及可选AudioCache；后台先排空，数据库走原生Backup并转独立DELETE日志模式。已知原子写入临时文件、pending与previous.bak不作为正式资料收入。严格白名单数据扩展名、路径/数量/解压大小/哈希/SQLite/角色/版本校验，禁止代码/密钥/诊断及备份套娃。

恢复安排为pending_restore.json中的StageId，引用本机RestoreStaging固定子目录。预览本身不替换资料；确认先生成Backups/before-restore快照，应用关闭后启动事务恢复。restore_transaction.json中的StageId/PointId/OriginalRoots/Committed供后端回滚，RestorePoints保存原目录。普通玩家不应手工编辑这三种事务文件。恢复保持Secrets/安装Runtime，重置工具和外部路径授权，不重放历史任务；ConfigRecovery保存用户明确重置的原配置。

发行目录Release/release_manifest.json记录候选版本、数据库/备份格式、Unity和完整文件哈希清单；Runtime按安装根读取，运行期写入在用户RuntimeCache。Diagnostics只输出固定版本/状态字段，不导出日志、Key、聊天、剪贴板或全部配置内容。
