# Phase5 当前状态

2026-10-18，**5A→5F已完成开发与技术验收，等待玩家体验反馈**。用户已批准按方案顺序实施；Phase6及以后仍仅规划。[交付与体验清单](phase5_delivery.md)、[实现与维护](phase5_implementation.md)、[checkpoint](phase5_checkpoint.md)。

| 阶段 | 完成情况 |
|---|---|
| 5A 窗口/热键/恢复 | 原生实际唤醒、冲突/替换/注销、透明命中、位置重置与Home恢复通过 |
| 5B 气泡/文字对话 | 同窗口圆角气泡、IME保护、分页/草稿/隐藏继续接收、同Core流式与历史/日记菜单 |
| 5C 触摸/拖动/LookAt | 真实头/身短点击、摸头、拖动与释放通过；动作/视线有界且文件映射 |
| 5D 工具闭环 | 原生SSE调用、严格参数/权限、同Core门控、同库v10台账、实际结果与取消反馈 |
| 5E 13项工具/玩家维护 | 全部接入；程序命名登记/移除、范围选择/移除、工具开关、敏感确认和记录 |
| 5F 发布/回归/交接 | 独立Phase5构建及启动器，171项自动测试、Live39/NoKey30、截图与占用记录通过 |

最新入口Tools/Start-Phase5.cmd。默认流式普通聊天，仅下一条消息可开启“允许桌面操作”。桌宠始终无TTS，回陪伴空间后日语静音合成及自有服务释放已验证。最终证据为[acceptance.json](Validation/Phase5/acceptance.json)，playerAcceptance=pending，不替玩家确认动作手感或中文IME选词。

Windows Release/Mono/D3D11/Unity6000.3.7f1，错误0，304674581字节，较ExpressionDiary增加141686字节。旧素材/Runtime/构建和验收35项哈希未变，不复制8.66GiB Runtime。正式聊天/剪贴板未用于测试，系统音量仅验证只读连接；敏感写入留给玩家在应用内明确确认体验。

首次使用先备份再从v9迁移v10；之后继续使用Phase5，旧版会拒绝新库。原构建/证据保留历史含义，完整恢复/独立分发与多屏矩阵属于Phase6。早期失败与修复记录留在Validation/Phase5/attempt1、attempt2和阶段日志，不能替代最终通过证据。