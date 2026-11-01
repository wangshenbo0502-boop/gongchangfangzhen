using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace WorkshopDemo
{
    [DefaultExecutionOrder(-20)]
    public sealed class WorkshopUI : MonoBehaviour
    {
        public WorkshopSimulation simulation;
        public Font chineseFont;
        static readonly Color Background = new Color(.075f, .085f, .095f, .99f);
        static readonly Color Surface = new Color(.12f, .135f, .15f, .99f);
        static readonly Color Line = new Color(.23f, .25f, .27f);
        static readonly Color Ink = new Color(.92f, .94f, .95f);
        static readonly Color Muted = new Color(.58f, .63f, .66f);
        static readonly Color Yellow = new Color(.96f, .77f, .15f);
        static readonly Color Green = new Color(.27f, .8f, .59f);
        static readonly Color Red = new Color(.98f, .36f, .32f);
        readonly List<Text> names = new List<Text>();
        readonly List<Text> states = new List<Text>();
        readonly List<Image> backgrounds = new List<Image>();
        readonly List<Image> indicators = new List<Image>();
        readonly List<Image> selectionBars = new List<Image>();
        readonly List<GameObject> eventRows = new List<GameObject>();
        Text counters, clock, nameLabel, identity, status, temperature, vibration, rpm;
        Text location, feedback, task, taskTarget, modeStatus, empty, operationLabel, alarmCount, recordCount;
        Button faultButton, dispatchButton, resolveButton, operationButton, alarmTab, recordTab;
        Button autoButton, manualButton, followButton;
        RectTransform eventContent, viewport, tableHeader;
        Toggle roofToggle;
        bool showRecords;
        int eventSignature = -1, lastWidth, lastHeight;
        const float Left = 214, Right = 242, Header = 52, Bottom = 184;

        void Awake()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1440, 900);
            scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<GraphicRaycaster>();
            if (!EventSystem.current)
            {
                var events = new GameObject("Workshop Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            Build();
        }

        void OnEnable()
        {
            simulation.Changed += Refresh;
            simulation.Sampled += RefreshMetrics;
        }

        void Start() { Refresh(); }

        void OnDisable()
        {
            if (!simulation) return;
            simulation.Changed -= Refresh;
            simulation.Sampled -= RefreshMetrics;
        }

        RectTransform Rect(Transform parent, string label, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            var rect = (RectTransform)new GameObject(label, typeof(RectTransform)).transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            return rect;
        }

        RectTransform Panel(Transform parent, string label, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax, Color color, bool blocks = true)
        {
            var rect = Rect(parent, label, min, max, offsetMin, offsetMax);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = blocks;
            return rect;
        }

        RectTransform Box(Transform parent, string name, float x, float y, float width, float height, Color color, bool blocks = true)
        {
            return Panel(parent, name, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(x, -y - height), new Vector2(x + width, -y), color, blocks);
        }

        Text Label(Transform parent, string content, int size, Color color, float x, float y,
            float width, float height, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var rect = Rect(parent, string.IsNullOrEmpty(content) ? "Value" : content,
                new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(x, -y - height), new Vector2(x + width, -y));
            var text = rect.gameObject.AddComponent<Text>();
            text.font = chineseFont; text.text = content; text.fontSize = size;
            text.color = color; text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        Button Command(Transform parent, string name, float x, float y, float width,
            Action clicked, float height = 32, Color? color = null)
        {
            var rect = Box(parent, name, x, y, width, height, color ?? Surface);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f);
            colors.pressedColor = new Color(.72f, .74f, .76f);
            colors.disabledColor = new Color(.5f, .5f, .5f, .65f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => clicked());
            Label(rect, name, 13, Ink, 6, 0, width - 12, height, TextAnchor.MiddleCenter);
            return button;
        }

        RectTransform Scroll(Transform parent, string name, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax, out RectTransform content)
        {
            var rect = Panel(parent, name, min, max, offsetMin, offsetMax, Background);
            rect.gameObject.AddComponent<RectMask2D>();
            var scroll = rect.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = rect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            content = Rect(rect, "Rows", new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(.5f, 1);
            scroll.content = content;
            return rect;
        }

        void Divider(Transform parent, float x, float y, float width)
        {
            Box(parent, "Divider", x, y, width, 1, Line, false);
        }

        void Build()
        {
            var header = Panel(transform, "Header", new Vector2(0, 1), Vector2.one,
                new Vector2(0, -Header), Vector2.zero, Background);
            Box(header, "Brand accent", 18, 17, 4, 18, Yellow, false);
            Label(header, "车间监控", 19, Ink, 32, 0, 112, Header);
            Label(header, "设备 / 巡检", 12, Muted, 146, 0, 110, Header);
            counters = Label(header, "", 13, Ink, 286, 0, 420, Header);
            var headerRight = Rect(header, "Header right", new Vector2(1, 1), Vector2.one,
                new Vector2(-345, -Header), Vector2.zero);
            clock = Label(headerRight, "", 12, Muted, 0, 0, 230, Header, TextAnchor.MiddleRight);
            Command(headerRight, "重置演示", 248, 11, 80, simulation.ResetDemo, 30);

            var left = Panel(transform, "Devices", Vector2.zero, new Vector2(0, 1),
                Vector2.zero, new Vector2(Left, -Header), Background);
            Label(left, "设备台账", 15, Ink, 18, 16, 132, 26);
            Label(left, simulation.devices.Length.ToString("00"), 14, Muted, 161, 16, 34, 26, TextAnchor.MiddleRight);
            Label(left, "生产车间 A", 12, Muted, 18, 46, 178, 22);
            Divider(left, 18, 80, Left - 36);
            Scroll(left, "Device list", Vector2.zero, Vector2.one,
                new Vector2(8, 48), new Vector2(-8, -91), out var content);
            content.sizeDelta = new Vector2(0, simulation.devices.Length * 55);
            foreach (var device in simulation.devices)
            {
                int i = names.Count;
                var button = Command(content, "", 0, i * 55, Left - 16,
                    () => simulation.Select(device), 53, Background);
                button.name = "Device " + device.deviceId;
                button.GetComponentInChildren<Text>().gameObject.SetActive(false);
                names.Add(Label(button.transform, device.deviceId + "  " + device.displayName, 13, Ink, 12, 4, 174, 23));
                states.Add(Label(button.transform, "", 11, Muted, 23, 27, 155, 19));
                indicators.Add(Box(button.transform, "Status indicator", 13, 34, 4, 4, Green, false).GetComponent<Image>());
                selectionBars.Add(Box(button.transform, "Selection", 0, 9, 2, 34, Yellow, false).GetComponent<Image>());
                backgrounds.Add(button.image);
                Divider(button.transform, 12, 52, 174);
            }
            var source = Label(left, "本地模拟  /  离线运行", 11, Muted, 18, 0, 178, 24);
            source.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, 14, 24);

            viewport = Rect(transform, "Scene viewport", Vector2.zero, Vector2.one,
                new Vector2(Left, Bottom), new Vector2(-Right, -Header));
            var toolbar = Panel(viewport, "View controls", new Vector2(0, 1), Vector2.one,
                new Vector2(0, -46), Vector2.zero, Background);
            Command(toolbar, "总览", 10, 8, 58, () => simulation.cameraRig.Overview(), 30);
            Command(toolbar, "自由观察", 72, 8, 78, () => {
                if (simulation.cameraRig.Following) simulation.cameraRig.ToggleFollow();
                simulation.Notify("自由观察");
            }, 30);
            followButton = Command(toolbar, "巡检跟随", 154, 8, 78, () => simulation.cameraRig.ToggleFollow(), 30);
            var toggleRect = Panel(toolbar, "Roof toggle", new Vector2(1, 1), Vector2.one,
                new Vector2(-82, -38), new Vector2(-10, -8), Surface);
            roofToggle = toggleRect.gameObject.AddComponent<Toggle>();
            var checkOutline = Box(toggleRect, "Checkbox", 9, 9, 12, 12, Line, false);
            var check = Box(checkOutline, "Check", 2, 2, 8, 8, Yellow, false);
            roofToggle.targetGraphic = toggleRect.GetComponent<Image>();
            roofToggle.graphic = check.GetComponent<Image>();
            Label(toggleRect, "屋顶", 12, Ink, 29, 0, 38, 30);
            roofToggle.isOn = false;
            roofToggle.onValueChanged.AddListener(value => { if (simulation.roof) simulation.roof.SetActive(value); });

            var right = Panel(transform, "Details", new Vector2(1, 0), Vector2.one,
                new Vector2(-Right, Bottom), new Vector2(0, -Header), Background);
            Label(right, "当前设备", 12, Muted, 18, 15, 206, 24);
            nameLabel = Label(right, "", 21, Ink, 18, 44, 206, 34);
            identity = Label(right, "", 11, Muted, 18, 81, 206, 22);
            status = Label(right, "", 13, Green, 18, 111, 206, 25);
            Divider(right, 18, 148, 206);
            temperature = Metric(right, "温度 / °C", 161);
            vibration = Metric(right, "振动 / mm/s", 210);
            rpm = Metric(right, "转速 / r/min", 259);
            location = Label(right, "", 11, Muted, 18, 310, 206, 22);
            operationButton = Command(right, "停止设备", 18, 344, 99, simulation.ToggleSelected);
            operationLabel = operationButton.GetComponentInChildren<Text>();
            faultButton = Command(right, "触发设备故障", 125, 344, 99, simulation.TriggerFault,
                32, new Color(.39f, .21f, .18f));
            Divider(right, 18, 397, 206);
            Label(right, "巡检机器人", 14, Ink, 18, 409, 206, 26);
            autoButton = Command(right, "自动巡检", 18, 446, 101, () => simulation.inspector.SetManualControl(false));
            manualButton = Command(right, "手动驾驶", 121, 446, 103, () => simulation.inspector.SetManualControl(true));
            modeStatus = Label(right, "", 11, Muted, 18, 484, 206, 24);
            task = Label(right, "", 13, Green, 18, 521, 206, 23);
            taskTarget = Label(right, "", 11, Muted, 18, 546, 206, 23);
            dispatchButton = Command(right, "派发巡检", 18, 584, 99, simulation.Dispatch, 32, new Color(.21f, .30f, .26f));
            resolveButton = Command(right, "确认处理", 125, 584, 99, simulation.Resolve, 32, new Color(.21f, .30f, .26f));

            var bottom = Panel(transform, "Events", Vector2.zero, new Vector2(1, 0),
                new Vector2(Left, 0), new Vector2(0, Bottom), Background);
            alarmTab = Command(bottom, "告警", 16, 7, 62, () => { showRecords = false; Refresh(); }, 30, Background);
            alarmCount = Label(bottom, "", 11, Muted, 81, 7, 32, 30);
            recordTab = Command(bottom, "巡检记录", 128, 7, 88, () => { showRecords = true; Refresh(); }, 30, Background);
            recordCount = Label(bottom, "", 11, Muted, 219, 7, 32, 30);
            tableHeader = Rect(bottom, "Table headings", new Vector2(0, 1), Vector2.one,
                new Vector2(16, -67), new Vector2(-16, -42));
            TableText(tableHeader, "编号", 0, .15f, Muted, 25);
            TableText(tableHeader, "时间", .15f, .28f, Muted, 25);
            TableText(tableHeader, "设备", .28f, .53f, Muted, 25);
            TableText(tableHeader, "故障", .53f, .81f, Muted, 25);
            TableText(tableHeader, "处理进度", .81f, 1, Muted, 25);
            Scroll(bottom, "Event list", Vector2.zero, Vector2.one,
                new Vector2(16, 33), new Vector2(-16, -69), out eventContent);
            empty = Label(eventContent, "", 12, Muted, 12, 3, 400, 30);
            feedback = Label(bottom, "", 11, Muted, 20, 0, 1050, 26);
            feedback.rectTransform.anchorMin = Vector2.zero;
            feedback.rectTransform.anchorMax = new Vector2(1, 0);
            feedback.rectTransform.offsetMin = new Vector2(20, 2);
            feedback.rectTransform.offsetMax = new Vector2(-20, 28);
        }

        Text Metric(Transform parent, string name, float y)
        {
            Label(parent, name, 11, Muted, 18, y, 106, 27);
            var value = Label(parent, "", 23, Ink, 124, y - 4, 100, 38, TextAnchor.MiddleRight);
            Divider(parent, 18, y + 37, 206);
            return value;
        }

        Text TableText(Transform parent, string text, float start, float end, Color color, float height = 30)
        {
            var label = Label(parent, text, 12, color, 0, 0, 100, height);
            label.rectTransform.anchorMin = new Vector2(start, 1);
            label.rectTransform.anchorMax = new Vector2(end, 1);
            label.rectTransform.offsetMin = new Vector2(10, -height);
            label.rectTransform.offsetMax = new Vector2(-8, 0);
            return label;
        }

        static void Segment(Button button, bool selected)
        {
            button.image.color = selected ? Yellow : Surface;
            button.GetComponentInChildren<Text>().color = selected ? Background : Muted;
        }

        void Refresh()
        {
            for (int i = 0; i < names.Count; i++)
            {
                var d = simulation.devices[i];
                var color = d.Status == DeviceStatus.Fault ? Red : d.IsRunning ? Green : Muted;
                states[i].text = d.StatusText;
                states[i].color = color;
                indicators[i].color = color;
                backgrounds[i].color = d == simulation.Selected ? Surface : Background;
                selectionBars[i].enabled = d == simulation.Selected;
            }
            feedback.text = simulation.Feedback;
            roofToggle.SetIsOnWithoutNotify(simulation.roof && simulation.roof.activeSelf);
            faultButton.interactable = simulation.CanFaultSelected;
            dispatchButton.interactable = simulation.CanDispatch;
            resolveButton.interactable = simulation.CanResolve;
            Segment(alarmTab, !showRecords);
            Segment(recordTab, showRecords);
            Segment(autoButton, !simulation.inspector.ManualControl);
            Segment(manualButton, simulation.inspector.ManualControl);
            alarmCount.text = simulation.OpenAlarmCount.ToString("00");
            alarmCount.color = simulation.OpenAlarmCount > 0 ? Red : Muted;
            recordCount.text = simulation.Records.Count.ToString("00");
            var selected = simulation.Selected;
            if (selected)
            {
                nameLabel.text = selected.displayName;
                identity.text = selected.deviceId + "  /  " + selected.KindText;
                status.text = selected.StatusText;
                status.color = selected.Status == DeviceStatus.Fault ? Red : selected.IsRunning ? Green : Muted;
                location.text = "坐标  " + selected.transform.position.x.ToString("F1") +
                    " / " + selected.transform.position.z.ToString("F1");
                operationButton.interactable = selected.Status != DeviceStatus.Fault;
                operationLabel.text = selected.IsRunning ? "停止设备" : "启动设备";
            }
            var alarm = simulation.inspector.TargetAlarm;
            task.text = alarm != null ? alarm.progress : "待命";
            task.color = simulation.CanResolve ? Yellow : Green;
            var pending = simulation.ActiveAlarm;
            taskTarget.text = alarm != null ? "任务  " + alarm.device.deviceId + " " + alarm.device.displayName :
                pending != null ? "待派发  " + pending.device.deviceId + " " + pending.device.displayName : "当前无巡检任务";
            RebuildEvents();
            RefreshMetrics();
        }

        void RefreshMetrics()
        {
            counters.text = "运行  " + simulation.RunningCount.ToString("00") +
                " / " + simulation.devices.Length.ToString("00") +
                "       告警  " + simulation.OpenAlarmCount.ToString("00");
            clock.text = "模拟  " + simulation.SimulationTime.ToString("MM.dd  HH:mm:ss");
            var robot = simulation.inspector;
            modeStatus.text = robot.ManualControl ? "手动控制" :
                robot.IsBusy && !robot.HasArrived ? "距目标  " + robot.RemainingDistance.ToString("F1") + " m" : "自动控制";
            var d = simulation.Selected;
            if (!d) return;
            temperature.text = d.Temperature.ToString("F1");
            temperature.color = d.Status == DeviceStatus.Fault ? Red : Ink;
            vibration.text = d.HasVibration ? d.Vibration.ToString("F2") : "--";
            rpm.text = d.HasRpm ? d.Rpm.ToString("F0") : "--";
        }

        void RebuildEvents()
        {
            int signature = HashCode.Combine(showRecords, simulation.Alarms.Count, simulation.Records.Count);
            foreach (var alarm in simulation.Alarms) signature = HashCode.Combine(signature, alarm.progress, alarm.resolved);
            if (signature == eventSignature) return;
            eventSignature = signature;
            foreach (var row in eventRows) { row.SetActive(false); Destroy(row); }
            eventRows.Clear();
            tableHeader.GetChild(3).GetComponent<Text>().text = showRecords ? "处理结果" : "故障";
            tableHeader.GetChild(4).GetComponent<Text>().text = showRecords ? "状态" : "处理进度";
            int count = showRecords ? simulation.Records.Count : simulation.Alarms.Count;
            eventContent.sizeDelta = new Vector2(0, Mathf.Max(36, count * 31));
            empty.gameObject.SetActive(count == 0);
            empty.text = showRecords ? "暂无巡检记录" : "暂无告警";
            for (int i = 0; i < count; i++)
            {
                var row = Panel(eventContent, "Event row " + i, new Vector2(0, 1), Vector2.one,
                    new Vector2(0, -(i + 1) * 31), new Vector2(0, -i * 31 - 1),
                    i % 2 == 0 ? Surface : Background);
                eventRows.Add(row.gameObject);
                if (showRecords)
                {
                    var r = simulation.Records[i];
                    TableText(row, r.alarmId, 0, .15f, Muted);
                    TableText(row, r.completedAt.ToString("HH:mm:ss"), .15f, .28f, Muted);
                    TableText(row, r.deviceName, .28f, .53f, Ink);
                    var action = TableText(row, r.action, .53f, .81f, Muted);
                    action.resizeTextForBestFit = true;
                    action.resizeTextMinSize = 9;
                    action.resizeTextMaxSize = 12;
                    TableText(row, "已完成", .81f, 1, Green);
                }
                else
                {
                    var a = simulation.Alarms[i];
                    var button = row.gameObject.AddComponent<Button>();
                    button.targetGraphic = row.GetComponent<Image>();
                    button.navigation = new Navigation { mode = Navigation.Mode.None };
                    button.onClick.AddListener(() => simulation.Select(a.device));
                    TableText(row, a.id, 0, .15f, a.resolved ? Muted : Red);
                    TableText(row, a.occurredAt.ToString("HH:mm:ss"), .15f, .28f, Muted);
                    TableText(row, a.device.deviceId + " " + a.device.displayName, .28f, .53f, Ink);
                    TableText(row, a.device.FaultText, .53f, .81f, a.resolved ? Muted : Red);
                    TableText(row, a.progress, .81f, 1, a.resolved ? Green : Yellow);
                }
            }
        }

        void LateUpdate()
        {
            Segment(followButton, simulation.cameraRig.Following);
            if (Screen.width == lastWidth && Screen.height == lastHeight) return;
            lastWidth = Screen.width; lastHeight = Screen.height;
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            viewport.GetWorldCorners(corners);
            var camera = simulation.cameraRig.GetComponent<Camera>();
            float toolbarHeight = 46 * GetComponent<Canvas>().scaleFactor;
            camera.rect = new Rect(corners[0].x / Screen.width, corners[0].y / Screen.height,
                (corners[2].x - corners[0].x) / Screen.width,
                (corners[2].y - corners[0].y - toolbarHeight) / Screen.height);
        }
    }
}
