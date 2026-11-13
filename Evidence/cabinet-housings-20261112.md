# 控制柜器件外壳与材质修订

日期：2026-11-12，Asia/Shanghai。范围：Blender 源模型和四张评审图。用户认为上一轮约为 75–80 分，器件仍过于方正，模型与贴图真实感不足；视觉认可前不进入 Unity。

## 文件与改动

- 输入保留：`model/source/electrical-cabinet-workmanship-review.blend`。
- 当前源文件：`model/source/electrical-cabinet-housing-review.blend`。
- 模型脚本：`model/scripts/refine_cabinet_housings.py`。
- 原创纹理脚本：`model/scripts/cabinet_housing_textures.py`。
- 纹理及标识：`model/textures/electrical-cabinet/housing-review/`。
- 打开入口：`model/scripts/Open-CabinetReview.ps1`。

双变频器改为有肩部与坡面的分件壳体、真实开放百叶、外露散热鳍、内嵌 LCD/薄膜按键、下检修盖与薄铭牌。接触器重做线圈壳、触头室、分相隔板、深端子井、辅助触点卡扣及热过载调节件。端子排重做 24 个阶梯绝缘体，增加开放螺钉孔道、压线件、标记桥、导轨脚和真实进线孔。安全继电器采用独立端子组，接口继电器增加烟色罩及内部机构；PLC 增加肩部、通风唇与卡扣。

保留 HMI、按钮材质及此前 183 条布线路径。新增变频器出线孔沿既有线缆入口方向切出，避免实体壳体与弯曲导线冲突。小屏使用独立图像 UV，并裁取原图有效显示区，避免方形底图压扁数字。柜体六个 Scene 共享修订网格。

塑料采用原创建模用 1024² 五通道纹理，按 45 mm 物理平铺；材质读取 BaseColor、Normal、Roughness，Metallic 为 0，Height 已用于生成法线。不同塑料使用独立颜色与粗糙度。细纹理避免大范围斑驳，技术标识使用单独图像 UV。原有柜板、金属、玻璃和线缆材质保留。

面数从小型紧固件、铰链等附件重新分配；简化后逐件检查非流形，失败对象保留原网格。可见导轨端挡和变频器安装耳保留原轮廓。保留所有历史源文件和评审图。

## 参考资料边界

本轮尝试检索厂商器件页面及柜内照片，网络工具未返回可核实的页面或图像内容，因此没有把这些请求作为已查看实物照片或取得厂商 CAD 的证据。本模型为原创通用教学设备，不是某厂商产品的精确复刻。

## 本次源文件检查

最终构建日志：`model/reports/electrical-cabinet-housing-build-final.log`。

2026-11-12 11:30（Asia/Shanghai）用 Blender 5.3.0 后台重新打开保存源文件运行 `verify_cabinet.py`，读取结果 JSON，不仅判断进程退出码。

| 项目 | 本次结果 |
| --- | --- |
| 总成 / 网格 / 三角面 | 16 / 3,141 / 109,426 |
| 面数上限 | 110,000，通过 |
| 宽 × 深 × 高 | 1.208147 × 0.655000 × 2.000000 m |
| G1–G12 / source_checks_pass | 全 true / true |
| 非流形 / 非正体积 / UV 失败对象 | 0 / 0 / 0 |
| UV 最大各向异性 | 1.221626 |
| UV 面积密度最大偏离倍数 | 1.154712 |
| 图片已打包 / 文件图片 | 55 / 55 |
| 门开启最大前伸 | 1.138988 m |
| 动画最大姿态误差 | 6.70 × 10^-8 |

原始结果：`model/reports/electrical-cabinet-housing-review-verification.json`。

## 本次布线检查

在最终保存源文件运行 `verify_cabinet_routing.py --devices`：

- 183 条记录中心线与实际保存线缆网格的截面环中心一致。
- 检查范围内露出线缆互穿记录为 0，扫掠段线间容差 0.15 mm。
- 线缆实际三角面与线槽梳齿、盖板、底座表面交叉记录为 0。
- 线缆与本轮 VFD、接触器、端子、安全继电器、接口继电器、PLC 表面交叉记录为 0。
- `pass = true`。

原始结果：`model/reports/electrical-cabinet-housing-routing-check.json`。

器件表面检查先由 BVH 取得候选三角形，再双向求三角形边与对方表面的交点；不使用长三角形重心估算穿插位置。端接排除线缆端点 10 mm 范围，以太网护套允许 18 mm 插入范围。检查不覆盖实体完全包容、共面接触、全部原有线缆或开门运动中的柔性线束，不代表电气设计验收。

## 渲染与视觉评审

四张正式图均已从保存源文件输出完成，为 Blender Cycles、CUDA、2400 × 1600、72 采样、降噪。使用 `view_image` 逐张打开，并以 Pillow 解码核实尺寸。实际模型渲染，不是生成图片。

| 图像 | 本次逐张查看 |
| --- | --- |
| `housing-review-drives.png` | 双变频器近景；分件壳体、百叶深度、检修盖、金属散热侧面可辨，小屏数字比例已修正 |
| `housing-review-terminations.png` | 接触器、安全继电器、端子与接线；开放端子孔及金属压线件可辨，导轨端挡已恢复原轮廓 |
| `housing-review-door-open.png` | 开门整机；柜体、门板、底座完整入画，右侧操作条自然遮挡部分内部 |
| `housing-review-operator.png` | 操作面近景；保留 HMI 玻璃反射和按钮原材质，此构图有意裁去柜体底部 |

图像目录：`model/previews/electrical-cabinet/`。变频器图日志为 `model/reports/electrical-cabinet-housing-render.log`；该进程完成第一张后为增加后台线程而主动停止。其余三张由新进程输出，正常退出，日志为 `model/reports/electrical-cabinet-housing-render-remaining.log`。变频器首图渲染约 7 分 48 秒，其余三图合计约 7 分 11 秒。

`Open-CabinetReview.ps1` 已指向本轮源文件，PowerShell AST 语法检查错误数为 0。本次未另开可见 Blender 窗口，也未重新验证观察侧栏鼠标操作。自动检查通过不代表完全写实、制造可用或用户视觉认可。

视觉是否满意仍由用户审图确认。本轮未执行 FBX 导出、Unity 导入、URP 转换、独立程序构建或主场景替换。
