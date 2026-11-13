using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class FanViewer : MonoBehaviour
{
    [Serializable] public class MotionKey { public float t; public Vector3 offset; }
    [Serializable] public class Transfer
    {
        public float fadeOutStart, hiddenAt, visibleFrom, fadeInEnd;
    }
    [Serializable] public class Component
    {
        public int index;
        public string name, label, anchor, description;
        public bool doorAttached;
        public Vector3 pivot;
        public string[] meshes;
        public MotionKey[] path;
        public Transfer transfer;
    }
    [Serializable] public sealed class HingeData
    {
        public float rotationStart, rotationEnd;
        public float x, y, z, angleDegrees;
        public string sourceAxis;
    }
    [Serializable] public class Manifest
    {
        public string model, units, axis, rotorNode, rotorLocalAxis;
        public float axisHeight, oneWaySeconds;
        public int meshCount, motionVersion;
        public HingeData hinge;
        public Component[] parts;
    }

    public Transform fan;
    public Camera viewCamera;
    public Font uiFont;
    public TextAsset componentData;
    public Shader selectionMaskShader, selectionOutlineShader;
    public bool IsAnimating => playing;
    public bool IsRenderReady => renderReady;
    public bool IsMotionPending => motionPending;
    public float RenderReadySeconds { get; private set; }
    public bool IsAutomatedReview { get; private set; }
    public float Progress => progress;
    public Shader SelectionMaskShader => selectionMaskShader;
    public Shader SelectionOutlineShader => selectionOutlineShader;

    Manifest manifest;
    Transform[] parts;
    Vector3[] home;
    Renderer[] renderers;
    Bounds assemblyBounds;
    Bounds[] partBounds, projectedBounds;
    Vector3[][] homeCorners;
    Vector3[] offsets;
    Quaternion doorRotation = Quaternion.identity;
    Vector3 reviewCameraOffset;
    Vector3 motionX, motionY, motionZ, hingeWorld;
    Renderer[] harnessRenderers;
    Material[] harnessMaterials;
    float harnessVisibility = 1;
    int harnessIndex = -1;
    float motionHandedness;
    Quaternion[] homeRotation;
    float projectedYaw = float.NaN, projectedPitch = float.NaN;
    float progress, destination, yaw = 34, pitch = 17, zoom = 1;
    bool playing, dragging, introductionsVisible = true;
    bool renderReady, motionPending;
    int stableStartupFrames;
    float startupAt;
    Canvas canvas;
    Text stateText, descriptionText;
    RectTransform descriptionPanel;
    Button introductionButton;
    FanPartInspection inspection;

    void Awake()
    {
        IsAutomatedReview = Environment.GetCommandLineArgs().Contains("-fanReviewAutomation");
        ConfigureFrameRate();
        startupAt = Time.realtimeSinceStartup;
        Application.runInBackground = true;
        manifest = JsonUtility.FromJson<Manifest>(componentData.text);
        ResolveMotionBasis(fan, out motionX, out motionY, out motionZ);
        motionHandedness = Mathf.Sign(Vector3.Dot(Vector3.Cross(motionX, motionY), motionZ));
        parts = manifest.parts.Select(p => fan.GetComponentsInChildren<Transform>(true)
            .Single(t => t.name == p.name)).ToArray();
        home = parts.Select(p => p.position).ToArray();
        homeRotation = parts.Select(p => p.rotation).ToArray();
        hingeWorld = fan.TransformPoint(WorldOffset(new Vector3(
            manifest.hinge.x, manifest.hinge.y, manifest.hinge.z)));
        renderers = fan.GetComponentsInChildren<Renderer>();
        harnessIndex = Array.FindIndex(manifest.parts,
            part => part.transfer != null && part.transfer.fadeInEnd > 0);
        if (harnessIndex >= 0)
        {
            harnessRenderers = parts[harnessIndex].GetComponentsInChildren<Renderer>();
            harnessMaterials = harnessRenderers.SelectMany(renderer => renderer.sharedMaterials)
                .Distinct().ToArray();
        }
        assemblyBounds = BoundsOfFan();
        CacheCameraGeometry();
        BuildUI();
        inspection = GetComponent<FanPartInspection>();
        inspection.Initialize(this, fan, viewCamera, canvas, uiFont, manifest);
        SetProgress(0);
        UpdateCamera(true);
        FanViewerVerification.AttachIfRequested(this);
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused) ConfigureFrameRate();
    }

    static void ConfigureFrameRate()
    {
        float refresh = (float)Screen.currentResolution.refreshRateRatio.value;
        if (float.IsNaN(refresh) || float.IsInfinity(refresh) || refresh < 30) refresh = 60;
        // A refresh divisor gives 120 Hz on 240 Hz displays and 144 Hz on 144 Hz.
        // Keep an explicit cap: windowed presentation can ignore driver VSync.
        int divisor = Mathf.Max(1, Mathf.CeilToInt(refresh / 144));
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = Mathf.Clamp(Mathf.RoundToInt(refresh / divisor), 30, 144);
    }

    void Update()
    {
        if (!renderReady)
        {
            // Submit the full-quality scene before moving parts. Initial GPU/resource
            // setup must not consume the beginning of the disassembly timeline.
            float frameBudget = Mathf.Max(1f / 45f, 1.5f / Application.targetFrameRate);
            stableStartupFrames = FanClarity.renderWidth > 0 &&
                Time.unscaledDeltaTime <= frameBudget ? stableStartupFrames + 1 : 0;
            if (stableStartupFrames >= 6 || Time.realtimeSinceStartup - startupAt >= 3)
            {
                renderReady = true;
                RenderReadySeconds = Time.realtimeSinceStartup - startupAt;
                if (motionPending)
                {
                    motionPending = false;
                    playing = true;
                }
            }
        }
        if (!IsAutomatedReview)
        {
            bool overUI = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) dragging = !overUI;
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1)) dragging = false;
            if (dragging)
            {
                yaw += Input.GetAxis("Mouse X") * 3.2f;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2.4f, -8, 78);
            }
            if (!overUI) zoom = Mathf.Clamp(zoom * Mathf.Exp(-Input.mouseScrollDelta.y * .1f), .42f, 2.1f);
            if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
        }
        if (playing)
        {
            float duration = Mathf.Max(.1f, manifest.oneWaySeconds);
            SetProgress(Mathf.MoveTowards(progress, destination, Time.unscaledDeltaTime / duration));
            if (Mathf.Abs(progress - destination) < .00001f) playing = false;
        }
        UpdateCamera(false);
        inspection.Tick(playing, progress, introductionsVisible);
    }

    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
        float rotationEnd = manifest.hinge.rotationEnd > 0 ? manifest.hinge.rotationEnd : .25f;
        float doorProgress = Mathf.SmoothStep(0, 1,
            Mathf.InverseLerp(manifest.hinge.rotationStart, rotationEnd, progress));
        doorRotation = Quaternion.AngleAxis(
            manifest.hinge.angleDegrees * motionHandedness * doorProgress, motionZ);
        for (int i = 0; i < parts.Length; i++)
        {
            Component part = manifest.parts[i];
            Quaternion hingeRotation = part.doorAttached ? doorRotation : Quaternion.identity;
            offsets[i] = WorldOffset(PathOffset(part.path, progress));
            Vector3 position = hingeWorld + hingeRotation * (home[i] - hingeWorld) + offsets[i];
            if (parts[i].position != position) parts[i].position = position;
            Quaternion rotation = hingeRotation * homeRotation[i];
            if (parts[i].rotation != rotation) parts[i].rotation = rotation;
        }
        if (progress < .9999f && inspection.SelectedPart >= 0) inspection.ClearSelection();
        Transfer transfer = harnessIndex < 0 ? null : manifest.parts[harnessIndex].transfer;
        bool transferring = transfer != null && progress > transfer.fadeOutStart &&
            progress < transfer.fadeInEnd;
        if (transfer != null)
        {
            float visibility = progress <= transfer.hiddenAt
                ? 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(transfer.fadeOutStart, transfer.hiddenAt, progress))
                : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(transfer.visibleFrom, transfer.fadeInEnd, progress));
            if (visibility != harnessVisibility)
            {
                bool visible = visibility > 0;
                if (visible != (harnessVisibility > 0))
                    foreach (Renderer renderer in harnessRenderers) renderer.enabled = visible;
                foreach (Material material in harnessMaterials)
                {
                    material.SetFloat("_CabinetVisibility", visibility);
                    if ((visibility >= 1) != (harnessVisibility >= 1))
                    {
                        material.SetShaderPassEnabled("ShadowCaster", visibility >= 1);
                        material.SetShaderPassEnabled("DepthOnly", visibility >= 1);
                        material.SetShaderPassEnabled("DepthNormals", visibility >= 1);
                    }
                }
                harnessVisibility = visibility;
            }
        }
        string state = transferring ? (destination > 0 ? "断开线束" : "接回线束") :
            progress < .001f ? "整机装配" :
            progress > .999f ? "完全拆解" : "拆装进程";
        if (stateText.text != state) stateText.text = state;
    }

    static Vector3 PathOffset(MotionKey[] path, float value)
    {
        for (int i = 1; i < path.Length; i++)
        {
            if (value > path[i].t) continue;
            float t = Mathf.Clamp01((value - path[i - 1].t) / (path[i].t - path[i - 1].t));
            t = t * t * (3 - 2 * t);
            return Vector3.LerpUnclamped(path[i - 1].offset, path[i].offset, t);
        }
        return path[path.Length - 1].offset;
    }

    Vector3 WorldOffset(Vector3 value) =>
        motionX * value.x + motionY * value.y + motionZ * value.z;

    public static void ResolveMotionBasis(Transform root, out Vector3 x,
        out Vector3 y, out Vector3 z)
    {
        Transform[] nodes = root.GetComponentsInChildren<Transform>(true);
        Vector3 origin = nodes.Single(node => node.name == "CabinetMotionOrigin").position;
        x = nodes.Single(node => node.name == "CabinetMotionX").position - origin;
        y = nodes.Single(node => node.name == "CabinetMotionY").position - origin;
        z = nodes.Single(node => node.name == "CabinetMotionZ").position - origin;
        if (Mathf.Abs(x.magnitude - 1) > .001f ||
            Mathf.Abs(y.magnitude - 1) > .001f || Mathf.Abs(z.magnitude - 1) > .001f ||
            Mathf.Abs(Vector3.Dot(x, y)) > .001f || Mathf.Abs(Vector3.Dot(x, z)) > .001f ||
            Mathf.Abs(Vector3.Dot(y, z)) > .001f)
            throw new InvalidOperationException("Invalid imported fan motion coordinate references.");
        Debug.Log("CABINET_MOTION_BASIS X=" + x.ToString("F6") +
            " Y=" + y.ToString("F6") + " Z=" + z.ToString("F6"));
    }

    void AnimateTo(float value)
    {
        destination = value;
        motionPending = !renderReady;
        playing = renderReady;
        if (motionPending) stateText.text = "准备中";
        inspection.ClearSelection();
    }

    Bounds BoundsOfFan()
    {
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers) bounds.Encapsulate(renderer.bounds);
        return bounds;
    }

    void CacheCameraGeometry()
    {
        partBounds = new Bounds[parts.Length];
        projectedBounds = new Bounds[parts.Length];
        homeCorners = new Vector3[parts.Length][];
        offsets = new Vector3[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            Renderer[] owned = parts[i].GetComponentsInChildren<Renderer>();
            var corners = new Vector3[owned.Length * 8];
            Bounds total = owned[0].bounds;
            int count = 0;
            foreach (Renderer renderer in owned)
            {
                Bounds bounds = renderer.bounds;
                total.Encapsulate(bounds);
                for (int corner = 0; corner < 8; corner++)
                    corners[count++] = bounds.center + Vector3.Scale(bounds.extents,
                        new Vector3((corner & 1) == 0 ? -1 : 1,
                            (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
            }
            partBounds[i] = total;
            homeCorners[i] = corners;
        }
    }

    void UpdateCamera(bool instant)
    {
        Bounds bounds = partBounds[0];
        bounds.center += offsets[0];
        for (int i = 1; i < parts.Length; i++)
        {
            if (i == harnessIndex && harnessVisibility <= 0) continue;
            Bounds moved = partBounds[i];
            if (manifest.parts[i].doorAttached && progress > 0)
                moved = RotatedDoorBounds(i, Quaternion.identity);
            else
                moved.center += offsets[i];
            bounds.Encapsulate(moved);
        }
        Vector3 center = Vector3.Lerp(assemblyBounds.center, bounds.center, .85f);
        float header = 90 * canvas.scaleFactor / Screen.height;
        float footer = 82 * canvas.scaleFactor / Screen.height;
        Rect viewport = new Rect(0, footer, 1, 1 - footer - header);
        if (viewCamera.rect != viewport) viewCamera.rect = viewport;
        float aspect = Mathf.Max(.5f, viewCamera.aspect);
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0);
        Quaternion inverse = Quaternion.Inverse(orbit);
        // Cache the assembled projection for parts that only translate.
        if (yaw != projectedYaw || pitch != projectedPitch)
        {
            for (int i = 0; i < parts.Length; i++)
            {
                Bounds projected = new Bounds(inverse * homeCorners[i][0], Vector3.zero);
                for (int j = 1; j < homeCorners[i].Length; j++)
                    projected.Encapsulate(inverse * homeCorners[i][j]);
                projectedBounds[i] = projected;
            }
            projectedYaw = yaw;
            projectedPitch = pitch;
        }
        float halfHeight = .1f;
        for (int i = 0; i < parts.Length; i++)
        {
            if (i == harnessIndex && harnessVisibility <= 0) continue;
            Bounds projected = projectedBounds[i];
            if (manifest.parts[i].doorAttached && progress > 0)
                projected = RotatedDoorBounds(i, inverse);
            else
                projected.center += inverse * offsets[i];
            Vector3 projectedCenter = projected.center - inverse * center;
            Vector3 extents = projected.extents;
            halfHeight = Mathf.Max(halfHeight, (Mathf.Abs(projectedCenter.y) + extents.y) / .90f);
            halfHeight = Mathf.Max(halfHeight, (Mathf.Abs(projectedCenter.x) + extents.x) / (aspect * .88f));
        }
        float size = halfHeight * zoom;
        viewCamera.orthographicSize = instant ? size :
            Mathf.Lerp(viewCamera.orthographicSize, size, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 10));
        center += reviewCameraOffset;
        Vector3 desired = center + orbit * new Vector3(0, 0, -7);
        if (instant) viewCamera.transform.position = desired;
        else viewCamera.transform.position = Vector3.Lerp(viewCamera.transform.position, desired,
            1 - Mathf.Exp(-Time.unscaledDeltaTime * 10));
        viewCamera.transform.LookAt(center);
    }

    Bounds RotatedDoorBounds(int index, Quaternion projection)
    {
        Bounds homeBounds = partBounds[index];
        Quaternion rotation = projection * doorRotation;
        Vector3 center = projection * (hingeWorld + doorRotation * (homeBounds.center - hingeWorld) + offsets[index]);
        Vector3 x = rotation * (Vector3.right * homeBounds.extents.x);
        Vector3 y = rotation * (Vector3.up * homeBounds.extents.y);
        Vector3 z = rotation * (Vector3.forward * homeBounds.extents.z);
        Vector3 extents = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
            Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y),
            Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
        return new Bounds(center, extents * 2);
    }

    internal void SetReviewView(float yawDegrees, float pitchDegrees, float zoomFactor, float raiseTarget = 0)
    {
        yaw = yawDegrees;
        pitch = pitchDegrees;
        zoom = zoomFactor;
        reviewCameraOffset = Vector3.up * raiseTarget;
        UpdateCamera(true);
    }

    RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    Text MakeText(string value, Transform parent, Vector2 min, Vector2 max,
        Vector2 offsetMin, Vector2 offsetMax, int size, Color color, TextAnchor align)
    {
        Text text = Rect(value, parent, min, max, offsetMin, offsetMax).gameObject.AddComponent<Text>();
        text.font = uiFont;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    Button MakeButton(string title, Transform parent, float x, float width, Action action,
        bool primary = false)
    {
        RectTransform rect = Rect(title, parent, Vector2.zero, Vector2.zero,
            new Vector2(x, 20), new Vector2(x + width, 62));
        Image image = rect.gameObject.AddComponent<Image>();
        Color ink = new Color(.16f, .19f, .20f);
        Color accent = new Color(.04f, .40f, .36f);
        image.color = primary ? accent : new Color(.91f, .93f, .93f);
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(.82f, .90f, .88f);
        colors.pressedColor = new Color(.68f, .79f, .76f);
        button.colors = colors;
        button.onClick.AddListener(() => action());
        MakeText(title, rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
            17, primary ? Color.white : ink, TextAnchor.MiddleCenter);
        return button;
    }

    void BuildUI()
    {
        GameObject root = new GameObject("Electrical cabinet display",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = .5f;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        RectTransform header = Rect("Header", root.transform, new Vector2(0, 1), Vector2.one,
            new Vector2(0, -90), Vector2.zero);
        header.gameObject.AddComponent<Image>().color = new Color(.965f, .97f, .97f, .98f);
        MakeText("PLC-01", header, Vector2.zero, Vector2.one,
            new Vector2(28, 42), new Vector2(-28, -8), 14, new Color(.05f, .40f, .37f),
            TextAnchor.MiddleLeft);
        MakeText("电气控制柜", header, Vector2.zero, Vector2.one,
            new Vector2(28, 8), new Vector2(-300, -34), 27, new Color(.16f, .19f, .20f),
            TextAnchor.MiddleLeft);
        MakeText("1.21 × 0.66 × 2.00 m   /   " + manifest.parts.Length + " 个总成",
            header, Vector2.zero, Vector2.one, new Vector2(330, 10), new Vector2(-28, -22),
            16, new Color(.36f, .4f, .41f), TextAnchor.MiddleRight);

        RectTransform footer = Rect("Assembly controls", root.transform, Vector2.zero,
            new Vector2(1, 0), Vector2.zero, new Vector2(0, 82));
        footer.gameObject.AddComponent<Image>().color = new Color(.97f, .975f, .975f, .98f);
        MakeButton("拆解", footer, 30, 104, () => AnimateTo(1), true);
        MakeButton("组装", footer, 144, 104, () => AnimateTo(0));
        introductionButton = MakeButton("部件简介", footer, 258, 130,
            () =>
            {
                introductionsVisible = !introductionsVisible;
                UpdateIntroductionButton();
            });
        UpdateIntroductionButton();
        stateText = MakeText("整机装配", footer, new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-280, 25), new Vector2(-30, 60), 16,
            new Color(.16f, .19f, .20f), TextAnchor.MiddleRight);
        descriptionPanel = Rect("Part introduction", root.transform, Vector2.one, Vector2.one,
            new Vector2(-360, -278), new Vector2(-24, -104));
        descriptionPanel.gameObject.AddComponent<Image>().color = new Color(.965f, .975f, .975f, .97f);
        descriptionText = MakeText("", descriptionPanel, Vector2.zero, Vector2.one,
            new Vector2(18, 14), new Vector2(-18, -14), 16,
            new Color(.16f, .19f, .20f), TextAnchor.UpperLeft);
        descriptionPanel.gameObject.SetActive(false);
    }

    public void ShowDescription(string text)
    {
        bool visible = introductionsVisible && !string.IsNullOrEmpty(text);
        if (descriptionPanel.gameObject.activeSelf != visible)
            descriptionPanel.gameObject.SetActive(visible);
        if (descriptionText.text != text) descriptionText.text = text;
    }

    public void EnableDescriptions()
    {
        introductionsVisible = true;
        UpdateIntroductionButton();
    }

    void UpdateIntroductionButton()
    {
        introductionButton.image.color = introductionsVisible ? new Color(.80f, .88f, .86f) :
            new Color(.91f, .94f, .93f);
    }
}
