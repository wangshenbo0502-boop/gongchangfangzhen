using System;
using System.IO;
using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace WorkshopDemo.Editor
{
    public static class WorkshopFiveModelLayout
    {
        public static void Audit()
        {
            EditorSceneManager.OpenScene(WorkshopFoundationBuilder.DemoScene);
            string folder = "Evidence/layout-audit-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(folder);
            var sim = UnityEngine.Object.FindFirstObjectByType<WorkshopSimulation>();
            foreach (var device in sim.devices) WorkshopEquipmentModel.Attach(device);
            var camera = sim.cameraRig.GetComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(-1.5f, 25, 0), Quaternion.Euler(90, 0, 0));
            camera.orthographic = true;
            camera.orthographicSize = 10;
            var target = new RenderTexture(1920, 1280, 24);
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(folder + "/warehouse-top.png", image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(image);
            target.Release();
            UnityEngine.Object.DestroyImmediate(target);
            File.WriteAllLines(folder + "/floor-objects.txt",
                UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                    .Where(r => r.bounds.min.y < .15f && r.bounds.max.y < .5f &&
                        r.bounds.Intersects(new Bounds(new Vector3(-1.5f, 0, 0), new Vector3(24, 1, 19))))
                    .Select(r => r.name + " " + r.bounds).OrderBy(s => s));
        }

        [MenuItem("Tools/Workshop Demo/Apply Five Model Layout")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before changing the layout.");
            string folder = "Evidence/layout-backup-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
            Directory.CreateDirectory(folder);
            File.Copy(WorkshopFoundationBuilder.DemoScene, folder + "/WorkshopDemo.unity");
            const string navPath = "Assets/WorkshopDemo/Generated/InspectionNavMesh.asset";
            File.Copy(navPath, folder + "/InspectionNavMesh.asset");
            var scene = EditorSceneManager.OpenScene(WorkshopFoundationBuilder.DemoScene);
            var sim = UnityEngine.Object.FindFirstObjectByType<WorkshopSimulation>();
            var originalDevices = sim.devices.ToArray();
            sim.devices = WorkshopTrainingLayout.DeviceIds.Select(id =>
                originalDevices.Single(d => d.deviceId == id)).ToArray();
            var surface = UnityEngine.Object.FindFirstObjectByType<NavMeshSurface>();
            foreach (var device in originalDevices)
            {
                var clearance = surface.transform.Find(device.deviceId + " clearance");
                if (!sim.devices.Contains(device))
                {
                    if (clearance) UnityEngine.Object.DestroyImmediate(clearance.gameObject);
                    UnityEngine.Object.DestroyImmediate(device.gameObject);
                    continue;
                }
                ConfigureEquipment(device);
                if (!clearance) throw new InvalidOperationException("Missing clearance: " + device.deviceId);
                var bounds = device.GetComponent<BoxCollider>().bounds;
                clearance.position = bounds.center;
                clearance.rotation = Quaternion.identity;
                clearance.localScale = Vector3.one;
                clearance.GetComponent<BoxCollider>().size = bounds.size;
            }
            var previous = surface.navMeshData;
            surface.BuildNavMesh();
            var baked = surface.navMeshData;
            surface.RemoveData();
            EditorUtility.CopySerialized(baked, previous);
            surface.navMeshData = previous;
            surface.AddData();
            EditorUtility.SetDirty(previous);
            if (!NavMesh.SamplePosition(WorkshopTrainingLayout.RobotSpawn, out var spawn, .25f, NavMesh.AllAreas))
                throw new InvalidOperationException("Central robot station is not on the NavMesh.");
            sim.inspector.transform.SetPositionAndRotation(spawn.position, Quaternion.identity);
            foreach (var device in sim.devices)
            {
                var path = new NavMeshPath();
                if (!NavMesh.SamplePosition(device.inspectionPoint, out var hit, .25f, NavMesh.AllAreas) ||
                    !NavMesh.CalculatePath(spawn.position, hit.position, NavMesh.AllAreas, path) ||
                    path.status != NavMeshPathStatus.PathComplete)
                    throw new InvalidOperationException("Unreachable station: " + device.deviceId);
            }
            WorkshopTrainingLayout.ConfigureCamera(sim.cameraRig);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            File.WriteAllText(folder + "/migration.txt", "Applied " + DateTimeOffset.Now.ToString("o") +
                "\nFour fixed stations + original inspection robot. Official assembly scene untouched.\n" +
                string.Join("\n", sim.devices.Select(d => d.deviceId + " position=" + d.transform.position +
                    " bounds=" + d.GetComponent<BoxCollider>().bounds + " inspect=" + d.inspectionPoint)) +
                "\nAll four stations have complete NavMesh paths from central spawn.");
            Debug.Log("Five model layout applied. Backup: " + folder);
        }

        public static void ConfigureEquipment(WorkshopDevice device)
        {
            device.transform.SetPositionAndRotation(WorkshopTrainingLayout.Position(device.deviceId),
                WorkshopTrainingLayout.Rotation(device.deviceId));
            device.inspectionPoint = WorkshopTrainingLayout.InspectionPoint(device.deviceId);
            var preview = new GameObject("Nominal envelope measurement");
            try
            {
                var model = preview.AddComponent<WorkshopEquipmentModel>();
                model.Initialize(device.kind, device.deviceId);
                WorkshopEquipmentModel.ConfigureEnvelope(device, model.NativeBounds);
            }
            finally { UnityEngine.Object.DestroyImmediate(preview); }
        }
    }
}
