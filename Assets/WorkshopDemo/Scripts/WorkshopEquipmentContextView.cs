using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WorkshopDemo
{
    // Copies renderers only; business scripts and colliders stay in the inspection scene.
    public sealed class WorkshopEquipmentContextView : MonoBehaviour, IDragHandler, IScrollHandler
    {
        GameObject scene, pivot;
        Camera cameraView;
        RenderTexture texture;
        readonly List<Transform> parts = new List<Transform>();
        readonly List<Vector3> origins = new List<Vector3>();
        float distance = 1, explosion;
        readonly List<MeshRenderer> copiedRenderers = new List<MeshRenderer>();
        public int PartCount => parts.Count;
        public bool Exploded => explosion > 0;
        public void Initialize(WorkshopDevice device)
        {
            Transform source = null;
            var refined = device.GetComponentInChildren<WorkshopEquipmentModel>();
            if (refined && refined.Refined) source = refined.transform;
            foreach (Transform child in device.transform)
                if (!source && child.name.StartsWith("Source model")) { source = child; break; }
            if (!source) return;
            var renderers = System.Array.FindAll(source.GetComponentsInChildren<MeshRenderer>(),
                r => r.enabled && r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float scale = 2.5f / Mathf.Max(.001f,bounds.size.magnitude);
            scene = new GameObject("Equipment appearance inspection");
            scene.transform.position = new Vector3(180,120,120);
            pivot = new GameObject("Equipment orbit");
            pivot.transform.SetParent(scene.transform,false);
            foreach (var original in renderers)
            {
                var filter = original.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh) continue;
                var part = new GameObject(original.name) { layer = 30 };
                part.transform.SetParent(pivot.transform,false);
                part.transform.localPosition = (original.transform.position-bounds.center)*scale;
                part.transform.localRotation = original.transform.rotation;
                part.transform.localScale = original.transform.lossyScale*scale;
                part.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                var copied = part.AddComponent<MeshRenderer>();
                copied.sharedMaterials = original.sharedMaterials;
                copiedRenderers.Add(copied);
                parts.Add(part.transform); origins.Add(part.transform.localPosition);
            }
            pivot.transform.localRotation = Quaternion.Euler(15,-35,0);
            var cameraObject = new GameObject("Equipment context camera");
            cameraObject.transform.SetParent(scene.transform,false);
            cameraView = cameraObject.AddComponent<Camera>();
            cameraView.cullingMask = 1 << 30;
            cameraView.clearFlags = CameraClearFlags.SolidColor;
            cameraView.backgroundColor = new Color(.10f,.12f,.14f);
            cameraView.nearClipPlane = .01f;
            cameraView.farClipPlane = 20;
            cameraView.fieldOfView = 32;
            texture = new RenderTexture(900,700,24) { antiAliasing = 4 };
            texture.Create();
            cameraView.targetTexture = texture;
            GetComponent<RawImage>().texture = texture;
            var lightObject = new GameObject("Equipment inspection light");
            lightObject.transform.SetParent(scene.transform,false);
            lightObject.transform.localPosition = new Vector3(-2,3,-3);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point; light.range = 12; light.intensity = 15;
            light.cullingMask = 1 << 30;
        }
        public void ToggleExplosion()
        {
            explosion = Exploded ? 0 : .55f;
            for (int i = 0; i < parts.Count; i++)
            {
                Vector3 direction = origins[i].normalized;
                if (direction.sqrMagnitude < .01f) direction = Vector3.up;
                parts[i].localPosition = origins[i]+direction*explosion;
            }
        }
        public void OnDrag(PointerEventData e)
        {
            if (!pivot) return;
            pivot.transform.Rotate(Vector3.up,-e.delta.x*.5f,Space.World);
            pivot.transform.Rotate(Vector3.right,e.delta.y*.5f,Space.World);
        }
        public void OnScroll(PointerEventData e) => distance = Mathf.Clamp(distance-e.scrollDelta.y*.1f,.5f,2);
        void Update()
        {
            if (!cameraView) return;
            cameraView.enabled = GetComponentInParent<Canvas>().enabled;
            var r = ((RectTransform)transform).rect;
            cameraView.aspect = r.width/Mathf.Max(1,r.height);
            Bounds bounds = copiedRenderers[0].bounds;
            foreach (var part in copiedRenderers) bounds.Encapsulate(part.bounds);
            float halfHeight = Mathf.Max(bounds.extents.y,bounds.extents.x/cameraView.aspect);
            float fitted = halfHeight/Mathf.Tan(cameraView.fieldOfView*Mathf.Deg2Rad*.5f) + bounds.extents.z;
            cameraView.transform.position = bounds.center + Vector3.back * fitted * 1.15f * distance;
        }
        void OnEnable() { if (scene) scene.SetActive(true); }
        void OnDisable() { if (scene) scene.SetActive(false); }
        void OnDestroy()
        {
            if (cameraView) cameraView.targetTexture = null;
            if (scene) Destroy(scene);
            if (texture) { texture.Release(); Destroy(texture); }
        }
    }
}
