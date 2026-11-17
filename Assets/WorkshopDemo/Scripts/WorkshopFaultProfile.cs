using System.Collections.Generic;
using UnityEngine;

namespace WorkshopDemo
{
    internal readonly struct WorkshopFaultReadout
    {
        public readonly string Key, Label, Unit, Source;
        public readonly float? Value;

        public WorkshopFaultReadout(string key, string label, float? value, string unit, string source)
        {
            Key = key;
            Label = label;
            Value = value;
            Unit = unit;
            Source = source;
        }
    }

    internal readonly struct WorkshopFaultFrame
    {
        public readonly string Code, StageId, StageLabel, TelemetrySource;
        public readonly float Progress, Seconds, Level, Wetness, Drip, Pool;
        public readonly float Activity, Pressure, Flow;
        public readonly bool Running;
        public readonly float Temperature, Rpm, Vibration, Voltage, Communication;

        public WorkshopFaultFrame(string code, string stageId, string stageLabel, string telemetrySource,
            float progress, float seconds, float level, float wetness, float drip, float pool,
            float activity = 0, float pressure = 0, float flow = 0, bool running = false,
            float temperature = 25, float rpm = 0, float vibration = 0, float voltage = 24, float communication = 100)
        {
            Code = code;
            StageId = stageId;
            StageLabel = stageLabel;
            TelemetrySource = telemetrySource;
            Progress = progress;
            Seconds = seconds;
            Level = level;
            Wetness = wetness;
            Drip = drip;
            Pool = pool;
            Activity = activity;
            Pressure = pressure;
            Flow = flow;
            Running = running;
            Temperature = temperature; Rpm = rpm; Vibration = vibration; Voltage = voltage; Communication = communication;
        }

        public WorkshopFaultReadout LevelReadout => new WorkshopFaultReadout("tank_level", "液位", Level, "%", TelemetrySource);
        public WorkshopFaultReadout RateReadout => new WorkshopFaultReadout("leak_rate", "漏率", null, "L/min", "未求解");

        public IEnumerable<WorkshopFaultReadout> Readouts()
        {
            if (Code != "TK-LEAK" && Code != "P-SEAL" && Code != "P-CAV")
            {
                var spec = WorkshopFaultCase.Find(Code);
                if (spec != null) foreach (var entry in spec.Readouts(this)) yield return entry;
                yield break;
            }
            if (Code == "TK-LEAK")
            {
                yield return LevelReadout;
                yield return new WorkshopFaultReadout("perforation_diameter", "预设孔径",
                    Progress >= .25f ? 12 : 0, "mm", "派生网格 / 非腐蚀求解");
            }
            if (Code != "P-CAV")
            {
                yield return RateReadout;
                yield break;
            }
            yield return new WorkshopFaultReadout("outlet_pressure", "出口压力", Pressure, "MPa", TelemetrySource);
            yield return new WorkshopFaultReadout("outlet_flow", "出口流量", Flow, "m³/h", TelemetrySource);
            yield return new WorkshopFaultReadout("inlet_pressure", "入口压力", null, "MPa", "未求解");
        }
    }

    internal sealed class WorkshopFaultProfile
    {
        public static readonly WorkshopFaultProfile TankLeak = new WorkshopFaultProfile("TK-LEAK",
            "tank.shell.frontLeak", "巡检侧 · 筒体局部壁面", "派生壳体 · 12 mm 预设穿孔（非腐蚀求解）", "局部穿孔漏损教学演示",
            "滴漏开始", .25f, "持续漏液", .65f);
        public static readonly WorkshopFaultProfile PumpSeal = new WorkshopFaultProfile("P-SEAL",
            "pump.seal.chamber", "机械密封端面与轴侧出口", "循环水泵 · 机械密封泄漏示意", "密封劣化与残余滴漏",
            "间歇滴漏", .30f, "保护停机", .60f);
        public static readonly WorkshopFaultProfile PumpCavitation = new WorkshopFaultProfile("P-CAV",
            "pump.inlet.cover", "吸入口与叶轮入口", "循环水泵 · 汽蚀机理示意", "汽泡形成与溃灭",
            "汽泡初现", .20f, "出口波动", .60f);
        public static readonly WorkshopFaultProfile FanCrack = new WorkshopFaultProfile("F-CRACK",
            "fan.impeller.crack", "第一叶片前缘局部", "验收版风机 · 原叶片局部裂面 / 后缘连接保留",
            "预制局部裂面（非断裂求解）", "局部起裂", .20f, "裂面发展", .35f);
        public string Code { get; }
        public string BindingId { get; }
        public string Component { get; }
        public string GeometrySource { get; }
        public string Recipe { get; }
        public string FirstMilestone { get; }
        public float FirstProgress { get; }
        public string SecondMilestone { get; }
        public float SecondProgress { get; }
        public float Duration => 18;

