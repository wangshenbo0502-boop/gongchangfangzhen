using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WorkshopDemo
{
    public sealed class WorkshopAbaqusFieldView : MonoBehaviour, IDragHandler, IScrollHandler, IPointerClickHandler
    {
        public Action<string> Probe;
        public WorkshopAbaqusFieldResult Result { get; private set; }
        public float Low { get; private set; }
        public float High { get; private set; }
        GameObject studio, body;
        Camera viewCamera;
        Mesh mesh;
        MeshCollider colliderMesh;
        Material material;
        RenderTexture target;
        WorkshopFieldFrame frame;
        float zoom = 4;
        RectTransform rect;

        public void Initialize(WorkshopAbaqusFieldResult result)
        {
            Result = result;
            rect = (RectTransform)transform;
            studio = new GameObject("ODB local thermal field");
            studio.transform.position = new Vector3(320, 320, 320);
            body = new GameObject("Original nodal field");
            body.layer = 31; body.transform.SetParent(studio.transform, false);
            body.transform.localRotation = Quaternion.Euler(20, -28, 0);
            mesh = new Mesh { name = "ODB DC3D4 external faces", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            var m = result.Mesh;
            var points = Enumerable.Range(0, m.nodeLabels.Length).Select(i =>
                new Vector3(m.coordinatesM[i * 3], m.coordinatesM[i * 3 + 1], m.coordinatesM[i * 3 + 2])).ToArray();
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (var point in points) bounds.Encapsulate(point);
            float scale = 2.8f / Mathf.Max(bounds.size.magnitude, 1e-12f);
            mesh.vertices = points.Select(p => (p - bounds.center) * scale).ToArray();
            var triangles = (int[])m.triangles.Clone();
            for (int i = 0; i < triangles.Length; i += 3)
                (triangles[i + 1], triangles[i + 2]) = (triangles[i + 2], triangles[i + 1]);
            mesh.triangles = triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            body.AddComponent<MeshFilter>().sharedMesh = mesh;
            material = new Material(Resources.Load<Shader>("WorkshopField"));
            material.SetFloat("_SurfaceShade", .65f); material.SetFloat("_Ambient", .72f);
            material.SetFloat("_Contours", 0);
            body.AddComponent<MeshRenderer>().sharedMaterial = material;
            colliderMesh = body.AddComponent<MeshCollider>(); colliderMesh.sharedMesh = mesh;
            var cameraRoot = new GameObject("Local field camera");
            cameraRoot.transform.SetParent(studio.transform, false);
            viewCamera = cameraRoot.AddComponent<Camera>();
            viewCamera.cullingMask = 1 << 31;
            viewCamera.clearFlags = CameraClearFlags.SolidColor; viewCamera.backgroundColor = new Color(.045f, .05f, .055f);
            viewCamera.nearClipPlane = .01f; viewCamera.farClipPlane = 20; viewCamera.fieldOfView = 48;
            target = new RenderTexture(1600, 1000, 24) { antiAliasing = 2 };
            target.Create(); viewCamera.targetTexture = target; GetComponent<RawImage>().texture = target;
            ShowFrame(0);
        }
        public void ShowFrame(int index)
        {
            frame = Result.Frame(index);
            Low = frame.temperatureC.Min(); High = frame.temperatureC.Max();
            mesh.colors = frame.temperatureC.Select(t => WorkshopAnalysisView.Ramp((t - Low) / Mathf.Max(1e-12f, High - Low))).ToArray();
            mesh.uv = frame.temperatureC.Select(t => new Vector2((t - Low) / Mathf.Max(1e-12f, High - Low), 0)).ToArray();
            Probe?.Invoke("—");
        }
        void LateUpdate()
        {
            if (!viewCamera) return;
            var canvas = GetComponentInParent<Canvas>();
            viewCamera.enabled = canvas && canvas.enabled;
            viewCamera.aspect = rect.rect.width / Mathf.Max(1, rect.rect.height);
            viewCamera.transform.localPosition = new Vector3(0, 0, -zoom);
        }
        public void OnDrag(PointerEventData data) => body.transform.Rotate(new Vector3(data.delta.y, -data.delta.x, 0) * .3f, Space.World);
        public void OnScroll(PointerEventData data) => zoom = Mathf.Clamp(zoom - data.scrollDelta.y * .2f, 2, 10);
        public void OnPointerClick(PointerEventData data)
        {
            if (data.dragging || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, data.position, data.pressEventCamera, out var p)) return;
            var uv = new Vector2((p.x - rect.rect.xMin) / rect.rect.width, (p.y - rect.rect.yMin) / rect.rect.height);
            var ray = viewCamera.ViewportPointToRay(uv);
            if (!colliderMesh.Raycast(ray, out var hit, 30)) return;
            var tri = mesh.triangles;
            int corner = hit.barycentricCoordinate.x >= hit.barycentricCoordinate.y && hit.barycentricCoordinate.x >= hit.barycentricCoordinate.z ?
                0 : hit.barycentricCoordinate.y >= hit.barycentricCoordinate.z ? 1 : 2;
            int node = tri[hit.triangleIndex * 3 + corner];
            Probe?.Invoke(Result.Mesh.instanceName + " / node " + Result.Mesh.nodeLabels[node] +
                "\n" + frame.temperatureC[node].ToString("G9") + " °C · NODAL · 无平均 / 原始节点值" +
                "\n表面单元 " + Result.Mesh.triangleElementLabels[hit.triangleIndex] +
                " / S" + Result.Mesh.triangleFaces[hit.triangleIndex]);
        }
        void OnEnable() { if (studio) studio.SetActive(true); }
        void OnDisable() { if (studio) studio.SetActive(false); }
        void OnDestroy()
        {
            if (viewCamera) viewCamera.targetTexture = null;
            if (target) { target.Release(); Destroy(target); }
            if (mesh) Destroy(mesh);
            if (material) Destroy(material);
            if (studio) Destroy(studio);
        }
    }
}
