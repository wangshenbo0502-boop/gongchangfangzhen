using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorkshopDemo
{
    public sealed partial class WorkshopEquipmentModel
    {
        [Serializable] sealed class TankMotionKey
        {
            public float time;
            public Vector3 offset;
        }

        [Serializable] sealed class TankPart
        {
            public string name, label, anchor;
            public TankMotionKey[] path;
        }

        [Serializable] sealed class TankManifest
        {
            public int motionVersion;
            public float durationSeconds;
            public TankPart[] parts;
        }

        TankManifest tankManifest;
        Vector3[] tankAnchors;
        Bounds[] tankAssemblyBounds;
        ReflectionProbe tankProbe;
        float tankLastProgress = -1;
        public bool ReviewedTankModel => tankManifest != null;
        public float WorkshopScale => ReviewedTankModel ? .60f : 1;
        public Bounds WorkshopBounds => new Bounds(NativeBounds.center * WorkshopScale, NativeBounds.size * WorkshopScale);

        bool ReviewedTank()
        {
            var prefab = Resources.Load<GameObject>("ReviewedEquipment/silo");
            var contract = Resources.Load<TextAsset>("ReviewedEquipment/tank-parts");
            if (!prefab || !contract) return false;
            tankManifest = JsonUtility.FromJson<TankManifest>(contract.text);
            if (tankManifest.motionVersion != 3 || tankManifest.parts.Length != 12 || tankManifest.durationSeconds <= 0)
                throw new InvalidOperationException("Unsupported reviewed tank motion.");
            var root = Instantiate(prefab, transform, false).transform;
            var nodes = root.GetComponentsInChildren<Transform>(true);
            Vector3 origin = nodes.Single(t => t.name == "TankMotionOrigin").position;
            Vector3 axisX = nodes.Single(t => t.name == "TankMotionX").position - origin;
            Vector3 axisY = nodes.Single(t => t.name == "TankMotionY").position - origin;
            Vector3 axisZ = nodes.Single(t => t.name == "TankMotionZ").position - origin;
            tankProbe = new GameObject("Reviewed tank reflection").AddComponent<ReflectionProbe>();
            tankProbe.transform.SetParent(transform, false);
            tankProbe.mode = ReflectionProbeMode.Custom;
            tankProbe.customBakedTexture = Resources.Load<Cubemap>("ReviewedEquipment/tank-reflection");
            tankProbe.blendDistance = 0;
            tankProbe.intensity = .38f;
            tankAnchors = new Vector3[tankManifest.parts.Length];
            tankAssemblyBounds = new Bounds[tankAnchors.Length];
            for (int i = 0; i < tankAnchors.Length; i++)
            {
                var part = tankManifest.parts[i];
                var assembly = nodes.Single(t => t.name == part.name);
                assemblies.Add(assembly);
                foreach (var key in part.path)
                {
                    var v = key.offset;
                    key.offset = assembly.parent.InverseTransformVector(axisX * v.x + axisY * v.y + axisZ * v.z);
                }
                var renderers = assembly.GetComponentsInChildren<MeshRenderer>();
                tankAnchors[i] = assembly.InverseTransformPoint(renderers.Single(r => r.name == part.anchor).bounds.center);
                var local = new Bounds(tankAnchors[i], Vector3.zero);
                foreach (var renderer in renderers)
                {
                    renderer.reflectionProbeUsage = ReflectionProbeUsage.Simple;
                    renderer.probeAnchor = tankProbe.transform;
                    var b = renderer.localBounds;
                    var toAssembly = assembly.worldToLocalMatrix * renderer.localToWorldMatrix;
                    for (int corner = 0; corner < 8; corner++)
                        local.Encapsulate(toAssembly.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents, BoundsCorner(corner))));
                }
                tankAssemblyBounds[i] = local;
            }
            DamageSurface = nodes.Single(t => t.name == "TNK_01_Shell_8mmWall").GetComponent<MeshFilter>();
            return true;
        }

        public Bounds ReviewedTankBounds(int assembly = -1)
        {
            int first = assembly < 0 ? 0 : assembly;
            int end = assembly < 0 ? assemblies.Count : assembly + 1;
            var bounds = new Bounds(AssemblyAnchor(first), Vector3.zero);
            for (int i = first; i < end; i++)
            {
                var local = tankAssemblyBounds[i];
                for (int corner = 0; corner < 8; corner++)
                    bounds.Encapsulate(assemblies[i].TransformPoint(local.center + Vector3.Scale(local.extents, BoundsCorner(corner))));
            }
            return bounds;
        }

        void ReviewedTankDisassembly(float step)
        {
            float progress = Mathf.Clamp01(step / assemblies.Count);
            if (tankLastProgress == progress) return;
            tankLastProgress = progress;
            for (int i = 0; i < assemblies.Count; i++)
            {
                var path = tankManifest.parts[i].path;
                Vector3 offset = path[path.Length - 1].offset;
                for (int k = 1; k < path.Length; k++)
                {
                    if (progress > path[k].time) continue;
                    var previous = path[k - 1];
                    float u = Mathf.InverseLerp(previous.time, path[k].time, progress);
                    float t = u * u * u * (10 + u * (-15 + 6 * u));
                    offset = Vector3.LerpUnclamped(previous.offset, path[k].offset, t);
                    break;
                }
                assemblies[i].localPosition = assemblyOrigins[i] + offset;
            }
        }
    }
}
