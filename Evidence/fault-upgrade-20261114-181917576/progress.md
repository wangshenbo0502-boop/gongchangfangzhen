# 故障模块升级首轮进度

2026-11-14 · Unity 6000.3.8f1 · WorkshopDemo · Windows x64

| 任务 | 状态 | 本轮交付与边界 |
| --- | --- | --- |
| T00 | 部分验证 | 记录 Git 基线、导入层级、六案例旧路径、既有储罐测试及保护清单。六案例各自的完整改前截图未补齐。 |
| T01 | 已验证首轮范围 | 四设备七组具名绑定按实例解析。教学进度与时间显式传入，效果可逆，漏点焦点独立，进入故障前沿原路径回装。剩余案例当前只验证了绑定候选。 |
| T02 | 外观与流程已验证 | TK-LEAK 使用原 12 总成储罐，提供湿痕、滴漏、地面积液、液位计变化、近景与带来源的读数。穿孔网格待制作，漏率无求解。 |
| T13 | 储罐及部件绑定已验证 | Windows 候选版支持 `-workshop-fault-upgrade-verify <输出目录>`。1080p、720p 的 UGUI 射线和控件事件、模型断言及实际导航均通过。 |
| T03–T12 | 待实施 | 剩余教案、工况扩容和实训规则尚未进入本轮。真实 Abaqus 样例需另行准备匹配部件的材料、工况与源结果。 |

## 实施路径

- `WorkshopFaultBinding.Resolve` 读取 `Resources/WorkshopFaults/bindings.json`。新增配置独立于拆装清单，模型资源及场景未重建。基线与锚点来源见 [改前基线](baseline.md)。
- `WorkshopFaultProfile.Evaluate` 统一预览阶段、教学液位和可用读数。`WorkshopEquipmentModel.SetDamage` 对 TK-LEAK 采用该帧，车间接入 `WorkshopDevice.Level`，观察室保留独立进度。滴液路径根据显式时间、法线和重力计算。返回观察、关窗和验收清理对应实例的视觉。
- `WorkshopEquipmentView.SetFaultMode` 处理回装和观测，`TryGetFaultFocus` 提供漏点包围盒。TK-PRESS、TK-LEVEL、TK-SUPPORT 暂沿用并明确标注旧版教学罐。
- `WorkshopUI.UpdateEquipmentText` 和 `ExportEquipmentDimensions` 接入 TK-LEAK 教学读数，漏率缺失值在 CSV 留空。原有工单、机器人派发、维修、驳回和验收方法继续使用。

## 构建与结果

- 使用 `D:/unity/Editor/Unity.exe -batchmode -quit -projectPath <项目目录> -executeMethod WorkshopDemo.Editor.WorkshopPlatformBuild.BuildFaultReview -logFile <日志>`。候选版为 `Build/WorkshopFaultReview-20261114-183756133/WorkshopPlatform.exe`，Player BuildReport 成功、0 错误。Unity Editor 因历史中文路径 TypeDb 问题退出码 1，脚本编译及 Windows Player 构建完成。[BuildReport](../unified-build-20261114-183815.txt) · [构建日志](unity-build-2.log)
- 以 `-workshop-fault-upgrade-verify <独立输出目录>` 分别在 1920×1080 和 1280×720 窗口运行。两个 Player 退出码均为 0。包括七组绑定、拆散中转故障回装、漏点近景、倒拖一致、双复位、运行／停机状态恢复、离场、驳回、重复提交、教学预览隔离。[1080p 报告](1080p/verification.txt) · [720p 报告](720p/verification.txt)
- 另跑 720p 平台原有 UGUI 回归，覆盖设备学习、四设备观察、水泵与风机拆装、参考分析、泵工单和小车训练场景打开及返回。退出码 0。官方场景的完整装配步骤未在该用例复验。[平台回归](platform-regression/verification.txt)
- 首次隐藏窗口运行产生黑帧，活动工单用例还走错了会打开工单详情的主按钮。改用“设备操作 → 观察与拆装”，并用可见窗口重跑。已查阅末态和近景画面。[1080p 末态](1080p/05-fault-teaching.png) · [近景与滴液](1080p/07-scrub-75.png) · [720p 末态](720p/05-fault-teaching.png)
- 以 1280×720 正常打开候选程序，使用 Windows 鼠标事件从设备目录进入 TK-01，选取故障末态，再返回观察。逐张核对了时间点、近景滴漏、地面积液和返回后恢复。[鼠标选取末态](mouse-720p/WorkshopFaultReview-20261114-183756133-04-final.png) · [恢复观察](mouse-720p/WorkshopFaultReview-20261114-183756133-05-observe.png)
- 通过 Windows 鼠标事件执行末态 CSV 导出，检查 18 s 对应教学液位 38%，未求解的漏率保留空值与 `available=false`。[CSV 样本](csv-720p/TK-LEAK-export.csv)
- 自动验证用 UGUI 顶层射线及控件事件，滑条使用值变更事件。保留的未覆盖项包括完整四角度漏点比对、性能与长期资源测量、实测漏率、求解结果和正式版部署。
- 正式版独立展示器共 671 个文件逐一核对了路径、长度和 SHA-256，包括水泵展示器 170 个文件。对照一致。[核对记录](protection-check.json)

## 下一接入点

先从 `WorkshopEquipmentModel.ReviewedPump.cs / ReviewedPump` 与 `WorkshopEquipmentModel.Faults.cs / ProfileFor` 接入 P-SEAL。使用 `pump.seal.faces` 定位现有动、静环，校核密封出口后实现局部漏液，然后按 `pump.impeller.inlet` 处理 P-CAV。TK-LEAK 如需表现穿孔，在 `TNK_01_Shell_8mmWall` 上制作带内外壁厚的独立派生网格。
