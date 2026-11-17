# 故障模块 AI 实施任务书

版本：2026-11-14，实施规格 v1。配套文档：[升级方向与 32 工况清单](故障模块升级指导书.md)。

**本文件用于让后续 AI 直接开展开发。** 已存在的文件和方法在下文明确列出；标注“新增”的类、文件、菜单、参数和协议均尚未实现。执行时先核对当前源码，若已经被后续版本修改，应以实际代码调整调用关系，不能机械覆盖。

## 0. 后续 AI 从哪里开始

首次接手，先完成 T00、T01、T02：核对基线，建立最小部件绑定与故障表现接口，然后把 TK-LEAK 接到精细储罐。交付一个完整可验收案例后，继续水泵、风机、控制柜。不要先把所有状态类、界面和 32 个案例一起重写。

| 任务 | 改造对象 | 前置任务 | 完成后应能看到的结果 |
| --- | --- | --- | --- |
| T00 | 基线、资产与调用链 | 无 | 可追溯的改前状态和真实部件清单 |
| T01 | 部件绑定、故障帧、镜头和恢复接口 | T00 | 新效果可挂在原模型上，并可逆回放 |
| T02 | 精细储罐 TK-LEAK | T01 | 故障模式仍是新储罐，局部漏损及近景有效 |
| T03 | 水泵 P-SEAL、P-CAV | T01 | 密封漏液与入口汽蚀有不同结构和现象 |
| T04 | 风机 F-CRACK、F-IMBAL | T01 | 局部自然裂面、不平衡响应及正确复位 |
| T05 | 控制柜 PLC-CONTACT | T01 | 具体带载端子局部发热及热视图 |
| T06 | 其余现有工况和 UI 收口 | T02–T05 | 16 个工况不再统一套用断裂与裂长读数 |
| T07 | 统一指标、阶段与保护状态 | 六个案例有可用样板 | 活动故障、指标和表现由业务快照驱动 |
| T08 | 16→24→32 工况 | T06、T07 | 每个新增工况具备独立证据与完整闭环 |
| T09 | Abaqus v1 包络浏览 | T00；可在视觉样板完成后独立推进 | 真实导入曲线可查看，参考云图来源保持准确 |
| T10 | Abaqus v2 全场最小接入 | T09 | 一个局部热／结构结果可逐帧查看与探测 |
| T11 | 四个设备局部算例及裂纹专题 | 对应资料具备、T10 | 求解、校核、导出、展示具有完整依据 |
| T12 | 证据式诊断、维修与复验 | T07，至少六个完整案例 | 学生根据证据判断并完成复验 |
| T13 | 当前版本验证、候选构建与交付 | 每个阶段均执行适用部分 | 当前主平台实际运行证据与清晰未完成项 |

这是一条任务依赖路线，工作状态由实际交付决定。可以缩小本轮任务范围；不得把“已写代码”直接标为“完成 Txx”。任务完成至少要求适用的编译、运行或数据验证。

## 1. 必须沿用的工程约束

- 先阅读根目录 README、AGENTS 和本次任务相关 Evidence；检查 Git 状态。已有用户修改、未跟踪资产和删除记录原样保留，不执行清理式回退。
- 使用当前 Unity 6000.3.8f1、URP、新 Input System；功能场景为 `Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`。
- 四类设备沿用已验收模型及 12／12／11／16 总成；模型故障变化限定在相关部件。保留 `.meta`、导入坐标、拆装路径、材质槽及官方小车训练。
- `WorkshopFoundationBuilder.Build()`／Build Demo 会重建并覆盖场景。此次故障开发默认不需要运行它；不得把它当作 EXE 构建入口。
- `Build/WorkshopPlatform/Viewers/WaterPumpViewer/` 是受保护交付物。开发和检查版构建不得删除、覆盖、移动它；其他现有独立展示器也需保护。
- 主业务继续以 `WorkshopDevice`、`WorkshopSimulation` 为准。学习回放不能创建或清除活动工单，不能修改小车到场许可。
- `Outline` 层 8／mask 256 保留。观察窗口使用的层 27、28 只用于其克隆模型和相机，不能扩散修改车间设备层。
- 没有真实 ODB 时继续完成接口与合成协议验证，但把真实数据回放标为未验证。不能生成假 ODB 标签、假寿命、假云图来“补齐功能”。

## 2. 已核实的调用链与容易漏掉的依赖

以下路径均相对项目根目录。括号内为当前真实方法／成员，后续 AI 应按名字定位，而不是依赖易变化的行号。

### 2.1 主车间与学习窗口使用不同实例

```text
车间：
WorkshopDevice.Start()
  → WorkshopEquipmentModel.Attach(device)
  → Initialize() → ReviewedPump/Tank/Fan/Cabinet()
  → Install() 设置场景缩放、碰撞代理、转子

当前主场景损伤：
WorkshopEquipmentModel.Update()
  → owner.FaultAge / 18
  → SetDamage(owner.Fault, progress)

设备学习窗口：
WorkshopUI.OpenEquipment()
  → WorkshopEquipmentView.Initialize(device, selectedFault, inset)
  → CreateStudioModel(device, true)
  → 另一个模型实例；设置独立灯光、相机与观察缩放

当前储罐模式切换：
WorkshopUI.Compact.cs / SetEquipmentMode(2)
  → equipmentView.ShowTankFaultModel(device, true)
  → CreateStudioModel(device, false)
  → 隐藏新储罐，显示旧教学储罐
```

只改学习窗口，主车间仍可能播放旧效果；只改 `SetDamage()`，模式切换仍可能生成旧罐。T02–T06 必须同时核对这两条路径。

### 2.2 部件、数据与历史假设

| 当前入口 | 已核实行为 | 改造时必须连带处理 |
| --- | --- | --- |
| `WorkshopEquipmentModel.Damage.cs / SetDamage()` | 新储罐直接返回；多个编码共用 `HasOpening` 和弯曲开口 | 分机制路由；支持新储罐；恢复历史损伤与读取状态 |
| `WorkshopEquipmentModel.ReviewedPump.cs / ReviewedPump()` | `assemblies[6]` 下创建 `Seal inspection fracture insert` | 取消 P-SEAL 对整个密封组的隐藏；按具名总成绑定，减少索引依赖 |
| `WorkshopEquipmentModel.ReviewedFan.cs / ReviewedFanDamage()` | 在 `F-CRACK_bulged`、`F-CRACK_opened` 间插值；F-BEAR 也用叶片弯曲 | 裂纹和轴承表现拆开；保留原叶片及剩余连接逻辑 |
| `WorkshopEquipmentView.cs / LateUpdate()` | 主镜头与近景多处直接取 `DamageSurface.GetComponent<Renderer>().bounds` | 新增可空的故障焦点接口；无可变网格的泄漏、热故障也应可聚焦 |
| `WorkshopEquipmentView.cs / SetProgress()` | 每次调用 `SetDamage()`，并调整网格层 | 防止层污染；复用同一快照；不要把粒子、测点误算成总成 |
| `WorkshopEquipmentView.cs / Update()` | 故障形变每约 0.05 秒更新，时长写死 18 秒 | 拆开低频读数与逐帧视觉；动态动作不以 20 Hz 阶梯播放 |
| `WorkshopUI.Compact.cs / SetEquipmentMode()` | 储罐切旧模型、隐藏部件按钮、写“教学模型” | 删除默认旧模型切换后，同步调整文案、部件列表与近景 |
| `WorkshopUI.Equipment.cs / UpdateEquipmentText()` | 各案例统一显示开口、弯曲、裂长、厚度及 55% 起裂 | 按可用指标生成内容；不适用字段隐藏或显示“未提供” |
| `WorkshopUI.Equipment.cs / ExportEquipmentDimensions()` | 固定输出裂纹几何 CSV 列 | 新版导出增加字段来源、有效性和单位，缺失值留空 |
| `WorkshopEquipmentModel.Geometry.cs / OnDestroy()` | 统一释放 `ownedMeshes`、`ownedMaterials` | 新资源进入现有生命周期，避免新增重复销毁方法 |
| `WorkshopUI.Analysis.cs / OpenAnalysis()` | 先读 `WorkshopAnalysisResult.Load(code)`，无参考件就提前返回 | 先建立来源路由，再决定能否打开 ODB 包络／全场窗口 |

