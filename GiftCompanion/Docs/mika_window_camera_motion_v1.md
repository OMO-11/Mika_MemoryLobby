# 窗边运镜第一版参考

2026-11-02，Asia/Shanghai。依据玩家要求先交参考GIF，待审阅后逐步精修；当前不接入正式Unity运行界面。

[参考GIF](Design/MikaVisual/CameraMotionV1/window-camera-reference-v1.gif) · [交互预览（原画质）](Design/MikaVisual/CameraMotionV1/preview.html) · [窗边关键帧](Design/MikaVisual/CameraMotionV1/04-window-meteor.png)

第一版顺序：

- 0—1秒：人物与窗后时钟，模拟点击时钟。
- 1—4.2秒：视角向右上窗边推进并逐步转正；先靠近人物，再从近景人物旁掠过。
- 4.2—8.4秒：停在窗边，显示四位时间、透明文字菜单与返回入口。4.9秒、6.75秒开始两道流星，不伴随闪屏。
- 8.4—10.8秒：演示点击“回到未花”后的反向移动；随后停留至11.6秒并循环。交互版不自动返回，可留在窗边观察。

GIF 960×540、原始15fps，共174渲染帧，GIF合并重复帧后157帧，总时长11.6秒，约13.06MB。浏览器逐帧绘制现有Unity导出层，Python只编码GIF，不绘制场景。使用整段统一调色板以减少帧间色变；GIF有调色板压缩，交互HTML和PNG为更清晰参考。

实现：新增仅编辑器WindowCameraMotionProbe，Unity6000.3.7f1在未保存的临时场景，沿用第五/九版姿态，分开导出room、character、curtains及完整scene。背景、人物、窗帘使用不同运动幅度；人物额外位移实现从镜头旁掠过，不用直接淡掉人物。镜头缓入缓出，终点center(1014,260)、zoom2.85、rotation-26.8166°；窗外流星受真实pane裁切。主界面时钟保留第九版高度/宽度，接近窗边时淡出，再以窗边构图显现。

本版是2.5D分层运镜参考；没有把平面Spine变成完整3D房间。人物背面、新的空间遮挡/地形、正式Unity摄像机与动态Spine动画仍需后续按选定路径实现。菜单文字用于布局参考，尚未绑定聊天/日记等生产功能；现有后端与玩家资料未改。交互版支持时钟区域前进、返回、完整演示按钮；背景音乐图标为参考画面中的简化标识，正式静态方案仍以第九版为准。

参数独立存于CameraMotionV1/motion.json，同时嵌入preview.html以便直接双击本地打开。后续改参数必须同步二者，保持一切皆文件。render_motion.mjs用于隐藏Edge关键帧/全帧渲染，encode_gif.py用于GIF编码；中间Frames目录为临时产物，交付后清理，可用脚本重新生成。原层、关键帧、GIF、报告保留。

检查：Unity层导出进程exit0；三个正式场景/行为配置SHA前后一致，详source-preservation.json；隐藏浏览器完整渲染无页面异常，render-report.json；GIF重新解码与总时长验证成功，gif-report.json。已人工查看起始、近景经过、窗台接近、流星停留及GIF解码帧。临时Unity/浏览器已退出。

待玩家反馈主要围绕：是否经过人物太近、推进/转正速度、终点是否过于贴近玻璃、时间与菜单的最终位置、流星亮度与出现间隔。先确认方向，之后逐步精修，不将当前镜头参数视为最终值。
