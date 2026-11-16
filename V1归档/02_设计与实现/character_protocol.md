# 角色协议：Phase 3 实现

2026-10-18补充：下文为原始协议沿革。当前数据库为v9；META可选新增`voice`字段，`emotion/action`和8种Mood不变，声音按实际PCM播放。新表现契约见本文末与[实现说明](<../03_阶段记录/声音与日记扩展/expression_diary_implementation.md>)。

更新：2026-10-16。Identity、Relationship、Memory、CharacterState 分开。Identity 从用户 Config/character_card.json 解析，缺失才从只读 identity.json 种子建立。玩家可通过人物卡面板主动修改名字、口吻和关系；角色 ID 固定，AppRoot 等请求收尾再应用到原 Core。MemoryService、LLM 与日记不得改写。模式固定为 Home / Companion / AgentPet，InteractionSource 为 Companion / AgentPet；两个身体共用唯一 CharacterCore、State、历史与记忆。

## 当前已实现

CharacterCore 的 SendMessageAsync / StreamMessageAsync 调用正式共享 LLMService，使用 deepseek-flash；无 Key 返回 LLMNotConfigured。成功 DialogueResult 包含自然语言 Text、request_id 及可选 DialoguePresentation；LLMChunk 可表达正文增量、元信息或结束。取消 / 失败不保存不完整 assistant，UI 不直接发 HTTP 或读写数据库。

Emotion 枚举固定为 neutral、happy、shy、concern、sad、surprised、annoyed、sleepy。Companion 使用 META 解析与正文流；AgentPet 仍为普通文字，提取 / 摘要维持已有 JSON 协议。原始消息和记忆支持 schema v8，v7 正文投影不变，v8 仅增加日记及来源；引用 / 遗忘规则见 `memory_protocol.md`。

Spine Adapter 提供 SetState / SetEmotion / PlayAction，状态含 Idle、Listening、Thinking、Speaking、Touched、SpecialAction、MomoTalk、Menu。情绪与状态独立，Manifest 允许的模型动作只有 none；neutral 为唯一已验证持久视觉，其余情绪均明确回退 neutral。三个 pat 预设仅供触摸，底层 PCM 组件保留但此阶段不播放语音、不假造口型。

## 已确认的设计方向

用户确认采用 Mika 与老师的原作关系基调，角色称呼用户为“老师”；允许小幅调整大厅构图与布局。Identity 的 displayName、Persona / Relationship 和 behavior 已填入基础草案，待用户体验口吻后精修；不虚构共同经历，不让记忆改写人格。

## META 与提交契约

Companion 按宪法使用首行控制头，后接普通自然语言：

```text
<META emotion="happy" action="none">
老师，今天过得怎么样？
```

纯 C# CompanionDialogueParser 处理任意分片、CRLF 与头 / 正文同片；头部缓存上限 256 字符，只解释开头的控制头。未知 emotion 回 neutral、未知 action 回 none；具体动作仅来自已验证 Manifest。缺失或非法 META 不中断正文，已识别的控制信息不进入字幕；超长头丢弃至闭合符或换行，避免无限等待。正文里的类似标签仅当文本，不再次执行。

schema v7：messages.content 保留完整模型原文，assistant_presentations 保存 protocol_version=1、body_text、emotion、action，以原 assistant message_id 关联。完整回复、表现、Mood、交互时间及提取待办同一事务提交，成功后才更新共享短期状态；取消或写入失败撤销本轮临时情绪。字幕、MomoTalk、近期上下文、提取 / 摘要读取正文投影，继续保留原始消息来源与记忆引用；v6 历史作为普通文本兼容，不改写原文。协议和白名单计入原有 12,000 字符提示预算。

Phase 3 用文字 Speaking 和字幕，嘴部不模拟发声。新轮取消并等待旧 Core 请求收尾，仅保留最新待发送，generation 防止迟到回调覆盖新轮。面板 / 触摸不隐式取消请求，关闭后恢复仍有效状态；返回 Home / 退出执行既有取消、保存与释放。SpeechSegmenter、正式 TTS / Audio Queue、PCM 同步、Ducking、缓存与历史复听留待 Phase 4。

完整顺序与验收边界见 [phase3_plan.md](<../03_阶段记录/phase3/phase3_plan.md>)，当前验证状态见 [phase3_status.md](<../03_阶段记录/phase3/phase3_status.md>)。META 可解析八情绪不代表八种全局美术表现已齐备。

## 独立发声意图（2026-10-18）

当前可输出`<META emotion="concern" action="none" voice="hesitant">`。voice允许原8种emotion词与`hesitant / flat`；缺省或未知voice继承合法emotion，整个头缺失继续neutral回退。Companion请求的近期assistant历史由程序补白名单规范头作为格式示例，计入原12000字符预算；正文/数据库/记忆不改，AgentPet请求仍用纯正文。已投影消息只读原文前256字符提取合法元信息，未投影旧文字不猜标签。提示明确当前每轮仍须带头、情绪按当前语义选择。犹豫/平直是本次表现，不写成新增Mood，不改变身份或记忆。协议版本仍为1，旧双属性控制头兼容。音频sidecar单独存VoiceIntent；数据库原文保留控制头，正文投影/记忆不接触新标签。
