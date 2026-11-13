# 电气控制柜 Blender 外观评审

## 当前入槽细节评审版（2026-11-12）

当前源文件为 [electrical-cabinet-duct-entry-review.blend](../source/electrical-cabinet-duct-entry-review.blend)，输入保留为 `electrical-cabinet-field-routing-review.blend`；`Open-CabinetReview.ps1` 指向本版。修订脚本为 `../scripts/refine_cabinet_duct_entries.py`。

用户进一步指出电源、PLC 和变频器附近存在可见线头、跨齿和并线假象。本轮在顶部、24V 电源与 PLC 之间、PLC 下方及变频器下方补齐八段卡扣槽盖，槽盖在中间竖槽处避让、留出拼缝。重排 10 根电源引线，各占独立槽口；32 根 PLC 上下引线重做平滑检修弯，保留原器件插入位置。电源线色与 L、N、PE、+V、−V 标识对应，电源和变频器 PE 线增加纵向黄条识别；修正一根端子线上次正对盖板拼缝的内部末端。盖板内冗余截面环用于调配面数，保留可见器件倒角与前轮现场出线结构。

正式近景另查出电源引线转弯过早穿过下沿壳体，已改为先出端子、避开壳体再转入槽口。穿插检查现覆盖全部 56 个电源网格，壳体无端头豁免；内存重现错误弯线可检出穿插，保存修正版通过同一检查。电源近景及全景已按修正源文件重渲染。

保存文件重开检查：16 总成、3,230 网格、109,824 三角面，G1–G12 和 `source_checks_pass` 全 true，52/52 张文件图片已打包。183 条路径与实际网格一致；本轮检查范围内线间穿插、线槽表面交叉和器件表面交叉均为零。新增 172 个槽内末端、每端 25 条射线的实际盖板遮挡检查；检查方法和限制见 [专项记录](../../Evidence/cabinet-duct-entry-20261112.md)。

四张新图命名 `duct-entry-{psu,drives,plc,internal}.png`，分别查看电源及 PLC 上部、变频器与接触器、PLC 下部、柜内全景。均已完成 2400 × 1600 正式渲染、完整解码和逐张查看，仍待用户审图，不进行 Unity 集成。线槽内末端是几何建模边界，本轮没有建立完整电气网表，也不将盖板遮挡当作回路连通验证。以下为历史记录。

## 上一轮端子区域布线评审版（2026-11-12）

当前源文件为 [electrical-cabinet-field-routing-review.blend](../source/electrical-cabinet-field-routing-review.blend)，从保留的 `electrical-cabinet-housing-review.blend` 修订；`Open-CabinetReview.ps1` 已指向新版本。

根据用户两张局部截图，修订安全继电器下方可见断头和端子下方重复扇形分线。端子排下方增加独立带盖现场出线槽，24 根线芯在槽内分配并插入五根电缆护套；五个粗电缆出口实际移除对应槽齿。端子上方及安全继电器上方补齐卡扣式槽盖，下方黑线重做服务弯；中间竖槽两侧的细线分别绕开槽壁与盖板。保留器件外壳、塑料纹理、门上操作件和 16 个总成。

重开检查为 3,216 网格、109,966 三角面，G1–G12 和 `source_checks_pass` 全通过；宽深高 1.208147 × 0.655000 × 2.000000 m。183 条记录路径与实际网格一致；本轮范围内未检出露出线间穿插、线缆与线槽或器件表面交叉。60 处修改接线记录中，端子入线坐标及全部 24 个线芯对护套插入检查通过。带盖线槽内端点是建模范围边界，未建立整柜电气网表，不能把隐藏端点或几何检查称为完整电气回路通过。

新图使用 `field-routing-{terminals,safety,internal,door-open}.png`。完整验证、图像查看结果和限制见 [本轮记录](../../Evidence/cabinet-field-routing-20261112.md)。仍待用户视觉确认，不进行 Unity 集成。以下章节为历史记录。

## 上一轮器件外壳与材质评审版（2026-11-12）

本轮源文件为 [electrical-cabinet-housing-review.blend](../source/electrical-cabinet-housing-review.blend)，输入为保留的 `electrical-cabinet-workmanship-review.blend`。`../scripts/Open-CabinetReview.ps1` 已指向本轮版本。下方布线版及 2026-11-11 内容均为历史记录。

