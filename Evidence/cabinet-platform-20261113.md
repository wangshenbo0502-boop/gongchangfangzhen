# PLC-01 控制柜接入统一平台

2026-11-13，Asia/Shanghai。经用户确认，把独立展示器中审看通过的控制柜接入主项目，更新本地 `Build/WorkshopPlatform/WorkshopPlatform.exe`。

- 车间中的 PLC-01 与设备观察窗口均加载同一份控制柜 Prefab。FBX 和 motionVersion 4 清单直接取自独立展示器工程。共 16 个总成、3,247 个网格、119,066 个三角面，导入 39 张材质引用贴图。渲染适配主工程的聚类灯光。
- 平台内拆装采用同一份路径及 14 秒单程时长。柜门、HMI 和散热组件先直线退出 160 mm，再旋转。线束断开与接回通过淡入淡出演示。原工位、选中框、巡检点和设备状态绑定继续使用 PLC-01 原有配置。
- 车间总览选中 PLC-01 后点“查看设备 → 拆装”。“视图 → 查看柜内”可只打开柜门，“在独立展示器中打开”启动平台配套的 `Viewers/CabinetViewer/CabinetViewer.exe`。Unity 菜单 `Tools/Workshop Demo/Import Reviewed Cabinet` 可重新生成资源。

Unity 6000.3.8f1 Windows x64 的最终 [BuildReport](unified-build-20261113-142756.txt) 为 Succeeded、Errors 0，保留监控场景和官方训练场景。批处理退出仍受工程中文路径下的 Unity 编辑器 TypeDb 缓存问题影响，退出码为 1。单独运行生成的 Windows Player 验证了交付物。

1920 × 1080 的 [Player 验证](cabinet-platform-final-1080-20261113/verification.txt) 通过 36 项。覆盖主场景加载、工位和落地、材质引用、界面射线点击、柜门保持角度退出、退出中反向、完整拆装归位，以及 PLC-01 停机、启动和 PLC-LOW 告警。Windows 侧独立记录了 [正确路径的查看器窗口](cabinet-platform-final-1080-20261113/viewer-window.txt)。首轮在 Unity Mono 内枚举子进程时异常退出，最终轮改用外部窗口检查。未复跑全部故障、工单和官方小车训练步骤。

实机截图见 [车间 PLC-01](cabinet-platform-final-1080-20261113/01-plc-in-workshop.png)、[装配](cabinet-platform-final-1080-20261113/02-assembled.png)、[直线退出](cabinet-platform-final-1080-20261113/03-door-clearance.png) 和 [完全拆开](cabinet-platform-final-1080-20261113/04-exploded.png)。原路径的文件核对与备份见 [部署记录](cabinet-platform-deployment-20261113.json)。
