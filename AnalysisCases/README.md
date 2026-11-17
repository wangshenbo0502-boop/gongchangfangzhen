# 局部分析档案

2026-11-15：尚未取得四个局部算例的可追溯材料、载荷、边界和真实 ODB；未执行 Abaqus 求解。T11 保持未完成。

| 案例 | 部件绑定 | 当前状态 |
| --- | --- | --- |
| PLC-CONTACT-local-thermal | cabinet.fieldTerminal.candidate | [待补输入](PLC-CONTACT-local-thermal/missing-inputs.md) |
| TK-CORROSION-local-static | tank.shell.frontLeak | [待补输入](TK-CORROSION-local-static/missing-inputs.md) |
| P-SEAL-local-contact | pump.seal.chamber / pump.seal.faces | [待补输入](P-SEAL-local-contact/missing-inputs.md) |
| F-CRACK-local-static | fan.impeller.crack | [待补输入](F-CRACK-local-static/missing-inputs.md) |

当前全场协议仅支持固定 DC3D4 热网格与原始 NODAL NT11。结构、接触、裂纹场需要另增协议与验证，不能改字段名接入温度读取器。

资料到位后的档案应包含：输入依据及版本、可重建脚本或 INP、参数表、单位制、求解器和授权信息、作业日志、目标步完成状态、收敛警告、ODB 存储位置及 SHA256、导出配置、三档网格对照、至少三点乘三帧的源结果与 Unity 数值对照、同条件完好/缺陷对照及局限说明。大 ODB 不直接塞入 Unity Resources。
