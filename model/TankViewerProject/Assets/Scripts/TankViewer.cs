using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class TankViewer : MonoBehaviour
{
    [Serializable] sealed class Part
    {
        public string name, label, description, anchor;
        public string[] meshes;
        public Key[] path;
    }
    [Serializable] sealed class Key { public float time; public Vector3 offset; }
    [Serializable] sealed class Manifest { public Part[] parts; public float durationSeconds; }

    public Transform tank;
    public Camera viewCamera;
    public TextAsset componentData;
    public Font uiFont;
    Manifest manifest;
    Transform[] parts;
    Transform[] anchors;
    Vector3[] homes;
    Bounds[] partBounds;
    Renderer[][] partRenderers;
    Vector3 axisX, axisY, axisZ, reviewOffset;
    float progress, target, yaw = 26, pitch = 15, zoom = 1;
    bool animating, dragging, moved, introVisible, pending;
    Vector2 mouseStart, pointerDown;
    Text status, intro;
    Text[] tags;
    Rect[] tagRects;
    RectTransform introPanel;
    Canvas canvas;

    void Awake()
    {
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = Mathf.Clamp(Mathf.RoundToInt(
            (float)Screen.currentResolution.refreshRateRatio.value), 60, 120);
        Application.runInBackground = true;
        manifest = JsonUtility.FromJson<Manifest>(componentData.text);
        var nodes = tank.GetComponentsInChildren<Transform>(true);
        parts = manifest.parts.Select(p => nodes.Single(t => t.name == p.name)).ToArray();
        anchors = manifest.parts.Select(p => nodes.Single(t => t.name == p.anchor)).ToArray();
        ResolveBasis(tank, out axisX, out axisY, out axisZ);
        partRenderers = parts.Select(p => p.GetComponentsInChildren<Renderer>()).ToArray();
        partBounds = partRenderers.Select(group =>
        {
            var bounds = group[0].bounds;
            foreach (var renderer in group) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }).ToArray();
        homes = parts.Select(p => p.position).ToArray();
        BuildUI();
        SetProgress(0);
        UpdateCamera(true);
        TankViewerVerification.Install(this);
    }

    public static void ResolveBasis(Transform root, out Vector3 x, out Vector3 y, out Vector3 z)
    {
        var nodes = root.GetComponentsInChildren<Transform>(true);
        Vector3 origin = nodes.Single(t => t.name == "TankMotionOrigin").position;
        x = nodes.Single(t => t.name == "TankMotionX").position - origin;
        y = nodes.Single(t => t.name == "TankMotionY").position - origin;
        z = nodes.Single(t => t.name == "TankMotionZ").position - origin;
    }

    void Update()
    {
        bool overUi = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
        if (Input.GetMouseButtonDown(0) && !overUi)
        {
            dragging = true;
            moved = false;
            pointerDown = mouseStart = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(0))
        {
            if (dragging && !moved && progress > .999f)
                SelectPart(Input.mousePosition);
            dragging = false;
        }
        if (dragging && Input.GetMouseButton(0))
        {
            Vector2 delta = (Vector2)Input.mousePosition - mouseStart;
            moved |= ((Vector2)Input.mousePosition - pointerDown).sqrMagnitude > 16;
            yaw += delta.x * .18f; pitch = Mathf.Clamp(pitch - delta.y * .12f, -10, 75);
            mouseStart = Input.mousePosition;
        }
        if (!overUi) zoom = Mathf.Clamp(zoom * Mathf.Exp(-Input.mouseScrollDelta.y * .09f), .58f, 1.8f);
        if (pending && TankClarity.renderWidth > 0)
        {
            pending = false;
            animating = true;
        }
        if (animating)
        {
            SetProgress(Mathf.MoveTowards(progress, target, Time.unscaledDeltaTime / manifest.durationSeconds));
            if (progress == target) animating = false;
        }
        UpdateCamera(false);
        UpdateTags();
    }

    void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
        for (int i = 0; i < parts.Length; i++) parts[i].position = homes[i] + Offset(i, progress);
        string state = progress < .001f ? "整机装配" : progress > .999f ? "完全拆解" :
            target > progress ? "拆解中" : "组装中";
        if (status.text != state) status.text = state;
        if (progress < .999f) introPanel.gameObject.SetActive(false);
    }
    Vector3 Offset(int index, float value)
    {
        var keys = manifest.parts[index].path;
        for (int k = 1; k < keys.Length; k++)
        {
            if (value > keys[k].time) continue;
            float t = Mathf.Clamp01((value - keys[k - 1].time) / (keys[k].time - keys[k - 1].time));
            t = t * t * t * (10 + t * (-15 + 6 * t));
            Vector3 a = keys[k - 1].offset, b = keys[k].offset;
            return ToWorld(Vector3.LerpUnclamped(a, b, t));
        }
        Vector3 end = keys[keys.Length - 1].offset;
        return ToWorld(end);
    }
    Vector3 ToWorld(Vector3 value) => axisX * value.x + axisY * value.y + axisZ * value.z;

    void PlayTo(float destination)
    {
        target = destination;
        pending = TankClarity.renderWidth == 0 && target != progress;
        animating = !pending && target != progress;
        ClearSelection();
    }
    public void Disassemble() => PlayTo(1);
    public void Assemble() => PlayTo(0);
    public void ToggleIntro()
    {
        introVisible = !introVisible;
        introPanel.gameObject.SetActive(introVisible);
        if (introVisible) intro.text = "部件简介\n\n拆解完成后，点击模型或部件标注。";
        else ClearSelection();
    }

    internal bool SelectPart(Vector2 point)
    {
        Ray ray = viewCamera.ScreenPointToRay(point);
        // The body is hollow and concave; use the actual triangle surfaces.
        Physics.SyncTransforms();
        if (!Physics.Raycast(ray, out var hit, 200)) return false;
        for (int i = 0; i < parts.Length; i++)
        {
            if (!hit.transform.IsChildOf(parts[i]) && hit.transform != parts[i]) continue;
            ShowPart(i);
            return true;
        }
        return false;
    }

    int selected = -1;
    MaterialPropertyBlock selection;

    void ShowPart(int index)
    {
        ClearSelection();
        selected = index;
        selection ??= new MaterialPropertyBlock();
        selection.SetVector("_InspectionTint", new Vector4(.22f, .15f, .035f, .46f));
        foreach (var renderer in partRenderers[index]) renderer.SetPropertyBlock(selection);
        intro.text = manifest.parts[index].label + "\n\n" + manifest.parts[index].description;
        introVisible = true;
        introPanel.gameObject.SetActive(true);
    }

    void ClearSelection()
    {
        if (selected >= 0)
            foreach (var renderer in partRenderers[selected]) renderer.SetPropertyBlock(null);
        selected = -1;
        introVisible = false;
        if (introPanel) introPanel.gameObject.SetActive(false);
    }

    void UpdateCamera(bool instant)
    {
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0);
        var bounds = partBounds[0];
        bounds.center += parts[0].position - homes[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var movedBounds = partBounds[i];
            movedBounds.center += parts[i].position - homes[i];
            bounds.Encapsulate(movedBounds);
        }
        Vector3 center = bounds.center;
        float header = 92 * canvas.scaleFactor / Screen.height;
        float footer = 82 * canvas.scaleFactor / Screen.height;
        viewCamera.rect = new Rect(0, footer, 1, 1 - header - footer);
        float aspect = Mathf.Max(.5f, viewCamera.aspect);
        var inverse = Quaternion.Inverse(orbit);
        float size = .1f;
        for (int i = 0; i < parts.Length; i++)
        {
            Vector3 position = partBounds[i].center + parts[i].position - homes[i] - center;
            for (int corner = 0; corner < 8; corner++)
            {
                var p = inverse * (position + Vector3.Scale(partBounds[i].extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1)));
                size = Mathf.Max(size, Mathf.Max(Mathf.Abs(p.y) / .91f, Mathf.Abs(p.x) / (.90f * aspect)));
            }
        }
        viewCamera.orthographic = true;
        float blend = instant ? 1 : 1 - Mathf.Exp(-Time.unscaledDeltaTime * 10);
        viewCamera.orthographicSize = Mathf.Lerp(viewCamera.orthographicSize, size * zoom, blend);
        center += reviewOffset;
        Vector3 cameraPosition = center + orbit * new Vector3(0, 0, -17);
        viewCamera.transform.SetPositionAndRotation(Vector3.Lerp(viewCamera.transform.position, cameraPosition, blend), orbit);
    }

    void UpdateTags()
    {
        bool visible = progress >= .999f && !animating;
        for (int i = 0; i < tags.Length; i++)
        {
            if (tags[i].transform.parent.gameObject.activeSelf != visible)
                tags[i].transform.parent.gameObject.SetActive(visible);
            if (!visible) continue;
            var point = viewCamera.WorldToScreenPoint(anchors[i].GetComponent<Renderer>().bounds.center);
            var rect = (RectTransform)tags[i].transform.parent;
            Vector2 size = rect.sizeDelta * canvas.scaleFactor;
            Vector2 center = new Vector2(Mathf.Clamp(point.x, size.x, Screen.width - size.x),
                Mathf.Clamp(point.y, viewCamera.pixelRect.yMin + size.y, viewCamera.pixelRect.yMax - size.y));
            var placed = new Rect(center - size * .5f, size);
            for (int previous = 0; previous < i; previous++)
            {
                if (!placed.Overlaps(tagRects[previous])) continue;
                placed.y = tagRects[previous].yMax + 3;
                previous = -1;
            }
            tagRects[i] = placed;
            rect.position = placed.center;
        }
    }

    internal float Progress => progress;
    internal bool IsAnimating => pending || animating;
    internal Transform PartAt(int index) => parts[index];
    internal int Selected => selected;
    internal int TagCount => tags.Count(tag => tag.transform.parent.gameObject.activeSelf);
    internal void ReviewView(float heading, float elevation, float scale, Vector3 offset)
    {
        yaw = heading; pitch = elevation; zoom = scale; reviewOffset = offset;
        UpdateCamera(true);
    }

    void BuildUI()
    {
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        canvas = new GameObject("Tank viewer UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        var paper = new Color(.956f,.964f,.963f);
        var ink = new Color(.13f,.18f,.19f);
        var header = Rect("Header", canvas.transform, Vector2.up, Vector2.one);
        header.offsetMin = new Vector2(0, -92);
        header.gameObject.AddComponent<Image>().color = paper;
        TextAt("TK / 01", header, Vector2.zero, Vector2.one, 14,
            TextAnchor.UpperLeft, new Vector2(30, 0), new Vector2(-30, -20)).color = new Color(.055f,.39f,.37f);
        TextAt("立式工艺储罐", header, Vector2.zero, Vector2.one, 26,
            TextAnchor.LowerLeft, new Vector2(30, 15), new Vector2(-30, 0)).color = ink;
        TextAt("筒径 3.00 m  /  高 5.86 m  /  12 总成", header, Vector2.zero, Vector2.one, 16,
            TextAnchor.MiddleRight, new Vector2(30, 0), new Vector2(-30, 0)).color = ink;
        var footer = Rect("Footer", canvas.transform, Vector2.zero, Vector2.right);
        footer.offsetMax = new Vector2(0, 82);
        footer.gameObject.AddComponent<Image>().color = paper;
        status = TextAt("整机装配", footer, Vector2.zero, Vector2.one, 16,
            TextAnchor.MiddleRight, new Vector2(30, 0), new Vector2(-30, 0));
        status.color = ink;
        ButtonAt(footer, "拆解", 30, 104, Disassemble, true);
        ButtonAt(footer, "组装", 144, 104, Assemble);
        ButtonAt(footer, "部件简介", 258, 130, ToggleIntro);
        tags = new Text[parts.Length];
        tagRects = new Rect[parts.Length];
        for (int i = 0; i < tags.Length; i++)
        {
            int index = i;
            var tag = Rect("Component tag " + i, canvas.transform, Vector2.zero, Vector2.zero);
            tag.sizeDelta = new Vector2(122, 24);
            var background = tag.gameObject.AddComponent<Image>();
            background.color = new Color(.045f,.07f,.075f,.92f);
            tag.gameObject.AddComponent<Button>().onClick.AddListener(() => ShowPart(index));
            tags[i] = TextAt((i+1).ToString("D2") + " " + manifest.parts[i].label, tag,
                Vector2.zero, Vector2.one, 12, TextAnchor.MiddleCenter);
            tag.gameObject.SetActive(false);
        }
        introPanel = Rect("Component information", canvas.transform, Vector2.one, Vector2.one);
        introPanel.offsetMin = new Vector2(-320,-262);
        introPanel.offsetMax = new Vector2(-24,-112);
        introPanel.gameObject.AddComponent<Image>().color = paper;
        intro = TextAt("", introPanel, Vector2.zero, Vector2.one, 16, TextAnchor.UpperLeft,
            new Vector2(18, 12), new Vector2(-18, -16));
        intro.color = ink;
        introPanel.gameObject.SetActive(false);
    }
    RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform)); var r = go.GetComponent<RectTransform>();
        r.SetParent(parent,false); r.anchorMin=min; r.anchorMax=max; r.offsetMin=Vector2.zero; r.offsetMax=Vector2.zero; return r;
    }
    Text TextAt(string value, Transform parent, Vector2 min, Vector2 max, int size,
        TextAnchor align=TextAnchor.MiddleLeft, Vector2? low=null, Vector2? high=null)
    {
        var r=Rect(value,parent,min,max); r.offsetMin=low??Vector2.zero; r.offsetMax=high??Vector2.zero;
        var t=r.gameObject.AddComponent<Text>(); t.font=uiFont; t.text=value; t.fontSize=size; t.color=new Color(.9f,.94f,.94f);
        t.raycastTarget = false;
        t.alignment=align; t.horizontalOverflow=HorizontalWrapMode.Wrap; t.verticalOverflow=VerticalWrapMode.Truncate; return t;
    }
    void ButtonAt(Transform parent, string label, float x, float width, UnityEngine.Events.UnityAction action, bool primary = false)
    {
        var r = Rect(label, parent, Vector2.zero, Vector2.zero);
        r.offsetMin = new Vector2(x, 20);
        r.offsetMax = new Vector2(x+width, 62);
        var image=r.gameObject.AddComponent<Image>(); image.color=Color.white;
        var button=r.gameObject.AddComponent<Button>(); button.onClick.AddListener(action);
        var colors = button.colors;
        colors.normalColor = primary ? new Color(.055f,.39f,.37f) : new Color(.897f,.921f,.918f);
        colors.highlightedColor = primary ? new Color(.07f,.48f,.45f) : new Color(.78f,.86f,.85f);
        colors.pressedColor = primary ? new Color(.04f,.31f,.29f) : new Color(.68f,.79f,.77f);
        colors.selectedColor = colors.normalColor;
        button.colors = colors;
        TextAt(label,r,Vector2.zero,Vector2.one,16,TextAnchor.MiddleCenter).color =
            primary ? Color.white : new Color(.13f,.18f,.19f);
    }
}
