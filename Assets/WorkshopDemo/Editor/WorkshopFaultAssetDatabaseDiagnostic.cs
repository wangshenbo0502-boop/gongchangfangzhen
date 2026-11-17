using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class WorkshopFaultAssetDatabaseDiagnostic
{
    const string RecoveryPending = "WorkshopFault.ScriptRecoveryPending";

    public static void RecompileAndBuildFaultReview()
    {
        SessionState.SetBool(RecoveryPending, true);
        CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
    }

    [InitializeOnLoadMethod]
    static void ResumeAfterCompilation()
    {
        if (!SessionState.GetBool(RecoveryPending, false)) return;
        EditorApplication.delayCall += () =>
        {
            SessionState.EraseBool(RecoveryPending);
            RunProbe(false, true);
        };
    }

    [Serializable]
    sealed class ScriptRow
    {
        public string guid;
        public string path;
        public string type;
        public string assembly;
        public string compilerAssembly;
        public bool assemblyTypePresent;
        public int instanceId;
        public bool resolved;
    }

    [Serializable]
    sealed class MissingRow
    {
        public string scene;
        public string gameObject;
        public int count;
    }

    [Serializable]
    sealed class Report
    {
        public string unityVersion;
        public string phase;
        public List<ScriptRow> scripts = new List<ScriptRow>();
        public List<MissingRow> missing = new List<MissingRow>();
        public List<string> loadedAssemblies = new List<string>();
        public List<string> workshopTypes = new List<string>();
        public List<string> compilationAssemblies = new List<string>();
    }

    static readonly string[] Scenes =
    {
        "Assets/WorkshopDemo/Scenes/WorkshopDemo.unity",
        "Assets/WorkshopDemo/External/FactoryTraining/Scenes/urp_scene_with_training.unity"
    };

    public static void Run()
    {
        RunProbe(false);
    }

    public static void RunSynchronousImportProbe()
    {
        RunProbe(true);
    }

    static void RunProbe(bool reimportScripts, bool buildAfterRecovery = false)
    {
        var report = new Report
        {
            unityVersion = Application.unityVersion,
            phase = reimportScripts ? "before-and-after-synchronous-import" : "current-state"
        };
        var originalScene = SceneManager.GetActiveScene().path;
        var scriptGuids = new SortedSet<string>(StringComparer.Ordinal);
        foreach (string scenePath in Scenes)
        {
            if (!File.Exists(scenePath)) continue;
            string text = File.ReadAllText(scenePath);
            foreach (Match match in Regex.Matches(text, @"m_Script:\s*\{fileID:\s*\d+,\s*guid:\s*([0-9a-f]{32})"))
                scriptGuids.Add(match.Groups[1].Value);
        }

        if (reimportScripts)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (string guid in scriptGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate |
                        ImportAssetOptions.ForceSynchronousImport);
            }
        }

        foreach (string scenePath in Scenes)
        {
            if (!File.Exists(scenePath)) continue;
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            foreach (GameObject root in scene.GetRootGameObjects())
                CollectMissing(root.transform, scenePath, report.missing);
        }

        foreach (string guid in scriptGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            MonoScript script = string.IsNullOrEmpty(path)
                ? null
                : AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            Type type = script == null ? null : script.GetClass();
            string compilerAssembly = null;
            foreach (var assembly in CompilationPipeline.GetAssemblies(AssembliesType.Player))
            {
                if (assembly.sourceFiles.Any(source =>
                        string.Equals(source.Replace('\\', '/'), path, StringComparison.OrdinalIgnoreCase)))
                {
                    compilerAssembly = assembly.name;
                    break;
                }
            }

            string className = Path.GetFileNameWithoutExtension(path);
            bool assemblyTypePresent = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .Select(assembly => assembly.GetType("WorkshopDemo." + className, false))
                .Any(candidate => candidate != null);
            report.scripts.Add(new ScriptRow
            {
                guid = guid,
                path = path,
                type = type == null ? null : type.FullName,
                assembly = type == null ? null : type.Assembly.GetName().Name,
                compilerAssembly = compilerAssembly,
                assemblyTypePresent = assemblyTypePresent,
                instanceId = script == null ? 0 : script.GetInstanceID(),
                resolved = type != null
            });
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic) continue;
            string name = assembly.GetName().Name;
            if (name == "Assembly-CSharp" || name == "Assembly-CSharp-Editor")
            {
                report.loadedAssemblies.Add(name + " " + assembly.Location);
                foreach (Type type in assembly.GetTypes())
                    if (type.FullName != null && type.FullName.StartsWith("WorkshopDemo.", StringComparison.Ordinal))
                        report.workshopTypes.Add(name + ": " + type.FullName);
            }
        }

        foreach (var assembly in CompilationPipeline.GetAssemblies(AssembliesType.Player))
            report.compilationAssemblies.Add(assembly.name + ": " +
                assembly.sourceFiles.Length + " source files");

        string destination = Path.GetFullPath(
            "Evidence/fault-module-20261116/asset-database-mapping-probe-" +
            DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(destination));
        File.WriteAllText(destination, JsonUtility.ToJson(report, true));
        Debug.Log("Fault asset database diagnostic written: " + destination +
            "; scripts=" + report.scripts.Count +
            "; unresolved=" + report.scripts.Count(row => !row.resolved) +
            "; missingComponents=" + report.missing.Sum(row => row.count));

        if (!string.IsNullOrEmpty(originalScene) && File.Exists(originalScene))
            EditorSceneManager.OpenScene(originalScene, OpenSceneMode.Single);
        if (buildAfterRecovery)
        {
            if (report.scripts.Any(row => !row.resolved) || report.missing.Count != 0)
            {
                Debug.LogError("Script recovery did not restore scene scripts; candidate build skipped.");
                EditorApplication.Exit(1);
                return;
            }
            WorkshopDemo.Editor.WorkshopPlatformBuild.BuildFaultReview();
        }
        EditorApplication.Exit(0);
    }

    static void CollectMissing(Transform transform, string scenePath, List<MissingRow> rows)
    {
        GameObject gameObject = transform.gameObject;
        int count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject);
        if (count > 0)
            rows.Add(new MissingRow
            {
                scene = scenePath,
                gameObject = BuildPath(transform),
                count = count
            });

        for (int i = 0; i < transform.childCount; i++)
            CollectMissing(transform.GetChild(i), scenePath, rows);
    }

    static string BuildPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }
}
