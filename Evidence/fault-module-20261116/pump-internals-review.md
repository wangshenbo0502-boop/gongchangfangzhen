# 水泵内部结构与局部磨蚀：2026-11-16

场景：`Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`。Unity 6000.3.8f1、URP、新 Input System。时间采用 Asia/Shanghai。

## 本次候选与改动

程序：`Build/WorkshopFaultReview-20261116-211755552/WorkshopPlatform.exe`。使用 `R:\` 项目路径构建；[构建日志](pump-internals-build-20261116-2119.log)记录 Editor exit 0，无 C# 编译错误或 Missing Script 构建警告。未运行 Build Demo，日常 `Build/WorkshopPlatform/` 未替换。

- P-IMPELLER 新增“看叶轮”，隐藏原蜗壳、入口盖和叶轮前盖完整总成，包括附属仪表、管件和紧固件；原叶轮保留。第一叶片外缘 22% 半径区域在运行时 Mesh 副本上按进度预设最多 2 mm 的径向损失，内部大部分顶点及拓扑保持不变；不是冲蚀求解。
- P-BEAR 新增“看轴承”，让开原轴承壳和护罩，显示原轴承圈、滚珠和轴。仍用可关闭轨迹表示教学振动；尚无滚道剥落几何或接触求解。
- P-INLET 将参考/异常流向标记放到已经缓存的实际外表面，焦点缩至 340 mm 区域，避免标记埋入实体。尚无实际滤网或堵塞物几何。
- 关闭剖视、切换模式和退出时恢复原 Renderer.enabled、共享 Mesh、局部变换及观察层。车间活动设备不进入剖视，也不使用观察室 27/28 层。

源码入口：`WorkshopLocalFaultPresenter.cs`、`.Mechanisms.cs`、`WorkshopEquipmentView.cs`、`WorkshopUI.Equipment.cs`、`WorkshopFaultCase.cs`。验证入口：`WorkshopCompactUIVerification.VisualPerformance.cs`。

## 本次已运行检查

以下是本次候选的可见 Windows Player、实际 UGUI 射线与控件事件结果，各进程 exit 0，报告均为 `Result: PASS`，零 FAIL。

| 输出目录 | 分辨率 | PASS | 范围 |
| --- | --- | --- | --- |
| [impeller-720](pump-internals-211755552/impeller-720/verification.txt) | 1280×720 | 41 | 叶轮四角度/阶段截图、剖视按钮、完整遮挡总成、局部磨蚀、倒拖、完好网格、恢复与层隔离 |
| [bearing-1080](pump-internals-211755552/bearing-1080/verification.txt) | 1920×1080 | 37 | 轴承画面、剖视按钮、原内部结构与可见性/层恢复 |
| [inlet-720](pump-internals-211755552/inlet-720/verification.txt) | 1280×720 | 25 | 入口画面采集及控件布局 |
| [pump-1080](pump-internals-211755552/pump-1080/verification.txt) | 1920×1080 | 100 | 现有 P-SEAL/P-CAV 专项，检查新增局部 Presenter 未破坏密封/汽蚀 |
| [impeller-performance-1080](pump-internals-211755552/impeller-performance-1080/verification.txt) | 1920×1080 | 9 | 剖视开启后效果开/关各约 8 秒采样，后台相机状态恢复 |

直接查看过本候选的 [叶轮界面](pump-internals-211755552/impeller-720/P-IMPELLER-ui.png)、[叶轮末态](pump-internals-211755552/impeller-720/P-IMPELLER-focus-1.000.png)、[轴承界面](pump-internals-211755552/bearing-1080/P-BEAR-ui.png)和[入口界面](pump-internals-211755552/inlet-720/P-INLET-ui.png)：原叶片、轴承圈/滚珠、入口标记可辨认，叶轮视图原悬空仪表问题已消除。静帧不能验收全部连续运动、音效、法线或所有观察角度。

## 短时性能与资源

RTX 4060 Laptop、i9-14900HX、D3D11、PC 画质、vsync 0、target -1；1920×1080，P-IMPELLER 剖视打开。[原始统计](pump-internals-211755552/impeller-performance-1080/performance.csv)：

| 效果 | 帧数 | 时长 s | 平均 ms | P95 ms | 最大 ms | Unity allocated 峰值 |
| --- | --- | --- | --- | --- | --- | --- |
| 关闭 | 2401 | 8.004721 | 3.333203 | 3.414154 | 6.699562 | 433691833 B |
| 开启 | 2400 | 8.002430 | 3.334165 | 3.414154 | 7.682800 | 433683817 B |

开窗耗时约 0.02385 s。WorkingSet API 返回 0，表示该项不可用，不是零内存。这是当前效果开/关对比，不是升级前/后基线，不代表长期稳定帧率或六案例重测。六次开关观察中 Mesh 5556、材质 534、RenderTexture 33 保持稳定；分配内存 460252496→476673920 B、托管内存 14573568→15011840 B。开关循环使用 PLC-CONTACT，不能计作叶片磨蚀专项生命周期或长期无泄漏认证。

## 文件保护与失败记录

[交付核对](pump-internals-delivery-211755552.json)：日常/候选各 671/671 个展示器文件长度及哈希一致；包含受保护 WaterPumpViewer。四份总成/运动配置与 HEAD 语义一致，新增资源 `.meta` 零缺失。EXE 与业务程序集 SHA256 见该 JSON。本轮未更改桌面分辨率或显示模式。

保留中间失败记录：`210950180` 首次剖视自动点击在同帧创建窗口后命中底层 Robot status；验证器等待一帧再进行射线检查后修正。`211412961` 自动检查通过，但审图发现隐藏壳体后附属仪表/管件悬空；改为隐藏完整总成，并在本候选增加对应断言。没有用中间候选截图冒充最终结果。

## 未覆盖与后续

本候选不继承 `204305255` 的储罐、六案例性能和普通启动真实鼠标成绩；这些结果仍保留在原候选记录。也未重跑此前双分辨率全目录回归。T06/T08 精细效果及连续运动/音效仍有剩余；真实 ODB 三点三帧和四案例 Abaqus 求解缺输入，T00–T13 不能整体验收。
