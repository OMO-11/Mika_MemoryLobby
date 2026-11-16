# V1归档

> 2026-11-14：V1 程序包已按用户要求清理，本目录继续保留研发与验收快照。下方 V1 启动器和程序路径为历史入口，已无法运行。请使用[当前 2.0.2 安装包](../GiftCompanion/Installers/GiftCompanion-2.0.2-Windows-x64.zip)。

2026-11-03（Asia/Shanghai）。这是V1研发文档与验收记录的分类快照，原工程与唯一收尾包均保留原位。

- **启动程序**：双击 `01_当前交付/启动V1.cmd`。完整程序仍在 `../GiftCompanion/Releases/Gift-V1-Closeout/`，本归档无需再复制约8.9GiB模型环境。
- **了解V1**：[设计与实现总结](02_设计与实现/v1_closeout_summary.md)。
- **查看进度**：[收尾checkpoint](03_阶段记录/V1收尾/v1_closeout_checkpoint.md)。
- **核对结果**：[收尾验收](04_验收记录/V1Closeout/acceptance.json)。历史165/39/10属于V1，不代表V2已验证。

## 分类

| 文件夹 | 内容 |
|---|---|
| 01_当前交付 | 启动入口、使用/维护/兼容/许可说明、发布清单 |
| 02_设计与实现 | 总宪法、架构、接口、记忆/角色协议与V1总结 |
| 03_阶段记录 | Phase0—6、声音/日记扩展、完善、收尾与开发日志 |
| 04_验收记录 | 精简验收JSON、最终测试XML与关键截图 |

## 使用约定

这是归档快照，不是第二套开发源。继续维护请使用下表“原文件”；不自动同步归档。归档中的有效相对链接已调整，旧构建已被其他任务清理的历史路径仍保留为记录，不能再启动。源码、Material、Runtime、Unity缓存、UserData、Secrets、数据库、音频与原始大日志未复制；没有删除或搬移原文件，也不启动Unity/API/TTS。

V2视觉稿与实施计划维持原分类，见[V2工作区](../GiftCompanion/Docs/Design/V2_Visual_Implementation/README.md)，不混入V1程序归档。若单独搬走本归档，启动器与回源链接需要原项目目录配合；归档文档本身仍可读。

## 文件索引

