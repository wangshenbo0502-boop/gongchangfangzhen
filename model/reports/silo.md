# 立式储罐模型构建实测

- 状态：Blender 源模型与预览阶段；尚未导入 Unity。
- Blender：5.3.0
- 设备网格对象：888（不含摄影棚地面）
- 组件组：12
- 网格三角面实测：241,468
- 实测包围盒 X/Y/Z：3.0044 / 4.1611 / 5.8575 m
- width X: 3.0044 m vs 3.00 m target (+0.15%; PASS)
- depth Y: 4.1611 m vs 3.00 m target (+38.70%; FAIL)
- height Z: 5.8575 m vs 5.86 m target (-0.04%; PASS)
- 巡检前侧投影：1.6136 m（上限 1.20 m；FAIL）
- 后侧投影：2.5475 m（设计包络 1.80 m；FAIL）
- 三角面预算：241,468 / 90,000（FAIL）
- UV 畸变检查：FAIL；4 个网格未通过，详见 `silo-uv.json`。UV 存在不等于畸变指标通过。
- 实测最小 Z：-0.0050 m
- 筒体名义壁厚：8 mm（脚本构造双层壳面；未做压力容器强度求解）。
- 用户本次指定总高约 5.86 m，直径仍为 3.00 m；筒体切线高度 1.55–5.10 m。
- 后平台深 650 mm，护栏高 1100 mm；安装包络包含平台、梯笼和全部阀组。
- 展开视图中的焊接封头分离仅为检查展示，不代表实际检修拆卸步骤。
- Unity 组件接入、导航通行、故障演化、压力设计与规范认证：未执行。

预览图：

- `previews/silo/01-assembled-front.png`
- `previews/silo/02-assembled-rear.png`
- `previews/silo/03-assembled-side.png`
- `previews/silo/04-exploded-front.png`
- `previews/silo/05-instrument-detail.png`
- `previews/silo/06-instrument-connections.png`
- `previews/silo/07-instrument-layout.png`
- `previews/silo/08-instrument-level-port.png`
- `previews/silo/09-enlarged-assembled.png`
- `previews/silo/10-instrument-enlarged-layout.png`
- `previews/silo/11-instrument-display-conduits.png`
- `previews/silo/12-instrument-level-enlarged.png`
- `previews/silo/13-spacing-assembled.png`
- `previews/silo/14-instrument-spacing-layout.png`
- `previews/silo/15-instrument-thermometer-clearance.png`

## 仪表设计与参考

- 用户要求罐壁附件约放大两倍：液位管外径 136 mm、侧尺宽 140 mm，连通支管、阀体、接头和支架同步加粗；测量高度及 1.24 m 静态示值不变。
- TI-01 就地玻璃温度计：400×72 mm 开口金属护壳、细玻璃毛细管和红色液柱；90°后接式热套管穿入罐壁，感温液泡在套管内部。0–60 C 刻度与液柱几何对应静态 24.8 C。
- 温度计采用通用工业护壳与后接结构，并非厂牌产品复刻或选型结果。电子 TT01 仍独立接入数显盒。
- 就地压力表外径 260 mm；取压短管、表弯、表头同步放大并连续连接。PT/TT 电子传感器放大 1.8 倍。
- 罐壁显示盒 650×490×160 mm，屏幕采用低强度自发光字符。传感器软尾线转入外径 22 mm 金属保护线管，经固定抱箍接入底部格兰头；显示值为静态演示值。
- 曲面铭牌增至 600×280 mm，继续贴合罐壁；附件放大图为 09–12，最新间距修订图为 13–15。
- TI-01 安装点改至 X=0.14 m、Z=2.20 m，较放大初版向左移 210 mm、向下移 200 mm；护壳尺寸不变，曲面垫座、热套管和筒壁开孔在新位置重建，避免紧贴上方和右侧线管。
- 人孔盖与颈部共用 650 mm 法兰外径、560 mm 螺栓圆及 12 孔相位；450 mm 通孔贯穿筒壁。
- DN100 进口阀组采用同口径连接与端部盲板，不再以细管插入大法兰；排净阀组连接下封头中心。
- 铭牌座、接地耳和仪表支座按筒壁曲率定位；吊耳采用有孔板式吊耳及曲面垫板。
- 本组按《建模目标与验收标准》第 4.2 节收敛；仪表外形是示意性工程可视化，不代表已完成仪表选型或压力计算。
- 当前几何仍未满足整机深度及前侧净空要求：前侧人孔盖/把手与后侧梯笼/平台共同扩大包络，需在导入 Unity 前重新布置，避免碰撞体侵入巡检通道。
- 后平台保留可站立踏面及完整护栏，未为缩小包围盒而压扁；因此安装深度超过旧包络，后续接入需配套修改设备布局/导航。
- 当前仅为 Blender 审图；未覆盖 TankViewer/FBX 输出、未接入 Unity，未做压力容器设计或仪表选型校核。

适用规范：

- `建模目标与验收标准.md` 第 1.2、2.2、4.2、5.2 节。
