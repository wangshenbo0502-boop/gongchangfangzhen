using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UI;

namespace WorkshopDemo
{
    public sealed partial class WorkshopCompactUIVerification
    {
        void CallUI(string method, params object[] args)
        {
            var target = typeof(WorkshopUI).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
            var parameters = target.GetParameters();
            target.Invoke(ui, parameters.Select((p, i) => i < args.Length ? args[i] : p.DefaultValue).ToArray());
        }

        static int seamCount(WorkshopEquipmentModel model) =>
            ((List<Vector2Int>)typeof(WorkshopEquipmentModel).GetField("fanSeam",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(model)).Count;

        IEnumerator VerifyUpgrade()
        {
            yield return new WaitForSecondsRealtime(3);
            var input = FindFirstObjectByType<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (input) input.enabled = false;
            ui = FindFirstObjectByType<WorkshopUI>(); sim = FindFirstObjectByType<WorkshopSimulation>();
            Check(ui && sim && sim.devices.Length == 4, "Current four-device monitoring scene");
            Check(WorkshopFaultCatalog.All.Length == 40 && sim.devices.Sum(d => WorkshopFaultCatalog.For(d.kind).Length) == 32,
                "32 applicable definitions plus 8 preserved historical definitions");
            VerifyFaultBindings();
            VerifyEnvelopeProtocol();
            var field = VerifyFieldProtocol();
            var pump = sim.devices.Single(d => d.deviceId == "P-01");
            CallUI("OpenAbaqusEnvelope", pump, WorkshopFaultCatalog.Find("P-ALIGN"), SyntheticEnvelope());
            yield return new WaitForSecondsRealtime(.4f);
            var envelopeDialog = Member<GameObject>("abaqusDialog");
            FitButtons(envelopeDialog.transform);
            Check(envelopeDialog.GetComponentInChildren<WorkshopAbaqusCurveGraphic>().Result.hasTemperature,
                "Synthetic temperature-only envelope rendered independently of a reference FEM");
            Capture("protocol-synthetic-envelope");
            yield return new WaitForSecondsRealtime(.3f);
            CallUI("CompactBack");
            CallUI("OpenAbaqusField", field);
            yield return new WaitForSecondsRealtime(.5f);
            FitButtons(Member<GameObject>("abaqusFieldDialog").transform);
            Check(Member<GameObject>("abaqusFieldDialog").GetComponentInChildren<WorkshopAbaqusFieldView>().High == 80,
                "Synthetic original nodal field rendered in an independent viewport");
            Capture("protocol-synthetic-field");
            yield return new WaitForSecondsRealtime(.3f);
            CallUI("CompactBack");

            foreach (var device in sim.devices)
            {
                sim.Select(device, false);
                var host = new GameObject("Reversible case verification");
                host.transform.position = new Vector3(450, 450, 450);
                try
                {
                    var model = host.AddComponent<WorkshopEquipmentModel>();
                    model.Initialize(device.kind, device.deviceId, device);
                    foreach (var fault in WorkshopFaultCatalog.For(device.kind))
                    {
                        var spec = WorkshopFaultCase.Find(fault.Code);
                        Check(spec != null && model.TryGetFaultBinding(spec.Binding, out _), fault.Code + " case definition and unique local binding");
                        var transforms = model.GetComponentsInChildren<Transform>(true);
                        var positions = transforms.Select(t => t.localPosition).ToArray();
                        var scales = transforms.Select(t => t.localScale).ToArray();
                        var renderers = model.GetComponentsInChildren<Renderer>(true);
                        var enabled = renderers.Select(r => r.enabled).ToArray();
                        var layers = transforms.Select(t => t.gameObject.layer).ToArray();
                        var restMesh = model.DamageSurface ? model.DamageSurface.sharedMesh : null;
                        var rest = restMesh && restMesh.isReadable ? restMesh.vertices : null;
                        if (fault.Code == "TK-LEAK") VerifyTankPerforation(model);
                        if (fault.Code == "TK-CORROSION") VerifyTankThinning(model);
                        model.SetDamage(fault, .75f);
                        var frame = model.FaultFrame;
                        var state = transforms.SelectMany(t => new[] { t.localPosition, t.localScale }).ToArray();
                        model.SetDamage(fault, .25f); model.SetDamage(fault, 1); model.SetDamage(fault, 0); model.SetDamage(fault, .75f);
                        Check(frame.HasValue && model.FaultPresentationAvailable && model.TryGetFaultFocus(out var focus, out _) &&
                            focus.size.sqrMagnitude > 0 && state.SequenceEqual(transforms.SelectMany(t => new[] { t.localPosition, t.localScale })) &&
                            model.FaultFrame.Value.StageId == frame.Value.StageId, fault.Code + " deterministic reverse scrub and local focus");
                        if (fault.Code != "F-CRACK") Check(!model.HasOpening, fault.Code + " no unrelated structural fracture");
                        if (fault.Code == "F-CRACK")
                        {
                            var crackFace = (Mesh)typeof(WorkshopEquipmentModel).GetField("fanCrackFaceMesh",
                                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(model);
                            var crackFaceObject = (GameObject)typeof(WorkshopEquipmentModel).GetField("fanCrackFaceObject",
                                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(model);
                            model.SetDamage(fault, .19f);
                            Check(model.DeflectionMm < .001f && model.OpeningMm < .001f &&
                                !crackFaceObject.activeSelf, "F-CRACK intact before 20% with closed fracture face");
                            model.SetDamage(fault, 1);
                            Check(crackFaceObject.activeSelf && crackFace.vertexCount == seamCount(model) * 2 &&
                                crackFace.bounds.size.sqrMagnitude > 0,
                                "F-CRACK opens an independent local fracture face");
                            int changed = rest.Zip(model.DamageSurface.sharedMesh.vertices, (a, b) => (a - b).sqrMagnitude > 1e-14f).Count(v => v);
                            Check(changed > 0 && changed < rest.Length && !model.Detached && model.DeflectionMm < 30,
                                "F-CRACK local deformation preserves unaffected vertices and rear connection");
                            var curve = (Vector3[])typeof(WorkshopEquipmentModel).GetField("fanSeamCurve",
                                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(model);
                            var seam = (List<Vector2Int>)typeof(WorkshopEquipmentModel).GetField("fanSeam",
                                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(model);
                            Check(curve.Max(v => v.magnitude) > .001f && curve.Max(v => v.magnitude) <= .004501f &&
                                curve.Any(v => v == Vector3.zero) &&
                                seam.All(pair => (curve[pair.x] - curve[pair.y]).sqrMagnitude < 1e-12f),
                                "F-CRACK bounded preset curved seam preserves paired lips and unaffected region");
                        }
                        if (fault.Code == "F-BLADE")
                        {
                            model.SetDamage(fault, 1);
                            var worn = model.DamageSurface.sharedMesh.vertices;
                            int changed = rest.Zip(worn, (a, b) => (a - b).sqrMagnitude > 1e-14f).Count(v => v);
                            Check(changed > 0 && changed < rest.Length / 2 && model.OpeningMm == 0 &&
                                model.DeflectionMm <= 6.01f, "F-BLADE localized preset edge loss preserves inner blade and has no crack");
                            model.SetDamage(WorkshopFaultCatalog.Find("F-CRACK"), 1);
                            Check(!worn.SequenceEqual(model.DamageSurface.sharedMesh.vertices),
                                "Fan same-progress case switch replaces wear with crack geometry");
                            model.SetDamage(fault, 1);
                            Check(worn.SequenceEqual(model.DamageSurface.sharedMesh.vertices),
                                "Fan same-progress case switch restores deterministic wear geometry");
                        }
                        if (model.LocalFaultPresenter?.Available == true && spec.Effect != "crack" && spec.Effect != "level")
                        {
                            var presenter = model.LocalFaultPresenter;
                            Check(presenter.ActiveMechanismLines > 0 && presenter.ActiveMechanismLines <= 12,
                                fault.Code + " pooled mechanism overlay present");
                            presenter.TeachingVisible = false;
                            model.SetDamage(fault, .75f);
                            Check(presenter.ActiveMechanismLines == 0, fault.Code + " teaching overlay toggle hides all vectors");
                            presenter.TeachingVisible = true;
                            model.SetDamage(fault, .75f);
                        }
                        model.SetDamage(null, 0); model.SetDamage(null, 0);
                        Check(positions.SequenceEqual(transforms.Select(t => t.localPosition)) &&
                            scales.SequenceEqual(transforms.Select(t => t.localScale)) &&
                            enabled.SequenceEqual(renderers.Select(r => r.enabled)) &&
                            layers.SequenceEqual(transforms.Select(t => t.gameObject.layer)) &&
                            (!restMesh || model.DamageSurface.sharedMesh == restMesh) &&
                            (rest == null || rest.SequenceEqual(model.DamageSurface.sharedMesh.vertices)),
                            fault.Code + " exact geometry/transform/enabled/layer restoration after two resets");
                    }
                }
                finally { Destroy(host); }
                yield return null;
                foreach (string code in new[] { device.kind == DeviceKind.Pump ? "P-SEAL" : device.kind == DeviceKind.Tank ? "TK-LEAK" :
                    device.kind == DeviceKind.Fan ? "F-CRACK" : "PLC-CONTACT" })
                {
                    CallUI("OpenEquipment");
                    yield return new WaitForSecondsRealtime(.3f);
                    var dialog = Member<GameObject>("equipmentDialog");
                    var view = Member<WorkshopEquipmentView>("equipmentView");
                    view.SelectFault(WorkshopFaultCatalog.Find(code));
                    Click("故障演示", dialog.transform);
                    view.SetProgress(.75f);
                    yield return new WaitForSecondsRealtime(.25f);
                    FitButtons(dialog.transform); Capture(device.deviceId + "-local-preview");
                    yield return new WaitForSecondsRealtime(.3f);
                    Click("返回车间", dialog.transform);
                    yield return null;
                }
            }
            var business = VerifyAllOrders();
            while (business.MoveNext()) yield return business.Current;
            sim.ResetDemo(); sim.ResetDemo(); yield return null;
            Check(sim.Alarms.Count == 0 && sim.Records.Count == 0 && sim.devices.All(d => d.Fault == null && !d.ExamActive),
                "Two global resets clear cases, training, records and privacy state");
            var samples = new List<float>();
            sim.Select(pump, false);
            CallUI("OpenEquipment");
            yield return new WaitForSecondsRealtime(.3f);
            var activeView = Member<WorkshopEquipmentView>("equipmentView");
            Click("故障演示", Member<GameObject>("equipmentDialog").transform);
            activeView.Playing = true;
            float until = Time.realtimeSinceStartup + 4;
            while (Time.realtimeSinceStartup < until) { yield return null; samples.Add(Time.unscaledDeltaTime * 1000); }
            samples.Sort();
            File.AppendAllText(report, "PERF current platform equipment fault preview: mean_ms=" + samples.Average() +
                " p95_ms=" + samples[(int)(samples.Count * .95f)] + " max_ms=" + samples.Last() +
                " managed_bytes=" + GC.GetTotalMemory(false) + " allocated_bytes=" +
                UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() + "\n");
            Capture("final-preview");
            yield return new WaitForSecondsRealtime(.3f);
        }

        void VerifyTankPerforation(WorkshopEquipmentModel model)
        {
            Check(model.TryGetFaultBinding("tank.shell.frontLeak", out var binding), "Tank perforation binding");
            var presenter = model.FaultPresenter;
            var mesh = presenter.PerforatedMesh;
            Check(mesh && mesh.isReadable, "Derived perforation mesh is readable for geometry validation");
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            Vector3 anchor = binding.Settings.anchorLocalM;
            Vector3 normal = binding.Settings.normalLocal.normalized;
            Vector3 tangent = Vector3.Cross(normal, Vector3.forward).normalized;
            int Hits(Vector3 center)
            {
                Vector3 origin = center + normal * .03f;
                Vector3 direction = -normal;
                int count = 0;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 edge1 = vertices[triangles[i + 1]] - a;
                    Vector3 edge2 = vertices[triangles[i + 2]] - a;
                    Vector3 cross = Vector3.Cross(direction, edge2);
                    float determinant = Vector3.Dot(edge1, cross);
                    if (Mathf.Abs(determinant) < 1e-10f) continue;
                    float inverse = 1 / determinant;
                    Vector3 offset = origin - a;
                    float u = Vector3.Dot(offset, cross) * inverse;
                    if (u < 0 || u > 1) continue;
                    Vector3 q = Vector3.Cross(offset, edge1);
                    float v = Vector3.Dot(direction, q) * inverse;
                    if (v < 0 || u + v > 1) continue;
                    float distance = Vector3.Dot(edge2, q) * inverse;
                    if (distance > 0 && distance < .06f) count++;
                }
                return count;
            }
            int centerHits = Hits(anchor), wallHits = Hits(anchor + tangent * .012f);
            Check(centerHits == 0 && wallHits >= 2,
                "Imported tank hole is through-wall at calibrated anchor; adjacent inner/outer walls remain (" +
                centerHits + "/" + wallHits + ")");
            var fault = WorkshopFaultCatalog.Find("TK-LEAK");
            model.SetDamage(fault, .19f);
            Check(model.DamageSurface.sharedMesh == presenter.IntactMesh, "Tank intact mesh before perforation milestone");
            model.SetDamage(fault, .25f);
            Check(model.DamageSurface.sharedMesh == mesh, "Tank preset 12 mm perforation at milestone");
            model.SetDamage(fault, .75f);
            var stain = presenter.StainMesh;
            var stainVertices = stain ? stain.vertices : System.Array.Empty<Vector3>();
            float innerRadius = stainVertices.Length >= 2
                ? new Vector2(stainVertices[0].x, stainVertices[0].y).magnitude : 0;
            float outerRadius = stainVertices.Length >= 2
                ? new Vector2(stainVertices[1].x, stainVertices[1].y).magnitude : 0;
            float minimumRadius = stainVertices.Length == 0 ? 0 :
                stainVertices.Min(v => new Vector2(v.x, v.y).magnitude);
            float maximumRadius = stainVertices.Length == 0 ? 0 :
                stainVertices.Max(v => new Vector2(v.x, v.y).magnitude);
            Check(stain && stainVertices.Length == 96 && stain.triangles.Length == 288 &&
                innerRadius >= .00649f && innerRadius <= .00701f &&
                outerRadius >= .019f && outerRadius <= .025f &&
                minimumRadius >= .00649f && maximumRadius <= .025f,
                "Tank wetting forms a perforation-clear annulus with bounded radial extent");
            model.SetDamage(null, 0);
            Check(model.DamageSurface.sharedMesh == presenter.IntactMesh, "Tank reverse scrub restores original shared mesh");
        }

        void VerifyTankThinning(WorkshopEquipmentModel model)
        {
            model.TryGetFaultBinding("tank.shell.frontLeak", out var binding);
            var shell = binding.Surface.GetComponent<MeshFilter>();
            var intact = shell.sharedMesh;
            var fault = WorkshopFaultCatalog.Find("TK-CORROSION");
            model.SetDamage(fault, .25f);
            var mesh = model.LocalFaultPresenter.ThinnedMesh;
            Check(mesh && mesh.isReadable && shell.sharedMesh == mesh && mesh != intact,
                "Tank corrosion uses a separately derived closed local thinning mesh");
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            Vector3 anchor = binding.Settings.anchorLocalM, normal = binding.Settings.normalLocal.normalized;
            Vector3 tangent = Vector3.Cross(normal, Vector3.forward).normalized;
            float Thickness(Vector3 center)
            {
                var distances = new List<float>();
                Vector3 start = center + normal * .03f, direction = -normal;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    var a = vertices[triangles[i]];
                    var e1 = vertices[triangles[i + 1]] - a;
                    var e2 = vertices[triangles[i + 2]] - a;
                    var cross = Vector3.Cross(direction, e2);
                    float determinant = Vector3.Dot(e1, cross);
                    if (Mathf.Abs(determinant) < 1e-10f) continue;
                    float inverse = 1 / determinant;
                    var offset = start - a;
                    float u = Vector3.Dot(offset, cross) * inverse;
                    var q = Vector3.Cross(offset, e1);
                    float v = Vector3.Dot(direction, q) * inverse;
                    float d = Vector3.Dot(e2, q) * inverse;
                    if (u >= 0 && v >= 0 && u + v <= 1 && d > 0 && d < .06f &&
                        distances.All(prior => Mathf.Abs(prior - d) > 1e-6f)) distances.Add(d);
                }
                distances.Sort();
                return distances.Count == 2 ? distances[1] - distances[0] : 0;
            }
            float centerWall = Thickness(anchor), left = Thickness(anchor + tangent * .009f),
                right = Thickness(anchor - tangent * .009f);
            Check(centerWall > .0035f && centerWall < .0045f && left >= .0035f && left <= .0085f &&
                right >= .0035f && right <= .0085f,
                "Imported corrosion retains inner/outer walls at three probes; center nominal 4 mm (" +
                centerWall + "/" + left + "/" + right + ")");
            model.SetDamage(fault, .19f);
            Check(shell.sharedMesh == intact, "Corrosion before preset milestone retains intact wall");
            model.SetDamage(fault, .75f);
            model.SetDamage(WorkshopFaultCatalog.Find("TK-LEAK"), .75f);
            Check(shell.sharedMesh == model.FaultPresenter.PerforatedMesh,
                "Corrosion to leakage switch preserves the new case mesh in the same frame");
            model.SetDamage(fault, .75f);
            Check(shell.sharedMesh == mesh, "Leakage to corrosion switch restores closed thinned geometry");
            model.SetDamage(null, 0);
            Check(shell.sharedMesh == intact, "Corrosion reset restores original shared mesh");
        }

        IEnumerator VerifyAllOrders()
        {
            var agent = sim.inspector.Agent;
            float speed = agent.speed, acceleration = agent.acceleration;
            try
            {
                agent.speed = 12; agent.acceleration = 50;
                foreach (var device in sim.devices)
                foreach (var fault in WorkshopFaultCatalog.For(device.kind))
                foreach (bool running in new[] { true, false })
                {
                    if (device.IsRunning != running) device.ToggleOperation();
                    Check(sim.TriggerFault(device, fault.Code) && device.Snapshot.Progress == 1 &&
                        device.IsRunning == (running && !fault.Trips), fault.Code + " direct injection and original operation=" + running);
                    var alarm = sim.ActiveAlarm;
                    var live = device.GetComponentInChildren<WorkshopEquipmentModel>();
                    var snapshot = device.Snapshot;
                    device.Sample(snapshot.BusinessTime);
                    Check(snapshot.Temperature == device.Snapshot.Temperature && snapshot.Rpm == device.Snapshot.Rpm &&
                        snapshot.Version == device.Snapshot.Version && !sim.TriggerFault(device, fault.Code) &&
                        !sim.SubmitRepair(alarm, "early"), fault.Code + " idempotent sampling, duplicate and early submission rejected");
                    Check(sim.StartInspection(alarm), fault.Code + " real robot dispatch");
                    float deadline = Time.realtimeSinceStartup + 35;
                    while (!sim.inspector.HasArrived && Time.realtimeSinceStartup < deadline) yield return null;
                    Check(sim.inspector.HasArrived && alarm.stage == WorkshopOrderStage.Repairing,
                        fault.Code + " actual NavMesh arrival");
                    yield return null;
                    Check(live.FaultFrame.HasValue && live.FaultFrame.Value.Progress == device.Snapshot.Progress,
                        fault.Code + " live presentation follows business snapshot");
                    Check(!sim.SubmitRepair(alarm, "") && sim.SubmitRepair(alarm, "候选版普通模式回归") &&
                        device.Fault != null && !sim.SubmitRepair(alarm, "duplicate") &&
                        sim.Accept(alarm, true) && device.Fault == null && device.IsRunning == running &&
                        !sim.Accept(alarm, true), fault.Code + " repair/accept/restore and duplicate prevention");
                }
                foreach (var device in sim.devices)
                {
                    var fault = WorkshopFaultCatalog.For(device.kind)[0];
                    if (!device.IsRunning) device.ToggleOperation();
                    Check(sim.TriggerProgressiveFault(device, fault.Code) && device.IsRunning && device.Snapshot.Progress == 0,
                        fault.Code + " progressive injection begins running");
                    var instance = device.FaultInstance;
                    device.Sample(instance.StartedAt + 18);
                    Check(device.Snapshot.Progress == 1 && device.Snapshot.ProtectionTriggered == fault.Trips,
                        fault.Code + " progressive protection threshold");
                }
                Check(sim.Alarms.Count(a => !a.resolved) == 4 && sim.devices.Select(d => d.FaultInstance.Id).Distinct().Count() == 4,
                    "Four concurrent active instances remain independent");
                sim.ResetDemo();
                foreach (var mode in new[] { WorkshopTrainingMode.Guided, WorkshopTrainingMode.Exam })
                {
                    var device = sim.devices.Single(d => d.deviceId == "PLC-01");
                    Check(sim.TriggerTrainingFault(device, "PLC-CONTACT", mode, true), "Training injection " + mode);
                    var alarm = sim.ActiveAlarm;
                    if (mode == WorkshopTrainingMode.Exam)
                    {
                        Check(sim.IsExam(device) && !device.StatusText.Contains(alarm.fault.Name), "Exam neutral device title");
                        CallUI("OpenEquipment"); CallUI("OpenAnalysis");
                        Check(!Member<GameObject>("equipmentDialog").activeSelf &&
                            !Member<GameObject>("abaqusDialog").activeSelf, "Exam answer-bearing previews blocked");
                    }
                    Check(!sim.RecordTraining(alarm, 3, "PLC-CONTACT", "PLC-CONTACT", true) && sim.StartInspection(alarm),
                        "Training evidence requires arrival");
                    float deadline = Time.realtimeSinceStartup + 35;
                    while (!sim.inspector.HasArrived && Time.realtimeSinceStartup < deadline) yield return null;
                    Check(sim.inspector.HasArrived, "Training actual arrival");
                    CallUI("OpenOrder", alarm); CallUI("OpenTrainingRecord");
                    yield return new WaitForSecondsRealtime(.3f);
                    FitButtons(Member<GameObject>("trainingDialog").transform);
                    Capture("training-" + mode);
                    yield return new WaitForSecondsRealtime(.3f);
                    CallUI("CompactBack");
                    Check(!sim.SubmitRepair(alarm, "no evidence"), "Empty training evidence rejected");
                    Check(sim.RecordTraining(alarm, 3, "PLC-LOW", "PLC-LOW", false) &&
                        !sim.SubmitRepair(alarm, "wrong"), "Wrong diagnosis rejected");
                    Check(sim.RecordTraining(alarm, 3, "PLC-CONTACT", "PLC-CONTACT", false) &&
                        sim.SubmitRepair(alarm, "正确证据诊断") && !sim.Accept(alarm, true), "Retest required before acceptance");
                    int history = alarm.training.History.Count;
                    Check(sim.Accept(alarm, false) && alarm.reworkCount == 1 && alarm.training.History.Count > history &&
                        alarm.training.EvidenceMask == 3 && alarm.training.RepairCode == null &&
                        sim.StartInspection(alarm), "Rejection preserves diagnostic history and requires rework");
                    deadline = Time.realtimeSinceStartup + 35;
                    while (!sim.inspector.HasArrived && Time.realtimeSinceStartup < deadline) yield return null;
                    Check(sim.RecordTraining(alarm, 3, "PLC-CONTACT", "PLC-CONTACT", false) &&
                        sim.SubmitRepair(alarm, "返工复核") && sim.RecordTraining(alarm, 0, null, null, true) &&
                        sim.Accept(alarm, true) && sim.Records[0].trainingSummary.Contains("已记录"),
                        "Training rework, retest, acceptance and archived summary");
                }
            }
            finally { agent.speed = speed; agent.acceleration = acceleration; }
        }

        WorkshopAbaqusResult SyntheticEnvelope() => new WorkshopAbaqusResult {
            schemaVersion = 1, source = "Abaqus ODB", deviceId = "P-01", faultCode = "P-ALIGN",
            job = "SYNTHETIC-PROTOCOL-ONLY", step = "Synthetic", region = "TEST-1",
            odbSha256 = new string('a', 64), exportedAt = "2026-11-15", analysisDescription = "合成协议测试 · 不是真实 ODB",
            timeMeaning = "physical", timeUnit = "s", stressUnit = "MPa", displacementUnit = "mm", temperatureUnit = "C",
            hasTemperature = true, frames = new[] { new WorkshopAbaqusFrame { time = 0, maxTemperatureC = 25 },
                new WorkshopAbaqusFrame { time = 2, maxTemperatureC = 35 }, new WorkshopAbaqusFrame { time = 7, maxTemperatureC = 80 } }
        };
        void Reject(Action action, string reason)
        {
            bool rejected = false;
            try { action(); } catch (Exception e) when (e is ArgumentException || e is IOException) { rejected = true; }
            Check(rejected, "Protocol rejection: " + reason);
        }
        void VerifyEnvelopeProtocol()
        {
            var result = SyntheticEnvelope();
            string json = JsonUtility.ToJson(result);
            var parsed = WorkshopAbaqusResult.Parse(json, "P-01", "P-ALIGN");
            Check(parsed.At(-1).maxTemperatureC == 25 && parsed.At(4).maxTemperatureC == 53 &&
                parsed.At(8).maxTemperatureC == 80 && !parsed.hasStress, "Envelope irregular-axis interpolation and clamping");
            Reject(() => WorkshopAbaqusResult.Parse(json, "F-01", "P-ALIGN"), "wrong device");
            Reject(() => WorkshopAbaqusResult.Parse(json.Replace("\"maxTemperatureC\":25", "\"omitted\":25"), "P-01", "P-ALIGN"), "missing active channel");
            Reject(() => WorkshopAbaqusResult.Parse(json.Replace(new string('a', 64), "bad"), "P-01", "P-ALIGN"), "bad ODB hash");
            Reject(() => WorkshopAbaqusResult.Parse(json.Replace("\"time\":2", "\"time\":0"), "P-01", "P-ALIGN"), "duplicate time");
            result.frames = new[] { new WorkshopAbaqusFrame { time = 3, maxTemperatureC = 42 } };
            Check(WorkshopAbaqusResult.Parse(JsonUtility.ToJson(result), "P-01", "P-ALIGN").At(100).maxTemperatureC == 42,
                "Envelope single temperature frame");
        }

        WorkshopAbaqusFieldResult VerifyFieldProtocol()
        {
            string root = Path.Combine(folder, "synthetic-v2-protocol-only");
            Directory.CreateDirectory(root);
            var mesh = new WorkshopFieldMesh { meshId = "fixed-0", instanceName = "TEST-1",
                nodeLabels = new[] { 7, 29, 450, 902 }, coordinatesM = new float[] { 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1 },
                elements = new[] { new WorkshopFieldElement { elementLabel = 84, type = "DC3D4", nodeIndices = new[] { 0, 1, 2, 3 } } },
                triangles = new[] { 0, 2, 1, 0, 1, 3, 1, 2, 3, 2, 0, 3 },
                triangleElementLabels = new[] { 84, 84, 84, 84 }, triangleFaces = new[] { 1, 2, 3, 4 } };
            File.WriteAllText(Path.Combine(root, "mesh.json"), JsonUtility.ToJson(mesh));
            var files = new List<WorkshopFieldFile>(); var frames = new List<WorkshopFieldIndex>();
            void Index(string name)
            {
                using var sha = SHA256.Create();
                files.Add(new WorkshopFieldFile { path = name, sha256 = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(Path.Combine(root, name)))).Replace("-", "").ToLowerInvariant() });
            }
            Index("mesh.json");
            for (int i = 0; i < 3; i++)
            {
                string name = "frame-" + i + ".json";
                File.WriteAllText(Path.Combine(root, name), JsonUtility.ToJson(new WorkshopFieldFrame { frameIndex = i, axisValue = i * 2,
                    meshId = "fixed-0", temperatureC = new float[] { 25 + i, 40 + i, 60 + i, 80 + i } }));
                Index(name); frames.Add(new WorkshopFieldIndex { frameIndex = i, axisValue = i * 2, path = name });
            }
            var m = new WorkshopFieldManifest { schemaVersion = 2, source = "Abaqus ODB", deviceId = "PLC-01", faultCode = "PLC-CONTACT",
                caseId = "SYNTHETIC-PROTOCOL-ONLY", componentId = "cabinet.fieldTerminal.candidate", modelVersion = "synthetic",
                job = "SYNTHETIC-PROTOCOL-ONLY", step = "Synthetic", region = "TEST-1", odbSha256 = new string('a', 64),
                inputSha256 = new string('b', 64), exporterVersion = "test", solverVersion = "not-a-solver",
                analysisDescription = "合成协议测试 · 不是真实 ODB", exportedAt = "2026-11-15", axisKind = "loadParameter", axisUnit = "1",
                coordinatesSpace = "globalCartesian", coordinatesUnit = "m", coordinateConversion = "identity", sourceLengthUnit = "m",
                sourceTemperatureUnit = "C", lengthToM = 1, displayTransform = new float[] { 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1 },
                fieldName = "temperatureC", fieldUnit = "C", outputPosition = "NODAL", averaging = "none", meshPath = "mesh.json",
                files = files.ToArray(), frames = frames.ToArray() };
            string manifest = Path.Combine(root, "manifest.json");
            string json = JsonUtility.ToJson(m); File.WriteAllText(manifest, json);
            var result = WorkshopAbaqusFieldResult.Open(root, "PLC-01", "PLC-CONTACT", true);
            Check(result.Mesh.nodeLabels[2] == 450 && result.Frame(1).temperatureC[2] == 61 && result.CachedFrames <= 2 &&
                result.AxisLabel == "载荷参数", "V2 original labels, raw nodal values, discrete load axis and bounded frame cache");
            Reject(() => WorkshopAbaqusFieldResult.Open(root, "P-01", "PLC-CONTACT"), "V2 wrong binding");
            Reject(() => WorkshopAbaqusFieldResult.SafePath(root, "../mesh.json"), "V2 traversal");
            File.WriteAllText(manifest, json.Replace("\"axisUnit\":\"1\"", "\"axisUnit\":\"s\""));
            Reject(() => WorkshopAbaqusFieldResult.Open(root, "PLC-01", "PLC-CONTACT"), "V2 static parameter cannot masquerade as seconds");
            File.WriteAllText(manifest, json);
            string framePath = Path.Combine(root, "frame-0.json"); string frameJson = File.ReadAllText(framePath);
            File.AppendAllText(framePath, " ");
            Reject(() => WorkshopAbaqusFieldResult.Open(root, "PLC-01", "PLC-CONTACT"), "V2 corrupted frame hash");
            File.WriteAllText(framePath, frameJson);
            return WorkshopAbaqusFieldResult.Open(root, "PLC-01", "PLC-CONTACT", true);
        }
    }
}
