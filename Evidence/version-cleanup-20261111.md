# 旧构建清理与发布

日期：2026-11-11。用户明确授权删除旧版本并上传 GitHub，同时要求保留 `D:\UnityProjects\WorkshopInspectionDemo\Build\WorkshopCombined`。

## 已清理的旧入口

以下目录原先位于 `D:\UnityProjects\WorkshopInspectionDemo\Build`，合计约 4.4 GiB：

| 目录 | 文件数 | 原入口 |
| --- | --- | --- |
| RobotAssembly | 181 | RobotAssembly.exe |
| WorkshopCombined-before-faults-20261109-232754 | 185 | WorkshopSystem.exe |
| WorkshopDemo | 181 | WorkshopDemo.exe |
| WorkshopInspectionV2 | 181 | WorkshopInspection.exe |

永久递归删除命令被自动审批拒绝，随后改为 Windows 回收站方式完成清理；没有清空回收站。执行前逐项核实绝对路径为上述 Build 目录的直接子目录、不是目录联接、不是受保护目录。

清理后旧 Build 目录仅剩 `WorkshopCombined`。其文件对照 `protected-combined-before-integration-20261111.csv` 全量 SHA256 校验：206 个文件，变更或缺失为 0。

## 保留内容

- 当前统一程序：本工程 `Build/WorkshopPlatform/WorkshopPlatform.exe`。
- 受保护成品：`D:\UnityProjects\WorkshopInspectionDemo\Build\WorkshopCombined`。
- 两个 Unity 源工程、官方训练场景、资源与 `.meta`、历史验证记录。
- 独立泵建模源码与本机查看器：它是模型审核工具，没有纳入旧车间构建清理。

## 发布范围

目标仓库：`wangshenbo0502-boop/gongchangfangzhen`，分支 `main`。

工程源码、Unity 资源、知识服务和验证记录通过 Git/Git LFS 提交。`Build/`、本机凭据、缓存、过程备份和独立查看器编译输出不进入源码提交；统一 Windows 程序单独打包作为 Release 附件。

工作区原先缺失的答辩 PPT、开发说明和项目交接文档属于本轮开始前已有变更，没有纳入本次旧构建清理或删除提交。

功能验证范围沿用 `platform-layout-20261111.md`；本次清理不修改业务源码。

## GitHub 发布结果

2026-11-11 19:41:58（+08:00）已发布 Windows 统一版，源码和全部 414 个待上传 LFS 对象推送成功，远端 `main` 已核对为 `e970332802f9a7f1ef7e5919e35365e890e8988e`。后续说明文档提交不改变此版本程序。

- Release：[workshop-platform-20261111](https://github.com/wangshenbo0502-boop/gongchangfangzhen/releases/tag/workshop-platform-20261111)。
- 对应源码：`e970332802f9a7f1ef7e5919e35365e890e8988e`。
- 附件：`WorkshopPlatform-Windows-x64-20261111.zip`，435,262,987 字节。
- SHA256：`C8674BDFFF2D6BEA82FCFC9C4DDAAD6AB62EE3DE5B281FAFC5FAE2D14C57586E`。
- ZIP 包含完整 `WorkshopPlatform/` 目录、EXE、Unity 运行库和数据文件；共 212 个条目（含目录）。

发布前核对 GitHub 附件状态为 `uploaded`，大小和服务端 SHA256 与本地一致；发布后通过无凭据 API 查询确认 Release 公开可见、`draft=false`。发布标签指向上述源码提交。受保护副本再次全量校验：206 个文件，变更或缺失为 0。
