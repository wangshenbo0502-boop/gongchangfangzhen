# 助手密钥更新与真实问答验证

日期：2026-11-12，Asia/Shanghai（UTC+08:00），约 19:41–19:44。用户反馈更新密钥后仍无法问答。

## 原因与处理

本机忽略文件 `Services/WorkshopAssistant/.env` 修改时间为 19:39:11。私下比较文件与运行中容器的密钥摘要，结果不相同；未输出密钥或摘要。Compose 在创建容器时注入环境变量，保存 `.env` 不会更新已运行的容器。

执行以下命令，仅重新创建助手容器，保留数据库、模型与知识卷：

```powershell
docker compose -f Services/WorkshopAssistant/compose.yaml up -d --no-deps --force-recreate assistant
```

重新比较结果为相同。没有修改凭据、Unity 源码、场景、模型、构建文件或受保护展示器目录。更新根 README 与服务说明中的当前验证状态，保留首次失败记录。

## 本次实际验证

| 方法 | 实际结果 |
| --- | --- |
| 容器新旧配置比较 | 重建前密钥不同，重建后相同；只输出比较结果 |
| `/health/ready` | ready，knowledgeReady/databaseReady=true，chunks=40，初始化错误为空 |
| 本机真实 POST `/api/chat` | HTTP 200，模型为 deepseek-flash，answer 非空，sources 包含 5 项知识引用 |
| 回答内容检查 | 区分 PLC-COMM 通信质量为 0% 与 PLC-LOW 控制电源欠压；指出无场景快照，不能推断当前设备状态 |
| Compose `ps`（19:42:13） | assistant 与 pgvector 均 healthy，服务绑定本机 8018 |

实际问题为“PLC-01 现场总线通信中断是不是整机断电？请依据知识库回答并引用资料。”，history 为空，未提供 Unity 场景快照。首项来源为 `控制柜故障速查.md` 第 3 号片段，score=0.832。此结果是真实模型请求，不是模拟上游测试。

## 客户端与范围

客户端源码 `WorkshopAssistantClient.Send` 在开始新问题时清除 Error，下一次请求即可使用服务端新凭据。凭据加载不要求重建或重启 Unity。

19:42 检查时统一平台已不在运行，因此重新启动已有 `Build/WorkshopPlatform/WorkshopPlatform.exe`，新进程 PID 41316。未代替用户发送界面草稿。本次没有重新验收 Unity 界面发送与回答渲染，也没有重新验证设备闭环或训练场景。此前失败与入库过程见 `assistant-startup-20261112.md`。
