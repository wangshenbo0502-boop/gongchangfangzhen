using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WorkshopDemo
{
    // Both cameras render the same equipment instance.
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
        public bool FaultMode { get; private set; }
        public bool WaitingForAssembly => FaultMode && Disassembly > 0;
        internal WorkshopFaultProfile Profile => Model.ProfileFor(Fault);
        internal float Duration => Profile?.Duration ?? 18;
        internal bool CanShowPumpSection => FaultMode && !WaitingForAssembly && Profile != null &&
            (((Fault.Code == "P-SEAL" || Fault.Code == "P-CAV") && Model.PumpFaultPresenter != null) ||
                Model.LocalFaultPresenter?.CanShowSection == true);
        internal bool ThermalVisible
        {
            get => Model.LocalFaultPresenter?.ThermalVisible ?? false;
            set { if (Model.LocalFaultPresenter != null) Model.LocalFaultPresenter.ThermalVisible = value; SetProgress(Progress); }
        }
        internal float MotionScale
        {
            get => Model.LocalFaultPresenter?.MotionScale ?? 1;
            set { if (Model.LocalFaultPresenter != null) Model.LocalFaultPresenter.MotionScale = value; SetProgress(Progress); }
        }
        internal bool TeachingVisible
        {
            get => Model.LocalFaultPresenter?.TeachingVisible ?? false;
            set { if (Model.LocalFaultPresenter != null) Model.LocalFaultPresenter.TeachingVisible = value; SetProgress(Progress); }
        }
        internal bool PumpSectionOpen => CanShowPumpSection && ((Fault.Code == "P-SEAL" || Fault.Code == "P-CAV") ?
            Model.PumpFaultPresenter.SectionOpen : Model.LocalFaultPresenter.SectionOpen);
        GameObject scene;
        WorkshopEquipmentModel tankFaultModel;
        WorkshopDevice device;
        Camera viewCamera, detailCamera, backgroundCamera;
        bool backgroundRenderingSuppressed, backgroundCameraWasEnabled;
        RenderTexture target, detail;
        RectTransform rect;
        RawImage image, detailImage;
        Canvas canvas;
        float yaw = -25, pitch = 12, zoom = 1;
        float nextMesh;
        int focusAssembly = -1;
        float? queuedProgress;
        GameObject legacyDetail;
        int legacyDetailLayer;
        Quaternion previewRotorHome;

        public void Initialize(WorkshopDevice device, WorkshopFault fault, RawImage inset)
        {
            Fault = fault;
            this.device = device;
            rect = (RectTransform)transform;
            image = GetComponent<RawImage>();
            detailImage = inset;
            canvas = GetComponentInParent<Canvas>();
            backgroundCamera = WorkshopCamera.Main;
            SetBackgroundRenderingSuppressed(canvas.enabled);
            scene = new GameObject("Inspectable equipment studio");
            scene.transform.position = new Vector3(260,100,100);
            Model = CreateStudioModel(device, true);
            if (Model.Rotor) previewRotorHome = Model.Rotor.localRotation;
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

        public void SetFaultMode(bool active)
        {
            if (FaultMode == active) return;
            Playing = false;
            queuedProgress = null;
            RestoreDetailLayer();
            DisplayModel.SetDamage(null, 0);
            if (Model.Rotor) Model.Rotor.localRotation = previewRotorHome;
            FaultMode = active;
            Progress = 0;
            if (active) TargetDisassembly = 0;
            UpdateDisplayModel();
            ResetView();
            if (active && !WaitingForAssembly) ApplyProgress(0);
        }

        internal void SelectPumpFault(WorkshopFault fault) => SelectFault(fault);

        internal void SelectFault(WorkshopFault fault)
        {
            if (Fault == fault || !Model || fault == null || fault.Kind != Model.Kind) return;
            SetFaultMode(false);
            Fault = fault;
            SetFaultMode(true);
        }

        internal void TogglePumpSection()
        {
            if (!CanShowPumpSection) return;
            bool opened = !PumpSectionOpen;
            if (Fault.Code == "P-SEAL" || Fault.Code == "P-CAV") Model.PumpFaultPresenter.SetSection(opened);
            else Model.LocalFaultPresenter.SetSection(opened);
            SetProgress(Progress);
            if (opened) FocusDamage();
            else ResetView();
        }

        void UpdateDisplayModel()
        {
            if (tankFaultModel) tankFaultModel.gameObject.SetActive(false);
            Model.gameObject.SetActive(true);
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
            if (WaitingForAssembly)
            {
                queuedProgress = Mathf.Clamp01(value);
                return;
            }
            ApplyProgress(value);
        }

        void ApplyProgress(float value)
        {
            Progress = Mathf.Clamp01(value);
            var model = DisplayModel;
            model.SetDamage(FaultMode ? Fault : null, Progress);
            if (FaultMode && Profile != null)
            {
                model.FaultPresenter?.SetDetailLayer(27);
                model.PumpFaultPresenter?.SetDetailLayer(27);
                model.LocalFaultPresenter?.SetDetailLayer(27);
                if (model.Rotor && model.FaultFrame.HasValue && model.Kind == DeviceKind.Fan)
                {
                    var frame = model.FaultFrame.Value;
                    float limit = WorkshopDevice.ProtectionProgress(Fault.Code) * Duration;
                    float seconds = Fault.Trips ? Mathf.Min(frame.Seconds, limit) : frame.Seconds;
                    float direction = Fault.Code == "F-REVERSE" && Progress > 0 ? -1 : 1;
                    // Surface inspections share one rotor phase for direct geometric comparisons.
                    if (Fault.Code == "F-CRACK" || Fault.Code == "F-BLADE" || Fault.Code == "F-RUB") seconds = 0;
                    model.Rotor.localRotation = previewRotorHome * Quaternion.AngleAxis(
                        Fault.Code == "F-IMBAL" ? WorkshopFaultProfile.ImbalancePreviewAngle(seconds,
                            model.NominalRpm, Fault.SpeedRatio) : seconds * 180 * direction, Vector3.forward);
                    model.LocalFaultPresenter?.SyncRotorPhase();
                }
            }
            else if (FaultMode && model.TryGetFaultFocus(out _, out _))
            {
                var part = model.DamageSurface.gameObject;
                if (legacyDetail != part)
                {
                    RestoreDetailLayer();
                    legacyDetail = part;
                    legacyDetailLayer = part.layer;
                    part.layer = 27;
                }
            }
            else RestoreDetailLayer();
            if (Progress >= 1) Playing = false;
        }

        void RestoreDetailLayer()
        {
            Model.FaultPresenter?.SetDetailLayer(null);
            Model.PumpFaultPresenter?.SetDetailLayer(null);
            Model.LocalFaultPresenter?.SetDetailLayer(null);
            if (!FaultMode && Model.Rotor) Model.Rotor.localRotation = previewRotorHome;
            if (legacyDetail) legacyDetail.layer = legacyDetailLayer;
            legacyDetail = null;
        }

        public void SetDisassemblyTarget(float value)
        {
            if (!FaultMode) TargetDisassembly = Mathf.Clamp(value, 0, Model.AssemblyCount);
        }
        public void FocusDamage()
        {
            if (WaitingForAssembly || !DisplayModel.TryGetFaultFocus(out _, out var normal)) return;
            Focused = true;
            focusAssembly = -1;
            zoom = 1;
            yaw = normal == Vector3.zero ? -8 : Mathf.Atan2(-normal.x, -normal.z) * Mathf.Rad2Deg;
            pitch = normal == Vector3.zero ? 8 :
                Mathf.Clamp(Mathf.Asin(Mathf.Clamp(normal.normalized.y, -1, 1)) * Mathf.Rad2Deg, -80, 80);
        }
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
            SetBackgroundRenderingSuppressed(visible);
            viewCamera.enabled = visible;
            if (!visible)
            {
                detailCamera.enabled = false;
                Model.PumpFaultPresenter?.UpdateAudio(false);
                nextMesh = Time.unscaledTime+.05f;
                return;
            }
            bool assembling = WaitingForAssembly;
            float assemblySpeed = Model.AssemblySeconds > 0 ? Model.AssemblyCount / Model.AssemblySeconds : 2.5f;
            Disassembly = Mathf.MoveTowards(Disassembly,TargetDisassembly,Time.unscaledDeltaTime*assemblySpeed);
            Model.SetDisassembly(Disassembly,OpenDoor,RemoveGuard);
            if (assembling && !WaitingForAssembly)
            {
                UpdateDisplayModel();
                ApplyProgress(queuedProgress ?? 0);
                queuedProgress = null;
            }
            if (Playing && FaultMode && !WaitingForAssembly && Profile != null)
                SetProgress(Progress + Time.unscaledDeltaTime / Duration);
            else if (Playing && FaultMode && !WaitingForAssembly && Time.unscaledTime >= nextMesh)
            {
                float elapsed = Mathf.Min(.2f,Time.unscaledTime-nextMesh+.05f);
                nextMesh = Time.unscaledTime+.05f;
                SetProgress(Progress+elapsed/Duration);
            }
            else if (!Playing || WaitingForAssembly) nextMesh = Time.unscaledTime+.05f;
            detailCamera.enabled = visible && detailImage.gameObject.activeInHierarchy && !WaitingForAssembly &&
                DisplayModel.TryGetFaultFocus(out _, out _);
            Model.PumpFaultPresenter?.UpdateAudio(Playing && FaultMode && !WaitingForAssembly);
            if (AutoOrbit) yaw += Time.unscaledDeltaTime*12;
        }

        void LateUpdate()
        {
            if (!Model || !canvas.enabled)
            {
                if (detailCamera) detailCamera.enabled = false;
                return;
            }
            UpdateRenderTarget();
            viewCamera.aspect = rect.rect.width/Mathf.Max(1,rect.rect.height);
            var model = DisplayModel;
            Bounds faultBounds = default;
            bool hasFaultFocus = FaultMode && !WaitingForAssembly &&
                model.TryGetFaultFocus(out faultBounds, out _);
            // A close inspection of pump internals must not place surrounding casing in front of the lens.
            bool internalInspection = Focused && focusAssembly < 0 && hasFaultFocus &&
                PumpSectionOpen && model.LocalFaultPresenter?.SectionOpen == true;
            viewCamera.cullingMask = internalInspection ? 1 << 27 : (1 << 28) | (1 << 27);
            Bounds b;
            if (Focused && focusAssembly < 0 && hasFaultFocus) b = faultBounds;
            else if (model.ReviewedTankModel) b = model.ReviewedTankBounds(Focused ? focusAssembly : -1);
            else if (model.ReviewedCabinetModel) b = model.ReviewedCabinetBounds(Focused ? focusAssembly : -1);
            else b = model.VisibleBounds(Focused && focusAssembly >= 0 ? model.AssemblyTransform(focusAssembly) : null);
            Vector3 direction = Quaternion.Euler(pitch,yaw,0)*Vector3.back;
            float distance = FittedDistance(viewCamera, b, direction)*1.12f*zoom;
            viewCamera.transform.position = b.center+direction*distance;
            viewCamera.transform.LookAt(b.center);
            if (!detailCamera.enabled || !model.TryGetFaultFocus(out var closeBounds, out var normal)) return;
            detailCamera.cullingMask = Fault.Code == "TK-LEAK"
                ? (1 << 27) | (1 << 28)
                : 1 << 27;
            detailCamera.aspect = (float)detail.width/detail.height;
            Vector3 closeDirection = normal == Vector3.zero ? Quaternion.Euler(8,-9,0)*Vector3.back :
                (normal + Vector3.up * .12f).normalized;
            if (Fault.Code == "TK-LEAK" && model.FaultPresenter != null)
            {
                closeBounds = model.FaultPresenter.CloseupBounds;
                closeDirection = (normal + model.transform.right * .24f + Vector3.up * .08f).normalized;
            }
            if (Fault.Code == "P-SEAL" && model.PumpFaultPresenter != null)
                closeBounds = model.PumpFaultPresenter.CloseupBounds;
            float framingScale = 1f;
            closeBounds.extents *= framingScale;
            float closeDistance = FittedDistance(detailCamera, closeBounds, closeDirection);
            detailCamera.transform.position = closeBounds.center+closeDirection*Mathf.Max(.08f,closeDistance*1.24f);
            detailCamera.transform.LookAt(closeBounds.center);
        }

        static float FittedDistance(Camera camera, Bounds bounds, Vector3 direction)
        {
            var inverse = Quaternion.Inverse(Quaternion.LookRotation(-direction, Vector3.up));
            float tangent = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * .5f);
            float distance = .05f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = inverse * Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                distance = Mathf.Max(distance, Mathf.Abs(corner.y) / tangent - corner.z,
                    Mathf.Abs(corner.x) / (tangent * camera.aspect) - corner.z);
            }
            return distance;
        }
        void SetBackgroundRenderingSuppressed(bool suppressed)
        {
            if (!backgroundCamera || backgroundRenderingSuppressed == suppressed) return;
            if (suppressed)
            {
                backgroundCameraWasEnabled = backgroundCamera.enabled;
                backgroundCamera.enabled = false;
            }
            else backgroundCamera.enabled = backgroundCameraWasEnabled;
            backgroundRenderingSuppressed = suppressed;
        }

        void OnEnable()
        {
            if (scene) scene.SetActive(true);
            if (canvas) SetBackgroundRenderingSuppressed(canvas.enabled);
        }
        void OnDisable()
        {
            SetBackgroundRenderingSuppressed(false);
            if (Model) SetFaultMode(false);
            if (scene) scene.SetActive(false);
        }
        void OnDestroy()
        {
            SetBackgroundRenderingSuppressed(false);
            if (viewCamera) viewCamera.targetTexture = null;
            if (detailCamera) detailCamera.targetTexture = null;
            if (scene) Destroy(scene);
            if (target) { target.Release(); Destroy(target); }
            if (detail) { detail.Release(); Destroy(detail); }
        }
    }
}