针对器件过于方正、模型和贴图缺少真实感的反馈，重新制作双变频器分件壳体、开放百叶、散热鳍片、嵌入式小屏和检修盖；接触器增加线圈壳、分相隔板、深端子井、辅助触点与热过载调节件；24 个端子增加阶梯绝缘体、真实螺钉孔道、金属压线件、标记桥和导轨脚；安全继电器增加可拆端子组，接口继电器增加烟色罩及内部机构，PLC 增加肩部轮廓、通风唇和卡扣。变频器出线端子增加与既有导线弯曲方向一致的真实进线孔。保留门上 HMI、克制的按钮材质及此前 183 条布线路径。

绝缘塑料使用原创 1024² 细纹理，按 45 mm 物理尺寸平铺，区分壳体、端子、薄膜按键和聚碳酸酯罩；纹理高度生成法线，避免大块颜色噪声。金属、柜板和玻璃沿用既有材质体系。原始五通道图和小型技术标识位于 `../textures/electrical-cabinet/housing-review/`。

本次重开源文件实测：16 总成、3,141 网格、109,426 三角面；宽深高 1.208147 × 0.655000 × 2.000000 m；55/55 张文件图片已打包。`electrical-cabinet-housing-review-verification.json` 的 `source_checks_pass` 为 true，G1–G12 全 true，非流形、非正体积、UV 失败对象均为 0，UV 最大各向异性 1.221626。

`electrical-cabinet-housing-routing-check.json` 的 `pass` 为 true：183 条中心线与保存网格一致；检查范围内未检出露出线缆互穿、线槽表面交叉或新增器件表面交叉。新增器件检查使用实际三角形边与表面的交点，排除端头 10 mm 插入段及以太网护套 18 mm 插入段；完整包容、共面接触、门运动线束和整柜全部接线未验证。

四张正式图文件名为 `housing-review-{operator,door-open,drives,terminations}.png`，位于 `../previews/electrical-cabinet/`。正式渲染及逐张检查状态见 [本轮记录](../../Evidence/cabinet-housings-20261112.md)。自动检查不能代替用户视觉认可，本轮不进行 Unity 集成。

## 上一轮按钮及布线评审版（2026-11-12）

本轮源文件为 [electrical-cabinet-workmanship-review.blend](../source/electrical-cabinet-workmanship-review.blend)，由保留的 `electrical-cabinet-industrial-review.blend` 修订。`../scripts/Open-CabinetReview.ps1` 默认打开本轮版本。下方 2026-11-11 数据及旧图保留为历史记录，不代表当前版本。

根据用户本轮意见，保留 HMI 屏幕及玻璃材质，降低指示灯透镜、按钮边圈、急停和隔离开关的高光；加入细微塑料表面纹理。重新布置 183 条走线路径，区分 PLC 信号线、工业以太网服务弯、动力接线和现场电缆分支，补充 PLC 管形端头、端子绝缘套、压接针、五组电缆分线护套及固定带。两根跨中间竖线槽的端子线改从两侧入槽，PLC 网线改用独立槽口。

重新打开保存文件实测：16 总成、2,749 网格、108,530 三角面；宽深高 1.208147 × 0.655000 × 2.000000 m；47/47 张文件图片已打包。`electrical-cabinet-workmanship-review-verification.json` 的 `source_checks_pass` 为 true，G1–G12 全通过，非流形边、非正体积和 UV 失败对象均为 0，门开启最大前伸 1.138988 m。

`electrical-cabinet-workmanship-routing-check.json` 的 `pass` 为 true：183 条中心线与保存网格吻合；检查范围内未检出露出线缆扫掠段互穿或修订线缆表面与线槽梳齿、盖板、底座的三角面相交。线间容差为 0.15 mm，排除线槽内部及线芯进入护套的连接区；主要器件壳体另作中心线检查。此结果不等于全部电气连接正确、完整线缆实体间隙或开门运动线束验证。

本轮效果图位于 `../previews/electrical-cabinet/`，以 `workmanship-review-` 开头；正式渲染与逐张查看状态见 [本轮验证记录](../../Evidence/cabinet-workmanship-20261112.md)。外观仍待用户确认，当前未进入 Unity。

## 历史评审版（2026-11-11）

日期：2026-11-11。状态：写实方向修订，等待用户外观反馈，尚未接入 Unity。

## 本轮范围

按用户要求制作原创通用控制柜，并针对“过于卡通、太假”的反馈调整外观。当前阶段只交付 Blender 源模型和真实渲染图，不执行规范中的 Unity 导入、场景重建、程序构建等后续计划。

