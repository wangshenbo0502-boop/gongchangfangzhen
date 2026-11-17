# 故障模块升级交付与验证记录

交付记录整理：2026-11-16，Asia/Shanghai；下列构建和运行验收发生于 2026-11-15。Unity 6000.3.8f1，URP，新 Input System，Windows x64。

本轮交付为源码、派生资产、可运行候选包及适用验证。32 个当前设备案例的定义、绑定、可逆预览和业务闭环已接通；六个重点案例采用对应部件表现。真实 Abaqus 求解、全部案例精细视觉制作及完整性能验收尚未完成，不能把本报告视为 T00–T13 全部验收通过。

## 运行入口与构建

- 候选程序：[WorkshopPlatform.exe](../../Build/WorkshopFaultReview-20261115-234044731/WorkshopPlatform.exe)。保留同目录配套文件，可直接运行。
- 场景：`Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`；候选同时包含官方 `urp_scene_with_training.unity`。
- 构建入口：`WorkshopDemo.Editor.WorkshopPlatformBuild.BuildFaultReview()`，未执行 Build Demo，也未重建用户场景。
- [本次 BuildReport](../unified-build-20261115-234102.txt)：Succeeded，Errors 0，Bytes 1,808,614,402。[完整构建日志](build-234036782/unity.log)。
- Player 构建成功，但批处理 Editor 仍因历史中文路径 TypeDb 问题以 1 退出，并在退出末段报 “Scripts have compiler errors”。日志无 C# `error CS` 诊断。应分别理解构建成功、Editor 非正常退出和下面的实际 Player 检查，不能宣称批处理全程无错误。
- [候选文件指纹](candidate-fingerprint.json)记录 EXE 和 Assembly-CSharp.dll 的 SHA-256；所有下列最终套件和鼠标检查使用此候选构建。之后仅补文档及鼠标辅助工具，未修改候选对应 C#。
- 日常入口 `Build/WorkshopPlatform/WorkshopPlatform.exe` 本轮未替换，未提交 Git 或发布 Release。

## T00–T13 状态

| 任务 | 状态 | 本次交付、通过范围与保留缺口 |
| --- | --- | --- |
| T00 基线与资产 | 部分已验证 | 保留 HEAD、原始脏工作区、隔离实例层级、资源及展示器清单；六案例完整改前截图未补齐，不能事后冒充改前证据。 |
| T01 公共接口 | 本次适用检查通过 | Profile、Frame、实例绑定、Presenter、焦点、来源读数、资源所有权已实现；32 案例倒拖与双复位通过。长期资源增长检查未完成。 |
| T02 储罐 TK-LEAK | 几何与业务已验证 | 同一 reviewed 储罐、12 总成、局部湿润滴漏积液、教学液位及预设 12 mm 贯通孔；孔中心与邻壁射线检查通过，漏率没有求解。完整四角度审查未完成。 |
| T03 P-SEAL / P-CAV | 专项检查通过 | 原动静环保留，取消通用断裂插片；轴侧泄漏、磨损示意与入口汽泡、剖视、压力流量读数区分；100 项专项断言通过。泄漏和汽蚀均非物理求解。 |
| T04 F-CRACK / F-IMBAL | 实现及适用回归通过 | 使用原叶片局部形变，保持非局部顶点及后缘连接；轴承案例不再弯叶片，局部振动提供 1×/5×。完整四角度、相邻关键帧的裂面自然度审查未完成。 |
| T05 PLC-CONTACT | 实现及适用回归通过 | 定位 X1-01 带载端子，局部热视图可切换、原材质可恢复；未把 PE 排当作普通负载热点。温度为教学数据，非真实热求解。 |
| T06 旧工况与 UI | 部分完成 | 当前四设备不再默认切旧储罐，各案例绑定与读数按机制路由，CSV 含来源、有效性和单位；部分表现仍为局部颜色或证据示意，未达到全部精细效果目标。 |
| T07 指标与保护 | 本次适用检查通过 | 故障实例、快照、直接/渐进注入、严重度与保护状态分离；四设备并存、预览隔离、原启停恢复通过。指标继续是明确标识的教学模拟。 |
| T08 32 案例 | 定义及闭环通过，视觉部分完成 | 32 张案例卡含绑定、现象、指标、证据、诊断、维修、复验与来源；32 案例各覆盖运行/停机实际巡检闭环。不能据数量宣称 32 套精细几何动画或分析完成。 |
| T09 v1 包络 | 接口与合成验证通过，真实数据待补 | 独立包络页、通道、单位、不规则时间插值、端点夹取、单帧和重载；不再依赖参考件存在才能打开。无真实 ODB，真实导出/对照未验证。 |
| T10 v2 局部全场 | 最小协议及合成验证通过，真实闭环待补 | 固定 DC3D4、原始 NODAL NT11 导出/导入/读取/离散帧/节点探针；文件哈希、预算、路径和两帧缓存检查。仅热场，结构、裂纹、变化拓扑不支持；真实三位置×三帧对照未完成。 |
| T11 四局部算例与裂纹 | 依赖待补齐，未完成 | 建立四份缺输入档案；缺尺寸依据、材料、载荷/热源、约束、基准及真实 ODB，未进行 Abaqus 求解。协议合成测试不计为求解。 |
| T12 证据式实训 | 本次适用检查通过 | 沿用原工单，增加证据、诊断、维修与复验，考核隐藏答案；负向、驳回返工、归档自动检查及一次真实鼠标完整引导实训通过。 |
| T13 构建与交付 | 适用回归通过，完整验收部分完成 | 两分辨率综合套件、泵/罐专项、原平台回归、Python 11 项、鼠标闭环及保护哈希通过；完整视觉审核、前后性能对比和长期检查未完成。 |

