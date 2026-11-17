# 2026-11-15 正式版全屏与详细展示

主平台的窗口模式原先禁止调整大小。旧故障候选包也没有配套 Viewers，因此“详细展示”会依实际文件可用性隐藏。此轮根据用户要求先完成展示入口与全屏。

- 主平台在监控和小车训练场景支持 F11，监控页的“更多”和设备观察页的“视图”也提供全屏／窗口切换。全屏采用桌面分辨率下的无边框模式，退出时恢复切换前窗口尺寸。项目设置启用窗口调整大小。
- “详细展示”按当前桌面尺寸给四个独立程序传入全屏启动参数。水泵的既有自动开窗选择会尊重显式宽高。候选构建复制本机配套展示器；正式版继续从原 Viewers 路径启动。
- 候选 Player 为 `Build/WorkshopFaultReview-20261114-185240267/WorkshopPlatform.exe`，Windows x64 BuildReport 成功且 0 错误。批处理仍有历史中文路径 TypeDb 问题，Editor 退出码 1。[BuildReport](../unified-build-20261114-185259.txt)
- 在 2560×1600 实体桌面使用 Windows 键鼠事件。经“更多”进入全屏并恢复到 1280×720，通过 F11 再次进入全屏。早期脚本因键盘扫描码、全屏坐标换算及窗口重建时句柄失效而重试。[主平台按键与菜单结果](fullscreen-final/WorkshopFaultReview-20261114-185240267.txt) · [主平台全屏画面](fullscreen-final/WorkshopFaultReview-20261114-185240267-platform-fullscreen.png)
- 四个独立展示器用各自的新平台进程测试，确认按钮启动了正确路径、传入全屏参数且客户区铺满 2560×1600。[四设备结果](viewer-launches/WorkshopFaultReview-20261114-185240267.txt)
- 720p Windows 候选版的储罐、工单及四设备绑定回归通过，Player 退出码 0。[回归结果](post-build-regression/verification.txt)
- 正式版保留原路径，备份并核对 205 个主程序文件后部署。独立展示器仍是原配套文件。[部署记录](fullscreen-deployment.json) · [展示器哈希核对](formal-viewer-protection.json)
- 部署后从正式路径复验水泵“详细展示”，确认启动的是正式 Viewers 下的进程，客户区为 2560×1600。[正式入口复验](formal-verified/WorkshopPlatform.txt)

当时已核对密封腔、静环、动环、入口盖、叶轮和出水压力表的导入位置。T03 水泵案例随后在候选版完成接入和验证。[水泵案例进度](../fault-pump-20261115-103703377/progress.md)
