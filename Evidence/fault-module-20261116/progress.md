# 故障模块升级：2026-11-16 交付与验证

时间采用 Asia/Shanghai。Unity 6000.3.8f1、URP、新 Input System；业务场景为 `Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`。承接 [2026-11-15 记录](../fault-module-20261115/progress.md)，保留历史输出。工作区基线 HEAD 为 `ca86cf08216592523a49227b64152e4213415af0`，未回退既有修改或删除记录。

源码、32 案例的业务闭环、六个重点机制和分析协议已接入。本记录不表示 T00–T13 全部验收：真实 ODB、工程材料与工况缺失；部分案例的精细几何仍待补齐。

## 2026-11-17 正式目录归整

用户明确要求统一到 `Build/WorkshopPlatform/` 并删除中间迭代版。已将验证通过的候选 `194622083` 整体迁入 [正式程序](../../Build/WorkshopPlatform/WorkshopPlatform.exe)，876 个文件的相对路径、长度与 SHA256 在迁移前后全部一致。未重新构建、未重复业务回归，原候选的验证范围及限制保持有效。

其余故障迭代构建、旧全屏备份及替换下来的正式版移入 Windows 回收站；`Releases/`、源码、美术资源、场景及历史验证证据保留。详细文件清单、处理路径与完成状态见 [归整审计 JSON](build-consolidation-20261117.json)。下方旧候选 EXE 路径只代表历史身份，不再作为启动入口；“未替换正式目录”是当时的历史状态。

## 历史验证：2026-11-17 收尾候选 194622083（现已迁入正式目录）

本轮验证对象为原路径 `Build/WorkshopFaultReview-20261117-194622083/WorkshopPlatform.exe`，现从 [正式目录](../../Build/WorkshopPlatform/WorkshopPlatform.exe) 启动。以下保留该构建的验证结论；后面的诊断、失败候选和旧版成绩保留历史归属。按用户收缩后的范围完成本地修复与必要验证，不宣称指导书 T00–T13 整体验收。

Unity `CompilationPipeline.RequestScriptCompilation(CleanBuildCache)` 恢复了脚本映射，未手工删除 Library、改动 GUID 或重建场景。[恢复报告](asset-database-mapping-probe-20261117-194239395.json) 为 **46/46** 个脚本引用可解析、缺失组件 **0**。原始异常的触发原因仍未确定。诊断入口新增编译后续建及带时间戳报告；构建入口新增两场景缺失脚本拦截，并在检查后关闭预览场景。[最终构建日志](final-build-20261117-recovery.log) 显示构建成功，Editor exit 0。

本轮另修正 `WorkshopEquipmentModel.ReviewedFan.cs` 裂面条带第二个三角形的连接，避免交叠/楔形拓扑；`WorkshopCompactUIVerification.Upgrade.cs` 的储罐湿润区断言同步既有内缘 6.5–7 mm、外缘约 20–23 mm 的收敛尺寸，没有为适配旧断言放大效果。

| 当前候选验证 | 结果 | 证据 |
| --- | --- | --- |
| 1080p 综合业务回归 | **757 PASS**、exit 0；Player 无 Missing Script 警告 | [verification.txt](final-acceptance-20261117-194622083/regression-1080/verification.txt) |
| 可见窗口风机 1080p 专项 | **25 PASS**、exit 0；23/23 张非空 PNG | [verification.txt](final-visible-20261117-194622083/fan-1080/verification.txt) |
| 可见窗口汽蚀 720p 专项 | **27 PASS**、exit 0；23/23 张非空 PNG | [verification.txt](final-visible-20261117-194622083/cavitation-720/verification.txt) |
| 文件与配置保护 | 正式/候选展示器各 **671/671** 文件匹配保护基线；四份总成配置与 HEAD 语义一致；相关 `.meta` 缺失 0 | [delivery JSON](final-delivery-20261117-194622083.json) |

综合回归覆盖 32 案例、部件绑定、可逆预览/恢复、实际巡检导航与启停恢复、实训隐私/验收拦截和合成 Abaqus 协议。已直接查看风机局部与聚焦图、汽蚀局部图：裂缝和开口可见、后缘连接保留，叶轮及青色气泡可辨；风机高光纹理、开口较小仍影响自然断裂观感，不能据此认定 T04 精细视觉全部完成。非空检查只证明捕获有效，不替代全部机制人工验收。

保留的拒收/中间证据：`183735020` 为 Missing Script 失败版；恢复后的 `194240704` 在 112 PASS 后停于储罐旧尺寸断言，不作为最终通过版。最终候选 `final-acceptance-20261117-194622083/` 下首次隐藏窗口的两组专项虽自动断言通过，但截图全黑，拒作视觉证据；有效截图仅取上述 `final-visible-20261117-194622083/`。隐藏窗口综合回归的帧时不作性能依据，当前候选没有新增可靠的可见性能基准。旧版本性能、鼠标与全矩阵成绩不转记到本版。

正式目录 `Build/WorkshopPlatform/` 未替换，受保护的 WaterPumpViewer 等独立展示器保持一致；没有修改桌面分辨率/显示模式或重建业务场景，没有执行 Git 提交。候选 `Assembly-CSharp.dll` SHA256 为 `5F2C53CEE725EC2ADB46F267FDEADF5EB797F1FA156CEACB2D9EB5CFA9A450BC`。

剩余范围明确保留：真实 ODB 同实例三点三帧对照未执行；四个 Abaqus 局部算例缺工程几何、材料、载荷/边界、可用求解环境及真实结果，见 [AnalysisCases](../../AnalysisCases/README.md)。部分 32 案例仍为教学叠加，全部精细几何、连续动态与声音未逐项人工验收。本轮以可运行升级候选、已完成的核心业务和上述验证收尾。

## 历史记录：2026-11-17 候选 183735020 与诊断收口

最新源码候选 `Build/WorkshopFaultReview-20261117-183735020/WorkshopPlatform.exe` 构建成功，但 1080p 回归首项失败，Player 报 116 条 Missing Script；候选不可交付，日常入口未替换。该候选加入的 F-CRACK 独立裂面网格和 P-CAV 气泡放大尚未获得有效运行时验收。

