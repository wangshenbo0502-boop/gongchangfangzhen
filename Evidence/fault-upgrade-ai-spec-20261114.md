# 故障模块 AI 实施规格核对记录

日期：2026-11-14，Asia/Shanghai。用户要求将升级指导书细化到后续 AI 知道具体怎样修改的程度。

## 本次交付

- 新增 [故障模块 AI 实施任务书](../故障模块AI实施任务书.md)，按 T00–T13 给出 14 个任务，覆盖文件和方法、现有调用链、部件绑定、接口与数据结构、六案例实现、业务迁移、Abaqus 分层接入、验证器及候选构建。
- [原指导书](../故障模块升级指导书.md)增加前置入口和第 13 节，明确关联修改及首轮落地范围。
- README 故障章节加入实施任务书入口。所有新增能力、命令入口和参数仍明确标为待实现。

## 额外核对出的关键依赖

1. `WorkshopUI.Compact.cs / SetEquipmentMode()` 才是储罐切旧模型的当前 UI 调用入口。
2. `WorkshopEquipmentView.LateUpdate()` 多处直接访问 `DamageSurface` 的 Renderer，需要改为适用于泄漏、热区等非形变网格的焦点获取方式。
3. 主车间 `WorkshopEquipmentModel.Update()` 与观察窗口分别使用 `FaultAge/18`、播放进度，两条路径都需要接入统一表现；教学回放和业务状态需分开。
4. `ReviewedTank()` 指定 `TNK_01_Shell_8mmWall`；四设备总成名称从实际 parts 清单读取。
5. 泵静环／动环名称在 `build_pump_detailed.py` 查到，任务书明确标为待实际导入层级核实的候选名。
6. 控制柜旧 DamageSurface 指向 PE 排，不能据此假定该部件是负载电流接点过热的位置。
7. 旧 `WorkshopFaultValidation` 仍依赖八设备、P-02、四故障、五条线和旧 Resolve 流程，不能直接用于本轮四设备升级验收。
8. `WorkshopCompactUIVerification.VerifyTank()` 要求切换旧罐；`WorkshopReviewedFanVerification` 要求较大开口／挠度，这些与本次目标冲突的断言必须随行为正确迁移。
9. `OpenAnalysis()` 对缺少参考件的工况提前返回，真实包络浏览要先修改入口路由。
10. `WorkshopPlatformBuild.BuildUIReview()` 已存在；独立时间戳 `BuildFaultReview()` 和新验证参数为待新增，任务书没有把它们当作现成命令。

## 验证范围

本次为源码、资源清单和文档的静态核对。技术方法补充核对 SIMULIA 单元输出和 XFEM 官方资料，链接放在实施任务书相关章节。

文档检查结果：实施任务书的 14 个任务编号完整；实施任务书、原指导书和本记录的本地链接分别为 1／38／2 个，目标均存在。20 个现有源码锚点逐项查到，1 个 JSON 样例解析通过，三个文件的 Markdown 代码围栏均闭合。3 个 PowerShell 命令块仅经语法解析，未执行；11 个建议新增运行时文件名未与当前已有文件冲突。README 的 Git diff 空白检查通过。

上述检查证明文档引用和模板结构可用，不等于代码设计已编译、运行或验证。没有编译任务书中的 C# 设计草案，也没有执行其中的构建、测试或程序启动命令模板。

本次未改动故障代码、模型、场景或构建产物；未执行 Unity、Windows 回归、Abaqus 求解或真实 ODB 对照。14 个任务目前是实施规格，不能记为 14 项开发已完成。
