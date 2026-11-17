using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorkshopDemo
{
    internal sealed class WorkshopFaultPresenter
    {
        readonly WorkshopEquipmentModel model;
        readonly WorkshopFaultBinding leak;
        readonly MeshFilter shell;
        internal Mesh IntactMesh { get; }
        internal Mesh PerforatedMesh { get; }
        readonly Transform gaugeLiquid, gaugeMeniscus;
        readonly Vector3 liquidHome, liquidScale, meniscusHome;
        readonly float gaugeBase;
        readonly Dictionary<GameObject, int> detailLayers = new Dictionary<GameObject, int>();
        readonly Transform[] drops = new Transform[12];
        GameObject effects;
        Transform stain, puddle;
        Material wetMaterial, waterMaterial;
        Mesh stainMesh, puddleMesh, dropMesh;
        int? detailLayer;
        WorkshopFaultFrame? lastFrame;

        public WorkshopFaultPresenter(WorkshopEquipmentModel model, WorkshopFaultBinding leak, WorkshopFaultBinding gauge)
        {
            this.model = model;
            this.leak = leak;
            shell = leak.Surface.GetComponent<MeshFilter>();
            IntactMesh = shell.sharedMesh;
            var asset = Resources.Load<GameObject>("WorkshopFaults/tank-shell-perforated");
            PerforatedMesh = asset ? asset.GetComponentsInChildren<MeshFilter>(true)
                .Single(f => f.name == shell.name).sharedMesh : null;
            if (!PerforatedMesh || (PerforatedMesh.bounds.center - IntactMesh.bounds.center).sqrMagnitude > 1e-10f ||
                (PerforatedMesh.bounds.size - IntactMesh.bounds.size).sqrMagnitude > 1e-10f ||
                PerforatedMesh.subMeshCount != IntactMesh.subMeshCount)
                throw new System.InvalidOperationException("储罐派生穿孔网格缺失或坐标不匹配: intact=" +
                    IntactMesh.bounds + " / " + IntactMesh.subMeshCount + ", derived=" +
                    (PerforatedMesh ? PerforatedMesh.bounds + " / " + PerforatedMesh.subMeshCount : "missing"));
            gaugeLiquid = gauge.Renderers[0].transform;
            gaugeMeniscus = gauge.Renderers[1].transform;
            liquidHome = gaugeLiquid.localPosition;
            liquidScale = gaugeLiquid.localScale;
            meniscusHome = gaugeMeniscus.localPosition;
            gaugeBase = liquidHome.z + gauge.Renderers[0].localBounds.extents.z * liquidScale.z;
        }

        public Vector3 Normal => leak.Normal;
        public Bounds FocusBounds => leak.FocusBounds();
        internal Bounds CloseupBounds
        {
            get
            {
                float scale = model.transform.TransformVector(Vector3.up).magnitude;
                return new Bounds(leak.Anchor - Vector3.up * (.01f * scale),
                    new Vector3(.06f, .075f, .06f) * scale);
            }
        }
        internal GameObject Effects => effects;
        internal Mesh StainMesh => stainMesh;

        public void Apply(WorkshopFaultFrame frame)
        {
            if (lastFrame.HasValue && lastFrame.Value.Progress == frame.Progress &&
                lastFrame.Value.Level == frame.Level && (frame.Drip == 0 || lastFrame.Value.Seconds == frame.Seconds))
                return;
            lastFrame = frame;
            shell.sharedMesh = frame.Progress >= .25f ? PerforatedMesh : IntactMesh;
            if (frame.Progress <= 0)
            {
                ResetVisual();
                return;
            }
            if (!effects) CreateEffects();
            effects.SetActive(true);
            float scale = model.transform.TransformVector(Vector3.up).magnitude;
            Vector3 anchor = leak.Anchor + Normal * (.0006f * scale);
            Vector3 floor = model.transform.TransformPoint(new Vector3(0, model.NativeBounds.min.y + .009f, 0));
            float height = Mathf.Max(.01f, anchor.y - floor.y);
            stain.SetPositionAndRotation(anchor, Quaternion.LookRotation(Normal, Vector3.up));
            stain.localScale = new Vector3(Mathf.Lerp(.5f, .8f, frame.Wetness), frame.Wetness, 1);
            puddle.gameObject.SetActive(frame.Pool > .001f);
            puddle.position = new Vector3(anchor.x, floor.y, anchor.z) + Normal * (.25f * scale);
            puddle.localScale = new Vector3(frame.Pool, 1, frame.Pool);

            float fallSeconds = Mathf.Sqrt(2 * height / (9.81f * scale));
            for (int i = 0; i < drops.Length; i++)
            {
                bool visible = i < frame.Drip * drops.Length && frame.Drip > .008f;
                drops[i].gameObject.SetActive(visible);
                if (!visible) continue;
                float u = Mathf.Repeat(frame.Seconds / fallSeconds + (i + 1) * .618034f, 1);
                float drift = Mathf.Lerp(.025f, .065f, frame.Drip) * (1 + .16f * Mathf.Sin(i * 13.1f));
                drops[i].position = anchor + Normal * (u * drift * scale) - Vector3.up * (height * u * u) +
                    Vector3.Cross(Vector3.up, Normal) * (Mathf.Sin(i * 4.9f) * .004f * u * scale);
                drops[i].localScale = new Vector3(.0022f, .0032f + .0035f * u, .0022f) * (1 + .25f * frame.Drip);
            }
            float ratio = Mathf.Clamp01(frame.Level / 65);
            gaugeLiquid.localScale = new Vector3(liquidScale.x, liquidScale.y, liquidScale.z * ratio);
            gaugeLiquid.localPosition = new Vector3(liquidHome.x, liquidHome.y, Mathf.Lerp(gaugeBase, liquidHome.z, ratio));
            gaugeMeniscus.localPosition = new Vector3(meniscusHome.x, meniscusHome.y, Mathf.Lerp(gaugeBase, meniscusHome.z, ratio));
        }

        public void ResetVisual()
        {
            lastFrame = null;
            shell.sharedMesh = IntactMesh;
            if (effects) effects.SetActive(false);
            gaugeLiquid.localPosition = liquidHome;
            gaugeLiquid.localScale = liquidScale;
            gaugeMeniscus.localPosition = meniscusHome;
        }

        public void SetDetailLayer(int? layer)
        {
            if (detailLayer == layer) return;
            foreach (var pair in detailLayers)
                if (pair.Key) pair.Key.layer = pair.Value;
            detailLayers.Clear();
            detailLayer = layer;
            if (!layer.HasValue) return;
            foreach (var renderer in leak.Renderers) AddToDetail(renderer.gameObject);
            if (effects)
                foreach (var renderer in effects.GetComponentsInChildren<MeshRenderer>(true)) AddToDetail(renderer.gameObject);
        }

        void AddToDetail(GameObject target)
        {
            if (!detailLayer.HasValue) return;
            detailLayers.Add(target, target.layer);
            target.layer = detailLayer.Value;
        }

        void CreateEffects()
        {
            effects = new GameObject("Tank wall leak effects");
            effects.transform.SetParent(model.transform, false);
            effects.layer = model.gameObject.layer;
            var shader = leak.Renderers[0].sharedMaterial.shader;
            // Use the same compiled URP Lit variant already used by the shell.
            wetMaterial = LiquidMaterial(shader, "Wet steel", new Color(.09f, .20f, .21f), .88f);
            waterMaterial = LiquidMaterial(shader, "Teaching water", new Color(.34f, .72f, .82f), .88f);
            stainMesh = Stain();
            puddleMesh = Patch("Floor wetting", (angle, r) =>
                new Vector3(Mathf.Cos(angle) * .32f * r, 0, -Mathf.Sin(angle) * .23f * r));
            dropMesh = Droplet();
            stain = Effect("Wall wetting", stainMesh, wetMaterial);
            puddle = Effect("Floor collection", puddleMesh, wetMaterial);
            for (int i = 0; i < drops.Length; i++) drops[i] = Effect("Drop " + (i + 1), dropMesh, waterMaterial);
        }

        internal static Material LiquidMaterial(Shader shader, string name, Color color, float smoothness)
        {
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", .18f);
            material.SetFloat("_Cull", 0);
            return material;
        }

        internal static Mesh Patch(string name, System.Func<float, float, Vector3> point)
        {
            const int segments = 32;
            var vertices = new Vector3[segments + 1];
            var triangles = new int[segments * 3];
            vertices[0] = point(0, 0);
            for (int i = 0; i < segments; i++)
            {
                float angle = i * (2 * Mathf.PI / segments);
                vertices[i + 1] = point(angle, 1 + .16f * Mathf.Sin(angle * 3) + .09f * Mathf.Cos(angle * 7));
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = i + 1;
                triangles[i * 3 + 2] = (i + 1) % segments + 1;
            }
            var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            return mesh;
        }

        static Mesh Stain()
        {
            const int segments = 48;
            var vertices = new Vector3[segments * 2];
            var triangles = new int[segments * 6];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * (2 * Mathf.PI / segments);
                float irregular = 1 + .045f * Mathf.Sin(angle * 3) + .02f * Mathf.Cos(angle * 7);
                float x = Mathf.Cos(angle);
                float y = Mathf.Sin(angle);
                float innerX = x * .0065f;
                float innerY = y * .007f;
                float outerX = x * .020f * irregular;
                float outerY = y * .023f * irregular;
                vertices[i * 2] = new Vector3(innerX, innerY, -.30f * innerX * innerX);
                vertices[i * 2 + 1] = new Vector3(outerX, outerY, -.30f * outerX * outerX);

                int next = (i + 1) % segments;
                int index = i * 6;
                int inner = i * 2, outer = inner + 1, nextInner = next * 2, nextOuter = nextInner + 1;
                triangles[index] = inner;
                triangles[index + 1] = outer;
                triangles[index + 2] = nextOuter;
                triangles[index + 3] = inner;
                triangles[index + 4] = nextOuter;
                triangles[index + 5] = nextInner;
            }
            var mesh = new Mesh { name = "Wall wetting with exposed perforation" };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            return mesh;
        }

        static Mesh Droplet()
        {
            const int rings = 8, segments = 12;
            var vertices = new Vector3[(rings + 1) * (segments + 1)];
            var triangles = new List<int>(rings * segments * 6);
            for (int ring = 0; ring <= rings; ring++)
            {
                float angle = Mathf.PI * ring / rings;
                float y = Mathf.Cos(angle);
                float radius = Mathf.Sin(angle) * (.8f - .2f * y);
                for (int segment = 0; segment <= segments; segment++)
                {
                    float around = 2 * Mathf.PI * segment / segments;
                    int index = ring * (segments + 1) + segment;
                    vertices[index] = new Vector3(Mathf.Cos(around) * radius, y, Mathf.Sin(around) * radius);
                    if (ring == rings || segment == segments) continue;
                    int next = index + segments + 1;
                    if (ring > 0) triangles.AddRange(new[] { index, index + 1, next });
                    if (ring < rings - 1) triangles.AddRange(new[] { index + 1, next + 1, next });
                }
            }
            var mesh = new Mesh { name = "Rounded water droplet", vertices = vertices, triangles = triangles.ToArray() };
            mesh.RecalculateNormals();
            return mesh;
        }

        Transform Effect(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name);
            go.transform.SetParent(effects.transform, false);
            go.layer = effects.layer;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            AddToDetail(go);
            return go.transform;
        }

        public void DisposeOwnedResources()
        {
            if (!effects) return;
            Object.Destroy(stainMesh);
            Object.Destroy(puddleMesh);
            Object.Destroy(dropMesh);
            Object.Destroy(wetMaterial);
            Object.Destroy(waterMaterial);
        }
    }
}
