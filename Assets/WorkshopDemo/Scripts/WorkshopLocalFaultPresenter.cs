using System.Collections.Generic;
using UnityEngine;

namespace WorkshopDemo
{
    // Local teaching overlays. Original property blocks, layers and transforms are reversible.
    internal sealed partial class WorkshopLocalFaultPresenter
    {
        readonly WorkshopEquipmentModel model;
        readonly Dictionary<string, WorkshopFaultBinding> bindings;
        readonly Dictionary<Renderer, MaterialPropertyBlock> originals = new Dictionary<Renderer, MaterialPropertyBlock>();
        readonly Dictionary<GameObject, int> layers = new Dictionary<GameObject, int>();
        readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();
        readonly Dictionary<Renderer, bool> sectionHidden = new Dictionary<Renderer, bool>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly GameObject[] drops = new GameObject[8];
        readonly Material water;
        readonly GameObject jointWet;
        readonly Mesh jointWetMesh;
        WorkshopFaultBinding binding;
        Renderer[] couplingContext;
        Renderer[] pumpContext;
        bool liveFrame;
        public bool SectionOpen { get; private set; }
        public bool CanShowSection => !liveFrame && pumpContext != null && pumpContext.Length > 0;
        MeshFilter wornBladeFilter;
        Mesh intactBlade, wornBlade;
        Vector3[] bladeVertices, bladeWear, bladeOutput;
        float bladeActivity = -1;
        MeshFilter raceFilter;
        Mesh intactRace, wornRace;
        Vector3[] raceVertices, raceWear, raceOutput;
        float raceActivity = -1;
        GameObject inletScreen, inletDeposit;
        Mesh screenMesh, depositMesh;
        Material screenMaterial, depositMaterial;
        WorkshopFaultBinding suction;
        float inletRadiusM;
        Vector3 inletCenterLocal;
        readonly Vector3[] depositVertices = new Vector3[66];
        float inletActivity = -1;
        Transform moving;
        readonly Quaternion rotorHome;
        WorkshopFaultFrame rotorFrame;
        float rotorPhase;
        Vector3 origin, liquidScale, liquidPosition;
        bool liquidChanged;
        WorkshopFaultCase spec;
        MeshFilter thinnedShell;
        Mesh intactShell;
        internal Mesh ThinnedMesh { get; private set; }
        public bool ThermalVisible = true;
        public float MotionScale = 1;
        public bool Available => binding != null;
        public Bounds FocusBounds
        {
            get
            {
                if (spec?.Code == "F-CRACK" && model.TryGetFanSeamBounds(out var seam)) return seam;
                if (SectionOpen && spec?.Code == "P-BEAR" && raceFilter)
                {
                    var race = raceFilter.GetComponent<Renderer>().bounds;
                    race.Expand(.008f * Mathf.Abs(model.transform.lossyScale.x));
                    return race;
                }
                if (SectionOpen)
                {
                    var context = pumpContext[0].bounds;
                    foreach (var r in pumpContext) context.Encapsulate(r.bounds);
                    context.Expand(.025f * model.transform.lossyScale.x);
                    return context;
                }
                if (spec?.Code == "P-INLET")
                    return new Bounds(model.transform.TransformPoint(inletCenterLocal),
                        Vector3.one * (.34f * model.transform.lossyScale.x));
                if (binding.Settings.anchorCalibrated) return binding.FocusBounds();
                var b = binding.Renderers[0].bounds;
                foreach (var r in binding.Renderers) b.Encapsulate(r.bounds);
                if (couplingContext != null)
                    foreach (var r in couplingContext)
                        b.Encapsulate(r.bounds);
                b.Expand(Mathf.Max(.008f * model.transform.lossyScale.x, b.size.magnitude * .35f));
                return b;
            }
        }
        public Vector3 Normal => spec?.Code == "TK-FLANGE" ?
            model.transform.TransformDirection(new Vector3(0, .06f, -1).normalized) :
            SectionOpen ? model.transform.TransformDirection(spec.Code == "P-IMPELLER" ?
                new Vector3(-1, .22f, -.25f).normalized : new Vector3(-1, .18f, -.65f).normalized) :
            spec?.Code == "P-INLET" && inletScreen ?
                model.transform.TransformDirection(new Vector3(-1, .12f, -.22f).normalized) :
            spec?.Code == "P-ALIGN" || spec?.Code == "TK-VENT" ?
            model.transform.TransformDirection(new Vector3(0, .65f, -1).normalized) :
            binding.Settings.anchorCalibrated ? binding.Normal :
            spec?.Code == "F-CRACK" && surfaceAvailable ?
                binding.Surface.TransformDirection(surfaceNormal).normalized :
            model.Kind == DeviceKind.Fan ? model.transform.TransformDirection(
                spec?.Code == "F-BEAR" ? Vector3.forward : Vector3.back) :
            surfaceAvailable ? binding.Surface.TransformDirection(surfaceNormal).normalized :
            model.transform.TransformDirection(Vector3.back);

