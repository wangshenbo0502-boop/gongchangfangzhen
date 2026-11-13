using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WorkshopDemo
{
    public sealed partial class WorkshopUI
    {
        GameObject quickMenu, compactDeviceTabs, compactTrainingTabs;
        RectTransform compactMetrics;
        readonly RectTransform[] sceneFrame = new RectTransform[4];
        readonly List<Button> workspaceButtons = new List<Button>();
        Button selectedAction, faultAction;
        GameObject analysisToolDrawer;
        readonly List<GameObject> analysisToolPages = new List<GameObject>();
        Dropdown analysisComparison;
        Button analysisExportButton, analysisPeakButton;
        RectTransform analysisParameterPanel;
        Transform analysisContextPanel;
        GameObject analysisScaleGroup, analysisCutGroup;
        GameObject equipmentAssemblyTools, equipmentFaultTools, equipmentPartsPanel, equipmentCloseup;
        readonly List<Button> equipmentModeButtons = new List<Button>();
        int equipmentModeIndex;

        void Place(Transform item, Transform parent, float x, float y, float width, float height)
        {
            var r = (RectTransform)item;
            r.SetParent(parent, false);
            r.anchorMin = r.anchorMax = Vector2.up;
            r.pivot = new Vector2(0, 1);
            r.anchoredPosition = new Vector2(x, -y);
            r.sizeDelta = new Vector2(width, height);
        }

        void BuildCompactShell()
        {
            Box(transform, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Background, false);
            for (int i = 0; i < sceneFrame.Length; i++)
                sceneFrame[i] = Box(transform, "Scene frame " + i, Vector2.zero, Vector2.zero,
                    Vector2.zero, Vector2.zero, Background, false);
            var rail = Box(transform, "Navigation", Vector2.zero, Vector2.up, Vector2.zero, new Vector2(180, 0), panel);
            Box(rail, "Brand accent", 22, 27, 4, 44, amber, false);
            Label(rail, "工厂实训", 24, ink, 38, 22, 132, 36);
            Label(rail, "WORKSHOP", 12, muted, 38, 59, 132, 22);
            Label(rail, "工作空间", 13, muted, 22, 126, 140, 26);
            var names = new[] { "车间总览", "设备学习", "实训任务" };
            var targets = new[] { 0, 1, 2 };
            for (int i = 0; i < names.Length; i++)
            {
                int target = targets[i];
                workspaceButtons.Add(Command(rail, names[i], 14, 168 + i * 64, 152, () => Navigate(target), false, 48));
            }
            Divider(rail, 22, 366, 136);
            workspaceButtons.Add(Command(rail, "AI 助手", 14, 388, 152, () => Navigate(4), false, 48));
            Command(rail, "更多", 14, 452, 152, () => OpenQuickMenu("平台工具",
                ("资料管理", () => Navigate(6)),
                (AudioListener.volume > .01f ? "静音（M）" : "恢复声音（M）", () => AudioListener.volume = AudioListener.volume > .01f ? 0 : 1),
                ("操作帮助", ShowControlsHelp)), false, 48);
            var note = Label(rail, "本地模拟\n4 台设备 · 1 台小车", 13, muted, 22, 0, 144, 58);
            note.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, 26, 58);
            var header = Box(transform, "Header", Vector2.up, Vector2.one,
                new Vector2(180, -84), Vector2.zero, panel);
            title = Label(header, "车间总览", 26, ink, 24, 16, 500, 42);
            clock = Label(header, "", 13, muted, 0, 0, 230, 42, TextAnchor.MiddleRight);
            var clockRect = clock.rectTransform;
            clockRect.anchorMin = clockRect.anchorMax = Vector2.one;
            clockRect.offsetMin = new Vector2(-254, -60); clockRect.offsetMax = new Vector2(-24, -18);

            compactMetrics = Rect(transform, "Metrics", Vector2.up, Vector2.one,
                new Vector2(204, -144), new Vector2(-24, -96));
            total = CompactKpi(0, "设备", ink); running = CompactKpi(1, "运行", ink);
            alarms = CompactKpi(2, "活动告警", amber); accepted = CompactKpi(3, "已验收", ink);
            compactDeviceTabs = Rect(transform, "Device navigation", Vector2.up, Vector2.one,
                new Vector2(204, -144), new Vector2(-24, -96)).gameObject;
            Command(compactDeviceTabs.transform, "设备目录", 0, 0, 126, () => Navigate(1));
            Command(compactDeviceTabs.transform, "故障学习", 138, 0, 126, () => Navigate(5));
            compactTrainingTabs = Rect(transform, "Training navigation", Vector2.up, Vector2.one,
                new Vector2(204, -144), new Vector2(-24, -96)).gameObject;
            Command(compactTrainingTabs.transform, "故障处置", 0, 0, 126, () => Navigate(2));
            Command(compactTrainingTabs.transform, "实训复盘", 138, 0, 126, () => Navigate(3));
            Command(compactTrainingTabs.transform, "小车装配", 276, 0, 126, () => {
                var switcher = FindFirstObjectByType<WorkshopSceneSwitcher>();
                if (switcher) switcher.SwitchTo(WorkshopSceneSwitcher.AssemblyScene);
            });
            Command(compactTrainingTabs.transform, "新建课题", 430, 0, 126, OpenFaultDialog, true);
            Command(compactTrainingTabs.transform, "会话", 568, 0, 92, () => OpenQuickMenu("本次实训",
                ("结束并重置…", () => OpenQuickMenu("清空本次告警和记录，恢复设备与小车？",
                    ("确认重置", () => { simulation.ResetDemo(); editingOrder = null; rowSignature = ""; Refresh(); })))));
            feedback = Label(transform, "", 13, muted, 204, 0, 1000, 30);
            feedback.rectTransform.anchorMin = Vector2.zero; feedback.rectTransform.anchorMax = Vector2.right;
            feedback.rectTransform.offsetMin = new Vector2(204, 4); feedback.rectTransform.offsetMax = new Vector2(-24, 34);
            BuildOverview(); BuildLedger(); BuildOrders(); BuildReview(); BuildKnowledge();
            BuildAnalysis(); BuildImport(); BuildFaultDialog();
        }

        Text CompactKpi(int index, string caption, Color color)
        {
            var slot = Rect(compactMetrics, caption, new Vector2(index / 4f, 0), new Vector2((index + 1) / 4f, 1),
                Vector2.zero, Vector2.zero);
            Label(slot, caption, 14, muted, 0, 6, 100, 32);
            return Label(slot, "00", 25, color, 104, 1, 100, 42);
        }

        void RefreshCompactNavigation()
        {
            CloseQuickMenu();
            int group = page == 0 ? 0 : page == 1 || page == 5 ? 1 : page == 2 || page == 3 ? 2 : page == 4 ? 3 : -1;
            for (int i = 0; i < workspaceButtons.Count; i++) StyleSelection(workspaceButtons[i], i == group);
            compactMetrics.gameObject.SetActive(page == 0);
            compactDeviceTabs.SetActive(group == 1); compactTrainingTabs.SetActive(group == 2);
            foreach (var b in compactDeviceTabs.GetComponentsInChildren<Button>()) StyleSelection(b, b.name == (page == 1 ? "设备目录" : "故障学习"));
            foreach (var b in compactTrainingTabs.GetComponentsInChildren<Button>())
                if (b.name == "故障处置" || b.name == "实训复盘") StyleSelection(b, b.name == (page == 2 ? "故障处置" : "实训复盘"));
            foreach (var root in pageRoots)
            {
                var r = (RectTransform)root.transform;
                r.offsetMin = new Vector2(204, 40);
                r.offsetMax = new Vector2(-24, page == 4 || page == 6 ? -102 : -156);
            }
        }

        void UpdateSceneFrame(Rect visible)
        {
            SetFrame(0, Vector2.zero, new Vector2(visible.xMin, 1));
            SetFrame(1, new Vector2(visible.xMax, 0), Vector2.one);
            SetFrame(2, new Vector2(visible.xMin, 0), new Vector2(visible.xMax, visible.yMin));
            SetFrame(3, new Vector2(visible.xMin, visible.yMax), new Vector2(visible.xMax, 1));
        }

        void SetFrame(int index, Vector2 min, Vector2 max)
        {
            var frame = sceneFrame[index];
            frame.anchorMin = min; frame.anchorMax = max;
            frame.offsetMin = frame.offsetMax = Vector2.zero;
        }

        void StyleSelection(Button button, bool selected)
        {
            button.image.color = selected ? amber : field;
            button.GetComponentInChildren<Text>().color = selected ? Background : ink;
        }

        void BuildCompactOverview()
        {
            var root = Page("Overview"); root.GetComponent<Image>().enabled = false;
            viewport = Rect(root, "Scene viewport", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-352, 0));
            var toolbar = Box(viewport, "Camera toolbar", Vector2.up, Vector2.one,
                new Vector2(0, -56), Vector2.zero, panel);
            var options = new List<string> { "车间 / 全部设备" };
            foreach (var d in simulation.devices) options.Add(d.deviceId + " " + d.displayName);
            modelMenu = Menu(toolbar, "Model device", 12, 9, 300, options.ToArray());
            modelMenu.onValueChanged.AddListener(i => {
                if (i == 0) simulation.cameraRig.Overview(); else simulation.Select(simulation.devices[i - 1]);
            });
            Command(toolbar, "总览", 326, 9, 84, () => { simulation.cameraRig.Overview(); modelMenu.SetValueWithoutNotify(0); }, false, 38);
            Command(toolbar, "视图", 422, 9, 84, () => OpenQuickMenu("车间视图",
                (simulation.cameraRig.Following ? "取消跟随巡检" : "跟随巡检", () => simulation.cameraRig.ToggleFollow()),
                (simulation.roof && simulation.roof.activeSelf ? "隐藏屋顶" : "显示屋顶", () => {
                    if (simulation.roof) simulation.roof.SetActive(!simulation.roof.activeSelf);
                })), false, 38);
            sceneRegion = Rect(viewport, "Live scene", Vector2.zero, Vector2.one, new Vector2(0, 48), new Vector2(0, -60));
            var tracking = Rect(viewport, "Spatial tracking", Vector2.zero, Vector2.one, new Vector2(0, 48), new Vector2(0, -60));
            tracking.gameObject.AddComponent<RectMask2D>();
            var overlay = tracking.gameObject.AddComponent<WorkshopSceneOverlay>();
            overlay.simulation = simulation; overlay.font = chineseFont; overlay.Build();
            var band = Box(viewport, "Robot status", Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0, 44), panel);
            taskText = Label(band, "", 13, ink, 14, 0, 580, 44);
            drivingMode = Command(band, "巡检控制", 0, 4, 114, () => OpenQuickMenu("巡检机器人",
                (simulation.inspector.ManualControl ? "恢复自动巡检" : "手动接管（方向键驾驶）", () => {
                    simulation.inspector.SetManualControl(!simulation.inspector.ManualControl); RefreshMetrics();
                }), ("跟随小车", () => { if (!simulation.cameraRig.Following) simulation.cameraRig.ToggleFollow(); })), false, 36);
            var dr = (RectTransform)drivingMode.transform;
            dr.anchorMin = dr.anchorMax = Vector2.right; dr.pivot = Vector2.right; dr.anchoredPosition = new Vector2(-10, 4);
            dr.sizeDelta = new Vector2(114, 36);
            orderReturn = Command(band, "返回工单", 0, 4, 114, () => OpenOrder(editingOrder), true, 36);
            var back = (RectTransform)orderReturn.transform;
            back.anchorMin = back.anchorMax = Vector2.right;
            back.pivot = Vector2.right;
            back.anchoredPosition = new Vector2(-134, 4);
            orderReturn.gameObject.SetActive(false);
            var side = Box(root, "Selected equipment", Vector2.right, Vector2.one, new Vector2(-336, 0), Vector2.zero, panel);
            Label(side, "当前设备", 13, muted, 20, 20, 296, 28);
            selectedName = Label(side, "", 23, ink, 20, 60, 296, 70);
            Box(side, "Equipment divider", 20, 148, 296, 1, field, false);
            selectedMetrics = Label(side, "", 16, ink, 20, 166, 296, 170, TextAnchor.UpperLeft);
            selectedAction = Command(side, "查看设备", 20, 354, 190, OpenSelectedEquipment, true, 44);
            Command(side, "设备操作", 222, 354, 94, OpenDeviceActions, false, 44);
            Label(side, "温度趋势 · 最近 30 秒", 13, muted, 20, 430, 296, 26);
            var plot = Rect(side, "Temperature trend", Vector2.zero, Vector2.one, new Vector2(20, 24), new Vector2(-20, -472));
            trend = plot.gameObject.AddComponent<WorkshopTrendGraphic>(); trend.color = amber; trend.Maximum = 120; trend.raycastTarget = false;
        }

        void OpenSelectedEquipment()
        {
            if (simulation.ActiveAlarm != null) OpenOrder(simulation.ActiveAlarm);
            else { RefreshAnalysis(); OpenEquipment(); }
        }

        void OpenDeviceActions()
        {
            var d = simulation.Selected;
            if (!d) return;
            var actions = new List<(string, Action)> {
                ("观察与拆装", () => { RefreshAnalysis(); OpenEquipment(); }),
                ("学习故障与处置", () => Navigate(5)),
                ("设备档案", ShowDeviceRecord)
            };
            if (d.Fault == null) actions.Add((d.IsRunning ? "停止设备" : "启动设备", simulation.ToggleSelected));
            else actions.Add(("查看当前工单", () => OpenOrder(simulation.ActiveAlarm)));
            if (ReviewedViewerAvailable()) actions.Add(("在独立展示器中打开", OpenReviewedViewer));
            OpenQuickMenu(d.deviceId + " · " + d.displayName, actions.ToArray());
        }

        bool ReviewedViewerAvailable()
        {
            return ReviewedViewerAvailable(simulation.Selected);
        }

        bool ReviewedViewerAvailable(WorkshopDevice device) =>
            TryReviewedViewer(device, out string path) && System.IO.File.Exists(path);

        void ShowDeviceRecord()
        {
            var d = simulation.Selected;
            OpenQuickMenu(d.deviceId + " · " + d.displayName + "\n" + d.KindText + " / A 车间\n" + d.StatusText + " · 本地模拟",
                ("查看结构", () => { RefreshAnalysis(); OpenEquipment(); }), ("学习故障", () => Navigate(5)));
        }

        void ShowControlsHelp() => OpenQuickMenu("操作帮助\n点选设备 · 拖动旋转 · 滚轮缩放\nH 隐藏界面 · M 静音 · Esc 返回\n手动接管后使用方向键驾驶");

        void CloseQuickMenu()
        {
            if (quickMenu) { quickMenu.SetActive(false); Destroy(quickMenu); quickMenu = null; }
        }

        void OpenQuickMenu(string caption, params (string label, Action action)[] items)
        {
            CloseQuickMenu();
            var root = Box(transform, "Quick menu", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0, 0, 0, .62f));
            quickMenu = root.gameObject;
            root.gameObject.AddComponent<Button>().onClick.AddListener(CloseQuickMenu);
            int lines = caption.Split('\n').Length;
            float head = 42 + lines * 26, height = head + items.Length * 52 + 66;
            var card = Box(root, "Menu card", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(-248, -height / 2), new Vector2(248, height / 2), panel);
            // Consume clicks in the card instead of letting them dismiss the backdrop.
            card.gameObject.AddComponent<Button>();
            Box(card, "Accent", 24, 22, 4, 28, amber, false);
            Label(card, caption, 17, ink, 40, 20, 430, head - 28);
            for (int i = 0; i < items.Length; i++)
            {
                var entry = items[i];
                Command(card, entry.label, 24, head + i * 52, 448, () => { CloseQuickMenu(); entry.action(); }, false, 42);
            }
            Command(card, "关闭", 24, head + items.Length * 52 + 6, 448, CloseQuickMenu, false, 40);
        }

        void CompactBack()
        {
            if (quickMenu) { CloseQuickMenu(); return; }
            if (analysisToolDrawer && analysisToolDrawer.activeSelf) { analysisToolDrawer.SetActive(false); return; }
            if (analysisDialog && analysisDialog.activeSelf) { analysisDialog.SetActive(false); return; }
            if (equipmentDialog && equipmentDialog.activeSelf) { equipmentDialog.SetActive(false); return; }
            if (faultDialog && faultDialog.activeSelf) faultDialog.SetActive(false);
        }

        void FaultPrimaryAction()
        {
            if (simulation.ActiveAlarm == null)
            {
                var d = simulation.Selected;
                if (!d) return;
                var mode = WorkshopFaultCatalog.For(d.kind)[analysisMode.value];
                if (!simulation.TriggerFault(d, mode.Code)) return;
            }
            OpenOrder(simulation.ActiveAlarm);
        }

        void CompactAnalysisLayout(RectTransform overlay)
        {
            analysisToolPages.Clear();
            var oldControls = overlay.Find("Model and field");
            normalModel.gameObject.SetActive(false); faultModel.gameObject.SetActive(false);
            stressField.gameObject.SetActive(false); displacementField.gameObject.SetActive(false); temperatureField.gameObject.SetActive(false);
            Place(analysisQuantity.transform, overlay, 20, 80, 300, 40);
            analysisComparison = Menu(overlay, "Analysis comparison", 334, 80, 180, "完好 / 基准", "故障 / 缺陷");
            analysisComparison.SetValueWithoutNotify(analysisModel);
            analysisComparison.onValueChanged.AddListener(v => { analysisModel = v; UpdateAnalysis(); });
            Place(overlay.Find("复位"), overlay, 528, 80, 84, 38);
            Command(overlay, "工具", 624, 80, 84, () => analysisToolDrawer.SetActive(!analysisToolDrawer.activeSelf), false, 38);
            if (fractureMode) { Place(fractureMode.transform, overlay, 724, 80, 126, 38); fractureModeHint.gameObject.SetActive(false); }
            var fieldView = (RectTransform)overlay.Find("Field viewport");
            fieldView.offsetMin = new Vector2(20, 130); fieldView.offsetMax = new Vector2(-20, -210);
            var summary = (RectTransform)overlay.Find("Results"); summary.offsetMin = new Vector2(20, -200); summary.offsetMax = new Vector2(-20, -132);
            analysisFooter.offsetMin = new Vector2(20, 18);
            if (fractureFooter) ((RectTransform)fractureFooter.transform).offsetMin = new Vector2(20, 18);
            analysisContextPanel = fieldView.Find("Equipment context");
            if (analysisContextPanel) analysisContextPanel.gameObject.SetActive(false);
            var detail = fieldView.Find("Defect closeup");
            if (detail) detail.gameObject.SetActive(false);
            var gauge = fieldView.Find("Mesh thickness comparison");
            if (gauge) gauge.gameObject.SetActive(false);

            var drawer = Panel(overlay, "Analysis tool drawer", Vector2.right, Vector2.one,
                new Vector2(-388, 130), new Vector2(-20, -128), panel);
            analysisToolDrawer = drawer.gameObject;
            Label(drawer, "分析工具", 20, ink, 20, 14, 220, 32);
            Command(drawer, "收起", 270, 14, 78, () => drawer.gameObject.SetActive(false), false, 34);
            var categories = Menu(drawer, "Tool category", 20, 62, 328, "显示", "剖切与变形", "观察与测量", "载荷与导出", "模型说明");
            for (int i = 0; i < 5; i++)
            {
                Scroll(drawer, "Tools " + i, Vector2.zero, Vector2.one, new Vector2(16, 16), new Vector2(-16, -114), out var content);
                content.sizeDelta = new Vector2(0, 440);
                analysisToolPages.Add(content.parent.gameObject);
            }
            Transform Content(int i) => analysisToolPages[i].GetComponent<ScrollRect>().content;
            var display = Content(0);
            int row = 0;
            foreach (var toggle in new[] { analysisWire, analysisSmooth, analysisContours, analysisAnnotations })
                Place(toggle.transform, display, 4, row++ * 46, 322, 38);
            var section = Content(1);
            Place(analysisSection.transform, section, 4, 0, 322, 38);
            analysisCutGroup = Rect(section, "Section settings", Vector2.up, Vector2.up, new Vector2(0, -142), new Vector2(332, -46)).gameObject;
            Label(analysisCutGroup.transform, "剖切位置", 14, muted, 12, 0, 290, 26);
            Place(analysisCut.transform, analysisCutGroup.transform, 12, 34, 290, 26);
            Place(analysisDeform.transform, section, 4, 156, 322, 38);
            oldControls.gameObject.SetActive(false);
            analysisScaleGroup = Rect(section, "Deformation settings", Vector2.up, Vector2.up, new Vector2(0, -298), new Vector2(332, -202)).gameObject;
            Place(analysisScale.transform, analysisScaleGroup.transform, 12, 0, 300, 28);
            Place(analysisScaleSlider.transform, analysisScaleGroup.transform, 12, 34, 290, 26);
            analysisSection.onValueChanged.AddListener(_ => RefreshCompactAnalysis());
            analysisDeform.onValueChanged.AddListener(_ => RefreshCompactAnalysis());
            var observe = Content(2);
            string[] views = { "正视", "俯视", "侧视", "极值定位" };
            for (int i = 0; i < views.Length; i++) Place(overlay.Find(views[i]), observe, 4 + i % 2 * 166, i / 2 * 48, 156, 38);
            analysisPeakButton = observe.Find("极值定位").GetComponent<Button>();
            Place(analysisOrbit.transform, observe, 4, 110, 322, 38);
            Place(analysisMeasure.transform, observe, 4, 156, 322, 38);
            AnalysisToggle(observe, "整机小窗", 4, 210, false, v => analysisContextPanel.gameObject.SetActive(v));
            if (detail) AnalysisToggle(observe, "局部近景", 4, 256, false, v => detail.gameObject.SetActive(v));
            if (gauge) AnalysisToggle(observe, "尺寸对比", 4, 302, false, v => gauge.gameObject.SetActive(v));
            var export = Content(3);
            Place(analysisLoad.transform, export, 12, 0, 304, 28);
            Place(analysisLoadSlider.transform, export, 12, 36, 290, 26);
            Place(analysisPlayback.transform, export, 4, 82, 322, 38);
            Place(overlay.Find("导出测点"), export, 4, 144, 322, 40);
            analysisExportButton = export.Find("导出测点").GetComponent<Button>();
            Command(export, "保存画面", 4, 196, 322, SaveAnalysisSnapshot, false, 40);
            var left = overlay.Find("Analysis data");
            analysisParameterPanel = (RectTransform)left.Find("Analysis parameters");
            analysisParameterPanel.gameObject.SetActive(false);
            var specs = left.Find("Model specifications");
            specs.SetParent(Content(4), false);
            Fill((RectTransform)specs);
            // Reuse the existing scroll view at the drawer's full height.
            ((RectTransform)specs).SetParent(analysisToolPages[4].transform.parent, false);
            var sr = (RectTransform)specs; sr.anchorMin = Vector2.zero; sr.anchorMax = Vector2.one;
            sr.offsetMin = new Vector2(20, 16); sr.offsetMax = new Vector2(-20, -114);
            analysisToolPages[4].SetActive(false); analysisToolPages[4] = specs.gameObject;
            left.gameObject.SetActive(false);
            categories.onValueChanged.AddListener(i => {
                for (int k = 0; k < analysisToolPages.Count; k++) analysisToolPages[k].SetActive(i == k);
            });
            for (int i = 0; i < analysisToolPages.Count; i++) analysisToolPages[i].SetActive(i == 0);
            drawer.SetAsLastSibling(); drawer.gameObject.SetActive(false);
            RefreshCompactAnalysis();
        }

        void RefreshCompactAnalysis()
        {
            if (analysisScaleGroup) analysisScaleGroup.SetActive(analysisDeform.isOn && analysisResult.analysis == "structural");
            if (analysisCutGroup) analysisCutGroup.SetActive(analysisSection.isOn);
            if (analysisComparison) analysisComparison.SetValueWithoutNotify(analysisModel);
        }

        void CompactEquipmentLayout(RectTransform overlay)
        {
            equipmentModeButtons.Clear();
            var modes = new[] { "观察", "拆装", "故障演示" };
            for (int i = 0; i < modes.Length; i++)
            {
                int mode = i;
                equipmentModeButtons.Add(Command(overlay, modes[i], 20 + i * 112, 80, 100,
                    () => SetEquipmentMode(mode), false, 38));
            }
            var hidden = Rect(overlay, "View options", Vector2.up, Vector2.one, Vector2.zero, Vector2.zero);
            string[] viewNames = { "整机视角", "故障近景", "正视", "俯视", "侧视", "部件标注", "自动环绕", "柜门开启", "移开护网" };
            foreach (string name in viewNames)
            {
                var item = overlay.Find(name);
                if (item) item.SetParent(hidden, false);
            }
            hidden.gameObject.SetActive(false);
            Command(overlay, "复位", 380, 80, 84, () => equipmentView.ResetView(), false, 38);
            Command(overlay, "视图", 476, 80, 84, () => OpenEquipmentTools(hidden), false, 38);
            Command(overlay, "故障学习", 572, 80, 118, () => {
                equipmentDialog.SetActive(false);
                Navigate(5);
            }, false, 38);
            var device = inspectedDevice;
            if (ReviewedViewerAvailable(device))
                Command(overlay, "详细展示", 702, 80, 118, () => OpenReviewedViewer(device), false, 38);
            equipmentPartsPanel = overlay.Find("Equipment specifications").gameObject;
            equipmentPartsPanel.SetActive(false);
            var pr = (RectTransform)equipmentPartsPanel.transform;
            pr.offsetMin = new Vector2(20, 150); pr.offsetMax = new Vector2(324, -186);
            var assembly = Rect(overlay, "Assembly controls", Vector2.up, Vector2.one,
                new Vector2(20, -174), new Vector2(-20, -130));
            equipmentAssemblyTools = assembly.gameObject;
            string[] names = { "逐步拆解", "逐步装回", "全部拆开", "重新组装" };
            for (int i = 0; i < names.Length; i++) Place(overlay.Find(names[i]), assembly, i * 142, 0, 130, 40);
            equipmentFaultTools = overlay.Find("Equipment transport").gameObject;
            string[] secondary = { "从头重播", "完好状态", "预设起裂", "最终状态", "保存画面", "导出尺寸" };
            foreach (string name in secondary)
                equipmentFaultTools.transform.Find(name).SetParent(hidden, false);
            Command(equipmentFaultTools.transform, "时间点", 126, 10, 100, () => OpenQuickMenu("故障演示时间点",
                ("从头重播", () => { equipmentView.SetProgress(0); equipmentView.Playing = true; }),
                ("完好状态", () => EquipmentScrub(0)),
                ("预设起裂", () => EquipmentScrub(WorkshopEquipmentModel.CrackStart)),
                ("最终状态", () => EquipmentScrub(1))), false, 30);
            equipmentCloseup = equipmentViewport.Find("Same mesh closeup").gameObject;
            equipmentTransport.rectTransform.offsetMin = new Vector2(20, 116);
            SetEquipmentMode(0);
        }

        void OpenEquipmentTools(Transform hidden)
        {
            var entries = new List<(string, Action)> {
                (equipmentPartsPanel.activeSelf ? "收起部件与参数" : "部件与参数", () => {
                    equipmentPartsPanel.SetActive(!equipmentPartsPanel.activeSelf); LayoutEquipmentViewport();
                }),
                ("正视", () => equipmentView.Preset(0)), ("俯视", () => equipmentView.Preset(1)),
                ("侧视", () => equipmentView.Preset(2)),
                (equipmentView.AutoOrbit ? "停止环绕" : "自动环绕", () => equipmentView.AutoOrbit = !equipmentView.AutoOrbit),
                (equipmentView.ShowLabels ? "隐藏部件标注" : "显示部件标注", () => equipmentView.ShowLabels = !equipmentView.ShowLabels)
            };
            if (equipmentModeIndex == 2) entries.Add(("故障近景", () => equipmentView.FocusDamage()));
            var door = hidden.Find("柜门开启");
            if (door) entries.Add(("切换柜门开合", () => { var t = door.GetComponent<Toggle>(); t.isOn = !t.isOn; }));
            if (equipmentView.Model.ReviewedCabinetModel)
                entries.Add(("查看柜内", () => equipmentView.SetDisassemblyTarget(
                    equipmentView.Model.CabinetDoorOpenStep)));
            var guard = hidden.Find("移开护网");
            if (guard) entries.Add(("切换护网显示", () => { var t = guard.GetComponent<Toggle>(); t.isOn = !t.isOn; }));
            entries.Add(("保存画面 / 导出", () => OpenQuickMenu("导出",
                ("保存画面", SaveEquipmentSnapshot), ("导出尺寸", ExportEquipmentDimensions))));
            OpenQuickMenu("设备视图", entries.ToArray());
        }

        void SetEquipmentMode(int mode)
        {
            equipmentModeIndex = mode;
            for (int i = 0; i < equipmentModeButtons.Count; i++) StyleSelection(equipmentModeButtons[i], i == mode);
            equipmentAssemblyTools.SetActive(mode == 1);
            equipmentFaultTools.SetActive(mode == 2);
            equipmentCloseup.SetActive(mode == 2);
            equipmentTransport.gameObject.SetActive(mode == 2);
            equipmentView.ShowTankFaultModel(inspectedDevice, mode == 2);
            if (equipmentView.Model.ReviewedTankModel)
            {
                equipmentSource.text = mode == 2 ? "储罐故障教学模型 · 预设损坏过程" : "验收版储罐 · 12 个拆解总成";
                equipmentAssemblyStep.gameObject.SetActive(mode != 2);
                var parts = equipmentAssemblyStep.transform.parent;
                for (int i = 0; i < equipmentView.Model.AssemblyCount; i++)
                    parts.Find((i + 1).ToString("D2")).gameObject.SetActive(mode != 2);
            }
            if (mode != 2) { equipmentView.Playing = false; equipmentView.SetProgress(0); }
            LayoutEquipmentViewport();
        }

        void LayoutEquipmentViewport()
        {
            equipmentViewport.offsetMin = new Vector2(equipmentPartsPanel.activeSelf ? 348 : 20, equipmentModeIndex == 2 ? 158 : 24);
            equipmentViewport.offsetMax = new Vector2(-20, equipmentModeIndex == 1 ? -186 : -132);
        }
    }
}
