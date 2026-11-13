using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WorkshopDemo
{
    // Both cameras render the same equipment and the same changing mesh.
    public sealed class WorkshopEquipmentView : MonoBehaviour, IDragHandler, IScrollHandler
    {
        public WorkshopEquipmentModel Model { get; private set; }
        public WorkshopEquipmentModel DisplayModel => tankFaultModel && tankFaultModel.gameObject.activeSelf
            ? tankFaultModel : Model;
        public WorkshopFault Fault { get; private set; }
        public bool Playing, AutoOrbit, ShowLabels = true;
        public bool OpenDoor = true, RemoveGuard = true;
        public float Progress { get; private set; }
        public float Disassembly { get; private set; }
        public float TargetDisassembly { get; private set; }
        public RenderTexture DetailTexture => detail;
        public bool Focused { get; private set; }
        GameObject scene;
        WorkshopEquipmentModel tankFaultModel;
        Camera viewCamera, detailCamera;
        RenderTexture target, detail;
        RectTransform rect;
        RawImage image, detailImage;
        Canvas canvas;
        float yaw = -25, pitch = 12, zoom = 1;
        float nextMesh;
        int focusAssembly = -1;

        public void Initialize(WorkshopDevice device, WorkshopFault fault, RawImage inset)
        {
            Fault = fault;
            rect = (RectTransform)transform;
            image = GetComponent<RawImage>();
            detailImage = inset;
            canvas = GetComponentInParent<Canvas>();
            scene = new GameObject("Inspectable equipment studio");
            scene.transform.position = new Vector3(260,100,100);
            Model = CreateStudioModel(device, true);
            viewCamera = Camera("Equipment orbit camera",28);
            detailCamera = Camera("Same component closeup camera",27);
            detail = Texture(720,560);
            detailCamera.targetTexture = detail;
            inset.texture = detail;
            UpdateRenderTarget();
            Light(new Vector3(-3,4,-4),new Color(1,.96f,.89f),32);
            Light(new Vector3(3,1,-2),new Color(.78f,.87f,1),18);
            Light(new Vector3(0,3,3),Color.white,30);
            SetProgress(0);
        }

        WorkshopEquipmentModel CreateStudioModel(WorkshopDevice device, bool useReviewedTank)
        {
            var assembly = new GameObject(useReviewedTank ? "Authored equipment" : "Tank fault demonstration");
            assembly.transform.SetParent(scene.transform, false);
            var model = assembly.AddComponent<WorkshopEquipmentModel>();
            model.Initialize(device.kind, device.deviceId, device, useReviewedTank);
            float scale = 2.8f / model.NativeBounds.size.magnitude;
            assembly.transform.localScale = Vector3.one * scale;
            assembly.transform.localPosition = -model.NativeBounds.center * scale;
            if (model.ReviewedFanModel || model.ReviewedCabinetModel || model.ReviewedTankModel)
                model.GetComponentInChildren<ReflectionProbe>().size = Vector3.one * 20;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 28;
            return model;
        }

        public void ShowTankFaultModel(WorkshopDevice device, bool show)
        {
            if (!Model.ReviewedTankModel || (DisplayModel != Model) == show) return;
            if (show && !tankFaultModel) tankFaultModel = CreateStudioModel(device, false);
            tankFaultModel.gameObject.SetActive(show);
            Model.gameObject.SetActive(!show);
            ResetView();
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

        void UpdateRenderTarget()
        {
            float pixelWidth = Mathf.Max(1, rect.rect.width * canvas.scaleFactor);
            float pixelHeight = Mathf.Max(1, rect.rect.height * canvas.scaleFactor);
            // Resolve to more samples than the viewport uses, without an unbounded allocation at 4K.
            float scale = Mathf.Min(1.5f, 3840f / pixelWidth, 2160f / pixelHeight);
            int width = Mathf.Max(8, Mathf.CeilToInt(pixelWidth * scale / 8f) * 8);
            int height = Mathf.Max(8, Mathf.CeilToInt(pixelHeight * scale / 8f) * 8);
            if (target && target.width == width && target.height == height) return;
            var previous = target;
            target = Texture(width, height);
            viewCamera.targetTexture = target;
            image.texture = target;
            if (previous) { previous.Release(); Destroy(previous); }
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
            var model = DisplayModel;
            model.SetDamage(Fault,Progress);
            if (!model.ReviewedCabinetModel && !model.ReviewedTankModel)
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true)) filter.gameObject.layer = 28;
            if (model.DamageSurface) model.DamageSurface.gameObject.layer = 27;
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
            viewCamera.enabled = visible;
            detailCamera.enabled = visible && detailImage.gameObject.activeInHierarchy;
            if (!visible) { nextMesh = Time.unscaledTime+.05f; return; }
            if (Playing && Time.unscaledTime >= nextMesh)
            {
                float elapsed = Mathf.Min(.2f,Time.unscaledTime-nextMesh+.05f);
                nextMesh = Time.unscaledTime+.05f;
                SetProgress(Progress+elapsed/18);
            }
            else if (!Playing) nextMesh = Time.unscaledTime+.05f;
            if (AutoOrbit) yaw += Time.unscaledDeltaTime*12;
            float assemblySpeed = Model.AssemblySeconds > 0 ? Model.AssemblyCount / Model.AssemblySeconds : 2.5f;
            Disassembly = Mathf.MoveTowards(Disassembly,TargetDisassembly,Time.unscaledDeltaTime*assemblySpeed);
            Model.SetDisassembly(Disassembly,OpenDoor,RemoveGuard);
        }

        void LateUpdate()
        {
            if (!Model || !canvas.enabled) return;
            UpdateRenderTarget();
            viewCamera.aspect = rect.rect.width/Mathf.Max(1,rect.rect.height);
            var model = DisplayModel;
            Bounds b = model.DamageSurface.GetComponent<Renderer>().bounds;
            if (model.ReviewedTankModel && (!Focused || focusAssembly >= 0))
                b = model.ReviewedTankBounds(Focused ? focusAssembly : -1);
            else if (model.ReviewedCabinetModel && (!Focused || focusAssembly >= 0))
                b = model.ReviewedCabinetBounds(Focused ? focusAssembly : -1);
            else if (Focused && focusAssembly >= 0)
            {
                var renderers = model.AssemblyTransform(focusAssembly).GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length > 0)
                {
                    b = renderers[0].bounds;
                    foreach (var r in renderers) if (r.enabled) b.Encapsulate(r.bounds);
                }
            }
            if (!Focused && !model.ReviewedCabinetModel && !model.ReviewedTankModel)
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>())
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
            var closeBounds = model.DamageSurface.GetComponent<Renderer>().bounds;
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
