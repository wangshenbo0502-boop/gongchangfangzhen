using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace WorkshopDemo
{
    [Serializable] public sealed class WorkshopFieldFile { public string path, sha256; }
    [Serializable] public sealed class WorkshopFieldIndex { public int frameIndex; public float axisValue; public string path; }
    [Serializable] public sealed class WorkshopFieldElement { public int elementLabel; public string type; public int[] nodeIndices; }
    [Serializable] public sealed class WorkshopFieldMesh
    {
        public string meshId, instanceName;
        public int[] nodeLabels, triangles, triangleElementLabels, triangleFaces;
        public float[] coordinatesM;
        public WorkshopFieldElement[] elements;
    }
    [Serializable] public sealed class WorkshopFieldFrame
    {
        public int frameIndex;
        public float axisValue;
        public string meshId;
        public float[] temperatureC;
    }
    [Serializable] public sealed class WorkshopFieldManifest
    {
        public int schemaVersion;
        public string source, deviceId, faultCode, caseId, componentId, modelVersion, job, step, region;
        public string odbSha256, inputSha256, exporterVersion, solverVersion, analysisDescription, exportedAt;
        public string axisKind, axisUnit, coordinatesSpace, coordinatesUnit, coordinateConversion;
        public string sourceLengthUnit, sourceTemperatureUnit, fieldName, fieldUnit, outputPosition, averaging, meshPath;
        public float lengthToM, temperatureOffsetC;
        public float[] displayTransform;
        public WorkshopFieldFile[] files;
        public WorkshopFieldIndex[] frames;
    }

    // The first v2 contract deliberately supports one fixed DC3D4 thermal mesh.
    public sealed class WorkshopAbaqusFieldResult
    {
        public const long ManifestBudget = 1024 * 1024, FileBudget = 32 * 1024 * 1024, PackageBudget = 256 * 1024 * 1024;
        public readonly string Root;
        public readonly WorkshopFieldManifest Manifest;
        public readonly WorkshopFieldMesh Mesh;
        readonly Dictionary<int, WorkshopFieldFrame> cache = new Dictionary<int, WorkshopFieldFrame>();
        readonly Dictionary<string, string> hashes;
        public int CachedFrames => cache.Count;
        public string AxisLabel => Manifest.axisKind == "physicalTime" ? "物理时间" :
            Manifest.axisKind == "loadParameter" ? "载荷参数" : Manifest.axisKind == "frequency" ? "频率" :
            Manifest.axisKind == "mode" ? "模态编号" : "循环数";

        WorkshopAbaqusFieldResult(string root, WorkshopFieldManifest manifest, WorkshopFieldMesh mesh)
        {
            Root = root; Manifest = manifest; Mesh = mesh;
            hashes = manifest.files.ToDictionary(f => f.path, f => f.sha256, StringComparer.OrdinalIgnoreCase);
        }
        static bool Finite(float n) => !float.IsNaN(n) && !float.IsInfinity(n);
        static void Require(bool valid, string reason) { if (!valid) throw new ArgumentException(reason); }
        public static bool Identifier(string text) => !string.IsNullOrWhiteSpace(text) && text.Length <= 96 &&
            text.All(c => c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-' || c == '_') &&
            char.IsLetterOrDigit(text[0]);
        public static bool Hash(string text) => text != null && text.Length == 64 && text.All(Uri.IsHexDigit);

        public static string SafePath(string root, string relative)
        {
            Require(!string.IsNullOrWhiteSpace(relative) && !Path.IsPathRooted(relative) &&
                !relative.Contains("\\") && relative.Split('/').All(p => Identifier(p.Replace(".", "_")) &&
                    p != "." && p != "..") && !relative.Contains(":"), "Unsafe package path: " + relative);
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string path = Path.GetFullPath(Path.Combine(fullRoot, relative));
            Require(path.StartsWith(fullRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase),
                "Package path escapes root");
            for (string current = path; current != null && current.Length >= fullRoot.Length; current = Path.GetDirectoryName(current))
                if (File.Exists(current) || Directory.Exists(current))
                    Require((File.GetAttributes(current) & FileAttributes.ReparsePoint) == 0, "Package links are not supported");
            return path;
        }

        static string Read(string path, long budget, string hash = null)
        {
            Require(File.Exists(path) && new FileInfo(path).Length <= budget, "Missing or oversized file: " + Path.GetFileName(path));
            byte[] bytes = File.ReadAllBytes(path);
            Require(bytes.LongLength <= budget, "File grew beyond budget");
            if (hash != null)
            {
                using var sha = SHA256.Create();
                Require(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").Equals(hash, StringComparison.OrdinalIgnoreCase),
                    "SHA256 mismatch: " + Path.GetFileName(path));
            }
            return System.Text.Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        }

        static long CheckHash(string path, string hash)
        {
            using var stream = File.OpenRead(path);
            Require(stream.Length <= FileBudget, "Oversized field package file");
            long length = stream.Length;
            using var sha = SHA256.Create();
            Require(BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").Equals(hash, StringComparison.OrdinalIgnoreCase),
                "SHA256 mismatch: " + Path.GetFileName(path));
            Require(stream.Length == length, "File changed during validation");
            return length;
        }

        public static WorkshopAbaqusFieldResult Open(string root, string device, string fault, bool validateAllFrames = false)
        {
            string json = Read(SafePath(root, "manifest.json"), ManifestBudget);
            var tree = WorkshopResultJson.Parse(json);
            var m = JsonUtility.FromJson<WorkshopFieldManifest>(json);
            Require(m != null && m.schemaVersion == 2 && m.source == "Abaqus ODB" &&
                Identifier(m.caseId) && m.deviceId == device && m.faultCode == fault,
                "Invalid schema, source or device/fault binding");
            foreach (string text in new[] { m.componentId, m.modelVersion, m.job, m.step, m.region, m.exporterVersion,
                m.solverVersion, m.analysisDescription, m.exportedAt, m.sourceLengthUnit, m.sourceTemperatureUnit, m.coordinateConversion })
                Require(!string.IsNullOrWhiteSpace(text), "Missing provenance metadata");
            Require(Hash(m.odbSha256) && Hash(m.inputSha256), "Invalid source hashes");
            var axes = new Dictionary<string, string> { { "physicalTime", "s" }, { "loadParameter", "1" },
                { "frequency", "Hz" }, { "mode", "1" }, { "cycles", "cycle" } };
            Require(m.axisKind != null && axes.TryGetValue(m.axisKind, out var unit) && m.axisUnit == unit, "Invalid analysis axis");
            WorkshopResultJson.Number(tree, "lengthToM"); WorkshopResultJson.Number(tree, "temperatureOffsetC");
            WorkshopResultJson.Numbers(tree, "displayTransform");
            Require(m.coordinatesSpace == "globalCartesian" && m.coordinatesUnit == "m" &&
                Finite(m.lengthToM) && m.lengthToM > 0 && Finite(m.temperatureOffsetC) &&
                m.displayTransform != null && m.displayTransform.Length == 16 &&
                m.displayTransform.Select((v, i) => v == (i % 5 == 0 ? 1 : 0)).All(v => v),
                "First thermal reader requires converted global XYZ metres and identity display transform");
            Require(m.fieldName == "temperatureC" && m.fieldUnit == "C" && m.outputPosition == "NODAL" && m.averaging == "none",
                "Unsupported field, output position or averaging");
            Require(m.files != null && m.files.Length >= 2 && m.files.Length <= 20001 &&
                m.frames != null && m.frames.Length >= 1 && m.frames.Length <= 20000, "Invalid file/frame count");
            var listed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = new FileInfo(SafePath(root, "manifest.json")).Length;
            // Validate all hashes before deserializing any large mesh or field array.
            foreach (var file in m.files)
            {
                Require(file != null && Hash(file.sha256) && listed.Add(file.path), "Invalid or duplicate file manifest");
                string path = SafePath(root, file.path);
                total += CheckHash(path, file.sha256);
                Require(total <= PackageBudget, "Package exceeds 256 MiB");
            }
            Require(listed.Contains(m.meshPath) && !listed.Contains("manifest.json"), "Mesh missing from file list");
            var framePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var rows = WorkshopResultJson.Required(tree, "frames");
            Require(rows.IsList && rows.AsList.Count == m.frames.Length, "Invalid frame index array");
            float previous = -1;
            for (int i = 0; i < m.frames.Length; i++)
            {
                WorkshopResultJson.Number(rows.AsList[i], "axisValue");
                WorkshopResultJson.Number(rows.AsList[i], "frameIndex");
                var f = m.frames[i];
                Require(f != null && f.frameIndex == i && Finite(f.axisValue) && f.axisValue >= 0 && f.axisValue > previous &&
                    listed.Contains(f.path) && f.path != m.meshPath && framePaths.Add(f.path), "Invalid frame index or axis ordering");
                if (m.axisKind == "mode" || m.axisKind == "cycles")
                    Require(f.axisValue == Mathf.Floor(f.axisValue), "Discrete axis must contain integers");
                previous = f.axisValue;
            }
            Require(listed.Count == framePaths.Count + 1, "Unreferenced package files");
            string meshJson = Read(SafePath(root, m.meshPath), FileBudget, m.files.Single(f => f.path == m.meshPath).sha256);
            var meshTree = WorkshopResultJson.Parse(meshJson);
            foreach (string key in new[] { "nodeLabels", "triangles", "triangleElementLabels", "triangleFaces" })
                WorkshopResultJson.Numbers(meshTree, key, true);
            WorkshopResultJson.Numbers(meshTree, "coordinatesM");
            var rawElements = WorkshopResultJson.Required(meshTree, "elements");
            Require(rawElements.IsList, "Invalid element list");
            foreach (var element in rawElements.AsList)
            {
                WorkshopResultJson.Number(element, "elementLabel");
                WorkshopResultJson.Numbers(element, "nodeIndices", true);
            }
            var mesh = JsonUtility.FromJson<WorkshopFieldMesh>(meshJson);
            ValidateMesh(mesh, m.region);
            var result = new WorkshopAbaqusFieldResult(Path.GetFullPath(root), m, mesh);
            if (validateAllFrames) for (int i = 0; i < m.frames.Length; i++) result.Frame(i);
            return result;
        }

        static void ValidateMesh(WorkshopFieldMesh mesh, string region)
        {
            Require(mesh != null && mesh.meshId == "fixed-0" && mesh.instanceName == region && mesh.nodeLabels != null &&
                mesh.nodeLabels.Length >= 4 && mesh.nodeLabels.Length <= 200000 &&
                mesh.nodeLabels.All(n => n > 0) && mesh.nodeLabels.Distinct().Count() == mesh.nodeLabels.Length &&
                mesh.coordinatesM != null && mesh.coordinatesM.Length == mesh.nodeLabels.Length * 3 && mesh.coordinatesM.All(Finite),
                "Invalid node identities or coordinates");
            int count = mesh.nodeLabels.Length;
            Require(mesh.elements != null && mesh.elements.Length > 0 && mesh.elements.Length <= 500000, "Invalid element count");
            var elements = new Dictionary<int, WorkshopFieldElement>();
            var faces = new Dictionary<string, int>();
            int[][] faceOrder = { new[] { 0, 1, 2, 3 }, new[] { 0, 3, 1, 2 }, new[] { 1, 3, 2, 0 }, new[] { 2, 3, 0, 1 } };
            string Key(IEnumerable<int> nodes) => string.Join(",", nodes.OrderBy(n => n));
            Vector3 Position(int n) => new Vector3(mesh.coordinatesM[n * 3], mesh.coordinatesM[n * 3 + 1], mesh.coordinatesM[n * 3 + 2]);
            foreach (var e in mesh.elements)
            {
                Require(e != null && e.type == "DC3D4", "Unsupported element type (only DC3D4)");
                Require(e.elementLabel > 0 && !elements.ContainsKey(e.elementLabel) && e.nodeIndices != null &&
                    e.nodeIndices.Length == 4 && e.nodeIndices.All(n => n >= 0 && n < count) &&
                    e.nodeIndices.Distinct().Count() == 4, "Invalid element connectivity");
                var p = e.nodeIndices.Select(Position).ToArray();
                Require(Math.Abs(Vector3.Dot(Vector3.Cross(p[1] - p[0], p[2] - p[0]), p[3] - p[0])) > 1e-24f,
                    "Degenerate tetrahedron");
                elements.Add(e.elementLabel, e);
                foreach (var order in faceOrder)
                {
                    string key = Key(order.Take(3).Select(n => e.nodeIndices[n]));
                    faces.TryGetValue(key, out int uses); faces[key] = uses + 1;
                    Require(uses < 2, "Nonmanifold tetrahedron face");
                }
            }
            Require(mesh.triangles != null && mesh.triangles.Length > 0 && mesh.triangles.Length % 3 == 0 &&
                mesh.triangleElementLabels != null && mesh.triangleElementLabels.Length == mesh.triangles.Length / 3 &&
                mesh.triangleFaces != null && mesh.triangleFaces.Length == mesh.triangleElementLabels.Length,
                "Invalid surface mappings");
            var seen = new HashSet<string>();
            for (int i = 0; i < mesh.triangleFaces.Length; i++)
            {
                Require(elements.TryGetValue(mesh.triangleElementLabels[i], out var element) &&
                    mesh.triangleFaces[i] >= 1 && mesh.triangleFaces[i] <= 4, "Unknown surface owner");
                int[] triangle = { mesh.triangles[i * 3], mesh.triangles[i * 3 + 1], mesh.triangles[i * 3 + 2] };
                var order = faceOrder[mesh.triangleFaces[i] - 1];
                string key = Key(triangle);
                Require(key == Key(order.Take(3).Select(n => element.nodeIndices[n])) &&
                    faces[key] == 1 && seen.Add(key), "Surface includes an internal, duplicate or mismatched face");
                Vector3 a = Position(triangle[0]), b = Position(triangle[1]), c = Position(triangle[2]);
                Require(Vector3.Dot(Vector3.Cross(b - a, c - a), Position(element.nodeIndices[order[3]]) - a) < 0,
                    "Surface normal points inward");
            }
            Require(seen.Count == faces.Count(f => f.Value == 1), "Incomplete external surface");
        }

        public WorkshopFieldFrame Frame(int index)
        {
            index = Mathf.Clamp(index, 0, Manifest.frames.Length - 1);
            foreach (int old in cache.Keys.Where(k => Math.Abs(k - index) > 1).ToArray()) cache.Remove(old);
            if (cache.TryGetValue(index, out var cached)) return cached;
            var entry = Manifest.frames[index];
            string json = Read(SafePath(Root, entry.path), FileBudget, hashes[entry.path]);
            var tree = WorkshopResultJson.Parse(json);
            WorkshopResultJson.Number(tree, "frameIndex"); WorkshopResultJson.Number(tree, "axisValue");
            var values = WorkshopResultJson.Required(tree, "temperatureC");
            Require(values.IsList && values.AsList.Count == Mesh.nodeLabels.Length &&
                values.AsList.All(v => v.IsInt64 || v.IsDouble), "Missing or nonnumeric temperature field");
            var frame = JsonUtility.FromJson<WorkshopFieldFrame>(json);
            Require(frame != null && frame.frameIndex == index && frame.axisValue == entry.axisValue && frame.meshId == Mesh.meshId &&
                frame.temperatureC != null && frame.temperatureC.Length == Mesh.nodeLabels.Length &&
                frame.temperatureC.All(v => Finite(v) && v >= -273.15f), "Invalid frame, mesh or nodal temperature");
            cache[index] = frame;
            return frame;
        }
    }
}
