# 控制柜端子区域布线修订

日期：2026-11-12，Asia/Shanghai。用户指出安全继电器下方和端子排附近线缆仍不自然，提交两张局部截图。本轮仅修改 Blender 模型并提供效果图，不导出或接入 Unity。

## 文件

- 输入保留：`model/source/electrical-cabinet-housing-review.blend`。
- 当前源文件：`model/source/electrical-cabinet-field-routing-review.blend`。
- 可复现脚本：`model/scripts/refine_cabinet_field_routes.py`。
- 当前打开入口：`model/scripts/Open-CabinetReview.ps1`。
- 路径与端接记录：`model/reports/electrical-cabinet-field-routing-review.json`。
- 重开验证：`model/reports/electrical-cabinet-field-routing-verification.json`。
- 穿插验证：`model/reports/electrical-cabinet-field-routing-check.json`。

## 本轮修改

上一轮部分线槽敞开，简化路径的末端直接可见；自动穿插检查未检查端点遮蔽或电气连接，不能据此前通过结果声称这处没有问题。

本轮在端子排下方增加 900 × 35 mm 现场出线槽和两段卡扣盖板。24 根线芯经槽齿间隙进入槽内，在盖板之后分配至五根电缆护套；端子外只保留接线和检修弯曲余量，取消原来五组重复的外露扇形分线。粗电缆出槽处实际移除槽齿，使用独立护口；保留 PE 排后的电缆弯曲与原固定带。

端子排上方控制线槽改为两段完整盖板，安全继电器上方线槽补齐盖板；重新扫掠 12 根安全继电器引线，增加小半径服务弯的采样密度。24 根端子上方导线和 24 根下方线芯重做入槽段，中间竖槽两侧的细线先避开盖板再入槽，避免相互交叉。总计修订 65 条线缆路径，其余路径保留。

预算从导线在盖板内的冗余截面环、原外露分线护套和五根粗电缆的扫掠分段中调配；保留前轮器件外壳、材质、HMI 玻璃和按钮。

## 本次检查

从最终保存源文件重新打开运行 `verify_cabinet.py` 和 `verify_cabinet_routing.py --devices`，读取 JSON 结果：

| 项目 | 结果 |
| --- | --- |
| 总成 / 网格 / 三角面 | 16 / 3,216 / 109,966 |
| 三角面上限 | 110,000，通过 |
| 宽 × 深 × 高 | 1.208147 × 0.655000 × 2.000000 m |
| G1–G12 / source_checks_pass | 全 true / true |
| 非流形 / 非正体积 / UV 失败对象 | 0 / 0 / 0 |
| UV 最大各向异性 / 图片已打包 | 1.221626 / 55 张，共 55 张 |
| 中心线与保存网格一致 | 183 / 183 |
| 检查范围内露出线间互穿 | 0 |
| 实际线缆三角面与槽齿、底板、盖板交叉 | 0 |
| 检查范围内器件表面交叉 | 0 |
| routing pass | true |
| 记录的修改接线位置 | 60 |
| 24 根现场线芯插入护套 | 全通过 |

检查过程中实际发现并修复新下线槽槽齿穿线、五个粗电缆出口撞齿、中间两根细线相交及中心竖槽盖板穿线；未修改穿插容差掩盖问题。新增线槽内部范围仅对本轮 `field-routing` 报告生效，原报告不套用新增槽体的内部排除区。两次初步草图对应过程版本，正式交付以当前源文件及四张正式图为准。

端接检查核对端子坐标，以及线芯末端径向包络和插入深度是否位于对应护套内。带盖槽内的导线末端属于建模边界，并未建立完整电气网表；检查不等于整柜电气回路、选型或制造验证。原有几何检查仍排除端头插入段及槽内线间分配，不覆盖完整实体包容、共面接触或开门动态线束。

## 渲染

正式图目标为 Blender Cycles / CUDA、2400 × 1600、72 采样、降噪，使用 12 个后台线程。渲染日志为 `model/reports/electrical-cabinet-field-final-render.log`。

- `model/previews/electrical-cabinet/field-routing-terminals.png`
- `model/previews/electrical-cabinet/field-routing-safety.png`
- `model/previews/electrical-cabinet/field-routing-internal.png`
- `model/previews/electrical-cabinet/field-routing-door-open.png`

2026-11-12 13:12，四张正式图全部渲染结束，后台进程正常退出。使用 Pillow 逐张解码核对均为 2400 × 1600，并使用 `view_image` 逐张打开检查：

| 正式图 | 本次查看结果 |
| --- | --- |
| terminals | 端子两侧的检修弯、上下分层线槽、槽盖接缝可辨；原来五组外露扇形分线已取消 |
| safety | 上方蓝线和下方黑线弯入带盖槽体；原截图里可见的断头已消除 |
| internal | 直接观察柜内的较大构图，含背板、器件、上下线槽及 PE 排；有意裁去整机底座 |
| door-open | 开门整机完整入画，右侧固定操作条按实物遮挡部分内部 |

渲染总耗时约 11 分 56 秒，含建模保存与设备初始化；首图曾与其他后台任务共享计算资源。未停止其他 Blender 窗口或其他任务。打开脚本的 PowerShell AST 语法检查错误数为 0，本轮未另开可见窗口。视觉真实感仍待用户确认。
