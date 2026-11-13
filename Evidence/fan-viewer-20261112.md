# 轴流风机独立展示器拆装与标签修复验证

验证日期：2026-11-12。日志与运行报告时间使用其原始 `+08:00` 时区。
范围为隔离工程 `model/FanViewerProject/` 和交付目录 `model/IndustrialFanViewer/`。
后续平台接入见 [平台验证](fan-platform-20261112.md)，本轮流畅度及平台配套副本更新见 [动画流畅度验证](fan-frame-pacing-20261112.md)。下文拆装几何检查继续有效，原有 59–60 FPS 为此前版本数据。

## 当前版本：实际导入坐标与拆装路径修复

用户连续截图指出 09 号检修盖与 11 号接线盒盖拆装时仍穿入风筒、加强环、后法兰及盒体。此前 Blender 源网格检查与 Unity 的轴向/高度间隙断言均未覆盖横向方向，不能据其通过宣称运行画面无穿模。

本次通过与设备一起导出的四个不可见坐标参照实测：本工程 FBX 导入后的 Blender X/Y/Z 分别为 Unity `(1,0,0)` / `(0,0,1)` / `(0,1,0)`。旧运行时代码使用 `(x,z,-y)`，把外移变成内移，这是反复调整源路径后仍穿模的原因。运行时现在从导入参照缓存三个基向量，以其计算所有总成的位移；不再硬写轴符号，也不在每帧查找节点。导出脚本不再把共享清单的 `motionVersion=4` 错误覆盖成 2。

09 号盖板先脱离筒面紧固件，轴向避开 08 号接线盒，再抬高、沿轴向移开；11 号盒盖先沿盒体外法线退出，再轴向错开，08 号盒体随后才提升。电机与导流支承先共同轴向退出风筒，再分离；回装沿相同路径逆序。前一轮几何修订保留真实盖板螺孔和短紧固件，移除护网背侧多余螺母、修正后罩及导流支承净空。源文件和生成器已同步，当前为 11 总成、590 网格、98,140 三角面。

- 成功构建：`model/reports/industrial-fan-windows-20261112-114809.log`；11 总成、11 锚点、590 网格、9 运动组，`Build Finished, Result: Success.`。
- 交付：`model/IndustrialFanViewer/IndustrialFanViewer.exe`。业务程序集 SHA256 为 `8F4A19ACB86F3FAF9E8C7416B27AFF46F457F84352D80AE37933A0D7E30DC92A`。
- 源模型重开检查：`industrial-fan-reopen-verification.json`，11:36:26（+08:00），G1–G12、UV、动画路径/反向/归位、16 张打包图像通过；标准 FBX 回读 `industrial-fan-fbx-roundtrip.json` 于 11:37:33 通过。
- Unity 导入后的实际世界空间网格及坐标基：`model/reports/industrial-fan-unity-motion-geometry.json`，由场景生成器从实际 Mesh 顶点和 Transform 导出，包含全部 590 个网格。
- 新转换下沿全部 11 个总成路径抽样 1201 个位置：`industrial-fan-import-basis-motion-audit.json`。BVH 表面相交仅检出原有 6 对静态鞍座/加强环连接，以及盒盖密封垫与盒体在最初 5 个样本的座合接触；没有新增运动表面相交。回装使用同一路径，检查覆盖相同位置集合；不是连续扫掠体积或制造公差认证。
- 旧转换对照：`industrial-fan-legacy-axis-reproduction.json`，同一实际导入网格、401 位置，确实复现盖板/把手进入筒壁、盒盖进入后法兰及盒体。该报告是失败复现证据，不属于通过结果。
- 运行验证新增根据实际几何位置推导外法线的检查：拆解约 14% 时，09 号盖板向外投影必须大于 65 mm，11 号盒盖必须大于 220 mm；旧符号会失败。另检查盖板与盒体轴向间隔、抬高后与筒顶净距、盒盖与盒体轴向间隔。
- 当前保持 2 倍渲染、4× MSAA、贴图和轻度锐化；相机包围盒、总成位移、标签与选择遮罩使用缓存，拆装期间停用拾取碰撞体，避免每帧遍历全部网格及分配。
- 修订前源模型、脚本及完整 EXE 备份位于 `model/revisions/fan-before-motion-smooth-20261112-111344/` 和 `fan-before-cover-lift-20261112-112829/`。

