# 双语声音试听版交付

2026-10-17。新增日语克隆语音，供玩家与中文一起听完后统一反馈。开发与技术复验完成，等待玩家统一试听反馈。最终证据见 [acceptance.json](Validation/Bilingual/acceptance.json)。

## 启动与切换

双击 [Start-Bilingual.cmd](../Tools/Start-Bilingual.cmd)，进入陪伴空间。按 Esc → 设置 → 声音与复听 → 点击“朗读：中文 / 日语” → 保存声音设置。默认中文，保存后停止当前回复并准备新音色，准备期间可继续文字聊天。历史按钮标注“中文复听 / 日语复听”，播放当时缓存的原语言。

本机运行时已含中日模型。只启动一个 Python、加载一种音色；切换先停止并卸载旧音色。开发启动器复用已配置 Key 和唯一 Runtime，不复制权重；不需要额外配置其他服务。

## 固定工作链

中文输入 → 主 LLM 生成中文回复 → 中文先显示并按原逻辑存聊天 / 记忆 → 日语分支独立廉价子调用本地化 → 实际日语文本进入 GPT-SoVITS 日语模型 → 播放并显示对应中文字幕。

日语子调用沿用已有 DeepSeek Key，独立 deepseek-flash、非思考、384 token、15秒超时、串行有界、64条短句内存缓存。只传当前短句，无整段历史、人物记忆或工具能力。配置在资料目录 Config/speech_localization.json，可独立维护，不改变主模型配置。中文模式跳过翻译；翻译失败回退中文文字，不把中文直接传入日语模型，不自动多次收费重试。

中文字幕不等待翻译。实际发声后切到该语音段的中文，Spine 继续用 PCM 口型、BGM 临时压低。新消息 / 停止 / 离场取消翻译和播放，迟到结果不能发布或发声。旧缓存复听不用网络或模型。每条回复保留当时一种音色，切换只影响新回复，不自动重配旧聊天。

## 文件与维护

- Config/settings.json：VoiceLanguage（zh / ja）与开关 / 音量；普通玩家通过菜单保存。
- Config/voice_runtime.json：复用既有运行参数；实际中日 profile / YAML 由语言配对。
- Config/speech_localization.json：独立模型 / 本地化说明 / 限额配置，修改后重启生效。错误文件保留，中文功能继续可用。
- AudioCache/<随机目录>/<序号>.wav 与同名 .speech.json：音频及可读的中文、实际日语、音色 / 翻译来源。只有完整消息 / 完整组音频发布 v9 索引；不把译文写回聊天 / 日记 / 事实。菜单清缓存同时清理 sidecar，保留聊天；旧无 sidecar 的 WAV 继续复听。
- 玩家资料目录下《双语声音说明.md》及菜单“使用说明”解释操作；资料检查列出新增配置位置。

程序仍为 Unity6000.3.7f1 / Windows Release / Mono / D3D11。沿用 v9，不迁库。当前共享 Runtime 约8.66GiB保持单份；未引入本地翻译大模型或新增框架。历史 Phase4 构建与其验收证据保留。

## 试听与反馈

[中文样例](Validation/Bilingual/comparison_zh.wav) / [日语样例](Validation/Bilingual/comparison_ja.wav) 取自同一条实际主 LLM 中文回复的首段；日语先独立翻译，原文与译文见 [comparison_text.json](Validation/Bilingual/comparison_text.json)。两份音频为真实模型输出，无人为替换参考音。它们是隔离测试样例，不进入正式聊天。

请分别听音色相似度、发音、语气、音量、停顿与等待时间；日语措辞和声音质量待你统一反馈。日记体验仍待你的原计划，Phase5/6未启动。

## 最终技术验收

- EditMode 87/87、PlayMode 22/22，Final2 报告通过，之后 C# 源码未改动。
- 最终 Windows Release 零错误，301,708,981字节，较原 Phase4 增加32,522字节；仍不包含第二份 Runtime。
- 最终 Player Write 15项、NoKey 10项全通过：自动准备声音、菜单切换并卸载旧音色、真实日语克隆、中文先显示与中文落库、来源sidecar、中日缓存保留和离线复听、清缓存保留历史、Home/退出释放进程。
- 本次最终运行中文加载22.246秒、日语切换加载22.211秒；已就绪后，日语新轮从发送到首音6.327秒。速度随模型冷启动、文本长度、网络和机器负载变化；首轮冷加载曾42.101秒。
- 15份原Runtime配置/模型/API与旧Phase4程序/验收文件哈希未变；104份C#源文件与最终证据记录哈希。发布包不含Key、测试程序集，三个许可证文件齐全。
- 已查看最终中文字幕与双语历史截图；声音菜单布局在首轮截图查看，最终菜单帧未抓到面板，未作为最终布局证明。最终菜单切换功能由真实Player标志验证。
- 项目自有语音Python与Bilingual Player均已退出；未停止其他Unity/素材制作/Python进程。正式资料未用于验收，人物卡/记忆/日记未更改。

复现命令（工程目录内）：

```powershell
& './Tools/Test-Phase3.ps1' -Mode Both -Checkpoint Final2 -ReportDirectory './Docs/Validation/Bilingual'
& './Tools/Build-Bilingual.ps1'
& './Tools/Test-BilingualPlayer.ps1'
python './Tools/Save-BilingualAcceptance.py'
```

验收工具只对随机 Validation 数据进行联网测试。试听导出已移除从隔离缓存继承的加密属性，可直接试听和复制；验收不导出Key。可读玩家操作说明见 [BilingualVoiceGuide.md](../Assets/StreamingAssets/Defaults/BilingualVoiceGuide.md)。
