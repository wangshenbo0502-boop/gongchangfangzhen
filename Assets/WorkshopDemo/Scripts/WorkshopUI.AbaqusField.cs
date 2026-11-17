using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace WorkshopDemo
{
    public sealed partial class WorkshopUI
    {
        GameObject abaqusFieldDialog;
        string fieldSourceStatus;

        void AddFieldSources(WorkshopDevice device, WorkshopFault fault, List<(string label, Action action)> choices)
        {
            fieldSourceStatus = "无 Abaqus 全场结果";
            string root = Path.Combine(Application.streamingAssetsPath, "WorkshopAbaqusFields", device.deviceId, fault.Code);
            if (!Directory.Exists(root)) return;
            try
            {
                foreach (string directory in Directory.GetDirectories(root))
                {
                    if (!WorkshopAbaqusFieldResult.Identifier(Path.GetFileName(directory))) continue;
                    try
                    {
                        var field = WorkshopAbaqusFieldResult.Open(directory, device.deviceId, fault.Code);
                        field.Frame(0);
                        if (field.Manifest.caseId != Path.GetFileName(directory)) throw new ArgumentException("Case folder mismatch");
                        var model = device.GetComponentInChildren<WorkshopEquipmentModel>();
                        if (!model || !model.TryGetFaultBinding(field.Manifest.componentId, out _))
                            throw new ArgumentException("对应部件绑定不存在：" + field.Manifest.componentId);
                        choices.Add(("Abaqus 局部全场 · " + field.Manifest.caseId, () => OpenAbaqusField(field)));
                    }
                    catch (Exception e) when (e is ArgumentException || e is IOException || e is UnauthorizedAccessException)
                    { fieldSourceStatus = "全场包校验失败：" + e.Message; }
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            { fieldSourceStatus = "全场目录不可读：" + e.Message; }
        }

        void OpenAbaqusField(WorkshopAbaqusFieldResult result)
        {
            if (abaqusFieldDialog) { abaqusFieldDialog.SetActive(false); Destroy(abaqusFieldDialog); }
            var root = Panel(transform, "ODB local field", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Background);
            abaqusFieldDialog = root.gameObject;
            var m = result.Manifest;
            Label(root, m.deviceId + " / " + m.faultCode + " / " + m.caseId, 19, Ink, 24, 16, 1200, 32);
            var close = Command(root, "返回工况", 0, 16, 120, () => abaqusFieldDialog.SetActive(false));
            close.GetComponent<RectTransform>().anchorMin = close.GetComponent<RectTransform>().anchorMax = Vector2.one;
            close.GetComponent<RectTransform>().anchoredPosition = new Vector2(-86, -32);
            Label(root, "Abaqus ODB · " + m.job + " / " + m.step + " / " + m.region +
                " · 部件 " + m.componentId + " · 模型 " + m.modelVersion +
                "\n" + m.analysisDescription, 14, Muted, 24, 60, 1450, 90, TextAnchor.UpperLeft);
            var area = Rect(root, "Local field viewport", Vector2.zero, Vector2.one, new Vector2(24, 200), new Vector2(-340, -160));
            area.gameObject.AddComponent<RawImage>();
            var view = area.gameObject.AddComponent<WorkshopAbaqusFieldView>();
            view.Initialize(result);
            var details = Label(root, "", 14, Ink, 0, 160, 290, 360, TextAnchor.UpperLeft);
            details.rectTransform.anchorMin = details.rectTransform.anchorMax = Vector2.one;
            details.rectTransform.pivot = Vector2.up;
            details.rectTransform.anchoredPosition = new Vector2(-316, -160);
            var probe = Label(root, "—", 14, Ink, 24, 0, 1420, 110, TextAnchor.UpperLeft);
            probe.rectTransform.anchorMin = probe.rectTransform.anchorMax = Vector2.zero;
            probe.rectTransform.pivot = Vector2.up;
            probe.rectTransform.anchoredPosition = new Vector2(24, 168);
            view.Probe = value => probe.text = value;
            var slider = AnalysisSlider(root, "Frame", 0, 0, m.frames.Length - 1, 0);
            slider.onValueChanged.RemoveAllListeners();
            slider.wholeNumbers = true; slider.interactable = m.frames.Length > 1;
            var rect = slider.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.zero; rect.anchoredPosition = new Vector2(24, 36);
            rect.pivot = Vector2.zero;
            void RefreshField()
            {
                try
                {
                    int index = Mathf.RoundToInt(slider.value);
                    view.ShowFrame(index);
                    details.text = "帧 " + index + " / " + (m.frames.Length - 1) +
                        "\n" + result.AxisLabel + " " + m.frames[index].axisValue.ToString("G9") + " " + m.axisUnit +
                        "\n温度 / °C\n" + view.Low.ToString("G9") + " → " + view.High.ToString("G9") +
                        "\n逐帧色标\nNODAL · 无平均\n原始节点温度 / 表面线性着色\n固定网格 · 无帧间插值\n坐标 m · XYZ 全局\n未映射整机\n\n求解器 " + m.solverVersion + "\n导出器 " + m.exporterVersion;
                }
                catch (Exception e) { details.text = "全场帧校验失败：" + e.Message; area.gameObject.SetActive(false); slider.interactable = false; }
            }
            slider.onValueChanged.AddListener(_ => RefreshField());
            var provenance = Label(root, "ODB SHA256 " + m.odbSha256 + "\n输入 SHA256 " + m.inputSha256, 11, Muted, 340, 0, 1100, 48);
            provenance.rectTransform.anchorMin = provenance.rectTransform.anchorMax = Vector2.zero;
            provenance.rectTransform.pivot = Vector2.up;
            provenance.rectTransform.anchoredPosition = new Vector2(340, 58);
            RefreshField();
        }
    }
}
