# Mika视觉稿第七版：水平调整与统一直角字形

2026-10-31，Asia/Shanghai。仅修改独立视觉稿，正式Unity运行界面尚未改版。

[打开第七版可点击稿](Design/MikaVisual/revision7.html)，选择“09 窗内校准”查看完整数字，选择“01 陪伴”查看真实前景遮挡。

- 删除第六版两段透视与matrix3d，整组2106只用一个SVG二维矩阵。
- 沿用第五版统一字高约218.47像素和26.8166°倾角，横向宽度约383.50像素；字号先等比设定，之后仅水平缩放，不做剪切或逐字倾斜。
- 上下等宽、相邻边保持90°，6与其他三位数字共用同一变换。完整矩形保留在既有五边形安全区内，不再强行填满每个角。
- SVG等宽数字的字宽由SVG自身getBBox测量，避免Canvas默认数字宽度与SVG tabular-nums不一致导致右侧6伸出目标矩形。Canvas仅用于字号的墨迹高度与基线。
- 复用第五版Unity实测窗沿、取景和人物/窗帘/窗框/花饰前景；音乐与菜单等交互沿用。

[无遮挡校准图](Design/MikaVisual/r7-windowcheck.png) · [正常陪伴图](Design/MikaVisual/r7-idle.png)。独立布局为Design/MikaVisual/window_clock_layout_v7.json，前六版保留。

隐藏Edge交互23项通过，包括上下等宽、四个直角、四位无冒号、统一倾角、键盘访问与窗口缩放。实际像素审计额外移除窗沿裁切以验证完整字形：59,509个变化像素，窗外0、安全区外0，检出像素距测得窗沿至少24.08像素。没有借助裁切隐藏越界字形。证据为revision7_checks.json、clock_pixel_audit_v7.json、window_clock_ink_screen_v7.json及r7-window-ink-on/off.png。

审阅截图已人工检查，临时浏览器已退出。未重新启动Unity、修改正式场景/运行逻辑/玩家数据或候选发布包。当前仍等待视觉反馈；正式Unity动态Spine遮挡、发布字体与输入方案留待实现阶段。
