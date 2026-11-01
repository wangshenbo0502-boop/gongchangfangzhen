using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WorkshopDemo
{
    // Installed only for an explicitly requested standalone acceptance run.
    public sealed class CombinedPlayerCheck : MonoBehaviour
    {
        string folder, report;
        int errors;
        IEnumerator steps;
        Mouse pointer;
        Keyboard keyboard;
        readonly List<InputDevice> disabledDevices = new List<InputDevice>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains("-verify-combined")) return;
            var runner = new GameObject("Combined Acceptance");
            DontDestroyOnLoad(runner);
            runner.AddComponent<CombinedPlayerCheck>();
        }

        void Awake()
        {
            folder = Path.GetFullPath("Evidence/Combined-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(folder);
            report = Path.Combine(folder, "player-verification.txt");
            File.WriteAllText(report, $"Time={DateTime.Now:O}\nUnity={Application.unityVersion}\n" +
                "Method=Visible standalone, Input System keyboard/mouse events, scene isolation, navigation and GPU captures.\n");
            Application.logMessageReceived += Log;
            Application.runInBackground = true;
            steps = Run();
            StartCoroutine(Advance());
        }

        void Log(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            errors++;
            File.AppendAllText(report, "RUNTIME ERROR " + message + "\n" + stack + "\n");
        }

        IEnumerator Advance()
        {
            float deadline = Time.realtimeSinceStartup + 300;
            while (true)
            {
                object next;
                try
                {
                    Require(Time.realtimeSinceStartup < deadline, "Acceptance deadline", false);
                    if (!steps.MoveNext()) break;
                    next = steps.Current;
                }
                catch (Exception exception)
                {
                    File.AppendAllText(report, "FAILED " + exception + "\n");
                    Application.Quit(1);
                    yield break;
                }
                yield return next;
            }
            File.AppendAllText(report, "COMPLETE\n");
            Application.Quit(0);
        }

        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(3);
            Require(!Application.isEditor && SceneManager.sceneCountInBuildSettings == 2, "Two-scene standalone");
            Require(SceneManager.GetActiveScene().path == WorkshopSceneSwitcher.AssemblyScene, "Assembly is startup scene");
            File.AppendAllText(report, $"Resolution={Screen.width}x{Screen.height}\n");
            foreach (var device in InputSystem.devices.ToArray())
                if (device is Mouse || device is Keyboard)
                {
                    disabledDevices.Add(device);
                    InputSystem.DisableDevice(device);
                }
            pointer = InputSystem.AddDevice<Mouse>("CombinedAcceptanceMouse");
            keyboard = InputSystem.AddDevice<Keyboard>("CombinedAcceptanceKeyboard");
            pointer.MakeCurrent();
            keyboard.MakeCurrent();

            for (int round = 0; round < 2; round++)
            {
                var assembly = FindFirstObjectByType<FactoryTrainingUI>();
                Require(assembly && !FindFirstObjectByType<WorkshopSimulation>(), "Isolated assembly round " + round);
                ValidateOwners();
                foreach (int view in new[] { 0, 1, 2, 3, 1 })
                {
                    foreach (var step in Click(assembly.cameraButtons[view])) yield return step;
                    Require(assembly.cameraSwitcher.cameras[view].activeSelf &&
                        assembly.cameraSwitcher.cameras.Count(c => c.activeSelf) == 1, "Assembly view " + view);
                }
                foreach (var step in Click(assembly.movement.startStopBTN)) yield return step;
                Require(assembly.movement.IsAutoRunning, "Assembly automatic movement starts");
                bool moved = false;
                float until = Time.realtimeSinceStartup + 2;
                while (Time.realtimeSinceStartup < until)
                {
                    moved |= assembly.movement.navAgentArr[0].velocity.sqrMagnitude > .01f;
                    yield return null;
                }
                Require(moved, "Assembly robot physically moves");
                foreach (var step in Press(Key.Digit1)) yield return step;
                Require(assembly && assembly.movement.IsAutoRunning, "Current scene key preserves assembly state");
                foreach (var step in Click(assembly.movement.startStopBTN)) yield return step;
                Require(!assembly.movement.IsAutoRunning, "Assembly automatic movement stops");
                yield return new WaitForEndOfFrame();
                Capture("assembly-" + round);
                foreach (var step in Press(Key.H)) yield return step;
                ValidateHidden();
                yield return new WaitForEndOfFrame();
                Capture("assembly-hidden-" + round);
                foreach (var step in Press(Key.H)) yield return step;
                Require(assembly.interfaceCanvases.Any(c => c && c.enabled), "Assembly H restores UI");
                foreach (var step in Press(Key.H)) yield return step;
                // Loading must work even if the previous scene was paused or muted.
                Time.timeScale = 0;
                AudioListener.volume = 0;
                foreach (var step in Switch(round == 0 ? Key.Digit2 : Key.Numpad2, WorkshopSceneSwitcher.InspectionScene))
                    yield return step;
                Require(!assembly, "Old assembly objects destroyed");
                var simulation = FindFirstObjectByType<WorkshopSimulation>();
                Require(simulation && simulation.RunningCount == 8 && simulation.Alarms.Count == 0 &&
                    simulation.Records.Count == 0 && !FindFirstObjectByType<FactoryTrainingUI>(), "Fresh isolated inspection");
                ValidateOwners();
                Require(Time.timeScale == 1 && AudioListener.volume == 1, "Scene switch resets pause and mute");
                var ui = FindFirstObjectByType<WorkshopUI>();
                Require(ui.GetComponent<Canvas>().enabled, "Inspection UI visible after assembly H");
                foreach (var device in simulation.devices)
                {
                    var path = new NavMeshPath();
                    Require(simulation.inspector.Agent.CalculatePath(device.inspectionPoint, path) &&
                        path.status == NavMeshPathStatus.PathComplete, "Inspection path " + device.deviceId);
                }
                foreach (var step in Click(Button("手动驾驶"))) yield return step;
                Vector3 start = simulation.inspector.transform.position;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
                yield return new WaitForSecondsRealtime(.5f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return new WaitForSecondsRealtime(.1f);
                Require(simulation.inspector.ManualControl &&
                    Vector3.Distance(start, simulation.inspector.transform.position) > .7f, "Inspection manual driving works");
                foreach (var step in Click(Button("自动巡检"))) yield return step;
                foreach (var step in Click(Button("触发设备故障"))) yield return step;
                foreach (var step in Press(Key.Digit2)) yield return step;
                Require(simulation && simulation.OpenAlarmCount == 1, "Current scene key preserves inspection state");
                foreach (var step in Click(Button("派发巡检"))) yield return step;
                Require(simulation.inspector.IsBusy && !simulation.inspector.ManualControl, "Inspection task dispatched");
                if (round == 0)
                {
                    float arrivalDeadline = Time.realtimeSinceStartup + 90;
                    while (!simulation.inspector.HasArrived && Time.realtimeSinceStartup < arrivalDeadline)
                        yield return null;
                    Require(simulation.CanResolve, "Inspection physically arrives after scene switch");
                    foreach (var step in Click(Button("确认处理"))) yield return step;
                    Require(simulation.Records.Count == 1 && simulation.RunningCount == 8, "Inspection fault recovered once");
                }
                yield return new WaitForEndOfFrame();
                Capture("inspection-" + round);
                foreach (var step in Press(Key.H)) yield return step;
                ValidateHidden();
                Require(simulation.cameraRig.GetComponent<Camera>().rect == new Rect(0, 0, 1, 1),
                    "Inspection hidden view fills window");
                yield return new WaitForEndOfFrame();
                Capture("inspection-hidden-" + round);
                foreach (var step in Press(Key.H)) yield return step;
                Require(ui.GetComponent<Canvas>().enabled && ui.GetComponent<GraphicRaycaster>().enabled &&
                    simulation.cameraRig.GetComponent<Camera>().rect.width < 1 &&
                    simulation.devices.All(d => d.worldLabel.GetComponent<Renderer>().enabled), "Inspection H restores UI, labels and viewport");
                foreach (var step in Press(Key.H)) yield return step;
                foreach (var step in Switch(round == 0 ? Key.Digit1 : Key.Numpad1, WorkshopSceneSwitcher.AssemblyScene))
                    yield return step;
                Require(!simulation && !ui, "Inspection task and UI destroyed on switch");
                assembly = FindFirstObjectByType<FactoryTrainingUI>();
                Require(assembly && assembly.interfaceCanvases.Any(c => c && c.enabled) &&
                    assembly.cameraSwitcher.cameras[0].activeSelf, "Assembly returns with visible UI and initial view");
            }
            ValidateOwners();
            Require(errors == 0, "No runtime errors");
        }

        void ValidateOwners()
        {
            Require(FindObjectsByType<WorkshopSceneSwitcher>(FindObjectsSortMode.None).Length == 1, "One persistent switcher");
            Require(SceneManager.sceneCount == 1 &&
                FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "One loaded scene and EventSystem");
        }

        void ValidateHidden()
        {
            Require(FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(c => !c.enabled) &&
                FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Include, FindObjectsSortMode.None).All(r => !r.enabled) &&
                FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .All(t => !t.GetComponent<Renderer>().enabled), "H hides every canvas, raycaster and world label");
        }

        IEnumerable<object> Switch(Key key, string path)
        {
            foreach (var step in Press(key)) yield return step;
            float until = Time.realtimeSinceStartup + 30;
            while ((SceneManager.GetActiveScene().path != path ||
                FindFirstObjectByType<WorkshopSceneSwitcher>().IsSwitching) && Time.realtimeSinceStartup < until)
                yield return null;
            Require(SceneManager.GetActiveScene().path == path, "Keyboard switches " + key + " to " + path);
            yield return new WaitForSecondsRealtime(1);
        }

        IEnumerable<object> Press(Key key)
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
            yield return new WaitForSecondsRealtime(.12f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(.12f);
        }

        static Button Button(string label) => FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Single(b => b.GetComponentsInChildren<Text>().Any(t => t.text == label));

        IEnumerable<object> Click(Button button)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Require(button.IsInteractable() && hits.Count > 0 &&
                hits[0].gameObject.GetComponentInParent<Button>() == button, "Unobstructed " + button.name, false);
            InputSystem.QueueStateEvent(pointer, new MouseState { position = position });
            yield return new WaitForSecondsRealtime(.12f);
            InputSystem.QueueStateEvent(pointer, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return new WaitForSecondsRealtime(.12f);
            InputSystem.QueueStateEvent(pointer, new MouseState { position = position });
            yield return new WaitForSecondsRealtime(.18f);
        }

        void Capture(string name)
        {
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
            var pixels = texture.GetPixels32();
            int low = 255, high = 0;
            for (int y = texture.height / 3; y < texture.height * 2 / 3; y += 9)
                for (int x = texture.width / 3; x < texture.width * 2 / 3; x += 9)
                {
                    var pixel = pixels[y * texture.width + x];
                    int value = (pixel.r + pixel.g + pixel.b) / 3;
                    low = Math.Min(low, value);
                    high = Math.Max(high, value);
                }
            Destroy(texture);
            Require(high - low > 50, "Nonblank 3D capture " + name);
        }

        void Require(bool condition, string message, bool record = true)
        {
            if (!condition) throw new InvalidOperationException(message);
            if (record) File.AppendAllText(report, "PASS " + message + "\n");
        }

        void OnDestroy()
        {
            Application.logMessageReceived -= Log;
            if (pointer != null && pointer.added) InputSystem.RemoveDevice(pointer);
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            foreach (var device in disabledDevices) if (device.added) InputSystem.EnableDevice(device);
        }
    }
}
