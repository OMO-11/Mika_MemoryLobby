# 内置语音运行时

> 2026-10-17：Phase4 已将运行时接入正式 Companion，见 [当前状态](<../03_阶段记录/phase4/phase4_status.md>)、[文件接口](<player_content_contracts.md>)。以下环境、裁剪与双语数据为 Phase0 历史装配记录；正式链默认中文，当前交付验证单独记录，不将历史 `/tts` 成功等同于本轮全部体验通过。

2026-10-11。GPT-SoVITS 已装配到工程的 `Runtime/GPT-SoVITS`，使用其自带 Python 3.9 / Torch 2.0.0 + CUDA 11.8，当前 RTX 4060 实测中文和日文 `/tts` 均成功。默认语言保留中文；不要求本机另外安装 Python。正式 Unity VoiceService 与分发集成在后续阶段完成。

## 资源与版本

源整合包 `GPT-SoVITS-v2pro-20250604.7z` 为 8,185,086,602 字节，原始解压文件约 13.16 GiB。角色 ZIP 内声明 **v4**；实际源码、v4 底模、LoRA、vocoder、BERT 和 HuBERT 已成功一起加载，未因整合包名称带 v2pro 而改用错误模型版本。

中文包 SHA-256：`464bc9a6d5be6dea9e4130f81daaf8d5e5cf52b7353d3d0f6638592e24a38120`。新日文包 SHA-256：`1df4552e6b0b0e0cd6fb5263c260baaed7ca056c5b421cc05466e08c0d6a0809`。此前两 ZIP 完全相同的记录是补充前的历史状态；现在已核对为不同模型。各语言独立保存 GPT、SoVITS 和参考 WAV，六份预置文件哈希均与交付一致。

中文参考音频：44.1 kHz 单声道、7.62 秒，位于 `models/mika/reference.wav`。日文参考音频：44.1 kHz 单声道、3.20 秒，位于 `models/mika-ja/reference.wav`。同目录的新参考 OGG / 转写保留在 Material，未混用中日文转写。

## 当前目录与体积

```text
Runtime/GPT-SoVITS/
  runtime/python.exe          # 内置解释器与已验证推理依赖
  api_v2.py                   # 交付 API 源码
  config.py
  GPT_SoVITS/                 # 推理代码、中文前处理与必需底模
  tools/                     # i18n、audio_sr、my_utils、AP_BWE 导入代码
  models/mika/               # 中文角色权重与参考 WAV
  models/mika-ja/            # 日文角色权重与参考 WAV
  voice.json                 # 默认中文参数
  voice-ja.json              # 日文参数
  tts_infer.yaml
  tts_infer-ja.yaml
```

实际目录 32,197 个文件、9,301,279,365 字节，约 **8.66 GiB**；运行生成少量缓存后数字可能变化。保守首轮装配含双语约 9.31 GiB，第二轮移出 **701,299,013 字节（约 669 MiB）** 的 CMake、Gradio、ASR、韩文词典、训练数据及 C++ 扩展头文件等。

裁剪依据是实际 import 记录、源码用途与裁剪后中日文重新启动及真实推理。PyAV / 原生 DLL、wordfreq、ipadic 实际被加载，因此保留；没有仅凭包名删除。Torch 约 4.05 GiB，为最大组成；Torch / CUDA DLL、ONNX Runtime、G2PW、BERT / HuBERT、v4 底模与 vocoder 保留。原始归档、训练成果及制作资源未删除或修改。

运行时放在 Unity Assets 外，避免 7z 和 Python 环境进入 AssetDatabase。最终分发布局是在 EXE 旁放置该 Runtime 目录；本轮 `Builds/Phase0` 是窗口验证构建，不复制第二份约 8.66 GiB 运行时，也不自动启动 TTS。

## 启动、验证与停止

在 `GiftCompanion` 目录使用 PowerShell；两种语言分别测试，同一时刻只启动一个服务以控制显存。

