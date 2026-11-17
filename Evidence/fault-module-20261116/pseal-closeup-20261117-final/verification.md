# P-SEAL 近景修正：2026-11-17

## 候选与修改

- 候选：`Build/WorkshopFaultReview-20261117-130035291/WorkshopPlatform.exe`；未替换 `Build/WorkshopPlatform/` 日常入口。
- 主视图继续显示泵体、轴系和支撑上下文；P-SEAL 专用近景不再将支撑脚纳入近景层，近景集中显示密封目标、密封环和滴液。
- 修改源码：`WorkshopPumpFaultPresenter.cs`、`WorkshopEquipmentView.cs`；新增自动断言位于 `WorkshopCompactUIVerification.Pump.cs`。
- 未修改场景、`.meta`、拆装配置、受保护展示器或桌面分辨率/显示模式。

## 本次验证

- 构建日志 `pseal-closeup-build-final-20261117.log` 报告 `Build Successful`，Player 产物已生成并运行；但 Unity Editor 随后输出 `Scripts have compiler errors.`，最终 Editor 退出码为 1。日志中未找到对应 `error CS`。因此只记录 Player 构建及专项结果，不宣称 Editor 正常退出或零编译错误。
- `pump-720/verification.txt`：1280×720 可见 Windows Player，P-SEAL/P-CAV 泵专项 `Result: PASS`，覆盖密封近景与主视图范围断言、原密封环、剖视恢复、汽蚀切换、音频与业务闭环。该项不覆盖真实鼠标、求解数据或性能。
- `visual-pseal-720/verification.txt`：1280×720 P-SEAL 固定视角、阶段及里程碑邻帧捕获 `Result: PASS`。直接审看近景截图，支撑脚已不再占据小窗，密封环与滴液可见；此为定向人工审图，不代表所有案例或所有角度通过。
- 四个受保护展示器共 671 个文件，候选与 `Build/WorkshopPlatform/Viewers/` 逐文件 SHA-256 一致：缺失 0、差异 0、额外 0。
- Unity Editor、Player 和 Crash Handler 进程复核时均未运行。
- `Tools/FaultAssets/build_review_sheet.py` 固定要求 166 张图，本次 P-SEAL 定向采集只有 20 张；因此该拼图脚本不适用，其数量门槛失败不代表截图采集或上述 P-SEAL 专项失败。

## 验收边界

本次只验证 P-SEAL 近景取景修正及相关泵专项。T00–T13 仍不能整体标记完成：32 组案例的精细几何、部分案例全部角度/连续动态与声音仍需验收；没有真实 ODB 的同实例同位置三点三帧对照，也没有四案例真实 Abaqus 求解。所需工程输入、ODB 及 Abaqus 环境见 `AnalysisCases/`。性能只沿用此前候选的短时采样结论，不转记为本候选性能验收。