## 修改入口与方法

以下路径相对工程根目录，既有用户修改保留；不是把全部脏文件都归为本轮新增。

| 文件或目录 | 实际行为与下一次维护入口 |
| --- | --- |
| `Scripts/WorkshopFaultBinding.cs`、`Resources/WorkshopFaults/bindings.json` | `WorkshopFaultBinding.Resolve` 按当前模型实例定位具名部件、锚点与焦点，配置独立于原拆装清单。 |
| `Scripts/WorkshopFaultProfile.cs`、`WorkshopFaultPresenter.cs`、`WorkshopEquipmentModel.Faults.cs` | 显式进度/时间求值、可用读数、恢复及模型适配；Frame 类型随 Profile 定义，无需按任务书草案另造同名文件。 |
| `Scripts/WorkshopPumpFaultPresenter.cs`、`WorkshopLocalFaultPresenter.cs`、`WorkshopEquipmentModel.ReviewedPump.cs`、`WorkshopEquipmentModel.ReviewedFan.cs` | 部件级泄漏、汽泡、热、振动及局部裂纹，分别恢复原网格、材质和变换；新资源沿既有生命周期释放。 |
| `Scripts/WorkshopEquipmentModel.Damage.cs`、`WorkshopEquipmentView.cs`、`WorkshopUI.Compact.cs`、`WorkshopUI.Equipment.cs` | 主车间/观察室双路径、近景、沿原拆装路线回装后排队播放、独立学习进度、来源读数及 CSV。 |
| `Scripts/WorkshopFaultCatalog.cs`、`WorkshopFaultCase.cs` | 40 目录定义，其中当前四设备适用 32，历史设备 8；32 案例卡与独立证据要求。 |
| `Scripts/WorkshopDevice.cs`、`WorkshopFaultState.cs`、`WorkshopSimulation.cs` | 活动故障实例、快照、保护及维修验收恢复原启停；业务状态继续以设备和 Simulation 为准。 |
| `Scripts/WorkshopSimulation.Training.cs`、`WorkshopUI.Training.cs`、`WorkshopAssistantClient.cs` | 证据记录、诊断与维修校验、复验、返工历史、复盘及考核根因提示隔离。 |
| `Scripts/WorkshopUI.Analysis.cs`、`WorkshopUI.Abaqus.cs`、`WorkshopAbaqusResult.cs`、`WorkshopAbaqusCurveGraphic.cs` | 保留参考件，增加独立包络来源路由与浏览，未将教学指标改标为 ODB。 |
| `Tools/Abaqus/export_field_odb.py`、`Editor/WorkshopAbaqusFieldImport.cs`、`Scripts/WorkshopAbaqusFieldResult.cs`、`WorkshopAbaqusFieldView.cs`、`WorkshopUI.AbaqusField.cs` | 最小热场包导出、校验、备份导入、离散浏览与原始节点探测；限制和不支持范围见 [接口说明](../../Tools/Abaqus/README.md)。 |
| `Scripts/WorkshopCompactUIVerification.Upgrade.cs`、`.Faults.cs`、`.Pump.cs` 及原罐/风机验证器 | 当前四设备验证入口和迁移后的断言；正常启动不创建故障验证宿主。 |
| `Editor/WorkshopPlatformBuild.cs`、`WorkshopFaultDerivedImport.cs` | 时间戳候选输出及受控展示器副本；仅派生壳体专用导入设置，未全量重新导入模型。 |

上表 `Scripts/`、`Editor/` 和 `Resources/` 均位于 `Assets/WorkshopDemo/` 下。长期变化保存在源码、配置和独立派生资产，没有仅在场景里做一次性改动。

## 本次运行证据