对可运行候选 `204305255` 与失败候选 `155109318`、`183735020` 的主 Player 元数据复核显示：三者主 `Assembly-CSharp` typeinfo 均有相同的 26 个 ScriptID 和 TypeTreeHash 集合，故不能再把差异简单归为全局 MonoScript 类型表缺项。失败候选的 `level0`/`level1` 额外将 25/23 个脚本类型引用指向 `sharedassets0.assets` 的 FileID 2；对应 PathID 1108 起的对象为 ClassID 115（MonoScript），且失败场景 typeinfo 含全零 TypeTreeHash 的 ScriptID `31e96ad1e0cfd631c089c0e0d7593cb7`。这是比此前更具体的映射差异，但尚未将这些对象逐项对应到程序集类型，也未证明其与 Missing Script 的因果关系；不据此修改场景、`.meta` 或构建流程。

本轮只读定向核对结束；没有重跑综合回归、重建候选或修改源码/场景/缓存/桌面设置。最新候选仍不可交付。其余已验证成绩只归属各自历史候选，不转记到 `183735020`。T00–T13 仍未整体验收；真实 ODB 同实例三点三帧对照、四案例 Abaqus 求解、部分精细视觉/连续动态与声音验收仍未完成。

## 2026-11-17 AssetDatabase 映射复核

使用临时 `R:` ASCII 盘符再次运行只读 `WorkshopFaultAssetDatabaseDiagnostic.Run`，Unity 6000.3.8f1 exit 0。报告仍为 46 个场景脚本引用中 20 个 `MonoScript.GetClass()` 可解析、26 个不可解析；两场景共 47 个 Missing MonoBehaviour。当前 Editor 已加载 `Assembly-CSharp.dll`，其中可见 304 个 `WorkshopDemo.*` 类型。报告见 [mapping probe JSON](asset-database-mapping-probe.json)，日志见 [ASCII 路径探针](asset-database-mapping-probe-20261117-alias.log)。

对 26 个不可解析项逐个核对后，场景序列化 GUID 与对应脚本 `.meta` GUID 全部一致；6 个自研 `WorkshopDemo` 主组件类型仍可在已加载的 `Assembly-CSharp` 中找到，但 `MonoScript.GetClass()` 为空。由此排除了这批脚本的 GUID/`.meta` 不匹配，线索指向 AssetDatabase 脚本映射或导入状态；尚无证据确定具体根因。

更正前一版文件取样：此前递归查找命中了 `Viewers` 展示器中的同名 `globalgamemanagers.assets`，不能代表主 Player。按候选根目录的精确路径复算 SHA256：可运行候选 `204305255` 的 `WorkshopPlatform_Data/globalgamemanagers.assets` 为 `049193AE5D5771DB73E3A25DE99D3E2FD1E75211E3DB26EB6EDE633A82D7BD79`（1,077,432 字节）；失败候选 `183735020` 为 `BA659B69DC286383F5080A6E229B0CEBA02D5D8E79EFA3B9AACC0030C34C1407`（1,066,056 字节）；旧失败候选 `155109318` 与 `183735020` 长度及 SHA256 完全一致。三个候选的 `Assembly-CSharp.dll` SHA256 均不同；`ScriptingAssemblies.json` 三者均为 `6AE10FEAB1BD8B041A821144B9216DE934E6F529955639F4696A91678B1E796C`，`RuntimeInitializeOnLoads.json` 三者均为 `981B744EFB659549D5E2746AACBCE7941E153BCC587BDA27348FFC836822AB18`。此前“主 Player 全局管理器文件相同，不能解释运行差异”的结论撤回；新的二进制差异是诊断线索，不足以证明映射缺失的因果关系。还需从这三个主 Player 的准确文件复核 MonoScript/type 映射内容，避免再次把展示器目录文件混入比较。

因此，ASCII 路径可使本次只读探针正常退出，但并未恢复 AssetDatabase 脚本映射；路径编码不能单独解释当前缺失。11 月 16 日历史探针曾为 46/46 可解析，当前 20/46 的状态变化原因仍未确认。未改场景、`.meta`、Library 缓存、候选或日常程序；没有重跑候选回归。候选 `183735020` 仍因 Player Missing Script 不可交付。下一步应对照 Player 实际程序集/场景加载结果与历史通过候选，并在获得明确因果证据后定位最小修复，不以删除缓存、重建场景或修改脚本 GUID 掩盖问题。

## 2026-11-17 TK-LEAK 定向收尾

本节为旧候选 `Build/WorkshopFaultReview-20261117-133924994/WorkshopPlatform.exe` 的历史记录；后续最新候选与结论见本文件开头的 2026-11-17 更新。该候选包含湿润区域收敛和 TK-LEAK 近景相机图层调整；正式入口 `Build/WorkshopPlatform/` 未替换。湿润环内缘由约 12–14 mm 收至 6.5–7 mm，外缘由 34–40 mm 收至 20–23 mm，降低了对 12 mm 穿孔的遮盖。近景只在 TK-LEAK 时允许读取设备层 28，其他故障仍只显示专用层 27。

可见 Player 定向检查 196 PASS、exit 0，孔径投影检查通过；该候选的初次审图判断为上下文不足。此判断不作为最新候选结论：升级任务书要求局部近景有效并保留主视图设备上下文，没有要求法兰必须进入同一近景画面。采集器产出 170 张图，旧版 `build_review_sheet.py` 的 166 张数量基线已同步为 170；这些自动检查不等于人工验收。

两次候选构建均报告 Player Build Successful；Unity Editor 随后仍输出 `Scripts have compiler errors.` 并以退出码 1 结束，日志未定位到对应 `error CS`。该问题已在后续构建中通过临时 ASCII 盘符别名规避；失败日志仍作为历史证据保留，见 [构建日志](tkleak-build-20261117-final2.log) 及前一次 [隔离复核日志](tkleak-build-20261117-final.log)。

## 2026-11-17 最新候选 135205338

最新隔离候选为 `Build/WorkshopFaultReview-20261117-135205338/WorkshopPlatform.exe`，构建入口 `WorkshopDemo.Editor.WorkshopPlatformBuild.BuildFaultReview`。中文工程路径触发 TypeDb 构建异常；本次临时使用空闲 ASCII 盘符 `R:` 指向同一工程构建，Unity Editor 日志以 return code 0 正常结束。映射已解除。正式入口 `Build/WorkshopPlatform/`、场景、桌面分辨率/显示模式未更改。

