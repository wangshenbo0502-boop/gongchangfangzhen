# 轴流风机 Blender 外观审核交付

首轮交付日期：2026-11-11；模型修订与复验：2026-11-12，Asia/Shanghai。范围：`F-01` / `industrial-fan` 的 Blender 建模、渲染和源文件复核。本次未将风机导入 Unity，也未重建车间或运行 Unity 业务验收。

用户要求以现有小车、离心泵为质量参照，提高结构细节与专业程度；允许合理增加结构分组，但不得超过 16 组。先交 3–4 张不同角度 Blender 效果图，用户满意后再接入 Unity。此要求覆盖原规范的八组及立即导入流程。

## 交付

- 源文件：`../model/source/industrial-fan.blend`。
- 四张整机主图：`../model/previews/industrial-fan/assembled.png`、`inlet-front.png`、`motor-side.png`、`elevated.png`。
- 接线盒铭牌近景：`../model/previews/industrial-fan/terminal-detail.png`。
- 补充预览：同目录拆解、内部、叶片近景及三张故障形态，共十张。
- 详细结构、导出例外与限制：`../model/reports/industrial-fan.md`。
- 独立观察入口：`../model/scripts/Open-FanReview.ps1`。已打开对应源文件的独立 Blender 窗口，保留用户原有未保存窗口。

2026-11-12 修订后为 11 个功能总成，634 个网格、99,146 个三角面，Blender XYZ 尺寸约 1.600000 × 1.600000 × 1.800000 m。按用户意见移除外露电线、格兰接口和接地线，保留接线盒本体及盒盖固定件；在盒盖新增机器编号、序列号、制造年月与条码。其余包含实体筒壁和法兰孔、八片展向扭曲翼型叶片、入口护网及出口防鸟网、电机散热筋与轴键、导流支承、曲面检修盖、端子和减振底座。

## 本次验证

| 方法与报告 | 时间（+08:00） | 结果 |
| --- | --- | --- |
| 最终 FBX 独立回读：`industrial-fan-fbx-roundtrip.json` | 20:42 | `fbx_checks_pass=true`；尺寸、11 总成及转轴通过，无相机/灯光/动画 Action |
| 图像尺寸与亮度变化：`industrial-fan-preview-verification.json` | 20:45 | 10/10 张均为 1920×1080，非空画面检查通过 |
| 最终源文件独立重开：`industrial-fan-reopen-verification.json` | 20:45 | `source_checks_pass=true`；G1–G12、叶片实测、材质打包和操作器检查通过 |
| 逐张查看四张主图 | 本轮 | 已查看取景、整机完整性、材质和细节；用户视觉认可仍待确认 |
| 2026-11-12 最终源文件重开：`industrial-fan-reopen-verification.json` | 00:35 | `source_checks_pass=true`；634 网格、99,146 面、G1–G12 全通过 |
| 2026-11-12 最终 FBX 独立回读：`industrial-fan-fbx-roundtrip.json` | 00:35 | `fbx_checks_pass=true`；11 总成、尺寸及转轴通过 |
| 接线盒铭牌近景复查 | 00:25 | 已查看条码、编号和年月的盖板摆放；仍待用户视觉确认 |

JSON 报告均位于 `../model/reports/`。G4 按用户授权的 11 组检查；原规范其他设备的分组规则不受影响。

八片叶片实测扭角约 20°，根部厚 16 mm、端部厚 3.5 mm；名义叶尖径向间隙约 43.394 mm。16/16 张文件图像打包。1–320 帧拆开再组装动画的共享路径、反向及归位检查通过，七个视图操作器、十一个总成选择操作器及拆解滑条检查通过。未将这些脚本检查表述为全部鼠标交互验收。

## 验证边界

用户外观审核待完成。未进行全程多物体碰撞认证、制造公差认证、Unity 材质转接/业务验证或 Abaqus 求解。叶片裂口是同网格形态键教学示意，完好态采用重合闭合的预置缝，不能视为完整焊接拓扑的真实起裂求解。

FBX 为保留总成层级而导出结构 Empty；关闭 `bake_space_transform` 后通过回读，开启时的轴向失败记录保留。部分多边形端盖的切线导出警告尚存，留待用户外观确认后在 Unity 接入阶段处理并验证。不得将本次源文件与图片检查写成全部建模规范或 Unity 最终验收通过。
