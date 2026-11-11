using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WorkshopDemo.Editor
{
    public static class WorkshopAbaqusImport
    {
        [MenuItem("Tools/Workshop Demo/Import Abaqus Fault Result")]
        public static void Import()
        {
            string path = EditorUtility.OpenFilePanel("Import Abaqus ODB envelope JSON", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (new FileInfo(path).Length > 8 * 1024 * 1024)
                    throw new ArgumentException("Result exceeds 8 MiB.");
                string json = File.ReadAllText(path);
                var header = JsonUtility.FromJson<WorkshopAbaqusResult>(json);
                var fault = WorkshopFaultCatalog.Find(header?.faultCode);
                string[] ids = { "CV-01", "M-01", "P-01", "P-02", "TK-01", "TK-02", "F-01", "PLC-01" };
                var kinds = new[] { DeviceKind.Conveyor, DeviceKind.Motor, DeviceKind.Pump, DeviceKind.Pump,
                    DeviceKind.Tank, DeviceKind.Tank, DeviceKind.Fan, DeviceKind.Cabinet };
                int index = header == null ? -1 : Array.IndexOf(ids, header.deviceId);
                if (index < 0 || fault == null || fault.Kind != kinds[index])
                    throw new ArgumentException("Unknown or mismatched device/fault identifiers.");
                var result = WorkshopAbaqusResult.Parse(json, header.deviceId, header.faultCode);
                string folder = Path.Combine("Assets/StreamingAssets/WorkshopFaultResults", result.deviceId);
                string target = Path.Combine(folder, result.faultCode + ".json");
                if (File.Exists(target))
                {
                    string backupFolder = "Evidence/AbaqusImportBackups";
                    Directory.CreateDirectory(backupFolder);
                    File.Copy(target, Path.Combine(backupFolder, result.deviceId + "-" + result.faultCode + "-" +
                        DateTime.Now.ToString("yyyyMMdd-HHmmssfff") + ".json"), false);
                }
                Directory.CreateDirectory(folder);
                File.WriteAllText(target, json);
                AssetDatabase.Refresh();
                Debug.Log("Imported " + target + ". Reinject the matching fault to replay. " + result.job);
            }
            catch (Exception exception)
            {
                Debug.LogError("Abaqus import rejected: " + exception.Message);
            }
        }
    }
}
