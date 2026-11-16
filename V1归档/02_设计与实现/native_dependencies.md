# Phase 0 原生依赖

2026-10-11。来源、固定版本与下载哈希见 `Validation/Phase0/native_dependencies.json`。

| 依赖 | 来源 / 版本 | 工程位置 | 许可 |
| --- | --- | --- | --- |
| SQLite Windows x64 | [官方下载](https://www.sqlite.org/download.html)，3.54.0；ZIP 官方 SHA3 已核对 | `Assets/Plugins/x86_64/gift_sqlite3.dll` | [SQLite public domain](https://www.sqlite.org/copyright.html) |
| UniWindowController | [官方仓库](https://github.com/kirurobo/UniWindowController)，0.9.8，固定提交 `304f9ba2aa4a8fae7f3c71f38118c44722a2f6cc` | 本地嵌入包 `Packages/com.kirurobo.uniwindowcontroller`，UPM 名 `com.kirurobo.uniwinc` | MIT，保留包内 `LICENSE.md` |

SQLite DLL 改名 `gift_sqlite3`，避免与 Unity 编辑器自带 SQLite 符号混用；Core 直接调用其 C ABI，没有额外数据库框架。FTS5 已使用实际 DLL 验证。

窗口控制使用 Windows 原生插件和 D3D11；按官方要求关闭 Flip Model Swapchain。未把 macOS bundle 和上游示例场景加入本轮 Windows 项目。编辑器不会透明化其自身窗口。

`Fetch-Phase0Dependencies.ps1` 通过系统 HTTPS 下载官方内容，再由 `fetch_phase0_dependencies.py --from-cache` 核对并装配。已有文件不同则保留并报错；不静默覆盖修改内容。重建或升级依赖后应重做对应原生测试。

Spine 的授权与 Runtime 许可证沿用已确认的交接记录；GPT-SoVITS 与 Python 依赖保留其交付 LICENSE / 包元数据，不将非商业用途等同于许可豁免。