### 2.3 已核实的部件名称

来自当前 `Assets/WorkshopDemo/Resources/ReviewedEquipment/*-parts.json` 与源码。新增绑定时仍需导出实际实例层级，检查唯一性；不要假设同名对象只会出现一次。

| 用途 | 总成或对象名称 | 定位依据 |
| --- | --- | --- |
| 水泵密封与支座 | `PMP_06_SealBearing` | pump-parts.json |
| 水泵叶轮 | `PMP_07_Impeller` | pump-parts.json |
| 水泵蜗壳／入口盖 | `PMP_04_Volute`／`PMP_05_InletCover` | pump-parts.json |
| 水泵轴承外壳／轴承 | `PMP_10_BearingHousing`／`PMP_11_BearingSet` | pump-parts.json |
| 水泵静环／动环候选名 | `PMP_06_SealBearing_StaticSealFace`／`PMP_06_SealBearing_RotatingSealFace` | `model/scripts/build_pump_detailed.py`；实施时核对导入实例 |
| 储罐筒体／支腿／仪表 | `TNK_01_Shell`／`TNK_00_Legs`／`TNK_05_Instruments` | tank-parts.json |
| 储罐壳体网格 | `TNK_01_Shell_8mmWall` | ReviewedTank() 当前绑定 |
| 储罐人孔／出口 | `TNK_04_Manway`／`TNK_10_ManwayCover`／`TNK_09_Outlet` | tank-parts.json |
| 风机叶轮／转子 | `FAN_02_Impeller`／`industrial-fan_rotor` | fan-parts.json、ReviewedFan() |
| 风机支撑／电机 | `FAN_06_Struts`／`FAN_04_Motor` | fan-parts.json |
| 控制柜端子／电源 | `CAB_02_Terminals`／`CAB_09_PowerSupply` | cabinet-parts.json |
| 控制柜散热／PLC／驱动 | `CAB_06_Cooling`／`CAB_05_PLC`／`CAB_12_VariableSpeedDrives` | cabinet-parts.json |
| 当前柜内故障焦点 | `CAB_02_Terminals_CopperPEBusbar` | ReviewedCabinet() 的 DamageSurface |

最后一项是 PE 接地排，只能证明旧代码用了这个网格作为焦点。PLC-CONTACT 的默认案例应选择实际带载端子，不能仅为复用焦点就宣称 PE 排正常承载负载电流并过热。

## 3. T00：建立改造基线

**改动范围：** 本任务以只读盘点和新增 Evidence 为主，不重建场景、不重新导入全部模型。

按顺序执行：

1. 阅读 README、AGENTS、两份升级文档和相关模型接入记录；执行 `git status --short`、`git diff --stat`，记录当时 HEAD。
2. 记录 Unity 版本、当前场景、四个设备 ID、模型资源路径、总成数量、现有故障数量和结果来源。
3. 在隔离的模型实例上导出层级与绑定候选：相对路径、名称、类型、Mesh 顶点／三角面数、材质槽、局部与世界 bounds、父子关系、blend shape 名称。编辑器临时实例检查后销毁，不保存到用户场景。
4. 分别截取六案例的观察模式、故障初始态、中间态、末态，以及近景。测试输出使用新的时间戳目录。
5. 对本轮会改的资产保存原始文件哈希；模型修改使用新派生资产或源文件副本。对交付展示器记录路径、长度、哈希，供最终核对。
6. 写出每个案例的当前缺口与本轮改动列表。没有安装可运行 Unity 或无法启动时，先完成静态盘点，运行部分明确标记未完成。

**输出建议：** `Evidence/fault-upgrade-<时间戳>/baseline.md`、`hierarchy.json`、`protected-files.json`、`before/`。这些是后续任务产生的文件，当前尚不存在。

**通过条件：** 四类设备的目标总成可唯一定位，六案例现状可追溯，目标部件找不到时给出明确条目，而不是退回设备中心坐标。

## 4. T01：最小公共接口，不先重写整套业务

### 4.1 新增文件与责任

建议新增以下运行时文件，放在 `Assets/WorkshopDemo/Scripts/`；各自生成并保留 `.meta`。可以合并小文件，但以下责任必须明确。

| 新增文件 | 最小责任 |
| --- | --- |
| `WorkshopFaultProfile.cs` | 案例的部件 ID、表现类型、阶段、可用读数、教学时间和参数来源 |
| `WorkshopFaultBinding.cs` | 将部件 ID 解析为实际节点、渲染器、局部锚点与焦点范围 |
| `WorkshopFaultFrame.cs` | 一帧输入、阶段和可选读数；用于统一表现与界面 |
| `WorkshopFaultPresenter.cs` | 对当前模型实例应用一帧效果、恢复原状态、释放自有资源 |
| `WorkshopEquipmentModel.Faults.cs` | 现有 partial 类的适配层，保存 Presenter 并对外提供安全的焦点／读数接口 |

第一轮继续保留 `WorkshopFaultCatalog.All/For/Find`、`WorkshopFault` 及现有编码。新增 Profile 以故障 Code 关联，不先全量迁移 ScriptableObject 或更改所有调用者。

配置建议位于 `Assets/WorkshopDemo/Resources/WorkshopFaults/`，绑定与故障参数独立于原 `*-parts.json` 拆装清单。原拆装配置不承载新损伤逻辑，避免一次更新影响所有展示器。

### 4.2 接口草案

下列为设计草案，不是已经存在或可直接复制编译的源码。实施 AI 应补齐类型和生命周期，并让调用者统一使用。

```csharp
// 输入中的所有时间都明确传入，不能在求值函数里另读 Time.time。
FaultFrame EvaluatePreview(string faultCode, float normalizedProgress, float motionSeconds);
FaultFrame EvaluateLive(WorkshopDevice device, FaultSnapshot snapshot);

// 对同一输入重复调用，模型结果一致；不能按调用次数累加位移。
void Apply(FaultFrame frame);
void ResetVisual();
bool TryGetFocusBounds(out Bounds worldBounds);
IReadOnlyList<FaultReadout> GetReadouts();
void DisposeOwnedResources();
```

`FaultFrame` 至少包含：`faultCode`、`stageId`、`normalizedProgress`、`motionSeconds`、`geometrySource`、`telemetrySource`、表现强度、是否有几何开口以及读数集合。

`FaultReadout` 至少包含：稳定键、中文名称、数值、单位、`available`、`source`、是否教学放大。无裂纹的案例不提供裂长；数据缺失与真实零值必须可区分。跨 JSON 的数据优先使用可序列化字段和数组，不把运行时 Transform 引用写进 JSON。

### 4.3 部件绑定格式

以下是格式样例，`anchorCalibrated=false` 表示尚未定位，解析器必须拒绝其作为正式漏点。零坐标不是已经核实的表面位置。

```json
{
  "schemaVersion": 1,
  "deviceKind": "Tank",
  "bindings": [
    {
      "id": "tank.shell.frontLeak",
      "assemblyName": "TNK_01_Shell",
      "rendererNames": ["TNK_01_Shell_8mmWall"],
      "anchorLocalM": {"x": 0, "y": 0, "z": 0},
      "normalLocal": {"x": 0, "y": 0, "z": 1},
      "anchorCalibrated": false
    }
  ]
}
```

正式配置补充相对路径、资源版本／哈希、局部焦点尺寸与参数依据。锚点先在无显示缩放的设备局部坐标确定，再通过目标 Transform 转到世界坐标。储罐车间的 0.60 缩放和观察室自动缩放都不改变配置中的物理尺寸。

绑定失败应使该案例显示“故障部件绑定缺失”，记录设备和部件 ID；不要自动使用 `(0,0,0)` 或旧教学模型掩盖错误。未迁移的旧工况可以继续走明确的旧路径，但必须知道是哪一项未迁移。

### 4.4 适配现有模型和相机