- 最终实际 EXE：1920×1080 于 11:50:19、1280×720 于 11:52:56（+08:00）通过按钮事件、中途反向、完整拆解/组装、射线选择、描边、简介与新增净空断言；运行日志异常数均为 0。最终归位最大误差分别约 2.70 / 4.13 微米（浮点 Transform 误差）。
- 09/11 向外退出投影分别为 89.44 / 250.00 mm；盖板避开盒体后轴向间隔 212 mm，抬高后距筒顶 377.47 mm；盒盖与盒体轴向间隔 173 mm。两种分辨率结果一致，高清渲染及锐化均生效。
- 不含截图、遥测与文件写入的连续运动实测：1080p 拆/装平均 59.22 / 59.84 FPS，P99 20.74 / 17.34 ms；720p 为 59.52 / 59.85 FPS，P99 19.37 / 17.38 ms。四段均无 Gen0 GC 或测量到的主线程新增分配。1080p 拆解有一次 87.82 ms 峰值，四段分别有 4/1/2/1 帧超过 33.33 ms，不能宣称完全没有慢帧。GPU 为 RTX 4060 Laptop；结果仅代表本机短时运行。
- 最新报告/13 个阶段截图：`industrial-fan-axis-fixed-final-1080-20261112/` 和 `industrial-fan-axis-fixed-final-720-20261112/`。已目视复核实际 EXE 的盖板初退、错开、完全抬离、全拆及回装中间帧；1080p 的 `09-cover-clearance-detail.png` 为原生截图裁切近景，无生成或补画。

尚未完成全部视角及 OS 物理鼠标操作的逐项验收；最终观感由用户确认。没有修改水泵展示器或统一平台。

下节开始保留较早版本的历史记录，网格统计、轴转换说明及运行结果不代表当前版本。

## 清晰度修订（历史版本）

用户要求在已确认的整体效果上稍微提高画面清晰度。保持当前几何、材质色值、灯光和相机构图，风机隔离工程的渲染比例由 1.5 提高到 2，保留 4× MSAA、全分辨率无压缩贴图及 16× 各向异性过滤；纹理 mip bias 从 -0.15 调整为 -0.25。禁用相机动态分辨率，显式关闭额外相机抗锯齿后处理。

新增 `FanClarity.cs` 和 `FanClarity.shader`，沿用水泵现有轻度细节增强算法，系数 0.22，输出限制在邻域最小/最大颜色之间。直接在生成器绑定 Shader 资源，防止仅靠字符串查找导致构建剥离。处理仅用于主展示相机，选中遮罩相机和屏幕 UI 不参与锐化；没有修改水泵工程或交付目录。

- 成功构建日志：`model/reports/industrial-fan-windows-20261112-110107.log`；`Build Finished, Result: Success.`，导入校验 11 个总成、11 个锚点、618 个网格、9 个运动总成。
- 交付入口：`model/IndustrialFanViewer/IndustrialFanViewer.exe`。业务程序集修改时间 11:03:41（+08:00），SHA256：`C80BD9A8B99991B2ABF2F5A7928BDC05CDC261B53B3EB8DB5B9E8D4C5FECD66A`。
- 修订前完整 EXE 目录和两份修改前 C# 源码备份：`model/revisions/fan-before-clarity-20261112/`。
- 1920×1080 实际 EXE 检查于 11:05:23 通过，平均 57.90 FPS；1280×720 于 11:06:15 通过，平均 56.22 FPS，GPU 为 RTX 4060 Laptop。这是本机短时观察，未测显存峰值。
- 两次运行均确认 `renderScale=2`、`msaaSamples=4`、`clarityShaderSupported=true`，主相机实际锐化输入纹理分别为 3840×1748 和 2560×1164；高度对应模型视口，排除上/下方 UI。验证驱动会在锐化未执行或渲染尺寸不足时失败。
- 两种分辨率均通过按钮事件、中途反向归位、全拆、碰撞体射线选中、描边及简介检查，反向归位误差 0，日志异常数 0，最终 11 个标签无矩形重叠或越出视口。
- 截图/报告：`model/reports/industrial-fan-clarity-1080-20261112/`、`industrial-fan-clarity-720-20261112/`。已目视复核 1080p 整机和选中图；同一位置的前后对照为 `industrial-fan-clarity-1080-20261112/07-clarity-comparison-2x.png`，左右均采用 200% 最近邻放大，只用于观察像素变化，非原生 4K 截图。

本次没有重新认证全部几何干涉或 OS 物理鼠标操作。支座几何和源文件检查沿用下面 10:54 的记录；最终画面观感仍交用户验收。