用户后续要求合理、精细，允许超过八组但不得超过 16 组；本版采用完整 16 组功能总成，将交换机、变频器、安全继电器和操作 HMI 单独分组。每颗螺丝和每根线仍归所属总成，不单列为装配步骤。

源文件：`../source/electrical-cabinet.blend`。运行 `../scripts/Open-CabinetReview.ps1` 可打开独立 Blender 评审窗口，不覆盖已有窗口。

## 外观修订

- 柜体使用 1.5 mm 钣金、折边、门缝、侧板紧固件、吊装耳及底座开口；保留细倒角，降低粗重的玩具轮廓。
- 门板和固定操作条使用一致的钢板材质，搭配深灰底座；降低色彩饱和度，安全色仅用于控制件和警示标识。
- 缩小铭牌及操作标签，标签改为约 0.12 mm 薄片；控制件使用较接近实物的比例，包括 22 mm 指示灯透镜、44 mm 急停头。
- HMI 重绘为克制的工业流程画面，包含罐、泵、阀与换热器符号。内容是演示图，不是实时工艺数据。
- 断路器增加三极分缝、端子凹槽、螺钉、联动拨杆和印字；PLC 增加接口、状态灯、连接器和技术印字。
- 布线增加三相进出线，并修正端点；保留带盖线槽、露出的梳齿、端子号、扎带、PE 铜排及门接地线。
- 调整柔光、反射板和透视相机，使钢板、绝缘塑料、镀锌背板、玻璃及铜排的质感更容易区分。

这是教学用原创设备外观，不对应某厂商产品，不代表电气选型、配线或安全认证。

## 总成

| 名称 | 内容 | 网格数 |
| --- | --- | --- |
| CAB_00_Enclosure | 柜体、底座、折边、吊耳 | 54 |
| CAB_01_Backplate | 镀锌背板和安装附件 | 82 |
| CAB_02_Terminals | 端子、导轨、PE 排 | 260 |
| CAB_03_Door | 门、观察窗、锁、铰链 | 51 |
| CAB_04_Breakers | 主开关及支路断路器 | 221 |
| CAB_05_PLC | CPU 和分布式 I/O | 168 |
| CAB_06_Cooling | 风扇、百叶、滤框、温控器 | 44 |
| CAB_07_Wiring | 线槽、线束、号码管、扎带 | 1,025 |
| CAB_08_ControlPanel | 固定操作条及按钮、指示灯、急停 | 34 |
| CAB_09_PowerSupply | 24 V DC 电源 | 56 |
| CAB_10_PlinthGlands | 底座进线格兰与附件 | 43 |
| CAB_11_Contactors | 接触器及热过载器件 | 86 |
| CAB_12_VariableSpeedDrives | 双变频器 | 128 |
| CAB_13_IndustrialNetwork | 工业以太网交换机及接口 | 243 |
| CAB_14_SafetyRelays | 安全继电器 | 59 |
| CAB_15_OperatorHMI | 门板触摸屏及壳体 | 74 |

细节保留为可编辑子网格；2,628 个网格不等于实时渲染批次数已优化。后续 Unity 集成需评估同总成合批。

## 实测与检查

数据来自重新打开源文件后的实际网格，见 `electrical-cabinet-verification.json`。

| 项目 | 本次结果 |
| --- | --- |
| Blender | 5.3.0 |
| 米制宽 × 深 × 高 | 1.208147 × 0.655000 × 2.000000 m |
| 根节点 | electrical-cabinet，底面中心 |
| 总成 / 网格 / 三角面 | 16 / 2,628 / 108,664 |
| 面数限制 | 110,000；通过 |
| G1–G12 | 全通过；G4 按用户后续允许的 16 组检查 |
| 非流形边 / 非正体积对象 | 0 / 0 |
| UV 最大各向异性 | 1.221626，低于 1.25 |
| UV 面积密度最大偏离倍数 | 1.154709，低于 1.25；按单对象中位数比较 |
| 铰链坐标 | Blender XYZ = (-0.574, -0.279, 0.160) m |
| 铰链方向 | Blender Z / Unity Y，开启 -100° |
| 门开启最大前伸 | 1.138988 m，低于 1.2 m |
| 动画最大姿态误差 | 约 1.2 × 10^-7 |
| 文件图片 / 已打包图片 | 26 / 26 |

