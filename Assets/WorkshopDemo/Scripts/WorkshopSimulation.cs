using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorkshopDemo
{
    [Serializable]
    public sealed class WorkshopAlarm
    {
        public string id;
        public WorkshopDevice device;
        public DateTime occurredAt;
        public string progress = "未派发";
        public bool resolved;
    }

    [Serializable]
    public sealed class InspectionRecord
    {
        public string alarmId;
        public string deviceName;
        public DateTime completedAt;
        public string action;
    }

    public sealed class WorkshopSimulation : MonoBehaviour
    {
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
            get { int n = 0; foreach (var d in devices) if (d.IsRunning) n++; return n; }
        }
        public int OpenAlarmCount
        {
            get { int n = 0; foreach (var a in Alarms) if (!a.resolved) n++; return n; }
        }
        public WorkshopAlarm ActiveAlarm => inspector && inspector.IsBusy ? inspector.TargetAlarm :
            Alarms.Find(a => !a.resolved && a.device == Selected) ?? Alarms.FindLast(a => !a.resolved);
        public bool CanFaultSelected => Selected && Selected.Status != DeviceStatus.Fault;
        public bool CanDispatch => ActiveAlarm != null && !inspector.IsBusy && ActiveAlarm.progress == "未派发";
        public bool CanResolve => ActiveAlarm != null && inspector.HasArrived &&
            inspector.TargetAlarm == ActiveAlarm;

        void Start()
        {
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
            if (!CanFaultSelected) { Notify("该设备已有未处理故障"); return; }
            var d = Selected;
            d.SetFault(true);
            Alarms.Insert(0, new WorkshopAlarm {
                id = "ALM-" + (++alarmSequence).ToString("000"),
                device = d, occurredAt = SimulationTime
            });
            Notify(d.deviceId + " " + d.FaultText + "，设备已保护停机");
        }

        public void Dispatch()
        {
            if (!CanDispatch) { Notify("当前没有可派发的任务"); return; }
            var alarm = ActiveAlarm;
            if (!inspector.Dispatch(alarm)) { Notify("目标不可达，未派发巡检"); return; }
            alarm.progress = "巡检途中";
            Select(alarm.device, false);
            Notify("巡检员已出发");
        }

        public void InspectorArrived()
        {
            if (inspector.TargetAlarm == null) return;
            inspector.TargetAlarm.progress = "待处理";
            Notify("巡检员已到达，等待确认处理");
        }

        public void Resolve()
        {
            if (!CanResolve) { Notify("巡检员到达故障设备后才能处理"); return; }
            var alarm = ActiveAlarm;
            alarm.resolved = true;
            alarm.progress = "已处理";
            alarm.device.SetFault(false);
            Records.Insert(0, new InspectionRecord {
                alarmId = alarm.id, deviceName = alarm.device.deviceId + " " + alarm.device.displayName,
                completedAt = SimulationTime, action = alarm.device.RepairText
            });
            inspector.Complete();
            Notify("故障已解除，巡检记录已生成");
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
