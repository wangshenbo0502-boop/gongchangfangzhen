using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DistributionCenter;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WorkshopDemo
{
    // Only installed when the standalone is explicitly launched for acceptance.
    public sealed class AssemblyPlayerCheck : MonoBehaviour
    {
        string folder;
        string report;
        int errors;
        Mouse pointer;
        IEnumerator steps;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Install()
        {
            if (!Environment.GetCommandLineArgs().Contains("-verify-assembly")) return;
            var runner = new GameObject("Assembly Acceptance");
            DontDestroyOnLoad(runner);
            runner.AddComponent<AssemblyPlayerCheck>();
        }

        void Awake()
        {
            folder = Path.GetFullPath("Evidence/AssemblyAcceptance");
            Directory.CreateDirectory(folder);
            report = Path.Combine(folder, "player-verification.txt");
            File.WriteAllText(report, $"Time={DateTime.Now:O}\nUnity={Application.unityVersion}\n" +
                "Scope=Windows player startup, four views, automatic movement and arm slider via Input System mouse events. Full assembly training is not covered.\n");
            Application.runInBackground = true;
            Application.logMessageReceived += Log;
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
            float deadline = Time.realtimeSinceStartup + 180;
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
            File.AppendAllText(report, "LoadedScene=" + SceneManager.GetActiveScene().path + "\n");
            Require(!Application.isEditor, "Standalone player");
            var ui = FindFirstObjectByType<FactoryTrainingUI>();
            Require(ui && ui.cameraButtons.Length == 4, "Original assembly UI loaded");
            Require(!FindFirstObjectByType<WorkshopSimulation>(), "Inspection Demo absent");
            pointer = InputSystem.AddDevice<Mouse>("AssemblyAcceptanceMouse");
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
            {
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(1);
                Require(Screen.width == size.x && Screen.height == size.y, "Resolution " + size);
                foreach (int index in new[] { 0, 1, 2, 3, 1 })
                {
                    foreach (var step in Click(ui.cameraButtons[index])) yield return step;
                    Require(ui.cameraSwitcher.cameras[index].activeSelf &&
                        ui.cameraSwitcher.cameras.Count(camera => camera.activeSelf) == 1,
                        "Mouse switches view " + index);
                    ScreenCapture.CaptureScreenshot(Path.Combine(folder, $"view-{index}-{size.x}.png"));
                    yield return new WaitForEndOfFrame();
                }
                foreach (var step in Click(ui.movement.startStopBTN)) yield return step;
                Require(ui.movement.IsAutoRunning, "Mouse starts automatic movement");
                bool moved = false;
                float until = Time.realtimeSinceStartup + 2;
                while (Time.realtimeSinceStartup < until)
                {
                    moved |= ui.movement.navAgentArr[0].velocity.sqrMagnitude > .01f;
                    yield return null;
                }
                Require(moved, "Robot physically moves");
                foreach (var step in Click(ui.movement.startStopBTN)) yield return step;
                Require(!ui.movement.IsAutoRunning, "Mouse stops automatic movement");
                foreach (float value in new[] { .75f, .25f })
                {
                    var slider = ui.armSlider;
                    var translator = FindObjectsByType<GOTranslator>(FindObjectsSortMode.None)
                        .Single(controller => controller.translationSlider == slider);
                    var arm = translator.GOTranslator_ARRAY.First(part => part);
                    Vector3 before = arm.transform.localPosition;
                    var area = (RectTransform)slider.handleRect.parent;
                    Vector2 start = Point(slider.handleRect);
                    Vector2 end = RectTransformUtility.WorldToScreenPoint(null,
                        area.TransformPoint(new Vector3(Mathf.Lerp(area.rect.xMin, area.rect.xMax, value),
                            area.rect.center.y, 0)));
                    foreach (var step in PointerEvent(start, false)) yield return step;
                    foreach (var step in PointerEvent(start, true)) yield return step;
                    for (int i = 1; i <= 6; i++)
                        foreach (var step in PointerEvent(Vector2.Lerp(start, end, i / 6f), true))
                            yield return step;
                    foreach (var step in PointerEvent(end, false)) yield return step;
                    Require(Mathf.Abs(slider.normalizedValue - value) < .03f &&
                        Vector3.Distance(before, arm.transform.localPosition) > .01f,
                        "Mouse drags arm slider and arm follows " + value);
                }
            }
            Require(errors == 0, "No runtime errors");
        }

        static Vector2 Point(RectTransform rect) =>
            RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));

        IEnumerable<object> Click(Button button)
        {
            Vector2 position = Point((RectTransform)button.transform);
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = position }, hits);
            Require(button.IsInteractable() && hits.Count > 0 &&
                hits[0].gameObject.GetComponentInParent<Button>() == button, "Unobstructed " + button.name, false);
            foreach (var step in PointerEvent(position, false)) yield return step;
            foreach (var step in PointerEvent(position, true)) yield return step;
            foreach (var step in PointerEvent(position, false)) yield return step;
        }

        IEnumerable<object> PointerEvent(Vector2 position, bool down)
        {
            var state = new MouseState { position = position };
            if (down) state = state.WithButton(MouseButton.Left);
            InputSystem.QueueStateEvent(pointer, state);
            yield return new WaitForSecondsRealtime(.15f);
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
