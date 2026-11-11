using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static WorkshopDemo.Editor.WorkshopTwinValidation;

namespace WorkshopDemo.Editor
{
    public static class WorkshopFaultValidation
    {
        public static IEnumerable<double> Verify(WorkshopUI ui, WorkshopSimulation simulation, int width)
        {
            SetResolution(width, width * 9 / 16);
            double until = EditorApplication.timeSinceStartup + 20;
            while (Screen.width != width && EditorApplication.timeSinceStartup < until) yield return .1;
            yield return 1;
            Check(Screen.width == width && Screen.height == width * 9 / 16, "Fault suite resolution " + width);
            Check(WorkshopFaultCatalog.All.Length == 24 &&
                WorkshopFaultCatalog.All.Select(f => f.Code).Distinct().Count() == 24, "24 unique fault mechanisms");
            simulation.ResetDemo();
            ValidateAbaqusContract();
            Click("演示故障");
            yield return .2;
            foreach (var delay in SelectOption(ui, "Fault device", "P-02")) yield return delay;
            foreach (var delay in SelectOption(ui, "Fault mode", "P-CAV")) yield return delay;
            Click("注入故障");
            Check(simulation.Selected.deviceId == "P-02" && simulation.Selected.Fault.Code == "P-CAV",
                "Menus and injection select P-02 cavitation");
            Check(simulation.Selected.IsRunning && simulation.RunningCount == 8, "Warning keeps equipment running");
            Click("故障工况");
            Canvas.ForceUpdateCanvases();
            foreach (var text in ui.GetComponentsInChildren<Text>())
            {
                if (text.text.Length == 0 || text.transform.parent.name == "Question input") continue;
                Check(text.preferredHeight <= text.rectTransform.rect.height + 3, "Fault UI text fits " + text.name);
            }
            var detail = ui.transform.Find("Fault scenarios/Scenario panel/Fault detail scroll/Rows").GetComponentInChildren<Text>();
            Check(Mathf.Abs(detail.rectTransform.offsetMin.x) < 1 && Mathf.Abs(detail.rectTransform.offsetMax.y) < 1,
                "Fault detail content starts inside scroll viewport");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Screenshot("fault-dialog", width)));
            yield return .4;
            Click("关闭");
            simulation.ResetDemo();
            simulation.inspector.Agent.speed = 18;
            simulation.inspector.Agent.acceleration = 80;
            simulation.inspector.Agent.angularSpeed = 720;
            int completed = 0;
            var captured = new HashSet<FaultVisual>();
            foreach (var device in simulation.devices)
            {
                var modes = WorkshopFaultCatalog.For(device.kind);
                Check(modes.Length == 4, device.deviceId + " has four compatible faults");
                foreach (var mode in modes)
                {
                    simulation.Select(device);
                    Check(simulation.TriggerFault(device, mode.Code), "Inject " + device.deviceId + "/" + mode.Code);
                    Check(!simulation.TriggerFault(device, mode.Code) && simulation.OpenAlarmCount == 1,
                        "Duplicate protection " + mode.Code);
                    Check(device.Status == DeviceStatus.Fault && device.IsRunning == !mode.Trips &&
                        simulation.RunningCount == (mode.Trips ? 7 : 8), "Operating policy " + mode.Code);
                    Check(device.AbaqusResult == null && device.ResultSource.Contains("无 Abaqus"),
                        "No fabricated Abaqus solution " + mode.Code);
                    simulation.Resolve();
                    Check(simulation.Records.Count == completed, "Early resolve blocked " + mode.Code);
                    yield return .3;
                    var effect = device.transform.Find("Fault effects - schematic");
                    Check(effect && effect.gameObject.activeSelf && effect.GetComponentsInChildren<LineRenderer>().Length == 5,
                        "Schematic visual active " + mode.Code);
                    if (device.HasRpm)
                        Check(mode.Trips ? device.Rpm == 0 && device.Vibration == 0 :
                            device.Rpm > 0 && device.Vibration > 0, "Motion and measurement " + mode.Code);
                    if (mode.Code == "P-CAV") Check(device.Pressure < .2f && device.Flow < 12, "Cavitation process values");
                    if (mode.Code == "PLC-COMM") Check(device.Communication == 0 && device.Voltage > 23, "Communication fault values");
                    if (mode.Code == "PLC-LOW") Check(device.Voltage < 18, "Undervoltage values");
                    if (mode.Code == "TK-LEVEL") Check(device.Level > 95, "High-level values");
                    if (captured.Add(mode.Visual))
                    {
                        simulation.cameraRig.Focus(device);
                        yield return 1;
                        ScreenCapture.CaptureScreenshot(Path.GetFullPath(
                            Screenshot("fault-" + mode.Visual, width)));
                        yield return .3;
                    }
                    simulation.Dispatch();
                    simulation.Dispatch();
                    Check(simulation.inspector.IsBusy && simulation.inspector.TargetAlarm.device == device,
                        "Single dispatch " + mode.Code);
                    foreach (var delay in Arrival(simulation)) yield return delay;
                    var alarm = simulation.ActiveAlarm;
                    simulation.Resolve();
                    simulation.Resolve();
                    completed++;
                    Check(simulation.Records.Count == completed && simulation.OpenAlarmCount == 0 &&
                        device.IsRunning && device.Fault == null && alarm.resolved &&
                        simulation.Records[0].faultCode == mode.Code && simulation.Records[0].action.Contains(mode.Action),
                        "Full navigation/resolve/typed record " + device.deviceId + "/" + mode.Code);
                    yield return .1;
                    Check(!effect.gameObject.activeSelf, "Effect cleared " + mode.Code);
                }
            }
            Check(completed == 32, "All 32 device/fault combinations completed");
            var pump = simulation.devices.First(d => d.kind == DeviceKind.Pump);
            Check(!simulation.TriggerFault(pump, "M-OVER") && simulation.OpenAlarmCount == 0,
                "Cross-kind fault rejected");
            pump.ToggleOperation();
            simulation.TriggerFault(pump, "P-CAV");
            Check(!pump.IsRunning && pump.Rpm == 0, "Warning preserves manual stop");
            simulation.Dispatch();
            foreach (var delay in Arrival(simulation)) yield return delay;
            simulation.Resolve();
            Check(pump.Status == DeviceStatus.Stopped, "Resolve restores prior manual stop");
            pump.ToggleOperation();
            var motor = simulation.devices.First(d => d.kind == DeviceKind.Motor);
            var tank = simulation.devices.First(d => d.kind == DeviceKind.Tank);
            simulation.TriggerFault(pump, "P-HOT");
            simulation.TriggerFault(motor, "M-BEAR");
            simulation.TriggerFault(tank, "TK-LEAK");
            Check(simulation.OpenAlarmCount == 3 && simulation.RunningCount == 6, "Three simultaneous device alarms");
            simulation.Select(pump);
            simulation.Dispatch();
            var task = simulation.inspector.TargetAlarm;
            simulation.Select(tank);
            Check(simulation.ActiveAlarm == task && !simulation.CanDispatch, "Selection cannot steal active robot task");
            foreach (var delay in Arrival(simulation)) yield return delay;
            simulation.Resolve();
            while (simulation.OpenAlarmCount > 0)
            {
                Check(simulation.CanDispatch, "Pending alarm is dispatchable");
                simulation.Dispatch();
                foreach (var delay in Arrival(simulation)) yield return delay;
                simulation.Resolve();
            }
            Check(simulation.RunningCount == 8, "Concurrent alarms all recovered");
            simulation.ResetDemo();
            simulation.ResetDemo();
            Check(simulation.RunningCount == 8 && simulation.Alarms.Count == 0 && simulation.Records.Count == 0 &&
                !simulation.inspector.IsBusy && simulation.devices.All(d => d.Fault == null && d.AbaqusResult == null),
                "Two resets clear faults, replay, queue and records");
            simulation.cameraRig.Overview();
            yield return .5;
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(Screenshot("fault-overview", width)));
            yield return .4;
            CheckPixels(Screenshot("fault-dialog", width), width);
            foreach (var visual in captured) CheckPixels(Screenshot("fault-" + visual, width), width);
        }

        static IEnumerable<double> Arrival(WorkshopSimulation simulation)
        {
            double until = EditorApplication.timeSinceStartup + 25;
            while (!simulation.inspector.HasArrived && EditorApplication.timeSinceStartup < until) yield return .1;
            Check(simulation.CanResolve, "Robot arrives via complete NavMesh path");
        }

        static IEnumerable<double> SelectOption(WorkshopUI ui, string menuName, string prefix)
        {
            var menu = ui.GetComponentsInChildren<Dropdown>().First(d => d.name == menuName);
            menu.Show();
            yield return .2;
            var item = ui.GetComponentsInChildren<Toggle>().First(t =>
                t.transform.parent.name == "Content" && t.GetComponentInChildren<Text>().text.StartsWith(prefix));
            var rect = (RectTransform)item.transform;
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == item.gameObject,
                "Dropdown option raycast " + prefix);
            ExecuteEvents.Execute(item.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            yield return .3;
            Check(menu.options[menu.value].text.StartsWith(prefix), "Dropdown selected " + prefix);
        }

        static void CheckPixels(string path, int width)
        {
            var image = new Texture2D(2, 2);
            image.LoadImage(File.ReadAllBytes(path));
            Check(image.width == width && image.height == width * 9 / 16, "Screenshot dimensions " + path);
            var colors = new HashSet<Color32>();
            for (int y = 0; y < image.height; y += 17)
                for (int x = 0; x < image.width; x += 17) colors.Add(image.GetPixel(x, y));
            Check(colors.Count > 200, "Nonblank rendered pixels " + path);
            UnityEngine.Object.DestroyImmediate(image);
        }

        static void ValidateAbaqusContract()
        {
            // In-memory protocol fixture only; never stored or presented as a solved analysis.
            var fixture = new WorkshopAbaqusResult {
                schemaVersion = 1, source = "Abaqus ODB", deviceId = "P-01", faultCode = "P-BEAR",
                job = "PROTOCOL-TEST-NOT-A-SOLUTION", step = "Transient", region = "PUMP",
                odbSha256 = new string('a', 64), exportedAt = DateTimeOffset.Now.ToString("o"),
                analysisDescription = "Protocol fixture only", timeMeaning = "physical",
                timeUnit = "s", stressUnit = "MPa", displacementUnit = "mm", temperatureUnit = "C",
                hasStress = true, hasDisplacement = true,
                frames = new[] { new WorkshopAbaqusFrame { time = 0, maxStressMPa = 10, maxDisplacementMm = .1f },
                    new WorkshopAbaqusFrame { time = 2, maxStressMPa = 30, maxDisplacementMm = .3f } }
            };
            string json = JsonUtility.ToJson(fixture);
            var parsed = WorkshopAbaqusResult.Parse(json, "P-01", "P-BEAR");
            Check(Mathf.Abs(parsed.At(1).maxStressMPa - 20) < .001f &&
                parsed.At(100).time == 2, "ODB protocol interpolation and end clamp (synthetic fixture)");
            bool rejected = false;
            try { WorkshopAbaqusResult.Parse(json, "P-02", "P-BEAR"); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "ODB device mismatch rejected");
            fixture.frames[1].time = 0;
            rejected = false;
            try { WorkshopAbaqusResult.Parse(JsonUtility.ToJson(fixture), "P-01", "P-BEAR"); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "ODB duplicate time rejected");
            fixture.frames[1].time = 2;
            fixture.stressUnit = "Pa";
            rejected = false;
            try { WorkshopAbaqusResult.Parse(JsonUtility.ToJson(fixture), "P-01", "P-BEAR"); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "ODB undeclared conversion rejected");
        }
    }
}
