using UnityEngine;
using UnityEngine.UI;

namespace WorkshopDemo
{
    public sealed partial class WorkshopUI
    {
        Button fractureMode, fracturePlay;
        Slider fractureTimeline;
        GameObject fractureFooter, fractureGauge, fractureParameters;
        Text fractureStatus, fractureMetrics, fractureDetailTitle, fractureModeHint;
        RectTransform analysisFooter;

        void BuildFractureControls(RectTransform overlay, RectTransform viewport)
        {
            fractureMode = null;
            if (analysisResult.variable != "notch") return;
            fractureMode = Command(overlay,"渐进开裂",832,75,136,ToggleFracture);
            fractureModeHint = Label(overlay,"过程示意 · 非裂纹扩展求解",12,Muted,982,75,400,32);
            var footer = Rect(overlay,"Fracture transport",Vector2.zero,Vector2.right,
                new Vector2(364,18),new Vector2(-20,128));
            fractureFooter = footer.gameObject;
            fracturePlay = Command(footer,"暂停",0,0,78,() => {
                if (analysisView.FractureProgress >= 1) analysisView.SetFractureProgress(0);
                analysisView.FracturePlaying = !analysisView.FracturePlaying;
                RefreshFractureReadout();
            });
            Command(footer,"重播",86,0,78,() => {
                analysisView.SetFractureProgress(0);
                analysisView.FracturePlaying = true;
                RefreshFractureReadout();
            });
            Command(footer,"起裂时刻",172,0,92,() => ScrubFracture(WorkshopAnalysisView.FractureInitiation));
            Command(footer,"最终裂口",272,0,92,() => ScrubFracture(1));
            fractureStatus = Label(footer,"",12,Yellow,378,0,650,32);
            fractureStatus.name = "Fracture stage";
            fractureStatus.rectTransform.anchorMax = Vector2.one;
            fractureStatus.rectTransform.offsetMax = Vector2.zero;
            var track = Rect(footer,"Fracture progress",Vector2.zero,Vector2.one,
                new Vector2(6,42),new Vector2(-6,-40));
            track.gameObject.AddComponent<Image>().color = Color.clear;
            fractureTimeline = track.gameObject.AddComponent<Slider>();
            fractureTimeline.minValue = 0; fractureTimeline.maxValue = 1;
            Panel(track,"Track",new Vector2(0,.5f),new Vector2(1,.5f),
                new Vector2(0,-2),new Vector2(0,2),Line,false);
            Panel(track,"Initiation marker",new Vector2(.55f,0),new Vector2(.55f,1),
                new Vector2(-1,2),new Vector2(1,-2),Yellow,false);
            var handle = Panel(track,"Handle",Vector2.zero,Vector2.up,
                new Vector2(-6,0),new Vector2(6,0),Yellow);
            fractureTimeline.handleRect = handle;
            fractureTimeline.targetGraphic = handle.GetComponent<Image>();
            fractureTimeline.navigation = new Navigation { mode = Navigation.Mode.None };
            fractureTimeline.onValueChanged.AddListener(ScrubFracture);
            var caption = Label(footer,"0 s   受载变形 → 9.9 s 预设起裂 → 18 s 最终裂口保持    |    名义裂口尺寸；形变单独放大",12,Muted,
                0,76,1030,28);
            caption.rectTransform.anchorMax = Vector2.one;
            caption.rectTransform.offsetMax = new Vector2(0,-76);
            var gauge = Panel(viewport,"Evolving crack dimensions",Vector2.one,Vector2.one,
                new Vector2(-294,-136),new Vector2(-12,-12),Surface,false);
            fractureGauge = gauge.gameObject;
            fractureMetrics = Label(gauge,"",13,Ink,12,4,258,116,TextAnchor.UpperLeft);
            fractureMetrics.name = "Fracture dimensions";
            var parameters = Panel(overlay.Find("Analysis data"),"Fracture settings",Vector2.zero,Vector2.right,
                Vector2.zero,new Vector2(0,202),Surface,false);
            fractureParameters = parameters.gameObject;
            Label(parameters,"运动学过程设定\n\n演示时长   18.0 s\n预设起裂   9.9 s / 55%\n形变倍率   独立显示放大\n\n名义裂口尺寸不包含放大形变",12,Ink,
                12,6,300,184,TextAnchor.UpperLeft);
            var detail = viewport.Find("Defect closeup");
            if (detail) fractureDetailTitle = detail.GetComponentInChildren<Text>();
            fractureFooter.SetActive(false);
            fractureGauge.SetActive(false);
            fractureParameters.SetActive(false);
        }

