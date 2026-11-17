using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WorkshopDemo
{
    public sealed partial class WorkshopUI
    {
        GameObject abaqusDialog;

        void OpenAbaqusEnvelope(WorkshopDevice device, WorkshopFault fault, WorkshopAbaqusResult result)
        {
            if (abaqusDialog) { abaqusDialog.SetActive(false); Destroy(abaqusDialog); }
            var root = Panel(transform, "ODB envelope", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Background);
            abaqusDialog = root.gameObject;
            Label(root, device.deviceId + " / " + fault.Code + " / Abaqus ODB 包络", 20, Ink, 24, 18, 1250, 34);
            Command(root, "返回工况", 1420, 18, 130, () => abaqusDialog.SetActive(false));
            Command(root, "重新加载", 1268, 18, 130, () => {
                var updated = WorkshopAbaqusResult.Load(device.deviceId, fault.Code, out string status);
                if (updated == null) { abaqusDialog.SetActive(false); OpenQuickMenu(status); }
                else OpenAbaqusEnvelope(device, fault, updated);
            });
            Label(root, "作业 " + result.job + " · 步 " + result.step + " · 区域 " + result.region +
                "\n" + result.analysisDescription + "\nODB SHA256 " + result.odbSha256 +
                "\n导出 " + result.exportedAt + " · 独立区域极值，不代表同一节点，不映射参考件云图",
                14, Muted, 24, 70, 1500, 120, TextAnchor.UpperLeft);
            var channels = Menu(root, "Envelope channel", 24, 196, 340);
            var ids = new List<int>(); var labels = new List<string>();
            if (result.hasStress) { ids.Add(0); labels.Add("最大 Mises 应力 / MPa"); }
            if (result.hasDisplacement) { ids.Add(1); labels.Add("最大位移 / mm"); }
            if (result.hasTemperature) { ids.Add(2); labels.Add("最高温度 / °C"); }
            channels.AddOptions(labels);
            var plotArea = Panel(root, "Envelope chart", Vector2.zero, Vector2.one,
                new Vector2(24, 190), new Vector2(-24, -250), Surface);
            var plotRoot = Rect(plotArea, "Curves", Vector2.zero, Vector2.one, new Vector2(16, 16), new Vector2(-16, -16));
            var plot = plotRoot.gameObject.AddComponent<WorkshopAbaqusCurveGraphic>();
            plot.Result = result; plot.color = Yellow; plot.raycastTarget = false;
            var text = Label(root, "", 16, Ink, 24, 0, 1500, 74, TextAnchor.UpperLeft);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = Vector2.zero;
            text.rectTransform.pivot = Vector2.up;
            text.rectTransform.anchoredPosition = new Vector2(24, 170);
            var sliderArea = Box(root, "Physical time", Vector2.zero, Vector2.right,
                new Vector2(24, 50), new Vector2(-24, 82), Surface);
            var slider = sliderArea.gameObject.AddComponent<Slider>();
            slider.minValue = result.frames[0].time;
            slider.maxValue = Mathf.Max(slider.minValue + .00001f, result.frames[result.frames.Length - 1].time);
            var handle = Box(sliderArea, "Cursor", 0, 0, 12, 32, Yellow);
            slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>();
            slider.interactable = result.frames.Length > 1;
            void RefreshEnvelope()
            {
                plot.Channel = ids[channels.value]; plot.Cursor = slider.value; plot.SetVerticesDirty();
                var frame = result.At(slider.value);
                bool raw = Array.Exists(result.frames, f => Mathf.Abs(f.time - frame.time) < .000001f);
                float lo = float.MaxValue, hi = float.MinValue;
                foreach (var f in result.frames) { lo = Mathf.Min(lo, plot.Value(f)); hi = Mathf.Max(hi, plot.Value(f)); }
                text.text = labels[channels.value] + "  " + plot.Value(frame).ToString("G7") +
                    "\n物理时间 " + frame.time.ToString("G7") + " s · " + (raw ? "原始帧" : "相邻帧线性插值") +
                    " · Y 范围 " + lo.ToString("G7") + " — " + hi.ToString("G7") +
                    " · X 范围 " + result.frames[0].time + " — " + result.frames[result.frames.Length - 1].time + " s";
            }
            slider.onValueChanged.AddListener(_ => RefreshEnvelope());
            channels.onValueChanged.AddListener(_ => RefreshEnvelope());
            slider.value = slider.minValue; RefreshEnvelope();
        }
    }
}
