# Phase6最终 checkpoint

2026-10-19 23:14（客户端日期/中国时区）。**本轮开发与V1候选技术交付结束，程序已正常关闭。Phase5玩家体验暂缓；Phase6正式玩家验收仍待反馈。** 不自动进入Phase7，也不因恢复本记录而重跑全部技术检查。

## 阅读与运行入口

先读phase6_status.md、phase6_delivery.md、phase6_compatibility.md、phase6_requirements.md。普通入口：Releases/Gift-V1-Candidate/GiftCompanion.exe，保留整个目录。候选约8.886GiB，一份双语Runtime；正常启动不读工程Editor Key，Home“资料与连接”配置本机Key。旧Builds/Phase5、素材、CHDDD V7及源Runtime保持原件，39项冻结基线复核通过。

## 实现

6A逐项宪法核对与冻结；6B掩码Key/DPAPI、显式测试、受控重启、配置修复/脱敏诊断/启动恢复；6C完整手动快照/恢复/迁移；6D独立目录/许可证/说明与文件哈希；6E本机短时验证；6F候选交接。数据库仍v10，备份格式v1，没有待实现的功能代码。

维护先暂停Core、等待MemoryService.PauseAndDrain、停止语音并保存状态，期间禁止切换。SQLite Backup包含WAL提交数据，副本转DELETE日志模式。快照含固定数据根和可选AudioCache，排除Secrets/日志诊断/Validation/Runtime/已有备份/已知临时文件。

恢复先暂存校验预览，确认后留before-restore快照。关闭连接后向RestorePoints完整复制原资料、Flush并核对哈希，再发布CopySnapshotComplete事务并复制替换。回滚不消耗副本，支持再次中断；成功清理暂存。Secrets/安装Runtime不替换，外部应用/搜索范围/工具重置，历史任务不重放。新Key封装兼容读旧DPAPI，旧版未必识别新封装，回退重配Key，详见维护说明。

## 最终证据

- ReleaseCandidate.EditMode153/153，FinalPreview.PlayMode36/36。最终Release构建0错误，304753730 bytes。后续只改备份纯逻辑，其原生路径已复测，场景代码未改。
- Standalone/Offline8、Restored12：真实关闭重启还原设置/MD批注/素材/权限，仍共享Core；最终隔离资料名phase6-candidate-20261019-c，正式玩家资料未用于测试。
- 整个目录实际移到中文新位置，禁用开发Key并设无效开发Runtime覆盖，双语9项通过后已移回。日语预热88365ms/推理10130ms，中文23445ms/10281ms，自有模型均退出。YAML和库缓存写用户RuntimeCache。
- Stability：12次模式往返+600.4603秒Home空闲，6项通过，主进程CPU按16线程归一平均1.071%。Unity私有内存0不可用；外部中间采样私有564.95→566.17MiB、工作集371.10→374.11MiB，不能证明完整无泄漏。PID30176已结束，勿再操作该PID，可能复用。
- 原生共35项通过；安装树27783份文件元数据前后无变化，39项源资产/旧交付冻结通过。
- 候选载荷9541379576 bytes、Runtime9236606178 bytes，27782条SHA256。副本仅排除64732362 bytes可再生字节码/日志，29份无源码字节码保留。
- Validation/Phase6/acceptance.json、package_summary.json、frozen_verification.json、install_write_verification.json均已通过。发布清单在候选Release/release_manifest.json，清单自身另有工程验收哈希。

## 已解决发现

StandaloneAttempt1：多行提示词误传Windows路径API，已改盘符/UNC识别并补回归。StandaloneAttempt2：加密AppData拒绝Directory.Move，已回滚且旧资料保留；复现后改复制事务，第三轮全通过。失败证据保留，不需继续调查已解决问题。十分钟外部辅助计时曾遇时区解析，已改进程启动时间，主Stopwatch计时未受影响。

## 待玩家与后续边界

真实Key在界面保存/重启的完整玩家流程、1—2小时真实对话、100/125/200%缩放、当前多屏/负坐标/拔屏、锁屏/睡眠、另一台电脑/Windows账户/其他GPU、严格只读ACL仍未验证。今天只有2560×1440/150%单屏、Windows11/RTX4060Laptop。技术通过不能代替这些体验，不宣布V1正式验收完成。

工具：Build-Phase6.ps1、Package-Phase6.py、Test-Phase6Player.ps1、Audit-Phase6Install.py、Inspect-Phase6Hardware.ps1、Save-Phase6Acceptance.py。重建会改变发布指纹，须按修改范围复核后封存。不要删除源Runtime/旧构建或结束无关Unity/Python。没有待轮询exec或未结束构建。用户要求额度低于7%立即交接；工具无账户额度百分比查询，未收到低额度信号，不虚构余额。
