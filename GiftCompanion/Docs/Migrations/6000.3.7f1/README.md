# Unity 6000.3.7f1 适配

2026-10-08 按用户已安装的 Unity 6000.3.7f1 进行兼容性适配。本轮只变更工程版本、包依赖、编辑器初始化 API、脚本路径解析与对应文档，角色核心和产品设计继续沿用骨架。

本目录 `source-before-adaptation.zip` 保留适配前的 Assets、Packages、ProjectSettings、Tools 和工程 README。旧测试报告在 `Docs/Validation/6000.6.4f1/`。这些备份不表示两个版本可以混用；需要恢复时必须协调源文件、包依赖与编辑器版本。

原先 uGUI 2.6.0 的 Physics 间接依赖改为显式声明，避免更换为 uGUI 2.0.0 时丢失胶囊体所需模块。JSON 库维持 3.2.2。包锁由目标版本 Package Manager 重新生成。

旧 Library 保留在工程内 `.MigrationCache/6000.6.4f1-Library`，不参与 6000.3.7f1 导入。首次初始化退出时的原生异常日志保留在 `setup-first-import.log`；后续初始化及两类测试均正常退出。当前结果见 `Docs/validation.md`。