所有套件均使用当前候选程序；综合套件由可见 UGUI 顶层射线、控件事件及实际导航驱动，不能等同于 OS 鼠标测试。

| 输出 | 分辨率 / 方法 | 结果 |
| --- | --- | --- |
| [最终综合套件 1080p](run-1080-calibrated/verification.txt) | 1920×1080，Windows Player | PASS，691 条 PASS 断言 |
| [最终综合套件 720p](run-720-final/verification.txt) | 1280×720，Windows Player | PASS，691 条 PASS 断言 |
| [水泵专项](run-pump-final/verification.txt) | Windows Player，`-workshop-check-pump` | PASS，100 条 PASS 断言 |
| [储罐专项](run-tank-final/verification.txt) | Windows Player，`-workshop-check-tank` | PASS，116 条 PASS 断言 |
| [原平台回归](run-platform-final/verification.txt) | Windows Player，UGUI 回归 | PASS，217 条 PASS 断言 |
| [Python 导出器测试](python-tests.txt) | `python -m unittest discover -s Tools/Abaqus -p 'test_export*.py' -v` | 11 tests，OK |
| [Windows 真实鼠标](mouse-final/verification.md) | 正常启动、1280×720、Windows 输入与逐张截图审阅 | P-SEAL 引导实训完整闭环通过 |

PASS 计数包含按钮可用、文字边界等重复性断言，不是 1,815 个独立业务测试案例。

综合套件在每个分辨率分别执行 32 案例×故障前运行/停机两种状态，共 64 次实际 NavMesh 派发、到场、维修、验收和恢复；还覆盖重复注入、提前提交、四设备独立实例、渐进保护、引导/考核的错误证据/诊断、缺复验、驳回返工及连续两次全局重置。

原平台回归覆盖参考有限元工具、旧参考件开裂播放、普通维修归档、设备观察、泵/风机内部拆装、手动/自动巡检，以及打开官方装配场景并返回。官方完整 15 工序拖装未在本轮验收。

真实鼠标单独完成“新建课题 → 注入 → 实际到场 → 采集证据 → 诊断维修 → 提交 → 缺复验验收被拦截 → 补复验 → 验收归档”。[最终归档截图](mouse-final/23-archive.png)显示 1 项归档、0 项未关闭，检查与复验已记录、无效操作 1 次。文本输入遇到中文输入法转换，归档非空原因成功保留，但未验收英文逐字符一致性。

## 储罐派生资产与坐标校准

原源文件为 `model/source/silo-viewer-fit-review.blend`；新增 `Tools/FaultAssets/build_tank_perforation.py` 派生 `Assets/WorkshopDemo/Resources/WorkshopFaults/tank-shell-perforated.fbx`，不保存或覆盖源文件。

- 机制是名义 8 mm 壁厚壳体上的预设 12 mm 局部贯通孔，25% 进度后切换派生壳体；不是腐蚀增长或漏率求解。
- [Blender 原始记录](tank-perforation.json)：724 个原顶点、824 个派生顶点，724 个非局部原顶点位置不变；非流形边前后均为 0，孔边保留厚度，模型 bounds 不变。
- 原始记录中的 `unityLocalAnchorM` 正 X 是初次坐标假设，已被 [Unity 导入校准记录](tank-imported-calibration.json)更正；保留原记录追溯失败过程。实际锚点为 `(-0.4354,-1.1354,-3.0)` m，法线 `(-0.2903,-0.9569,0)`，导入转换为 `(Blender X, Blender Y, -Blender Z)`。
- 两分辨率最终套件均验证孔中心射线 0 次表面相交、邻壁射线 2 次内外壁相交；初始/早期原网格保留、后期派生网格生效、复位恢复原网格。
- 原始 source SHA-256 为 `e02d18a42c58192bbe9e9c57d826f266cfcec2905a2b917b9dd1cb1f1a717f96`；派生 FBX 为 `3fd52dbf2df1a973fe7b5ee7b92e282a1f542d9f20dc9725a7a5098e72cfa0f3`。

## 数据来源与真实分析依赖

v1 包络和 v2 热场测试使用明确标识的合成数据，保留在独立 Evidence 目录，不放入正式 StreamingAssets 结果目录；可验证协议、数值传递及界面，不能证明真实导出或求解正确。

v2 当前只支持固定 DC3D4 与原始 NODAL NT11；节点标签不连续与字段乱序可正确映射，拒绝坏哈希、越界路径、错误绑定、不支持字段及冒充物理秒数的静力参数。结构位移、应力外推、接触、XFEM、单元删除和变化拓扑仍需独立扩展。真实实例变换、源结果三位置×三帧及物理模型校核尚无证据。

