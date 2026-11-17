# 轴承、叶轮聚焦及入口受阻复核

场景：`Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`。Unity 6000.3.8f1、URP、新 Input System。时间采用 Asia/Shanghai，日期 2026-11-16。未改变桌面分辨率/显示模式；验证参数仅设置 Player 窗口尺寸。

## 实现与来源

- `WorkshopEquipmentView.LateUpdate()`：仅在轴承/叶轮故障内部剖视且聚焦时，主观察相机只渲染局部检查层。回到正面/侧面/斜视显示完整设备，不改车间相机或源资产层。
- `WorkshopLocalFaultPresenter.Mechanisms.cs`：原外圈滚道运行时 Mesh 副本在局部 36 度范围预设最多 1 mm 材料损失，大部分原顶点不变；配合可关闭的青色参考/橙色异常线。保留原轴承圈、滚珠、轴和拓扑。这不是接触、剥落增长或寿命求解。
- P-INLET 新增 `pump.suction.bore` 绑定，指向原吸入凸面法兰和管口。按原网格实际内孔尺寸生成教学网格，置于原法兰面后方；受阻物为随进度增加的预设扇区。共用网格/材质并复用 66 顶点缓冲，倒拖按输入重建；观察模式和退出时关闭插入件并恢复原层。
- 滤网是新增教学插入件，不表示原设备实际装配了过滤器；受阻范围、流量下降和汽蚀之间未做流阻求解。
- `WorkshopFaultCase.cs` 与水泵知识文档同步来源边界；本候选验收时尚未重新入库，后续一致性核对见 [风机修订交付](fan-phase-review.md)。

对应 T04/T06/T08/T13 的局部实现与必要检查；不是全部任务验收。源场景、原总成、共享 Mesh 和独立展示器保持原内容。

## 候选与实际结果

最终候选：`Build/WorkshopFaultReview-20261116-214915712/WorkshopPlatform.exe`。使用 `R:\` 工程别名调用 `WorkshopDemo.Editor.WorkshopPlatformBuild.BuildFaultReview`。[构建日志](inlet-strainer-build-20261116.log)记录 Build Successful、Editor exit 0。日志有许可证通知通道信息，但没有 C# 编译错误或 Missing Script 构建警告；运行检查未报告异常或 Missing Script。

以下均为该最终候选、可见 Windows Player、exit 0、零 FAIL：

| 输出 | 分辨率 | PASS | 范围 |
| --- | --- | --- | --- |
| [综合回归](final-214915712/regression-1080/verification.txt) | 1920×1080 | 756 | 32 适用案例、64 次原启停组合实际巡检、可逆预览、训练门禁及合成分析协议 |
| [密封/汽蚀专项](final-214915712/pump-1080/verification.txt) | 1920×1080 | 100 | 原密封/汽蚀结构、回放、音源播放/静音/暂停状态、实际巡检与验收后清理 |
| [入口专项](inlet-strainer-214915712/inlet-720/verification.txt) | 1280×720 | 32 | 原孔径内放置、完好无受阻物、倒拖复现、辅助线关闭后保留受阻物、退出恢复、控件及画面采集 |

直接查看最终 [入口近景](inlet-strainer-214915712/inlet-720/P-INLET-detail.png) 与 [末态聚焦](inlet-strainer-214915712/inlet-720/P-INLET-focus-1.000.png)：原法兰、孔内滤网、预设受阻物可区分；受阻物未延伸到原法兰孔外。该几何为简单教学插入件，不是实物过滤器的建模验收。

前一候选 `214307642` 的内部聚焦检查分别为 [轴承 1080p 47 PASS](internal-focus-214307642/bearing-1080/verification.txt) 和 [叶轮 720p 45 PASS](internal-focus-214307642/impeller-720/verification.txt)，均 exit 0、零 FAIL。已直接查看它的轴承与叶轮末态聚焦图：轴承圈/滚珠或原叶片可见，先前遮挡消除。这些截图只证明该候选的可见画面；最终候选在其基础上增加入口功能，没有重新采集轴承/叶轮的整组画面。

## 保护与限制

[最终交付核对](final-214915712-delivery.json)：日常 `Build/WorkshopPlatform/Viewers/` 与候选副本各 671/671 个文件长度/哈希一致，包含 WaterPumpViewer；四份总成/运动配置与 HEAD 语义一致，相关资源 `.meta` 无缺失。程序/程序集指纹记录在 JSON。未运行 Build Demo，未替换日常入口。

综合回归中的四秒预览帧耗时记录为 mean 3.345063 ms、P95 3.395302 ms、max 17.3504 ms，仅属于该回归末尾的单预览；不能当作六案例性能、长期稳定性或升级前后基线。六案例性能及真实鼠标闭环仍引用各自历史候选记录。

保留拒收记录：`bearing-focus-213515452/` 隐藏运行生成黑图，不作为视觉证据；`bearing-focus-visible-213515452/` 断言通过但主聚焦仍受周边零件遮挡，故继续修复。最终记录没有把黑图、非空检查或自动断言计为人工视觉合格。

本候选当时尚缺 F-IMBAL 转子/轨迹相位一致性；后续候选 `220916628` 已修复并完成定向检查，见 [同相回放证据](fan-phase-review.md)，不追记为本候选成绩。完整连续运动、全部精细效果与声音听感仍需分别验收。真实 ODB 三点三帧、四份算例工程输入和真实 Abaqus 求解仍缺，合成协议检查不替代求解。
