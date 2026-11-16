# Mika MemoryLobby

基于 Unity 的 Windows 桌面陪伴项目。当前版本 **2.0.2**，包含窗边阁楼、海边记忆大厅、流式聊天、共同记忆、MomoTalk、日记、背景音乐和中日双语本地语音。

## 下载与安装

在 [v2.0.2 Release](https://github.com/OMO-11/Mika_MemoryLobby/releases/tag/v2.0.2) 下载以下文件，放到同一个文件夹：

- `GiftCompanion-2.0.2-Setup.exe`
- `GiftCompanion-2.0.2-Setup-1.bin` 至 `GiftCompanion-2.0.2-Setup-4.bin`（共四个分卷）
- `SHA256SUMS.txt` 和 `INSTALL.md`（安装说明）

双击 Setup.exe，按提示安装。分卷无需合并或解压，保持文件名不变。安装文件共约 6.18 GB，完整程序约 9.58 GB；建议预留至少 20 GB 空间供下载及安装。安装包包含本地语音环境，无需另装 Unity 或 Python。

GitHub Release 单个附件需小于 2 GiB，因此使用安装器自带分卷：[GitHub 官方说明](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases)。页面自动提供的 Source code 是开发源码，安装请使用 Setup.exe。

默认安装到 `%LOCALAPPDATA%/Programs/GiftCompanion`，支持选择目录及桌面快捷方式。对话、记忆、配置和缓存位于 `%LOCALAPPDATA%/GiftCompanion`，安装与卸载均保留资料。迁移前请先在应用内备份。

在 PowerShell 中执行 `Get-FileHash .\GiftCompanion-2.0.2-Setup* -Algorithm SHA256`，与 SHA256SUMS.txt 对照即可核验下载完整性。

## 使用

- Enter 打开输入并发送，Shift+Enter 换行；Esc 打开菜单或逐层返回。
- Esc → 陪伴空间，切换窗边阁楼和海边记忆大厅。
- 在“资料与连接”配置模型服务和 Key。未配置时仍可进入大厅、查看已有资料和缓存；在线聊天需要可用的模型服务。
- 声音设置提供语音及复听选项。语音推理在本机运行，实际速度取决于硬件；已验证设备与边界见[兼容说明](GiftCompanion/Docs/phase6_compatibility.md)。

当前默认进入陪伴空间，桌宠入口暂时关闭。

## 开发

工程固定 **Unity 6000.3.7f1**，Built-in / Gamma / D3D11。先安装 Git LFS，再克隆：

```powershell
git lfs install
git clone https://github.com/OMO-11/Mika_MemoryLobby.git
cd Mika_MemoryLobby
git lfs pull
```

在 Unity Hub 添加 `GiftCompanion`，打开 `Assets/Scenes/Bootstrap.unity`。保留 `.meta` 文件和现有依赖版本。

| 目录 | 内容 |
| --- | --- |
| `GiftCompanion/Assets` | C# 源码、场景、角色、运行素材与测试 |
| `GiftCompanion/Packages` / `ProjectSettings` | Unity 依赖与工程设置 |
| `GiftCompanion/Tools` | 构建、验证、语音和安装器工具 |
| `GiftCompanion/Docs` | 架构、操作说明、设计与验证记录 |
| `Material` | 保留的角色、大厅和音频制作资料 |
| `Docs` / `Tools` | 素材审查与制作流程 |
| `V1归档` | 历史文档与验收记录；旧启动路径仅供追溯 |

Unity 缓存、个人资料、密钥、旧阶段构建、安装文件、大型原始语音压缩包及生成的制作预览不提交。FBX、OGG、Blender 制作稿和 Unity 资源包通过 Git LFS 管理。

语音运行环境不随 Git 克隆下载。可从 Release 安装后的程序目录复制 `Runtime/GPT-SoVITS` 到工程的 `GiftCompanion/Runtime/GPT-SoVITS`。恢复方法见[语音环境说明](GiftCompanion/Docs/voice_runtime.md)。

重新封装前先生成并核验 `GiftCompanion/Releases/Gift-V2-Seaside` 完整目录，再使用[安装器维护流程](GiftCompanion/Tools/Installer/README.md)。历史脚本可能引用已清理构建，以当前流程为准。

## 版本与验证

2.0.2 包含切换大厅后收起菜单、阁楼原生入场和海边时钟修正。2026-11-14 完成安装、27,790 项发布文件 SHA-256、55 项大厅检查、3 项跨进程重开及卸载资料保留验证，见[原交付报告](GiftCompanion/Docs/Delivery/Installer-2.0.2-2026-11-14/交付与清理报告.md)。

2026-11-16 沿用同一安装器，重新核对全部分卷哈希；本轮未重跑 Unity 测试、真实模型聊天或 TTS 推理。清理范围见[发布整理记录](GiftCompanion/Docs/Delivery/GitHub-2.0.2-2026-11-16/README.md)。

## 第三方资源

见[第三方说明](GiftCompanion/Docs/phase6_licenses.md)及安装目录的 `Licenses`。角色、美术、音乐、语音模型、Spine 等依赖沿用各自许可和项目既有范围，本仓库不授予第三方素材的额外使用或再分发权。
