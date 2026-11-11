using UnityEngine;
using UnityEngine.UI;

namespace WorkshopDemo
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkshopDashboardGraphic : Graphic
    {
        readonly int[] counts = new int[3];
        readonly Color32[] tints = {
            new Color32(66, 232, 222, 255),
            new Color32(255, 109, 133, 255),
            new Color32(103, 120, 126, 255)
        };
        float nextFrame;

        void Update()
        {
            if (Time.unscaledTime < nextFrame) return;
            nextFrame = Time.unscaledTime + .05f;
            SetVerticesDirty();
        }

        public void SetCounts(int running, int faults, int stopped)
        {
            if (counts[0] == running && counts[1] == faults && counts[2] == stopped) return;
            counts[0] = running; counts[1] = faults; counts[2] = stopped;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            var rect = rectTransform.rect;
            float radius = Mathf.Min(rect.width, rect.height) * .39f;
            for (int i = 0; i < 60; i++)
            {
                float angle = i * Mathf.PI / 30;
                Vector2 direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                WorkshopHudGraphic.Line(helper, rect.center + direction * (radius + 10),
                    rect.center + direction * (radius + (i % 5 == 0 ? 16 : 13)),
                    new Color(.25f, .68f, .69f, i % 5 == 0 ? .8f : .28f), 1);
            }
            WorkshopHudGraphic.Arc(helper, rect.center, radius - 12, 0, 360, new Color(.15f, .4f, .42f, .35f), 1);
            float phase = Time.unscaledTime * 12;
            WorkshopHudGraphic.Arc(helper, rect.center, radius + 19, phase, phase + 38,
                new Color(.26f, .91f, .87f, .75f), 2);
            WorkshopHudGraphic.Arc(helper, rect.center, radius + 19, phase + 180, phase + 218,
                new Color(.26f, .91f, .87f, .3f), 2);
            int total = counts[0] + counts[1] + counts[2];
            float start = 0;
            for (int category = 0; category < counts.Length; category++)
            {
                float fraction = total > 0 ? (float)counts[category] / total : category == 2 ? 1 : 0;
                if (fraction <= 0) continue;
                int steps = Mathf.Max(2, Mathf.CeilToInt(fraction * 100));
                float gap = fraction < .999f ? .025f : 0;
                for (int i = 0; i < steps; i++)
                {
                    float from = start + gap + (fraction * Mathf.PI * 2 - gap * 2) * i / steps + .004f;
                    float to = start + gap + (fraction * Mathf.PI * 2 - gap * 2) * (i + 1) / steps - .004f;
                    Vector2 Direction(float angle) => new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                    int index = helper.currentVertCount;
                    helper.AddVert(rect.center + Direction(from) * radius, tints[category], Vector2.zero);
                    helper.AddVert(rect.center + Direction(to) * radius, tints[category], Vector2.zero);
                    helper.AddVert(rect.center + Direction(to) * (radius - 6), tints[category], Vector2.zero);
                    helper.AddVert(rect.center + Direction(from) * (radius - 6), tints[category], Vector2.zero);
                    helper.AddTriangle(index, index + 1, index + 2);
                    helper.AddTriangle(index, index + 2, index + 3);
                }
                start += fraction * Mathf.PI * 2;
            }
        }
    }
}
