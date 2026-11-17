using System;
using UnityEngine;

namespace WorkshopDemo
{
    public enum WorkshopFaultInjection { Immediate, Progressive }

    public sealed class WorkshopFaultInstance
    {
        public string Id { get; } = Guid.NewGuid().ToString("N");
        public WorkshopFault Definition { get; }
        public float StartedAt { get; }
        public WorkshopFaultInjection Injection { get; }
        public int Seed { get; }
        public bool ProtectionTriggered { get; internal set; }
        public int Version { get; internal set; }
        public string Stage { get; internal set; }
        public FaultSeverity Severity => Definition.Severity;

        internal WorkshopFaultInstance(WorkshopFault fault, float startedAt, WorkshopFaultInjection injection)
        {
            Definition = fault;
            StartedAt = startedAt;
            Injection = injection;
            // Stable across platforms, independent of randomized string.GetHashCode().
            int seed = 481;
            foreach (char c in fault.Code) seed = unchecked(seed * 31 + c);
            Seed = seed;
            ProtectionTriggered = fault.Trips && injection == WorkshopFaultInjection.Immediate;
        }
    }

    public sealed class WorkshopFaultSnapshot
    {
        public readonly string InstanceId, Stage, Source;
        public readonly int Version;
        public readonly float BusinessTime, Age, Progress;
        public readonly bool Running, ProtectionTriggered;
        public readonly float Temperature, Rpm, Vibration, Pressure, Flow, Level, Voltage, Communication, Load;

        internal WorkshopFaultSnapshot(WorkshopDevice d, float businessTime, string stage)
        {
            InstanceId = d.FaultInstance?.Id;
            Version = d.FaultInstance?.Version ?? 0;
            Stage = stage;
            Source = "本地教学模型";
            BusinessTime = businessTime;
            Age = d.FaultAge;
            Progress = d.FaultInstance?.Injection == WorkshopFaultInjection.Immediate ? 1 : Mathf.Clamp01(Age / 18);
            Running = d.IsRunning;
            ProtectionTriggered = d.FaultInstance?.ProtectionTriggered ?? false;
            Temperature = d.Temperature; Rpm = d.Rpm; Vibration = d.Vibration;
            Pressure = d.Pressure; Flow = d.Flow; Level = d.Level;
            Voltage = d.Voltage; Communication = d.Communication; Load = d.LoadPercent;
        }

        public bool Available(string key, DeviceKind kind) => key switch {
            "rpm" or "vibration" => kind != DeviceKind.Tank && kind != DeviceKind.Cabinet,
            "pressure" or "flow" => kind == DeviceKind.Pump || kind == DeviceKind.Tank,
            "level" => kind == DeviceKind.Tank,
            "voltage" or "communication" => kind == DeviceKind.Cabinet,
            "temperature" or "load" => true,
            _ => false
        };
    }
}
