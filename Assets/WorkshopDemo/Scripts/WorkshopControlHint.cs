using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WorkshopDemo
{
    public sealed class WorkshopControlHint : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string Caption;
        public Font Font;
        GameObject hint;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!hint)
            {
                var canvas = GetComponentInParent<Canvas>();
                hint = new GameObject("Control hint", typeof(RectTransform), typeof(Image));
                var rect = (RectTransform)hint.transform;
                rect.SetParent(canvas.transform, false);
                rect.sizeDelta = new Vector2(104, 30);
                hint.GetComponent<Image>().color = new Color(.025f, .09f, .10f, 1);
                hint.GetComponent<Image>().raycastTarget = false;
                var label = new GameObject("Caption", typeof(RectTransform), typeof(Text));
                var labelRect = (RectTransform)label.transform;
                labelRect.SetParent(rect, false);
                labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
                var text = label.GetComponent<Text>();
                text.text = Caption; text.font = Font; text.fontSize = 12; text.color = Color.white;
                text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
            }
            hint.SetActive(true);
            var button = (RectTransform)transform;
            hint.transform.position = button.TransformPoint(new Vector3(0, -button.rect.height * .5f - 22, 0));
            hint.transform.SetAsLastSibling();
        }

        public void OnPointerExit(PointerEventData eventData) { if (hint) hint.SetActive(false); }
        void OnDisable() { if (hint) hint.SetActive(false); }
        void OnDestroy() { if (hint) Destroy(hint); }
    }
}
