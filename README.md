# 工业车间设备三维可视化监控与交互巡检系统

Unity 课程项目，包含官方工厂环境/装配训练样例，以及自研的设备监控、模拟告警和交互巡检 Demo。

## 获取与打开

工程使用 **Unity 6000.3.8f1**、URP 和新 Input System。贴图、模型、音频、字体及答辩 PPT 使用 Git LFS；请先安装 Git LFS，然后获取工程：

```powershell
git lfs install
git clone https://github.com/wangshenbo0502-boop/gongchangfangzhen.git
cd gongchangfangzhen
git lfs pull
git lfs fsck
```

在 Unity Hub 中添加克隆后的工程目录，使用对应 Unity 版本打开，等待包恢复及资源导入。资源约 3 GB，首次获取和导入需要预留时间及磁盘空间。GitHub 的普通 ZIP 下载不应作为获取完整 LFS 工程的首选方法。

## 场景与当前状态

- 当前构建入口：`Assets/WorkshopDemo/External/FactoryTraining/Scenes/urp_scene_with_training.unity`，用于官方工厂环境和装配训练。
- 自研监控巡检场景：`Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`。
- 核心源码：`Assets/WorkshopDemo/Scripts/`；生成和验证工具：`Assets/WorkshopDemo/Editor/`。
- 自研 Demo 的历史自动验证在 `WorkshopUI.Refresh()` 列表越界处中断，尚不能视为完整验收。官方高亮测试通过的范围与自研 Demo 分开记录。
- `Tools/Workshop Demo/Build Demo` 会重建并覆盖自研场景，同时替换构建入口；执行前保护人工修改。

## 接手开发

先阅读 [项目交接文档](项目交接文档.md)。后续 agent 从 [AGENTS.md](AGENTS.md) 开始，了解模块关系、已知问题、整改优先级及验证要求。

原始需求位于 `开发说明.txt`。资源来源及许可说明位于 `Assets/WorkshopDemo/External/SOURCES.md` 和对应资源目录；官方样例并非全部为 CC0 或本项目原创资源。本仓库没有为所有内容统一授予开源许可。

## 版本管理范围

保留 `Assets` 及全部配套 `.meta`、`Packages`、`ProjectSettings`、`Tools`、主要 `Evidence` 报告、文档和答辩 PPT。Unity 缓存、临时文件、构建产物、IDE 文件、本机状态及 `Downloads` 原始下载归档不纳入版本管理。场景所需的已导入资源仍完整保留。

`Tools` 中部分资源恢复脚本依赖未上传的原始下载归档或远端来源，不属于日常打开和运行步骤。克隆工程无需重新提取原始包。
