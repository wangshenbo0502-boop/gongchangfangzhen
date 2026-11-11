using UnityEngine;

namespace WorkshopDemo
{
    public static class WorkshopTrainingLayout
    {
        public static readonly string[] DeviceIds = { "P-01", "TK-01", "F-01", "PLC-01" };
        public static readonly Vector3 RobotSpawn = new Vector3(5.1f, 0, -2.8f);
        public static readonly Vector3 OverviewTarget = new Vector3(5.5f, 1, .25f);
        public const float OverviewDistance = 16;
        public const float OverviewPitch = 48;

        public static Vector3 Position(string id)
        {
            switch (id)
            {
                case "P-01": return new Vector3(5.7f, 0, 2.6f);
                case "TK-01": return new Vector3(3.35f, 0, 2.6f);
                case "F-01": return new Vector3(7.15f, 0, -1.1f);
                case "PLC-01": return new Vector3(7.7f, 0, 2.6f);
                default: throw new System.ArgumentException("Not a training station: " + id);
            }
        }

        public static Quaternion Rotation(string id)
        {
            return id == "F-01" ? Quaternion.Euler(0, 180, 0) : Quaternion.identity;
        }

        public static Vector3 InspectionPoint(string id)
        {
            var position = Position(id);
            return position + new Vector3(0, 0, position.z > 0 ? -1.9f : 1.9f);
        }

        public static void ConfigureCamera(WorkshopCamera camera)
        {
            camera.overviewTarget = OverviewTarget;
            camera.overviewDistance = OverviewDistance;
            camera.overviewPitch = OverviewPitch;
        }
    }
}