构建后执行可见 Player `visuals` 矩阵，覆盖六重点案例、32 机制、四角度、指定阶段和里程碑相邻帧；该矩阵不是综合业务回归，性能采样另列：

| 范围 | 1080p | 720p | 结果 |
| --- | --- | --- | --- |
| Player 视觉/控件断言 | 196 PASS | 196 PASS | 均 exit 0；TK-LEAK 12 mm 孔投影 52.9 px |
| PNG 采集与非空校验 | 170/170 | 170/170 | 均通过；仅证明图像已捕获且非空 |
| TK-LEAK 储罐业务专项 | 116 PASS | — | 1280×720 可见 Player，exit 0；覆盖拆装、演示预览、维修验收、停机恢复及重置 |
| 六重点案例性能采样 | 12 个 8 秒样本 | 12 个 8 秒样本 | 均 exit 0；效果开/关均采，PASS 只表示采样完成 |
| 交付保护核对 | 日常版 671/671 | 候选版 671/671 | SHA256 一致；四份总成配置一致；相关 `.meta` 缺失 0 |

本次直接查看候选 TK-LEAK 局部图：12 mm 预设孔缘、孔周湿润环和液滴可辨；整体储罐上下文由同屏主视图提供。按任务书 T02 的近景、局部漏损和同一精细储罐要求，本次不再以“法兰必须进入局部近景”为额外门槛。截图与自动检查位于 [双分辨率视觉证据](tkleak-visual-20261117-alias/)，交付核对见 [delivery JSON](tkleak-delivery-20261117-alias.json)，构建日志见 [ASCII 路径构建日志](tkleak-build-20261117-alias.log)。人工查看 TK-LEAK 不代表 32 个机制都完成独立人工视觉验收。

本候选性能原始记录见 [1080p CSV](tkleak-performance-20261117-alias/performance-1080/performance.csv) 与 [720p CSV](tkleak-performance-20261117-alias/performance-720/performance.csv)：效果开启时，1080p 六案例平均帧时间范围约 3.333–7.693 ms、P95 约 3.426–8.598 ms；720p 约 3.333–7.437 ms、P95 约 3.433–8.148 ms。采样存在超过 16.67 ms 的慢帧，尤其 1080p P-SEAL 最慢单帧约 57.46 ms；短时均值/P95 不构成稳定 60 FPS 结论。储罐专项原始记录为 [verification.txt](tkleak-tank-20261117-alias/verification.txt)。

补充人工查看最新候选 `135205338` 的 1080p `P-CAV-detail.png` 与 `F-CRACK-detail.png`（见 [双分辨率视觉证据](tkleak-visual-20261117-alias/visuals-1080/)）：P-CAV 气泡标记偏小、形态不够清晰；F-CRACK 裂纹线可见，但裂口/裂面细节不足且高光纹理造成干扰。两项精细视觉均待改进和复验；本次没有改代码或重建候选，也没有把静帧当作动态验收。整套综合回归未重复执行。

本次没有重跑全套综合业务回归；历史 `220916628` 的回归、性能和鼠标证据不转记为候选 `135205338` 的成绩。日常入口未替换。T00–T13 不作整体完成声明：T09–T10 仍缺真实 ODB 同实例三点三帧对照；T11 四个局部算例缺可追溯工程输入、ODB 与 Abaqus 求解环境；部分机制的精细效果、动态及声音仍待逐项验收。真实求解/结果对照不能由合成协议或本次视觉矩阵代替。

## 2026-11-17 P-SEAL 近景修正候选

最新候选为 `Build/WorkshopFaultReview-20261117-130035291/WorkshopPlatform.exe`，日常入口未替换。P-SEAL 小窗现只呈现密封目标、密封环和滴液；支撑脚不再进入近景专用层，主视图仍保留设备支撑上下文。1280×720 P-SEAL/P-CAV 泵专项及 P-SEAL 视角/阶段矩阵均 `Result: PASS`，近景画面已人工审看。四个受保护展示器共 671 个文件逐文件哈希一致；相关截图及准确范围见 [本次专项记录](pseal-closeup-20261117-final/verification.md)。

构建日志显示 Player Build Successful 且候选可运行，但 Unity Editor 随后报告 `Scripts have compiler errors.`，最终退出码 1；日志未找到对应 `error CS`，因此不宣称 Editor 正常退出。旧拼图脚本要求 166 张，本次定向采集为 20 张，不适用于该采集。Unity/Player 进程已退出；桌面分辨率和显示模式未更改。

本次仅修正和验收 P-SEAL 取景，不转记其他候选的性能结果，也不代表 T00–T13 全部验收。32 组案例的精细几何、部分全角度/连续动态与声音仍有未验项；真实 ODB 同实例三点三帧对照和四案例 Abaqus 求解仍缺输入及环境。

## 历史记录：2026-11-17 候选 130035291 与 220916628

前一候选 `Build/WorkshopFaultReview-20261116-220916628/WorkshopPlatform.exe` 的双分辨率性能记录仍归属于该候选，不转记为当前 `130035291` 候选结果。该轮按用户要求没有重复综合回归：保留其已有的 1080p 756 PASS、720p 风机专项 45 PASS、六重点案例真实鼠标播放抽样及文件保护记录。

性能记录位于 [current-performance-20261117-1228](current-performance-20261117-1228/)。Windows Player 可见窗口、PC 画质、D3D11、vsync 0，每个重点案例效果关闭/开启各采 8 秒；两种分辨率均完成六案例、相机生命周期检查和资源计数，Player exit 0。效果开启时：

| 分辨率 | 六案例平均帧时间范围 | P95 范围 | 最慢单帧 | 外部采样峰值工作集 |
| --- | --- | --- | --- | --- |
| 1920×1080 | 3.336–8.182 ms | 3.433–9.163 ms | 26.199 ms | 1,147,637,760 bytes |
| 1280×720 | 3.354–8.266 ms | 3.555–10.025 ms | 30.981 ms | 1,149,939,712 bytes |

上述平均与 P95 为短时六案例范围，不表示每帧都达到 60 FPS；最慢帧超过 16.67 ms。1080p 汽蚀样本最高 26.199 ms，720p 储罐样本最高 30.981 ms。测试仅比较当前候选效果开/关，不是升级前后基线或长期稳定性结论。六轮开关后 Mesh/Material/RenderTexture 计数均为 5556/534/33；短期计数不能替代长期泄漏认证。Unity Profiler 未用于定位 CPU/GPU 瓶颈。

