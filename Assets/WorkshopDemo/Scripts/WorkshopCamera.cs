using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace WorkshopDemo
{
    [RequireComponent(typeof(Camera))]
    public sealed class WorkshopCamera : MonoBehaviour
    {
        public static Camera Main { get; private set; }
        public WorkshopSimulation simulation;
        public Vector3 overviewTarget = new Vector3(-4, 1, 0);
        public float overviewDistance = 48;
        public float overviewPitch = 56;
        public bool Following { get; private set; }
        Vector3 target = new Vector3(0, .8f, 0);
        Vector3 smoothTarget;
        float yaw = -33;
        float pitch = 48;
        float distance = 35;
        float smoothDistance = 35;
        Vector2 downAt;
        bool dragged;
        bool pointerBeganInScene;
        Camera view;

        void Awake()
        {
            Main = view = GetComponent<Camera>();
            Overview();
            smoothTarget = target;
            smoothDistance = distance;
        }

        public void Overview()
        {
            Following = false;
            target = overviewTarget;
            yaw = -22; pitch = overviewPitch; distance = overviewDistance;
        }

        public void Focus(WorkshopDevice device)
        {
            Following = false;
            target = device.transform.position + Vector3.up * (device.deviceId == "TK-01" ? 2 : 1.1f);
            yaw = device.deviceId == "TK-01" ? -90 : device.kind == DeviceKind.Cabinet ? 158 : -22;
            distance = device.kind == DeviceKind.Conveyor ? 9 : device.deviceId == "TK-01" ? 10 : 6;
            pitch = 32;
        }

        public void ToggleFollow()
        {
            Following = !Following;
            if (Following) { distance = 8; pitch = 32; }
        }

        void LateUpdate()
        {
            var mouse = Mouse.current;
            if (Following && simulation && simulation.inspector)
                target = simulation.inspector.transform.position + Vector3.up;
            if (mouse != null)
            {
                Vector2 pos = mouse.position.ReadValue();
                bool overUi = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
                bool inScene = view.pixelRect.Contains(pos) && !overUi;
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    downAt = pos; dragged = false;
                    pointerBeganInScene = inScene;
                }
                if (mouse.leftButton.isPressed && Vector2.Distance(pos, downAt) > 6) dragged = true;
                if (inScene)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    if (mouse.rightButton.isPressed || (mouse.leftButton.isPressed && dragged && pointerBeganInScene))
                    {
                        Following = false;
                        yaw += delta.x * .18f;
                        pitch = Mathf.Clamp(pitch - delta.y * .16f, 16, 78);
                    }
                    if (mouse.middleButton.isPressed)
                    {
                        Following = false;
                        Vector3 right = Quaternion.Euler(0, yaw, 0) * Vector3.right;
                        Vector3 forward = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                        target += (-right * delta.x - forward * delta.y) * distance * .0012f;
                        target.x = Mathf.Clamp(target.x, -26, 18);
                        target.z = Mathf.Clamp(target.z, -27, 25);
                    }
                    distance = Mathf.Clamp(distance - mouse.scroll.ReadValue().y * .012f, 3, 65);
                    if (mouse.leftButton.wasReleasedThisFrame && !dragged && pointerBeganInScene)
                    {
                        var ray = view.ScreenPointToRay(pos);
                        if (Physics.Raycast(ray, out var hit, 150))
                        {
                            var device = hit.collider.GetComponentInParent<WorkshopDevice>();
                            if (device) simulation.Select(device, false);
                        }
                    }
                }
            }
            float blend = 1 - Mathf.Exp(-Time.deltaTime * 6);
            smoothTarget = Vector3.Lerp(smoothTarget, target, blend);
            smoothDistance = Mathf.Lerp(smoothDistance, distance, blend);
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 desired = smoothTarget - rotation * Vector3.forward * smoothDistance;
            desired.y = Mathf.Max(desired.y, 1.3f);
            transform.SetPositionAndRotation(desired, Quaternion.LookRotation(smoothTarget - desired));
        }

        void OnDestroy() { if (Main == view) Main = null; }
    }
}
