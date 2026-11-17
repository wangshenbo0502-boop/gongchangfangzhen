# Abaqus 故障结果接入

第一次看故障云图，可从 [Abaqus 与故障分析入门](../../Services/WorkshopAssistant/knowledge/Abaqus故障分析入门.md) 的水泵超温和风机裂纹案例开始。

本目录提供 ODB 包络及局部温度全场的导出接口。当前四台设备各有 8 个教学工况，共 32 个；另保留 8 个历史设备编码。深入分析按可用来源分别进入 scikit-fem 参考件、Abaqus v1 包络或 v2 局部温度全场，不把教学指标标成求解结果。2026-11-15 本次验证仍使用合成协议数据，没有真实 ODB 求解验收。

## 当前接入范围

- 可选通道：Mises 应力 `S`、位移模长 `U`、温度 `NT11` 或 `TEMP`。
- 对指定装配实例逐帧提取最大值，保留物理时间（秒）。运行时结果对象支持按时间插值，超出末帧时返回末帧。
- 显式转换为 MPa、mm、摄氏度。Abaqus 使用一致单位制，脚本不能自动推断模型采用的单位。
- 每个最大值来自其独立空间包络，可能不在同一节点/单元；当前不导出节点、网格、应力场或模型坐标。
- 三维热流线、振动环、漏液线、变形轮廓、电气闪烁和卡阻标记均为示意，过程指标仍由教学模型生成。
- 单纯通信中断、PLC 联锁、液位失衡等属于过程/控制工况；不能把它们称为 Abaqus 直接求解的结构故障。

## 真实分析准备

应先为对应设备建立可追溯模型：实际尺寸/CAD、材料及温度依赖、接触和约束、载荷/热边界、网格、分析步。
对轴承/叶轮可研究动力响应，对柜体/接点可研究热响应，对罐体/支座可研究承载与变形。
汽蚀本身不能仅靠一个结构应力结果证明；疲劳寿命或裂纹扩展也不能用一次位移包络替代。
必须由建模人员确认具体故障能否由该模型代表，检查收敛、网格敏感性与结果合理性。
现有展示 FBX 不等于已经具备工程尺寸、材料和可求解网格。

## 导出

建议 Abaqus 2024+ 的 Python 3 环境。先在已授权的 Abaqus 中求解并生成 `.odb`。
下面是命令形式示例；`pump_bearing.odb` 必须是用户自己的真实结果。

```powershell
abaqus python Tools/Abaqus/export_fault_odb.py `
  --odb D:/Analysis/pump_bearing.odb `
  --step Transient --instance PUMP-1 `
  --device P-01 --fault P-BEAR --fields S,U `
  --stress-to-mpa 1 --displacement-to-mm 1 --temperature-offset-c 0 `
  --confirm-physical-time `
  --analysis-description "实际泵轴承座；材料、约束、载荷和网格见配套分析报告" `
  --output D:/Analysis/P-01-P-BEAR.json
