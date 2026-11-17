# 2026-11-14 故障模块改前基线

- 工程 HEAD `ca86cf08216592523a49227b64152e4213415af0`。工作区已有 README、知识资料、历史证据及其他用户修改。记录见 [Git 状态](git-before.txt) 与 [差异统计](diff-before.txt)。
- Unity 6000.3.8f1，默认活动场景 `Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`。精细设备通过 `WorkshopDevice.Start → WorkshopEquipmentModel.Attach` 接入，观察窗口通过 `WorkshopEquipmentView.CreateStudioModel` 创建独立实例。
- [导入后层级](hierarchy.json) 来自 Editor 临时实例，包含相对路径、局部姿态、网格与世界包围盒、顶点、三角面、材质槽和 Blend Shape。临时实例在退出前销毁，未保存场景。首次导出遇到 Unity Editor 的空组件占位对象，改用 Unity 对象存在性判断后重跑成功。
- 故障目录有 24 个历史定义、现役四设备适用 16 个。scikit-fem 参考件共有 21 份，14 份对应当前设备。Abaqus 入口只支持独立包络，当前没有设备真实 ODB 结果。
- [旧正式版验证输出](before/verification.txt) 复跑通过。首次隐藏窗口截图为黑帧。可用改前画面引用历史 [储罐整机](../tank-platform-20261113/1080/02-assembled.png)、[旧储罐教学末态](../tank-platform-20261113/1080/05-fault-teaching.png) 和 [密封参考件开裂](../workflow-chat-20261113/1080/08-fracture.png)。六案例各阶段改前新截图未补齐。
- [独立展示器文件清单](protected-files.json) 覆盖正式版 Viewers 下 671 个文件，其中 WaterPumpViewer 170 个。记录相对路径、字节长度和 SHA-256。

| 工位与资源 | 总成 | 重点案例与改前缺口 |
| --- | --- | --- |
| P-01 · ReviewedEquipment/water-pump | 12 | P-SEAL 隐藏精细密封组，使用断裂插片。P-CAV 复用通用振动效果。已定位动／静环及叶轮入口候选。 |
| TK-01 · ReviewedEquipment/silo | 12 | TK-LEAK 切到旧罐的通用开缝。新罐筒壁网格 `TNK_01_Shell_8mmWall` 有 2602 顶点、1468 三角面及 1 个材质槽。 |
| F-01 · ReviewedEquipment/industrial-fan | 11 | F-CRACK 使用第一片叶片的三组预制形状键。F-IMBAL 未区分轮毂相对偏摆与整机振动示意。 |
| PLC-01 · ReviewedEquipment/electrical-cabinet | 16 | PLC-CONTACT 旧焦点是 PE 接地排。X1-01 夹笼候选已定位，通电及负载路径需随案例核实。 |

漏点位于名义筒壁局部坐标 `(0.4354, -1.1354, -2.47) m`，外法向 `(0.2903, -0.9569, 0)`。此处局部轴沿原 FBX 方向，负 Z 对应 Unity 高度方向。取原 64 边筒壁在 16.875° 位置的外表面，中心半径 1.50 m、中心局部 Y 约 0.30 m。导入姿态把它转到朝巡检通道的筒壁下段。依据是原建模脚本的筒壁尺寸、导入层级与表面包围盒；近景范围按名义模型的 0.44 × 0.64 × 0.18 m 设置。
