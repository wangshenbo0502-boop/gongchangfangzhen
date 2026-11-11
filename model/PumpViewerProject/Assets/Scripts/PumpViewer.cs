using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed partial class PumpViewer : MonoBehaviour
{
    public Transform pump;
    public Camera viewCamera;
    public Font uiFont;
    public TextAsset componentData;

    [Serializable] public class Component
    {
        public string name, label;
        public string anchor;
        public string[] meshes;
        public float start;
        public Vector3 offset;
        public MotionKey[] path;
    }
    [Serializable] public class MotionKey { public float time; public Vector3 offset; }
    [Serializable] public class ComponentManifest
    {
        public int meshCount, motionVersion;
        public float durationSeconds;
        public Component[] parts;
    }
    ComponentManifest manifest;
    Transform[] parts;
    Vector3[] home;
    Vector3 axis, side;
    float progress, destination, yaw, baseYaw, pitch = 19, zoom = 1;
    bool playing, loop, rotating, dragging, detail, jointReview;
    Slider scrubber;
    Text stateText, progressText, pauseText;
    Canvas canvas;
    Renderer[] pumpRenderers;
    Renderer[] internalRenderers;
    Bounds assemblyBounds;
    readonly List<string> errors = new List<string>();
    string verifyDirectory;
    readonly Color ink = new Color(.16f, .19f, .20f);
    readonly Color accent = new Color(.05f, .40f, .37f);

    void Awake()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        Application.runInBackground = true;
        string[] startupArgs = Environment.GetCommandLineArgs();
        if (!Application.isEditor && !startupArgs.Contains("-screen-width") &&
            !startupArgs.Contains("-screen-height"))
        {
            Resolution desktop = Screen.currentResolution;
            int height = Mathf.Min(1260,Mathf.Min(desktop.height-180,(desktop.width-100)*9/16));
            if (height >= 720)
                Screen.SetResolution(height*16/9,height,FullScreenMode.Windowed);
        }
        Application.logMessageReceived += OnLog;
        manifest = JsonUtility.FromJson<ComponentManifest>(componentData.text);
        parts = manifest.parts.Select(p => pump.GetComponentsInChildren<Transform>(true).Single(t => t.name == p.name)).ToArray();
        pumpRenderers = pump.GetComponentsInChildren<Renderer>();
        internalRenderers = parts.Where(p => p.name.Contains("Impeller") || p.name.Contains("Bearing") ||
            p.name.Contains("Seal")).SelectMany(p => p.GetComponentsInChildren<Renderer>()).ToArray();
        home = parts.Select(p => p.position).ToArray();
        axis = Vector3.ProjectOnPlane(home[1] - home[4], Vector3.up).normalized;
        Renderer plate = pump.GetComponentsInChildren<Renderer>().Single(r => r.name == "PMP_01_Motor_Nameplate");
        side = Vector3.ProjectOnPlane(plate.bounds.center-home[1], Vector3.up);
        side = (side-axis*Vector3.Dot(side,axis)).normalized;
        Vector3 approach = (-axis*3.5f+side*4.4f).normalized;
        baseYaw = yaw = Mathf.Atan2(-approach.x,-approach.z)*Mathf.Rad2Deg;
        assemblyBounds = BoundsOfPump();
        BuildUI();
        SetProgress(0);
        UpdateCamera(true);
        string[] args = Environment.GetCommandLineArgs();
        int at = Array.IndexOf(args, "-verifyPump");
        if (at >= 0 && at + 1 < args.Length)
        {
            verifyDirectory = args[at + 1];
            canvas.GetComponent<GraphicRaycaster>().enabled = false;
            StartCoroutine(Verify());
        }
    }

    void OnDestroy() { Application.logMessageReceived -= OnLog; }
    void OnLog(string condition, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            errors.Add(condition);
    }

    Bounds BoundsOfPump()
    {
        Renderer[] renderers = pump.GetComponentsInChildren<Renderer>();
        Bounds result = renderers[0].bounds;
        foreach (Renderer renderer in renderers) result.Encapsulate(renderer.bounds);
        return result;
    }

    void Update()
    {
        if (verifyDirectory == null)
        {
            bool overUI = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)) dragging = !overUI;
            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1)) dragging = false;
            if (dragging)
            {
                yaw += Input.GetAxis("Mouse X") * 3.3f;
                pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * 2.5f, -8, 78);
            }
            if (!overUI) zoom = Mathf.Clamp(zoom * Mathf.Exp(-Input.mouseScrollDelta.y*.10f), .42f, 2.1f);
            if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
            if (Input.GetKeyDown(KeyCode.Escape)) Application.Quit();
        }
        if (rotating && !dragging) yaw += Time.unscaledDeltaTime*12;
        if (playing)
        {
            SetProgress(Mathf.MoveTowards(progress, destination, Time.unscaledDeltaTime / manifest.durationSeconds));
            if (Mathf.Abs(progress-destination) < .00001f)
            {
                if (loop) destination = 1-destination;
                else playing = false;
            }
        }
        UpdateCamera(false);
        UpdateLabels();
        pauseText.text = playing ? "\u6682\u505c" : "\u7ee7\u7eed";
    }

    public void SetProgress(float value)
    {
        progress = Mathf.Clamp01(value);
        for (int i = 0; i < parts.Length; i++)
        {
            parts[i].position = home[i] + WorldOffset(MotionOffset(i, progress));
        }
        if (scrubber) scrubber.SetValueWithoutNotify(progress);
        if (progressText) progressText.text = Mathf.RoundToInt(progress*100) + "%";
        if (stateText)
            stateText.text = progress < .001f ? "\u6574\u673a\u88c5\u914d" :
                progress > .999f ? "\u5b8c\u5168\u62c6\u89e3" : "\u62c6\u88c5\u8fdb\u7a0b";
    }

    Vector3 WorldOffset(Vector3 offset) { return axis*offset.x-side*offset.y+Vector3.up*offset.z; }
    Vector3 MotionOffset(int index, float value)
    {
        MotionKey[] path = manifest.parts[index].path;
        for (int key = 1; key < path.Length; key++)
        {
            if (value > path[key].time) continue;
            float t = Mathf.Clamp01((value-path[key-1].time)/(path[key].time-path[key-1].time));
            t = t*t*t*(10+t*(-15+6*t));
            return Vector3.LerpUnclamped(path[key-1].offset,path[key].offset,t);
        }
        return path[path.Length-1].offset;
    }

    void AnimateTo(float value) { destination = value; playing = true; loop = false; }
    void TogglePause()
    {
        if (!playing && Mathf.Abs(progress-destination) < .001f) destination = progress > .5f ? 0 : 1;
        playing = !playing;
    }
    void ResetView() { yaw = baseYaw; pitch = 19; zoom = 1; detail = false; }

    Bounds ComponentBounds(Transform part)
    {
        Renderer[] rs = part.GetComponentsInChildren<Renderer>();
        Bounds b = rs[0].bounds;
        foreach (Renderer r in rs) b.Encapsulate(r.bounds);
        return b;
    }

    Bounds InternalBounds()
    {
        Bounds b = ComponentBounds(parts.Single(p => p.name == "PMP_07_Impeller"));
        foreach (Transform part in parts.Where(p =>
            p.name.Contains("FrontShroud") || p.name.Contains("BearingHousing") ||
            p.name.Contains("BearingSet") ||
            p.name.Contains("StaticSeal") || p.name.Contains("RotatingSeal")))
            b.Encapsulate(ComponentBounds(part));
        return b;
    }

    void UpdateCamera(bool instant)
    {
        // The world-space model remains stationary while the camera orbits.
        Bounds bounds = detail ? InternalBounds() : BoundsOfPump();
        Vector3 center = jointReview ? home[4]-axis*.008f-side*.23f+Vector3.up*.16f :
            detail ? bounds.center : Vector3.Lerp(assemblyBounds.center, bounds.center, .85f);
        float bottom = 128*canvas.scaleFactor/Screen.height;
        float top = 94*canvas.scaleFactor/Screen.height;
        viewCamera.rect = new Rect(0,bottom,1,1-bottom-top);
        float aspect = Mathf.Max(.5f,viewCamera.aspect);
        Quaternion orbit = Quaternion.Euler(pitch, yaw, 0);
        Quaternion inverse = Quaternion.Inverse(orbit);
        float halfHeight = .1f;
        foreach (Renderer renderer in detail ? internalRenderers : pumpRenderers)
        foreach (int sx in new[] {-1,1}) foreach (int sy in new[] {-1,1}) foreach (int sz in new[] {-1,1})
        {
            Vector3 point = inverse*(renderer.bounds.center+Vector3.Scale(renderer.bounds.extents,new Vector3(sx,sy,sz))-center);
            halfHeight = Mathf.Max(halfHeight,Mathf.Abs(point.y)/.91f);
            float horizontalFill = progress > .96f && !detail ? .78f : .90f;
            halfHeight = Mathf.Max(halfHeight,Mathf.Abs(point.x)/(aspect*horizontalFill));
        }
        float size = jointReview ? .40f : halfHeight*zoom;
        viewCamera.orthographicSize = instant ? size :
            Mathf.Lerp(viewCamera.orthographicSize,size,1-Mathf.Exp(-Time.unscaledDeltaTime*10));
        Vector3 desired = center + orbit * new Vector3(0, 0, -7);
        if (instant) viewCamera.transform.position = desired;
        else viewCamera.transform.position = Vector3.Lerp(viewCamera.transform.position, desired, 1-Mathf.Exp(-Time.unscaledDeltaTime*10));
        viewCamera.transform.LookAt(center);
    }

    RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = min; rect.anchorMax = max;
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        return rect;
    }
    Text TextAt(string value, Transform parent, Vector2 min, Vector2 max,
        Vector2 lo, Vector2 hi, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        RectTransform rect = Rect(value, parent, min, max, lo, hi);
        Text text = rect.gameObject.AddComponent<Text>();
        text.font = uiFont; text.text = value; text.fontSize = size;
        text.color = color; text.alignment = alignment; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }
    Image Panel(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }
    Button ButtonAt(string text, Transform parent, float x, float width, UnityEngine.Events.UnityAction action, bool primary = false)
    {
        RectTransform rect = Rect(text, parent, new Vector2(0, 0), new Vector2(0, 0),
            new Vector2(x, 20), new Vector2(x+width, 62));
        Image image = Panel(rect, primary ? accent : new Color(.91f, .93f, .93f));
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(.82f, .9f, .88f);
        colors.pressedColor = new Color(.68f, .79f, .76f);
        button.colors = colors;
        button.onClick.AddListener(action);
        TextAt(text, rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 17,
            primary ? Color.white : ink, TextAnchor.MiddleCenter);
        return button;
    }

    void BuildUI()
    {
        GameObject c = new GameObject("Pump display", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = c.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = c.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1600, 900);
        scaler.matchWidthOrHeight = .5f;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        RectTransform header = Rect("Header", c.transform, new Vector2(0,1), Vector2.one,
            new Vector2(0,-94), Vector2.zero);
        Panel(header, new Color(.965f,.97f,.97f,.98f));
        TextAt("P / 01", header, Vector2.zero, Vector2.one, new Vector2(30,40), new Vector2(-30,-8), 14, accent);
        TextAt("\u79bb\u5fc3\u6c34\u6cf5", header, Vector2.zero, Vector2.one,
            new Vector2(30,10), new Vector2(-300,-34), 27, ink);
        TextAt("1.90 \u00d7 1.00 \u00d7 1.35 m   /   "+parts.Length+" \u90e8\u4ef6", header, Vector2.zero, Vector2.one,
            new Vector2(350,12), new Vector2(-30,-20), 16, new Color(.36f,.4f,.41f), TextAnchor.MiddleRight);
        RectTransform footer = Rect("Assembly controls", c.transform, Vector2.zero, new Vector2(1,0),
            Vector2.zero, new Vector2(0,128));
        Panel(footer, new Color(.97f,.975f,.975f,.98f));
        ButtonAt("\u62c6\u89e3", footer, 30, 104, () => AnimateTo(1), true);
        ButtonAt("\u7ec4\u88c5", footer, 144, 104, () => AnimateTo(0));
        Button pause = ButtonAt("\u7ee7\u7eed", footer, 258, 90, TogglePause);
        pauseText = pause.GetComponentInChildren<Text>();
        Button loopButton = ButtonAt("\u5faa\u73af", footer, 358, 90, () => { loop = !loop; playing = true; destination = progress > .999f ? 0 : 1; });
        Button rotateButton = ButtonAt("\u81ea\u8f6c", footer, 458, 90, () => rotating = !rotating);
        ButtonAt("\u590d\u4f4d\u89c6\u89d2", footer, 558, 130, ResetView);
        ButtonAt("\u5185\u90e8\u8fd1\u770b", footer, 698, 130, () =>
        {
            detail = !detail; zoom = 1;
            if (detail) { yaw = baseYaw; pitch = 24; AnimateTo(1); }
        });
        TextAt("\u88c5\u914d\u8fdb\u5ea6", footer, Vector2.zero, new Vector2(0,0),
            new Vector2(30,85), new Vector2(136,118), 15, ink);
        progressText = TextAt("0%", footer, new Vector2(1,0), new Vector2(1,0),
            new Vector2(-84,84), new Vector2(-28,118), 15, accent, TextAnchor.MiddleRight);
        stateText = TextAt("", footer, new Vector2(1,0), new Vector2(1,0),
            new Vector2(-280,25), new Vector2(-30,60), 16, ink, TextAnchor.MiddleRight);
        RectTransform slider = Rect("Progress", footer, Vector2.zero, new Vector2(1,0),
            new Vector2(150,87), new Vector2(-104,117));
        Panel(slider, new Color(0,0,0,0));
        scrubber = slider.gameObject.AddComponent<Slider>();
        RectTransform track = Rect("Track", slider, new Vector2(0,.5f), new Vector2(1,.5f),
            new Vector2(0,-2), new Vector2(0,2));
        Panel(track, new Color(.78f,.82f,.82f));
        RectTransform fillArea = Rect("Fill area", slider, new Vector2(0,.5f), new Vector2(1,.5f),
            new Vector2(0,-2), new Vector2(0,2));
        RectTransform fill = Rect("Fill", fillArea, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Panel(fill, accent);
        RectTransform handleArea = Rect("Handle area", slider, new Vector2(0,.5f), new Vector2(1,.5f),
            new Vector2(0,-9), new Vector2(0,9));
        RectTransform handle = Rect("Handle", handleArea, Vector2.zero, new Vector2(0,1),
            new Vector2(-7,0), new Vector2(7,0));
        scrubber.targetGraphic = Panel(handle, accent);
        scrubber.fillRect = fill; scrubber.handleRect = handle;
        scrubber.onValueChanged.AddListener(v => { playing = false; loop = false; SetProgress(v); });
        BuildLabels();
    }

    [Serializable] class Verification
    {
        public string utc;
        public int width, height, components, meshes, triangles;
        public Vector3 assembledSize;
        public float returnErrorM, explodedMotorTravelM, meanFrameMs, explodedProgress, assembledProgress;
        public float frontShroudRelativeTravelM, bearingHousingRelativeTravelM;
        public bool sourcePartsMatch, reflectionProbeReady;
        public float renderScale, paintLinearColorError, reflectionIntensity;
        public int msaaSamples, outputPixels, estimatedShadedPixels;
        public int clarityRenderWidth, clarityRenderHeight;
        public float viewportLitFraction;
        public string[] textureFormats;
        public int motionVersion, labelOverlapPairs, leaderCrossingPairs, labelViewSamples;
        public float maxLeaderPixels, intermediateReverseErrorM, motionReturnPathErrorM;
        public bool allLabelsInFrame, textureCompressionDisabled, dynamicResolutionDisabled;
        public bool hdr, orthographic, fullTextureResolution;
        public string[] partNames;
        public bool allShadersSupported, noPinkPixels, imageHasContrast, cameraMoved, allPartsInFrame, passed;
        public string[] errors;
    }

    IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        Texture2D image = ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(verifyDirectory, name+".png"), image.EncodeToPNG());
        Destroy(image);
    }

    IEnumerator Verify()
    {
        Directory.CreateDirectory(verifyDirectory);
        yield return new WaitForSecondsRealtime(.8f);
        yield return Capture("assembled");
        jointReview = true;
        yield return new WaitForSecondsRealtime(.8f);
        yield return Capture("outlet-joint-front");
        yaw = baseYaw-107;
        yield return new WaitForSecondsRealtime(.8f);
        yield return Capture("outlet-joint-back");
        jointReview = false;
        ResetView();
        yield return new WaitForSecondsRealtime(.8f);
        float elapsed = Time.realtimeSinceStartup;
        // Run the real command path over intermediate poses rather than teleporting to the endpoint.
        AnimateTo(1);
        float[] reviewPoses = {.08f,.24f,.36f,.43f,.53f,.62f,.74f,.80f,.86f,.91f,.955f};
        int nextPose = 0;
        while (playing)
        {
            if (nextPose < reviewPoses.Length && progress >= reviewPoses[nextPose])
            {
                yield return Capture("motion-"+Mathf.RoundToInt(reviewPoses[nextPose]*100).ToString("00"));
                nextPose++;
            }
            yield return null;
        }
        yield return new WaitForSecondsRealtime(.8f);
        yield return Capture("exploded");
        float maxLeader = 0;
        int overlaps = 0, crossings = 0;
        bool labelsInFrame = true;
        int labelViews = 8;
        for (int view = 0; view < labelViews; view++)
        {
            yaw = baseYaw+view*45;
            yield return new WaitForSecondsRealtime(.65f);
            maxLeader = Mathf.Max(maxLeader,MaxLeaderPixels());
            overlaps = Mathf.Max(overlaps,LabelOverlapCount());
            crossings = Mathf.Max(crossings,LeaderCrossingCount());
            labelsInFrame &= LabelsInFrame();
            if (view%2 == 0) yield return Capture("labels-"+(view*45).ToString("000"));
        }
        ResetView();
        yield return new WaitForSecondsRealtime(.7f);
        float explodedProgress = progress;
        float moved = Vector3.Distance(parts[1].position,home[1]);
        int shroudIndex = Array.FindIndex(parts,p => p.name.Contains("FrontShroud"));
        int housingIndex = Array.FindIndex(parts,p => p.name.Contains("BearingHousing"));
        int impellerIndex = Array.FindIndex(parts,p => p.name == "PMP_07_Impeller");
        int bearingIndex = Array.FindIndex(parts,p => p.name.Contains("BearingSet"));
        float shroudTravel = ((parts[shroudIndex].position-parts[impellerIndex].position)-
            (home[shroudIndex]-home[impellerIndex])).magnitude;
        float housingTravel = ((parts[housingIndex].position-parts[bearingIndex].position)-
            (home[housingIndex]-home[bearingIndex])).magnitude;
        Bounds bounds = BoundsOfPump();
        bool inFrame = true;
        foreach (Renderer r in pump.GetComponentsInChildren<Renderer>())
        {
            foreach (int sx in new[] {-1,1}) foreach (int sy in new[] {-1,1}) foreach (int sz in new[] {-1,1})
            {
                Vector3 world = r.bounds.center + Vector3.Scale(r.bounds.extents,new Vector3(sx,sy,sz));
                Vector3 p = viewCamera.WorldToViewportPoint(world);
                inFrame &= p.z > 0 && p.x > .015f && p.x < .985f && p.y > .015f && p.y < .985f;
            }
        }
        detail = true;
        yield return new WaitForSecondsRealtime(.8f);
        yield return Capture("internal-detail");
        detail = false;
        AnimateTo(0);
        float pathError = 0;
        while (playing)
        {
            for (int i = 0; i < parts.Length; i++)
                pathError = Mathf.Max(pathError,Vector3.Distance(parts[i].position,
                    home[i]+WorldOffset(MotionOffset(i,progress))));
            yield return null;
        }
        yield return new WaitForSecondsRealtime(.8f);
        yield return Capture("reassembled");
        float error = parts.Select((p,i) => Vector3.Distance(p.position,home[i])).Max();
        // A mid-trajectory direction change must preserve every transform.
        SetProgress(.715f);
        Vector3[] midPose = parts.Select(p => p.position).ToArray();
        AnimateTo(0);
        float reverseError = parts.Select((p,i) => Vector3.Distance(p.position,midPose[i])).Max();
        playing = false;
        SetProgress(0);
        yield return new WaitForSecondsRealtime(.8f);
        Vector3 cameraBefore = viewCamera.transform.position;
        yaw += 70; zoom = .8f;
        yield return new WaitForSecondsRealtime(.8f);
        bool cameraMoved = Vector3.Distance(cameraBefore,viewCamera.transform.position) > .5f;
        yield return Capture("rotated");
        ResetView();
        yield return new WaitForSecondsRealtime(.8f);
        yield return new WaitForEndOfFrame();
        Texture2D finalImage = ScreenCapture.CaptureScreenshotAsTexture();
        Color32[] pixels = finalImage.GetPixels32();
        int pink = pixels.Count(p => p.r > 190 && p.b > 190 && p.g < 65);
        double mean = pixels.Average(p => (p.r+p.g+p.b)/3.0);
        double variance = pixels.Average(p => Math.Pow((p.r+p.g+p.b)/3.0-mean,2));
        int lit = 0, sampled = 0;
        for (int y = Screen.height/4; y < Screen.height*3/4; y+=2)
        for (int x = Screen.width/10; x < Screen.width*9/10; x+=2)
        {
            Color32 p = pixels[y*Screen.width+x];
            if (p.r+p.g+p.b > 90) lit++;
            sampled++;
        }
        Destroy(finalImage);
        MeshFilter[] meshes = pump.GetComponentsInChildren<MeshFilter>();
        var pipeline = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
        Material paint = pumpRenderers.Single(r => r.name == "PMP_01_Motor_CastHousing").sharedMaterial;
        Vector4 paintValue = paint.GetVector("_BaseColor");
        float paintError = (new Vector3(paintValue.x,paintValue.y,paintValue.z)-
            new Vector3(.038f,.069f,.086f)).magnitude;
        Texture2D[] textures = pumpRenderers.SelectMany(r => r.sharedMaterials)
            .Distinct().SelectMany(m => new[] {"_BaseMap","_BumpMap","_RoughMap"}
                .Select(key => m.GetTexture(key) as Texture2D)).Where(t => t).Distinct().ToArray();
        var report = new Verification
        {
            utc = DateTime.UtcNow.ToString("O"), width = Screen.width, height = Screen.height,
            components = parts.Length, meshes = meshes.Length,
            triangles = meshes.Sum(m => m.sharedMesh.triangles.Length/3),
            assembledSize = assemblyBounds.size, returnErrorM = error,
            explodedMotorTravelM = moved, cameraMoved = cameraMoved,
            frontShroudRelativeTravelM = shroudTravel, bearingHousingRelativeTravelM = housingTravel,
            partNames = parts.Select(p => p.name).ToArray(),
            sourcePartsMatch = parts.Select((p,i) =>
                p.GetComponentsInChildren<MeshFilter>().Select(m => m.name).OrderBy(n => n)
                .SequenceEqual(manifest.parts[i].meshes.OrderBy(n => n))).All(match => match),
            reflectionProbeReady = FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)
                .Any(p => p.mode == ReflectionProbeMode.Custom && p.customBakedTexture),
            renderScale = pipeline.renderScale, msaaSamples = pipeline.msaaSampleCount,
            outputPixels = Screen.width*Screen.height,
            estimatedShadedPixels = Mathf.RoundToInt(Screen.width*Screen.height*viewCamera.rect.height*
                pipeline.renderScale*pipeline.renderScale),
            clarityRenderWidth = PumpClarity.renderWidth, clarityRenderHeight = PumpClarity.renderHeight,
            viewportLitFraction = (float)lit/sampled,
            textureFormats = textures.Select(t => t.name+" "+t.width+"x"+t.height+" "+t.format).ToArray(),
            paintLinearColorError = paintError,
            reflectionIntensity = RenderSettings.reflectionIntensity,
            hdr = viewCamera.allowHDR && pipeline.supportsHDR,
            orthographic = viewCamera.orthographic,
            fullTextureResolution = QualitySettings.globalTextureMipmapLimit == 0,
            motionVersion = manifest.motionVersion,
            maxLeaderPixels = maxLeader, labelOverlapPairs = overlaps,
            leaderCrossingPairs = crossings, allLabelsInFrame = labelsInFrame,
            labelViewSamples = labelViews, intermediateReverseErrorM = reverseError,
            motionReturnPathErrorM = pathError,
            dynamicResolutionDisabled = !viewCamera.allowDynamicResolution,
            textureCompressionDisabled = textures.Length > 0 && textures.All(t =>
                t.format == TextureFormat.RGBA32 || t.format == TextureFormat.RGB24 ||
                t.format == TextureFormat.R8 || t.format == TextureFormat.RG16),
            explodedProgress = explodedProgress, assembledProgress = progress,
            allShadersSupported = PumpClarity.shaderSupported &&
                pump.GetComponentsInChildren<Renderer>().All(r => r.sharedMaterials.All(m => m && m.shader.isSupported)),
            noPinkPixels = pink < pixels.Length*.001, imageHasContrast = variance > 400,
            allPartsInFrame = inFrame, errors = errors.ToArray()
        };
        elapsed = Time.realtimeSinceStartup;
        for (int i = 0; i < 60; i++) yield return null;
        report.meanFrameMs = (Time.realtimeSinceStartup-elapsed)*1000/60;
        report.passed = report.components == manifest.parts.Length && report.meshes == manifest.meshCount &&
            report.sourcePartsMatch && report.reflectionProbeReady && report.hdr && report.orthographic &&
            report.fullTextureResolution && paintError < .00001f && report.renderScale >= 2f &&
            report.textureCompressionDisabled && PumpClarity.renderWidth >= Screen.width*2 &&
            manifest.motionVersion == 3 && reverseError < .00001f && pathError < .00001f &&
            maxLeader <= MaxLeader*canvas.scaleFactor+1 && overlaps == 0 && crossings == 0 && labelsInFrame &&
            shroudTravel > .4f && housingTravel > .5f && error < .00001f &&
            Mathf.Abs(moved-manifest.parts[1].offset.magnitude) < .001f && cameraMoved && report.allShadersSupported &&
            report.noPinkPixels && report.imageHasContrast && report.viewportLitFraction > .5f &&
            inFrame && errors.Count == 0 &&
            Mathf.Abs(explodedProgress-1) < .00001f && progress < .00001f;
        File.WriteAllText(Path.Combine(verifyDirectory,"verification.json"),JsonUtility.ToJson(report,true));
        Debug.Log("PUMP_VIEWER_VERIFICATION "+JsonUtility.ToJson(report));
        Application.Quit(report.passed ? 0 : 2);
    }
}
