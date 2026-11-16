# Phase5 完成交接 checkpoint

2026-10-18（Asia/Shanghai）。用户批准的5A→5F全部完成开发与技术验收，当前等待玩家体验反馈。**不自动开始Phase6**。前一版ExpressionDiary保持用户已验收基线，本轮Phase5尚未获玩家主观验收。

## 当前入口与证据

- Tools/Start-Phase5.cmd / Start-Phase5.ps1，对应Builds/Phase5/GiftCompanion.exe；继续沿用已有安全Key与单份Runtime。正常资料为%LOCALAPPDATA%/GiftCompanion；本轮未打开或迁移正式聊天库。
- Docs/phase5_delivery.md给玩家体验步骤，phase5_implementation.md解释实现，player_content_contracts.md保存文件接口。
- Docs/Validation/Phase5/acceptance.json：136 EditMode+35 PlayMode、Live39项+NoKey30项和透明合成全部通过；playerAcceptance=pending。最终构建304674581字节、Unity6000.3.7f1、Release/Mono/D3D11、错误0；源码与实际EXE/Core/Runtime DLL哈希已保存，验收报告均晚于该构建。
- 同目录pet_live.png、pet_nokey.png已目检；tool_samples.json为合成任务请求/真实API回复，companion_resume.wav为回陪伴空间后的日语合成证据，不自动播放。原35个素材/Runtime/旧发布及验收哈希均未变。

## 实现状态

唯一AppRoot/CharacterCore/State/SQLite。Home中转模式，桌宠不调用TTS/翻译或启动Python。默认普通流式，输入上方“允许桌面操作”仅下一条消息生效，发送即关闭；工具规划先缓冲，执行真实结果后最终回复流式，不提前声称完成。

窗口线程RegisterHotKey、透明圆角气泡、置顶/焦点/位置/重置/返回/退出、头身点击/长按/拖动/释放和受限LookAt均完成。历史/日记/人物卡与陪伴空间共享；菜单可登记/命名/移除应用、选择/移除搜索范围、启停13工具、看简明记录和修改热键，不要求终端或编辑JSON。

工具：get_current_time、open_application、open_folder、search_file、open_url、get_running_apps、focus_application、open_system_setting、open_file、get_clipboard、set_clipboard、set_volume、mute_volume。代码白名单不能由文件扩展；完整SSE后才执行，参数/类型/长度/字段与真实目标再校验。打开文件、剪贴板、系统音量/静音逐次具体确认，关闭/取消/离开场景拒绝；默认执行网络预算120秒，确认独立180秒；最多4调用/3模型请求。应用无自由启动参数，搜索默认无范围，不读正文或扫整盘，不做Shell/Computer Use/文件写删/进程终止。

schema v10新增同库agent_tool_runs，迁移前原生备份。记录started及requested/allowed/denied等决定，实际succeeded/failed/denied/interrupted结果；取消/断网保留已发起结果，不自动重做。启动将残留started标为结果不明，不重放。JSONL派生审计不存参数、路径、剪贴板、聊天或Key。工具轮次仍可看历史，但排除自动记忆/检索上下文/日记；个人信息在关闭桌面操作的正常聊天中表达。

Config/settings.json沿用PetHotkey，pet_settings.json为窗口/阈值/帧率，pet_presentation.json为绑定/映射/LookAt，known_apps.json为应用列表，agent_tools.json为开关/范围/上限。坏文件保留原件，工具配置坏时禁用工具；菜单保存经AtomicDocument校验并保留上一版。

## 本轮实际修复与验证边界

- 原型返回按钮被正式菜单取代，场景测试按实际菜单路径更新；新增测试显式引用Newtonsoft DLL，编译失败历史未抹去。
- 权限Hide后异步回调重开气泡已修复；迟到历史/日记/记录回调不唤醒已收起面板。输入框点击获得焦点，应用命名可中文输入。
- vSync曾使目标30fps失效，进入桌宠临时关闭并在离开恢复，实测30/60fps。空闲进程CPU约占全机算力0.90—0.94%、工作集297—323MiB；活动约1.68—1.80%。4秒短样本；GPU活动3D三样本均值4.19%、最高4.97%，不作跨机器保证。
- 跨屏DPI虚拟化导致短点击误拖动与透明命中不一致，PetNative坐标读写统一物理作用域，透明单像素采样用相同客户端坐标，不改第三方包。真实HeadClick/BodyClick/HeadPatStart/End/DragStart/End、精确角落穿透均过。拖回原点持续移动另有Core回归。
- 应用名称加入模型上下文；丢失应用如实失败，窗口ID复核进程身份。执行后断网/取消不重试，停止保留自然结果反馈。实际时间/受控exe/受控目录联网通过，NoKey重启仍可读历史与操作窗口。
- AgentPet阶段OwnedVoicePID=0；回Companion后实际日语合成成功，再停止自有语音进程，最后回Home/退出。验收Player已退出；不操作用户其他Unity/制作/Python进程。

敏感权限与失败边界主要用替身测试，未读取正式剪贴板或改系统音量；音量COM仅只读连通验证。玩家需按交付清单测试自己的允许/取消、常用应用、IME选词、动作手感和屏幕边缘。全面多屏/混合DPI、托盘/启动深度集成、完整备份恢复和独立运行包仍按Phase6及后续已规划范围，不在本轮追加。

## 恢复任务时

先阅读本文件、phase5_status、phase5_delivery和真实反馈。171项自动测试、同一最终构建Live/NoKey与哈希已过，不因恢复对话无故重跑TTS或重复有副作用的工具。仅按新反馈定向修复；首次正式启动会备份升级v10，此后继续用Phase5，勿启动旧版读取新库。不要把playerAcceptance改为通过，直到玩家明确验收。

用户此前要求账户额度低于7%立即停工并写checkpoint，该约束持续有效；本工具没有账户额度百分比接口，无法可靠监测，未收到可见低额度提示，不能用上下文余量冒充账户额度。