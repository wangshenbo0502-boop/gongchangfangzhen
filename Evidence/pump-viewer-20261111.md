# 离心泵独立展示器：拆装路径与清晰度验证

日期：2026-11-11，Asia/Shanghai。Unity 6000.3.8f1，URP，Windows 独立程序。

## 范围与交付

入口：`model/WaterPumpViewer/WaterPumpViewer.exe`。本轮保留用户已认可的组件标注和材质色调，修改拆装路径、实际几何间隙与清晰度；未把模型接入主车间。

Blender 与独立 Unity 工程共用 12 总成配置，实际 349 网格、119,696 三角面。单程 14 秒，分段平滑退出、移开与内部件分离，组装沿同一路径反向返回。前盖先轴向退出再侧移，叶轮前盖板先随叶轮退出再分离。修复叶轮扫掠空间、前盖螺栓通孔、电机脚座与导轨抽离孔、轴承支座及护罩端部间隙；同步可重复生成脚本。

清晰度使用 HDR、4× MSAA、宽高各 2 倍超采样和轻度受限锐化，无 FXAA/TAA 或动态分辨率。实际贴图为铸件 2048²、机加工 1024²、橡胶 512²，九张导入贴图使用完整尺寸、非压缩格式；不属于原生 4K 贴图升级。

## 本次运行检查

最终构建日志：`model/reports/pump-clarity-fixed-build.log`。修复后的锐化 shader 编译成功。

| 项目 | 1600×900 | 1920×1080 |
| --- | ---: | ---: |
| 报告目录，均位于 model/reports | pump-clearance-clarity-final-1600 | pump-clearance-clarity-final-1920 |
| verification.json / passed | true | true |
| 3D 视口实际超采样尺寸 | 3200×1356 | 3840×1626 |
| 3D 中央区域有亮度像素比例 | 0.780889 | 0.780642 |
| 标注观察角度 | 8 | 8 |
| 标注重叠 / 引导线交叉 | 0 / 0 | 0 / 0 |
| 最长引导线，屏幕像素 | 43.69 | 52.43 |
| 归位最大误差，m | 2.38×10⁻⁷ | 2.38×10⁻⁷ |
| 返回路径误差，m | 3.63×10⁻⁷ | 3.63×10⁻⁷ |
| 中途反向切换误差，m | 0 | 0 |
| 60 帧上限下平均帧时间，ms | 16.40 | 16.40 |
| 运行报告 errors | 空 | 空 |

3D 视口尺寸扣除了顶部与底部 UI，不是整个窗口乘二；引导线长度随 Canvas 比例缩放，逻辑上限仍为 48 Canvas 单位。帧时间仅代表本机本轮短时运行，不代表压力测试。

已查看最终 1600 的整机、拆开与中间过程截图，以及 1920 的整机与内部近景。模型与材质正常显示，无黑色视口；拆开时前盖、叶轮与盖板分离。内部近景为局部构图，会裁切外围零件。内腔局部仍可见多边形高光，不宣称照片级一致性。

默认无分辨率参数启动检查见 `model/reports/pump-clearance-default-window/window-verification.json`：桌面 2560×1600，实际客户区 2240×1260，与默认尺寸算法一致；截图 `default-window.png` 已查看，程序留在整机状态供用户审核。可复现脚本为 `model/scripts/Test-PumpViewerDefaultWindow.ps1`。

## 源模型与运动表面检查

`model/reports/water-pump-reopen-verification.json`：重新打开源文件通过，G1–G12 全部通过，无非流形、非正体积或 UV 失败对象；16/16 文件图像已打包。源动画与共享路径最大误差约 1.33×10⁻⁷ m，归位误差为零。

`model/reports/water-pump-motion-intersections.json`：采样 300 个拆解位置，返回沿相同路径。未解决的初始穿透、新增相交、运动中持续穿透列表均为空，`surface_checks_pass=true`。没有整体忽略初始碰撞；仅将至少一个轴的 AABB 重叠不超过 10 微米的表面相交分类为就位接触。

这属于离散位置的多边形表面检查，不保证连续碰撞、完全包容、制造公差、装配工艺或物理可拆性；未开展有限元或 Abaqus 求解。本轮运行验证直接调用展示器的实际动画命令；未重跑 OS 鼠标自动化脚本，不能据此宣称所有鼠标操作本次重新通过。

## 失败记录与范围边界

首轮锐化 shader 缺少 Core.hlsl，导致 UI 正常而 3D 视口黑屏；当时旧检查仍给出 passed=true，已判为无效。保留 `model/reports/pump-clearance-clarity-1600/VISUAL-FAILURE.md` 标记；该目录不作为成功证据。随后修复 include，并增加构建期 shader 编译错误检查、全屏 shader 支持检查和中央 3D 区域亮度检查，最终通过结果仅以上述 final 两目录为准。

改动限定在 `model/`、README 与本专项记录。未修改主工程 Assets、小车、主场景或其他开发副本；其余三类设备、主车间接入、巡检导航和业务闭环不在本次验收范围。

## 后续修正：护罩下方悬空支架

用户截图指出两侧支架上端与护罩断开。检查源模型发现旧支架下端也未落在底板上，且六个支架网格错误归在联轴器总成。

将两根横跨轴线的旧托梁改成两侧底梁，底面落在设备底板 z=0.114 m；四根立柱从底梁顶面 z=0.132 m 延伸至护罩底边 z=0.652 m。六个网格全部归安全护罩总成，随护罩运动；三个 Blender Scene 和生成器已同步。当前仍为 12 总成、349 网格、119,696 三角面，联轴器含 9 网格，安全护罩含 18 网格。

加长支架后，首次运动检查发现旧侧移路径会穿过联轴器；保留 `pump-guard-support-motion.log` 记录，随后将护罩改为先退让、抬高 0.85 m，再侧移 0.80 m 后降到展示位置。最终 `pump-guard-support-motion-final.log` 和 `water-pump-motion-intersections.json` 的 300 位置检查通过，未解决初始穿透、新增相交和运动中持续穿透均为空。组装反向使用相同路径。

`model/reports/water-pump-guard-support-contact-verification.json` 的 10 项护罩支架连接检查及归属检查通过，1 mm 连接容差；底梁与底板、立柱与底梁的测量间隔为零，立柱与上边框的顶点至表面距离约 0.414 mm，含倒角影响。复现：Blender 后台运行 `model/scripts/verify_pump_contacts.py -- --guard-only`。本次还执行了原有全组接触脚本，结果未通过，见 `pump-guard-support-contacts.log` 和 `water-pump-contact-verification.json`，涉及泵脚、电机垫片、轴承座等其他连接；未修改这些范围，不宣称整机全部接触合格。

源文件重新打开检查通过，记录于 `pump-guard-support-source-final.log`。构建日志为 `pump-viewer-build-20261111-191605.log`；本轮独立程序结果保存在 `model/reports/pump-guard-support-final-1920-191710/`，与上文较早的双分辨率记录分开。

该目录 `verification.json` 本次 passed=true，1920×1080、12 总成、349 网格、119,696 三角面，源分组一致；8 个观察角度的标注重叠和引导线交叉均为零，归位误差约 2.38×10⁻⁷ m，无运行错误。已查看本次 assembled.png 和 exploded.png，确认支脚连接及随护罩拆出。默认启动检查另存 `model/reports/pump-guard-support-default-window/`：2240×1260 客户区通过，程序以整机状态留开供用户审核。本次几何修正后只重跑 1920×1080 运行检查，1600×900 仍是上文旧几何版本的历史结果。
