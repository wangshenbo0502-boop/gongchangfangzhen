using System;
using System.IO;
using UnityEngine;

namespace WorkshopDemo
{
    public sealed partial class WorkshopUI
    {
        bool TryReviewedViewer(WorkshopDevice device, out string path)
        {
            path = null;
            if (!device) return false;
            string viewer;
            switch (device.kind)
            {
                case DeviceKind.Pump: viewer = "WaterPumpViewer"; break;
                case DeviceKind.Tank: viewer = "TankViewer"; break;
                case DeviceKind.Fan: viewer = "IndustrialFanViewer"; break;
                case DeviceKind.Cabinet: viewer = "CabinetViewer"; break;
                default: return false;
            }
            path = Application.isEditor
                ? Path.GetFullPath("model/" + viewer + "/" + viewer + ".exe")
                : Path.Combine(Path.GetDirectoryName(Application.dataPath), "Viewers", viewer, viewer + ".exe");
            return true;
        }

        void OpenReviewedViewer() => OpenReviewedViewer(simulation.Selected);

        void OpenReviewedViewer(WorkshopDevice device)
        {
            if (!TryReviewedViewer(device, out string path) || !File.Exists(path))
            {
                OpenQuickMenu("当前设备的独立展示器不可用。");
                return;
            }
            try
            {
                using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path)
                    { UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(path) });
                feedback.text = "已打开" + device.displayName + "详细展示。";
            }
            catch (Exception e) { OpenQuickMenu("打开详细展示失败\n" + e.Message); }
        }
    }
}