```powershell
& .\Tools\Start-VoiceRuntime.ps1
& .\Tools\Test-VoiceRuntime.ps1
& .\Tools\Stop-VoiceRuntime.ps1

& .\Tools\Start-VoiceRuntime.ps1 -Language ja
& .\Tools\Test-VoiceRuntime.ps1 -Language ja
& .\Tools\Stop-VoiceRuntime.ps1 -Language ja
```

启动工具只使用项目内 Python，工作目录固定，绑定 `127.0.0.1:9880`，进程内设置 ffmpeg 路径、UTF-8 日志与 Hugging Face 离线标志，不更改用户全局环境。检查工具等待 OpenAPI 就绪后 POST `/tts`，记录真实耗时；停止工具核对进程路径和启动时间，仅停止本工具记录的进程，并等待退出。日志和进程记录位于 `UserData/Logs`。

`voice_api_runner.py` 不改动交付 API，额外记录已加载 Python 模块供裁剪审查。`trim_voice_runtime.py` 默认只输出计划，`--apply` 要求先停止记录中的服务并把未用组件移出 Runtime。`verify_voice_bundle.py` 核对文件、角色哈希与裁剪后的真实 WAV，更新验收清单；缺少文件或音频早于裁剪时明确失败。

本轮双语复测通过后，已用 `Clean-VoiceTrimBackup.ps1` 清理约 669 MiB 临时裁剪备份，避免继续占用本地空间。清理前检查验证报告、绝对目录边界和重解析点；原始 7z 保留，可从源归档恢复被裁剪组件。

从原始归档重建时依次使用 `prepare_voice_assets.py`、`prepare_japanese_voice.py`、`assemble_voice_runtime.py`；这属于恢复操作，当前已完成环境无需再解包。装配脚本已补齐 API 间接依赖，并排除 Gradio 前端带方括号的路径，避免 tar 模式匹配错误。重建后需要重新跑双语推理与裁剪验证。

## 实测结果与边界

| 语言 | 请求正文 | WAV | 裁剪后首次请求 |
| --- | --- | --- | --- |
| 中文 | 老师，今天也一起度过吧。 | 48 kHz、单声道、3.32 秒、318,764 字节，非静音 | 约 10.70 秒 |
| 日文 | 先生、今日も一緒に過ごそうね。 | 48 kHz、单声道、3.28 秒、314,924 字节，非静音 | 约 8.38 秒 |

音频见 `Validation/Phase0/tts_probe_zh.wav` 和 `tts_probe_ja.wav`。这证明两套权重能通过真实本地 HTTP 推理；声音相似度和听感仍待用户试听，不把 WAV 容器正常等同于艺术质量验收。CPU 模式、其他显卡及所有语言组合尚未验收。

Phase0 当时仅完成独立 HTTP 验证。Phase4 正式接入的 VoiceRuntimeManager 调用单独随包 voice_host.py，通过带实例校验的 /gift/health / /gift/tts / /gift/shutdown 管理原 API；不修改本目录的交付源码。普通玩家从声音面板操作；上述旧脚本只供独立排查，不能与正式陪伴语音同时占用同一端口。当前运行 / 退出与玩家体验结论以 Phase4 记录为准。


## 2026-10-17 正式双语扩展

日语已接入正式中文主回复→独立廉价本地化→日语克隆链，菜单可切换；复用同一运行环境，切换先卸载旧音色。当前入口与最终验收见 [bilingual_voice_delivery.md](<../03_阶段记录/声音与日记扩展/bilingual_voice_delivery.md>)，交接见 [bilingual_voice_checkpoint.md](<../03_阶段记录/声音与日记扩展/bilingual_voice_checkpoint.md>)。原阶段0/4数据保留其历史含义，主观声音与翻译质量等待玩家统一反馈。


2026-10-17 后续听感/字幕修订已交付，最新接续 [voice_polish_checkpoint.md](<../03_阶段记录/声音与日记扩展/voice_polish_checkpoint.md>)，入口Tools/Start-VoicePolish.cmd。
