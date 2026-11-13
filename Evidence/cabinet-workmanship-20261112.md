# 控制柜按钮及布线修订验证

日期：2026-11-12，Asia/Shanghai。任务范围：Blender 源模型及四张效果图；用户视觉认可后再考虑 Unity。

## 文件与修改

- 输入：`model/source/electrical-cabinet-industrial-review.blend`，保留。
- 输出：`model/source/electrical-cabinet-workmanship-review.blend`。
- 可复现脚本：`model/scripts/refine_cabinet_workmanship.py`。
- 评审入口：`model/scripts/Open-CabinetReview.ps1`，默认打开本轮版本。
- 屏幕及玻璃材质保留；指示灯、按钮边圈、急停、隔离开关降低反光与自发光，区分金属、透镜和塑料质感。
- 重做 183 条路径，包含 PLC、24 V、动力、接触器、安全回路、交换机、端子和现场电缆。增加弯曲过渡、实际槽口分配、端头绝缘套、压接针、分线护套和固定带；24 根现场线芯分入五根电缆。
- 微小插槽倒角改为单段边缘，保留主要柜体及器件轮廓，将新增走线细节控制在原面数预算内。

## 本次源文件检查

Blender 5.3.0 后台重新打开输出文件，运行 `verify_cabinet.py`，检查 JSON 内容，未仅依赖进程退出码。

| 项目 | 本次实测 |
| --- | --- |
| 总成 / 网格 / 三角面 | 16 / 2,749 / 108,530 |
| 宽 × 深 × 高 | 1.208147 × 0.655000 × 2.000000 m |
| G1–G12 / source_checks_pass | 全 true / true |
| 非流形 / 非正体积 / UV 失败 | 0 / 0 / 0 |
| UV 最大各向异性 | 1.221626 |
| UV 面积密度最大偏离倍数 | 1.154712 |
| 门开启最大前伸 | 1.138988 m |
| 打包图片 / 文件图片 | 47 / 47 |

原始结果：`model/reports/electrical-cabinet-workmanship-review-verification.json`。

## 本次布线检查

运行 `verify_cabinet_routing.py`，结果 `pass = true`：

- 183 条记录的中心线与实际保存线缆网格的截面环中心一致。
- 露出线缆扫掠段互穿记录为 0，线间容差 0.15 mm。
- 修订线缆实际三角面与线槽梳齿、盖板、底座表面相交记录为 0。
- 修订脚本的主要器件壳体中心线检查无穿越记录；现场电缆在 PE 铜排高度范围的采样最小间隙约 5.26 mm。

原始结果：`model/reports/electrical-cabinet-workmanship-routing-check.json`、`model/reports/electrical-cabinet-workmanship-review.json`。

边界：排除线槽内部及线芯插入护套的连接区；端接允许进入连接器。主要器件检查使用中心线，不能等同全线缆表面间隙验证。本次未验证开门运动中的柔性线束，也未进行完整电气回路、选型或制造验证。

## 渲染与视觉评审

四张正式图已由本轮保存源文件在 Blender Cycles 中渲染完成，均为 2400 × 1600、96 采样并降噪；图像解码和尺寸检查通过。渲染进程正常结束，日志为 `model/reports/electrical-cabinet-workmanship-render.log`。不是生成图片或 Unity 截图。

| 图像（位于 model/previews/electrical-cabinet/） | 本次逐张查看 |
| --- | --- |
| workmanship-review-operator.png | 操作面近景，按钮与急停高光减弱，保留屏幕玻璃反射；此图有意裁掉柜体底部 |
| workmanship-review-door-open.png | 开门整机，柜体与门板完整入画；右侧固定操作条遮住部分内部器件 |
| workmanship-review-wiring.png | PLC、交换机和部分驱动近景，接口、出线端头及服务弯可辨；使用移开操作条的内部观察状态 |
| workmanship-review-terminals.png | 端子、分线护套、现场电缆及 PE 铜排近景，端接与铜排后方路径可辨；使用内部观察状态 |

四张图均已通过 `view_image` 实际打开查看，无空白或整幅缺失。器件仍采用有序 DIN 导轨安装，本轮主要改善走线层次、入槽分配和分支弯曲，没有重新设计全部器件外壳。自动检查及本次查看不代表用户认可专业真实感或达到泵、小车同等水准；视觉验收待用户反馈。

未进行 Unity 导入、材质转换、独立程序构建或主车间替换。
