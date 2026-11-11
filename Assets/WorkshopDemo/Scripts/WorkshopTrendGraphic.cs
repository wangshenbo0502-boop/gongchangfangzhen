using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WorkshopDemo
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkshopTrendGraphic : Graphic
    {
        readonly List<float> samples = new List<float>();
        public int Capacity = 120;
        public float Minimum = 20;
        public float Maximum = 100;

        public void Sample(float value)
        {
            samples.Add(value);
            if (samples.Count > Capacity) samples.RemoveAt(0);
            SetVerticesDirty();
        }

        public void Clear() { samples.Clear(); SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            var area = rectTransform.rect;
            for (int i = 0; i < 4; i++)
            {
                float y = area.yMin + area.height * i / 3;
                Line(helper, new Vector2(area.xMin, y), new Vector2(area.xMax, y),
                    new Color(.4f, .6f, .6f, .35f), 1);
            }
            for (int i = 1; i < samples.Count; i++)
            {
                Vector2 Point(int index) => new Vector2(
                    area.xMin + area.width * index / Mathf.Max(1, samples.Count - 1),
                    area.yMin + area.height * Mathf.InverseLerp(Minimum, Maximum, samples[index]));
                var from = Point(i - 1);
                var to = Point(i);
                int index = helper.currentVertCount;
                var fill = new Color(color.r, color.g, color.b, .10f);
                helper.AddVert(new Vector2(from.x, area.yMin), fill, Vector2.zero);
                helper.AddVert(from, fill, Vector2.zero);
                helper.AddVert(to, fill, Vector2.zero);
                helper.AddVert(new Vector2(to.x, area.yMin), fill, Vector2.zero);
                helper.AddTriangle(index, index + 1, index + 2);
                helper.AddTriangle(index, index + 2, index + 3);
                Line(helper, from, to, color, 2);
            }
        }

        static void Line(VertexHelper helper, Vector2 from, Vector2 to, Color tint, float width)
        {
            var normal = new Vector2(-(to - from).y, (to - from).x).normalized * width * .5f;
            int index = helper.currentVertCount;
            helper.AddVert(from - normal, tint, Vector2.zero);
            helper.AddVert(from + normal, tint, Vector2.zero);
            helper.AddVert(to + normal, tint, Vector2.zero);
            helper.AddVert(to - normal, tint, Vector2.zero);
            helper.AddTriangle(index, index + 1, index + 2);
            helper.AddTriangle(index, index + 2, index + 3);
        }
    }
}
