# 表达目标与日记排版交付

2026-10-18。开发、技术验收和本次用户阶段验收均已通过，用户反馈整体效果符合预期。入口为 [Start-ExpressionDiary.cmd](<../../../GiftCompanion/Tools/Start-ExpressionDiary.cmd>)，实现方法见 [学习说明](<expression_diary_implementation.md>)，维护状态见 [checkpoint](<expression_diary_checkpoint.md>)。后续[Phase5方案](<../phase5/phase5_plan.md>)已提交待审批，尚未实施。

## 如何体验

双击启动器，进入陪伴空间。默认继续日语，输入、中文字幕与共享历史继续中文；当前已明确保存的语言选择保留。现有活泼/害羞、句间呼吸、字幕随实际声音出现的行为保留。

可以自然聊天，也可以提出“这句有点犹豫地说”“平静地确认收到消息”等表达要求。模型根据当前内容选择发声目标，不要求玩家输入协议代码。

| 新目标 | 当前实现 | 限制 |
|---|---|---|
| 恼火 | 克制措辞，原参考，1.02语速 | 没有独立怒声参考，仅表达近似 |
| 伤心 | 低落遗憾，原参考，0.92语速 | 没有独立哭腔训练，不凭空加哭声 |
| 犹豫 | 保留原文迟疑，复用害羞参考，0.93语速 | 不把肯定事实改成猜测，不强制结巴 |
| 平直 | 减少夸张句尾，原参考，1.00语速 | 尽量克制，不能保证音高完全不变 |

本轮扩展日语表达，没有新增四套中文情绪参考。均复用现有模型和WAV，不变调、不重训、不增加本地大模型。主观情绪强度和相似度仍请以试听判断。

## 日记与颜文字

Esc → 资料与回顾 → 我们的相处日记 → **回望从相识到现在**。只在玩家主动操作时整理，新版不会自动覆盖旧日记。“打开日记文件夹”可查看同正文Markdown。

模型按话题和相处变化返回自然段数组，段间留空行；资料少时允许少段，不凑篇幅。程序对超长段只补空行，不改写内容、不另请求一次LLM修复。默认目标每段160字、最长260字，数据库、前端和MD使用同一正文。

颜文字从原来最多1个放宽为默认整篇最多4个：短篇少放，较长篇可用2–4个，不每段必放、不强制凑满，严肃内容可不用。`Config/diary_policy.json`的`MaximumKaomoji`可设0–12；0表示不用。旧默认1会升级，明确关闭或其他自定义上限保留。实际数量仍由生成规则引导。

## 文件与学习入口

资料目录为普通Player的`%LOCALAPPDATA%/GiftCompanion/`，可从应用菜单打开；Editor仍用工程UserData。

- `Config/voice_delivery.json` v2：目标、参考、语速；`VoiceReferences/`保留现有短参考。
- `Config/speech_localization.json` v3：独立日语本地化规则；默认规则升级，自定义规则保留。
- `Config/diary_policy.json` v2：段长与颜文字配置，下一次主动生成生效。
- `AudioCache/`的`.speech.json`：原Mood对应Emotion、本次VoiceIntent、实际参考与语速；旧缓存保留当时声音，复听不联网。
- `Diary/*.md`与同一数据库中的日记记录：同正文，旧文件不自动改写。

声音面板“使用说明”打开《表达与日记说明.md》。想学习代码，可阅读 [实现说明](<expression_diary_implementation.md>)，其中有主链图、协议/配置示例、分段兜底算法、兼容升级和能力边界。

多轮实测发现仅加强提示仍可能省略控制头，最终通过为Companion请求历史补白名单规范格式示例改善。该请求内格式计入12000字符预算，不修改旧中文正文/记忆/数据库，AgentPet仍用纯正文；概率生成不保证永远服从，缺头保留安全回退。

## 验证与样例

Final EditMode104/104、PlayMode24/24，真实Player Write18项、NoKey7项全部通过。最新Windows Release零错误，304,532,895字节，比VoicePolish增加24,881字节，现有8.66GiB共享Runtime未复制；数据库仍v9。31个冻结文件、参考源文件、112份C#及15份证据哈希核对通过，见 [acceptance.json](<../../04_验收记录/ExpressionDiary/acceptance.json>)。

四个真实TTS缓存样例（48kHz单声道PCM16）：

- [恼火](<../../../GiftCompanion/Docs/Validation/ExpressionDiary/ja_annoyed.wav>)：3.66秒。
- [伤心](<../../../GiftCompanion/Docs/Validation/ExpressionDiary/ja_sad.wav>)：4.16秒。
- [犹豫](<../../../GiftCompanion/Docs/Validation/ExpressionDiary/ja_hesitant.wav>)：6.54秒。
- [平直](<../../../GiftCompanion/Docs/Validation/ExpressionDiary/ja_flat.wav>)：3.00秒。

原中文、日语译文和实际参考见 [sample_texts.json](<../../../GiftCompanion/Docs/Validation/ExpressionDiary/sample_texts.json>)。它们是各自演示语境下生成的语音，不是同句的严格声学对照；qualityAccepted=false保留技术交付时原值，本次用户反馈另记于本文，不回写旧验收JSON。

[隔离测试日记](<../../../GiftCompanion/Docs/Validation/ExpressionDiary/sample_diary.md>)仅含4轮语气演示，最终生成3段（124/122/130字）和2个颜文字，DB/MD一致。没有用正式聊天做测试。初次与第二次失败报告分别保留FirstPublished/SecondPublished，不与本次通过证据混用。

本项目测试Player与自有语音进程均已退出，其他用户程序未动。旧VoicePolish/Bilingual/Phase4构建和原验收保留；本轮不开始Phase5/6。
