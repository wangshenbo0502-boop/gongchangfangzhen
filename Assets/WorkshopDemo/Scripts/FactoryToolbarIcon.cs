using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WorkshopDemo
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FactoryToolbarIcon : Graphic, IPointerEnterHandler, IPointerExitHandler
    {
        public bool isAudio;
        public TMP_Text tooltip;
        public string caption;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if (isAudio)
            {
                Line(vh, -10, -5, -5, -5);
                Line(vh, -10, 5, -5, 5);
                Line(vh, -10, -5, -10, 5);
                Line(vh, -5, 5, 2, 11);
                Line(vh, 2, 11, 2, -11);
                Line(vh, 2, -11, -5, -5);
                Line(vh, 7, -6, 10, -3);
                Line(vh, 10, -3, 10, 3);
                Line(vh, 10, 3, 7, 6);
                if (AudioListener.volume == 0)
                    Line(vh, -13, -14, 14, 14);
            }
            else
            {
                Line(vh, -12, -10, 12, -10);
                Line(vh, 12, -10, 12, 10);
                Line(vh, 12, 10, -12, 10);
                Line(vh, -12, 10, -12, -10);
                Line(vh, -4, -10, -4, 10);
                Line(vh, 0, 4, 8, 4);
                Line(vh, 0, -2, 8, -2);
            }
        }

        void Line(VertexHelper vh, float x1, float y1, float x2, float y2)
        {
            Vector2 a = new Vector2(x1, y1), b = new Vector2(x2, y2);
            Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized;
            int i = vh.currentVertCount;
            vh.AddVert(a - n, color, Vector2.zero);
            vh.AddVert(a + n, color, Vector2.zero);
            vh.AddVert(b + n, color, Vector2.zero);
            vh.AddVert(b - n, color, Vector2.zero);
            vh.AddTriangle(i, i + 1, i + 2);
            vh.AddTriangle(i, i + 2, i + 3);
        }

        void Update() { if (isAudio) SetVerticesDirty(); }

        public void OnPointerEnter(PointerEventData eventData)
        {
            tooltip.text = caption;
            tooltip.gameObject.SetActive(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            tooltip.gameObject.SetActive(false);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            if (tooltip != null) tooltip.gameObject.SetActive(false);
        }
    }
}
