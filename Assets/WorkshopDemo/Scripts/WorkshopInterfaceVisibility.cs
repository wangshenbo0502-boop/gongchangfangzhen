using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WorkshopDemo
{
    // Preserve disabled canvases and labels when restoring the scene's interface.
    public sealed class WorkshopInterfaceVisibility
    {
        readonly Dictionary<Behaviour, bool> behaviours = new Dictionary<Behaviour, bool>();
        readonly Dictionary<Renderer, bool> labels = new Dictionary<Renderer, bool>();
        public bool Visible { get; private set; } = true;

        public void Toggle()
        {
            if (!Visible) { Restore(); return; }
            Visible = false;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                behaviours[canvas] = canvas.enabled;
                canvas.enabled = false;
            }
            foreach (var raycaster in Object.FindObjectsByType<GraphicRaycaster>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                behaviours[raycaster] = raycaster.enabled;
                raycaster.enabled = false;
            }
            foreach (var text in Object.FindObjectsByType<TextMesh>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var renderer = text.GetComponent<Renderer>();
                if (!renderer) continue;
                labels[renderer] = renderer.enabled;
                renderer.enabled = false;
            }
        }

        public void Restore()
        {
            foreach (var entry in behaviours)
                if (entry.Key) entry.Key.enabled = entry.Value;
            foreach (var entry in labels)
                if (entry.Key) entry.Key.enabled = entry.Value;
            behaviours.Clear();
            labels.Clear();
            Visible = true;
        }
    }
}
