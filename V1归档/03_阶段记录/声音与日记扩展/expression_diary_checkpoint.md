# 表达目标与日记排版：完成交接

后续交接已迁移至[Phase5 checkpoint](<../phase5/phase5_checkpoint.md>)。本页保留ExpressionDiary交付时的历史状态。

2026-10-18（Asia/Shanghai）。本轮开发、技术验收和**本次用户阶段验收均已通过**，用户反馈整体效果符合预期。用户恢复任务后已完成暂停时剩余的真实复验及必要修正；本文件保留交付时的进程核对记录。本次按用户要求新增[Phase5方案](<../phase5/phase5_plan.md>)，待审批，尚未开始Phase5/6实现。

## 当前入口与证据

- 双击 `Tools/Start-ExpressionDiary.cmd`，对应 `Builds/ExpressionDiary/GiftCompanion.exe`。默认日语，保留已有活泼/害羞和句间呼吸、中文字幕随实际语音推进。
- [交付与试听](<expression_diary_delivery.md>)、[实现学习说明](<expression_diary_implementation.md>)、[最终验收](<../../04_验收记录/ExpressionDiary/acceptance.json>)。
- Final EditMode **104/104**、PlayMode **24/24**；之后C#未再改。
- Windows Release / Mono / D3D11 / Unity6000.3.7f1，错误0，**304,532,895字节**，42.645秒；比VoicePolish增加**24,881字节**，没有新增模型/参考WAV/依赖，也不复制共享Runtime。
- 最新EXE真实Write **18项**、NoKey **7项**全部通过，报告均晚于本次构建。
- 31个冻结原始/旧程序与证据文件、4条现有参考及其源素材哈希核对通过；15份最终证据与112份C#源码哈希写入acceptance。发布无测试DLL/DPAPI文件，3份许可证齐全。

## 本轮实现

`DialoguePresentation.VoiceIntent`与原8种共享Mood分开。META支持可选voice属性，旧双属性头兼容，未知目标回退合法emotion。CharacterCore→CompanionScreen→CompanionVoicePlayer→独立SpeechLocalizer→同模型TTS→音频sidecar贯通。犹豫/平直不扩充数据库枚举，schema仍v9。

`Config/voice_delivery.json` v2：annoyed原参考/1.02，sad原参考/0.92，hesitant复用ja-shy/0.93，flat原参考/1.00。原活泼/害羞保留，只升级内置旧默认并补映射，玩家自定义优先。恼火/伤心是措辞与节奏近似，没有独立强情绪录音或新训练；flat不能保证音高恒定，不使用AudioSource.pitch变调。

`Config/speech_localization.json` v3：克制恼火、低落不强加哭声、犹豫不把肯定改猜测、平直减少夸张。仍是原Flash非思考、15秒/384token/64条有界缓存，无整库历史或记忆/工具权限；只迁移未改过的v1/v2默认规则。

`RelationshipDiary`最终提示返回paragraphs数组，`DiaryTextLayout`统一自然段；旧diary字符串响应兼容。默认目标160字/最长260字，超长段优先句末补空行，无标点时有界切分且不拆UTF16代理对。只改空白，不删改文字、不另花一次修复API调用。DB/UI/MD共用同一正文。

`Config/diary_policy.json` v2：MaximumKaomoji默认4，可0–12；短0–1、中1–3、长2–4稀疏使用，不强制填满。旧v1上限1升为4，0或其他自定义值保留。narration-v3与策略加入指纹，仅玩家下次主动生成时写新回顾，已有MD不自动改写。最新隔离样例3段（124/122/130字），使用2个颜文字，DB/MD一致。

玩家从声音面板“使用说明”打开《表达与日记说明.md》。日记在Esc→资料与回顾→我们的相处日记→回望从相识到现在；“打开日记文件夹”查看MD。独立文件/后端接口继续符合一切皆文件，不让LLM直接改资料。

