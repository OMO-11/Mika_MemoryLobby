# Mika视觉稿第八版：竖向轻微加高

2026-10-31，Asia/Shanghai。独立视觉稿，正式Unity界面未改。

[可点击稿](Design/MikaVisual/revision8.html) · [正常画面](Design/MikaVisual/r8-idle.png) · [无遮挡校准](Design/MikaVisual/r8-windowcheck.png)

按玩家“竖直方向再拉长一点，不要过头”的要求，在第七版基础上加高5%：高度218.469→229.393像素，宽度仍383.501像素，倾角仍26.8166°。四个数字统一尺寸，上下等宽、四角90°，无分段透视。为容纳略高的完整矩形，在已有安全区内微调位置；安全区本身未扩大，数字仍无冒号，真实前景遮挡保留。

独立布局window_clock_layout_v8.json。沿用SVG自身测量等宽数字宽度，避免6被截断。复用既有Unity实测图，不改场景或源素材。

浏览器23项通过；移除窗口裁切后审计实际字形60,635个变化像素，窗外0、安全区外0，像素最小窗沿净距20.92。证据：revision8_checks.json、clock_pixel_audit_v8.json、window_clock_ink_screen_v8.json、r8-window-ink-on/off.png。已查看校准截图，测试浏览器退出。前七版保留，开发日志及checkpoint同步，等待视觉反馈。
