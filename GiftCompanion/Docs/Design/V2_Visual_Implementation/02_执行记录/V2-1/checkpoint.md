# V2-1 checkpoint

2026-11-03，Asia/Shanghai。**视觉资源与配置基础已完成，等待安排 V2-2。**

已接入 Lolita/TMP 与 Noto fallback、七枚透明图标、唯一默认视觉 JSON、每空间锚点和安全区、原子保存/重载及损坏回退。AppRoot 持有 VisualSettings；首次读取默认不写玩家新文件。共用坐标投影/逆映射、等比 Fit 与菜单组件可供下一阶段调用，未知空间走屏幕菜单回退。

Unity 6000.3.7f1 全工程编译成功，`V2VisualFoundationProbe.Run` 最终 31 项检查通过，记录 `validation.json`；增加了 VisualSettings 初始化断言的既有独立启动场景测试 1/1 通过，见 `startup.PlayMode.xml`。检查使用隔离资料，无真实 API/TTS 调用。开发中修复了未知版本异常捕获、动态字体 fallback 表初始化及 Sprite 导入模式；最终结果对应修正后的代码。

已实际查看 `人物起点.png`、`窗边终点.png`、`未适配空间菜单回退.png`，并修正菜单的 TMP 基线偏移。静态截图使用冻结分层，效果未包含完整月光/流星/动态轻纱，不代表生产运镜已实现。新增字体/图标可被 Unity 加载，不代表整套 V1 UI 已替换。

资源许可范围见 `配置契约与资源说明.md`；字体再分发授权留待独立交付前核对。冻结基准 96 项 SHA256 全部一致；V1 收尾包未重建，正式资料未作测试。数据库与备份版本、桌宠关闭状态保持原基线。

下一阶段从 VisualSettings、CompanionVisualFoundation 与本阶段坐标说明继续，接入真实动态 Spine 分层、相机视图状态和遮挡一致命中。当前不自动执行 V2-2。
