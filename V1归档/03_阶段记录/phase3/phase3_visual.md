# Phase 3 视觉与人设初稿

2026-10-14。用户授权先按方案实现、随后精修。人设仅落实已确认的 Mika / 老师关系及中文陪伴语气；不添加原作具体事件、私人共同经历或预设恋爱关系。配置保持只读，不受记忆提取修改。

**恢复后最终结果：** 已查看暂停对比，确认显式 Straight Alpha 能消除发布版异常白色覆盖，随后将该 Shader 绑定到场景加载器自有运行时材质。原 Shader 本身已在 Always Included，补配置不足以解决；Unity 内部分支异常根因未完全隔离。原始图集 / 骨架、第三方加载器和 Shader 文件均保留，主程序不关闭 HDR。发布修正与最终 EXE 回归已通过，人工明暗 / 口吻精修待用户体验，见 [phase3_delivery.md](<phase3_delivery.md>)。

保留 CH0069_home 的完整场景和原作 Idle。全局八情绪先全部可解析，neutral 使用原作基础表情，其余明确回退 neutral；思考继续使用已有 thinking_neutral。三个摸头预设保持独立，不冒充八种持久表情。模型动作白名单先仅 none，新的演出待独立验证后开放。这是首版资源限制，后续可在同一 Adapter 上精修。

3A 历史编辑器对比保存到 `Validation/Phase3/3A.before.png`、`3A.after.png`、`3A.visual.json`。实际骨架为 284 个 Normal、10 个 Additive 插槽，无 Multiply / Screen，补齐后者试验未改变画面，已撤回。Companion 相机独立曝光 0.95 / 高光压缩 0.35，同帧编辑器接近纯白像素 22,452 → 1,632；它是独立观感调整，不能代替发布 Alpha 修正。

3E 新 EXE 使用同一 Idle 1.25 秒比较，接近纯白像素：原 Shader 254,655，显式 Alpha 1,744，再关闭 HDR 112。主程序保留 HDR，采用显式 Alpha；修正编辑器图 1,632。发布图包含时钟 / 提示，统计不宣称编辑器 / EXE 逐像素相同。报告及图片见 `Validation/Phase3/visual_review.json` / `release_explicit_alpha.png` / `3E.editor.final.png`；较早图像保存在 FirstUI / PausedRendering。正常发布 UI 图也已审阅，历史按钮、字幕、时钟与 1024×768 布局可读，仍以用户观感为最终精修依据。

中文使用 Noto Sans CJK SC Regular，原始字体约 16.4 MB，动态 TMP 图集支持输入和历史中的汉字。字体直接来自 [Noto 官方仓库](https://github.com/notofonts/noto-cjk/tree/main/Sans/OTF/SimplifiedChinese)，随附 [SIL OFL 许可](https://github.com/notofonts/noto-cjk/blob/main/Sans/LICENSE)。正文禁用 TMP 富文本，不将模型文字当作排版指令。

布局采用低干扰时钟、下方字幕、临时输入和居中菜单；MomoTalk 使用临时历史面板。BGM 暂为空列表，正常静音，不使用交付台词充当音乐。
