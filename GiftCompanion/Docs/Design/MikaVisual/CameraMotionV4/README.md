# 窗边运镜参考 04

2026-11-02。玩家否定V2/V3斜向终点，最新构图回到第一版正面窗户，镜头稍退。终点center(980,282)、zoom2.5（第一版2.85）、rotation-26.8165748°与窗面倾角相抵，菜单/时钟终点呈正向，不斜排。保留月光、纱帘、窗框与窗台下沿。

菜单紧凑单列16px萝莉体、行距18.5，无额外字距。用户给出的系统字体名Lolita Regular通过注册表定位至C:/Users/fufu/AppData/Local/Microsoft/Windows/Fonts/Lolita.ttf，复制一次到../Fonts/Lolita.ttf用于本地参考；source.json记录来源/哈希。@font-face明确绑定MikaLolita，并等待字体加载，失败会报错，不静默冒充其它字体。font-report.json验证loaded。

时钟重新设计为透明指针钟，半径36逻辑像素，12个小时主刻度及分钟细刻度，示例21:06，无旧大数字、宽字、分组数字或衬线时钟继承。时间/菜单仍是单一场景对象，与背景矩阵相同，alpha0.76/0.78恒定，不淡出，人物/帘自然遮挡。

preview.html本地直开，motion.json独立并同步嵌入页面。点击指针钟前往窗边，菜单“回到未花”返回；业务菜单仍为视觉占位。资源复用CameraMotionV1/Layers，本轮无Unity启动、不改生产程序。render_motion.mjs渲染，encode_gif.py编码，Frames为可清理临时帧。