1. 在 `Initialize()` 创建完 reviewed 模型后解析绑定；同一个资源定义可用于车间实例与观察室实例，不能跨实例缓存 Transform。
2. 在 `SetDamage()` 开头处理 `fault == null` 的恢复；已迁移 Code 交给 Presenter 后结束当前分支，避免同帧再执行旧形变。
3. 新增 `TryGetFaultFocusBounds()`。`WorkshopEquipmentView.LateUpdate()` 先取故障焦点；无焦点时取选中总成／整机 bounds。所有 `DamageSurface` 使用点同步加明确适用判断。
4. `SetProgress()` 求值并应用，不改变 `WorkshopDevice.Fault`。`SetEquipmentMode()` 退出故障预览时恢复视觉，不调用 `SetFault(false)`。
5. 近景应能显示多个 Renderer 和粒子，不再只依赖一个 `MeshFilter`。观察室内记录原层后临时设置近景层；切换案例或关闭窗口时恢复。车间实例不使用近景层策略。
6. `UpdateEquipmentText()` 和 CSV 导出从可用读数生成；修改测量显示时连带调整“预设起裂”“最终裂口”等按钮名称与可见性。

### 4.5 恢复、顺序和所有权

- 初始化时记录原始 Mesh、材质参数、Renderer.enabled、局部变换和观察室层；之后的形变从原始数据求值，不用 `+=` 累积。
- 可变 Mesh 必须实例化并登记到自有资源列表；清理只销毁自己创建的对象，不能 Destroy 资源目录中的共享材质／网格。
- 资源只能有一个释放者：登记到模型 `ownedMeshes/ownedMaterials` 的对象由已有 `OnDestroy()` 释放；Presenter 的清理函数只处理自己持有且未登记的效果对象与资源，不能二次销毁同一网格或材质。
- 拆装仍由原 `SetDisassembly()` 控制总成路径；旋转仍由已有转子逻辑控制。附加振动使用独立子节点或明确的最终偏移组合，避免两个 Update 争写同一 Transform。
- 参数不变时不重复查全层级、分配数组或重建材质。网格需要变化时才更新；动态旋转和振动视觉逐帧计算，文字可低频刷新。
- 采用固定种子和可重建的粒子时间状态，确保拖到同一时间点得到一致漏液／汽泡画面；向后拖动先清除旧粒子再重建。
- 初版故障与拆装互斥：进入故障模式先沿原路径回装，完全回位后才启用播放；在回装中提前点击时排队开始或明确提示。退出故障模式后恢复完好几何，禁止直接硬设总成位置破坏既有动画。

**通过条件：** `p=0→0.75→0.25→1→0` 与直接设置同一 p 的结果相同；关闭重开、切换设备、连续两次重置后没有残留、异常或资源持续增长。该条件需覆盖 Mesh、粒子、材质、层和相机。

## 5. 六个重点案例的具体任务

本章的进度比例和曲线是教学编排，不是物理求解结果。初版可沿用 18 秒预览以减少界面改动，但从 Profile 读取时长；不得把 18 秒解释为失效预测。

### T02：新储罐上的 TK-LEAK

**本轮目标：** 故障演示始终显示已验收的新储罐，漏点位于新筒体局部，支持近景、湿润、滴漏和液位变化说明；不出现整条罐壁撕开。

**修改现有文件：** `WorkshopUI.Compact.cs`、`WorkshopEquipmentView.cs`、`WorkshopEquipmentModel.Damage.cs`、`WorkshopEquipmentModel.ReviewedTank.cs`，以及 T01 新接口。

**实施步骤：**

1. 解析 `TNK_01_Shell`／`TNK_01_Shell_8mmWall`，在面向巡检通道的可见局部确定漏点，记录位置、法线和焦点范围。不要用壳体整体 bounds 作为细小漏点的近景范围。
2. 将 `SetEquipmentMode()` 默认路径中的 `ShowTankFaultModel()` 替换为同模型的故障模式激活。第一轮保留旧方法供未迁移案例兼容时，必须显示明确的旧模式标记；完成储罐四案例迁移后默认不再走旧方法。
3. 改 `SetDamage()` 对 `ReviewedTankModel` 的无条件早退，使 TK-LEAK 能进入新 Presenter；其他储罐工况暂不冒充已迁移。
4. 给当前实例添加局部湿润层、滴漏发射器、地面接液区和近景锚点。漏液受重力、表面方向和地面高度影响；相机旋转不改变漏点。
5. 默认机制选择“局部壁面穿孔漏损”。若要近距离显示穿孔，派生一个只修改对应壳体局部的缺陷网格，保留内外壁和孔边厚度。加载派生网格只替换该壳体实例，不隐藏筒体总成，不重建整罐。只有湿润／滴流而没有孔几何的中间交付，需明确标记“外观演示已完成，穿孔几何待完成”。
6. 阶段建议：0% 完好；0–25% 缺陷定位／初始湿润；25–65% 形成滴漏与水迹；65–100% 稳定漏流和积液。孔径变化若无物理模型按预设几何说明，不显示为真实腐蚀速率。
7. 预览液位使用同一案例求值器的教学曲线。T07 接入活动状态后改读设备快照；不能让预览滑条降低车间实际液位。
8. 更新储罐模式标题、部件按钮与读数：显示漏点、阶段、液位及漏率来源；没有漏率模型时显示“漏率未求解”。裂长／裂深／弯曲幅值不出现。

**必测结果：** `DisplayModel == Model`、`ReviewedTankModel == true`，场景中不激活 `Tank fault demonstration`；总成数仍为 12；四角度漏点贴合；车间缩放 0.60 和观察室缩放下位置一致；退回观察时原材质、网格、层恢复。

**旧验证同步：** `WorkshopCompactUIVerification.VerifyTank()` 当前明确要求 `DisplayModel != Model`、旧模型开口。改为上述同模型与局部漏损断言，保留旧记录，不能为了通过旧断言继续使用旧罐。新增效果不应算入原始资产的网格／三角面统计，检查名义模型子树或单独记录效果开销。

### T03-A：P-SEAL 改为密封副失效和局部漏液

**修改现有文件：** `WorkshopEquipmentModel.ReviewedPump.cs / ReviewedPump()`、`WorkshopEquipmentModel.Damage.cs / SetDamage()`；配套部件绑定与泄漏 Presenter。

**实施步骤：**

1. 从实际实例确认 `PMP_06_SealBearing` 内的静环、动环、密封腔和支撑；不要只依赖 `assemblies[6]`。静／动环候选名见第 2.3 节。
2. 新路径停止创建／激活 `Seal inspection fracture insert`，停止为 P-SEAL 隐藏 `reviewedSealRenderers` 整组。先确保静环、动环与支架都仍可见。
3. 默认机制为密封端面劣化或局部失去接触。使用环面磨痕、局部损伤纹理及小间隙表现；需要放大间隙时单独显示“教学放大”，不按放大值报告真实间隙。
4. 漏点位于轴穿过密封区域的合理出口，液体沿局部表面汇集后滴落。不要在泵壳外侧凭空发射水平蓝线。
5. “看密封”只对遮挡密封的指定部件做可逆剖视／隐藏，保留上下文；不要为看见静环把整个泵体和支座全部删除。
6. 阶段建议：0% 完好；0–30% 接触面异常近景；30–60% 局部湿润、间歇滴漏；60–100% 漏液增强／保护后残余滴漏。业务注入 P-SEAL 的原有联锁行为在 T07 前保持。
7. 从默认 P-SEAL 的 `HasOpening` 通用分支移除；可另做明确命名的“密封环崩裂”子情景，不改变普通泄漏的默认含义。

**必测结果：** 完好和故障期间同一个 12 总成泵；原密封组可见，泄漏位置正确，轴／动环与已有旋转控制一致；回放不撕裂支撑；退出后静／动环及隐藏状态恢复；原水泵独立展示器哈希不变。

### T03-B：P-CAV 改为叶轮入口汽蚀教学

**修改范围：** 绑定 `PMP_05_InletCover`、`PMP_07_Impeller` 与 T01 Presenter；T07 再联动 `WorkshopDevice.Sample()` 的过程数据。

