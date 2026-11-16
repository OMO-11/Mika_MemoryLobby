# 窗边运镜参考第三版：上移取景与竖排字体

2026-11-02，Asia/Shanghai。根据玩家标图继续细修参考GIF，正式运行界面未改。

[参考GIF](Design/MikaVisual/CameraMotionV3/window-camera-reference-v3.gif) · [交互原画质](Design/MikaVisual/CameraMotionV3/preview.html) · [窗边关键帧](Design/MikaVisual/CameraMotionV3/04-window-meteor.png)

相对第二版，终点center由(920,280)改为(920,244)，向上36逻辑像素；zoom1.95与rotation-10°保持。减少底部墙纸，保留窗台下沿、窗框、月光、纱帘和花瓶。

菜单按标注移到左侧world(886,125)，改为单列六项：聊天记录、日记、陪伴空间、人物设定、设置、回到未花。字体改17px KaiTi（备选STKaiti），字距1.5、行距27。保留自然倾斜的窗面关系，透明背景，不增加包围面板。

时间改为细线Segoe UI Light，300字重、56px，小时/分钟分成两组“21 06”，不使用冒号、横向加宽或旧块状样式，配一条很淡的细线。world origin(1030,100)，分钟偏移84。系统字体仅在原型中引用，未复制/分发字体文件；正式Unity字体待实现阶段确定。

保留第二版场景固定规则：时间/菜单与背景共用镜头矩阵，仅各一份，alpha恒0.76/0.78，全程不淡出，人物/帘子自然遮挡。返回点击区与演示点击提示已随竖排位置更新。菜单业务尚未接入，参考仍是2.5D。

独立参数CameraMotionV3/motion.json同步嵌入preview.html，本地直开无需服务。原始层继续引用CameraMotionV1/Layers，无新素材拷贝或Unity启动。前两版保留。

验证：隐藏Edge完整174帧渲染无页面异常；6个前进/停留/返回时点alpha恒定，world-ui-report.json。GIF960×540、15fps、174编码帧、11600毫秒、16,162,302字节；已重新解码、校验时长及窗边画面。关键帧已查看。三个正式场景/行为配置hash未变，source-preservation.json；浏览器已退出，中间Frames清理，可用渲染脚本重建。正式程序/玩家资料/候选包未改，等待视觉反馈。
