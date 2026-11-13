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
        Vector3[] fanRest, fanBent, fanOpened, fanCurrent;
        readonly List<Vector2Int> fanSeam = new List<Vector2Int>();
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
            // FBX may split/reorder vertices; identify the authored seam by coincident rest
            // positions with different opening deltas, rather than copying Blender vertex IDs.
            for (int i = 0; i < fanRest.Length; i++)
                for (int j = i + 1; j < fanRest.Length; j++)
                    if ((fanRest[i] - fanRest[j]).sqrMagnitude < 1e-12f &&
                        (fanOpened[i] - fanOpened[j]).sqrMagnitude > 1e-10f)
                        fanSeam.Add(new Vector2Int(i, j));
            if (fanSeam.Count == 0)
                throw new InvalidOperationException("Reviewed fan blade has no opening seam.");
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
            float bend = code == "F-CRACK" || code == "F-BEAR"
                ? Mathf.SmoothStep(0, 1, Mathf.Min(Progress / CrackStart, 1)) : 0;
            float opening = code == "F-CRACK"
                ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((Progress - CrackStart) / (1 - CrackStart))) : 0;
            if (code == "F-BEAR") bend *= .12f;
            for (int i = 0; i < fanRest.Length; i++)
            {
                Vector3 delta = Vector3.LerpUnclamped(fanBent[i] * bend, fanOpened[i], opening);
                fanCurrent[i] = fanRest[i] + delta;
                DeflectionMm = Mathf.Max(DeflectionMm, delta.magnitude * 1000);
            }
            var mesh = DamageSurface.sharedMesh;
            mesh.vertices = fanCurrent;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            float maxGap = -1;
            foreach (var pair in fanSeam)
            {
                float gap = Vector3.Distance(fanCurrent[pair.x], fanCurrent[pair.y]);
                if (gap <= maxGap) continue;
                maxGap = gap; fanLeftLip = pair.x; fanRightLip = pair.y;
            }
            OpeningMm = maxGap * 1000;
            // This authored partial seam retains a rear ligament, so it never detaches.
            Recipe = code == "F-CRACK"
                ? "原叶片预制形变：弯曲 → 前缘裂口张开（保留后缘连接，非断裂求解）"
                : code == "F-BEAR" ? "原叶片轻微形变示意（非轴承动力学求解）" : "原始叶片";
        }
    }
}
