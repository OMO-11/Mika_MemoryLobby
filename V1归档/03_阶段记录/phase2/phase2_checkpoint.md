# Phase 2C 暂停与续接记录

后续说明（2026-10-13）：用户已明确恢复，2D / 2E 已完成，最新入口见 `phase2_delivery.md`，状态见 `phase2_status.md`。下文和 `checkpoint.json` 保留 2C 暂停时的快照，不作为最新待办；上次中断遗漏的哈希文件已在 2D 修改前补存并标注实际捕获时间。

2026-10-13。用户明确要求完成 2C 后暂时停止。**2A、2B、2C 已通过；当前暂停，2D 和 2E 未开始。** 不自动继续后续开发。

## 已保存的实现

| 部分 | 当前交付 |
| --- | --- |
| DatabaseService / ConversationStore | schema v1 → v4 事务迁移、原生备份、原始消息、会话、中断恢复、幂等完成与交互状态共同提交 |
| MemoryStore / MemoryExtractor | memories / user_facts / open_loops、用户原文证据校验、去重、基础事实版本更新和事项解决 |
| MemoryService | 唯一共享后台处理器、持久化待办、前台优先、失败显式重试、应用级取消和退出收尾 |
| MemoryRetrieval | FTS5、CJK 单字 / 双字预处理、英文标识符、active 过滤、相关度 / 重要度 / 时间评分和近期 6 轮 |
| PromptBuilder / LLMService | 固定上下文顺序、12,000 字符总上限、真实 user / assistant 历史角色、当前输入一次、来源依赖和上下文版本检查 |
| AppRoot / CharacterCore | 正式服务装配、先保存用户原文再请求、完整对话事务、跨模式近期上下文、切换关闭会话与后台收尾 |

代码位于 `Assets/App/Core/Memory/`、`Assets/App/Core/Persistence/`、`Assets/App/Core/LLM/`、`Assets/App/Core/Character/CharacterCore.cs` 与 `Assets/App/Bootstrap/AppRoot.cs`。新增测试位于 `Assets/Tests/EditMode/ConversationTests.cs`、`MemoryExtractionTests.cs`、`MemoryRetrievalTests.cs` 和 `MemoryTransportTests.cs`。

schema v2 建 messages / sessions；v3 建 memories / user_facts / open_loops、memory_sources、memory_meta、excluded_messages、prompt_dependencies；v4 建 memory_search。`app_state.state_version` 仍为 1，与数据库 schema 版本不同。升级前备份路径为同目录 `.pre-v4-随机ID.bak`，新建空库不伪造历史数据；更高版本仍拒绝写入。

## 实测证据

| 检查点 | EditMode | PlayMode |
| --- | --- | --- |
| 2A | 29 / 29 | 6 / 6 |
| 2B | 33 / 33 | 本检查点未另跑，后续 2C 已回归 |
| 2C 最终 | 41 / 41 | 6 / 6 |

报告位于 `Validation/Phase2/`，以 `2C.EditMode.xml` / `.log`、`2C.PlayMode.xml` / `.log` 为当前代码最终测试证据。2C 初次 40 / 40 后，收尾补充中英文连续书写代号检索和上下文版本变化的事务检查，最终 41 / 41、6 / 6。新增源码及最终测试哈希汇总见 `Validation/Phase2/checkpoint.json`。

所有验收使用 Validation 隔离库，未迁移用户正式数据、未更改 Secrets、未发起本阶段真实 DeepSeek 请求，未启动语音服务。原始 Material、冻结 Unity V7 与 Spine、渲染基线均未修改。没有构建 Phase 2 EXE，现有 `Builds/Phase1/GiftCompanion.exe` 仍为历史 Phase 1 版本。

复验入口：在工程根的 PowerShell 调用 `GiftCompanion/Tools/Test-Phase2.ps1 -Checkpoint 2C`；默认执行两个测试模式，也可传 `-Mode EditMode` 或 `-Mode PlayMode`。需先关闭占用该工程的 Unity 编辑器；脚本按 ProjectVersion.txt 查找 6000.3.7f1，窗口隐藏运行。此命令不调用真实云 API。

## 恢复时的下一步

用户明确恢复后，从 `phase2_plan.md` 的 **2D** 开始：

1. 实现模式退出 / 长会话 / 新消息阈值触发的后台摘要，保存覆盖位置和可追溯来源；当前仅有字段与 PromptBuilder 插槽，没有实际摘要任务。
2. 实现明确对象的遗忘接口和可验证的来源排除。不能只改 status；需处理近期原文、依赖该事实的 assistant 复述、摘要、索引及正在运行的旧提取。
3. 完整验证纠错 / 遗忘后的回流治理、版本竞态和摘要失效重建。当前基础 superseded 与直接来源排除不等于该完整链已验收。
4. 2D 通过后再做 2E：显式开发输入 / 验收入口、真实 DeepSeek 双向跨模式回忆、关闭重开、Windows 新构建及窗口回归。历史 Phase 1 Player Probe 的 schema v1 固定判断届时需随新版探针同步，不能拿旧 EXE 当本阶段交付。

本阶段检索为词面匹配，支持实际中文 / 日文片段及混合代号；同义改写命中率尚待真实联调。提取保留原文证据，但模型对事实主题、否定和语境的语义判断仍需 2E 联调 / 人工审阅。无 Key 时本地 Memory 可查询，不表示云端提取可用。角色 persona / relationship 仍保持用户尚未填写的状态。

Spine 过曝仍登记在 Phase 3，未调整；完整 UI、语音队列、Agent Tools 和后续分发保持原阶段边界。
