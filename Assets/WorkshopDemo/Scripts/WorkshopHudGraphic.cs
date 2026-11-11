using UnityEngine;
using UnityEngine.UI;

namespace WorkshopDemo
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkshopHudGraphic : Graphic
    {
        public enum Shape { Panel = 0, Header = 1, Viewport = 2, Gauge = 3, Icon = 5, Pipeline = 6 }
        public Shape shape;
        public string symbol;
        public float Value;
        public int Stage;
        readonly Color aqua = new Color32(66, 232, 222, 255);
        readonly Color dim = new Color32(55, 96, 100, 255);
        readonly Color warning = new Color32(255, 191, 92, 255);
        float nextFrame;

        void Update()
        {
            if (shape != Shape.Header && shape != Shape.Pipeline) return;
            if (Time.unscaledTime < nextFrame) return;
            nextFrame = Time.unscaledTime + .05f;
            SetVerticesDirty();
        }

        public void SetValue(float value)
        {
            if (Mathf.Approximately(Value, value)) return;
            Value = value;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = rectTransform.rect;
            Vector2 P(float x, float y) => new Vector2(r.xMin + x, r.yMax - y);
            void L(float x, float y, float xx, float yy, Color tint, float width = 1) =>
                Line(mesh, P(x, y), P(xx, yy), tint, width);
            float w = r.width, h = r.height;
            switch (shape)
            {
                case Shape.Panel:
                    var points = new[] { P(0, 12), P(12, 0), P(w - 30, 0), P(w, 0),
                        P(w, h - 12), P(w - 12, h), P(0, h) };
                    Polygon(mesh, points, color);
                    for (int i = 0; i < points.Length; i++)
                        Line(mesh, points[i], points[(i + 1) % points.Length], Alpha(aqua, .18f), 1);
                    L(0, 36, 0, 12, aqua, 2);
                    L(0, 12, 12, 0, aqua, 2);
                    L(12, 0, 64, 0, aqua, 2);
                    L(w - 44, h, w - 12, h, Alpha(aqua, .6f), 2);
                    L(w - 12, h, w, h - 12, Alpha(aqua, .6f), 2);
                    break;
                case Shape.Header:
                    float c = w * .5f;
                    Polygon(mesh, new[] { P(c - 340, 1), P(c + 340, 1), P(c + 294, 74),
                        P(c - 294, 74) }, new Color(.035f, .095f, .105f, 1));
                    L(20, 28, c - 374, 28, dim);
                    L(c - 374, 28, c - 330, 80, aqua, 1.5f);
                    L(c - 330, 80, c + 330, 80, Alpha(aqua, .65f), 1.5f);
                    L(c + 330, 80, c + 374, 28, aqua, 1.5f);
                    L(c + 374, 28, w - 20, 28, dim);
                    L(20, 90, c - 368, 90, Alpha(aqua, .25f));
                    L(c + 368, 90, w - 20, 90, Alpha(aqua, .25f));
                    for (int i = 0; i < 6; i++)
                    {
                        float a = .15f + .5f * Mathf.Pow(Mathf.Max(0,
                            Mathf.Sin(Time.unscaledTime * 1.4f - i * .5f)), 4);
                        L(c - 360 - i * 9, 65, c - 346 - i * 9, 81, Alpha(aqua, a), 3);
                        L(c + 360 + i * 9, 65, c + 346 + i * 9, 81, Alpha(aqua, a), 3);
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        L(c - 58 + i * 43, 88, c - 28 + i * 43, 88, i == 1 ? aqua : dim, 2);
                    }
                    break;
                case Shape.Viewport:
                    Fade(mesh, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin + 100),
                        new Color(.01f, .045f, .05f, .55f), Color.clear);
                    Fade(mesh, new Vector2(r.xMin, r.yMax - 180), new Vector2(r.xMax, r.yMax - 120),
                        Color.clear, new Color(.01f, .045f, .05f, .35f));
                    float length = 36;
                    for (int i = 0; i < 4; i++)
                    {
                        float x = i % 2 == 0 ? 1 : w - 1;
                        float y = i < 2 ? 1 : h - 1;
                        float sx = i % 2 == 0 ? 1 : -1, sy = i < 2 ? 1 : -1;
                        L(x, y, x + length * sx, y, Alpha(aqua, .75f), 2);
                        L(x, y, x, y + length * sy, Alpha(aqua, .75f), 2);
                    }
                    for (int i = 1; i <= 7; i++)
                    {
                        float y = 110 + (h - 168) * i / 8;
                        L(1, y, i % 2 == 0 ? 9 : 5, y, Alpha(aqua, .45f));
                        L(w - 1, y, w - (i % 2 == 0 ? 9 : 5), y, Alpha(aqua, .45f));
                    }
                    L(w * .5f - 24, h - 7, w * .5f + 24, h - 7, Alpha(aqua, .5f), 2);
                    break;
                case Shape.Gauge:
                    for (int i = 0; i < 32; i++)
                    {
                        float x = i * w / 32;
                        bool filled = i < Mathf.Clamp01((Value - 20) / 100) * 32;
                        Color tint = filled ? Value >= 85 ? warning : aqua : Alpha(dim, .5f);
                        Quad(mesh, P(x, 0), P(x + w / 32 - 3, h), tint);
                    }
                    break;
                case Shape.Pipeline:
                    for (int i = 0; i < 4; i++)
                    {
                        float x = 14 + (w - 28) * i / 3;
                        var tint = i <= Stage ? aqua : dim;
                        Arc(mesh, P(x, h * .5f), 4, 0, 360, tint, i == Stage ? 3 : 1.5f);
                        if (i < 3)
                            L(x + 9, h * .5f, x + (w - 28) / 3 - 9, h * .5f, Alpha(tint, .45f));
                    }
                    break;
                case Shape.Icon:
                    DrawIcon(mesh, r, symbol, color);
                    break;
            }
        }

        static void DrawIcon(VertexHelper mesh, Rect rect, string code, Color tint)
        {
            float side = Mathf.Min(rect.width, rect.height);
            Vector2 P(float x, float y) => rect.center + new Vector2(x - 12, 12 - y) * side / 24;
            void L(float x, float y, float xx, float yy) => Line(mesh, P(x, y), P(xx, yy), tint, 1.5f);
            void Circle(float radius, float from = 0, float to = 360) =>
                Arc(mesh, rect.center, radius * side / 24, from, to, tint, 1.5f);
            switch (code)
            {
                case "总览":
                case "定位设备":
                    Circle(6); L(12, 2, 12, 7); L(12, 17, 12, 22); L(2, 12, 7, 12); L(17, 12, 22, 12);
                    break;
                case "自由观察":
                    L(2, 12, 7, 6); L(7, 6, 17, 6); L(17, 6, 22, 12);
                    L(22, 12, 17, 18); L(17, 18, 7, 18); L(7, 18, 2, 12); Circle(3);
                    break;
                case "巡检跟随":
                case "派发巡检":
                    L(3, 17, 7, 17); L(7, 17, 7, 7); L(7, 7, 20, 7);
                    L(15, 2, 20, 7); L(15, 12, 20, 7); L(3, 14, 3, 20);
                    break;
                case "确认处理":
                    Circle(9); L(6, 12, 10, 16); L(10, 16, 18, 7);
                    break;
                case "重置":
                    Circle(8, 35, 335); L(3, 4, 3, 10); L(3, 10, 9, 10);
                    break;
                case "停止设备":
                    Circle(9, 40, 320); L(12, 1, 12, 11);
                    break;
                case "发送":
                    L(3, 3, 22, 12); L(22, 12, 3, 21); L(3, 21, 7, 12); L(7, 12, 3, 3);
                    L(7, 12, 15, 12);
                    break;
                case "清空":
                    L(4, 6, 20, 6); L(8, 6, 8, 3); L(8, 3, 16, 3); L(16, 3, 16, 6);
                    L(6, 6, 7, 21); L(7, 21, 17, 21); L(17, 21, 18, 6);
                    L(10, 10, 10, 17); L(14, 10, 14, 17);
                    break;
                case "关闭":
                    L(5, 5, 19, 19); L(19, 5, 5, 19);
                    break;
                case "巡检记录":
                    L(5, 3, 19, 3); L(19, 3, 19, 21); L(19, 21, 5, 21); L(5, 21, 5, 3);
                    L(8, 8, 16, 8); L(8, 12, 16, 12); L(8, 16, 13, 16);
                    break;
                case "分析设备":
                    L(3, 4, 21, 4); L(21, 4, 21, 17); L(21, 17, 10, 17);
                    L(10, 17, 5, 22); L(5, 22, 5, 17); L(5, 17, 3, 17); L(3, 17, 3, 4);
                    L(7, 10, 10, 10); L(13, 10, 17, 10);
                    break;
                default:
                    L(12, 2, 23, 21); L(23, 21, 1, 21); L(1, 21, 12, 2);
                    L(12, 8, 12, 14); L(12, 17, 12, 18);
                    break;
            }
        }

        static Color Alpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        static void Polygon(VertexHelper mesh, Vector2[] points, Color tint)
        {
            int start = mesh.currentVertCount;
            foreach (var point in points) mesh.AddVert(point, tint, Vector2.zero);
            for (int i = 1; i < points.Length - 1; i++) mesh.AddTriangle(start, start + i, start + i + 1);
        }

        static void Quad(VertexHelper mesh, Vector2 a, Vector2 b, Color tint)
        {
            Polygon(mesh, new[] { a, new Vector2(a.x, b.y), b, new Vector2(b.x, a.y) }, tint);
        }

        static void Fade(VertexHelper mesh, Vector2 bottom, Vector2 top, Color from, Color to)
        {
            int start = mesh.currentVertCount;
            mesh.AddVert(bottom, from, Vector2.zero);
            mesh.AddVert(new Vector2(top.x, bottom.y), from, Vector2.zero);
            mesh.AddVert(top, to, Vector2.zero);
            mesh.AddVert(new Vector2(bottom.x, top.y), to, Vector2.zero);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start, start + 2, start + 3);
        }

        internal static void Line(VertexHelper mesh, Vector2 a, Vector2 b, Color tint, float width)
        {
            Vector2 normal = new Vector2(a.y - b.y, b.x - a.x).normalized * width * .5f;
            Polygon(mesh, new[] { a - normal, a + normal, b + normal, b - normal }, tint);
        }

        internal static void Arc(VertexHelper mesh, Vector2 center, float radius, float from,
            float to, Color tint, float width)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt(Mathf.Abs(to - from) / 5));
            Vector2 Point(float angle) => center + new Vector2(Mathf.Sin(angle * Mathf.Deg2Rad),
                Mathf.Cos(angle * Mathf.Deg2Rad)) * radius;
            for (int i = 0; i < steps; i++)
                Line(mesh, Point(Mathf.Lerp(from, to, (float)i / steps)),
                    Point(Mathf.Lerp(from, to, (float)(i + 1) / steps)), tint, width);
        }
    }
}
