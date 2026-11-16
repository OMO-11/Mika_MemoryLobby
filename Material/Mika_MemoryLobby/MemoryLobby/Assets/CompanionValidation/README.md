# CH0069_home Runtime Validation

独立验证原作素材在 spine-unity 内的局部交互，设计基准为工作区 Docs/Design/CH0069_Companion_运行时行为设计.md。

打开本目录 CompanionValidation.unity，点击 Play：

- 默认先单次播放约 20 秒的原作 `Start_Idle_01` 入场特写，再自动转入 `Idle_01`。入场期间暂停附加交互；F8 面板可 `Skip entrance`／`Replay entrance`。骨架重建恢复 Idle，不重播入场。
- 原作 Idle 与背景持续播放；常驻张望和鼠标关注只额外移动眼球。鼠标静止约 2.2 秒后恢复张望。
- 命中沿脸、头顶、发髻及短前发的实际网格；单击有原作头部短反馈，按住约 0.25 秒并轻拖持续摸头。当前已按用户要求回退到接入 10.png 之前的表现，摸头恢复原有三组随机表情，一次手势保持同一组。
- `enablePat10Trial` 已关闭；运行时不创建 10.png 试验组件、不加载该试验贴图，F8 不显示试验控件。
- F8 显示/隐藏验证面板，可切换 Thinking、播放两段语音、暂停/恢复、静音或停止语音。
- 说话时可以摸头，语音拥有嘴部控制权。松开、越界或失焦释放摸头。
- 嘴形使用短淡化和原作范围内的连续缩放；表情在短眨眼遮挡下切换虹膜，避免双瞳重影。

CH0069Behavior.asset 保存真实节点、插槽、附件与限幅配置。原始素材不在此处修改。

Play Mode 下，菜单 Companion Validation / Run Behavior Smoke、Run Voice Smoke 和 Review v0.2 可分别复现检查。检查会短暂接管表现；不要同时运行或混用手动操作。语音检查约 40 秒。报告与截图位于工作区 Docs/Audits/Spine/MemoryLobby/RuntimeBehavior，当前说明见其中《运行时反馈优化_v0.2.md》。脚本修改前先退出 Play，避免热重载后的旧绑定影响检查。

这是素材能力验证原型。正式对话状态、TTS 队列、八种情绪、动作白名单和模式切换以应用架构及逐项验收为准。

10.png 试验代码、StreamingAssets/ExpressionTrials 中的原始像素副本及 Docs/Audits/Spine/MemoryLobby/Pat10Trial 中的历史报告保留备查，当前不参与运行。试验菜单仅适用于显式启用 `enablePat10Trial` 后重新播放的验证工程。
