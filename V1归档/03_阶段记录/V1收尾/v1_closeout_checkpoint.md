# V1收尾 checkpoint

2026-11-02（Asia/Shanghai）。**入口修改、V1总结、技术验证与独立收尾候选封存完成，等待玩家体验反馈。** 用户要求暂时关闭桌宠入口，启动直接进入陪伴空间，并总结整个V1设计与实现。

已实现Config/app_entry.json，默认companion/false；Bootstrap读配置，Home隐藏桌宠及找回位置按钮，AppController拒绝关闭状态的桌宠切换。源码、资产、共享记忆保留。既有多模式测试仅在自身Validation文件中显式开启Home/桌宠，不改变正式默认。

隔离Both已通过：EditMode165/165、PlayMode39/39。独立构建0错误，304792610 bytes；首轮验证探针引用未启用的ScreenCapture模块，改用已有ReadPixels帧捕获后通过，仅调整探针未改入口业务逻辑。独立EXE默认启动/隐藏/路由拦截/维护/同Core/关闭保存10项通过，截图已查看。

Runtime27585份文件已复制并逐一核对SHA256，对应旧便携环境。新包finish完成，完整Releases/Gift-V1-Closeout的独立10项检查再次通过，截图已查看；测试程序正常退出。新包不含正式Key/聊天，数据库/备份版本不变。

Package seal完成，27782条文件清单、约8.886GiB。Runtime沿用本次逐份校验的副本哈希并核对尺寸/修改时间；旧候选清单与3份程序指纹保持不变。Validation/V1Closeout/acceptance.json passed=true，165逻辑/39场景/10原生检查通过；封包程序与已验证构建的EXE/Core/Runtime DLL哈希一致。两级README入口已指向收尾版。没有待轮询exec、构建或未退出的自有测试进程。

总结已写v1_closeout_summary.md，玩家说明v1_closeout_player_guide.md，完整包同附《V1设计与实现总结.md》。新入口Releases/Gift-V1-Closeout/GiftCompanion.exe，保留整个目录。数据库v10/备份v1，无迁移，正式玩家资料未用于测试。Phase8/9未启动，正式玩家验收保持待反馈。

恢复任务先读本记录与总结，不自动恢复桌宠或重跑旧阶段。需要重新开放时，明确修改玩家Config/app_entry.json的PetEntryEnabled并重启；原始代码、场景、资产与同一数据库都保留。默认StartupMode=companion，损坏配置仍以陪伴/关闭桌宠回退。历史多模式测试单独用自己的Validation配置选择home/true，不能复制该配置到正式资料。
