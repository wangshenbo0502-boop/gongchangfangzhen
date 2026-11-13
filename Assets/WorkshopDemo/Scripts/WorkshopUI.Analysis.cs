using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace WorkshopDemo
{
    public sealed partial class WorkshopUI
    {
        GameObject analysisDialog;
        WorkshopAnalysisView analysisView;
        WorkshopAnalysisResult analysisResult;
        Text analysisDetails, analysisNumbers, analysisScale, analysisLoad, analysisLegend, analysisProbe;
        RectTransform analysisSpecsContent;
        Button normalModel, faultModel, stressField, displacementField, temperatureField;
        Slider analysisLoadSlider, analysisScaleSlider;
        Toggle analysisWire, analysisDeform, analysisSection;
        Toggle analysisSmooth, analysisContours, analysisAnnotations, analysisMeasure, analysisOrbit, analysisPlayback;
        Dropdown analysisQuantity;
        readonly List<int> analysisQuantityIds = new List<int>();
        Slider analysisCut;
        WorkshopEquipmentContextView equipmentContext;
        float nextAnalysisFrame;
        int analysisModel = 1, analysisField;
        bool preparingAnalysis;

        Toggle AnalysisToggle(Transform parent, string name, float x, float y, bool initial, Action<bool> change)
        {
            var root = Box(parent, name, x, y, 106, 30, Surface);
            var toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = root.GetComponent<Image>();
            var frame = Box(root, "Checkbox", 8, 9, 12, 12, Line);
            toggle.graphic = Box(frame, "Check", 2, 2, 8, 8, Yellow, false).GetComponent<Image>();
            toggle.navigation = new Navigation { mode = Navigation.Mode.None };
            Label(root, name, 12, Ink, 27, 0, 76, 30);
            toggle.SetIsOnWithoutNotify(initial);
            toggle.onValueChanged.AddListener(v => change(v));
            return toggle;
        }

        Slider AnalysisSlider(Transform parent, string name, float y, float min, float max, float value)
        {
            var r = Box(parent, name, 12, y, 290, 22, Background);
            var slider = r.gameObject.AddComponent<Slider>();
            slider.minValue = min; slider.maxValue = max; slider.value = value;
            Box(r, "Track", 0, 9, 290, 4, Line, false);
            var slide = Rect(r, "Handle area", Vector2.zero, Vector2.one, new Vector2(6, 0), new Vector2(-6, 0));
            var handle = Panel(slide, "Handle", Vector2.zero, Vector2.one, new Vector2(-6, 2), new Vector2(6, -2), Yellow);
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.onValueChanged.AddListener(_ => UpdateAnalysis());
            return slider;
        }

        void OpenAnalysis()
        {
            var device = simulation.Selected;
            if (!device) return;
            var mode = WorkshopFaultCatalog.For(device.kind)[analysisMode.value];
            var result = WorkshopAnalysisResult.Load(mode.Code, out string message);
            if (result == null)
            {
                simulation.Notify(message + " 当前工况：" + mode.Name);
                analysisLive.text = message + "\n当前工况：" + mode.Name;
                return;
            }
            preparingAnalysis = true;
            if (analysisDialog) { analysisDialog.SetActive(false); Destroy(analysisDialog); }
            analysisResult = result;
            analysisModel = 1;
            analysisField = result.analysis == "thermal" ? 2 : 0;
            var overlay = Panel(transform, "Finite element analysis", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Color(Background.r, Background.g, Background.b, 1));
            analysisDialog = overlay.gameObject;
            Label(overlay, device.deviceId + " · " + mode.Name + " · " + result.component, 18, Ink, 20, 9, 1000, 28);
            Label(overlay, result.source, 12, Yellow, 20, 40, 900, 22);
            var close = Command(overlay, "返回工况", 0, 12, 90, () => analysisDialog.SetActive(false));
            close.GetComponent<RectTransform>().anchorMin = close.GetComponent<RectTransform>().anchorMax = Vector2.one;
            close.GetComponent<RectTransform>().anchoredPosition = new Vector2(-65, -28);

            var controls = Box(overlay, "Model and field", 20, 75, 860, 36, Background);
            normalModel = Command(controls, "完好 / 基准", 0, 0, 106, () => { analysisModel = 0; UpdateAnalysis(); });
            faultModel = Command(controls, "故障 / 缺陷", 112, 0, 106, () => { analysisModel = 1; UpdateAnalysis(); });
            stressField = Command(controls, "应力", 242, 0, 64, () => { analysisField = 0; UpdateAnalysis(); });
            displacementField = Command(controls, "位移", 312, 0, 64, () => { analysisField = 1; UpdateAnalysis(); });
            temperatureField = Command(controls, "温度", 382, 0, 64, () => { analysisField = 2; UpdateAnalysis(); });
            analysisWire = AnalysisToggle(controls, "单元边线", 464, 0, false, _ => UpdateAnalysis());
            analysisDeform = AnalysisToggle(controls, "变形显示", 578, 0, result.analysis == "structural", _ => UpdateAnalysis());
            analysisSection = AnalysisToggle(controls, "半剖显示", 692, 0, result.geometry.Contains("外半径"), _ => UpdateAnalysis());
            stressField.interactable = displacementField.interactable = result.analysis == "structural";
            temperatureField.interactable = result.analysis == "thermal";
            analysisDeform.interactable = result.analysis == "structural";
            BuildAnalysisTools(overlay, result);

            var left = Rect(overlay, "Analysis data", Vector2.zero, Vector2.up,
                new Vector2(20, 18), new Vector2(344, -164));
            Scroll(left, "Model specifications", Vector2.zero, Vector2.one,
                new Vector2(0, 210), Vector2.zero, out analysisSpecsContent);
            analysisDetails = Label(analysisSpecsContent, "", 13, Ink, 0, 0, 316, 900, TextAnchor.UpperLeft);
            analysisDetails.rectTransform.pivot = Vector2.up;
            analysisDetails.rectTransform.anchoredPosition = Vector2.zero;
            var sliders = Panel(left, "Analysis parameters", Vector2.zero, Vector2.right,
                Vector2.zero, new Vector2(0, 202), Surface);
            analysisLoad = Label(sliders, "", 12, Ink, 12, 4, 300, 24);
            analysisLoadSlider = AnalysisSlider(sliders, "Load factor", 32, .25f, 1.5f, 1);
            analysisScale = Label(sliders, "", 12, Ink, 12, 66, 300, 24);
            analysisScaleSlider = AnalysisSlider(sliders, "Deformation magnification", 94, 0, 3, 1);
            analysisScaleSlider.interactable = result.analysis == "structural";
            Label(sliders, "剖切面位置", 12, Ink, 12, 132, 300, 24);
            analysisCut = AnalysisSlider(sliders, "Section position", 164, -.95f, .95f, 0);

            var right = Panel(overlay, "Field viewport", Vector2.zero, Vector2.one,
                new Vector2(364, 138), new Vector2(-20, -242), new Color(.035f, .038f, .042f));
            var renderArea = Rect(right, "Interactive field", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            renderArea.gameObject.AddComponent<RawImage>();
            analysisView = renderArea.gameObject.AddComponent<WorkshopAnalysisView>();
            analysisView.Initialize(result);
            analysisView.Probe = value => analysisProbe.text = value;
            AnalysisThicknessGauge(right, result);
            BuildAnalysisDetail(right, result);
            BuildEquipmentContext(right, device);
            var numbers = Rect(overlay, "Results", Vector2.up, Vector2.one,
                new Vector2(364, -232), new Vector2(-20, -164));
            analysisNumbers = Label(numbers, "", 14, Ink, 0, 0, 100, 58, TextAnchor.UpperLeft);
            analysisNumbers.name = "Analysis quantitative comparison";
            analysisNumbers.rectTransform.anchorMin = Vector2.zero;
            analysisNumbers.rectTransform.anchorMax = Vector2.one;
            analysisNumbers.rectTransform.offsetMin = Vector2.zero;
            analysisNumbers.rectTransform.offsetMax = Vector2.zero;
            var footer = Rect(overlay, "Field legend", Vector2.zero, Vector2.right,
                new Vector2(364, 18), new Vector2(-20, 128));
            analysisFooter = footer;
            for (int i = 0; i < 40; i++)
            {
                var swatch = Panel(footer, "Color " + i, new Vector2(i / 40f, 1), new Vector2((i + 1) / 40f, 1),
                    new Vector2(0, -12), Vector2.zero, WorkshopAnalysisView.Ramp(i / 39f), false);
            }
            analysisLegend = Label(footer, "", 12, Ink, 0, 16, 900, 24);
            analysisLegend.rectTransform.anchorMax = new Vector2(1, 1);
            analysisLegend.rectTransform.offsetMax = new Vector2(0, -16);
            analysisProbe = Label(footer, "探针：—", 12, Muted, 0, 43, 900, 64, TextAnchor.UpperLeft);
            analysisProbe.name = "Analysis probe";
            analysisProbe.rectTransform.anchorMax = new Vector2(1, 1);
            analysisProbe.rectTransform.offsetMax = new Vector2(0, -43);
            BuildFractureControls(overlay, right);
            analysisScaleSlider.SetValueWithoutNotify(Mathf.Log10(analysisView.Magnification));
            CompactAnalysisLayout(overlay);
            preparingAnalysis = false;
            UpdateAnalysis();
        }

        void BuildAnalysisTools(RectTransform overlay, WorkshopAnalysisResult result)
        {
            analysisQuantity = Menu(overlay, "Analysis quantity", 20, 116, 270);
            analysisQuantityIds.Clear();
            var options = new List<string>();
            for (int i = 0; i < WorkshopAnalysisResult.FieldNames.Length; i++)
                if (result.analysis == "structural" ? i != 2 && i != 17 : i == 2 || i == 17)
                {
                    analysisQuantityIds.Add(i);
                    options.Add(WorkshopAnalysisResult.FieldNames[i]+" / "+WorkshopAnalysisResult.Unit(i));
                }
            analysisQuantity.AddOptions(options);
            analysisQuantity.onValueChanged.AddListener(v => {
                analysisField = analysisQuantityIds[v]; UpdateAnalysis();
            });
            analysisSmooth = AnalysisToggle(overlay, "节点平均", 304, 116, true, _ => UpdateAnalysis());
            analysisContours = AnalysisToggle(overlay, "等值线", 418, 116, true, _ => UpdateAnalysis());
            analysisAnnotations = AnalysisToggle(overlay, "工程标注", 532, 116, true, _ => UpdateAnalysis());
            analysisMeasure = AnalysisToggle(overlay, "两点测量", 646, 116, false, v => {
                if (analysisView) { analysisView.Measuring = v; analysisView.ClearProbes(); }
            });
            analysisOrbit = AnalysisToggle(overlay, "环绕观察", 760, 116, false, v => {
                if (analysisView) analysisView.AutoRotate = v;
            });
            analysisPlayback = AnalysisToggle(overlay, "载荷往复", 874, 116, false, _ => { });
            Command(overlay, "正视", 994,116,52, () => analysisView.Preset(0),30);
            Command(overlay, "俯视", 1052,116,52, () => analysisView.Preset(1),30);
            Command(overlay, "侧视", 1110,116,52, () => analysisView.Preset(2),30);
            Command(overlay, "复位", 1168,116,52, () => analysisView.ResetView(),30);
            Command(overlay, "极值定位", 1226,116,82, () => analysisView.FocusPeak(),30);
            Command(overlay, "导出测点", 1314,116,98, ExportAnalysisProbes,30);
        }

        void BuildEquipmentContext(RectTransform parent, WorkshopDevice device)
        {
            var context = Panel(parent, "Equipment context", Vector2.zero, Vector2.zero,
                new Vector2(12,12),new Vector2(236,198),Surface);
            Label(context, device.deviceId+"  整机外观",12,Ink,8,2,208,24);
            var image = Rect(context,"Equipment appearance",Vector2.zero,Vector2.one,
                new Vector2(4,36),new Vector2(-4,-27));
            image.gameObject.AddComponent<RawImage>();
            equipmentContext = image.gameObject.AddComponent<WorkshopEquipmentContextView>();
            equipmentContext.Initialize(device);
            Command(context,"拆分观察",8,154,88,() => equipmentContext.ToggleExplosion(),26);
            Command(context,"保存快照",108,154,106,SaveAnalysisSnapshot,26);
        }

        void BuildAnalysisDetail(RectTransform parent, WorkshopAnalysisResult result)
        {
            if (result.variable != "notch" && result.variable != "thin") return;
            var frame = Panel(parent, "Defect closeup", Vector2.one, Vector2.one,
                new Vector2(-294, -338), new Vector2(-12, -144), Surface);
            Label(frame, result.variable == "notch" ? "裂口局部 / 同一求解网格" : "减薄内壁 / 同一求解网格",
                12, Ink, 8, 2, 266, 24);
            var image = Rect(frame, "Interactive defect closeup", Vector2.zero, Vector2.one,
                new Vector2(4, 4), new Vector2(-4, -28)).gameObject.AddComponent<RawImage>();
            analysisView.CreateDetail(image);
        }

        void ExportAnalysisProbes()
        {
            if (analysisView.FractureActive) return;
            string directory = Path.Combine(Application.persistentDataPath,"AnalysisExports");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory,analysisResult.code+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv");
            File.WriteAllText(path,"# source="+analysisResult.source+"\n# sha256="+analysisResult.sha256+"\n"+analysisView.ProbeCsv(),
                new System.Text.UTF8Encoding(true));
            analysisProbe.text = "已导出 "+analysisView.ProbeCount+" 个测点："+path;
        }

        void SaveAnalysisSnapshot()
        {
            string directory = Path.Combine(Application.persistentDataPath,"AnalysisExports");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory,analysisResult.code+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png");
            ScreenCapture.CaptureScreenshot(path);
            analysisProbe.text = "快照已请求保存："+path;
        }

        void UpdateAnalysisPlayback()
        {
            if (UpdateFracturePlayback()) return;
            if (!analysisDialog || !analysisDialog.activeInHierarchy || !analysisPlayback || !analysisPlayback.isOn ||
                !GetComponent<Canvas>().enabled || Time.unscaledTime < nextAnalysisFrame) return;
            nextAnalysisFrame = Time.unscaledTime+.12f;
            analysisLoadSlider.value = .25f+1.25f*(.5f+.5f*Mathf.Sin(Time.unscaledTime*.85f));
        }

        void AnalysisThicknessGauge(RectTransform parent, WorkshopAnalysisResult result)
        {
            bool tube = result.code == "TK-LEAK" || result.code == "M-BEAR" || result.code == "P-BEAR";
            bool notch = result.code == "F-CRACK" || result.code == "P-SEAL" || result.code == "TK-SUPPORT";
            if (!tube && !notch) return;
            var values = new float[2];
            for (int k = 0; k < 2; k++)
            {
                var m = result.models[k];
                float start = float.MaxValue, end = float.MinValue;
                for (int i = 0; i < m.nodes; i++) { float x = m.Point(i).x; start = Mathf.Min(start, x); end = Mathf.Max(end, x); }
                float low = float.MaxValue, high = float.MinValue;
                for (int i = 0; i < m.nodes; i++)
                {
                    Vector3 p = m.Point(i);
                    float location = (p.x - start) / (end - start);
                    if (tube ? Mathf.Abs(location - .5f) > .0001f || Mathf.Abs(p.y) > 1e-6f || p.z <= 0 :
                        location <= .16f + .0001f || location >= .20f - .0001f) continue;
                    float coordinate = tube ? p.z : p.y;
                    low = Mathf.Min(low, coordinate); high = Mathf.Max(high, coordinate);
                }
                if (high <= low) return;
                values[k] = (high - low) * 1000;
            }
            var gauge = Panel(parent, "Mesh thickness comparison", Vector2.one, Vector2.one,
                new Vector2(-294, -136), new Vector2(-12, -12), Surface, false);
            Label(gauge, tube ? "减薄处壁厚 / mm" : "裂口处剩余厚度 / mm", 13, Ink, 12, 5, 258, 24);
            for (int k = 0; k < 2; k++)
            {
                Label(gauge, (k == 0 ? "完好  " : "故障  ") + values[k].ToString("F3"), 12,
                    Ink, 12, 32 + k * 35, 258, 20);
                Box(gauge, "Thickness " + k, 12, 55 + k * 35, 258 * values[k] / values[0],
                    7, k == 0 ? Green : Yellow, false);
            }
        }

        static string Percent(float baseline, float fault) => Mathf.Abs(baseline) < 1e-10f ? "—" :
            ((fault / baseline - 1) * 100).ToString("+0.00;-0.00;0.00") + "%";

        void UpdateAnalysis()
        {
            if (preparingAnalysis || analysisView == null || analysisResult == null) return;
            if (analysisView.FractureActive)
            {
                analysisView.EndFracture();
                SetFractureControls(false);
            }
            var r = analysisResult;
            var a = r.models[0]; var b = r.models[1]; var m = r.models[analysisModel];
            float load = analysisLoadSlider.value, scale = Mathf.Pow(10, analysisScaleSlider.value);
            analysisView.Section = analysisSection.isOn;
            analysisCut.interactable = analysisSection.isOn;
            analysisView.Cut = analysisCut.value;
            analysisView.Smooth = analysisSmooth.isOn;
            analysisView.Contours = analysisContours.isOn;
            analysisView.ShowAnnotations = analysisAnnotations.isOn;
            analysisView.Configure(analysisModel, analysisField, load, scale, analysisDeform.isOn, analysisWire.isOn);
            analysisQuantity.SetValueWithoutNotify(analysisQuantityIds.IndexOf(analysisField));
            RefreshCompactAnalysis();
            Segment(normalModel, analysisModel == 0); Segment(faultModel, analysisModel == 1);
            Segment(stressField, analysisField == 0); Segment(displacementField, analysisField == 1);
            Segment(temperatureField, analysisField == 2);
            analysisLoad.text = (r.analysis == "thermal" ? "热源倍率" : "载荷倍率") + $"  {load:F2}×";
            analysisScale.text = $"变形放大  {scale:F1}×" + (analysisDeform.isOn ? "" : "（关闭）");
            analysisNumbers.text = r.analysis == "thermal" ?
                $"峰值温度：基准 {25+(a.maxTemperature-25)*load:F2} → 故障 {25+(b.maxTemperature-25)*load:F2} °C\n" +
                $"温升变化 {Percent(a.maxTemperature-25,b.maxTemperature-25)}；温差 {(b.maxTemperature-a.maxTemperature)*load:F2} °C" :
                $"最大单元均值应力：{a.maxStress*load:F3} → {b.maxStress*load:F3} MPa（{Percent(a.maxStress,b.maxStress)}）\n" +
                $"最大位移：{a.maxDisplacement*load:F5} → {b.maxDisplacement*load:F5} mm（{Percent(a.maxDisplacement,b.maxDisplacement)}）";
            analysisNumbers.text += r.analysis == "thermal" ?
                $"\n热源 / 散热 {m.powerW*load:F3} / {m.heatOutW*load:F3} W；热平衡误差 {m.balance:E2}" :
                $"\n应变能 {m.energy*load*load:G5} J；体积 {m.volumeM3*1e6:F2} cm³；反力平衡误差 {m.balance:E2}";
            r.Range(analysisField,out float low,out float high);
            float start = analysisField == 2 ? 25+(low-25)*load : low*load;
            float end = analysisField == 2 ? 25+(high-25)*load : high*load;
            analysisLegend.text = $"{start:G5}       {WorkshopAnalysisResult.FieldNames[analysisField]} / {WorkshopAnalysisResult.Unit(analysisField)}" +
                $"       {end:G5}    共用色标 · "+(analysisSmooth.isOn ? "面积加权节点平均显示" : "原始单元显示");
            analysisProbe.text = "探针：—";
            analysisDetails.text = r.component + "\n\n几何 / 缺陷\n" + r.geometry + "\n" + r.change +
                "\n\n物理机制\n" + r.mechanism + "\n\n材料\n" + r.material + "\n\n载荷与约束\n" + r.boundary +
                "\n\n当前显示\n" + m.label + $"；{m.nodes:N0} 节点 / {m.elements:N0} 四面体\n" + m.elementType + "\n" +
                (analysisSection.isOn ? "半剖：隐藏半侧表面以观察内壁；不是新增损伤。\n" : "") +
                $"相对方程残差 {m.residual:E2}\n守恒误差 {m.balance:E2}" +
                AnalysisExtraMetrics(m,load) +
                "\n\n求解器\n" + r.solver + "\nSI 输入；位移 mm / 应力 MPa / 温度 °C\n" +
                "倍率按线性方程缩放；不是新增非线性求解。基准与故障共用色标。" +
                "\n\n适用边界\n" + r.limitation + "\n\n结果 SHA256\n" + r.sha256;
            Canvas.ForceUpdateCanvases();
            analysisDetails.rectTransform.sizeDelta = new Vector2(316, 900);
            float height = Mathf.Max(900, analysisDetails.preferredHeight + 20);
            analysisDetails.rectTransform.sizeDelta = new Vector2(316, height);
            analysisSpecsContent.sizeDelta = new Vector2(0, height);
        }

        string AnalysisExtraMetrics(WorkshopAnalysisMesh m,float load)
        {
            if (analysisResult.analysis == "thermal")
            {
                float minimum = float.MaxValue, sum = 0, maxFlux = 0;
                foreach (float t in m.temperature) { minimum = Mathf.Min(minimum,t); sum += t; }
                for (int f = 0; f < m.tri.Length/3; f++) maxFlux = Mathf.Max(maxFlux,m.FaceValue(17,f,0));
                return $"\n\n热学统计\n最低温度 {25+(minimum-25)*load:F3} °C\n节点算术平均 {25+(sum/m.nodes-25)*load:F3} °C" +
                    $"\n表面最大单元热流 {maxFlux*load:F2} W/m²\n导热系数 45 W/(m·K)\n热源 {m.powerW*load:F3} W\n散热 {m.heatOutW*load:F3} W";
            }
            Vector3 maxU = Vector3.zero;
            for (int i = 0; i < m.nodes; i++)
                maxU = Vector3.Max(maxU,new Vector3(Mathf.Abs(m.u[i*3]),Mathf.Abs(m.u[i*3+1]),Mathf.Abs(m.u[i*3+2]))*1000f);
            var s = "\n\n结构统计 / 工程 xyz\n";
            s += $"|Ux|max {maxU.x*load:F6} mm\n|Uy|max {maxU.y*load:F6} mm\n|Uz|max {maxU.z*load:F6} mm";
            for (int k = 0; k < 3; k++)
                s += $"\nF{(char)('x'+k)} / R{(char)('x'+k)} = {m.appliedN[k]*load:G5} / {m.reactionN[k]*load:G5} N";
            float maxPrincipal = float.MinValue, minPrincipal = float.MaxValue;
            for (int i = 0; i < m.principal.Length; i+=3)
            {
                maxPrincipal = Mathf.Max(maxPrincipal,m.principal[i]);
                minPrincipal = Mathf.Min(minPrincipal,m.principal[i+2]);
            }
            return s+$"\n表面最大主应力 {maxPrincipal*load:F4} MPa\n表面最小主应力 {minPrincipal*load:F4} MPa" +
                $"\n应变能 {m.energy*load*load:G6} J\n参考体积 {m.volumeM3*1e6:F3} cm³" +
                "\n等效偏应变 = Mises / (3G)\n整机外观与参考件未作 CAD 配准。\n拆分仅用于观察原有美术部件。\n载荷往复为线性准静态显示，不是疲劳或动力学求解。";
        }
    }
}