        internal WorkshopFaultProfile(string code, string bindingId, string component, string geometry, string recipe,
            string first, float firstProgress, string second, float secondProgress)
        {
            Code = code;
            BindingId = bindingId;
            Component = component;
            GeometrySource = geometry;
            Recipe = recipe;
            FirstMilestone = first;
            FirstProgress = firstProgress;
            SecondMilestone = second;
            SecondProgress = secondProgress;
        }

        internal const float ImbalanceRotationScale = 1f / 48;

        internal static float ImbalancePreviewAngle(float seconds, float nominalRpm, float speedRatio)
        {
            // Integrate the preview's SmoothStep speed ramp so scrubbing has no accumulated state.
            const float rampSeconds = 18 * .7f;
            float t = Mathf.Max(0, seconds);
            float x = Mathf.Clamp01(t / rampSeconds);
            float severityIntegral = rampSeconds * (x * x * x - .5f * x * x * x * x) +
                Mathf.Max(0, t - rampSeconds);
            return 6 * nominalRpm * (t + (Mathf.Max(.25f, speedRatio) - 1) * severityIntegral) *
                ImbalanceRotationScale;
        }

        public WorkshopFaultFrame Evaluate(float progress, float seconds, WorkshopDevice live, float nominalRpm = 1450)
        {
            var snapshot = live ? live.Snapshot : null;
            if (this == PumpSeal)
            {
                float wear = Mathf.SmoothStep(0, 1, progress / FirstProgress);
                float buildup = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(FirstProgress, SecondProgress, progress));
                float tripAt = live && live.FaultInstance?.Injection == WorkshopFaultInjection.Immediate ? 0 : Duration * SecondProgress;
                float residual = Mathf.Exp(-Mathf.Max(0, seconds - tripAt) * .20f);
                bool running = snapshot != null ? snapshot.Running : progress < SecondProgress;
                string stage = progress <= 0 ? "intact" : progress < FirstProgress ? "wear" : running ? "dripping" : "residual";
                string label = stage == "intact" ? "完好状态" : stage == "wear" ? "密封端面接触异常" :
                    stage == "dripping" ? "间歇滴漏" : "保护停机 / 残余滴漏";
                return new WorkshopFaultFrame(Code, stage, label, "未求解", progress, seconds,
                    0, wear, buildup * residual, 0, running: running);
            }
            if (this == PumpCavitation)
            {
                bool running = snapshot == null || snapshot.Running;
                float activity = running ? Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.10f, .93f, progress)) : 0;
                float wave = Mathf.Sin(seconds * 12) * .7f + Mathf.Sin(seconds * 29) * .3f;
                float pressure = snapshot != null ? snapshot.Pressure : .32f - activity * (.148f - wave * .032f);
                float flow = snapshot != null ? snapshot.Flow : 18 - activity * (9 - wave * 1.75f);
                string stage = progress <= 0 ? "intact" : !running ? "stopped" : progress < FirstProgress ? "starved" :
                    progress < SecondProgress ? "bubbles" : "fluctuating";
                string label = stage == "intact" ? "完好状态" : stage == "stopped" ? "停机 / 汽泡效果停止" :
                    stage == "starved" ? "入口供液不足" : stage == "bubbles" ? "叶轮入口形成汽泡" : "汽泡溃灭 / 出口压力流量波动";
                return new WorkshopFaultFrame(Code, stage, label, live ? "车间模拟采样" : "独立教学预览",
                    progress, seconds, 0, 0, 0, 0, activity, pressure, flow, running);
            }
            if (this == TankLeak) return EvaluateTank(progress, seconds, snapshot?.Level);
            var fault = WorkshopFaultCatalog.Find(Code);
            float severity = Mathf.SmoothStep(0, 1, progress / .7f);
            bool active = snapshot != null ? snapshot.Running : !fault.Trips || progress < WorkshopDevice.ProtectionProgress(Code);
            float temperature = snapshot != null ? snapshot.Temperature : Mathf.Lerp(25, fault.Temperature, severity);
            if (Code == "PLC-CONTACT" && !live)
                temperature = progress < .7f ? Mathf.Lerp(25, 96, severity) : 25 + 71 * Mathf.Exp(-(seconds - .7f * Duration) / 45);
            string genericStage = progress <= 0 ? "intact" : this == FanCrack ?
                progress < .2f ? "locating" : progress < .35f ? "initiation" : progress < .8f ? "opening" : "retained" :
                progress < .25f ? "onset" : progress < .7f ? "developing" : !active && fault.Trips ? "protected" : "established";
            string genericLabel = genericStage == "intact" ? "完好状态" : this == FanCrack ?
                genericStage == "locating" ? "局部定位 / 裂面闭合" : genericStage == "initiation" ? "预设局部起裂" :
                genericStage == "opening" ? "局部裂面发展" : "末态保持 / 后缘连接保留" :
                genericStage == "onset" ? "异常定位" : genericStage == "developing" ? "异常发展" :
                genericStage == "protected" ? "保护停机 / 残余状态" : "异常运行 / 末态保持";
            return new WorkshopFaultFrame(Code, genericStage, genericLabel, live ? "车间教学快照" : "独立教学预览",
                progress, seconds, snapshot != null ? snapshot.Level : Code == "TK-LEVEL" ? Mathf.Lerp(65, 96, severity) : 65,
                severity, severity, 0, severity,
                snapshot != null ? snapshot.Pressure : Mathf.Lerp(Code.StartsWith("TK-") ? .45f : .32f,
                    (Code.StartsWith("TK-") ? .45f : .32f) * fault.ProcessRatio, severity),
                snapshot != null ? snapshot.Flow : active ? 18 * Mathf.Lerp(1, fault.ProcessRatio, severity) : 0,
                active, temperature, snapshot != null ? snapshot.Rpm : active ? nominalRpm * Mathf.Lerp(1, Mathf.Max(.25f, fault.SpeedRatio), severity) : 0,
                snapshot != null ? snapshot.Vibration : active ? Mathf.Lerp(1.2f, fault.Vibration, severity) : 0,
                snapshot != null ? snapshot.Voltage : Code == "PLC-LOW" ? Mathf.Lerp(24, 17.3f, severity) : 24,
                snapshot != null ? snapshot.Communication : Code == "PLC-COMM" ? Mathf.Lerp(100, 0, severity) : 100);
        }

        WorkshopFaultFrame EvaluateTank(float progress, float seconds, float? liveLevel)
        {
            float level = liveLevel ?? WorkshopDevice.TeachingTankLeakLevel(seconds);
            float wet = Mathf.SmoothStep(0, 1, progress / SecondProgress);
            float flow = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(FirstProgress, SecondProgress, progress)) *
                Mathf.InverseLerp(10, 65, level);
            string source = liveLevel.HasValue ? "车间模拟液位" : "教学液位曲线";
            if (progress <= 0) return new WorkshopFaultFrame(Code, "intact", "完好状态", source, 0, seconds, level, 0, 0, 0);
            string stage = progress < FirstProgress ? "wetting" : progress < SecondProgress ? "dripping" : "pooling";
            string label = progress < FirstProgress ? "漏点定位 / 初始湿润" :
                progress < SecondProgress ? "滴漏发展 / 壁面水迹" :
                flow > .001f ? "持续漏液 / 地面积液" : "残余水迹 / 地面积液";
            return new WorkshopFaultFrame(Code, stage, label, source, progress, seconds, level, wet, flow,
                Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.38f, 1, progress)));
        }
    }
}
