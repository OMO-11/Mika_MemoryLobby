# Phase 3 状态

> 2026-10-15 后续：基础 Phase 3 保留已验收；新增 Phase 3F 文件化资料 / 长期回顾按用户要求暂停，源码与本文旧版基线不同，尚无 Phase3Plus 发布。最新续接见 [phase3plus_checkpoint.md](phase3plus_checkpoint.md)。

2026-10-14。用户恢复任务后，按批准顺序完成 3A—3E 初版开发与技术验收，**等待用户体验 / 精修确认**。启动与操作见 [phase3_delivery.md](phase3_delivery.md)，综合证据见 [acceptance.json](Validation/Phase3/acceptance.json)。暂停交接和 checkpoint.json 保留历史，不作为当前源码哈希。

| 检查点 | 实际状态 | 证据与边界 |
| --- | --- | --- |
| 3A 视觉 / 人设 / 映射 | 初版完成，发布画面已修正 | Mika / 老师只读配置、官方中文字体；只有 neutral 持久视觉，其余明确回退，动作仅 none |
| 3B META / Core | 通过 | 任意分片 / 降级，原文与正文分离，schema v7 迁移 / 事务，Mood 失败回退 |
| 3C 状态 / 输入 / 字幕 / Clock | 通过技术检查 | 文字状态、UI 捕获、旧轮取消并等待、IME 保护、字幕分页；真实输入法选词待人工体验 |
| 3D MomoTalk / 设置 / BGM | 通过 | 共享历史游标 / 遗忘、持久设置、合成循环 / 淡出；用户 BGM 空列表静音 |
| 3E 集成 / Windows | 技术验收完成 | 最终 59/59、10/10；新 EXE 真实 UI / 跨模式记忆 / 重启 / NoKey / 透明窗口全部 passed，发布图已查看 |

当前构建 `Builds/Phase3/GiftCompanion.exe`：Unity 6000.3.7f1、Built-in / Gamma / D3D11、Windows Mono Release，零错误，BuildReport 133,051,035 字节，16.28 秒。Spine 采用场景专用显式 Straight Alpha Shader，保留 HDR 和相机轻量高光调整；原始素材 / 第三方加载器未改。原 Shader 内部异常根因不作最终定论。

同帧发布图接近纯白像素 254,655 → 1,744，修正编辑器图为 1,632；小按钮和时钟 / 提示可读。最终 1280×720 / 1024×768 UI 与本机透明合成通过，600×500 客户区四角匹配背景，Home 恢复普通 1280×720。字体 OFL / Notice、Spine 许可随包保存；冻结导入文件 862 个全部哈希一致。

全部自动验收使用 Validation 隔离库，Key 只在测试子进程环境使用，不打印、不复制到构建。未迁移正式用户聊天库，Phase 2 已验收交付保留。`FirstUI/` / `PausedRendering/` 保存历史失败与暂停证据；最终报告、源码及三份程序文件哈希见 acceptance.json。

后续先由用户体验画面、Mika 口吻、字幕节奏、实际输入法和触摸，再做细节精修。BGM 曲目待用户提供；正式语音、PCM 同步、Ducking、缓存 / 复听属 Phase 4；AgentPet 最终交互 / 工具属 Phase 5。没有启动 TTS 或提前实施后续阶段。
