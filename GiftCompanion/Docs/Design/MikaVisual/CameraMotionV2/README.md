# 窗边运镜参考 02

2026-11-02。入口preview.html，可双击本地打开；motion.json为独立参数文件，同时嵌入HTML以避免file://跨文件配置读取限制，调整时需同步。

复用../CameraMotionV1/Layers中的Unity原始分层，不重复拷贝素材。终点退后为center(920,280)、zoom1.95、rotation-10°；保留窗台、窗框、花瓶、月光和纱帘。时间改为原比例52px Georgia的2106，不加宽、不保留旧大型块状时间。菜单两排三列靠窗下沿，含返回。时间/菜单只有一份，固定场景坐标，并使用与背景相同的camera矩阵；透明度恒定，无屏幕空间替身，无前进/返回时的淡出。人物/窗帘在其后绘制，自然遮挡它们。

新增月光为Canvas渐变光带，纱帘为透明曲面和柔和折痕，非新AI绘制或源纹理改写。原始分层与生产UI不变。本目录为2.5D视觉参考，菜单业务尚未接入。

render_motion.mjs用于隐藏Edge渲染，encode_gif.py编码GIF。Frames为临时中间文件，编码确认后可清理。保留关键帧、gif-report.json、render-report.json、world-ui-report.json及最终GIF。前一版独立保留。
