# Phase 3F / Phase3Plus 交付与体验

2026-10-16。本轮从 checkpoint 继续完成文件化音乐、空间接口、玩家人物卡与长期回顾。Unity 保持 6000.3.7f1，独立程序在 `Builds/Phase3Plus/`；旧 `Builds/Phase3/` 保留。以下功能已完成发布版 Write / Restore / NoKey 验证；最终综合证据见 [acceptance.json](<../../04_验收记录/Phase3Plus/acceptance.json>)，进度见 [phase3plus_status.md](<phase3plus_status.md>)。

## 启动与使用

在资源管理器双击工程 `Tools/Start-Phase3Plus.cmd`。也可以运行：

```powershell
& 'W:\Machine\VSCode\Mika_Gift\GiftCompanion\Tools\Start-Phase3Plus.ps1'
```

启动器使用你已配置的本机 Editor Key，仅传给应用子进程，不复制到构建或普通配置。已另行配置 Player Key 时使用 `-UsePlayerKey`。直接打开 EXE 时只读取当前环境 / Player 自身的 Key；没有 Key 仍可进入场景、播放音乐和查看本地资料。保留完整构建目录，退出用菜单，暂时不要在新旧版本间混用同一资料库：旧 Phase3 仅支持 schema v7，新版首次正常启动会备份并升级至 v8。

进入陪伴空间 → Esc →「资料与回顾」。平时无需 Unity、终端或手改 JSON。

| 菜单 | 可进行的操作 |
| --- | --- |
| 音乐 | 默认两首完整 BGM，选歌 / 上下首，添加 MP3 / OGG / WAV，移出歌单；添加后由玩家点击播放 |
| 陪伴空间 | 导入兼容 Spine 4.2 包并切换；始终是同一人物、Core、State、历史和记忆 |
| 人物卡 | 修改名字、口吻 / 性格、与老师的关系与边界，保存应用或重新读取文件 |
| 我们的相处日记 | 玩家决定是否回望从首次使用到现在的完整聊天；可停止整理、浏览已生成回顾、打开 Markdown 文件夹 |
| 资料文件与维护 | 打开资料目录、重新读取、保存文件状态 / 指纹报告、查看普通玩家说明 |

音量与静音仍在 Esc → 设置。当前歌曲一直单曲循环，只有玩家选歌或上下首才切换；切歌交叉淡化，循环边缘音量柔和收放。保留原视频完整约 67.5 / 99.7 分钟音轨，OGG 合计 168,392,311 字节，没有擅自截短。构建报告总计 301,565,220 字节，不含 MP4、ffmpeg 或 8.66 GiB GPT-SoVITS Runtime。循环边缘的音量缓和不能让任意非循环音乐在乐句上无缝。

## 人物卡与独立文件

初始口吻种子在 `Assets/Characters/MainCharacter/Config/identity.json`，仍按 Mika 与老师原作关系设计。新版真正读取的可修改人物卡在：

- Player：`%LOCALAPPDATA%/GiftCompanion/Config/character_card.json`。
- Editor：`W:/Machine/VSCode/Mika_Gift/GiftCompanion/UserData/Config/character_card.json`。

首次运行才从种子创建，之后使用玩家文件；角色 ID 固定，保存不重置历史。自动记忆与日记不会改写卡片。菜单可直接修改，不需要知道路径。

资料根同时包含 `Config/bgm_playlist.json`、`Config/companion_spaces.json`、`Config/diary_policy.json`、普通 `settings.json`，以及 `Content/Music/`、`Content/Spaces/`、`Diary/`、`memory.db`、《使用说明.md》和可生成的《资料检查.md》。随包路径使用 `bundle:`，玩家导入路径相对资料根；不依赖开发机器的素材绝对路径。完整字段、限制和后端调用见 [player_content_contracts.md](<../../02_设计与实现/player_content_contracts.md>)。

配置保留上一版 `.previous.bak`，格式损坏不在启动时覆盖原件。普通文件系统原子替换；当前执行环境遇到文件层跨设备错误时以校验和 `.pending` 副本恢复中断写入，不修改权限 / 加密。请在保存中断后先重启，避免手动删除待提交文件。备份整个资料目录前正常退出，Key 单独保管。

## 日记与记忆关系

日记手动生成，覆盖两个模式全部已完成且未排除的聊天，长历史分批翻阅；不按天自动生成，不只读取最新几轮。数据库保存正文、实际消息来源、版本 / 水位与待导出状态，再输出可读 Markdown。相同来源版本和人物卡复用旧回顾，缺失副本可补回，不必重新联网。

回顾表现角色在记录中说过的感受与关系变化，以及明确标作现在回看的感想；不评好感度，不把生成叙事当作客观心理测量、新事实或永久人格。日期范围由程序显示，提示禁止自行补写天气 / 地点 / 未发生的经历。模型总结仍可能省略或误解，重要信息请回看 MomoTalk 原文。

更正 / 遗忘会保守收起该角色旧日记：数据库清空正文，Markdown 更新为收起说明，不保留旧正文导出备份；原始聊天按既有规则保留。MD 外部编辑不写回数据库，普通查看保留已有副本，遗忘时仍会更新；重要手工批注请另存文件。

## 验证及剩余人工体验

- Final4 EditMode **69/69**、PlayMode **12/12**；包括全历史 45 轮分页、JSON HTTP 跨层预算、迁移备份、遗忘 / 取消竞态、并发导入、损坏文件保留和中断提交恢复。
- 最终发布版 **Write / Restore / NoKey 全通过**：两首长轨真实解码、长轨末尾同曲循环、添加 WAV 不自动切换、卡片 / 空间 / 日记重启恢复、真实 DeepSeek 对话与日记、缺失 MD 修复、遗忘 DB / MD 同步、无 Key 本地查看 / 自然失败、维护报告、1024×768 缩放与退出保存。
- 逻辑回归后只收紧日记提示文字、修正 Windows 专用探针按钮定位；两项由最终构建 / 真实 Player 复验，未冒称旧测试单独覆盖全部发布代码。
- 两个新增空间条目使用同一已交付大厅及其导入副本。通用接口已验证，尚无第二份新美术，不能视为未来任意 Spine 包已适配。外部新包先用基础 Idle。
- 最终 Windows 透明桌面合成、无边框 / 置顶、返回 Home 恢复、SQLite / FTS5 重开通过。人工仍需确认实际文件 / 文件夹选择、中文输入法候选操作、主观音量 / 循环听感和人物口吻。UI 美化按用户安排后续精修。

测试仅用随机 Validation 目录的验收对话，正式历史未读取、修改或迁移。没有自动为用户启动正式新版本。

## 下一步边界

先由玩家体验本轮文件管理、人物卡和回顾，随后精修 UI / 口吻 / 第二套空间适配。Phase 4 再做 GPT-SoVITS 生命周期、PCM 口型、Ducking、缓存与复听；Phase 5 做桌宠输入 / 工具；Phase 6 做完整安装 / Key 引导、备份恢复界面、跨机分发和体积优化。此轮不提前实现这些内容。维护报告便于普通玩家求助，源码审计仍由维护者负责，文件指纹不代表安全审计结论。
