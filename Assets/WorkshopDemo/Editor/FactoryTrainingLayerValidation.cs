using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ImmersiveTraining.TrainingInteractions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace WorkshopDemo.Editor
{
    // Exercise the sample's own highlight methods on real training parts in Play Mode.
    [InitializeOnLoad]
    public static class FactoryTrainingLayerValidation
    {
        const string Key = "FactoryTraining.LayerValidation";
        const string Report = "Evidence/factory-layer-validation.txt";
        static double started;
        static bool exercised;
        static bool stopping;

        static FactoryTrainingLayerValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += StateChanged;
            Application.logMessageReceived += CaptureError;
            started = EditorApplication.timeSinceStartup;
        }

        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Run validation from Edit Mode.");
            SessionState.SetBool(Key, true);
            SessionState.SetInt(Key + ".errors", 0);
            SessionState.SetBool(Key + ".passed", false);
            Directory.CreateDirectory("Evidence");
            File.WriteAllText(Report, "Started " + DateTime.Now.ToString("s") + "\n");
            EditorSceneManager.OpenScene(FactoryTrainingImport.OriginalScene);
            if (LayerMask.NameToLayer("Outline") != 8)
                throw new InvalidOperationException("The original outline renderer expects layer 8.");
            typeof(EditorApplication).Assembly.GetType("UnityEditor.LogEntries")
                ?.GetMethod("Clear", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            EditorApplication.isPlaying = true;
        }

        static void CaptureError(string message, string trace, LogType type)
        {
            if (!SessionState.GetBool(Key, false) ||
                (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)) return;
            SessionState.SetInt(Key + ".errors", SessionState.GetInt(Key + ".errors", 0) + 1);
            File.AppendAllText(Report, "ERROR " + message + "\n" + trace + "\n");
        }

        static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying ||
                EditorApplication.isCompiling || EditorApplication.isPaused || stopping) return;
            double elapsed = EditorApplication.timeSinceStartup - started;
            try
            {
                if (!exercised && elapsed > 3)
                {
                    var manager = UnityEngine.Object.FindFirstObjectByType<HighlightManager>();
                    if (!manager) throw new InvalidOperationException("Sample HighlightManager not found.");
                    int layer = LayerMask.NameToLayer(manager.outlineLayerName);
                    if (layer != 8) throw new InvalidOperationException("Invalid configured outline layer: " + layer);
                    File.AppendAllText(Report, "PASS Outline resolves to layer 8; renderer mask is 256.\n");
                    var apply = typeof(HighlightManager).GetMethod("ApplyHighlight", BindingFlags.Instance | BindingFlags.NonPublic);
                    var remove = typeof(HighlightManager).GetMethod("RemoveHighlight", BindingFlags.Instance | BindingFlags.NonPublic);
                    var parts = GameObject.Find("ImmersiveTraining").transform.Find("RobotParts");
                    if (!parts) throw new InvalidOperationException("Training parts not found.");
                    int count = 0;
                    foreach (Transform part in parts)
                    {
                        var renderers = part.GetComponentsInChildren<Renderer>();
                        if (renderers.Length == 0) continue;
                        for (int cycle = 0; cycle < 3; cycle++)
                        {
                            apply.Invoke(manager, new object[] { part.gameObject });
                            if (renderers.Any(r => r.gameObject.layer != layer))
                                throw new InvalidOperationException("Highlight layer not applied: " + part.name);
                            remove.Invoke(manager, new object[] { part.gameObject });
                            if (renderers.Any(r => r.gameObject.layer != 0))
                                throw new InvalidOperationException("Highlight layer not restored: " + part.name);
                        }
                        count++;
                    }
                    if (count == 0) throw new InvalidOperationException("No rendered training parts exercised.");
                    File.AppendAllText(Report, "PASS " + count + " real training parts highlighted/restored three times each.\n");
                    exercised = true;
                }
                if (exercised && elapsed > 18)
                {
                    SessionState.SetBool(Key + ".passed", true);
                    stopping = true;
                    EditorApplication.isPlaying = false;
                }
            }
            catch (Exception e)
            {
                File.AppendAllText(Report, "FAILED " + e + "\n");
                stopping = true;
                EditorApplication.isPlaying = false;
            }
        }

        static void StateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key, false)) return;
            int errors = SessionState.GetInt(Key + ".errors", 0);
            bool passed = SessionState.GetBool(Key + ".passed", false) && errors == 0;
            File.AppendAllText(Report, "Runtime errors: " + errors + "\n" +
                (passed ? "PASS startup, repeated highlighting, 18-second runtime and stop. Scene unchanged.\nCOMPLETE\n" : "FAILED\n"));
            SessionState.SetBool(Key, false);
        }
    }
}