        public WorkshopLocalFaultPresenter(WorkshopEquipmentModel model, Dictionary<string, WorkshopFaultBinding> bindings)
        {
            this.model = model; this.bindings = bindings;
            rotorHome = model.Rotor ? model.Rotor.localRotation : Quaternion.identity;
            water = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            water.SetColor("_BaseColor", new Color(.05f, .48f, .65f));
            water.SetFloat("_Smoothness", .85f);
            water.SetFloat("_Cull", 0);
            jointWet = new GameObject("Local seal wetting");
            jointWet.transform.SetParent(model.transform, false);
            jointWetMesh = WorkshopFaultPresenter.Patch("Local seal gravity streak", (angle, r) =>
                new Vector3(Mathf.Cos(angle) * .018f * r, Mathf.Sin(angle) * .03f * r - .018f, 0));
            jointWet.AddComponent<MeshFilter>().sharedMesh = jointWetMesh;
            jointWet.AddComponent<MeshRenderer>().sharedMaterial = water;
            jointWet.SetActive(false);
            for (int i = 0; i < drops.Length; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = "Local joint residual droplet " + i;
                go.transform.SetParent(model.transform, false);
                Object.Destroy(go.GetComponent<Collider>());
                go.GetComponent<Renderer>().sharedMaterial = water;
                go.SetActive(false);
                drops[i] = go;
            }
            CreateMechanismOverlays();
        }

