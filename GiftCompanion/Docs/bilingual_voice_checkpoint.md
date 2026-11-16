# 双语语音完成交接

2026-10-17。用户新增日语克隆功能已完成开发与技术复验，当前等待玩家听完中文/日语后统一给出结论与修改建议。没有未完成的构建/验收进程，不自动开始Phase5。

## 恢复入口

- 启动 Tools/Start-Bilingual.cmd；Windows程序 Builds/Bilingual/GiftCompanion.exe。
- Docs/bilingual_voice_delivery.md 是玩家操作、架构、文件、试听样例与最终结果。
- Docs/Validation/Bilingual/acceptance.json 是最终证据汇总；Final2 EditMode87/87、PlayMode22/22，最终Release301,708,981字节/零错误，Player Write15项和NoKey10项全通过。
- Phase4 原构建/证据保留。FirstPublished 为本次首轮报告，不能与最终报告混用。

## 必须保持的链路

中文输入 → 同一个主CharacterCore/LLM中文流式回复 → 前端中文/原中文历史/记忆 → 独立廉价子LLM日语本地化 → 已有日语GPT-SoVITS音色。中文模式跳过翻译。前端不等翻译；发声后显示对应段中文字幕。日语不覆盖聊天、事实、Mood或日记。翻译失败只回退文字，不给日语TTS硬塞中文。

SpeechLocalizer（纯Core）独立HTTP客户端复用现有DPAPI/子进程环境Key：deepseek-flash非思考，15秒/384token/串行/64条短句缓存，参数和说明在Config/speech_localization.json，重启生效。无历史/记忆/工具写权限，无本地大翻译模型。VoiceLanguage=zh/ja在settings.json，UI保存语言时先取消/停声/停止自有Python并Drain，再创建配对profile。VoiceRuntimeSettings.ForLanguage从原包配对；原profiles、YAML、权重/API不改，不复制8.66GiB运行环境，不双模型并驻。

每段新WAV旁有.speech.json：中文/实际朗读/语言/音色指纹/译文模型与说明hash。索引仍schema v9，每条完整回复只保留当时的一种语言；旧缓存不补合成、不覆盖，原无sidecar也能复听。恢复/清缓存处理程序命名sidecar，保留其他资料；复听不调用LLM/TTS。场景Session固定provider/localizer/language，取消与迟到过滤继续生效。菜单资料检查已列声音文件。

## 最终实测与限制

中文/日语加载22.246/22.211秒，就绪后日语首音6.327秒；首轮冷加载42.101秒，仅为本机样本。15个冻结模型/配置/API与旧Phase4相关文件哈希一致；Core无Unity引用、发布无测试DLL/Key、许可证齐全。最终截图已查看中文字幕/中日复听标签；菜单布局首轮查看，最终菜单截帧为空场景故不作布局证据。用户主观音色、发音、本地化措辞待反馈，不宣称主观验收通过。

正式资料未用于测试，唯一Validation路径见player_run.json。comparison_zh.wav与comparison_ja.wav使用同一主LLM中文首段，comparison_text.json含原文和译文。中文样例首次复制时继承隔离缓存EFS属性，已仅对这份可分享的测试样例移除加密，哈希不变；测试启动工具也加入相同的导出处理，普通读取已通过，无需向聊天提供Key。

所有本次自有Player/语音Python已退出，其他Unity/Material制作现场/Python未关闭。日记仍待原计划体验，人物卡未更改，Phase5/6未开始。下轮按用户双语听感反馈修订，不重做已通过基础阶段。


2026-10-17 后续听感/字幕修订已交付，最新接续 [voice_polish_checkpoint.md](voice_polish_checkpoint.md)，入口Tools/Start-VoicePolish.cmd。
