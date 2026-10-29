using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DistributionCenter;
using ImmersiveTraining.Management;
using ImmersiveTraining.StateHandling;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WorkshopDemo.Editor
{
    [InitializeOnLoad]
    public static class FactoryTrainingUIValidation
    {
        const string Key = "Workshop.FactoryUIValidation";
        static string Report => SessionState.GetBool(Key + ".ArmOnly", false)
            ? "Evidence/factory-arm-ui-verification-20261029.txt"
            : "Evidence/factory-ui-verification-20261029.txt";
        static IEnumerator<double> steps;
        static double next;
        static bool stopping;
        static readonly List<string> errors = new List<string>();
        static EditorWindow gameView;
        static int savedSize;
        static readonly List<int> addedSizes = new List<int>();

        static FactoryTrainingUIValidation()
        {
            EditorApplication.update += Update;
            Application.logMessageReceived += OnLog;
        }

        public static void Begin() => Begin(false);

        public static void BeginArmLayout() => Begin(true);

        static void Begin(bool armOnly)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                SceneManager.GetActiveScene().path != FactoryTrainingUIEditor.ScenePath)
                throw new InvalidOperationException("请先停止运行并打开装配训练场景。");
            Directory.CreateDirectory("Evidence");
            SessionState.SetBool(Key + ".ArmOnly", armOnly);
            if (File.Exists(Report))
                File.Move(Report, Report + "." + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".previous");
            errors.Clear();
            addedSizes.Clear();
            steps = null;
            stopping = false;
            SessionState.SetBool(Key + ".Passed", false);
            SessionState.SetInt(Key + ".Errors", 0);
            File.WriteAllText(Report, $"Time={DateTime.Now:O}\nScene={FactoryTrainingUIEditor.ScenePath}\n" +
                (armOnly
                    ? "Method=Editor Play; slider 0/50/100%; visible sprite bounds; GPU screenshots.\n" +
                      "Scope=Arm UI at 1920x1080 and 1280x720. Other training flows and Windows player are not covered.\n"
                    : "Method=Editor Play; queued keyboard input, button and training events; GPU screenshots.\n" +
                      "Scope=Training UI only. Mouse dragging and Windows player are not covered.\n"));
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".Stage", 0);
            EditorApplication.isPlaying = true;
        }

        static void OnLog(string message, string stack, LogType type)
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errors.Add(message + "\n" + stack);
                SessionState.SetInt(Key + ".Errors", SessionState.GetInt(Key + ".Errors", 0) + 1);
                File.AppendAllText(Report, "RUNTIME ERROR " + message + "\n" + stack + "\n");
            }
        }

        static void Update()
        {
            if (!SessionState.GetBool(Key, false)) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (stopping || SessionState.GetInt(Key + ".Stage", 0) == 2)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Finish();
                return;
            }
            if (!EditorApplication.isPlaying) return;
            if (steps == null)
            {
                SessionState.SetInt(Key + ".Stage", 1);
                steps = Run();
                next = EditorApplication.timeSinceStartup + 2;
            }
            if (EditorApplication.timeSinceStartup < next) return;
            try
            {
                if (steps.MoveNext())
                {
                    next = EditorApplication.timeSinceStartup + steps.Current;
                    gameView?.Repaint();
                }
                else Stop();
            }
            catch (Exception exception)
            {
                File.AppendAllText(Report, "FAIL " + exception + "\n");
                Stop();
            }
        }

        static IEnumerator<double> Run()
        {
            var ui = UnityEngine.Object.FindFirstObjectByType<FactoryTrainingUI>();
            Require(ui != null, "Chinese UI component loaded; Play entered successfully");
            var timer = UnityEngine.Object.FindFirstObjectByType<TimeManager>();
            var serialized = new SerializedObject(timer);
            serialized.FindProperty("jsonFileName").stringValue = "FactoryUIVerification.json";
            serialized.ApplyModifiedPropertiesWithoutUndo();
            JsonFileUtility.WriteJsonToFile(new TimeDataCollection(), "FactoryUIVerification.json");
            gameView = EditorWindow.GetWindow(typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView"));
            savedSize = (int)gameView.GetType().GetProperty("selectedSizeIndex",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(gameView);
            if (SessionState.GetBool(Key + ".ArmOnly", false))
            {
                ui.cameraButtons[1].onClick.Invoke();
                foreach (var resolution in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
                {
                    SetResolution(resolution.x, resolution.y);
                    yield return 1;
                    Require(Screen.width == resolution.x && Screen.height == resolution.y,
                        $"Render size {resolution.x}x{resolution.y}");
                    foreach (var delay in CheckArmLayout(ui, true)) yield return delay;
                    CheckText(ui.gameObject);
                }
                Require(SessionState.GetInt(Key + ".Errors", 0) == 0, "No runtime errors during arm verification");
                SessionState.SetBool(Key + ".Passed", true);
                yield break;
            }
            SetResolution(1920, 1080);
            yield return 1;
            Require(Screen.width == 1920 && Screen.height == 1080,
                $"1920x1080 render size (actual {Screen.width}x{Screen.height})");
            string[] labels = { "组装", "使用", "远景", "近景" };
            for (int i = 0; i < 4; i++)
                Require(ui.cameraButtons[i].GetComponentInChildren<TMP_Text>().text == labels[i],
                    "Navigation " + labels[i]);
            var selected = ui.cameraButtons[0];
            Require(selected.GetComponent<Image>().color == FactoryTrainingUI.Accent &&
                selected.GetComponentInChildren<TMP_Text>().color == FactoryTrainingUI.OnAccent,
                "Equipment yellow selection with dark text");
            var surface = selected.GetComponent<Image>();
            Require(surface.type == Image.Type.Sliced && surface.sprite && surface.sprite.border.x == 16 &&
                surface.pixelsPerUnitMultiplier == 2, "Sliced rounded button surface");
            Require(ui.transform.Find("TopRightPanel") == null, "Corner shortcut buttons removed");
            foreach (var delay in CheckShortcuts(ui)) yield return delay;
            Capture("assembly-1920");
            yield return 1;
            var info = ui.cameraSwitcher.trainingSubMenu.GetComponentInChildren<UI_Toggle>(true);
            info.ToggleObjects();
            yield return .5;
            Require(ui.instruction.activeInHierarchy && ui.guideRows.Length == 13, "13 Chinese instruction rows opened");
            CheckText(ui.instruction);
            Capture("instructions-1920");
            yield return 1;
            ui.instruction.transform.Find("UI_Content/Close").GetComponent<Button>().onClick.Invoke();
            yield return .3;
            Require(!ui.instruction.activeSelf, "Instruction close button");
            for (int i = 1; i < 4; i++)
            {
                ui.cameraButtons[i].onClick.Invoke();
                yield return .5;
                Require(ui.cameraSwitcher.cameras[i].activeSelf, "Camera switch " + labels[i]);
                if (i == 1)
                {
                    var start = ui.cameraSwitcher.subMenu.transform.Find("subMenu_01/startStopBTN")
                        .GetComponent<Button>();
                    start.onClick.Invoke();
                    yield return .4;
                    Require(ui.movement.IsAutoRunning, "Automatic movement start");
                    start.onClick.Invoke();
                    foreach (var delay in CheckArmLayout(ui)) yield return delay;
                    ui.armSlider.value = Mathf.Lerp(ui.armSlider.minValue, ui.armSlider.maxValue, .65f);
                    yield return .3;
                    Require(!ui.movement.IsAutoRunning && ui.armValue.text == "65%", "Movement stop and arm slider");
                    Capture("usage-1920");
                    yield return 1;
                }
            }
            ui.cameraButtons[0].onClick.Invoke();
            yield return .5;
            var mode = ui.cameraSwitcher.trainingSubMenu.transform.Find("subMenu_01/TrainingModeButton")
                .GetComponent<Button>();
            if (ui.trainingMode.IsTrainingMode) mode.onClick.Invoke();
            yield return .5;
            Require(!ui.trainingMode.IsTrainingMode && ui.modeLabel.text == "独立考核", "Assessment mode");
            EventManager.TriggerEvent(EventTypes.POP_MODAL_PANEL_WITH_MESSAGE_ON_OBJECT,
                ui.training.CurrentState.gameObject);
            yield return .3;
            Require(ui.trainingDialogs[2].activeInHierarchy, "Assessment notice before timeout");
            EventManager.TriggerEvent(EventTypes.ALLOWED_TIME_EXPIRED, ui.training.CurrentState.gameObject);
            yield return .5;
            var timeout = ui.trainingDialogs[1];
            Require(timeout.activeInHierarchy && !ui.trainingDialogs[2].activeSelf,
                "Timeout dialog clears step notice");
            CheckText(timeout);
            Capture("timeout-1920");
            yield return 1;
            timeout.transform.Find("TrainingButton").GetComponent<Button>().onClick.Invoke();
            yield return 2;
            Require(ui.trainingMode.IsTrainingMode && !timeout.activeSelf &&
                ui.training.GetStateTrackerText() == "1 / 15", "Timeout return and reset");
            Require(ui.training.GetTrainingStates().All(state => state.PartToMove.GetComponent<BoxCollider>().enabled),
                "Part colliders restored after timeout reset");
            SetResolution(1280, 720);
            yield return 1;
            Require(Screen.width == 1280 && Screen.height == 720, "1280x720 render size");
            info.ToggleObjects();
            yield return .5;
            CheckText(ui.instruction);
            Capture("instructions-1280");
            yield return 1;
            ui.CloseInstructions();
            for (int i = 0; i < 15; i++)
            {
                if (i == 1)
                {
                    mode.onClick.Invoke();
                    yield return .3;
                    Require(!ui.trainingMode.IsTrainingMode, "Assessment for remaining steps");
                }
                var state = ui.training.CurrentState as StateTrainingWithPass;
                Require(state == ui.training.GetTrainingStates()[i], "Training step " + (i + 1));
                state.PartToMove.transform.position = state.GetTrainingData().correctFinalTransform.position;
                EventManager.TriggerEvent(EventTypes.TRAINING_STATE_COMPLETION_CHECK, state.PartToMove);
                yield return .7;
                var notice = ui.trainingDialogs[2];
                var continueButton = notice.transform.Find("Button").GetComponent<Button>();
                if (i == 0)
                {
                    Require(notice.activeInHierarchy && continueButton.gameObject.activeSelf,
                        "Guided notice retains Continue");
                    yield return 1.5;
                    Require(notice.activeInHierarchy, "Guided notice persists beyond assessment deadline");
                    Capture("guided-notification-1280");
                    yield return 1;
                    continueButton.onClick.Invoke();
                    Require(!notice.activeSelf, "Continue closes guided notice");
                }
                else if (i < 14)
                {
                    Require(notice.activeInHierarchy && !continueButton.gameObject.activeSelf,
                        "Assessment notice without Continue, step " + (i + 1));
                    if (i == 1)
                    {
                        float elapsed = TimerValue(timer);
                        Capture("assessment-notification-1280");
                        yield return 1.4;
                        Require(!notice.activeSelf, "Assessment notice dismisses automatically after 1.8 seconds");
                        Require(TimerValue(timer) > elapsed + 1,
                            "Automatic dismissal preserves next-step timer");
                    }
                    else if (i == 2)
                    {
                        EventManager.TriggerEvent(EventTypes.POP_MODAL_PANEL_WITH_MESSAGE_ON_OBJECT,
                            state.gameObject);
                        yield return 1.3;
                        Require(notice.activeInHierarchy, "Repeated notice renews dismissal deadline");
                        yield return .8;
                        Require(!notice.activeSelf, "Repeated notice also dismisses automatically");
                    }
                    else if (i == 3)
                    {
                        mode.onClick.Invoke();
                        yield return .2;
                        Require(ui.trainingMode.IsTrainingMode && !notice.activeSelf,
                            "Mode change clears previous notice");
                        mode.onClick.Invoke();
                        yield return .2;
                    }
                    else
                    {
                        yield return 1.4;
                        Require(!notice.activeSelf, "Assessment notice dismissed, step " + (i + 1));
                    }
                }
            }
            yield return .5;
            Require(ui.trainingDialogs[0].activeInHierarchy && !ui.trainingDialogs[2].activeSelf,
                "15-step completion dialog clears final notice");
            CheckText(ui.trainingDialogs[0]);
            var final = ui.trainingDialogs[0].transform.Find("FinalTimeText").GetComponent<TMP_Text>();
            Require(final.text.StartsWith("装配总用时"), "Chinese final total time");
            Capture("complete-1280");
            yield return 1;
            SetResolution(1920, 1080);
            yield return 1;
            Capture("complete-1920");
            yield return 1;
            ui.trainingDialogs[0].transform.Find("Button").GetComponent<Button>().onClick.Invoke();
            yield return 2;
            Require(ui.trainingMode.IsTrainingMode && ui.training.GetStateTrackerText() == "1 / 15" &&
                !ui.trainingDialogs[0].activeSelf, "Completion retry");
            EventManager.TriggerEvent(EventTypes.POP_MODAL_PANEL_WITH_MESSAGE_ON_OBJECT,
                ui.training.CurrentState.gameObject);
            yield return .2;
            ui.ResetAssembly();
            yield return 2;
            Require(!ui.trainingDialogs[2].activeSelf && ui.trainingMode.IsTrainingMode &&
                ui.training.GetStateTrackerText() == "1 / 15", "Reset clears pending notice");
            CheckText(ui.gameObject);
            Require(SessionState.GetInt(Key + ".Errors", 0) == 0, "No runtime errors during verification");
            SessionState.SetBool(Key + ".Passed", true);
        }

        static float TimerValue(TimeManager timer)
        {
            return (float)typeof(TimeManager).GetField("timer",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(timer);
        }

        static IEnumerable<double> CheckArmLayout(FactoryTrainingUI ui, bool capture = false)
        {
            var arm = (RectTransform)ui.armSlider.transform.parent;
            var diagram = arm.Find("motoIcon");
            foreach (float value in new[] { 0f, .5f, 1f })
            {
                ui.armSlider.normalizedValue = value;
                yield return .2;
                Canvas.ForceUpdateCanvases();
                var sliderBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(
                    arm, ui.armSlider.transform);
                var diagramBounds = VisibleDiagramBounds(arm, diagram);
                Require(diagramBounds.min.x >= arm.rect.xMin + 19 &&
                    diagramBounds.max.x <= arm.rect.xMax - 19 &&
                    diagramBounds.min.y >= arm.rect.yMin + 10 &&
                    diagramBounds.max.y < sliderBounds.min.y - 10,
                    "Arm diagram inside panel and below slider at " + value);
                Require(Mathf.Abs(diagramBounds.center.x - arm.rect.center.x) <= 4 &&
                    diagramBounds.size.x >= 220, "Visible arm centered and enlarged at " + value);
                var handle = ui.armSlider.handleRect.rect;
                Require(handle.width <= 20 && handle.height <= 32,
                    "Compact slider handle at " + value);
                if (capture)
                {
                    Require(ui.armValue.text == Mathf.RoundToInt(value * 100) + "%", "Arm percentage at " + value);
                    File.AppendAllText(Report, $"BOUNDS value={value} visible={diagramBounds} slider={sliderBounds}\n");
                    Capture($"arm-{Mathf.RoundToInt(value * 100)}-{Screen.width}");
                    yield return .5;
                }
            }
        }

        static Bounds VisibleDiagramBounds(RectTransform relativeTo, Transform diagram)
        {
            Bounds? bounds = null;
            foreach (var image in diagram.GetComponentsInChildren<Image>())
            {
                var texture = new Texture2D(2, 2);
                try
                {
                    texture.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(image.sprite)));
                    var pixels = texture.GetPixels32();
                    var sprite = image.sprite.rect;
                    int minX = (int)sprite.xMax, minY = (int)sprite.yMax;
                    int maxX = -1, maxY = -1;
                    for (int y = (int)sprite.yMin; y < sprite.yMax; y++)
                        for (int x = (int)sprite.xMin; x < sprite.xMax; x++)
                            if (pixels[y * texture.width + x].a > 0)
                            {
                                minX = Mathf.Min(minX, x);
                                minY = Mathf.Min(minY, y);
                                maxX = Mathf.Max(maxX, x + 1);
                                maxY = Mathf.Max(maxY, y + 1);
                            }
                    var rect = image.rectTransform.rect;
                    foreach (var pixel in new[] { new Vector2(minX, minY), new Vector2(maxX, minY),
                                 new Vector2(minX, maxY), new Vector2(maxX, maxY) })
                    {
                        var local = rect.min + Vector2.Scale(rect.size,
                            (pixel - sprite.min) / sprite.size);
                        var point = relativeTo.InverseTransformPoint(image.transform.TransformPoint(local));
                        if (bounds.HasValue)
                        {
                            var expanded = bounds.Value;
                            expanded.Encapsulate(point);
                            bounds = expanded;
                        }
                        else bounds = new Bounds(point, Vector3.zero);
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
            return bounds ?? throw new InvalidOperationException("Arm diagram has no visible images");
        }

        static IEnumerable<double> CheckShortcuts(FactoryTrainingUI ui)
        {
            Require(Keyboard.current != null, "Keyboard available");
            var canvases = ui.interfaceCanvases;
            var canvasStates = canvases.Select(canvas => canvas.enabled).ToArray();
            var raycasters = canvases.Select(canvas => canvas.GetComponent<GraphicRaycaster>())
                .Where(raycaster => raycaster != null).ToArray();
            var raycasterStates = raycasters.Select(raycaster => raycaster.enabled).ToArray();
            float volume = AudioListener.volume;
            QueueKeys(UnityEngine.InputSystem.Key.H);
            yield return .3;
            Require(canvases.All(canvas => !canvas.enabled) && raycasters.All(raycaster => !raycaster.enabled),
                "H hides all canvases and blocks UI raycasts");
            QueueKeys();
            yield return .2;
            QueueKeys(UnityEngine.InputSystem.Key.A);
            yield return .3;
            float mutedVolume = AudioListener.volume;
            Require(mutedVolume == (volume > 0 ? 0 : 1), "A toggles audio while UI is hidden");
            yield return .4;
            Require(AudioListener.volume == mutedVolume, "Holding A does not retrigger audio");
            var agents = UnityEngine.Object.FindObjectsByType<AgentMovement>(FindObjectsSortMode.None);
            Require(agents.Length > 0 && agents.All(agent => agent.useArrowKeys &&
                !(bool)typeof(AgentMovement).GetField("manualControl",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(agent)),
                "A does not steer robots");
            QueueKeys();
            yield return .2;
            QueueKeys(UnityEngine.InputSystem.Key.A);
            yield return .3;
            Require(AudioListener.volume == volume, "A restores audio");
            QueueKeys();
            yield return .2;
            QueueKeys(UnityEngine.InputSystem.Key.H);
            yield return .3;
            Require(canvases.Select(canvas => canvas.enabled).SequenceEqual(canvasStates) &&
                raycasters.Select(raycaster => raycaster.enabled).SequenceEqual(raycasterStates),
                "H restores prior canvas and raycaster states");
            yield return .4;
            Require(canvases.Select(canvas => canvas.enabled).SequenceEqual(canvasStates),
                "Holding H does not retrigger visibility");
            QueueKeys();
            yield return .2;
            QueueKeys(UnityEngine.InputSystem.Key.LeftArrow);
            yield return .3;
            Require(agents.Any(agent => (bool)typeof(AgentMovement).GetField("manualControl",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(agent)),
                "Arrow keys control robots");
            QueueKeys();
            yield return .2;
        }

        static void QueueKeys(params UnityEngine.InputSystem.Key[] keys)
        {
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys));
        }

        static void CheckText(GameObject root)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in root.GetComponentsInChildren<TMP_Text>())
            {
                text.ForceMeshUpdate();
                if (text.text.Length == 0) continue;
                if (text.preferredWidth > text.rectTransform.rect.width + 2 &&
                    text.preferredHeight > text.rectTransform.rect.height + 2)
                    throw new InvalidOperationException("Text overflow: " + FactoryTrainingUIEditor.PathOf(text.transform));
                if (text.preferredHeight > text.rectTransform.rect.height + 3)
                    throw new InvalidOperationException("Text height overflow: " + text.text);
            }
        }

        static object Group()
        {
            var assembly = typeof(UnityEditor.Editor).Assembly;
            var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
            var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var sizes = singleton.GetProperty("instance").GetValue(null);
            return sizesType.GetProperty("currentGroup").GetValue(sizes);
        }

        static void SetResolution(int width, int height)
        {
            var group = Group();
            int count = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null);
            string label = "Workshop UI " + width + "x" + height;
            int index = -1;
            for (int i = 0; i < count; i++)
            {
                var size = group.GetType().GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
                if ((string)size.GetType().GetProperty("baseText").GetValue(size) == label) index = i;
            }
            if (index < 0)
            {
                var assembly = typeof(UnityEditor.Editor).Assembly;
                var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),
                    new object[] { Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeType"), 1), width, height, label });
                group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
                index = count;
                addedSizes.Add(index);
            }
            SelectSize(index);
            gameView.Show();
            gameView.Focus();
            gameView.Repaint();
        }

        static void SelectSize(int index)
        {
            gameView.GetType().GetMethod("SizeSelectionCallback").Invoke(gameView, new object[] { index, null });
        }

        static void Capture(string name)
        {
            string path = "Evidence/factory-ui-" + name + "-20261029.png";
            ScreenCapture.CaptureScreenshot(path);
            File.AppendAllText(Report, "SCREENSHOT " + path + "\n");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            File.AppendAllText(Report, "PASS " + message + "\n");
        }

        static void Stop()
        {
            try
            {
                if (Keyboard.current != null) QueueKeys();
                if (gameView != null)
                {
                    SelectSize(savedSize);
                    var group = Group();
                    foreach (int index in addedSizes.OrderByDescending(value => value))
                        group.GetType().GetMethod("RemoveCustomSize").Invoke(group, new object[] { index });
                }
            }
            catch (Exception exception)
            {
                SessionState.SetBool(Key + ".Passed", false);
                File.AppendAllText(Report, "FAIL restoring Game view: " + exception + "\n");
            }
            finally
            {
                SessionState.SetInt(Key + ".Stage", 2);
                stopping = true;
                EditorApplication.isPlaying = false;
            }
        }

        static void Finish()
        {
            foreach (var path in Directory.GetFiles("Assets", "FactoryUIVerification*"))
            {
                string target = Path.Combine("Evidence", Path.GetFileName(path));
                if (File.Exists(target)) target += "." + DateTime.Now.ToString("yyyyMMddHHmmssfff");
                File.Move(path, target);
            }
            bool passed = SessionState.GetBool(Key + ".Passed", false) &&
                SessionState.GetInt(Key + ".Errors", 0) == 0;
            File.AppendAllText(Report, $"Result={(passed ? "COMPLETE" : "FAILED")}\nEnd={DateTime.Now:O}\n");
            SessionState.SetBool(Key, false);
            SessionState.SetBool(Key + ".Passed", false);
            steps = null;
            stopping = false;
            addedSizes.Clear();
            errors.Clear();
        }
    }
}
