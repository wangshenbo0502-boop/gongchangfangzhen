using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEngine.Rendering.Universal;

namespace WorkshopDemo.Editor
{
    public static class WorkshopFoundationBuilder
    {
        public const string DemoScene = "Assets/WorkshopDemo/Scenes/WorkshopDemo.unity";
        const string Generated = "Assets/WorkshopDemo/Generated";
        const string External = "Assets/WorkshopDemo/External/";

        public static void RestoreOfficial()
        {
            SessionState.SetBool("Workshop.Verify", false);
            var scene = EditorSceneManager.OpenScene(FactoryTrainingImport.OriginalScene);
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene(FactoryTrainingImport.OriginalScene, true)
            };
            AssetDatabase.SaveAssets();
            File.WriteAllText("Evidence/official-scene-restored.txt",
                "Restored " + DateTime.Now.ToString("s") + "\nScene: " + scene.path +
                "\nOfficial models, materials, textures and layout preserved.");
        }

        public static void Audit()
        {
            using (var w = new StreamWriter("Evidence/equipment-audit.txt"))
            {
                w.WriteLine("Playing: " + EditorApplication.isPlaying);
                foreach (var p in AssetDatabase.FindAssets("t:Model", new[] { "Assets/WorkshopDemo/External/Grab3D" })
                    .Select(AssetDatabase.GUIDToAssetPath))
                {
                    var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(p));
                    var renderers = go.GetComponentsInChildren<Renderer>(true);
                    var bounds = new Bounds();
                    if (renderers.Length > 0)
                    {
                        bounds = renderers[0].bounds;
                        foreach (var r in renderers) bounds.Encapsulate(r.bounds);
                    }
                    w.WriteLine(p + " bounds=" + bounds);
                    foreach (var r in renderers.Take(35))
                        w.WriteLine("  " + r.name + " " + r.bounds + " materials=" +
                            string.Join(",", r.sharedMaterials.Select(m => m ? m.name + ":" + m.shader.name : "NULL")));
                    UnityEngine.Object.DestroyImmediate(go);
                }
                w.WriteLine("Scene roots:");
                foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                    w.WriteLine(root.name + " active=" + root.activeSelf);
            }
        }

        [MenuItem("Tools/Workshop Demo/Build Demo")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before building the demo.");
            Directory.CreateDirectory(Generated);
            AssetDatabase.Refresh();
            // Always derive this generated scene from the untouched official sample.
            var scene = EditorSceneManager.OpenScene(FactoryTrainingImport.OriginalScene);
            foreach (var root in scene.GetRootGameObjects())
                if (new[] { "DistributionCenterScripts", "ImmersiveTraining", "Canvas", "EventSystem",
                    "NavMesh_GO", "NavBlocker", "stopTargets", "cam", "mottoes", "audio" }.Contains(root.name))
                    root.SetActive(false);

            var env = GameObject.Find("env");
            foreach (var script in env.GetComponentsInChildren<MonoBehaviour>(true))
                if (script && script.GetType().Namespace != null &&
                    (script.GetType().Namespace.StartsWith("DistributionCenter") ||
                     script.GetType().Namespace.StartsWith("ImmersiveTraining")))
                    script.enabled = false;

            var generated = new GameObject("Workshop Demo - Generated");
            var simulation = generated.AddComponent<WorkshopSimulation>();
            var roof = new GameObject("Roof visibility");
            roof.transform.SetParent(generated.transform);
            foreach (var name in new[] { "ceiling", "wallTopSpacer" })
            {
                var part = env.transform.Find(name);
                if (part) part.SetParent(roof.transform, true);
            }
            roof.SetActive(false);
            simulation.roof = roof;

            var font = AssetDatabase.LoadAssetAtPath<Font>(External + "SourceHanSans/SourceHanSansSC-Regular.otf");
            var cyan = Material("Selected", new Color(.03f, .83f, .65f), true);
            var alarm = Material("Fault", new Color(1, .19f, .09f), true);
            var deviceRoot = new GameObject("Monitored equipment").transform;
            deviceRoot.SetParent(generated.transform);
            var devices = new[] {
                Equipment(deviceRoot, "CV-01", "装配输送线", DeviceKind.Conveyor, "conveyor-belt", new Vector3(-7,0,-3), 1.35f, font, cyan, alarm),
                Equipment(deviceRoot, "M-01", "输送驱动电机", DeviceKind.Motor, "motor", new Vector3(-2,0,-3), 4, font, cyan, alarm),
                Equipment(deviceRoot, "P-01", "循环水泵", DeviceKind.Pump, "water-pump", new Vector3(2,0,-3), 1.8f, font, cyan, alarm),
                Equipment(deviceRoot, "P-02", "备用循环泵", DeviceKind.Pump, "water-pump", new Vector3(5,0,-3), 1.8f, font, cyan, alarm),
                Equipment(deviceRoot, "TK-01", "工艺储罐", DeviceKind.Tank, "silo", new Vector3(-8,0,3), .35f, font, cyan, alarm),
                Equipment(deviceRoot, "TK-02", "稳压储罐", DeviceKind.Tank, "pressure-tank", new Vector3(-4,0,3), 1.25f, font, cyan, alarm),
                Equipment(deviceRoot, "F-01", "车间排风机", DeviceKind.Fan, "industrial-fan", new Vector3(0,0,3), 1.1f, font, cyan, alarm),
                Equipment(deviceRoot, "PLC-01", "设备控制柜", DeviceKind.Cabinet, "electrical-cabinet", new Vector3(4,0,3), 1.2f, font, cyan, alarm)
            };
            simulation.devices = devices;
            foreach (var d in devices)
            {
                d.nominalTemperature = d.kind == DeviceKind.Cabinet ? 32 : d.kind == DeviceKind.Tank ? 25 : 42;
                d.nominalRpm = d.kind == DeviceKind.Conveyor ? 120 : d.kind == DeviceKind.Fan ? 980 : 1450;
                d.inspectionPoint = d.transform.position + new Vector3(0, 0, d.transform.position.z > 0 ? -1.8f : 1.8f);
            }

            var cameraObject = new GameObject("Workshop Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.transform.SetParent(generated.transform);
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.GetComponent<Camera>();
            camera.fieldOfView = 50;
            camera.nearClipPlane = .1f;
            camera.farClipPlane = 240;
            camera.allowHDR = true;
            cameraObject.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
            simulation.cameraRig = cameraObject.AddComponent<WorkshopCamera>();
            simulation.cameraRig.simulation = simulation;
            cameraObject.transform.position = new Vector3(6, 15, -20);
            cameraObject.transform.LookAt(new Vector3(-2, .8f, 0));

            var navigation = new GameObject("Inspection navigation");
            navigation.transform.SetParent(generated.transform);
            var surface = navigation.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.overrideVoxelSize = true;
            surface.voxelSize = .08f;
            Collision(navigation.transform, "Floor collision", new Bounds(new Vector3(-2, -.15f, 0), new Vector3(24, .3f, 19)), false);
            foreach (var d in devices)
                Collision(navigation.transform, d.deviceId + " clearance", BoundsOf(d.transform.GetChild(0).gameObject), true);
            // Include actual nearby stock-scene obstacles; overhead ductwork and floor decals are excluded.
            foreach (var renderer in env.GetComponentsInChildren<MeshRenderer>())
            {
                var b = renderer.bounds;
                if (b.max.y < .3f || b.min.y > 1.7f || b.size.x < .12f || b.size.z < .12f ||
                    b.size.x > 15 || b.size.z > 15 || b.max.x < -14 || b.min.x > 10 ||
                    b.max.z < -9.5f || b.min.z > 9.5f) continue;
                Collision(navigation.transform, "Stock " + renderer.name, b, true);
            }
            surface.BuildNavMesh();
            const string navPath = Generated + "/InspectionNavMesh.asset";
            var existingNav = AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath);
            if (existingNav)
            {
                EditorUtility.CopySerialized(surface.navMeshData, existingNav);
                surface.RemoveData();
                surface.navMeshData = existingNav;
                surface.AddData();
            }
            else AssetDatabase.CreateAsset(surface.navMeshData, navPath);
            simulation.inspector = Inspector(generated.transform, cyan);
            simulation.inspector.simulation = simulation;
            var ui = new GameObject("Workshop UI", typeof(RectTransform)).AddComponent<WorkshopUI>();
            ui.transform.SetParent(generated.transform);
            ui.simulation = simulation;
            ui.chineseFont = font;
            EditorSceneManager.SaveScene(scene, DemoScene);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(DemoScene, true) };
            AssetDatabase.SaveAssets();
            File.WriteAllText("Evidence/demo-build.txt", "Built " + DateTime.Now.ToString("s") +
                "\nScene: " + DemoScene + "\nDevices: " + devices.Length +
                "\nNavMesh vertices: " + NavMesh.CalculateTriangulation().vertices.Length +
                "\nSource factory scene preserved. Official asset references retained.");
        }

        static Material Material(string name, Color color, bool emission)
        {
            string path = Generated + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.color = color;
            if (emission)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 1.6f);
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("No renderers: " + go.name);
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static WorkshopDevice Equipment(Transform parent, string id, string title, DeviceKind kind,
            string modelName, Vector3 position, float scale, Font font, Material selected, Material fault)
        {
            string path = modelName == "motor" ?
                External + "FactoryTraining/Prefabs/moto_part/motor.prefab" :
                External + "Grab3D/" + modelName + "/" + modelName + ".fbx";
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (!asset) throw new FileNotFoundException("Missing downloaded equipment model", path);
            var root = new GameObject(id + " " + title);
            root.transform.SetParent(parent);
            root.transform.position = position;
            var model = (GameObject)PrefabUtility.InstantiatePrefab(asset, root.transform);
            model.name = "Source model - " + modelName;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * scale;
            foreach (var script in model.GetComponentsInChildren<MonoBehaviour>(true))
                if (script) script.enabled = false;
            var bounds = BoundsOf(model);
            model.transform.position += new Vector3(position.x - bounds.center.x, -bounds.min.y, position.z - bounds.center.z);
            bounds = BoundsOf(model);
            var device = root.AddComponent<WorkshopDevice>();
            device.deviceId = id; device.displayName = title; device.kind = kind;
            var hitbox = root.AddComponent<BoxCollider>();
            hitbox.center = root.transform.InverseTransformPoint(bounds.center);
            hitbox.size = bounds.size + new Vector3(.08f, .05f, .08f);
            device.selectionRing = new GameObject("Selection perimeter");
            device.selectionRing.transform.SetParent(root.transform);
            var line = device.selectionRing.AddComponent<LineRenderer>();
            line.sharedMaterial = selected;
            line.useWorldSpace = true;
            line.loop = true;
            line.widthMultiplier = .045f;
            line.positionCount = 4;
            float x = bounds.extents.x + .25f, z = bounds.extents.z + .25f;
            line.SetPositions(new[] { position + new Vector3(-x,.04f,-z), position + new Vector3(-x,.04f,z),
                position + new Vector3(x,.04f,z), position + new Vector3(x,.04f,-z) });
            device.selectionRing.SetActive(false);
            var label = new GameObject("Device status");
            label.transform.SetParent(root.transform);
            label.transform.localPosition = new Vector3(0, bounds.size.y + .3f, 0);
            var text = label.AddComponent<TextMesh>();
            text.font = font; text.fontSize = 48; text.characterSize = .032f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = new Color(.85f, 1, .95f);
            text.text = id + " RUN";
            text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            device.worldLabel = text;
            var marker = UnityEngine.Object.Instantiate(label, root.transform);
            marker.name = "Fault marker";
            marker.transform.localPosition += Vector3.up * .32f;
            marker.GetComponent<TextMesh>().text = "! FAULT";
            marker.GetComponent<TextMesh>().color = new Color(1,.2f,.08f);
            device.faultMarker = marker;
            marker.SetActive(false);
            var rotor = model.GetComponentsInChildren<Transform>().FirstOrDefault(t =>
                t.name == modelName + "_rotor" || t.name == modelName + "_drum");
            if (rotor)
            {
                var pivot = new GameObject("Rotor pivot").transform;
                pivot.SetParent(model.transform, true);
                pivot.position = rotor.GetComponent<Renderer>().bounds.center;
                rotor.SetParent(pivot, true);
                device.rotor = pivot;
                device.rotorAxis = kind == DeviceKind.Fan ? Vector3.forward : Vector3.forward;
            }
            var lamp = model.GetComponentsInChildren<Renderer>().FirstOrDefault(r => r.name.Contains("lamp") && r.name.Contains("lens"));
            device.statusLamp = lamp;
            if (kind == DeviceKind.Conveyor)
            {
                var boxPath = AssetDatabase.FindAssets("t:Prefab", new[] { External + "FactoryTraining/Prefabs" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == "box_prefab");
                if (boxPath == null)
                    boxPath = AssetDatabase.FindAssets("t:Prefab", new[] { External + "FactoryTraining/Prefabs" })
                        .Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault(p => Path.GetFileNameWithoutExtension(p).StartsWith("box"));
                if (boxPath != null)
                {
                    device.movingPackages = new Transform[3];
                    for (int i = 0; i < 3; i++)
                    {
                        var box = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(boxPath), root.transform);
                        box.transform.localScale = Vector3.one * .4f;
                        box.transform.localPosition = Vector3.zero;
                        var bb = BoundsOf(box);
                        box.transform.position += position + new Vector3(-1.4f + 1.4f * i, 1.04f - bb.min.y, 0) - new Vector3(bb.center.x, 0, bb.center.z);
                        device.movingPackages[i] = box.transform;
                    }
                }
            }
            return device;
        }

        static void Collision(Transform parent, string name, Bounds bounds, bool blocked)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            go.transform.position = bounds.center;
            go.AddComponent<BoxCollider>().size = bounds.size;
            go.layer = 2;
            if (blocked)
            {
                var modifier = go.AddComponent<NavMeshModifier>();
                modifier.overrideArea = true;
                modifier.area = 1;
            }
        }

        static WorkshopInspector Inspector(Transform parent, Material routeMaterial)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                External + "FactoryTraining/Prefabs/moto_bot/moto_prefab.prefab");
            var robot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            robot.name = "Inspection robot";
            foreach (var script in robot.GetComponentsInChildren<MonoBehaviour>(true)) if (script) script.enabled = false;
            foreach (var audio in robot.GetComponentsInChildren<AudioSource>()) audio.enabled = false;
            var agent = robot.GetComponent<NavMeshAgent>();
            if (!agent) agent = robot.AddComponent<NavMeshAgent>();
            agent.radius = .4f; agent.height = 1.2f; agent.speed = 2.8f;
            agent.acceleration = 6; agent.angularSpeed = 220; agent.stoppingDistance = .4f;
            if (!NavMesh.SamplePosition(new Vector3(-10, 0, -6), out var hit, 3, NavMesh.AllAreas))
                throw new InvalidOperationException("Inspector spawn has no walkable NavMesh");
            robot.transform.position = hit.position;
            var route = new GameObject("Inspection route").AddComponent<LineRenderer>();
            route.transform.SetParent(robot.transform);
            route.sharedMaterial = routeMaterial;
            route.widthMultiplier = .065f;
            route.useWorldSpace = true;
            route.positionCount = 0;
            var inspector = robot.AddComponent<WorkshopInspector>();
            inspector.route = route;
            return inspector;
        }
    }
}
