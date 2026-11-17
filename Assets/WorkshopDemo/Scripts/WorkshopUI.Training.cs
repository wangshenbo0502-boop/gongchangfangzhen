using System;
using UnityEngine;
using UnityEngine.UI;

namespace WorkshopDemo
{
    public sealed partial class WorkshopUI
    {
        Dropdown trainingModeMenu, injectionMenu;
        GameObject trainingDialog;
        static string TrainingFaultTitle(WorkshopAlarm a) =>
            a.training?.Mode == WorkshopTrainingMode.Exam && !a.resolved ? "独立考核 · 待诊断" : a.fault.Name;

        void OpenTrainingRecord()
        {
            var a = editingOrder;
            if (a?.training == null) { simulation.Notify("普通工单直接填写故障原因；新建课题可选择引导实训或独立考核"); return; }
            if (trainingDialog) Destroy(trainingDialog);
            var overlay = Box(transform, "Training evidence", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Color(0, 0, 0, .9f));
            trainingDialog = overlay.gameObject;
            var root = Box(overlay, "Evidence panel", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(-400, -312), new Vector2(400, 312), panel);
            var t = a.training;
            var spec = WorkshopFaultCase.Find(a.fault.Code);
            Label(root, a.id + " · " + TrainingFaultTitle(a), 20, ink, 24, 12, 650, 36);
            Command(root, "关闭", 684, 14, 90, () => trainingDialog.SetActive(false));
            Label(root, a.fault.Symptom, 16, ink, 24, 62, 750, 72);
            bool guided = t.Mode == WorkshopTrainingMode.Guided;
            Label(root, guided ? spec.Evidence : "现场观察与独立指标对照", 15, muted, 24, 134, 750, 46);
            var location = AnalysisToggle(root, "部位观察", 24, 188, (t.EvidenceMask & 1) != 0, _ => { });
            var telemetry = AnalysisToggle(root, "指标对照", 154, 188, (t.EvidenceMask & 2) != 0, _ => { });
            var modes = WorkshopFaultCatalog.For(a.device.kind);
            var diagnoses = new string[modes.Length + 1];
            var repairs = new string[modes.Length + 1];
            diagnoses[0] = "选择诊断"; repairs[0] = "选择已完成的维修检查";
            for (int i = 0; i < modes.Length; i++)
            {
                diagnoses[i + 1] = modes[i].Name;
                repairs[i + 1] = WorkshopFaultCase.Find(modes[i].Code).Repair;
            }
            var diagnosis = Menu(root, "Diagnosis", 24, 236, 750, diagnoses);
            var repair = Menu(root, "Repair checks", 24, 290, 750, repairs);
            diagnosis.SetValueWithoutNotify(Array.FindIndex(modes, f => f.Code == t.Diagnosis) + 1);
            repair.SetValueWithoutNotify(Array.FindIndex(modes, f => f.Code == t.RepairCode) + 1);
            Label(root, guided ? spec.Retest : "复核维修后的指标、现场状态与保护条件", 15, muted, 24, 348, 750, 50);
            var retest = AnalysisToggle(root, "复验完成", 24, 404, t.Retested, _ => { });
            bool accepting = a.stage == WorkshopOrderStage.AwaitingAcceptance;
            location.interactable = telemetry.interactable = diagnosis.interactable = repair.interactable = !accepting;
            retest.interactable = accepting;
            var status = Label(root, t.Summary, 14, amber, 24, 452, 750, 68);
            Command(root, "记录检查", 24, 548, 160, () => {
                bool ok = simulation.RecordTraining(a, (location.isOn ? 1 : 0) | (telemetry.isOn ? 2 : 0),
                    diagnosis.value == 0 ? null : modes[diagnosis.value - 1].Code,
                    repair.value == 0 ? null : modes[repair.value - 1].Code, retest.isOn);
                status.text = simulation.Feedback;
                if (ok) trainingDialog.SetActive(false);
            }, true, 42);
        }
    }
}
