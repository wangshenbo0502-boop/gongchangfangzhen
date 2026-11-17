using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorkshopDemo
{
    public sealed partial class WorkshopEquipmentModel
    {
        [Serializable] sealed class FanMotion { public float t; public Vector3 offset; }
        [Serializable] sealed class FanPart { public string name, label; public FanMotion[] path; }
        [Serializable] sealed class FanParts { public int motionVersion; public FanPart[] parts; }
        FanParts reviewedFanParts;
        public bool ReviewedFanModel => reviewedFanParts != null;
        Vector3[] fanRest, fanBent, fanOpened, fanCurrent, fanWear, fanRadialOpening, fanSeamCurve;
        float[] fanLocalWeights;
        float fanApplied = -1, fanOpening, fanDeflection;
        string fanAppliedCode;
        readonly List<Vector2Int> fanSeam = new List<Vector2Int>();
        Mesh fanCrackFaceMesh;
        Vector3[] fanCrackFaceVertices;
        GameObject fanCrackFaceObject;
        int fanLeftLip, fanRightLip;

        bool ReviewedFan()
        {
            var prefab = Resources.Load<GameObject>("ReviewedEquipment/industrial-fan");
            var contract = Resources.Load<TextAsset>("ReviewedEquipment/fan-parts");
            if (!prefab || !contract) return false;
            reviewedFanParts = JsonUtility.FromJson<FanParts>(contract.text);
            if (reviewedFanParts.motionVersion != 4)
                throw new InvalidOperationException("Unsupported reviewed fan motion version.");
            var root = Instantiate(prefab, transform, false).transform;
            // The inlet faces the station's service side, toward the inspection corridor.
            root.localRotation = Quaternion.Euler(0, -90, 0) * root.localRotation;
            var nodes = root.GetComponentsInChildren<Transform>(true);
            Vector3 origin = nodes.Single(t => t.name == "FanMotionOrigin").position;
            Vector3 x = nodes.Single(t => t.name == "FanMotionX").position - origin;
            Vector3 y = nodes.Single(t => t.name == "FanMotionY").position - origin;
            Vector3 z = nodes.Single(t => t.name == "FanMotionZ").position - origin;
            foreach (var part in reviewedFanParts.parts)
            {
                var assembly = nodes.Single(t => t.name == part.name);
                assemblies.Add(assembly);
                // References undergo the same FBX conversion as geometry; cache in parent space.
                foreach (var key in part.path)
                    key.offset = assembly.parent.InverseTransformVector(
                        x * key.offset.x + y * key.offset.y + z * key.offset.z);
            }
            Rotor = nodes.Single(t => t.name == "industrial-fan_rotor");
            DamageSurface = root.GetComponentsInChildren<MeshFilter>()
                .Single(f => f.sharedMesh.blendShapeCount > 0);
            var mesh = Instantiate(DamageSurface.sharedMesh);
            mesh.name = "Reviewed fan blade - teaching deformation";
            ownedMeshes.Add(mesh);
            DamageSurface.sharedMesh = mesh;
            fanRest = mesh.vertices;
            fanCurrent = new Vector3[fanRest.Length];
            fanBent = FanShape(mesh, "F-CRACK_bulged");
            fanOpened = FanShape(mesh, "F-CRACK_opened");
            var radialPositions = new Vector3[fanRest.Length];
            float outerRadius = 0;
            for (int i = 0; i < fanRest.Length; i++)
            {
                var point = Rotor.InverseTransformPoint(DamageSurface.transform.TransformPoint(fanRest[i]));
                radialPositions[i] = new Vector3(point.x, point.y, 0);
                outerRadius = Mathf.Max(outerRadius, radialPositions[i].magnitude);
            }
            fanWear = new Vector3[fanRest.Length];
            fanRadialOpening = new Vector3[fanRest.Length];
            for (int i = 0; i < fanRest.Length; i++)
            {
                // The outer 12% of the actual rotor radius carries a preset 6 mm teaching defect.
                var radial = radialPositions[i];
                float edge = Mathf.InverseLerp(outerRadius * .88f, outerRadius, radial.magnitude);
                float scallop = .65f + .35f * Mathf.Sin(Mathf.Atan2(radial.y, radial.x) * 31);
                fanWear[i] = DamageSurface.transform.InverseTransformVector(Rotor.TransformVector(
                    -radial.normalized * (.006f * edge * edge * scallop)));
                // Open the radial cut across its lips; axial motion alone overlaps in the inspection view.
                fanRadialOpening[i] = DamageSurface.transform.InverseTransformVector(Rotor.TransformVector(
                    radial.normalized)) * fanOpened[i].magnitude;
            }
            // FBX may split/reorder vertices; identify the authored seam by coincident rest
            // positions with different opening deltas, rather than copying Blender vertex IDs.
            for (int i = 0; i < fanRest.Length; i++)
                for (int j = i + 1; j < fanRest.Length; j++)
                    if ((fanRest[i] - fanRest[j]).sqrMagnitude < 1e-12f &&
                        (fanOpened[i] - fanOpened[j]).sqrMagnitude > 1e-10f)
                        fanSeam.Add(new Vector2Int(i, j));
            if (fanSeam.Count == 0)
                throw new InvalidOperationException("Reviewed fan blade has no opening seam.");
            fanLocalWeights = new float[fanRest.Length];
            fanSeamCurve = new Vector3[fanRest.Length];
            Vector3 start = fanRest[fanSeam[0].x], end = start;
            float spanSquared = 0;
            foreach (var a in fanSeam)
                foreach (var b in fanSeam)
                {
                    float span = (fanRest[a.x] - fanRest[b.x]).sqrMagnitude;
                    if (span <= spanSquared) continue;
                    spanSquared = span; start = fanRest[a.x]; end = fanRest[b.x];
                }
            Vector3 chord = end - start;
            fanSeam.Sort((a, b) =>
                Vector3.Dot((fanRest[a.x] + fanRest[a.y]) * .5f - start, chord).CompareTo(
                    Vector3.Dot((fanRest[b.x] + fanRest[b.y]) * .5f - start, chord)));
            BuildFanCrackFace();
            for (int i = 0; i < fanRest.Length; i++)
            {
                float distance = float.MaxValue;
                foreach (var pair in fanSeam)
                    distance = Mathf.Min(distance, Vector3.Distance(fanRest[i], fanRest[pair.x]));
                fanLocalWeights[i] = 1 - Mathf.SmoothStep(0, 1, distance / .09f);
                // A preset in-surface meander curves the doubled seam without separating its
                // paired vertices or moving the shared rear ligament. The source mesh stays intact.
                float u = Mathf.Clamp01(Vector3.Dot(fanRest[i] - start, chord) / spanSquared);
                float meander = .0045f * Mathf.Sin(Mathf.PI * u) *
                    Mathf.Sin(Mathf.PI * 2 * u + .4f) * fanLocalWeights[i];
                fanSeamCurve[i] = DamageSurface.transform.InverseTransformVector(Rotor.TransformVector(
                    radialPositions[i].normalized)) * meander;
            }
            var environment = Resources.Load<Cubemap>("ReviewedEquipment/fan-reflection");
            if (!environment) throw new InvalidOperationException("Reviewed fan reflection is missing.");
            var renderers = root.GetComponentsInChildren<Renderer>();
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            var probe = new GameObject("Reviewed fan finish reflection", typeof(ReflectionProbe))
                .GetComponent<ReflectionProbe>();
            probe.transform.SetParent(transform, false);
            probe.transform.position = bounds.center;
            probe.mode = ReflectionProbeMode.Custom;
            probe.customBakedTexture = environment;
            probe.size = new Vector3(1.75f, 1.95f, 1.75f);
            probe.blendDistance = 0;
            probe.intensity = .40f;
            // Keep the workshop probe inside this station's envelope.
            foreach (var renderer in renderers)
            {
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Simple;
                renderer.probeAnchor = probe.transform;
            }
            return true;
        }

        static Vector3[] FanShape(Mesh mesh, string suffix)
        {
            int index = Enumerable.Range(0, mesh.blendShapeCount)
                .Single(i => mesh.GetBlendShapeName(i).EndsWith(suffix, StringComparison.Ordinal));
            var delta = new Vector3[mesh.vertexCount];
            mesh.GetBlendShapeFrameVertices(index, mesh.GetBlendShapeFrameCount(index) - 1,
                delta, null, null);
            return delta;
        }

        internal bool TryGetFanSeamBounds(out Bounds bounds)
        {
            bounds = default;
            if (!DamageSurface || fanSeam.Count == 0 || fanCurrent == null) return false;
            var surface = DamageSurface.transform;
            bounds = new Bounds(surface.TransformPoint(fanCurrent[fanSeam[0].x]), Vector3.zero);
            foreach (var pair in fanSeam)
            {
                bounds.Encapsulate(surface.TransformPoint(fanCurrent[pair.x]));
                bounds.Encapsulate(surface.TransformPoint(fanCurrent[pair.y]));
            }
            bounds.Expand(.012f * Mathf.Abs(transform.lossyScale.x));
            return true;
        }

        void ReviewedFanDisassembly(float step)
        {
            float progress = Mathf.Clamp01(step / assemblies.Count);
            for (int i = 0; i < assemblies.Count; i++)
            {
                var path = reviewedFanParts.parts[i].path;
                Vector3 offset = path[path.Length - 1].offset;
                for (int k = 1; k < path.Length; k++)
                {
                    if (progress > path[k].t) continue;
                    float t = Mathf.Clamp01((progress - path[k - 1].t) / (path[k].t - path[k - 1].t));
                    t = t * t * (3 - 2 * t);
                    offset = Vector3.LerpUnclamped(path[k - 1].offset, path[k].offset, t);
                    break;
                }
                assemblies[i].localPosition = assemblyOrigins[i] + offset;
            }
        }

        void ReviewedFanDamage(string code)
        {
            float p = code == "F-CRACK" || code == "F-BLADE" ? Progress : 0;
            if (fanApplied == p && fanAppliedCode == code)
            {
                OpeningMm = fanOpening; DeflectionMm = fanDeflection;
                return;
            }
            fanApplied = p;
            fanAppliedCode = code;
            float bend = .025f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, .35f, p));
            float curve = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, .35f, p));
            float opening = .35f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.2f, .8f, p));
            DeflectionMm = 0;
            for (int i = 0; i < fanRest.Length; i++)
            {
                Vector3 delta = (fanBent[i] * bend * (1 - opening) +
                    (fanRadialOpening[i] + fanOpened[i] * .15f) * opening) * fanLocalWeights[i];
                if (code == "F-CRACK") delta += fanSeamCurve[i] * curve;
                if (code == "F-BLADE")
                {
                    delta = fanWear[i] * Mathf.SmoothStep(0, 1, p);
                }
                fanCurrent[i] = fanRest[i] + delta;
                DeflectionMm = Mathf.Max(DeflectionMm, delta.magnitude * 1000);
            }
            var mesh = DamageSurface.sharedMesh;
            mesh.vertices = fanCurrent;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            for (int i = 0; i < fanSeam.Count; i++)
            {
                fanCrackFaceVertices[i * 2] = fanCurrent[fanSeam[i].x];
                fanCrackFaceVertices[i * 2 + 1] = fanCurrent[fanSeam[i].y];
            }
            fanCrackFaceMesh.vertices = fanCrackFaceVertices;
            fanCrackFaceMesh.RecalculateNormals();
            fanCrackFaceMesh.RecalculateBounds();
            fanCrackFaceObject.SetActive(code == "F-CRACK" && p >= .2f);
            float maxGap = -1;
            foreach (var pair in fanSeam)
            {
                float gap = Vector3.Distance(fanCurrent[pair.x], fanCurrent[pair.y]);
                if (gap <= maxGap) continue;
                maxGap = gap; fanLeftLip = pair.x; fanRightLip = pair.y;
            }
            OpeningMm = code == "F-CRACK" ? maxGap * 1000 : 0;
            fanOpening = OpeningMm; fanDeflection = DeflectionMm;
            // This authored partial seam retains a rear ligament, so it never detaches.
            Recipe = code == "F-CRACK"
                ? "原叶片局部预制曲线裂面 / 后缘连接保留（非断裂求解）"
                : code == "F-BLADE" ? "原叶片外缘预设磨蚀 ≤6 mm / 非增长求解" : "原始叶片";
        }

        void BuildFanCrackFace()
        {
            fanCrackFaceVertices = new Vector3[fanSeam.Count * 2];
            var triangles = new int[(fanSeam.Count - 1) * 6];
            for (int i = 0; i < fanSeam.Count - 1; i++)
            {
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3, t = i * 6;
                triangles[t] = a; triangles[t + 1] = b; triangles[t + 2] = c;
                triangles[t + 3] = b; triangles[t + 4] = d; triangles[t + 5] = c;
            }
            fanCrackFaceMesh = new Mesh
            {
                name = "Reviewed fan local fracture face",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            fanCrackFaceMesh.vertices = fanCrackFaceVertices;
            fanCrackFaceMesh.triangles = triangles;
            ownedMeshes.Add(fanCrackFaceMesh);

            fanCrackFaceObject = new GameObject("F-CRACK independent fracture face");
            fanCrackFaceObject.layer = DamageSurface.gameObject.layer;
            fanCrackFaceObject.transform.SetParent(DamageSurface.transform, false);
            fanCrackFaceObject.AddComponent<MeshFilter>().sharedMesh = fanCrackFaceMesh;
            var material = Finish("Fresh fan fracture metal", new Color(.24f, .27f, .28f), .72f, .3f);
            material.SetFloat("_Cull", 0);
            fanCrackFaceObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            fanCrackFaceObject.SetActive(false);
        }
    }
}
