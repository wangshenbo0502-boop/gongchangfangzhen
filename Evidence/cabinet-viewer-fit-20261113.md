# 控制柜展示器装配、吊耳与开门验证

2026-11-13，Asia/Shanghai。检查对象为 `model/CabinetViewer/CabinetViewer.exe` 的独立展示器。

- 左侧铰链改为同轴交错的固定节和转动节，销留在柜体上。铰链轴向左移 14 mm、向柜外移 15 mm。门内接地编织带让开左立柱。正面重做门缝止口和侧操作面板折边，闭合后从正面看不到柜内零件穿过接缝。
- 两只吊耳采用圆顶耳板、贯通孔、安装座和螺栓。按 Unity 导入顶点实测，耳板与顶板相距 4.50 mm，间距由安装座填满，安装座与耳板、顶板的配合误差均在 0.01 mm 内。两个吊装孔均通过射线贯通检查。
- 参考风机与水泵展示器的 ACES 调色和反射环境，针对柜体调整主光和补光。喷粉钢板采用深蓝灰底色和低对比粗糙度，保留五金的金属反光。减弱 HMI 面板泛白和观察窗反光。
- 变频器在 PLC 前拆出。跨端子和格兰头的线束通过淡出与淡入表现断开和接回，切换位置期间不显示。其他总成沿原路径反向回装。

Blender FBX 回读通过，16 个总成、3,247 个网格、119,066 个三角面。Unity 6000.3.8f1 Windows 构建日志为 `model/reports/electrical-cabinet-viewer-windows-20261113-090348.log`。

使用 Unity 导入的三角网格及实际拆装路径抽查 2,411 个姿态。闭合态门组件与柜框相交为 0，拆装中新出现或扩大的网格相交为 0。其他总成装配态的 294 对网格接触保留在报告中。线束完全隐藏的切换区间不纳入可见碰撞检查。原始报告见 [网格检查](../model/reports/electrical-cabinet-imported-motion.json)。

1920 × 1080 Windows Player 自动化运行实际按钮回调，检查完整拆装、开门中途反向、线束切换可见性、部件射线选择、描边、简介和重复归位。截图包括 [整机](../model/reports/cabinet-hinge-1080-20261113/01-assembled.png)、[正面](../model/reports/cabinet-hinge-1080-20261113/01a-front-fit.png)、[吊耳](../model/reports/cabinet-hinge-1080-20261113/01b-lifting-lugs.png)、[起转](../model/reports/cabinet-hinge-1080-20261113/03-door-start.png) 和 [转过立柱](../model/reports/cabinet-hinge-1080-20261113/03-door-clears-jamb.png)。结果见 [Player 自动化记录](../model/reports/cabinet-hinge-1080-20261113/automation-result.txt)。

用户复看后指出原地起转仍有穿模观感。最终交付改用 motionVersion 4，柜门、HMI 和散热组件在前约 0.91 秒保持平行，统一向柜外直线退出 160 mm，再以退出后的轴线旋转。回装先转正，再直线贴合。固定铰链销收进柜侧轴节，便于区分拆下后的两半铰链。旧版记录保留用于对照。

最终 Unity Windows 构建日志为 `model/reports/electrical-cabinet-viewer-windows-20261113-091111.log`。按导入网格计算，旋转开始时门总成与其他组件沿柜深方向的包围区间相距 5.75 mm。2,411 个姿态未发现新增或扩大的网格相交。1920 × 1080 自动化增加了直线退出期间角度不变、柜门/HMI/散热件同步沿柜深退出至少 150 mm、退出途中反向归位的检查。实机截图见 [旋转前已退出](../model/reports/cabinet-door-release-1080-20261113/03-door-cleared-before-rotation.png) 和 [开始旋转](../model/reports/cabinet-door-release-1080-20261113/03-door-turning-clear-of-jamb.png)，运行结果见 [最终自动化记录](../model/reports/cabinet-door-release-1080-20261113/automation-result.txt)。
