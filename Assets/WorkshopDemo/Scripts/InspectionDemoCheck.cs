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
    // Only installed by an explicit command-line switch in a dedicated acceptance player.
    public sealed class InspectionDemoCheck : MonoBehaviour
    {
        string folder, report;
        int errors;
        WorkshopSimulation simulation;
        IEnumerator steps;
        Mouse pointer;
        Keyboard keyboard;
        readonly List<InputDevice> disabledDevices = new List<InputDevice>();
        float deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains("-verify-inspection")) return;
            var runner = new GameObject("Inspection Acceptance");
            DontDestroyOnLoad(runner);
            runner.AddComponent<InspectionDemoCheck>();
        }

        void Awake()
        {
            folder = Path.GetFullPath("Evidence/InspectionV2-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(folder);
            report = Path.Combine(folder, "player-verification.txt");
            File.WriteAllText(report, $"Time={DateTime.Now:O}\nUnity={Application.unityVersion}\n" +
                "Method=Queued Input System mouse/keyboard events, UI raycasts, business assertions, actual navigation and GPU captures.\n");
            Application.logMessageReceived += Log;
            Application.runInBackground = true;
            deadline = Time.realtimeSinceStartup + 900;
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
            while (true)
            {
                object next = null;
                bool failed = false, finished = false;
                try
                {
                    Require(Time.realtimeSinceStartup < deadline, "Acceptance deadline", false);
                    finished = !steps.MoveNext();
                    if (!finished) next = steps.Current;
                }
                catch (Exception exception)
                {
                    File.AppendAllText(report, "FAILED " + exception + "\n");
                    Application.Quit(1);
                    failed = true;
                }
                if (failed) yield break;
                if (finished) break;
                yield return next;
            }
            File.AppendAllText(report, "COMPLETE\n");
            Application.Quit(0);
        }

        IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(3);
            File.AppendAllText(report, $"Scene={SceneManager.GetActiveScene().path}\nResolution={Screen.width}x{Screen.height}\n");
            // Keep physical input from stealing the current device during this opt-in test.
            foreach (var device in InputSystem.devices.ToArray())
                if (device is Mouse || device is Keyboard)
                {
                    disabledDevices.Add(device);
                    InputSystem.DisableDevice(device);
                }
            pointer = InputSystem.AddDevice<Mouse>("InspectionAcceptanceMouse");
            keyboard = InputSystem.AddDevice<Keyboard>("InspectionAcceptanceKeyboard");
            pointer.MakeCurrent();
            keyboard.MakeCurrent();
            simulation = FindFirstObjectByType<WorkshopSimulation>();
            Require(simulation && simulation.devices.Length == 8 && simulation.RunningCount == 8, "Eight devices running");
            Require(FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1, "One EventSystem");
            var conveyor = simulation.devices.First(d => d.kind == DeviceKind.Conveyor);
            var fan = simulation.devices.First(d => d.kind == DeviceKind.Fan);
            Require(conveyor.movingPackages.Length > 0 && fan.rotor, "Animation references");
            Vector3 package = conveyor.movingPackages[0].position;
            Quaternion rotor = fan.rotor.localRotation;
            yield return new WaitForSecondsRealtime(.5f);
            Require(Vector3.Distance(package, conveyor.movingPackages[0].position) > .1f &&
                Quaternion.Angle(rotor, fan.rotor.localRotation) > 1, "Conveyor and fan animate");
            foreach (var device in simulation.devices)
            {
                var path = new NavMeshPath();
                Require(NavMesh.SamplePosition(device.inspectionPoint, out var hit, 1, NavMesh.AllAreas) &&
                    simulation.inspector.Agent.CalculatePath(hit.position, path) &&
                    path.status == NavMeshPathStatus.PathComplete, "Complete path " + device.deviceId);
                foreach (var step in Click(DeviceButton(device))) yield return step;
                Require(simulation.Selected == device && device.selectionRing.activeSelf, "Mouse selects " + device.deviceId);
            }
            foreach (var step in Click(Button("停止设备"))) yield return step;
            Require(simulation.Selected.Status == DeviceStatus.Stopped, "Mouse stops selected device");
            foreach (var step in Click(Button("启动设备"))) yield return step;
            Require(simulation.Selected.IsRunning, "Mouse starts selected device");
            foreach (var step in Click(Button("总览"))) yield return step;
            yield return new WaitForSecondsRealtime(1);
            ValidateLayout();
            yield return new WaitForEndOfFrame();
            Capture("overview");

            foreach (string id in simulation.devices.Select(d => d.deviceId))
            {
                foreach (var step in Click(DeviceButton(simulation.devices.Single(d => d.deviceId == id)))) yield return step;
                yield return new WaitForSecondsRealtime(.7f);
                yield return new WaitForEndOfFrame();
                Capture("equipment-" + id);
            }

            var canvas = FindFirstObjectByType<WorkshopUI>().GetComponent<Canvas>();
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.H));
            yield return null;
            Require(!canvas.enabled && !raycaster.enabled, "H hides UI and disables UI raycasts");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.H));
            yield return null;
            Require(canvas.enabled && raycaster.enabled, "H restores UI and raycasts");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Canvas.ForceUpdateCanvases();

            var inspector = simulation.inspector;
            foreach (var step in Click(Button("手动驾驶"))) yield return step;
            Require(inspector.ManualControl, "Mouse enables manual driving");
            Vector3 start = inspector.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
            yield return new WaitForSecondsRealtime(.5f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(.15f);
            Vector3 forward = inspector.transform.position;
            Require(Vector3.Distance(start, forward) > .7f, "Up arrow physically drives forward");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.DownArrow));
            yield return new WaitForSecondsRealtime(.4f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(.15f);
            Require(Vector3.Distance(inspector.transform.position, start) < Vector3.Distance(forward, start),
                "Down arrow physically reverses");
            Quaternion heading = inspector.transform.rotation;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow));
            yield return new WaitForSecondsRealtime(.4f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(.15f);
            Require(Quaternion.Angle(heading, inspector.transform.rotation) > 20, "Right arrow turns robot");
            heading = inspector.transform.rotation;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftArrow));
            yield return new WaitForSecondsRealtime(.4f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(.15f);
            Require(Quaternion.Angle(heading, inspector.transform.rotation) > 20, "Left arrow turns robot");
            foreach (var step in Click(Button("自动巡检"))) yield return step;
            Require(!inspector.ManualControl, "Mouse returns to automatic mode");

            if (Environment.GetCommandLineArgs().Contains("-inspection-smoke"))
            {
                ValidateLayout();
                Require(errors == 0, "No runtime errors");
                yield break;
            }

            foreach (var step in Click(DeviceButton(simulation.devices[0]))) yield return step;
            foreach (var step in Click(Button("触发设备故障"))) yield return step;
            var first = simulation.ActiveAlarm;
            Require(first != null && first.device == simulation.devices[0], "Fault applies to selected device");
            foreach (var step in Click(Button("派发巡检"))) yield return step;
            Require(inspector.TargetAlarm == first && inspector.IsBusy, "Mouse dispatches first task");
            foreach (var step in Click(Button("手动驾驶"))) yield return step;
            Vector3 paused = inspector.transform.position;
            yield return new WaitForSecondsRealtime(.5f);
            Require(inspector.ManualControl && inspector.TargetAlarm == first &&
                Vector3.Distance(paused, inspector.transform.position) < .05f,
                $"Manual takeover pauses and preserves task (manual={inspector.ManualControl}, " +
                $"target={inspector.TargetAlarm?.id}, drift={Vector3.Distance(paused, inspector.transform.position):F3})");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.UpArrow));
            yield return new WaitForSecondsRealtime(.3f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(.15f);
            Require(Vector3.Distance(paused, inspector.transform.position) > .3f, "Manual driving during active task");
            foreach (var device in simulation.devices.Skip(1))
            {
                foreach (var step in Click(DeviceButton(device))) yield return step;
                foreach (var step in Click(Button("触发设备故障"))) yield return step;
                Require(device.Status == DeviceStatus.Fault && device.faultMarker.activeSelf && device.Rpm == 0,
                    "Fault and protective stop " + device.deviceId);
            }
            Require(simulation.OpenAlarmCount == 8 && simulation.RunningCount == 0 &&
                inspector.TargetAlarm == first && simulation.ActiveAlarm == first, "Eight concurrent faults preserve in-flight target");
            package = conveyor.movingPackages[0].position;
            rotor = fan.rotor.localRotation;
            yield return new WaitForSecondsRealtime(.3f);
            Require(Vector3.Distance(package, conveyor.movingPackages[0].position) < .001f &&
                Quaternion.Angle(rotor, fan.rotor.localRotation) < .01f, "Fault stops conveyor and fan animation");
            simulation.TriggerFault();
            simulation.Resolve();
            simulation.Dispatch();
            Require(simulation.Alarms.Count == 8 && simulation.Records.Count == 0 && !Button("确认处理").interactable,
                "Duplicate fault, duplicate dispatch and early resolve blocked");
            foreach (var step in Click(Button("总览"))) yield return step;
            yield return new WaitForSecondsRealtime(.7f);
            yield return new WaitForEndOfFrame();
            Capture("eight-faults");
            foreach (var step in Click(Button("自动巡检"))) yield return step;
            Require(!inspector.ManualControl && inspector.TargetAlarm == first && inspector.Agent.hasPath,
                "Automatic resume recalculates preserved task path");
            foreach (var step in Click(Button("巡检跟随"))) yield return step;
            Require(simulation.cameraRig.Following, "Mouse enables follow camera");
            Time.timeScale = 3;
            File.AppendAllText(report, "Navigation time scale=3 (acceptance only)\n");
            var visited = new HashSet<string>();
            for (int i = 0; i < 8; i++)
            {
                var alarm = inspector.TargetAlarm;
                Require(alarm != null && visited.Add(alarm.device.deviceId), "Unique task " + alarm?.device.deviceId);
                float arrivalDeadline = Time.realtimeSinceStartup + 50;
                while (!inspector.HasArrived && Time.realtimeSinceStartup < arrivalDeadline) yield return null;
                Require(inspector.HasArrived && simulation.CanResolve &&
                    Vector3.Distance(inspector.transform.position, alarm.device.inspectionPoint) < .7f,
                    "Physically arrived " + alarm.device.deviceId);
                ValidateLayout();
                if (i == 0)
                {
                    yield return new WaitForEndOfFrame();
                    Capture("arrived");
                }
                foreach (var step in Click(Button("确认处理"))) yield return step;
                simulation.Resolve();
                Require(alarm.resolved && alarm.device.IsRunning && !alarm.device.faultMarker.activeSelf &&
                    simulation.Records.Count == i + 1 && !inspector.IsBusy,
                    "Recovered once " + alarm.device.deviceId);
                if (i == 7) break;
                var next = simulation.Alarms.Last(a => !a.resolved);
                foreach (var step in Click(DeviceButton(next.device))) yield return step;
                foreach (var step in Click(Button("派发巡检"))) yield return step;
                Require(inspector.TargetAlarm == next, "Selected alarm dispatched " + next.device.deviceId);
            }
            Time.timeScale = 1;
            Require(simulation.OpenAlarmCount == 0 && simulation.RunningCount == 8 &&
                simulation.Records.Select(r => r.alarmId).Distinct().Count() == 8, "All eight faults resolved with unique records");
            foreach (var step in Click(Button("巡检记录"))) yield return step;
            ValidateLayout();
            yield return new WaitForEndOfFrame();
            Capture("records");
            foreach (var step in Click(Button("重置演示"))) yield return step;
            foreach (var step in Click(Button("重置演示"))) yield return step;
            Require(simulation.Alarms.Count == 0 && simulation.Records.Count == 0 && !inspector.IsBusy &&
                !inspector.ManualControl && simulation.RunningCount == 8, "Consecutive resets clear state and mode");
            simulation.TriggerFault();
            simulation.Dispatch();
            yield return new WaitForSecondsRealtime(.3f);
            simulation.ResetDemo();
            Require(!inspector.IsBusy && !inspector.Agent.hasPath && simulation.OpenAlarmCount == 0, "Reset cancels moving task");
            Require(errors == 0, "No runtime errors");
        }

        static Button[] Buttons() => FindObjectsByType<Button>(FindObjectsSortMode.None);
        static Button Button(string label) => Buttons().Single(b => b.GetComponentsInChildren<Text>().Any(t => t.text == label));
        static Button DeviceButton(WorkshopDevice device) => Buttons().Single(b => b.name == "Device " + device.deviceId);

        IEnumerable<object> Click(Button button)
        {
            Require(button.interactable, "Clickable " + button.name, false);
            var rect = (RectTransform)button.transform;
            Vector2 position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Require(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == button,
                "Unobstructed " + button.name, false);
            InputSystem.QueueStateEvent(pointer, new MouseState { position = position });
            yield return new WaitForSecondsRealtime(.12f);
            InputSystem.QueueStateEvent(pointer, new MouseState { position = position }.WithButton(MouseButton.Left));
            yield return new WaitForSecondsRealtime(.12f);
            InputSystem.QueueStateEvent(pointer, new MouseState { position = position });
            yield return new WaitForSecondsRealtime(.18f);
        }

        void ValidateLayout()
        {
            Canvas.ForceUpdateCanvases();
            foreach (var button in Buttons())
            {
                if (button.GetComponentInParent<ScrollRect>()) continue;
                var corners = new Vector3[4];
                ((RectTransform)button.transform).GetWorldCorners(corners);
                Require(corners[0].x >= -1 && corners[0].y >= -1 &&
                    corners[2].x <= Screen.width + 1 && corners[2].y <= Screen.height + 1,
                    "Button fits screen " + button.name, false);
            }
            foreach (var text in FindObjectsByType<Text>(FindObjectsSortMode.None))
                Require(text.font && text.preferredHeight <= text.rectTransform.rect.height + .5f,
                    "Text fits " + text.text + $" height={text.preferredHeight}/{text.rectTransform.rect.height}", false);
            var view = simulation.cameraRig.GetComponent<Camera>().pixelRect;
            Require(view.width > 400 && view.height > 300, "Text fits and camera viewport usable");
        }

        void Capture(string name)
        {
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), texture.EncodeToPNG());
            var rect = simulation.cameraRig.GetComponent<Camera>().pixelRect;
            var colors = texture.GetPixels32();
            int low = 255, high = 0;
            for (int y = (int)rect.yMin + 10; y < rect.yMax - 10; y += 9)
                for (int x = (int)rect.xMin + 10; x < rect.xMax - 10; x += 9)
                {
                    var c = colors[y * texture.width + x];
                    int value = (c.r + c.g + c.b) / 3;
                    low = Math.Min(low, value);
                    high = Math.Max(high, value);
                }
            Destroy(texture);
            Require(high - low > 50, "Nonblank camera capture " + name);
            File.AppendAllText(report, "SCREENSHOT " + name + ".png\n");
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
