using UnityEngine;
using UnityEngine.UI;

namespace WorkshopDemo
{
    internal sealed class WorkshopAbaqusCurveGraphic : MaskableGraphic
    {
        public WorkshopAbaqusResult Result;
        public int Channel;
        public float Cursor;
        public float Value(WorkshopAbaqusFrame f) => Channel == 0 ? f.maxStressMPa : Channel == 1 ? f.maxDisplacementMm : f.maxTemperatureC;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (Result == null) return;
            float lo = float.MaxValue, hi = float.MinValue;
            foreach (var frame in Result.frames) { lo = Mathf.Min(lo, Value(frame)); hi = Mathf.Max(hi, Value(frame)); }
            float pad = Mathf.Max((hi - lo) * .1f, .01f); lo -= pad; hi += pad;
            var rect = rectTransform.rect;
            float minTime = Result.frames[0].time, maxTime = Result.frames[Result.frames.Length - 1].time;
            Vector2 Point(WorkshopAbaqusFrame frame) => new Vector2(
                Mathf.Lerp(rect.xMin + 12, rect.xMax - 12, minTime == maxTime ? .5f : Mathf.InverseLerp(minTime, maxTime, frame.time)),
                Mathf.Lerp(rect.yMin + 12, rect.yMax - 12, Mathf.InverseLerp(lo, hi, Value(frame))));
            for (int i = 0; i < Result.frames.Length; i++)
            {
                Vector2 point = Point(Result.frames[i]);
                if (i > 0) Line(vh, Point(Result.frames[i - 1]), point, 2, color);
                Line(vh, point - Vector2.right * 3, point + Vector2.right * 3, 6, color);
            }
            float x = Point(Result.At(Cursor)).x;
            Line(vh, new Vector2(x, rect.yMin), new Vector2(x, rect.yMax), 1, Color.white);
        }
        static void Line(VertexHelper vh, Vector2 a, Vector2 b, float width, Color tint)
        {
            Vector2 n = (b - a).normalized;
            n = new Vector2(-n.y, n.x) * width * .5f;
            int start = vh.currentVertCount;
            foreach (Vector2 p in new[] { a - n, a + n, b + n, b - n }) vh.AddVert(p, tint, Vector2.zero);
            vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
        }
    }
}
