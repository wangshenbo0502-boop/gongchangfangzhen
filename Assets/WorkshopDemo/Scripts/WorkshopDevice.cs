using UnityEngine;

namespace WorkshopDemo
{
    public enum DeviceKind { Conveyor, Motor, Pump, Tank, Fan, Cabinet }
    public enum DeviceStatus { Running, Fault, Stopped }

    public sealed class WorkshopDevice : MonoBehaviour
    {
        public string deviceId;
        public string displayName;
        public DeviceKind kind;
        public float nominalTemperature = 40;
        public float nominalRpm = 1450;
        public float nominalVibration = 1.2f;
        public Vector3 inspectionPoint;
        public Transform rotor;
        public Vector3 rotorAxis = Vector3.right;
        public Transform[] movingPackages;
        public Renderer statusLamp;
        public GameObject selectionRing;
        public GameObject faultMarker;
        public TextMesh worldLabel;
        public float conveyorSpan = 4.2f;
        public DeviceStatus Status { get; private set; }
        public float Temperature { get; private set; }
        public float Rpm { get; private set; }
        public float Vibration { get; private set; }
        public bool HasRpm => kind != DeviceKind.Tank && kind != DeviceKind.Cabinet;
        public bool HasVibration => kind != DeviceKind.Tank && kind != DeviceKind.Cabinet;
        public WorkshopFault Fault { get; private set; }
        public WorkshopFaultInstance FaultInstance { get; private set; }
        public WorkshopFaultSnapshot Snapshot { get; private set; }
        public WorkshopAbaqusResult AbaqusResult { get; private set; }
        public string ResultSource { get; private set; } = "教学故障模型";
        public float FaultAge { get; private set; }
        internal float FaultElapsed => FaultAge;
        internal float SampledAtTime { get; private set; }
        public float LoadPercent { get; private set; }
        public float Pressure { get; private set; }
        public float Flow { get; private set; }
        public float Level { get; private set; }
        public float Voltage { get; private set; }
        public float Communication { get; private set; }
        public bool IsRunning => Status == DeviceStatus.Running ||
            (Status == DeviceStatus.Fault && Fault != null && !(FaultInstance?.ProtectionTriggered ?? Fault.Trips) && runningBeforeFault);
        internal bool ExamActive { get; set; }
        public string StatusText => Status == DeviceStatus.Fault ? ExamActive ? "异常 · 待诊断" : Fault?.Name ?? "设备故障" :
            Status == DeviceStatus.Stopped ? "已停机" : "运行正常";
        public string KindText => new[] { "辊道输送机", "三相异步电机", "离心循环泵", "工艺储罐", "轴流风机", "PLC 控制柜" }[(int)kind];
        MaterialPropertyBlock propertyBlock;
        float phase;
        Vector3[] initialPackages;
        Quaternion initialRotor;
        Renderer[] bodyRenderers;
        bool runningBeforeFault;
        float businessTime;

        void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            bodyRenderers = System.Array.FindAll(GetComponentsInChildren<MeshRenderer>(true),
                renderer => renderer.name.Contains("_body") || renderer.name.Contains("_rotor"));
            phase = transform.GetSiblingIndex() * 1.37f;
            if (rotor) initialRotor = rotor.localRotation;
            if (movingPackages != null)
            {
                initialPackages = new Vector3[movingPackages.Length];
                for (int i = 0; i < movingPackages.Length; i++)
                    initialPackages[i] = movingPackages[i].localPosition;
            }
            ResetState();
            if (!GetComponent<WorkshopFaultVisual>()) gameObject.AddComponent<WorkshopFaultVisual>();
        }

        void Start()
        {
            WorkshopEquipmentModel.Attach(this);
            if (rotor) initialRotor = rotor.localRotation;
        }

