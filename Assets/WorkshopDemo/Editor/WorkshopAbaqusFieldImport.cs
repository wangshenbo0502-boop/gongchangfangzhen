using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace WorkshopDemo.Editor
{
    public static class WorkshopAbaqusFieldImport
    {
        [Serializable] sealed class Bindings { public Binding[] bindings; }
        [Serializable] sealed class Binding { public string id, model; }
        [MenuItem("Tools/Workshop Demo/Import Abaqus Local Thermal Field")]
        public static void Import()
        {
            string path = EditorUtility.OpenFilePanel("Select complete v2 manifest.json", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            try { Debug.Log("Imported validated field package: " + ImportPackage(Path.GetDirectoryName(path))); AssetDatabase.Refresh(); }
            catch (Exception e) { Debug.LogError("Abaqus field import rejected: " + e.Message); }
        }

        public static string ImportPackage(string source)
        {
            string manifestPath = WorkshopAbaqusFieldResult.SafePath(source, "manifest.json");
            if (new FileInfo(manifestPath).Length > WorkshopAbaqusFieldResult.ManifestBudget)
                throw new ArgumentException("Manifest exceeds 1 MiB");
            var header = JsonUtility.FromJson<WorkshopFieldManifest>(File.ReadAllText(manifestPath));
            string[] ids = { "P-01", "TK-01", "F-01", "PLC-01" };
            var kinds = new[] { DeviceKind.Pump, DeviceKind.Tank, DeviceKind.Fan, DeviceKind.Cabinet };
            int index = header == null ? -1 : Array.IndexOf(ids, header.deviceId);
            if (index < 0 || WorkshopFaultCatalog.Find(header.faultCode)?.Kind != kinds[index])
                throw new ArgumentException("Unknown or mismatched device/fault");
            var result = WorkshopAbaqusFieldResult.Open(source, header.deviceId, header.faultCode, true);
            var bindings = Resources.Load<TextAsset>("WorkshopFaults/bindings");
            string[] models = { "water-pump", "silo", "industrial-fan", "electrical-cabinet" };
            if (!bindings || !JsonUtility.FromJson<Bindings>(bindings.text).bindings
                .Any(b => b.id == header.componentId && b.model == models[index]))
                throw new ArgumentException("Unknown component binding");
            string parent = Path.GetFullPath(Path.Combine("Assets/StreamingAssets/WorkshopAbaqusFields", header.deviceId, header.faultCode));
            string target = WorkshopAbaqusFieldResult.SafePath(parent, header.caseId);
            string stage = WorkshopAbaqusFieldResult.SafePath(parent, "stage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            foreach (string relative in result.Manifest.files.Select(f => f.path).Append("manifest.json"))
            {
                string destination = WorkshopAbaqusFieldResult.SafePath(stage, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                string directory = Path.GetDirectoryName(destination);
                while (directory.Length > stage.Length)
                {
                    string relativeDirectory = directory.Substring(stage.Length + 1).Replace('\\', '/');
                    string oldDirectoryMeta = WorkshopAbaqusFieldResult.SafePath(target, relativeDirectory + ".meta");
                    if (File.Exists(oldDirectoryMeta) && !File.Exists(directory + ".meta"))
                        File.Copy(oldDirectoryMeta, directory + ".meta", false);
                    directory = Path.GetDirectoryName(directory);
                }
                File.Copy(WorkshopAbaqusFieldResult.SafePath(source, relative), destination, false);
                // Keep existing asset identities for paths that survive the update.
                string oldMeta = WorkshopAbaqusFieldResult.SafePath(target, relative + ".meta");
                if (File.Exists(oldMeta)) File.Copy(oldMeta, destination + ".meta", false);
            }
            WorkshopAbaqusFieldResult.Open(stage, header.deviceId, header.faultCode, true);
            string backup = null;
            if (Directory.Exists(target))
            {
                string archive = Path.GetFullPath("Evidence/AbaqusFieldImportBackups");
                Directory.CreateDirectory(archive);
                backup = WorkshopAbaqusFieldResult.SafePath(archive, header.caseId + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmssfff"));
                Directory.Move(target, backup);
            }
            try { Directory.Move(stage, target); }
            catch { if (backup != null && !Directory.Exists(target)) Directory.Move(backup, target); throw; }
            return target;
        }
    }
}
