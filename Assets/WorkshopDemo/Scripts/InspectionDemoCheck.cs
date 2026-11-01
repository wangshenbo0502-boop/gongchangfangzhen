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
    // Explicit opt-in for the standalone acceptance run; absent in normal sessions.
    public sealed class InspectionDemoCheck : MonoBehaviour
    {
        string folder;
        string report;
        int errors;
        WorkshopSimulation simulation;
        IEnumerator steps;
        float deadline;
        Mouse pointer;

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
            folder = Path.GetFullPath("Evidence/InspectionAcceptance");
            Directory.CreateDirectory(folder);
            report = Path.Combine(folder, "player-verification.txt");
            File.WriteAllText(report, $"Time={DateTime.Now:O}\nScene={SceneManager.GetActiveScene().path}\n" +
                $"Unity={Application.unityVersion} Editor={Application.isEditor}\n" +
                "Method=Real Input System mouse events, EventSystem raycasts, complete NavMesh paths, state assertions and GPU captures.\n");
            Application.logMessageReceived += Log;
            Application.runInBackground = true;
            deadline = Time.realtimeSinceStartup + 240;
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
                bool failed = false;
                bool finished = false;
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
            yield return new WaitForSecondsRealtime(2);
            File.AppendAllText(report, "LoadedScene=" + SceneManager.GetActiveScene().path + "\n");
            pointer = InputSystem.AddDevice<Mouse>("InspectionAcceptanceMouse");
            simulation = FindFirstObjectByType<WorkshopSimulation>();
            Require(simulation && simulation.devices.Length == 8 && simulation.RunningCount == 8,
                "Eight devices running");
            Require(FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length == 1,
                "One active EventSystem");
            var conveyor = simulation.devices.First(d => d.kind == DeviceKind.Conveyor);
            var fan = simulation.devices.First(d => d.kind == DeviceKind.Fan);
            Require(conveyor.movingPackages.Length > 0 && fan.rotor, "Animation references");
            Vector3 package = conveyor.movingPackages[0].position;
            Quaternion rotor = fan.rotor.localRotation;
            yield return new WaitForSecondsRealtime(.7f);
            Require(Vector3.Distance(package, conveyor.movingPackages[0].position) > .05f &&
                Quaternion.Angle(rotor, fan.rotor.localRotation) > .1f, "Conveyor and fan move");

            foreach (var device in simulation.devices)
            {
                var path = new NavMeshPath();
                Require(NavMesh.SamplePosition(device.inspectionPoint, out var hit, 1, NavMesh.AllAreas) &&
                    simulation.inspector.Agent.CalculatePath(hit.position, path) &&
                    path.status == NavMeshPathStatus.PathComplete, "Complete path " + device.deviceId);
            }

            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
            {
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(1);
                Require(Screen.width == size.x && Screen.height == size.y, "Resolution " + size);
                Canvas.ForceUpdateCanvases();
                ValidateLayout();

                foreach (var device in simulation.devices)
                {
                    var button = Buttons().First(b => b.GetComponentsInChildren<Text>()
                        .Any(t => t.text.StartsWith(device.deviceId + "  ")));
                    foreach (var step in Click(button)) yield return step;
                    Require(simulation.Selected == device && device.selectionRing.activeSelf,
                        "Mouse selects " + device.deviceId);
                }
                foreach (var step in Click(Button("停止设备"))) yield return step;
                Require(simulation.Selected.Status == DeviceStatus.Stopped, "Mouse stops device");
                foreach (var step in Click(Button("启动设备"))) yield return step;
                Require(simulation.Selected.IsRunning, "Mouse starts device");
                foreach (var step in Click(Button("总览"))) yield return step;
                yield return new WaitForSecondsRealtime(1);
                Capture("overview-" + size.x);
                yield return new WaitForEndOfFrame();

                foreach (var step in Click(Button("触发演示故障"))) yield return step;
                var alarm = simulation.ActiveAlarm;
                Require(alarm != null && alarm.device.deviceId == "P-01" &&
                    alarm.device.Status == DeviceStatus.Fault && alarm.device.Rpm == 0 &&
                    alarm.device.faultMarker.activeSelf && simulation.RunningCount == 7,
                    "Mouse triggers visible P-01 fault");
                simulation.TriggerFault();
                simulation.Resolve();
                Require(simulation.OpenAlarmCount == 1 && simulation.Records.Count == 0 &&
                    !Button("确认处理").interactable, "Duplicate fault and early resolve blocked");
                foreach (var step in Click(Button("派发巡检"))) yield return step;
                simulation.Dispatch();
                Require(simulation.inspector.IsBusy && !Button("派发巡检").interactable,
                    "Mouse dispatches once");
                foreach (var step in Click(Button("巡检跟随"))) yield return step;
                Require(simulation.cameraRig.Following, "Follow camera enabled");
                Vector3 start = simulation.inspector.transform.position;
                float timeout = Time.realtimeSinceStartup + 45;
                while (!simulation.inspector.HasArrived && Time.realtimeSinceStartup < timeout)
                    yield return null;
                Require(simulation.inspector.HasArrived && simulation.CanResolve &&
                    Vector3.Distance(start, simulation.inspector.transform.position) > 1 &&
                    Vector3.Distance(simulation.inspector.transform.position,
                        simulation.inspector.Agent.destination) <= .6f,
                    "Robot physically arrives at inspection point");
                Require(Button("确认处理").interactable && alarm.progress == "待处理",
                    "Arrival enables confirmation");
                ValidateLayout();
                Capture("arrived-" + size.x);
                yield return new WaitForEndOfFrame();
                foreach (var step in Click(Button("确认处理"))) yield return step;
                simulation.Resolve();
                Require(simulation.OpenAlarmCount == 0 && simulation.RunningCount == 8 &&
                    simulation.Records.Count == 1 && !simulation.inspector.IsBusy &&
                    !alarm.device.faultMarker.activeSelf, "Recovery and exactly one record");
                foreach (var step in Click(Button("巡检记录"))) yield return step;
                Require(FindObjectsByType<Text>(FindObjectsSortMode.None)
                    .Any(t => t.text.Contains("完成冷却检查")), "Record visible in UI");
                ValidateLayout();
                Capture("record-" + size.x);
                yield return new WaitForEndOfFrame();
                foreach (var step in Click(Button("重置演示"))) yield return step;
                foreach (var step in Click(Button("重置演示"))) yield return step;
                Require(simulation.Alarms.Count == 0 && simulation.Records.Count == 0 &&
                    simulation.RunningCount == 8 && !simulation.inspector.IsBusy &&
                    !simulation.cameraRig.Following, "Consecutive resets clear state");
                foreach (var step in Click(Button("告警"))) yield return step;
            }
            // A reset in transit must cancel navigation as well as clear the alarm.
            simulation.TriggerFault();
            simulation.Dispatch();
            yield return new WaitForSecondsRealtime(.5f);
            simulation.ResetDemo();
            yield return new WaitForSecondsRealtime(.4f);
            Require(!simulation.inspector.IsBusy && !simulation.inspector.Agent.hasPath &&
                simulation.RunningCount == 8, "Reset cancels in-flight task");
            Require(errors == 0, "No runtime errors");
        }

        static Button[] Buttons() => FindObjectsByType<Button>(FindObjectsSortMode.None);
        static Button Button(string label) => Buttons().Single(b =>
            b.GetComponentsInChildren<Text>().Any(t => t.text == label));

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
            foreach (var button in Buttons())
            {
                var corners = new Vector3[4];
                ((RectTransform)button.transform).GetWorldCorners(corners);
                Require(corners[0].x >= -1 && corners[0].y >= -1 &&
                    corners[2].x <= Screen.width + 1 && corners[2].y <= Screen.height + 1,
                    "Button fits screen: " + button.name, false);
            }
            foreach (var text in FindObjectsByType<Text>(FindObjectsSortMode.None))
            {
                Require(text.font && text.preferredHeight <= text.rectTransform.rect.height + 3,
                    "Text fits: " + text.text, false);
            }
            var view = simulation.cameraRig.GetComponent<Camera>().pixelRect;
            Require(view.width > 400 && view.height > 300, "Layout and usable camera viewport");
        }

        void Capture(string name)
        {
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
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
        }
    }
}