最终文件保护报告 [delivery-final.json](current-performance-20261117-1228/delivery-final.json) 通过：日常入口与候选各 671/671 展示器文件长度/哈希一致，四设备总成配置与 HEAD 语义一致，相关 `.meta` 缺失数为 0；受保护 `WaterPumpViewer` 未变。没有改桌面分辨率/显示模式、正式构建入口、场景、设备拆装配置或展示器文件。

当前可交付结论：本地已有可运行升级候选，适用自动回归、专项抽样、鼠标播放抽样、双分辨率性能采集和文件保护复核均有相应证据；这不等于指导书全部验收，也不等于所有案例视觉完全通过。剩余状态：

- T00 的当前基线、资产/配置保护和可追溯记录已有；完整六案例改前画面无法事后补造，按部分完成保留。
- T01–T08 已实现较多接口、32 案例闭环和六重点机制；自动回归/定向检查通过不代表全部 32 套精细几何、所有角度、连续运动及声音完成。候选 135205338 的 TK-LEAK 局部孔口、湿润区与液滴已按 T02 范围人工查看；部分泵故障可见性和全案例动态审查仍有边界。
- T09–T10 的读取/导入接口及合成协议有回归证据；真实 ODB 同实例、同输出位置的三点三帧对照未执行。
- T11 未完成：`AnalysisCases/*/missing-inputs.md` 所列工程几何尺寸、材料、载荷/热源、连接边界、对照依据和真实 ODB/求解环境信息不齐，未宣称真实 Abaqus 求解。
- T12 有实训业务回归及 P-SEAL 单案例真实鼠标闭环证据，不是六案例逐一鼠标考核验收。
- T13 当前候选构建、回归抽样、视觉证据、性能采集、鼠标抽样和保护检查均有记录；由于部分精细效果未完全验收、性能有慢帧且真实分析缺输入，T13 只能按已执行部分检查记录，不能宣称整体验收完成。

下一步不需要重跑已有整套自动回归。可在本机继续做的事项是定向人工查看剩余 32 机制/六案例画面与连续运动/声音，并在确认问题后按需构建候选；无法仅靠继续等待完成的事项是 T09–T11 真实结果对照与求解，需先取得四案例工程输入、ODB 和 Abaqus 许可/环境。日常正式版的替换仍需单独授权，本轮没有部署正式入口。

## 历史记录：候选 220916628

当前可运行候选为 `Build/WorkshopFaultReview-20261116-220916628/WorkshopPlatform.exe`，日常入口未替换。修复 F-IMBAL 显示转子、局部轨迹和教学箭头相位不一致；预览采用设备名义转速及显式 1/48 教学时间倍率，可直接定位/倒拖，车间轨迹在唯一转子控制器之后组合。1×/5× 仅改变位移，停机与复位恢复轴心，设备工位/巡检点/碰撞代理保持不动。

本候选完成：1080p 综合回归 **756 PASS**、720p 风机同相与视觉专项 **45 PASS**，均 exit 0、零 FAIL。已直接查看末态聚焦、局部轨迹与完整窗口。日常/候选各 **671/671** 个展示器文件保护核对通过，四份总成/运动配置与 HEAD 语义一致，相关 `.meta` 零缺失。四设备知识源与数据库分块/摘要一致性通过，仍为 11 文档/85 片段；没有新增模型请求或 Unity GUI 在线问答验收。详见 [本候选证据](fan-phase-review.md)。

补充完成六重点案例普通启动的真实鼠标播放抽样和复位操作，保留末态；F-IMBAL 暂停/继续另有有效序列。普通输入记录也保留储罐一次空视口退出、误选设备及开始菜单遮挡，相关画面拒收，干净启动后的储罐画面正常。PLC-CONTACT 定向复核确认复位进度归零；热辅助层是独立开关，关闭后局部红色热叠加消失。旧复位截图实际显示 18/18 秒，不是复位后证据，现标记拒收；本次未发现复位代码问题。见[复位对照记录](fan-phase-220916628/contact-reset-20261117/verification.md)。

启动日志仍有 NavMesh 初始化与阴影图集警告；实际导航及业务回归已通过，不能称日志零警告。当前整体目标仍在进行；全部 32 套精细几何、无干扰完整连续运动/声音、真实 ODB 三点三帧及四案例真实 Abaqus 求解未整体验收。下方保留原候选的各自成绩，不自动继承为本候选的本次成绩。

## 前一候选 214915712

当前可运行候选为 `Build/WorkshopFaultReview-20261116-214915712/WorkshopPlatform.exe`，日常入口未替换。修复内部聚焦时轴承/叶轮仍被周围零件遮挡的问题；新增外圈滚道最多 1 mm 的局部预设损失，在原吸入法兰孔内新增教学滤网和可倒拖的预设受阻物。未覆盖原模型网格资产、场景或拆装配置，未改变桌面分辨率/显示模式。

本候选完成：1080p 综合回归 **756 PASS**、1080p 密封/汽蚀专项 **100 PASS**、720p 入口定向视觉与恢复检查 **32 PASS**；均为可见 Windows Player、exit 0、零 FAIL。入口最终近景和聚焦画面已直接查看，滤网与受阻物位于原入口孔内。日常/候选各 **671/671** 个展示器文件长度/哈希一致，四份总成/运动配置与 HEAD 语义一致，新增资源 `.meta` 零缺失。详见 [最新证据](bearing-and-inlet-review.md)。

轴承/叶轮内部聚焦修复在前一候选 `214307642` 分别取得 1080p **47 PASS**、720p **45 PASS**，并直接查看有效截图；这是该候选的视觉证据，不转记为最新候选的逐项运行成绩。`213515452` 隐藏运行的黑截图拒收，可见运行的轴承主聚焦仍有遮挡，也拒收；其自动断言成功不能替代视觉验收。

32 案例定义与业务闭环在本候选回归，但不代表全部 32 套精细几何、全部角度与连续运动均已验收。汽蚀的播放/静音/暂停音源状态已在本候选专项检查，完整声音听感仍未验收。真实 ODB 三点三帧及四案例 Abaqus 求解缺输入；当时未重新入库的最新知识文字已在后续候选交付中完成一致性核对。下方按候选保留历史结果，不能把旧版性能或鼠标结果自动继承为本版成绩。

