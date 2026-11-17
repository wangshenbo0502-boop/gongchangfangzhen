using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace WorkshopDemo
{
    public sealed partial class WorkshopUI
    {
        GameObject equipmentDialog;
        WorkshopEquipmentView equipmentView;
        WorkshopDevice inspectedDevice;
        Text equipmentStage, equipmentMetrics, equipmentTransport, equipmentAssemblyStep, equipmentSource, equipmentCase;
        Slider equipmentProgress;
        Button equipmentPlay, equipmentPumpSection;
        readonly List<Text> equipmentLabels = new List<Text>();
        readonly List<RectTransform> equipmentLeaders = new List<RectTransform>();
        RectTransform equipmentViewport;
        bool preparingEquipment;
        float nextEquipmentText;
        int inspectedParts, inspectedTriangles;

        void OpenEquipment(WorkshopFault selectedFault = null)
        {
            var device = simulation.Selected;
            if (!device) return;
            if (simulation.IsExam(device)) { simulation.Notify("独立考核期间请从现场与诊断记录采集证据"); return; }
            var mode = selectedFault ?? device.Fault ?? (device.kind == DeviceKind.Tank ?
                WorkshopFaultCatalog.Find("TK-LEAK") : SelectedLearningFault());
            if (equipmentDialog) { equipmentDialog.SetActive(false); Destroy(equipmentDialog); }
            preparingEquipment = true;
            inspectedDevice = device;
            equipmentLabels.Clear(); equipmentLeaders.Clear();
            var overlay = Panel(transform,"Equipment inspection",Vector2.zero,Vector2.one,
                Vector2.zero,Vector2.zero,new Color(Background.r,Background.g,Background.b,1));
            equipmentDialog = overlay.gameObject;
            Label(overlay,device.deviceId+"  "+device.displayName+"  /  整机与部件",20,Ink,20,10,1000,30);
            equipmentSource = Label(overlay,"名义设计外形 · 预设损坏过程 · 非实测 CAD / 非断裂求解",12,Yellow,20,43,1080,24);
            var close = Command(overlay,page == 0 ? "返回车间" : page == 1 ? "返回目录" : "返回工况",0,12,90,() => equipmentDialog.SetActive(false));
            var cr = close.GetComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = Vector2.one; cr.anchoredPosition = new Vector2(-65,-28);

            var leftFrame = Rect(overlay,"Equipment specifications",Vector2.zero,Vector2.up,
                new Vector2(20,158),new Vector2(324,-106));
            Scroll(leftFrame, "Assembly parts scroll", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, out var left);
            left.sizeDelta = new Vector2(0, 850);
            equipmentStage = Label(left,"",17,Yellow,0,0,304,48);
            equipmentStage.name = "Equipment stage";
            equipmentMetrics = Label(left,"",13,Ink,0,52,304,300,TextAnchor.UpperLeft);
            equipmentMetrics.name = "Equipment dimensions";
            Divider(left,0,356,304);
            equipmentAssemblyStep = Label(left,"",13,Ink,0,365,304,28);
            int partCount = device.GetComponentInChildren<WorkshopEquipmentModel>()?.AssemblyCount ??
                (device.kind == DeviceKind.Pump ? 7 : device.kind == DeviceKind.Tank ? 6 :
                device.kind == DeviceKind.Fan ? 5 : device.kind == DeviceKind.Cabinet ? 4 : 2);
            for (int i = 0; i < partCount; i++)
            {
                int index = i;
                Command(left,(i+1).ToString("D2"),0,402+i*32,304,
                    () => { if (equipmentView.DisplayModel == equipmentView.Model) equipmentView.FocusAssembly(index); },28);
            }
            Command(overlay,"逐步拆解",348,76,96,() =>
                equipmentView.SetDisassemblyTarget(equipmentView.TargetDisassembly+1),30);
            Command(overlay,"逐步装回",450,76,96,() =>
                equipmentView.SetDisassemblyTarget(equipmentView.TargetDisassembly-1),30);
            Command(overlay,"全部拆开",552,76,96,() =>
                equipmentView.SetDisassemblyTarget(equipmentView.Model.AssemblyCount),30);
            Command(overlay,"重新组装",654,76,96,() => equipmentView.SetDisassemblyTarget(0),30);
            Command(overlay,"整机视角",766,76,88,() => equipmentView.ResetView(),30);
            Command(overlay,"故障近景",860,76,88,() => equipmentView.FocusDamage(),30);
            Command(overlay,"正视",954,76,52,() => equipmentView.Preset(0),30);
            Command(overlay,"俯视",1012,76,52,() => equipmentView.Preset(1),30);
            Command(overlay,"侧视",1070,76,52,() => equipmentView.Preset(2),30);
            AnalysisToggle(overlay,"部件标注",1132,76,false,v => equipmentView.ShowLabels = v);
            AnalysisToggle(overlay,"自动环绕",1246,76,false,v => equipmentView.AutoOrbit = v);
            equipmentViewport = Panel(overlay,"Equipment viewport",Vector2.zero,Vector2.one,
                new Vector2(348,158),new Vector2(-20,-116),new Color(.18f,.20f,.21f));
            equipmentViewport.gameObject.AddComponent<RectMask2D>();
            var raw = Rect(equipmentViewport,"Interactive equipment",Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero);
            raw.gameObject.AddComponent<RawImage>();
            var inset = Panel(equipmentViewport,"Same mesh closeup",Vector2.one,Vector2.one,
                new Vector2(-278,-246),new Vector2(-12,-12),Surface,false);
            Label(inset,"故障部位近景",12,Ink,8,2,252,25);
            var image = Rect(inset,"Damage closeup",Vector2.zero,Vector2.one,
                new Vector2(4,4),new Vector2(-4,-29)).gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            equipmentView = raw.gameObject.AddComponent<WorkshopEquipmentView>();
            equipmentView.Initialize(device,mode,image);
            equipmentView.ShowLabels = false;
            inspectedParts = equipmentView.Model.PartCount;
            inspectedTriangles = equipmentView.Model.TriangleCount;
            for (int i = 0; i < equipmentView.Model.AssemblyCount; i++)
            {
                var label = Label(equipmentViewport,equipmentView.Model.AssemblyLabel(i),11,Ink,
                    12,16+i*30,230,24);
                label.gameObject.SetActive(false);
                equipmentLabels.Add(label);
                var leader = Box(equipmentViewport,"Part leader "+i,0,0,1,1,new Color(.58f,.65f,.66f,.65f),false);
                leader.gameObject.SetActive(false); equipmentLeaders.Add(leader);
                var button = left.Find((i+1).ToString("D2")).GetComponentInChildren<Text>();
                button.text = equipmentView.Model.AssemblyLabel(i);
                button.alignment = TextAnchor.MiddleLeft;
            }
            if (device.kind == DeviceKind.Cabinet && !equipmentView.Model.ReviewedCabinetModel)
                AnalysisToggle(overlay,"柜门开启",20,76,true,v => equipmentView.OpenDoor = v);
            if (device.kind == DeviceKind.Fan && !equipmentView.Model.ReviewedFanModel)
                AnalysisToggle(overlay,"移开护网",20,76,true,v => equipmentView.RemoveGuard = v);
            equipmentTransport = Label(overlay,"",13,Ink,348,0,1000,30);
            var tr = equipmentTransport.rectTransform;
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.right;
            tr.offsetMin = new Vector2(348,116); tr.offsetMax = new Vector2(-20,146);
            var transport = Panel(overlay,"Equipment transport",Vector2.zero,Vector2.right,
                new Vector2(20,16),new Vector2(-20,108),Surface);
            equipmentPlay = Command(transport,"播放演化",12,10,98,() => {
                if (equipmentView.Progress >= 1) equipmentView.SetProgress(0);
                equipmentView.Playing = !equipmentView.Playing;
                UpdateEquipmentText();
            },30);
            Command(transport,"从头重播",118,10,98,() => {
                equipmentView.SetProgress(0); equipmentView.Playing = true; UpdateEquipmentText();
            },30);
            Command(transport,"完好状态",224,10,98,() => EquipmentScrub(0),30);
            Command(transport,"预设起裂",330,10,98,() => EquipmentScrub(WorkshopEquipmentModel.CrackStart),30);
            Command(transport,"最终状态",436,10,98,() => EquipmentScrub(1),30);
            Command(transport,"保存画面",542,10,98,SaveEquipmentSnapshot,30);
            Command(transport,"导出尺寸",648,10,98,ExportEquipmentDimensions,30);
            equipmentCase = Label(transport,mode.Code+"  "+mode.Name,13,Yellow,720,10,585,30);
            var track = Panel(transport,"Equipment progress",Vector2.zero,Vector2.right,
                new Vector2(18,14),new Vector2(-18,37),Background);
            equipmentProgress = track.gameObject.AddComponent<Slider>();
            equipmentProgress.minValue = 0; equipmentProgress.maxValue = 1;
            var rail = Panel(track,"Track",new Vector2(0,.5f),new Vector2(1,.5f),
                new Vector2(0,-2),new Vector2(0,2),Line,false);
            var handleArea = Rect(track,"Handle area",Vector2.zero,Vector2.one,
                new Vector2(7,0),new Vector2(-7,0));
            var handle = Panel(handleArea,"Handle",Vector2.zero,Vector2.one,
                new Vector2(-6,1),new Vector2(6,-1),Yellow);
            equipmentProgress.handleRect = handle; equipmentProgress.targetGraphic = handle.GetComponent<Image>();
            equipmentProgress.navigation = new Navigation { mode = Navigation.Mode.None };
            equipmentProgress.onValueChanged.AddListener(v => {
                if (!preparingEquipment) EquipmentScrub(v);
            });
            CompactEquipmentLayout(overlay);
            preparingEquipment = false;
            UpdateEquipmentText();
        }

        void EquipmentScrub(float value)
        {
            equipmentView.Playing = false;
            equipmentView.SetProgress(value);
            UpdateEquipmentText();
        }

        void UpdateEquipmentPlayback()
        {
            if (!equipmentDialog || !equipmentDialog.activeInHierarchy || !GetComponent<Canvas>().enabled) return;
            if (Time.unscaledTime >= nextEquipmentText)
            {
                nextEquipmentText = Time.unscaledTime+.1f;
                UpdateEquipmentText();
            }
            var r = equipmentViewport.rect;
            for (int i = 0; i < equipmentLabels.Count; i++)
            {
                bool shown = equipmentView.ShowLabels && equipmentView.DisplayModel == equipmentView.Model;
                equipmentLabels[i].gameObject.SetActive(shown);
                equipmentLeaders[i].gameObject.SetActive(shown);
                if (!shown) continue;
                Vector2 p = equipmentView.ProjectAssembly(i);
                Vector2 endpoint = new Vector2(Mathf.Clamp(p.x*r.width,260,r.width-20),
                    Mathf.Clamp((1-p.y)*r.height,20,r.height-20));
                Vector2 start = new Vector2(244,28+i*30), delta = endpoint-start;
                var line = equipmentLeaders[i];
                line.pivot = new Vector2(0,.5f);
                line.sizeDelta = new Vector2(delta.magnitude,1);
                line.anchoredPosition = new Vector2(start.x,-start.y);
                line.localRotation = Quaternion.Euler(0,0,-Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            }
        }

        void UpdateEquipmentText()
        {
            var m = equipmentView.DisplayModel;
            bool tankFault = m != equipmentView.Model;
            float p = equipmentView.Progress;
            var profile = equipmentView.Profile;
            var frame = m.FaultFrame;
            equipmentCloseup.SetActive(equipmentView.FaultMode && !equipmentView.WaitingForAssembly &&
                m.TryGetFaultFocus(out _, out _) &&
                ((profile != WorkshopFaultProfile.PumpCavitation && equipmentView.Fault.Code != "P-IMPELLER") ||
                    equipmentView.PumpSectionOpen));
            if (equipmentPumpSection)
            {
                equipmentPumpSection.gameObject.SetActive(equipmentView.FaultMode &&
                    (profile == WorkshopFaultProfile.PumpSeal || profile == WorkshopFaultProfile.PumpCavitation ||
                        equipmentView.Fault.Code == "P-IMPELLER" || equipmentView.Fault.Code == "P-BEAR"));
                equipmentPumpSection.interactable = equipmentView.CanShowPumpSection;
                equipmentPumpSection.GetComponentInChildren<Text>().text = equipmentView.PumpSectionOpen ? "关闭剖视" :
                    profile == WorkshopFaultProfile.PumpSeal ? "看密封" :
                    equipmentView.Fault.Code == "P-IMPELLER" ? "看叶轮" :
                    equipmentView.Fault.Code == "P-BEAR" ? "看轴承" : "看流道";
            }
            equipmentStage.text = equipmentView.WaitingForAssembly ? "正在沿拆装路径回装" :
                frame.HasValue ? (m.FaultPresentationAvailable ? frame.Value.StageLabel : "故障部件绑定缺失") :
                m.ReviewedFanModel && p >= 1 && m.HasOpening ? "裂口张开 / 后缘保持连接" :
                p == 0 ? "完好状态" : m.HasOpening ? p < WorkshopEquipmentModel.CrackStart ?
                "逐渐变形 / 裂缝闭合" : p < 1 ? "裂纹扩展 / 开口增大" : "贯通开口 / 末态保持" : m.Stage;
            var size = m.NativeBounds.size*1000;
            equipmentMetrics.text = "名义外形 / 非实测尺寸\n"+size.x.ToString("F0")+" × "+
                size.y.ToString("F0")+" × "+size.z.ToString("F0")+" mm\n"+
                (tankFault ? "旧版储罐教学模型\n\n" : inspectedParts+" 个外观零件 / "+m.AssemblyCount+" 个拆解组件\n"+
                inspectedTriangles.ToString("N0")+" 个表面三角形\n\n");
            if (equipmentView.FaultMode && profile != null)
            {
                equipmentMetrics.text += "部位：" + profile.Component + "\n";
                if (m.LocalFaultPresenter?.ActiveMechanismLines > 0)
                    equipmentMetrics.text += "辅助示意：青色为参考，橙色为异常\n";
                if (equipmentView.Fault.Code == "F-BLADE")
                    equipmentMetrics.text += "外缘预设退缩 ≤6 mm（非磨蚀速率）\n";
                if (equipmentView.MotionScale != 1) equipmentMetrics.text += "振动显示放大 " + equipmentView.MotionScale + "×（读数不变）\n";
                if (frame.HasValue)
                {
                    foreach (var entry in frame.Value.Readouts())
                        equipmentMetrics.text += entry.Value.HasValue ?
                            (entry.Key == "tank_level" ? "预览" : "") + entry.Label + "  " +
                            entry.Value.Value.ToString(entry.Unit == "MPa" ? "F3" : "F1") + " " + entry.Unit +
                            "（" + entry.Source + "）\n" : entry.Label + entry.Source + "\n";
                    if (profile == WorkshopFaultProfile.PumpSeal)
                        equipmentMetrics.text += "剖视可查看静环、动环与局部磨痕";
                    else if (profile == WorkshopFaultProfile.PumpCavitation)
                        equipmentMetrics.text += "汽泡、声响和波动为教学示意";
                }
            }
            else if (equipmentView.FaultMode && !equipmentView.WaitingForAssembly)
                equipmentMetrics.text += "部位："+m.ComponentName+"\n"+
                "当前开口  "+m.OpeningMm.ToString("F3")+" mm\n"+
                "弯曲幅值  "+m.DeflectionMm.ToString("F3")+" mm\n"+
                (m.ReviewedFanModel ? "原叶片预制裂口 / 保留后缘连接\n非断裂求解，不提供裂长与厚度测量\n\n" :
                "裂缝延伸  "+m.CrackLengthMm.ToString("F3")+" mm\n"+
                "未裂长度  "+m.RemainingMm.ToString("F3")+" mm\n"+
                "部件厚度  "+m.ThicknessMm.ToString("F3")+" mm\n\n")+
                "工况设定温度  "+equipmentView.Fault.Temperature.ToString("F1")+" °C\n"+
                "设备当前温度  "+inspectedDevice.Temperature.ToString("F1")+" °C\n"+
                (m.HasOpening ? "预设起裂进度  55%（非强度极限）" : "此工况不预设结构断裂");
            equipmentAssemblyStep.text = "部件层级   "+equipmentView.Disassembly.ToString("F1")+" / "+m.AssemblyCount;
            float duration = equipmentView.Duration;
            equipmentTransport.text = equipmentView.WaitingForAssembly ? "正在回装，完成后应用所选进度或开始播放" :
                (p*duration).ToString("F1")+" / "+duration.ToString("F1")+" s    "+(p*100).ToString("F1")+"%    "+m.Recipe;
            equipmentProgress.SetValueWithoutNotify(p);
            equipmentCase.text = equipmentView.Fault.Code + "  " + equipmentView.Fault.Name;
            equipmentPlay.GetComponentInChildren<Text>().text = equipmentView.Playing ?
                (equipmentView.WaitingForAssembly ? "取消待播" : "暂停演化") : "播放演化";
        }

        void SaveEquipmentSnapshot()
        {
            string directory = Path.Combine(Application.persistentDataPath,"EquipmentExports");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory,inspectedDevice.deviceId+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".png");
            ScreenCapture.CaptureScreenshot(path);
            equipmentSource.text = "画面保存至 EquipmentExports / "+Path.GetFileName(path);
        }

        void ExportEquipmentDimensions()
        {
            string directory = Path.Combine(Application.persistentDataPath,"EquipmentExports");
            Directory.CreateDirectory(directory);
            var m = equipmentView.DisplayModel;
            string path = Path.Combine(directory,inspectedDevice.deviceId+"-"+equipmentView.Fault.Code+"-"+
                DateTime.Now.ToString("yyyyMMdd-HHmmss")+".csv");
            var c = System.Globalization.CultureInfo.InvariantCulture;
            if (equipmentView.Profile != null)
            {
                var frame = m.FaultFrame ?? equipmentView.Profile.Evaluate(0, 0, null);
                var csv = new System.Text.StringBuilder("schemaVersion,deviceId,faultCode,stageId,previewSeconds,metricKey,value,unit,source,available,visualScale\n");
                foreach (var readout in frame.Readouts())
                    csv.Append("1,").Append(inspectedDevice.deviceId).Append(',').Append(frame.Code).Append(',')
                        .Append(frame.StageId).Append(',').Append(frame.Seconds.ToString(c)).Append(',').Append(readout.Key)
                        .Append(',').Append(readout.Value?.ToString(c)).Append(',').Append(readout.Unit).Append(',')
                        .Append('"').Append(readout.Source.Replace("\"", "\"\"")).Append('"').Append(',')
                        .Append(readout.Value.HasValue ? "true" : "false").Append(',')
                        .Append(equipmentView.MotionScale.ToString(c)).Append('\n');
                File.WriteAllText(path, csv.ToString(), new System.Text.UTF8Encoding(true));
                equipmentSource.text = "案例读数已导出至 EquipmentExports / " + Path.GetFileName(path);
                return;
            }
            File.WriteAllText(path,"# Authored nominal geometry; prescribed kinematics; not a fracture solution\n"+
                "device,fault,progress,opening_mm,bend_amplitude_mm,crack_length_mm,uncracked_length_mm,thickness_mm\n"+
                inspectedDevice.deviceId+","+equipmentView.Fault.Code+","+equipmentView.Progress.ToString(c)+","+
                m.OpeningMm.ToString(c)+","+m.DeflectionMm.ToString(c)+","+m.CrackLengthMm.ToString(c)+","+
                m.RemainingMm.ToString(c)+","+m.ThicknessMm.ToString(c)+"\n",new System.Text.UTF8Encoding(true));
            equipmentSource.text = "名义几何尺寸已导出至 EquipmentExports / "+Path.GetFileName(path);
        }
    }
}