        public void Apply(WorkshopFaultFrame frame, bool live)
        {
            var next = WorkshopFaultCase.Find(frame.Code);
            if (next != spec)
            {
                ResetVisual();
                spec = next;
                if (spec == null || !bindings.TryGetValue(spec.Binding, out binding)) return;
                if (frame.Code == "P-ALIGN")
                    couplingContext = binding.Surface.parent.GetComponentsInChildren<Renderer>(true);
                if (frame.Code == "P-IMPELLER" || frame.Code == "P-BEAR")
                    for (int i = 0; i < model.AssemblyCount; i++)
                    {
                        var node = model.AssemblyTransform(i);
                        if (node.name == (frame.Code == "P-IMPELLER" ? "PMP_07_Impeller" : "PMP_11_BearingSet"))
                            pumpContext = node.GetComponentsInChildren<Renderer>(true);
                    }
                CacheSurface();
                if (frame.Code == "P-IMPELLER") CacheBladeWear();
                if (frame.Code == "P-BEAR") CacheRaceWear();
                if (frame.Code == "P-INLET") CacheInletRestriction();
                if (frame.Code == "TK-CORROSION")
                {
                    thinnedShell = binding.Surface.GetComponent<MeshFilter>();
                    intactShell = thinnedShell.sharedMesh;
                    var asset = Resources.Load<GameObject>("WorkshopFaults/tank-shell-thinned");
                    if (asset)
                        foreach (var filter in asset.GetComponentsInChildren<MeshFilter>(true))
                            if (filter.name == thinnedShell.name) ThinnedMesh = filter.sharedMesh;
                    if (!ThinnedMesh || (ThinnedMesh.bounds.center - intactShell.bounds.center).sqrMagnitude > 1e-10f ||
                        (ThinnedMesh.bounds.size - intactShell.bounds.size).sqrMagnitude > 1e-10f ||
                        ThinnedMesh.subMeshCount != intactShell.subMeshCount)
                        throw new System.InvalidOperationException("储罐局部减薄网格缺失或坐标不匹配");
                }
                foreach (var r in binding.Renderers)
                {
                    var original = new MaterialPropertyBlock();
                    r.GetPropertyBlock(original); originals[r] = original;
                }
                moving = frame.Code == "F-IMBAL" ? model.Rotor : binding.Surface;
                origin = moving.localPosition;
                if (frame.Code == "TK-LEVEL")
                {
                    liquidScale = binding.Surface.localScale;
                    liquidPosition = binding.Surface.localPosition;
                }
            }
            if (!Available) return;
            liveFrame = live;
            rotorFrame = frame;
            if (live && SectionOpen) SetSection(false);
            if (frame.Code == "P-IMPELLER") ApplyBladeWear(frame.Progress <= 0 ? 0 : frame.Activity);
            if (frame.Code == "P-BEAR") ApplyRaceWear(frame.Progress <= 0 ? 0 : frame.Activity);
            if (frame.Code == "P-INLET") ApplyInletRestriction(frame.Progress <= 0 ? 0 : frame.Activity);
            if (thinnedShell) thinnedShell.sharedMesh = frame.Progress >= .25f ? ThinnedMesh : intactShell;
            float intensity = frame.Progress <= 0 ? 0 : frame.Activity;
            foreach (var pair in originals)
            {
                var r = pair.Key;
                r.SetPropertyBlock(pair.Value);
                if (intensity == 0 || (spec.Effect == "thermal" && !ThermalVisible)) continue;
                if (spec.Effect == "crack" || spec.Effect == "orbit" || spec.Effect == "offset" ||
                    spec.Effect == "reverse" || spec.Effect == "level" || spec.Effect == "signal" ||
                    spec.Effect == "restriction" || spec.Effect == "wear" || spec.Effect == "erosion" ||
                    spec.Effect == "corrosion" || spec.Effect == "leak") continue;
                r.GetPropertyBlock(block);
                var material = r.sharedMaterial;
                Color rest = material && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.gray;
                Color target = spec.Effect == "thermal" ? Color.Lerp(new Color(1, .75f, .15f), new Color(1, .12f, .02f),
                    Mathf.InverseLerp(35, 100, frame.Temperature)) :
                    spec.Effect == "signal" ? new Color(1, .60f, .05f) :
                    spec.Effect == "restriction" ? new Color(.21f, .24f, .20f) :
                    spec.Effect == "wear" || spec.Effect == "corrosion" ? new Color(.47f, .22f, .07f) :
                    new Color(.05f, .38f, .45f);
                float weight = spec.Effect == "thermal" ? Mathf.InverseLerp(25, 100, frame.Temperature) : intensity * .7f;
                // Adjacent insulator receives a weaker thermal overlay than the contact.
                if (r != binding.Renderers[0]) weight *= .35f;
                block.SetColor("_BaseColor", Color.Lerp(rest, target, weight));
                block.SetColor("_EmissionColor", spec.Effect == "signal" ? target * intensity * .4f : Color.black);
                r.SetPropertyBlock(block);
            }
            if (moving)
            {
                moving.localPosition = origin;
                if (spec.Effect == "orbit" && frame.Code != "F-IMBAL" && frame.Running && frame.Progress > 0)
                {
                    float phase = frame.Seconds * 2 * Mathf.PI * Mathf.Min(frame.Rpm / 60, 8);
                    float amplitude = .00025f * frame.Vibration * MotionScale;
                    moving.localPosition = origin + new Vector3(Mathf.Sin(phase), Mathf.Cos(phase) * .7f, 0) * amplitude;
                }
                else if (spec.Effect == "offset")
                    moving.localPosition = origin + Vector3.down * (.0015f * intensity * MotionScale);
            }
            if (spec.Effect == "level")
            {
                float ratio = frame.Level / 65;
                binding.Surface.localScale = Vector3.Scale(liquidScale, new Vector3(1, 1, ratio));
                var mesh = binding.Surface.GetComponent<MeshFilter>();
                if (mesh)
                {
                    float bottom = mesh.sharedMesh.bounds.min.z;
                    binding.Surface.localPosition = liquidPosition +
                        binding.Surface.localRotation * new Vector3(0, 0, bottom * liquidScale.z * (1 - ratio));
                }
                liquidChanged = true;
            }
            bool jointLeak = surfaceAvailable && spec.Effect == "leak" && intensity > 0 && frame.Progress > .25f;
            jointWet.SetActive(jointLeak);
            Vector3 leakPoint = surfaceAvailable ? binding.Settings.anchorCalibrated ? binding.Anchor :
                binding.Surface.TransformPoint(surfacePoint) : Vector3.zero;
            Vector3 outward = model.transform.TransformDirection(Vector3.back).normalized;
            if (jointLeak)
            {
                jointWet.transform.SetPositionAndRotation(leakPoint + outward * (.003f * model.transform.lossyScale.x),
                    Quaternion.LookRotation(outward, Vector3.up));
                jointWet.transform.localScale = Vector3.one * intensity;
            }
            for (int i = 0; i < drops.Length; i++)
            {
                bool active = jointLeak;
                drops[i].SetActive(active);
                if (!active) continue;
                float t = Mathf.Repeat(frame.Seconds * .9f + i * .137f, 1);
                float scale = model.transform.lossyScale.x;
                drops[i].transform.position = leakPoint + outward * ((.003f + t * .045f) * scale) +
                    Vector3.down * t * t * .18f * scale;
                drops[i].transform.localScale = new Vector3(.004f, .007f, .004f) * intensity;
            }
            ApplyMechanismOverlays(frame);
            if (frame.Code == "F-IMBAL") SyncRotorPhase();
            if (!live && model.Kind == DeviceKind.Cabinet)
            {
                Expose("CAB_03_Door");
                Expose("CAB_08_ControlPanel");
                Expose("CAB_15_OperatorHMI");
                if (frame.Code != "PLC-HOT") Expose("CAB_06_Cooling");
            }
            if (!live && frame.Code == "P-ALIGN") Expose("PMP_03_Guard");
            if (!live && model.Kind == DeviceKind.Fan &&
                (frame.Code == "F-CRACK" || frame.Code == "F-BLADE" || frame.Code == "F-RUB")) Expose("FAN_03_InletGuard");
        }

