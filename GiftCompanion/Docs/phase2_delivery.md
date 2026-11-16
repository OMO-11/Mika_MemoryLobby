# Phase 2 交付与验收入口

2026-10-13。Phase 2A → 2E 已完成。最终 Unity 6000.3.7f1 EditMode **51 / 51**、PlayMode **7 / 7**；Windows Mono / D3D11 干净构建错误 **0**。构建目录 `Builds/Phase2/`，程序包约 176.9 MB；精确字节数见 `player_build.json`，语音 Runtime 保持工程原位置，本阶段没有复制第二份或启动 TTS。

用户已于 2026-10-13 确认本阶段验收完成、整体效果符合预期。后续 [Phase 3 方案](phase3_plan.md) 待审批，本交付和技术证据继续保留。

## 手动体验当前记忆能力

推荐在 PowerShell 运行：

```powershell
& 'W:\Machine\VSCode\Mika_Gift\GiftCompanion\Tools\Start-Phase2Dev.ps1'
```

该脚本打开可交互的开发程序，沿用本机已经配置的 Editor Key，仅通过子进程环境传入，不显示 Key、不复制秘密文件。运行数据依然属于 Player 的 `%LOCALAPPDATA%/GiftCompanion/`，首次运行新版本会原生备份并升级已有库。本轮自动验收全部使用另外的 Validation 隔离库，没有迁移正式库。

若已单独配置 Player Key，可传 `-UsePlayerKey`。也可直接运行：

```powershell
& 'W:\Machine\VSCode\Mika_Gift\GiftCompanion\Builds\Phase2\GiftCompanion.exe' -phase2-dev
```

直接运行 EXE 使用 Player Key / 启动进程环境，不自动读取工程内 Editor 秘密目录。长期配置入口为 `Tools/Configure-DeepSeek.ps1 -Target Player`，隐藏输入；当前本机只检测到 Editor Key，开发启动脚本可直接沿用。

在 Home 选择 Companion 或 AgentPet 后，用临时面板发送文字。面板支持查看事实 / 事件 / 未完成事项，按显示的确定记录点“忘记这条记忆”，或用“不记上一轮”排除整轮来源；原始聊天仍在本地数据库中。“重试失败的提取 / 摘要”为显式操作。可收起面板；普通启动不带 `-phase2-dev` 时隐藏。

编辑器中运行 Bootstrap 后，使用菜单 `Gift Companion > Phase 2 > Open Development Panel`，继续沿用现有 Editor 配置。该面板用于本阶段验收，正式沉浸式输入 / 字幕 / MomoTalk 留到 Phase 3。

建议人工验收：在 Companion 明确告诉角色一个新事实，经 Home 切到 AgentPet 提问；反向重复一次；修改该事实再问；通过面板遗忘，再问并检查活跃记忆列表。正常关闭重开后，仍先进入 Home，未遗忘的事实可继续回忆。

## 已完成验证

| 验收 | 当前证据 |
| --- | --- |
| 核心 / 场景回归 | `Validation/Phase2/2E.EditMode.xml` 51 / 51；`2E.PlayMode.xml` 7 / 7 |
| 编辑器真实接口前置验收 | `core_write.json` / `core_restore.json` / `core_nokey.json` 全部 passed；该轮 schema v5，随后引用追踪细化为 v6 |
| 最终 EXE 真实记忆 | `player_write.json`：双向跨模式、移出近期窗口的长期回忆、纠错、遗忘上下文排除、原文与独立事实保留均通过 |
| 最终 EXE 重开 / 无 Key | `player_restore.json` / `player_nokey.json` 全部 passed；启动 Home、持久事实、近期窗口外校验、唯一根与退出保存通过 |
| 窗口与素材回归 | `Window/player_probe.json`、`Window/window_composition.json`：物理 600×500 透明 / 无边框 / 置顶，3 个角点与临时背景匹配，返回 Home 恢复普通 1280×720 |
| 构建 | `player_build.json`：Succeeded，errors=0，Direct3D11，Flip Model 关闭 |

已查看新 EXE 窗口截图。该截图只覆盖测试程序客户区；3D 仍沿用冻结 V7 的现有偏粉材质，不将窗口通过等同于最终美术验收。Spine 过曝仍登记到 Phase 3。

实际 Player 验收使用 `%LOCALAPPDATA%/GiftCompanion/Validation/` 下随机子目录，位置见 `Validation/Phase2/player_run.json`；编辑器真实验收位置见 `core_acceptance.json`。Key 只传入测试进程环境；报告保存布尔条件、计数、时间和来源元数据，不保存 Key、请求头或聊天正文。完整测试对话仅保留于隔离数据库供本地复核。

## 存储与任务规则

schema v6：v2 原始对话 / 会话，v3 记忆 / 来源，v4 FTS5，v5 摘要来源 / 版本，v6 具体记忆记录与回复的引用关系。已有库在事务迁移前原生备份，app_state 的内部 state_version 仍为 1。

完整回复落库后后台提取；前台优先，提取 / 摘要各使用受限 JSON，后台合计最多一个云请求。模式退出、10 个新完整轮次或 8,000 字符触发摘要，退出不等云端摘要。摘要保存覆盖位置及来源，失效后从允许的原文重建，不替代原文。

纠错保留 superseded 历史，遗忘使用 forgotten。消息引用与具体记忆引用分别追踪，能排除目标的复述和旧任务，同时保留同条原文中的独立结构化事实及其后续回答。Source / 索引 / 摘要 / 近期上下文保持一致；重新记住需新的明确陈述或授权。

事实键归一化以复用主题，事实值按原文精确保留；识别码大小写改变会形成新的活跃版本。普通运行升级数据后应使用 Phase 2 程序；旧 Phase 1 会按版本保护拒绝写入新版库。

检索为 FTS5 词面匹配，模型提取仍受语境理解影响；本次验证不承诺任意同义改写和所有语义判断都正确。当前没有主动提醒或桌面工具执行，也没有完整自然语言遗忘对象消歧 UI。正式 UI、八情绪 / 动作、语音队列、Agent Tools 和最终分发按原阶段推进。

综合证据与源码 / 构建哈希见 `Validation/Phase2/acceptance.json`。2C 暂停快照 `checkpoint.json` 保持原样，后续恢复与收尾过程见 `development_log.md`。
