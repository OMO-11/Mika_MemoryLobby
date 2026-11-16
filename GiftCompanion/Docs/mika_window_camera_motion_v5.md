# 窗边运镜参考第五版：左侧菜单与右上数字时间日期

2026-11-02，Asia/Shanghai。按玩家最新要求调整排版，继续为独立视觉参考。

[参考GIF](Design/MikaVisual/CameraMotionV5/window-camera-reference-v5.gif) · [交互原画质](Design/MikaVisual/CameraMotionV5/preview.html) · [窗边关键帧](Design/MikaVisual/CameraMotionV5/04-window-meteor.png)

菜单移至左侧world(900,185)，保留已实际加载的萝莉体16px、紧凑单列、行距18.5。右上world(1060,190)替换指针钟：32px萝莉体的标准24小时“HH : mm”，下方12px日期“yyyy.MM.dd”，仅两行文字，不加表盘或装饰面板。GIF示例21 : 06与2026.11.02；交互HTML按Asia/Shanghai、hourCycle=h23读取实时时间与日期，小时分钟补零，不显示AM/PM。

正面终点和镜头路线沿用第四版：center(980,282)、zoom2.5、rotation-26.8165748°。时间/日期和菜单仍只有各一份场景对象，随背景同一镜头矩阵变换，alpha恒0.76/0.78，不做运镜淡出，人物/帘子自然遮挡；前进点击区域改为数字时间日期区域，菜单末项返回。其余业务菜单暂为视觉占位。

参数CameraMotionV5/motion.json独立并同步嵌入HTML，复用V1原始分层和共享Fonts/Lolita.ttf，无新素材拷贝、不启动Unity。正式程序、玩家资料、候选包未改，旧版保留。

验证：完整174帧隐藏Edge渲染无页面异常、6时点常驻alpha验证通过、实际萝莉体loaded、时间日期格式检查通过；见render-report.json/world-ui-report.json/font-report.json/clock-report.json。GIF960×540、15fps、174编码帧、11600ms、15,015,400字节；重新解码/总时长/窗边帧校验通过。三个正式场景/行为配置hash与基准一致，浏览器退出，中间Frames清理，其余资料保留。
