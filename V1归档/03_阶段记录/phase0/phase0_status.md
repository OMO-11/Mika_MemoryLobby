# Phase 0 状态

更新：2026-10-12。用户已配置本机 Key；DeepSeek 真实请求验收通过，**Phase 0 六项技术链均已实测通过**。后续 Phase 1 亦已按批准方案实施并通过，见 `phase1_status.md`。

| 必须验证的技术链 | 当前状态 | 证据 / 剩余工作 |
| --- | --- | --- |
| Spine 示例正常播放 | 已通过 | 完整 CH0069_home；743 骨骼、294 槽、52 源动画；实际网格、Idle / Thinking、触摸取消及卸载测试通过 |
| 3D 播放原动画 | 已通过 | Generic / 无 Root Motion；18 身体与 18 脸部状态、时长核对，Walk 实际移动骨骼，摸头 Enter 中释放走 Exit / Idle |
| DeepSeek Streaming | 已通过真实联网验证 | Unity 6000.3.7f1 读取本机 DPAPI Key，请求官方 `deepseek-flash`；3 个文本片段，首段 1712 ms、完整结束 2668 ms，正常收到 `[DONE]`；计数与耗时入报告，不保存 Key 或回复正文 |
| SQLite 消息保存 | 已通过 | 官方 SQLite 3.54.0 x64；EditMode 落盘重开、UTF-8 原文、FTS5、24 并发串行写入及取消通过；Windows EXE 再次关闭重开并搜索通过 |
| GPT-SoVITS `/tts` | 已通过技术链验证 | 内置 Python / CUDA / v4，中文和新增日文独立权重实际返回 HTTP 200、非静音 48 kHz 单声道 WAV；裁剪后两种语言重启复测通过 |
| 透明桌宠窗口 | 已通过 Windows 实机验证 | 同一窗口 / 唯一 AppRoot；D3D11，桌宠 600×500、透明 / 无边框 / 置顶；真实桌面合成截图通过；返回 Home 恢复 1280×720、普通边框及非置顶 |

2026-10-11 的 EditMode **11 / 11**、PlayMode **4 / 4** 及 D3D11 图形播放 **4 / 4** 通过，Windows Player 构建和透明窗口检查通过。2026-10-12 新增 DeepSeek 真实联网证据，编辑器编译与探针正常退出；没有重复运行上述既有检查。

主要证据位于 `Validation/Phase0/`：`deepseek_probe.json` / `.txt` / `.log`、`player_build.json`、`player_probe.json`、`window_composition.json` / `.png`、`tts_probe_zh.json` / `.wav`、`tts_probe_ja.json` / `.wav`、`voice_assembly.json`、`voice_trim.json`、`native_dependencies.json`、`local_secrets_probe.json`。SQLite 的独立验证库为同目录 `player_messages.db`，不用于正式角色记忆。DeepSeek 耗时仅代表本次短请求。

当前内置语音目录实际约 **8.66 GiB**，包含中日两套角色资源；从保守装配中移出约 **669 MiB** 未用组件。原始整合包解压文件合计约 13.16 GiB。实际加载的 PyAV、日文词典、Torch / CUDA、ONNX Runtime、G2PW、BERT / HuBERT 与 v4 底模保留。细节见 `voice_runtime.md`。

技术链通过不等于完整产品完成：后续 Phase 1 已让正式 DatabaseService 持久化短期状态，并接通共享 LLM / CharacterCore。完整 Memory 在 Phase 2，正式 VoiceService / WAV 队列尚未实现；当前 EXE 不自动启动语音服务。声音相似度、中文 / 日文听感、3D 偏粉材质、最终中文 UI、真实鼠标触摸及拖动、快捷键、长期运行与多显示器缩放仍待相应阶段验收。

初次 71 份交接源文件校验记录见 `asset_import.json`。2026-10-11 最后只读复核为 **70 / 71 未变化**：`Material/CHDDD/CHDDD_actions_v2.blend` 修改时间为 2026-10-11 20:53:02，哈希与旧交接记录不同。未改写该制作稿；已保留当前文件并登记 `source_recheck.json`，不自动重导或覆盖。当日已导入主工程的 **73 项 Unity V7 资源负载哈希全部一致**，播放与窗口验证使用这一冻结版本。
