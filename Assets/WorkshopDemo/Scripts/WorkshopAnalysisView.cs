using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WorkshopDemo
{
    // Renders the solved component separately from the uncalibrated factory art.
    public sealed partial class WorkshopAnalysisView : MonoBehaviour, IDragHandler, IScrollHandler, IPointerClickHandler
    {
        public WorkshopAnalysisResult Result { get; private set; }
        public int Model { get; private set; } = 1;
        public int Field { get; private set; }
        public float Factor { get; private set; } = 1;
        public float Magnification { get; private set; } = 1;
        public bool Deformed { get; private set; } = true;
        public bool Wire { get; private set; }
        public bool Section { get; set; }
        public bool Smooth = true, Contours = true, AutoRotate, Measuring;
        public float Cut;
        public int ProbeCount => samples.Count;
        public Action<string> Probe;
        GameObject scene, body;
        Camera viewCamera;
        RenderTexture target;
        Mesh mesh, wireMesh;
        Material material;
        MeshCollider surfaceCollider;
        MeshRenderer wireRenderer;
        int[] displayedFaces;
        float size, zoom = 2.8f;
        Vector3 center;
        RectTransform rect;
        static readonly Vector3 Origin = new Vector3(120, 120, 120);
        float DefaultZoom => Result.shape == "tank" || Result.shape == "shaft" || Result.shape == "pipe" ? 3.6f : 2.8f;

        public static Color Ramp(float t)
        {
            var colors = new[] { new Color(.10f,.22f,.85f), new Color(0,.73f,.95f),
                new Color(.15f,.84f,.37f), new Color(1,.88f,.08f), new Color(.96f,.13f,.08f) };
            float f = Mathf.Clamp01(t) * 4;
            return Color.Lerp(colors[Mathf.Min(3, (int)f)], colors[Mathf.Min(4, (int)f + 1)], f - Mathf.Min(3, (int)f));
        }

        public void Initialize(WorkshopAnalysisResult result)
        {
            Result = result;
            zoom = DefaultZoom;
            rect = (RectTransform)transform;
            Field = result.analysis == "thermal" ? 2 : 0;
            Magnification = result.displacementRange > 0 ? Mathf.Clamp(.04f *
                Length(result.models[0]) / (result.displacementRange / 1000), 1, 500) : 1;
            scene = new GameObject("FEM result camera and mesh");
            scene.transform.position = Origin;
            body = new GameObject("Solved component");
            body.layer = 31;
            body.transform.SetParent(scene.transform, false);
            body.transform.localRotation = Quaternion.Euler(22, -32, -12);
            mesh = new Mesh { name = "Solved tetrahedral boundary", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(Resources.Load<Shader>("WorkshopField"));
            body.AddComponent<MeshRenderer>().sharedMaterial = material;
            surfaceCollider = body.AddComponent<MeshCollider>();
            var wire = new GameObject("Element edges");
            wire.layer = 31;
            wire.transform.SetParent(body.transform, false);
            wireMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            wire.AddComponent<MeshFilter>().sharedMesh = wireMesh;
            wireRenderer = wire.AddComponent<MeshRenderer>();
            wireRenderer.sharedMaterial = material;
            var cameraObject = new GameObject("Analysis camera");
            cameraObject.transform.SetParent(scene.transform, false);
            viewCamera = cameraObject.AddComponent<Camera>();
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = new Color(.045f, .055f, .07f);
            viewCamera.cullingMask = (1 << 31) | (1 << 29);
            viewCamera.nearClipPlane = .01f;
            viewCamera.farClipPlane = 30;
            target = new RenderTexture(1800, 1200, 24) { antiAliasing = 4 };
            target.Create();
            viewCamera.targetTexture = target;
            GetComponent<RawImage>().texture = target;
            InitializeAnnotations();
            Rebuild();
        }

        static float Length(WorkshopAnalysisMesh m)
        {
            var b = new Bounds(m.Point(0), Vector3.zero);
            for (int i = 1; i < m.nodes; i++) b.Encapsulate(m.Point(i));
            return b.size.magnitude;
        }

        public void Configure(int model, int field, float factor, float scale, bool deformed, bool wire)
        {
            Model = model; Field = field; Factor = factor; Magnification = scale; Deformed = deformed; Wire = wire;
            Rebuild();
        }

        public void Rebuild()
        {
            if (Result == null) return;
            if (FractureActive) return;
            var m = Result.models[Model];
            var b = new Bounds(Result.models[0].Point(0), Vector3.zero);
            for (int i = 0; i < Result.models[0].nodes; i++) b.Encapsulate(Result.models[0].Point(i));
            center = b.center;
            size = b.size.magnitude;
            float normalization = 2.8f / size;
            var faces = new System.Collections.Generic.List<int>();
            for (int f = 0; f < m.tri.Length / 3; f++)
            {
                float section = (m.Point(m.tri[f*3]).z + m.Point(m.tri[f*3+1]).z + m.Point(m.tri[f*3+2]).z) / 3;
                if (!Section || section >= Cut * b.extents.z - 1e-7f) faces.Add(f);
            }
            int count = faces.Count * 3;
            var vertices = new Vector3[count];
            var colors = new Color[count];
            var indices = new int[count];
            var edges = new int[count * 2];
            var normals = new Vector3[count];
            var coordinates = new Vector2[count];
            Result.Range(Field, out float low, out float high);
            var averaged = new float[m.nodes];
            var weights = new float[m.nodes];
            var nodeNormals = new Vector3[m.nodes];
            for (int f = 0; f < m.tri.Length / 3; f++)
            {
                int a = m.tri[f*3], d = m.tri[f*3+2], c = m.tri[f*3+1];
                Vector3 n = Vector3.Cross(m.Point(d)-m.Point(a), m.Point(c)-m.Point(a));
                float area = n.magnitude;
                for (int corner = 0; corner < 3; corner++)
                {
                    int node = corner == 0 ? a : corner == 1 ? d : c;
                    averaged[node] += m.FaceValue(Field, f, node) * area;
                    weights[node] += area; nodeNormals[node] += n;
                }
            }
            for (int i = 0; i < m.nodes; i++) { averaged[i] /= Mathf.Max(1e-20f, weights[i]); nodeNormals[i].Normalize(); }
            for (int i = 0; i < count; i++)
            {
                int face = faces[i / 3];
                int corner = i % 3;
                int node = m.tri[face * 3 + (corner == 0 ? 0 : 3 - corner)];
                vertices[i] = (m.Point(node) - center +
                    (Deformed ? m.Displacement(node) * (Factor * Magnification) : Vector3.zero)) * normalization;
                float value = Smooth ? averaged[node] : m.FaceValue(Field, face, node);
                float t = (value - low) / Mathf.Max(1e-20f, high - low);
                colors[i] = Ramp(t);
                coordinates[i] = new Vector2(t, 0);
                normals[i] = nodeNormals[node];
                indices[i] = i;
                edges[i * 2] = i;
                edges[i * 2 + 1] = i / 3 * 3 + (i % 3 + 1) % 3;
            }
            for (int i = 0; i < count; i += 3)
            {
                Vector3 flat = Vector3.Cross(vertices[i+1]-vertices[i], vertices[i+2]-vertices[i]).normalized;
                for (int c = 0; c < 3; c++)
                    if (Vector3.Dot(normals[i+c], flat) < .88f) normals[i+c] = flat;
            }
            surfaceCollider.sharedMesh = null;
            mesh.Clear(); mesh.vertices = vertices; mesh.colors = colors; mesh.normals = normals; mesh.uv = coordinates; mesh.triangles = indices;
            mesh.RecalculateBounds();
            surfaceCollider.sharedMesh = mesh;
            displayedFaces = faces.ToArray();
            var wireVertices = new Vector3[count];
            for (int i = 0; i < count; i++) wireVertices[i] = vertices[i] * 1.0007f;
            wireMesh.Clear(); wireMesh.vertices = wireVertices;
            var edgeColors = new Color[count];
            for (int i = 0; i < count; i++) edgeColors[i] = new Color(.04f,.06f,.08f);
            wireMesh.colors = edgeColors;
            wireMesh.SetIndices(edges, MeshTopology.Lines, 0);
            wireMesh.RecalculateBounds();
            wireRenderer.enabled = Wire;
            material.SetFloat("_SurfaceShade", .65f);
            material.SetFloat("_Ambient", .72f);
            material.SetFloat("_Contours", Contours ? 1 : 0);
            RefreshAnnotations(b, normalization);
            RefreshDetail(b, normalization);
            ClearProbes();
            PositionCamera();
        }

        void PositionCamera()
        {
            if (!viewCamera) return;
            viewCamera.transform.localPosition = cameraFocus + new Vector3(0, 0, -zoom);
            viewCamera.transform.localRotation = Quaternion.identity;
            viewCamera.fieldOfView = 48;
            float ratio = rect.rect.width / Mathf.Max(1, rect.rect.height);
            viewCamera.aspect = ratio;
        }

        void Update()
        {
            if (!viewCamera) return;
            bool visible = GetComponentInParent<Canvas>().enabled;
            viewCamera.enabled = visible;
            UpdateDetailCamera(visible);
            if (visible && AutoRotate) body.transform.Rotate(Vector3.up, Time.unscaledDeltaTime * 12, Space.World);
            PositionCamera();
            UpdateAnnotationPositions();
        }
        public void OnDrag(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right)
            {
                cameraFocus += new Vector3(-e.delta.x, -e.delta.y, 0) * (zoom / Mathf.Max(1, rect.rect.height));
                return;
            }
            body.transform.Rotate(Vector3.up, -e.delta.x * .4f, Space.World);
            body.transform.Rotate(Vector3.right, e.delta.y * .4f, Space.World);
        }
        public void OnScroll(PointerEventData e) { zoom = Mathf.Clamp(zoom - e.scrollDelta.y * .18f, 1.4f, 8); PositionCamera(); }
        public void OnPointerClick(PointerEventData e)
        {
            if (FractureActive) return;
            if (e.dragging || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, null, out var p)) return;
            var r = rect.rect;
            var ray = viewCamera.ViewportPointToRay(new Vector3((p.x - r.x) / r.width, (p.y - r.y) / r.height, 0));
            if (!surfaceCollider.Raycast(ray, out var hit, 50)) return;
            var m = Result.models[Model];
            int face = displayedFaces[hit.triangleIndex];
            int a = m.tri[face * 3], b = m.tri[face * 3 + 2], c = m.tri[face * 3 + 1];
            var w = hit.barycentricCoordinate;
            Vector3 u = m.Displacement(a) * w.x + m.Displacement(b) * w.y + m.Displacement(c) * w.z;
            float t = m.temperature[a] * w.x + m.temperature[b] * w.y + m.temperature[c] * w.z;
            Vector3 position = m.Point(a) * w.x + m.Point(b) * w.y + m.Point(c) * w.z;
            AddProbe(face, position, body.transform.InverseTransformPoint(hit.point), u, t);
        }
        void OnEnable() { if (scene) scene.SetActive(true); }
        void OnDisable() { if (scene) scene.SetActive(false); }
        void OnDestroy()
        {
            DisposeFracture();
            DisposeDetail();
            if (viewCamera) viewCamera.targetTexture = null;
            if (target) { target.Release(); Destroy(target); }
            if (scene) Destroy(scene);
            if (mesh) Destroy(mesh);
            if (wireMesh) Destroy(wireMesh);
            if (material) Destroy(material);
            foreach (var owned in ownedAnnotationObjects) if (owned) Destroy(owned);
        }
    }
}