        void ToggleFracture()
        {
            if (analysisView.FractureActive) { UpdateAnalysis(); return; }
            analysisPlayback.isOn = false;
            analysisOrbit.isOn = false;
            analysisMeasure.isOn = false;
            if (!analysisView.BeginFracture()) return;
            SetFractureControls(true);
            analysisDetails.text = analysisResult.component + "\n\n渐进开裂 / 运动学过程示意\n" +
                "先受载弯曲，再在预设时刻起裂，裂口两侧逐渐张开，达到目标后保持。\n\n" +
                "几何参数\n目标裂深 " + (analysisResult.inputValues[1]*1000).ToString("F3") + " mm\n" +
                "目标口宽 " + (analysisResult.dimensionsMm[0]*.04f).ToString("F3") + " mm\n" +
                "裂口贯穿参考件宽度，下部保留连续韧带。\n\n" +
                "过程设定\n演示时长 18 s；起裂 9.9 s（55%）。这是播放时间，不是材料真实破坏时间。\n" +
                "开口与裂深从零连续变化；名义尺寸不含放大形变。\n" +
                "弯曲采用连续的悬臂形状插值，以参考件静态端部位移定标；不是中间时刻的有限元结果。\n\n" +
                "科学边界\n预设起裂点不代表计算得到的材料极限。未求解塑性、断裂韧度、能量释放率或裂纹扩展路径。" +
                "没有中间过程的应力、损伤变量或安全系数。\n\n" +
                "返回静态场可继续读取原有完好/缺陷求解数据；V 形开裂示意与矩形缺口参考算例的几何不同。";
            RefreshFractureReadout();
        }

        void SetFractureControls(bool active)
        {
            analysisFooter.gameObject.SetActive(!active);
            fractureFooter.SetActive(active);
            fractureGauge.SetActive(active);
            fractureParameters.SetActive(active);
            analysisDialog.transform.Find("Analysis data/Analysis parameters").gameObject.SetActive(!active);
            var gauge = analysisView.transform.parent.Find("Mesh thickness comparison");
            if (gauge) gauge.gameObject.SetActive(!active);
            fractureMode.GetComponentInChildren<Text>().text = active ? "返回静态场" : "渐进开裂";
            Segment(fractureMode,active);
            fractureModeHint.text = active ? "几何过程示意 · 预设起裂点" : "过程示意 · 非裂纹扩展求解";
            if (fractureDetailTitle)
                fractureDetailTitle.text = active ? "裂口局部 / 几何过程示意" : "裂口局部 / 同一求解网格";
            foreach (var control in new Selectable[] { analysisQuantity,analysisWire,analysisDeform,analysisSection,
                analysisSmooth,analysisContours,analysisAnnotations,analysisMeasure,analysisPlayback,analysisLoadSlider,
                analysisScaleSlider,analysisCut,stressField,displacementField })
                control.interactable = !active;
            analysisCut.interactable = !active && analysisSection.isOn;
            analysisDialog.transform.Find("导出测点").GetComponent<Button>().interactable = !active;
            analysisDialog.transform.Find("极值定位").GetComponentInChildren<Text>().text = active ? "裂口定位" : "极值定位";
        }

        void ScrubFracture(float progress)
        {
            if (!analysisView || !analysisView.FractureActive) return;
            analysisView.FracturePlaying = false;
            analysisView.SetFractureProgress(progress);
            RefreshFractureReadout();
        }

        void RefreshFractureReadout()
        {
            var v = analysisView;
            fractureTimeline.SetValueWithoutNotify(v.FractureProgress);
            fracturePlay.GetComponentInChildren<Text>().text = v.FracturePlaying ? "暂停" :
                v.FractureProgress >= 1 ? "播放" : "继续";
            fractureStatus.text = $"{v.FractureProgress*WorkshopAnalysisView.FractureDuration:F1} / 18.0 s   " +
                $"{v.FractureProgress*100:F0}%   {v.FractureStage}";
            analysisNumbers.text = $"渐进开裂 · {v.FractureStage}（过程示意）\n" +
                $"名义口宽 {v.FractureOpeningMm:F3} mm    裂深 {v.FractureDepthMm:F3} mm    剩余厚度 {v.FractureRemainingMm:F3} mm\n" +
                $"形变放大 {v.FractureVisualScale:F1}×；预设 55% 起裂；中间过程不显示求解场";
            fractureMetrics.text = $"当前裂口几何 / mm\n\n口宽  {v.FractureOpeningMm:F3}\n" +
                $"裂深  {v.FractureDepthMm:F3}\n剩余  {v.FractureRemainingMm:F3}";
        }

        bool UpdateFracturePlayback()
        {
            if (!analysisView || !analysisView.FractureActive) return false;
            if (analysisDialog.activeInHierarchy && GetComponent<Canvas>().enabled)
            {
                analysisView.AdvanceFracture(Time.unscaledDeltaTime);
                RefreshFractureReadout();
            }
            return true;
        }
    }
}
