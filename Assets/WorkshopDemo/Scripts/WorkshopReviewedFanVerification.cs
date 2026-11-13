using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace WorkshopDemo
{
    // Opt-in checks run inside the delivered player; normal startup never installs this object.
    public sealed class WorkshopReviewedFanVerification : MonoBehaviour
    {
        [Serializable] sealed class Motion { public float t; public Vector3 offset; }
        [Serializable] sealed class Part { public string name; public Motion[] path; }
        [Serializable] sealed class Manifest { public Part[] parts; }
        [Serializable] sealed class Geometry
        {
            public string date;
            public Vector3 axisX, axisY, axisZ;
            public GeometryMesh[] meshes;
        }
        [Serializable] sealed class GeometryMesh
        {
            public string name;
            public int part;
            public Vector3[] vertices;
            public int[] triangles;
        }
        string folder;
        readonly List<string> checks = new List<string>(), errors = new List<string>();
        WorkshopSimulation sim;
        WorkshopUI ui;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr SendMessageTimeout(IntPtr window, uint message, UIntPtr wParam,
            IntPtr lParam, uint flags, uint timeout, out UIntPtr result);

        static bool WindowResponds(System.Diagnostics.Process process)
        {
            process.Refresh();
            // Mono's Process.Responding reports false for this responsive Unity child window.
            return !process.HasExited && process.MainWindowHandle != IntPtr.Zero &&
                SendMessageTimeout(process.MainWindowHandle, 0, UIntPtr.Zero, IntPtr.Zero,
                    2, 500, out _) != IntPtr.Zero;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-workshop-fan-verify");
            if (index < 0 || index + 1 >= args.Length) return;
            Application.runInBackground = true;
            var host = new GameObject("Reviewed fan delivery verification");
            DontDestroyOnLoad(host);
            host.AddComponent<WorkshopReviewedFanVerification>().folder = Path.GetFullPath(args[index + 1]);
        }

        IEnumerator Start()
        {
            Directory.CreateDirectory(folder);
            Application.logMessageReceived += Log;
            var routine = Verify();
            while (true)
            {
                object next;
                try
                {
                    if (!routine.MoveNext()) break;
                    next = routine.Current;
                }
                catch (Exception e) { errors.Add(e.ToString()); break; }
                yield return next;
            }
            File.WriteAllText(Path.Combine(folder, "verification.txt"),
                "Date: " + DateTimeOffset.Now.ToString("o") +
                "\nPlayer: " + Application.version + " / Unity " + Application.unityVersion +
                "\nResolution: " + Screen.width + "x" + Screen.height +
                "\nMethod: delivered Windows player, UGUI raycasts/events, physics, actual NavMesh arrival" +
                "\n" + string.Join("\n", checks) +
                "\n" + string.Join("\n", errors.Select(e => "FAIL " + e)) +
                "\nResult: " + (errors.Count == 0 ? "PASS" : "FAIL"));
            Application.logMessageReceived -= Log;
            Application.Quit(errors.Count == 0 ? 0 : 1);
        }

        void Log(string text, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception)
                errors.Add(text + "\n" + stack);
        }

        void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            checks.Add("PASS " + message);
            File.AppendAllText(Path.Combine(folder, "progress.txt"), checks.Last() + "\n");
        }

        T Member<T>(string name) => (T)typeof(WorkshopUI)
            .GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(ui);

        void Click(string name)
        {
            var button = ui.GetComponentsInChildren<Button>().Single(b => b.name == name);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Check(button.interactable && hits.Count > 0 &&
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) == button.gameObject,
                "Visible top UI raycast: " + name);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        void Capture(string name) =>
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));

        void CheckImage(string name)
        {
            var image = new Texture2D(2, 2);
            image.LoadImage(File.ReadAllBytes(Path.Combine(folder, name + ".png")));
            var colors = new HashSet<Color32>();
            for (int y = 0; y < image.height; y += 13)
                for (int x = 0; x < image.width; x += 13) colors.Add(image.GetPixel(x, y));
            Check(image.width == Screen.width && image.height == Screen.height && colors.Count > 100,
                "Nonblank screenshot at native resolution: " + name + " colors=" + colors.Count);
            Destroy(image);
        }

        void CheckMotion(WorkshopEquipmentModel model)
        {
            var manifest = JsonUtility.FromJson<Manifest>(
                Resources.Load<TextAsset>("ReviewedEquipment/fan-parts").text);
            var nodes = model.GetComponentsInChildren<Transform>(true);
            Vector3 origin = nodes.Single(t => t.name == "FanMotionOrigin").position;
            Vector3 x = nodes.Single(t => t.name == "FanMotionX").position - origin;
            Vector3 y = nodes.Single(t => t.name == "FanMotionY").position - origin;
            Vector3 z = nodes.Single(t => t.name == "FanMotionZ").position - origin;
            Vector3[] home = Enumerable.Range(0, 11).Select(i => model.AssemblyTransform(i).position).ToArray();
            float maxError = 0;
            foreach (int sample in Enumerable.Range(0, 1201).Concat(Enumerable.Range(0, 1201).Reverse()))
            {
                float value = sample / 1200f;
                model.SetDisassembly(value * 11, false, false);
                for (int i = 0; i < 11; i++)
                {
                    var path = manifest.parts[i].path;
                    Vector3 offset = path.Last().offset;
                    for (int k = 1; k < path.Length; k++)
                    {
                        if (value > path[k].t) continue;
                        float t = Mathf.Clamp01((value - path[k - 1].t) / (path[k].t - path[k - 1].t));
                        t = t * t * (3 - 2 * t);
                        offset = Vector3.LerpUnclamped(path[k - 1].offset, path[k].offset, t);
                        break;
                    }
                    Vector3 expected = home[i] + x * offset.x + y * offset.y + z * offset.z;
                    maxError = Mathf.Max(maxError, Vector3.Distance(expected, model.AssemblyTransform(i).position));
                }
            }
            // The inspection studio sits at 260 m; world float cancellation costs about 30 microns.
            float coordinate = Mathf.Max(Mathf.Abs(origin.x), Mathf.Abs(origin.y), Mathf.Abs(origin.z));
            float tolerance = Mathf.Max(.00001f, coordinate * 2 * 1.1920929e-7f);
            Check(maxError < tolerance, "1201 forward/reverse motion samples match approved paths; max world error=" +
                maxError + " m; coordinate-based float tolerance=" + tolerance + " m");
        }

        void ExportGeometry(WorkshopEquipmentModel model)
        {
            var nodes = model.GetComponentsInChildren<Transform>(true);
            Vector3 origin = nodes.Single(t => t.name == "FanMotionOrigin").position;
            var geometry = new Geometry {
                date = DateTimeOffset.Now.ToString("o"),
                axisX = nodes.Single(t => t.name == "FanMotionX").position - origin,
                axisY = nodes.Single(t => t.name == "FanMotionY").position - origin,
                axisZ = nodes.Single(t => t.name == "FanMotionZ").position - origin,
                meshes = Enumerable.Range(0, 11).SelectMany(i =>
                    model.AssemblyTransform(i).GetComponentsInChildren<MeshFilter>().Select(f => new GeometryMesh {
                        name = f.name, part = i, triangles = f.sharedMesh.triangles,
                        vertices = f.sharedMesh.vertices.Select(v => f.transform.TransformPoint(v)).ToArray()
                    })).ToArray()
            };
            File.WriteAllText(Path.Combine(folder, "fan-platform-motion-geometry.json"), JsonUtility.ToJson(geometry));
        }

        IEnumerator LaunchViewer(string name)
        {
            int[] previous = System.Diagnostics.Process.GetProcessesByName(name).Select(p => p.Id).ToArray();
            Click("详细展示");
            System.Diagnostics.Process launched = null;
            float until = Time.realtimeSinceStartup + 20;
            while (Time.realtimeSinceStartup < until && launched == null)
            {
                yield return new WaitForSecondsRealtime(.5f);
                launched = System.Diagnostics.Process.GetProcessesByName(name).FirstOrDefault(p => !previous.Contains(p.Id));
            }
            Check(launched != null, "Platform launches " + name);
            until = Time.realtimeSinceStartup + 45;
            while (!launched.HasExited && !WindowResponds(launched) &&
                Time.realtimeSinceStartup < until)
            {
                yield return new WaitForSecondsRealtime(.5f);
                launched.Refresh();
            }
            yield return new WaitForSecondsRealtime(2);
            launched.Refresh();
            string expected = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath),
                "Viewers", name, name + ".exe"));
            File.AppendAllText(Path.Combine(folder, "viewer-processes.txt"),
                name + " pid=" + launched.Id + " exited=" + launched.HasExited +
                " window=" + launched.MainWindowHandle + " nativeResponding=" + WindowResponds(launched) +
                " path=" + (launched.HasExited ? "" : launched.MainModule.FileName) + "\n");
            Check(!launched.HasExited && WindowResponds(launched) &&
                string.Equals(launched.MainModule.FileName, expected, StringComparison.OrdinalIgnoreCase),
                "Correct deployed viewer process responsive: " + expected);
            launched.CloseMainWindow();
            yield return new WaitForSecondsRealtime(1);
            if (!launched.HasExited) launched.Kill();
            launched.Dispose();
        }

        IEnumerator Verify()
        {
            yield return new WaitForSecondsRealtime(4);
            sim = FindFirstObjectByType<WorkshopSimulation>();
            ui = FindFirstObjectByType<WorkshopUI>();
            Check(sim && ui && sim.devices.Length == 4, "Four workshop devices and existing UI");
            var fan = sim.devices.Single(d => d.deviceId == "F-01");
            var pump = sim.devices.Single(d => d.deviceId == "P-01");
            var model = fan.GetComponentInChildren<WorkshopEquipmentModel>();
            Check(model && model.ReviewedFanModel && model.AssemblyCount == 11 &&
                model.PartCount == 590 && model.TriangleCount == 98140, "Approved fan replaces procedural model: 11/590/98140");
            Check(pump.GetComponentInChildren<WorkshopEquipmentModel>().AssemblyCount == 12,
                "Reviewed pump retains 12 assemblies");
            Check(model.GetComponentsInChildren<Renderer>().All(r =>
                r.sharedMaterials.All(m => m && m.shader.name == "Fan/Physical Finish" && m.shader.isSupported)),
                "All fan materials use supported reviewed shader");
            Check(model.GetComponentInChildren<ReflectionProbe>().customBakedTexture &&
                model.GetComponentsInChildren<Renderer>().All(r => r.probeAnchor),
                "Authored reflection map and fan-only renderer anchors retained");
            Check(model.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials)
                .Where(m => m.GetTexture("_BaseMap")).All(m =>
                    m.GetTexture("_BaseMap").width ==
                        (m.GetTexture("_BaseMap").name.StartsWith("cast_") ? 2048 :
                        m.GetTexture("_BaseMap").name.StartsWith("machined_") ? 1024 : 512) &&
                    m.GetTexture("_BumpMap") && m.GetTexture("_RoughMap")),
                "Approved 2048/1024/512 base textures with normal and roughness maps");
            Check(Vector3.Distance(fan.transform.position, WorkshopTrainingLayout.Position("F-01")) < .001f,
                "Existing fan station preserved");
            var bounds = fan.GetComponent<BoxCollider>().bounds;
            Check(Mathf.Abs(bounds.min.y) < .04f && bounds.size.y > 1.8f &&
                bounds.min.x > 1.9f && bounds.max.x < 8.5f &&
                bounds.min.z > -4 && bounds.max.z < 3.5f, "Grounded nominal metre scale within equipment area: " + bounds);
            Check(sim.devices.Where(d => d != fan).All(d =>
                !bounds.Intersects(d.GetComponent<BoxCollider>().bounds)), "Fan separated from all other equipment");
            Vector3 inlet = -model.transform.TransformDirection(Vector3.forward);
            Check(Vector3.Dot(inlet, (fan.inspectionPoint - fan.transform.position).normalized) > .99f,
                "Inlet and service face point toward inspection corridor");
            var hit = Physics.RaycastAll(bounds.center + inlet * 3, -inlet, 4);
            Check(hit.Any(h => h.collider.GetComponentInParent<WorkshopDevice>() == fan), "Scene physics raycast selects F-01");
            var rotation = fan.rotor.localRotation;
            yield return new WaitForSecondsRealtime(.5f);
            Check(Quaternion.Angle(rotation, fan.rotor.localRotation) > 5 &&
                Mathf.Abs(Vector3.Dot(fan.rotor.TransformDirection(Vector3.forward), fan.transform.forward)) > .999f,
                "Only authored rotor turns around physical fan axis");
            fan.ToggleOperation();
            var stopped = fan.rotor.localRotation;
            yield return new WaitForSecondsRealtime(.3f);
            Check(Quaternion.Angle(stopped, fan.rotor.localRotation) < .001f, "Stop command stops reviewed rotor");
            fan.rotor.localRotation = (Quaternion)typeof(WorkshopDevice)
                .GetField("initialRotor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(fan);
            model.SetDamage(null, 0);
            CheckMotion(model);
            ExportGeometry(model);
            var rest = model.DamageSurface.sharedMesh.vertices;
            model.SetDamage(WorkshopFaultCatalog.Find("F-CRACK"), .5f);
            Check(model.OpeningMm < .001f && model.DeflectionMm > 1, "Authored blade bends before crack opens");
            model.SetDamage(WorkshopFaultCatalog.Find("F-CRACK"), 1);
            Check(model.OpeningMm > 5 && model.DeflectionMm > 30 && !model.Detached,
                "Original blade seam opens while rear ligament remains connected");
            model.SetDamage(null, 0);
            Check(rest.Zip(model.DamageSurface.sharedMesh.vertices, (a, b) => Vector3.Distance(a, b)).Max() < .000001f,
                "Clearing fault restores original blade geometry");
            fan.ToggleOperation();
            Click("设备台账");
            yield return null;
            Click("Device F-01");
            yield return new WaitForSecondsRealtime(2);
            Check(sim.Selected == fan && fan.selectionRing.activeSelf, "Ledger selection binds F-01 and selected perimeter");
            Capture("fan-in-workshop");
            yield return new WaitForSecondsRealtime(.4f);
            CheckImage("fan-in-workshop");
            Click("整机拆装");
            yield return new WaitForSecondsRealtime(1);
            var view = Member<WorkshopEquipmentView>("equipmentView");
            Check(view.Model.ReviewedFanModel && view.Model.AssemblyCount == 11, "Platform inspection uses same reviewed 11 assemblies");
            CheckMotion(view.Model);
            Click("全部拆开");
            float deadline = Time.realtimeSinceStartup + 25;
            while (view.Disassembly < 10.999f && Time.realtimeSinceStartup < deadline) yield return null;
            Check(view.Disassembly > 10.999f, "Animated full fan disassembly reaches endpoint");
            Capture("fan-platform-exploded");
            yield return new WaitForSecondsRealtime(.4f);
            CheckImage("fan-platform-exploded");
            Click("重新组装");
            yield return new WaitForSecondsRealtime(3);
            Click("全部拆开");
            yield return new WaitForSecondsRealtime(2);
            Click("重新组装");
            deadline = Time.realtimeSinceStartup + 25;
            while (view.Disassembly > .001f && Time.realtimeSinceStartup < deadline) yield return null;
            Check(view.Disassembly < .001f, "Animation reverses and assembles without accumulated offsets");
            Capture("fan-platform-assembled");
            yield return new WaitForSecondsRealtime(.4f);
            CheckImage("fan-platform-assembled");
            Click("返回工况");
            yield return null;
            var launch = LaunchViewer("IndustrialFanViewer");
            while (launch.MoveNext()) yield return launch.Current;
            Check(sim.TriggerFault(fan, "F-CRACK") && !fan.IsRunning && fan.faultMarker.activeSelf,
                "F-CRACK updates existing simulation, alarm, interlock and marker");
            var alarm = sim.ActiveAlarm;
            Check(sim.Report(alarm, "验证员", "设备部", "风机叶片检查") &&
                sim.Assign(alarm, "维修员", 30), "Fan work order dispatches actual inspector");
            float speed = sim.inspector.Agent.speed, acceleration = sim.inspector.Agent.acceleration;
            sim.inspector.Agent.speed = 18; sim.inspector.Agent.acceleration = 80;
            deadline = Time.realtimeSinceStartup + 35;
            while (!sim.inspector.HasArrived && Time.realtimeSinceStartup < deadline) yield return null;
            Check(sim.inspector.HasArrived && sim.CanResolve, "Robot actually reaches fan inspection point");
            Check(sim.SubmitRepair(alarm, "教学预制裂口", "检查叶片并恢复", "无") &&
                sim.Accept(alarm, true, "检查完成") && fan.Fault == null && fan.IsRunning &&
                sim.Records.Any(r => r.faultCode == "F-CRACK"), "Fan repair, acceptance, restart and record close loop");
            sim.inspector.Agent.speed = speed; sim.inspector.Agent.acceleration = acceleration;
            sim.Select(pump);
            yield return null;
            launch = LaunchViewer("WaterPumpViewer");
            while (launch.MoveNext()) yield return launch.Current;
            var switcher = FindFirstObjectByType<WorkshopSceneSwitcher>();
            switcher.SwitchTo(WorkshopSceneSwitcher.AssemblyScene);
            deadline = Time.realtimeSinceStartup + 40;
            while ((switcher.IsSwitching || SceneManager.GetActiveScene().path != WorkshopSceneSwitcher.AssemblyScene) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            Check(SceneManager.GetActiveScene().path == WorkshopSceneSwitcher.AssemblyScene, "Original robot training scene still opens");
            switcher.SwitchTo(WorkshopSceneSwitcher.InspectionScene);
            deadline = Time.realtimeSinceStartup + 40;
            while ((switcher.IsSwitching || SceneManager.GetActiveScene().path != WorkshopSceneSwitcher.InspectionScene) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSecondsRealtime(2);
            Check(FindFirstObjectByType<WorkshopSimulation>().devices.Single(d => d.deviceId == "F-01")
                .GetComponentInChildren<WorkshopEquipmentModel>().ReviewedFanModel, "Returning to platform restores reviewed fan");
        }
    }
}
