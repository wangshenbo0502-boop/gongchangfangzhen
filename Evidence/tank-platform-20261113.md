# TK-01 储罐接入统一平台

2026-11-13，Asia/Shanghai。经用户验收，平台的 TK-01 工位和设备观察窗口改用独立展示器的储罐资产。导入了 12 个总成、892 个网格、245,364 个三角面、9 张材质引用贴图和反射环境。平台读取同一份 motionVersion 3 清单，并通过 FBX 的坐标基准还原路径和 14 秒单程时长。

车间模型按 60% 等比放置，保留 TK-01 的原工位、巡检点及设备状态。设备详情仍显示约 5.86 m 总高、3.00 m 筒体直径的名义尺寸。故障演示使用原储罐教学模型，以保留预设漏损、液位和支撑变形的展示。车间内的告警标记、小车和工单仍绑定 TK-01。

## 本次运行验证

Unity 6000.3.8f1 Windows x64 [构建报告](unified-build-20261113-211351.txt) 显示 Succeeded、Errors 0，包含车间监控场景和官方小车训练场景。批处理结束时，既有中文路径 TypeDb 缓存问题仍使编辑器退出码为 1。生成的 Windows Player 已单独运行。

1920×1080 [验证记录](tank-platform-20261113/1080/verification.txt) 通过 46 项。检查原工位落地、邻机净空、材质引用、人孔盖先沿法兰轴退出、退出中反向、全拆全装回位、漏损教学模式和 TK-LEAK 派工。实际巡检小车到达原巡检点，工单进入维修阶段。“设备学习 → TK-01 → 详细展示”调用了配套的独立展示器。本次验证通过可见 UGUI 控件射线和点击事件触发。

- [车间工位](tank-platform-20261113/1080/01-tank-in-workshop.png) · [整机观察](tank-platform-20261113/1080/02-assembled.png) · [人孔盖退出](tank-platform-20261113/1080/03-cover-release.png) · [完全拆开](tank-platform-20261113/1080/04-exploded.png)
- [漏损教学](tank-platform-20261113/1080/05-fault-teaching.png) · [小车到场](tank-platform-20261113/1080/06-robot-arrived.png)

正式入口为 `Build/WorkshopPlatform/WorkshopPlatform.exe`。部署前保留了原平台目录，并逐文件核对了 205 个主程序文件、167 个储罐展示器文件和其余三个展示器的 504 个文件。正式入口在 1280×720 复跑同一组 46 项，Windows 确认“详细展示”启动正式目录下的 TankViewer 窗口。[部署清单](tank-platform-20261113/deployment.json) · [正式版验证](tank-platform-20261113/live-720/verification.txt) · [正式版展示器窗口](tank-platform-20261113/live-720/viewer-window.txt)

全部网格接触采样见[独立展示器验收记录](tank-viewer-fit-20261113.md)。本次主平台验证覆盖导入后的指定动作、TK-01 工单和展示入口。官方训练和其他设备的全套业务流程未在本次运行复测。
