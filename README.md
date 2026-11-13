# 工业车间设备三维可视化监控与交互巡检系统

基于 Unity 的工业车间监控与运维教学项目，包含 **三维设备监控巡检** 和 **小车装配训练/使用演示** 两套场景。前者展示设备、故障、告警和巡检闭环，后者展示现有工业机器人小车的分部件装配与运动。

**接手先看：** 本工程默认打开数字孪生监控场景；要查看小车部件和装配交互，请打开官方训练场景。现有小车是后续四类设备建模的外观与拆装交互参考。全部监控指标为本地模拟数据，故障示意不等于真实工业诊断或 Abaqus 求解。

文档核对日期：**2026-11-13**。统一平台采用黑灰黄主题，主导航收敛为“车间总览、设备学习、实训任务”。AI 助手和更多位于左栏，知识导入位于“更多 → 资料管理”，小车装配位于“实训任务”。设备观察、拆装、故障演示按模式显示工具；有限元分析默认收起专业工具。新建故障后直接进入对应工单，设备名称自动带入，维修只填写故障原因。系统内拆装按视口像素提高渲染分辨率。验收范围见 [本次验证](Evidence/workflow-chat-20261113.md)。仓库中共五个模型：小车，以及泵、罐、风机、控制柜四台受监控设备。储罐、水泵、控制柜沿仓库右侧设备区排齐，风机位于侧边，操作面朝向巡检通道，避开白色网格区和绿色人行道。已移除小地图，保留自动巡检与方向键手动驾驶；两场景统一使用 `H` 隐藏/恢复 UI、`M` 静音/恢复声音。功能来源与边界见 [整合清单](Evidence/version-integration-20261111.md)，布局历史验收见 [统一平台与布局验证](Evidence/platform-layout-20261111.md)，风机本次接入见 [风机平台验证](Evidence/fan-platform-20261112.md)。

## 快速启动与版本区分

| 目的 | 入口 | 说明 |
| --- | --- | --- |
| 运行统一平台 | [WorkshopPlatform.exe](Build/WorkshopPlatform/WorkshopPlatform.exe) | 日常统一入口；包含监控平台与原小车装配训练 |
| 打开设备独立展示器 | 设备学习 → 选择设备 → 详细展示 | 水泵、储罐、风机、控制柜各自打开配套 Unity 窗口 |
| 运行本工程监控大屏 | [WorkshopDemo.unity](Assets/WorkshopDemo/Scenes/WorkshopDemo.unity) | 当前默认构建场景；也可用 `Tools/Workshop Demo/Open Digital Twin` 打开 |
| 查看小车装配和使用 | [urp_scene_with_training.unity](Assets/WorkshopDemo/External/FactoryTraining/Scenes/urp_scene_with_training.unity) | 官方样例保留；统一程序内通过“小车装配训练”进入并返回 |
| 查看环境工作副本 | `Assets/WorkshopDemo/Scenes/FactoryFoundation.unity` | 环境工作副本，不是监控入口 |
| 查看此前合并版程序 | `D:\UnityProjects\WorkshopInspectionDemo\Build\WorkshopCombined\WorkshopSystem.exe` | 属于另一个开发副本；按 `1` 进入装配，按 `2` 进入巡检，`H` 隐藏/恢复界面 |

当前本机正式版已接入新储罐 TK-01。它在车间内按 60% 等比显示，保留原工位及巡检点，在设备学习页提供 12 总成拆装和独立展示入口。正式版已验证储罐拆装、漏损演示和巡检到场。[储罐接入验证](Evidence/tank-platform-20261113.md)

`Build/` 中保留日常使用的 `WorkshopPlatform/` 和历史发布包 `Releases/`。本轮回收了旧构建、独立展示工程的 Unity 缓存及可重建的导入包。重新打开独立展示工程时，Unity 会重建对应缓存。[清理清单](Evidence/project-cleanup-20261113.json)

本 README 描述当前目录 `D:\UnityProjects\工业车间设备三维可视化监控与交互巡检系统`。旧开发副本中的设备观察器、渐进裂口和故障参考分析已按源码迁移到统一平台；当前结果以本工程的新验证报告为准。2026-11-11 经用户确认，四个旧车间构建已移入回收站；受保护的 `WorkshopCombined` 副本保持原样，当前统一入口为 `Build/WorkshopPlatform/WorkshopPlatform.exe`。清单见 [旧构建清理记录](Evidence/version-cleanup-20261111.md)。

1. 用 Unity Hub 添加当前工程，使用 **Unity 6000.3.8f1** 打开，等待导入和编译完成。
2. 选择上表中的目标场景，点击 Play。日常运行无需重建场景、安装 Blender 或启动 Abaqus。
3. 需要知识问答时，再运行下列脚本启动 Docker 服务；监控和巡检可独立演示。

```powershell
.\Tools\Start-WorkshopAssistant.ps1
```

知识检索与真实在线问答已于 2026-11-12 通过本机服务验证；历史 DeepSeek **401** 鉴权失败记录保留。配置见 [知识助手说明](Services/WorkshopAssistant/README.md)，密钥仅放在被忽略的 `.env`，不要提交。

2026-11-12 已补入四类设备故障速查与工单答疑，知识库实测为 6 份资料、40 个片段；服务、数据库与中文检索通过。用户更新密钥后，重新创建助手容器以加载新配置，真实 `/api/chat` 返回 HTTP 200，回答附带知识引用。修改 `.env` 后需重新创建容器，仅保存文件不会更新运行中的环境变量。启动与验证范围见 [本次记录](Evidence/assistant-startup-20261112.md) 和 [密钥更新验证](Evidence/assistant-key-reload-20261112.md)；本次成功请求未携带 Unity 场景快照。

通过 Git 获取资源时先执行 `git lfs install`、`git lfs pull` 和 `git lfs fsck`。迁移时保留 `Assets` 及全部 `.meta`、`Packages`、`ProjectSettings`；资源缺失应先检查 LFS，不要重新提取全部美术资源。

Git 仓库保存源码、资源和验证记录；Windows 统一程序以仓库 Release 附件发布。`Build/`、本机缓存、建模过程快照和密钥不提交，下载程序后应保留解压目录内全部配套文件。

