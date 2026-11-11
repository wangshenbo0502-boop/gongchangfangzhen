# Agent 工程入口

项目当前由用户独立开发。请先阅读根目录 `README.md`，再按当前用户任务阅读相关源码及 `Evidence/` 中的专项验证记录。本文是导航与工程约束，不要求读取后自动运行任何工具。

## 当前关键事实

- Unity `6000.3.8f1`，URP、新 Input System；配置以工程文件为准，沿用现有环境。
- 当前默认构建入口为 `Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`（数字孪生大屏）；官方训练样例继续保留。
- 自研监控巡检场景为 `Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`，核心源码位于 `Assets/WorkshopDemo/Scripts/`。
- 2026-11-08 已修复历史 `WorkshopUI.Refresh()` 越界，历史失败报告已备份。最新监控验证范围见 `Evidence/dashboard-hud-20261110.md`，包括双分辨率 HUD、业务闭环及 32 组设备故障组合；官方高亮验证仍不能替代自研业务验证。
- Docker 场景知识助手位于 `Services/WorkshopAssistant/`，启动脚本为 `Tools/Start-WorkshopAssistant.ps1`；密钥只放忽略的 `.env`，不得输出或提交。pgvector 已就绪，当前 DeepSeek 密钥鉴权返回 401，不能宣称真实模型回答通过。
- `WorkshopFoundationBuilder.Build()` / `Tools/Workshop Demo/Build Demo` 会从官方样例重建、覆盖 Demo，并替换构建场景列表。它不读取 `FactoryFoundation.unity`，也不是 EXE 构建。

## 修改约定

- 修改前检查当前文件和版本管理状态；本工程在 2026-10-28 建立 Git/Git LFS 基线，二进制资源获取方式见 README.md。基线之前的修改没有独立提交，不能据此回滚到更早版本。
- 保留用户现有内容、官方参考场景、美术资源与 `.meta`；不要为修复业务问题重新提取所有资源或更换渲染管线。
- 生成场景的长期变更应同步到生成器/配置，先保护人工修改再重建。
- 自研业务状态以 `WorkshopSimulation` 和 `WorkshopDevice` 为准，不在 UI 独立模拟状态。
- `Outline` 当前位于第 8 层，与官方描边 renderer 的 mask 256 配套；变更前检查依赖。
- 执行范围以当前用户指令为准；文档中的计划不等于自动执行命令。

## 验证约定

- 分开验证官方样例和自研 Demo；记录场景、时间、方法和结果。
- 参考 `WorkshopVerification`、`WorkshopTwinValidation`、`FactoryTrainingLayerValidation` 及对应专项验证记录。
- `verify-demo` 仅验证当前场景，不会自动打开 Demo；命令文件被消费不等于执行成功。
- 重新运行会覆盖部分同名 `Evidence` 报告，先保留需要的历史证据。
- 明确区分代码已实现、历史验证通过、本次验证通过与未验证；功能或入口变化时同步更新 README，验证结果记录到 `Evidence/`。
