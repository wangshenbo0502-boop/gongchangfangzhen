using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorkshopDemo.Editor
{
    public static class WorkshopFaultSurfaceBake
    {
        [Serializable] sealed class Definition
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

        const string PathName = "Assets/WorkshopDemo/Resources/WorkshopFaults/bindings.json";

        public static void Bake()
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(PathName));
            foreach (var group in manifest.bindings.GroupBy(d => d.model))
            {
                var prefab = Resources.Load<GameObject>("ReviewedEquipment/" + group.Key);
                var root = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    if (group.Key == "industrial-fan")
                        root.transform.localRotation = Quaternion.Euler(0, -90, 0) * root.transform.localRotation;
                    foreach (var definition in group)
                    {
                        var node = root.transform.Find(definition.paths[0]);
                        if (!node) throw new InvalidOperationException("Missing surface: " + definition.id);
                        var mesh = node.GetComponent<MeshFilter>().sharedMesh;
                        var vertices = mesh.vertices;
                        var triangles = mesh.triangles;
                        // Match the model's runtime orientation, including the fan's inlet rotation.
                        var desiredWorld = group.Key == "industrial-fan" && definition.id == "fan.motor.bearing"
                            ? Vector3.forward : Vector3.back;
                        if (definition.id == "tank.outlet.flange")
                            desiredWorld = new Vector3(0, .7f, -1).normalized;
                        Vector3 normal = node.InverseTransformDirection(desiredWorld).normalized;
                        Vector3 start = mesh.bounds.center + normal * (mesh.bounds.size.magnitude + .01f);
                        var ray = new Ray(start, -normal);
                        float nearest = float.PositiveInfinity;
                        Vector3 hit = default, hitNormal = default;
                        // Intersect the actual surface through its center, rather than choosing a bounds corner.
                        for (int i = 0; i < triangles.Length; i += 3)
                        {
                            var a = vertices[triangles[i]];
                            var b = vertices[triangles[i + 1]];
                            var c = vertices[triangles[i + 2]];
                            var plane = new Plane(a, b, c);
                            if (!plane.Raycast(ray, out float distance) || distance >= nearest) continue;
                            var point = ray.GetPoint(distance);
                            var cross = Vector3.Cross(b - a, c - a);
                            if (cross.sqrMagnitude < 1e-20f) continue;
                            float area = cross.magnitude;
                            float pieces = Vector3.Cross(b - point, c - point).magnitude +
                                Vector3.Cross(c - point, a - point).magnitude +
                                Vector3.Cross(a - point, b - point).magnitude;
                            if (pieces > area * 1.0001f) continue;
                            nearest = distance;
                            hit = point;
                            hitNormal = Vector3.Dot(plane.normal, normal) < 0 ? -plane.normal : plane.normal;
                        }
                        if (float.IsPositiveInfinity(nearest))
                        {
                            float best = float.PositiveInfinity;
                            for (int i = 0; i < triangles.Length; i += 3)
                            {
                                var a = vertices[triangles[i]];
                                var b = vertices[triangles[i + 1]];
                                var c = vertices[triangles[i + 2]];
                                var faceNormal = Vector3.Cross(b - a, c - a).normalized;
                                if (Mathf.Abs(Vector3.Dot(faceNormal, normal)) < .3f) continue;
                                var point = (a + b + c) / 3;
                                var offset = point - mesh.bounds.center;
                                float score = Vector3.ProjectOnPlane(offset, normal).sqrMagnitude -
                                    Vector3.Dot(offset, normal) * .0001f;
                                if (score >= best) continue;
                                best = score;
                                hit = point;
                                hitNormal = Vector3.Dot(faceNormal, normal) < 0 ? -faceNormal : faceNormal;
                            }
                            if (float.IsPositiveInfinity(best))
                                throw new InvalidOperationException("No surface triangle: " + definition.id);
                        }
                        if (definition.id == "tank.outlet.flange")
                        {
                            // A seal leaks at its exposed perimeter, not through the bore or upper flange.
                            float front = float.NegativeInfinity;
                            foreach (var vertex in vertices)
                            {
                                float score = Vector3.Dot(node.TransformPoint(vertex), Vector3.back);
                                if (score <= front) continue;
                                front = score;
                                hit = vertex;
                            }
                        }
                        definition.surfaceCached = true;
                        definition.surfaceLocalM = hit;
                        definition.surfaceNormalLocal = hitNormal;
                        definition.surfaceSource = AssetDatabase.GetAssetPath(mesh) + " / " + mesh.name;
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            File.WriteAllText(PathName, JsonUtility.ToJson(manifest, true));
            AssetDatabase.ImportAsset(PathName);
            Debug.Log("Fault surface coordinates baked from original meshes: " + manifest.bindings.Length);
        }
    }
}