`electrical-cabinet-door-clearance.json`：柜门开启的 31 个实际动画位置，8 个主要移动网格对 12 个固定网格的三角面 BVH 检查，未检出相交。该检查排除铰链节、螺钉及可压缩密封，不等于完整装配干涉或制造公差认证。

`electrical-cabinet-review-ui.json`：6 个状态切换、每状态 16 个总成选择及滑条帧映射通过。属于操作符级检查，未据此宣称鼠标交互验收通过。

## 源文件与图像

源文件含材质、打包图片、摄影灯光、观察相机和六个 Scene：

1. `01_ASSEMBLED`：闭门整机；1–281 帧开门、拆分、沿原路径复位。
2. `02_DOOR_OPEN`：铰链开门。
3. `03_EXPLODED`：分组拆开。
4. `04_INTERNAL`：移开右侧操作条以展示完整内部，属于观察状态。
5. `05_ELECTRICAL_FAULT`：屏幕及灯光故障示意，不制造机械断裂。
6. `06_FRONT`：正面比例。

主要评审图位于 `../previews/electrical-cabinet/`：`assembled.png`、`door-open.png`、`internal-detail.png`、`front.png`，均为 Blender Cycles 2400 × 1600。当前图为源模型渲染，不是生成图片或 Unity 截图。该版虽补充了工业以太网交换机、变频器、PLC/I/O、安全继电器等器件，整体材质和布线仍显得过于规整、表面层次不足；用户尚未认可专业真实感。早期预览保留在 `previous-v1/`，`draft-*` 仅作过程记录。

## 泵与小车对照

`compare_cabinet_quality.py` 只读加载当前控制柜、已有泵源文件和官方 `moto_bot.fbx`，不保存到任何参考资源。输出同灯光、曝光、相机和 1200 × 1200 分辨率下的 `quality-cabinet.png`、`quality-cabinet-open.png`、`quality-pump.png`、`quality-cart.png`。各设备最大尺寸统一显示为 2 m，属于细节比较，不是实际比例排布。

`quality-*.png` 和 `electrical-cabinet-quality-comparison.json` 是早期 10 组草稿的对照渲染，不代表当前 16 组模型；其中的控制柜旧统计为 1,325 网格 / 106,928 三角面。网格数和三角面数本身不是视觉质量排名，当前版本尚未重做同场景对照渲染。

已逐张查看：泵的散热筋、法兰、紧固件与护罩依靠实体结构形成轮廓；控制柜的细节密度集中在门内的安装元件和接线区，外壳保持大面积平整钢板。小车原有贴图包含更丰富的边缘磨损、污迹和标识层次，当前控制柜维持较干净的新设备外观，表面局部变化仍少于小车。是否接受这个方向及整体质量，需要用户确认。

小车在 Blender 中重新连接了 52 张原始 PBR 通道贴图；没有复现其 Unity 专用着色器。这组对照不能作为 Unity 观感或用户认可的证明。

## 待确认与后续边界

- 视觉是否满意由用户确认；不能用自动几何检查替代“与泵、小车同一水准”的判断。
- 原始贴图库按 2048 / 1024 / 512 分级生成，主要金属有粗糙度变化与细法线。程序拉丝节点在将来实时材质转换时需烘焙或等效实现。
- 当前 `../fbx/electrical-cabinet.fbx` 是此前草稿，单位回读检查失败，已被当前源模型替代，**不得用于导入或视为本轮交付**。相关旧 export / fbx-verification JSON 也不是本轮通过证据。用户确认外观后再重导出并验证。
- 未进行 Unity 导入、URP 材质转换、性能测试、主车间替换、导航或业务验收。

## 复现

先用系统 Python 运行 `../scripts/cabinet_textures.py` 生成原创贴图，再用 Blender 后台运行 `../scripts/build_cabinet.py`。脚本会备份已有源模型，并生成六个评审 Scene；当前构建默认不导出 FBX。

从现有源模型渲染四张高清图：

```powershell
& 'E:\steam\steamapps\common\Blender\blender.exe' --background 'model/source/electrical-cabinet.blend' --threads 6 --python 'model/scripts/build_cabinet.py' -- --render-only --states 01_ASSEMBLED 02_DOOR_OPEN 04_INTERNAL 06_FRONT
```

重新打开检查分别使用 `verify_cabinet.py`、`cabinet_door_clearance.py`、`verify_cabinet_review_ui.py`；应检查输出 JSON 中的通过标志，不能只依赖 Blender 进程退出码。
