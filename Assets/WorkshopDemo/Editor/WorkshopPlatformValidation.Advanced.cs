using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using InputKey = UnityEngine.InputSystem.Key;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WorkshopDemo.Editor
{
    public static partial class WorkshopPlatformValidation
    {
        static T Member<T>(string name) => (T)typeof(WorkshopUI).GetField(name,
            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(ui);

        static void QueueKeys(params InputKey[] keys) =>
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));

        static IEnumerable<double> Shortcuts(bool inspection)
        {
            EventSystem.current.SetSelectedGameObject(null);
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var previous = canvases.Select(c => c.enabled).ToArray();
            Check(previous.Any(v => v), "Visible interface before H");
            AudioListener.volume = 1;
            QueueKeys(InputKey.H); yield return .3;
            Check(canvases.All(c => !c.enabled), "H hides interface" + (inspection ? " platform" : " assembly"));
            if (inspection)
                Check(sim.cameraRig.GetComponent<Camera>().rect == new Rect(0, 0, 1, 1), "H expands scene to full screen");
            yield return .3;
            Check(canvases.All(c => !c.enabled), "Holding H does not repeat");
            QueueKeys(); yield return .15;
            QueueKeys(InputKey.M); yield return .3;
            Check(AudioListener.volume == 0, "M mutes with interface hidden");
            QueueKeys(); yield return .15;
            QueueKeys(InputKey.M); yield return .3;
            Check(AudioListener.volume == 1, "M restores sound");
            QueueKeys(); yield return .15;
            QueueKeys(InputKey.H); yield return .3;
            Check(canvases.Select(c => c.enabled).SequenceEqual(previous), "H restores prior canvas states");
            if (inspection)
                Check(sim.cameraRig.GetComponent<Camera>().rect.width < .95f, "H restores dashboard viewport");
            QueueKeys(); yield return .15;
        }

        static void TexturePixels(RawImage image, string name)
        {
            var rt = image.texture as RenderTexture;
            Check(rt && rt.IsCreated(), name + " render target");
            var old = RenderTexture.active;
            RenderTexture.active = rt;
            var pixels = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); pixels.Apply();
            RenderTexture.active = old;
            var colors = new HashSet<Color32>();
            for (int y = 0; y < pixels.height; y += 13)
                for (int x = 0; x < pixels.width; x += 13) colors.Add(pixels.GetPixel(x, y));
            Check(colors.Count > 30, name + " nonblank rendered geometry (" + colors.Count + ")");
            UnityEngine.Object.DestroyImmediate(pixels);
        }

        static IEnumerable<double> Advanced(int width)
        {
            int cases = 0;
            foreach (string path in Directory.GetFiles(Application.streamingAssetsPath + "/WorkshopAnalysis", "*.json"))
            {
                var result = WorkshopAnalysisResult.Load(Path.GetFileNameWithoutExtension(path), out var error);
                Check(result != null && result.models.Length == 2 && result.sha256.Length == 64,
                    "Reference dataset " + Path.GetFileNameWithoutExtension(path) + " " + error);
                cases++;
            }
            Check(cases == 21, "All 21 reference cases load");
            sim.Select(sim.devices.First(d => d.kind == DeviceKind.Fan), false);
            var modes = WorkshopFaultCatalog.For(sim.Selected.kind);
            Member<Dropdown>("analysisMode").value = Array.FindIndex(modes, m => m.Code == "F-CRACK");
            Click("有限元分析"); yield return .8;
            var view = Member<WorkshopAnalysisView>("analysisView");
            Check(view.Result.code == "F-CRACK", "Analysis uses selected device and fault");
            TexturePixels(view.GetComponent<RawImage>(), "FEM main");
            foreach (var delay in Capture("fem", width)) yield return delay;
            Click("完好 / 基准"); Check(view.Model == 0, "Baseline model switch");
            Click("故障 / 缺陷"); Check(view.Model == 1, "Defect model switch");
            var quantity = Member<Dropdown>("analysisQuantity");
            Check(quantity.options.Count == 16, "Sixteen structural quantities");
            for (int i = 0; i < quantity.options.Count; i++)
            {
                quantity.value = i;
                Check(view.Field != 2 && view.Field != 17, "Structural quantity " + quantity.options[i].text);
            }
            quantity.value = 0;
            Member<Toggle>("analysisSection").isOn = true;
            Member<Slider>("analysisCut").value = .3f;
            Check(view.Section && Mathf.Abs(view.Cut - .3f) < .001f, "Movable section");
            Member<Toggle>("analysisSection").isOn = false;
            Member<Toggle>("analysisWire").isOn = true;
            Check(view.Wire, "Element edges");
            Member<Toggle>("analysisWire").isOn = false;
            view.ClearProbes(); Physics.SyncTransforms();
            var rect = (RectTransform)view.transform;
            for (int y = 2; y < 14 && view.ProbeCount < 3; y++)
                for (int x = 2; x < 18 && view.ProbeCount < 3; x++)
                {
                    var local = new Vector3(rect.rect.xMin + rect.rect.width * x / 20,
                        rect.rect.yMin + rect.rect.height * y / 16, 0);
                    view.OnPointerClick(new PointerEventData(EventSystem.current) {
                        position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(local))
                    });
                }
            Check(view.ProbeCount == 3, "Three actual surface probes count=" + view.ProbeCount);
            Check(view.ProbeCsv().Trim().Split('\n').Length == 4, "Three probe CSV rows");
            Click("导出测点");
            string exports = Application.persistentDataPath + "/AnalysisExports";
            Check(Directory.GetFiles(exports, "F-CRACK-*.csv").Length > 0, "Probe CSV file exported");
            Click("渐进开裂"); view.FracturePlaying = false;
            view.SetFractureProgress(.4f);
            Check(view.FractureActive && view.FractureOpeningMm == 0, "Pre-initiation closed geometry");
            view.SetFractureProgress(.8f);
            Check(view.FractureOpeningMm > 0 && view.FractureDepthMm > 0, "Progressive crack opening");
            yield return .2;
            foreach (var delay in Capture("fracture", width)) yield return delay;
            Click("渐进开裂");
            Check(!view.FractureActive, "Return to solved static field");
            Click("返回工况"); yield return .1;

            foreach (var device in sim.devices.GroupBy(d => d.kind).Select(g => g.First()))
            {
                sim.Select(device, false);
                Click("整机与演化"); yield return .5;
                var equipment = Member<WorkshopEquipmentView>("equipmentView");
                Check(equipment.Model.Kind == device.kind && equipment.Model.AssemblyCount >= 2,
                    "Whole equipment " + device.kind);
                TexturePixels(equipment.GetComponent<RawImage>(), "Equipment " + device.kind);
                equipment.SetProgress(1); yield return .1;
                Check(equipment.Progress == 1, "Equipment final progress " + device.kind);
                TexturePixels(equipment.GetComponent<RawImage>(), "Final equipment " + device.kind);
                Click("全部拆开"); yield return .8;
                Check(equipment.Disassembly > 0 && equipment.TargetDisassembly == equipment.Model.AssemblyCount,
                    "Animated disassembly " + device.kind);
                Click("重新组装");
                if (device.kind == DeviceKind.Pump)
                {
                    equipment.SetProgress(.8f);
                    foreach (var delay in Capture("equipment", width)) yield return delay;
                    Click("导出尺寸");
                    Check(Directory.GetFiles(Application.persistentDataPath + "/EquipmentExports", "*.csv").Length > 0,
                        "Equipment dimension CSV exported");
                }
                Click("返回工况"); yield return .1;
            }
            sim.Select(sim.devices.First(d => d.kind == DeviceKind.Cabinet), false);
            var cabinetModes = WorkshopFaultCatalog.For(sim.Selected.kind);
            Member<Dropdown>("analysisMode").value = Array.FindIndex(cabinetModes, f => f.Code == "PLC-HOT");
            Click("有限元分析"); yield return .5;
            view = Member<WorkshopAnalysisView>("analysisView");
            Check(view.Result.analysis == "thermal" && Member<Dropdown>("analysisQuantity").options.Count == 2,
                "Thermal temperature and heat flux quantities");
            TexturePixels(view.GetComponent<RawImage>(), "Thermal field");
            Click("返回工况"); yield return .1;
        }

        static IEnumerable<double> SceneRoundTrip()
        {
            // The preserved official sample assumes desktop input devices exist, including in batch verification.
            if (UnityEngine.InputSystem.Keyboard.current == null)
                UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            if (UnityEngine.InputSystem.Mouse.current == null)
                UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            yield return .2;
            var switcher = UnityEngine.Object.FindFirstObjectByType<WorkshopSceneSwitcher>();
            Check(switcher, "Unified scene switcher installed");
            Click("小车装配训练");
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 40;
            while ((SceneManager.GetActiveScene().path != WorkshopSceneSwitcher.AssemblyScene || switcher.IsSwitching) &&
                UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return .2;
            Check(SceneManager.GetActiveScene().path == WorkshopSceneSwitcher.AssemblyScene, "Assembly scene in same program");
            yield return 1;
            var training = UnityEngine.Object.FindFirstObjectByType<ImmersiveTraining.Management.StateManager_Training>();
            Check(training && training.GetTrainingStates().Count == 15, "Original fifteen training steps retained");
            foreach (var delay in Shortcuts(false)) yield return delay;
            foreach (var delay in Capture("assembly", Screen.width)) yield return delay;
            switcher.SwitchTo(WorkshopSceneSwitcher.InspectionScene);
            deadline = UnityEditor.EditorApplication.timeSinceStartup + 40;
            while ((SceneManager.GetActiveScene().path != WorkshopSceneSwitcher.InspectionScene || switcher.IsSwitching) &&
                UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return .2;
            yield return 1;
            sim = UnityEngine.Object.FindFirstObjectByType<WorkshopSimulation>();
            ui = UnityEngine.Object.FindFirstObjectByType<WorkshopUI>();
            Check(sim && ui && sim.devices.Length == 4, "Return from assembly to complete platform");
        }

        static IEnumerable<double> ManualAndVisibility()
        {
            sim.ResetDemo();
            Click("手动接管");
            var robot = sim.inspector;
            Vector3 start = robot.transform.position;
            QueueKeys(InputKey.UpArrow); yield return .3;
            QueueKeys(); yield return .2;
            Check(Vector3.Distance(start, robot.transform.position) > .15f, "Up arrow actually drives robot");
            Vector3 forward = robot.transform.position;
            QueueKeys(InputKey.DownArrow); yield return .25;
            QueueKeys(); yield return .2;
            Check(Vector3.Distance(forward, robot.transform.position) > .15f &&
                Vector3.Distance(start, robot.transform.position) < Vector3.Distance(start, forward),
                "Down arrow reverses robot");
            var heading = robot.transform.rotation;
            QueueKeys(InputKey.RightArrow); yield return .3;
            QueueKeys(); yield return .2;
            Check(Quaternion.Angle(heading, robot.transform.rotation) > 5, "Right arrow steers robot");
            var turned = robot.transform.rotation;
            QueueKeys(InputKey.LeftArrow); yield return .25;
            QueueKeys(); yield return .2;
            Check(Quaternion.Angle(turned, robot.transform.rotation) > 5 &&
                Quaternion.Angle(heading, robot.transform.rotation) < Quaternion.Angle(heading, turned),
                "Left arrow counter-steers robot");
            sim.ResetDemo();
            var pump = sim.devices.First(d => d.kind == DeviceKind.Pump);
            sim.TriggerFault(pump, "P-CAV"); var order = sim.ActiveAlarm;
            sim.Report(order, "实训员", "车间", "振动异常");
            sim.Assign(order, "维修员", 30);
            foreach (var delay in Arrival()) yield return delay;
            Click("模型展示"); yield return .1;
            Click("手动接管");
            Check(sim.inspector.ManualControl, "UI manual robot takeover");
            QueueKeys(InputKey.DownArrow); yield return .8;
            QueueKeys(); yield return .2;
            Check(!sim.inspector.HasArrived && order.stage == WorkshopOrderStage.Dispatched &&
                !sim.SubmitRepair(order, "故障", "维修", "无"), "Manual departure invalidates repair permission");
            Click("恢复自动");
            Check(!sim.inspector.ManualControl, "UI resumes automatic driving");
            foreach (var delay in Arrival()) yield return delay;
            Check(order.stage == WorkshopOrderStage.Repairing, "Automatic route resumes after manual departure");
            sim.ResetDemo();
            foreach (var delay in Shortcuts(true)) yield return delay;
        }
    }
}
