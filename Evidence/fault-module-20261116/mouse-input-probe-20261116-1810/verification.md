# 候选 174917301 鼠标续测

日期：2026-11-16（Asia/Shanghai）。程序：`Build/WorkshopFaultReview-20261116-174917301/WorkshopPlatform.exe`。普通窗口启动，Player PID 35096；仅为匹配界面布局，将 Windows 显示模式临时切至 1280x720，退出后恢复为 2560x1600、240 Hz。

18:28–18:51 使用鼠标完成并观察到：进入实训任务、创建并注入 P-SEAL 故障、派发巡检、小车到场、填写故障原因、提交维修、点击验收并归档。维修单原因 `Seal wear observed; seal inspected and serviced` 在界面显示；实训复盘统计显示发现 1 项、验收归档 1 项、未关闭 0 项。Player 通过正常窗口关闭退出。

本次实际维修页在提交维修后提供“验收通过”，点击后直接归档；因此本次只证明上述维修单路径，不记作缺少复验时的拦截验证。更早的候选 `mouse-final` 中有独立缺少复验拦截及补录复验后验收证据，不能转移为本候选成绩。该流程也不代表 P-SEAL 以外的案例、独立考核或全部 T00–T13 已验收。

截图：

- `inspection-arrival-check.png`：小车到场与原因录入页。
- `repair-submit-check.png`：原因提交后的待验收工单。
- `recheck-gate.png`：点击验收后的归档结果。本候选未在此路径拦截缺少复验。
- `training-review-check.png`：本次会话实训复盘统计。

输入过程中曾因 Windows 桌面模式与应用控件布局不匹配而生成黑/裁切截图；这些截图不用于验收。完成后显示模式已恢复，进程已退出。