四份实际缺口在 [AnalysisCases](../../AnalysisCases/README.md)：端子局部热、罐壁减薄静力、密封接触及叶片固定缺陷。所缺材料/电热参数、工况与边界来源、可追溯几何、Abaqus 运行环境及 ODB 分别列在对应 `missing-inputs.md`。未实际求解，T11 保持未完成。裂纹增长、疲劳寿命与真实漏率没有实现或预测。

继续入口：资料就绪后先完成 `PLC-CONTACT-local-thermal` 的真实输入和成功目标步，再用 `Tools/Abaqus/export_field_odb.py`、`WorkshopAbaqusFieldImport` 和 `WorkshopUI.OpenAbaqusField` 完成源软件与 Unity 的三点三帧对照；其余三例需先扩展相应结构/接触协议，不能改温度字段名称冒充支持。

## 性能、失败与未覆盖项

当前 GPU 日志为 NVIDIA GeForce RTX 4060 Laptop GPU，Direct3D 11。综合套件仅对当前平台故障预览取约 4 秒短样本：

| 分辨率 | 平均 / P95 / 最大帧耗时 | managed_bytes / allocated_bytes |
| --- | --- | --- |
| 1920×1080 | 17.13001 / 18.9704 / 24.9336 ms | 17,203,200 / 410,361,518 |
| 1280×720 | 17.28724 / 19.1323 / 21.116 ms | 16,355,328 / 416,808,458 |

这些是采样时的内存计数，不是内存峰值或增长率。没有同机升级前对照、加载耗时、长时间运行及连续资源增长测量；不能认定 1080p 60 FPS 目标通过，也未借用独立风机展示器性能成绩。

六案例的正面/侧面/斜视/近景完整矩阵、各阶段与起裂相邻帧审查仍未做完。现有截图、局部顶点和恢复断言只覆盖较窄范围；后续从当前候选各案例固定镜头截图继续。其余部分案例仍用局部材质/颜色和诊断证据表达，精细效果不能按 32 项全计完成。

历史失败候选和 `run-1080-first/second/third/final/perforation` 等输出保留，早期错误包含配置、UI 路由与储罐派生导入坐标；本报告接受的是 `234044731` 候选及上表最终输出，不能把历史失败报告混作当前通过证据。

Player 启动仍有历史 “Failed to create agent because there is no valid NavMesh” 和阴影图集降级警告；随后实际导航验证通过。验证器订阅期间未记录运行异常，但不据此称完整日志无警告。

## 保护与基线

- 基线 HEAD：`ca86cf08216592523a49227b64152e4213415af0`；原工作区修改、未跟踪文件与删除记录未回退。基线详见 [原始记录](../fault-upgrade-20261114-181917576/baseline.md)与该目录 Git 清单。
- [protected-before.json](protected-before.json) 对 `Build/WorkshopPlatform/Viewers/` 记录 671 个文件的路径、长度与 SHA-256。
- [最终核对](protection-final.json)：原目录 671/671 不变，当前候选副本 671/671 与基线一致，含受保护 WaterPumpViewer。未覆盖、删除或移动原展示器。
- [模型配置核对](model-protection-after.json)核对原四份 parts.json 不变；该清单不是全部模型资源哈希，不能扩大证明范围。[源文件保护](protection-after.json)同时确认原储罐 Blender 源文件未变。
- `sources-before.json` 是 70 份脚本的改前指纹，不是全工程资产快照。当前新增脚本及故障资源对应 `.meta` 检查无缺失。
- 保留四设备 12/12/11/16 总成、原拆装路径、官方训练、Outline 层 8 / mask 256；观察室 27/28 层仅用于克隆。未切换渲染管线、输入系统或重新提取全部资源。
- 最终文档链接检查通过，新增脚本/故障资源缺失 `.meta` 数为 0，`git diff --check` 无空白错误；Git 的 LF/CRLF 提示保留，未做无关换行重写。结束检查未发现仍运行的 WorkshopPlatform 或 Unity 批处理进程。

## 后续准确入口

1. 视觉收口：`WorkshopLocalFaultPresenter.Apply` 与 `WorkshopFaultCase.All` 逐案例补足尚为颜色/证据示意的局部效果；六重点案例完成四角度和相邻关键帧视觉审查，再更新 T04/T06/T08/T13。
2. 性能验收：当前候选固定画质，记录同条件基线、预热后常用案例、加载耗时及长时内存/帧时，定位性能瓶颈后再定优化范围。
3. 真实分析：按 `AnalysisCases/*/missing-inputs.md` 补资料；先端子热场，完成成功求解、输入/ODB 哈希、网格校核与三点三帧对照，再更新 T09/T10/T11。
4. 需要部署时采用当前候选及配套目录；正式替换、Git 提交和公开发布均未在本轮执行。
