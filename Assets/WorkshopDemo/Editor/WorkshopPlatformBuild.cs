using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WorkshopDemo.Editor
{
    public static class WorkshopPlatformBuild
    {
        [MenuItem("Tools/Workshop Demo/Build Unified Platform EXE")]
        public static void Build()
        {
            var scenes = new[] {
                WorkshopSceneSwitcher.InspectionScene, WorkshopSceneSwitcher.AssemblyScene
            };
            string output = "Build/WorkshopPlatform/WorkshopPlatform.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(scenes, output, BuildTarget.StandaloneWindows64,
                BuildOptions.None);
            string evidence = "Evidence/unified-build-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
            File.WriteAllText(evidence, "Result: " + report.summary.result + "\nOutput: " +
                Path.GetFullPath(output) + "\nScenes:\n" + string.Join("\n", scenes) +
                "\nErrors: " + report.summary.totalErrors + "\nBytes: " + report.summary.totalSize);
            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Unified platform build failed: " + report.summary.result);
            Debug.Log("Unified platform built: " + Path.GetFullPath(output));
        }
    }
}
