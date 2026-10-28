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
        readonly Color background = new Color(.10f, .12f, .13f, .98f);
        readonly Color surface = new Color(.16f, .18f, .19f);
        readonly Color foreground = new Color(.88f, .92f, .93f);
        readonly Color muted = new Color(.58f, .66f, .68f);
        readonly Color teal = new Color(.05f, .66f, .58f);
        readonly Color red = new Color(.94f, .28f, .23f);
        readonly List<Text> deviceRows = new List<Text>();
        readonly List<Image> deviceBackgrounds = new List<Image>();
        Text counters, clock, nameLabel, identity, status, temperature, vibration, rpm, location, feedback;
        Button faultButton, dispatchButton, resolveButton, operationButton, alarmTab, recordTab;
        Text operationLabel;
        RectTransform eventContent, viewport;
        Canvas canvas;
        Toggle roofToggle;
        bool showRecords;
        int lastWidth, lastHeight;

        void Awake()
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
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
            var go = new GameObject(label, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            return rect;
        }

        RectTransform Panel(Transform parent, string label, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var rect = Rect(parent, label, min, max, offsetMin, offsetMax);
            rect.gameObject.AddComponent<Image>().color = color;
            return rect;
        }

        Text Label(Transform parent, string content, int size, Color color, float x, float y,
            float width, float height, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var rect = Rect(parent, content, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(x, -y - height), new Vector2(x + width, -y));
            var text = rect.gameObject.AddComponent<Text>();
            text.font = chineseFont; text.text = content; text.fontSize = size;
            text.color = color; text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        Button Command(Transform parent, string content, float x, float y, float width,
            Action clicked, Color? color = null, float height = 34)
        {
            var rect = Panel(parent, content, new Vector2(0, 1), new Vector2(0, 1),
                new Vector2(x, -y - height), new Vector2(x + width, -y), color ?? surface);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(.7f, .8f, .8f);
            colors.disabledColor = new Color(.45f, .45f, .45f);
            button.colors = colors;
            button.onClick.AddListener(() => clicked());
            Label(rect, content, 14, foreground, 6, 0, width - 12, height, TextAnchor.MiddleCenter);
            return button;
        }

        RectTransform Scroll(Transform parent, string name, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax, out RectTransform content)
        {
            var rect = Panel(parent, name, min, max, offsetMin, offsetMax, background);
            rect.gameObject.AddComponent<RectMask2D>();
            var scroll = rect.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = rect;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 26;
            content = Rect(rect, "Rows", new Vector2(0, 1), Vector2.one, Vector2.zero, Vector2.zero);
            content.pivot = new Vector2(.5f, 1);
            scroll.content = content;
            return rect;
        }

        void Build()
        {
            var header = Panel(transform, "Header", new Vector2(0, 1), Vector2.one,
                new Vector2(0, -56), Vector2.zero, background);
            Label(header, "工业车间 / 设备监控与巡检", 18, foreground, 20, 0, 370, 56);
            counters = Label(header, "", 14, foreground, 406, 0, 360, 56);
            var headerRight = Rect(header, "Clock", new Vector2(1, 1), Vector2.one,
                new Vector2(-338, -56), Vector2.zero);
            clock = Label(headerRight, "", 13, muted, 0, 0, 318, 56, TextAnchor.MiddleRight);

            var left = Panel(transform, "Devices", Vector2.zero, new Vector2(0, 1),
                Vector2.zero, new Vector2(232, -56), background);
            Label(left, "设备台账", 17, foreground, 18, 14, 196, 30);
            Label(left, "生产车间 A · 8 台设备", 12, muted, 18, 45, 196, 23);
            Scroll(left, "Device list", Vector2.zero, Vector2.one,
                new Vector2(10, 48), new Vector2(-10, -82), out var content);
            content.sizeDelta = new Vector2(0, simulation.devices.Length * 65);
            for (int i = 0; i < simulation.devices.Length; i++)
            {
                var device = simulation.devices[i];
                var button = Command(content, "", 0, i * 65, 212, () => simulation.Select(device), surface, 59);
                Destroy(button.GetComponentInChildren<Text>().gameObject);
                var row = Label(button.transform, "", 13, foreground, 10, 4, 192, 51);
                deviceRows.Add(row);
                deviceBackgrounds.Add(button.GetComponent<Image>());
            }
            Label(left, "数据来源：本地模拟", 12, muted, 18, 0, 196, 30).rectTransform
                .SetInsetAndSizeFromParentEdge(RectTransform.Edge.Bottom, 12, 30);

            viewport = Rect(transform, "Scene viewport", Vector2.zero, Vector2.one,
                new Vector2(232, 176), new Vector2(-252, -56));
            var toolbar = Panel(viewport, "View controls", new Vector2(0, 1), Vector2.one,
                new Vector2(10, -48), new Vector2(-10, -10), new Color(.10f, .12f, .13f, .92f));
            Command(toolbar, "总览", 4, 2, 66, () => simulation.cameraRig.Overview());
            Command(toolbar, "自由观察", 76, 2, 84, () => {
                if (simulation.cameraRig.Following) simulation.cameraRig.ToggleFollow();
                simulation.Notify("自由观察");
            });
            Command(toolbar, "巡检跟随", 166, 2, 84, () => simulation.cameraRig.ToggleFollow());
            var toggle = Panel(toolbar, "Roof toggle", new Vector2(1, 1), Vector2.one,
                new Vector2(-96, -36), new Vector2(-6, -2), surface).gameObject.AddComponent<Toggle>();
            var check = Panel(toggle.transform, "Check", new Vector2(0, .5f), new Vector2(0, .5f),
                new Vector2(8, -6), new Vector2(20, 6), teal);
            toggle.graphic = check.GetComponent<Image>();
            toggle.targetGraphic = toggle.GetComponent<Image>();
            Label(toggle.transform, "屋顶", 13, foreground, 27, 0, 57, 34);
            toggle.isOn = false;
            roofToggle = toggle;
            toggle.onValueChanged.AddListener(value => { if (simulation.roof) simulation.roof.SetActive(value); });

            var right = Panel(transform, "Details", new Vector2(1, 0), Vector2.one,
                new Vector2(-252, 176), new Vector2(0, -56), background);
            Label(right, "设备详情", 13, muted, 18, 13, 214, 25);
            nameLabel = Label(right, "", 20, foreground, 18, 48, 216, 42);
            identity = Label(right, "", 12, muted, 18, 95, 216, 25);
            status = Label(right, "", 17, teal, 18, 137, 216, 32);
            temperature = Label(right, "", 17, foreground, 18, 190, 216, 34);
            vibration = Label(right, "", 16, foreground, 18, 235, 216, 34);
            rpm = Label(right, "", 16, foreground, 18, 280, 216, 34);
            location = Label(right, "", 12, muted, 18, 332, 216, 36);
            operationButton = Command(right, "停止设备", 18, 391, 216, simulation.ToggleSelected);
            operationLabel = operationButton.GetComponentInChildren<Text>();

            var bottom = Panel(transform, "Events", Vector2.zero, new Vector2(1, 0),
                new Vector2(232, 0), new Vector2(0, 176), background);
            alarmTab = Command(bottom, "告警", 12, 8, 80, () => { showRecords = false; Refresh(); });
            recordTab = Command(bottom, "巡检记录", 98, 8, 94, () => { showRecords = true; Refresh(); });
            var actions = Rect(bottom, "Actions", new Vector2(1, 1), Vector2.one,
                new Vector2(-546, -44), new Vector2(-12, -8));
            faultButton = Command(actions, "触发演示故障", 0, 0, 124, simulation.TriggerFault, new Color(.61f, .28f, .12f));
            dispatchButton = Command(actions, "派发巡检", 132, 0, 100, simulation.Dispatch, teal);
            resolveButton = Command(actions, "确认处理", 240, 0, 100, simulation.Resolve, teal);
            Command(actions, "重置演示", 348, 0, 100, simulation.ResetDemo);
            Label(actions, "模拟", 12, muted, 458, 0, 70, 34, TextAnchor.MiddleRight);
            Scroll(bottom, "Event list", Vector2.zero, Vector2.one,
                new Vector2(12, 36), new Vector2(-12, -51), out eventContent);
            feedback = Label(bottom, "", 12, muted, 14, 144, 960, 27);
        }

        void Refresh()
        {
            for (int i = 0; i < simulation.devices.Length; i++)
            {
                var d = simulation.devices[i];
                deviceRows[i].text = d.deviceId + "  " + d.displayName + "\n" +
                    (d.Status == DeviceStatus.Fault ? "! " : d.IsRunning ? "> " : "- ") + d.StatusText;
                deviceRows[i].color = d.Status == DeviceStatus.Fault ? red : foreground;
                deviceBackgrounds[i].color = d == simulation.Selected ? new Color(.08f, .31f, .30f) : surface;
            }
            feedback.text = simulation.Feedback;
            roofToggle.SetIsOnWithoutNotify(simulation.roof && simulation.roof.activeSelf);
            faultButton.interactable = simulation.ActiveAlarm == null;
            dispatchButton.interactable = simulation.CanDispatch;
            resolveButton.interactable = simulation.CanResolve;
            alarmTab.image.color = !showRecords ? teal : surface;
            recordTab.image.color = showRecords ? teal : surface;
            var selected = simulation.Selected;
            if (selected)
            {
                nameLabel.text = selected.displayName;
                identity.text = selected.deviceId + "  /  " + selected.KindText;
                status.text = (selected.Status == DeviceStatus.Fault ? "! " : selected.IsRunning ? "> " : "- ") + selected.StatusText;
                status.color = selected.Status == DeviceStatus.Fault ? red : selected.IsRunning ? teal : muted;
                location.text = "生产车间 A\n位置  X " + selected.transform.position.x.ToString("F1") +
                    " / Z " + selected.transform.position.z.ToString("F1");
                operationButton.interactable = selected.Status != DeviceStatus.Fault;
                operationLabel.text = selected.IsRunning ? "停止设备" : "启动设备";
            }
            RebuildEvents();
            RefreshMetrics();
        }

        void RefreshMetrics()
        {
            counters.text = "运行  " + simulation.RunningCount + " / " + simulation.devices.Length +
                "      未处理告警  " + simulation.OpenAlarmCount;
            clock.text = "模拟数据  |  " + simulation.SimulationTime.ToString("yyyy-MM-dd HH:mm:ss");
            var d = simulation.Selected;
            if (!d) return;
            temperature.text = "温度     " + d.Temperature.ToString("F1") + " °C";
            vibration.text = "振动     " + (d.HasVibration ? d.Vibration.ToString("F2") + " mm/s" : "不适用");
            rpm.text = "转速     " + (d.HasRpm ? d.Rpm.ToString("F0") + " r/min" : "不适用");
        }

        void RebuildEvents()
        {
            for (int i = eventContent.childCount - 1; i >= 0; i--)
            {
                var child = eventContent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
            int count = showRecords ? simulation.Records.Count : simulation.Alarms.Count;
            eventContent.sizeDelta = new Vector2(0, Mathf.Max(40, count * 38));
            if (count == 0)
            {
                Label(eventContent, showRecords ? "暂无巡检记录" : "暂无告警 · 设备运行正常", 13, muted, 8, 0, 900, 40);
                return;
            }
            for (int i = 0; i < count; i++)
            {
                if (showRecords)
                {
                    var r = simulation.Records[i];
                    Label(eventContent, r.completedAt.ToString("HH:mm:ss") + "    " + r.alarmId +
                        "    " + r.deviceName + "    " + r.action, 12, foreground, 8, i * 38, 970, 36);
                }
                else
                {
                    var alarm = simulation.Alarms[i];
                    string row = alarm.id + "    " + alarm.occurredAt.ToString("HH:mm:ss") + "    " +
                        alarm.device.deviceId + " " + alarm.device.displayName +
                        "    超温告警 / 严重    " + alarm.progress;
                    Command(eventContent, row, 0, i * 38, 990, () => simulation.Select(alarm.device),
                        alarm.resolved ? surface : new Color(.29f, .13f, .12f), 34);
                }
            }
        }

        void LateUpdate()
        {
            if (Screen.width == lastWidth && Screen.height == lastHeight) return;
            lastWidth = Screen.width; lastHeight = Screen.height;
            Canvas.ForceUpdateCanvases();
            var corners = new Vector3[4];
            viewport.GetWorldCorners(corners);
            simulation.cameraRig.GetComponent<Camera>().rect = new Rect(corners[0].x / Screen.width,
                corners[0].y / Screen.height, (corners[2].x - corners[0].x) / Screen.width,
                (corners[2].y - corners[0].y) / Screen.height);
        }
    }
}
