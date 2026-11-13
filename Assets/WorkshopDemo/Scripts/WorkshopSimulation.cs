using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorkshopDemo
{
    public enum WorkshopOrderStage { Detected, Reported, Dispatched, Repairing, AwaitingAcceptance, Closed }

    [Serializable]
    public sealed class WorkshopAlarm
    {
        public string id;
        public WorkshopDevice device;
        public DateTime occurredAt;
        public string progress = "待报修";
        public bool resolved;
        public WorkshopFault fault;
        public WorkshopOrderStage stage;
        public string reporter, department, description, assignee, cause, repairAction, materials, acceptanceNote;
        public DateTime reportedAt, dueAt, submittedAt, closedAt;
        public int reworkCount;
        public string StageText => new[] { "待报修", "待派工", "巡检途中", "待维修", "待验收", "已关闭" }[(int)stage];
    }

    [Serializable]
    public sealed class InspectionRecord
    {
        public string alarmId;
        public string deviceName;
        public DateTime completedAt;
        public string action;
        public string faultCode;
        public string faultName;
        public string resultSource;
        public string reporter, assignee, cause, materials, acceptanceNote;
        public double elapsedMinutes;
        public int reworkCount;
    }

    public sealed class WorkshopSimulation : MonoBehaviour
    {
        public const int DefaultInspectionMinutes = 30;
        public WorkshopDevice[] devices;
        public WorkshopCamera cameraRig;
        public WorkshopInspector inspector;
        public GameObject roof;
        public WorkshopDevice Selected { get; private set; }
        public List<WorkshopAlarm> Alarms { get; } = new List<WorkshopAlarm>();
        public List<InspectionRecord> Records { get; } = new List<InspectionRecord>();
        public DateTime SimulationTime => startedAt.AddSeconds(elapsed);
        public float Elapsed => elapsed;
        public string Feedback { get; private set; } = "车间运行正常";
        public event Action Changed;
        public event Action Sampled;
        DateTime startedAt;
        float elapsed;
        float sampleTimer;
        int alarmSequence;

        public int RunningCount
        {
            get { int n = 0; if (devices != null) foreach (var d in devices) if (d && d.IsRunning) n++; return n; }
        }
        public int OpenAlarmCount
        {
            get { int n = 0; foreach (var a in Alarms) if (!a.resolved) n++; return n; }
        }
        public WorkshopAlarm ActiveAlarm => Alarms.Find(a => !a.resolved && a.device == Selected);
        public bool CanDispatch => ActiveAlarm != null && inspector && !inspector.IsBusy &&
            ActiveAlarm.stage == WorkshopOrderStage.Reported;
        public bool CanResolve => ActiveAlarm != null && ActiveAlarm.stage == WorkshopOrderStage.Repairing &&
            inspector && inspector.HasArrived && inspector.TargetAlarm == ActiveAlarm;
        public int PendingAcceptanceCount => Alarms.FindAll(a => a.stage == WorkshopOrderStage.AwaitingAcceptance).Count;
        public int OverdueCount => Alarms.FindAll(a => !a.resolved && a.stage != WorkshopOrderStage.Detected &&
            a.dueAt != default && SimulationTime > a.dueAt).Count;
        public double AverageClosureMinutes
        {
            get { double total = 0; foreach (var record in Records) total += record.elapsedMinutes;
                return Records.Count == 0 ? 0 : total / Records.Count; }
        }

        void Start()
        {
            if (devices == null || devices.Length == 0 || Array.Exists(devices, d => !d) ||
                !inspector || !cameraRig)
            {
                Debug.LogError("Workshop simulation requires devices, inspector and camera.", this);
                enabled = false;
                return;
            }
            startedAt = DateTime.Now;
            inspector.simulation = this;
            cameraRig.simulation = this;
            Select(devices[0], false);
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            sampleTimer += Time.deltaTime;
            if (sampleTimer < .25f) return;
            sampleTimer = 0;
            foreach (var d in devices) d.Sample(elapsed);
            Sampled?.Invoke();
        }

        public void Select(WorkshopDevice device, bool focus = true)
        {
            if (!device) return;
            if (Selected) Selected.SetSelected(false);
            Selected = device;
            device.SetSelected(true);
            if (focus) cameraRig.Focus(device);
            Changed?.Invoke();
        }

        public void TriggerFault()
        {
            var d = Array.Find(devices, x => x.kind == DeviceKind.Pump);
            if (!d) return;
            TriggerFault(d, "P-HOT");
        }

        public bool TriggerFault(WorkshopDevice d, string code)
        {
            var fault = WorkshopFaultCatalog.Find(code);
            if (!d || devices == null || Array.IndexOf(devices, d) < 0 || fault == null || fault.Kind != d.kind)
            { Notify("故障与设备类型不匹配"); return false; }
            if (d.Fault != null || Alarms.Exists(a => !a.resolved && a.device == d))
            { Notify(d.deviceId + " 已有活动故障，请先完成处理"); return false; }
            if (!d.ApplyFault(fault)) return false;
            Alarms.Insert(0, new WorkshopAlarm {
                id = "ALM-" + (++alarmSequence).ToString("000"),
                device = d, occurredAt = SimulationTime, fault = fault
            });
            Select(d);
            Notify(d.deviceId + " " + fault.Name + " · " + fault.SeverityText +
                (fault.Trips ? " · 联锁停机" : " · 异常运行") + "（教学注入）");
            return true;
        }

        public bool Report(WorkshopAlarm alarm, string reporter, string department, string description)
        {
            if (alarm == null || !Alarms.Contains(alarm) || alarm.stage != WorkshopOrderStage.Detected)
            { Notify("请先选择一条待报修告警"); return false; }
            if (string.IsNullOrWhiteSpace(reporter) || string.IsNullOrWhiteSpace(department) ||
                string.IsNullOrWhiteSpace(description))
            { Notify("请填写报修人、部门和故障现象"); return false; }
            alarm.reporter = reporter.Trim(); alarm.department = department.Trim();
            alarm.description = description.Trim(); alarm.reportedAt = SimulationTime;
            SetStage(alarm, WorkshopOrderStage.Reported);
            Notify(alarm.id + " 已报修，等待主管派工");
            return true;
        }

        static void SetStage(WorkshopAlarm alarm, WorkshopOrderStage stage)
        {
            alarm.stage = stage;
            alarm.progress = alarm.StageText;
        }

        public void Dispatch() { Assign(ActiveAlarm, "实训维修员", 30); }

        public bool StartInspection(WorkshopAlarm alarm) =>
            DispatchOrder(alarm, null, DefaultInspectionMinutes, true);

        public bool Assign(WorkshopAlarm alarm, string assignee, int minutes)
        {
            if (string.IsNullOrWhiteSpace(assignee) || minutes < 1 || minutes > 240)
            { Notify("请填写维修人员及 1–240 分钟处理时限"); return false; }
            return DispatchOrder(alarm, assignee.Trim(), minutes, false);
        }

        bool DispatchOrder(WorkshopAlarm alarm, string assignee, int minutes, bool allowDetected)
        {
            if (alarm == null || !Alarms.Contains(alarm) ||
                (alarm.stage != WorkshopOrderStage.Reported && !(allowDetected && alarm.stage == WorkshopOrderStage.Detected)))
            { Notify("请选择一张等待巡检的工单"); return false; }
            if (!inspector || inspector.IsBusy)
            { Notify("小车正在执行其他任务或不可用"); return false; }
            if (!inspector.Dispatch(alarm)) { Notify("目标不可达，未派发巡检"); return false; }
            if (alarm.reportedAt == default) alarm.reportedAt = SimulationTime;
            if (assignee != null) alarm.assignee = assignee;
            alarm.dueAt = SimulationTime.AddMinutes(minutes);
            SetStage(alarm, WorkshopOrderStage.Dispatched);
            Select(alarm.device, false);
            Notify(alarm.device.displayName + " · 小车正在前往现场");
            return true;
        }

        public void InspectorArrived()
        {
            var alarm = inspector.TargetAlarm;
            if (alarm == null || alarm.stage != WorkshopOrderStage.Dispatched) return;
            SetStage(alarm, WorkshopOrderStage.Repairing);
            Notify(alarm.device.displayName + " · 小车已到达，请填写故障原因");
        }

        public void InspectorDeparted()
        {
            var alarm = inspector.TargetAlarm;
            if (alarm == null || alarm.stage != WorkshopOrderStage.Repairing) return;
            SetStage(alarm, WorkshopOrderStage.Dispatched);
            Notify(alarm.id + " 已离开巡检点，返回现场后才能提交维修");
        }

        public void Resolve()
        {
            Notify("填写故障原因并提交维修，再验收归档");
        }

        public bool SubmitRepair(WorkshopAlarm alarm, string cause, string action = null, string materials = null)
        {
            if (alarm == null || !Alarms.Contains(alarm) || alarm.stage != WorkshopOrderStage.Repairing ||
                !inspector || inspector.TargetAlarm != alarm || !inspector.HasArrived)
            { Notify("巡检机器人实际到达后才能提交维修"); return false; }
            if (string.IsNullOrWhiteSpace(cause))
            { Notify("请填写故障原因"); return false; }
            alarm.cause = cause.Trim();
            alarm.repairAction = action?.Trim();
            alarm.materials = materials?.Trim();
            alarm.submittedAt = SimulationTime;
            SetStage(alarm, WorkshopOrderStage.AwaitingAcceptance);
            inspector.Complete();
            Notify(alarm.device.displayName + " · 维修已提交，等待验收");
            return true;
        }

        public bool Accept(WorkshopAlarm alarm, bool passed, string note = null)
        {
            if (alarm == null || !Alarms.Contains(alarm) || alarm.stage != WorkshopOrderStage.AwaitingAcceptance)
            { Notify("仅待验收工单可执行验收"); return false; }
            alarm.acceptanceNote = note?.Trim();
            if (!passed)
            {
                alarm.reworkCount++;
                SetStage(alarm, WorkshopOrderStage.Reported);
                Notify(alarm.device.displayName + " · 已退回，等待重新巡检");
                return true;
            }
            alarm.resolved = true;
            alarm.closedAt = SimulationTime;
            SetStage(alarm, WorkshopOrderStage.Closed);
            string resultSource = alarm.device.ResultSource;
            alarm.device.SetFault(false);
            Records.Insert(0, new InspectionRecord {
                alarmId = alarm.id, deviceName = alarm.device.deviceId + " " + alarm.device.displayName,
                completedAt = SimulationTime, action =
                    (string.IsNullOrWhiteSpace(alarm.repairAction) ? "" : alarm.repairAction + "；") +
                    (alarm.device.IsRunning ? "恢复运行" : "保持原停机状态"),
                faultCode = alarm.fault.Code, faultName = alarm.fault.Name, resultSource = resultSource,
                reporter = alarm.reporter, assignee = alarm.assignee, cause = alarm.cause,
                materials = alarm.materials, acceptanceNote = alarm.acceptanceNote,
                elapsedMinutes = (alarm.closedAt - alarm.reportedAt).TotalMinutes, reworkCount = alarm.reworkCount
            });
            Notify(alarm.id + " 使用方验收通过，设备恢复并归档");
            return true;
        }

        public void ToggleSelected()
        {
            if (!Selected || Selected.Status == DeviceStatus.Fault) return;
            Selected.ToggleOperation();
            Notify(Selected.deviceId + (Selected.IsRunning ? " 已启动" : " 已停机"));
        }

        public void ResetDemo()
        {
            inspector.ResetInspector();
            Alarms.Clear();
            Records.Clear();
            alarmSequence = 0;
            elapsed = sampleTimer = 0;
            startedAt = DateTime.Now;
            foreach (var d in devices) d.ResetState();
            Select(devices[0], false);
            cameraRig.Overview();
            if (roof) roof.SetActive(false);
            Notify("演示已重置");
        }

        public void Notify(string message)
        {
            Feedback = message;
            Changed?.Invoke();
        }
    }
}
