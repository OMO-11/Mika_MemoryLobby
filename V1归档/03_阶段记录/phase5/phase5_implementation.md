# Phase5 实现与维护

Phase5沿用唯一AppRoot、CharacterCore、角色状态和memory.db。Home仍是模式中转；桌宠不调用TTS或语音翻译，也不启动Python。原V7模型、动作和第三方运行时保持，新增行为通过场景适配器和独立配置接入。

## 两种对话路径

默认继续调用原Core流式正文接口，边收到边显示。输入上方“允许桌面操作”只对下一条消息有效，发送后归零。开启时，同一个Core建立一次请求/取消范围和前台记忆租约，AgentConversation先缓冲DeepSeek规划响应；收到完整工具调用、通过参数和目标校验、必要时得到本次确认后才执行。工具结果携带真实观察值，最终回答使用tool_choice=none重新流式接收。模型执行前说的“已经完成”不会提前显示。

工具中间消息只属于当前任务，数据库仍只提交最终中文正文。应用打开等结果只能证明已发起时，消息使用launch_requested；窗口焦点另核实focus_confirmed，系统拒绝则失败。任何工具失败使用本地真实结果反馈，断网/停止后保留已执行结果，不自动重做。普通聊天不走工具规划。

传输遵循[DeepSeek官方Tool Calls](https://api-docs.deepseek.com/guides/tool_calls/)及[Chat Completions](https://api-docs.deepseek.com/api/create-chat-completion/)，保持现有Flash、非思考和直接HTTP，不增加SDK/Agent框架。SSE按调用index合并id/name/arguments；必须有正常tool_calls结束及DONE，才向执行器交付。未知函数、重复JSON字段、字段不全、类型/大小不符及超限批次均拒绝。

## 权限与持久状态

后端仅实现固定13项函数，JSON不能创造新能力。普通明确任务的SAFE操作直接执行，文档打开、剪贴板和系统音量操作每次在同一气泡确认。确认显示实际参数；剪贴板读前明确告知会把纯文本发给DeepSeek。收起/Esc/切场景不等于同意。网络/执行默认总预算120秒，确认等待另计180秒；最多4调用、3次模型请求，文件可缩小有界配置。

schema v10新增agent_tool_runs，同一个DatabaseService串行处理。先登记started，再记录requested/allowed/denied等权限决定；执行后持久succeeded/failed/denied/interrupted。主键request_id+call_id防止同ID重做，同请求相同工具/参数缓存已观察结果。启动把残留started标记OutcomeUnknown，不自动重放。系统副作用和SQLite无法构成同一个原子事务，因此崩溃时保留“结果待核对”。

Logs/tool_audit.jsonl是同库最近200条执行记录的派生文件，不存参数、路径、聊天、剪贴板或Key，导出失败可从数据库重建。聊天仍能看工具轮次，但整轮排除自动记忆、检索上下文与日记，避免临时资料变成经历；想让角色记住的个人信息可关掉桌面操作，另行正常告诉她。

## Windows边界

应用优先登记表，其次有限开始菜单/PATH查找；同名不猜，无法解析时引导从菜单选择实际程序登记。不传自由命令参数；快捷方式检查实际exe目标和参数，拒绝命令解释器/脚本。文件名搜索只在玩家选择的具体目录，默认空范围，跳过链接/系统/隐藏和Secrets等目录，最多深度5、6000项、20结果、约2.5秒。仅文件名，不读取正文。

网页限http/https、拒绝嵌入凭据和自定义协议。打开文档还需在已选范围内，并是常见文档/图片/音视频白名单，不执行脚本或快捷方式。可见应用仅返回进程名和窗口ID，聚焦前复核窗口，遵守[Windows前台限制](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setforegroundwindow)。剪贴板限纯文本4000字，不监视。系统音量通过[IAudioEndpointVolume](https://learn.microsoft.com/en-us/windows/win32/api/endpointvolume/nn-endpointvolume-iaudioendpointvolume)修改并读回，和项目BGM/Voice音量分开。

## 窗口与动作

线程内[RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)注册/取WM_HOTKEY/注销，换键先尝试新键，失败保留旧键；退出和回Home清理。透明命中继续用现有UniWindowController。输入时才请求焦点，正常存在时不持续抢焦点。窗口移动、工作区约束与客户区恢复使用同一Win32坐标系，跨屏尺寸变化后重新安排气泡和相机；本机验证不能代替Phase6全面多屏矩阵。

PetGesture区分头身点击、长按和拖动，拖动优先于摸头。窗口外释放、失焦、切场景均释放交互；拖动暂藏气泡，结束重排并保存位置。头部LookAt以有界平滑旋转叠加，不改原骨架，Working等拥有头部动作时回正。配置映射逻辑动作，缺失回Idle。进入桌宠临时关闭vSync使30/60限帧有效，离开恢复此前vSync/帧率及输入背景设置。

## 文件接口与测试

Config/settings.json沿用PetHotkey；pet_settings.json保存尺寸、位置、置顶、回复停留、长按/拖动阈值、30/60帧率；pet_presentation.json保存骨绑定/动作与LookAt。known_apps.json为Name/Path应用列表，agent_tools.json保存Enabled、SearchRoots、MaxCalls、MaxRounds、TimeoutSeconds、ConfirmationSeconds。AtomicDocument校验保存并保留上一版，坏文件保留原件，工具配置坏时禁用工具。菜单能登记/命名/移除应用、选择/移除范围、启停工具、看记录、改热键和找回位置。

自动回归使用随机临时库、伪模型与执行器覆盖权限/协议/取消/迁移，不操作真实剪贴板和系统音量。原生Player在随机Validation资料中测试受控程序/目录、实际DeepSeek、热键/透明/触摸拖动、模式恢复和无Key；系统音量只检查只读COM连接，写入由玩家应用内明确确认后体验。最终结果以Validation/Phase5的最终报告为准，attempt子目录与早期失败保留诊断意义。