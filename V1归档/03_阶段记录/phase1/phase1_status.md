# Phase 1 状态

2026-10-12。用户已批准 `phase1_plan.md`，已按检查点顺序完成实现与验收，**Phase 1 通过**。

用户随后确认本阶段已验收，整体效果符合预期，并反馈 Spine 有些过曝。该反馈仍登记为 Phase 3 视觉对齐事项，未修改渲染基线。后续 Phase 2 于 2026-10-13 经暂停 / 恢复后完成，见 `phase2_status.md`；下文保留 Phase 1 验收时结果。

| 检查点 | 状态 | 证据 |
| --- | --- | --- |
| 1A 配置、日志与正式数据库 | 已通过 | `Validation/Phase1/1A.EditMode.xml`，15 / 15；Key 工具目标语法检查通过 |
| 1B 共享 LLM / CharacterCore 最小文字链 | 已通过 | `1B.EditMode.xml` 22 / 22；`core_probe.json` 真实请求通过 |
| 1C 切换、取消、恢复与退出 | 已通过 | `1C.EditMode.xml` 23 / 23、`1C.PlayMode.xml` 5 / 5 |
| 1D 集成验收 | 已通过 | 最终 EditMode 24 / 24、PlayMode 6 / 6；零错误 Windows 构建，Write / Restore / NoKey 与真实透明合成均通过 |

完整 Memory、正式对话 UI、VoiceService 与 Agent Tools 保持后续阶段边界。

最终证据均位于 `Validation/Phase1/`：`EditMode.xml`、`PlayMode.xml`、`core_probe.json`、`player_build.json`、`player_write.json`、`player_restore.json`、`player_nokey.json`、`Window/player_probe.json`、`Window/window_composition.json` / `.png` 与 `acceptance.json`。

正式 DatabaseService 已使用 SQLite schema v1，只有 app_state；短期状态可退出重开恢复。CharacterCore / LLMService 已接通真实文字 Streaming，开发探针按显式入口触发，正式聊天面板尚未制作。无 Key 不影响 Home 和素材播放。Editor Key 不自动复制到 Player，配置工具已支持 `-Target Editor` / `-Target Player`。

Windows 实际验收使用 `%LOCALAPPDATA%/GiftCompanion/Validation/` 下的随机隔离目录，保留数据供复核，不覆盖正式用户状态。默认 Player / Editor 路径解析另有纯逻辑测试。真实 Key 只传入联网测试子进程环境，不写入测试目录或构建。

编辑器场景回归也使用每项测试独立的 Validation 目录，6 / 6 复测通过。最初 Smoke 自行创建的 Editor 测试状态库经只读核对后移至 UserData/Validation 留存，Secrets 未改变；普通运行重新创建正式状态库。测试隔离入口只编译进编辑器。

首轮构建缓存错误及窗口检查失败记录保留。CleanBuildCache 后构建错误为 0；窗口探针改为等待真实原生状态。透明合成脚本处理 150% DPI 坐标虚拟化后，实际物理客户区 600×500，3 个背景角点匹配，返回 Home 恢复普通窗口。已查看截图；不将此宣称为多显示器 / 任意缩放的完整验收。