## 前一候选 211755552

当前可运行候选为 `Build/WorkshopFaultReview-20261116-211755552/WorkshopPlatform.exe`，日常入口未替换。新增 P-IMPELLER/P-BEAR 可逆内部结构剖视，第一叶片局部预设最多 2 mm 磨蚀，以及 P-INLET 外表面标记与局部焦点；保留原总成和共享资产。

本候选完成：720p 叶轮 41 PASS、1080p 轴承 37 PASS、720p 入口 25 PASS、1080p 原密封/汽蚀专项 100 PASS、1080p 叶轮剖视性能与相机恢复 9 PASS。均为可见 Windows Player、exit 0、零 FAIL。叶轮剖视效果开启的约 8 秒平均/P95 为 3.334165/3.414154 ms。日常/候选各 671 个展示器文件保护核对通过。已直接查看最终叶轮、轴承、入口截图；完整运动与声音未验收。

详见 [该候选内部结构与磨蚀证据](pump-internals-review.md)。下文 `204305255` 的储罐、六案例性能及普通启动真实鼠标结果，以及更早全套回归，分别适用于原候选，未转移为该候选成绩。当时未完成的 P-BEAR 滚道缺陷、P-INLET 实体堵塞已在后续补充教学几何，见顶部最新状态；真实 ODB/Abaqus 缺口未消除，T00–T13 不能整体验收。

## 前一候选 204305255

此前可运行候选为 `Build/WorkshopFaultReview-20261116-204305255/WorkshopPlatform.exe`，日常 `Build/WorkshopPlatform/` 未替换。20:43 后新增 TK-LEAK 局部特写/小液滴修订和设备窗口后台相机渲染优化；构建 exit 0，无 Missing Script 构建警告。下面 `143353617` 的任务表及整套回归是历史结果，不自动归入该候选。

本候选完成：1080p 储罐专项 116 PASS、720p TK-LEAK 定向视觉 26 PASS、1080p 六案例可见性能及五项相机生命周期断言、P-SEAL 单案例普通启动真实鼠标闭环及缺复验门禁、日常/候选各 671 个展示器文件保护核对。六案例效果开启平均/P95 为 3.342–9.112 / 3.441–10.612 ms，均低于 16.67 ms；短时仍有慢帧，不是长期帧率保证。最新特写已能辨认预设孔口、水迹及小液滴，全部视角/连续动态未完整验收。

详见 [特写与后台渲染优化](local-closeup-and-background-rendering.md) 和 [真实鼠标记录](mouse-guided-204305255/verification.md)。真实 ODB 三点三帧、四案例真实 Abaqus 求解及部分泵内部结构精细表现仍未完成，T00–T13 不能整体验收。

## 历史候选 143353617

- 程序：`Build/WorkshopFaultReview-20261116-143353617/WorkshopPlatform.exe`。
- 构建入口：`WorkshopDemo.Editor.WorkshopPlatformBuild.BuildFaultReview`，未运行 Build Demo。
- 本候选构建日志：[leak-visual-build-final-20261116.log](leak-visual-build-final-20261116.log)。Player BuildReport 成功，Editor 批处理正常退出，退出码 0。
- 日常目录 `Build/WorkshopPlatform/` 未替换；候选包含现有独立展示器的受控副本。
- 本版继承主/近景按观察方向拟合包围盒、法线俯仰、呼吸口风帽焦点和出口上垫片绑定、储罐局部减薄及减薄/穿孔同帧恢复；修正可见侧联轴器教学轴线、出口法兰湿润/滴漏镜头，新增原叶片实例 Mesh 的局部预制曲线裂面。旧候选的成绩不自动继承为本版成绩。
- 本轮追加：`WorkshopEquipmentView.cs` 为 TK-LEAK 近景加入斜向观察角；`WorkshopPumpFaultPresenter.cs` 将已核实的泵支撑脚、支承腹板、轴承圈纳入 P-SEAL 近景上下文。

## 历史候选 143353617 实现与任务状态

| 任务 | 状态 | 已交付与限制 |
| --- | --- | --- |
| T00 | 部分已验证 | 原脏工作区、资源指纹、隔离层级、部件与展示器清单已保留；完整六案例改前画面缺失，不能事后补造。 |
| T01 | 本版自动回归通过 | 实例绑定、显式时间/进度、可逆 Presenter、来源读数、Mesh/材质/层恢复与回装后播放；使用当前实例，不跨设备缓存 Transform。六轮开关资源观察不等于长期无泄漏认证。 |
| T02 | 本版自动专项通过 | 同一 12 总成精细储罐；局部湿润/滴漏/积液与预设 12 mm 贯通孔，原内外壁保留，漏率未求解。派生来源及坐标见前日报告。 |
| T03 | 本版自动专项通过，视觉有边界 | P-SEAL 保留动静环、轴侧漏液和磨损示意；P-CAV 提供流道、汽泡、过程读数及可静音音效。均为教学模型，整机视角下微小漏液/汽泡不明显，不视作所有角度视觉已验收。 |
| T04 | 本版自动回归通过，视觉部分通过 | F-CRACK 原叶片局部预制曲线裂面、后缘连接、径向开口；配对裂唇偏移一致、局部曲率偏移大于 1 mm 且不超过 4.501 mm，完好态恢复。F-IMBAL 局部轨迹与 1×/5×。F-BEAR 不弯叶片；F-BLADE 仅外 12% 半径内预设不超过 6 mm 叶缘损失。曲线仍是教学预设，动态观感/声音未完整验收。 |
| T05 | 本版自动回归通过 | X1-01 带载端子局部温度与热视图；柜门、操作面板、HMI 在克隆故障预览中让开并可恢复。PE 排不承担默认热点。 |
| T06 | 部分完成 | 16 旧工况已按机制路由；不再统一输出裂长和开口。部分入口/磨蚀/轴承画面仍可能遮挡或仅作教学标记，不按精细效果全计完成。 |
| T07 | 本版自动回归通过 | 业务故障实例、快照、渐进/直接注入、保护和严重度分离；预览不改工单、故障年龄或实际到场状态。 |
| T08 | 定义与闭环本版通过，视觉部分完成 | 当前 32 案例及历史 8 定义保留；各有绑定、证据、诊断、维修和复验规则。TK-CORROSION 新增名义 8→4 mm 的局部预设减薄，实际测厚值仍“未提供”；不得当作全部 32 套精细动画完成。 |
| T09 | 合成协议检查已通过，真实数据待补 | 独立 v1 包络页、单位、通道、时间轴、插值与重载；无真实 ODB 对照。 |
| T10 | 最小合成协议检查已通过，真实闭环待补 | 固定 DC3D4、原始 NODAL NT11、标签映射、哈希/路径/预算、两帧缓存与探针；不支持结构/接触/XFEM/变化拓扑，未完成真实三点三帧。 |
| T11 | 依赖待补齐，未完成 | 四份 `AnalysisCases/*/missing-inputs.md` 分别记录尺寸依据、材料、载荷/热源、约束、基准及 ODB 缺口。未执行真实 Abaqus 求解，合成数据不计求解。 |
| T12 | 本版自动回归及单案例真实鼠标通过 | 沿原工单加入证据、诊断、维修、复验、驳回历史；考核隐藏答案及助手根因提示。P-SEAL 引导实训真实鼠标闭环和缺复验拦截通过，非 32 案例逐例鼠标验收。 |
| T13 | 本轮适用检查已执行，完整目标未通过 | 候选 143353617 已完成双分辨率自动回归、专项、截图采集、可见性能、单案例真实鼠标及文件保护检查。真实 ODB 对照缺输入、60 FPS 未达标、TK-LEAK 特写仍未视觉验收，不能写为 T00–T13 全部验收。 |

