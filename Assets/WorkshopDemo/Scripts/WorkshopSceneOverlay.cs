using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WorkshopDemo
{
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkshopSceneOverlay : Graphic
    {
        public WorkshopSimulation simulation;
        public Font font;
        readonly List<RectTransform> tags = new List<RectTransform>();
        readonly List<Text> labels = new List<Text>();
        readonly List<Vector2> targets = new List<Vector2>();
        readonly List<Rect> placed = new List<Rect>();
        readonly List<Image> fills = new List<Image>();
        readonly List<Renderer> worldLabels = new List<Renderer>();
        readonly List<bool> worldLabelStates = new List<bool>();
        readonly Color cyan = new Color32(66, 232, 222, 255);

        public void Build()
        {
            raycastTarget = false;
            foreach (var device in simulation.devices)
            {
                if (device.worldLabel && device.worldLabel.TryGetComponent<Renderer>(out var renderer))
                {
                    worldLabels.Add(renderer);
                    worldLabelStates.Add(renderer.enabled);
                    renderer.enabled = false;
                }
                var root = new GameObject("Track " + device.deviceId, typeof(RectTransform),
                    typeof(Image), typeof(Button));
                var rect = (RectTransform)root.transform;
                rect.SetParent(transform, false);
                rect.anchorMin = rect.anchorMax = Vector2.zero;
                rect.pivot = Vector2.zero;
                rect.sizeDelta = new Vector2(88, 28);
                var image = root.GetComponent<Image>();
                image.color = new Color(.025f, .09f, .10f, .90f);
                fills.Add(image);
                var button = root.GetComponent<Button>();
                button.targetGraphic = image;
                button.onClick.AddListener(() => simulation.Select(device, false));
                var child = new GameObject("Device identity", typeof(RectTransform), typeof(Text));
                var labelRect = (RectTransform)child.transform;
                labelRect.SetParent(rect, false);
                labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = new Vector2(7, 0); labelRect.offsetMax = new Vector2(-7, 0);
                var text = child.GetComponent<Text>();
                text.font = font; text.fontSize = 12; text.color = cyan;
                text.alignment = TextAnchor.MiddleCenter;
                text.raycastTarget = false;
                labels.Add(text);
                tags.Add(rect);
                targets.Add(Vector2.zero);
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            foreach (var label in worldLabels) if (label) label.enabled = false;
        }

        protected override void OnDisable()
        {
            for (int i = 0; i < worldLabels.Count; i++)
                if (worldLabels[i]) worldLabels[i].enabled = worldLabelStates[i];
            base.OnDisable();
        }

        void LateUpdate()
        {
            if (!simulation || !simulation.cameraRig || tags.Count != simulation.devices.Length) return;
            Camera camera = simulation.cameraRig.GetComponent<Camera>();
            placed.Clear();
            float width = rectTransform.rect.width, height = rectTransform.rect.height;
            for (int i = 0; i < tags.Count; i++)
            {
                var device = simulation.devices[i];
                var bounds = device.GetComponent<Collider>().bounds;
                Vector3 screen = camera.WorldToScreenPoint(bounds.center + Vector3.up * .15f);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, null, out var local);
                Vector2 target = local - rectTransform.rect.min;
                targets[i] = local;
                bool visible = screen.z > 0 && target.x > 18 && target.x < width - 18 &&
                    target.y > 12 && target.y < height - 12;
                tags[i].gameObject.SetActive(visible);
                if (!visible) continue;
                bool selected = device == simulation.Selected;
                tags[i].sizeDelta = new Vector2(selected ? 124 : 74, selected ? 44 : 26);
                Vector2 size = tags[i].sizeDelta;
                var position = new Vector2(Mathf.Clamp(target.x - size.x * .5f, 16, width - size.x - 16),
                    Mathf.Clamp(target.y + 24, 12, height - 12 - size.y));
                Rect area = new Rect(position, size);
                for (int pass = 0; pass < 8; pass++)
                {
                    bool collision = false;
                    foreach (Rect other in placed)
                        if (area.Overlaps(other)) { collision = true; break; }
                    if (!collision) break;
                    area.y += size.y + 5;
                    if (area.yMax > height - 12) { area.y = Mathf.Max(12, target.y - size.y - 30); area.x += 32; }
                    area.x = Mathf.Clamp(area.x, 16, width - size.x - 16);
                }
                placed.Add(area);
                tags[i].anchoredPosition = area.position;
                labels[i].text = device.deviceId + (selected ? "\n" + device.Temperature.ToString("F1") + " °C" : "");
                labels[i].color = device.Fault != null ?
                    device.Fault.Severity == FaultSeverity.Critical ? new Color32(255, 109, 133, 255) :
                    new Color32(255, 191, 92, 255) :
                    selected ? Color.white : cyan;
                fills[i].color = selected ? new Color(.025f, .17f, .18f, .96f) : new Color(.025f, .07f, .08f, .88f);
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            for (int i = 0; i < tags.Count; i++)
            {
                if (!tags[i].gameObject.activeSelf) continue;
                var tint = labels[i].color;
                tint.a = .65f;
                Vector2 start = targets[i];
                Vector2 end = rectTransform.rect.min + tags[i].anchoredPosition +
                    new Vector2(tags[i].rect.width * .5f, 0);
                Vector2 elbow = end - Vector2.up * 9;
                WorkshopHudGraphic.Line(mesh, start, elbow, tint, 1);
                WorkshopHudGraphic.Line(mesh, elbow, end, tint, 1);
                WorkshopHudGraphic.Arc(mesh, start, 4, 0, 360, tint, 1.5f);
                Vector2 top = end + new Vector2(-tags[i].rect.width * .5f, tags[i].rect.height);
                WorkshopHudGraphic.Line(mesh, top, top + Vector2.right * tags[i].rect.width, tint, 1);
                if (simulation.devices[i] == simulation.Selected)
                {
                    float phase = Time.unscaledTime * 26;
                    WorkshopHudGraphic.Arc(mesh, start, 14, phase, phase + 80, tint, 1.5f);
                    WorkshopHudGraphic.Arc(mesh, start, 14, phase + 180, phase + 260, tint, 1.5f);
                    DrawSelection(mesh, simulation.devices[i], tint);
                }
            }
        }

        void DrawSelection(VertexHelper mesh, WorkshopDevice device, Color tint)
        {
            var bounds = device.GetComponent<Collider>().bounds;
            var camera = simulation.cameraRig.GetComponent<Camera>();
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 world = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                Vector3 screen = camera.WorldToScreenPoint(world);
                if (screen.z <= 0) return;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, screen, null, out var point);
                min = Vector2.Min(min, point); max = Vector2.Max(max, point);
            }
            var r = rectTransform.rect;
            min = Vector2.Max(min - Vector2.one * 9, new Vector2(r.xMin + 14, r.yMin + 14));
            max = Vector2.Min(max + Vector2.one * 9, new Vector2(r.xMax - 14, r.yMax - 14));
            if (max.x <= min.x || max.y <= min.y) return;
            float size = Mathf.Min(16, Mathf.Min(max.x - min.x, max.y - min.y) * .25f);
            for (int i = 0; i < 4; i++)
            {
                var at = new Vector2(i % 2 == 0 ? min.x : max.x, i < 2 ? min.y : max.y);
                WorkshopHudGraphic.Line(mesh, at, at + Vector2.right * size * (i % 2 == 0 ? 1 : -1), tint, 1.5f);
                WorkshopHudGraphic.Line(mesh, at, at + Vector2.up * size * (i < 2 ? 1 : -1), tint, 1.5f);
            }
            tint.a = .24f;
            float scan = Mathf.Lerp(min.y, max.y, Mathf.PingPong(Time.unscaledTime * .3f, 1));
            WorkshopHudGraphic.Line(mesh, new Vector2(min.x, scan), new Vector2(max.x, scan), tint, 1);
        }
    }
}
