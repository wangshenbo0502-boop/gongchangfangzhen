# 车间场景知识助手

Unity 大屏通过本机 HTTP 服务发送问题和当前业务快照。FastAPI 使用本地中文向量模型检索 PostgreSQL/pgvector，再把相关知识与快照交给 DeepSeek，回答附带知识片段编号。DeepSeek 密钥只保存在服务端，不进入 Unity 资源或客户端。

## 启动

需要 Docker Desktop（Linux containers）。在工程根目录执行：

```powershell
.\Tools\Start-WorkshopAssistant.ps1
```

本机 `.env` 已配置。新机器从 `.env.example` 建立自己的 `.env`，填写数据库密码和有效的 DeepSeek 密钥。首次启动需下载 Python 镜像、依赖和中文向量模型；之后模型保存在 Docker 卷中。启动后可以检查：

```powershell
.\Tools\Start-WorkshopAssistant.ps1 -CheckOnly
Invoke-RestMethod http://127.0.0.1:8018/health/ready
```

`knowledgeReady=true` 表示数据库和索引可用，`deepseekConfigured=true` 仅表示填写了密钥，不代表密钥鉴权通过。2026-11-08 本机知识库已就绪（9 个片段、512 维），当时访问官方接口返回 401，Unity 实际请求能显示该错误。2026-11-12 用户更换密钥并重新创建容器后，真实在线问答通过。若密钥来自兼容服务商，需匹配该服务的基础地址和模型名。

2026-11-12 的真实 `/api/chat` 在重新加载有效密钥后返回 HTTP 200，回答区分了现场总线中断与断电并附知识引用。2026-11-13 的正式 EXE 已用 Unity 当前设备快照获取在线回答。见 [密钥更新验证](../../Evidence/assistant-key-reload-20261112.md) 和 [工单与助手验证](../../Evidence/workflow-chat-20261113.md)。

修改 `.env` 后执行以下命令加载新配置；仅保存文件或执行 `restart` 不会更新容器环境变量。随后在 AI 问答页重新发送问题，旧报错会在开始请求时清除：

```powershell
docker compose -f Services/WorkshopAssistant/compose.yaml up -d --no-deps --force-recreate assistant
```

日常运行 `Build/WorkshopPlatform/WorkshopPlatform.exe`，点击左栏“AI 助手”并输入问题，或使用快捷提问。通过“更多 → 资料管理”查看资料列表并导入正文或 UTF-8 MD/TXT。也可在 Unity 打开 `Tools/Workshop Demo/Open Digital Twin` 后点击 Play。设备启停、派发和维修验收通过对应业务按钮执行。

## 配置

| 配置项 | 用途 |
| --- | --- |
| `POSTGRES_USER / POSTGRES_PASSWORD / POSTGRES_DB` | 专用数据库的首次初始化配置 |
| `DATABASE_URL` | 可选；复用已有 PostgreSQL 的连接 URI，需支持 vector 扩展且容器可访问 |
| `DEEPSEEK_API_KEY` | 服务端模型密钥 |
| `DEEPSEEK_BASE_URL` | 默认 `https://api.deepseek.com`，程序追加 `/chat/completions` |
| `DEEPSEEK_MODEL` | 当前默认 `deepseek-flash`，按账号/兼容服务商可用模型调整 |
| `WORKSHOP_ASSISTANT_TOKEN` | 可选本机服务令牌，开启时 Unity 启动环境须设置同名变量 |
| `HF_ENDPOINT` | 中文模型下载地址；本机使用可访问的镜像 |
| `PYTHON_IMAGE / PIP_INDEX_URL` | 可选构建镜像和 Python 包源，本机已配置网络镜像 |

默认仅暴露 `127.0.0.1:8018`，数据库不映射主机端口。Unity 可用启动环境变量 `WORKSHOP_ASSISTANT_URL` 更改助手地址。默认界面用于桌面横屏，已经针对 1280×720 和 1920×1080 检查；本轮未做移动端或 WebGL 适配。

首次创建的数据库密码会保存在数据卷中，之后仅修改 `.env` 不会修改已有 PostgreSQL 用户密码。不要删除卷来试密码。当前使用独立 `workshop-twin` 项目，保留原机器已有的知识库容器及数据；没有套用猜测密码修改已有数据库。

## 知识与数据

- 将 UTF-8 `.md` / `.txt` 放入 `knowledge/`，允许子目录。2026-11-16 本机实际索引为 **11 份资料、85 个片段**，覆盖四类设备的 32 种故障、结构与拆装、工单处置、复盘及故障云图。2026-11-13 的 81 个片段为历史记录。可从 [Abaqus 与故障分析入门](knowledge/Abaqus故障分析入门.md) 阅读带截图的水泵超温案例和风机裂纹案例。[此前在线问答验证](../../Evidence/fault-module-20261116/assistant-upgrade-retrieval.json)。最新泵滤网/滚道、风机同相慢放及四设备正文已完成 [源文件与索引一致性核对](../../Evidence/fault-module-20261116/fan-phase-220916628/knowledge-index.json)，该次未请求模型，不计为新增在线回答验收。
- 执行 `docker compose -f Services/WorkshopAssistant/compose.yaml restart assistant`，启动时按标题切块，使用 `BAAI/bge-small-zh-v1.5` 生成 512 维向量并更新变化内容。
- 数据表为 `workshop_knowledge_chunks`；优先查找问题中明确写出的故障编码，再补充余弦距离检索结果，去重后最多 5 段。设备编号不作为故障编码，相关度不足时不附加语义片段。
- 场景快照来自 `WorkshopSimulation` / `WorkshopDevice`，包括设备状态与指标、选中设备、活动告警、巡检状态和记录数。不会把 UI 文案当作另一套设备状态。
- **所有指标仍为模拟数据。** 数据库保存知识和向量；告警、巡检记录仍保存在 Unity 内存，重置/退出会清空，尚未做业务历史持久化。
- 无效密钥、余额不足、服务未就绪、网络超时会显示明确错误，不用预置文案伪装模型回答。

## 维护与验证

```powershell
docker compose -f Services/WorkshopAssistant/compose.yaml ps
docker compose -f Services/WorkshopAssistant/compose.yaml logs --tail 50 assistant
docker compose -f Services/WorkshopAssistant/compose.yaml stop
```

停止容器会保留知识和模型卷。凭据文件已加入 Git 忽略；不要将 `.env`、带密码的连接 URI 或完整展开后的 Compose 配置提交到仓库。

服务端测试可以在项目目录挂载 `tests/` 到已构建镜像执行：

```powershell
$testsPath = (Resolve-Path Services/WorkshopAssistant/tests).Path
docker run --rm --network none --mount "type=bind,source=$testsPath,target=/app/tests,readonly" workshop-twin-assistant python -m pytest -q
```

2026-11-08 实际通过 5 项服务测试，包括中文分块、场景知识、令牌鉴权、索引不可用、快照/引用及上游错误映射。该组测试的成功回答使用模拟上游响应。

2026-11-16 通过 6 项离线服务测试，新增明确故障编码优先、重复编码去重及设备编号排除检查，见 [测试输出](../../Evidence/fault-module-20261116/assistant-tests.txt)。另一次真实在线 `/api/chat` 返回 HTTP 200，能引用 P-SEAL、P-CAV 及新增 P-ALIGN/P-DRY 资料，并说明教学特效不能证明 Abaqus 已求解；这次 API 检查使用显式构造的模拟业务快照，不代替 Unity 界面在线问答验收。
