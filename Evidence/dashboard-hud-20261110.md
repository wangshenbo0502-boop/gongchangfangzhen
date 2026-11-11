# 2026-11-10 空间 HUD 重做验收

场景：`Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`。打开已有场景并 Play 即显示当前 UI。

环境：Unity 6000.3.8f1、现有 URP 与新 Input System、Editor Play / Direct3D 11。以下时间均为本机 +08:00；结束时间取报告最后写入时间。

## 实现范围

根据用户对上一版缺少科技感、呆板的反馈，使用 `bigdata-dashboard-skill` 的设备监测布局和电厂仪表视觉作为参考，重做原生 Unity HUD。来源、归档提交和上游许可见 `Tools/DashboardTemplate/README.md`；`power_plant` 仅作视觉参考，没有复制其源码/图片，没有加载网页或模板虚构数据。

- 折角中轴标题、动态信号分段、旋转分段状态环、青绿主信号、琥珀预警、粉红严重告警。
- 半透明三维指标条、8 台设备矩阵、实际温度曲线、设备详情、分段温度计及知识助手。
- 设备屏幕投影标签与引线；点击选择真实业务设备。选中框使用实际 Collider 包围盒，带扫描线与旋转准星。隐藏重复的原世界文字，停用时恢复其原可见状态。
- XZ 定位图使用实际设备和机器人位置，任务路径使用现有 NavMeshAgent 的路径拐点。
- 告警、派发、到达、归档四阶段流程显示来自实际任务和记录；空闲显示未激活。保留故障工况选择、启停、并发告警、完整巡检闭环、记录和重置。
- 图标命令按钮增加悬停名称；本轮没有执行 OS 鼠标悬停验收。

更新 `WorkshopUI.cs`、`WorkshopDashboardGraphic.cs`、`WorkshopTwinValidation.cs`；新增 `WorkshopHudGraphic.cs`、`WorkshopSceneOverlay.cs`、`WorkshopControlHint.cs` 及 Unity 自动生成的 `.meta`。`WorkshopTrendGraphic.cs` 沿用上一轮实现。

总览构图在 `WorkshopUI.Start()` 设置俯角 52、距离 22，并调用现有 `Overview()`。本轮没有修改相机控制源码、业务模拟、故障规则、助手服务端、场景、构建入口、引擎/包或美术资源，没有执行 `WorkshopFoundationBuilder.Build()`。

## 最终实际验证

| 范围 | 执行时间 | 报告 | 结果 |
| --- | --- | --- | --- |
| 1280×720 UI 与业务闭环 | 00:49:37–00:49:55 | `dashboard-hud-1280-20261110.txt` | COMPLETE |
| 1920×1080 UI 与业务闭环 | 00:51:12–00:51:30 | `dashboard-hud-1920-20261110.txt` | COMPLETE |
| 1920×1080 完整故障回归 | 00:52:06–00:52:52 | `dashboard-hud-faults-1920-20261110.txt` | COMPLETE |

双分辨率 UI 检查包括：

- 实际分辨率、中央视口面积、温度曲线与状态环网格、百分比字形，以及四个顶部指标实际数字网格。
- 8 台设备空间绑定、可见标签、引线网格、不阻挡场景的装饰、定位图业务绑定；标题信号颜色随时间变化。
- 空间标签与设备矩阵的 EventSystem 首个射线命中、实际业务选择、停止/启动及正常运行率更新。
- 弹窗故障注入、禁止提前处理、派发及重复保护、温度仪表与选中设备同步、严重故障标签颜色。
- 机器人通过 NavMesh 实际到达、确认处理、恢复 8 台运行、归档单条记录、记录页签、连续两次重置；流程显示依次为告警/派发/到达/归档/空闲。
- 长中文对话滚动与顶部锚点、告警截图尺寸和非空像素。

完整故障套件验证 24 个唯一机制 / 32 组设备故障组合：故障下拉菜单射线、弹窗文字与滚动、预警继续运行及联锁停机、重复注入/派发/处理保护、实际导航/处理/带类型记录、六类示意特效、三设备并发任务保护、人工停机恢复、连续重置。测试中巡检机器人速度设为 18，仅影响本次 Play，不写入场景。Abaqus 协议使用内存合成测试，没有真实求解结果。

已查看最终截图：

- `dashboard-hud-overview-1920-20261110.png`
- `dashboard-hud-alarm-1920-20261110.png`
- `dashboard-hud-overview-1280-20261110.png`
- `dashboard-hud-alarm-1280-20261110.png`
- `dashboard-hud-fault-dialog-1920-20261110.png`
- `dashboard-hud-fault-Electrical-1920-20261110.png`

最终截图显示顶部数字与环图百分比可见、三维场景正常、空间标签与设备对应、选中框和故障颜色联动，检查范围内未见明显文字截断、界面重叠或弹窗残影。截图只展示静态时刻；标题信号的时间变化另有网格断言，未录制视频。

三份最终报告均无 `FAIL` / `RUNTIME ERROR`；对应日志无 C# 编译错误。仍有官方脚本既有编译警告、URP 阴影图集提示、启动许可令牌更新提示和助手服务离线的连接提示，不声称全工程日志完全无提示。

## 重跑

先退出占用本工程的 Editor，每次使用独立进程；不要影响其他工程的 Unity。

```powershell
& 'D:/unity/Editor/Unity.exe' -batchmode -force-d3d11 `
  -projectPath 'D:/UnityProjects/工业车间设备三维可视化监控与交互巡检系统' `
  -executeMethod WorkshopDemo.Editor.WorkshopTwinValidation.Run `
  -twin-width 1920 -dashboard-hud `
  -logFile 'D:/UnityProjects/工业车间设备三维可视化监控与交互巡检系统/Evidence/dashboard-hud-1920.log'
```

1280 将宽度及日志名改为 1280；故障回归将方法改为 `WorkshopDemo.Editor.WorkshopTwinValidation.RunFaults`。不要传 `-quit`，异步检查结束后自行退出。`-dashboard-hud` 使用独立报告和截图前缀；同名报告自动备份为 `.previous`，截图和日志重跑前另行保护。

## 备份与边界

- 修改前 UI、环图、曲线、相机和验证源码在 `UIBackup-HUD-20261110-003057/`；相机与曲线备份用于追溯，并不表示本轮修改过它们。
- 第一轮总览构图和扫描标记调整前截图在上述目录的 `iteration-01/`。
- 早期 1280 报告通过，但人工查看截图发现四项顶部数字在缩放后未显示。扩大数字容器、略减字号并增加真实字形断言后，重新通过双分辨率验证。该轮及中间 1920 截图、日志在 `iteration-02/`；早期报告不是最终显示验收依据。
- 2026-11-09 及更早证据、源码备份继续保留，没有用新报告覆盖历史 `dashboard-redesign-*`、`fault-*`、`twin-*`。
- 本轮助手服务离线，UI 如实显示；仅验收入口和对话排版，未验证在线模型回答，历史 DeepSeek 401 限制仍适用。
- 未验证 Windows EXE、真实工业采集、OS 鼠标相机拖拽/悬停或官方训练流程；32 组完整回归仅在 1920×1080 执行。
