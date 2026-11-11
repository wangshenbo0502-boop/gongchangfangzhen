using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace WorkshopDemo
{
    [DefaultExecutionOrder(-20)]
    public sealed partial class WorkshopUI : MonoBehaviour
    {
        public WorkshopSimulation simulation;
        public Font chineseFont;
        readonly Color ink = new Color32(224, 231, 238, 255);
        readonly Color muted = new Color32(146, 160, 178, 255);
        readonly Color panel = new Color32(28, 34, 43, 255);
        readonly Color field = new Color32(40, 48, 59, 255);
        readonly Color accent = new Color32(61, 199, 181, 255);
        readonly Color active = new Color32(24, 107, 98, 255);
        readonly Color amber = new Color32(246, 190, 83, 255);
        readonly Color red = new Color32(255, 109, 133, 255);
        readonly string[] pages = { "模型展示", "设备台账", "维修工单", "实训复盘", "AI问答", "故障分析", "知识导入" };
        readonly int[] navigationOrder = { 4, 0, 5, 1, 2, 3, 6 };
        readonly List<Button> navigation = new List<Button>();
        readonly List<GameObject> pageRoots = new List<GameObject>();
        readonly List<Text> deviceStates = new List<Text>();
        readonly List<Text> deviceMetrics = new List<Text>();
        readonly List<Button> deviceButtons = new List<Button>();
        readonly List<GameObject> formGroups = new List<GameObject>();
        Text title, clock, feedback, total, running, alarms, accepted, selectedName, selectedMetrics, taskText;
        Text orderSummary, orderDetail, orderError, reviewText, knowledgeText, assistantStatus, assistantText;
        Text dialogDescription, dialogError, primaryText, operationText;
        Text analysisText, analysisLive, importStatus, importList, questionContext;
        RectTransform analysisContent, importsContent;
        RectTransform viewport, orderRows, orderDetailContent, knowledgeContent, assistantContent, reviewContent, dialogContent;
        WorkshopTrendGraphic trend;
        WorkshopDevice trendDevice;
        WorkshopAlarm editingOrder, loadedOrder;
        WorkshopOrderStage loadedStage = (WorkshopOrderStage)(-1);
        Button primary, reject, operation, inject, send, drivingMode;
        InputField search, person, department, description, worker, deadline, cause, action, materials, acceptance, question;
        InputField importTitle, importBody, importPath;
        Dropdown orderFilter, deviceMenu, modeMenu, lessonMenu;
        Dropdown modelMenu, analysisDevice, analysisMode;
        Button importSubmit;
        GameObject faultDialog, form;
        WorkshopAssistantClient assistant;
        ScrollRect assistantScroll;
        int page, lastWidth, lastHeight;
        float lastSample = -1;
        bool built, updating;
        string rowSignature = "";
        WorkshopDevice FaultDevice => simulation.devices[deviceMenu.value];

        void Start()
        {
            if (!simulation || !chineseFont || simulation.devices == null || !simulation.cameraRig)
            { Debug.LogError("Workshop UI requires simulation, camera and Chinese font.", this); enabled = false; return; }
            var canvas = GetComponent<Canvas>();
            if (!canvas) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = GetComponent<CanvasScaler>();
            if (!scaler) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 900); scaler.matchWidthOrHeight = .5f;
            if (!GetComponent<GraphicRaycaster>()) gameObject.AddComponent<GraphicRaycaster>();
            if (!EventSystem.current)
                new GameObject("Workshop Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            assistant = GetComponent<WorkshopAssistantClient>();
            if (!assistant) assistant = gameObject.AddComponent<WorkshopAssistantClient>();
            assistant.simulation = simulation;
            var switcher = FindFirstObjectByType<WorkshopSceneSwitcher>();
            if (switcher) switcher.InterfaceFont = chineseFont;
            Build();
            WorkshopTrainingLayout.ConfigureCamera(simulation.cameraRig);
            simulation.cameraRig.Overview();
            built = true; Subscribe(); Navigate(0); RefreshAssistant();
        }
        void Subscribe()
        {
            simulation.Changed += Refresh; simulation.Sampled += RefreshMetrics;
            assistant.Updated += RefreshAssistant;
        }
        void OnEnable() { if (built) { Subscribe(); Refresh(); } }
        void OnDisable()
        {
            if (simulation) { simulation.Changed -= Refresh; simulation.Sampled -= RefreshMetrics; }
            if (assistant) assistant.Updated -= RefreshAssistant;
        }
        RectTransform Rect(Transform parent, string name, Vector2 min, Vector2 max, Vector2 from, Vector2 to)
        {
            var r = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
            r.SetParent(parent, false); r.anchorMin = min; r.anchorMax = max;
            r.offsetMin = from; r.offsetMax = to; return r;
        }
        RectTransform Box(Transform parent, string name, Vector2 min, Vector2 max, Vector2 from, Vector2 to,
            Color tint, bool blocks = true)
        {
            var r = Rect(parent, name, min, max, from, to);
            var image = r.gameObject.AddComponent<Image>(); image.color = tint; image.raycastTarget = blocks;
            return r;
        }
        Text Label(Transform parent, string value, int size, Color tint, float x, float y, float w, float h,
            TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var r = Rect(parent, value.Length > 24 ? "Text" : value, Vector2.up, Vector2.up,
                new Vector2(x, -y - h), new Vector2(x + w, -y));
            var t = r.gameObject.AddComponent<Text>();
            t.font = chineseFont; t.text = value; t.fontSize = size; t.color = tint; t.alignment = alignment;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            t.supportRichText = false; t.raycastTarget = false; return t;
        }
        static void Fill(RectTransform r, float left = 0, float bottom = 0, float right = 0, float top = 0)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(left, bottom); r.offsetMax = new Vector2(-right, -top);
        }
        Button Command(Transform parent, string text, float x, float y, float width, Action callback,
            bool emphasized = false, float height = 36)
        {
            var r = Box(parent, text, Vector2.up, Vector2.up,
                new Vector2(x, -y - height), new Vector2(x + width, -y), emphasized ? active : field);
            var b = r.gameObject.AddComponent<Button>(); b.targetGraphic = r.GetComponent<Image>();
            var colors = b.colors;
            colors.highlightedColor = new Color(.8f, .94f, 1); colors.pressedColor = new Color(.65f, .8f, .85f);
            colors.disabledColor = new Color(.45f, .45f, .45f); b.colors = colors;
            b.onClick.AddListener(() => callback());
            var label = Label(r, text, 14, ink, 0, 0, width, height, TextAnchor.MiddleCenter);
            Fill(label.rectTransform, 8, 2, 8, 2); return b;
        }
        ScrollRect Scroll(Transform parent, string name, Vector2 min, Vector2 max, Vector2 from, Vector2 to,
            out RectTransform content)
        {
            var r = Box(parent, name, min, max, from, to, panel);
            r.gameObject.AddComponent<RectMask2D>();
            var scroll = r.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = r; scroll.horizontal = false; scroll.scrollSensitivity = 35;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            content = Rect(r, "Content", Vector2.up, Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(.5f, 1); scroll.content = content; return scroll;
        }
        InputField Input(Transform parent, string name, string hint, float x, float y, float width, float height = 40)
        {
            var r = Box(parent, name, Vector2.up, Vector2.up,
                new Vector2(x, -y - height), new Vector2(x + width, -y), field);
            var input = r.gameObject.AddComponent<InputField>(); input.characterLimit = 600;
            input.lineType = height > 50 ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            input.textComponent = Label(r, "", 14, ink, 10, 0, width - 20, height);
            Fill(input.textComponent.rectTransform, 10, 6, 10, 6);
            var placeholder = Label(r, hint, 13, muted, 10, 0, width - 20, height);
            Fill(placeholder.rectTransform, 10, 6, 10, 6); input.placeholder = placeholder; return input;
        }
        Dropdown Menu(Transform parent, string name, float x, float y, float width, params string[] options)
        {
            var r = Box(parent, name, Vector2.up, Vector2.up,
                new Vector2(x, -y - 38), new Vector2(x + width, -y), field);
            var menu = r.gameObject.AddComponent<Dropdown>(); menu.targetGraphic = r.GetComponent<Image>();
            menu.captionText = Label(r, "", 14, ink, 12, 0, width - 38, 38);
            Label(r, "▾", 14, muted, width - 28, 0, 20, 38, TextAnchor.MiddleCenter);
            var template = Box(r, "Template", Vector2.zero, Vector2.right, new Vector2(0, -230), Vector2.zero, field);
            template.pivot = new Vector2(.5f, 1);
            var view = Rect(template, "Viewport", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            view.gameObject.AddComponent<RectMask2D>();
            var content = Rect(view, "Content", Vector2.up, Vector2.one, new Vector2(0, -38), Vector2.zero);
            content.pivot = Vector2.up;
            var item = Box(content, "Item", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, field);
            var toggle = item.gameObject.AddComponent<Toggle>(); toggle.targetGraphic = item.GetComponent<Image>();
            toggle.graphic = Box(item, "Check", Vector2.zero, Vector2.zero,
                new Vector2(4, 14), new Vector2(8, 24), accent, false).GetComponent<Image>();
            menu.itemText = Label(item, "", 14, ink, 14, 0, width - 24, 38);
            var scroll = template.gameObject.AddComponent<ScrollRect>();
            scroll.content = content; scroll.viewport = view; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            menu.template = template; template.gameObject.SetActive(false);
            menu.AddOptions(new List<string>(options)); return menu;
        }
        Text FlowText(RectTransform content, string name, int size = 15)
        {
            var t = Label(content, "", size, ink, 0, 0, 600, 100, TextAnchor.UpperLeft);
            t.name = name; t.rectTransform.pivot = Vector2.up; return t;
        }
        void FitText(Text text, RectTransform content, string value)
        {
            text.text = value;
            float width = Mathf.Max(80, content.rect.width - 32);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = Vector2.up;
            text.rectTransform.pivot = Vector2.up; text.rectTransform.anchoredPosition = new Vector2(16, -12);
            text.rectTransform.sizeDelta = new Vector2(width, 100);
            float height = Mathf.Max(100, text.preferredHeight + 8);
            text.rectTransform.sizeDelta = new Vector2(width, height); content.sizeDelta = new Vector2(0, height + 28);
        }
        RectTransform Page(string name)
        {
            var r = Box(transform, name, Vector2.zero, Vector2.one,
                new Vector2(200, 40), new Vector2(-20, -166), panel);
            pageRoots.Add(r.gameObject); return r;
        }
        Text Kpi(Transform parent, int index, string caption, Color tint)
        {
            var r = Rect(parent, caption, new Vector2(index / 4f, 0), new Vector2((index + 1) / 4f, 1),
                new Vector2(16, 8), new Vector2(-16, -8));
            Label(r, caption, 13, muted, 0, 0, 160, 24);
            return Label(r, "", 28, tint, 0, 25, 200, 42);
        }
        void Build()
        {
            // The live camera is revealed by the overview's transparent viewport only.
            Box(transform, "Background", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color32(19, 24, 31, 255), false);
            var frameColor = new Color32(19, 24, 31, 255);
            Box(transform, "Frame left", Vector2.zero, Vector2.up, Vector2.zero, new Vector2(200, 0), frameColor, false);
            Box(transform, "Frame right", Vector2.right, Vector2.one, new Vector2(-376, 0), Vector2.zero, frameColor, false);
            Box(transform, "Frame top", Vector2.up, Vector2.one, new Vector2(0, -214), Vector2.zero, frameColor, false);
            Box(transform, "Frame bottom", Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0, 84), frameColor, false);
            var rail = Box(transform, "Navigation", Vector2.zero, Vector2.up, Vector2.zero, new Vector2(180, 0), panel);
            Label(rail, "WORKSHOP", 20, accent, 18, 22, 150, 30);
            Label(rail, "数字孪生实训平台", 14, ink, 18, 57, 150, 26);
            Label(rail, "核心实训", 12, muted, 22, 111, 140, 24);
            for (int i = 0; i < pages.Length; i++) navigation.Add(null);
            for (int i = 0; i < navigationOrder.Length; i++)
            {
                int captured = navigationOrder[i];
                float y = i < 3 ? 144 + i * 52 : 350 + (i - 3) * 52;
                navigation[captured] = Command(rail, pages[captured], 12, y, 156, () => Navigate(captured), false, 42);
            }
            Label(rail, "业务与资料", 12, muted, 22, 316, 140, 24);
            var assembly = Command(rail, "小车装配训练", 12, 0, 156, () => {
                var switcher = FindFirstObjectByType<WorkshopSceneSwitcher>();
                if (switcher) switcher.SwitchTo(WorkshopSceneSwitcher.AssemblyScene);
                else simulation.Notify("装配场景尚未加入当前运行配置");
            }, false, 38);
            assembly.GetComponent<RectTransform>().SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, 102, 38);
            var note = Label(rail, "4 台设备 + 1 台小车\n本地模拟 / 本次会话", 12, muted, 18, 0, 150, 58);
            note.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, 22, 58);
            var header = Box(transform, "Header", Vector2.up, Vector2.one, new Vector2(180, -72), Vector2.zero, panel);
            title = Label(header, "数字孪生 / 模型展示", 22, ink, 22, 14, 480, 42);
            var right = Rect(header, "Session controls", Vector2.one, Vector2.one,
                new Vector2(-460, -58), new Vector2(-20, -16));
            clock = Label(right, "", 13, muted, 0, 0, 232, 38);
            Command(right, "新建故障课题", 238, 0, 124, OpenFaultDialog, true, 38);
            Command(right, "重置", 370, 0, 70, () => {
                simulation.ResetDemo(); editingOrder = null; rowSignature = ""; Refresh();
            }, false, 38);
            var metrics = Rect(transform, "Metrics", Vector2.up, Vector2.one,
                new Vector2(200, -156), new Vector2(-20, -82));
            total = Kpi(metrics, 0, "设备总数", ink); running = Kpi(metrics, 1, "当前运行", accent);
            alarms = Kpi(metrics, 2, "未关闭告警", amber); accepted = Kpi(metrics, 3, "验收归档", accent);
            feedback = Label(transform, "", 12, muted, 200, 0, 1000, 30);
            feedback.rectTransform.anchorMin = Vector2.zero; feedback.rectTransform.anchorMax = Vector2.right;
            feedback.rectTransform.offsetMin = new Vector2(200, 4); feedback.rectTransform.offsetMax = new Vector2(-20, 34);
            BuildOverview(); BuildLedger(); BuildOrders(); BuildReview(); BuildKnowledge();
            BuildAnalysis(); BuildImport(); BuildFaultDialog();
        }
        void BuildOverview()
        {
            var root = Page("Overview"); root.GetComponent<Image>().enabled = false;
            viewport = Rect(root, "Scene viewport", Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-356, 0));
            var toolbar = Box(viewport, "Camera toolbar", Vector2.up, Vector2.one, new Vector2(0, -46), Vector2.zero, panel);
            Command(toolbar, "总览", 8, 6, 72, () => simulation.cameraRig.Overview(), false, 34);
            Command(toolbar, "跟随巡检", 88, 6, 96, () => simulation.cameraRig.ToggleFollow(), false, 34);
            Command(toolbar, "屋顶", 192, 6, 72, () => {
                if (simulation.roof) simulation.roof.SetActive(!simulation.roof.activeSelf);
            }, false, 34);
            var modelOptions = new List<string> { "A 车间 / 全部设备" };
            foreach (var d in simulation.devices) modelOptions.Add(d.deviceId + " " + d.displayName);
            modelMenu = Menu(toolbar, "Model device", 280, 4, 320, modelOptions.ToArray());
            modelMenu.onValueChanged.AddListener(index => {
                if (index == 0) simulation.cameraRig.Overview();
                else simulation.Select(simulation.devices[index - 1]);
            });
            sceneRegion = Rect(viewport, "Live scene", Vector2.zero, Vector2.one,
                new Vector2(0, 44), new Vector2(0, -48));
            var tracking = Rect(viewport, "Spatial tracking", Vector2.zero, Vector2.one,
                new Vector2(0, 44), new Vector2(0, -48));
            tracking.gameObject.AddComponent<RectMask2D>();
            var overlay = tracking.gameObject.AddComponent<WorkshopSceneOverlay>();
            overlay.simulation = simulation; overlay.font = chineseFont; overlay.Build();
            var band = Box(viewport, "Robot status", Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0, 42), panel);
            taskText = Label(band, "", 13, ink, 14, 0, 610, 42);
            var side = Box(root, "Selected equipment", Vector2.right, Vector2.one,
                new Vector2(-344, 0), Vector2.zero, panel);
            Label(side, "当前设备", 16, ink, 18, 14, 300, 30);
            selectedName = Label(side, "", 18, ink, 18, 56, 308, 64);
            selectedMetrics = Label(side, "", 14, muted, 18, 126, 308, 146, TextAnchor.UpperLeft);
            operation = Command(side, "停止设备", 18, 286, 148, simulation.ToggleSelected);
            operationText = operation.GetComponentInChildren<Text>();
            Command(side, "设备档案", 178, 286, 148, () => Navigate(1));
            Command(side, "故障分析", 18, 334, 148, () => Navigate(5), true, 40).name = "Analyze selected";
            Command(side, "处理当前工单", 178, 334, 148, () => { editingOrder = simulation.ActiveAlarm; Navigate(2); }, false, 40);
            Command(toolbar, "整机拆装", 612, 6, 96, () => {
                RefreshAnalysis(); OpenEquipment();
            }, false, 34);
            drivingMode = Command(band, "手动接管", 620, 4, 92, () =>
                simulation.inspector.SetManualControl(!simulation.inspector.ManualControl), false, 34);
            Label(side, "温度趋势 / 最近 30 秒", 13, muted, 18, 400, 308, 26);
            var plot = Rect(side, "Temperature trend", Vector2.zero, Vector2.one,
                new Vector2(18, 20), new Vector2(-18, -442));
            trend = plot.gameObject.AddComponent<WorkshopTrendGraphic>();
            trend.color = accent; trend.Maximum = 120; trend.raycastTarget = false;
        }
        void BuildLedger()
        {
            var root = Page("Ledger");
            Label(root, "设备档案", 18, ink, 20, 14, 260, 32);
            search = Input(root, "Device search", "搜索编号、名称或类型", 300, 12, 340, 38);
            search.onValueChanged.AddListener(_ => FilterDevices());
            Label(root, "设备编号 / 名称", 12, muted, 26, 66, 320, 26);
            Label(root, "类别 / 位置", 12, muted, 370, 66, 220, 26);
            Label(root, "运行状态", 12, muted, 650, 66, 140, 26);
            Scroll(root, "Device rows", Vector2.zero, Vector2.one,
                new Vector2(16, 66), new Vector2(-16, -104), out var rows);
            for (int i = 0; i < simulation.devices.Length; i++)
            {
                var d = simulation.devices[i];
                var b = Command(rows, "", 0, i * 62, 1000, () => { simulation.Select(d); Navigate(0); }, false, 56);
                b.name = "Device " + d.deviceId;
                var r = (RectTransform)b.transform; r.anchorMax = Vector2.one;
                r.offsetMin = new Vector2(0, -i * 62 - 56); r.offsetMax = new Vector2(0, -i * 62);
                Label(b.transform, d.deviceId + "   " + d.displayName, 15, ink, 12, 0, 330, 56);
                Label(b.transform, d.KindText + " / A 车间", 14, muted, 354, 0, 240, 56);
                deviceStates.Add(Label(b.transform, "", 14, accent, 634, 0, 170, 56));
                var value = Label(b.transform, "", 13, muted, 0, 0, 160, 56);
                value.rectTransform.anchorMin = value.rectTransform.anchorMax = Vector2.one;
                value.rectTransform.offsetMin = new Vector2(-120, -56); value.rectTransform.offsetMax = new Vector2(-16, 0);
                deviceMetrics.Add(value); deviceButtons.Add(b);
            }
            rows.sizeDelta = new Vector2(0, simulation.devices.Length * 62);
            var note = Label(root, "档案来源：场景配置    |    点击设备进入三维现场    |    维修历史见实训复盘", 12, muted, 24, 0, 1100, 34);
            note.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, 16, 34);
        }
        GameObject Group(Transform parent, string name)
        {
            var r = Rect(parent, name, Vector2.up, Vector2.one, new Vector2(0, -300), Vector2.zero);
            r.pivot = new Vector2(.5f, 1); formGroups.Add(r.gameObject); return r.gameObject;
        }
        InputField Field(Transform parent, string caption, string hint, float y, float height = 40)
        {
            Label(parent, caption, 13, muted, 16, y, 380, 24);
            var input = Input(parent, caption, hint, 16, y + 28, 410, height);
            var r = (RectTransform)input.transform; r.anchorMax = Vector2.one; r.offsetMax = new Vector2(-16, -y - 28);
            return input;
        }
        void BuildOrders()
        {
            var root = Page("Orders");
            Label(root, "巡检与维修工单", 18, ink, 20, 12, 280, 34);
            orderFilter = Menu(root, "Order filter", 312, 12, 182,
                "全部工单", "待报修", "待派工", "巡检途中", "待维修", "待验收", "已关闭");
            orderFilter.onValueChanged.AddListener(_ => { rowSignature = ""; RebuildOrders(); });
            orderSummary = Label(root, "", 13, amber, 516, 12, 630, 38);
            Scroll(root, "Order list", Vector2.zero, new Vector2(.48f, 1),
                new Vector2(16, 16), new Vector2(-12, -66), out orderRows);
            var detail = Box(root, "Work order detail", new Vector2(.48f, 0), Vector2.one,
                new Vector2(0, 16), new Vector2(-16, -66), field);
            Scroll(detail, "Order detail scroll", Vector2.up, Vector2.one,
                new Vector2(0, -178), Vector2.zero, out orderDetailContent);
            orderDetail = FlowText(orderDetailContent, "Order details", 14);
            form = Box(detail, "Workflow form", Vector2.zero, Vector2.one,
                new Vector2(0, 98), new Vector2(0, -186), panel).gameObject;
            var report = Group(form.transform, "Report fields");
            person = Field(report.transform, "报修人 *", "填写姓名", 0);
            department = Field(report.transform, "所属部门 *", "填写部门", 78);
            description = Field(report.transform, "故障现象 *", "记录现场可见现象", 156, 68);
            var assign = Group(form.transform, "Assignment fields");
            worker = Field(assign.transform, "维修责任人 *", "填写维修人员", 0);
            deadline = Field(assign.transform, "处理时限 / 分钟 *", "1–240", 78);
            deadline.contentType = InputField.ContentType.IntegerNumber;
            var repair = Group(form.transform, "Repair fields");
            cause = Field(repair.transform, "故障原因 *", "填写诊断结论", 0);
            action = Field(repair.transform, "维修措施 *", "填写实际处理步骤", 78, 64);
            materials = Field(repair.transform, "配件材料 *", "无更换填“无”", 180);
            var accept = Group(form.transform, "Acceptance fields");
            acceptance = Field(accept.transform, "使用方验收意见 *", "确认恢复情况，或填写驳回原因", 0, 80);
            var formRect = (RectTransform)form.transform; formRect.gameObject.AddComponent<RectMask2D>();
            var scroll = formRect.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = formRect; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 35;
            foreach (var g in formGroups) g.SetActive(false);
            orderError = Label(detail, "", 12, amber, 16, 0, 500, 40);
            orderError.rectTransform.anchorMin = Vector2.zero; orderError.rectTransform.anchorMax = Vector2.right;
            orderError.rectTransform.offsetMin = new Vector2(16, 52); orderError.rectTransform.offsetMax = new Vector2(-16, 94);
            var commands = Rect(detail, "Workflow commands", Vector2.zero, Vector2.right,
                new Vector2(16, 8), new Vector2(-16, 46));
            primary = Command(commands, "提交报修", 0, 0, 180, SubmitWorkflow, true, 38);
            primaryText = primary.GetComponentInChildren<Text>();
            reject = Command(commands, "驳回重修", 192, 0, 120, () => {
                if (!simulation.Accept(editingOrder, false, acceptance.text)) orderError.text = simulation.Feedback;
            }, false, 38);
            Command(commands, "定位", 324, 0, 72, () => {
                if (editingOrder != null) { simulation.Select(editingOrder.device); Navigate(0); }
            }, false, 38);
        }
        void BuildReview()
        {
            var root = Page("Review"); Label(root, "实训复盘 / 本次会话", 18, ink, 24, 18, 600, 32);
            Scroll(root, "Review scroll", Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -64), out reviewContent);
            reviewText = FlowText(reviewContent, "Review report", 16);
        }
        void BuildKnowledge()
        {
            var root = Page("Knowledge"); Label(root, "AI问答 / 设备与工单上下文", 18, ink, 20, 14, 600, 34);
            var side = Rect(root, "Assistant", Vector2.zero, new Vector2(.62f, 1),
                new Vector2(8, 8), new Vector2(-12, -60));
            questionContext = Label(side, "", 13, accent, 16, 0, 730, 34);
            assistantStatus = Label(side, "", 12, amber, 16, 38, 680, 30);
            assistantScroll = Scroll(side, "Conversation", Vector2.zero, Vector2.one,
                new Vector2(0, 124), new Vector2(0, -80), out assistantContent);
            assistantText = FlowText(assistantContent, "Assistant conversation", 16);
            var reference = Rect(root, "Reference", new Vector2(.62f, 0), Vector2.one,
                new Vector2(0, 8), new Vector2(-8, -60));
            Label(reference, "知识参考", 16, ink, 16, 0, 360, 32);
            lessonMenu = Menu(reference, "Knowledge topic", 16, 46, 450,
                "当前设备 · 故障与处置", "维修闭环 · 角色与规则", "巡检与保养 · 区别与标准", "指标口径 · 实训边界");
            lessonMenu.onValueChanged.AddListener(_ => RefreshKnowledge());
            Scroll(reference, "Local knowledge", Vector2.zero, Vector2.one,
                new Vector2(0, 0), new Vector2(0, -100), out knowledgeContent);
            knowledgeText = FlowText(knowledgeContent, "Local reference", 14);
            var commands = Rect(side, "Question commands", Vector2.zero, Vector2.right,
                new Vector2(16, 72), new Vector2(-16, 108));
            Command(commands, "询问当前设备", 0, 0, 160, () => assistant.Ask(
                "请结合选中设备和当前工单状态，说明应检查的现象及下一步处理。"));
            Command(commands, "清空", 172, 0, 72, () => assistant.Clear());
            var band = Rect(side, "Question band", Vector2.zero, Vector2.right,
                new Vector2(16, 18), new Vector2(-16, 60));
            question = Input(band, "Question input", "输入设备或维修问题", 0, 0, 300, 42);
            var qr = (RectTransform)question.transform; qr.anchorMax = Vector2.one;
            qr.offsetMin = new Vector2(0, -42); qr.offsetMax = new Vector2(-86, 0);
            var anchor = Rect(band, "Send anchor", Vector2.one, Vector2.one, new Vector2(-76, 0), new Vector2(-76, 0));
            send = Command(anchor, "发送", 0, 0, 76, SendQuestion, true, 42);
            question.onSubmit.AddListener(_ => SendQuestion());
        }
        void BuildAnalysis()
        {
            var root = Page("Analysis");
            Label(root, "故障分析 / 工况、现象与处置", 18, ink, 20, 14, 640, 34);
            var devices = new List<string>();
            foreach (var d in simulation.devices) devices.Add(d.deviceId + " " + d.displayName);
            analysisDevice = Menu(root, "Analysis device", 20, 64, 330, devices.ToArray());
            analysisDevice.onValueChanged.AddListener(index => simulation.Select(simulation.devices[index], false));
            analysisMode = Menu(root, "Analysis mode", 370, 64, 590);
            analysisMode.onValueChanged.AddListener(_ => RefreshAnalysisText());
            analysisLive = Label(root, "", 14, amber, 24, 120, 1200, 64, TextAnchor.UpperLeft);
            Scroll(root, "Analysis evidence", Vector2.zero, Vector2.one,
                new Vector2(8, 76), new Vector2(-8, -196), out analysisContent);
            analysisText = FlowText(analysisContent, "Fault analysis", 16);
            var commands = Rect(root, "Analysis commands", Vector2.zero, Vector2.right,
                new Vector2(20, 18), new Vector2(-20, 56));
            Command(commands, "观察三维设备", 0, 0, 150, () => {
                simulation.cameraRig.Focus(simulation.Selected); Navigate(0);
            });
            Command(commands, "注入选定工况", 166, 0, 150, () => {
                var modes = WorkshopFaultCatalog.For(simulation.Selected.kind);
                if (simulation.TriggerFault(simulation.Selected, modes[analysisMode.value].Code)) RefreshAnalysis();
            }, true);
            Command(commands, "进入维修工单", 332, 0, 150, () => {
                editingOrder = simulation.ActiveAlarm; Navigate(2);
            });
            Command(commands, "有限元分析", 498, 0, 150, OpenAnalysis, true);
            Command(commands, "整机与演化", 664, 0, 150, OpenEquipment);
            Command(commands, "向AI提问", 830, 0, 150, () => {
                var fault = WorkshopFaultCatalog.For(simulation.Selected.kind)[analysisMode.value];
                Navigate(4);
                question.text = "请结合当前设备快照，解释 " + fault.Code + " " + fault.Name +
                    " 的现象、可能原因和检查步骤，并区分教学工况与实际诊断。";
            });
        }
        void RefreshAnalysis()
        {
            var d = simulation.Selected;
            if (!d) return;
            analysisDevice.SetValueWithoutNotify(Array.IndexOf(simulation.devices, d));
            var modes = WorkshopFaultCatalog.For(d.kind);
            string previous = analysisMode.options.Count > 0 ? analysisMode.options[analysisMode.value].text : "";
            var options = new List<string>(); int chosen = 0;
            for (int i = 0; i < modes.Length; i++)
            {
                options.Add(modes[i].Code + " / " + modes[i].Name + " / " + modes[i].SeverityText);
                if (d.Fault != null ? d.Fault == modes[i] : previous.StartsWith(modes[i].Code + " /")) chosen = i;
            }
            analysisMode.ClearOptions(); analysisMode.AddOptions(options);
            analysisMode.SetValueWithoutNotify(chosen); RefreshAnalysisText();
        }
        void RefreshAnalysisText()
        {
            var d = simulation.Selected;
            if (!d || analysisMode.options.Count == 0) return;
            var f = WorkshopFaultCatalog.For(d.kind)[analysisMode.value];
            var a = simulation.ActiveAlarm;
            analysisLive.text = d.deviceId + " / " + d.StatusText + "    温度 " + d.Temperature.ToString("F1") +
                " °C    " + ProcessMetrics(d) + "\n当前工单：" + (a == null ? "无活动工单" : a.id + " / " + a.StageText);
            int past = simulation.Records.FindAll(r => r.deviceName.StartsWith(d.deviceId + " ")).Count;
            FitText(analysisText, analysisContent, f.Code + " / " + f.Name +
                (d.Fault == f ? "    当前活动工况" : "    工况参考") +
                "\n\n现场现象\n" + f.Symptom + "\n\n故障机理（预设教学条件）\n" + f.Mechanism +
                "\n\n处置参考\n" + f.Action +
                "\n\n工况影响\n" + (f.Trips ? "严重故障，触发联锁停机。" : "预警工况，设备可继续异常运行。") +
                "\n\n处置进度\n" + (a == null ? "尚无活动故障，可选择工况开展实训。" :
                    a.id + " / " + a.StageText + " / 责任人 " + (a.assignee ?? "待派工") +
                    (a.cause == null ? "" : "\n维修诊断：" + a.cause) +
                    (a.acceptanceNote == null ? "" : "\n验收记录：" + a.acceptanceNote)) +
                "\n\n历史依据\n本次会话该设备已验收 " + past +
                " 单。当前工况来源于人工教学注入，分析不代表自动诊断或真实工业测量。");
        }
        void BuildImport()
        {
            var root = Page("Knowledge import");
            Label(root, "知识导入 / 设备资料与维护规程", 18, ink, 20, 14, 700, 34);
            var entry = Rect(root, "Import editor", Vector2.zero, new Vector2(.59f, 1),
                new Vector2(20, 90), new Vector2(-20, -64));
            var scroll = Scroll(entry, "Import form", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, out var content);
            content.sizeDelta = new Vector2(0, 526);
            importPath = Field(content, "资料文件路径", "UTF-8 .md 或 .txt 文件的完整路径", 0);
            Command(content, "读取文件", 0, 80, 110, ReadKnowledgeFile);
#if UNITY_EDITOR
            Command(content, "选择文件", 122, 80, 110, () => {
                string path = UnityEditor.EditorUtility.OpenFilePanel("选择设备资料", "", "");
                if (!string.IsNullOrEmpty(path)) { importPath.text = path; ReadKnowledgeFile(); }
            });
#endif
            importTitle = Field(content, "资料标题 *", "例如：循环水泵维护规程", 132);
            importBody = Field(content, "资料正文 *", "粘贴资料正文，或读取文件后检查内容", 210, 260);
            importBody.characterLimit = 50000;
            var submit = Rect(root, "Import commands", Vector2.zero, Vector2.right,
                new Vector2(20, 28), new Vector2(-20, 66));
            importSubmit = Command(submit, "导入并建立索引", 0, 0, 180, () => {
                assistant.ImportKnowledge(importTitle.text, importBody.text);
            }, true);
            Command(submit, "刷新资料列表", 196, 0, 150, () => assistant.RefreshDocuments());
            var library = Rect(root, "Imported library", new Vector2(.59f, 0), Vector2.one,
                new Vector2(0, 90), new Vector2(-16, -64));
            importStatus = Label(library, "", 13, amber, 16, 0, 500, 96, TextAnchor.UpperLeft);
            Scroll(library, "Imported documents", Vector2.zero, Vector2.one,
                Vector2.zero, new Vector2(0, -110), out importsContent);
            importList = FlowText(importsContent, "Knowledge documents", 14);
        }
        void ReadKnowledgeFile()
        {
            try
            {
                string path = (importPath.text ?? "").Trim().Trim('"');
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension != ".md" && extension != ".txt") throw new IOException("支持 UTF-8 的 .md / .txt 资料");
                if (new FileInfo(path).Length > 200000) throw new IOException("资料文件请控制在 200 KB 以内");
                string body = File.ReadAllText(path, new UTF8Encoding(false, true));
                if (body.Length > 50000) throw new IOException("正文请控制在 50000 字以内");
                importTitle.text = Path.GetFileNameWithoutExtension(path); importBody.text = body;
                importStatus.text = "资料已读取，待导入。";
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException)
            { importStatus.text = "读取失败：" + e.Message; }
        }
        void BuildFaultDialog()
        {
            var overlay = Box(transform, "Fault scenarios", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                new Color(0, 0, 0, .72f)); faultDialog = overlay.gameObject;
            var root = Box(overlay, "Scenario panel", new Vector2(.5f, .5f), new Vector2(.5f, .5f),
                new Vector2(-410, -320), new Vector2(410, 320), panel);
            Label(root, "新建故障实训课题", 22, ink, 24, 16, 610, 40);
            Command(root, "关闭", 708, 18, 88, () => faultDialog.SetActive(false));
            var devices = new List<string>();
            foreach (var d in simulation.devices) devices.Add(d.deviceId + "  " + d.displayName);
            deviceMenu = Menu(root, "Fault device", 24, 76, 772, devices.ToArray());
            modeMenu = Menu(root, "Fault mode", 24, 128, 772);
            deviceMenu.onValueChanged.AddListener(_ => PopulateFaultModes());
            modeMenu.onValueChanged.AddListener(_ => RefreshFaultDescription());
            Scroll(root, "Fault details", Vector2.zero, Vector2.one,
                new Vector2(12, 96), new Vector2(-12, -184), out dialogContent);
            dialogDescription = FlowText(dialogContent, "Fault description", 16);
            dialogError = Label(root, "", 13, amber, 24, 550, 772, 26);
            inject = Command(root, "注入故障", 24, 586, 170, () => {
                var modes = WorkshopFaultCatalog.For(FaultDevice.kind);
                if (simulation.TriggerFault(FaultDevice, modes[modeMenu.value].Code))
                { faultDialog.SetActive(false); editingOrder = simulation.ActiveAlarm; Navigate(0); }
                else dialogError.text = simulation.Feedback;
            }, true, 38);
            Label(root, "教学工况 / 注入后进入待报修", 13, muted, 224, 586, 560, 38);
            faultDialog.SetActive(false);
        }
        void OpenFaultDialog()
        {
            deviceMenu.SetValueWithoutNotify(Mathf.Max(0, Array.IndexOf(simulation.devices, simulation.Selected)));
            dialogError.text = ""; faultDialog.SetActive(true); PopulateFaultModes();
        }
        void PopulateFaultModes()
        {
            updating = true; modeMenu.ClearOptions();
            var options = new List<string>();
            foreach (var m in WorkshopFaultCatalog.For(FaultDevice.kind))
                options.Add(m.Code + "  " + m.Name + " / " + m.SeverityText);
            modeMenu.AddOptions(options); modeMenu.SetValueWithoutNotify(0); updating = false; RefreshFaultDescription();
        }
        void RefreshFaultDescription()
        {
            if (updating || !faultDialog.activeSelf) return;
            var m = WorkshopFaultCatalog.For(FaultDevice.kind)[modeMenu.value];
            FitText(dialogDescription, dialogContent, m.Name + " / " + m.SeverityText +
                (m.Trips ? " / 联锁停机" : " / 异常运行") + "\n\n故障机理\n" + m.Mechanism +
                "\n\n现场现象\n" + m.Symptom + "\n\n参考处置\n" + m.Action +
                "\n\n工况为本地教学模拟，维修提交后仍需使用方验收。");
            inject.interactable = FaultDevice.Fault == null;
        }
        void Navigate(int target)
        {
            page = target;
            for (int i = 0; i < pageRoots.Count; i++)
            { pageRoots[i].SetActive(i == page); navigation[i].image.color = i == page ? active : panel; }
            title.text = "数字孪生 / " + pages[page];
            if (page == 2 && editingOrder == null) editingOrder = simulation.ActiveAlarm;
            if (page == 6) assistant.RefreshDocuments();
            transform.Find("Background").GetComponent<Image>().enabled = page != 0;
            Refresh(); lastWidth = 0;
        }
        void Refresh()
        {
            if (!built) return;
            feedback.text = simulation.Feedback;
            for (int i = 0; i < simulation.devices.Length; i++)
            {
                var d = simulation.devices[i]; deviceStates[i].text = d.StatusText;
                deviceStates[i].color = d.Fault != null ? red : d.IsRunning ? accent : muted;
                deviceButtons[i].image.color = d == simulation.Selected ? active : field;
            }
            var selected = simulation.Selected;
            selectedName.text = selected ? selected.deviceId + "\n" + selected.displayName : "尚未选择设备";
            operation.interactable = selected && selected.Fault == null;
            operationText.text = selected && selected.Fault != null ? "故障锁定" :
                selected && selected.IsRunning ? "停止设备" : "启动设备";
            if (trendDevice != selected || simulation.Elapsed < lastSample)
            { trendDevice = selected; trend.Clear(); lastSample = -1; }
            if (page == 2) { RebuildOrders(); RefreshOrder(); }
            if (page == 3) RefreshReview();
            if (page == 4) RefreshKnowledge();
            if (page == 5) RefreshAnalysis();
            if (page == 6) RefreshAssistant();
            RefreshMetrics();
        }
        void RefreshMetrics()
        {
            if (!built) return;
            total.text = simulation.devices.Length.ToString("00"); running.text = simulation.RunningCount.ToString("00");
            alarms.text = simulation.OpenAlarmCount.ToString("00"); accepted.text = simulation.Records.Count.ToString("00");
            clock.text = simulation.SimulationTime.ToString("yyyy-MM-dd  HH:mm:ss");
            var d = simulation.Selected;
            selectedMetrics.text = d ? d.KindText + " / A 车间\n状态  " + d.StatusText +
                "\n温度  " + d.Temperature.ToString("F1") + " °C\n" +
                (d.HasVibration ? "振动  " + d.Vibration.ToString("F2") + " mm/s" : "振动  不适用") +
                "\n" + (d.HasRpm ? "转速  " + d.Rpm.ToString("F0") + " r/min" : "转速  不适用") +
                "\n" + ProcessMetrics(d) : "";
            var robot = simulation.inspector;
            drivingMode.name = robot && robot.ManualControl ? "恢复自动" : "手动接管";
            drivingMode.GetComponentInChildren<Text>().text = drivingMode.name;
            drivingMode.image.color = robot && robot.ManualControl ? active : field;
            taskText.text = robot && robot.IsBusy ?
                "巡检机器人 / " + (robot.ManualControl ? "手动接管" : robot.HasArrived ? "已到达 · 待维修" : "前往现场") + " / " + robot.TargetAlarm.device.deviceId :
                "巡检机器人 / " + (robot && robot.ManualControl ? "手动驾驶" : "待命") + "    ·    待验收 " + simulation.PendingAcceptanceCount + " 单";
            if (d && (lastSample < 0 || simulation.Elapsed - lastSample >= .24f))
            { trend.Sample(d.Temperature); lastSample = simulation.Elapsed; }
            for (int i = 0; i < deviceMetrics.Count; i++)
                deviceMetrics[i].text = simulation.devices[i].Temperature.ToString("F1") + " °C";
            orderSummary.text = "待验收 " + simulation.PendingAcceptanceCount + "   /   逾期 " + simulation.OverdueCount +
                "   /   已关闭 " + simulation.Records.Count;
            if (page == 4) questionContext.text = "当前设备：" + (d ? d.deviceId + " " + d.displayName : "未选择") +
                " / 工单：" + (simulation.ActiveAlarm?.StageText ?? "无活动工单");
            if (page == 5) RefreshAnalysisText();
        }
        void FilterDevices()
        {
            string text = (search.text ?? "").Trim(); int visible = 0;
            for (int i = 0; i < deviceButtons.Count; i++)
            {
                var d = simulation.devices[i];
                bool show = (d.deviceId + d.displayName + d.KindText).IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
                deviceButtons[i].gameObject.SetActive(show);
                if (!show) continue;
                var r = (RectTransform)deviceButtons[i].transform;
                r.offsetMin = new Vector2(0, -visible * 62 - 56); r.offsetMax = new Vector2(0, -visible * 62); visible++;
            }
            ((RectTransform)deviceButtons[0].transform.parent).sizeDelta = new Vector2(0, visible * 62);
        }
        void RebuildOrders()
        {
            string signature = orderFilter.value + ":" + simulation.Alarms.Count + ":" + editingOrder?.id;
            foreach (var a in simulation.Alarms) signature += ":" + a.id + a.stage + a.reworkCount;
            if (signature == rowSignature) return; rowSignature = signature;
            foreach (Transform child in orderRows) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            int row = 0;
            foreach (var a in simulation.Alarms)
            {
                if (orderFilter.value > 0 && (int)a.stage != orderFilter.value - 1) continue;
                var alarm = a;
                var b = Command(orderRows, alarm.id + "   " + alarm.device.deviceId + "   " + alarm.StageText +
                    "\n" + alarm.fault.Name + " / " + alarm.fault.SeverityText,
                    0, row * 76, 520, () => { editingOrder = alarm; simulation.Select(alarm.device, false); }, false, 68);
                b.name = "Order " + alarm.id; b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                var r = (RectTransform)b.transform; r.anchorMax = Vector2.one;
                r.offsetMin = new Vector2(0, -row * 76 - 68); r.offsetMax = new Vector2(0, -row * 76);
                b.image.color = alarm == editingOrder ? active : field; row++;
            }
            if (row == 0) Label(orderRows, "暂无符合条件的工单", 15, muted, 16, 12, 400, 50);
            orderRows.sizeDelta = new Vector2(0, Mathf.Max(80, row * 76));
        }
        void RefreshOrder()
        {
            var a = editingOrder;
            if (a != null && !simulation.Alarms.Contains(a)) a = editingOrder = null;
            if (a == null)
            {
                FitText(orderDetail, orderDetailContent, "尚未选择工单\n\n新建故障课题后，选择告警开始报修。");
                form.SetActive(false); primary.interactable = false; reject.gameObject.SetActive(false); return;
            }
            FitText(orderDetail, orderDetailContent, a.id + " / " + a.StageText + " / " + a.device.deviceId +
                "\n" + a.device.displayName + " · " + a.fault.Name + " · " + a.fault.SeverityText +
                "\n发现 " + a.occurredAt.ToString("HH:mm:ss") +
                (a.reportedAt != default ? "  报修 " + a.reportedAt.ToString("HH:mm:ss") : "") +
                "\n报修人 " + (a.reporter ?? "待填写") + " / " + (a.department ?? "待填写") +
                "    维修人 " + (a.assignee ?? "待指派") +
                (a.dueAt != default ? "\n计划完成 " + a.dueAt.ToString("HH:mm:ss") : "") +
                (a.description != null ? "\n现场记录 " + a.description : "") +
                (a.repairAction != null ? "\n维修措施 " + a.repairAction + "\n配件 " + a.materials : "") +
                (a.acceptanceNote != null ? "\n验收意见 " + a.acceptanceNote : "") +
                (a.reworkCount > 0 ? "\n已驳回 " + a.reworkCount + " 次" : ""));
            bool changed = loadedOrder != a || loadedStage != a.stage;
            loadedOrder = a; loadedStage = a.stage;
            int group = a.stage == WorkshopOrderStage.Detected ? 0 : a.stage == WorkshopOrderStage.Reported ? 1 :
                a.stage == WorkshopOrderStage.Repairing ? 2 : a.stage == WorkshopOrderStage.AwaitingAcceptance ? 3 : -1;
            form.SetActive(group >= 0);
            for (int i = 0; i < formGroups.Count; i++) formGroups[i].SetActive(i == group);
            if (group >= 0)
            {
                var scroll = form.GetComponent<ScrollRect>(); scroll.content = (RectTransform)formGroups[group].transform;
                scroll.content.sizeDelta = new Vector2(0, group == 0 || group == 2 ? 300 : 200);
                if (changed) { scroll.content.anchoredPosition = Vector2.zero; scroll.verticalNormalizedPosition = 1; }
            }
            if (changed)
            {
                person.text = a.reporter ?? ""; department.text = a.department ?? ""; description.text = a.description ?? "";
                worker.text = a.assignee ?? ""; deadline.text = "30"; cause.text = a.cause ?? "";
                action.text = a.repairAction ?? ""; materials.text = a.materials ?? ""; acceptance.text = ""; orderError.text = "";
            }
            primaryText.text = new[] { "提交报修", "主管派工", "前往现场中", "提交维修", "验收通过", "已归档" }[(int)a.stage];
            primary.interactable = group >= 0 && (a.stage != WorkshopOrderStage.Reported || !simulation.inspector.IsBusy);
            reject.gameObject.SetActive(a.stage == WorkshopOrderStage.AwaitingAcceptance);
            if (a.stage == WorkshopOrderStage.Reported && simulation.inspector.IsBusy)
                orderError.text = "机器人正在执行其他任务，空闲后可派工。";
            if (a.stage == WorkshopOrderStage.AwaitingAcceptance) orderError.text = "当前验收角色：" + a.reporter + "（使用方）";
        }
        void SubmitWorkflow()
        {
            if (editingOrder == null) return;
            bool ok = false;
            switch (editingOrder.stage)
            {
                case WorkshopOrderStage.Detected:
                    ok = simulation.Report(editingOrder, person.text, department.text, description.text); break;
                case WorkshopOrderStage.Reported:
                    if (!int.TryParse(deadline.text, out int minutes)) { orderError.text = "处理时限请填写整数分钟"; return; }
                    ok = simulation.Assign(editingOrder, worker.text, minutes); break;
                case WorkshopOrderStage.Repairing:
                    ok = simulation.SubmitRepair(editingOrder, cause.text, action.text, materials.text); break;
                case WorkshopOrderStage.AwaitingAcceptance:
                    ok = simulation.Accept(editingOrder, true, acceptance.text); break;
            }
            if (!ok) orderError.text = simulation.Feedback;
        }
        void RefreshReview()
        {
            var text = new StringBuilder();
            text.Append("本次实训结果\n\n已发现 ").Append(simulation.Alarms.Count).Append(" 项故障    未关闭 ")
                .Append(simulation.OpenAlarmCount).Append(" 项    验收归档 ").Append(simulation.Records.Count)
                .Append(" 项\n待验收 ").Append(simulation.PendingAcceptanceCount).Append(" 单    逾期 ")
                .Append(simulation.OverdueCount).Append(" 单\n平均报修闭环时长 ")
                .Append(simulation.Records.Count == 0 ? "暂无样本" : simulation.AverageClosureMinutes.ToString("F2") + " 分钟")
                .Append("\n\n设备故障分布\n");
            foreach (var d in simulation.devices)
            {
                int count = simulation.Alarms.FindAll(a => a.device == d).Count;
                if (count > 0) text.Append(d.deviceId).Append(" ").Append(d.displayName).Append("    ").Append(count).Append(" 次\n");
            }
            text.Append("\n验收归档明细\n");
            if (simulation.Records.Count == 0) text.Append("暂无完成验收的工单。\n");
            foreach (var r in simulation.Records)
                text.Append("\n").Append(r.alarmId).Append(" / ").Append(r.deviceName).Append(" / ").Append(r.faultName)
                    .Append("\n报修 ").Append(r.reporter).Append(" → 维修 ").Append(r.assignee)
                    .Append(" → 使用方验收 / ").Append(r.completedAt.ToString("HH:mm:ss"))
                    .Append("\n故障原因：").Append(r.cause).Append("\n处理措施：").Append(r.action)
                    .Append("\n配件材料：").Append(r.materials).Append("\n验收结论：").Append(r.acceptanceNote)
                    .Append("\n闭环 ").Append(r.elapsedMinutes.ToString("F2")).Append(" 分钟 / 驳回 ").Append(r.reworkCount).Append(" 次\n");
            text.Append("\n统计口径：闭环时长 = 验收关闭时间 − 报修时间；仅统计已关闭工单。\n本次会话记录保存在内存，重置或退出运行后清空。");
            FitText(reviewText, reviewContent, text.ToString());
        }
        void RefreshKnowledge()
        {
            var d = simulation.Selected; var text = new StringBuilder();
            switch (lessonMenu.value)
            {
                case 0:
                    text.Append(d ? d.deviceId + " / " + d.displayName + "\n" : "");
                    if (d) foreach (var f in WorkshopFaultCatalog.For(d.kind))
                        text.Append("\n").Append(f.Code).Append(" · ").Append(f.Name).Append("\n现象：").Append(f.Symptom)
                            .Append("\n机理：").Append(f.Mechanism).Append("\n处置参考：").Append(f.Action).Append("\n");
                    text.Append("\n资料来源：本项目 WorkshopFaultCatalog，教学工况。"); break;
                case 1:
                    text.Append("维修闭环\n\n1. 报修人：填写设备、故障现象、部门和姓名。\n\n2. 维修主管：指定维修人和完成时限，确认机器人可达现场。\n\n3. 维修人员：到达后填写故障原因、维修措施和配件材料。\n\n4. 使用方：确认恢复情况；通过后归档，驳回后重新派工复查。\n\n维修提交不解除故障，验收通过后恢复故障前启停状态。\n\n实训角色按节点切换，当前版本不提供账号鉴权。"); break;
                case 2:
                    text.Append("巡检与保养\n\n巡检：检查运行状态、观察异常并记录现场现象。\n\n保养：依照标准执行清洁、润滑、紧固与调整。\n\n巡检异常需进入报修工单，处理完成后再验收。\n\n本平台已实现故障触发巡检；周期巡检计划、保养排程和备件库存尚未接入。维修材料只记录本次使用情况。"); break;
                default:
                    text.Append("指标与数据边界\n\n运行数：当前处于运行状态的场景设备数量。\n\n未关闭告警：尚未通过验收的故障数量。\n\n验收归档：使用方确认通过后生成的记录数量。\n\n逾期：已派工、尚未关闭且超过计划完成时间的工单。\n\n平均闭环时长：已关闭工单的“验收关闭时间 − 报修时间”均值，包含等待与维修。\n\n设备指标均为本地模拟；故障示意不等于真实工业诊断。在线问答使用场景快照，失败时显示实际错误。"); break;
            }
            FitText(knowledgeText, knowledgeContent, text.ToString());
        }
        void SendQuestion()
        {
            if (assistant.Busy || string.IsNullOrWhiteSpace(question.text)) return;
            string text = question.text; question.text = ""; assistant.Ask(text);
        }
        void RefreshAssistant()
        {
            if (!built) return;
            assistantStatus.text = assistant.ConnectionStatus;
            importStatus.text = assistant.ImportStatus + "\n" + assistant.ConnectionStatus;
            importSubmit.interactable = !assistant.ImportBusy;
            var docs = new StringBuilder("已入库资料\n\n");
            if (assistant.Documents.Count == 0) docs.Append("暂无资料列表。\n");
            foreach (var doc in assistant.Documents)
                docs.Append(doc.title).Append("\n").Append(doc.source).Append("\n")
                    .Append(doc.chunks).Append(" 个检索片段\n\n");
            FitText(importList, importsContent, docs.ToString());
            var text = new StringBuilder();
            foreach (var t in assistant.Conversation)
                text.Append(t.role == "user" ? "提问\n" : "知识助手\n").Append(t.content).Append("\n\n");
            if (assistant.Busy) text.Append("正在检索资料…\n");
            if (!string.IsNullOrEmpty(assistant.Error)) text.Append(assistant.Error).Append("\n");
            if (assistant.LastAnswer?.sources != null)
                foreach (var s in assistant.LastAnswer.sources)
                    text.Append("\n[").Append(s.id).Append("] ").Append(s.source).Append(" / 第 ").Append(s.chunk_index + 1).Append(" 段");
            if (text.Length == 0) text.Append("暂无对话。");
            FitText(assistantText, assistantContent, text.ToString());
            send.interactable = !assistant.Busy; question.interactable = !assistant.Busy;
            assistantScroll.verticalNormalizedPosition = 0;
        }
        static string ProcessMetrics(WorkshopDevice d)
        {
            if (d.kind == DeviceKind.Pump) return "压力 " + d.Pressure.ToString("F2") + " MPa / 流量 " + d.Flow.ToString("F1") + " m³/h";
            if (d.kind == DeviceKind.Tank) return "压力 " + d.Pressure.ToString("F2") + " MPa / 液位 " + d.Level.ToString("F1") + "%";
            if (d.kind == DeviceKind.Cabinet) return "电压 " + d.Voltage.ToString("F1") + " V / 通信 " + d.Communication.ToString("F0") + "%";
            return "负载 " + d.LoadPercent.ToString("F0") + "%";
        }
        RectTransform sceneRegion;
        void LateUpdate()
        {
            if (!built || !interfaceVisibility.Visible || Screen.width < 1 || Screen.height < 1 ||
                lastWidth == Screen.width && lastHeight == Screen.height) return;
            lastWidth = Screen.width; lastHeight = Screen.height; Canvas.ForceUpdateCanvases();
            foreach (var t in GetComponentsInChildren<Text>()) t.SetAllDirty();
            // Keep the existing camera's screen-space ray and overlay mapping intact.
            var corners = new Vector3[4]; sceneRegion.GetWorldCorners(corners);
            var camera = simulation.cameraRig.GetComponent<Camera>();
            camera.rect = new Rect(corners[0].x / Screen.width, corners[0].y / Screen.height,
                (corners[2].x - corners[0].x) / Screen.width, (corners[2].y - corners[0].y) / Screen.height);
            transform.Find("Background").GetComponent<Image>().enabled = page != 0;
            if (page == 2) RefreshOrder();
            if (page == 3) RefreshReview();
            if (page == 4) { RefreshKnowledge(); RefreshAssistant(); }
            if (page == 5) RefreshAnalysis();
            if (page == 6) RefreshAssistant();
            if (faultDialog.activeSelf) RefreshFaultDescription();
        }
    }
}