        public void ResetState()
        {
            Status = DeviceStatus.Running;
            Fault = null;
            FaultInstance = null;
            ExamActive = false;
            businessTime = 0;
            AbaqusResult = null;
            ResultSource = "教学故障模型";
            FaultAge = 0;
            if (rotor) rotor.localRotation = initialRotor;
            if (initialPackages != null)
                for (int i = 0; i < initialPackages.Length; i++)
                    movingPackages[i].localPosition = initialPackages[i];
            Sample(0);
            SetSelected(false);
            RefreshVisual();
        }

        public void Sample(float elapsed)
        {
            if (float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < 0)
                throw new System.ArgumentOutOfRangeException(nameof(elapsed));
            bool newSample = businessTime != elapsed || FaultInstance?.Version == 0;
            businessTime = elapsed;
            SampledAtTime = Time.time;
            float wave = Mathf.Sin(elapsed * .23f + phase);
            FaultAge = FaultInstance == null ? 0 : Mathf.Max(0, elapsed - FaultInstance.StartedAt);
            float progress = FaultInstance?.Injection == WorkshopFaultInjection.Immediate ? 1 : Mathf.Clamp01(FaultAge / 18);
            if (FaultInstance != null)
            {
                FaultInstance.ProtectionTriggered = Fault.Trips &&
                    (FaultInstance.Injection == WorkshopFaultInjection.Immediate || progress >= ProtectionProgress(Fault.Code));
                FaultInstance.Stage = FaultInstance.ProtectionTriggered ? "protected" : progress == 0 ? "onset" : progress < .25f ? "onset" :
                    progress < .7f ? "developing" : FaultInstance.ProtectionTriggered ? "protected" : "established";
                if (newSample) FaultInstance.Version++;
            }
            Temperature = Fault != null ? Fault.Temperature + wave * .6f : nominalTemperature + wave * 1.1f;
            Rpm = IsRunning ? nominalRpm * (Fault?.SpeedRatio ?? 1) + Mathf.Sin(elapsed * .8f + phase) * 4 : 0;
            Vibration = IsRunning ? (Fault?.Vibration ?? nominalVibration) + wave * .08f : 0;
            LoadPercent = Fault != null ? Fault.ProcessRatio * 65 : IsRunning ? 65 + wave * 2 : 0;
            Pressure = IsRunning ? .32f * (Fault?.ProcessRatio ?? 1) + wave * .004f : 0;
            Flow = IsRunning ? 18 * (Fault?.ProcessRatio ?? 1) + wave * .2f : 0;
            Level = Fault?.Code == "TK-LEAK" ? TeachingTankLeakLevel(FaultAge) :
                Fault?.Code == "TK-LEVEL" ? 96 + wave : 65 + wave;
            if (kind == DeviceKind.Tank) Pressure = .45f * (Fault?.ProcessRatio ?? 1) + wave * .003f;
            Voltage = Fault?.Code == "PLC-LOW" ? 17.3f : 24 + wave * .05f;
            Communication = Fault?.Code == "PLC-COMM" ? 0 : 100;
            if (Fault?.Code == "P-CAV" && IsRunning)
            {
                float activity = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.10f, .93f, progress));
                float fluctuation = Mathf.Sin(FaultAge * 12) * .7f + Mathf.Sin(FaultAge * 29) * .3f;
                Pressure = .32f - activity * (.148f - fluctuation * .032f);
                Flow = 18 - activity * (9 - fluctuation * 1.75f);
            }
            if (FaultInstance?.Injection == WorkshopFaultInjection.Progressive)
            {
                float heating = Mathf.SmoothStep(0, 1, progress / .7f);
                Temperature = Mathf.Lerp(nominalTemperature, Fault.Temperature, heating);
                if (IsRunning)
                {
                    Rpm = nominalRpm * Mathf.Lerp(1, Mathf.Max(.25f, Fault.SpeedRatio), heating);
                    Vibration = Mathf.Lerp(nominalVibration, Fault.Vibration, heating);
                    LoadPercent = 65 * Mathf.Lerp(1, Fault.ProcessRatio, heating);
                    if (Fault.Code != "P-CAV")
                    {
                        Pressure = (kind == DeviceKind.Tank ? .45f : .32f) * Mathf.Lerp(1, Fault.ProcessRatio, heating);
                        Flow = 18 * Mathf.Lerp(1, Fault.ProcessRatio, heating);
                    }
                }
                if (Fault.Code == "TK-LEVEL") Level = Mathf.Lerp(65, 96, heating);
                if (Fault.Code == "PLC-LOW") Voltage = Mathf.Lerp(24, 17.3f, heating);
                if (Fault.Code == "PLC-COMM") Communication = Mathf.Lerp(100, 0, heating);
            }
            if (Fault != null && Fault.Trips && !IsRunning)
            {
                float tripAt = FaultInstance.Injection == WorkshopFaultInjection.Immediate ? 0 : 18 * ProtectionProgress(Fault.Code);
                float atTrip = !runningBeforeFault ? nominalTemperature :
                    FaultInstance.Injection == WorkshopFaultInjection.Immediate ? Fault.Temperature :
                    Mathf.Lerp(nominalTemperature, Fault.Temperature, Mathf.SmoothStep(0, 1, tripAt / 12.6f));
                Temperature = 25 + (atTrip - 25) * Mathf.Exp(-Mathf.Max(0, FaultAge - tripAt) / 45);
            }
            if (Fault?.Code == "PLC-CONTACT")
            {
                float heat = Mathf.SmoothStep(0, 1, progress / .7f);
                float tripAt = FaultInstance.Injection == WorkshopFaultInjection.Immediate ? 0 : 12.6f;
                Temperature = !runningBeforeFault ? 25 : IsRunning ? Mathf.Lerp(25, Fault.Temperature, heat) :
                    25 + (Fault.Temperature - 25) * Mathf.Exp(-Mathf.Max(0, FaultAge - tripAt) / 45);
            }
            Snapshot = new WorkshopFaultSnapshot(this, elapsed, FaultInstance?.Stage ?? "normal");
        }

        internal static float ProtectionProgress(string code) => code == "P-SEAL" ? .6f : code == "F-CRACK" ? .8f : .7f;

        internal static float TeachingTankLeakLevel(float seconds) => Mathf.Max(10, 65 - seconds * 1.5f);

        public void SetFault(bool value)
        {
            if (value) { ApplyFault(WorkshopFaultCatalog.For(kind)[0]); return; }
            if (Fault == null) return;
            Status = runningBeforeFault ? DeviceStatus.Running : DeviceStatus.Stopped;
            Fault = null;
            FaultInstance = null;
            ExamActive = false;
            AbaqusResult = null;
            ResultSource = "教学故障模型";
            FaultAge = 0;
            Sample(businessTime);
            RefreshVisual();
        }

        public bool ApplyFault(WorkshopFault fault) => ApplyFault(fault, businessTime, WorkshopFaultInjection.Immediate);

        internal bool ApplyFault(WorkshopFault fault, float time, WorkshopFaultInjection injection)
        {
            if (fault == null || fault.Kind != kind || Fault != null) return false;
            runningBeforeFault = IsRunning;
            Fault = fault;
            FaultInstance = new WorkshopFaultInstance(fault, time, injection);
            Status = DeviceStatus.Fault;
            AbaqusResult = WorkshopAbaqusResult.Load(deviceId, fault.Code, out string source);
            ResultSource = source;
            Sample(time);
            RefreshVisual();
            return true;
        }

        public void ToggleOperation()
        {
            if (Status == DeviceStatus.Fault) return;
            Status = IsRunning ? DeviceStatus.Stopped : DeviceStatus.Running;
            Sample(businessTime);
            RefreshVisual();
        }

        public void SetSelected(bool selected)
        {
            if (selectionRing) selectionRing.SetActive(selected);
        }

        void RefreshVisual()
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
            Color bodyColor = kind == DeviceKind.Pump ? new Color(.10f, .39f, .49f) :
                kind == DeviceKind.Fan ? new Color(.72f, .53f, .16f) :
                kind == DeviceKind.Cabinet ? new Color(.20f, .39f, .31f) :
                kind == DeviceKind.Tank ? new Color(.45f, .55f, .58f) : new Color(.23f, .28f, .30f);
            if (Fault != null) bodyColor = Color.Lerp(bodyColor, WorkshopFaultCatalog.ColorFor(Fault.Visual), .6f);
            else if (Status == DeviceStatus.Stopped) bodyColor = new Color(.32f, .35f, .36f);
            if (bodyRenderers != null)
                foreach (var renderer in bodyRenderers)
                {
                    if (!renderer) continue;
                    renderer.GetPropertyBlock(propertyBlock);
                    propertyBlock.SetColor("_BaseColor", bodyColor);
                    propertyBlock.SetColor("_Color", bodyColor);
                    propertyBlock.SetColor("_EmissionColor", Color.black);
                    propertyBlock.SetFloat("_Metallic", .25f);
                    propertyBlock.SetFloat("_Smoothness", .3f);
                    renderer.SetPropertyBlock(propertyBlock);
                }
            Color color = Status == DeviceStatus.Fault ? new Color(1, .12f, .06f) :
                IsRunning ? new Color(.12f, .94f, .56f) : new Color(.6f, .64f, .66f);
            if (statusLamp)
            {
                statusLamp.GetPropertyBlock(propertyBlock);
                propertyBlock.SetColor("_BaseColor", color);
                propertyBlock.SetColor("_EmissionColor", color * 2);
                statusLamp.SetPropertyBlock(propertyBlock);
            }
            if (faultMarker) faultMarker.SetActive(Status == DeviceStatus.Fault);
            if (faultMarker && Fault != null)
            {
                var text = faultMarker.GetComponent<TextMesh>();
                if (text) { text.text = ExamActive ? "CHECK" : Fault.Code; text.color = WorkshopFaultCatalog.ColorFor(Fault.Visual); }
            }
            if (worldLabel) worldLabel.text = deviceId + "  " + (Status == DeviceStatus.Fault ? "! FAULT" :
                IsRunning ? "RUN" : "STOP");
        }

        void Update()
        {
            if (IsRunning)
            {
                if (rotor) rotor.Rotate(rotorAxis, (Fault?.Code == "F-REVERSE" ? -1 : 1) *
                    (Fault?.Code == "F-IMBAL" ? Rpm * 6 * WorkshopFaultProfile.ImbalanceRotationScale :
                        Mathf.Min(Rpm * .13f, 260)) * Time.deltaTime, Space.Self);
                if (movingPackages != null)
                    for (int i = 0; i < movingPackages.Length; i++)
                    {
                        var p = movingPackages[i].localPosition;
                        p.x = Mathf.Repeat(p.x + Time.deltaTime * .7f + conveyorSpan * .5f, conveyorSpan) - conveyorSpan * .5f;
                        movingPackages[i].localPosition = p;
                        if (Fault != null)
                        {
                            p.x -= Time.deltaTime * .7f * (1 - Fault.SpeedRatio);
                            if (Fault.Code == "CV-TRACK") p.z = initialPackages[i].z + Mathf.Sin(Time.time * 3) * .1f;
                            movingPackages[i].localPosition = p;
                        }
                    }
            }
            if (worldLabel && WorkshopCamera.Main)
                worldLabel.transform.rotation = WorkshopCamera.Main.transform.rotation;
            if (faultMarker && faultMarker.activeSelf)
            {
                if (WorkshopCamera.Main) faultMarker.transform.rotation = WorkshopCamera.Main.transform.rotation;
                faultMarker.transform.localScale = Vector3.one * (1 + .08f * Mathf.Sin(Time.time * 5));
            }
        }
    }
}
