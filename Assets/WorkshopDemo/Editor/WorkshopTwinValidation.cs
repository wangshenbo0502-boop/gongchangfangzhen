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
    public static class WorkshopTwinValidation
    {
        const string Key = "Workshop.TwinVisual";
        static int Width
        {
            get
            {
                var args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "-twin-width");
                return index >= 0 && index + 1 < args.Length ? int.Parse(args[index + 1]) : 1920;
            }
        }
        static bool FaultSuite => SessionState.GetBool("Workshop.FaultSuite", false);
        static bool DashboardHud => Environment.GetCommandLineArgs().Contains("-dashboard-hud");
        static bool DashboardRedesign => DashboardHud || Environment.GetCommandLineArgs().Contains("-dashboard-redesign");
        static string Report => DashboardHud ? "Evidence/dashboard-hud-" +
            (FaultSuite ? "faults-" : "") + Width + "-20261110.txt" :
            DashboardRedesign ? "Evidence/dashboard-redesign-" +
            (FaultSuite ? "faults-" : "") + Width + "-20261109.txt" :
            FaultSuite ? "Evidence/fault-verification-" + Width + "-20261109.txt" :
            "Evidence/twin-dashboard-verification-" + Width + "-20261108.txt";
        internal static string Screenshot(string label, int width) => DashboardHud ?
            "Evidence/dashboard-hud-" + label + "-" + width + "-20261110.png" : DashboardRedesign ?
            "Evidence/dashboard-redesign-" + label + "-" + width + "-20261109.png" :
            "Evidence/" + label + "-" + width + "-20261109.png";
        static IEnumerator<double> routine;
        static double next, deadline;
        static EditorWindow gameView;
        static WorkshopUI ui;
        static WorkshopSimulation simulation;
        static bool failed, stopping;
        static int savedSize;
        static readonly List<int> addedSizes = new List<int>();

        static WorkshopTwinValidation()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += Log;
        }

        public static void Run()
        {
            SessionState.SetBool("Workshop.FaultSuite", false);
            Begin();
        }

        static void Begin()
        {
            Directory.CreateDirectory("Evidence");
            if (File.Exists(Report))
                File.Copy(Report, Report + "." + DateTime.Now.ToString("HHmmss") + ".previous");
            File.WriteAllText(Report, "Started " + DateTimeOffset.Now.ToString("o") +
                "\nScene " + WorkshopFoundationBuilder.DemoScene + "\nMethod Editor Play / D3D11 / UGUI raycasts\n");
            EditorSceneManager.OpenScene(WorkshopFoundationBuilder.DemoScene);
            SessionState.SetBool(Key, true);
            deadline = EditorApplication.timeSinceStartup + (FaultSuite ? 600 : 240);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("Tools/Workshop Demo/Verify Fault Scenarios")]
        public static void RunFaults()
        {
            SessionState.SetBool("Workshop.FaultSuite", true);
            Begin();
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || EditorApplication.isCompiling) return;
            if (stopping)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                SessionState.SetBool(Key, false);
                File.AppendAllText(Report, failed ? "FAILED\n" : "COMPLETE\n");
                if (Application.isBatchMode) EditorApplication.Exit(failed ? 1 : 0);
                return;
            }
            try
            {
                if (deadline == 0) deadline = EditorApplication.timeSinceStartup + (FaultSuite ? 600 : 240);
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Dashboard verification");
                if (!EditorApplication.isPlaying || EditorApplication.isPaused ||
                    EditorApplication.timeSinceStartup < next) return;
                if (routine == null)
                {
                    simulation = UnityEngine.Object.FindFirstObjectByType<WorkshopSimulation>();
                    ui = UnityEngine.Object.FindFirstObjectByType<WorkshopUI>();
                    if (!simulation || !ui || simulation.Elapsed < 2) return;
                    gameView = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
                    savedSize = (int)gameView.GetType().GetProperty("selectedSizeIndex",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(gameView);
                    routine = Verify().GetEnumerator();
                }
                if (routine.MoveNext()) next = EditorApplication.timeSinceStartup + routine.Current;
                else Stop();
            }
            catch (Exception exception)
            {
                failed = true;
                File.AppendAllText(Report, "FAIL " + exception + "\n");
                Stop();
            }
        }

        static void Log(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(Key, false) || (type != LogType.Exception && type != LogType.Error)) return;
            failed = true;
            File.AppendAllText(Report, "RUNTIME ERROR " + message + "\n" + stack + "\n");
        }

        static IEnumerable<double> Verify()
        {
            if (FaultSuite)
            {
                foreach (var delay in WorkshopFaultValidation.Verify(ui, simulation, Width)) yield return delay;
                yield break;
            }
            foreach (var resolution in new[] { new Vector2Int(Width, Width * 9 / 16) })
            {
                SetResolution(resolution.x, resolution.y);
                double until = EditorApplication.timeSinceStartup + 20;
                while ((Screen.width != resolution.x || Screen.height != resolution.y) &&
                    EditorApplication.timeSinceStartup < until)
                {
                    gameView.Repaint();
                    yield return .1;
                }
                yield return 1;
                Check(Screen.width == resolution.x && Screen.height == resolution.y,
                    "Resolution " + resolution);
                Canvas.ForceUpdateCanvases();
                var plot = ui.GetComponentInChildren<WorkshopTrendGraphic>();
                Check(plot && plot.canvasRenderer, "Temperature trend has CanvasRenderer");
                var plotMesh = plot.canvasRenderer.GetMesh();
                Check(plotMesh && plotMesh.vertexCount > 16, "Temperature trend renders sampled line and grid");
                if (DashboardRedesign)
                {
                    var chart = ui.GetComponentInChildren<WorkshopDashboardGraphic>();
                    Check(chart && chart.canvasRenderer.GetMesh().vertexCount >= 400,
                        "Fleet status donut renders actual device counts");
                    var rate = chart.GetComponentsInChildren<Text>().First(text => text.text.Contains("%"));
                    Check(rate.text == "100%" && rate.canvasRenderer.GetMesh().vertexCount >= 16,
                        "Healthy equipment percentage has visible glyphs");
                }
                var camera = simulation.cameraRig.GetComponent<Camera>();
                Check(camera.pixelWidth > resolution.x * .4f && camera.pixelHeight > resolution.y * .45f,
                    "Central 3D viewport has usable area");
                if (DashboardHud)
                {
                    var readings = ui.transform.Find("Scene viewport/Metrics")
                        .GetComponentsInChildren<Text>().Where(text => text.name == "Metric value").ToArray();
                    Check(readings.Length == 4 && readings.All(text =>
                        text.text.Length == 2 && text.canvasRenderer.GetMesh().vertexCount >= 8),
                        "All four KPI values have visible digit geometry");
                    var tracking = ui.GetComponentInChildren<WorkshopSceneOverlay>();
                    Check(tracking && tracking.transform.childCount == simulation.devices.Length &&
                        tracking.GetComponentsInChildren<Button>().Length >= 2,
                        "World tracking binds all devices and projects visible labels");
                    Check(tracking.canvasRenderer.GetMesh().vertexCount > 16 && !tracking.raycastTarget,
                        "World leaders render without blocking scene input");
                    Check(!ui.GetComponentsInChildren<WorkshopHudGraphic>()
                            .Any(graphic => (int)graphic.shape == 4),
                        "Mini map removed from model view");
                    var header = ui.GetComponentsInChildren<WorkshopHudGraphic>()
                        .First(graphic => graphic.shape == WorkshopHudGraphic.Shape.Header);
                    var headerColors = header.canvasRenderer.GetMesh().colors32;
                    yield return .3;
                    Check(!headerColors.SequenceEqual(header.canvasRenderer.GetMesh().colors32),
                        "Header signal animation advances in rendered mesh");
                    var tag = tracking.GetComponentsInChildren<Button>()
                        .First(button => button.name != "Track " + simulation.Selected.deviceId);
                    var pointer = new PointerEventData(EventSystem.current) {
                        position = RectTransformUtility.WorldToScreenPoint(null,
                            tag.transform.TransformPoint(((RectTransform)tag.transform).rect.center))
                    };
                    var hits = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(pointer, hits);
                    Check(hits.Count > 0 && ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == tag.gameObject,
                        "Spatial tag receives top UI raycast");
                    ExecuteEvents.Execute(tag.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    Check(tag.name == "Track " + simulation.Selected.deviceId,
                        "Spatial tag selects corresponding business device");
                    simulation.ResetDemo();
                    yield return .3;
                }
                File.AppendAllText(Report, "CAMERA " + camera.transform.position + " " + camera.rect + "\n");
                foreach (var device in simulation.devices)
                    File.AppendAllText(Report, "DEVICE " + device.deviceId + " " +
                        device.GetComponent<Collider>().bounds + "\n");
                string overview = DashboardRedesign ? Screenshot("overview", resolution.x) :
                    "Evidence/twin-overview-" + resolution.x + "-20261108.png";
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(overview));
                yield return .5;
                foreach (var text in ui.GetComponentsInChildren<Text>())
                {
                    if (text.text.Length == 0 || text.transform.parent.name == "Question input") continue;
                    Check(text.preferredHeight <= text.rectTransform.rect.height + 3,
                        "Text fits: " + text.gameObject.name);
                }
                Click("P-02");
                Check(simulation.Selected.deviceId == "P-02", "Device ledger selects actual device");
                Click("停止设备");
                Check(simulation.Selected.Status == DeviceStatus.Stopped && simulation.RunningCount == 7,
                    "Stop updates business state");
                if (DashboardRedesign)
                    Check(ui.GetComponentInChildren<WorkshopDashboardGraphic>().GetComponentsInChildren<Text>()
                        .Any(text => text.text == "88%"), "Donut percentage follows stopped equipment");
                Click("停止设备");
                Check(simulation.Selected.IsRunning && simulation.RunningCount == 8,
                    "Start updates business state");
                Click("演示故障");
                yield return .2;
                Click("注入故障");
                Check(simulation.OpenAlarmCount == 1 && simulation.Selected.deviceId == "P-02",
                    "Fault dialog injects selected pump and creates one alarm");
                if (DashboardHud) CheckHudStage(0);
                Check(!ButtonNamed("确认处理").interactable, "Premature resolution disabled");
                Click("派发巡检");
                Check(simulation.inspector.IsBusy && !ButtonNamed("派发巡检").interactable,
                    "Dispatch creates task and prevents repeat");
                if (DashboardHud) CheckHudStage(1);
                yield return 1;
                if (DashboardHud)
                {
                    var gauge = ui.GetComponentsInChildren<WorkshopHudGraphic>()
                        .First(graphic => graphic.shape == WorkshopHudGraphic.Shape.Gauge);
                    Check(Mathf.Abs(gauge.Value - simulation.Selected.Temperature) < 2,
                        "Thermal instrument follows selected equipment measurement");
                    var tracking = ui.GetComponentInChildren<WorkshopSceneOverlay>();
                    var label = tracking.transform.Find("Track " + simulation.Selected.deviceId)
                        .GetComponentInChildren<Text>();
                    Check(label.color == (Color)new Color32(255, 109, 133, 255),
                        "Critical spatial label matches business fault severity");
                }
                string path = DashboardRedesign ? Screenshot("alarm", resolution.x) :
                    "Evidence/twin-dashboard-" + resolution.x + "-20261108.png";
                ScreenCapture.CaptureScreenshot(Path.GetFullPath(path));
                yield return 1;
                Check(File.Exists(path), "Screenshot saved " + path);
                var image = new Texture2D(2, 2);
                image.LoadImage(File.ReadAllBytes(path));
                Check(image.width == resolution.x && image.height == resolution.y, "Screenshot dimensions");
                var colors = new HashSet<Color32>();
                for (int y = 0; y < image.height; y += 17)
                    for (int x = 0; x < image.width; x += 17) colors.Add(image.GetPixel(x, y));
                Check(colors.Count > 200, "Rendered pixels are nonblank (" + colors.Count + " colors)");
                UnityEngine.Object.DestroyImmediate(image);
                if (DashboardRedesign)
                {
                    double arriveUntil = EditorApplication.timeSinceStartup + 45;
                    while (!simulation.inspector.HasArrived && EditorApplication.timeSinceStartup < arriveUntil)
                        yield return .1;
                    Check(simulation.CanResolve, "Robot reaches selected equipment via NavMesh");
                    if (DashboardHud) CheckHudStage(2);
                    Click("确认处理");
                    Check(simulation.OpenAlarmCount == 0 && simulation.RunningCount == 8 &&
                        simulation.Records.Count == 1, "Resolution restores equipment and archives one record");
                    if (DashboardHud) CheckHudStage(3);
                    Click("巡检记录");
                    Canvas.ForceUpdateCanvases();
                    Check(ui.transform.Find("Events").GetComponentsInChildren<Text>()
                        .Any(text => text.text.Contains("P-02") && text.text.Contains("ALM-001")),
                        "Completed inspection appears in records tab");
                    Click("告警");
                    Click("重置");
                    Click("重置");
                    Check(simulation.Alarms.Count == 0 && simulation.Records.Count == 0 &&
                        simulation.RunningCount == 8 && !simulation.inspector.IsBusy,
                        "Two UI resets clear tasks, records and faults");
                    if (DashboardHud) CheckHudStage(-1);
                }
                simulation.ResetDemo();
                yield return .3;
            }
            var client = ui.GetComponent<WorkshopAssistantClient>();
            client.Conversation.Add(new WorkshopAssistantClient.Turn {
                role = "user", content = string.Concat(Enumerable.Repeat("长问题滚动与中文排版检查。", 120))
            });
            typeof(WorkshopUI).GetMethod("RefreshAssistant", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(ui, null);
            Canvas.ForceUpdateCanvases();
            var scroll = ui.transform.Find("Details and assistant/Conversation").GetComponent<ScrollRect>();
            Check(scroll.content.rect.height > scroll.viewport.rect.height * 2, "Long conversation scrolls");
            var answer = scroll.content.GetComponentInChildren<Text>().rectTransform;
            Check(Mathf.Abs(answer.offsetMax.y) < 1, "Conversation text stays at top of content");
            client.Clear();
            if (Environment.GetCommandLineArgs().Contains("-twin-check-service"))
            {
                double readyUntil = EditorApplication.timeSinceStartup + 25;
                while (!client.KnowledgeReady && EditorApplication.timeSinceStartup < readyUntil)
                    yield return .2;
                Check(client.KnowledgeReady, "Unity connects to Docker knowledge service");
                client.Ask("P-01 超温后如何派发巡检？请结合当前状态说明。");
                double answerUntil = EditorApplication.timeSinceStartup + 100;
                while (client.Busy && EditorApplication.timeSinceStartup < answerUntil)
                    yield return .2;
                Check(!client.Busy, "Unity assistant request completes");
                if (string.IsNullOrEmpty(client.Error))
                    Check(client.LastAnswer != null && client.LastAnswer.sources.Length > 0,
                        "Live answer includes knowledge citations");
                else
                {
                    Check(client.Error.Contains("密钥"), "Invalid upstream credential displayed honestly");
                    Check(client.Conversation.Count == 0, "Failed request creates no fabricated answer");
                    File.AppendAllText(Report, "MODEL BLOCKED " + client.Error + "\n");
                }
                client.Clear();
            }
        }

        static void CheckHudStage(int expected)
        {
            var lifecycle = ui.GetComponentsInChildren<WorkshopHudGraphic>()
                .First(graphic => graphic.shape == WorkshopHudGraphic.Shape.Pipeline);
            Check(lifecycle.Stage == expected, "HUD lifecycle follows business stage " + expected);
        }

        internal static Button ButtonNamed(string name) =>
            ui.GetComponentsInChildren<Button>().First(button => button.name == name);

        internal static void Click(string name)
        {
            var button = name == "P-02" ? ui.GetComponentsInChildren<Button>()
                .First(value => value.GetComponentInChildren<Text>().text.StartsWith("P-02")) : ButtonNamed(name);
            Check(button.interactable, "Button enabled: " + name);
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Check(hits.Count > 0 &&
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
                "Top UI raycast: " + name);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        static object Group()
        {
            var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            var instance = typeof(ScriptableSingleton<>).MakeGenericType(type).GetProperty("instance").GetValue(null);
            return type.GetProperty("currentGroup").GetValue(instance);
        }

        internal static void SetResolution(int width, int height)
        {
            var group = Group();
            int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),
                new object[] { Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeType"), 1),
                    width, height, "Twin validation " + width });
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            addedSizes.Add(count);
            gameView.GetType().GetMethod("SizeSelectionCallback").Invoke(gameView, new object[] { count, null });
            gameView.Show(); gameView.Focus(); gameView.Repaint();
        }

        internal static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            File.AppendAllText(Report, "PASS " + message + "\n");
        }

        static void Stop()
        {
            if (gameView != null)
            {
                gameView.GetType().GetMethod("SizeSelectionCallback").Invoke(gameView, new object[] { savedSize, null });
                var group = Group();
                foreach (int index in addedSizes.OrderByDescending(value => value))
                    group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { index });
            }
            stopping = true;
            EditorApplication.isPlaying = false;
        }
    }
}
