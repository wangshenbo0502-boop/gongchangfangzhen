# 水泵故障案例升级

2026-11-15 · Unity 6000.3.8f1 · WorkshopDemo · Windows x64

## 实施内容

- **T03-A / P-SEAL**。原 12 总成水泵的静环、动环和支座继续参与显示。移除原通用断裂插片。用密封腔轴侧出口绑定局部湿痕与重力滴漏，剖视只隐藏密封腔外壳并显示静环接触磨痕。业务注入维持原保护停机规则。
- **T03-B / P-CAV**。以吸入口盖和叶轮入口定位汽泡。流道剖视开关控制汽泡可见性，动画按时间重建汽泡生成、输运与缩小，出口模拟压力驱动原压力表表针。运行时表现读取设备采样，观察室使用标明来源的教学曲线与教学声响。
- **公共入口**。从“设备学习 → P-01 → 故障演示 → 水泵工况”选择案例，也可在故障学习页选择案例后点击“查看演示”。“看密封／看流道”开启局部剖视。“视图 → 部件与参数”可查看读数，并可导出带来源的 CSV。

## 建模与业务边界

绑定来自首轮导入层级记录和原水泵建模脚本。泵模型坐标中的密封出口为 `(-0.375, 0.633, -0.031) m`，叶轮入口为 `(-0.573, 0.660, 0) m`。压降、流量、汽泡与声响采用教学编排。漏率、密封间隙和入口压力缺少求解数据。密封面变化用局部磨痕示意，实体密封环保持名义几何。[四模型导入记录](../fault-upgrade-20261114-181917576/hierarchy.json) · [绑定配置](../../Assets/WorkshopDemo/Resources/WorkshopFaults/bindings.json)

主车间和观察室分别拥有表现实例。播放、暂停、倒拖和剖视不写入设备或工单状态。原设备、派发、到场、维修、验收和启停恢复继续由现有业务方法负责。

## 复验入口

`WorkshopDemo.Editor.WorkshopPlatformBuild.BuildFaultReview` 构建独立候选版。候选程序参数 `-workshop-fault-upgrade-verify <输出目录> -workshop-check-pump` 对两项水泵故障执行 UGUI 控件、模型、音频状态和导航工单验证。

## 构建与本次验证

- 最终候选版位于 `Build/WorkshopFaultReview-20261115-104245612/WorkshopPlatform.exe`。Unity 6000.3.8f1 的 Windows Player 构建成功，BuildReport 为 0 错误。Editor 仍因已知的中文路径 TypeDb 生成问题退出码 1。[BuildReport](../unified-build-20261115-104306.txt) · [构建日志](build-verified.log)
- 1920×1080 和 1280×720 两次水泵自动验收均通过，Windows Player 退出码为 0。验证原泵 12 总成和 119,696 个名义三角面、剖视恢复、倒拖复现、M 静音与暂停停声。两案例均从运行与停机两种状态实际派发巡检，确认到场后提交、验收和启停恢复。[1080p](verified-1080p/verification.txt) · [720p](verified-720p/verification.txt)
- 首轮自动化在点击故障页后没有等待 UGUI 布局刷新。脚本加等待后重跑，披露的问题没有带入最终两次报告。复审截图后调整密封镜头，并把随入口盖隐藏的紧固件、密封圈及铭牌附件纳入可恢复剖视，最终截图已复核。[密封近景](verified-1080p/01-seal-section.png) · [汽蚀剖视](verified-1080p/03-cavitation-section.png)
- 另外用 Windows 鼠标在 1280×720 下切换两个案例、拖动进度、开启剖视及恢复汽蚀泵壳，并逐张核对画面。[鼠标操作脚本](mouse-verified/verify-mouse.ps1) · [密封](mouse-verified/WorkshopFaultReview-20261115-104245612-02-seal.png) · [汽蚀](mouse-verified/WorkshopFaultReview-20261115-104245612-03-cavitation.png) · [恢复泵壳](mouse-verified/WorkshopFaultReview-20261115-104245612-04-restored-cover.png)
- 同一候选版的 720p 储罐回归通过，包含四设备绑定、储罐原拆装路径和 TK-LEAK 巡检工单。[储罐回归](tank-regression/verification.txt)
- 正式版及最终候选版的配套展示器共 671 个文件，路径、长度和 SHA-256 均与本轮改前清单一致，其中水泵展示器 170 个文件。本轮交付到候选版。[哈希比对](viewer-protection-final.json)

未纳入本轮的项目包括密封间隙的几何或试验校核、真实汽蚀声和 CFD、长时内存及性能测量。T04 风机和 T05 控制柜仍在后续实施范围。

T04 从 `WorkshopEquipmentModel.ReviewedFan.cs / ReviewedFanDamage` 接续。先确认第一片叶片的预制形状键和接缝端盖，再分别制作 F-CRACK 的局部裂面和 F-IMBAL 的轮毂相对摆振。复用 `fan.impeller.crack`、`fan.impeller.hub` 两组已验绑定。
