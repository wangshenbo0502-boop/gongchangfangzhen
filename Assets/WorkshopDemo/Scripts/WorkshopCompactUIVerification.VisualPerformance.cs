using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UI;

namespace WorkshopDemo
{
    public sealed partial class WorkshopCompactUIVerification
    {
        static readonly string[] PriorityCases = { "TK-LEAK", "P-SEAL", "P-CAV", "F-CRACK", "F-IMBAL", "PLC-CONTACT" };
        static readonly CultureInfo CsvCulture = CultureInfo.InvariantCulture;
        string reviewCase;
        IEnumerable<string> ReviewCases => reviewCase == null ? PriorityCases : new[] { reviewCase };

        void PrepareReview()
        {
            var input = FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (input) input.enabled = false;
            ui = FindFirstObjectByType<WorkshopUI>();
            sim = FindFirstObjectByType<WorkshopSimulation>();
            Check(ui && sim && UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "WorkshopDemo",
                "Review uses current WorkshopDemo scene");
        }

        WorkshopEquipmentView OpenReviewCase(string code)
        {
            var fault = WorkshopFaultCatalog.Find(code);
            sim.Select(sim.devices.Single(d => d.kind == fault.Kind), false);
            CallUI("OpenEquipment", fault);
            CallUI("SetEquipmentMode", 2);
            var view = Member<WorkshopEquipmentView>("equipmentView");
            view.Playing = view.AutoOrbit = false;
            view.ShowLabels = false;
            return view;
        }

