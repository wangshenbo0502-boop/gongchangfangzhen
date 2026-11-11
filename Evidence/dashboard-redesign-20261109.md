# 2026-11-09 数字大屏 UI 替换验收

场景：`Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`。

环境：Unity 6000.3.8f1，现有 URP、新 Input System，Editor Play / Direct3D 11。时间均为本机 +08:00。

## 已实现

- 安装 `bigdata-dashboard-skill`，选用 `device_monitor`（064 设备环境监测平台）。模板源码归档、来源提交和 AGPL-3.0 许可证见 `Tools/DashboardTemplate/README.md`。
- 将模板结构移植为 Unity 原生 UGUI，地图改为已有三维车间，不添加 WebView 或网页依赖。
- 重做顶部指标、左侧设备态势与台账、温度曲线、中央相机工具栏、右侧设备详情与助手、底部告警和巡检操作。
- 新增 `WorkshopDashboardGraphic.cs` 及 Unity 生成的 `.meta`；环图读取现有设备业务状态。`WorkshopTrendGraphic.cs` 增加面积填充，UI 将范围设为 20–120°C。
- 正常运行率计算正常运行且无故障的设备比例；顶部运行设备计数沿用业务的 `IsRunning`，因此预警运行设备会包含在顶部运行数、归入环图故障分类。
- 修复缩放后的文字模糊、环图数字容器过小导致字形网格为空，以及相机视口外空隙不刷新的弹窗残影。

未修改业务模拟、设备故障规则、相机控制源码或服务端。未执行场景重建、覆盖保存场景、切换构建入口、升级包或重新提取资源。

## 本次实际通过

| 范围 | 执行时间 | 报告 | 结果 |
| --- | --- | --- | --- |
| 1920×1080 大屏 | 23:54:29–23:54:46 | `dashboard-redesign-1920-20261109.txt` | COMPLETE |
| 1280×720 大屏 | 23:56:04–23:56:21 | `dashboard-redesign-1280-20261109.txt` | COMPLETE |
| 1920×1080 完整故障回归 | 23:57:00–23:57:47 | `dashboard-redesign-faults-1920-20261109.txt` | COMPLETE |

双分辨率检查包含文字高度、环图百分比实际字形网格、状态统计联动、温度曲线网格、中央视口面积、UGUI 首个射线命中与按钮响应。使用现有业务状态验证设备选择、停机/启动、故障注入、禁止提前处理、派发及防重复、NavMesh 到达、确认处理、恢复运行和单条记录、记录页签、两次重置、长中文对话滚动。截图尺寸和非空像素检查通过。

故障套件验证 24 个唯一机制与 32 组设备/故障组合，涵盖下拉菜单选择射线、故障弹窗中文文字、预警继续运行与联锁停机、重复注入/派发/处理保护、全导航/处理/归档、六类示意特效、三设备并发告警、人工停机状态恢复、连续重置。Abaqus 协议仅使用内存合成测试，未伪造或加载真实求解结果。

已查看 `dashboard-redesign-overview-{1920,1280}-20261109.png`、`dashboard-redesign-alarm-{1920,1280}-20261109.png` 及 `dashboard-redesign-fault-dialog-1920-20261109.png`，最终截图中文字清晰、环图数字可见、三维车间正常渲染，弹窗关闭后空隙无残影。

## 重跑方法

每种分辨率使用独立 Unity 进程，先退出占用本工程的 Editor。以 1920 为例：

```powershell
& 'D:/unity/Editor/Unity.exe' -batchmode -force-d3d11 `
  -projectPath 'D:/UnityProjects/工业车间设备三维可视化监控与交互巡检系统' `
  -executeMethod WorkshopDemo.Editor.WorkshopTwinValidation.Run `
  -twin-width 1920 -dashboard-redesign `
  -logFile 'Evidence/dashboard-redesign-1920.log'
```

1280 将宽度和日志名改为 1280；完整故障套件将方法改为 `WorkshopDemo.Editor.WorkshopTwinValidation.RunFaults`。异步套件会自行退出，不传 `-quit`。`-dashboard-redesign` 使用本轮独立报告和截图前缀，不覆盖原 `twin-*` 或 `fault-*` 证据；同名前次报告会另存 `.previous`，重跑前仍应保护需要保留的截图。

## 历史与未验证项

- 修改前的 `WorkshopUI.cs`、`WorkshopTrendGraphic.cs` 保存在 `UIBackup-20261109-234307/`。
- 首轮同帧开启弹窗后立即注入，射线断言失败；加入 `.2` 秒界面刷新等待后通过。前次报告保存为 `.previous`。
- 第一次截图文字模糊；第二次截图显示环图百分比为空及弹窗残影，均已修正并重新运行。前两版截图/日志保存在备份目录，不作为最终显示验收依据。
- 本次三份最终报告无 `FAIL` / `RUNTIME ERROR`，编译无 C# 错误。Unity 日志仍有官方源码既有警告、阴影提示、启动许可令牌提示及助手离线的连接提示，不声称整个工程日志完全无提示。
- 知识库服务本轮离线，UI 如实显示“知识库服务离线”。保留助手入口及长文本滚动；未重新验证在线回答，历史 DeepSeek 401 限制仍适用。
- 未验证 Windows EXE、真实采集、OS 鼠标拖拽相机及官方训练流程。完整 32 组故障回归仅在 1920×1080 执行。
