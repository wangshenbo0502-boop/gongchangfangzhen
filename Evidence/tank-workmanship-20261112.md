# 2026-11-12 立式储罐外观与清晰度复核

## 修改范围

- Blender 源模型：`model/source/silo.blend`
- 生成脚本：`model/scripts/build_tank_detailed.py`
- 审核预览：`model/previews/silo/`
- 本轮仅更新 Blender 审核模型与预览；没有导出 FBX、没有接入 Unity，也没有改动 WaterPumpViewer。

## 本轮处理

- 铭牌由平面板和两块矩形悬空座改为顺罐壁曲率的闭合薄板；文字按曲面变形定位，铆钉随局部罐壁法线安装。
- 磁翻板液位计夹座和就地显示盒支承鞋改为贴合圆筒的曲面实体垫板。
- 保持涂层钢外观，不将罐体改为裸镜面金属；提升 Cycles 到 128 samples、开启降噪并关闭景深。
- 整机审核图为 2400×2700，仪表近景为 2880×2160，均为 PNG。

## 本次验证

2026-11-12 较早一轮，Blender 5.3.0 后台构建完成并保存。随后从磁盘重开 `.blend` 运行 `model/scripts/verify_tank_review.py`（当前后续仪表修订结果见下节）：

- 重点结构检查：PASS。模型高 5.8575 m、12 个组件组；全部网格有归属、UV 与材质；人孔通孔、盖板、螺栓孔、顶部排气孔及纹理打包检查通过。
- 几何实测：903 个网格对象，208,756 个三角面。
- 仍未通过：90,000 三角面预算；目标安装深度 3.00 m（当前 4.1611 m）；前侧投影 1.20 m 上限（当前 1.6136 m）；3 个进口法兰 UV 畸变检查。
- 未验证：TankViewer/FBX、Unity 接入、导航/碰撞、压力容器强度及规范认证。
- 完整机器可读结果：`model/reports/silo-review-verification.json`；构建实测：`model/reports/silo.md`。

## 同日后续：温度计与液位计结构重做

- 将原红白长条仪表改为中空玻璃管液位计：上下支管、隔离阀、玻璃侧口和罐壁开孔连通；抱箍有接往罐壁曲面垫板的支臂，侧置窄刻度尺通过金属横托连接。管内水体采用透射、折射与体积吸收材质，不再用红色实心条代替。
- 新增 TI-01 就地玻璃温度计：200×36 mm 开口金属护壳、内嵌刻度面、细玻璃毛细管和红色液柱；六角接头、安装肩与 90°后接热套管连到罐壁，感温液泡藏于套管内。采用通用工业结构，不宣称厂牌产品复刻或选型通过。
- 保留独立电子 TT01，通过固定电缆接入显示盒。温度计与液位计归原仪表总成，仍为 12 总成。
- 修正审核场景的保存方式，重开后保留 `01_ASSEMBLED`、`02_EXPLODED`、`03_WALL_DETAIL`。

本轮从保存的 `model/source/silo.blend` 重开实测：

- 843 个设备网格、224,472 个三角面；包围盒 3.0044×4.1611×5.8575 m，12 总成。
- 温度液柱实际最高顶点相对 0 和 60 两条刻度网格换算为 **24.799994℃**；不是只读取写入的显示属性。
- 液位实际液面顶点相对侧尺零位为 **1.240000 m**。两项均为静态演示示值，不是采集或热力求解。
- 温度计玻璃 IOR 1.47、透射 1；水体 IOR 1.333、透射 1；液位管玻璃 IOR 1.47、透射约 0.92。
- 两处液位支管到玻璃侧口、隔离阀、接头和罐壁的中心射线通路检查通过；选定支臂/抱箍、支臂/曲面座、刻度尺/横托的实体接触点检查通过。
- 既有人孔、螺栓孔和顶部排气孔检查、网格归属、UV/材质存在性与贴图打包检查通过。上述为局部结构检查，不等于整机干涉验收。

首次支架检查采用短射线，从长刻度尺内部出发，因距离内没有出口面而误报不接触；已保留 `silo-review-verification.20261112-interior-ray-failed.json`，改为基于实际网格最近表面法线的实体内点检查并重跑通过。没有因该误报移动模型。

本轮文件与复现：

- 构建日志：`model/reports/silo-thermometer-build-20261112.txt`。
- 重开检查：`model/reports/silo-review-verification.json`；日志 `silo-thermometer-verification-20261112.txt`。
- 新图：`model/previews/silo/05-instrument-detail.png`（温度计正面）、`06-instrument-connections.png`（温度计侧接）、`07-instrument-layout.png`（仪表布局）、`08-instrument-level-port.png`（液位计上接管）。01–04 是早前整机图，本次没有刷新其中仪表细节。
- 完整重建本次模型：`blender.exe --background --factory-startup --python model/scripts/build_tank_detailed.py -- --review-only --instrument-only`。
- 补渲布局与接管图：`blender.exe --background model/source/silo.blend --python model/scripts/build_tank_detailed.py -- --render-saved --instrument-context-only`。
- 重开核对：`blender.exe --background model/source/silo.blend --python-exit-code 1 --python model/scripts/verify_tank_review.py`。

仍未通过 90,000 三角面预算、旧深度/前侧安装净空和 3 个进口法兰 UV 畸变指标；尚未做 FBX、Unity 展示、导航及仪表选型/压力设计验证。水泵独立交付目录未改动。

## 同日追加：罐壁附件约两倍尺寸修订

用户认为仪表及管线相对 5.86 m 罐体偏小。本轮按安装位置分别放大仪表，曲面垫座重新生成以贴合原筒壁，不缩放罐体或液位测量高度：

