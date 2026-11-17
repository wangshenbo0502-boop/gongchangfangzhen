using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WorkshopDemo
{
    public sealed partial class WorkshopCompactUIVerification
    {
        IEnumerator VerifyPumpFaults()
        {
            var input = FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (input) input.enabled = false;
            yield return new WaitForSecondsRealtime(4);
            ui = FindFirstObjectByType<WorkshopUI>();
            sim = FindFirstObjectByType<WorkshopSimulation>();
            Check(ui && sim && SceneManager.GetActiveScene().path == WorkshopSceneSwitcher.InspectionScene,
                "WorkshopDemo and its UI initialized");
            var device = sim.devices.Single(d => d.deviceId == "P-01");
            sim.Select(device, false);
            var workshop = device.GetComponentInChildren<WorkshopEquipmentModel>();
            Check(workshop.AssemblyCount == 12 && workshop.PartCount == 349 && workshop.TriangleCount == 119696 &&
                workshop.PumpFaultPresenter != null, "P-01 uses the 12 assemblies and unchanged nominal pump meshes");
            Click("设备学习"); yield return new WaitForSecondsRealtime(.25f);
            Click("Device P-01"); yield return new WaitForSecondsRealtime(.25f);
            var dialog = Member<GameObject>("equipmentDialog").transform;
            var view = Member<WorkshopEquipmentView>("equipmentView");
            Click("故障演示", dialog);
            yield return new WaitForSecondsRealtime(.25f);
            Click("水泵工况", dialog); yield return new WaitForSecondsRealtime(.2f);
            Click("P-SEAL  机械密封泄漏"); yield return new WaitForSecondsRealtime(.25f);
            var model = view.Model;
            var presenter = model.PumpFaultPresenter;
            Check(model.TryGetFaultBinding("pump.seal.chamber", out var chamber) &&
                Vector3.Distance(model.transform.InverseTransformPoint(chamber.Anchor), new Vector3(-.375f,.633f,-.031f)) < .001f,
                "Seal leak starts at the authored shaft-side outlet in pump coordinates");
            var sealFocus = chamber.FocusBounds();
            var sealContext = presenter.FocusBounds;
            var sealCloseup = presenter.CloseupBounds;
            Check((sealCloseup.center - sealFocus.center).sqrMagnitude < .000001f &&
                (sealCloseup.size - sealFocus.size).sqrMagnitude < .000001f &&
                sealContext.size.sqrMagnitude > sealCloseup.size.sqrMagnitude,
                "Seal inset frames the leak target while the main focus retains support context");
            Check(model.TryGetFaultBinding("pump.seal.faces", out var faces) &&
                faces.Renderers.All(r => r.enabled && r.transform.IsChildOf(model.transform)), "Original seal rings remain enabled");
            var ring = faces.Renderers[1].transform;
            Quaternion originalRing = ring.localRotation;
            var slider = Member<Slider>("equipmentProgress");
            slider.value = .48f;
            Hit(Member<Button>("equipmentPumpSection"));
            yield return new WaitForSecondsRealtime(.35f);
            Check(view.DisplayModel == model && view.PumpSectionOpen && !model.HasOpening &&
                !chamber.Renderers[0].enabled && faces.Renderers.All(r => r.enabled) &&
                !model.transform.GetComponentsInChildren<Transform>(true).Any(t => t.name == "Seal inspection fracture insert"),
                "MODEL-02 Seal section exposes the original pair and hides only the authored chamber");
            Check(presenter.SealEffects && presenter.SealEffects.activeInHierarchy &&
                presenter.SealEffects.transform.Find("Contact wear arc").gameObject.activeInHierarchy &&
                presenter.SealEffects.transform.Find("Seal drop 1").gameObject.activeInHierarchy,
                "Seal wear and gravity-fed drips are rendered by the profile");
            Capture("01-seal-section"); yield return new WaitForSecondsRealtime(.5f);
            slider.value = .72f;
            var sealFrame = PresenterState(presenter.SealEffects);
            var stoppedRing = ring.localRotation;
            slider.value = .95f;
            Check(Quaternion.Angle(stoppedRing, ring.localRotation) < .01f,
                "Teaching seal protection stops the shaft and moving ring");
            slider.value = 0; slider.value = .72f;
            Check(PresenterState(presenter.SealEffects).SequenceEqual(sealFrame), "Seal scrubbing is reversible");
            Hit(Member<Button>("equipmentPumpSection"));
            Check(chamber.Renderers[0].enabled && !presenter.SealEffects.transform.Find("Contact wear arc").gameObject.activeSelf,
                "Closing the seal section restores its chamber and hides contact overlay");
            yield return new WaitForSecondsRealtime(.35f);
            Capture("02-seal-exterior"); yield return new WaitForSecondsRealtime(.5f);

            Click("水泵工况", dialog); yield return new WaitForSecondsRealtime(.2f);
            Click("P-CAV  汽蚀"); yield return new WaitForSecondsRealtime(.25f);
            Check(model.TryGetFaultBinding("pump.inlet.cover", out var inlet) &&
                Vector3.Distance(model.transform.InverseTransformPoint(inlet.Anchor), new Vector3(-.573f,.660f,0)) < .001f,
                "Cavitation uses the authored impeller eye");
            Check(chamber.Renderers[0].enabled && !presenter.SealEffects.activeSelf &&
                Quaternion.Angle(originalRing, ring.localRotation) < .01f, "Switching cases restores the seal visuals");
            slider.value = .74f;
            var needle = model.TryGetFaultBinding("pump.discharge.gauge", out var gauge) ? gauge.Surface : null;
            Check(needle && !presenter.CavitationEffects && inlet.Renderers.All(r => r.enabled),
                "Intact exterior conceals the vapor bubbles");
            var gaugeRotation = needle.localRotation;
            Hit(Member<Button>("equipmentPumpSection"));
            yield return new WaitForSecondsRealtime(.4f);
            var coverBolt = model.GetComponentsInChildren<MeshRenderer>().Single(r => r.name == "PMP_05_InletCover_Bolt_01_HexHead");
            Check(view.PumpSectionOpen && inlet.Renderers.All(r => !r.enabled) &&
                presenter.CavitationEffects.activeInHierarchy && presenter.CavitationEffects.transform.Cast<Transform>()
                    .Any(t => t.gameObject.activeInHierarchy) && !model.HasOpening && !coverBolt.enabled,
                "Section exposes vapor and hides associated cover fasteners, preserving the impeller");
            var vaporFrame = PresenterState(presenter.CavitationEffects);
            float pressure = model.FaultFrame.Value.Pressure;
            slider.value = .42f; slider.value = 1; slider.value = 0; slider.value = .74f;
            Check(vaporFrame.SequenceEqual(PresenterState(presenter.CavitationEffects)) &&
                pressure == model.FaultFrame.Value.Pressure && Quaternion.Angle(gaugeRotation, needle.localRotation) < .01f,
                "Cavitation bubbles, pressure and needle reproduce the same preview frame");
            Click("视图", dialog); yield return new WaitForSecondsRealtime(.2f);
            Click("部件与参数"); yield return new WaitForSecondsRealtime(.3f);
            Check(Member<Text>("equipmentMetrics").text.Contains("独立教学预览") &&
                Member<Text>("equipmentMetrics").text.Contains("入口压力未求解"), "Cavitation UI labels preview and unavailable pressure");
            FitButtons(dialog);
            Capture("03-cavitation-section"); yield return new WaitForEndOfFrame();
            using (var soundCheck = VerifyCavitationSound(view, presenter, dialog))
                while (soundCheck.MoveNext()) yield return soundCheck.Current;
            Click("复位", dialog);
            Check(!view.PumpSectionOpen && inlet.Renderers.All(r => r.enabled) && coverBolt.enabled && !presenter.CavitationEffects.activeSelf,
                "Reset closes the section and clears vapor");
            Click("观察", dialog);
            Check(!model.FaultFrame.HasValue && chamber.Renderers[0].enabled && faces.Renderers.All(r => r.enabled) &&
                model.AssemblyCount == 12, "Returning to observation restores all assemblies");
            Click("返回目录", dialog); yield return new WaitForSecondsRealtime(.25f);
            using (var orders = VerifyPumpOrders(device))
                while (orders.MoveNext()) yield return orders.Current;
        }

        static Vector3[] PresenterState(GameObject effects) =>
            effects.GetComponentsInChildren<Transform>(true)
                .SelectMany(t => new[] { t.localPosition, t.localScale, t.gameObject.activeSelf ? Vector3.one : Vector3.zero }).ToArray();

        System.Collections.Generic.IEnumerator<object> VerifyCavitationSound(WorkshopEquipmentView view,
            WorkshopPumpFaultPresenter presenter, Transform dialog)
        {
            float volume = AudioListener.volume;
            try
            {
                AudioListener.volume = 1;
                Click("播放演化", dialog); yield return new WaitForSecondsRealtime(.25f);
                Check(view.Playing && presenter.SoundSource && presenter.SoundSource.isPlaying, "Cavitation teaching sound plays");
                AudioListener.volume = 0;
                yield return new WaitForSecondsRealtime(.1f);
                Check(!presenter.SoundSource.isPlaying, "Global mute stops the cavitation sound");
                AudioListener.volume = 1;
                yield return new WaitForSecondsRealtime(.1f);
                Check(presenter.SoundSource.isPlaying, "Restored global volume resumes cavitation audio");
                Click("播放演化", dialog); yield return new WaitForSecondsRealtime(.2f);
                Check(!view.Playing && !presenter.SoundSource.isPlaying, "Pausing stops the teaching sound");
            }
            finally
            {
                AudioListener.volume = volume;
            }
        }

        System.Collections.Generic.IEnumerator<object> VerifyPumpOrders(WorkshopDevice device)
        {
            var agent = sim.inspector.Agent;
            float speed = agent.speed, acceleration = agent.acceleration;
            var model = device.GetComponentInChildren<WorkshopEquipmentModel>();
            try
            {
                agent.speed = 12;
                agent.acceleration = 50;
                foreach (bool starting in new[] { true, false })
                    foreach (string code in new[] { "P-SEAL", "P-CAV" })
                    {
                        if (device.IsRunning != starting) device.ToggleOperation();
                        Check(sim.TriggerFault(device, code) && device.IsRunning == (starting && code == "P-CAV"),
                            code + " preserves trip versus running warning, prior running=" + starting);
                        var order = sim.ActiveAlarm;
                        Check(order != null && !sim.SubmitRepair(order, "提前提交") && sim.StartInspection(order),
                            code + " rejects premature repair and dispatches the real robot");
                        float deadline = Time.realtimeSinceStartup + 25;
                        while (!sim.inspector.HasArrived && Time.realtimeSinceStartup < deadline) yield return null;
                        Check(sim.inspector.HasArrived && order.stage == WorkshopOrderStage.Repairing &&
                            model.FaultFrame?.Code == code && !model.HasOpening &&
                            !model.PumpFaultPresenter.SectionOpen, code + " connects live presentation and arrival");
                        if (code == "P-CAV" && !starting)
                            Check(model.FaultFrame.Value.Activity == 0 && model.FaultFrame.Value.Pressure == 0,
                                "Stopped P-CAV has no sustained cavitation or delivery pressure");
                        Check(sim.SubmitRepair(order, "巡检确认 " + code) && device.Fault != null &&
                            sim.Accept(order, true) && device.Fault == null && device.IsRunning == starting,
                            code + " restores prior availability only after acceptance");
                        yield return new WaitForSecondsRealtime(.25f);
                        Check(!model.FaultFrame.HasValue &&
                            (!model.PumpFaultPresenter.SoundSource || !model.PumpFaultPresenter.SoundSource.isPlaying),
                            code + " clears fault visuals and audio after acceptance");
                    }
            }
            finally
            {
                agent.speed = speed;
                agent.acceleration = acceleration;
            }
        }
    }
}
