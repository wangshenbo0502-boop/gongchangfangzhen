using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WorkshopDemo
{
    // Only installed with the explicit review flag. No work occurs during normal startup.
    public sealed class WorkshopCompactUIVerification : MonoBehaviour
    {
        string folder;
        WorkshopUI ui;
        WorkshopSimulation sim;
        readonly List<string> errors = new List<string>();
        string report;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-workshop-ui-verify");
            if (index < 0 || index + 1 >= args.Length) return;
            Application.runInBackground = true;
            var host = new GameObject("Compact UI verification");
            DontDestroyOnLoad(host);
            host.AddComponent<WorkshopCompactUIVerification>().folder = Path.GetFullPath(args[index + 1]);
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            report = Path.Combine(folder, "verification.txt");
            File.WriteAllText(report, "Started " + DateTimeOffset.Now.ToString("o") +
                "\nMethod: Windows Player, visible UGUI raycasts and control events, actual navigation\n");
            Application.logMessageReceived += Log;
            var args = Environment.GetCommandLineArgs();
            var routine = args.Contains("-workshop-check-tank") ? VerifyTank() :
                args.Contains("-workshop-check-cabinet") ? VerifyCabinet() : Verify();
            while (true)
            {
                object next;
                try { if (!routine.MoveNext()) break; next = routine.Current; }
                catch (Exception e) { errors.Add(e.ToString()); break; }
                yield return next;
            }
            if (errors.Count > 0) { ScreenCapture.CaptureScreenshot(Path.Combine(folder, "failure.png")); yield return new WaitForSecondsRealtime(.8f); }
            File.AppendAllText(report, string.Join("\n", errors.Select(e => "FAIL " + e)) + "\nResult: " + (errors.Count == 0 ? "PASS" : "FAIL") + "\n");
            Application.logMessageReceived -= Log;
            Application.Quit(errors.Count == 0 ? 0 : 1);
        }

        void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Exception) errors.Add(message + "\n" + stack);
        }
        T Member<T>(string name) => (T)typeof(WorkshopUI).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ui);
        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            File.AppendAllText(report, "PASS " + message + "\n");
        }
        void Hit(Selectable control)
        {
            Canvas.ForceUpdateCanvases();
            Check(control && control.IsActive() && control.IsInteractable(), "Enabled " + control.name);
            var r = (RectTransform)control.transform;
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, r.TransformPoint(r.rect.center)) };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == control.gameObject,
                "Top raycast " + control.name + " (" + (hits.Count == 0 ? "none" : hits[0].gameObject.name) + ")");
            ExecuteEvents.Execute(control.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        Button Button(string name, Transform scope = null) => (scope ? scope : ui.transform).GetComponentsInChildren<Button>().Single(b => b.name == name);
        void Click(string name, Transform scope = null) => Hit(Button(name, scope));
        void Choose(Dropdown control, int index)
        {
            // Validate the real header raycast, close its list, then dispatch its value event.
            Hit(control); control.Hide(); control.value = index;
        }
        void Field(string name, string value) => Member<InputField>(name).text = value;
        void Capture(string name)
        {
            Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
        }
        void FitButtons(Transform scope)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var button in scope.GetComponentsInChildren<Button>())
            {
                if (button.GetComponentInParent<ScrollRect>()) continue;
                var r = (RectTransform)button.transform;
                var corners = new Vector3[4]; r.GetWorldCorners(corners);
                Check(corners[0].x >= -1 && corners[0].y >= -1 && corners[2].x <= Screen.width + 1 && corners[2].y <= Screen.height + 1,
                    "Button in screen " + button.name);
                var label = button.GetComponentInChildren<Text>();
                if (label) Check(label.preferredHeight <= label.rectTransform.rect.height + 2, "Button text fits " + button.name);
            }
        }

        IEnumerator Verify()
        {
            var inputModule = FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (inputModule) inputModule.enabled = false; // Isolate event-driven verification from desktop clicks.
            yield return new WaitForSecondsRealtime(3);
            ui = FindFirstObjectByType<WorkshopUI>(); sim = FindFirstObjectByType<WorkshopSimulation>();
            Check(ui && sim, "Monitoring scene initialized at " + Screen.width + "x" + Screen.height);
            var navigation = ui.transform.Find("Navigation");
            Click("AI 助手", navigation);
            yield return new WaitForSecondsRealtime(.4f);
            Check(Member<int>("page") == 4 && Member<InputField>("question").gameObject.activeInHierarchy,
                "Sidebar opens the chat composer");
            Capture("00-assistant");
            Click("更多", navigation);
            yield return new WaitForSecondsRealtime(.2f);
            Click("关闭", Member<GameObject>("quickMenu").transform);
            Click("车间总览", navigation);
            yield return new WaitForSecondsRealtime(.2f);
            sim.Select(sim.devices.First(d => d.deviceId == "P-01"), false);
            FitButtons(ui.transform);
            Check(ui.transform.Find("Overview/Scene viewport/Camera toolbar").GetComponentsInChildren<Button>().Length == 2, "Home toolbar has two actions plus device selector");
            Capture("01-overview"); yield return new WaitForSecondsRealtime(.5f);
            Click("视图"); yield return new WaitForSecondsRealtime(.2f); yield return null;
            Click("跟随巡检"); yield return new WaitForSecondsRealtime(.2f); Check(sim.cameraRig.Following, "Follow available in view menu");
            Click("总览"); yield return new WaitForSecondsRealtime(.2f);
            Click("查看设备"); yield return new WaitForSecondsRealtime(.2f); yield return new WaitForSecondsRealtime(.8f);
            var equipment = Member<GameObject>("equipmentDialog");
            Check(!Member<GameObject>("equipmentFaultTools").activeSelf && !Member<GameObject>("equipmentCloseup").activeSelf, "Observation hides fault transport and closeup");
            FitButtons(equipment.transform); Capture("02-equipment"); yield return new WaitForSecondsRealtime(.5f);
            Click("拆装", equipment.transform); yield return new WaitForSecondsRealtime(.2f); yield return new WaitForSecondsRealtime(.2f); Click("全部拆开", equipment.transform); yield return new WaitForSecondsRealtime(.2f);
            Check(Member<WorkshopEquipmentView>("equipmentView").TargetDisassembly == 12, "Pump 12-assembly target preserved");
            yield return new WaitForSecondsRealtime(2);
            Capture("03-assembly"); yield return new WaitForSecondsRealtime(.5f);
            Click("重新组装", equipment.transform); yield return new WaitForSecondsRealtime(.2f);
            Check(Member<WorkshopEquipmentView>("equipmentView").TargetDisassembly == 0, "Assembly reverse target preserved");
            Click("故障演示", equipment.transform); yield return new WaitForSecondsRealtime(.2f); yield return new WaitForSecondsRealtime(.2f); Click("播放演化", equipment.transform); yield return new WaitForSecondsRealtime(.2f);
            Check(Member<WorkshopEquipmentView>("equipmentView").Playing, "Fault animation can play");
            Click("返回车间", equipment.transform); yield return new WaitForSecondsRealtime(.2f);
            Click("设备学习"); yield return new WaitForSecondsRealtime(.2f); Click("故障学习"); yield return new WaitForSecondsRealtime(.2f);
            var modes = WorkshopFaultCatalog.For(sim.Selected.kind);
            Choose(Member<Dropdown>("analysisMode"), Array.FindIndex(modes, f => f.Code == "P-HOT")); yield return new WaitForSecondsRealtime(.2f);
            yield return new WaitForSecondsRealtime(.3f);
            Check(!ui.transform.Find("Metrics").gameObject.activeSelf, "KPI hidden outside workshop");
            Check(ui.transform.Find("Analysis/Analysis commands").GetComponentsInChildren<Button>().Length == 3, "Fault page has three actions");
            Capture("04-fault-learning"); yield return new WaitForSecondsRealtime(.5f);
            Click("深入分析"); yield return new WaitForSecondsRealtime(.2f); yield return new WaitForSecondsRealtime(.8f);
            var analysis = Member<GameObject>("analysisDialog");
            Check(!Member<GameObject>("analysisToolDrawer").activeSelf, "Analysis tools collapsed by default");
            FitButtons(analysis.transform); Capture("05-analysis"); yield return new WaitForSecondsRealtime(.5f);
            Choose(Member<Dropdown>("analysisQuantity"), 1); yield return new WaitForSecondsRealtime(.2f);
            Check(Member<int>("analysisField") == 17, "Single result selector changes thermal result");
            Choose(Member<Dropdown>("analysisQuantity"), 0); yield return new WaitForSecondsRealtime(.2f);
            Choose(Member<Dropdown>("analysisComparison"), 0); yield return new WaitForSecondsRealtime(.2f);
            Check(Member<int>("analysisModel") == 0, "Baseline comparison remains functional");
            Click("工具", analysis.transform); yield return new WaitForSecondsRealtime(.2f); yield return new WaitForSecondsRealtime(.2f);
            Hit(Member<Toggle>("analysisWire")); yield return new WaitForSecondsRealtime(.2f);
            var category = analysis.GetComponentsInChildren<Dropdown>().Single(d => d.name == "Tool category");
            Choose(category, 1); yield return new WaitForSecondsRealtime(.2f); yield return new WaitForSecondsRealtime(.2f);
            bool cutBefore = Member<Toggle>("analysisSection").isOn;
            Hit(Member<Toggle>("analysisSection")); yield return new WaitForSecondsRealtime(.2f);
            Check(Member<GameObject>("analysisCutGroup").activeSelf == !cutBefore, "Section slider follows enabled state");
            Check(!Member<GameObject>("analysisScaleGroup").activeSelf, "Thermal mode hides deformation slider");
            Capture("06-analysis-tools"); yield return new WaitForSecondsRealtime(.5f);
            Choose(category, 2); yield return new WaitForSecondsRealtime(.2f); yield return new WaitForSecondsRealtime(.2f); Click("正视", analysis.transform); yield return new WaitForSecondsRealtime(.2f); Hit(Member<Toggle>("analysisMeasure")); yield return new WaitForSecondsRealtime(.2f);
            Choose(category, 4); yield return new WaitForSecondsRealtime(.2f); Capture("07-analysis-details"); yield return new WaitForSecondsRealtime(.5f);
            Click("收起", analysis.transform); yield return new WaitForSecondsRealtime(.2f); Click("返回工况", analysis.transform); yield return new WaitForSecondsRealtime(.2f);
            Choose(Member<Dropdown>("analysisMode"), Array.FindIndex(modes, f => f.Code == "P-SEAL")); yield return new WaitForSecondsRealtime(.2f);
            Click("深入分析"); yield return new WaitForSecondsRealtime(.2f); yield return new WaitForSecondsRealtime(.5f);
            analysis = Member<GameObject>("analysisDialog");
            Click("渐进开裂", analysis.transform); yield return new WaitForSecondsRealtime(.2f);
            Check(Member<WorkshopAnalysisView>("analysisView").FractureActive, "Fracture playback survives compact tool layout");
            yield return new WaitForSecondsRealtime(.5f);
            Capture("08-fracture"); yield return new WaitForSecondsRealtime(.5f);
            Click("渐进开裂", analysis.transform); yield return new WaitForSecondsRealtime(.2f);
            Check(!Member<WorkshopAnalysisView>("analysisView").FractureActive, "Return to static fields");
            Click("返回工况", analysis.transform); yield return new WaitForSecondsRealtime(.2f);
            Choose(Member<Dropdown>("analysisMode"), Array.FindIndex(modes, f => f.Code == "P-HOT")); yield return new WaitForSecondsRealtime(.2f);
            Click("开始故障实训"); yield return new WaitForSecondsRealtime(.2f);
            Check(sim.ActiveAlarm != null && Member<WorkshopAlarm>("editingOrder") == sim.ActiveAlarm && Member<int>("page") == 2,
                "Start lesson creates selected fault and opens its order directly");
            Check(ui.transform.Find("Orders").GetComponentsInChildren<InputField>().Length == 0, "Start inspection needs no form");
            Hit(Member<Button>("primary")); yield return new WaitForSecondsRealtime(.2f); Check(sim.ActiveAlarm.stage == WorkshopOrderStage.Dispatched, "Dispatch starts actual navigation");
            var order = sim.ActiveAlarm;
            Hit(Member<Button>("primary")); yield return new WaitForSecondsRealtime(.2f);
            Check(Member<int>("page") == 0 && sim.cameraRig.Following, "View site follows robot");
            Click("返回工单"); yield return new WaitForSecondsRealtime(.2f);
            Check(Member<WorkshopAlarm>("editingOrder") == order, "Return restores the same order");
            sim.inspector.Agent.speed = 12; sim.inspector.Agent.acceleration = 50;
            float until = Time.realtimeSinceStartup + 45;
            while (!sim.inspector.HasArrived && Time.realtimeSinceStartup < until) yield return new WaitForSecondsRealtime(.2f);
            Check(sim.inspector.HasArrived, "Robot actually arrived");
            Check(ui.transform.Find("Orders").GetComponentsInChildren<InputField>(true).Length == 1 &&
                Member<Text>("orderDetail").text.Contains(order.device.displayName), "Repair has device name and only one input");
            Hit(Member<Button>("primary")); yield return new WaitForSecondsRealtime(.2f);
            Check(order.stage == WorkshopOrderStage.Repairing, "Empty cause rejected");
            Field("cause", "教学散热异常");
            Capture("09-repair"); yield return new WaitForSecondsRealtime(.5f);
            Hit(Member<Button>("primary")); yield return new WaitForSecondsRealtime(.2f); Check(sim.ActiveAlarm.stage == WorkshopOrderStage.AwaitingAcceptance && sim.Selected.Fault != null, "Repair waits for acceptance");
            Hit(Member<Button>("primary")); yield return new WaitForSecondsRealtime(.2f);
            Check(sim.Records.Count == 1 && sim.Selected.Fault == null &&
                sim.Records[0].cause == "教学散热异常" && sim.Records[0].deviceName.Contains(order.device.displayName),
                "One-click acceptance archives the device and fault cause");
            Click("实训复盘"); yield return new WaitForSecondsRealtime(.2f); Capture("10-review"); yield return new WaitForSecondsRealtime(.5f);
            Click("车间总览"); yield return new WaitForSecondsRealtime(.2f); Click("巡检控制"); yield return new WaitForSecondsRealtime(.2f); Click("手动接管（方向键驾驶）"); yield return new WaitForSecondsRealtime(.2f);
            Check(sim.inspector.ManualControl, "Manual takeover reachable from robot control");
            Click("巡检控制"); yield return new WaitForSecondsRealtime(.2f); Click("恢复自动巡检"); yield return new WaitForSecondsRealtime(.2f); Check(!sim.inspector.ManualControl, "Automatic control restored");
            foreach (var device in sim.devices)
            {
                sim.Select(device, false); Click("查看设备"); yield return new WaitForSecondsRealtime(.2f); yield return new WaitForSecondsRealtime(.25f);
                Check(Member<WorkshopEquipmentView>("equipmentView").Model, "Equipment opens " + device.deviceId);
                if (device.kind == DeviceKind.Pump || device.kind == DeviceKind.Fan)
                {
                    var modelView = Member<WorkshopEquipmentView>("equipmentView");
                    var rendered = modelView.GetComponent<RawImage>();
                    var target = (RenderTexture)rendered.texture;
                    Check(target.width > rendered.rectTransform.rect.width * rendered.canvas.scaleFactor &&
                        target.height > rendered.rectTransform.rect.height * rendered.canvas.scaleFactor,
                        device.deviceId + " model target " + target.width + "x" + target.height + " exceeds viewport pixels");
                    var scope = Member<GameObject>("equipmentDialog").transform;
                    Click("拆装", scope); yield return new WaitForSecondsRealtime(.2f);
                    Click("全部拆开", scope); yield return new WaitForSecondsRealtime(2);
                    Capture(device.deviceId + "-internal-exploded"); yield return new WaitForSecondsRealtime(.4f);
                }
                Click("返回车间", Member<GameObject>("equipmentDialog").transform); yield return new WaitForSecondsRealtime(.2f);
            }
            Click("实训任务"); yield return new WaitForSecondsRealtime(.2f);
            Click("小车装配");
            var switcher = FindFirstObjectByType<WorkshopSceneSwitcher>();
            float sceneDeadline = Time.realtimeSinceStartup + 40;
            while ((switcher.IsSwitching || SceneManager.GetActiveScene().path != WorkshopSceneSwitcher.AssemblyScene) &&
                Time.realtimeSinceStartup < sceneDeadline) yield return null;
            Check(SceneManager.GetActiveScene().path == WorkshopSceneSwitcher.AssemblyScene, "Assembly scene opened from training tab");
            yield return new WaitForSecondsRealtime(.6f);
            switcher.SwitchTo(WorkshopSceneSwitcher.InspectionScene);
            sceneDeadline = Time.realtimeSinceStartup + 40;
            while ((switcher.IsSwitching || SceneManager.GetActiveScene().path != WorkshopSceneSwitcher.InspectionScene) &&
                Time.realtimeSinceStartup < sceneDeadline) yield return null;
            yield return new WaitForSecondsRealtime(1);
            ui = FindFirstObjectByType<WorkshopUI>(); sim = FindFirstObjectByType<WorkshopSimulation>();
            Check(ui && sim && Member<int>("page") == 0, "Return restores the monitoring overview");
            var frame = Member<RectTransform[]>("sceneFrame");
            var rect = sim.cameraRig.GetComponent<Camera>().rect;
            Check(frame.Length == 4 && frame.All(f => f.GetComponent<Image>().color.a == 1) &&
                Mathf.Approximately(frame[0].anchorMax.x, rect.xMin) &&
                Mathf.Approximately(frame[1].anchorMin.x, rect.xMax) &&
                Mathf.Approximately(frame[2].anchorMax.y, rect.yMin) &&
                Mathf.Approximately(frame[3].anchorMin.y, rect.yMax), "Opaque frame covers areas outside the monitoring camera");
            Capture("11-return-to-monitoring"); yield return new WaitForSecondsRealtime(.5f);
            Check(errors.Count == 0, "No runtime exceptions during checks");
        }

        IEnumerator VerifyCabinet()
        {
            var input = FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (input) input.enabled = false;
            yield return new WaitForSecondsRealtime(4);
            ui = FindFirstObjectByType<WorkshopUI>();
            sim = FindFirstObjectByType<WorkshopSimulation>();
            Check(ui && sim && sim.devices.Length == 4, "Four workshop stations loaded");
            var cabinet = sim.devices.Single(device => device.deviceId == "PLC-01");
            var model = cabinet.GetComponentInChildren<WorkshopEquipmentModel>();
            Check(model && model.ReviewedCabinetModel && model.AssemblyCount == 16 &&
                model.PartCount == 3247 && model.TriangleCount == 119066, "PLC-01 uses the approved geometry");
            var collider = cabinet.GetComponent<BoxCollider>();
            Check(Vector3.Distance(cabinet.transform.position, WorkshopTrainingLayout.Position("PLC-01")) < .01f &&
                Vector3.Distance(cabinet.inspectionPoint, WorkshopTrainingLayout.InspectionPoint("PLC-01")) < .01f &&
                Mathf.Abs(collider.bounds.min.y + .025f) < .01f, "Cabinet retains its station, floor contact and inspection point");
            Check(cabinet.statusLamp && cabinet.statusLamp.name == "cabinet_lamp_lens_0" &&
                model.GetComponentInChildren<ReflectionProbe>().customBakedTexture, "Status lamp and cabinet reflection are connected");
            var finish = model.GetComponentsInChildren<MeshRenderer>().SelectMany(r => r.sharedMaterials)
                .First(m => m.name == "cabsteel");
            Check(finish.shader.isSupported && finish.GetTexture("_BaseMap") && finish.GetTexture("_RoughMap"),
                "Approved powder finish and maps are loaded");
            sim.Select(cabinet);
            yield return new WaitForSecondsRealtime(1.2f);
            Capture("01-plc-in-workshop");
            yield return new WaitForEndOfFrame();
            Click("查看设备");
            yield return new WaitForSecondsRealtime(.65f);
            var view = Member<WorkshopEquipmentView>("equipmentView");
            var dialog = Member<GameObject>("equipmentDialog").transform;
            Check(view.Model.ReviewedCabinetModel && view.Model.AssemblyCount == 16,
                "Equipment workspace loads the 16-part cabinet");
            Capture("02-assembled");
            Click("拆装", dialog);
            yield return new WaitForSecondsRealtime(.35f);
            var doorParts = new[] { 3, 6, 15 }.Select(i => view.Model.AssemblyTransform(i)).ToArray();
            var home = doorParts.Select(t => t.position).ToArray();
            var rotations = doorParts.Select(t => t.rotation).ToArray();
            Click("全部拆开", dialog);
            while (view.Disassembly < .061f * 16)
            {
                CheckDoorAngle(doorParts, rotations);
                yield return null;
            }
            CheckDoorAngle(doorParts, rotations);
            float scale = view.Model.transform.lossyScale.x;
            for (int i = 0; i < doorParts.Length; i++)
            {
                var movement = view.Model.transform.InverseTransformVector(doorParts[i].position - home[i]);
                Check(movement.z < -.15f && Mathf.Abs(movement.x) < .001f && Mathf.Abs(movement.y) < .001f,
                    "Door-attached assembly retreats straight outward");
            }
            Capture("03-door-clearance");
            yield return new WaitForEndOfFrame();
            Click("重新组装", dialog);
            while (view.Disassembly > 0) yield return null;
            Check(doorParts.Select((t, i) => Vector3.Distance(t.position, home[i])).Max() < .0001f * scale,
                "Reversing during straight release restores the home pose");
            Click("全部拆开", dialog);
            while (view.Disassembly < 16) yield return null;
            Check(Quaternion.Angle(doorParts[0].rotation, rotations[0]) > 110, "Door fully rotates after release");
            Capture("04-exploded");
            yield return new WaitForEndOfFrame();
            Click("重新组装", dialog);
            while (view.Disassembly > 0) yield return null;
            Check(doorParts.Select((t, i) => Vector3.Distance(t.position, home[i])).Max() < .0001f * scale,
                "Complete reverse path restores the home pose");
            Click("视图", dialog);
            yield return new WaitForSecondsRealtime(.3f);
            string expected = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Viewers", "CabinetViewer", "CabinetViewer.exe");
            Check(File.Exists(expected), "Packaged cabinet viewer exists");
            Click("在独立展示器中打开");
            Check(Member<Text>("feedback").text.StartsWith("已打开"), "Viewer button returns a successful launch");
            // Inspect the launched process from the Windows test harness, outside Unity Mono.
            Click("返回车间", dialog);
            yield return new WaitForSecondsRealtime(.3f);
            sim.ToggleSelected();
            Check(cabinet.Status == DeviceStatus.Stopped, "PLC-01 stops through the existing simulation");
            sim.ToggleSelected();
            Check(cabinet.IsRunning, "PLC-01 starts through the existing simulation");
            Check(sim.TriggerFault(cabinet, "PLC-LOW") && cabinet.Fault != null &&
                cabinet.faultMarker.activeSelf && sim.Alarms[0].device == cabinet, "PLC-LOW retains fault marker and alarm binding");
            Check(errors.Count == 0, "No runtime exceptions during cabinet checks");
        }

        IEnumerator VerifyTank()
        {
            var input = FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (input) input.enabled = false;
            yield return new WaitForSecondsRealtime(4);
            ui = FindFirstObjectByType<WorkshopUI>();
            sim = FindFirstObjectByType<WorkshopSimulation>();
            Check(ui && sim && sim.devices.Length == 4, "Four stations loaded at " + Screen.width + "x" + Screen.height);
            var tank = sim.devices.Single(device => device.deviceId == "TK-01");
            var model = tank.GetComponentInChildren<WorkshopEquipmentModel>();
            Check(model && model.ReviewedTankModel && model.AssemblyCount == 12 &&
                model.PartCount == 892 && model.TriangleCount == 245364, "TK-01 uses the approved geometry");
            var bounds = tank.GetComponent<BoxCollider>().bounds;
            Check(Vector3.Distance(tank.transform.position, WorkshopTrainingLayout.Position("TK-01")) < .01f &&
                Vector3.Distance(tank.inspectionPoint, WorkshopTrainingLayout.InspectionPoint("TK-01")) < .01f &&
                Mathf.Abs(bounds.min.y + .025f) < .01f &&
                Mathf.Abs(model.NativeBounds.size.y - 5.8575f) < .01f,
                "Original station and inspection point, grounded scaled model and full-size nominal height");
            Check(sim.devices.Where(device => device != tank).All(device =>
                !device.GetComponent<BoxCollider>().bounds.Intersects(bounds)), "Tank envelope clears other equipment");
            var agent = sim.inspector.Agent;
            Check(Vector3.Distance(bounds.ClosestPoint(tank.inspectionPoint), tank.inspectionPoint) > agent.radius &&
                NavMesh.SamplePosition(tank.inspectionPoint, out var hit, .25f, NavMesh.AllAreas) &&
                agent.CalculatePath(hit.position, new NavMeshPath()), "Robot can reach the original approach point");
            var probe = model.GetComponentInChildren<ReflectionProbe>();
            var materials = model.GetComponentsInChildren<MeshRenderer>().SelectMany(r => r.sharedMaterials).Distinct().ToArray();
            Check(probe.customBakedTexture && materials.All(m => m.shader.isSupported) &&
                materials.Any(m => m.GetTexture("_BaseMap") && m.GetTexture("_RoughMap")), "Reviewed finishes and reflection are loaded");
            sim.Select(tank);
            yield return new WaitForSecondsRealtime(1.2f);
            Capture("01-tank-in-workshop"); yield return new WaitForEndOfFrame();
            Click("查看设备");
            yield return new WaitForSecondsRealtime(.65f);
            var view = Member<WorkshopEquipmentView>("equipmentView");
            var dialog = Member<GameObject>("equipmentDialog").transform;
            Check(view.Model.ReviewedTankModel && view.Model.AssemblyCount == 12 && view.Model.AssemblySeconds == 14,
                "Inspection shows the approved 12 assemblies and 14-second path");
            Capture("02-assembled"); yield return new WaitForEndOfFrame();
            Click("拆装", dialog);
            yield return new WaitForSecondsRealtime(.25f);
            var parts = Enumerable.Range(0, 12).Select(view.Model.AssemblyTransform).ToArray();
            var home = parts.Select(t => t.localPosition).ToArray();
            var nodes = view.Model.GetComponentsInChildren<Transform>();
            var axis = (nodes.Single(t => t.name == "TankMotionY").position -
                nodes.Single(t => t.name == "TankMotionOrigin").position).normalized;
            var cover = parts.Single(t => t.name == "TNK_10_ManwayCover");
            var coverHome = cover.position;
            Click("全部拆开", dialog);
            while (view.Disassembly < .039f * 12) yield return null;
            var movement = (cover.position - coverHome) / view.Model.transform.lossyScale.x;
            Check(Vector3.Dot(movement, -axis) > .74f && Vector3.ProjectOnPlane(movement, axis).magnitude < .002f,
                "Manway cover clears axially before lateral travel");
            Capture("03-cover-release"); yield return new WaitForEndOfFrame();
            Click("重新组装", dialog);
            while (view.Disassembly > 0) yield return null;
            Check(parts.Select((t, i) => t.localPosition == home[i]).All(equal => equal), "Mid-release reversal restores all home poses");
            Click("全部拆开", dialog);
            while (view.Disassembly < 12) yield return null;
            Check(parts.Where((t, i) => t.localPosition != home[i]).Count() == 12, "All 12 assemblies reach their separated positions");
            Capture("04-exploded"); yield return new WaitForEndOfFrame();
            Click("重新组装", dialog);
            while (view.Disassembly > 0) yield return null;
            Check(parts.Select((t, i) => t.localPosition == home[i]).All(equal => equal), "Full reverse path restores all home poses");
            Click("故障演示", dialog);
            yield return new WaitForSecondsRealtime(.25f);
            Click("时间点", dialog);
            yield return new WaitForSecondsRealtime(.25f);
            Click("最终状态");
            yield return new WaitForSecondsRealtime(.25f);
            Check(view.DisplayModel != view.Model && !view.Model.gameObject.activeSelf &&
                view.DisplayModel.HasOpening && view.DisplayModel.OpeningMm > 0 &&
                Member<Text>("equipmentSource").text.Contains("教学模型"), "Tank leakage teaching and close-up still operate");
            Capture("05-fault-teaching"); yield return new WaitForEndOfFrame();
            Click("观察", dialog);
            Check(view.DisplayModel == view.Model && view.Model.gameObject.activeSelf, "Observation restores the approved tank");
            Click("返回车间", dialog);
            yield return new WaitForSecondsRealtime(.3f);
            Check(sim.TriggerFault(tank, "TK-LEAK") && tank.faultMarker.activeSelf, "TK-LEAK still raises the physical marker and alarm");
            var alarm = sim.Alarms[0];
            Check(alarm.device == tank && sim.Report(alarm, "验证员", "设备部", "储罐泄漏巡检") &&
                sim.Assign(alarm, "维修员", 30), "Tank order dispatches the actual inspection robot");
            float deadline = Time.realtimeSinceStartup + 25;
            while (!sim.inspector.HasArrived && Time.realtimeSinceStartup < deadline)
            {
                CheckRobotOutsideTank(bounds, sim.inspector.transform.position);
                yield return null;
            }
            Check(sim.inspector.HasArrived && alarm.stage == WorkshopOrderStage.Repairing, "Robot clears tank and reaches repair stage");
            Capture("06-robot-arrived"); yield return new WaitForEndOfFrame();
            Click("设备学习", ui.transform.Find("Navigation"));
            yield return new WaitForSecondsRealtime(.3f);
            Click("Device TK-01");
            yield return new WaitForSecondsRealtime(.4f);
            Click("详细展示", Member<GameObject>("equipmentDialog").transform);
            Check(Member<Text>("feedback").text.StartsWith("已打开"), "Equipment catalogue launches the packaged TankViewer");
            yield return new WaitForSecondsRealtime(1);
            Check(errors.Count == 0, "No runtime exceptions during tank checks");
        }

        static void CheckRobotOutsideTank(Bounds bounds, Vector3 position)
        {
            position.y = bounds.center.y;
            if (bounds.Contains(position)) throw new InvalidOperationException("Robot route intersects tank envelope.");
        }

        static void CheckDoorAngle(Transform[] parts, Quaternion[] home)
        {
            for (int i = 0; i < parts.Length; i++)
                if (Quaternion.Angle(parts[i].rotation, home[i]) > .05f)
                    throw new InvalidOperationException("A door-attached part rotated before release.");
        }
    }
}
