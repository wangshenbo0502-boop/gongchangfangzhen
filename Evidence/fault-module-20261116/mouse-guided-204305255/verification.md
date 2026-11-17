# 候选 204305255：引导实训真实鼠标闭环

日期：2026-11-16，本机日志时区 +08:00。场景 `Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`，程序 `Build/WorkshopFaultReview-20261116-204305255/WorkshopPlatform.exe`，1280×720 窗口，普通启动，无自动验证参数。未更改桌面分辨率。

20:56–20:59，PID 34112，通过 `Tools/FaultAssets/check_fault_mouse.ps1` 的 Windows 鼠标/键盘输入操作实际 UGUI，没有调用业务方法或按钮回调：

1. 新建 P-01 / P-SEAL，明确选择“引导实训”；注入并派发，小车实际到达后工单进入待维修。见 `07-fault-options.png`、`09-dispatched.png`、`10-arrived.png`。
2. 输入原因 `Seal wear checked and seal serviced`；勾选部位观察、指标对照，诊断选择“机械密封泄漏”，维修选择“隔离后维护密封副”，保存检查。见 `14-evidence-selected.png`、`15-record-saved.png`。
3. 提交维修后进入待验收；未记录复验直接验收，界面提示“请在诊断记录中完成维修后复验，再执行验收”，工单保持待验收，已关闭为 0、无效操作为 1。见 `16-repair-submitted.png`、`17-retest-gate.png`。
4. 在诊断记录勾选复验并保存，再执行验收成功。复盘显示已发现 1、未关闭 0、验收归档 1、证据齐全、维修已检查、复验已记录；保留这次故意触发的无效操作 1。见 `19-retest-recorded.png`、`20-archived.png`、`21-review.png`。
5. 20:59:11 通过 CloseMainWindow 关闭自启 Player，WaitForExit 确认进程结束；此附着进程句柄未返回退出码，故不宣称 exit 0。

结论：该候选单个 P-SEAL 引导实训的真实输入闭环及缺复验门禁本次通过。不代表 32 案例逐例鼠标验收，不涵盖全部考核、快捷键、动态效果或声音。

同目录的首个 PID 34792 尝试因 DPI 坐标偏差没有完成流程，保留日志/截图，不计通过。脚本已增加当前线程 DPI 感知，使坐标对应实际客户端像素；没有修改显示模式。此前 `mouse-guided-retest-20261116-1859/` 的维修项不匹配失败也保留，不能用文件名误计为门禁通过。
