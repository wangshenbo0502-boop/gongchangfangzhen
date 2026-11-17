# 当前候选 Windows 鼠标闭环

时间：2026-11-16 01:40–01:47，Asia/Shanghai。场景 WorkshopDemo，窗口客户区 1280×720。

程序：`Build/WorkshopFaultReview-20261116-013003835/WorkshopPlatform.exe`，PID 26484。普通启动，仅设置窗口尺寸及日志，无故障验收参数。01:47:07 经 CloseMainWindow 关闭并确认进程退出；跨 PowerShell 进程读取的退出码为空，不填造退出码。

方法：`Tools/FaultAssets/check_fault_mouse.ps1` 的 Windows SendInput 鼠标点击、键盘输入和桌面截图。未调用业务方法或 UGUI 事件，未改实际到场状态。操作时间与坐标见 [actions.txt](actions.txt)，日志见 [player.log](player.log)。

| 操作 | 实际结果与证据 |
| --- | --- |
| 新建 P-SEAL 引导实训、直接注入 | 生成 ALM-001，转入对应工单；[配置](04-configured.png)、[工单](05-order.png) |
| 派发并等待实际导航 | 状态从巡检途中转为待维修，显示小车已到场；[途中](06-dispatched.png)、[到场](07-site.png) |
| 采集部位/指标证据，选择机械密封诊断与适用维修 | 表单保留两项证据、诊断及维修选择；[表单](11-evidence-filled.png) |
| 保存检查、填写原因并提交维修 | 系统粘贴保存 `Seal wear checked and seal serviced`；[原因](21-os-paste.png)、[待验收](22-awaiting.png) |
| 未复验直接验收 | 显示“请在诊断记录中完成维修后复验，再执行验收”，仍待验收，无效操作 1；[拦截](23-missing-retest-block.png) |
| 勾选复验、保存、再次验收 | 验收成功、故障清除；[复验](24-retested.png)、[验收](25-accepted.png) |
| 打开实训复盘 | 已发现 1、未关闭 0、归档 1；证据齐全、诊断/维修/复验已记录，无效操作 1；[复盘](26-archive.png) |

结论：当前候选 P-SEAL 引导实训一次完整真实鼠标闭环及缺复验负向操作通过。不等于 32 案例逐一真实鼠标、完整相机操作或独立考核鼠标验收。

## 失败尝试及限制

- 初次 Hidden 启动后 MainWindowHandle 为 0，窗口边界读取失败。按本 PID 枚举并恢复窗口后继续，最终截图来自可见窗口。
- 12–20 号截图中的 SendKeys、Unicode/虚拟按键/扫描码注入没有填入原因，不能按文件名中的 filled/repaired/block 推断成功。14/16 号实际是空原因提交拦截，不是缺复验拦截。
- 最终使用操作系统剪贴板和 Ctrl+V 成功输入，再逐图确认提交和验收；没有直接写 InputField。辅助脚本恢复原剪贴板文本。期间尝试向当前窗口请求英文布局，未证明它解决直接输入问题。
- 日志含启动时 `Failed to create agent because there is no valid NavMesh` 以及阴影图集降分辨率提示。本次随后实际到场及闭环通过，但不据此声称日志零警告或所有 NavMesh 创建时序均无问题。
- 未采集声音、连续运动录像、长时间稳定性或关闭程序后持久化记录。本次复盘本身注明会话记录存于内存。
