using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace WorkshopDemo
{
    public sealed class WorkshopAssistantClient : MonoBehaviour
    {
        [Serializable] public sealed class Turn { public string role; public string content; }
        [Serializable] public sealed class Source
        {
            public int id;
            public string source;
            public int chunk_index;
            public string content;
            public float score;
        }
        [Serializable] public sealed class Answer
        {
            public string answer;
            public string model;
            public string snapshotAt;
            public Source[] sources;
        }
        [Serializable] public sealed class KnowledgeDocument
        {
            public string title, source;
            public int chunks;
        }
        [Serializable] sealed class DocumentList { public KnowledgeDocument[] documents; }
        [Serializable] sealed class ImportRequest { public string title, content; }
        [Serializable] sealed class ImportResult { public string source; public int chunks; }
        [Serializable] sealed class Health
        {
            public bool knowledgeReady;
            public bool databaseReady;
            public bool deepseekConfigured;
            public int chunks;
        }
        [Serializable] sealed class Failure { public string detail; }
        [Serializable] sealed class DeviceSnapshot
        {
            public string deviceId, name, kind, status;
            public float temperature, vibration, rpm;
            public bool hasRpm, hasVibration;
            public string faultCode, resultSource;
            public float pressure, flow, level, voltage, communication, loadPercent;
        }
        [Serializable] sealed class AlarmSnapshot
        {
            public string id, deviceId, progress, faultCode, faultName, severity, action;
            public string reporter, department, description, assignee, cause, repairAction, acceptanceNote, dueAt;
            public bool trips;
        }
        [Serializable] sealed class Snapshot
        {
            public string capturedAt, selectedDeviceId, inspectorStatus;
            public string dataSource = "simulation";
            public int runningCount, recordCount;
            public DeviceSnapshot[] devices;
            public AlarmSnapshot[] alarms;
        }
        [Serializable] sealed class Request
        {
            public string question;
            public Turn[] history;
            public Snapshot snapshot;
        }

        public WorkshopSimulation simulation;
        public event Action Updated;
        public readonly List<Turn> Conversation = new List<Turn>();
        public bool Busy { get; private set; }
        public string ConnectionStatus { get; private set; } = "正在连接知识库";
        public string Error { get; private set; } = "";
        public Answer LastAnswer { get; private set; }
        public bool KnowledgeReady { get; private set; }
        public bool ImportBusy { get; private set; }
        public string ImportStatus { get; private set; } = "待导入资料";
        public readonly List<KnowledgeDocument> Documents = new List<KnowledgeDocument>();
        bool listing;
        string endpoint;
        string token;
        UnityWebRequest activeRequest;
        UnityWebRequest importRequest, listRequest;

        void Awake()
        {
            endpoint = Environment.GetEnvironmentVariable("WORKSHOP_ASSISTANT_URL");
            if (string.IsNullOrWhiteSpace(endpoint)) endpoint = "http://127.0.0.1:8018";
            endpoint = endpoint.TrimEnd('/');
            token = Environment.GetEnvironmentVariable("WORKSHOP_ASSISTANT_TOKEN");
        }

        void OnEnable() { StartCoroutine(PollHealth()); }

        IEnumerator PollHealth()
        {
            while (true)
            {
                using (var request = UnityWebRequest.Get(endpoint + "/health"))
                {
                    request.timeout = 5;
                    yield return request.SendWebRequest();
                    KnowledgeReady = false;
                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        Health health = null;
                        try { health = JsonUtility.FromJson<Health>(request.downloadHandler.text); }
                        catch (ArgumentException) { }
                        if (health != null)
                        {
                            KnowledgeReady = health.knowledgeReady;
                            ConnectionStatus = !health.databaseReady ? "数据库未连接" :
                                !health.knowledgeReady ? "知识库索引初始化中" :
                                !health.deepseekConfigured ? "知识库就绪 · 待配置模型密钥" :
                                "知识库已连接 · " + health.chunks + " 个片段";
                        }
                        else ConnectionStatus = "服务响应无效";
                    }
                    else ConnectionStatus = "知识库服务离线";
                }
                Updated?.Invoke();
                yield return new WaitForSecondsRealtime(15);
            }
        }

        public void Ask(string question)
        {
            question = (question ?? "").Trim();
            if (Busy || question.Length == 0 || !simulation) return;
            if (question.Length > 1500)
            {
                Error = "问题请控制在 1500 字以内";
                Updated?.Invoke();
                return;
            }
            StartCoroutine(Send(question));
        }

        Snapshot Capture()
        {
            var devices = new List<DeviceSnapshot>();
            foreach (var device in simulation.devices)
            {
                if (!device) continue;
                devices.Add(new DeviceSnapshot {
                    deviceId = device.deviceId, name = device.displayName, kind = device.KindText,
                    status = device.StatusText, temperature = device.Temperature,
                    vibration = device.Vibration, rpm = device.Rpm,
                    hasRpm = device.HasRpm, hasVibration = device.HasVibration,
                    faultCode = device.Fault?.Code ?? "", resultSource = device.ResultSource,
                    pressure = device.Pressure, flow = device.Flow, level = device.Level,
                    voltage = device.Voltage, communication = device.Communication, loadPercent = device.LoadPercent
                });
            }
            var alarms = new List<AlarmSnapshot>();
            foreach (var alarm in simulation.Alarms)
                if (!alarm.resolved && alarm.device)
                    alarms.Add(new AlarmSnapshot {
                        id = alarm.id, deviceId = alarm.device.deviceId, progress = alarm.progress,
                        faultCode = alarm.fault.Code, faultName = alarm.fault.Name,
                        severity = alarm.fault.SeverityText, action = alarm.fault.Action, trips = alarm.fault.Trips
                        , reporter = alarm.reporter, department = alarm.department, description = alarm.description,
                        assignee = alarm.assignee, cause = alarm.cause, repairAction = alarm.repairAction,
                        acceptanceNote = alarm.acceptanceNote, dueAt = alarm.dueAt == default ? "" : alarm.dueAt.ToString("o")
                    });
            return new Snapshot {
                capturedAt = DateTimeOffset.Now.ToString("o"), devices = devices.ToArray(),
                alarms = alarms.ToArray(),
                selectedDeviceId = simulation.Selected ? simulation.Selected.deviceId : "",
                runningCount = simulation.RunningCount, recordCount = simulation.Records.Count,
                inspectorStatus = !simulation.inspector ? "未配置" :
                    simulation.inspector.HasArrived ? "待维修记录" :
                    simulation.inspector.IsBusy ? "巡检途中" : "待命"
            };
        }

        IEnumerator Send(string question)
        {
            Busy = true;
            Error = "";
            LastAnswer = null;
            var payload = new Request {
                question = question, snapshot = Capture(), history = Conversation.ToArray()
            };
            Conversation.Add(new Turn { role = "user", content = question });
            Updated?.Invoke();
            using (var request = new UnityWebRequest(endpoint + "/api/chat", "POST"))
            {
                activeRequest = request;
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrWhiteSpace(token))
                    request.SetRequestHeader("Authorization", "Bearer " + token);
                request.timeout = 90;
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    LastAnswer = null;
                    try { LastAnswer = JsonUtility.FromJson<Answer>(request.downloadHandler.text); }
                    catch (ArgumentException) { }
                    if (LastAnswer != null && !string.IsNullOrWhiteSpace(LastAnswer.answer))
                        Conversation.Add(new Turn { role = "assistant", content = LastAnswer.answer });
                    else Error = "助手未返回有效回答";
                }
                else
                {
                    Error = request.responseCode == 0 ? "无法连接本地知识库服务" :
                        "问答失败（HTTP " + request.responseCode + "）";
                    try
                    {
                        var failure = JsonUtility.FromJson<Failure>(request.downloadHandler.text);
                        if (!string.IsNullOrWhiteSpace(failure?.detail)) Error = failure.detail;
                    }
                    catch (ArgumentException) { }
                }
                activeRequest = null;
            }
            // Only successful pairs become model history.
            if (!string.IsNullOrEmpty(Error) && Conversation.Count > 0)
                Conversation.RemoveAt(Conversation.Count - 1);
            while (Conversation.Count > 8) Conversation.RemoveRange(0, 2);
            Busy = false;
            Updated?.Invoke();
        }

        public void Clear()
        {
            if (Busy) return;
            Conversation.Clear();
            LastAnswer = null;
            Error = "";
            Updated?.Invoke();
        }

        string RequestError(UnityWebRequest request)
        {
            if (request.responseCode == 0) return "无法连接本地知识库服务";
            try
            {
                var failure = JsonUtility.FromJson<Failure>(request.downloadHandler.text);
                if (!string.IsNullOrWhiteSpace(failure?.detail)) return failure.detail;
            }
            catch (ArgumentException) { }
            return "请求失败（HTTP " + request.responseCode + "）";
        }
        void Authorize(UnityWebRequest request)
        {
            if (!string.IsNullOrWhiteSpace(token)) request.SetRequestHeader("Authorization", "Bearer " + token);
        }
        public void ImportKnowledge(string title, string content)
        {
            if (ImportBusy) return;
            if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 100 ||
                string.IsNullOrWhiteSpace(content) || content.Length > 50000)
            {
                ImportStatus = "导入未提交：标题需为 1–100 字，正文需为 1–50000 字";
                Updated?.Invoke(); return;
            }
            StartCoroutine(Import(new ImportRequest { title = title.Trim(), content = content }));
        }
        IEnumerator Import(ImportRequest payload)
        {
            ImportBusy = true; ImportStatus = "正在保存资料并建立检索索引…"; Updated?.Invoke();
            using (var request = new UnityWebRequest(endpoint + "/api/knowledge/import", "POST"))
            {
                importRequest = request;
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json"); Authorize(request); request.timeout = 90;
                yield return request.SendWebRequest();
                ImportStatus = "导入失败：" + RequestError(request);
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var result = JsonUtility.FromJson<ImportResult>(request.downloadHandler.text);
                        ImportStatus = result != null && result.chunks > 0 ?
                            "入库成功：" + result.source + " / " + result.chunks + " 个片段" : "导入失败：服务返回结果无效";
                    }
                    catch (ArgumentException) { ImportStatus = "导入失败：服务返回结果无效"; }
                }
                importRequest = null;
            }
            ImportBusy = false; Updated?.Invoke(); RefreshDocuments();
        }
        public void RefreshDocuments()
        {
            if (!listing) StartCoroutine(ListDocuments());
        }
        IEnumerator ListDocuments()
        {
            listing = true;
            using (var request = UnityWebRequest.Get(endpoint + "/api/knowledge"))
            {
                listRequest = request; request.timeout = 10; Authorize(request);
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    try
                    {
                        var result = JsonUtility.FromJson<DocumentList>(request.downloadHandler.text);
                        Documents.Clear();
                        if (result?.documents != null) Documents.AddRange(result.documents);
                    }
                    catch (ArgumentException) { ImportStatus = "资料列表返回格式无效"; }
                }
                else ImportStatus = "资料列表不可用：" + RequestError(request);
                listRequest = null;
            }
            listing = false; Updated?.Invoke();
        }

        void OnDisable()
        {
            if (activeRequest != null) activeRequest.Abort();
            if (importRequest != null) importRequest.Abort();
            if (listRequest != null) listRequest.Abort();
            activeRequest = null;
            importRequest = listRequest = null;
            StopAllCoroutines();
            if (Busy && Conversation.Count > 0 && Conversation[Conversation.Count - 1].role == "user")
                Conversation.RemoveAt(Conversation.Count - 1);
            Busy = false;
            ImportBusy = listing = false;
        }
    }
}