## 多轮协议遵循问题及最终处理

最初与暂停恢复后的两次真实Write都在第3/4轮省略META，hesitant/flat回退neutral/ja-bright。仅强化系统提示没有解决，失败证据保留在 `Validation/ExpressionDiary/FirstPublished/` 与 `SecondPublished/`，不能当成最终通过样例。

最终修正：`PromptBuilder.HistoryAssistant`只在Companion请求中为近期assistant历史重建白名单格式头，给模型一致的角色消息示例。已投影的Companion消息只读原文前256字符；raw==body的未投影旧消息不猜标签，不整段重放原始控制文本。新增头计入原12000字符预算，整轮对话成对保留；数据库/显示/记忆正文不变，AgentPet请求仍只有正文。系统提示要求按当前语义重新选择，不沿用历史情绪。

新增2项测试覆盖旧文字、跨模式、正文不变与预算；随后104+24项、重新构建与真实四轮全部通过。概率模型不能被宣称绝对服从；缺头仍安全回退，不根据玩家文字硬猜情绪。

## 实测文件与数据边界

- 最终样例 `ja_annoyed.wav / ja_sad.wav / ja_hesitant.wav / ja_flat.wav` 均为真实缓存首段，48kHz单声道PCM16，时长3.66/4.16/6.537/3.00秒。原句、译文、Mood、参考/语速见sample_texts.json；qualityAccepted仍false，主观声音效果由玩家判断。
- `player_run.json`记录最终隔离数据：`%LOCALAPPDATA%/GiftCompanion/Validation/expressiondiary-1098bf72a4124fbaa83d1d1fb91f646a`。4轮全是语气演示，没有读取或改写正式聊天/人物卡。
- NoKey验证无翻译请求、无Runtime启动即可复听新目标缓存和恢复日记；Home/退出释放与保存通过，单GPU推理上限1。
- `process_review.json`确认本项目Runtime Python与ExpressionDiary Player为0；没有停止其他Unity/Material制作/Python进程。
- `pause_manifest.json / pause_process_review.json`仅为00:38暂停基线，后续源码和实测已变，不应拿旧快照验证最终交付。暂停及恢复过程原文另存 `checkpoint_history.md`。

## 后续接手

当前无需返工或重复跑已通过测试。用户已确认本阶段效果符合预期，后续按新反馈再细修；下一步为[Phase5方案](<../phase5/phase5_plan.md>)审批，状态见[phase5_status.md](<../phase5/phase5_status.md>)。按用户后续要求，[Phase6方案](<../phase6/phase6_plan.md>)与[Phase7—9可选路线](<../../../GiftCompanion/Docs/post_phase6_roadmap.md>)也已提供；均为规划，不跳过Phase5开始实施。本次概括验收不推定所有硬件组合或未提供资源均已验证。学习从DialoguePresentation→PromptBuilder/CharacterCore→CompanionScreen→CompanionVoicePlayer→SpeechLocalizer/VoiceDelivery阅读；日记从GenerateAsync→DiaryTextLayout→提交/导出阅读。普通玩家先用菜单，配置路径与示例在学习说明。

有新C#改动时再跑必要回归、构建、隔离Write/NoKey，并重新生成acceptance；不能复用旧构建报告。工具为 `Test-Phase3.ps1 -Mode Both -Checkpoint Final -ReportDirectory .../Validation/ExpressionDiary`、`Build-ExpressionDiary.ps1`、`Test-ExpressionDiaryPlayer.ps1`、`Save-ExpressionDiaryAcceptance.py`。联网Key只进子进程环境，不打印；全部沿用随机Validation资料。

用户要求额度低于7%立即停并写checkpoint。当前工具不提供账户额度百分比，无法可靠监测，已向用户说明；本轮无可见低额度提示。以后若收到可见提示，立即停止新增工作保存交接，不编造余额。Phase5/6仍未开始。