        IEnumerator VerifyVisualMatrix()
        {
            yield return new WaitForSecondsRealtime(3);
            PrepareReview();
            foreach (string code in ReviewCases)
            {
                var view = OpenReviewCase(code);
                yield return null;
                if (code == "F-IMBAL") yield return VerifyFanImbalance(view);
                if (code == "P-IMPELLER" || code == "P-BEAR") VerifyLocalPumpSection(view, code);
                if (code == "P-INLET") VerifyInletRestriction(view);
                if (view.CanShowPumpSection && !view.PumpSectionOpen) view.TogglePumpSection();
                yield return new WaitForSecondsRealtime(.4f);
                FitButtons(Member<GameObject>("equipmentDialog").transform);
                var values = new[] { 0f, .1f, .5f, 1f };
                foreach (string angle in new[] { "front", "side", "oblique", "focus" })
                foreach (float p in values)
                {
                    SetReviewAngle(view, angle);
                    view.SetProgress(p);
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    if ((code == "P-BEAR" || code == "P-IMPELLER") && p == 0)
                    {
                        var camera = (Camera)typeof(WorkshopEquipmentView).GetField("viewCamera",
                            BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                        Check(camera.cullingMask == (angle == "focus" ? 1 << 27 : (1 << 28) | (1 << 27)),
                            code + " " + angle + " switches internal inspection and complete equipment context");
                    }
                    SaveReviewTexture(view, code + "-" + angle + "-" + p.ToString("F3", CsvCulture));
                }
                view.FocusDamage();
                foreach (float p in new[] { view.Profile.FirstProgress - .005f, view.Profile.FirstProgress + .005f,
                    view.Profile.SecondProgress - .005f, view.Profile.SecondProgress + .005f })
                {
                    view.SetProgress(p);
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    SaveReviewTexture(view, code + "-milestone-" + p.ToString("F3", CsvCulture));
                }
                view.ResetView();
                view.SetProgress(.6f);
                CallUI("UpdateEquipmentText");
                yield return new WaitForSecondsRealtime(.25f);
                yield return new WaitForEndOfFrame();
                SaveReviewTexture(view, code + "-detail", true);
                if (code == "TK-LEAK")
                {
                    var camera = (Camera)typeof(WorkshopEquipmentView).GetField("detailCamera",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                    view.Model.TryGetFaultBinding("tank.shell.frontLeak", out var binding);
                    var center = camera.WorldToViewportPoint(binding.Anchor);
                    float scale = view.Model.transform.TransformVector(Vector3.up).magnitude;
                    var edge = camera.WorldToViewportPoint(binding.Anchor + camera.transform.right * (.006f * scale));
                    float diameter = Mathf.Abs(edge.x - center.x) * view.DetailTexture.width * 2;
                    Check(center.z > camera.nearClipPlane && center.x > .3f && center.x < .7f &&
                        center.y > .3f && center.y < .7f && diameter >= 48,
                        "Tank calibrated 12 mm hole is inside closeup with projected diameter " +
                        diameter.ToString("F1", CsvCulture) + " pixels; visual acceptance is separate");
                    var detailProgress = new[] { 0f, .25f, 1f, 0f };
                    for (int i = 0; i < detailProgress.Length; i++)
                    {
                        view.SetProgress(detailProgress[i]);
                        yield return null;
                        yield return new WaitForEndOfFrame();
                        SaveReviewTexture(view, "TK-LEAK-detail-" + (i == 3 ? "restored" :
                            detailProgress[i].ToString("F3", CsvCulture)), true);
                    }
                    view.SetProgress(.6f);
                    yield return null;
                    yield return new WaitForEndOfFrame();
                }
                Capture(code + "-ui");
                yield return new WaitForSecondsRealtime(.2f);
                Check(view.Model.FaultPresentationAvailable, code + " full visual matrix captured");
                Member<GameObject>("equipmentDialog").SetActive(false);
                yield return null;
            }
            foreach (var spec in WorkshopFaultCase.All.Where(spec => reviewCase == null || spec.Code == reviewCase))
            {
                var view = OpenReviewCase(spec.Code);
                if (view.CanShowPumpSection && !view.PumpSectionOpen) view.TogglePumpSection();
                view.SetProgress(.6f);
                view.FocusDamage();
                yield return null;
                yield return new WaitForEndOfFrame();
                SaveReviewTexture(view, spec.Code + "-mechanism");
                if (spec.Code == "F-BLADE")
                {
                    view.SetProgress(0);
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    SaveReviewTexture(view, "F-BLADE-intact");
                    view.SetProgress(1);
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    SaveReviewTexture(view, "F-BLADE-worn");
                }
                Check(view.Model.FaultPresentationAvailable, spec.Code + " mechanism snapshot captured");
                Member<GameObject>("equipmentDialog").SetActive(false);
                yield return null;
            }
        }

        IEnumerator VerifyFanImbalance(WorkshopEquipmentView view)
        {
            var model = view.Model;
            var rotor = model.Rotor;
            var device = sim.Selected;
            var origin = rotor.localPosition;
            var home = rotor.localRotation;
            view.SetProgress(0);
            Check(rotor.localPosition == origin && model.FaultFrame.Value.Rpm == device.nominalRpm,
                "F-IMBAL zero progress restores the shaft center and uses the device nominal RPM");
            foreach (float progress in new[] { .17f, .61f, 1f, .17f })
            {
                view.SetProgress(progress);
                var radial = (Quaternion.Inverse(home) * rotor.localRotation) * Vector3.right;
                float amplitude = .00025f * model.FaultFrame.Value.Vibration;
                var expected = home * new Vector3(radial.x, radial.y * .7f, 0) * amplitude;
                Check((rotor.localPosition - origin - expected).sqrMagnitude < 1e-12f,
                    "F-IMBAL shaft orbit follows the rendered rotor phase at " + progress);
                var lines = (LineRenderer[])typeof(WorkshopLocalFaultPresenter).GetField("mechanismLines",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(model.LocalFaultPresenter);
                var vector = lines[1].GetPosition(1) - lines[1].GetPosition(0);
                Check(Vector3.Dot(vector.normalized, rotor.parent.TransformVector(expected).normalized) > .9999f,
                    "F-IMBAL teaching arrow follows the same orbit phase at " + progress);
            }
            view.SetProgress(.61f);
            var directRotation = rotor.localRotation;
            var directPosition = rotor.localPosition;
            float reading = model.FaultFrame.Value.Vibration;
            view.MotionScale = 5;
            Check((rotor.localPosition - origin - (directPosition - origin) * 5).sqrMagnitude < 1e-11f &&
                model.FaultFrame.Value.Vibration == reading && Quaternion.Angle(rotor.localRotation, directRotation) < .01f,
                "F-IMBAL 5x changes displacement only, preserving phase and vibration telemetry");
            view.MotionScale = 1;
            view.SetProgress(.1f);
            view.SetProgress(.61f);
            Check(rotor.localPosition == directPosition && Quaternion.Angle(rotor.localRotation, directRotation) < .01f,
                "F-IMBAL backward scrub matches direct progress without phase accumulation");
            float seconds = view.Progress * view.Duration, dt = .01f;
            float speed = (WorkshopFaultProfile.ImbalancePreviewAngle(seconds + dt, device.nominalRpm, view.Fault.SpeedRatio) -
                WorkshopFaultProfile.ImbalancePreviewAngle(seconds - dt, device.nominalRpm, view.Fault.SpeedRatio)) / (2 * dt);
            Check(Mathf.Abs(speed - model.FaultFrame.Value.Rpm * 6 * WorkshopFaultProfile.ImbalanceRotationScale) < .03f,
                "F-IMBAL preview phase derivative matches RPM with explicit 1/48 teaching time scale");
            view.Playing = true;
            yield return new WaitForSecondsRealtime(.2f);
            view.Playing = false;
            yield return null;
            var pausedRotation = rotor.localRotation;
            var pausedPosition = rotor.localPosition;
            float pausedProgress = view.Progress;
            yield return new WaitForSecondsRealtime(.15f);
            Check(rotor.localPosition == pausedPosition && rotor.localRotation == pausedRotation &&
                view.Progress == pausedProgress && view.Progress > .61f,
                "F-IMBAL playback advances then pause freezes rotor, orbit and progress");
            view.SetProgress(0);
            Check(rotor.localPosition == origin && Quaternion.Angle(rotor.localRotation, home) < .01f,
                "F-IMBAL reset restores nominal rotor pose");
            var live = device.GetComponentInChildren<WorkshopEquipmentModel>();
            var liveOrigin = live.Rotor.localPosition;
            var liveHome = (Quaternion)typeof(WorkshopLocalFaultPresenter).GetField("rotorHome",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(live.LocalFaultPresenter);
            var devicePosition = device.transform.position;
            var point = device.inspectionPoint;
            var collider = device.GetComponent<BoxCollider>();
            var colliderCenter = collider.center;
            var colliderSize = collider.size;
            Check(sim.TriggerFault(device, "F-IMBAL"), "F-IMBAL live injection uses the business authority");
            yield return new WaitForSecondsRealtime(.2f);
            yield return new WaitForEndOfFrame();
            var firstRotation = live.Rotor.localRotation;
            var relative = (Quaternion.Inverse(liveHome) * firstRotation) * Vector3.right;
            var actualOffset = Quaternion.Inverse(liveHome) * (live.Rotor.localPosition - liveOrigin);
            Check(Vector3.Dot(new Vector3(actualOffset.x, actualOffset.y / .7f, 0).normalized,
                new Vector3(relative.x, relative.y, 0).normalized) > .9999f,
                "F-IMBAL live orbit samples the rotor after its sole rotation controller");
            yield return new WaitForSecondsRealtime(.15f);
            yield return new WaitForEndOfFrame();
            Check(Quaternion.Angle(firstRotation, live.Rotor.localRotation) > 1 &&
                device.transform.position == devicePosition && device.inspectionPoint == point &&
                collider.center == colliderCenter && collider.size == colliderSize,
                "F-IMBAL live rotor advances without moving equipment, inspection point or collider");
            sim.ResetDemo();
            yield return new WaitForSecondsRealtime(.15f);
            Check(live.Rotor.localPosition == liveOrigin, "F-IMBAL business reset restores the shaft center");
            device.ToggleOperation();
            Check(sim.TriggerFault(device, "F-IMBAL") && !device.IsRunning,
                "F-IMBAL injection into an already stopped device preserves stopped state");
            yield return new WaitForSecondsRealtime(.15f);
            var stoppedRotation = live.Rotor.localRotation;
            yield return new WaitForSecondsRealtime(.15f);
            Check(live.Rotor.localPosition == liveOrigin && live.Rotor.localRotation == stoppedRotation &&
                live.FaultFrame.Value.Rpm == 0 && live.FaultFrame.Value.Vibration == 0,
                "F-IMBAL stopped gate freezes rotation, removes orbit and zeroes live RPM/vibration");
            sim.ResetDemo();
            view.SetProgress(0);
        }

        void VerifyInletRestriction(WorkshopEquipmentView view)
        {
            var model = view.Model;
            Check(model.TryGetFaultBinding("pump.suction.bore", out var bore),
                "Pump inlet strainer resolves original suction flange and pipe");
            var original = bore.Surface.GetComponent<MeshFilter>().sharedMesh;
            var screen = model.transform.Find("Teaching inlet strainer insert").GetComponent<MeshFilter>();
            var debris = model.transform.Find("Teaching inlet retained debris").GetComponent<MeshFilter>();
            view.SetProgress(0);
            Check(screen.gameObject.activeSelf && !debris.gameObject.activeSelf,
                "Pump inlet zero progress has an open teaching strainer without retained debris");
            float radius = original.vertices.Min(v => Vector3.ProjectOnPlane(
                bore.Surface.TransformPoint(v) - bore.Renderers[0].bounds.center, model.transform.right).magnitude);
            var center = screen.transform.position;
            Check(screen.sharedMesh.vertices.All(v => Vector3.ProjectOnPlane(
                screen.transform.TransformPoint(v) - center, model.transform.right).magnitude < radius),
                "Pump inlet strainer fits inside the original flange bore");
            Check(Vector3.Dot(center - bore.Renderers[0].bounds.center, model.transform.right) > 0,
                "Pump inlet strainer is behind the original flange face");
            view.SetProgress(.75f);
            var direct = debris.sharedMesh.vertices;
            view.SetProgress(.25f);
            view.SetProgress(.75f);
            Check(debris.gameObject.activeSelf && direct.SequenceEqual(debris.sharedMesh.vertices),
                "Pump inlet restriction reverse scrub reproduces the same obstruction geometry");
            view.TeachingVisible = false;
            Check(model.LocalFaultPresenter.ActiveMechanismLines == 0 && debris.gameObject.activeSelf,
                "Pump inlet retained debris remains visible when teaching flow arrows are disabled");
            view.TeachingVisible = true;
            view.SetFaultMode(false);
            Check(!screen.gameObject.activeSelf && !debris.gameObject.activeSelf &&
                bore.Surface.GetComponent<MeshFilter>().sharedMesh == original &&
                bore.Renderers.All(r => r.gameObject.layer == 28),
                "Pump inlet observation restores original mesh and layers and removes the teaching insert");
            view.SetFaultMode(true);
        }

        void VerifyLocalPumpSection(WorkshopEquipmentView view, string code)
        {
            var model = view.Model;
            var renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            var visibility = renderers.ToDictionary(r => r, r => r.enabled);
            Check(view.CanShowPumpSection && !view.PumpSectionOpen, code + " preview offers a closed section");
            CallUI("UpdateEquipmentText");
            Click("Pump section", Member<GameObject>("equipmentDialog").transform);
            Check(view.PumpSectionOpen && renderers.Any(r => visibility[r] && !r.enabled),
                code + " UGUI section removes occluding parts");
            string assemblyName = code == "P-IMPELLER" ? "PMP_07_Impeller" : "PMP_11_BearingSet";
            var assembly = Enumerable.Range(0, model.AssemblyCount).Select(model.AssemblyTransform)
                .Single(t => t.name == assemblyName);
            Check(assembly.GetComponentsInChildren<MeshRenderer>(true).All(r => r.enabled == visibility[r]),
                code + " original internal parts remain visible");
            if (code == "P-IMPELLER")
            {
                Check(Enumerable.Range(0, model.AssemblyCount).Select(model.AssemblyTransform)
                    .Where(t => t.name == "PMP_04_Volute" || t.name == "PMP_05_InletCover" ||
                        t.name == "PMP_09_ImpellerFrontShroud")
                    .SelectMany(t => t.GetComponentsInChildren<MeshRenderer>(true)).All(r => !r.enabled),
                    "Pump impeller section hides complete cover assemblies including their attached gauges and fasteners");
                model.TryGetFaultBinding("pump.impeller.inlet", out var blade);
                var filter = blade.Surface.GetComponent<MeshFilter>();
                view.SetProgress(0);
                var intact = filter.sharedMesh;
                var vertices = intact.vertices;
                view.SetProgress(1);
                var worn = filter.sharedMesh;
                float scale = model.transform.lossyScale.x;
                var offsets = worn.vertices.Select((v, i) =>
                    blade.Surface.TransformVector(v - vertices[i]).magnitude / scale).ToArray();
                Check(worn != intact && worn.vertexCount == intact.vertexCount &&
                    offsets.Max() > .0019f && offsets.Max() <= .002001f &&
                    offsets.Count(v => v < 1e-7f) > vertices.Length / 2,
                    "Pump wear is local, topology-preserving, at most 2 mm; inner blade remains intact");
                view.SetProgress(.75f);
                var direct = filter.sharedMesh.vertices;
                view.SetProgress(.25f);
                view.SetProgress(.75f);
                Check(direct.SequenceEqual(filter.sharedMesh.vertices), "Pump blade reverse scrub is deterministic");
                view.SetProgress(0);
                Check(filter.sharedMesh == intact && intact.vertices.SequenceEqual(vertices),
                    "Pump blade zero progress restores original shared mesh without asset mutation");
            }
            else
            {
                var filter = assembly.GetComponentsInChildren<MeshFilter>(true)
                    .Where(f => f.name.StartsWith("PMP_06_SealBearing_BearingOuterRace", StringComparison.Ordinal))
                    .OrderBy(f => model.transform.InverseTransformPoint(f.transform.position).x).First();
                view.SetProgress(0);
                var intact = filter.sharedMesh;
                var vertices = intact.vertices;
                view.SetProgress(1);
                var worn = filter.sharedMesh;
                float scale = model.transform.lossyScale.x;
                var offsets = worn.vertices.Select((v, i) =>
                    filter.transform.TransformVector(v - vertices[i]).magnitude / scale).ToArray();
                Check(worn != intact && worn.vertexCount == intact.vertexCount &&
                    offsets.Max() > .0009f && offsets.Max() <= .001001f &&
                    offsets.Count(v => v < 1e-7f) > vertices.Length * .85f &&
                    worn.triangles.SequenceEqual(intact.triangles),
                    "Pump race loss is local, topology-preserving, at most 1 mm; most race vertices remain intact");
                var focus = model.LocalFaultPresenter.FocusBounds;
                var target = filter.GetComponent<Renderer>().bounds;
                Check((focus.center - target.center).magnitude < .00001f &&
                    focus.size.magnitude < target.size.magnitude + .02f * scale,
                    "Pump bearing closeup frames the damaged original race");
                view.TeachingVisible = false;
                Check(model.LocalFaultPresenter.ActiveMechanismLines == 0 && filter.sharedMesh == worn,
                    "Pump bearing teaching markers hide independently of nominal defect geometry");
                view.TeachingVisible = true;
                Check(model.LocalFaultPresenter.ActiveMechanismLines == 3,
                    "Pump bearing section marks the local reference and damaged race sector");
                view.SetProgress(.75f);
                var direct = filter.sharedMesh.vertices;
                view.SetProgress(.25f);
                view.SetProgress(.75f);
                Check(direct.SequenceEqual(filter.sharedMesh.vertices), "Pump race reverse scrub is deterministic");
                view.SetProgress(0);
                Check(filter.sharedMesh == intact && intact.vertices.SequenceEqual(vertices),
                    "Pump race zero progress restores original shared mesh without asset mutation");
            }
            Click("Pump section", Member<GameObject>("equipmentDialog").transform);
            Check(!view.PumpSectionOpen && visibility.All(pair => pair.Key.enabled == pair.Value),
                code + " closing section restores every original renderer state");
            view.TogglePumpSection();
            view.SetFaultMode(false);
            Check(visibility.All(pair => pair.Key.enabled == pair.Value) &&
                renderers.All(r => r.gameObject.layer == 28),
                code + " exiting fault mode restores visibility and studio layers");
            var liveModel = sim.Selected.GetComponentInChildren<WorkshopEquipmentModel>();
            Check(liveModel != model && liveModel.LocalFaultPresenter?.SectionOpen != true &&
                !liveModel.GetComponentsInChildren<Transform>(true).Any(t => t.gameObject.layer == 27 || t.gameObject.layer == 28),
                code + " section and closeup layers stay isolated from the live equipment");
            view.SetFaultMode(true);
        }

        static void SetReviewAngle(WorkshopEquipmentView view, string angle)
        {
            view.ResetView();
            if (angle == "front") view.Preset(0);
            else if (angle == "side") view.Preset(2);
            else if (angle == "focus") view.FocusDamage();
        }

        void SaveReviewTexture(WorkshopEquipmentView view, string name, bool detail = false)
        {
            var camera = (Camera)typeof(WorkshopEquipmentView).GetField(detail ? "detailCamera" : "viewCamera",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
            var source = camera.targetTexture;
            float ratio = Mathf.Min(1, 960f / source.width, 720f / source.height);
            var resized = RenderTexture.GetTemporary(Mathf.RoundToInt(source.width * ratio),
                Mathf.RoundToInt(source.height * ratio), 0);
            var prior = RenderTexture.active;
            Texture2D image = null;
            try
            {
                Graphics.Blit(source, resized);
                RenderTexture.active = resized;
                image = new Texture2D(resized.width, resized.height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, resized.width, resized.height), 0, 0);
                image.Apply();
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = prior;
                RenderTexture.ReleaseTemporary(resized);
                if (image) Destroy(image);
            }
        }

        IEnumerator VerifyPerformance()
        {
            yield return new WaitForSecondsRealtime(3);
            PrepareReview();
            var cameraLifecycle = VerifyBackgroundCameraLifecycle();
            while (cameraLifecycle.MoveNext()) yield return cameraLifecycle.Current;
            string csv = Path.Combine(folder, "performance.csv");
            File.WriteAllText(csv, "case,effects,frames,seconds,mean_ms,p95_ms,max_ms,peak_allocated_bytes,peak_working_set_bytes,open_seconds\n");
            File.AppendAllText(report, "ENV GPU=" + SystemInfo.graphicsDeviceName + " API=" + SystemInfo.graphicsDeviceType +
                " CPU=" + SystemInfo.processorType + " RAM_MB=" + SystemInfo.systemMemorySize +
                " quality=" + QualitySettings.names[QualitySettings.GetQualityLevel()] + " vsync=" +
                QualitySettings.vSyncCount + " target_fps=" + Application.targetFrameRate +
                " startup_seconds=" + Time.realtimeSinceStartup.ToString(CsvCulture) +
                "\nComparison is current-candidate effects off/on; it is not an upgrade-before/after baseline.\n");
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
            {
                foreach (string code in ReviewCases)
                {
                    float openedAt = Time.realtimeSinceStartup;
                    var view = OpenReviewCase(code);
                    yield return null;
                    if (view.CanShowPumpSection && !view.PumpSectionOpen) view.TogglePumpSection();
                    yield return new WaitForEndOfFrame();
                    float load = Time.realtimeSinceStartup - openedAt;
                    foreach (bool effects in new[] { false, true })
                    {
                        view.SetProgress(effects ? .45f : 0);
                        yield return new WaitForSecondsRealtime(2);
                        var samples = new List<float>(2000);
                        long peakAllocated = 0, peakWorking = 0;
                        float startedAt = Time.realtimeSinceStartup;
                        float previousFrame = startedAt;
                        while (Time.realtimeSinceStartup - startedAt < 8)
                        {
                            if (effects) view.SetProgress(.2f + Mathf.Repeat(
                                (Time.realtimeSinceStartup - startedAt) / view.Duration, .35f));
                            yield return null;
                            float now = Time.realtimeSinceStartup;
                            samples.Add((now - previousFrame) * 1000);
                            previousFrame = now;
                            peakAllocated = Math.Max(peakAllocated, Profiler.GetTotalAllocatedMemoryLong());
                            if (samples.Count % 30 == 0)
                            {
                                process.Refresh();
                                peakWorking = Math.Max(peakWorking, process.WorkingSet64);
                            }
                        }
                        samples.Sort();
                        File.AppendAllText(csv, string.Join(",", code, effects, samples.Count,
                            (Time.realtimeSinceStartup - startedAt).ToString(CsvCulture),
                            samples.Average().ToString(CsvCulture), samples[(int)(samples.Count * .95f)].ToString(CsvCulture),
                            samples.Last().ToString(CsvCulture), peakAllocated, peakWorking, load.ToString(CsvCulture)) + "\n");
                        Check(samples.Count > 100, code + " effects=" + effects + " eight-second performance sample");
                    }
                    Member<GameObject>("equipmentDialog").SetActive(false);
                    yield return null;
                    var studio = (GameObject)typeof(WorkshopEquipmentView).GetField("scene",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view);
                    var cameras = studio.GetComponentsInChildren<Camera>(true);
                    Check(!view.Model.gameObject.activeInHierarchy && cameras.Length == 2 &&
                        cameras.All(c => !c.isActiveAndEnabled),
                        code + " closed studio inactive");
                }
            }
            var counts = new List<string>();
            for (int i = 0; i < 6; i++)
            {
                OpenReviewCase("PLC-CONTACT").SetProgress(.6f);
                yield return new WaitForSecondsRealtime(.2f);
                Member<GameObject>("equipmentDialog").SetActive(false);
                yield return null;
                if (i == 0 || i == 5)
                {
                    yield return Resources.UnloadUnusedAssets();
                    GC.Collect();
                    counts.Add("cycle=" + (i + 1) + " meshes=" + Resources.FindObjectsOfTypeAll<Mesh>().Length +
                        " materials=" + Resources.FindObjectsOfTypeAll<Material>().Length +
                        " renderTextures=" + Resources.FindObjectsOfTypeAll<RenderTexture>().Length +
                        " allocated_bytes=" + Profiler.GetTotalAllocatedMemoryLong() +
                        " managed_bytes=" + GC.GetTotalMemory(false));
                }
            }
            File.WriteAllLines(Path.Combine(folder, "lifecycle.txt"), counts);
            File.AppendAllText(report, "Performance statistics recorded; PASS means collection succeeded, not a 60 FPS acceptance.\n");
        }

        IEnumerator VerifyBackgroundCameraLifecycle()
        {
            var camera = WorkshopCamera.Main;
            var canvas = ui.GetComponent<Canvas>();
            bool originallyEnabled = camera.enabled;
            OpenReviewCase(reviewCase ?? "TK-LEAK");
            yield return null;
            Check(!camera.enabled, "Opaque equipment window suspends background camera");
            canvas.enabled = false;
            yield return null;
            Check(camera.enabled == originallyEnabled, "Hidden UI restores original background camera state");
            canvas.enabled = true;
            yield return null;
            Check(!camera.enabled, "Restored equipment UI suspends background camera again");
            Member<GameObject>("equipmentDialog").SetActive(false);
            yield return null;
            Check(camera.enabled == originallyEnabled, "Closing equipment restores background camera");
            camera.enabled = false;
            OpenReviewCase(reviewCase ?? "TK-LEAK");
            yield return null;
            Member<GameObject>("equipmentDialog").SetActive(false);
            yield return null;
            Check(!camera.enabled, "Equipment lifecycle preserves an initially disabled background camera");
            camera.enabled = originallyEnabled;
        }
    }
}
