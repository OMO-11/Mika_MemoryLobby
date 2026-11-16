# GiftCompanion 2.0.2 离线安装包维护

当前唯一完整输入为 `Releases/Gift-V2-Seaside`，不再依赖旧 V1/V2 包。

1. 使用官方 Inno Setup 7.1.1 x64；先核验编译器数字签名为有效的 Pyrsys B.V.。`Get-InstallerCompiler.ps1` 提供核验及便携编译工具提取流程，提取后会卸载临时工具安装。本机工具位于 `Tools/InstallerCompiler/InnoSetup/ISCC.exe`。
2. 执行 `python Tools/Installer/Package-Installer.py source`，核对完整源目录 SHA-256。
3. 执行 `Tools/Installer/Build-Installer.ps1`，输出至 `Installers/GiftCompanion-2.0.2`。
4. 执行 `Tools/Installer/Test-Installer.ps1`，在隔离目录安装、核对全部文件、验证快捷方式和卸载注册、运行 55 项大厅检查和 3 项重开检查，再卸载测试安装。该脚本需要真实用户环境，不能使用被沙箱重定向的 LocalAppData；不会正常启动到正式资料。若已存在同 AppId 的安装，脚本拒绝覆盖。
5. 在输出目录准备《安装说明.md》，执行 `python Tools/Installer/Package-Installer.py archive`，生成单个 ZIP64 交付文件及 SHA-256。外层不重复压缩已压缩的安装分卷，逐成员读回并校验 SHA-256。

默认安装至 `%LOCALAPPDATA%\Programs\GiftCompanion`，程序资料继续使用 `%LOCALAPPDATA%\GiftCompanion`。安装器不清理正式资料，也不在安装后自动启动应用；可在完成页手动勾选启动。卸载仅移除安装器登记的程序文件及快捷方式。

`Clear-ObsoleteVersions.ps1` 是本次用户授权的精确清理清单，仅能在安装、运行、卸载和归档校验全部通过后使用，不是通用的构建清理器。旧封装脚本依赖已清理的历史包，仅保留作历史源码；重做安装包请使用此目录的新流程。
