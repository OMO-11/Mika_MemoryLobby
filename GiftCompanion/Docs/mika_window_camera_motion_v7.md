# 窗边运镜参考第七版：整体退镜与上方星空

2026-11-02，Asia/Shanghai。

[参考GIF](Design/MikaVisual/CameraMotionV7/window-camera-reference-v7.gif) · [交互预览](Design/MikaVisual/CameraMotionV7/preview.html) · [窗边关键帧](Design/MikaVisual/CameraMotionV7/04-window-meteor.png)

玩家纠正：要求终点整体缩小，而不是只缩小菜单/时间日期，并稍向上露出上方夜空星星，菜单再往左下。第七版改动的是整个终点摄像机：zoom2.5→2.3，物体整体缩小约8%，center(980,282)→(955,268)，视野后退且上抬。正面rotation-26.8165748°保持。

撤回第六版独立的UI 0.85缩放，worldUi.scale恢复1，沿用第五版字号，界面与整个背景同随摄像机缩放。菜单世界位置(900,185)改为(845,195)，在实际新取景中向左下移动。保留紧凑萝莉体、右上24小时HH : mm与日期、常驻场景/自然遮挡不淡出、月光纱帘与流星。

参数CameraMotionV7/motion.json独立并同步嵌入HTML。复用原始层/共享字体，未启动Unity或修改正式程序、玩家资料、候选包。前六版保留，V6只缩UI的做法不作为此要求的后续方向。

验证：完整174帧浏览器渲染无异常，alpha/字体/日期格式检查通过。camera-view.json记录终点逆投影视口四角均在原始1280×720图层内，没有以空画布拼出上方星空；已查看窗边帧。GIF960×540、15fps、174编码帧、11600ms、15,122,672字节，重新解码/时长/窗边帧校验通过。三个正式场景/配置hash不变，浏览器退出，中间Frames清理。
