# 语音、字幕与日记微调交付

2026-10-17。按玩家第一轮双语反馈调整，开发与技术验收完成，等待玩家听感和使用反馈。最终证据见 [acceptance.json](<../../04_验收记录/VoicePolish/acceptance.json>)。

## 体验入口

双击 [Start-VoicePolish.cmd](<../../../GiftCompanion/Tools/Start-VoicePolish.cmd>) 进入陪伴空间。默认日语；Esc → 设置 → 声音与复听可切中文。第一次读取旧设置会把默认改为日语一次，保留音量/开关；之后手动选中文并保存，重启保留选择。

## 此次变化

- 中文仍是主 LLM 回复及共享历史/记忆的唯一正文。语音就绪时，中文先接收并缓存在前端，字幕只随实际语音逐句出现，不提前闪现整段再回放。语音关闭、准备中或失败时正常文字回退；MomoTalk仍显示完整中文。
- 句间增加0.45秒呼吸，短分句0.18秒；已等待下一段合成的时间算在间隔内，避免双重延迟。停止/新消息立刻打断；离线复听同样应用。句间BGM保持柔和。
- 日语使用亲近、活泼的常体，减少です・ます及过度敬语，随语义调整句尾、停顿，不凭空加笑声、承诺或舞台指示。
- 主控制头纠正“必须固定neutral”的歧义：情绪根据正文选择，经同一元信息传给本地化与音色参考选择，不引入第二份角色状态。
- 使用已提供日语大厅明朗/害羞参考，中文使用较柔和的新参考，结合小幅语速变化。不是重训模型或增加新模型；不通过播放器变调伪造情绪。素材未覆盖的关切、低落等使用原参考及较慢语速，不能宣称已有完整八情绪配音。
- 日记默认每篇最多1个短颜文字，可不用，严肃部分不用。Config/diary_policy.json 的 MaximumKaomoji 可为0–2；只在玩家主动生成新回顾时应用，已有日记文件不重写。

## 独立文件与升级

Config/settings.json 保存 VoiceLanguage 与 VoicePreferencesVersion=2；旧设置仅一次升级。Config/speech_localization.json 原版默认规则升为v2并留备份；玩家自定义规则不覆盖。本地化仍是独立Flash非思考/15秒/384token/64条有界缓存，只传当前中文句子与情绪，不传整库历史。

Config/voice_runtime.json 增加 SentencePauseSeconds/ClausePauseSeconds。Config/voice_delivery.json 保存语言、情绪、参考ID、参考WAV、准确转写及SpeedFactor；VoiceReferences存四条3–10秒小WAV，首次启动从包内种子复制，现有文件保留，后端直接读取。音频sidecar增加Emotion/ReferenceId/SpeedFactor，实际音色指纹包含引用配置和参考文件内容。修改语气资料后重试语音或重启；翻译规则修改后重启。

原Material与Runtime权重、profile、YAML、API不改；新参考从已有OGG和文本按句末静音截取，原始时长/边界/哈希在 reference_assets.json 记录。没有引入新的推理依赖或第二份8.66GiB运行包。数据库继续v9；旧音频保留当时声音，不自动重新生成。

玩家说明见 [VoicePolishGuide.md](<../../../GiftCompanion/Assets/StreamingAssets/Defaults/VoicePolishGuide.md>)。旧Bilingual和Phase4程序/验收证据保持历史含义。

## 最终验证与试听

- EditMode92/92、PlayMode23/23，Final报告通过；之后C#源码未修改。
- Windows Release / Mono / D3D11 / Unity6000.3.7f1，零错误，304,508,014字节，比Bilingual增加2,799,033字节，其中参考WAV2,766,598字节；共享运行包不复制。
- 最终Player Write15项、NoKey7项全通过，包含默认语言、无全文提前显示、逐段中文字幕、情绪/参考传递、明朗/害羞/中文柔和实合成、单自有进程切换、日记数据库/MD一致、无Key复听和Home/退出释放。
- 本次模型加载17.475秒，已就绪后首音10.677秒；两句现场实际间隔3.334秒，因下一句仍在合成；首轮间隔0.503秒。0.45秒是最小呼吸，不承诺每次都恰好0.45秒。合成慢时仍会有较长等待。
- 已检查最终菜单和历史截图、译文的时间/条件含义。保留115项测试与15份证据、108份C#源码哈希；27份原模型/API/profile/素材与旧程序证据未改；发布无Key/测试DLL，许可证齐全。
- 所有此次Player和语音Python已退出，其他Unity/素材制作/Python进程未关闭，正式聊天与人物卡未用于测试或改写。主观音色/情绪改善不替玩家下结论。

试听文件均由本次真实TTS生成：

- [日语连续复听样例](<../../../GiftCompanion/Docs/Validation/VoicePolish/ja_conversation.wav>)：使用实际两段缓存，按0.45秒呼吸拼接，代表缓存完整时的节奏，不是现场等待合成的录音。
- [日语活泼参考](<../../../GiftCompanion/Docs/Validation/VoicePolish/ja_happy.wav>) / [日语害羞参考](<../../../GiftCompanion/Docs/Validation/VoicePolish/ja_shy.wav>)：同一中文首句、分别按情绪本地化及选择对应参考，译文见 [sample_texts.json](<../../../GiftCompanion/Docs/Validation/VoicePolish/sample_texts.json>)。
- [中文柔和参考样例](<../../../GiftCompanion/Docs/Validation/VoicePolish/zh_revised.wav>)：同一中文首句，使用新柔和参考及0.96语速，未对模型重训；尖锐感与普通话质量仍以试听反馈为准。
- [隔离测试日记](<../../../GiftCompanion/Docs/Validation/VoicePolish/sample_diary.md>)：只有本次合成测试对话，不包含正式聊天。最多1个颜文字是上限，允许不使用；首轮使用1个，最终样例未使用。

本地化的时间/条件关系曾在首轮把“忙完以后”宽泛译成“不忙的话”，已补充忠实规则。最终样例为“先生、お仕事が終わったら…”，并使用常体。FirstPublished仅保存早期报告，不能与最终结果混用。

复验：

```powershell
& './Tools/Test-Phase3.ps1' -Mode Both -Checkpoint Final -ReportDirectory './Docs/Validation/VoicePolish'
& './Tools/Build-VoicePolish.ps1'
& './Tools/Test-VoicePolishPlayer.ps1'
python './Tools/Save-VoicePolishAcceptance.py'
```

上述联网验证只使用随机Validation资料，Key仅传子进程环境，绝不打印。阶段范围为本轮听感/字幕修订，不开始Phase5/6。
