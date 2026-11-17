using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace WorkshopDemo
{
    internal sealed class WorkshopPumpFaultPresenter
    {
        readonly WorkshopEquipmentModel model;
        readonly WorkshopFaultBinding chamber, faces, inlet;
        readonly Transform shaft, impeller, ring, needle;
        readonly Quaternion shaftRest, impellerRest, ringRest, needleRest;
        readonly Vector3 ringHome, ringPivot, ringAxis, needleHome, needlePivot, needleAxis, impellerHome, impellerAxis;
        readonly MeshRenderer[] inletAttachments;
        readonly MeshRenderer[] sealContext;
        readonly Dictionary<MeshRenderer, bool> hidden = new Dictionary<MeshRenderer, bool>();
        readonly Dictionary<GameObject, int> detailLayers = new Dictionary<GameObject, int>();
        readonly Transform[] particles = new Transform[18];
        GameObject root, sealEffects, cavitationEffects;
        Transform wear, wet;
        Material markMaterial, liquidMaterial, bubbleMaterial;
        Mesh particleMesh, wetMesh, wearMesh;
        AudioSource audio;
        AudioClip sound;
        int? detailLayer;
        WorkshopFaultFrame? frame;
        bool live;

        public WorkshopPumpFaultPresenter(WorkshopEquipmentModel model, WorkshopFaultBinding chamber,
            WorkshopFaultBinding faces, WorkshopFaultBinding inlet, WorkshopFaultBinding gauge, Transform impeller)
        {
            this.model = model;
            this.chamber = chamber;
            this.faces = faces;
            this.inlet = inlet;
            this.impeller = impeller;
            shaft = model.Rotor;
            ring = faces.Renderers[1].transform;
            needle = gauge.Renderers[0].transform;
            shaftRest = shaft.localRotation;
            impellerRest = impeller.localRotation;
            impellerHome = impeller.localPosition;
            impellerAxis = impeller.InverseTransformDirection(model.transform.right);
            ringRest = ring.localRotation;
            needleRest = needle.localRotation;
            ringHome = ring.localPosition;
            needleHome = needle.localPosition;
            ringPivot = ring.parent.InverseTransformPoint(faces.Renderers[1].bounds.center);
            ringAxis = ring.parent.InverseTransformDirection(model.transform.right);
            needlePivot = needle.parent.InverseTransformPoint(gauge.Renderers[1].bounds.center);
            needleAxis = needle.parent.InverseTransformDirection(model.transform.forward);
            inletAttachments = model.GetComponentsInChildren<MeshRenderer>().Where(IsInletAttachment).ToArray();
            sealContext = model.GetComponentsInChildren<MeshRenderer>()
                .Where(IsSealContext).ToArray();
        }

        static bool IsSealContext(MeshRenderer renderer) =>
            renderer.name == "PMP_06_SealBearing_BearingHousing" ||
            renderer.name == "PMP_06_SealBearing_SupportFoot" ||
            renderer.name == "PMP_06_SealBearing_PedestalWeb" ||
            renderer.name == "PMP_06_SealBearing_BearingInnerRace" ||
            renderer.name == "PMP_06_SealBearing_BearingOuterRace" ||
            renderer.name.StartsWith("PMP_02_Coupling_PumpShaft", StringComparison.Ordinal);

        static bool IsInletAttachment(MeshRenderer renderer)
        {
            string name = renderer.name;
            return name.StartsWith("PMP_05_InletCover_Bolt_", StringComparison.Ordinal) ||
                name == "PMP_05_InletCover_ORing" || name == "PMP_05_InletCover_RegisterORing" ||
                name.StartsWith("PMP_04_Volute_Data", StringComparison.Ordinal) ||
                name.StartsWith("PMP_04_Volute_Plate", StringComparison.Ordinal);
        }

        bool IsSeal => frame?.Code == "P-SEAL";
        WorkshopFaultBinding Focus => IsSeal ? chamber : inlet;
        public Bounds FocusBounds
        {
            get
            {
                var bounds = Focus.FocusBounds();
                if (IsSeal)
                    foreach (var renderer in sealContext)
                        if (renderer && renderer.enabled) bounds.Encapsulate(renderer.bounds);
                return bounds;
            }
        }
        public Bounds CloseupBounds => IsSeal ? Focus.FocusBounds() : FocusBounds;
        public Vector3 Normal => IsSeal ? model.transform.TransformDirection(new Vector3(.26f, 0, -.966f)) : inlet.Normal;
        public bool SectionOpen { get; private set; }
        internal GameObject Effects => root;
        internal AudioSource SoundSource => audio;
        internal GameObject SealEffects => sealEffects;
        internal GameObject CavitationEffects => cavitationEffects;

        public void Apply(WorkshopFaultFrame value, bool liveFrame)
        {
            if (frame.HasValue && frame.Value.Code != value.Code) ResetVisual();
            frame = value;
            live = liveFrame;
            if (IsSeal) ApplySeal(value);
            else ApplyCavitation(value);
            if (!live) ApplyRotation(value);
        }

        public void SyncLiveRotation()
        {
            PoseRotatingParts(Quaternion.Inverse(shaftRest) * shaft.localRotation);
        }

        void ApplyRotation(WorkshopFaultFrame value)
        {
            float seconds = IsSeal ? Mathf.Min(value.Seconds, WorkshopFaultProfile.PumpSeal.SecondProgress *
                WorkshopFaultProfile.PumpSeal.Duration) : value.Seconds;
            float degreesPerSecond = IsSeal ? 188.5f : 160.225f;
            Quaternion shaftMotion = Quaternion.AngleAxis(seconds * degreesPerSecond, Vector3.right);
            shaft.localRotation = shaftRest * shaftMotion;
            PoseRotatingParts(shaftMotion);
        }

        void PoseRotatingParts(Quaternion shaftMotion)
        {
            impeller.localRotation = impellerRest * Quaternion.AngleAxis(shaftMotion.eulerAngles.x, impellerAxis);
            var motion = Quaternion.AngleAxis(shaftMotion.eulerAngles.x, ringAxis);
            ring.localPosition = ringPivot + motion * (ringHome - ringPivot);
            ring.localRotation = motion * ringRest;
        }

        public void SetSection(bool opened)
        {
            if (SectionOpen == opened || (opened && (!frame.HasValue || live))) return;
            RestoreSection();
            SectionOpen = opened;
            if (opened)
            {
                if (IsSeal) Hide(chamber.Renderers[0]);
                else
                {
                    foreach (var renderer in inlet.Renderers) Hide(renderer);
                    foreach (var renderer in inletAttachments) Hide(renderer);
                }
            }
            if (frame.HasValue) Apply(frame.Value, live);
        }

        void Hide(MeshRenderer renderer)
        {
            if (hidden.TryAdd(renderer, renderer.enabled)) renderer.enabled = false;
        }

        void RestoreSection()
        {
            foreach (var pair in hidden)
                if (pair.Key) pair.Key.enabled = pair.Value;
            hidden.Clear();
            SectionOpen = false;
        }

        void ApplySeal(WorkshopFaultFrame value)
        {
            if (value.Progress <= 0)
            {
                if (sealEffects) sealEffects.SetActive(false);
                return;
            }
            if (!sealEffects) CreateSeal();
            sealEffects.SetActive(true);
            wear.gameObject.SetActive(SectionOpen);
            markMaterial.SetColor("_BaseColor", Color.Lerp(new Color(.41f, .38f, .33f),
                new Color(.24f, .11f, .05f), value.Wetness));
            wet.gameObject.SetActive(value.Drip > .025f);
            var anchor = model.transform.InverseTransformPoint(chamber.Anchor);
            wet.localPosition = anchor + new Vector3(-.003f, -.020f, -.010f);
            for (int i = 0; i < sealEffects.transform.childCount - 2; i++)
            {
                var drop = sealEffects.transform.GetChild(i + 2);
                float t = Mathf.Repeat(value.Seconds * 3.4f + i * .213f, 1);
                drop.gameObject.SetActive(value.Drip > .06f && i < 1 + value.Drip * 6);
                drop.localPosition = anchor + new Vector3(-.008f - .009f * t, -.060f - .223f * t * t,
                    -.012f - .039f * Mathf.Clamp01(t * 2));
                drop.localScale = new Vector3(.0032f, .004f + .006f * t, .0032f);
            }
        }

        void ApplyCavitation(WorkshopFaultFrame value)
        {
            float angle = Mathf.Clamp((value.Pressure - .32f) * 230, -68, 12);
            var deflection = Quaternion.AngleAxis(angle, needleAxis);
            var vibration = model.transform.TransformVector(new Vector3(0, 0,
                Mathf.Sin(value.Seconds * 47) * value.Activity * .0013f));
            needle.localPosition = needlePivot + deflection * (needleHome - needlePivot);
            needle.localRotation = deflection * needleRest;
            // Move the display assembly through the established impeller parent, independently of shaft rotation.
            impeller.localPosition = impellerHome + impeller.parent.InverseTransformVector(vibration);
            bool show = SectionOpen && value.Activity > .002f;
            if (!show)
            {
                if (cavitationEffects) cavitationEffects.SetActive(false);
                return;
            }
            if (!cavitationEffects) CreateCavitation();
            cavitationEffects.SetActive(true);
            Vector3 eye = model.transform.InverseTransformPoint(inlet.Anchor);
            for (int i = 0; i < particles.Length; i++)
            {
                float phase = Mathf.Repeat(value.Seconds * (1.48f + (i % 3) * .13f) + i * .618034f, 1);
                float turn = i * 2.399963f + phase * .6f;
                float radius = Mathf.Lerp(.028f + .014f * (i % 4), .121f, Mathf.InverseLerp(.53f, 1, phase));
                var bubble = particles[i];
                bubble.gameObject.SetActive(i < Mathf.CeilToInt(value.Activity * particles.Length));
                bubble.localPosition = eye + new Vector3(Mathf.Lerp(-.135f, .070f, phase),
                    Mathf.Cos(turn) * radius, Mathf.Sin(turn) * radius);
                float size = (.006f + .006f * value.Activity) * Mathf.Sin(Mathf.PI * phase);
                bubble.localScale = Vector3.one * Mathf.Max(.0001f, size);
            }
        }

        public void UpdateAudio(bool moving)
        {
            bool shouldPlay = moving && frame.HasValue && !IsSeal && frame.Value.Running &&
                frame.Value.Activity > .025f && AudioListener.volume > .01f;
            if (!shouldPlay)
            {
                if (audio && audio.isPlaying) audio.Stop();
                return;
            }
            if (!audio) CreateAudio();
            audio.volume = .13f * frame.Value.Activity;
            if (!audio.isPlaying) audio.Play();
        }

        public void ResetVisual()
        {
            RestoreSection();
            if (!frame.HasValue) return;
            if (!live) shaft.localRotation = shaftRest;
            impeller.localPosition = impellerHome;
            impeller.localRotation = impellerRest;
            ring.localPosition = ringHome;
            ring.localRotation = ringRest;
            needle.localPosition = needleHome;
            needle.localRotation = needleRest;
            if (sealEffects) sealEffects.SetActive(false);
            if (cavitationEffects) cavitationEffects.SetActive(false);
            frame = null;
            UpdateAudio(false);
        }

        public void SetDetailLayer(int? layer)
        {
            if (detailLayer == layer) return;
            foreach (var pair in detailLayers)
                if (pair.Key) pair.Key.layer = pair.Value;
            detailLayers.Clear();
            detailLayer = layer;
            if (!layer.HasValue || !frame.HasValue) return;
            foreach (var renderer in Focus.Renderers) Detail(renderer.gameObject);
            foreach (var renderer in (IsSeal ? faces : inlet).Renderers) Detail(renderer.gameObject);
            if (!IsSeal)
                foreach (var renderer in impeller.GetComponentsInChildren<MeshRenderer>()) Detail(renderer.gameObject);
            if (root)
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) Detail(renderer.gameObject);
        }

        void Detail(GameObject target)
        {
            if (detailLayer.HasValue && detailLayers.TryAdd(target, target.layer)) target.layer = detailLayer.Value;
        }

        GameObject Child(string name, Transform parent)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.layer = model.gameObject.layer;
            return child;
        }

        Transform Effect(string name, Transform parent, Mesh mesh, Material material)
        {
            var child = Child(name, parent);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            Detail(child);
            return child.transform;
        }

        void EnsureRoot()
        {
            if (root) return;
            root = Child("Pump fault presentation", model.transform);
            const int rings = 8, sides = 12;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int row = 0; row <= rings; row++)
                for (int col = 0; col <= sides; col++)
                {
                    float latitude = Mathf.PI * row / rings, azimuth = 2 * Mathf.PI * col / sides;
                    vertices.Add(new Vector3(Mathf.Sin(latitude) * Mathf.Cos(azimuth), Mathf.Cos(latitude),
                        Mathf.Sin(latitude) * Mathf.Sin(azimuth)));
                }
            for (int row = 0; row < rings; row++)
                for (int col = 0; col < sides; col++)
                {
                    int a = row * (sides + 1) + col, b = a + sides + 1;
                    triangles.AddRange(new[] { a, a + 1, b, a + 1, b + 1, b });
                }
            particleMesh = new Mesh { name = "Pump teaching fluid particle" };
            particleMesh.SetVertices(vertices);
            particleMesh.SetTriangles(triangles, 0);
            particleMesh.SetNormals(vertices);
            particleMesh.RecalculateBounds();
        }

        void CreateSeal()
        {
            EnsureRoot();
            sealEffects = Child("Seal wetting and drops", root.transform);
            var shader = chamber.Renderers[0].sharedMaterial.shader;
            markMaterial = WorkshopFaultPresenter.LiquidMaterial(shader, "Seal contact wear", Color.gray, .32f);
            liquidMaterial = WorkshopFaultPresenter.LiquidMaterial(shader, "Seal water", new Color(.14f, .43f, .55f), .85f);
            var vertices = new List<Vector3>();
            var faces = new List<int>();
            const int segments = 26;
            for (int i = 0; i <= segments; i++)
            {
                float a = Mathf.Lerp(2.5f, 5.4f, i / (float)segments);
                float r = .070f + .0008f * Mathf.Sin(i * 4);
                vertices.Add(new Vector3(0, r * Mathf.Cos(a), r * Mathf.Sin(a)));
                vertices.Add(new Vector3(0, (r + .003f) * Mathf.Cos(a), (r + .003f) * Mathf.Sin(a)));
                if (i > 0) faces.AddRange(new[] { 2*i - 2, 2*i, 2*i - 1, 2*i - 1, 2*i, 2*i + 1 });
            }
            wearMesh = new Mesh { name = "Local seal face wear arc" };
            wearMesh.SetVertices(vertices);
            wearMesh.SetTriangles(faces, 0);
            wearMesh.RecalculateNormals();
            wear = Effect("Contact wear arc", sealEffects.transform, wearMesh, markMaterial);
            var staticFace = this.faces.Renderers[0].bounds;
            wear.position = staticFace.center - model.transform.right * (staticFace.extents.x + .0006f);
            wetMesh = WorkshopFaultPresenter.Patch("Seal wet streak", (a, r) =>
                new Vector3(.003f * Mathf.Cos(a) * r, -.036f + .050f * Mathf.Sin(a) * r,
                    -.010f + .008f * Mathf.Cos(a) * r));
            wet = Effect("Seal water streak", sealEffects.transform, wetMesh, liquidMaterial);
            for (int i = 0; i < 7; i++) Effect("Seal drop " + (i + 1), sealEffects.transform, particleMesh, liquidMaterial);
        }

        void CreateCavitation()
        {
            EnsureRoot();
            cavitationEffects = Child("Inlet vapor bubbles", root.transform);
            bubbleMaterial = WorkshopFaultPresenter.LiquidMaterial(inlet.Renderers[0].sharedMaterial.shader,
                "Vapor bubble illustration", new Color(.36f, .72f, .84f), .92f);
            for (int i = 0; i < particles.Length; i++)
                particles[i] = Effect("Inlet vapor " + (i + 1), cavitationEffects.transform, particleMesh, bubbleMaterial);
        }

        void CreateAudio()
        {
            EnsureRoot();
            var emitter = Child("Teaching cavitation sound", root.transform);
            emitter.transform.position = inlet.Anchor;
            audio = emitter.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.loop = true;
            audio.spatialBlend = live ? 1 : 0;
            audio.maxDistance = 8;
            const int rate = 16000;
            sound = AudioClip.Create("Teaching cavitation crackle", rate, 1, rate, false);
            var samples = new float[rate];
            var random = new System.Random(481);
            float impulse = 0, last = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                if (random.NextDouble() < .006) impulse = .3f + (float)random.NextDouble() * .5f;
                float noise = (float)(2 * random.NextDouble() - 1);
                samples[i] = (noise - last) * (.03f + impulse) * .55f;
                impulse *= .982f;
                last = noise;
            }
            sound.SetData(samples, 0);
            audio.clip = sound;
        }

        public void DisposeOwnedResources()
        {
            foreach (var resource in new Object[] { particleMesh, wetMesh, wearMesh, markMaterial, liquidMaterial, bubbleMaterial, sound })
                if (resource) Object.Destroy(resource);
        }
    }
}