1. 在叶轮入口附近定义汽泡显示区域和流向，制作剖视／透视开关；外部视图只保留可观察的声音、轻微局部振动和仪表变化。
2. 粒子或实例化汽泡仅在流道可见范围内出现、向下游移动并缩小消失；该效果标为“汽蚀机理示意”，不是 CFD 输出。
3. 预览强度随进度增加，驱动同一来源的供液不足、压力／流量波动与教学声音强度。暂未接入 T07 时，预览读数标记为独立教学预览。
4. 壳体和叶片不随汽泡动画开裂。长期汽蚀侵蚀另用 P-IMPELLER 案例解释，不能在 18 秒内默认把叶轮蚀穿。
5. 静音使用现有 M 音频策略；窗口关闭、暂停、设备停止或切换时同步处理声音与汽泡。

**必测结果：** 与 P-SEAL 的漏点、声音、指标和镜头明显不同；正常转速演示与停止状态一致；隐藏剖视后汽泡不穿过壳体显示；重播画面可复现，M 静音有效。

### T04-A：F-CRACK 局部裂纹与真实裂面

**修改现有文件：** `WorkshopEquipmentModel.ReviewedFan.cs / ReviewedFanDamage()`、`WorkshopEquipmentModel.Damage.cs`；若重做形态，新增局部资产脚本。现有 `model/scripts/build_fan.py` 提供历史 shape key 生成依据，但不要直接重跑整机生成覆盖已验收修订。

1. 先查看原 `F-CRACK_bulged`、`F-CRACK_opened` 形态及当前断面。确认裂纹两侧、剩余连接、UV 和内部材质，再决定重做局部形态还是增加中间形态。
2. 初版固定为“局部裂纹、保留连接、保护停机”的案例。起裂位置与案例说明一致，不默认完全甩断。
3. 用局部影响权重控制形变，远离损伤区权重衰减为零。先让细裂缝和局部张开可见，降低全叶片大幅弯曲；不要为满足旧测试的 30 mm 挠度阈值而继续夸张形变。
4. 教学阶段建议：0–20% 完好／定位；20–35% 预设局部起裂；35–80% 裂面逐步发展和张开；80–100% 局部末态保持。阶段和参数放进 Profile，不再全局固定 55% 起裂。
5. 内部裂面应有独立材质槽，保留厚度和两侧几何，不以黑线替代真实开口。完好时裂面闭合，不出现闪烁或漏光；禁止对裂纹两侧节点误合并。
6. FBX 会拆分或重排顶点。若使用离线权重／裂面标识，导入后重新建立对应并核对几何签名，不能直接复制 Blender 顶点索引。沿用并改进现有“重合原位＋不同开口位移”的裂缝配对思路，避免每帧执行全顶点两两查找。
7. 删除 F-BEAR 用叶片弯曲表示轴承抱死的耦合。F-BEAR 转向轴承／电机支撑位置，至少实现正确的停转、局部热提示和可用近景。
8. `Detached`、剩余连接说明、读数与按钮按配置同步。没有经定义的裂纹长度测量就不补一个由开口宽度推算的裂长。

**必测结果：** 0、20%、35%、60%、80%、100% 及阶段相邻帧可单独观察；没有提前裂缝、突然全断、薄片消失或跨裂面穿插；退出后原叶片顶点恢复，11 总成拆装和转子轴向保持。

**旧验证同步：** `WorkshopReviewedFanVerification` 中 `OpeningMm > 5`、`DeflectionMm > 30` 等阈值是旧预制形态断言，应改为局部位移上限、目标开口范围、剩余连接与恢复误差。保留真实导航、坐标和拆装路径检查，不整段删除测试。

### T04-B：F-IMBAL 不平衡

1. 绑定 `FAN_02_Impeller` 和支撑部位；选择“叶片附着质量／质量偏心”的明确教学机制，必要时用可关闭标记说明偏心。
2. 旋转相位从当前转速与输入时间计算；附加小幅轴心轨迹或支撑相对运动。偏心、相位和振动幅度进入配置，不把车间整台风机及导航碰撞体整体移动。
3. 默认名义比例，设置独立“振动放大”开关。屏幕幅度放大不改变设备振动读数。
4. 诊断面板提供同转速完好／不平衡对照。若绘制教学频谱，应标记为信号模型；没有传感器／采样依据时不能显示“实测”。
5. 停转时暂停强迫振动或采用明确衰减过程；回装与转子旋转各保留唯一控制器。

**必测结果：** 设备工位、巡检点和碰撞代理不变；局部运动随转速和播放状态一致；不开裂，复位后无位置漂移。

### T05：PLC-CONTACT 端子过热

1. 在 `CAB_02_Terminals` 中选择一个真实带载端子，核对线缆连接与局部表面；绑定位置和说明一起保存。不能直接沿用 PE 排作为负载接点。
2. 正常外观保留原涂装、标识、线色和线槽。新增局部热视图覆盖层及温度测点，教学热层可关闭。
3. 阶段建议：0–25% 正常／轻微温升；25–70% 局部升温并向附近扩散；70–100% 温度预警／保护末态。没有材料损伤依据时，末态也不默认烧黑、起火或电弧。
4. 初版热区可由配置权重和温度曲线表示，标注教学示意；T11 得到真实局部温度场后增加数据源选项，不能仅替换来源文字。
5. 停机或隔离后的冷却使用时间曲线，不瞬间归零。预览中的保护事件不修改活动工单。
6. `WorkshopEquipmentView` 近景显示选中端子与相邻线缆，绑定不足时提示缺失；不再把整个柜内的颜色闪烁当作端子定位。

**必测结果：** 开关热视图只改变辅助显示；热点停留在同一端子；关闭窗口后材质恢复；拆装 16 总成、柜门退出再旋转路径和独立展示器保持可用。

## 6. T06：把剩余现有工况和 UI 一起收口

六个样板完成后，再将公共能力用于其余现有工况。以下是最低改造内容，不要求都制作可变形网格。

| 工况 | 必须修正／补齐的表现 | 不再沿用的表现 |
| --- | --- | --- |
| P-HOT | 明确泵体或轴承座热源、温升与保护；热辅助层可关闭 | 整机无差别发红、固定曲线与动画阶段矛盾 |
| P-BEAR | 轴承总成近景、局部温振与正常／故障对比 | 全泵平移作为唯一证据 |
| TK-PRESS | 压力、保护动作与可选形变放大；默认无结构破裂 | 通用罐壁贯通开口 |
| TK-LEVEL | 新储罐上的液位／仪表示值和进出料失衡提示 | 卡阻符号或裂纹解释高液位 |
| TK-SUPPORT | 新支腿／底板局部变形或支撑偏差，限定一种案例机制 | 默认断腿、整罐像橡胶一样脉动 |
| F-BLOCK | 入口／出口受阻位置、风量变化，明确转速控制条件 | 仅把叶轮减速当作风道阻塞的充分证据 |
| F-BEAR | 轴承或支撑局部温升、转动阻力与停止 | 叶片弯曲替代轴承抱死 |
| PLC-HOT | 定位散热组件／滤网，展示器件温升及散热路径 | 与端子过热完全相同的热点 |
| PLC-LOW | 电源、测点电压、负载和输出反馈 | 不相干的裂缝、电弧 |
| PLC-COMM | 工业网络或 PLC 节点、通信质量与控制反馈 | 把通信中断默认解释为断电或结构损伤 |

**界面修改清单：**

1. `UpdateEquipmentText()` 从 `FaultFrame` 生成阶段、部位和读数；`ComponentName` 不再只按 DeviceKind 返回一个固定部位。
2. 仅 F-CRACK 等确有预设起裂的案例显示“起裂时刻”；其他案例显示 Profile 定义的“异常开始”“保护动作”等关键节点。
3. `equipmentTransport` 使用 Profile.durationSeconds，不拼接固定 `/18.0 s`。
4. `ExportEquipmentDimensions()` 可保留旧入口名作兼容，输出升级为案例读数：`schemaVersion,deviceId,faultCode,stageId,previewSeconds,metricKey,value,unit,source,available,visualScale`；一个读数一行，缺失 value 留空。
5. 观察模式完整恢复原设备；故障模式只展示当前案例的工具。无裂纹案例不显示 0 mm 裂长来占位。
6. 所有已迁移案例接入主场景时，关闭与其重复的旧 `WorkshopFaultVisual` 效果。未迁移工况可保留旧符号并标记；告警标记本身继续由业务控制。
7. 完成四个储罐案例后，移除默认调用 `ShowTankFaultModel()` 的路径；旧资产是否归档另行处理，不能顺手删除用户资源。
8. 更新说明与知识资料时使用真实完成状态：有专属效果但没有 FEM 就写“教学演示＋未提供求解”，不要为了按钮可用强塞参考云图。

