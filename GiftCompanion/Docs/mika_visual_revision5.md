# Mika视觉稿第五版：窗内时钟校准

2026-10-30，Asia/Shanghai。针对第四版数字严重溢出的问题，重新取景测量和排版。

[打开第五版可点击稿](Design/MikaVisual/revision5.html)。选择“09 窗内校准”可在无遮人物、窗帘的画面中检查完整时间。前三轮已确认的菜单、音乐、字幕风格继续保留。

## 修正依据

本轮实际启动Unity6000.3.7f1，在未保存的临时空场景加载现有Spine资源，输出完整场景、隐藏人物与窗帘的背景，以及人物/窗帘/窗框/花饰的透明前景层。没有保存正式场景或资源。两个正式场景及行为配置的前后SHA256一致，记录见 `Design/MikaVisual/WindowProbe/source-preservation.json`。

窗沿依据真实骨架 `BG2/Window_01`、`BG2/Window_02` 顶点在1280×720相机中的投影坐标确定。左下内角由窗扇内沿和窗台内沿求交；安全区域进一步内缩20像素，并限制在可见画面内。位置和测量来源保存在独立文件 `Design/MikaVisual/window_clock_layout.json`。

第四版采用超大数字加裁切，不能保证整段时间排入窗内。本版先计算完整字形包围框，再将它映射到窗内安全矩形；最后的边界裁切仅作为保护。完整文字包围框距测得窗沿最少25.20像素。保留粗方块数字、沿窗面倾斜和柔焦，允许人物遮挡时间，但不再让画面边缘截断数字。

人物遮挡也从手绘头部轮廓改成Unity输出的真实前景透明度，包含窗帘、窗框和花饰。它仍是当前姿态的静态视觉原型，正式界面尚未实现动态Spine遮挡。

## 验证

- Unity实际取景完成；正式Companion/Bootstrap场景与行为配置三项哈希保持不变。新增的 `Assets/Editor/WindowClockVisualProbe.cs` 仅用于编辑器取景，不进入运行逻辑。
- 浏览器交互20项通过，包含完整文字四角位于窗内且保留20像素净距。
- 对实际拟合后的SVG时钟进行1280×720原尺寸独立像素渲染，比较时钟开/关两帧：39,849个时钟变化像素，窗外0像素、安全区域外0像素；阈值为每通道差值至少3。检出的实际数字像素距测得窗沿至少32.43像素，柔焦边缘没有穿出安全区。
- 像素审计使用独立平底画布，避免审阅边框、参考线在浏览器合成切换时产生无关抗锯齿差异；与页面使用同一份SVG、字体、拟合矩阵和裁切区域。

结果见 `revision5_checks.json`、`clock_pixel_audit.json`；可重现脚本为 `check_revision5.mjs`、`audit_clock_pixels.py`，布局生成器为 `calibrate_window.py`，均位于 `Docs/Design/MikaVisual`。前四版原型和图片保留。

[无遮挡校准图](Design/MikaVisual/r5-windowcheck.png) · [正常陪伴画面](Design/MikaVisual/r5-idle.png) · [Unity原始无遮人物与窗帘截图](Design/MikaVisual/WindowProbe/window-without-character-curtains.png)

校准图青线为测得窗内边界，虚线为内缩后的安全区域；这些线不出现在正式陪伴布局。隐藏人物后出现的床铺底部空洞来自原素材被人物遮住的部分，不属于时钟裁切错误。

本轮仅新增编辑器测量工具、取景证据、文档和独立原型；未替换正式程序界面，也未读写玩家聊天、密钥、记忆或配置。等待视觉审阅后再进入正式界面实现。