        internal void SyncRotorPhase()
        {
            if (spec?.Code != "F-IMBAL" || !moving) return;
            var radial = (Quaternion.Inverse(rotorHome) * moving.localRotation) * Vector3.right;
            rotorPhase = Mathf.Atan2(radial.y, radial.x);
            float amplitude = rotorFrame.Running && rotorFrame.Progress > 0 ?
                .00025f * rotorFrame.Vibration * MotionScale : 0;
            moving.localPosition = origin +
                rotorHome * new Vector3(Mathf.Cos(rotorPhase), Mathf.Sin(rotorPhase) * .7f, 0) * amplitude;
            ApplyMechanismOverlays(rotorFrame);
        }

        public void SetSection(bool opened)
        {
            if (SectionOpen == opened || (opened && !CanShowSection)) return;
            foreach (var pair in sectionHidden) if (pair.Key) pair.Key.enabled = pair.Value;
            sectionHidden.Clear();
            SectionOpen = opened;
            if (!opened) return;
            for (int i = 0; i < model.AssemblyCount; i++)
            {
                var node = model.AssemblyTransform(i);
                bool occludes = spec.Code == "P-IMPELLER" ?
                    node.name == "PMP_04_Volute" || node.name == "PMP_05_InletCover" ||
                        node.name == "PMP_09_ImpellerFrontShroud" :
                    node.name == "PMP_10_BearingHousing" || node.name == "PMP_03_Guard";
                if (!occludes) continue;
                foreach (var r in node.GetComponentsInChildren<Renderer>(true)) HideSection(r);
            }
        }

        void HideSection(Renderer renderer)
        {
            if (!sectionHidden.ContainsKey(renderer)) sectionHidden.Add(renderer, renderer.enabled);
            renderer.enabled = false;
        }

        void Expose(string assembly)
        {
            for (int i = 0; i < model.AssemblyCount; i++)
            {
                var node = model.AssemblyTransform(i);
                if (node.name != assembly) continue;
                foreach (var r in node.GetComponentsInChildren<Renderer>(true))
                {
                    if (!hidden.ContainsKey(r)) hidden[r] = r.enabled;
                    r.enabled = false;
                }
            }
        }