**通过条件：** 原有 16 个工况逐一打开、播放、近景、复位，无错误部件和固定裂纹文案；用户可从现象或读数区分六个样板；双分辨率布局没有新增遮挡。

## 7. T07：统一故障阶段与指标，保持当前闭环兼容

### 7.1 先保留直接注入的原有语义

当前 `TriggerFault(device, code)` 会立即建立告警，`ApplyFault()` 根据 `Fault.Trips` 决定是否运行。第一轮视觉改造不改变这一行为，避免告警、巡检和旧用例一起失效。

进入 T07 后，可增加有明确名字的“渐进实训注入”，例如新增 `TriggerProgressiveFault()`。默认 `TriggerFault()` 仍采用直接进入活动故障的策略；两个模式在 Profile 和 UI 中明确选择。不能把所有故障默默改成 18 秒后才保护。

### 7.2 数据结构最小改造

建议让 `WorkshopDevice` 持有一个新的 `FaultInstance`，保留 `Fault` 作为指向现有定义的兼容读取属性。实例至少记录：编码、实例 ID、发生业务时间、注入模式、阶段、严重度、是否已触发保护、种子、快照版本。

每次采样生成 `FaultSnapshot`，至少提供设备运行许可、阶段、温度、转速、振动、压力、流量、液位、电压、通信质量以及每个量的可用性和来源。已有公开数值属性可继续返回快照字段，减少 UI 和助手一次性改动。

改造顺序：

1. 在 `WorkshopSimulation.Update()` 的既有采样节奏中传递统一业务时间；新增测试可注入时间的纯求值方法。
2. `WorkshopDevice.Sample()` 不再把 `Time.time`、调用参数 elapsed 和预览进度混用；从统一时间计算实例 age。
3. 将旧固定参数封装为“LegacyTeaching”求值器，先保证所有未迁移案例行为一致。
4. 六个案例逐个替换求值器；温度、漏液、液位等的阶段关系写在同一处，UI 与 Presenter 不各自发明公式。
5. `IsRunning` 对已迁移实例读取实际保护状态；`WorkshopFault.Trips` 保留为定义的保护策略。`TriggerFault()` 的通知文字也改读实际状态，不能只拼 `fault.Trips`。
6. 保护事件、告警阶段变化触发 `Changed`；连续指标仍通过 `Sampled` 更新。不要每帧重复创建告警。
7. `SetFault(false)` 清空实例并恢复 `runningBeforeFault`；`ResetState()` 清理所有残留，连续重置可重复。
8. `WorkshopEquipmentModel.Update()` 改读快照，取消自行计算 `FaultAge/18`；`WorkshopFaultVisual` 同样消费快照或明确退回 Legacy 路由。

### 7.3 过程关系的最低要求

以下是实施约束，不提供未经标定的真实阈值：

- 水泵泄漏强度、入口工况和保护状态来自同一案例；停泵后是否有残余泄漏由介质条件决定，不一律归零。
- 储罐液位变化与进出流量／漏流的假设相容；若使用 `dV/dt = Qin - Qout - Qleak`，注明容积与液位换算假设及单位。模型缺少完整容积数据时，不把线性百分比曲线称为精确液位预测。
- 局部温度按升温／冷却状态延续，停止旋转不代表立刻回到环境温度。
- 振动读数与视觉放大分离；三维显示用采样快照和明确相位时钟平滑，不能反过来从屏幕位移算出“测得振动”。
- 通信中断可令通信质量不可用／为零，但控制输出如何保持或停止需由所选策略决定，不能自动修改无关温度、电压。

**必测用例：** 同一业务时间重复求值一致；原本运行／人工停机设备分别注入并验收；直接注入与渐进注入各符合其策略；快进预览不会修改活动实例；故障重复注入仍被拒绝；验收前故障不清除；驳回后可重新派工。

## 8. T08：新增故障按批次交付

32 个候选定义和机制沿用主指导书第 3 节。为了让后续 AI 明确第一批做什么，建议固定如下顺序。

| 批次 | 每台新增两项 | 当前四台可用目标 | 全历史目录的预期数量 |
| --- | --- | --- | --- |
| 第一批 | P-ALIGN、P-INLET；TK-CORROSION、TK-FLANGE；F-LOOSE、F-RUB；PLC-OVERLOAD、PLC-SENSOR | 24 | 32，含历史输送线／电机 8 项 |
| 第二批 | P-IMPELLER、P-DRY；TK-SENSOR、TK-VENT；F-BLADE、F-REVERSE；PLC-IO、PLC-DRIVE | 32 | 40，含历史输送线／电机 8 项 |

这些数字仅在保留原目录且无其他新增的基线上成立。实际验收从当前设备适用编码计算，不能把 `All.Length` 当成现役案例数量。

**每新增一个案例必须同时提交：**

1. 稳定编码、定义和 Profile；保留现有编码和旧案例顺序。
2. 正确的部件绑定、观察／故障镜头、独特现象或诊断证据。
3. 指标／阶段／保护策略及其参数来源，至少一个正常对照。
4. 分析路由：参考件、真实结果、过程模型或明确无结果；不存在时返回可读状态。
5. 知识说明与维修／复验要点。
6. 正常选择、注入、重复拒绝、播放、重置和工单闭环证据。

**UI 必须连带检查：** `OpenEquipment()`、`OpenAnalysis()` 当前直接用 `For(kind)[analysisMode.value]`。切换设备、重载配置和数量变化后应按 Code 维护选择，索引做边界检查；缺少选项时显示空状态，不能出现越界。

**禁止的快捷交付：** 只新增菜单名称；将四条固定参数复制并换色；对新故障统一加载 F-CRACK 云图；删除旧电机／输送线定义以凑总数；一开始就放开同设备任意故障叠加。

## 9. T09：先让真实 Abaqus 包络可以被看见

### 9.1 当前接口与改动位置

已有接口：`WorkshopAbaqusResult.Load(device, fault, out source)`、`Parse()`、`At(seconds)`；已有导入器为 `WorkshopAbaqusImport.Import()`，v1 路径为 `Assets/StreamingAssets/WorkshopFaultResults/<设备>/<编码>.json`。

新增建议：`WorkshopUI.Abaqus.cs`、`WorkshopAbaqusCurveGraphic.cs`、`WorkshopAnalysisRouter.cs`。保留现有参考云图窗口，新增 ODB 包络视图，不把包络数值涂到参考件网格上。

### 9.2 明确修改步骤

1. 将 `OpenAnalysis()` 中“先加载参考件、失败即返回”的逻辑改为先查当前 Code 可用来源。
2. 来源列表区分 `ReferenceField`、`AbaqusEnvelope`、后续的 `AbaqusField` 和 `Unavailable`。页面主动选择来源，不能导入一个文件就悄悄把所有分析替换。
3. 学习页通过当前设备 ID＋选中故障 Code 独立读取结果，不要求先注入故障。活动设备的 `AbaqusResult` 仍属于活动实例，不因浏览别的案例被替换。
4. 包络页显示来源、作业、步、实例区域、分析说明、ODB 哈希、时间范围与可用通道。
5. 应力／位移／温度分开显示单位与坐标轴，不能把三个单位混在一条无说明的 Y 轴。选中时间显示原始帧点和插值状态。
6. 允许使用现有 `At()` 在物理时间轴上做线性插值，超过范围钳制；区分原始帧值与插值值，保留独立区域极值说明。
7. 只有一个有效帧时显示单点结果；只有温度时不出现伪应力／位移零曲线；无文件、校验失败、设备不匹配各有不同状态。
8. 文件已经更新而窗口仍打开时，提供显式重新加载或按文件版本更新缓存；不要对每个刷新帧重复读 JSON。

### 9.3 验证和真实数据边界

