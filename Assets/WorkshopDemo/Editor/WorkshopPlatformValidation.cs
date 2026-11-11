using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WorkshopDemo.Editor
{
    [InitializeOnLoad]
    public static partial class WorkshopPlatformValidation
    {
        const string Key = "Workshop.PlatformValidation";
        static string Folder => SessionState.GetString(Key + ".Folder", "");
        static string Report => Folder + "/verification.txt";
        static WorkshopUI ui;
        static WorkshopSimulation sim;
        static IEnumerator<double> routine;
        static double next, until;
        static bool failed, stopping;
        static EditorWindow gameView;
        static int previousSize;
        static readonly List<int> added = new List<int>();
        static WorkshopPlatformValidation()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += Log;
        }
        [MenuItem("Tools/Workshop Demo/Verify Training Platform")]
        public static void Run()
        {
            string folder = "Evidence/platform-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(folder); SessionState.SetString(Key + ".Folder", folder);
            File.WriteAllText(Report, "Started " + DateTimeOffset.Now.ToString("o") +
                "\nScene " + WorkshopFoundationBuilder.DemoScene + "\nEditor Play / UGUI raycasts / actual NavMesh arrival\n");
            EditorSceneManager.OpenScene(WorkshopFoundationBuilder.DemoScene);
            SessionState.SetBool(Key, true); EditorApplication.isPlaying = true;
        }
        static void Log(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(Key, false) || type != LogType.Error && type != LogType.Exception) return;
            failed = true; File.AppendAllText(Report, "RUNTIME ERROR " + message + "\n" + stack + "\n");
        }
        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || EditorApplication.isCompiling) return;
            if (stopping)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                File.AppendAllText(Report, (failed ? "FAILED " : "COMPLETE ") + DateTimeOffset.Now.ToString("o") + "\n");
                SessionState.SetBool(Key, false);
                if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
                return;
            }
            try
            {
                if (until == 0) until = EditorApplication.timeSinceStartup + 600;
                if (EditorApplication.timeSinceStartup > until) throw new TimeoutException("Platform verification");
                if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < next) return;
                if (routine == null)
                {
                    sim = UnityEngine.Object.FindFirstObjectByType<WorkshopSimulation>();
                    ui = UnityEngine.Object.FindFirstObjectByType<WorkshopUI>();
                    if (!sim || !ui || sim.Elapsed < 2) return;
                    gameView = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                    previousSize = (int)gameView.GetType().GetProperty("selectedSizeIndex",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(gameView);
                    routine = Verify().GetEnumerator();
                }
                if (routine.MoveNext()) next = EditorApplication.timeSinceStartup + routine.Current;
                else Stop();
            }
            catch (Exception e) { failed = true; File.AppendAllText(Report, "FAIL " + e + "\n"); Stop(); }
        }
        static object Sizes()
        {
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            var instance = typeof(ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance").GetValue(null);
            return type.GetProperty("currentGroup").GetValue(instance);
        }
        static void Resolution(int width)
        {
            var group = Sizes(); var assembly = typeof(UnityEditor.Editor).Assembly;
            int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),
                new object[] { Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeType"), 1),
                    width, width * 9 / 16, "Platform " + width });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size }); added.Add(count);
            gameView.GetType().GetMethod("SizeSelectionCallback").Invoke(gameView, new object[] { count, null });
            gameView.Show(); gameView.Focus(); gameView.Repaint();
        }
        static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            File.AppendAllText(Report, "PASS " + message + "\n");
        }
        static Button Named(string name) => ui.GetComponentsInChildren<Button>().First(b => b.name == name);
        static void Click(Button b)
        {
            Canvas.ForceUpdateCanvases();
            Check(b.interactable, "Enabled " + b.name);
            var rect = (RectTransform)b.transform;
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == b.gameObject,
                "Top raycast " + b.name + " hit=" + (hits.Count > 0 ? hits[0].gameObject.name : "none"));
            ExecuteEvents.Execute(b.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        static void Click(string name) => Click(Named(name));
        static void Field(string name, string value) =>
            ui.GetComponentsInChildren<InputField>(true).First(i => i.name == name).text = value;
        static void Fits()
        {
            Canvas.ForceUpdateCanvases();
            foreach (var t in ui.GetComponentsInChildren<Text>())
            {
                if (string.IsNullOrEmpty(t.text) || t.GetComponentInParent<InputField>()) continue;
                Check(t.preferredHeight <= t.rectTransform.rect.height + 3, "Text fits " + t.name);
            }
        }
        static IEnumerable<double> Capture(string label, int width)
        {
            string path = Folder + "/" + label + "-" + width + ".png";
            ScreenCapture.CaptureScreenshot(Path.GetFullPath(path)); yield return .4;
            Check(File.Exists(path), "Screenshot " + label);
            var image = new Texture2D(2, 2); image.LoadImage(File.ReadAllBytes(path));
            Check(image.width == width && image.height == width * 9 / 16, "Screenshot dimensions " + label);
            var colors = new HashSet<Color32>();
            for (int y = 0; y < image.height; y += 11)
                for (int x = 0; x < image.width; x += 11) colors.Add(image.GetPixel(x, y));
            Check(colors.Count > 80, "Nonblank pixels " + label + " (" + colors.Count + ")");
            UnityEngine.Object.DestroyImmediate(image);
        }
        static IEnumerable<double> Arrival()
        {
            double deadline = EditorApplication.timeSinceStartup + 45;
            while (!sim.inspector.HasArrived && EditorApplication.timeSinceStartup < deadline) yield return .1;
            Check(sim.inspector.HasArrived, "Actual robot arrival");
        }
        static IEnumerable<double> Verify()
        {
            if (UnityEngine.InputSystem.Keyboard.current == null)
                UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
            if (UnityEngine.InputSystem.Mouse.current == null)
                UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Mouse>();
            sim.inspector.Agent.speed = 18; sim.inspector.Agent.acceleration = 80;
            sim.inspector.Agent.angularSpeed = 720;
            var pump = sim.devices.First(d => d.deviceId == "P-01");
            Check(sim.devices.Select(d => d.deviceId).SequenceEqual(WorkshopTrainingLayout.DeviceIds) &&
                UnityEngine.Object.FindObjectsByType<WorkshopDevice>(FindObjectsSortMode.None).Length == 4 &&
                UnityEngine.Object.FindObjectsByType<WorkshopInspector>(FindObjectsSortMode.None).Length == 1,
                "Exactly four equipment models and one robot");
            foreach (var device in sim.devices)
            {
                var model = device.GetComponentInChildren<WorkshopEquipmentModel>();
                Check(model && model.transform.localScale == Vector3.one, "Nominal metre scale " + device.deviceId);
                var bounds = device.GetComponent<BoxCollider>().bounds;
                Check(Mathf.Abs(bounds.min.y) < .04f && bounds.size.y > 1,
                    "Grounded full-size envelope " + device.deviceId + " " + bounds);
                Check(Vector3.Distance(device.transform.position, WorkshopTrainingLayout.Position(device.deviceId)) < .01f,
                    "Station position " + device.deviceId);
                Check(Quaternion.Angle(device.transform.rotation, WorkshopTrainingLayout.Rotation(device.deviceId)) < .1f,
                    "Station orientation " + device.deviceId);
                if (device.kind == DeviceKind.Cabinet || device.kind == DeviceKind.Fan || device.kind == DeviceKind.Tank)
                    Check(Vector3.Dot(-device.transform.forward,
                        (device.inspectionPoint - device.transform.position).normalized) > .99f,
                        "Service face opens toward inspection corridor " + device.deviceId);
                Check(bounds.min.x > 1.9f && bounds.max.x < 8.5f &&
                    bounds.min.z > -4 && bounds.max.z < 3.5f,
                    "Inside equipment area, outside crosshatch and pedestrian lane " + device.deviceId);
                foreach (var other in sim.devices.Where(d => d != device))
                    Check(!bounds.Intersects(other.GetComponent<BoxCollider>().bounds),
                        "Separated stations " + device.deviceId + "/" + other.deviceId);
                foreach (var obstacle in UnityEngine.Object.FindFirstObjectByType<Unity.AI.Navigation.NavMeshSurface>()
                    .GetComponentsInChildren<BoxCollider>().Where(c => c.name.StartsWith("Stock ")))
                    Check(!bounds.Intersects(obstacle.bounds), "Clear of stock obstacle " + device.deviceId + "/" + obstacle.name);
            }
            foreach (int width in new[] { 1920, 1280 })
            {
                Resolution(width);
                double deadline = EditorApplication.timeSinceStartup + 20;
                while (Screen.width != width && EditorApplication.timeSinceStartup < deadline) yield return .1;
                yield return 1;
                Check(Screen.width == width && Screen.height == width * 9 / 16,
                    "Resolution " + width + " actual=" + Screen.width + "x" + Screen.height);
                sim.ResetDemo(); Click("模型展示"); yield return .3; Fits();
                Check(!ui.GetComponentsInChildren<Transform>(true).Any(t =>
                    t.name == "Mini map" || t.name == "Mini Map" || t.name == "小地图") &&
                    !ui.GetComponentsInChildren<WorkshopHudGraphic>(true).Any(g => (int)g.shape == 4),
                    "No mini map in model view");
                foreach (string name in new[] { "AI问答", "模型展示", "故障分析", "设备台账", "维修工单", "实训复盘", "知识导入" })
                    Check(ui.GetComponentsInChildren<Button>().Any(b => b.name == name), "Sidebar entry " + name);
                var camera = sim.cameraRig.GetComponent<Camera>();
                Check(camera.pixelWidth > width * .48f && camera.pixelHeight > Screen.height * .55f, "Usable live viewport");
                foreach (var delay in Capture("overview", width)) yield return delay;
                Click("设备台账"); yield return .2; Fits();
                Check(ui.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Device ")) == 4, "Four ledger rows");
                Field("Device search", "P-01"); yield return .1;
                Check(ui.GetComponentsInChildren<Button>().Count(b => b.name.StartsWith("Device ")) == 1, "Ledger search filters");
                Click("Device P-01"); Check(sim.Selected == pump, "Ledger selects actual device");
                yield return .2;
                Click("停止设备"); Check(!pump.IsRunning, "Operation stops business device");
                Click("停止设备"); Check(pump.IsRunning, "Operation restarts business device");
                Click("新建故障课题"); yield return .2; Fits();
                foreach (var delay in Capture("fault-dialog", width)) yield return delay;
                Click("注入故障"); Check(sim.ActiveAlarm.stage == WorkshopOrderStage.Detected, "Injected fault awaits report");
                Click("处理当前工单"); yield return .2;
                Click("提交报修"); Check(sim.ActiveAlarm.stage == WorkshopOrderStage.Detected, "Empty report blocked");
                Field("报修人 *", "实训学员"); Field("所属部门 *", "生产一组"); Field("故障现象 *", "泵体发热，转速归零");
                Click("提交报修"); Check(sim.ActiveAlarm.stage == WorkshopOrderStage.Reported, "Report saved");
                Field("维修责任人 *", "维修学员");
                Click("提交报修"); Check(sim.inspector.IsBusy, "UI assignment dispatches robot");
                var order = sim.ActiveAlarm;
                Check(!sim.SubmitRepair(order, "过热", "冷却", "无"), "Repair before arrival rejected");
                Click("模型展示"); foreach (var delay in Arrival()) yield return delay;
                Click("处理当前工单"); yield return .2; Fits();
                Field("故障原因 *", "润滑不足"); Field("维修措施 *", "检查润滑、冷却并完成复位"); Field("配件材料 *", "无");
                foreach (var delay in Capture("repair", width)) yield return delay;
                Click("提交报修");
                Check(order.stage == WorkshopOrderStage.AwaitingAcceptance && pump.Fault != null &&
                    sim.Records.Count == 0 && !sim.inspector.IsBusy, "Repair submission awaits acceptance and releases robot");
                Field("使用方验收意见 *", "温度与转动已确认");
                foreach (var delay in Capture("acceptance", width)) yield return delay;
                Click("提交报修");
                Check(order.resolved && pump.IsRunning && sim.Records.Count == 1, "Acceptance closes and restores");
                Check(!sim.Accept(order, true, "重复") && sim.Records.Count == 1, "Duplicate acceptance blocked");
                Click("实训复盘"); yield return .2; Fits();
                foreach (var delay in Capture("review", width)) yield return delay;
                Click("AI问答"); yield return .2; Fits();
                foreach (var delay in Capture("knowledge", width)) yield return delay;
                Click("故障分析"); yield return .2; Fits();
                foreach (var delay in Capture("analysis", width)) yield return delay;
                foreach (var delay in Advanced(width)) yield return delay;
                Click("知识导入"); yield return .2; Fits();
                var client = ui.GetComponent<WorkshopAssistantClient>();
                client.ImportKnowledge("", ""); Check(!client.ImportBusy && client.ImportStatus.Contains("未提交"), "Empty import blocked");
                Field("资料标题 *", "循环水泵维护规程");
                Field("资料正文 *", "检查入口供液、润滑及冷却。仅用于实训，按现场规程验收。");
                foreach (var delay in Capture("import", width)) yield return delay;
                Click("设备台账"); Field("Device search", ""); yield return .2; Fits();
                foreach (var delay in Capture("ledger", width)) yield return delay;
                Click("重置"); Click("重置");
                Check(sim.Alarms.Count == 0 && sim.Records.Count == 0 && sim.RunningCount == 4, "Two UI resets");
            }
            // Validate business guards against all catalog combinations using actual navigation.
            int completed = 0;
            foreach (var device in sim.devices)
                foreach (var fault in WorkshopFaultCatalog.For(device.kind))
                {
                    Check(sim.TriggerFault(device, fault.Code), "Inject " + device.deviceId + "/" + fault.Code);
                    var a = sim.ActiveAlarm;
                    Check(!sim.Assign(a, "维修员", 30), "Unreported dispatch blocked");
                    Check(!sim.Accept(a, true, "通过"), "Early acceptance blocked");
                    Check(sim.Report(a, "报修员", "实训车间", fault.Symptom), "Valid report");
                    Check(!sim.Assign(a, "", 30) && !sim.Assign(a, "维修员", 0), "Invalid assignment blocked");
                    Check(sim.Assign(a, "维修员", 30), "Valid dispatch");
                    Check(!sim.Assign(a, "维修员", 30), "Duplicate dispatch blocked");
                    sim.Select(sim.devices.First(d => d != device), false);
                    foreach (var delay in Arrival()) yield return delay;
                    Check(a.stage == WorkshopOrderStage.Repairing && sim.inspector.TargetAlarm == a,
                        "Selection cannot change arriving work order");
                    Check(!sim.SubmitRepair(a, "", fault.Action, "无"), "Empty repair blocked");
                    Check(sim.SubmitRepair(a, fault.Mechanism, fault.Action, "无"), "Repair submitted");
                    Check(device.Fault != null && !a.resolved, "Fault remains until acceptance");
                    Check(sim.Accept(a, true, "现场确认恢复"), "Acceptance passed");
                    completed++;
                    Check(sim.Records.Count == completed && device.Fault == null && device.IsRunning,
                        "Typed record and restored state");
                }
            Check(completed == 16, "All 16 current device/fault combinations");
            sim.ResetDemo(); sim.Select(pump); sim.ToggleSelected();
            sim.TriggerFault(pump, "P-CAV"); var rework = sim.ActiveAlarm;
            sim.Report(rework, "使用方", "生产", "振动异常"); sim.Assign(rework, "维修", 1);
            foreach (var delay in Arrival()) yield return delay;
            sim.SubmitRepair(rework, "入口供液不足", "检查入口", "无");
            Check(sim.Accept(rework, false, "需要进一步复查") && rework.reworkCount == 1 &&
                rework.stage == WorkshopOrderStage.Reported && sim.Records.Count == 0, "Rejection returns for redispatch");
            sim.Assign(rework, "复查维修员", 30);
            foreach (var delay in Arrival()) yield return delay;
            sim.SubmitRepair(rework, "过滤器堵塞", "清理过滤器", "无"); sim.Accept(rework, true, "复查通过");
            Check(pump.Status == DeviceStatus.Stopped && sim.Records[0].reworkCount == 1, "Rework preserves prior manual stop");
            sim.ResetDemo();
            var fan = sim.devices.First(d => d.kind == DeviceKind.Fan);
            sim.TriggerFault(pump, "P-HOT"); var first = sim.ActiveAlarm;
            sim.TriggerFault(fan, "F-BEAR"); var second = sim.ActiveAlarm;
            sim.Report(first, "甲", "生产", "过热"); sim.Report(second, "乙", "生产", "异响");
            sim.Assign(first, "维修甲", 30);
            Check(!sim.Assign(second, "维修乙", 30), "Concurrent robot task blocked");
            foreach (var delay in Arrival()) yield return delay;
            sim.SubmitRepair(first, "冷却不足", "清理冷却", "无"); sim.Assign(second, "维修乙", 30);
            foreach (var delay in Arrival()) yield return delay;
            sim.SubmitRepair(second, "轴承磨损", "检查润滑", "无");
            first.dueAt = sim.SimulationTime.AddSeconds(-1);
            Check(sim.OverdueCount == 1 && sim.PendingAcceptanceCount == 2, "Overdue and pending counts");
            sim.Accept(first, true, "通过"); sim.Accept(second, true, "通过");
            Check(sim.Records.Count == 2 && sim.OpenAlarmCount == 0 && sim.RunningCount == 4, "Concurrent independent acceptance");
            sim.ResetDemo(); Click("模型展示"); yield return .2;
            foreach (var delay in ManualAndVisibility()) yield return delay;
            foreach (var delay in SceneRoundTrip()) yield return delay;
        }
        static void Stop()
        {
            if (gameView)
            {
                gameView.GetType().GetMethod("SizeSelectionCallback").Invoke(gameView, new object[] { previousSize, null });
                var group = Sizes();
                foreach (int index in added.OrderByDescending(i => i))
                    group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { index });
            }
            stopping = true; EditorApplication.isPlaying = false;
        }
    }
}
