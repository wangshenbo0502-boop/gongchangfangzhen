using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WorkshopDemo
{
    // Both cameras render the same equipment and the same changing mesh.
    public sealed class WorkshopEquipmentView : MonoBehaviour, IDragHandler, IScrollHandler
    {
        public WorkshopEquipmentModel Model { get; private set; }
        public WorkshopFault Fault { get; private set; }
        public bool Playing, AutoOrbit, ShowLabels = true;
        public bool OpenDoor = true, RemoveGuard = true;
        public float Progress { get; private set; }
        public float Disassembly { get; private set; }
        public float TargetDisassembly { get; private set; }
        public RenderTexture DetailTexture => detail;
        public bool Focused { get; private set; }
        GameObject scene;
        Camera viewCamera, detailCamera;
        RenderTexture target, detail;
        RectTransform rect;
        Canvas canvas;
        float yaw = -25, pitch = 12, zoom = 1;
        float nextMesh;
        int focusAssembly = -1;

        public void Initialize(WorkshopDevice device, WorkshopFault fault, RawImage inset)
        {
            Fault = fault;
            rect = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
            scene = new GameObject("Inspectable equipment studio");
            scene.transform.position = new Vector3(260,100,100);
            var assembly = new GameObject("Authored equipment");
            assembly.transform.SetParent(scene.transform,false);
            Model = assembly.AddComponent<WorkshopEquipmentModel>();
            Model.Initialize(device.kind,device.deviceId,device);
            float scale = 2.8f/Model.NativeBounds.size.magnitude;
            assembly.transform.localScale = Vector3.one*scale;
            assembly.transform.localPosition = -Model.NativeBounds.center*scale;
            foreach (var t in Model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 28;
            viewCamera = Camera("Equipment orbit camera",28);
            detailCamera = Camera("Same component closeup camera",27);
            target = Texture(1600,1100);
            detail = Texture(720,560);
            viewCamera.targetTexture = target;
            detailCamera.targetTexture = detail;
            GetComponent<RawImage>().texture = target;
            inset.texture = detail;
            Light(new Vector3(-3,4,-4),new Color(1,.96f,.89f),32);
            Light(new Vector3(3,1,-2),new Color(.78f,.87f,1),18);
            Light(new Vector3(0,3,3),Color.white,30);
            SetProgress(0);
        }

        Camera Camera(string name, int layer)
        {
            var go = new GameObject(name);
            go.transform.SetParent(scene.transform,false);
            var c = go.AddComponent<Camera>();
            c.cullingMask = (1 << layer) | (layer == 28 ? 1 << 27 : 0);
            c.clearFlags = CameraClearFlags.SolidColor;
            c.backgroundColor = new Color(.18f,.20f,.21f);
            c.nearClipPlane = .005f; c.farClipPlane = 40; c.fieldOfView = 32;
            return c;
        }

        static RenderTexture Texture(int width, int height)
        {
            var t = new RenderTexture(width,height,24) { antiAliasing = 4 };
            t.Create(); return t;
        }

        void Light(Vector3 position, Color color, float intensity)
        {
            var go = new GameObject("Studio light");
            go.transform.SetParent(scene.transform,false);
            go.transform.localPosition = position;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point; light.range = 15;
            light.intensity = intensity; light.color = color;
            light.cullingMask = (1 << 28) | (1 << 27);
        }

        public void SetProgress(float value)
        {
            Progress = Mathf.Clamp01(value);
            Model.SetDamage(Fault,Progress);
            foreach (var filter in Model.GetComponentsInChildren<MeshFilter>(true)) filter.gameObject.layer = 28;
            Model.DamageSurface.gameObject.layer = 27;
            if (Progress >= 1) Playing = false;
        }

        public void SetDisassemblyTarget(float value)
            => TargetDisassembly = Mathf.Clamp(value,0,Model.AssemblyCount);
        public void FocusDamage() { Focused = true; focusAssembly = -1; zoom = 1; yaw = -8; pitch = 8; }
        public void FocusAssembly(int i) { Focused = true; focusAssembly = i; zoom = 1; }
        public void ResetView() { Focused = false; focusAssembly = -1; zoom = 1; yaw = -25; pitch = 12; }
        public void Preset(int value)
        {
            yaw = value == 2 ? 90 : 0;
            pitch = value == 1 ? 89 : 0;
        }
        public void OnDrag(PointerEventData e)
        {
            yaw -= e.delta.x*.4f; pitch = Mathf.Clamp(pitch+e.delta.y*.35f,-85,89);
            AutoOrbit = false;
        }
        public void OnScroll(PointerEventData e) => zoom = Mathf.Clamp(zoom-e.scrollDelta.y*.10f,.35f,2.5f);
        public Vector2 ProjectAssembly(int i)
        {
            var p = viewCamera.WorldToViewportPoint(Model.AssemblyAnchor(i));
            return new Vector2(p.x,p.y);
        }

        void Update()
        {
            if (!Model) return;
            bool visible = canvas.enabled;
            viewCamera.enabled = detailCamera.enabled = visible;
            if (!visible) { nextMesh = Time.unscaledTime+.05f; return; }
            if (Playing && Time.unscaledTime >= nextMesh)
            {
                float elapsed = Mathf.Min(.2f,Time.unscaledTime-nextMesh+.05f);
                nextMesh = Time.unscaledTime+.05f;
                SetProgress(Progress+elapsed/18);
            }
            else if (!Playing) nextMesh = Time.unscaledTime+.05f;
            if (AutoOrbit) yaw += Time.unscaledDeltaTime*12;
            Disassembly = Mathf.MoveTowards(Disassembly,TargetDisassembly,Time.unscaledDeltaTime*2.5f);
            Model.SetDisassembly(Disassembly,OpenDoor,RemoveGuard);
        }

        void LateUpdate()
        {
            if (!Model || !canvas.enabled) return;
            viewCamera.aspect = rect.rect.width/Mathf.Max(1,rect.rect.height);
            Bounds b = Model.DamageSurface.GetComponent<Renderer>().bounds;
            if (Focused && focusAssembly >= 0)
            {
                var renderers = Model.AssemblyTransform(focusAssembly).GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length > 0)
                {
                    b = renderers[0].bounds;
                    foreach (var r in renderers) if (r.enabled) b.Encapsulate(r.bounds);
                }
            }
            if (!Focused)
                foreach (var r in Model.GetComponentsInChildren<MeshRenderer>())
                    if (r.enabled) b.Encapsulate(r.bounds);
            Vector3 direction = Quaternion.Euler(pitch,yaw,0)*Vector3.back;
            var rotation = Quaternion.LookRotation(-direction,Vector3.up);
            float tangent = Mathf.Tan(viewCamera.fieldOfView*Mathf.Deg2Rad/2);
            float fitted = .05f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = Quaternion.Inverse(rotation)*Vector3.Scale(b.extents,
                    new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                fitted = Mathf.Max(fitted,Mathf.Abs(corner.y)/tangent-corner.z,
                    Mathf.Abs(corner.x)/(tangent*viewCamera.aspect)-corner.z);
            }
            float distance = fitted*1.12f*zoom;
            viewCamera.transform.position = b.center+direction*distance;
            viewCamera.transform.LookAt(b.center);
            var closeBounds = Model.DamageSurface.GetComponent<Renderer>().bounds;
            detailCamera.aspect = (float)detail.width/detail.height;
            float closeDistance = Mathf.Max(closeBounds.extents.y,closeBounds.extents.x/detailCamera.aspect)/
                Mathf.Tan(detailCamera.fieldOfView*Mathf.Deg2Rad*.5f)+closeBounds.extents.z;
            detailCamera.transform.position = closeBounds.center+
                Quaternion.Euler(8,-9,0)*Vector3.back*Mathf.Max(.08f,closeDistance*1.24f);
            detailCamera.transform.LookAt(closeBounds.center);
        }
        void OnEnable() { if (scene) scene.SetActive(true); }
        void OnDisable() { if (scene) scene.SetActive(false); }
        void OnDestroy()
        {
            if (viewCamera) viewCamera.targetTexture = null;
            if (detailCamera) detailCamera.targetTexture = null;
            if (scene) Destroy(scene);
            if (target) { target.Release(); Destroy(target); }
            if (detail) { detail.Release(); Destroy(detail); }
        }
    }
}