| 归档文件 | 原文件（后续维护用） |
|---|---|
| [01_当前交付/v1_closeout_player_guide.md](01_当前交付/v1_closeout_player_guide.md) | [GiftCompanion/Docs/v1_closeout_player_guide.md](../GiftCompanion/Docs/v1_closeout_player_guide.md) |
| [01_当前交付/phase6_compatibility.md](01_当前交付/phase6_compatibility.md) | [GiftCompanion/Docs/phase6_compatibility.md](../GiftCompanion/Docs/phase6_compatibility.md) |
| [01_当前交付/v1_refinement_maintenance.md](01_当前交付/v1_refinement_maintenance.md) | [GiftCompanion/Docs/v1_refinement_maintenance.md](../GiftCompanion/Docs/v1_refinement_maintenance.md) |
| [01_当前交付/phase6_licenses.md](01_当前交付/phase6_licenses.md) | [GiftCompanion/Docs/phase6_licenses.md](../GiftCompanion/Docs/phase6_licenses.md) |
| [02_设计与实现/项目总宪法.md](02_设计与实现/项目总宪法.md) | [项目总宪法.md](../项目总宪法.md) |
| [02_设计与实现/v1_closeout_summary.md](02_设计与实现/v1_closeout_summary.md) | [GiftCompanion/Docs/v1_closeout_summary.md](../GiftCompanion/Docs/v1_closeout_summary.md) |
| [02_设计与实现/v1_design.md](02_设计与实现/v1_design.md) | [GiftCompanion/Docs/v1_design.md](../GiftCompanion/Docs/v1_design.md) |
| [02_设计与实现/architecture.md](02_设计与实现/architecture.md) | [GiftCompanion/Docs/architecture.md](../GiftCompanion/Docs/architecture.md) |
| [02_设计与实现/character_protocol.md](02_设计与实现/character_protocol.md) | [GiftCompanion/Docs/character_protocol.md](../GiftCompanion/Docs/character_protocol.md) |
| [02_设计与实现/memory_protocol.md](02_设计与实现/memory_protocol.md) | [GiftCompanion/Docs/memory_protocol.md](../GiftCompanion/Docs/memory_protocol.md) |
| [02_设计与实现/player_content_contracts.md](02_设计与实现/player_content_contracts.md) | [GiftCompanion/Docs/player_content_contracts.md](../GiftCompanion/Docs/player_content_contracts.md) |
| [02_设计与实现/asset_manifest.md](02_设计与实现/asset_manifest.md) | [GiftCompanion/Docs/asset_manifest.md](../GiftCompanion/Docs/asset_manifest.md) |
| [02_设计与实现/native_dependencies.md](02_设计与实现/native_dependencies.md) | [GiftCompanion/Docs/native_dependencies.md](../GiftCompanion/Docs/native_dependencies.md) |
| [02_设计与实现/voice_runtime.md](02_设计与实现/voice_runtime.md) | [GiftCompanion/Docs/voice_runtime.md](../GiftCompanion/Docs/voice_runtime.md) |
| [02_设计与实现/validation.md](02_设计与实现/validation.md) | [GiftCompanion/Docs/validation.md](../GiftCompanion/Docs/validation.md) |
| [03_阶段记录/声音与日记扩展/bilingual_voice_checkpoint.md](03_阶段记录/声音与日记扩展/bilingual_voice_checkpoint.md) | [GiftCompanion/Docs/bilingual_voice_checkpoint.md](../GiftCompanion/Docs/bilingual_voice_checkpoint.md) |
| [03_阶段记录/声音与日记扩展/bilingual_voice_delivery.md](03_阶段记录/声音与日记扩展/bilingual_voice_delivery.md) | [GiftCompanion/Docs/bilingual_voice_delivery.md](../GiftCompanion/Docs/bilingual_voice_delivery.md) |
| [03_阶段记录/开发日志/development_log.md](03_阶段记录/开发日志/development_log.md) | [GiftCompanion/Docs/development_log.md](../GiftCompanion/Docs/development_log.md) |
| [03_阶段记录/声音与日记扩展/expression_diary_checkpoint.md](03_阶段记录/声音与日记扩展/expression_diary_checkpoint.md) | [GiftCompanion/Docs/expression_diary_checkpoint.md](../GiftCompanion/Docs/expression_diary_checkpoint.md) |
| [03_阶段记录/声音与日记扩展/expression_diary_delivery.md](03_阶段记录/声音与日记扩展/expression_diary_delivery.md) | [GiftCompanion/Docs/expression_diary_delivery.md](../GiftCompanion/Docs/expression_diary_delivery.md) |
| [03_阶段记录/声音与日记扩展/expression_diary_implementation.md](03_阶段记录/声音与日记扩展/expression_diary_implementation.md) | [GiftCompanion/Docs/expression_diary_implementation.md](../GiftCompanion/Docs/expression_diary_implementation.md) |
| [03_阶段记录/phase0/phase0_status.md](03_阶段记录/phase0/phase0_status.md) | [GiftCompanion/Docs/phase0_status.md](../GiftCompanion/Docs/phase0_status.md) |
| [03_阶段记录/phase1/phase1_plan.md](03_阶段记录/phase1/phase1_plan.md) | [GiftCompanion/Docs/phase1_plan.md](../GiftCompanion/Docs/phase1_plan.md) |
| [03_阶段记录/phase1/phase1_status.md](03_阶段记录/phase1/phase1_status.md) | [GiftCompanion/Docs/phase1_status.md](../GiftCompanion/Docs/phase1_status.md) |
| [03_阶段记录/phase2/phase2_checkpoint.md](03_阶段记录/phase2/phase2_checkpoint.md) | [GiftCompanion/Docs/phase2_checkpoint.md](../GiftCompanion/Docs/phase2_checkpoint.md) |
| [03_阶段记录/phase2/phase2_delivery.md](03_阶段记录/phase2/phase2_delivery.md) | [GiftCompanion/Docs/phase2_delivery.md](../GiftCompanion/Docs/phase2_delivery.md) |
| [03_阶段记录/phase2/phase2_plan.md](03_阶段记录/phase2/phase2_plan.md) | [GiftCompanion/Docs/phase2_plan.md](../GiftCompanion/Docs/phase2_plan.md) |
| [03_阶段记录/phase2/phase2_status.md](03_阶段记录/phase2/phase2_status.md) | [GiftCompanion/Docs/phase2_status.md](../GiftCompanion/Docs/phase2_status.md) |
| [03_阶段记录/phase3/phase3_checkpoint.md](03_阶段记录/phase3/phase3_checkpoint.md) | [GiftCompanion/Docs/phase3_checkpoint.md](../GiftCompanion/Docs/phase3_checkpoint.md) |
| [03_阶段记录/phase3/phase3_delivery.md](03_阶段记录/phase3/phase3_delivery.md) | [GiftCompanion/Docs/phase3_delivery.md](../GiftCompanion/Docs/phase3_delivery.md) |
| [03_阶段记录/phase3/phase3_file_plan.md](03_阶段记录/phase3/phase3_file_plan.md) | [GiftCompanion/Docs/phase3_file_plan.md](../GiftCompanion/Docs/phase3_file_plan.md) |
| [03_阶段记录/phase3/phase3_plan.md](03_阶段记录/phase3/phase3_plan.md) | [GiftCompanion/Docs/phase3_plan.md](../GiftCompanion/Docs/phase3_plan.md) |
| [03_阶段记录/phase3/phase3_status.md](03_阶段记录/phase3/phase3_status.md) | [GiftCompanion/Docs/phase3_status.md](../GiftCompanion/Docs/phase3_status.md) |
| [03_阶段记录/phase3/phase3_visual.md](03_阶段记录/phase3/phase3_visual.md) | [GiftCompanion/Docs/phase3_visual.md](../GiftCompanion/Docs/phase3_visual.md) |
| [03_阶段记录/phase3plus/phase3plus_checkpoint.md](03_阶段记录/phase3plus/phase3plus_checkpoint.md) | [GiftCompanion/Docs/phase3plus_checkpoint.md](../GiftCompanion/Docs/phase3plus_checkpoint.md) |
| [03_阶段记录/phase3plus/phase3plus_delivery.md](03_阶段记录/phase3plus/phase3plus_delivery.md) | [GiftCompanion/Docs/phase3plus_delivery.md](../GiftCompanion/Docs/phase3plus_delivery.md) |
| [03_阶段记录/phase3plus/phase3plus_status.md](03_阶段记录/phase3plus/phase3plus_status.md) | [GiftCompanion/Docs/phase3plus_status.md](../GiftCompanion/Docs/phase3plus_status.md) |
| [03_阶段记录/phase4/phase4_checkpoint.md](03_阶段记录/phase4/phase4_checkpoint.md) | [GiftCompanion/Docs/phase4_checkpoint.md](../GiftCompanion/Docs/phase4_checkpoint.md) |
| [03_阶段记录/phase4/phase4_delivery.md](03_阶段记录/phase4/phase4_delivery.md) | [GiftCompanion/Docs/phase4_delivery.md](../GiftCompanion/Docs/phase4_delivery.md) |
| [03_阶段记录/phase4/phase4_plan.md](03_阶段记录/phase4/phase4_plan.md) | [GiftCompanion/Docs/phase4_plan.md](../GiftCompanion/Docs/phase4_plan.md) |
| [03_阶段记录/phase4/phase4_status.md](03_阶段记录/phase4/phase4_status.md) | [GiftCompanion/Docs/phase4_status.md](../GiftCompanion/Docs/phase4_status.md) |
| [03_阶段记录/phase5/phase5_checkpoint.md](03_阶段记录/phase5/phase5_checkpoint.md) | [GiftCompanion/Docs/phase5_checkpoint.md](../GiftCompanion/Docs/phase5_checkpoint.md) |
| [03_阶段记录/phase5/phase5_delivery.md](03_阶段记录/phase5/phase5_delivery.md) | [GiftCompanion/Docs/phase5_delivery.md](../GiftCompanion/Docs/phase5_delivery.md) |
| [03_阶段记录/phase5/phase5_implementation.md](03_阶段记录/phase5/phase5_implementation.md) | [GiftCompanion/Docs/phase5_implementation.md](../GiftCompanion/Docs/phase5_implementation.md) |
| [03_阶段记录/phase5/phase5_plan.md](03_阶段记录/phase5/phase5_plan.md) | [GiftCompanion/Docs/phase5_plan.md](../GiftCompanion/Docs/phase5_plan.md) |
| [03_阶段记录/phase5/phase5_status.md](03_阶段记录/phase5/phase5_status.md) | [GiftCompanion/Docs/phase5_status.md](../GiftCompanion/Docs/phase5_status.md) |
| [03_阶段记录/phase6/phase6_checkpoint.md](03_阶段记录/phase6/phase6_checkpoint.md) | [GiftCompanion/Docs/phase6_checkpoint.md](../GiftCompanion/Docs/phase6_checkpoint.md) |
| [03_阶段记录/phase6/phase6_delivery.md](03_阶段记录/phase6/phase6_delivery.md) | [GiftCompanion/Docs/phase6_delivery.md](../GiftCompanion/Docs/phase6_delivery.md) |
| [03_阶段记录/phase6/phase6_integration.md](03_阶段记录/phase6/phase6_integration.md) | [GiftCompanion/Docs/phase6_integration.md](../GiftCompanion/Docs/phase6_integration.md) |
| [03_阶段记录/phase6/phase6_maintenance.md](03_阶段记录/phase6/phase6_maintenance.md) | [GiftCompanion/Docs/phase6_maintenance.md](../GiftCompanion/Docs/phase6_maintenance.md) |
| [03_阶段记录/phase6/phase6_plan.md](03_阶段记录/phase6/phase6_plan.md) | [GiftCompanion/Docs/phase6_plan.md](../GiftCompanion/Docs/phase6_plan.md) |
| [03_阶段记录/phase6/phase6_player_guide.md](03_阶段记录/phase6/phase6_player_guide.md) | [GiftCompanion/Docs/phase6_player_guide.md](../GiftCompanion/Docs/phase6_player_guide.md) |
| [03_阶段记录/phase6/phase6_requirements.md](03_阶段记录/phase6/phase6_requirements.md) | [GiftCompanion/Docs/phase6_requirements.md](../GiftCompanion/Docs/phase6_requirements.md) |
| [03_阶段记录/phase6/phase6_status.md](03_阶段记录/phase6/phase6_status.md) | [GiftCompanion/Docs/phase6_status.md](../GiftCompanion/Docs/phase6_status.md) |
| [03_阶段记录/V1收尾/v1_closeout_checkpoint.md](03_阶段记录/V1收尾/v1_closeout_checkpoint.md) | [GiftCompanion/Docs/v1_closeout_checkpoint.md](../GiftCompanion/Docs/v1_closeout_checkpoint.md) |
| [03_阶段记录/V1完善/v1_refinement_checkpoint.md](03_阶段记录/V1完善/v1_refinement_checkpoint.md) | [GiftCompanion/Docs/v1_refinement_checkpoint.md](../GiftCompanion/Docs/v1_refinement_checkpoint.md) |
| [03_阶段记录/V1完善/v1_refinement_delivery.md](03_阶段记录/V1完善/v1_refinement_delivery.md) | [GiftCompanion/Docs/v1_refinement_delivery.md](../GiftCompanion/Docs/v1_refinement_delivery.md) |
| [03_阶段记录/V1完善/v1_refinement_plan.md](03_阶段记录/V1完善/v1_refinement_plan.md) | [GiftCompanion/Docs/v1_refinement_plan.md](../GiftCompanion/Docs/v1_refinement_plan.md) |
| [03_阶段记录/V1完善/v1_refinement_player_guide.md](03_阶段记录/V1完善/v1_refinement_player_guide.md) | [GiftCompanion/Docs/v1_refinement_player_guide.md](../GiftCompanion/Docs/v1_refinement_player_guide.md) |
| [03_阶段记录/V1完善/v1_refinement_visual.md](03_阶段记录/V1完善/v1_refinement_visual.md) | [GiftCompanion/Docs/v1_refinement_visual.md](../GiftCompanion/Docs/v1_refinement_visual.md) |
| [03_阶段记录/声音与日记扩展/voice_polish_checkpoint.md](03_阶段记录/声音与日记扩展/voice_polish_checkpoint.md) | [GiftCompanion/Docs/voice_polish_checkpoint.md](../GiftCompanion/Docs/voice_polish_checkpoint.md) |
| [03_阶段记录/声音与日记扩展/voice_polish_delivery.md](03_阶段记录/声音与日记扩展/voice_polish_delivery.md) | [GiftCompanion/Docs/voice_polish_delivery.md](../GiftCompanion/Docs/voice_polish_delivery.md) |
| [04_验收记录/Phase0/player_build.json](04_验收记录/Phase0/player_build.json) | [GiftCompanion/Docs/Validation/Phase0/player_build.json](../GiftCompanion/Docs/Validation/Phase0/player_build.json) |
| [04_验收记录/Phase1/acceptance.json](04_验收记录/Phase1/acceptance.json) | [GiftCompanion/Docs/Validation/Phase1/acceptance.json](../GiftCompanion/Docs/Validation/Phase1/acceptance.json) |
| [04_验收记录/Phase1/player_build.json](04_验收记录/Phase1/player_build.json) | [GiftCompanion/Docs/Validation/Phase1/player_build.json](../GiftCompanion/Docs/Validation/Phase1/player_build.json) |
| [04_验收记录/Phase2/acceptance.json](04_验收记录/Phase2/acceptance.json) | [GiftCompanion/Docs/Validation/Phase2/acceptance.json](../GiftCompanion/Docs/Validation/Phase2/acceptance.json) |
| [04_验收记录/Phase2/player_build.json](04_验收记录/Phase2/player_build.json) | [GiftCompanion/Docs/Validation/Phase2/player_build.json](../GiftCompanion/Docs/Validation/Phase2/player_build.json) |
| [04_验收记录/Phase3/acceptance.json](04_验收记录/Phase3/acceptance.json) | [GiftCompanion/Docs/Validation/Phase3/acceptance.json](../GiftCompanion/Docs/Validation/Phase3/acceptance.json) |
| [04_验收记录/Phase3/player_build.json](04_验收记录/Phase3/player_build.json) | [GiftCompanion/Docs/Validation/Phase3/player_build.json](../GiftCompanion/Docs/Validation/Phase3/player_build.json) |
| [04_验收记录/Phase3Plus/acceptance.json](04_验收记录/Phase3Plus/acceptance.json) | [GiftCompanion/Docs/Validation/Phase3Plus/acceptance.json](../GiftCompanion/Docs/Validation/Phase3Plus/acceptance.json) |
| [04_验收记录/Phase3Plus/player_build.json](04_验收记录/Phase3Plus/player_build.json) | [GiftCompanion/Docs/Validation/Phase3Plus/player_build.json](../GiftCompanion/Docs/Validation/Phase3Plus/player_build.json) |
| [04_验收记录/Phase4/acceptance.json](04_验收记录/Phase4/acceptance.json) | [GiftCompanion/Docs/Validation/Phase4/acceptance.json](../GiftCompanion/Docs/Validation/Phase4/acceptance.json) |
| [04_验收记录/Phase4/player_build.json](04_验收记录/Phase4/player_build.json) | [GiftCompanion/Docs/Validation/Phase4/player_build.json](../GiftCompanion/Docs/Validation/Phase4/player_build.json) |
| [04_验收记录/Bilingual/acceptance.json](04_验收记录/Bilingual/acceptance.json) | [GiftCompanion/Docs/Validation/Bilingual/acceptance.json](../GiftCompanion/Docs/Validation/Bilingual/acceptance.json) |
| [04_验收记录/Bilingual/player_build.json](04_验收记录/Bilingual/player_build.json) | [GiftCompanion/Docs/Validation/Bilingual/player_build.json](../GiftCompanion/Docs/Validation/Bilingual/player_build.json) |
| [04_验收记录/VoicePolish/acceptance.json](04_验收记录/VoicePolish/acceptance.json) | [GiftCompanion/Docs/Validation/VoicePolish/acceptance.json](../GiftCompanion/Docs/Validation/VoicePolish/acceptance.json) |
| [04_验收记录/VoicePolish/player_build.json](04_验收记录/VoicePolish/player_build.json) | [GiftCompanion/Docs/Validation/VoicePolish/player_build.json](../GiftCompanion/Docs/Validation/VoicePolish/player_build.json) |
| [04_验收记录/ExpressionDiary/acceptance.json](04_验收记录/ExpressionDiary/acceptance.json) | [GiftCompanion/Docs/Validation/ExpressionDiary/acceptance.json](../GiftCompanion/Docs/Validation/ExpressionDiary/acceptance.json) |
| [04_验收记录/ExpressionDiary/player_build.json](04_验收记录/ExpressionDiary/player_build.json) | [GiftCompanion/Docs/Validation/ExpressionDiary/player_build.json](../GiftCompanion/Docs/Validation/ExpressionDiary/player_build.json) |
| [04_验收记录/Phase5/acceptance.json](04_验收记录/Phase5/acceptance.json) | [GiftCompanion/Docs/Validation/Phase5/acceptance.json](../GiftCompanion/Docs/Validation/Phase5/acceptance.json) |
| [04_验收记录/Phase5/player_build.json](04_验收记录/Phase5/player_build.json) | [GiftCompanion/Docs/Validation/Phase5/player_build.json](../GiftCompanion/Docs/Validation/Phase5/player_build.json) |
| [04_验收记录/Phase5/frozen_verification.json](04_验收记录/Phase5/frozen_verification.json) | [GiftCompanion/Docs/Validation/Phase5/frozen_verification.json](../GiftCompanion/Docs/Validation/Phase5/frozen_verification.json) |
| [04_验收记录/Phase6/acceptance.json](04_验收记录/Phase6/acceptance.json) | [GiftCompanion/Docs/Validation/Phase6/acceptance.json](../GiftCompanion/Docs/Validation/Phase6/acceptance.json) |
| [04_验收记录/Phase6/player_build.json](04_验收记录/Phase6/player_build.json) | [GiftCompanion/Docs/Validation/Phase6/player_build.json](../GiftCompanion/Docs/Validation/Phase6/player_build.json) |
| [04_验收记录/Phase6/package_summary.json](04_验收记录/Phase6/package_summary.json) | [GiftCompanion/Docs/Validation/Phase6/package_summary.json](../GiftCompanion/Docs/Validation/Phase6/package_summary.json) |
| [04_验收记录/Phase6/frozen_verification.json](04_验收记录/Phase6/frozen_verification.json) | [GiftCompanion/Docs/Validation/Phase6/frozen_verification.json](../GiftCompanion/Docs/Validation/Phase6/frozen_verification.json) |
| [04_验收记录/Phase6/install_write_verification.json](04_验收记录/Phase6/install_write_verification.json) | [GiftCompanion/Docs/Validation/Phase6/install_write_verification.json](../GiftCompanion/Docs/Validation/Phase6/install_write_verification.json) |
| [04_验收记录/V1Refinement/acceptance.json](04_验收记录/V1Refinement/acceptance.json) | [GiftCompanion/Docs/Validation/V1Refinement/acceptance.json](../GiftCompanion/Docs/Validation/V1Refinement/acceptance.json) |
| [04_验收记录/V1Refinement/player_build.json](04_验收记录/V1Refinement/player_build.json) | [GiftCompanion/Docs/Validation/V1Refinement/player_build.json](../GiftCompanion/Docs/Validation/V1Refinement/player_build.json) |
| [04_验收记录/V1Refinement/package_summary.json](04_验收记录/V1Refinement/package_summary.json) | [GiftCompanion/Docs/Validation/V1Refinement/package_summary.json](../GiftCompanion/Docs/Validation/V1Refinement/package_summary.json) |
| [04_验收记录/V1Refinement/frozen_verification.json](04_验收记录/V1Refinement/frozen_verification.json) | [GiftCompanion/Docs/Validation/V1Refinement/frozen_verification.json](../GiftCompanion/Docs/Validation/V1Refinement/frozen_verification.json) |
| [04_验收记录/V1Refinement/install_write_verification.json](04_验收记录/V1Refinement/install_write_verification.json) | [GiftCompanion/Docs/Validation/V1Refinement/install_write_verification.json](../GiftCompanion/Docs/Validation/V1Refinement/install_write_verification.json) |
| [04_验收记录/V1Refinement/previous_release_verification.json](04_验收记录/V1Refinement/previous_release_verification.json) | [GiftCompanion/Docs/Validation/V1Refinement/previous_release_verification.json](../GiftCompanion/Docs/Validation/V1Refinement/previous_release_verification.json) |
| [04_验收记录/V1Closeout/acceptance.json](04_验收记录/V1Closeout/acceptance.json) | [GiftCompanion/Docs/Validation/V1Closeout/acceptance.json](../GiftCompanion/Docs/Validation/V1Closeout/acceptance.json) |
| [04_验收记录/V1Closeout/player_build.json](04_验收记录/V1Closeout/player_build.json) | [GiftCompanion/Docs/Validation/V1Closeout/player_build.json](../GiftCompanion/Docs/Validation/V1Closeout/player_build.json) |
| [04_验收记录/V1Closeout/package.json](04_验收记录/V1Closeout/package.json) | [GiftCompanion/Docs/Validation/V1Closeout/package.json](../GiftCompanion/Docs/Validation/V1Closeout/package.json) |
| [04_验收记录/V1Closeout/Entry.EditMode.xml](04_验收记录/V1Closeout/Entry.EditMode.xml) | [GiftCompanion/Docs/Validation/V1Closeout/Entry.EditMode.xml](../GiftCompanion/Docs/Validation/V1Closeout/Entry.EditMode.xml) |
| [04_验收记录/V1Closeout/Entry.PlayMode.xml](04_验收记录/V1Closeout/Entry.PlayMode.xml) | [GiftCompanion/Docs/Validation/V1Closeout/Entry.PlayMode.xml](../GiftCompanion/Docs/Validation/V1Closeout/Entry.PlayMode.xml) |
| [04_验收记录/V1Closeout/Native/native.json](04_验收记录/V1Closeout/Native/native.json) | [GiftCompanion/Docs/Validation/V1Closeout/Native/native.json](../GiftCompanion/Docs/Validation/V1Closeout/Native/native.json) |
| [04_验收记录/V1Closeout/Native/direct_companion.png](04_验收记录/V1Closeout/Native/direct_companion.png) | [GiftCompanion/Docs/Validation/V1Closeout/Native/direct_companion.png](../GiftCompanion/Docs/Validation/V1Closeout/Native/direct_companion.png) |
| [04_验收记录/V1Closeout/Native/home_without_pet.png](04_验收记录/V1Closeout/Native/home_without_pet.png) | [GiftCompanion/Docs/Validation/V1Closeout/Native/home_without_pet.png](../GiftCompanion/Docs/Validation/V1Closeout/Native/home_without_pet.png) |
| [04_验收记录/V1Refinement/Final2.EditMode.xml](04_验收记录/V1Refinement/Final2.EditMode.xml) | [GiftCompanion/Docs/Validation/V1Refinement/Final2.EditMode.xml](../GiftCompanion/Docs/Validation/V1Refinement/Final2.EditMode.xml) |
| [04_验收记录/V1Refinement/Final2.PlayMode.xml](04_验收记录/V1Refinement/Final2.PlayMode.xml) | [GiftCompanion/Docs/Validation/V1Refinement/Final2.PlayMode.xml](../GiftCompanion/Docs/Validation/V1Refinement/Final2.PlayMode.xml) |
| [04_验收记录/V1Refinement/VisualAfter/Refinement.json](04_验收记录/V1Refinement/VisualAfter/Refinement.json) | [GiftCompanion/Docs/Validation/V1Refinement/VisualAfter/Refinement.json](../GiftCompanion/Docs/Validation/V1Refinement/VisualAfter/Refinement.json) |
| [04_验收记录/V1Refinement/VisualAfter/run.json](04_验收记录/V1Refinement/VisualAfter/run.json) | [GiftCompanion/Docs/Validation/V1Refinement/VisualAfter/run.json](../GiftCompanion/Docs/Validation/V1Refinement/VisualAfter/run.json) |
| [04_验收记录/V1Refinement/VisualAfter/settings.png](04_验收记录/V1Refinement/VisualAfter/settings.png) | [GiftCompanion/Docs/Validation/V1Refinement/VisualAfter/settings.png](../GiftCompanion/Docs/Validation/V1Refinement/VisualAfter/settings.png) |
| [04_验收记录/V1Refinement/VisualAfter/display.png](04_验收记录/V1Refinement/VisualAfter/display.png) | [GiftCompanion/Docs/Validation/V1Refinement/VisualAfter/display.png](../GiftCompanion/Docs/Validation/V1Refinement/VisualAfter/display.png) |
| [04_验收记录/V1Refinement/Standalone/Offline.json](04_验收记录/V1Refinement/Standalone/Offline.json) | [GiftCompanion/Docs/Validation/V1Refinement/Standalone/Offline.json](../GiftCompanion/Docs/Validation/V1Refinement/Standalone/Offline.json) |
| [04_验收记录/V1Refinement/Standalone/Restored.json](04_验收记录/V1Refinement/Standalone/Restored.json) | [GiftCompanion/Docs/Validation/V1Refinement/Standalone/Restored.json](../GiftCompanion/Docs/Validation/V1Refinement/Standalone/Restored.json) |
| [04_验收记录/V1Refinement/Voice/Voice.json](04_验收记录/V1Refinement/Voice/Voice.json) | [GiftCompanion/Docs/Validation/V1Refinement/Voice/Voice.json](../GiftCompanion/Docs/Validation/V1Refinement/Voice/Voice.json) |
| [01_当前交付/发布文件清单.json](01_当前交付/发布文件清单.json) | [GiftCompanion/Releases/Gift-V1-Closeout/Release/release_manifest.json](../GiftCompanion/Releases/Gift-V1-Closeout/Release/release_manifest.json) |
