# 宪法第50节逐项核对

2026-10-19。旧功能沿用原验收与本轮EditMode/PlayMode的回归，最终数量见交付记录；新增目录/资料边界以Phase6原生结果为准。此表不是玩家验收签字。

| 第50节要求 | 实现/主要证据 | 本轮范围 |
|---|---|---|
| 启动选择两个模式 | Home/AppController；SceneLifecycleTests | 新入口回归 |
| 两模式唯一角色意识 | 单AppRoot/Core、共享服务引用测试 | 维护不创建第二Core |
| Companion使用Spine | 原SpinePresentation与空间清单；Phase3/3Plus交付 | 资产冻结 |
| 没有常驻聊天窗口 | CompanionScreen临时输入/字幕；既有人工验收 | 维护仅Home临时覆盖层 |
| Enter快速输入 | Companion输入控制；Phase3验证 | 保留 |
| LLM Streaming | Core/DeepSeek SSE；Phase5 Live与流解析测试 | 保留 |
| Thinking角色动画 | Spine/Pet呈现映射；Phase3/5 | 保留 |
| 分句GPT-SoVITS | SpeechSession；Phase4与双语交付 | 包内Runtime验证 |
| 首段不等全文 | 有界逐句合成/播放管线；SpeechSession测试 | 保留 |
| 说话BGM降低 | BgmManager duck/hold/fade；Phase4/VoicePolish | 资料页离场仍先淡出 |
| MomoTalk历史 | ConversationHistoryService；历史测试 | 资料快照包含同库 |
| 缓存语音复听 | AudioCache/历史页；Phase4缓存验证 | 缓存可选，缺失不重合成 |
| 打断说话 | Core取消、SpeechSession晚到过滤；Phase4 | 维护前停止并排空 |
| 跨模式共享记忆 | 同库Retriever/Memory；Phase2、共享引用回归 | 原生恢复后再切模式 |
| 长期事实更新 | 事实supersede/纠错/遗忘；Memory测试 | 快照保留当时状态 |
| AgentPet原始骨骼 | CHDDD V7冻结；Phase1/5 | 不重导模型 |
| Blender动作可加Clip | Phase1动画导入/独立Clip管线与manifest | 沿用资源接口，不加新动作 |
| 桌宠拖动 | PetInteraction/PetNative；Phase5真实输入 | 当前DPI记录，旧证据保留 |
| 点击/摸头 | HitTester/PetInteraction；Phase5原生命中 | 同上 |
| 看向鼠标 | Pet视线/骨绑定；Phase5与配置 | 同上 |
| 透明区域穿透 | 一像素Alpha/PMv2物理坐标；Phase5合成/点击 | 无更换窗口栈 |
| 全局热键/无箭头Bubble | PetHotkeyHost/PetScreen；Phase5 | 维护切换释放旧场景 |
| 拖动隐藏Bubble | PetInteraction/PetScreen；Phase5 | 保留 |
| Level1 Tools | 13项白名单、权限确认/审计；Phase5 Live39 | 恢复后授权重置 |
| 无任意Shell | 固定平台工具与参数校验测试 | 无新增执行工具 |
| 工具失败不能声称成功 | 工具结果回传/失败历史；Phase5 Live | 不重放旧任务 |
| API错误不崩溃 | LLM错误码/草稿保留、NoKey30 | 新掩码配置和无Key向导 |
| TTS失败可文字对话 | Voice状态/失败回退；Phase4/VoicePolish | 包内依赖/拥有进程检查 |
| 重启后Memory仍存在 | SQLite v10/中断恢复；Phase2/5 | 新完整快照/事务恢复 |

额外资料保护证据：PlayerBackupTests（WAL快照、语音包含/精简、MD批注、路径攻击/篡改/新版本拒绝、取消、中断回滚、DPAPI）；MemoryExtraction维护租约等待收尾；SceneLifecycle维护期间拒绝切换。最终独立包证据汇总见Validation/Phase6/acceptance.json，未完成前不将该文件视作已通过。