原有 `Tools/Abaqus/test_export_fault_odb.py` 是合成对象的协议测试，后续修改导出器时可执行：

```powershell
python -m unittest discover -s Tools/Abaqus -p "test_export_fault_odb.py" -v
```

增加纯温度、单帧、不等时间间隔、错绑定、缺字段、损坏哈希格式、文件超限、重复时间和非有限值用例。合成样例放测试目录或测试内存，生产菜单不得将其显示为真实 ODB。

真实数据验证至少选择一份可追溯 ODB，对照 Abaqus 的同一步、同一区域与相同时刻极值。没有该文件时交付“协议与 UI 已完成，真实 ODB 对照待完成”，不要继续在正式结果目录生成假文件。

**通过条件：** 新包络页面实际可进入；无 FEM 参考件但有合法 ODB 包络的案例也可查看；现有 scikit-fem 页面来源不变；活动告警不受分析浏览影响。

## 10. T10：全场结果协议按最小范围扩展

### 10.1 首个交付范围

首个全场样板选一个局部热模型：固定网格、已确认的节点温度、一个实例、少量时间帧。先支持明确列入白名单的一次四面体热单元及其外表面，再增加结构位移、应力、二次单元和裂纹。

旧参考算例中存在 C3D10，不能为了省事丢掉中间节点并宣称保留二次场精度。首次遇到未支持的单元类型，应拒绝并报告类型；扩展时为该类型实现表面构造与场插值验证。

建议新增文件（均尚未实现）：

- `Tools/Abaqus/export_field_odb.py`：全场导出，独立于 v1。
- `Assets/WorkshopDemo/Scripts/WorkshopAbaqusFieldResult.cs`：协议、校验与数据读取。
- `Assets/WorkshopDemo/Scripts/WorkshopAbaqusFieldView.cs`：独立场视图与探针。
- `Assets/WorkshopDemo/Editor/WorkshopAbaqusFieldImport.cs`：验证整个结果包并导入。
- `Assets/StreamingAssets/WorkshopAbaqusFields/<device>/<fault>/<caseId>/`：通过验证的正式包；尚无真实结果时不创建冒充内容。

### 10.2 包结构与字段契约

```text
<caseId>/
  manifest.json             元数据、单位、字段列表、帧索引、文件哈希
  mesh.json                 原节点标识、坐标、连接关系、表面映射
  frames/frame-000000.json  当前帧的可用场量
  frames/frame-000001.json
```

第一版可以用分文件 JSON，确认正确后再增加压缩或二进制缓存；格式优化不能改变语义。

| 文件 | 必须字段和规则 |
| --- | --- |
| manifest | `schemaVersion=2`、`source`、`deviceId`、`faultCode`、`caseId`、`componentId`、模型版本、作业／步／区域、ODB 哈希、导出器与求解器版本、分析说明、单位、文件清单及哈希 |
| 分析轴 | `axisKind` 明确为 physicalTime／loadParameter／frequency／mode／cycles；配套 `axisUnit` 和每帧 `axisValue`；只有真实物理时间轴使用秒播放 |
| 坐标 | `coordinatesSpace`、源坐标基与长度单位、导出转换说明、展示用变换；声明位移与张量分量所在坐标系 |
| mesh | N 个节点的实例名＋nodeLabel、3N 坐标、单元类型／elementLabel／连接关系、3K 表面三角索引、每个三角的原实例／单元／表面对应 |
| frame | `frameIndex`、`axisValue`、`meshId`、字段清单；例如 `temperatureC` 长 N、`displacementMm` 长 3N；不存在的字段不生成零数组 |
| 字段说明 | 名称、分量顺序、单位、位置（节点／积分点／单元节点等）、平均／外推方法、可用性；与探针信息保持一致 |

原节点号可能不连续，不同实例可重号。必须通过 `(instanceName,nodeLabel)` 建表，不能用 `nodeLabel - 1` 直接当数组索引。单元标签也按原标识保留。

### 10.3 导出顺序

1. 用实际 Abaqus Python 环境只读打开 ODB，核对指定步、实例／区域、请求字段与单元白名单。
2. 读取原节点与单元并建立映射，保留必要的实例变换。确认导出接口返回的坐标语义；使用一个带平移和旋转的实例做对照，避免重复变换。
3. 固定四面体样板按面键统计内外面，只导出区域外表面。面朝向和法线在坐标变换后检查；不得将相邻薄壁两面按距离合并。
4. 对节点温度按 nodeLabel 映射；节点位移保留向量。不得用遍历顺序假设不同字段具有相同排序。
5. 应力阶段保留原积分点／截面点标识与数值。若表面显示采用外推或平均，另存显示数据及方法，探针可追溯原值；不要复制区域最大值给每个表面点。
6. 坐标轴变换作用于位置与向量；应力分量需要相应张量变换，不能只交换位置的 Y/Z。显示缩放单独处理。
7. 输出实际存在的帧、字段、来源与哈希。拒绝静态伪时间冒充物理秒；v1 的严格规则保持。

