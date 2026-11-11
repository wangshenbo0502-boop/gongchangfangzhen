# 巡检设备材质

`Materials/` 为巡检场景专用 URP Lit 材质，`Textures/` 为它们使用的贴图。
不修改原版装配场景的共享材质或导入模型。

贴图复用工程内 Factory Training 的金属、喷漆和橡胶资源，来源为
`Assets/WorkshopDemo/External/FactoryTraining/Textures/`，保留其 Unity
Companion License 及 `External/SOURCES.md` 中的来源说明，并非全部为原创或 CC0。

对应关系由 `WorkshopEquipmentArt.Prepare()` 和 `Apply()` 维护：
蓝色喷漆用于主水泵，绿色金属用于备用泵及稳压罐，镀锌钢用于输送机和工艺罐，
警示黄用于风机外框，深灰柜体配浅灰柜门。仪表刻度与指示灯保留原有材质。

生成器复制所需的六张贴图（保持源资源不变），生成专用材质并绑定到设备模型；
重新生成不会重复创建资产。法线贴图按 Normal Map 导入，最大尺寸为 1024。
