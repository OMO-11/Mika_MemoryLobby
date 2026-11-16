# Phase 4 完成交接

2026-10-17。用户批准的4A→4E已完成开发与技术验收，当前等待玩家测试反馈，无后台任务需要继续等待。新反馈从本文件、phase4_status.md、phase4_delivery.md 和 development_log.md 继续，不重做已通过阶段，不自动开始Phase5。

## 交付与核心实现

入口 Tools/Start-Phase4.cmd；程序 Builds/Phase4/GiftCompanion.exe，Release / Mono / D3D11，Unity6000.3.7f1。启动器复用现有Editor Key，仅子进程环境，并为开发构建定位 Runtime/GPT-SoVITS，不复制第二份8.66GiB模型。进入Companion才准备语音；Home/AgentPet不运行GPT-SoVITS。

AppRoot唯一VoiceRuntimeManager/AudioCache。Core/Audio包含独立VoiceConfiguration、SpeechSegmenter、WaveAudio、AudioCache、受控进程管理器；StreamingAssets/Voice/voice_host.py调用原API而不改交付源码，健康响应核对实例/PID、推理单并发、父进程死亡退出。取消不释放仍运行的推理门控；超时先退役确切自有进程。端口占用不接管其他程序，启动失败清理未取得PID的Process对象。

CompanionVoicePlayer场景双队列/代次过滤，语音AudioSource独立于BGM，短生命周期复用原PCM口型组件。模型准备/失败时显示文字；就绪后新轮分句朗读，正文与朗读双完成；符号回复无音频也显示。Ducking独立临时系数，保留原音量/静音/手切单曲循环。VoiceUI提供保存开关/音量、重试/停止、缓存清理、说明。MomoTalk只复听完整本地缓存，不重新TTS。

schema v9 audio_records新增索引，WAV在AudioCache随机会话目录；完整assistant提交才发布整组。取消/失败不发布半组，启动清孤立程序文件，清缓存先去索引再删校验范围内WAV，不递归删用户资料。聊天/记忆/人物卡/日记保留，v8首次普通启动备份升级。原Phase3/Phase3Plus不能读v9，同一库不要用旧程序打开。

## 最终证据

- Final2.EditMode.xml **78/78**；Final3.PlayMode.xml **19/19**。Core在78项后未再变；后来仅Runtime符号/缓存提示与PlayMode修订，由19项和最终Player覆盖。
- player_build.json：零错误、301,676,459字节、15.4364936秒。
- player_write / player_restore / player_nokey / player_faults.json均passed，时间均晚于最终构建；player_run.json为最终随机Validation目录，绝非正式聊天库。
- 真实中文、DeepSeek、实际GPU取消门控、PCM/Ducking、重启/无服务/无Key缓存复听、缺段禁用/不补生成、清缓存保留历史、缺运行时、外来端口保留、自有服务崩溃/菜单重试、1024×768、准备中离开/快速重入均通过。
- 本机最后运行模型加载21.104秒、短回复文字1.637秒、首音5.910秒，actualOverlapObserved=0必须如实保留；控制流测试证明没有全文门槛。首版数据在FirstPublished。
- 已查看最终speaking、voice_settings、history_replay、restored_nokey、voice_ready_resized图。技术画面通过，不替玩家完成声音相似度/主观听感验收。
- 离线汇总器Tools/Save-Phase4Acceptance.py保存最终static_review/visual_review/acceptance与哈希；原始模型/API/862冻结导入与旧构建核对，秘密文件/测试程序集检查均记录在报告。final_review.json做源与证据哈希读回。

## 后续与边界

按玩家反馈调整声音音量/停顿/字幕与口吻，不自动改人物卡、不重新总结正式日记。日记仍待玩家体验，默认中文，日文资源保留但不宣称其正式整链通过。第二份新Spine待明确交接，未映射嘴部的空间可播放声音/字幕并保持基础Idle。跨机安装/进一步模型精简留Phase6，桌宠工具留Phase5。

测试自有进程均应在最终检查中确认退出。用户另有Material/Mika_MemoryLobby/MemoryLobby_Seaside工程与其他Python进程，不按进程名批量终止，不修改其制作现场。普通沙箱Unity会卡授权，后续必要验证使用已授权的明确批处理提权命令。当前没有未完运行会话，源文件与构建保持最终验收版本。


## 后续双语扩展

2026-10-17 用户追加日语克隆已完成，最新接续 [bilingual_voice_checkpoint.md](<../声音与日记扩展/bilingual_voice_checkpoint.md>)。原Phase4证据保持历史中文验收含义；本次双语证据独立在 Validation/Bilingual，等待玩家中日试听反馈。
