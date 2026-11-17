# 局部特写与后台渲染修订

日期：2026-11-16。日志内实际时间采用本机 +08:00；文件名中的 2045、2050、2055 是本次任务标签，不是精确启动时间。业务场景 `WorkshopDemo`，Unity 6000.3.8f1、URP、PC 画质。日常 `Build/WorkshopPlatform/` 未替换。

## 实现

- `WorkshopFaultPresenter.cs`：保留派生 12 mm 穿孔与原 8 mm 壁厚；近景范围收紧至漏点附近 120×150×120 mm，中心向下 25 mm。湿润层表面偏移由 4 mm 缩小至 0.6 mm。液滴改为圆润网格、较小尺寸、靠近漏点的连续落下范围及较小侧向漂移，仍为教学演示，不提供求解漏率。
- `WorkshopEquipmentView.cs`：TK-LEAK 特写使用独立局部范围与略斜观察方向；设备窗口遮住车间时暂停主车间 Camera，关闭、销毁或隐藏 UI 时恢复此前启用状态。原本已禁用的相机不会被误启用。模拟、告警及巡检仍运行。
- `WorkshopCompactUIVerification*.cs`：视觉/性能可通过 `-workshop-review-case TK-LEAK` 定向执行；记录穿孔投影像素与初始、25%、末态、恢复特写。性能检查新增主相机生命周期断言。

## 候选与验证

候选 `203857001` 包含特写修订；候选 `204305255` 进一步包含后台相机优化。两次 Editor 均退出 0；没有运行 Build Demo，未重建或保存业务场景。

`203857001`：

- [定向视觉](local-closeup-review-20261116-2045/verification.txt)：1280×720 可见窗口，exit 0 / PASS，穿孔投影直径 28.3 个近景 RT 像素。
- 直接检查初始、25%、末态图：初始无孔/水迹，25% 可见局部孔口和小水迹，末态能分辨孔口、周边水迹及小圆润液滴。旧图的巨大菱形液滴已消除；该静态检查不等于全部视角和动态漏流验收。
- [优化前定向性能](local-closeup-performance-before-20261116-2050/performance.csv)：1920×1080，TK-LEAK 效果关闭/开启平均 19.883 / 20.240 ms。

`204305255`：

- [1080p 性能及相机生命周期](background-camera-performance-1080/verification.txt)：exit 0 / PASS。五项相机恢复断言通过；六重点案例效果开/关各采样 8 秒，单个可见窗口顺序执行。
- [原始统计](background-camera-performance-1080/performance.csv)：效果开启平均/P95/最大帧时如下。它们是短期采样，不保证长时运行无慢帧，也不是全部升级前后对比。

| 案例 | 平均 ms | P95 ms | 最大 ms |
| --- | --- | --- | --- |
| TK-LEAK | 3.347 | 3.502 | 11.898 |
| P-SEAL | 3.346 | 3.441 | 23.144 |
| P-CAV | 3.343 | 3.448 | 14.938 |
| F-CRACK | 3.342 | 3.502 | 12.474 |
| F-IMBAL | 3.346 | 3.448 | 16.266 |
| PLC-CONTACT | 9.112 | 10.612 | 25.803 |

本机 RTX 4060 Laptop / i9-14900HX / D3D11，vsync 0、targetFrameRate -1。六案例的平均与 P95 均低于 16.67 ms，但 P-SEAL、PLC-CONTACT 仍有超过该值的帧。TK-LEAK 优化前后使用相同分辨率与采样逻辑；主要代码差异为关闭被完全遮住的主相机。此比较表明后台渲染有显著开销，不作为 CPU/GPU Profiler 归因。

六轮窗口开关后 Mesh 5556、Material 534、RenderTexture 33 均保持一致，不能据此宣称长期零泄漏。CSV 内 working set 为 0，表示该接口不可用，不能解释为零内存。

本候选另外执行了 [1080p 储罐专项](background-camera-tank-1080/verification.txt)，116 PASS / exit 0，及 [720p TK-LEAK 定向视觉](background-camera-visuals-720/verification.txt)，26 PASS / exit 0。直接查看 `TK-LEAK-ui.png` 确认整机和特写都在渲染，后台相机优化没有造成设备画面空白。静帧和非空断言不代替所有角度、连续漏流与全部材质验收。捕获工具设定预览进度后未显式刷新暂停的 UI 文字，因此截图进度文字可能落后于模型，不据此判断初始态。

[真实鼠标引导实训](mouse-guided-204305255/verification.md)：普通启动 P-SEAL 注入、派发、实际到场、证据/诊断/匹配维修、提交、缺复验拦截、补录复验、验收归档通过。复盘为 1 归档、0 未关闭；Player 已正常请求关闭并确认退出。仅覆盖此单案例。

[文件保护](background-camera-delivery.json)：日常目录和候选目录各 671 个展示器文件长度/哈希一致，四个拆装配置与 HEAD 语义一致，相关 `.meta` 缺失 0。受保护水泵展示器未被改写。

真实 ODB 三点三帧及四案例 Abaqus 求解仍未完成；本次改动与上述性能成绩不能替代这些验收。