不同输出位置和平均方式的含义以 [SIMULIA 单元输出文档](https://docs.software.vt.edu/abaqusv2026/English/SIMACAEOUTRefMap/simaout-c-dboutput-elementoutput.htm)为依据。具体 API 和输出变量以实际安装版本及该作业可用字段为准。

### 10.4 导入、显示与异常处理

1. 先校验 manifest、绑定、完整文件清单和哈希，再分配大数组；校验数组长度、索引范围、有限值、单位和帧轴顺序。
2. 初始预算建议：manifest 不超过 1 MiB、单个数据文件不超过 32 MiB、结果包不超过 256 MiB；同屏缓存限制当前帧与相邻帧。数值是实施起点，最终按主平台内存实测调整并写配置，不直接删除上限。
3. 结果包内文件只能位于包目录中。导入完成后再切换到新包，避免部分复制成功却被页面读取；旧包备份，`.meta` 保留。
4. 固定拓扑样板先提供逐帧切换，时间插值作为后续显式选项。变形倍率只改变显示坐标，读数仍使用原结果。
5. 首版在独立局部视图中显示；点整机部件跳转对应算例，不立刻给整台高精度美术网格投色。
6. 色标、单位、原始／平滑显示、来源、当前帧、部件范围必须可见。完好和缺陷对比可用同一色标；未建立成对算例时不要自动制造“完好”结果。
7. 对无数据、损坏文件、不支持字段、没有对应部件等情况显示原因，不退回另一套来源却继续保留 Abaqus 标题。

### 10.5 裂纹全场单独立项

通过常规热／结构样板后，才扩展裂面缓存或拓扑分段。XFEM 存在附加不连续描述，普通节点坐标和位移不足以保证重建真实裂口；应确定裂面导出或预处理表面方案后再编码。[SIMULIA XFEM 文档](https://docs.software.vt.edu/abaqusv2026/English/SIMACAEANLRefMap/simaanl-c-enrichment.htm)

需要明确处理：裂缝两侧独立顶点、界面法线、剩余连接、单元失效或删除后的活动状态、帧间 meshId 变化及禁止跨拓扑变化插值。所需裂面／损伤字段不存在时，将该算例标为不支持裂纹回放，不用预设动画顶替求解帧。

**通过条件：** 一份真实局部结果完成源软件与 Unity 的三位置、三帧对照；非连续标签、实例变换、单位转换、缺字段和坏包被覆盖；变形放大不改变读数；原 v1 和 scikit-fem 页面继续可用。

## 11. T11：真实算例应怎样交给后续 AI 做

### 11.1 一例一个档案，不直接给整机 FBX 自动套材料

建议建立 `AnalysisCases/<caseId>/`，其中包含输入来源、建模脚本或输入文件、参数表、求解日志、导出配置与校核说明。ODB 等大文件可以独立归档，但该目录需记录实际存储位置和哈希。

每个案例先填写以下输入清单：部件尺寸／厚度、材料与温度范围、连接与约束、载荷／热源、分析目的、步骤类型、输出字段、网格策略、对照基准、允许结论、尚无依据的参数。

没有资料时：把缺口写入 `missing-inputs.md`，可继续做有明示假设的教学参考算例和工具接入，但不得将假设变成实测参数。真实工程算例只在输入可追溯时验收。

### 11.2 四例的最小实施路线

| 案例 | 最小模型和步骤 | 第一次必须导出的东西 | 首次校核 |
| --- | --- | --- | --- |
| `PLC-CONTACT-local-thermal` | 指定端子及邻近导体／绝缘区域；先明确等效热源，具备电参数后再做电热耦合 | 节点温度、若有真实瞬态则含物理时间；热源、散热边界与测点 | 热量平衡、节点温度对照、热源和接触参数敏感性 |
| `TK-CORROSION-local-static` | 名义罐壁局部及明确减薄区；边界来自简化依据或整体模型 | 位移、应力及输出位置、反力、正常／减薄工况 | 载荷／反力、壁厚变化趋势、网格敏感性；不导出漏率结论 |
| `P-SEAL-local-contact` | 指定静环／动环及接触，依据资料选择机械或热机械步骤 | 位移、适用的接触压力／间隙、温度；仅输出实际求解通道 | 接触状态、约束、收敛和关键尺寸；间隙不直接等同实际漏率 |
| `F-CRACK-local-static` | 与叶片局部结构对应的固定缺陷模型；先区分缺口与裂纹 | 位移、应力、载荷与约束，适用时输出断裂相关量 | 完好／缺陷对照、网格敏感性和连接条件；不预测循环寿命 |

先交付端子局部热分析全链路，其余案例逐个复制流程。已有 `Tools/FiniteElement/AbaqusInputs/` 可作为参考件复算入口，但不能只改文件名就将其称为新设备分析。

### 11.3 从固定裂纹走到扩展分析

F-CRACK 固定缺陷分析完成后，另写“扩展分析任务卡”：明确材料、起裂判据、演化规律、载荷路径、连接区域、预期输出和对照依据，再选择 XFEM、内聚或其他适合的方法。不同方法不是互换的视觉插件。

后续 AI 需要逐项交付：

1. 一个先在简化试件上核对的方法样板，确认参数与输出含义。
2. 对实际叶片局部模型的适配，记录模型简化和材料假设。
3. 收敛／稳定性、网格敏感性及适用的能量检查，说明可接受范围由什么依据确定。
4. 真实分析帧对应的裂面或网格缓存，T10 读取器可以明确区分它与预设动画。
5. 至少三个关键状态的 Abaqus 与 Unity 几何／数值对照；没有实际断开时 UI 不显示断开。
6. 若进一步分析疲劳，另外提供载荷谱、循环轴与材料疲劳／裂纹增长数据。不能从一段单调加载动画生成剩余寿命。

**通过条件：** 每例有可追溯输入、成功的目标分析步、源结果对照和局限说明；缺真实材料／载荷依据的案例只能完成“参考算例”层级。实际求解未发生时 T11 保持未完成，不能借用 T10 合成协议测试替代。

## 12. T12：诊断、维修与复验落到业务方法

当前 `SubmitRepair()` 已要求小车实际到达与非空原因，`Accept()` 负责清除故障。保留这两个入口及现有普通模式，增加实训规则时不要另建平行工单系统。

**建议改动：**

1. 在 `WorkshopAlarm` 或关联的实训实例中记录场景 ID、所选证据、诊断答案、完成的维修检查项和复验结果；工单编号和设备绑定不变。
2. 新增案例级规则对象，定义证据点、可选诊断、适用维修动作和复验条件。界面显示规则，业务方法验证规则。
3. 在 `SubmitRepair()` 的原有到场和原因检查之后，检查当前实训模式要求的步骤；普通模式继续接受原调用。
4. 在 `Accept()` 的原有待验收检查之后，检查复验是否满足。通过后仍由现有逻辑归档、调用 `SetFault(false)` 并恢复故障前启停。
5. 驳回后保留诊断历史与返工次数，允许重新到场补充证据；离开维修点仍撤销现场提交许可。
6. `InspectionRecord` 增加实训结果摘要时保持旧记录可读，不将全部临时 UI 状态塞进归档。

六案例的初始证据可以具体到：P-SEAL 的密封漏点与试漏结果；P-CAV 的入口条件、压力／流量波动；TK-LEAK 的漏点与液位趋势；F-CRACK 的局部裂面与检查结论；F-IMBAL 的同转速对比和连接检查；PLC-CONTACT 的端子温度与复测。

学习模式可显示解释；考核模式不在标题、快捷提示和 AI 默认上下文中直接泄露根因答案。AI 助手使用证据及工单状态解释，不拥有跳过到场、直接验收或修改设备结果的权限。

**通过条件：** 普通模式原流程可用；实训模式空证据、错误步骤与复验缺失有明确反馈；多设备工单不串数据；驳回、离场、重复提交与两轮重置均正确。

## 13. T13：给新版建立可执行验收

### 13.1 不可直接沿用的旧断言

| 旧脚本 | 当前旧假设 | 正确处理 |
| --- | --- | --- |
| `Editor/WorkshopFaultValidation.cs` | 24 个目录定义、每类 4 项、8 台运行、P-02、五条 LineRenderer、旧 Resolve 流程 | 保留历史验证含义；新增当前四设备套件或明确升级其入口；按当前定义与实际工单方法验证 |
| `Scripts/WorkshopCompactUIVerification.cs / VerifyTank()` | 故障时切换旧罐，并要求 HasOpening | 改为新罐仍可见、漏点与相关效果有效、模式恢复正确 |
| `Scripts/WorkshopReviewedFanVerification.cs` | 大于 5 mm 的开口、大于 30 mm 的形变等旧视觉阈值 | 根据新案例目标检查局部形变、连接和恢复，不为通过旧数值维持夸张效果 |
| `WorkshopVerification.Begin()`／`verify-demo` | 验证当前场景，且含旧流程 | 不认为写入命令文件就已打开正确场景或验证完成 |

`WorkshopTwinValidation.RunFaults()` 会打开 Demo，但它内部调用的故障用例仍有上表历史假设；“自动打开场景”也不能让旧断言自动适配新版。

### 13.2 新增验证入口的要求

建议新增 `Assets/WorkshopDemo/Scripts/WorkshopFaultUpgradeVerification.cs`，仅当命令行带新的 `-workshop-fault-upgrade-verify <输出目录>` 时启用。此参数是本任务书提出的待实现入口，当前不能直接使用。

验证器应：

1. 确认活动场景确实为 WorkshopDemo，找到 P-01、TK-01、F-01、PLC-01 四台设备；缺失即报错，不用旧八设备常量补齐。
2. 运行所选阶段的用例清单，输出逐项 PASS／FAIL、截图、异常、测试方法、版本和未覆盖项。
3. 测试结束恢复临时提高的小车速度、相机／输入设置和日志订阅；只在验收启动模式中退出应用。
4. 使用可见 UGUI 控件验证交互；业务断言走实际 `TriggerFault → StartInspection/Assign → 到场 → SubmitRepair → Accept`，不能直接设置 HasArrived 或清除故障。
5. 允许独立运行六案例、全目录、结果协议等分组。正常运行不挂载测试宿主或强制关闭输入。

### 13.3 最小验收用例表

| 用例 ID | 操作 | 必须成立 |
| --- | --- | --- |
| BIND-01 | 四模型创建车间与观察室实例 | 每个已迁移案例的部件与焦点唯一有效，不跨实例引用 |
| MODEL-01 | TK-LEAK 进入／退出故障模式 | 同一个 reviewed 储罐，12 总成，原工位与巡检点不变 |
| MODEL-02 | P-SEAL 播放到末态 | 精细密封组仍存在，通用断裂插片不启用，漏点位于密封区域 |
| MODEL-03 | F-CRACK 全程与暂停 | 裂面／连接符合配置，末态不假称已脱落 |
| MODEL-04 | PLC-CONTACT 热图开／关 | 带载端子局部定位，关闭后原材质恢复 |
| SCRUB-01 | 0→0.75→0.25→1→0，再直接设置各点 | 相同输入对应相同几何、读数和可重建效果 |
| RESET-01 | 切工况、切设备、关闭重开、两次重置 | 无旧粒子、材质、层、Mesh 和声音残留 |
| MOTION-01 | 拆装未完成时切故障 | 先按原路线回装，播放开始时结构完整，不硬跳部件 |
| LOOP-01 | 正常运行设备注入并完成工单 | 实际到场后可提交，验收才清故障，恢复原运行状态 |
| LOOP-02 | 人工停机后注入并完成工单 | 验收后仍停机 |
| LOOP-03 | 提前提交、重复注入、驳回、离场 | 各限制符合业务逻辑，记录不重复 |
| PREVIEW-01 | 活动工单期间倒拖教学回放 | 工单、运行时故障年龄和到场状态不被改写 |
| ANALYSIS-01 | 浏览不同来源和缺结果工况 | 来源准确、错误可读，缺值不显示为求解零值 |
| ANALYSIS-02 | 真实 ODB 三点三帧对照 | 单位、输出位置、轴与数值误差满足已记录门槛 |
| UI-01 | 720p、1080p 点选与关键帧截图 | 控件可点击、文字可读、近景不遮挡关键操作 |
| PERF-01 | 同机同画质运行同案例 | 记录平均／P95／峰值帧耗时、内存及加载时间，不借用独立展示器成绩 |
| PROTECT-01 | 比较受保护交付文件清单 | 不属于授权修改范围的独立展示器路径、长度和哈希不变 |

几何恢复误差可先沿用局部坐标 `10⁻⁶ m` 量级的检查，再根据实际导入尺度与浮点精度说明调整。不能因为观察室位于较远世界坐标，就拿世界坐标误差误判局部网格恢复。

新增效果存在时，原模型资产统计与效果统计分开；不为了保持旧三角面数量而隐藏需要的资产，也不把增加粒子当成原模型被改坏。

### 13.4 构建检查版，不让正式交付承担首次回归

当前已有 `WorkshopPlatformBuild.BuildUIReview()`，输出 `Build/WorkshopPlatformUIReview/WorkshopPlatform.exe`，且不部署 Viewers。它可用于当前 UI 冒烟，但再次调用会写同名检查版目录，需要先保护需要保留的旧结果。

建议在 `WorkshopPlatformBuild.cs` 增加 `BuildFaultReview()`，复用现有私有 `BuildAt(output, false)`，输出带时间戳的独立目录。可按下列形式实现；这是待新增方法：

```csharp
public static void BuildFaultReview()
{
    string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
    BuildAt("Build/WorkshopFaultReview-" + stamp + "/WorkshopPlatform.exe", false);
}
```

该检查版包含现有监控与小车训练场景，不复制或写入正式 Viewers。独立展示器入口验证需要采用明确的现有部署路径或为候选包准备受控副本，不能将“检查版不带 Viewers”误报为新代码损坏。

新增方法编译成功、Unity 路径核实且同一工程未被另一个批处理占用后，才可采用如下命令模板。此处编写模板不代表已运行：

```powershell
$projectRoot = (Get-Location).Path
$unityExe = '填写已核实的 Unity 6000.3.8f1 Editor/Unity.exe 绝对路径'
if (-not (Test-Path -LiteralPath $unityExe -PathType Leaf)) { throw 'Unity 路径未配置' }
$stamp = Get-Date -Format 'yyyyMMdd-HHmmssfff'
$runFolder = Join-Path $projectRoot ('Evidence/fault-upgrade-' + $stamp)
New-Item -ItemType Directory -Path $runFolder | Out-Null
$buildLog = Join-Path $runFolder 'unity-build.log'
& $unityExe -batchmode -quit -projectPath $projectRoot -executeMethod WorkshopDemo.Editor.WorkshopPlatformBuild.BuildFaultReview -logFile $buildLog
```

先读新的 BuildReport 和完整日志，记录实际输出路径。历史上存在 Player 构建成功但 Editor 退出时报中文路径 TypeDb 错误的情况，需分别记录构建结果、退出码和程序运行结果；不能只看其中之一宣布全通过。

新验证器编译进候选程序后，使用下面的命令形式。`$candidateExe` 填本次 BuildReport 的实际绝对路径，`$runFolder` 使用本次独立 Evidence 目录：

```powershell
$candidateExe = '填写本次候选 WorkshopPlatform.exe 的绝对路径'
if (-not (Test-Path -LiteralPath $candidateExe -PathType Leaf)) { throw '候选程序不存在' }
$caseFolder = Join-Path $runFolder '1080p'
New-Item -ItemType Directory -Path $caseFolder | Out-Null
$arguments = @(
    '-screen-width', '1920', '-screen-height', '1080', '-screen-fullscreen', '0',
    '-workshop-fault-upgrade-verify', ('"{0}"' -f $caseFolder),
    '-logFile', ('"{0}"' -f (Join-Path $caseFolder 'player.log'))
)
$candidateProcess = Start-Process -FilePath $candidateExe -ArgumentList $arguments -WindowStyle Hidden -PassThru
```

记录 PID，分段查看输出直到验收完成；不要仅凭进程启动成功或命令文件被消费就宣告通过。720p 使用独立输出目录重复适用用例。真实鼠标验收另行正常打开候选程序并记录，不能由上述 UGUI 自动回归替代。

完成授权范围内的实现与检查后，是否部署正式版、提交 Git 或发布，遵循当时用户指令；本任务书不会自动扩大授权范围。

## 14. 每个任务的交付格式与后续 AI 续作方式

在最新的独立 Evidence 目录维护 `progress.md`，每次结束更新以下内容：

```text
任务 ID：Txx
状态：未开始 / 实现中 / 已实现未验证 / 已验证 / 依赖待补齐
本轮修改文件：
具体修改方法与行为：
新增资产和可追溯来源：
已运行检查、命令、程序版本与结果路径：
本次通过范围：
失败与未验证项：
当前仍保留的旧路径及原因：
下一步准确入口：文件 + 方法 + 尚需完成的动作
受保护文件核对结果：
```

“依赖待补齐”必须写清缺哪份材料／数据、阻塞哪个任务，以及哪些不依赖它的工作已继续完成。不要把没有 Abaqus 数据当成所有视觉和接口工作都无法推进的理由。

### 可直接交给后续 AI 的第一轮指令

```text
请在当前 Unity 工程执行故障模块升级。

先阅读 README.md、AGENTS.md、故障模块升级指导书.md、故障模块AI实施任务书.md，
再检查 Git 状态和最新 Evidence 进度。以当前源码核对任务书中的现有方法。

本轮完成 T00、T01、T02，并执行 T13 中适用的检查：
1. 建立四设备部件绑定和改前证据。
2. 实现最小故障表现接口、可逆预览和安全近景焦点。
3. 让 TK-LEAK 在已验收的新储罐模型上演示，默认不切旧教学罐。
4. 实现局部漏点、湿润／滴漏、近景与正确读数，保留 12 总成与原拆装路径。
5. 同步修改 SetEquipmentMode、SetDamage、LateUpdate、相关 UI 和旧储罐断言。
6. 检查预览不改业务状态、重置与工单闭环；构建独立检查版并保留证据。

直接完成上述范围内的代码和资源适配，不停留于重复方案。
保护已有用户改动、模型、meta、官方样例和独立展示器，尤其 WaterPumpViewer。
不运行 Build Demo 重建场景；不顺手重新生成或导出全部设备。
新接口、新参数要实际实现后才使用；没有真实 ODB 不制造分析结果。
按照任务书交付 progress.md，分清已实现、本次通过、失败和未验证。
```

第二轮可明确指定 T03–T06 及适用 T13；第三轮指定 T07–T08；分析路线从 T09 开始，具备真实数据再完成 T10–T11；T12 按实训需求推进。后续 AI 应读已有进度继续，不重复实现已经通过的基础能力。

## 15. 本文件的使用边界

本文件在 2026-11-14 根据当前源码编写，属于具体实施规格。当前没有因编写本文件而创建上述新 C# 类、资源配置、命令参数或 Abaqus 结果，也没有执行场景重建、求解、构建和发布。

现有代码位置已经静态核对；未来实现时仍需验证导入模型的实际层级、运行效果、字段支持和软件版本。遇到名称或接口变化，更新本任务书与进度记录，使下一位 AI 能按新的真实状态继续工作。
