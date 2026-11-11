using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WorkshopDemo.Editor
{
    [InitializeOnLoad]
    public static class WorkshopTwinSetup
    {
        static WorkshopTwinSetup()
        {
            EditorApplication.update += WatchBatch;
        }

        [MenuItem("Tools/Workshop Demo/Open Digital Twin")]
        public static void Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(WorkshopFoundationBuilder.DemoScene);
        }

        [MenuItem("Tools/Workshop Demo/Set Digital Twin Build Entry")]
        public static void ConfigureBuild()
        {
            Directory.CreateDirectory("Evidence");
            string backup = "Evidence/build-settings-before-twin-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt";
            File.Copy("ProjectSettings/EditorBuildSettings.asset", backup, false);
            EditorBuildSettings.scenes = new[] {
                new EditorBuildSettingsScene(WorkshopFoundationBuilder.DemoScene, true)
            };
            AssetDatabase.SaveAssets();
        }

        // Existing generated scene stays intact; the runtime UI and service client require no rebuild.
        public static void VerifyBatch()
        {
            SessionState.SetBool("Workshop.TwinBatch", true);
            Directory.CreateDirectory("Evidence");
            string previous = "Evidence/runtime-verification.txt";
            if (File.Exists(previous))
                File.Copy(previous, "Evidence/runtime-verification-before-twin-" +
                    DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt", false);
            EditorSceneManager.OpenScene(WorkshopFoundationBuilder.DemoScene);
            WorkshopVerification.Begin();
        }

        static void WatchBatch()
        {
            if (!SessionState.GetBool("Workshop.TwinBatch", false) || EditorApplication.isCompiling) return;
            const string report = "Evidence/runtime-verification.txt";
            if (!File.Exists(report)) return;
            string result = File.ReadAllText(report);
            if (!result.Contains("COMPLETE") && !result.Contains("FAILED")) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
                return;
            }
            SessionState.SetBool("Workshop.TwinBatch", false);
            if (Application.isBatchMode) EditorApplication.Exit(result.Contains("COMPLETE") ? 0 : 1);
        }
    }
}