        public void SetDetailLayer(int? layer)
        {
            if (!layer.HasValue)
            {
                foreach (var pair in layers) if (pair.Key) pair.Key.layer = pair.Value;
                layers.Clear(); return;
            }
            if (!Available) return;
            foreach (var r in binding.Renderers) Layer(r.gameObject, layer.Value);
            if (pumpContext != null)
                foreach (var r in pumpContext) Layer(r.gameObject, layer.Value);
            if (spec.Code == "P-INLET" && inletScreen)
            {
                foreach (var r in suction.Renderers) Layer(r.gameObject, layer.Value);
                Layer(inletScreen, layer.Value);
                Layer(inletDeposit, layer.Value);
            }
            foreach (var go in drops) Layer(go, layer.Value);
            Layer(jointWet, layer.Value);
            foreach (var line in mechanismLines) Layer(line.gameObject, layer.Value);
            // Nearby wires are context only, never reassigned outside this model instance.
            if (model.Kind == DeviceKind.Cabinet)
            {
                var focus = FocusBounds;
                focus.Expand(.06f * model.transform.lossyScale.x);
                foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
                    if (r.bounds.Intersects(focus)) Layer(r.gameObject, layer.Value);
            }
        }

        void Layer(GameObject go, int layer)
        {
            if (!layers.ContainsKey(go)) layers[go] = go.layer;
            go.layer = layer;
        }

        public void ResetVisual()
        {
            SetSection(false);
            SetDetailLayer(null);
            if (wornBladeFilter) wornBladeFilter.sharedMesh = intactBlade;
            bladeActivity = -1;
            if (raceFilter) raceFilter.sharedMesh = intactRace;
            raceActivity = -1;
            if (inletScreen) inletScreen.SetActive(false);
            if (inletDeposit) inletDeposit.SetActive(false);
            inletActivity = -1;
            foreach (var pair in originals) if (pair.Key) pair.Key.SetPropertyBlock(pair.Value);
            originals.Clear();
            foreach (var pair in hidden) if (pair.Key) pair.Key.enabled = pair.Value;
            hidden.Clear();
            if (thinnedShell) thinnedShell.sharedMesh = intactShell;
            thinnedShell = null;
            intactShell = null;
            if (moving) moving.localPosition = origin;
            if (liquidChanged && binding != null)
            {
                binding.Surface.localScale = liquidScale;
                binding.Surface.localPosition = liquidPosition;
            }
            liquidChanged = false;
            ResetMechanismOverlays();
            jointWet.SetActive(false);
            jointWet.transform.localPosition = Vector3.zero;
            jointWet.transform.localRotation = Quaternion.identity;
            jointWet.transform.localScale = Vector3.one;
            foreach (var drop in drops)
            {
                if (!drop) continue;
                drop.SetActive(false);
                drop.transform.localPosition = Vector3.zero;
                drop.transform.localRotation = Quaternion.identity;
                drop.transform.localScale = Vector3.one;
            }
            moving = null; binding = null; spec = null;
            couplingContext = null;
            pumpContext = null;
            liveFrame = false;
        }

        public void DisposeOwnedResources()
        {
            ResetVisual();
            foreach (var drop in drops) if (drop) Object.Destroy(drop);
            if (jointWet) Object.Destroy(jointWet);
            if (jointWetMesh) Object.Destroy(jointWetMesh);
            foreach (var line in mechanismLines) if (line) Object.Destroy(line.gameObject);
            if (mechanismMaterial) Object.Destroy(mechanismMaterial);
            if (water) Object.Destroy(water);
            if (wornBlade) Object.Destroy(wornBlade);
            if (wornRace) Object.Destroy(wornRace);
            if (inletScreen) Object.Destroy(inletScreen);
            if (inletDeposit) Object.Destroy(inletDeposit);
            if (screenMesh) Object.Destroy(screenMesh);
            if (depositMesh) Object.Destroy(depositMesh);
            if (screenMaterial) Object.Destroy(screenMaterial);
            if (depositMaterial) Object.Destroy(depositMaterial);
        }
    }
}
