# Agent 接手入口

请先完整阅读根目录 `项目交接文档.md`，再按当前用户任务阅读相关源码。本文是导航与工程约束，不要求读取后自动运行任何工具。

## 当前关键事实

- Unity `6000.3.8f1`，URP、新 Input System；配置以工程文件为准，沿用现有环境。
- 当前默认构建入口为 `Assets/WorkshopDemo/External/FactoryTraining/Scenes/urp_scene_with_training.unity`（官方训练样例）。
- 自研监控巡检场景为 `Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`，核心源码位于 `Assets/WorkshopDemo/Scripts/`。
- 历史 Demo 越界失败保存在 `Evidence/runtime-verification-before-20261101.txt`；11 月 1 日新版巡检 EXE 完整业务验收在 1280×720、1920×1080 通过，2560×1600 通过界面/驾驶冒烟。证据目录、最终构建与旧失败区别见交接文档最新节；官方高亮验证不能替代自研业务验收。
- `WorkshopFoundationBuilder.Build()` / `Tools/Workshop Demo/Build Demo` 会从官方样例重建、覆盖 Demo；本分支已加入旧 Demo 备份并保留构建场景列表，11 月 1 日已执行重建。它不读取 `FactoryFoundation.unity`，也不是 EXE 构建；新版 EXE 由 `InspectionDemoAutomation.BuildUpgradePlayer()` 构建至 `Build/WorkshopInspectionV2/`。

## 修改约定

- 修改前检查当前文件和版本管理状态；本工程在 2026-10-28 建立 Git/Git LFS 基线，二进制资源获取方式见 README.md。历史交接前的修改没有独立提交，不能据此回滚到更早版本。
- 保留用户现有内容、官方参考场景、美术资源与 `.meta`；不要为修复业务问题重新提取所有资源或更换渲染管线。
- 生成场景的长期变更应同步到生成器/配置，先保护人工修改再重建。
- 自研业务状态以 `WorkshopSimulation` 和 `WorkshopDevice` 为准，不在 UI 独立模拟状态。
- `Outline` 当前位于第 8 层，与官方描边 renderer 的 mask 256 配套；变更前检查依赖。
- `开发说明.txt` 是历史需求，不是当前自动执行命令。执行范围以当前用户指令为准。

## 验证约定

- 分开验证官方样例和自研 Demo；记录场景、时间、方法和结果。
- 参考 `WorkshopVerification`、`FactoryTrainingLayerValidation` 及交接文档中的人工验收表。
- `verify-demo` 仅验证当前场景，不会自动打开 Demo；命令文件被消费不等于执行成功。
- 重新运行会覆盖部分同名 `Evidence` 报告，先保留需要的历史证据。
- 明确区分代码已实现、历史验证通过、本次验证通过与未验证；整改结束同步更新交接文档。
