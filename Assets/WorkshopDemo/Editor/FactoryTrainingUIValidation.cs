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
using UnityEngine.EventSystems;
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
        static string Report => SessionState.GetBool(Key + ".DialogsOnly", false)
            ? "Evidence/factory-dialogs-verification-20261030.txt"
            : SessionState.GetBool(Key + ".ArmOnly", false)
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

        public static void BeginDialogs() => Begin(false, true);

        public static void InspectInput()
        {
            var ui = UnityEngine.Object.FindFirstObjectByType<FactoryTrainingUI>();
            var system = EventSystem.current;
            string report = $"Evidence/factory-input-{DateTime.Now:yyyyMMdd-HHmmssfff}.txt";
            File.WriteAllText(report, $"Time={DateTime.Now:O}\nPlaying={Application.isPlaying} " +
                $"Screen={Screen.width}x{Screen.height} EventSystem={system} Module={system?.currentInputModule}\n");
            if (!ui || !system) return;
            Canvas.ForceUpdateCanvases();
            var targets = ui.cameraButtons.Select(button => button.GetComponent<RectTransform>())
                .Concat(new[] { ui.movement.startStopBTN.GetComponent<RectTransform>(),
                    ui.armSlider.GetComponent<RectTransform>() });
            foreach (var rect in targets)
            {
                var canvas = rect.GetComponentInParent<Canvas>();
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
                var hits = new List<RaycastResult>();
                system.RaycastAll(new PointerEventData(system) { position = position }, hits);
                File.AppendAllText(report, $"TARGET {FactoryTrainingUIEditor.PathOf(rect)} at={position}\n" +
                    string.Join("\n", hits.Select(hit => "  HIT " + FactoryTrainingUIEditor.PathOf(hit.gameObject.transform) +
                        $" module={hit.module} order={hit.sortingOrder} depth={hit.depth}")) + "\n");
            }
        }

        static void Begin(bool armOnly, bool dialogsOnly = false)
        {
            if (SessionState.GetBool(Key, false))
            {
                Debug.Log("训练界面验证正在进行，请等待本轮结果。");
                return;
            }
            if (SceneManager.GetActiveScene().path != FactoryTrainingUIEditor.ScenePath)
            {
                Debug.LogWarning("请先打开装配训练场景，再运行训练界面验证。");
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(Key + ".Pending", true);
                SessionState.SetBool(Key + ".PendingArmOnly", armOnly);
                SessionState.SetBool(Key + ".PendingDialogsOnly", dialogsOnly);
                EditorApplication.isPlaying = false;
                return;
            }
            Directory.CreateDirectory("Evidence");
            SessionState.SetBool(Key + ".ArmOnly", armOnly);
            SessionState.SetBool(Key + ".DialogsOnly", dialogsOnly);
            if (File.Exists(Report))
                File.Move(Report, Report + "." + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".previous");
            errors.Clear();
            addedSizes.Clear();
            steps = null;
            stopping = false;
            SessionState.SetBool(Key + ".Passed", false);
            SessionState.SetInt(Key + ".Errors", 0);
            File.WriteAllText(Report, $"Time={DateTime.Now:O}\nScene={FactoryTrainingUIEditor.ScenePath}\n" +
                (dialogsOnly
                    ? "Method=Editor Play; queued mouse input and EventSystem raycasts; training events; bounds and GPU screenshots.\n" +
                      "Scope=Mouse navigation, automatic movement and arm slider dragging; step notices in both modes; completion and retry at 1920x1080, 1280x720 and 1024x768; robot enable/disable. Windows player and manual assembly dragging are not covered.\n"
                    : armOnly
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
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            if (!SessionState.GetBool(Key, false))
            {
                if (SessionState.GetBool(Key + ".Pending", false) &&
                    !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    SessionState.SetBool(Key + ".Pending", false);
                    Begin(SessionState.GetBool(Key + ".PendingArmOnly", false),
                        SessionState.GetBool(Key + ".PendingDialogsOnly", false));
                }
                return;
            }
            if (stopping || SessionState.GetInt(Key + ".Stage", 0) == 2)
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                Finish();
                return;
            }
            if (!EditorApplication.isPlaying)
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                    SessionState.GetInt(Key + ".Stage", 0) == 1)
                {
                    SessionState.SetBool(Key + ".Passed", false);
                    File.AppendAllText(Report, "FAIL Play stopped before verification finished.\n");
                    Stop();
                }
                return;
            }
            if (steps == null)
            {
                EditorApplication.isPaused = false;
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
            if (SessionState.GetBool(Key + ".DialogsOnly", false))
            {
                foreach (var delay in CheckAgentLifecycle(ui)) yield return delay;
                foreach (var delay in CheckDialogs(ui, timer)) yield return delay;
                Require(SessionState.GetInt(Key + ".Errors", 0) == 0, "No runtime errors during dialog verification");
                SessionState.SetBool(Key + ".Passed", true);
                yield break;
            }
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
                        "Guided notice has Continue");
                    Capture("guided-notification-1280");
                    continueButton.onClick.Invoke();
                    yield return .25;
                    Require(!notice.activeSelf, "Continue closes guided notice");
                    EventManager.TriggerEvent(EventTypes.POP_MODAL_PANEL_WITH_MESSAGE_ON_OBJECT, state.gameObject);
                    yield return 2.9;
                    Require(!notice.activeSelf, "Guided notice automatically dismisses after 2.4 seconds and exit");
                }
                else if (i < 14)
                {
                    Require(notice.activeInHierarchy && continueButton.gameObject.activeSelf,
                        "Assessment notice has Continue, step " + (i + 1));
                    if (i == 1)
                    {
                        float elapsed = TimerValue(timer);
                        Capture("assessment-notification-1280");
                        yield return 2.2;
                        Require(!notice.activeSelf, "Assessment notice dismisses automatically after 2.4 seconds and exit");
                        Require(TimerValue(timer) > elapsed + 1,
                            "Automatic dismissal preserves next-step timer");
                    }
                    else if (i == 2)
                    {
                        EventManager.TriggerEvent(EventTypes.POP_MODAL_PANEL_WITH_MESSAGE_ON_OBJECT,
                            state.gameObject);
                        yield return 1.3;
                        Require(notice.activeInHierarchy, "Repeated notice renews dismissal deadline");
                        yield return 1.6;
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
                        yield return 2.2;
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

        static IEnumerable<double> CheckAgentLifecycle(FactoryTrainingUI ui)
        {
            foreach (var resolution in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720),
                         new Vector2Int(1024, 768) })
            {
                foreach (var delay in RenderAt(resolution)) yield return delay;
                foreach (int index in new[] { 1, 0, 2, 3, 1 })
                {
                    foreach (var delay in Click(ui.cameraButtons[index])) yield return delay;
                    Require(ui.cameraSwitcher.cameras[index].activeSelf &&
                        ui.cameraSwitcher.cameras.Count(camera => camera.activeSelf) == 1,
                        $"Mouse switches camera {index} at {resolution}");
                }
                foreach (var delay in Click(ui.movement.startStopBTN)) yield return delay;
                Require(ui.movement.IsAutoRunning, "Mouse starts automatic movement");
                yield return .5;
                foreach (var delay in Click(ui.movement.startStopBTN)) yield return delay;
                Require(!ui.movement.IsAutoRunning, "Mouse stops automatic movement");
                foreach (float value in new[] { .75f, .25f })
                    foreach (var delay in DragArm(ui, value)) yield return delay;
                Capture("usage-input-" + resolution.x);
                foreach (var delay in WaitFrames()) yield return delay;
            }
            for (int i = 0; i < 2; i++)
            {
                ui.movement.enabled = false;
                foreach (var delay in WaitFrames()) yield return delay;
                ui.movement.enabled = true;
                foreach (var delay in WaitFrames()) yield return delay;
                foreach (var delay in Click(ui.movement.startStopBTN)) yield return delay;
                Require(ui.movement.IsAutoRunning, "One click starts robot after re-enable " + (i + 1));
                bool moved = false;
                float until = Time.unscaledTime + 2;
                while (Time.unscaledTime < until)
                {
                    moved |= ui.movement.navAgentArr[0].velocity.sqrMagnitude > .01f;
                    yield return .05;
                }
                Require(moved, "Robot moves after re-enable " + (i + 1));
                foreach (var delay in Click(ui.movement.startStopBTN)) yield return delay;
                Require(!ui.movement.IsAutoRunning, "One click stops robot after re-enable " + (i + 1));
            }
        }

        static IEnumerable<double> DragArm(FactoryTrainingUI ui, float value)
        {
            var slider = ui.armSlider;
            var translator = UnityEngine.Object.FindObjectsByType<GOTranslator>(FindObjectsSortMode.None)
                .Single(controller => controller.translationSlider == slider);
            var arm = translator.GOTranslator_ARRAY.First(part => part);
            var before = arm.transform.localPosition;
            Canvas.ForceUpdateCanvases();
            var canvas = slider.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var handle = slider.handleRect;
            var area = (RectTransform)handle.parent;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(camera,
                handle.TransformPoint(handle.rect.center));
            Vector2 destination = RectTransformUtility.WorldToScreenPoint(camera,
                area.TransformPoint(new Vector3(Mathf.Lerp(area.rect.xMin, area.rect.xMax, value),
                    area.rect.center.y, 0)));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Require(slider.IsInteractable() && hits.Count > 0 &&
                ExecuteEvents.GetEventHandler<IDragHandler>(hits[0].gameObject) == slider.gameObject,
                "EventSystem reaches slider handle");
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = position });
            foreach (var delay in WaitFrames(2)) yield return delay;
            InputSystem.QueueStateEvent(Mouse.current,
                new MouseState { position = position }.WithButton(MouseButton.Left));
            foreach (var delay in WaitFrames(2)) yield return delay;
            for (int i = 1; i <= 6; i++)
            {
                InputSystem.QueueStateEvent(Mouse.current,
                    new MouseState { position = Vector2.Lerp(position, destination, i / 6f) }
                        .WithButton(MouseButton.Left));
                foreach (var delay in WaitFrames(2)) yield return delay;
            }
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = destination });
            foreach (var delay in WaitFrames(2)) yield return delay;
            Require(Mathf.Abs(slider.normalizedValue - value) < .03f &&
                ui.armValue.text == $"{Mathf.RoundToInt(slider.normalizedValue * 100)}%",
                $"Mouse drags arm slider to {value:P0}");
            Require(Vector3.Distance(before, arm.transform.localPosition) > .01f,
                "Arm physically follows slider drag");
        }

        static IEnumerable<double> CheckDialogs(FactoryTrainingUI ui, TimeManager timer)
        {
            var notice = ui.trainingDialogs[2];
            var continueButton = notice.transform.Find("Button").GetComponent<Button>();
            var mode = ui.cameraSwitcher.trainingSubMenu.transform.Find("subMenu_01/TrainingModeButton")
                .GetComponent<Button>();
            var resolutions = new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720),
                new Vector2Int(1024, 768) };
            ui.cameraButtons[0].onClick.Invoke();
            foreach (var resolution in resolutions)
            {
                foreach (var delay in RenderAt(resolution)) yield return delay;
                foreach (bool guided in new[] { true, false })
                {
                    ui.ResetAssembly();
                    yield return 1.4;
                    if (!guided) mode.onClick.Invoke();
                    foreach (var delay in WaitFrames()) yield return delay;
                    Require(ui.trainingMode.IsTrainingMode == guided,
                        $"Mode {(guided ? "guided" : "assessment")} at {resolution}");
                    CompleteCurrentStep(ui);
                    yield return .45;
                    Require(notice.activeInHierarchy && continueButton.gameObject.activeInHierarchy,
                        "Step notice and Continue visible");
                    CheckText(notice);
                    CheckInside((RectTransform)notice.transform,
                        continueButton.GetComponent<RectTransform>(), "Continue inside notice");
                    Capture($"notice-{(guided ? "guided" : "assessment")}-{resolution.x}");
                    foreach (var delay in WaitFrames()) yield return delay;
                    float before = TimerValue(timer);
                    float clickedAt = Time.unscaledTime;
                    foreach (var delay in Click(continueButton)) yield return delay;
                    Require(!notice.activeSelf || !notice.GetComponent<CanvasGroup>().blocksRaycasts,
                        "Continue immediately stops notice from intercepting input");
                    while (notice.activeSelf && Time.unscaledTime - clickedAt < .6f) yield return .02;
                    Require(!notice.activeSelf && Time.unscaledTime - clickedAt < .7f,
                        "Mouse click dismisses with short exit animation");
                    Require(TimerValue(timer) >= before, "Continue preserves next-step timer");

                    CompleteCurrentStep(ui);
                    float shownAt = Time.unscaledTime;
                    float lastVisible = shownAt;
                    float animationAt = -1;
                    bool sawFadeAndSlide = false;
                    var group = notice.GetComponent<CanvasGroup>();
                    var rect = notice.GetComponent<RectTransform>();
                    float shownY = rect.anchoredPosition.y;
                    before = TimerValue(timer);
                    while (notice.activeSelf && Time.unscaledTime - shownAt < 3.5f)
                    {
                        lastVisible = Time.unscaledTime;
                        if (!group.blocksRaycasts && animationAt < 0) animationAt = Time.unscaledTime;
                        if (group.alpha > 0 && group.alpha < 1 && rect.anchoredPosition.y < shownY)
                            sawFadeAndSlide = true;
                        yield return .02;
                    }
                    float duration = Time.unscaledTime - shownAt;
                    File.AppendAllText(Report,
                        $"TIMING mode={(guided ? "guided" : "assessment")} size={resolution} " +
                        $"exitStarted={animationAt - shownAt:F3}s lastVisible={lastVisible - shownAt:F3}s hidden={duration:F3}s\n");
                    Require(!notice.activeSelf && animationAt - shownAt >= 2.35f &&
                        animationAt - shownAt <= 2.65f && duration <= 3,
                        "Notice starts automatic dismissal around 2.4 unscaled seconds");
                    Require(duration - (animationAt - shownAt) >= .15f &&
                        duration - (animationAt - shownAt) <= .4f, "Exit animation lasts about 0.2 seconds");
                    Require(sawFadeAndSlide, "Exit visibly fades and slides down");
                    Require(TimerValue(timer) > before + 2.3f, "Automatic dismissal preserves next-step timer");
                }
            }

            foreach (bool guided in new[] { true, false })
            {
                ui.ResetAssembly();
                yield return 1.4;
                if (!guided) mode.onClick.Invoke();
                EventManager.TriggerEvent(EventTypes.POP_MODAL_PANEL_WITH_MESSAGE_ON_OBJECT,
                    ui.training.CurrentState.gameObject);
                yield return 1;
                EventManager.TriggerEvent(EventTypes.POP_MODAL_PANEL_WITH_MESSAGE_ON_OBJECT,
                    ui.training.CurrentState.gameObject);
                yield return 1.1;
                Require(notice.activeSelf, "Repeated notice renews dismissal deadline");
                yield return 1.7;
                Require(!notice.activeSelf, "Repeated notice automatically dismisses");
            }

            EventManager.TriggerEvent(EventTypes.POP_MODAL_PANEL_WITH_MESSAGE_ON_OBJECT,
                ui.training.CurrentState.gameObject);
            mode.onClick.Invoke();
            Require(!notice.activeSelf, "Mode change clears notice");
            EventManager.TriggerEvent(EventTypes.POP_MODAL_PANEL_WITH_MESSAGE_ON_OBJECT,
                ui.training.CurrentState.gameObject);
            ui.ResetAssembly();
            yield return 1.4;
            Require(!notice.activeSelf, "Reset clears notice");
            mode.onClick.Invoke();
            EventManager.TriggerEvent(EventTypes.POP_MODAL_PANEL_WITH_MESSAGE_ON_OBJECT,
                ui.training.CurrentState.gameObject);
            EventManager.TriggerEvent(EventTypes.ALLOWED_TIME_EXPIRED, ui.training.CurrentState.gameObject);
            yield return .3;
            Require(ui.trainingDialogs[1].activeInHierarchy && !notice.activeSelf,
                "Timeout clears notice");
            foreach (var delay in Click(ui.trainingDialogs[1].transform.Find("TrainingButton").GetComponent<Button>()))
                yield return delay;
            yield return 1.4;
            Require(ui.trainingMode.IsTrainingMode && !ui.trainingDialogs[1].activeSelf &&
                ui.training.GetStateTrackerText() == "1 / 15", "Timeout return resets assembly");

            for (int i = 0; i < 15; i++)
            {
                Require(ui.training.CurrentState == ui.training.GetTrainingStates()[i], "Complete step " + (i + 1));
                CompleteCurrentStep(ui);
                foreach (var delay in WaitFrames()) yield return delay;
            }
            var complete = ui.trainingDialogs[0];
            Require(complete.activeInHierarchy && !notice.activeSelf, "Completion clears final notice");
            foreach (var resolution in resolutions)
            {
                foreach (var delay in RenderAt(resolution)) yield return delay;
                Canvas.ForceUpdateCanvases();
                var surface = (RectTransform)complete.transform.Find("UI_Surface");
                var list = (RectTransform)complete.transform.Find("StepsTimePanel");
                var rows = list.GetComponentsInChildren<TMP_Text>();
                Require(rows.Length == 15, "All 15 completion rows visible");
                CheckInside(surface, list, "Completion list inside surface");
                Bounds? previous = null;
                for (int i = 0; i < rows.Length; i++)
                {
                    Require(rows[i].text.StartsWith($"{i + 1:00}   {FactoryTrainingUI.StepTitle(i)}") &&
                        rows[i].text.EndsWith("秒"), "Completion row order, name and time " + (i + 1));
                    CheckInside(list, rows[i].rectTransform, "Row inside list " + (i + 1));
                    var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(list, rows[i].transform);
                    Require(!previous.HasValue || bounds.max.y <= previous.Value.min.y + .1f,
                        "Completion rows do not overlap " + (i + 1));
                    previous = bounds;
                    Require(rows[i].preferredWidth <= rows[i].rectTransform.rect.width + 2,
                        "Completion text fits width " + (i + 1));
                }
                Require(complete.transform.Find("FinalTimeText").GetComponent<TMP_Text>().text
                    .StartsWith("装配总用时"), "Chinese completion total");
                CheckText(complete);
                var retry = complete.transform.Find("Button").GetComponent<RectTransform>();
                CheckInside(surface, retry, "Retry inside surface");
                Capture("completion-" + resolution.x);
                foreach (var delay in WaitFrames()) yield return delay;
            }
            foreach (var delay in Click(complete.transform.Find("Button").GetComponent<Button>()))
                yield return delay;
            yield return 1.4;
            Require(!complete.activeSelf && ui.trainingMode.IsTrainingMode &&
                ui.training.GetStateTrackerText() == "1 / 15", "Mouse click retries assembly");
        }

        static void CompleteCurrentStep(FactoryTrainingUI ui)
        {
            var state = (StateTrainingWithPass)ui.training.CurrentState;
            state.PartToMove.transform.position = state.GetTrainingData().correctFinalTransform.position;
            EventManager.TriggerEvent(EventTypes.TRAINING_STATE_COMPLETION_CHECK, state.PartToMove);
        }

        static IEnumerable<double> WaitFrames(int count = 3)
        {
            int frame = Time.frameCount;
            double deadline = EditorApplication.timeSinceStartup + 10;
            while (Time.frameCount < frame + count && EditorApplication.timeSinceStartup < deadline)
            {
                gameView?.Repaint();
                yield return .04;
            }
            Require(Time.frameCount >= frame + count,
                $"Game view rendered frames (start={frame}, current={Time.frameCount}, paused={EditorApplication.isPaused})");
        }

        static IEnumerable<double> RenderAt(Vector2Int resolution)
        {
            SetResolution(resolution.x, resolution.y);
            double deadline = EditorApplication.timeSinceStartup + 12;
            while ((Screen.width != resolution.x || Screen.height != resolution.y) &&
                EditorApplication.timeSinceStartup < deadline)
            {
                gameView.Repaint();
                yield return .1;
            }
            foreach (var delay in WaitFrames()) yield return delay;
            Require(Screen.width == resolution.x && Screen.height == resolution.y,
                $"Render size {resolution.x}x{resolution.y} (actual {Screen.width}x{Screen.height})");
        }

        static IEnumerable<double> Click(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = button.GetComponent<RectTransform>();
            var canvas = button.GetComponentInParent<Canvas>();
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            Require(raycaster && raycaster.isActiveAndEnabled && button.IsInteractable(),
                "Button has enabled GraphicRaycaster and is interactable");
            var pointer = new PointerEventData(EventSystem.current) { position = position };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0 &&
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
                "EventSystem top raycast hits " + button.name + "; actual=" +
                string.Join(", ", hits.Select(hit => FactoryTrainingUIEditor.PathOf(hit.gameObject.transform))));
            Require(Mouse.current != null, "Mouse available");
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = position }
                .WithButton(MouseButton.Left));
            foreach (var delay in WaitFrames(2)) yield return delay;
            InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = position });
            foreach (var delay in WaitFrames(2)) yield return delay;
        }

        static void CheckInside(RectTransform outer, RectTransform inner, string message)
        {
            var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(outer, inner);
            var rect = outer.rect;
            Require(bounds.min.x >= rect.xMin - .2f && bounds.max.x <= rect.xMax + .2f &&
                bounds.min.y >= rect.yMin - .2f && bounds.max.y <= rect.yMax + .2f, message);
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
            bool dialogsOnly = SessionState.GetBool(Key + ".DialogsOnly", false);
            string path = dialogsOnly
                ? "Evidence/factory-dialogs-" + name + "-20261030.png"
                : "Evidence/factory-ui-" + name + "-20261029.png";
            if (File.Exists(path))
                File.Move(path, path + "." + DateTime.Now.ToString("yyyyMMddHHmmssfff") + ".previous");
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
                if (Mouse.current != null)
                    InputSystem.QueueStateEvent(Mouse.current, new MouseState { position = Mouse.current.position.ReadValue() });
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
