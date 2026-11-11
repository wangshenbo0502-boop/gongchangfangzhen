using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WorkshopDemo
{
    public sealed partial class WorkshopAnalysisView
    {
        Camera detailCamera;
        RenderTexture detailTexture;
        Vector3 detailPoint, detailDirection;
        float detailDistance, detailZoom = 1;
        public RenderTexture DetailTexture => detailTexture;

        public bool CreateDetail(RawImage image)
        {
            if (Result.variable != "notch" && Result.variable != "thin") return false;
            var cameraObject = new GameObject("Solved defect closeup camera");
            cameraObject.transform.SetParent(scene.transform, false);
            detailCamera = cameraObject.AddComponent<Camera>();
            detailCamera.clearFlags = CameraClearFlags.SolidColor;
            detailCamera.backgroundColor = new Color(.045f, .055f, .07f);
            detailCamera.cullingMask = 1 << 31;
            detailCamera.nearClipPlane = .001f;
            detailCamera.farClipPlane = 10;
            detailCamera.fieldOfView = 40;
            detailTexture = new RenderTexture(960, 640, 24) { antiAliasing = 4 };
            detailTexture.Create();
            detailCamera.targetTexture = detailTexture;
            image.texture = detailTexture;
            image.gameObject.AddComponent<WorkshopAnalysisDetailInput>().View = this;
            return true;
        }

        void RefreshDetail(Bounds bounds, float normalization)
        {
            if (!detailCamera) return;
            bool notch = Result.variable == "notch";
            var m = Result.models[Model];
            Vector3 targetPoint = notch ?
                new Vector3(bounds.min.x + bounds.size.x * .18f,
                    bounds.max.y - Result.inputValues[Model] * .5f, bounds.min.z + bounds.size.z * .12f) :
                new Vector3(bounds.center.x, 0, bounds.max.z - Result.dimensionsMm[2] / 2000);
            int nearest = 0;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < m.nodes; i++)
            {
                float distance = (m.Point(i) - targetPoint).sqrMagnitude;
                if (distance < nearestDistance) { nearest = i; nearestDistance = distance; }
            }
            detailPoint = (targetPoint - center +
                (Deformed ? m.Displacement(nearest) * Factor * Magnification : Vector3.zero)) * normalization;
            detailDirection = notch ? new Vector3(.65f, .55f, -1).normalized :
                new Vector3(.20f, -.10f, -1).normalized;
            detailDistance = notch ? Mathf.Clamp(bounds.size.x * .18f * normalization, .40f, .85f) :
                bounds.extents.z * normalization * .75f;
            UpdateDetailCamera(true);
        }

        void UpdateDetailCamera(bool visible)
        {
            if (!detailCamera) return;
            detailCamera.enabled = visible;
            Vector3 targetPoint = body.transform.TransformPoint(detailPoint);
            detailCamera.transform.position = targetPoint +
                body.transform.TransformDirection(detailDirection) * detailDistance * detailZoom;
            detailCamera.transform.LookAt(targetPoint, body.transform.up);
            detailCamera.aspect = 1.5f;
        }

        public void ZoomDetail(float delta) => detailZoom = Mathf.Clamp(detailZoom - delta * .12f, .5f, 2.5f);

        public void FocusDefect()
        {
            cameraFocus = scene.transform.InverseTransformPoint(body.transform.TransformPoint(detailPoint));
            zoom = 1.4f;
            PositionCamera();
        }

        void DisposeDetail()
        {
            if (detailCamera) detailCamera.targetTexture = null;
            if (detailTexture) { detailTexture.Release(); Destroy(detailTexture); }
        }
    }

    public sealed class WorkshopAnalysisDetailInput : MonoBehaviour, IScrollHandler, IPointerClickHandler
    {
        public WorkshopAnalysisView View;
        public void OnScroll(PointerEventData data) => View.ZoomDetail(data.scrollDelta.y);
        public void OnPointerClick(PointerEventData data) => View.FocusDefect();
    }
}
