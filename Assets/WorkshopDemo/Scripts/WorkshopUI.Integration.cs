using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace WorkshopDemo
{
    public sealed partial class WorkshopUI
    {
        Color Background => new Color32(19, 24, 31, 255);
        Color Surface => panel;
        Color Line => field;
        Color Ink => ink;
        Color Muted => muted;
        Color Yellow => amber;
        Color Green => accent;
        Color Red => red;
        readonly WorkshopInterfaceVisibility interfaceVisibility = new WorkshopInterfaceVisibility();

        RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max,
            Vector2 from, Vector2 to, Color tint, bool blocks = true) =>
            Box(parent, name, min, max, from, to, tint, blocks);

        RectTransform Box(Transform parent, string name, float x, float y, float width,
            float height, Color tint, bool blocks = true) =>
            Box(parent, name, Vector2.up, Vector2.up, new Vector2(x, -y - height),
                new Vector2(x + width, -y), tint, blocks);

        Button Command(Transform parent, string text, float x, float y, float width,
            Action callback, float height) => Command(parent, text, x, y, width, callback, false, height);

        void Segment(Button button, bool selected) { button.image.color = selected ? active : field; }
        void Divider(Transform parent, float x, float y, float width) =>
            Box(parent, "Divider", x, y, width, 1, field, false);

        public static bool Typing =>
            EventSystem.current && EventSystem.current.currentSelectedGameObject &&
            EventSystem.current.currentSelectedGameObject.GetComponent<InputField>() is InputField input && input.isFocused;

        void Update()
        {
            if (!built) return;
            var keyboard = Keyboard.current;
            if (!Typing && keyboard != null && keyboard.hKey.wasPressedThisFrame)
            {
                interfaceVisibility.Toggle();
                if (!interfaceVisibility.Visible) simulation.cameraRig.GetComponent<Camera>().rect = new Rect(0, 0, 1, 1);
                else lastWidth = 0;
            }
            if (!Typing && keyboard != null && keyboard.mKey.wasPressedThisFrame)
                AudioListener.volume = AudioListener.volume > .01f ? 0f : 1f;
            if (!interfaceVisibility.Visible) return;
            UpdateAnalysisPlayback();
            UpdateEquipmentPlayback();
        }

        void OnDestroy() { interfaceVisibility.Restore(); }
    }
}
