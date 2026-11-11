using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace WorkshopDemo
{
    [Serializable]
    public sealed class WorkshopAnalysisMesh
    {
        public string label, elementType;
        public float[] x, u, stress, temperature, tensor, principal, flux, appliedN, reactionN;
        public int[] tri;
        public int nodes, elements;
        public float maxStress, maxDisplacement, maxTemperature, residual, balance, energy, powerW, heatOutW, volumeM3;
        public Vector3 Point(int i) => new Vector3(x[i * 3], x[i * 3 + 2], x[i * 3 + 1]);
        public Vector3 Displacement(int i) => new Vector3(u[i * 3], u[i * 3 + 2], u[i * 3 + 1]);
        public float FaceValue(int field, int face, int node)
        {
            if (field == 0) return stress[face];
            if (field == 1) return Displacement(node).magnitude * 1000;
            if (field == 2) return temperature[node];
            if (field >= 3 && field <= 5) return u[node * 3 + field - 3] * 1000;
            if (field >= 6 && field <= 11) return tensor[face * 6 + field - 6];
            if (field >= 12 && field <= 14) return principal[face * 3 + field - 12];
            if (field == 15) return (tensor[face*6] + tensor[face*6+1] + tensor[face*6+2]) / 3;
            if (field == 16) return stress[face] / (3 * (210000f / 2.6f)) * 1000000;
            if (field == 17) return new Vector3(flux[face*3], flux[face*3+1], flux[face*3+2]).magnitude;
            return 0;
        }
    }

    [Serializable]
    public sealed class WorkshopAnalysisResult
    {
        public int schema;
        public string code, component, solver, source, analysis, geometry, material, boundary, change, mechanism, limitation, shape, variable;
        public float[] dimensionsMm, inputValues;
        public WorkshopAnalysisMesh[] models;
        public float stressRange, displacementRange, temperatureRange;
        [NonSerialized] public string sha256;
        readonly System.Collections.Generic.Dictionary<int, Vector2> ranges = new System.Collections.Generic.Dictionary<int, Vector2>();
        public static readonly string[] FieldNames = { "Mises 应力", "位移模长", "温度",
            "Ux 位移", "Uy 位移", "Uz 位移", "Sxx 正应力", "Syy 正应力", "Szz 正应力",
            "Sxy 剪应力", "Syz 剪应力", "Sxz 剪应力", "最大主应力", "中间主应力",
            "最小主应力", "平均正应力", "等效偏应变", "热流密度" };
        public static string Unit(int field) => field == 2 ? "°C" : field == 1 || field >= 3 && field <= 5 ?
            "mm" : field == 16 ? "με" : field == 17 ? "W/m²" : "MPa";
        public void Range(int field, out float low, out float high)
        {
            if (ranges.TryGetValue(field,out var cached)) { low = cached.x; high = cached.y; return; }
            low = float.MaxValue; high = float.MinValue;
            foreach (var m in models)
                for (int f = 0; f < m.tri.Length / 3; f++)
                    for (int c = 0; c < 3; c++)
                    {
                        float v = m.FaceValue(field, f, m.tri[f*3+c]);
                        low = Mathf.Min(low, v); high = Mathf.Max(high, v);
                    }
            if (field == 0 || field == 1 || field == 16 || field == 17) low = 0;
            if (field == 2) low = 25;
            if (field >= 3 && field <= 15) { high = Mathf.Max(Mathf.Abs(low), Mathf.Abs(high)); low = -high; }
            ranges[field] = new Vector2(low,high);
        }

        public static WorkshopAnalysisResult Load(string code, out string message)
        {
            message = "此工况属于过程 / 控制故障，没有可用的有限元参考模型。";
            string path = Path.Combine(Application.streamingAssetsPath, "WorkshopAnalysis", code + ".json");
            if (!File.Exists(path)) return null;
            try
            {
                byte[] bytes = File.ReadAllBytes(path);
                var result = JsonUtility.FromJson<WorkshopAnalysisResult>(System.Text.Encoding.UTF8.GetString(bytes));
                if (result == null || result.schema != 1 || result.code != code || result.models == null || result.models.Length != 2 ||
                    (result.analysis != "structural" && result.analysis != "thermal"))
                    throw new InvalidDataException("Invalid analysis schema");
                foreach (var m in result.models)
                {
                    if (m == null || m.x == null || m.x.Length < 12 || m.x.Length % 3 != 0 || m.u == null || m.u.Length != m.x.Length ||
                        m.tri == null || m.tri.Length < 3 || m.tri.Length % 3 != 0 ||
                        m.stress == null || m.stress.Length != m.tri.Length / 3 ||
                        m.temperature == null || m.temperature.Length != m.x.Length / 3 ||
                        m.nodes != m.x.Length / 3 || m.elements < 1)
                        throw new InvalidDataException("Invalid field dimensions");
                    int faces = m.tri.Length / 3;
                    if (result.analysis == "structural" ?
                        m.tensor == null || m.tensor.Length != faces * 6 || m.principal == null || m.principal.Length != faces * 3 :
                        m.flux == null || m.flux.Length != faces * 3)
                        throw new InvalidDataException("Missing tensor or heat flux");
                    foreach (int index in m.tri)
                        if (index < 0 || index >= m.nodes) throw new InvalidDataException("Invalid triangle");
                    foreach (var values in new[] { m.x, m.u, m.stress, m.temperature })
                        foreach (float v in values)
                            if (float.IsNaN(v) || float.IsInfinity(v)) throw new InvalidDataException("Invalid field value");
                    foreach (var values in new[] { m.tensor, m.principal, m.flux })
                        if (values != null) foreach (float v in values)
                            if (float.IsNaN(v) || float.IsInfinity(v)) throw new InvalidDataException("Invalid derived field");
                    foreach (float v in new[] { m.maxStress, m.maxDisplacement, m.maxTemperature,
                        m.residual, m.balance, result.stressRange, result.displacementRange, result.temperatureRange })
                        if (float.IsNaN(v) || float.IsInfinity(v) || v < 0)
                            throw new InvalidDataException("Invalid result metadata");
                    if (m.residual > 1e-5f || m.balance > 1e-5f)
                        throw new InvalidDataException("Solver balance check failed");
                }
                using var sha = SHA256.Create();
                result.sha256 = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                message = result.source;
                return result;
            }
            catch (Exception e) when (e is IOException || e is ArgumentException || e is InvalidDataException)
            {
                message = "分析结果不可用：" + e.Message;
                return null;
            }
        }
    }
}
