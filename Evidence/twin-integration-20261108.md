# 数字孪生大屏与知识助手联调

日期：2026-11-08，Asia/Shanghai（UTC+08:00）。

## 本轮实现

交付场景 `Assets/WorkshopDemo/Scenes/WorkshopDemo.unity`，Unity 6000.3.8f1 / URP / Input System 沿用工程版本。现有场景、导航、官方参考和美术资源保留；未运行场景生成器或重提取资源。

数字孪生大屏增加统计、台账、实际模拟温度曲线、设备详情、告警/记录、场景助手。修复 UI 构建前订阅导致的列表访问风险；业务真值保持在 Simulation/Device。相机关闭训练场景过强的后处理，生成器配置同步；运行时设置设备状态配色。

独立 Docker Compose 项目 `workshop-twin`：PostgreSQL 16 + pgvector、FastAPI、中文本地向量模型和 DeepSeek API。服务仅映射 127.0.0.1:8018，数据库无主机映射端口。知识与模型分别使用持久化卷，业务记录仍在 Unity 内存。

16:39:58 已将默认构建入口切换为业务 Demo，原设置备份为 `build-settings-before-twin-20261108-163958.txt`；Unity 配置命令日志正常退出（code 0）。没有构建 Windows EXE。

## 实际验证

| 时间 / 方法 | 结果与证据 |
| --- | --- |
| Docker 镜像中 pytest，禁用测试容器网络 | 5 passed in 2.14s；成功问答响应结构使用模拟上游，覆盖快照/知识引用与错误映射 |
| 容器健康检查 + `/health/ready` | 两个容器 healthy；knowledgeReady/databaseReady=true；9 段、BAAI/bge-small-zh-v1.5 |
| PostgreSQL 实际查询 | `车间场景知识.md` 9 行，vector_dims=512 |
| 中文问题“P-01水泵超温后如何派发巡检”实际向量检索 | 告警段 0.6825、维护段 0.6620、派发/处理段 0.6208，另命中操作保护和台账 |
| 16:37:01，1280×720，独立 D3D11 Editor Play | `twin-dashboard-verification-1280-20261108.txt` COMPLETE |
| 16:37:48，1920×1080，独立 D3D11 Editor Play | `twin-dashboard-verification-1920-20261108.txt` COMPLETE，同时记录模型鉴权受阻 |
| 1920 实际 Unity→Docker→DeepSeek 请求 | 健康检查成功；请求完成；上游鉴权失败明确显示；未产生虚假回答或成功历史 |
| 16:38:31，干净进程运行 WorkshopTwinSetup.VerifyBatch | `runtime-verification.txt` COMPLETE，两轮巡检闭环及连续重置 |

双分辨率专项检查：文字高度、温度曲线 CanvasRenderer/实际网格、中央视口、台账选择、启停、故障、派发、提前处理禁用、重复派发保护、长中文对话滚动、截图尺寸与非空像素。已查看 `twin-dashboard-1280-20261108.png`、`twin-dashboard-1920-20261108.png`，八台台账完整显示，3D 设备和曲线可见。按钮使用 EventSystem 射线和 ExecuteEvents；不等同于操作系统真实鼠标全流程。

业务检查：8 台运行、逐台程序选择与导航点可计算、输送与风机动画、唯一告警、到达前不能处理、只派发一次、实际导航到达、处理恢复和单条记录、连续两次重置、第二轮完整流程。

## 未完成的外部条件与验收边界

- 用户提供的 DeepSeek 密钥在官方地址返回 HTTP 401；服务映射为清晰的 502 错误，Unity 已验证能显示。没有完成有效密钥下的真实模型回答验收。需要有效密钥，或密钥对应服务商的基础地址/可用模型。
- `deepseekConfigured=true` 仅表示存在配置，不表示账户或模型可用。不在报告、Unity 或 Git 中记录密钥。
- 全部指标为模拟数据，无真实 PLC/传感器接入。当前单机器人、P-01 单活动告警；知识持久化不等于业务历史持久化。
- Windows EXE、移动端/WebGL、长时间运行、真实鼠标三维选择/相机全交互、本轮官方训练流程没有验收。
- 首次导入/热重载验证曾出现 Inspector.Update 空引用；已加入引用保护，最终干净进程业务复测完成，但所有热重载生命周期未验收。
- 早期大屏检查曾在同进程切换分辨率失败；最终改为每个分辨率独立进程。曲线缺少 CanvasRenderer 已修复；新增验证工具一度因 GetMesh API 用法错误编译中止，修正后重跑通过。以表中最终报告为准。
- 图形日志仍有官方脚本弃用警告和编辑器 UIElements/阴影相关诊断；专项报告未记录业务运行 Error/Exception，不将其解读为整个工程所有日志完全无警告。

## 环境与历史保留

Docker Desktop 启动初期的 secrets socket 故障通过备份旧 socket 和重启修复，未重置 Docker、删除数据卷或改动已有知识库密码。新建专用数据库，原机器其他容器保留。

历史业务失败报告保存在 `runtime-verification-before-twin-20261108-162249.txt`；本轮中间业务通过报告另存 `runtime-verification-before-twin-20261108-163830.txt`。图形检查中间报告保存为 `.previous` 文件；当前 PNG 为最终截图，不用早期过曝截图作为交付效果。

启动、配置、更新知识与维护命令见 `Services/WorkshopAssistant/README.md`。本机服务保持运行。
