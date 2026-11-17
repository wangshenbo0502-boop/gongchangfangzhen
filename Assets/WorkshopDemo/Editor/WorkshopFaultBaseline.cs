using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorkshopDemo.Editor
{
    public static class WorkshopFaultBaseline
    {
        [Serializable] sealed class Report { public Model[] models; }
        [Serializable] sealed class Model
        {
            public string resource;
            public Bounds bounds;
            public Part[] parts;
        }
        [Serializable] sealed class Part
        {
            public string path;
            public Vector3 localPosition;
            public Quaternion localRotation;
            public Vector3 localScale;
            public Bounds localBounds, worldBounds;
            public int vertices, triangles, materialSlots;
            public string[] blendShapes;
        }

        public static void Export()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-workshop-fault-baseline");
            if (index < 0 || index + 1 == args.Length)
                throw new ArgumentException("Provide -workshop-fault-baseline <output directory>.");
            Directory.CreateDirectory(args[index + 1]);
            var report = new Report { models = new[] { "water-pump", "silo", "industrial-fan", "electrical-cabinet" }
                .Select(Inspect).ToArray() };
            File.WriteAllText(Path.Combine(args[index + 1], "hierarchy.json"), JsonUtility.ToJson(report, true));
        }

        static Model Inspect(string resource)
        {
            var prefab = Resources.Load<GameObject>("ReviewedEquipment/" + resource);
            var root = UnityEngine.Object.Instantiate(prefab);
            try
            {
                var parts = root.GetComponentsInChildren<Transform>(true).Select(t =>
                {
                    var filter = t.GetComponent<MeshFilter>();
                    var mesh = filter ? filter.sharedMesh : null;
                    var renderer = t.GetComponent<Renderer>();
                    return new Part {
                        path = AnimationUtility.CalculateTransformPath(t, root.transform),
                        localPosition = t.localPosition, localRotation = t.localRotation, localScale = t.localScale,
                        localBounds = mesh ? mesh.bounds : default, worldBounds = renderer ? renderer.bounds : default,
                        vertices = mesh ? mesh.vertexCount : 0,
                        triangles = mesh ? Enumerable.Range(0, mesh.subMeshCount).Sum(i => (int)mesh.GetIndexCount(i) / 3) : 0,
                        materialSlots = renderer ? renderer.sharedMaterials.Length : 0,
                        blendShapes = mesh ? Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName).ToArray()
                            : Array.Empty<string>()
                    };
                }).ToArray();
                var bounds = root.GetComponentsInChildren<Renderer>().Select(r => r.bounds)
                    .Aggregate((a, b) => { a.Encapsulate(b); return a; });
                return new Model { resource = resource, bounds = bounds, parts = parts };
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
