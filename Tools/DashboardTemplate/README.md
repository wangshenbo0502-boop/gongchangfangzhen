# 数字大屏模板来源与移植

- 技能：`bigdata-dashboard-skill`，已安装到本机 Codex skills 目录。
- 上游：`https://github.com/AragornZJF/bigdata-dashboard-skill`
- 分支：`master`
- 归档提交：`9b581ccebf5dd050aee38961e6f8bf3387948d80`
- 主布局参考：`device_monitor`，064 设备环境监测平台。
- 2026-11-10 仪表视觉参考：技能内置 `power_plant`，结合原项目重做为三维空间 HUD。这里只保留 `device_monitor` 源码归档，没有复制 `power_plant` 的源码或图片。
- 上游许可证：AGPL-3.0，原文见同目录 `LICENSE`。归档保留原有来源信息。

`device_monitor/` 保存下载的模板骨架，供追溯设计来源；不是 Unity 运行时依赖。原始 HTML 包含上游外部跳转标签，因此不作为交付预览入口运行。

本项目将设备统计、状态环图、设备台账、维护事件的布局移植为 Unity UGUI。2026-11-10 根据视觉反馈进一步重做：折角中轴标题、分段旋转环仪表、半透明三维仪表条、设备空间标签与引线、选中设备扫描框、实际 XZ 定位图、分段温度计和四阶段巡检流程。色彩使用近黑底、青绿信号、浅绿运行状态、琥珀预警和粉红严重告警。地图主体替换为已有车间的实时三维相机视口。

运行入口仍为 `Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`：

- `WorkshopUI` 在运行时构建界面，并设置本界面的总览俯角和距离，不覆盖场景。
- `WorkshopDashboardGraphic` 绘制状态环图；`WorkshopTrendGraphic` 绘制实际采样温度曲线。
- `WorkshopHudGraphic` 绘制边框、标题信号、温度刻度、实际设备/机器人 XZ 位置、NavMesh 路径和巡检阶段。
- `WorkshopSceneOverlay` 将设备投影为可点击标签，选中框跟随实际设备包围盒；运行期间隐藏重复的旧世界文字，停用时恢复。
- `WorkshopControlHint` 为图标按钮提供悬停名称。

状态与指标来自 `WorkshopSimulation` 和 `WorkshopDevice`；定位路径来自现有 `NavMeshAgent`。动画仅表示界面信号和选择状态，没有新造业务数值。未接入模板的虚构示例数据、地理位置或网页跳转。

此次仅替换运行时界面，不需要执行覆盖场景的 `Build Demo`。

本轮验收范围与截图见 `Evidence/dashboard-hud-20261110.md`；2026-11-09 旧版验收保留在原文件中。
