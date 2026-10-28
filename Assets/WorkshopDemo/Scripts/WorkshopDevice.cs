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
        public bool IsRunning => Status == DeviceStatus.Running;
        public string StatusText => Status == DeviceStatus.Fault ? "超温故障" :
            Status == DeviceStatus.Stopped ? "已停机" : "运行正常";
        public string KindText => new[] { "辊道输送机", "三相异步电机", "离心循环泵", "工艺储罐", "轴流风机", "PLC 控制柜" }[(int)kind];
        MaterialPropertyBlock propertyBlock;
        float phase;
        Vector3[] initialPackages;
        Quaternion initialRotor;

        void Awake()
        {
            propertyBlock = new MaterialPropertyBlock();
            phase = transform.GetSiblingIndex() * 1.37f;
            if (rotor) initialRotor = rotor.localRotation;
            if (movingPackages != null)
            {
                initialPackages = new Vector3[movingPackages.Length];
                for (int i = 0; i < movingPackages.Length; i++)
                    initialPackages[i] = movingPackages[i].localPosition;
            }
            ResetState();
        }

        public void ResetState()
        {
            Status = DeviceStatus.Running;
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
            float wave = Mathf.Sin(elapsed * .23f + phase);
            Temperature = Status == DeviceStatus.Fault ? 92 + wave * .6f : nominalTemperature + wave * 1.1f;
            Rpm = IsRunning ? nominalRpm + Mathf.Sin(elapsed * .8f + phase) * 4 : 0;
            Vibration = Status == DeviceStatus.Fault ? 5.8f : IsRunning ? nominalVibration + wave * .08f : 0;
        }

        public void SetFault(bool value)
        {
            Status = value ? DeviceStatus.Fault : DeviceStatus.Running;
            Sample(Time.time);
            RefreshVisual();
        }

        public void ToggleOperation()
        {
            if (Status == DeviceStatus.Fault) return;
            Status = IsRunning ? DeviceStatus.Stopped : DeviceStatus.Running;
            Sample(Time.time);
            RefreshVisual();
        }

        public void SetSelected(bool selected)
        {
            if (selectionRing) selectionRing.SetActive(selected);
        }

        void RefreshVisual()
        {
            if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
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
            if (worldLabel) worldLabel.text = deviceId + "  " + (Status == DeviceStatus.Fault ? "! FAULT" :
                IsRunning ? "RUN" : "STOP");
        }

        void Update()
        {
            if (IsRunning)
            {
                if (rotor) rotor.Rotate(rotorAxis, Mathf.Min(Rpm * .13f, 260) * Time.deltaTime, Space.Self);
                if (movingPackages != null)
                    for (int i = 0; i < movingPackages.Length; i++)
                    {
                        var p = movingPackages[i].localPosition;
                        p.x = Mathf.Repeat(p.x + Time.deltaTime * .7f + conveyorSpan * .5f, conveyorSpan) - conveyorSpan * .5f;
                        movingPackages[i].localPosition = p;
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