## 本日新增或修正入口

- `WorkshopEquipmentModel.ReviewedFan.cs`：局部裂口聚焦、径向开口和正弦曲线预设偏移；配对裂唇共享偏移，保留后缘连接，完好态恢复。修改运行时自有 Mesh，不覆盖离线源资产。
- `WorkshopEquipmentView.cs`：局部故障定位采用法线俯仰，主/近景均按相机空间八角点拟合；F-CRACK/F-BLADE/F-RUB 预览固定转子朝向。
- `WorkshopLocalFaultPresenter.cs`、`.Mechanisms.cs`：复用教学线、参考/异常条件对照、机制开关及可逆遮挡处理；局部效果不写业务指标。
- `WorkshopFaultSurfaceBake.cs`、`Resources/WorkshopFaults/bindings.json`：按真实 Mesh 三角面缓存表面点；风机采用实际运行旋转；出口上垫片从斜上方定位后选择前侧实际边缘，呼吸口纳入风帽与法兰。
- `Tools/FaultAssets/build_tank_perforation.py --mechanism thinning` 与 `WorkshopFaultDerivedImport.cs`：从源文件派生局部减薄壳体，原源文件和穿孔资产均保留。新增导入后三点壁厚及案例切换检查。
- `WorkshopCompactUIVerification.VisualPerformance.cs`：六案例四角度/四进度、阶段前后、近景、全部 32 机制画面；可见窗口效果开/关性能与资源回收取样。
- `Tools/FaultAssets/Test-FaultCandidate.ps1`、`build_review_sheet.py`：独立输出、可见窗口截图与性能；拒绝空画面，构建复核拼图。
- `Services/WorkshopAssistant/knowledge/`：四设备共 32 案例资料，标明教学来源及未实现范围；`app/main.py` 增加明确故障编码优先检索，避免向量检索漏掉新增案例。

## 助手与 Python

- [Python 导出器](python-tests-current.txt)：11 tests，OK。只验证协议和合成输入，不证明真实 ODB 导出。
- [助手离线](assistant-tests.txt)：6 passed；覆盖分块、32 案例、故障编码匹配、鉴权、索引错误及上游错误。
- [助手最终在线复测](assistant-thinning-retrieval.json)：health 200 ready，11 文档/85 片段；包含新增减薄与法兰说明的四份更新文档所有分块内容与库内摘要相符，chat 200。回答引用 P-SEAL/P-CAV/P-ALIGN/P-DRY 并否认教学效果等于 Abaqus 求解。首轮容器检查因缺少 `PYTHONPATH=/app` 未执行，补足执行环境后得到上述结果。
- API 检查使用构造的模拟快照，不能写成这版 Unity GUI 在线问答已验收。未输出凭据，未改其他知识库容器或数据库卷。

## 历史结果与拒收证据

本日较早 `004545579` 候选：`final-regression/` 综合 1080p/720p 各 748 PASS，泵 100、罐 116、原平台 217；`final-radial-visuals/` 两分辨率各 175 PASS，166 张 PNG 非空。它们是该候选的事实，不代替最新候选复测。

Hidden/最小化窗口曾产生黑截图及不真实的约 0.4 ms 帧耗时；这些画面和 PERF 行不采纳为渲染/性能证据。原 `build_review_sheet.py` 的标准差阈值曾误拒深色金属图，现同时检查标准差、亮像素与前景比例；仅非空检查仍不代表视觉合格。

出口上垫片为薄环面，初次正面射线未找到面，`build-005749129/` 失败保留；改为斜上方真实三角面定位后重建成功。没有用设备中心兜底。

## 历史记录：候选 143353617 自动结果与资产

输出位于 [leak-final-acceptance-20261116-143400000](leak-final-acceptance-20261116-143400000/)，程序为 `143353617` 候选。综合、专项、视觉采集和性能采集均 exit 0，报告为 `Result: PASS`、零 FAIL；PASS 仅表示自动检查或采集通过：

| 套件 | 1920×1080 | 1280×720 | 通过含义 |
| --- | --- | --- | --- |
| 综合回归 | 755 PASS | 755 PASS | 当前 32 案例、启停组合、业务/训练、可逆预览和合成协议断言 |
| 水泵专项 | 100 PASS | 未单跑 | 密封、汽蚀专项；720p 综合回归另计 |
| 储罐专项 | 116 PASS | 未单跑 | 新储罐、穿孔/减薄恢复及几何检测 |
| 原平台专项 | 217 PASS | 未单跑 | 当前业务与既有平台检查，非官方 15 步训练全验收 |
| 视觉采集 | 175 PASS | 175 PASS | 各 166 张 PNG 非空；不代表全部人工视觉合格 |
| 可见性能采样 | 19 PASS | 19 PASS | 只代表统计成功，不代表 60 FPS 达标 |

