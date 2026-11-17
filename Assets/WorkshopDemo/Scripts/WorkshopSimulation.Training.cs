using System;
using System.Collections.Generic;

namespace WorkshopDemo
{
    public enum WorkshopTrainingMode { Normal, Guided, Exam }

    [Serializable]
    public sealed class WorkshopTrainingState
    {
        public WorkshopTrainingMode Mode;
        public int EvidenceMask, Mistakes;
        public string Diagnosis, RepairCode;
        public bool Retested;
        public readonly List<string> History = new List<string>();
        public string Summary => (Mode == WorkshopTrainingMode.Exam ? "独立考核" : "引导实训") +
            " · 证据 " + (EvidenceMask == 3 ? "齐全" : "待补") +
            " · 诊断 " + (string.IsNullOrEmpty(Diagnosis) ? "未记录" : "已记录") +
            " · 维修 " + (string.IsNullOrEmpty(RepairCode) ? "待检查" : "已检查") +
            " · 复验 " + (Retested ? "已记录" : "待复验") + " · 无效操作 " + Mistakes;
        internal void Rework()
        {
            History.Add("验收驳回，保留证据和诊断历史");
            RepairCode = null;
            Retested = false;
        }
    }

    public sealed partial class WorkshopSimulation
    {
        public bool IsExam(WorkshopDevice device) => Alarms.Exists(a =>
            !a.resolved && a.device == device && a.training?.Mode == WorkshopTrainingMode.Exam);

        public bool TriggerTrainingFault(WorkshopDevice device, string code, WorkshopTrainingMode mode, bool progressive)
        {
            if (!InjectFault(device, code, progressive ? WorkshopFaultInjection.Progressive : WorkshopFaultInjection.Immediate, mode))
                return false;
            if (mode != WorkshopTrainingMode.Normal)
            {
                ActiveAlarm.training.History.Add("课题建立");
                if (mode == WorkshopTrainingMode.Exam) Notify(device.deviceId + " 独立考核已建立，请根据现场证据判断");
            }
            return true;
        }

        public bool RecordTraining(WorkshopAlarm alarm, int evidenceMask, string diagnosis, string repairCode, bool retested)
        {
            if (alarm == null || !Alarms.Contains(alarm) || alarm.training == null || alarm.resolved)
            { Notify("当前工单不需要实训记录"); return false; }
            bool arrived = inspector && inspector.TargetAlarm == alarm && inspector.HasArrived &&
                alarm.stage == WorkshopOrderStage.Repairing;
            if (!arrived && alarm.stage != WorkshopOrderStage.AwaitingAcceptance)
            { Notify("到达现场后才能记录证据和维修检查"); return false; }
            var t = alarm.training;
            if (arrived)
            {
                t.EvidenceMask = evidenceMask & 3;
                t.Diagnosis = diagnosis;
                t.RepairCode = repairCode;
                t.Retested = false;
                t.History.Add("现场记录：证据=" + t.EvidenceMask + "，诊断=" + diagnosis + "，维修=" + repairCode);
            }
            else
            {
                t.Retested = retested;
                t.History.Add(retested ? "维修后复验检查已记录" : "复验待完成");
            }
            Notify(t.Summary);
            return true;
        }

        bool TrainingFailure(WorkshopAlarm alarm, string message)
        {
            alarm.training.Mistakes++;
            alarm.training.History.Add(message);
            Notify(message);
            return false;
        }

        bool ValidateTrainingRepair(WorkshopAlarm alarm)
        {
            var t = alarm.training;
            if (t == null) return true;
            if (t.EvidenceMask != 3) return TrainingFailure(alarm, "请先记录部位观察与指标对照两项证据");
            if (t.Diagnosis != alarm.fault.Code) return TrainingFailure(alarm, "诊断与现场证据不一致，请重新排查");
            if (t.RepairCode != alarm.fault.Code) return TrainingFailure(alarm, "请完成与诊断相符的维修检查");
            t.History.Add("维修提交通过");
            return true;
        }

        bool ValidateTrainingRetest(WorkshopAlarm alarm)
        {
            if (alarm.training == null || alarm.training.Retested) return true;
            return TrainingFailure(alarm, "请在诊断记录中完成维修后复验，再执行验收");
        }
    }
}
