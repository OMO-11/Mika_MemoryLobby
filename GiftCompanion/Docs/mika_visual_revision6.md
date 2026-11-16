# Mika视觉稿第六版：数字填满虚线区域

2026-10-31，Asia/Shanghai。仅修改独立视觉原型，正式Unity界面未变。

[打开第六版可点击稿](Design/MikaVisual/revision6.html)，选择“09 窗内校准”查看完整四位数字，选择“01 陪伴”查看恢复人物后的遮挡效果。

- 删除时间冒号，示例从21:06改为2106，小时和分钟保留四位。
- 按用户要求同时拉长、拉高数字，填入第五版已有的虚线安全区域；没有放宽窗沿边界。
- 使用实际字形墨迹边界消除字体上下留白；将整个字形分成两段透视映射，贴合安全区域上沿的折角。各角距虚线约1像素，保持数字在窗内。
- 保留Unity导出的真实人物、窗帘、窗框和花饰前景遮挡，以及第四版已确认的菜单与音乐控件。

[无遮挡校准图](Design/MikaVisual/r6-windowcheck.png) · [正常陪伴画面](Design/MikaVisual/r6-idle.png)

本轮复用第五版Unity取景与窗框测量，不重新修改场景或素材。布局文件为独立window_clock_layout_v6.json，baseline字段仅保留第五版矩形作为来源记录；当前排版直接使用safePolygon生成两段透视区域。原型暂以本机Impact显示字形，正式发布字形与动态Spine遮挡仍待Unity实现阶段。

隐藏Edge交互22项通过，包含无冒号四位数字、虚线区域角点贴合、原有音乐和菜单交互。1280×720实际时钟像素审计：88,364个变化像素，窗外0、安全区域外0；检出像素距测得窗沿至少20.23像素。证据为Design/MikaVisual/revision6_checks.json和clock_pixel_audit_v6.json；脚本check_revision6.mjs、audit_clock_pixels_v6.py。字形拉伸、截图已人工检查，不宣称正式程序已实现。

前五版及证据保留，等待本版视觉反馈。