[人工审图与未完成效果](visual-review.md) 区分已直接查看的图和自动捕获图。以下结论属于候选 143353617：F-CRACK 完好态无明显裂口，末态可见局部曲线及保留连接；P-SEAL 保留支架/轴系上下文；TK-LEAK 特写当时未通过。该候选历史结论不覆盖最新候选 135205338；后者的 TK-LEAK 人工审图范围见本记录顶部和 [审图记录](visual-review.md)。部分泵入口/磨蚀效果仍有遮挡。未将非空截图检查计为精细效果验收。

减薄导入后三点射线检测为 **4.000004 / 4.967846 / 4.970584 mm**；中心与两侧仍有内外壁。完好阶段、减薄→穿孔、穿孔→减薄及双复位通过。数值来自展示几何检测，不能替代实际测厚或承载求解。[Blender 派生记录](tank-thinning-source.json) 保存源文件与派生 FBX 哈希、局部布尔参数、边界和流形检查；所有 724 个原非局部顶点保留，非流形边为 0。

新候选 `143353617` 和日常入口的独立展示器分别核对 **671/671** 个文件，长度/哈希均一致，零缺失和修改；包括受保护 WaterPumpViewer。四份总成/运动配置与 HEAD 语义一致，新增相关资源 `.meta` 无缺失。[本候选交付核对与程序指纹](delivery-check-leak-final-20261116-144300000.json)。这不是所有模型源文件的全量历史哈希认证。日常入口未替换。

## 可见窗口性能与真实鼠标

[当前候选完整性能统计](performance-review.md)：RTX 4060 Laptop、i9-14900HX、D3D11、PC 画质、vsync 0。候选 143353617 效果开启时 1080p 六案例平均帧时间 20.36–28.14 ms，720p 为 20.26–26.96 ms；建议 60 FPS 未达标。效果开/关不是升级前/后基线；尚未用 GPU/CPU Profiler 确定瓶颈。

六轮开关后 Mesh、材质和 RenderTexture 数量稳定；短期采样不作为长期无泄漏认证。

[本候选真实鼠标验证](mouse-candidate-143353617/verification.md)：14:47–14:55，普通启动 PID 9804、1280×720、无验证参数；P-SEAL 注入→派发→实际到场→证据/诊断/维修→提交→缺复验拦截→补复验→验收归档通过。Player 正常退出并确认进程结束。该单案例结果不代表 32 案例逐例鼠标验收，也不覆盖独立考核和快捷键。

## 未完成项与准确续作入口

- T00：六个重点案例的完整改前画面缺失，历史不可补造；保留现存基线和版本证据。
- T03/T04/T06/T08：按 [人工审图](visual-review.md) 中的源码入口继续补 TK-LEAK 穿孔/液滴构图、入口/磨蚀/轴承结构可见性及连续运动检查。定义完成与独立精细效果完成分别统计。
- T09/T10：`Tools/Abaqus/export_fault_odb.py`、`export_field_odb.py` 与 `WorkshopUI.Abaqus*.cs` 需使用可追溯真实 ODB 完成同实例、同输出位置的三点三帧对照；现有合成检查不替代它。
- T11：依次补齐 [四份缺输入档案](../../AnalysisCases/README.md) 的尺寸/材料/连接、载荷/热源、边界、基准、Abaqus 授权及结果。现未执行真实求解。
- T13 性能：从 `WorkshopEquipmentView.cs` 的观察室/近景相机与 RenderTexture、主场景后台相机开始用 Profiler 定位瓶颈；当前候选双分辨率可见采样均未达到 60 FPS，未据此断言 GPU 或 CPU 原因。
- 较早 `133824706` 候选曾出现 Editor 退出码 1；当前 `143353617` 候选构建已正常退出，日志保留供追溯。

## 2026-11-16 15:59 后续候选复核

本节补充并覆盖“当前候选”称谓；上文 `143353617` 的完整测试结果仍是该历史候选的实测结果，不转移到后续产物。

- `154550035` 与 `155109318` 均有 Player BuildReport 成功、Editor 退出码 0，但不可验收。两者普通业务回归均在首项 `Current four-device monitoring scene` 失败。
- `155109318` 于 15:58:33 普通启动（无验证参数，1280×720 窗口）。Player 日志报出大量 `The referenced script ... is missing`，覆盖 `Workshop UI`、四台设备、`Inspection robot`、训练步骤及相机等对象；随后提示 NavMesh 无效。进程在本次观察后正常关闭、退出码 0，这不代表场景功能正常。截图尝试因当前 PowerShell 会话缺少 `System.Windows.Forms.Screen` 未产出有效截图。
- 场景 45 个 MonoBehaviour 脚本 GUID 中，静态扫描在 `Assets/` 与 `Packages/` 找到 25 个对应 `.meta`，另有 20 个未解析。此项是资产完整性线索，不足以单独证明全部运行时 Missing 的根因；Unity Player 仍会对部分已解析来源的脚本报告 Missing。
- `155109318` 含 `Assembly-CSharp.dll` 且 `ScriptingAssemblies.json` 登记该程序集；不能据此确定脚本类型映射为何失败。候选间程序集哈希不同也仅表示内容不同，不据此推断编译缓存或构建器故障。
- 两个失败候选和证据均保留于 `Build/WorkshopFaultReview-20261116-154550035/`、`Build/WorkshopFaultReview-20261116-155109318/` 与 `finish-20261116-154513/candidate-*`。`Build/WorkshopPlatform/` 正式入口未替换；本次未改场景、构建器、脚本 `.meta` 或用户工作区源文件。
- 因尚未定位脚本映射故障，不能把失败归因为验证器启动顺序，也不应以改场景、重生成或清理缓存绕过。当前源码没有一个可验收的新 Player 候选。下一步应在不修改资产的前提下检查失败候选实际场景序列化与 Unity 脚本类型映射，并复核 `WorkshopDemo.unity` 45 个脚本引用中 20 个未解析 GUID 的来源；确认可运行后再执行完整回归、双分辨率视觉和文件保护检查。

结论：`143353617` 仍是上一份完整自动回归及单案例鼠标检查通过的候选，但不包含后续 `WorkshopFaultPresenter.cs` 修改。最新源码候选 `155109318` 构建成功而运行失败，故目前升级尚未形成可交付的新版本；性能、TK-LEAK 视觉与真实 ODB/Abaqus 求解等原有未完成项仍然有效。

## 2026-11-16 后续只读诊断补记

