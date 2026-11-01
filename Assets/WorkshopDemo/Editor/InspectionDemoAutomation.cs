using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using System.Linq;
using Object = UnityEngine.Object;

namespace WorkshopDemo.Editor
{
    [InitializeOnLoad]
    public static class InspectionDemoAutomation
    {
        static double deadline;

        static InspectionDemoAutomation()
        {
            if (!SessionState.GetBool("Inspection.Automation", false)) return;
            deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update += Watch;
        }

        public static void Baseline()
        {
            Directory.CreateDirectory("Evidence");
            const string report = "Evidence/runtime-verification.txt";
            if (File.Exists(report) && !File.Exists("Evidence/runtime-verification-before-20261101.txt"))
                File.Copy(report, "Evidence/runtime-verification-before-20261101.txt", true);
            EditorSceneManager.OpenScene(WorkshopFoundationBuilder.DemoScene);
            SessionState.SetBool("Inspection.Automation", true);
            WorkshopVerification.Begin();
            deadline = EditorApplication.timeSinceStartup + 240;
            EditorApplication.update += Watch;
        }

        static void Watch()
        {
            if (EditorApplication.isCompiling) return;
            string text = File.ReadAllText("Evidence/runtime-verification.txt");
            if (!text.Contains("COMPLETE") && !text.Contains("FAILED") &&
                EditorApplication.timeSinceStartup < deadline) return;
            EditorApplication.update -= Watch;
            SessionState.SetBool("Inspection.Automation", false);
            EditorApplication.Exit(text.Contains("COMPLETE") ? 0 : 1);
        }

        public static void BuildPlayer()
        {
            WorkshopVerification.BuildPlayer();
            if (!File.ReadAllText("Evidence/player-build.txt").StartsWith("Succeeded"))
                throw new InvalidOperationException("Inspection player build failed.");
        }

        public static void BuildUpgrade()
        {
            WorkshopFoundationBuilder.Build();
            BuildUpgradePlayer();
        }

        public static void BuildUpgradePlayer()
        {
            Directory.CreateDirectory("Build/WorkshopInspectionV2");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { WorkshopFoundationBuilder.DemoScene },
                locationPathName = "Build/WorkshopInspectionV2/WorkshopInspection.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            File.WriteAllText("Evidence/inspection-v2-build.txt",
                $"Time={DateTime.Now:O}\nScene={WorkshopFoundationBuilder.DemoScene}\n" +
                $"Result={result.summary.result}\nErrors={result.summary.totalErrors}\nSize={result.summary.totalSize}\n");
            if (result.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Inspection upgrade build failed.");
        }

        public static void InspectLayout()
        {
            EditorSceneManager.OpenScene(WorkshopFoundationBuilder.DemoScene);
            using var writer = new StreamWriter("Evidence/inspection-layout-audit.txt");
            foreach (var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                var b = renderer.bounds;
                if (b.min.y < 2 && b.max.y > .15f)
                    writer.WriteLine($"{renderer.transform.root.name}/{renderer.name} bounds={b}");
            }
            foreach (var device in Object.FindFirstObjectByType<WorkshopSimulation>().devices)
            {
                writer.WriteLine($"DEVICE {device.deviceId} position={device.transform.position}");
                foreach (var filter in device.transform.GetChild(0).GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = filter.sharedMesh;
                    writer.WriteLine($" MESH {filter.name} vertices={mesh.vertexCount} uv={mesh.uv.Length} rotation={filter.transform.eulerAngles}");
                    foreach (var mat in filter.GetComponent<Renderer>().sharedMaterials)
                        writer.WriteLine($" MATERIAL {mat.name} color={mat.color} textures=" +
                            string.Join(",", mat.GetTexturePropertyNames().Select(p => p + ":" + mat.GetTexture(p))));
                }
            }
        }

        public static void BuildAssembly()
        {
            Directory.CreateDirectory("Build/RobotAssembly");
            var result = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { FactoryTrainingImport.OriginalScene },
                locationPathName = "Build/RobotAssembly/RobotAssembly.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            Directory.CreateDirectory("Evidence");
            File.WriteAllText("Evidence/assembly-player-build.txt",
                $"Time={DateTime.Now:O}\nScene={FactoryTrainingImport.OriginalScene}\n" +
                $"Result={result.summary.result}\nErrors={result.summary.totalErrors}\nSize={result.summary.totalSize}\n");
            if (result.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Assembly player build failed.");
        }
    }
}
