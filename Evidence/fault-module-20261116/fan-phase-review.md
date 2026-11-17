# 风机不平衡同相回放与当前交付

日期：2026-11-16，日志采用本机 +08:00。场景 `Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`；Unity 6000.3.8f1、URP、新 Input System。

## 修改与来源

修复 F-IMBAL 的显示转子、轴心轨迹和教学箭头原先使用不同相位时钟的问题。学习预览对转速变化积分，任意定位和倒拖不累积相位；车间在现有唯一旋转控制器执行后读取实际显示的转子方向，再组合局部轨迹。设备工位、巡检点和碰撞代理不参与振动。

预览读取 F-01 的名义转速 980 r/min，不再使用通用 1450 r/min。转子采用明确的 **1/48 教学时间倍率**；1×/5× 只改变显示位移，振动读数、相位和业务状态保持独立。该轨迹是教学示意，不是转子动力学或采样频谱。完好进度恢复轴心；人工停机时无轨迹，转子不再旋转。

相关入口：`WorkshopFaultProfile.ImbalancePreviewAngle()`、`WorkshopEquipmentView.SetProgress()`、`WorkshopLocalFaultPresenter.SyncRotorPhase()`、`WorkshopEquipmentModel.LateUpdate()`、`WorkshopDevice.Update()`。`WorkshopFaultCase` 的来源读数及风机知识资料同步更新。

## 候选与本次结果

候选：`Build/WorkshopFaultReview-20261116-220916628/WorkshopPlatform.exe`。经 `R:\` 工程别名调用 `WorkshopDemo.Editor.WorkshopPlatformBuild.BuildFaultReview`；[构建日志](fan-phase-build-20261116.log)记录构建成功，Editor exit 0，无 C# 编译错误或 Missing Script 构建警告。未运行 Build Demo，未替换日常程序。

以下是本候选实际运行结果，均为可见 Windows Player、exit 0、零 FAIL：

| 检查 | 分辨率 | PASS | 含义 |
| --- | --- | --- | --- |
| [综合回归](fan-phase-220916628/regression-1080/verification.txt) | 1920×1080 | 756 | 32 适用案例、64 次原启停组合实际巡检、训练门禁、可逆预览与合成分析协议 |
| [风机同相专项](fan-phase-220916628/visual-720/verification.txt) | 1280×720 | 45 | 相位/箭头、多进度、倒拖、1×/5×、暂停/复位、活动业务与停机门禁，以及控件和画面采集 |

已直接查看本候选 [末态聚焦](fan-phase-220916628/visual-720/F-IMBAL-focus-1.000.png)、[局部轨迹](fan-phase-220916628/visual-720/F-IMBAL-detail.png) 和 [完整窗口](fan-phase-220916628/visual-720/F-IMBAL-ui.png)：原风机、叶轮及局部轨迹正常渲染，关键操作未被遮挡。静帧不单独证明连续运动或声音听感。

两次 Player 日志均保留启动时的 `Failed to create agent because there is no valid NavMesh` 和阴影图集降分辨率警告。综合回归随后实际到场与闭环检查通过；不将启动日志描述为无警告，也不据此宣称已消除 NavMesh 初始化警告。

## 文件保护与知识

[交付核对及程序指纹](fan-phase-220916628/delivery.json)：日常和候选各 **671/671** 个展示器文件长度/哈希一致；四份总成/运动配置与 HEAD 语义一致，相关资源 `.meta` 缺失为 0。保留泵/罐/风机/柜的 12/12/11/16 总成和受保护 WaterPumpViewer。验证仅设置 Player 窗口尺寸，未改变桌面分辨率或显示模式。

重启本项目助手后，[知识源与索引一致性](fan-phase-220916628/knowledge-index.json)通过：11 文档、85 片段；四份设备速查文档的分块正文/摘要均与当前源文件一致，包含最新滤网/滚道及同相慢放说明。此检查不请求模型，不计为新的真实在线问答或 Unity GUI 问答验收。

前一候选的泵/入口专项、六案例性能及真实鼠标结果仍分别归属原候选，见 [轴承和入口](bearing-and-inlet-review.md)、[特写和后台渲染](local-closeup-and-background-rendering.md)。本候选没有重复执行全部历史套件。

## 普通启动输入补充

本候选另完成六重点案例的普通启动、真实鼠标播放抽样与复位操作，保留各自 18/18 秒末态；F-IMBAL 还完成 4.6 秒处暂停与继续。直接查看了原尺寸局部画面及播放、末态、复位拼图。它不使用自动验证入口，也不等同于每帧连续运动或声音验收。

第一轮储罐空视口后退出、第二轮误选设备、首次无效暂停和开始菜单遮挡等记录均保留并拒收相应证据；干净启动后的储罐画面正常，原退出原因尚未确定。PLC-CONTACT 定向复核确认复位进度为 0/18 秒，关闭独立热辅助层后局部红色热叠加消失。旧复位图实际仍为 18/18 秒，已拒收，不能据此认定复位故障。详见 [复位对照记录](fan-phase-220916628/contact-reset-20261117/verification.md) 和 [普通启动原始记录与边界](fan-phase-220916628/motion-input/verification.md)。Player 均已退出，没有留下验证程序。

## 边界

32 案例的定义、绑定、读数和业务闭环不等同于全部 32 套精细几何验收。真实 ODB 的三点三帧对照、四份工程算例输入、实际 Abaqus 求解及裂纹扩展专题仍缺，见 `AnalysisCases/*/missing-inputs.md`。合成协议、预设孔口、滚道/叶片损失及同相轨迹均不替代真实求解，T00–T13 不能整体验收。
