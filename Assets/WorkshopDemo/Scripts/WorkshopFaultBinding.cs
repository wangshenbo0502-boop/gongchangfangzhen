using System;
using System.Collections.Generic;
using UnityEngine;

namespace WorkshopDemo
{
    internal sealed class WorkshopFaultBinding
    {
        [Serializable] internal sealed class Definition
        {
            public string id, model;
            public string[] paths;
            public Vector3 anchorLocalM, normalLocal, focusSizeM;
            public bool anchorCalibrated;
            public Vector3 surfaceLocalM, surfaceNormalLocal;
            public bool surfaceCached;
            public string surfaceSource;
        }

        [Serializable] sealed class Manifest
        {
            public int schemaVersion;
            public Definition[] bindings;
        }

        static readonly Lazy<Manifest> manifest = new Lazy<Manifest>(() =>
        {
            var asset = Resources.Load<TextAsset>("WorkshopFaults/bindings");
            return asset ? JsonUtility.FromJson<Manifest>(asset.text) : null;
        });

        public Definition Settings { get; }
        public MeshRenderer[] Renderers { get; }
        public Transform Surface => Renderers[0].transform;
        public Vector3 Anchor => Surface.TransformPoint(Settings.anchorLocalM);
        public Vector3 Normal => Surface.TransformDirection(Settings.normalLocal).normalized;

        WorkshopFaultBinding(Definition settings, MeshRenderer[] renderers)
        {
            Settings = settings;
            Renderers = renderers;
        }

        public static Dictionary<string, WorkshopFaultBinding> Resolve(Transform root, string model, string deviceId)
        {
            var result = new Dictionary<string, WorkshopFaultBinding>(StringComparer.Ordinal);
            var data = manifest.Value;
            if (data?.schemaVersion != 1 || data.bindings == null)
            {
                Debug.LogWarning(deviceId + ": 故障部件绑定清单缺失或版本无效");
                return result;
            }
            foreach (var settings in data.bindings)
            {
                if (settings.model != model) continue;
                var renderers = Array.ConvertAll(settings.paths, path =>
                {
                    var node = FindUniquePath(root, path);
                    return node ? node.GetComponent<MeshRenderer>() : null;
                });
                if (renderers.Length == 0 || Array.Exists(renderers, renderer => !renderer))
                {
                    Debug.LogWarning(deviceId + ": 故障部件绑定缺失或重复 (" + settings.id + ")");
                    continue;
                }
                result.Add(settings.id, new WorkshopFaultBinding(settings, renderers));
            }
            return result;
        }

        static Transform FindUniquePath(Transform root, string path)
        {
            foreach (string segment in path.Split('/'))
            {
                Transform match = null;
                foreach (Transform child in root)
                {
                    if (child.name != segment) continue;
                    if (match) return null;
                    match = child;
                }
                if (!match) return null;
                root = match;
            }
            return root;
        }

        public Bounds FocusBounds()
        {
            var size = Settings.focusSizeM;
            var across = Vector3.Cross(Vector3.up, Normal).normalized;
            var scale = Surface.TransformVector(Settings.normalLocal.normalized).magnitude;
            var center = Anchor - Vector3.up * (size.y * .22f * scale);
            var bounds = new Bounds(center, Vector3.zero);
            for (int i = 0; i < 8; i++)
                bounds.Encapsulate(center + scale * .5f * (across * size.x * ((i & 1) == 0 ? -1 : 1) +
                    Vector3.up * size.y * ((i & 2) == 0 ? -1 : 1) + Normal * size.z * ((i & 4) == 0 ? -1 : 1)));
            return bounds;
        }
    }
}
