# 统一平台水泵接入验证

日期：2026-11-11（Asia/Shanghai）

## 本次结果

- Unity `6000.3.8f1` 平台构建结果：Succeeded，构建报告 `Errors: 0`。
- 输出：`Build/WorkshopPlatform/WorkshopPlatform.exe`，构建报告记录大小 1,349,263,960 bytes。
- 场景：`Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`、`Assets/WorkshopDemo/External/FactoryTraining/Scenes/urp_scene_with_training.unity`。
- 新精细水泵 Prefab 和 12 总成配置已导入 `Assets/WorkshopDemo/Resources/ReviewedEquipment/`，供循环水泵设备运行时加载。
- 独立查看器随平台输出到 `Build/WorkshopPlatform/Viewers/WaterPumpViewer/`。
- 已从目标输出目录启动平台程序；窗口标题正确，进程持续响应。运行日志初始化 Unity 6000.3.8f1、D3D11 与 NVIDIA RTX 4060 Laptop GPU，未见 C# 异常。

## 尚未验证

- 本次未进行鼠标自动化操作，未独立确认主界面中水泵外观、选中态及“详细展示”按钮的点击闭环。
- 已知独立展示器的双击部件简介等交互验证见 [pump-viewer-20261111.md](pump-viewer-20261111.md) 与 `model/reports/pump-viewer-dblclick-final-1920-20261111/verification.json`；这不替代主平台交互验收。
- 日志中的无效 NavMesh 警告和阴影图集分辨率自动降低提示仍存在，不影响本次确认的启动响应，不据此宣称其余业务已完整回归。

## 操作入口

运行 `Build/WorkshopPlatform/WorkshopPlatform.exe`，在监控场景选择循环水泵，再使用设备详情中的“详细展示”入口打开独立泵查看器。