```

示例转换只适用于应力以 MPa、长度以 mm、温度以摄氏度定义的模型。
若原始模型为 Pa/m/K，转换分别为 `0.000001`、`1000`、`-273.15`。
只导出温度可设置 `--fields NT11`；单独导出应力可设置 `--fields S`。
必须明确勾定（命令参数确认）步时间是真实秒数；静力步的伪时间、模态频率或屈曲特征值不能冒充时间回放。
脚本拒绝非 TIME 域；静力分析同样可能是 TIME 域，因此仍需建模人员核对物理时间含义。
缺少所请求通道的帧会跳过并报告数量，区域为空、单位无效或非有限数值会拒绝。
输出文件已存在时拒绝覆盖。

## 导入与使用

1. Unity 菜单 `Tools/Workshop Demo/Import Abaqus Fault Result` 选择导出 JSON。
2. 导入器校验设备/故障类型、通道、单位、时间顺序、ODB SHA-256 与分析说明。
3. 文件保存到 `Assets/StreamingAssets/WorkshopFaultResults/<设备编号>/<故障编码>.json`；
   覆盖前的旧文件备份到 `Evidence/AbaqusImportBackups/`，原 `.meta` 保留。
4. 打开已有 `WorkshopDemo.unity`，进入 Play，通过“实训任务 → 新建课题”选择对应设备与故障，点击“注入故障”。
5. “深入分析”的来源菜单提供独立 ODB 包络页：通道选择、物理时间滑条、曲线和原始来源说明。没有参考件也能打开已导入包络；只有一个来源时直接打开。设备的教学过程指标保持原来源，不随包络导入改变。

导入器无需重建场景；新结果要重新注入对应故障才会加载。Windows 构建会包含 StreamingAssets 文件。
文件中的 SHA-256 用于追溯原 ODB，运行时不能仅凭 JSON 验证模型正确性或证明原 ODB 仍可获取。
结果文件限 8 MiB / 20,000 帧，错误文件退回教学模式并显示校验失败。

## 当前设备故障编码

| 设备 | 编码 |
| --- | --- |
| P-01 | P-HOT / P-CAV / P-SEAL / P-BEAR / P-ALIGN / P-INLET / P-IMPELLER / P-DRY |
| TK-01 | TK-PRESS / TK-LEAK / TK-LEVEL / TK-SUPPORT / TK-CORROSION / TK-FLANGE / TK-SENSOR / TK-VENT |
| F-01 | F-IMBAL / F-BLOCK / F-BEAR / F-CRACK / F-LOOSE / F-RUB / F-BLADE / F-REVERSE |
| PLC-01 | PLC-HOT / PLC-LOW / PLC-COMM / PLC-CONTACT / PLC-OVERLOAD / PLC-SENSOR / PLC-IO / PLC-DRIVE |

## v2 局部温度全场

`export_field_odb.py` 仅支持单实例、固定 `DC3D4` 网格、原始 `NODAL NT11`。保留节点/单元编号、外表面到原单元及面号的对应、全局米坐标、分析轴、输入及 ODB 哈希。它不提交求解，不支持应力外推、结构变形、XFEM、删除单元或变化拓扑。

在已授权的 Abaqus Python 中使用 `--help` 查看必填追溯参数；必须明确长度转换、温标转换及分析轴语义，已有输出目录拒绝覆盖。静态载荷参数用 `loadParameter`，不能标为物理秒数。

Unity 菜单 `Tools/Workshop Demo/Import Abaqus Local Thermal Field` 选择完整包的 `manifest.json`。导入器核验设备、工况、部件、每个文件哈希和完整性，将包放到 `Assets/StreamingAssets/WorkshopAbaqusFields/<设备>/<工况>/<caseId>/`；旧包备份到 `Evidence/AbaqusFieldImportBackups/`，保留存续资源的 `.meta`。

深入分析的全场入口显示独立局部网格、离散帧、每帧色标和原始节点探针。视图未配准到整机，不做跨帧插值；表面颜色为节点温度的线性着色，探针标明原始节点值、无平均。单个清单限 1 MiB，单文件 32 MiB，整包 256 MiB；帧缓存上限为 2。

四个待分析档案在 `AnalysisCases/`。目前缺少真实材料、载荷、可追溯输入、ODB 及三位置三帧对照，不能据此宣称 T10 真实结果闭环或 T11 求解校核完成。

## 验证边界

协议与导出聚合逻辑可用合成测试验证，但合成数据不会写入正式结果目录。
本机未发现可用 Abaqus 启动命令或常见安装路径，尚未运行真实求解、真实 ODB 导出或标定云图。
Unity 的合成协议用例见 [WorkshopFaultValidation.cs](../../Assets/WorkshopDemo/Editor/WorkshopFaultValidation.cs)，参考云图的 Windows 界面验证见 [2026-11-13 工单流程与分析验证](../../Evidence/workflow-chat-20261113.md)。
