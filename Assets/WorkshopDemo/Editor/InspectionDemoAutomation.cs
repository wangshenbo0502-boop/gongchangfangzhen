using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;

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
