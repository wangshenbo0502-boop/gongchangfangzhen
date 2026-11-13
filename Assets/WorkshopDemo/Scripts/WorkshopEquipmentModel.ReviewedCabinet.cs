using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorkshopDemo
{
    public sealed partial class WorkshopEquipmentModel
    {
        [Serializable] sealed class CabinetTransfer
        {
            public float fadeOutStart, hiddenAt, visibleFrom, fadeInEnd;
        }
        [Serializable] sealed class CabinetPart
        {
            public string name, label, anchor;
            public bool doorAttached;
            public FanMotion[] path;
            public CabinetTransfer transfer;
        }
        [Serializable] sealed class CabinetHinge
        {
            public float x, y, z, angleDegrees, rotationStart, rotationEnd;
        }
        [Serializable] sealed class CabinetManifest
        {
            public int motionVersion;
            public float oneWaySeconds;
            public CabinetHinge hinge;
            public CabinetPart[] parts;
        }
        CabinetManifest cabinetManifest;
        Quaternion[] cabinetHomeRotations;
        Vector3[] cabinetHinges, cabinetHingeAxes, cabinetAnchors;
        float[] cabinetAngles;
        Bounds[] cabinetAssemblyBounds;
        Renderer[] cabinetHarnessRenderers;
        Material[] cabinetHarnessMaterials;
        CabinetTransfer cabinetHarnessTransfer;
        int cabinetHarnessIndex;
        float cabinetVisibility = 1, cabinetLastProgress = -1;
        public bool ReviewedCabinetModel => cabinetManifest != null;
        public float CabinetDoorOpenStep => cabinetManifest.hinge.rotationEnd * AssemblyCount;
        public float AssemblySeconds => cabinetManifest?.oneWaySeconds ?? tankManifest?.durationSeconds ?? (ReviewedFanModel ? 14 : 0);

        bool ReviewedCabinet()
        {
            var prefab = Resources.Load<GameObject>("ReviewedEquipment/electrical-cabinet");
            var contract = Resources.Load<TextAsset>("ReviewedEquipment/cabinet-parts");
            if (!prefab || !contract) return false;
            cabinetManifest = JsonUtility.FromJson<CabinetManifest>(contract.text);
            if (cabinetManifest.motionVersion != 4 || cabinetManifest.parts.Length != 16)
                throw new InvalidOperationException("Unsupported reviewed cabinet motion.");
            var root = Instantiate(prefab, transform, false).transform;
            var nodes = root.GetComponentsInChildren<Transform>(true);
            var manifest = cabinetManifest;
            int count = manifest.parts.Length;
            cabinetHomeRotations = new Quaternion[count];
            cabinetHinges = new Vector3[count];
            cabinetHingeAxes = new Vector3[count];
            cabinetAngles = new float[count];
            cabinetAnchors = new Vector3[count];
            cabinetAssemblyBounds = new Bounds[count];
            var renderers = root.GetComponentsInChildren<Renderer>();
            var probe = new GameObject("Reviewed cabinet reflection", typeof(ReflectionProbe))
                .GetComponent<ReflectionProbe>();
            probe.transform.SetParent(transform, false);
            probe.transform.localPosition = Vector3.up;
            probe.mode = ReflectionProbeMode.Custom;
            probe.customBakedTexture = Resources.Load<Cubemap>("ReviewedEquipment/cabinet-reflection");
            probe.size = new Vector3(1.4f, 2.2f, .85f);
            probe.blendDistance = 0;
            probe.intensity = .38f;
            foreach (var renderer in renderers)
            {
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Simple;
                renderer.probeAnchor = probe.transform;
            }
            for (int i = 0; i < count; i++)
            {
                var part = manifest.parts[i];
                var assembly = nodes.Single(t => t.name == part.name);
                var parent = assembly.parent;
                assemblies.Add(assembly);
                Vector3 origin = parent.InverseTransformPoint(nodes.Single(t => t.name == "CabinetMotionOrigin").position);
                Vector3 x = parent.InverseTransformPoint(nodes.Single(t => t.name == "CabinetMotionX").position) - origin;
                Vector3 y = parent.InverseTransformPoint(nodes.Single(t => t.name == "CabinetMotionY").position) - origin;
                Vector3 z = parent.InverseTransformPoint(nodes.Single(t => t.name == "CabinetMotionZ").position) - origin;
                cabinetHomeRotations[i] = assembly.localRotation;
                cabinetHinges[i] = origin + x * manifest.hinge.x + y * manifest.hinge.y + z * manifest.hinge.z;
                cabinetHingeAxes[i] = z.normalized;
                cabinetAngles[i] = part.doorAttached
                    ? manifest.hinge.angleDegrees * Mathf.Sign(Vector3.Dot(Vector3.Cross(x, y), z)) : 0;
                foreach (var key in part.path)
                    key.offset = x * key.offset.x + y * key.offset.y + z * key.offset.z;
                var owned = assembly.GetComponentsInChildren<Renderer>();
                var anchor = owned.Single(renderer => renderer.name == part.anchor);
                cabinetAnchors[i] = assembly.InverseTransformPoint(anchor.bounds.center);
                var localBounds = new Bounds(cabinetAnchors[i], Vector3.zero);
                foreach (var renderer in owned)
                {
                    Bounds bounds = renderer.localBounds;
                    Matrix4x4 toAssembly = assembly.worldToLocalMatrix * renderer.localToWorldMatrix;
                    for (int j = 0; j < 8; j++)
                        localBounds.Encapsulate(toAssembly.MultiplyPoint3x4(bounds.center +
                            Vector3.Scale(bounds.extents, BoundsCorner(j))));
                }
                cabinetAssemblyBounds[i] = localBounds;
                if (part.transfer != null && part.transfer.fadeInEnd > 0)
                {
                    cabinetHarnessIndex = i;
                    cabinetHarnessTransfer = part.transfer;
                    cabinetHarnessRenderers = owned;
                }
            }
            indicator = nodes.Single(t => t.name == "cabinet_lamp_lens_0");
            DamageSurface = nodes.Single(t => t.name == "CAB_02_Terminals_CopperPEBusbar").GetComponent<MeshFilter>();
            return true;
        }

        static Vector3 BoundsCorner(int index) =>
            new Vector3((index & 1) == 0 ? -1 : 1, (index & 2) == 0 ? -1 : 1, (index & 4) == 0 ? -1 : 1);

        public Bounds ReviewedCabinetBounds(int assembly = -1)
        {
            int first = assembly < 0 ? 0 : assembly;
            int end = assembly < 0 ? assemblies.Count : assembly + 1;
            var bounds = new Bounds(AssemblyAnchor(first), Vector3.zero);
            for (int i = first; i < end; i++)
            {
                if (assembly < 0 && i == cabinetHarnessIndex && cabinetVisibility <= 0) continue;
                var transform = assemblies[i];
                var local = cabinetAssemblyBounds[i];
                for (int j = 0; j < 8; j++)
                    bounds.Encapsulate(transform.TransformPoint(local.center + Vector3.Scale(local.extents, BoundsCorner(j))));
            }
            return bounds;
        }

        void ReviewedCabinetDisassembly(float step)
        {
            float progress = Mathf.Clamp01(step / assemblies.Count);
            if (cabinetLastProgress == progress) return;
            cabinetLastProgress = progress;
            var hinge = cabinetManifest.hinge;
            float swing = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(hinge.rotationStart, hinge.rotationEnd, progress));
            for (int i = 0; i < assemblies.Count; i++)
            {
                var path = cabinetManifest.parts[i].path;
                Vector3 offset = path[path.Length - 1].offset;
                for (int k = 1; k < path.Length; k++)
                {
                    if (progress > path[k].t) continue;
                    var previous = path[k - 1];
                    float t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(previous.t, path[k].t, progress));
                    offset = Vector3.LerpUnclamped(previous.offset, path[k].offset, t);
                    break;
                }
                var rotation = Quaternion.AngleAxis(swing * cabinetAngles[i], cabinetHingeAxes[i]);
                var pivot = cabinetHinges[i];
                Vector3 position = swing == 0 || cabinetAngles[i] == 0 ? assemblyOrigins[i] :
                    pivot + rotation * (assemblyOrigins[i] - pivot);
                assemblies[i].SetLocalPositionAndRotation(position + offset, rotation * cabinetHomeRotations[i]);
            }
            UpdateCabinetHarness(progress);
        }

        void UpdateCabinetHarness(float progress)
        {
            var transfer = cabinetHarnessTransfer;
            float visibility = progress <= transfer.hiddenAt
                ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(transfer.fadeOutStart, transfer.hiddenAt, progress))
                : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(transfer.visibleFrom, transfer.fadeInEnd, progress));
            if (visibility == cabinetVisibility) return;
            if (cabinetHarnessMaterials == null)
            {
                var clones = new Dictionary<Material, Material>();
                foreach (var renderer in cabinetHarnessRenderers)
                    renderer.sharedMaterials = renderer.sharedMaterials.Select(source =>
                    {
                        if (clones.TryGetValue(source, out var material)) return material;
                        material = new Material(source);
                        ownedMaterials.Add(material);
                        clones.Add(source, material);
                        return material;
                    }).ToArray();
                cabinetHarnessMaterials = clones.Values.ToArray();
            }
            if ((visibility > 0) != (cabinetVisibility > 0))
                foreach (var renderer in cabinetHarnessRenderers) renderer.enabled = visibility > 0;
            foreach (var material in cabinetHarnessMaterials)
            {
                material.SetFloat("_CabinetVisibility", visibility);
                if ((visibility >= 1) == (cabinetVisibility >= 1)) continue;
                material.SetShaderPassEnabled("ShadowCaster", visibility >= 1);
                material.SetShaderPassEnabled("DepthOnly", visibility >= 1);
                material.SetShaderPassEnabled("DepthNormals", visibility >= 1);
            }
            cabinetVisibility = visibility;
        }
    }
}
