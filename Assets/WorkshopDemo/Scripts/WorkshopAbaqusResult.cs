using System;
using System.IO;
using UnityEngine;

namespace WorkshopDemo
{
    [Serializable]
    public sealed class WorkshopAbaqusFrame
    {
        public float time, maxStressMPa, maxDisplacementMm, maxTemperatureC;
    }

    [Serializable]
    public sealed class WorkshopAbaqusResult
    {
        public int schemaVersion;
        public string source, deviceId, faultCode, job, step, region, odbSha256, exportedAt, analysisDescription, timeMeaning;
        public string timeUnit, stressUnit, displacementUnit, temperatureUnit;
        public bool hasStress, hasDisplacement, hasTemperature;
        public WorkshopAbaqusFrame[] frames;

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static WorkshopAbaqusResult Parse(string json, string device, string fault)
        {
            var result = JsonUtility.FromJson<WorkshopAbaqusResult>(json);
            if (result == null || result.schemaVersion != 1 || result.source != "Abaqus ODB" ||
                result.deviceId != device || result.faultCode != fault ||
                string.IsNullOrWhiteSpace(result.job) || string.IsNullOrWhiteSpace(result.step) ||
                string.IsNullOrWhiteSpace(result.region) || string.IsNullOrWhiteSpace(result.exportedAt) ||
                string.IsNullOrWhiteSpace(result.analysisDescription) || result.timeMeaning != "physical" ||
                result.odbSha256 == null || result.odbSha256.Length != 64 ||
                Array.Exists(result.odbSha256.ToCharArray(), c => !Uri.IsHexDigit(c)) ||
                result.timeUnit != "s" || result.stressUnit != "MPa" ||
                result.displacementUnit != "mm" || result.temperatureUnit != "C" ||
                (!result.hasStress && !result.hasDisplacement && !result.hasTemperature) ||
                result.frames == null || result.frames.Length == 0 || result.frames.Length > 20000)
                throw new ArgumentException("Abaqus result metadata, units or device/fault binding is invalid.");
            float previous = -1;
            foreach (var frame in result.frames)
            {
                if (frame == null || !Finite(frame.time) || frame.time < 0 || frame.time <= previous ||
                    (result.hasStress && (!Finite(frame.maxStressMPa) || frame.maxStressMPa < 0)) ||
                    (result.hasDisplacement && (!Finite(frame.maxDisplacementMm) || frame.maxDisplacementMm < 0)) ||
                    (result.hasTemperature && (!Finite(frame.maxTemperatureC) || frame.maxTemperatureC < -273.15f)))
                    throw new ArgumentException("Abaqus frames must contain finite values and increasing time.");
                previous = frame.time;
            }
            return result;
        }

        public static WorkshopAbaqusResult Load(string device, string fault, out string source)
        {
            source = "教学故障模型 · 无 Abaqus 结果";
            string path = Path.Combine(Application.streamingAssetsPath, "WorkshopFaultResults", device, fault + ".json");
            if (!File.Exists(path)) return null;
            try
            {
                if (new FileInfo(path).Length > 8 * 1024 * 1024) throw new ArgumentException("Result exceeds 8 MiB.");
                var result = Parse(File.ReadAllText(path), device, fault);
                source = "Abaqus ODB 包络回放 · " + result.job;
                return result;
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException ||
                exception is UnauthorizedAccessException)
            {
                source = "教学故障模型 · Abaqus 文件校验失败";
                Debug.LogWarning("Ignored Abaqus result: " + exception.Message);
                return null;
            }
        }

        public WorkshopAbaqusFrame At(float seconds)
        {
            if (seconds <= frames[0].time) return frames[0];
            int end = Array.FindIndex(frames, f => f.time >= seconds);
            if (end < 0) return frames[frames.Length - 1];
            var a = frames[end - 1]; var b = frames[end];
            float t = Mathf.InverseLerp(a.time, b.time, seconds);
            return new WorkshopAbaqusFrame {
                time = seconds, maxStressMPa = Mathf.Lerp(a.maxStressMPa, b.maxStressMPa, t),
                maxDisplacementMm = Mathf.Lerp(a.maxDisplacementMm, b.maxDisplacementMm, t),
                maxTemperatureC = Mathf.Lerp(a.maxTemperatureC, b.maxTemperatureC, t)
            };
        }

        public string Describe(float seconds)
        {
            var frame = At(seconds);
            return "ODB 时间 " + frame.time.ToString("F2") + " s / " + frames[frames.Length - 1].time.ToString("F2") + " s\n" +
                (hasStress ? "最大 Mises 应力 " + frame.maxStressMPa.ToString("F2") + " MPa\n" : "") +
                (hasDisplacement ? "最大位移 " + frame.maxDisplacementMm.ToString("F3") + " mm\n" : "") +
                (hasTemperature ? "最高温度 " + frame.maxTemperatureC.ToString("F1") + " °C\n" : "") +
                "区域 " + region + " · 独立包络极值\n分析说明 " + analysisDescription;
        }
    }
}
