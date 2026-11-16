# 窗边运镜参考 05

2026-11-02。菜单移至左侧world(900,185)，保持实际萝莉体16px/行距18.5。右上时钟world(1060,190)改32px萝莉体HH : mm标准24小时格式，下方12px显示yyyy.MM.dd，不加表盘、额外装饰或包围面板。GIF固定示例21 : 06与2026.11.02，交互预览使用Intl.DateTimeFormat、Asia/Shanghai、hourCycle=h23显示实时时间与日期。

保留V4正面终点center(980,282)、zoom2.5、rotation-26.8165748°；时间/菜单同一场景矩阵，全程不淡出、自然遮挡。菜单业务为视觉占位，时钟前进与末项返回可用。motion.json独立并同步嵌入preview.html，本地直开。

复用V1原层及共享Fonts/Lolita.ttf，不再复制素材，不启动Unity。render_motion.mjs渲染，encode_gif.py编码；Frames可清理。正式程序/玩家数据/候选包未改，前四版保留。
