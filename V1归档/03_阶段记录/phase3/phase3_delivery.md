# Phase 3 文字陪伴交付

2026-10-14，Asia/Shanghai。按批准的 3A → 3E 顺序完成初版开发与技术验收，供用户体验和精修；人工观感 / 真实中文输入法验收待用户确认。没有进入 Phase 4。

## 启动与操作

在 PowerShell 运行：

```powershell
& 'W:\Machine\VSCode\Mika_Gift\GiftCompanion\Tools\Start-Phase3.ps1'
```

该入口启动 `Builds/Phase3/GiftCompanion.exe`，沿用已配置的 Editor Key，仅传入子进程环境，不重复输入或复制秘密文件。若已单独配置 Player Key，可加 `-UsePlayerKey`，或直接启动 EXE。请保留整个 Phase3 构建目录，不能单独移动 EXE。

进入 Home 选择“陪伴空间”。平时只显示完整大厅、时钟和短暂操作提示；Enter 打开输入，再次 Enter 发送，Shift+Enter 换行。中文选词 Enter 有组合保护，但真实输入法操作仍请体验。Esc 先收起输入 / 子面板，再返回菜单或大厅。新消息会取消并等待旧轮收尾，再显示最新回答。

Esc 菜单提供 MomoTalk、设置、返回 Home、退出。MomoTalk 每页 30 轮，两个模式共享历史，显示完整正文；“不记住本轮”影响未来回忆，原始聊天仍保留。设置支持 BGM 音量 / 静音、字幕字号和按明确记录移除记忆；显式“保存设置”才持久化，关闭未保存面板恢复原值。历史面板读取打开时的快照，重新打开可刷新。

“来到桌面”继续使用既有 CHDDD V7 和透明窗口；AgentPet 的最终产品输入、拖动和工具仍留到 Phase 5。Phase 2 开发面板普通启动隐藏，原阶段交付与证据保留。

## 本次完成

- Mika / 老师基础只读人设、随包中文字体、中文 Home、沉浸式输入、流式 / 分页字幕、Clock、菜单与 UI 触摸接管。
- 首行 META 增量解析，schema v7 保留模型原文和正文投影，完整事务提交 Mood；取消 / 失败不保存伪造回复或临时情绪。
- 共享历史游标分页、明确轮次 / 记忆遗忘、持久设置，以及有限 BGM 循环 / 切歌 / 淡入淡出 / 释放能力。
- 发布版异常白色覆盖修正：场景自有运行时材质使用显式 Straight Alpha Shader。保留原图集 / 骨架 / 第三方加载器、Built-in / Gamma / D3D11 和 HDR；全局八情绪只有 neutral 持久视觉，其余明确回退，模型动作仅 none。

## 验证证据

最终 EditMode **59 / 59**、PlayMode **10 / 10**。新 EXE 真实 DeepSeek UI Write / NoKey、跨模式记忆 Write / Restore / NoKey、透明窗口 / Home 恢复全部通过。1280×720 与 1024×768 截图已查看，小按钮文字、字幕和时钟可读；实际透明客户区 600×500，四角均匹配临时纯色背景。

同一 Idle 1.25 秒发布图的接近纯白像素由 **254,655 → 1,744**；修正后的编辑器画面为 1,632。统计包含发布 UI，因此不是逐像素相等的承诺。Unity 原 Shader 分支异常的内部根因未完全隔离；已验证显式 Alpha 适配有效，不把问题归咎于原始素材。

Windows Release / Mono / D3D11 构建错误 **0**，BuildReport **133,051,035 字节**（约 133.1 MB），16.28 秒。字体 OFL / 版权 Notice 与 Spine Runtime 许可已随包复制。862 个冻结导入文件全部哈希一致；构建中秘密 DPAPI 文件和项目测试 DLL 均为 0。

综合报告见 [acceptance.json](<../../04_验收记录/Phase3/acceptance.json>)，画面对比见 [visual_review.json](<../../../GiftCompanion/Docs/Validation/Phase3/visual_review.json>)，实现说明见 [architecture.md](<../../02_设计与实现/architecture.md>)。`FirstUI/` 与 `PausedRendering/` 保留较早失败画面，暂停 `checkpoint.json` 只作历史；最终证据对应当前 EXE / Core / Runtime DLL 哈希，不复用旧构建通过声明。

## 数据与本轮限制

普通 Player 数据为 `%LOCALAPPDATA%/GiftCompanion/`，编辑器为工程 `UserData/`。普通启动会先备份并版本化迁移已有数据库到 v7；自动验收只使用 Validation 隔离库，没有打开或迁移正式聊天库。Key 不随构建，日志不记录完整聊天。

BGM 按用户决定保持空列表、静音。正式语音、真实音频驱动口型、Ducking、缓存和历史复听属于 Phase 4；当前复听不可用。GPT-SoVITS 没有在本轮启动或重复复制，中文仍为后续默认语音。

请体验 Mika 口吻、整体明暗、字幕字号 / 停留、中文选词与触摸手感；这些需要人工确认。已检查的窗口尺寸和本机单屏透明合成不代表所有 DPI / 多显示器全面通过。启动脚本已保存并语法核对，本轮未额外启动正式数据目录的交互实例。
