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
        readonly Color ink = new Color32(237, 237, 232, 255);
        readonly Color muted = new Color32(165, 168, 170, 255);
        readonly Color panel = new Color32(29, 31, 33, 255);
        readonly Color field = new Color32(43, 45, 47, 255);
        readonly Color accent = new Color32(205, 208, 205, 255);
        readonly Color active = new Color32(232, 182, 62, 255);
        readonly Color amber = new Color32(232, 182, 62, 255);
        readonly Color red = new Color32(239, 105, 95, 255);
        readonly string[] pages = { "车间总览", "设备目录", "故障处置", "实训复盘", "AI 助手", "故障学习", "资料管理" };
        readonly List<GameObject> pageRoots = new List<GameObject>();
        readonly List<Text> deviceStates = new List<Text>();
        readonly List<Text> deviceMetrics = new List<Text>();
        readonly List<Button> deviceButtons = new List<Button>();
        Text title, clock, feedback, total, running, alarms, accepted, selectedName, selectedMetrics, taskText;
        Text orderSummary, orderDetail, orderError, reviewText, assistantStatus;
        Text dialogDescription, dialogError, primaryText;
        Text analysisText, analysisLive, importStatus, importList, questionContext;
        RectTransform analysisContent, importsContent;
        RectTransform viewport, orderRows, orderDetailContent, assistantContent, reviewContent, dialogContent;
        WorkshopTrendGraphic trend;
        WorkshopDevice trendDevice;
        WorkshopAlarm editingOrder, loadedOrder;
        Button primary, reject, orderReturn, inject, send, drivingMode;
        InputField search, cause, question;
        InputField importTitle, importBody, importPath;
        Dropdown orderFilter, deviceMenu, modeMenu;
        Dropdown modelMenu, analysisDevice, analysisMode;
        Button importSubmit;
        GameObject faultDialog, form;
        WorkshopAssistantClient assistant;
        ScrollRect assistantScroll;
        Button chatReset;
        GameObject chatWelcome;
        WorkshopAssistantClient.Turn renderedChatTail;
        int renderedChatCount = -1;
        float renderedChatWidth;
        string renderedChatStatus;
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
            t.font = chineseFont; t.text = value; t.fontSize = Mathf.Max(14, size); t.color = tint; t.alignment = alignment;
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
            colors.highlightedColor = new Color(.92f, .92f, .88f); colors.pressedColor = new Color(.75f, .75f, .72f);
            colors.disabledColor = new Color(.45f, .45f, .45f); b.colors = colors;
            b.onClick.AddListener(() => callback());
            var label = Label(r, text, 16, emphasized ? Background : ink, 0, 0, width, height, TextAnchor.MiddleCenter);
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
        void Build() => BuildCompactShell();
        void BuildOverview() => BuildCompactOverview();
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
                var b = Command(rows, "", 0, i * 62, 1000, () => { simulation.Select(d); RefreshAnalysis(); OpenEquipment(); }, false, 56);
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
            var note = Label(root, "档案来源：场景配置    |    点击设备查看结构与拆装    |    维修历史见实训复盘", 12, muted, 24, 0, 1100, 34);
            note.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, 16, 34);
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
                "全部工单", "待巡检", "巡检途中", "待维修", "待验收", "已关闭");
            orderFilter.onValueChanged.AddListener(_ => { rowSignature = ""; RebuildOrders(); });
            orderSummary = Label(root, "", 13, amber, 516, 12, 630, 38);
            Scroll(root, "Order list", Vector2.zero, new Vector2(.48f, 1),
                new Vector2(16, 16), new Vector2(-12, -66), out orderRows);
            var detail = Box(root, "Work order detail", new Vector2(.48f, 0), Vector2.one,
                new Vector2(0, 16), new Vector2(-16, -66), field);
            Scroll(detail, "Order detail scroll", Vector2.zero, Vector2.one,
                new Vector2(0, 98), Vector2.zero, out orderDetailContent);
            orderDetail = FlowText(orderDetailContent, "Order details", 14);
            form = Box(detail, "Workflow form", Vector2.zero, Vector2.one,
                new Vector2(0, 98), new Vector2(0, -152), panel).gameObject;
            cause = Field(form.transform, "故障原因 *", "记录现场确认的故障原因", 16, 116);
            orderError = Label(detail, "", 12, amber, 16, 0, 500, 40);
            orderError.rectTransform.anchorMin = Vector2.zero; orderError.rectTransform.anchorMax = Vector2.right;
            orderError.rectTransform.offsetMin = new Vector2(16, 52); orderError.rectTransform.offsetMax = new Vector2(-16, 94);
            var commands = Rect(detail, "Workflow commands", Vector2.zero, Vector2.right,
                new Vector2(16, 8), new Vector2(-16, 46));
            primary = Command(commands, "开始巡检", 0, 0, 180, SubmitWorkflow, true, 38);
            primaryText = primary.GetComponentInChildren<Text>();
            reject = Command(commands, "驳回重修", 192, 0, 120, () => {
                if (!simulation.Accept(editingOrder, false)) orderError.text = simulation.Feedback;
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
            var root = Page("Knowledge");
            root.GetComponent<Image>().color = Background;
            var chat = Rect(root, "Chat column", new Vector2(.10f, 0), new Vector2(.90f, 1),
                new Vector2(0, 12), new Vector2(0, -12));
            var header = Rect(chat, "Chat header", Vector2.up, Vector2.one, new Vector2(0, -58), Vector2.zero);
            questionContext = Label(header, "", 15, ink, 0, 0, 600, 28);
            assistantStatus = Label(header, "", 13, muted, 0, 28, 600, 24);
            var resetAnchor = Rect(header, "New chat anchor", Vector2.one, Vector2.one,
                new Vector2(-110, 0), new Vector2(-110, 0));
            chatReset = Command(resetAnchor, "新对话", 0, 2, 110, () => {
                question.SetTextWithoutNotify("");
                assistant.Clear();
            });
            assistantScroll = Scroll(chat, "Conversation", Vector2.zero, Vector2.one,
                new Vector2(0, 104), new Vector2(0, -64), out assistantContent);
            assistantScroll.GetComponent<Image>().color = Background;
            chatWelcome = Rect(chat, "Chat suggestions", new Vector2(0, .5f), new Vector2(1, .5f),
                new Vector2(0, -90), new Vector2(0, 80)).gameObject;
            Label(chatWelcome.transform, "今天想了解什么？", 28, ink, 20, 0, 510, 48);
            Command(chatWelcome.transform, "当前设备状态", 20, 68, 172,
                () => AskSuggestion("当前设备是什么状态？有什么值得关注的指标？"), false, 42);
            Command(chatWelcome.transform, "下一步怎么做", 204, 68, 172,
                () => AskSuggestion("根据当前设备和工单状态，我在软件里下一步该点哪里？"), false, 42);
            Command(chatWelcome.transform, "了解故障原因", 388, 68, 172,
                () => AskSuggestion("当前设备有哪些常见教学故障？怎么从现象区分它们？"), false, 42);
            var band = Box(chat, "Question band", Vector2.zero, Vector2.right,
                Vector2.zero, new Vector2(0, 88), panel);
            question = Input(band, "Question input", "询问设备、告警或实训操作…", 12, 8, 500, 64);
            question.characterLimit = 1500;
            question.lineType = InputField.LineType.MultiLineSubmit;
            var qr = (RectTransform)question.transform;
            qr.anchorMax = Vector2.one;
            qr.offsetMax = new Vector2(-100, -8);
            var anchor = Rect(band, "Send anchor", Vector2.one, Vector2.one, new Vector2(-88, 0), new Vector2(-88, 0));
            send = Command(anchor, "发送", 0, 16, 76, SendQuestion, true, 42);
            question.onSubmit.AddListener(_ => SendQuestion());
            question.onValueChanged.AddListener(value => send.interactable = !assistant.Busy && !string.IsNullOrWhiteSpace(value));
            Label(band, "Enter 发送", 12, muted, 14, 69, 180, 18);
        }

        void AskSuggestion(string suggestion)
        {
            question.text = suggestion;
            SendQuestion();
        }

        void RefreshChat()
        {
            if (page != 4) return;
            assistantStatus.text = assistant.ConnectionStatus;
            var selected = simulation.Selected;
            var alarm = simulation.ActiveAlarm;
            questionContext.text = selected ? selected.deviceId + "  " + selected.displayName +
                (alarm == null ? "" : "  ·  " + OrderStageLabel(alarm.stage)) : "车间上下文";
            if (assistant.FailedQuestion.Length > 0 && question.text.Length == 0 && renderedChatStatus != assistant.Error)
                question.SetTextWithoutNotify(assistant.FailedQuestion);
            send.interactable = !assistant.Busy && !string.IsNullOrWhiteSpace(question.text);
            chatReset.interactable = !assistant.Busy;
            question.interactable = !assistant.Busy;
            var conversation = assistant.Conversation;
            var tail = conversation.Count > 0 ? conversation[conversation.Count - 1] : null;
            string status = assistant.Busy ? "正在查找资料并组织回答…" : assistant.Error;
            float width = assistantContent.rect.width;
            if (renderedChatCount == conversation.Count && renderedChatTail == tail &&
                renderedChatStatus == status && Mathf.Approximately(renderedChatWidth, width)) return;
            renderedChatCount = conversation.Count;
            renderedChatTail = tail;
            renderedChatStatus = status;
            renderedChatWidth = width;
            chatWelcome.SetActive(conversation.Count == 0 && status.Length == 0);
            foreach (Transform child in assistantContent)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            float y = 14;
            foreach (var turn in conversation)
            {
                bool user = turn.role == "user";
                float messageWidth = Mathf.Max(120, (width - 32) * (user ? .72f : 1));
                float x = user ? width - 16 - messageWidth : 16;
                if (!user)
                {
                    Label(assistantContent, "助手", 14, amber, x, y, messageWidth, 24);
                    y += 28;
                }
                var message = Box(assistantContent, user ? "User message" : "Assistant message",
                    x, y, messageWidth, 1, user ? field : Background, false);
                var body = Label(message, turn.content, 16, ink, 12, 8, messageWidth - 24, 100, TextAnchor.UpperLeft);
                float bodyHeight = Mathf.Max(24, body.preferredHeight + 4);
                body.rectTransform.offsetMin = new Vector2(12, -8 - bodyHeight);
                message.offsetMin = new Vector2(x, -y - bodyHeight - 16);
                y += bodyHeight + 24;
                if (turn.sources != null && turn.sources.Length > 0)
                {
                    var sources = turn.sources;
                    Command(assistantContent, "引用 " + sources.Length, x + 12, y, 104, () => OpenChatSources(sources), false, 32);
                    y += 42;
                }
                y += 16;
            }
            if (status.Length > 0)
            {
                var state = Label(assistantContent, status, 14, assistant.Busy ? muted : amber,
                    16, y, Mathf.Max(120, width - 32), 100, TextAnchor.UpperLeft);
                float height = Mathf.Max(26, state.preferredHeight + 6);
                state.rectTransform.offsetMin = new Vector2(16, -y - height);
                y += height;
            }
            assistantContent.sizeDelta = new Vector2(0, y + 12);
            if (assistantScroll.isActiveAndEnabled)
            {
                Canvas.ForceUpdateCanvases();
                assistantScroll.verticalNormalizedPosition = 0;
            }
        }

        void OpenChatSources(WorkshopAssistantClient.Source[] sources)
        {
            CloseQuickMenu();
            var overlay = Box(transform, "Chat sources", Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero, new Color(0, 0, 0, .68f));
            quickMenu = overlay.gameObject;
            var card = Box(overlay, "Sources card", new Vector2(.19f, .13f), new Vector2(.81f, .87f),
                Vector2.zero, Vector2.zero, panel);
            Label(card, "回答引用", 21, ink, 24, 14, 300, 44);
            var anchor = Rect(card, "Close anchor", Vector2.one, Vector2.one, new Vector2(-104, -12), new Vector2(-104, -12));
            Command(anchor, "关闭", 0, 0, 84, CloseQuickMenu);
            Scroll(card, "Source excerpts", Vector2.zero, Vector2.one,
                new Vector2(8, 12), new Vector2(-8, -64), out var content);
            var details = new StringBuilder();
            foreach (var source in sources)
                details.Append('[').Append(source.id).Append("] ").Append(source.source)
                    .Append(" · 第 ").Append(source.chunk_index + 1).Append(" 段\n")
                    .Append(source.content).Append("\n\n");
            Canvas.ForceUpdateCanvases();
            FitText(FlowText(content, "Cited material", 15), content, details.ToString());
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
            faultAction = Command(commands, "开始故障实训", 0, 0, 190, FaultPrimaryAction, true, 42);
            Command(commands, "查看演示", 206, 0, 150, () => { OpenEquipment(); SetEquipmentMode(2); }, false, 42);
            Command(commands, "深入分析", 372, 0, 150, OpenAnalysis, false, 42);
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
            if (faultAction) faultAction.GetComponentInChildren<Text>().text = a == null ? "开始故障实训" : "继续处理工单";
            analysisLive.text = d.deviceId + " / " + d.StatusText + "    温度 " + d.Temperature.ToString("F1") +
                " °C    " + ProcessMetrics(d) + "\n当前工单：" + (a == null ? "无活动工单" : a.id + " / " + a.StageText);
            int past = simulation.Records.FindAll(r => r.deviceName.StartsWith(d.deviceId + " ")).Count;
            FitText(analysisText, analysisContent, f.Code + " / " + f.Name +
                (d.Fault == f ? "    当前活动工况" : "    工况参考") +
                "\n\n现场现象\n" + f.Symptom + "\n\n故障机理（预设教学条件）\n" + f.Mechanism +
                "\n\n处置参考\n" + f.Action +
                "\n\n工况影响\n" + (f.Trips ? "严重故障，触发联锁停机。" : "预警工况，设备可继续异常运行。") +
                "\n\n处置进度\n" + (a == null ? "尚无活动故障，可选择工况开展实训。" :
                    a.id + " / " + OrderStageLabel(a.stage) +
                    (a.cause == null ? "" : "\n故障原因：" + a.cause)) +
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
                { faultDialog.SetActive(false); OpenOrder(simulation.ActiveAlarm); }
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
            if (target != 0) orderReturn.gameObject.SetActive(false);
            for (int i = 0; i < pageRoots.Count; i++)
            { pageRoots[i].SetActive(i == page); }
            RefreshCompactNavigation();
            title.text = pages[page];
            if (page == 2 && editingOrder == null) editingOrder = simulation.ActiveAlarm;
            if (page == 6) assistant.RefreshDocuments();
            transform.Find("Background").GetComponent<Image>().enabled = page != 0;
            Refresh(); lastWidth = 0;
        }
        void OpenOrder(WorkshopAlarm alarm)
        {
            editingOrder = alarm;
            orderFilter.SetValueWithoutNotify(0);
            Navigate(2);
        }

        void ViewOrderScene(WorkshopAlarm alarm)
        {
            simulation.Select(alarm.device);
            if (simulation.inspector.TargetAlarm == alarm && !simulation.cameraRig.Following)
                simulation.cameraRig.ToggleFollow();
            Navigate(0);
            orderReturn.gameObject.SetActive(true);
        }
        void Refresh()
        {
            if (!built) return;
            feedback.text = simulation.Feedback;
            for (int i = 0; i < simulation.devices.Length; i++)
            {
                var d = simulation.devices[i]; deviceStates[i].text = d.StatusText;
                deviceStates[i].color = d.Fault != null ? red : d.IsRunning ? accent : muted;
                deviceButtons[i].image.color = d == simulation.Selected ? new Color32(64, 60, 46, 255) : field;
            }
            var selected = simulation.Selected;
            selectedName.text = selected ? selected.deviceId + "\n" + selected.displayName : "尚未选择设备";
            if (selectedAction)
            {
                selectedAction.interactable = selected;
                selectedAction.GetComponentInChildren<Text>().text = simulation.ActiveAlarm == null ? "查看设备" : "处理告警";
            }
            if (trendDevice != selected || simulation.Elapsed < lastSample)
            { trendDevice = selected; trend.Clear(); lastSample = -1; }
            if (page == 2) { RebuildOrders(); RefreshOrder(); }
            if (page == 3) RefreshReview();
            if (page == 4) RefreshChat();
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
            drivingMode.GetComponentInChildren<Text>().text = robot && robot.ManualControl ? "手动驾驶中" : "巡检控制";
            StyleSelection(drivingMode, robot && robot.ManualControl);
            taskText.text = robot && robot.IsBusy ?
                "巡检机器人 / " + (robot.ManualControl ? "手动接管" : robot.HasArrived ? "已到达 · 待维修" : "前往现场") + " / " + robot.TargetAlarm.device.deviceId :
                "巡检机器人 / " + (robot && robot.ManualControl ? "手动驾驶" : "待命") + "    ·    待验收 " + simulation.PendingAcceptanceCount + " 单";
            if (d && (lastSample < 0 || simulation.Elapsed - lastSample >= .24f))
            { trend.Sample(d.Temperature); lastSample = simulation.Elapsed; }
            for (int i = 0; i < deviceMetrics.Count; i++)
                deviceMetrics[i].text = simulation.devices[i].Temperature.ToString("F1") + " °C";
            orderSummary.text = "待验收 " + simulation.PendingAcceptanceCount + "   /   逾期 " + simulation.OverdueCount +
                "   /   已关闭 " + simulation.Records.Count;
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
                int filterIndex = a.stage == WorkshopOrderStage.Detected ? 1 : (int)a.stage;
                if (orderFilter.value > 0 && filterIndex != orderFilter.value) continue;
                var alarm = a;
                var b = Command(orderRows, alarm.device.deviceId + "  " + alarm.device.displayName +
                    "\n" + OrderStageLabel(alarm.stage) + " · " + alarm.fault.Name,
                    0, row * 76, 520, () => { editingOrder = alarm; simulation.Select(alarm.device, false); }, false, 68);
                b.name = "Order " + alarm.id; b.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleLeft;
                var r = (RectTransform)b.transform; r.anchorMax = Vector2.one;
                r.offsetMin = new Vector2(0, -row * 76 - 68); r.offsetMax = new Vector2(0, -row * 76);
                b.image.color = alarm == editingOrder ? new Color32(64, 60, 46, 255) : field; row++;
            }
            if (row == 0) Label(orderRows, "暂无符合条件的工单", 15, muted, 16, 12, 400, 50);
            orderRows.sizeDelta = new Vector2(0, Mathf.Max(80, row * 76));
        }

        static string OrderStageLabel(WorkshopOrderStage stage) =>
            stage == WorkshopOrderStage.Detected || stage == WorkshopOrderStage.Reported ? "待巡检" :
            stage == WorkshopOrderStage.Closed ? "已归档" : stage switch {
                WorkshopOrderStage.Dispatched => "巡检途中",
                WorkshopOrderStage.Repairing => "待维修",
                _ => "待验收"
            };

        void RefreshOrder()
        {
            var a = editingOrder;
            if (a != null && !simulation.Alarms.Contains(a)) a = editingOrder = null;
            if (a == null)
            {
                FitText(orderDetail, orderDetailContent, "选择一张工单，或点击「新建课题」开始故障实训。");
                loadedOrder = null;
                form.SetActive(false); primary.gameObject.SetActive(false); reject.gameObject.SetActive(false);
                orderError.text = "";
                return;
            }
            bool waiting = a.stage == WorkshopOrderStage.Detected || a.stage == WorkshopOrderStage.Reported;
            bool repairing = a.stage == WorkshopOrderStage.Repairing;
            string next = a.stage switch {
                WorkshopOrderStage.Dispatched => "小车正在前往设备。可查看现场，或等待到达。",
                WorkshopOrderStage.Repairing => "小车已到场。填写故障原因后提交维修。",
                WorkshopOrderStage.AwaitingAcceptance => "确认设备处理结果，通过后归档。",
                WorkshopOrderStage.Closed => "工单已验收归档。",
                _ => "点击开始巡检，小车将前往这台设备。"
            };
            FitText(orderDetail, orderDetailContent, a.device.deviceId + "  " + a.device.displayName +
                "\n" + a.id + " · " + OrderStageLabel(a.stage) + " · " + a.fault.Name +
                "\n\n" + next + (!repairing && a.cause != null ? "\n\n故障原因：" + a.cause : ""));
            form.SetActive(repairing);
            if (loadedOrder != a) cause.SetTextWithoutNotify(a.cause ?? "");
            loadedOrder = a;
            primaryText.text = a.stage switch {
                WorkshopOrderStage.Dispatched => "查看现场",
                WorkshopOrderStage.Repairing => "提交维修",
                WorkshopOrderStage.AwaitingAcceptance => "验收通过",
                WorkshopOrderStage.Closed => "查看复盘",
                _ => "开始巡检"
            };
            primary.gameObject.SetActive(true);
            primary.interactable = !waiting || (simulation.inspector && !simulation.inspector.IsBusy);
            reject.gameObject.SetActive(a.stage == WorkshopOrderStage.AwaitingAcceptance);
            orderError.text = waiting && !primary.interactable ? "小车忙碌或不可用，空闲后可开始巡检。" : "";
        }
        void SubmitWorkflow()
        {
            if (editingOrder == null) return;
            bool ok = false;
            switch (editingOrder.stage)
            {
                case WorkshopOrderStage.Detected:
                case WorkshopOrderStage.Reported:
                    ok = simulation.StartInspection(editingOrder); break;
                case WorkshopOrderStage.Dispatched:
                    ViewOrderScene(editingOrder); return;
                case WorkshopOrderStage.Repairing:
                    ok = simulation.SubmitRepair(editingOrder, cause.text); break;
                case WorkshopOrderStage.AwaitingAcceptance:
                    ok = simulation.Accept(editingOrder, true); break;
                case WorkshopOrderStage.Closed:
                    Navigate(3); return;
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
                    .Append("\n验收时间 ").Append(r.completedAt.ToString("HH:mm:ss"))
                    .Append("\n故障原因：").Append(r.cause)
                    .Append("\n闭环 ").Append(r.elapsedMinutes.ToString("F2")).Append(" 分钟 / 驳回 ").Append(r.reworkCount).Append(" 次\n");
            text.Append("\n统计口径：闭环时长 = 验收关闭时间 − 报修时间；仅统计已关闭工单。\n本次会话记录保存在内存，重置或退出运行后清空。");
            FitText(reviewText, reviewContent, text.ToString());
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
            RefreshChat();
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
            var visible = new Rect(corners[0].x / Screen.width, corners[0].y / Screen.height,
                (corners[2].x - corners[0].x) / Screen.width, (corners[2].y - corners[0].y) / Screen.height);
            simulation.cameraRig.GetComponent<Camera>().rect = visible;
            UpdateSceneFrame(visible);
            transform.Find("Background").GetComponent<Image>().enabled = page != 0;
            if (page == 2) RefreshOrder();
            if (page == 3) RefreshReview();
            if (page == 4) RefreshChat();
            if (page == 5) RefreshAnalysis();
            if (page == 6) RefreshAssistant();
            if (faultDialog.activeSelf) RefreshFaultDescription();
        }
    }
}
