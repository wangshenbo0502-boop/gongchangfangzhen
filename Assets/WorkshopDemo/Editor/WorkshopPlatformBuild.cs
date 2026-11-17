using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WorkshopDemo.Editor
{
    public static class WorkshopPlatformBuild
    {
        [MenuItem("Tools/Workshop Demo/Build Unified Platform EXE")]
        public static void Build()
        {
            BuildAt("Build/WorkshopPlatform/WorkshopPlatform.exe", true);
        }

        // Review output never writes to the delivered player or its protected viewers.
        public static void BuildUIReview()
        {
            BuildAt("Build/WorkshopPlatformUIReview/WorkshopPlatform.exe", false);
        }

        public static void BuildCabinetReview()
        {
            const string directory = "Build/WorkshopPlatformCabinetReview";
            BuildAt(directory + "/WorkshopPlatform.exe", false);
            DeployViewer("CabinetViewer", false, directory);
        }

        public static void BuildFaultReview()
        {
            RequireSceneScripts();
            WorkshopFaultSurfaceBake.Bake();
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmssfff");
            string directory = "Build/WorkshopFaultReview-" + stamp;
            BuildAt(directory + "/WorkshopPlatform.exe", false);
            const string deliveredViewers = "Build/WorkshopPlatform/Viewers";
            foreach (string name in new[] { "WaterPumpViewer", "TankViewer", "IndustrialFanViewer", "CabinetViewer" })
            {
                string source = Path.Combine(deliveredViewers, name);
                if (!Directory.Exists(source)) source = Path.Combine("model", name);
                if (!Directory.Exists(source)) continue;
                RequireViewer(source, name);
                CopyViewer(source, Path.Combine(directory, "Viewers", name));
            }
        }

        public static void BuildTankReview()
        {
            const string directory = "Build/WorkshopPlatformTankReview";
            BuildAt(directory + "/WorkshopPlatform.exe", false);
            DeployViewer("TankViewer", false, directory);
        }

        static void BuildAt(string output, bool deployViewers)
        {
            RequireSceneScripts();
            WorkshopFaultDerivedImport.EnsureImported();
            var scenes = new[] {
                WorkshopSceneSwitcher.InspectionScene, WorkshopSceneSwitcher.AssemblyScene
            };
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(scenes, output, BuildTarget.StandaloneWindows64,
                BuildOptions.None);
            string evidence = "Evidence/unified-build-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
            File.WriteAllText(evidence, "Result: " + report.summary.result + "\nOutput: " +
                Path.GetFullPath(output) + "\nScenes:\n" + string.Join("\n", scenes) +
                "\nErrors: " + report.summary.totalErrors + "\nBytes: " + report.summary.totalSize);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Unified platform build failed: " + report.summary.result);
            if (deployViewers)
            {
                DeployViewer("WaterPumpViewer", true);
                DeployViewer("IndustrialFanViewer", false);
                if (File.Exists("model/CabinetViewer/CabinetViewer.exe"))
                    DeployViewer("CabinetViewer", false);
                if (File.Exists("model/TankViewer/TankViewer.exe"))
                    DeployViewer("TankViewer", false);
            }
            Debug.Log("Unified platform built: " + Path.GetFullPath(output));
        }

        static void RequireSceneScripts()
        {
            foreach (string path in new[] {
                WorkshopSceneSwitcher.InspectionScene, WorkshopSceneSwitcher.AssemblyScene })
            {
                var scene = EditorSceneManager.OpenPreviewScene(path);
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) > 0)
                                throw new InvalidOperationException(
                                    "Build blocked by missing scene scripts: " + path + " / " + transform.name);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }

        static void DeployViewer(string name, bool preserveExisting, string platformDirectory = "Build/WorkshopPlatform")
        {
            string viewerSource = Path.GetFullPath("model/" + name);
            string viewerTarget = Path.GetFullPath(Path.Combine(platformDirectory, "Viewers", name));
            if (Directory.Exists(viewerTarget) && preserveExisting)
            {
                RequireViewer(viewerTarget, name);
                Debug.Log("Protected viewer preserved without writes: " + viewerTarget);
                return;
            }
            RequireViewer(viewerSource, name);
            if (Directory.Exists(viewerTarget))
            {
                string backup = Path.GetFullPath("model/revisions/" + name + "-platform-before-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                CopyViewer(viewerTarget, backup);
            }
            CopyViewer(viewerSource, viewerTarget);
            RequireViewer(viewerTarget, name);
            Debug.Log("Viewer deployed: " + viewerTarget);
        }

        static void RequireViewer(string directory, string name)
        {
            foreach (string relative in new[] { name + ".exe", "UnityPlayer.dll",
                name + "_Data/Managed/Assembly-CSharp.dll", name + "_Data/globalgamemanagers" })
                if (!File.Exists(Path.Combine(directory, relative)))
                    throw new FileNotFoundException("Incomplete standalone viewer", Path.Combine(directory, relative));
        }

        static void CopyViewer(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
            foreach (string file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)), true);
        }
    }
}
