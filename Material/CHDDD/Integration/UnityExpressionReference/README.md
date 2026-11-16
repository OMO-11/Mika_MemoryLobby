# Unity 表情参考工程（冻结）

2026-10-11：从已完成渲染验证的 Temp/UnityExpressionReviewV17 原样复制 Assets、Packages、ProjectSettings，保留全部 .meta。未复制 Library、Logs、UserSettings。

使用 Unity 6000.3.7f1 独立打开本目录，然后打开 Assets/Review/UnityExpressions.unity。包含 >< V15 和 -ω- V17 的几何、8 个形变、遮罩、材质与实际渲染用参数。FBX 仅含测试用 Idle 参照，不能替代完整 18 动作模型。

Editor/CHDDDUnityExpressionReview.cs 为编辑器渲染工具，非 Player 运行时控制器；输出路径仍指向原工作区 Reviews/UnityExpressionV17。正式移植请按 Docs/Production/CHDDD/动作与表情详解.md 的遮罩、透明和复位约定实现。不要整目录混入正式工程导致 GUID 冲突。

验证记录与预览保留在 Material/CHDDD/Reviews/UnityExpressionV17；副本逐文件哈希见上级 unity_expression_reference_files.json。
