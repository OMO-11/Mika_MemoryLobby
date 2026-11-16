# Phase 3F：文件化资料与长期回顾

> 2026-10-16 已按用户要求恢复。此文件为方案及接口说明，最新验证 / 交付进度见 [phase3plus_status.md](<../phase3plus/phase3plus_status.md>)；[checkpoint](<../phase3plus/phase3plus_checkpoint.md>) 保留历史暂停快照。

2026-10-15，按用户补充要求实施，沿用 Phase 3 基础 UI，视觉美化与深入互动后续精修。用户明确要求「一切皆文件」「沉浸体验至上」，同时允许普通玩家主动修改人物卡；Memory / LLM 仍不得自动修改 Identity。

## 本轮边界

| 本轮补充 | 文件与接口 | 玩家操作 |
| --- | --- | --- |
| BGM 与手动选歌 | bgm_playlist.json、ContentLibrary、BgmFilePlayer、BgmManager | 选歌、上下首、添加音乐、移出歌单；当前曲目持续单曲循环 |
| BGM 平滑过渡 | 歌单 FadeSeconds、双 AudioSource 手动交叉淡化、循环首尾音量包络 | 音量 / 静音在设置，循环不会自动切到下一首 |
| 空间接口 | companion_spaces.json、每包 space.json、CompanionPresentation.SwitchSpaceAsync | 添加兼容资源包、选择空间；人物、State、历史不更换 |
| 人物卡 | character_card.json、ConfigService、受控 Core 卡片替换 | 修改名字、性格 / 口吻、关系；显式保存 / 读取 |
| 长期回顾 | schema v8 relationship_journals / journal_sources、Diary Markdown、RelationshipDiary | 主动点击回望相识至今；完整历史分批整理，可取消 |
| 文件维护 | 使用说明.md、资料检查.md、独立 JSON 与上一版备份 | 打开资料文件夹、重新读取、检查、阅读说明 |

运行时不使用开发机器的 W: 绝对路径。随包默认资料在 StreamingAssets/Defaults，首次启动在用户资料目录建立独立文件；已有玩家文件不覆盖。bundle: 表示随包内容，其余路径相对用户资料根目录。Core 文件 / 日记服务无 Unity 引用，界面不直接访问 SQLite / HTTP。

默认 BGM 来自用户的两份 MP4，保留约 67.5 / 99.7 分钟完整音轨，OGG 共约 168 MB，不复制视频或 ffmpeg 到新构建。音轨离线按 -18 LUFS / -2 dBTP 处理；播放器流式加载，目录清单允许配置时长上限，实际淡入、循环边缘淡出 / 淡入与手动交叉淡化保持音量上界，不用自动播放下一首的逻辑。

空间资源目前仅有已验收大厅；以其副本验证通用包入口，不能冒充第二份新美术已验收。新包须兼容 Spine 4.2，描述待机动画、骨架 / atlas / 全图集，未经适配只开放基础 Idle，不假设存在 Mika 的 gaze / 摸头 / 嘴部附件。失败保留原空间，原始冻结素材不改，模型动作仍仅 none。

## 回顾与记忆治理

用户明确日记用于了解从使用开始到现在聊过什么、角色表达过什么感受与变化，而非当天自动流水账。只读取已完成、未排除的两个模式共享原文 / 正文投影，通过稳定水位分批覆盖全部历史，不用最近 6 轮替代全量历史；字数分批可配置，长轮次分段，取消不保存半篇。

滚动整理与最终日记均通过共享 LLMService 的 JSON 接口，玩家主动触发；与正常对话互斥并让后台记忆任务让路。日记记录回顾，不自动进入事实检索、不修改人物卡、不评好感度，不把生成感想当作新的客观心理事实。相同来源版本与人物卡复用已存回顾，无需重复生成。

数据库提交来源引用、正文和待导出状态，Markdown 作为可读投影；导出失败保留待办，打开 / 重启重试。遗忘 / 更正使旧回顾收起，正文清空、MD 改为自然提示，不留含遗忘内容的导出备份；原始历史继续按既有规则保留。MD 外部编辑不自动写回数据库，避免两套记忆相互覆盖。

## 后续顺序

- Phase 3 体验精修：人物口吻、布局 / 配色 / 字幕节奏、真实输入法、真实第二份 Spine 交接与特定互动适配。先稳定文件接口，保持 UI 可以替换。
- Phase 4：GPT-SoVITS 生命周期、分句 / 音频队列、真正 PCM 口型、Ducking、缓存和历史复听；本轮跳过这些已排期内容。
- Phase 5：AgentPet 最终输入 / 拖动 / 热键、Level 1 工具与权限。玩家手动导入 / 编辑资料不代表授予模型任意文件写入或 Shell 权限。
- Phase 6：跨机分发、安装 / Key 配置引导、完整备份恢复界面、资源压缩与全面 DPI / 多屏 / 视觉整合。当前提供资料维护与可读报告，不把这些等同于普通玩家能自行审计所有代码。

源码审阅入口：Core/Config/LibraryFiles.cs、ConfigService.cs、Core/Memory/RelationshipDiary.cs、Persistence/DiaryStore.cs、Companion/BGM、CompanionSpaces.cs、CompanionLibraryPanels.cs。普通玩家使用说明随包为 Defaults/PlayerGuide.md，首次复制到资料文件夹；不需要理解这些代码路径才能正常添加 / 修改资料。
