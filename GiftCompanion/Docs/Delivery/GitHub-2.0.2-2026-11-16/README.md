# 2.0.2 GitHub 发布整理

2026-11-16，Asia/Shanghai。目标仓库 `OMO-11/Mika_MemoryLobby`，保留原私有可见性。

沿用 2026-11-14 完成安装与运行验证的安装器，重新核对 Setup.exe、四个 .bin 和原安装说明的 SHA-256，与原归档记录一致。见 [installer-verification.json](installer-verification.json)。本轮未重新编译或重跑应用功能测试。

清理旧 Builds/V2SeasideReview、根目录 Temp 临时工程、两套自动测试场景及 meta、与安装器分卷重复的 ZIP 及对应校验文件。ZIP 删除前重新核对了完整 SHA-256。共清理 868 个文件、6,546,714,224 bytes，精确范围见 [cleanup.json](cleanup.json)。最终程序、安装器分卷、制作源文件、原始语音档案及玩家资料保留。

Unity 缓存、原始语音压缩包、生成预览和模型检查点留在本机并忽略，二进制制作素材使用 Git LFS。Release 使用原生安装分卷，Setup.exe 与四个 .bin 下载后放在同一目录，无需合并。
