# Phase 4 当前状态

2026-10-18后继状态：当前ExpressionDiary陪伴/语音/日记扩展已获本次用户阶段验收，见[最新交接](expression_diary_checkpoint.md)。下一步为[Phase5方案](phase5_plan.md)，尚未实施；下方保留原4A—4E交付时的技术记录。

2026-10-17。**Phase4 4A—4E 开发与发布技术验收完成，待玩家测试反馈。** [交付与测试说明](phase4_delivery.md)、[文件接口](player_content_contracts.md)、[阶段方案](phase4_plan.md)、[恢复记录](phase4_checkpoint.md)。验收证据保存在 Validation/Phase4/，最终 acceptance.json 由离线汇总器保存。

- 正式中文分句语音、自有模型服务生命周期、打断 / 迟到过滤、实际 PCM 口型与字幕、BGM Ducking、文件缓存 / schema v9 索引、MomoTalk 离线复听及声音管理面板已完成。
- **Final2 EditMode 78/78、Final3 PlayMode 19/19**。两次最后修订仅涉及 Runtime 的符号回复显示 / 坏缓存提示及相应用例；Core 在78项通过后未再改变。早期编译 / 音乐测试失败日志保留。
- 最终 Windows Release / Mono / D3D11 构建零错误，**301,676,459 字节**；沿用已有约8.66GiB Runtime，不重复复制。
- 最终 **Write / Restore / NoKey / Faults 全通过**：真实中文推理与 DeepSeek、取消保持推理门控、PCM / Ducking、缓存整组提交、重启 / 无服务 / 无 Key 复听、缺段禁用 / 无暗中补生成、清缓存保留聊天、自有服务崩溃 / 菜单重试、外来端口保留、缺运行时文字可用、1024×768、启动中离开与快速重入 / 正常退出。
- 最终本机加载约21.104秒；短回复文字约1.637秒完成、首音约5.910秒，未观察真实短回复生成 / 播放重叠（actualOverlapObserved=0）。控制流测试证明首段不等待全文；不通过拖慢文本制造观测重叠。
- 已查看最终字幕 / 口型、声音面板、MomoTalk复听、无Key本地记录与1024×768声音就绪图；主画面不显示技术状态。
- 模型准备 / 不可用时当前轮继续文字；没有可朗读内容的合法回复（如☆）仍显示正文。新轮在服务Ready后才使用语音。

普通入口：双击 Tools/Start-Phase4.cmd，声音设置在 Esc → 设置 → 声音与复听。首次正式启动先备份并升级旧库到v9，旧Phase3Plus不能读取新库；正式聊天库未用于本轮测试。旧构建 / 原第三方API与模型 / 冻结素材保留。

声音相似度、主观语气 / 音量 / 停顿与字幕节奏等待玩家反馈。默认中文，日文独立资源仍在但不宣称日文整链已验收；第二份新Spine待交接，通用空间无嘴部映射时安全回退。日记仍待玩家体验，Phase5 / 6 不提前启动。
