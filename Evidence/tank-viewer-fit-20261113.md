# TK-01 独立储罐展示器修订

2026-11-13，Asia/Shanghai。独立验收入口为 `model/TankViewer/TankViewer.exe`。专用源模型为 [silo-viewer-fit-review.blend](../model/source/silo-viewer-fit-review.blend)，由 `refine_tank_viewer_fit.py` 在原有 `silo.blend` 基础上生成。用户验收后的统一平台接入见 [储罐接入验证](tank-platform-20261113.md)。

- 人孔盖先沿法兰轴向退出 850 mm，再向侧边移开。进水管组、仪表、人孔颈、平台和梯子按先后顺序退出。顶部附件先移离上封头。下部抬升后先抽出阀组，再降低、移出支腿，最后分离下封头。组装原路返回，单程 14 秒。
- 吊耳用实际封头曲面生成安装座，耳板留贯通孔并用螺栓固定安装座。两只耳板到曲面的最小竖向距离均为约 25.36 mm，由座体填满。支腿及加强板按下封头曲面修剪，移动接地紧固件，重新配合梯口踏板和护栏。
- 采用与风机、水泵相近的深色舞台、ACES 调色、HDR 环境反射和轻度锐化。绑定喷涂和金属的颜色、法线、粗糙度贴图，玻璃和水使用透明材质，数显保留自发光。仪表静态示值保持原有设置。
- 检查部件时可点击 12 个标注或直接点模型，查看中文简介和选中着色。拆解时镜头随总成包络调整。

Unity 6000.3.8f1、Windows x64，构建成功、0 错误。网格为 892 个对象、245,364 个三角面、12 总成。该版本仍高于原 90,000 三角面预算，主平台的导航、工位适配和性能另行验证。上下封头分离为教学展示。

旧路径在 307 个姿态中记录 92 对新增网格相交。最终以 Unity 导入网格和新版路径检查 2,409 个姿态，新增或分离后再次相交为 0。83 对闭合装配接触及其中 30 对在脱开时三角形接触数短时增加的记录保留在 [原始检查](../model/reports/tank-unity-motion-audit-20261113.json)，主要位于已相接的法兰、紧固件、焊接或安装面。该采样没有计算实体穿透深度。

实机验证覆盖人孔盖轴向退出、拆装中途反向、完整拆开和归位、12 个标注、射线选中与简介。两种分辨率均通过，见 [1080p 记录](../model/reports/tank-viewer-delivery-1080-20261113/verification.txt) 和 [720p 记录](../model/reports/tank-viewer-delivery-720-20261113/verification.txt)。截图为 [整机](../model/reports/tank-viewer-delivery-1080-20261113/01-assembled.png)、[仪表](../model/reports/tank-viewer-delivery-1080-20261113/02-instruments.png)、[吊耳](../model/reports/tank-viewer-delivery-1080-20261113/03-roof-lifting-lugs.png)、[人孔盖退出](../model/reports/tank-viewer-delivery-1080-20261113/04-manway-cover-release.png)、[完全拆解](../model/reports/tank-viewer-delivery-1080-20261113/06-exploded.png)。

通过检查的构建已放回原独立展示器路径并核对主模型资源哈希。旧交付目录备份至 `model/revisions/tank-viewer-prior-delivery-20261113-210206`，原始 `silo.blend` 与备份哈希一致。用户视觉验收待完成。
