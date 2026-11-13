# 设备页详细展示验证

2026-11-13，Unity 6000.3.8f1 Windows Player，车间监控场景。设备学习目录进入整机与部件页后，“详细展示”位于“故障学习”右侧，使用该页的设备打开配套 Unity 程序。

| 设备 | 展示器 |
| --- | --- |
| P-01 循环水泵 | WaterPumpViewer.exe |
| TK-01 工艺储罐 | TankViewer.exe |
| F-01 车间排风机 | IndustrialFanViewer.exe |
| PLC-01 设备控制柜 | CabinetViewer.exe |

在 1280×720 Windows 检查版用系统鼠标逐台点击设备目录和“详细展示”，核对新进程的绝对路径与窗口句柄，四台均通过。[点击记录](detailed-viewer-20261113/WorkshopPlatformUIReview.txt) · [水泵入口截图](detailed-viewer-20261113/WorkshopPlatformUIReview-WaterPumpViewer.png)

Player 构建成功。批处理退出仍出现已知的中文路径 TypeDb 缓存问题。部署使用经过检查的 Player 文件，备份原主程序，将现有独立储罐展示器复制到配套目录，并逐项核对交付文件及此前三个独立展示器的哈希。[部署记录](detailed-viewer-20261113/deployment.json)
