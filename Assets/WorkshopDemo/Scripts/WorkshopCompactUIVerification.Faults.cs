using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace WorkshopDemo
{
    public sealed partial class WorkshopCompactUIVerification
    {
        void VerifyFaultBindings()
        {
            var bindings = new Dictionary<string, string[]> {
                { "P-01", new[] { "pump.seal.faces", "pump.impeller.inlet" } },
                { "TK-01", new[] { "tank.shell.frontLeak", "tank.instruments.level" } },
                { "F-01", new[] { "fan.impeller.crack", "fan.impeller.hub" } },
                { "PLC-01", new[] { "cabinet.fieldTerminal.candidate" } }
            };
            foreach (var station in bindings)
            {
                var device = sim.devices.Single(d => d.deviceId == station.Key);
                var live = device.GetComponentInChildren<WorkshopEquipmentModel>();
                var root = new GameObject("Temporary fault binding check");
                try
                {
                    var preview = root.AddComponent<WorkshopEquipmentModel>();
                    preview.Initialize(device.kind, device.deviceId, device);
                    foreach (var id in station.Value)
                    {
                        Check(live.TryGetFaultBinding(id, out var a) && preview.TryGetFaultBinding(id, out var b) &&
                            a.Renderers.Length == b.Renderers.Length &&
                            a.Renderers.All(r => r.transform.IsChildOf(live.transform)) &&
                            b.Renderers.All(r => r.transform.IsChildOf(preview.transform)),
                            "BIND-01 " + station.Key + " " + id + " resolved independently");
                    }
                }
                finally { Destroy(root); }
            }
        }

        IEnumerator<object> VerifyTankLeakPreview(WorkshopEquipmentView view, Transform dialog,
            Transform[] parts, Vector3[] home, WorkshopEquipmentModel liveModel)
        {
            Click("故障演示", dialog);
            yield return new WaitForSecondsRealtime(.25f);
            Check(view.WaitingForAssembly && view.Disassembly > 0 && view.TargetDisassembly == 0 && view.Progress == 0,
                "MOTION-01 Fault mode returns the tank along the authored route");
            Click("时间点", dialog);
            yield return new WaitForSecondsRealtime(.25f);
            Check(!Member<GameObject>("quickMenu").GetComponentsInChildren<Button>().Any(b => b.name == "预设起裂"),
                "TK-LEAK timeline uses leak milestones");
            Click("最终状态");
            Check(view.WaitingForAssembly && view.Progress == 0, "Queued terminal state waits for assembly");
            float deadline = Time.realtimeSinceStartup + 25;
            while (view.WaitingForAssembly && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSecondsRealtime(.25f);
            Check(view.Disassembly == 0 && parts.Select((t, i) => (t.localPosition - home[i]).sqrMagnitude < 1e-12f).All(equal => equal),
                "MOTION-01 All 12 assemblies seated before applying fault preview");
            var model = view.Model;
            var presenter = model.FaultPresenter;
            Check(view.DisplayModel == model && model.gameObject.activeSelf && model.ReviewedTankModel &&
                model.AssemblyCount == 12 && !model.HasOpening && presenter != null && presenter.Effects &&
                presenter.Effects.activeInHierarchy && model.PartCount == 892 &&
                model.DamageSurface.sharedMesh == presenter.PerforatedMesh &&
                !GameObject.Find("Tank fault demonstration"), "MODEL-01 The same reviewed tank shows the local leak");
            Click("视图", dialog); yield return new WaitForSecondsRealtime(.2f);
            Click("部件与参数"); yield return new WaitForSecondsRealtime(.2f);
            Check(model.FaultFrame?.Level == 38 && !model.FaultFrame.Value.RateReadout.Value.HasValue &&
                Member<Text>("equipmentMetrics").text.Contains("漏率未求解") &&
                !Member<Text>("equipmentMetrics").text.Contains("裂缝"), "Teaching level and unavailable leak rate are identified");
            FitButtons(dialog);
            Capture("05-fault-teaching"); yield return new WaitForEndOfFrame();
            Click("视图", dialog); yield return new WaitForSecondsRealtime(.2f); Click("故障近景");
            yield return new WaitForSecondsRealtime(.25f);
            Check(view.Focused && model.TryGetFaultFocus(out var focus, out var normal) &&
                focus.size.y < model.ReviewedTankBounds().size.y * .2f && Vector3.Dot(normal, Vector3.back) > .9f,
                "Close-up is local and faces the service aisle");
            Capture("06-local-closeup"); yield return new WaitForEndOfFrame();
            model.TryGetFaultBinding("tank.shell.frontLeak", out var studioLeak);
            liveModel.TryGetFaultBinding("tank.shell.frontLeak", out var liveLeak);
            var shell = studioLeak.Surface.GetComponent<MeshFilter>();
            var originalMesh = presenter.IntactMesh;
            var originalMaterial = studioLeak.Renderers[0].sharedMaterial;
            Check(Vector3.Distance(studioLeak.Surface.InverseTransformPoint(studioLeak.Anchor),
                liveLeak.Surface.InverseTransformPoint(liveLeak.Anchor)) < .0001f && Mathf.Approximately(liveModel.WorkshopScale, .60f),
                "Nominal leak anchor agrees at workshop and studio scales");
            var slider = Member<Slider>("equipmentProgress");
            slider.value = 0; slider.value = .75f;
            var mid = EffectState(presenter);
            var midLevel = model.FaultFrame.Value.Level;
            Capture("07-scrub-75"); yield return new WaitForEndOfFrame();
            slider.value = .25f;
            Capture("08-scrub-25"); yield return new WaitForEndOfFrame();
            slider.value = 1; slider.value = 0; slider.value = .75f;
            Check(EffectState(presenter).SequenceEqual(mid) && model.FaultFrame.Value.Level == midLevel,
                "SCRUB-01 Reverse and repeated jumps reconstruct identical droplets and readings");
            Click("复位", dialog); Click("复位", dialog);
            Check(view.Progress == 0 && !presenter.Effects.activeSelf, "RESET-01 Two resets hide all leak effects");
            Click("观察", dialog);
            Check(view.DisplayModel == model && model.gameObject.activeSelf && !presenter.Effects.activeSelf &&
                shell.sharedMesh == originalMesh && model.TriangleCount == 245364 &&
                studioLeak.Renderers[0].sharedMaterial == originalMaterial &&
                studioLeak.Surface.gameObject.layer == 28 && !model.FaultFrame.HasValue,
                "RESET-01 Observation restores tank, mesh, material and layer");
        }

        static Vector3[] EffectState(WorkshopFaultPresenter presenter) =>
            presenter.Effects.GetComponentsInChildren<Transform>(true)
                .SelectMany(t => new[] { t.localPosition, t.localScale, t.gameObject.activeSelf ? Vector3.one : Vector3.zero }).ToArray();

        IEnumerator<object> VerifyTankOrders(WorkshopDevice tank)
        {
            var model = tank.GetComponentInChildren<WorkshopEquipmentModel>();
            var agent = sim.inspector.Agent;
            float speed = agent.speed, acceleration = agent.acceleration;
            try
            {
                agent.speed = 12;
                agent.acceleration = 50;
                foreach (bool running in new[] { true, false })
                {
                    if (tank.IsRunning != running) tank.ToggleOperation();
                    Check(sim.TriggerFault(tank, "TK-LEAK") && tank.faultMarker.activeSelf && !tank.IsRunning,
                        "TK-LEAK raises the linked marker and trips the station");
                    var alarm = sim.ActiveAlarm;
                    Check(!sim.TriggerFault(tank, "TK-LEAK") && !sim.SubmitRepair(alarm, "未到场"),
                        "LOOP-03 Duplicate injection and early repair are rejected");
                    Check(sim.StartInspection(alarm), "Actual robot dispatch");
                    if (running)
                    {
                        Click("设备操作"); yield return new WaitForSecondsRealtime(.2f);
                        Click("观察与拆装"); yield return new WaitForSecondsRealtime(.35f);
                        var dialog = Member<GameObject>("equipmentDialog").transform;
                        Click("故障演示", dialog);
                        var view = Member<WorkshopEquipmentView>("equipmentView");
                        var originalFault = tank.Fault;
                        var stage = alarm.stage;
                        float age = tank.FaultAge, level = tank.Level;
                        bool arrived = sim.inspector.HasArrived;
                        var slider = Member<Slider>("equipmentProgress");
                        slider.value = 1; slider.value = .25f;
                        Check(view.Fault.Code == "TK-LEAK" && tank.Fault == originalFault && alarm.stage == stage &&
                            tank.FaultAge == age && tank.Level == level && arrived == sim.inspector.HasArrived,
                            "PREVIEW-01 Scrubbing during an open order never writes to live state");
                        Click("返回车间", dialog);
                        yield return null;
                        Check(!view.FaultMode && !view.Playing && !view.Model.FaultPresenter.Effects.activeSelf,
                            "Closing the equipment window restores its preview");
                    }
                    do
                    {
                        float deadline = Time.realtimeSinceStartup + 25;
                        while (!sim.inspector.HasArrived && Time.realtimeSinceStartup < deadline)
                        {
                            CheckRobotOutsideTank(tank.GetComponent<BoxCollider>().bounds, sim.inspector.transform.position);
                            yield return null;
                        }
                        Check(sim.inspector.HasArrived && alarm.stage == WorkshopOrderStage.Repairing,
                            "Robot reaches the actual tank approach point");
                        if (running && alarm.reworkCount == 0)
                        {
                            sim.inspector.SetManualControl(true);
                            Vector3 retreat = Vector3.ProjectOnPlane(tank.inspectionPoint - tank.transform.position, Vector3.up).normalized;
                            for (int i = 0; i < 12 && sim.inspector.HasArrived; i++)
                            {
                                agent.Move(retreat * .15f);
                                yield return null;
                            }
                            Check(!sim.inspector.HasArrived && alarm.stage == WorkshopOrderStage.Dispatched &&
                                !sim.SubmitRepair(alarm, "已经离场"), "LOOP-03 Leaving site revokes repair permission");
                            sim.inspector.SetManualControl(false);
                            deadline = Time.realtimeSinceStartup + 25;
                            while (!sim.inspector.HasArrived && Time.realtimeSinceStartup < deadline) yield return null;
                            Check(sim.inspector.HasArrived && alarm.stage == WorkshopOrderStage.Repairing,
                                "Returning to the inspection point restores repair permission");
                        }
                        Check(model.FaultPresenter.Effects && model.FaultPresenter.Effects.activeSelf &&
                            model.FaultFrame?.Level == tank.Level &&
                            !tank.transform.Find("Fault effects - schematic").gameObject.activeSelf,
                            "Live presentation uses the tank's sampled level and replaces the schematic");
                        Capture(running ? "09-live-running-order" : "10-live-stopped-order");
                        yield return new WaitForEndOfFrame();
                        Check(!sim.SubmitRepair(alarm, "") && sim.SubmitRepair(alarm, "实训：局部壁面漏损") &&
                            alarm.stage == WorkshopOrderStage.AwaitingAcceptance && tank.Fault != null &&
                            !sim.SubmitRepair(alarm, "重复提交"), "Repair awaits acceptance and rejects duplicate submission");
                        if (!running || alarm.reworkCount > 0) break;
                        Check(sim.Accept(alarm, false) && alarm.reworkCount == 1 && tank.Fault != null &&
                            sim.StartInspection(alarm), "LOOP-03 Rejection preserves fault and allows redispatch");
                    } while (true);
                    Check(sim.Accept(alarm, true) && tank.Fault == null && tank.IsRunning == running &&
                        sim.Records.Count(r => r.alarmId == alarm.id) == 1 && !sim.Accept(alarm, true),
                        running ? "LOOP-01 Acceptance restores running condition once" : "LOOP-02 Acceptance preserves prior stop");
                    yield return new WaitForSecondsRealtime(.3f);
                    Check(!model.FaultPresenter.Effects.activeSelf && !model.FaultFrame.HasValue, "Live leak presentation clears after acceptance");
                }
                sim.ResetDemo(); sim.ResetDemo();
                yield return null;
                Check(sim.Alarms.Count == 0 && sim.Records.Count == 0 && tank.Fault == null &&
                    !model.FaultPresenter.Effects.activeSelf, "RESET-01 Two business resets leave no leakage or orders");
            }
            finally
            {
                agent.speed = speed;
                agent.acceleration = acceleration;
            }
        }
    }
}