- 显示盒实测宽 650.000 mm、高 490.000 mm、深 160.000 mm；文字与屏面同步放大，采用深色屏面及低强度自发光字符。
- 温度计护壳背板实测高 400.000 mm、宽 72.000 mm；接头、热套管、感温泡及刻度同步放大。
- 压力表表壳实测外径 260.000 mm；表弯及取压支管同步放大。PT/TT 电子传感器放大 1.8 倍。
- 液位玻璃管实测外径 136.000 mm、长 2.820 m；侧尺宽 140.000 mm。上下接口中心高度仍为 1.98 m 和 4.38 m，阀组、连通支管、端部压紧套和支架加粗。
- 铭牌增至 600×280 mm并重新排版；液位开关移到玻璃管外侧，通过实体支座连到抱箍。
- 三路保护线管按外径 22 mm 生成，传感器至管路为短软尾线，管路沿罐壁弯走并通过抱箍固定，接入显示盒底部格兰头。

从保存的 `.blend` 重开后，本轮局部检查通过：

- 888 个设备网格、241,460 个三角面、12 总成；整机包围盒 3.0044×4.1611×5.8575 m。
- 温度计实际液柱与刻度换算为 24.800039℃，实际液位为 1.240000 m，均为静态演示。
- 两处液位支管通路、选定支架连接点、温度计感温泡位于套管内、液位开关与抱箍支架连接、仪表实际尺寸检查通过。
- 三路线管实际网格末端到达格兰头轴向范围；按管道半径对中心线路径分段采样，与显示盒、铭牌和温度计壳体的接触检查通过。此项不覆盖全部管路彼此间距或整机装配干涉认证。
- 原有人孔、螺栓孔、顶部排气孔、贴图打包与三个观察 Scene 保留检查通过。

首次新增连接检查存在两处方法错误：将格兰头对象原点当作网格中心，以及将弧面铭牌最近面法线符号用于任意外点的内外判定。首次结果保留在 `model/reports/silo-accessory-verification-initial-check-20261112.json`；后续改为实际网格轴向范围与封闭网格射线奇偶判定并重跑。没有为绕过检查移动模型。

仍未通过：90,000 三角面预算、旧 3.00 m 安装深度与 1.20 m 前侧净空；当前 UV 畸变报告为 4 个未通过网格（PT01 保护线管及原有 3 个进口法兰）。UV 存在不等于 UV 畸变指标通过。本轮没有导出 FBX 或进入 Unity，未改动受保护水泵展示器。

交付与复现：

- 源文件：`model/source/silo.blend`；生成器：`model/scripts/build_tank_detailed.py`。
- 本轮审核图：`09-enlarged-assembled.png`、`10-instrument-enlarged-layout.png`、`11-instrument-display-conduits.png`、`12-instrument-level-enlarged.png`，均位于 `model/previews/silo/`；01–08 保留旧版本，不作为本轮尺寸证据。
- 原版备份：`model/source/silo-accessory-backup-20261112-135230/`。
- 构建：`blender.exe --background --factory-startup --python-exit-code 1 --python model/scripts/build_tank_detailed.py -- --review-only --fittings-only`。
- 构建日志：`model/reports/silo-accessory-build-final-20261112.txt`。
- 重开检查：`blender.exe --background model/source/silo.blend --python-exit-code 1 --python model/scripts/verify_tank_review.py`；日志 `model/reports/silo-accessory-verification-20261112.txt`，结果 `model/reports/silo-review-verification.json`。

## 同日追加：温度计与线管间距调整

用户反馈放大后的温度计顶部靠近 TT01 横管，右侧竖管也使局部显得拥挤。本轮保留已认可的仪表尺寸，仅将 TI-01 安装点从 X=0.35 m、Z=2.40 m 改为 X=0.14 m、Z=2.20 m，即向左 210 mm、向下 200 mm。曲面垫座、热套管及罐壁开孔由生成器在新位置完整重建。

从磁盘重开 `model/source/silo.blend`，32 项局部检查通过：

- 新温度计接孔通路畅通，旧安装点的筒壁已恢复实体；热套管穿过筒壁，感温泡仍位于套管内部。
- 温度计护壳各顶点到 TT01 线管表面的抽样最小距离为 0.249319 m，超过本轮 0.150 m 检查门槛。此数为顶点抽样距离，不代替所有面之间的精确最短距离或完整干涉检查。
- 护壳仍为 400×72 mm；液柱按实际刻度网格换算为 24.800002℃，液位仍为 1.24 m，均为静态演示值。
- 888 个设备网格、241,468 个三角面、12 总成；保留三个观察 Scene、材质、贴图和原仪表尺寸。重建开孔使三角面数比前轮增加 8。
- 90,000 三角面预算、旧安装包络和 4 个 UV 畸变失败项依旧未通过；没有导出 FBX、没有进入 Unity，也未改动水泵展示器。

文件与复现：

- 修改前备份：`model/source/silo-layout-backup-20261112-142032/`，含前轮源文件、脚本、09–12 图片及验证报告。
- 本轮图：`model/previews/silo/13-spacing-assembled.png`、`14-instrument-spacing-layout.png`、`15-instrument-thermometer-clearance.png`。09–12 保留为修改前图。
- 构建命令：`blender.exe --background --factory-startup --python-exit-code 1 --python model/scripts/build_tank_detailed.py -- --review-only --layout-only`。
- 构建日志：`model/reports/silo-spacing-build-20261112.txt`。
- 重开检查命令与前轮相同；本轮日志 `model/reports/silo-spacing-verification-20261112.txt`，最新结果 `model/reports/silo-review-verification.json`。
