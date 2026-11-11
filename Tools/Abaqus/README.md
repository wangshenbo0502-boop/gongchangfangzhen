# Abaqus 故障结果接入

本目录提供真实 ODB 的包络导出工具，不包含伪造的求解结果，也不自动创建有限元模型。
Unity 内置 24 类教学故障；已有 8 台设备各支持 4 种，合计 32 个设备工况。
它们无需 Abaqus 即可演示。没有匹配 ODB 结果时界面明确显示“教学故障模型 · 无 Abaqus 结果”。

## 当前接入范围

- 可选通道：Mises 应力 `S`、位移模长 `U`、温度 `NT11` 或 `TEMP`。
- 对指定装配实例逐帧提取最大值，保留物理时间（秒）；回放随故障发生后的游戏时间推进，到最后一帧后保持。
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
4. 打开已有 `WorkshopDemo.unity`，进入 Play，通过“演示故障”选择对应设备与故障，点击“注入故障”。
5. 再打开“故障工况”，滚动到结果来源，即可查看实际包络、作业名、区域及分析说明。

导入器无需重建场景；新结果要重新注入对应故障才会加载。Windows 构建会包含 StreamingAssets 文件。
文件中的 SHA-256 用于追溯原 ODB，运行时不能仅凭 JSON 验证模型正确性或证明原 ODB 仍可获取。
结果文件限 8 MiB / 20,000 帧，错误文件退回教学模式并显示校验失败。

## 故障编码

| 设备 | 编码 |
| --- | --- |
| CV-01 | CV-JAM / CV-TRACK / CV-BEAR / CV-SLIP |
| M-01 | M-OVER / M-BEAR / M-PHASE / M-ALIGN |
| P-01、P-02 | P-HOT / P-CAV / P-SEAL / P-BEAR |
| TK-01、TK-02 | TK-PRESS / TK-LEAK / TK-LEVEL / TK-SUPPORT |
| F-01 | F-IMBAL / F-BLOCK / F-BEAR / F-CRACK |
| PLC-01 | PLC-HOT / PLC-LOW / PLC-COMM / PLC-CONTACT |

## 验证边界

协议与导出聚合逻辑可用合成测试验证，但合成数据不会写入正式结果目录。
本机未发现可用 Abaqus 启动命令或常见安装路径，尚未运行真实求解、真实 ODB 导出或标定云图。
当前完成情况与 Unity 验证以 `Evidence/fault-integration-20261109.md` 为准。
