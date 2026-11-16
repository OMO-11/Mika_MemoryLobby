# Phase6维护说明

程序：完整目录复制/移动，Runtime仅一份；Release/release_manifest.json包含文件尺寸、SHA256、Unity与数据格式版本。首次候选由工程Tools/Build-Phase6.ps1与Package-Phase6.py装配。开发启动器不进入玩家包，普通启动不读取Editor Secrets，不需要工程W盘路径。

资料：Config、Content、VoiceReferences、Diary、AudioCache和memory.db是快照范围。Secrets、Validation、Logs、Diagnostics、RuntimeCache、Backups和RestorePoints排除，防止私钥外泄和备份套娃。不要分享完整资料包作为诊断；应用内“导出脱敏诊断”仅输出固定状态字段。

恢复：先检查文件路径/数量/解压大小/SHA256/SQLite完整性/角色与数据库版本，再预览。确认后先产生before-restore快照，pending_restore.json安排下次启动；应用获取单实例锁后、打开任何数据库前应用恢复。先向RestorePoints复制原数据、落盘并核对哈希，再发布CopySnapshotComplete事务；之后复制替换固定数据根，不依赖文件夹重命名（本机加密资料目录实际拒绝Directory.Move）。未提交事务在下次启动从完整副本回滚，回滚不消耗副本，允许中断后再次回滚。需要为暂存、原资料副本和恢复点备份留额外磁盘空间。未知或损坏事务不盲目继续，应保留目录让维护者处理。源快照未改变，正式玩家资料不用于开发恢复试验。

外部文件：音乐、空间、人物卡和声音继续使用现有独立JSON文件，不建立第二套数据库配置。修复单项配置前保留原件到ConfigRecovery。更新Key或重读全部资料后重启，使所有客户端统一加载；不会同时生成第二个CharacterCore。

更新：先备份，使用新的完整版本目录，不覆盖旧目录或玩家资料。回退前核对数据库版本，必要时明确选择旧快照，接受其时间回溯。Phase6读取旧原生DPAPI密文，新保存采用DPAPI1封装；较旧程序可能不认识新封装，回退时需用对应版本重新配置本机Key。不要把Secrets当跨机器迁移文件。无自动更新或自动跨库合并。

环境裁剪：仅在发布副本排除可再生Python字节码、日志与占位文件；原Runtime、双语模型、Torch/CUDA动态库均保留。发行依赖许可证继续随原Runtime保留，Licenses中有顶层索引。训练素材和模型原档仍由维护者保管，不进入普通玩家资料备份。
