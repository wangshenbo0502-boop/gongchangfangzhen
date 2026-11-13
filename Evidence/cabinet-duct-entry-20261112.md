# 控制柜入槽细节修订

日期：2026-11-12，Asia/Shanghai。本轮依据用户连续三张局部截图，修正 24V 电源、PLC 和变频器附近线头停在敞开槽体、并线、入槽缺乏细节的问题。仅修订 Blender，未导出或接入 Unity。

## 文件与复现

- 输入保留：`model/source/electrical-cabinet-field-routing-review.blend`。
- 当前源文件：`model/source/electrical-cabinet-duct-entry-review.blend`。
- 脚本：`model/scripts/refine_cabinet_duct_entries.py`。
- 入口：`model/scripts/Open-CabinetReview.ps1`。
- 路径和操作：`model/reports/electrical-cabinet-duct-entry-review.json`。
- 几何重开检查：`model/reports/electrical-cabinet-duct-entry-verification.json`。
- 穿插检查：`model/reports/electrical-cabinet-duct-entry-check.json`。
- 末端和坐标重开检查：`model/reports/electrical-cabinet-duct-entry-inspection.json`。

使用 `E:\steam\steamapps\common\Blender\blender.exe`，从保留的输入运行脚本生成本版。重开本版分别运行 `verify_cabinet.py -- --output ...`、`verify_cabinet_routing.py -- --report ... --devices --output ...` 和修订脚本的 `--check-only`。不得从旧外壳版重新生成，否则会丢失上一轮端子下方的现场出线结构。

## 修改

1. 顶部 z=1.910、电源/PLC 之间 z=1.588、变频器下方 z=1.057、PLC 下方 z=1.217 四处线槽，换为共八段完整卡扣盖板，盖板厚 5 mm、边缘倒角 0.45 mm。上下贯通的中间竖槽处，横盖与竖盖错开边缘，保留装配缝。
2. 10 根电源引线各占独立槽口，取消原右侧两对汇入同一槽口的路径。出线有短直插入段、连续弯曲和插入槽后的深度，并增加十个端头绝缘套。按原电源上的 L/N/PE/+V/−V 标识分配棕、蓝、黄绿、红、蓝线色。
3. 32 根 PLC 上下引线保持器件端坐标，重做入槽前的平滑弯曲和进入槽内的深度。电源与变频器 PE 导体增加沿管形网格延伸的黄色识别条；不用额外网格包覆，面数不增加。
4. 新的遮挡检查发现端子第 13 根上引线末端恰对旧盖板拼缝，增加 10 mm 槽内转向。保留前轮端子接线、现场出线槽和五根护套分支。
5. 从已被槽盖遮挡的路径内消减 1,068 个三角面，保留外露曲线采样和器件倒角；共 134 条路径记录变化，其中包含几何未露出的简化。电气器件外壳、HMI、门上按钮及原有材质方向保持。
6. 第一张正式电源近景暴露出新引线转弯过早，穿过电源前面板下沿及底部壳体。重新生成本版，十根线先沿端子方向出线，在壳体下方再向槽口弯曲；保持端子起点、套管和网格预算。重新渲染受影响的电源局部与柜内全景，PLC/变频器局部无几何变化。

## 实测

| 项目 | 本次结果 |
| --- | --- |
| 总成 / 网格 / 三角面 | 16 / 3,230 / 109,824 |
| 宽 × 深 × 高 | 1.208147 × 0.655000 × 2.000000 m |
| G1–G12 / source_checks_pass | 全 true / true |
| 非流形 / 非正体积 / UV 失败对象 | 0 / 0 / 0 |
| UV 最大各向异性 | 1.221626 |
| 文件图片 / 已打包 | 52 / 52 |
| 与实际网格一致的路径 | 183 / 183 |
| 检查范围内露出线间穿插 | 0 |
| 线缆实际三角面与槽齿、盖、底板交叉 | 0 |
| 检查范围内器件表面交叉 | 0 |
| routing pass | true |
| 器件表面检查网格 / 其中电源网格 | 1,035 / 56 |
| 槽内末端盖板遮挡 | 172 个端帽，每个 25 条射线，通过 |

初次生成时检查实际查出右侧电源两对槽口重复；修正后未更改线间容差。末端遮挡检查使用真实盖板三角形，对端帽中心及四个周边偏移点，各向正面、左右斜向、上下斜向发射射线，不以“末端坐标在线槽里”替代遮挡测试。另以保存模型重新核对十个独立电源槽口和 42 个原器件接线起点。

器件表面检查扩展到电源组全部 56 个网格：电源壳体不允许任何端头豁免，绿色端子条仅允许 5 mm 插入。`check_cabinet_psu_regression.py` 在内存复现被否决的提前转弯，不保存 Blender 文件；`electrical-cabinet-psu-negative-check.json` 检出 30 对线缆/电源表面交叉，其中 20 对为壳体或前面板，10 对为端子条。该负对照的 `pass=false` 为预期，不能用来代替保存源文件的检查结果。保存源文件使用同一检测器无交叉、`pass=true`。

线槽内部导线末端仍为本模型的范围边界，没有建立完整电气网表；无法据此宣称整柜回路连通或制造设计验证。线间检查沿用 0.15 mm 容差，排除槽内线间分配和端头插入段；其余器件端头沿用 10 mm、网口护套 18 mm 的插入豁免，电源采用上述更严规则。不覆盖完整实体包容、共面接触或开门动态线束。遮挡检查覆盖指定五个方向，不能代替任意视角的检查。

## 渲染与审图

Blender Cycles / CUDA，正式图 2400 × 1600、72 samples、降噪，12 个后台线程。首轮渲染日志：`model/reports/electrical-cabinet-duct-entry-final-render.log`；电源出线修正后的重渲染日志：`model/reports/electrical-cabinet-duct-entry-psu-corrected-render.log`。电源和全景以重渲染后的同名图片为准。

- `model/previews/electrical-cabinet/duct-entry-psu.png`
- `model/previews/electrical-cabinet/duct-entry-drives.png`
- `model/previews/electrical-cabinet/duct-entry-plc.png`
- `model/previews/electrical-cabinet/duct-entry-internal.png`

三个局部草图已用 `view_image` 查看，电源局部因俯视遮挡接线而调整机位后重渲染草图。四张正式图均已渲染完成并逐张用 `view_image` 查看，Pillow 完整解码确认均为 2400 × 1600、非空白。电源修正图可连续看清端子至槽口的引线；变频器和 PLC 局部未再出现用户截图中的外露截断线头；全景确认本轮槽盖与既有设备布局关系。此为本次审图记录，不等于用户最终认可。

| 正式图 | 文件字节数 |
| --- | --- |
| duct-entry-psu.png | 3,931,123 |
| duct-entry-drives.png | 3,789,494 |
| duct-entry-plc.png | 3,891,075 |
| duct-entry-internal.png | 3,954,486 |

首轮与电源修正后两次后台渲染均正常退出，未关闭其他 Blender 窗口。几何、穿插、末端检查 JSON 已重新确认通过。仍待用户视觉审阅，未执行控制柜 Unity 导出、导入或构建。
