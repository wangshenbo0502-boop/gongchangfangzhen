using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WorkshopDemo.Editor
{
    public static class FactoryTrainingImport
    {
        const string PackagePath = "Downloads/FactoryTraining-assets-only.unitypackage";
        public const string OriginalScene = "Assets/WorkshopDemo/External/FactoryTraining/Scenes/urp_scene_with_training.unity";
        public const string FoundationScene = "Assets/WorkshopDemo/Scenes/FactoryFoundation.unity";

        public static void Import()
        {
            if (!File.Exists(PackagePath)) throw new FileNotFoundException("Factory Training download is not complete", PackagePath);
            Directory.CreateDirectory("Evidence");
            File.WriteAllText("Evidence/factory-import-status.txt", "Import started: " + DateTime.Now.ToString("s"));
            AssetDatabase.importPackageCompleted += Completed;
            AssetDatabase.importPackageFailed += Failed;
            AssetDatabase.ImportPackage(Path.GetFullPath(PackagePath), false);
        }

        static void Completed(string package)
        {
            AssetDatabase.importPackageCompleted -= Completed;
            AssetDatabase.importPackageFailed -= Failed;
            File.AppendAllText("Evidence/factory-import-status.txt", "\nImport completed: " + package + " " + DateTime.Now.ToString("s"));
            if (!File.Exists(OriginalScene))
                File.AppendAllText("Evidence/factory-import-status.txt",
                    "\nVerification: package API did not import the sample scene. Use Tools/extract_factory_training.py.");
            Inventory();
        }

        static void Failed(string package, string error)
        {
            AssetDatabase.importPackageCompleted -= Completed;
            AssetDatabase.importPackageFailed -= Failed;
            File.AppendAllText("Evidence/factory-import-status.txt", "\nImport failed: " + package + "\n" + error);
        }

        public static void Inventory()
        {
            Directory.CreateDirectory("Evidence");
            File.WriteAllLines("Evidence/factory-scenes.txt", AssetDatabase.FindAssets("t:Scene", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath));
            File.WriteAllLines("Evidence/factory-prefabs.txt", AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath));
            File.WriteAllLines("Evidence/factory-models.txt", AssetDatabase.FindAssets("t:Model", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath));
            const string root = "Assets/WorkshopDemo/External/FactoryTraining";
            File.WriteAllText("Evidence/factory-import-verification.txt",
                "Verified at: " + DateTime.Now.ToString("s") +
                "\nOriginal scene exists: " + File.Exists(OriginalScene) +
                "\nImported sample scenes: " + AssetDatabase.FindAssets("t:Scene", new[] { root }).Length +
                "\nImported sample models: " + AssetDatabase.FindAssets("t:Model", new[] { root }).Length +
                "\nImported sample prefabs: " + AssetDatabase.FindAssets("t:Prefab", new[] { root }).Length +
                "\nImported sample materials: " + AssetDatabase.FindAssets("t:Material", new[] { root }).Length);
        }

        [MenuItem("Tools/Workshop Demo/Open Factory Training")]
        public static void OpenOriginal()
        {
            InspectScene(OriginalScene);
        }

        [MenuItem("Tools/Workshop Demo/Prepare Factory Foundation")]
        public static void CreateFoundation()
        {
            if (!AssetDatabase.IsValidFolder("Assets/WorkshopDemo/Scenes"))
                AssetDatabase.CreateFolder("Assets/WorkshopDemo", "Scenes");
            if (!File.Exists(FoundationScene))
            {
                EditorSceneManager.OpenScene(OriginalScene);
                if (!EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), FoundationScene))
                    throw new IOException("Could not save the factory foundation scene");
            }
            InspectScene(FoundationScene);
            File.WriteAllText("Evidence/factory-foundation.txt",
                "Existing official Factory Training scene copied with its original asset references.\n" +
                "Source: " + OriginalScene + "\nWorking copy: " + FoundationScene +
                "\nMonitoring and inspection integration remains separate work.");
        }

        public static void InspectScene(string path)
        {
            var scene = EditorSceneManager.OpenScene(path);
            using (var writer = new StreamWriter("Evidence/factory-hierarchy.txt"))
            {
                foreach (var root in scene.GetRootGameObjects()) WriteTransform(writer, root.transform, 0);
            }
            File.WriteAllText("Evidence/factory-scene-validation.txt",
                "Scene: " + path + "\nRoot objects: " + scene.rootCount +
                "\nMissing MonoBehaviour scripts: " + scene.GetRootGameObjects()
                    .Sum(root => root.GetComponentsInChildren<Transform>(true)
                        .Sum(t => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject))));
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            File.WriteAllLines("Evidence/factory-cameras.txt", cameras.Select(c =>
                c.name + " enabled=" + c.enabled + " position=" + c.transform.position +
                " rotation=" + c.transform.eulerAngles + " near=" + c.nearClipPlane + " far=" + c.farClipPlane));
            EditorApplication.delayCall += CaptureScene;
        }

        public static void CaptureScene()
        {
            var cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            var camera = cameras.FirstOrDefault(c => c.enabled && c.CompareTag("MainCamera")) ??
                cameras.FirstOrDefault(c => c.enabled);
            if (!camera) throw new InvalidOperationException("Imported scene has no active camera");
            var previous = RenderTexture.active;
            var target = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            try
            {
                target.Create();
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                image.Apply();
                File.WriteAllBytes("Evidence/factory-original-scene.png", image.EncodeToPNG());
                var renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                File.WriteAllLines("Evidence/factory-render-audit.txt", renderers.Select(r =>
                    r.name + " bounds=" + r.bounds + " materials=" + string.Join(",", r.sharedMaterials.Select(m =>
                        m ? m.name + ":" + (m.shader ? m.shader.name : "MISSING SHADER") : "MISSING MATERIAL"))));
            }
            finally
            {
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        static void WriteTransform(StreamWriter writer, Transform t, int depth)
        {
            if (depth > 7) return;
            writer.WriteLine(new string(' ', depth * 2) + t.name + " | local=" + t.localPosition +
                " world=" + t.position + " scale=" + t.localScale + " | " +
                string.Join(", ", t.GetComponents<Component>().Select(c => c ? c.GetType().Name : "MISSING SCRIPT")));
            foreach (Transform child in t) WriteTransform(writer, child, depth + 1);
        }
    }
}
