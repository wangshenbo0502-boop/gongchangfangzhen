# 轴流风机接入统一平台验证

日期：2026-11-12，Asia/Shanghai。范围：用户批准将已验收风机按离心泵方式替换到主项目，并更新本地统一 EXE。

## 交付与实现

- 主程序：`Build/WorkshopPlatform/WorkshopPlatform.exe`。
- 操作入口：模型展示中选择 `F-01`，使用“整机拆装”查看平台内模型；“详细展示”启动 `Build/WorkshopPlatform/Viewers/IndustrialFanViewer/IndustrialFanViewer.exe`。
- 导入资源：`Assets/WorkshopDemo/Art/ReviewedFan/`；运行时 Prefab、拆装清单和反射图：`Assets/WorkshopDemo/Resources/ReviewedEquipment/`。
- 采用批准版的 11 总成、590 网格、98,140 三角面，保留叶片形变、铭牌、法兰、护网及材质贴图。FBX 和 `parts.json` 与独立工程源文件的 SHA256 分别一致。
- 通过 `WorkshopReviewedFanImport.Import()` 可重建资源；运行时由 `WorkshopEquipmentModel.ReviewedFan.cs` 加载。未重建或覆盖用户场景。
- 保留 FBX 根节点原有 X 轴旋转，再叠加入风口朝向；运动向量由同次导入的四个坐标参照计算，沿用 `motionVersion=4`、三次平滑插值和 14 秒拆/装路径。回装沿原路径反向执行。
- 主平台使用聚类渲染，在复制的风机 Shader 中补充 `_CLUSTER_LIGHT_LOOP` 和 `_REFLECTION_PROBE_ATLAS` 变体。保留原始贴图分辨率、法线、粗糙度；为风机设置局部反射图及 Renderer 锚点。独立展示器未修改。
- 接入原有 `WorkshopSimulation` / `WorkshopDevice` 的运行、选择、故障、告警和工单流程。F-CRACK 使用原叶片预制形变及裂口，保留后缘连接；不冒充断裂求解或真实裂长、厚度测量。

## 本次构建

Unity `6000.3.8f1`，Windows x64，最终报告 [unified-build-20261112-132546.txt](unified-build-20261112-132546.txt)：`Succeeded`、`Errors: 0`，1,442,121,134 bytes。保留原监控平台与小车训练两个构建场景。

完整日志为 `fan-platform-build-delivery-20261112.log`。必须区分：Player 构建成功，但批处理 Editor 随后因 `Library/BuildPlayerData/Editor/TypeDb-All.json` 生成器处理中文路径异常，以退出码 1 结束，末尾显示 `Scripts have compiler errors.`。不能把此次批处理描述为零退出码；交付 Player 已通过下述实际运行检查。

## 本次实际运行

通过交付目录内 Windows EXE 运行专用可选参数 `-workshop-fan-verify <输出目录>`。此模式在后台继续运行；正常启动不安装验收对象。界面通过可见控件的 UGUI 顶层射线和点击事件操作，未模拟操作系统物理鼠标。

| 分辨率 | 完成时间（+08:00） | 结果与原始报告 |
| --- | --- | --- |
| 1920×1080 | 13:27:27 | 45 项 PASS，[verification.txt](fan-platform-final-delivery-1080-20261112/verification.txt) |
| 1280×720 | 13:28:58 | 45 项 PASS，[verification.txt](fan-platform-final-delivery-720-20261112/verification.txt) |

两次均通过：

- 4 台设备存在；新风机 11 总成及网格/三角面统计正确，水泵保持 12 总成；Shader 支持、反射图和原分辨率贴图有效。
- F-01 原站位、落地、尺度、与其他设备包围盒分离、入口朝向巡检通道及场景物理射线选择。
- 原叶轮沿实际风筒轴旋转，停机停止；原叶片先弯曲后张开、清除故障恢复几何。
- 设备台账选择 F-01 并显示选中周界；进入平台整机拆装、完整动画拆解、中途反向及重新归位。
- 主场景和独立观察空间分别进行 1201 个正向及 1201 个反向位置检查，与批准 JSON 路径一致。最大世界误差分别为 `9.653336E-07 m` 和 `3.14568E-05 m`；后者位于约 260 m 的观察空间，阈值按坐标浮点精度计算为 `6.198883E-05 m`。
- “详细展示”启动正确部署路径的风机 EXE 与受保护的水泵 EXE；Windows `WM_NULL` 限时消息确认窗口响应，记录见各目录的 `viewer-processes.txt`。Mono 的 `Process.Responding` 在前次检查对正常子窗口返回 false，失败记录保留在 `fan-platform-release-1080-20261112/`，没有作为通过证据。
- F-CRACK 联锁停机、告警标记、报修、派工、NavMesh 实际到场、提交维修、验收、恢复运行及记录归档。测试临时提高机器人速度和加速度以缩短等待，结束后恢复；未直接设置到场状态。
- 原小车训练场景加载及返回平台后新风机再次加载。此项不替代小车全部 15 道训练工序回归。

每个最终目录均有 `fan-in-workshop.png`、`fan-platform-exploded.png`、`fan-platform-assembled.png`。已检查两分辨率全部六张原生截图，模型、界面和主要按钮可见，无新增遮挡。拆解截图的部件层级文字可能因 UI 刷新间隔显示 10.9/11，实际端点由运行时位置断言确认。

## 拆装几何检查

`model/reports/industrial-fan-platform-motion-audit.json` 使用平台实际导出的世界顶点和三角面，抽样全部总成 1201 个位置，反向复用相同路径。其输入来自 `fan-platform-verified-1080-20261112/fan-platform-motion-geometry.json`；后续修改仅为 Shader、反射和验收逻辑，模型及运动清单不变。

与批准版 `industrial-fan-import-basis-motion-audit.json` 对比，相交网格对名称完全一致：6 对静态底座/风筒连接在全部样本存在，以及盒体/盖板密封垫最初 5 个样本的座合接触。未发现新增运动表面相交对。这是离散 BVH 表面检查，不是连续扫掠或制造级干涉认证。

## 文件保护与限制

最终哈希记录：[fan-platform-viewer-hashes-20261112.json](fan-platform-viewer-hashes-20261112.json)，13:29:02：

- 水泵基线与当前均为 170 个文件，路径、长度、SHA256 差异为 0。构建器对现有水泵目录只检查完整性，不写入。
- 风机源与平台部署均为 167 个文件，差异为 0；`Assembly-CSharp.dll` SHA256 为 `8F4A19ACB86F3FAF9E8C7416B27AFF46F457F84352D80AE37933A0D7E30DC92A`。
- 原平台备份（不包含 Viewers）：`model/revisions/fan-platform-before-20261112-125002/`。风机部署前版本另存于 `model/revisions/IndustrialFanViewer-platform-before-*`。

运行日志仍含既有 NavMesh 初始化警告、阴影图集自动降低分辨率提示和本机 `127.0.0.1:8018` 知识服务连接失败。本次风机实际导航到场通过，未据此宣称所有导航对象或在线知识问答通过。

未重新测量主平台拆装帧率、显存峰值，未进行 OS 物理鼠标验收、全部故障组合回归或用户视觉终验。独立展示器历史帧率只适用于其原有测试，不能套用到主平台。
