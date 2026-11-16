# 窗边运镜参考第四版：回归正面、萝莉体与指针钟

2026-11-02，Asia/Shanghai。玩家指出V2/V3方向偏离，要求重点效果回到第一版，仅稍缩小取景；不再斜排，使用萝莉体并收紧菜单，时钟重新设计。

[参考GIF](Design/MikaVisual/CameraMotionV4/window-camera-reference-v4.gif) · [交互原画质](Design/MikaVisual/CameraMotionV4/preview.html) · [窗边关键帧](Design/MikaVisual/CameraMotionV4/04-window-meteor.png)

终点恢复第一版-26.8165748°转正角，与26.8165748°窗面倾角相抵，所以菜单/时钟终点为正向。zoom由第一版2.85改为2.5，中心(980,282)，画面稍退、稍宽，露出窗框、窗台下沿、纱帘/月光/花瓶。保留已认可的接近人物、经过人物、窗边停留与返回顺序。

菜单位于world(1055,290)，单列六项，16px萝莉体，行距18.5（字号1.156倍）、无额外字距，减少选项空隙。用户提供字体名“C:/Windows/Fonts/Lolita Regular”；注册表解析实际文件为C:/Users/fufu/AppData/Local/Microsoft/Windows/Fonts/Lolita.ttf。原始文件11,949,844字节，复制一次到共享Docs/Design/MikaVisual/Fonts/Lolita.ttf，仅用于参考，来源哈希存Fonts/source.json。页面明确@font-face加载并等待完成，font-report.json确认MikaLolita loaded，没有使用幼圆或楷体替代。

时钟全新设计为透明细线指针钟：半径36逻辑像素，小时主刻度/分钟细刻度，简洁长短针，示例21:06。不再继承旧的大号加宽数字、衬线数字或分组细线数字。world origin(1100,242)。时间和菜单仍只绘制一份、使用同一背景镜头矩阵、alpha恒0.76/0.78，人物与帘子自然遮挡，往返不淡出。点击圆形时钟前进，紧凑菜单末项返回。

独立参数CameraMotionV4/motion.json同步嵌入preview.html，本地直开。复用V1分层，不改源纹理、不再次启动Unity；此前所有版本保留，但V2/V3斜向布局已被否定，不作为后续优化基准。当前仍为2.5D视觉参考，业务菜单尚未接入正式程序。

验证：隐藏Edge完整174帧渲染无页面异常；6时点常驻UI透明度验证通过；萝莉体加载状态验证通过。GIF960×540、15fps、174编码帧、11600毫秒、15,028,236字节；重新解码并校验时长和窗边关键帧。三个正式场景/行为配置hash与基准一致；浏览器退出，中间Frames清理，源层/字体/关键帧/代码/报告/成品保留。正式程序、玩家资料及候选包未改。