- 复核候选 `155109318` 的 Unity 构建日志，共有 **131** 条 `Script attached ... is missing or no valid script is attached` 警告：WorkshopDemo 主场景 70 条、官方训练场景 61 条。BuildReport 仍记为构建成功、0 errors，因此构建成功不能代表组件引用有效。
- 更正上一节静态 GUID 统计：当时报告“20 个未解析”的 20 个脚本 GUID，现均可在当前 `Library/PackageCache` 找到对应包 `.meta`。包缓存中存在文件不等价于 Unity AssetDatabase 已正确导入或 `MonoScript.GetClass()` 有效。
- 静态检查候选序列化数据时，`155109318` 的 `globalgamemanagers.assets` 未找到 `Assembly-CSharp` 对应 MonoScript 映射项；历史候选 `143353617` 有 80 项。新版 `Assembly-CSharp.dll` 与 `ScriptingAssemblies.json` 均存在，但缺少 MonoScript 映射的生成原因仍未确认。
- 本次只读诊断未修改项目源文件、场景、`.meta`、构建器或资产，也未运行 Unity；没有清理 `Library`、重建场景或覆盖任何候选。上述差异是诊断线索，不是已确认根因。
- 下一步：从构建日志提取 131 条警告对应的资产路径/组件，逐项映射场景与包脚本 GUID；使用当前 Unity 的只读 Editor/AssetDatabase 检查相关 `MonoScript.GetClass()` 结果和导入状态，再比较候选生成阶段的序列化映射。只有确认最小根因后才修改，并保留失败候选及其日志。

## 2026-11-16 18:52 候选 174917301 鼠标续测

- 候选 `Build/WorkshopFaultReview-20261116-174917301/WorkshopPlatform.exe` 的双分辨率自动回归、泵/罐/平台专项、视觉采集、可见性能采样及文件保护检查结果见 [wetting-contrast-acceptance-20261116-175000](wetting-contrast-acceptance-20261116-175000/) 和 [交付核对](wetting-contrast-delivery-20261116-175000.json)。该候选性能仍未达到建议 60 FPS；TK-LEAK 特写仍未通过人工视觉验收；真实 ODB/Abaqus 对照未完成。
- 18:28–18:51 普通启动 PID 35096，以真实鼠标完成 P-SEAL 注入、派发、到场、原因录入、维修提交、验收归档；实训复盘显示 1 项验收归档、0 项未关闭。逐步截图及边界见 [鼠标续测记录](mouse-input-probe-20261116-1810/verification.md)。
- 更正本节范围：本候选该条维修路径在提交维修后直接允许验收归档，**本次没有验证缺少复验拦截**。早先 `mouse-final` 的复验门禁证据只适用于对应历史候选，不能移作本候选成绩。
- 用户桌面原显示为 2560x1600、240 Hz；为恢复该候选已验证的 1280x720 控件布局，鼠标操作期间临时变更显示模式，完成后通过 Windows 显示重置恢复。Player 已正常退出。未改源码、场景、日常构建入口或受保护展示器。
- 当前总体验收仍未完成。下一轮定向本地验证约需 45–90 分钟：在该候选上真实鼠标复测引导实训的复验门禁，并尝试修正 TK-LEAK 特写构图后构建和复核；原候选自动套件与文件保护检查已有结果，不需重复覆盖。定位并优化到建议 60 FPS 可能另需数小时，结果也未保证。真实 ODB/Abaqus 三点三帧验证及求解取决于四案例资料、ODB 和 Abaqus 环境，依赖到齐前无法给出总完成日期。

## 2026-11-16 16:49 同步导入探针

- 为 `WorkshopFaultAssetDatabaseDiagnostic` 增加单独的 `RunSynchronousImportProbe` 入口；执行前刷新 AssetDatabase，并仅对主场景及官方训练场景实际引用的 46 个脚本 GUID 做 `ForceUpdate | ForceSynchronousImport`。未删除或清理 `Library`，未保存或改写场景、`.meta`、源模型和正式构建目录。
- 本次日志：[asset-database-import-probe.log](asset-database-import-probe.log)。Unity 6000.3.8f1 完成并正常退出；报告：[asset-database-import-probe.json](asset-database-import-probe.json)。
- 定向同步导入后仍有 **26/46** 个脚本 `MonoScript.GetClass()` 为空，主场景与官方训练场景合计 **47** 个 Missing MonoBehaviour；当前加载的 `Assembly-CSharp` 仍包含 **297** 个 `WorkshopDemo.*` 类型。重复导入不是修复，根因仍未确认。
- 尚未生成新 Player 候选；没有场景／`.meta` 改动，也没有改变正式入口或展示器。下一步需核查 Unity 生成 TypeDB 与项目程序集的对应关系及导入日志；在映射恢复前禁止把 Player BuildReport 成功当成交付通过。

## 2026-11-17 F-CRACK 裂面候选复核

- `WorkshopEquipmentModel.ReviewedFan.cs` 增加独立局部裂面网格，`WorkshopPumpFaultPresenter.cs` 放大 P-CAV 气泡示意；`WorkshopCompactUIVerification.Upgrade.cs` 增加裂面状态和几何断言。首次编译发现该验证文件的 `try/finally` 结构错误，已修正并重新构建。
- Unity 6000.3.8f1 经已验证可见的 `R:\` ASCII 工程别名构建候选 `Build/WorkshopFaultReview-20261117-183735020/WorkshopPlatform.exe`，Unity 正常退出码 0，Build Successful。桌面显示设置未修改，正式入口和受保护展示器未覆盖。
- 仅运行候选回归套件首项 `regression-1080`，即失败退出；没有继续运行其余套件。Player 日志含 116 条 Missing Script，`Current four-device monitoring scene` 断言失败。因此新裂面代码已编译进候选，但运行时及视觉效果未验证，候选不可交付。记录：[构建日志](fcrack-face-build-20261117-alias-final.log)、[失败回归](fcrack-face-acceptance-alias-20261117/regression-1080/)。
- 完整目标仍未完成。下一步阻断是查明 Unity Player 的 MonoScript/TypeDB 映射丢失；此前同步导入探针已不能解决，不能通过 Build Successful 推定可运行。真实 ODB/Abaqus、缺失工程输入、性能达标和若干案例精细视觉也仍分别未完成或依赖外部条件。