## 接线盒支座贴合修订（10:54 完成）

用户指出接线盒下方支座与吊耳相交、和筒壁不贴合。已把方形撑块改为两组曲面鞍垫与开孔支板，并缩短随支座安装的径向紧固件。盒体、铭牌和既有拆装配置保留。修改同步到 `model/scripts/build_fan.py`，已有源模型由 `refine_fan_terminal_mount.py` 定向更新；四张整机预览和两张近景由 `render_fan_terminal_mount.py` 更新。

- 当前 11 个总成、618 个网格、99,770 个三角面；G1–G12、UV 和重新打开源模型/FBX 回读检查通过。
- 支座与吊耳底板轴向最小净距 20.000 mm；84 个鞍垫内侧顶点到实际分段筒壁的间隙为 0.420–0.793 mm。
- 装配状态新支座与筒壁/吊耳无三角面相交；抽样 101 个接线盒退出位置与吊耳无相交。检查仅覆盖此次支座，不能推广为全机扫掠干涉认证。
- 几何明细：`model/reports/industrial-fan-terminal-mount.json`。源模型/FBX 独立复核时间为 10:51（+08:00），两份报告分别为 `industrial-fan-reopen-verification.json` 和 `industrial-fan-fbx-roundtrip.json`。
- 构建日志：`model/reports/industrial-fan-windows-20261112-105108.log`，成功；实际 EXE 在 10:54 完成双分辨率自动化，1280×720 / 1920×1080 平均 FPS 为 55.62 / 56.86，归位误差 0，日志异常数 0。验证范围仍为按钮事件、运行时动画、射线选择、描边与简介。
- 截图及报告：`model/reports/industrial-fan-terminal-mount-720-20261112/`、`industrial-fan-terminal-mount-1080-20261112/`。Blender 近景：`model/previews/industrial-fan/terminal-mount-clearance.png`、`terminal-mount-fit.png`。
- 修订前源模型、生成器、报告及四张预览保存在 `model/revisions/fan-before-terminal-saddle-20261112-104653/`。

以下记录保留较早迭代的构建与验证数据，不代表最新渲染配置。

## 09 号检修盖路径复核

2026-11-12 09:34（`+08:00`）根据用户运行截图复核发现：检修盖最初虽先向筒面外抬离，但后续朝 +X 移动会靠近上方接线盒，造成视角下与 08/11 号总成重叠、看起来像穿入设备。现改为沿盖板所在筒面法线先外移约 1.04 m，再朝入口侧 −X 移出；入口护网在此前已退出。

- 修改配置：`model/scripts/fan_components.py` 的 `FAN_08_AccessCover` 路径。
- 实际交付：`model/IndustrialFanViewer/IndustrialFanViewer.exe`，通过 `Build-FanViewerReview.ps1 -BuildWindows` 重建。
- 中间阶段截图：`model/reports/industrial-fan-cover-path-final-1080-20261112/03-cover-cleared.png`、`04-cover-translated.png`；完全拆解截图为 `05-exploded.png`。已目视检查盖板离开筒体并与接线盒分开。
- 1280×720 与 1920×1080 自动化均通过，真实 UI 按钮事件、反向组装、完全拆解、射线选取、描边和简介检查通过；运行异常数为 0，归位误差为 0。

该验证通过实际渲染中间帧检查视觉路径，不等于完整的三角网格扫掠碰撞认证；OS 级鼠标实点、所有观察角度和制造级干涉验证仍未完成。此次没有触碰水泵展示器交付目录。

## 问题与修复

用户截图中，拆解按钮未带动部件，中间出现无文字黑条，并有标签跑到窗口边缘。

- 导出的拆装位移原为 JSON 数组，而 Unity `JsonUtility` 的 `Vector3` 字段需要 `x/y/z` 对象。导出侧已修正编码；当时运行时使用 `(x,z,-y)` 转换，后来确认其横向符号错误，已由顶部当前版本的实际导入坐标参照替代。
- FBX 选择未包含叶轮中间的转轴父节点。现在导出完整祖先链，并检查总成网格归属、标签锚点及路径。最新构建记录为 `FAN_MANIFEST_VALIDATED assemblies=11 anchors=11 meshes=634 moving=9`。
- 标签底板的坐标、文字填充和锚点匹配已修正，标签及引导线不拦截鼠标；使用实际视口限制和矩形避让。选中描边相机也已调整。
- 旧风机交付目录备份位于 `model/revisions/fan-viewer-before-input-fix-20261112-014715/`。本次未修改或部署水泵交付目录。

