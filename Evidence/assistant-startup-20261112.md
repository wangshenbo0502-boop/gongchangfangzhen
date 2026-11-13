# 知识补充与统一平台启动

日期：2026-11-12，Asia/Shanghai（UTC+08:00）。本次操作约 19:22–19:32，用户要求补充知识库并启动项目试用 AI 助手。

## 本次变更

新增 `Services/WorkshopAssistant/knowledge/` 下五份 UTF-8 Markdown：

- 循环水泵故障速查：P-HOT、P-CAV、P-SEAL、P-BEAR。
- 储罐故障速查：TK-PRESS、TK-LEAK、TK-LEVEL、TK-SUPPORT。
- 排风机故障速查：F-IMBAL、F-BLOCK、F-BEAR、F-CRACK。
- 控制柜故障速查：PLC-HOT、PLC-LOW、PLC-COMM、PLC-CONTACT。
- 助手试用与工单答疑：试用问题、派工条件、到场限制、维修提交、验收驳回、启停恢复和鉴权说明。

资料依据本工程 `WorkshopFaultCatalog.cs`、`WorkshopSimulation.cs`、`WorkshopAssistantClient.cs` 和 README 编写，保留本地模拟、教学工况、状态以请求快照为准、助手不能直接控制设备等边界。修正原场景知识中“精细泵尚未接入”的过时描述，说明泵/风机已接入、储罐/控制柜新版模型仍未替换平台。

未改动 Unity 业务源码、场景、模型、`.meta` 或凭据；未执行场景生成器或 EXE 重建。原工作区已有大量未提交修改，全部保留。

## 启动与环境处理

初次 `Tools/Start-WorkshopAssistant.ps1` 等待 Docker 引擎超时。Docker 本机错误记录为旧 `docker-secrets-engine/engine.sock` 无法访问，导致 Secrets Engine 初始化失败。

Windows 普通重命名和 Win32 文件句柄备份尝试均未成功；最终从已有 `docker-desktop` WSL 发行版，使用明确路径将该遗留通信文件移为同目录 `engine.sock.backup-20261112-1930`，并重新启动 Docker Desktop。此文件是运行时通信端点，未删除数据库卷、重置 Docker、修改数据库密码或更改 Docker 配置。

再次执行项目启动脚本成功，构建助手镜像并启动 `workshop-twin`。现有 pgvector 数据卷保留，Compose 本次新建 `knowledge_imports` 卷。仅本机暴露 8018 端口。

实际启动 `Build/WorkshopPlatform/WorkshopPlatform.exe`，进程 PID 28836，窗口正常响应；切换到“AI问答”。截图检查使用 DPI 感知后的 1600×900 客户区，见 [AI 页面](assistant-startup-20261112/ai-page.png)。截图显示四台设备、知识库已连接及 40 个片段。保留用户输入框内容，未代替用户发送界面中的草稿。

## 本次验证

| 方法 | 实际结果 |
| --- | --- |
| 服务本机虚拟环境 `python -m pytest -q` | 5 passed in 4.69s；其中成功模型响应仍是模拟上游测试 |
| `docker info` | 引擎恢复，ServerVersion 29.8.0 |
| Compose `ps` | assistant 与 pgvector 均 healthy |
| `/health/ready` | ready；knowledgeReady/databaseReady=true；chunks=40；初始化错误为空 |
| `/api/knowledge` | 6 份资料：原场景知识 13 段、答疑 7 段、四类故障速查各 5 段 |
| PostgreSQL 实际查询 | 共 40 行，各资料 `vector_dims(embedding)=512` |
| 实际中文向量检索“P-01 汽蚀与轴承损伤有什么区别” | 首项命中循环水泵故障速查的辨析段，score=0.8330 |
| 实际中文向量检索“PLC-01 现场总线中断是不是断电” | 首项命中控制柜 PLC-COMM 段，score=0.8285 |
| 实际中文向量检索“维修提交后为什么故障还在” | 首项命中待验收说明，score=0.8361 |
| 真实 HTTP `/api/chat` | HTTP 502，detail 为“DeepSeek 密钥无效或无访问权限，请检查服务端配置” |
| Windows 统一程序 | 实际运行并打开 AI 问答页，显示知识库连接及 40 段 |

真实问答问题为“P-01 汽蚀和轴承损伤有什么区别？请引用知识库，并说明维修提交后是否立即解除故障。”，通过现有本机服务发送，没有 Unity 状态快照。服务将上游 401/403 映射为上述 502；本次未单独记录上游究竟为哪一个状态码。`deepseekConfigured=true` 只说明环境中存在密钥，不能作为鉴权或回答通过证据。

## 保持运行与未完成项

统一平台及两个项目容器保持运行。知识查询、资料列表与中文检索已通过；在线模型回答因现有凭据鉴权失败而受阻。需要用户在忽略的 `Services/WorkshopAssistant/.env` 中配置有效密钥；若为兼容服务，需匹配基础地址和模型，再执行说明中的 `up -d --force-recreate assistant`。不在报告或日志输出任何密钥、令牌或数据库连接 URI。

本次没有重跑设备业务闭环、双分辨率、模型展示或训练场景验收，也没有宣称真实模型回答成功。服务端测试通过、实际检索通过、窗口连接成功与在线回答失败分别记录，不能相互替代。
