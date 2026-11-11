using UnityEngine;

namespace WorkshopDemo
{
    // Symbolic effects only. These lines are not a solved stress or displacement field.
    [RequireComponent(typeof(WorkshopDevice))]
    public sealed class WorkshopFaultVisual : MonoBehaviour
    {
        WorkshopDevice device;
        GameObject effects;
        readonly LineRenderer[] lines = new LineRenderer[5];
        Material material;
        Transform sourceModel;
        Vector3 sourcePosition;
        Bounds bounds;
        string code;
        public bool Visible { get; set; } = true;

        void Start()
        {
            device = GetComponent<WorkshopDevice>();
            sourceModel = transform.Find("Source model - " + ModelName(device.kind));
            if (sourceModel) sourcePosition = sourceModel.localPosition;
            var box = GetComponent<BoxCollider>();
            bounds = box ? new Bounds(box.center, box.size) : new Bounds(Vector3.up, Vector3.one * 2);
            effects = new GameObject("Fault effects - schematic");
            effects.transform.SetParent(transform, false);
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            material = new Material(shader);
            for (int i = 0; i < lines.Length; i++)
            {
                var child = new GameObject("Effect " + i);
                child.transform.SetParent(effects.transform, false);
                var line = child.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = false;
                line.widthMultiplier = .026f;
                line.positionCount = 33;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lines[i] = line;
            }
            effects.SetActive(false);
        }

        static string ModelName(DeviceKind kind) => kind == DeviceKind.Motor ? "motor" :
            kind == DeviceKind.Pump ? "water-pump" : kind == DeviceKind.Fan ? "industrial-fan" :
            kind == DeviceKind.Tank ? "" : kind == DeviceKind.Cabinet ? "electrical-cabinet" : "conveyor-belt";

        void LateUpdate()
        {
            if (!effects) return;
            var fault = device.Fault;
            bool active = fault != null && Visible;
            effects.SetActive(active);
            if (sourceModel) sourceModel.localPosition = sourcePosition;
            if (!active) { code = null; return; }
            if (code != fault.Code)
            {
                code = fault.Code;
                Color color = WorkshopFaultCatalog.ColorFor(fault.Visual);
                material.SetColor("_BaseColor", color);
                material.SetColor("_Color", color);
                foreach (var line in lines) { line.startColor = color; line.endColor = color; }
            }
            float clock = Time.time;
            Vector3 center = bounds.center, ext = bounds.extents;
            float radius = Mathf.Max(.35f, Mathf.Min(ext.x, ext.z));
            if (fault.Visual == FaultVisual.Vibration && sourceModel)
                sourceModel.localPosition += new Vector3(Mathf.Sin(clock * 29), 0, Mathf.Cos(clock * 23)) * .025f;
            for (int i = 0; i < lines.Length; i++)
                for (int j = 0; j <= 32; j++)
                {
                    float t = j / 32f, angle = t * Mathf.PI * 2;
                    Vector3 p;
                    switch (fault.Visual)
                    {
                        case FaultVisual.Leak:
                            float fall = Mathf.Repeat(t + clock * .35f + i * .15f, 1);
                            p = center + new Vector3(radius * .6f + fall * .45f, -ext.y * fall, (i - 2) * .07f);
                            if (i == 4) p = new Vector3(center.x + radius, bounds.min.y + .04f,
                                center.z) + new Vector3(Mathf.Cos(angle) * .6f, 0, Mathf.Sin(angle) * .32f);
                            break;
                        case FaultVisual.Heat:
                            p = center + new Vector3((i - 2) * radius * .35f + Mathf.Sin(t * 9 - clock * 3) * .06f,
                                ext.y * .5f + t * .8f, Mathf.Cos(i * 1.7f) * radius * .3f);
                            break;
                        case FaultVisual.Vibration:
                            float ring = radius + .12f + i * .09f + Mathf.Sin(clock * 7 - i) * .04f;
                            p = center + new Vector3(Mathf.Cos(angle) * ring, -.25f + i * .12f,
                                Mathf.Sin(angle) * ring);
                            break;
                        case FaultVisual.Deformation:
                            float pulse = 1 + .045f * Mathf.Sin(clock * 4 + i);
                            p = center + new Vector3(Mathf.Cos(angle) * (ext.x + .08f) * pulse,
                                -ext.y + ext.y * .5f * i, Mathf.Sin(angle) * (ext.z + .08f) * pulse);
                            break;
                        case FaultVisual.Electrical:
                            p = center + new Vector3((i - 2) * radius * .3f + Mathf.Sin(j * 2.3f + Mathf.Floor(clock * 8)) * .075f,
                                (t - .5f) * bounds.size.y * .8f, ext.z + .08f);
                            break;
                        default:
                            p = center + new Vector3((t - .5f) * bounds.size.x,
                                ext.y + .12f + Mathf.Sin(t * Mathf.PI) * .15f, (i - 2) * .13f);
                            break;
                    }
                    lines[i].SetPosition(j, p);
                }
        }

        void OnDisable()
        {
            if (effects) effects.SetActive(false);
            if (sourceModel) sourceModel.localPosition = sourcePosition;
        }

        void OnDestroy() { if (material) Destroy(material); }
    }
}
