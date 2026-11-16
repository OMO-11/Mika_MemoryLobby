# 窗边运镜参考第六版：缩小界面、增加夜空留白

2026-11-02，Asia/Shanghai。

[参考GIF](Design/MikaVisual/CameraMotionV6/window-camera-reference-v6.gif) · [交互预览](Design/MikaVisual/CameraMotionV6/preview.html) · [窗边关键帧](Design/MikaVisual/CameraMotionV6/04-window-meteor.png)

在第五版基础上将菜单、时间和日期统一缩小15%（worldUi.scale=0.85），腾出更多夜空留白。镜头、左右位置锚点、萝莉体、紧凑行距、24小时HH : mm与日期规则保持，点击区域及返回点击提示同步缩放。完整画面仅显示比例变化，不移动窗台或新增功能。

参数CameraMotionV6/motion.json独立并同步嵌入HTML。复用V1图层与共享字体，不增加素材拷贝、不启动Unity，前五版保留。正式程序、玩家数据、候选包未改。

验证：完整174帧渲染无异常；常驻alpha/字体载入/时间日期格式检查通过；已查看缩小后关键帧。GIF960×540、15fps、174编码帧、11600ms、14,906,366字节，重新解码和时长验证通过。三个场景/行为配置hash不变，浏览器退出，中间Frames清理，报告与成品保留。