## 构建

- Unity：6000.3.8f1，Windows x64，URP；生成场景为 `model/FanViewerProject/Assets/Generated/FanViewer.unity`。
- 最新成功日志：`model/reports/industrial-fan-windows-20261112-020422.log`，约于 02:04:45 完成，包含 `Build Finished, Result: Success.` 和退出码 0。
- 交付程序：`model/IndustrialFanViewer/IndustrialFanViewer.exe`，须保留同目录 Data、UnityPlayer 和运行库。
- 本次业务程序集 `IndustrialFanViewer_Data/Managed/Assembly-CSharp.dll` 的修改时间为 02:04:41，SHA256 为 `22E2410B8CDA494F7DDA77576D288642AE48C1C5291575A81BF3D095BE0BB1DA`。Unity 启动 EXE 的文件时间可能早于业务程序集，不能仅凭 EXE 时间判断版本。
- 当前渲染设置为 HDR、4× MSAA、renderScale 1.5、全分辨率贴图。

可复现构建：在项目根目录执行 `.\model\scripts\Build-FanViewerReview.ps1 -BuildWindows`。历史失败编译日志保留，不能替代上述最新成功构建结果。

## 运行验证

从实际交付目录启动 EXE，通过 `Run-FanViewerAutomation.ps1` 启用专用运行时验证驱动。调用真实 UI 按钮绑定的 `onClick`，在拆解约 4 秒后反向组装，再完整拆解，通过实际碰撞体射线选择总成，检查白色描边和部件简介。

| 项目 | 1280×720 | 1920×1080 |
| --- | --- | --- |
| 报告时间 `+08:00` | 02:06:56 | 02:09:22 |
| 实际窗口分辨率 | 1280×720 | 1920×1080 |
| 运行时验证结果 | passed | passed |
| 中途反向组装归位误差 | 0 m | 0 m |
| 完全拆解、射线选择、描边、简介 | 通过 | 通过 |
| 最终可见标签数 | 11 | 11 |
| 最终标签矩形重叠 / 越出视口 | 0 / 0 | 0 / 0 |
| 最长引导线，Canvas 单位 | 29 | 29 |
| 运行报告平均 FPS | 58.96 | 58.94 |
| 运行日志异常匹配数 | 0 | 0 |

GPU 为 NVIDIA GeForce RTX 4060 Laptop GPU，程序目标帧率为 60。上述帧率是这两次短时运行的观察结果，不是其他硬件的性能保证；未测量显存峰值。报告中的约 124 MB 是 Unity 已分配内存统计，不是显存。

报告和截图分别位于：

- `model/reports/industrial-fan-player-20261112/`
- `model/reports/industrial-fan-player-1920-20261112/`

每个目录包含 `interaction-verification.json`、`state.json`、`Player.log` 及四张实际运行截图：整机、反向组装归位、完全拆解、选中总成。已查看整机、完全拆解与选中图，原截图中挡在叶轮中间的无文字黑条不再出现。

复现时使用新的输出目录，保留已有证据：

```powershell
.\model\scripts\Run-FanViewerAutomation.ps1 -Width 1280 -Height 720 -OutputDirectory .\model\reports\fan-player-recheck-720
.\model\scripts\Run-FanViewerAutomation.ps1 -Width 1920 -Height 1080 -OutputDirectory .\model\reports\fan-player-recheck-1080
```

## 验证边界

Windows 自动化会话未能可靠取得前台焦点，真实鼠标点击、双击时序、拖动旋转和滚轮操作未完成操作系统级点验。`Test-FanViewerInput.ps1` 为未稳定成功的前台自动化尝试，不能作为通过证据。

射线选择通过的是运行时调用路径，并未逐一完成 11 总成的真实双击验收。最终固定视角下中心采样可射中 10 个总成，曲面检修盖未找到无遮挡的中心采样点；这不等于其他视角无法选取。标签统计仅覆盖本次固定视角快照，未证明任意视角都无重叠。

归位误差 0 来自中途反向组装时的检查；最终 `state.json` 停在完全拆解状态，其 `maxHomeError` 表示当前位置离装配原位的距离，不应当作归位失败。尚未进行拆装全程网格干涉认证、长时间反复操作、全部视角或用户视觉验收。风机未部署到统一平台。