GitHub Releases 的 2026-11-11 下载包对应源码提交 `e970332`。完整解压 `WorkshopPlatform-Windows-x64-20261111.zip` 后运行 `WorkshopPlatform/WorkshopPlatform.exe`。后续模型和界面更新以仓库源码及本机正式版的验证记录为准。[历史 Windows 下载](https://github.com/wangshenbo0502-boop/gongchangfangzhen/releases/tag/workshop-platform-20261111) · [发布记录](Evidence/version-cleanup-20261111.md)

## 小车模型与交互

### 模型来源与用途

这里的“小车”指 **Factory Training – Industry Sample** 中的工业机器人，资源名称使用 `moto` / `moto_bot`，并非跑车。整机、装配部件、材质和装配逻辑来自已有样例，中文界面及巡检业务在此基础上定制。

它在项目中有两种用途：

- **训练场景：** 通过独立部件、定位提示、拖装与自动演示学习装配；“使用”页演示小车运动和机械臂滑动。
- **监控场景：** 复用完整小车的外观，由 `WorkshopInspector` 控制它前往故障设备，展示路径、到达和任务完成。

资源来源及许可记录见 [SOURCES.md](Assets/WorkshopDemo/External/SOURCES.md)。小车资源记录为 **Unity Companion License**，不属于目录中其他 CC0 工业素材。

### 从哪里找到模型

以下资源均位于 `Assets/WorkshopDemo/External/FactoryTraining/`：

| 相对路径 | 用途 |
| --- | --- |
| [Prefabs/moto_bot/moto_prefab.prefab](Assets/WorkshopDemo/External/FactoryTraining/Prefabs/moto_bot/moto_prefab.prefab) | 完整小车；监控场景生成器实例化的资源 |
| [Prefabs/moto_bot/moto_bot_forassembly.prefab](Assets/WorkshopDemo/External/FactoryTraining/Prefabs/moto_bot/moto_bot_forassembly.prefab) | 装配版小车 |
| [Prefabs/moto_part/](Assets/WorkshopDemo/External/FactoryTraining/Prefabs/moto_part/) | 16 个独立部件预制体 |
| [Prefabs/moto_training_parts/](Assets/WorkshopDemo/External/FactoryTraining/Prefabs/moto_training_parts/) | 对应的 16 个训练部件预制体 |
| [Fbx/moto_bot.fbx](Assets/WorkshopDemo/External/FactoryTraining/Fbx/moto_bot.fbx) | 完整模型网格源资源 |
| [Fbx/moto_bot_forassembly.fbx](Assets/WorkshopDemo/External/FactoryTraining/Fbx/moto_bot_forassembly.fbx) | 装配版网格源资源 |
| [Materials/moto/](Assets/WorkshopDemo/External/FactoryTraining/Materials/moto/) | 金属、镀铬、塑料、橡胶、灯光及反光件材质 |
| [Textures/moto/](Assets/WorkshopDemo/External/FactoryTraining/Textures/moto/) | Base Color、Normal、Roughness、Metallic 等贴图 |
| [Animations/](Assets/WorkshopDemo/External/FactoryTraining/Animations/) | 装配示意 Animator、步骤动画和 UI 动画 |

查看整机外观可先打开完整 prefab；查看实际拖装效果应进入训练场景，单独打开 FBX 不会运行训练逻辑。

### 部件组成

下表按现有资源名分组，中文仅作查找说明，精确位置以 prefab 层级和场景绑定为准：

| 分组 | 部件 prefab 名称 |
| --- | --- |
| 车体与支撑 | `body_main`、`body_front`、`chassis_support`、`frame_cover` |
| 机械臂底座与桥架 | `arm_chassis`、`bridge` |
| 左右滑动组件 | `gliders_left`、`gliders_right`、`shelf_Gliders_GRP_left`、`shelf_Gliders_GRP_right` |
| 轴系 | `inner_shaft`、`outer_shaft` |
| 驱动与夹持件 | `motor`、`claw` |
| 线缆与线缆护罩 | `cables`、`cable_housing` |

**16 个部件预制体不等于 16 道训练工序。** 现有训练统计界面按 15 项工序展示，实际步骤由状态管理器的 `_stateData` 配置决定，不能仅按文件数推算。

### 装配、观察和使用

训练场景顶部提供“组装、使用、远景、近景”入口。“组装”包含引导训练和独立考核；部件高亮、目标虚影、步骤提示、计时与完成统计共同构成训练流程。

1. 当前步骤指定可操作部件、拖动参考平面和正确装配位置。
2. 鼠标选中并拖动部件；拖动受步骤配置约束，并非任意空间自由移动。
3. 松开后检查与目标位置的距离：正确时吸附归位并推进步骤，错误时播放返回动画。
4. 步骤还提供自动演示/完成入口；重置恢复训练。装配示意由步骤状态和 Animator 配合，不能理解为任意模型都能自动生成拆装。

“使用”页通过滑条移动指定机械臂对象；官方 `AgentMovement` 提供键盘移动，按对象的 `useArrowKeys` 配置使用方向键或 WASD，`RandomAgentMovement` 负责自动游走，`wheelRotation` 提供轮子视觉旋转。滑条和轮子属于 Transform 动画，不是关节载荷、轮胎接触或整车动力学求解。

训练界面支持 `M` 静音/恢复声音、`H` 隐藏/恢复界面。步骤提示可点击“继续”收起，也会约 2.4 秒后开始淡出，淡出约 0.2 秒。详细历史交互验证边界见后文，不能据此宣称完整 15 步本次重新验收通过。

### 小车代码导航

官方逻辑在 `Assets/WorkshopDemo/External/FactoryTraining/Scripts/`，下表路径相对此目录：

| 脚本 | 负责什么 |
| --- | --- |
| `ImmersiveTraining/Management/StateManager_Training.cs` | 步骤顺序、当前部件、推进/完成/重置 |
| `ImmersiveTraining/StateHandling/StateTrainingWithPass.cs` | 部件、目标虚影、拖动平面、目标位置、吸附判定和步骤示意 |
| `ImmersiveTraining/TrainingInteractions/TrainingPartMover.cs` | 射线选取、拖动、释放检查、错误回位和自动演示 |
| `ImmersiveTraining/TrainingInteractions/HighlightManager.cs` | 部件高亮 |
| `ImmersiveTraining/Management/TrainingTypeManager.cs` | 引导训练与独立考核切换 |
| `ImmersiveTraining/Management/ModalPanel.cs`、`TimeManager.cs` | 步骤提示、计时和统计 |
| `DistributionCenter/AgentMovement.cs`、`RandomAgentMovement.cs` | 官方场景手动移动与自动游走 |
| `DistributionCenter/GOTranslator.cs` | 滑条到机械臂对象位移的映射 |
| `DistributionCenter/wheelRotation.cs` | 轮子视觉动画 |
| `DistributionCenter/CameraSwitcher.cs`、`CamInput.cs` | 官方场景视角与输入 |

自定义训练界面在 [FactoryTrainingUI.cs](Assets/WorkshopDemo/Scripts/FactoryTrainingUI.cs)，编辑器配置在 [FactoryTrainingUIEditor.cs](Assets/WorkshopDemo/Editor/FactoryTrainingUIEditor.cs)。

### 在巡检系统中的接入

[WorkshopFoundationBuilder.cs](Assets/WorkshopDemo/Editor/WorkshopFoundationBuilder.cs) 的 `Inspector(...)` 实例化完整小车并命名为 `Inspection robot`，关闭继承的 `MonoBehaviour` 和音频，再配置 `NavMeshAgent`、路线 `LineRenderer` 和 [WorkshopInspector.cs](Assets/WorkshopDemo/Scripts/WorkshopInspector.cs)。

当前工程由 `WorkshopInspector` 检查完整可达路径、移动、更新路线、朝向目标和报告到达；`WorkshopSimulation` 决定能否派发与处理。统一平台已接入手动接管、方向键驾驶和轮子转动；手动离开维修点会撤销到达许可，恢复自动后继续原任务。不要同时启用官方游走和巡检导航，以免两个控制器争用位置。

维护模型时保留原部件层级、枢轴、装配目标、碰撞体、材质槽、UV 和 `.meta`。改网格后需要同时检查拖装、虚影、动画和巡检导航尺寸。`Outline` 使用第 8 层，与描边 renderer 的 mask `256` 配套。

### 后续四类设备的建模状态

控制柜最新 Blender 评审源文件为 [electrical-cabinet-duct-entry-review.blend](model/source/electrical-cabinet-duct-entry-review.blend)，运行 `.\model\scripts\Open-CabinetReview.ps1` 打开。2026-11-12 继续修正电源、PLC 和变频器附近的敞开线槽、露出线头及并线问题，补齐四处槽盖、重排电源独立槽口和 PLC 引线，并按电源端子标识修正线色；正式近景查出的电源出线穿壳问题也已修正。保留前轮现场出线槽、器件、材质和 16 总成。重开检查为 109,824 三角面、G1–G12 全通过；183 条路径的本轮穿插检查通过，检查扩展至电源壳体，172 个槽内末端增加正面与斜向遮挡检查。四张 Blender 效果图及检查边界见 [入槽细节修订记录](Evidence/cabinet-duct-entry-20261112.md)。

计划保留小车，重做离心泵、立式储罐、轴流风机和控制柜；要求结构、材质与拆装体验向现有小车看齐。原规范按八个主要组件设计；离心泵按用户后续要求改为 **12 个总成**，Blender 与 Unity 使用同一分组和拆装配置，不把每颗螺栓单列成步骤。要求见 [建模目标与验收标准](model/建模目标与验收标准.md)。

**保护要求：`Build/WorkshopPlatform/Viewers/WaterPumpViewer/` 是必须保留的独立水泵展示器交付物。后续优化、平台重建、清理构建目录或整理文件时，严禁删除、覆盖或移动该目录及其中配套文件；如需更新，先备份并确认新版本可运行，再原位替换。** 它不是可随时重建的缓存。

**离心泵、轴流风机和控制柜已接入统一平台，并保留各自独立展示器。** 控制柜也可通过 [CabinetViewer.exe](model/CabinetViewer/CabinetViewer.exe) 独立运行。模型信息见 [控制柜报告](model/reports/electrical-cabinet.md) 和 [源模型检查](Evidence/cabinet-modeling-20261111.md)。平台中选择循环水泵后可打开详细展示；双击 [WaterPumpViewer.exe](model/WaterPumpViewer/WaterPumpViewer.exe) 也可单独查看整机、12 总成拆装、内部结构、旋转和缩放，无需安装 Unity。平台内受保护副本位于 `Build/WorkshopPlatform/Viewers/WaterPumpViewer/`，不得因后续优化而删除。源文件为 [water-pump.blend](model/source/water-pump.blend)，含整机/拆开/内部三个 Scene，以及 1–320 帧拆开再组装动画。运行 `.\model\scripts\Open-PumpReview.ps1` 可在 Blender 打开观察侧栏。

2026-11-13 审看通过的 16 总成控制柜已接入统一平台的 PLC-01。选择 PLC-01 后进入“查看设备 → 拆装”，或在“视图”里选“查看柜内”“在独立展示器中打开”。拆解时柜门总成先直线退出 160 mm，再转向停放位置，回装时逆序归位。线束断开与接回以淡出、淡入演示。源模型为 [electrical-cabinet-viewer-fit-review.blend](model/source/electrical-cabinet-viewer-fit-review.blend)，资源可由 Unity 菜单 `Tools/Workshop Demo/Import Reviewed Cabinet` 重新导入。吊耳、门缝和材质修订见 [独立展示器验证](Evidence/cabinet-viewer-fit-20261113.md)，平台运行范围见 [控制柜接入验证](Evidence/cabinet-platform-20261113.md)。

轴流风机按用户允许的上限 16 组采用 **11 个总成**，2026-11-12 修订后为 590 个网格、98,140 个三角面；源文件为 [industrial-fan.blend](model/source/industrial-fan.blend)，四张整机角度图及接线盒近景位于 [风机预览目录](model/previews/industrial-fan/)。已移除外露电线和接口，增加机器编号、序列号、制造年月及条码；接线盒采用贴合筒壁的曲面鞍垫。运行 `.\model\scripts\Open-FanReview.ps1` 可打开 Blender 观察窗口。经用户同意，精细风机已替换统一平台的 F-01，保留原设备工位、启停、告警及巡检业务；“整机拆装”使用同一 11 总成和 motionVersion 4 路径，“详细展示”打开平台配套的 `Build/WorkshopPlatform/Viewers/IndustrialFanViewer/IndustrialFanViewer.exe`。独立源交付仍保留在 [IndustrialFanViewer.exe](model/IndustrialFanViewer/IndustrialFanViewer.exe)，须保留同目录配套文件。路径读取随 FBX 导入的坐标参照：盖板先外退、避开盒体、再抬高平移，盒盖先外退再错开，组装原路返回。独立展示器历史 1201 位置表面抽样未发现新增运动相交，保留原有静态连接及初始密封垫座合接触；该检查不等于制造级干涉认证。独立展示器保留 2 倍渲染、4× MSAA 与轻度锐化，历史运动平均约 59–60 FPS，仍有少量慢帧，此性能数字不代表主平台本轮测量。独立展示器中间帧见 `model/reports/industrial-fan-axis-fixed-final-1080-20261112/`；平台本轮运行证据、截图和验证边界见 [风机平台验证](Evidence/fan-platform-20261112.md)。独立构建脚本为 `.\model\scripts\Build-FanViewerReview.ps1 -BuildWindows`；平台构建使用 `WorkshopPlatformBuild.Build()`。详见 [风机报告](model/reports/industrial-fan.md)、[Blender 验证](Evidence/fan-modeling-20261111.md) 和 [独立 EXE 验证](Evidence/fan-viewer-20261112.md)。

风机独立展示器于 2026-11-12 下午完成动画流畅度修订，并同步平台“详细展示”配套 EXE：根据屏幕刷新率设置帧率上限，本机 240 Hz 屏幕使用 120 FPS；刚启动时提前点击会待高清渲染就绪后自动播放。1997×1217 / 1280×720 的首次及重复拆装实测均约 120 FPS，八段运动无超过 16.67 ms 的帧，真实鼠标立即点击也通过。保留原画质与拆装路径。测量边界、原始报告及文件保护核对见 [动画流畅度验证](Evidence/fan-frame-pacing-20261112.md)；此数据不代表主平台内“整机拆装”的帧率。

2026-11-12 晚间，风机独立展示器进一步对齐受保护水泵 EXE 的呈现：统一 ACES 色调映射、曝光、对比度、灯光与环境反射配置，压实深色漆面和橙色，统一页面按钮与部件简介排版；原有贴图、粗糙度、模型及拆装路径保留。源版和平台“详细展示”配套 EXE 均已更新，1080p / 720p 交互检查通过，平台副本真实鼠标拆装约 120 FPS；完整帧时统计和本次范围见 [风机配色与金属呈现验证](Evidence/fan-pump-palette-20261112.md)。

立式储罐 TK-01 的名义外形为 **约 5.86 m 总高、3.00 m 筒体直径、12 个总成**。验收源文件为 [silo-viewer-fit-review.blend](model/source/silo-viewer-fit-review.blend)。附件包括同口径阀组、人孔通孔和螺栓、顶部排气帽与吊耳支承、检修平台和梯子。压力与温度传感器通过固定线缆接入罐壁数显盒，仪表显示数据为静态演示。整机预览和修订记录见 [储罐预览目录](model/previews/silo/)、[储罐报告](model/reports/silo.md) 和 [高度与附件修订记录](Evidence/tank-height-accessories-20261112.md)。

储罐同日仪表修订：按用户要求将罐壁仪表与安装件调整为原版约两倍；显示盒为 650×490×160 mm、温度计护壳 400×72 mm、压力表外径 260 mm、液位玻璃管外径 136 mm、侧尺宽 140 mm，电子压力/温度传感器放大 1.8 倍。采用 22 mm 外径保护线管、匹配格兰头及固定抱箍，曲面铭牌增至 600×280 mm。按后续拥挤反馈，温度计向左移 210 mm、向下移 200 mm，安装座、热套管与罐壁孔同步重建。温度计液柱按实际刻度网格换算仍为静态 24.8℃，液位相对侧尺零位仍为 1.24 m；示值不因外形放大而改变。最新源文件重开检查为 888 个设备网格、241,468 个三角面，保持 12 总成。最新间距修订图为预览目录中的 **13–15**，09–12 为放大初版，01–08 为更早版本；仍处于 Blender 审图阶段，面数、部分 UV 及安装净空尚未全部达标。检查范围与未完成项见 [仪表及外观复核](Evidence/tank-workmanship-20261112.md)。

独立 Unity 工程为 `model/PumpViewerProject`，复现构建运行 `.\model\scripts\Build-PumpViewer.ps1`。FBX、线性材质参数、微法线/粗糙度和工作室反射图从源模型转接；渲染使用 URP HDR、4× MSAA、宽高各 2 倍超采样及轻度细节锐化，保留现有材质色调。默认启动按桌面尺寸选择较大窗口（最高 2240×1260），可调整窗口；显式传入分辨率参数时使用指定尺寸。它是实时展示器，与 Blender Cycles 不保证逐像素一致。

后续将 Blender 审核模型制作成独立 Unity 展示器时，按 [Blender 到 Unity 独立展示器制作提示词](model/Blender到Unity独立展示器制作提示词.md) 执行；提示词基于水泵流程，并明确要求隔离新设备工程、保护水泵交付目录和逐项记录验证边界。

拆解与组装单程约 14 秒，采用分段退出、移开、内部件分离的路径，组装沿原路返回。2026-11-11 修正叶轮腔、护罩端部、紧固件通孔及支座间隙，并拉开端盖与叶轮盖板；保留已确认的组件标注与短引导线。当前 12 总成、349 网格、119,696 三角面；源模型重新打开检查与 300 个拆解位置的表面相交检查通过。该检查不等于制造公差或完整装配干涉认证。

2026-11-12 风机早期仅调整 09 号曲面检修盖源路径，后续用户截图证明 Unity 中仍穿模；现已定位并修复实际导入坐标符号问题，当前验证以上述最新 EXE 及 [风机展示器验证](Evidence/fan-viewer-20261112.md) 顶部记录为准。

同日按用户截图修正护罩下方悬空支架：四根立柱上接护罩、下接落地底梁，底梁贴合设备底板；支架归安全护罩总成，拆解时先整体抬起再侧移。总成数、网格数及三角面数保持不变。

实际网格、自检结果和限制见 [离心泵报告](model/reports/water-pump.md)、[重新打开验证](model/reports/water-pump-reopen-verification.json)、[独立展示器验证](Evidence/pump-viewer-20261111.md) 与 [统一平台水泵接入验证](Evidence/pump-platform-20261111.md)。精细泵与风机均已替换统一平台的对应展示模型，并提供独立详细展示入口。风机运行验证和水泵入口回归见 [风机平台验证](Evidence/fan-platform-20261112.md)。

验收通过的储罐已接入主平台 TK-01。车间内按 60% 等比显示以适配原工位和巡检通道，设备观察中显示原始名义尺寸。打开“设备学习 → TK-01 → 拆装”可查看原 12 个总成的拆装路径，“详细展示”启动随平台打包的 [TankViewer.exe](model/TankViewer/TankViewer.exe)。“故障演示”使用预设的储罐教学模型。Unity 菜单 `Tools/Workshop Demo/Import Reviewed Tank` 可从独立展示器工程重新导入资源。见 [储罐展示器验证](Evidence/tank-viewer-fit-20261113.md) 和 [储罐接入验证](Evidence/tank-platform-20261113.md)。

## 一、系统主题

### 1.1 核心定位

系统名称为 **工业车间设备三维可视化监控与交互巡检系统**。

以四台设备和一台巡检小车的虚拟车间为载体，把原本分散的设备台账、运行参数和告警信息对应到具体三维对象。用户既可以从列表查设备，也可以直接在场景中点选设备，再通过故障与巡检演示理解设备运维过程。

### 1.2 交互特色与应用价值

- **设备与信息关联：** 三维设备、左侧台账、右侧详情使用同一设备状态，选择设备后同步显示其编号、类别、位置及指标。
- **空间观察：** 提供总览、自由观察、巡检跟随及屋顶显示切换，便于查看设备分布和巡检过程。
- **异常联动：** 演示故障同时改变设备状态、模拟指标、故障标记、运行统计和告警信息。
- **过程可追踪：** 从故障发生到巡检到达、确认处理和记录生成，形成可重复演示的运维流程。

当前定位是本地教学原型，用于说明三维交互与设备运维的结合方式；真实工业部署涉及的数据接入和运维能力不在本阶段实现范围内。

## 二、开发目标

### 2.1 面向领域与用户

面向工业制造、车间设备管理与设备运维教学。预期使用者包括设备运维人员、车间管理人员、安全管理人员，以及学习三维可视化与交互开发的师生。

### 2.2 需求痛点

- **设备信息缺少空间关联：** 仅看表格难以迅速判断设备在车间中的具体位置及周边关系。
- **故障定位不直观：** 告警名称与现场设备对应不够清晰，需要通过三维选择和相机聚焦辅助定位。
- **巡检过程难以展示：** 单纯显示处理结果，无法说明任务派发、路径行进和到达确认之间的关系。
- **状态容易不一致：** 设备外观、详情、告警和统计若分别维护，可能出现相互矛盾的信息。

### 2.3 本阶段目标

建立一个能说明完整业务逻辑的三维原型：设备状态由统一模块管理，用户通过鼠标和中文界面完成观察、查询、故障触发、巡检派发与处理操作；支持重置，便于课堂重复演示。

验收目标包括四台设备选择、相机操作、运行与停机表现、报修至验收闭环、重复操作防护、中文显示、两种目标分辨率以及 Windows 独立运行。实际通过范围见 [本次验收](Evidence/platform-layout-20261111.md)；第七节保留此前历史记录。

## 三、功能与业务流程

以下功能依据当前源码和场景生成配置整理，表示已有实现内容，不等同于全部通过运行验收。

### 3.1 车间与设备

监控场景共四类、四台设备，加一台原巡检小车：

- `P-01` 循环水泵。
- `TK-01` 工艺储罐。
- `F-01` 车间排风机。
- `PLC-01` 设备控制柜。

每台设备具有唯一编号、名称、类别、场景位置和状态。正常、人工停机、故障及预警通过文字、状态灯和故障标记反馈；预警可继续运行，联锁类故障会停机，选中设备显示选择框。

### 3.2 模拟监控与界面

系统约每 0.25 秒更新一次模拟指标。正常温度、振动和转速在标称值附近平滑变化；储罐和控制柜的转速、振动显示为“不适用”。界面标注“本地模拟”及“模拟数据”，避免与真实采集混淆。

界面包含顶部统计、设备矩阵、三维视口与空间标签、选中设备详情及温度曲线、告警/巡检记录和知识助手。设备台账选择会触发相机聚焦，场景点选同步设备详情。

设备支持启动与停机。输送包裹移动、转子旋转等动画与运行状态关联，停机或故障时停止运动。

### 3.3 故障与巡检闭环

当前四台设备各支持四种故障，共 **16 组可操作设备故障组合**，目录见 [WorkshopFaultCatalog.cs](Assets/WorkshopDemo/Scripts/WorkshopFaultCatalog.cs)。旧输送线、电机故障目录与参考算例作为资源保留，不再额外生成展示模型。

1. 选择设备，通过故障工况弹窗选择并注入适用故障；指标、外观、告警和统计同步更新。
2. 可为多台设备注入并存告警，同一设备已有未处理故障时禁止重复注入。
3. 在对应工单点击“开始巡检”，系统检查完整路径后派出小车。途中可以点击“查看现场”跟随小车，再点“返回工单”。
4. 小车实际到达后填写故障原因并提交维修，随后点“验收通过”。验收前设备仍处于故障状态，驳回后可以重新开始巡检。
5. 验收通过才清除故障并归档；设备恢复故障前的启停状态，原先人工停机的设备保持停机。
6. “重置演示”清空告警和记录，恢复设备、机器人及观察状态。

故障参数是教学设定，不能当作材料断裂极限。现有 Abaqus 接口用于导入 ODB 导出的区域极值时序，详见 [Abaqus 接入说明](Tools/Abaqus/README.md)；有接口不代表已有真实求解或逐节点断裂网格。当前验收使用内存合成协议数据，没有真实 Abaqus 求解结果。

### 3.4 数字孪生大屏与场景助手

大屏展示设备总数、运行数、活动告警、完成验收、设备台账、三维车间、选中设备温度曲线、详情及工单/记录。保留设备屏幕投影标签、选中框及实际导航路径，原 XZ 小地图已移除。UI 直接读取业务状态，温度曲线使用实际模拟样本。

左栏“AI 助手”显示当前设备和工单阶段，支持在对话区追问并展开回答引用。服务将当次快照与问题一同用于检索，根据设备、工况和阶段提供操作建议。资料导入在“更多 → 资料管理”。本次服务与真实回答的验证范围见 [本次验证](Evidence/workflow-chat-20261113.md)。

## 四、技术路线

### 4.1 开发环境

- Unity 编辑器：`6000.3.8f1`。
- 开发语言：C#。
- 渲染：URP `17.3.0`。
- 输入：Input System `1.18.0`。
- 界面：UGUI `2.0.0`，中文字体为思源黑体。
- 导航：AI Navigation `2.0.10`，使用 `NavMeshSurface` 与 `NavMeshAgent`。
- 主要运行目标：Unity Play 模式及 Windows x64 独立程序。

具体环境以 `ProjectSettings/ProjectVersion.txt`、`Packages/manifest.json` 和包锁定文件为准，打开工程时沿用当前版本与渲染管线。

Windows 编辑器图形配置沿用 10 月 29 日的修复，已固定为 Direct3D 11，以避开本机当时出现的 DX12 设备错误；短时启动和 Play 冒烟检查通过，长期稳定性仍未完整验证。

### 4.2 模块职责

核心运行时代码集中在 `Assets/WorkshopDemo/Scripts/`：

- `WorkshopDevice.cs`：单设备状态、模拟指标、状态表现、选择框及运行动画。
- `WorkshopSimulation.cs`：统一管理设备选择、采样、运行统计、告警、任务派发、处理、记录和重置。
- `WorkshopInspector.cs`：巡检路径检查、导航移动、到达判断及路线显示。
- `WorkshopCamera.cs`：三维点选、轨道观察、聚焦、总览、跟随及 UI 输入隔离。
- `WorkshopUI.cs`：创建中文界面，响应业务事件，刷新台账、指标、告警及记录。
- `WorkshopTrendGraphic.cs`：绘制实际模拟温度样本曲线。
- `WorkshopAssistantClient.cs`：知识服务健康检查、业务快照、问答、引用与错误显示。
- `WorkshopFaultCatalog.cs`、`WorkshopFaultVisual.cs`：故障目录、教学参数与对应示意表现。
- `WorkshopAbaqusResult.cs`：外部结果的单位、元数据、设备绑定校验及极值时序读取。
- `WorkshopHudGraphic.cs`、`WorkshopSceneOverlay.cs`、`WorkshopDashboardGraphic.cs`：HUD 图形、空间标签及状态可视化。

设备状态以 `WorkshopDevice` 为基础，业务流程由 `WorkshopSimulation` 协调。界面从业务模块读取数据，通过事件刷新，不在面板内部独立生成另一套监控数据。

### 4.3 关键实现方法

- **模拟数据：** 以设备标称值叠加正弦变化，形成平滑波动；故障通过明确操作触发，便于重复演示。
- **三维选择：** 使用相机射线检测设备碰撞体，将选中结果同步给业务模块和详情界面。
- **相机控制：** 使用轨道相机和平滑插值实现观察与聚焦，对观察角度、距离和高度设置限制。
- **导航巡检：** 基于静态碰撞代理烘焙导航网格，派发前检查路径完整性，通过路径拐点绘制路线。
- **界面联动：** 业务状态变化与指标采样分别发送事件，按钮可用性依据派发和处理条件更新。
- **场景生成：** Editor 工具集中配置设备、相机、界面及导航资源，减少手动绑定工作。

导航实现面向当前静态布局，不应据此认定复杂动态避障或全厂区路径规划已经完成。

## 五、独立开发与维护

项目当前由个人独立开发，以本 README、现有源码及 `Evidence/` 中的验证记录为维护依据。新增功能、模型和运行入口发生变化时同步更新对应说明，明确已实现、已验证和计划中的内容。

修改设备业务优先查看 `WorkshopSimulation` / `WorkshopDevice`；修改小车优先查看前面的模型与交互章节。生成场景中的长期改动应同步到生成器或配置，避免下次重建丢失。

## 六、打开与运行

### 6.1 准备工程

通过 Unity Hub 添加工程根目录，使用 **Unity 6000.3.8f1** 打开，等待依赖恢复、资源导入和脚本编译完成。首次打开或恢复依赖可能需要联网；基础监控、装配与巡检不依赖在线问答或真实硬件。

工程使用 Git LFS 管理模型、贴图、音频、字体和演示文稿。若通过 Git 获取工程，请在工程根目录确认已安装 Git LFS，并执行：

```powershell
git lfs install
git lfs pull
git lfs fsck
```

如果拿到的是已包含完整资源的工程副本，可直接通过 Unity Hub 打开。若出现模型、贴图或字体缺失，应先检查文件是否完整，以及是否仍为 LFS 指针文本。正常启动无需重新下载、提取全部美术资源。

### 6.2 选择正确场景

1. 等待编译结束，检查 Console；保存正在编辑的内容。
2. 在 Project 窗口打开 `Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`。
3. 点击 Play，在 Game 视图查看监控界面。
4. 若出现 UI 初始化异常或界面不完整，应先停止运行并排查第七节的问题，避免继续演示时误判功能状态。

官方参考场景 `Assets/WorkshopDemo/External/FactoryTraining/Scenes/urp_scene_with_training.unity` 继续保留，用于工厂环境与装配训练展示。其高亮验证结果不能替代监控流程验收。数字孪生构建入口可由 `Tools/Workshop Demo/Set Digital Twin Build Entry` 设置，该操作先备份构建列表，不会重建场景。

`Assets/WorkshopDemo/Scenes/FactoryFoundation.unity` 是环境工作副本，也不是监控业务入口。

工厂训练场景现采用深色面板、设备黄色强调色与圆角控件，顶部入口为“组装、使用、远景、近景”。机械臂滑条与示意图分区显示，示意图已居中并放大。按 `M` 切换静音，按 `H` 隐藏或恢复界面；原静音和隐藏按钮已移除。

引导装配与独立考核的步骤提示均支持点击“继续”立即开始收起；未点击时，约 **2.4 秒**后自动开始收起。收起动画在约 **0.2 秒**内轻微下移并淡出，保持下一工序计时。完成统计采用单列 15 项工序布局。“使用”页和顶部按钮曾被透明相机热区拦截，现已在热区创建代码中关闭射线拦截；当前验收范围见第七节。

### 6.3 基本操作

- 左键单击设备：选中设备并查看详情。
- 左键拖动或按住右键拖动：旋转观察。
- 鼠标滚轮：缩放。
- 按住中键拖动：平移观察。
- 点击设备台账或告警行：选中并聚焦对应设备。
- “总览”：恢复整体视角；“自由观察”：退出跟随；“巡检跟随”：切换跟随状态。
- “屋顶”：切换屋顶显示；“启动设备／停止设备”：切换选中设备运行状态。
- “手动接管”：方向键前进、倒车和转向；按钮切换为“恢复自动”，点击后继续尚未完成的巡检任务。
- `H`：隐藏/恢复界面；`M`：静音/恢复声音。填写输入框时不触发监控快捷键。
- 业务命令：选择故障工况、派发巡检、确认处理及重置演示。

故障设备不能通过普通启停按钮解除故障，需要完成巡检处理流程。

### 6.4 场景生成与构建

日常查看优先打开已有场景，**无需先执行** `Tools/Workshop Demo/Build Demo`。

该菜单用于重新生成场景，不是导出 EXE。它会覆盖 `WorkshopDemo.unity` 和相关生成资源，并替换构建场景列表；生成前必须保护需要保留的人工场景修改。需要长期保留的生成配置应同步到 `WorkshopFoundationBuilder.cs`。

统一 Windows 构建使用 `Tools/Workshop Demo/Build Unified Platform EXE` / `WorkshopPlatformBuild.Build()`，包含监控及原装配两场景，输出到 `Build/WorkshopPlatform/WorkshopPlatform.exe`。`WorkshopVerification.BuildPlayer()` 是仅构建监控的旧入口。

构建后应保留同目录配套文件。2026-11-11 统一构建成功，独立程序已实际启动并检查 1280×720 画面、H 隐藏界面、方向键手动前进和恢复自动模式；完整 Windows 业务流程与声音采集仍未验收，详情见 [本次记录](Evidence/platform-layout-20261111.md)。

## 七、当前进度与验证状态

### 7.1 已有工程内容

当前统一平台包含四台设备和一台小车、七栏后台界面、故障参考分析、拆装观察、导航/手动接管及报修验收闭环。原装配场景和独立 Docker 知识服务保留。当前验证见 [本次记录](Evidence/platform-layout-20261111.md)；以下 2026-11-10 的八设备 HUD 报告为历史版本。

此前监控验证记录为 [2026-11-10 空间 HUD 验收](Evidence/dashboard-hud-20261110.md)：

| 范围 | 历史报告 | 结果 |
| --- | --- | --- |
| 1280×720 HUD 与业务闭环 | [dashboard-hud-1280-20261110.txt](Evidence/dashboard-hud-1280-20261110.txt) | COMPLETE |
| 1920×1080 HUD 与业务闭环 | [dashboard-hud-1920-20261110.txt](Evidence/dashboard-hud-1920-20261110.txt) | COMPLETE |
| 1920×1080 的 24 机制 / 32 组合故障回归 | [dashboard-hud-faults-1920-20261110.txt](Evidence/dashboard-hud-faults-1920-20261110.txt) | COMPLETE |

上述为 **Editor Play 历史验证**，覆盖 UI 射线、选择/启停、实际导航到达、处理记录、并存告警和连续重置；本次 README 更新没有重跑。未包含 Windows EXE、完整官方装配流程、OS 鼠标相机拖动/悬停或真实在线模型回答。更早知识服务和 UI 修复见 [2026-11-08 联调记录](Evidence/twin-integration-20261108.md)。

### 7.2 历史验证记录

- **2026 年 10 月 28 日 15:09:36：** 场景生成报告记录了 8 台设备和 358 个导航顶点，见 `Evidence/demo-build.txt`。该报告只证明当次生成结果。
- **2026 年 10 月 28 日 15:11:01 开始：** 监控自动验证通过“8 台设备运行”检查，随后在 `WorkshopUI.Refresh()` 发生列表索引越界，验证中断，没有 `COMPLETE`；原报告已备份至 `Evidence/runtime-verification-before-twin-20261108-162249.txt`。
- **2026 年 10 月 28 日 15:34:13 开始：** 工厂训练场景完成图层与 16 个部件重复高亮、恢复及运行检查，报告记录运行错误为 0，见 `Evidence/factory-layer-validation.txt`。此结果仅适用于该训练场景的已测范围。

**上述 UI 越界属于历史失败，2026-11-08 已修复并完成业务脚本复测。** Windows EXE 的本次启动与驾驶检查见 6.4；真实设备接入和有效密钥下的真实模型回答仍未验收。

**2026 年 10 月 29 日训练界面更新：** 中文界面、黄色圆角样式、快捷键及机械臂布局已保存到训练场景，并同步至 `FactoryTrainingUIEditor`。本轮 Play 检查中，机械臂滑条在 0%、50%、100% 时的可见图案居中、尺寸及间距检查通过，并查看了 1920×1080 截图。双分辨率自动检查因 Game 视图尺寸切换未达到预期而中断，见 `Evidence/factory-arm-ui-verification-20261029.txt`，不能据此宣称双分辨率通过；完整训练 UI 检查也未通过全部检查，见 `Evidence/factory-ui-verification-20261029.txt`。本轮未验证监控业务闭环或 Windows 独立程序。

**2026 年 10 月 30 日训练交互修复：** 已实现两种模式统一的 2.4 秒提示、点击“继续”立即启动 0.2 秒淡出下移动画，并保持下一工序计时；完成统计改为固定单列，提示 Canvas 补充鼠标射线支持。随后定位到运行时透明 `LeftHotboxPanel` 覆盖左侧 UI，将左右相机热区的 `raycastTarget` 设为 `false`，恢复导航、自动运行按钮和滑条的输入通路；同时处理机器人速度缓存和事件退订的生命周期问题。场景布局与对应生成代码均已保留。

**2026-10-30 验证及用户反馈：** 当日 09:21:50–09:22:09（+08:00），Editor Play 通过排队鼠标输入验证了 **1920×1080** 的四个导航入口、自动启停、滑条拖至 75%/25% 及机械臂实际移动，截图为 `Evidence/factory-dialogs-usage-input-1920-20261030.png`。**1280×720** 导航和自动启动通过，随后在停止断言处中断，`Evidence/factory-dialogs-verification-20261030.txt` 仍记录 `FAILED`。用户其后确认“改好了”，但该反馈不能视为完整自动验收通过。1024×768、完整提示时序与计时保持、15 步完成/重试、三维部件鼠标拖装和 Windows 独立运行仍待复验。

### 7.3 复验范围

修改后应至少检查：正常启动、四台设备的台账与三维选择、相机与 UI 输入隔离、停机动画、多设备并存告警、完整可达路径、提前处理限制、重复点击保护、验收和驳回、记录生成、连续两次重置和第二轮完整流程。改动小车后，还应在训练场景检查部件拖装、自动示意、滑条、视角及完整工序，并单独复验巡检。

还需在 **1280×720** 与 **1920×1080** 检查中文、布局及文字溢出，并在 Windows 独立程序中复验。已有 `WorkshopVerification` 工具可辅助检查，但程序调用选择不等于真实鼠标交互验收；`verify-demo` 只验证当前场景，不会自动打开监控场景。

### 7.4 当前限制

- 数据均由本地脚本模拟，未接入真实设备、传感器或工业通信协议。
- 已支持多设备、多类型并存告警；单台巡检小车一次执行一个任务，未实现多机器人调度或并行任务队列。
- 告警和巡检记录仅保存在运行期内存中，重置会清空，退出运行后不保留；新增数据库与后端只保存、检索知识资料。
- 小车保留原装配训练；四类设备的名义模型已有拆装观察，不能等同于精细建模目标全部完成。
- 故障表现以教学示意为主；没有真实材料极限求解、真实 Abaqus 断裂结果或整车动力学求解。
- 当前场景使用现有模型与材质搭建，资源来源及许可分别记录；离心泵与轴流风机精细模型已接入统一平台，储罐与控制柜的 Blender 修订版仍待审图和接入。
- 本工程 Windows 已通过启动、画面、H 隐藏界面及手动前进/恢复自动模式检查；完整独立程序业务流程、M 的系统声音采集与真实模型在线回答仍待验收。当前双分辨率七栏平台、自动任务实际到达与 H/M 状态检查见 2026-11-11 专项记录，2026-11-10 HUD 为历史记录。

## 八、建议课堂演示顺序

以下约三分钟流程用于课堂演示；业务脚本已通过本轮复测，真实鼠标与独立程序仍需人工试演。

1. **0:00～0:30，介绍主题与边界：** 说明系统面向设备运维教学，展示车间总览，明确所有指标是模拟数据。
2. **0:30～1:00，观察与查询：** 选择设备，展示台账、详情及三维选择联动；旋转、缩放并切换屋顶显示。
3. **1:00～1:20，运行状态：** 展示输送或风机动画，停止并重新启动一台设备，观察状态、指标和统计同步变化。
4. **1:20～1:45，异常定位：** 选择一个设备故障，例如 `P-01` 超温，说明故障标记、告警及保护停机的联动，点击告警定位。
5. **1:45～2:30，巡检与处理：** 派发机器人，展示路线及跟随视角；到达后确认处理，查看设备恢复与巡检记录。
6. **2:30～3:00，重置与说明：** 重置演示，介绍统一状态来源、导航技术和当前限制。

实际导航耗时以运行情况为准，应提前试演并调整讲解节奏。

## 九、工程目录与查阅入口

```text
Assets/WorkshopDemo/
  Scripts/       设备、模拟、巡检、相机和界面代码
  Editor/        场景生成、构建、验证及编辑器工具
  Scenes/        监控场景及环境工作副本
  Generated/     生成的材质与导航数据
  External/      场景所需模型、材质、字体等资源
Packages/        包依赖与锁定文件
ProjectSettings/ Unity 版本、渲染、输入及构建设置
Evidence/        历史生成报告、验证记录和截图
Tools/           资源整理与恢复辅助工具
Services/WorkshopAssistant/ Docker、知识文档、接口和测试
model/           建模规范、离心泵精细源文件、贴图、预览、复现脚本与检查报告
```

- [小车资源](Assets/WorkshopDemo/External/FactoryTraining/Prefabs/moto_bot/)：完整模型与装配版。
- [最新 HUD 验收](Evidence/dashboard-hud-20261110.md)：当前监控界面的验证范围和复跑方法。
- [建模目标](model/建模目标与验收标准.md)：四类设备的质量要求、制作进度和验收项。
- [离心泵建模记录](Evidence/pump-modeling-20261111.md)：本轮源模型、Blender 交互验证与未完成项。
- [知识助手](Services/WorkshopAssistant/README.md) / [Abaqus 接口](Tools/Abaqus/README.md)：两个独立集成的配置与边界。
- [AGENTS.md](AGENTS.md)：编码助手的工程导航与修改约束。

迁移工程时应保留 `Assets` 及其全部 `.meta`、`Packages` 和 `ProjectSettings`；文档、工具及验证证据一并保留。`Library`、`Temp`、`Logs` 等缓存可重新生成，构建产物和原始下载归档不纳入版本管理。部分资源恢复工具依赖归档或远端文件，不属于日常运行步骤。

美术资源与字体的许可记录保留在 `Assets/WorkshopDemo/External/SOURCES.md` 及相应资源目录，分发时应保留必要的许可文件。业务实现说明与资源权利归属分别记录，工程未对全部内容统一授予开源许可。
