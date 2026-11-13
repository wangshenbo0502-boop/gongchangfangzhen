using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;

// Enabled only by the local review script; it observes actual player input and state.
public sealed class FanViewerVerification : MonoBehaviour
{
    [Serializable] public struct SlowFrame
    {
        public float milliseconds, progress;
        public double mainThreadMs, renderThreadMs, gpuMs, presentWaitMs;
    }

    [Serializable] public sealed class MotionPerformance
    {
        public string phase;
        public int frames, over16ms, over25ms, over33ms, over50ms, gen0Collections;
        public float averageFps, p50Ms, p95Ms, p99Ms, maxMs;
        public long mainThreadAllocatedBytes;
        public SlowFrame[] slowFrames;
    }
    [Serializable] public sealed class PartState
    {
        public string name;
        public Vector3 position;
        public Vector2 pickPoint;
        public bool pickable;
    }

    [Serializable] public sealed class Snapshot
    {
        public int width, height, selectedPart, visibleLabels, overlappingLabels, labelsOutsideViewport;
        public float progress, maxHomeError, longestLeader, averageFps;
        public float coverBoxGap, coverVerticalGap, lidBoxGap;
        public float coverOutwardTravel, lidOutwardTravel;
        public bool animating, descriptionVisible, outlineVisible;
        public string automationStatus, automationError;
        public long allocatedMemoryBytes;
        public string graphicsDevice;
        public int clarityRenderWidth, clarityRenderHeight, msaaSamples, vSyncCount, targetFrameRate;
        public float renderScale, displayRefreshHz;
        public bool clarityShaderSupported, focused, renderReady, motionPending;
        public float renderReadySeconds;
        public PartState[] parts;
        public MotionPerformance[] motionPerformance;
    }

    FanViewer viewer;
    FanPartInspection inspection;
    Transform[] parts;
    Vector3[] home;
    string outputDirectory;
    float nextWrite, seconds;
    int frames;
    string completionStatus = "running", completionError = "", reportDetails = "";
    readonly List<float> motionFrames = new List<float>(8192);
    readonly List<MotionPerformance> performance = new List<MotionPerformance>();
    readonly List<SlowFrame> slowFrames = new List<SlowFrame>(256);
    readonly FrameTiming[] timing = new FrameTiming[1];
    bool measuring, observeInputMotion;
    int startCollections, skipFrames;
    long lastAllocated, motionAllocated;
    float coverBoxGap, coverVerticalGap, lidBoxGap;
    Vector3 coverOutward, lidOutward;
    float coverOutwardTravel, lidOutwardTravel;

    public static void AttachIfRequested(FanViewer viewer)
    {
        string[] args = Environment.GetCommandLineArgs();
        int flag = Array.IndexOf(args, "-fanReviewOutput");
        if (flag < 0 || flag + 1 >= args.Length) return;
        var review = viewer.gameObject.AddComponent<FanViewerVerification>();
        review.viewer = viewer;
        review.inspection = viewer.GetComponent<FanPartInspection>();
        review.outputDirectory = Path.GetFullPath(args[flag + 1]);
        Directory.CreateDirectory(review.outputDirectory);
        FanViewer.Manifest manifest = JsonUtility.FromJson<FanViewer.Manifest>(viewer.componentData.text);
        Transform[] nodes = viewer.fan.GetComponentsInChildren<Transform>(true);
        review.parts = manifest.parts.Select(p => nodes.Single(t => t.name == p.name)).ToArray();
        review.home = review.parts.Select(p => p.position).ToArray();
        FanViewer.ResolveMotionBasis(viewer.fan, out Vector3 axis, out _, out _);
        review.coverOutward = Vector3.ProjectOnPlane(
            review.BoundsForPart(8).center - review.BoundsForPart(1).center, axis).normalized;
        review.lidOutward = (review.BoundsForPart(10).center -
            review.BoundsForPart(7).center).normalized;
        if (args.Contains("-fanReviewAutomation"))
        {
            if (EventSystem.current) EventSystem.current.enabled = false;
            review.StartCoroutine(review.RunAutomation());
        }
        else review.observeInputMotion = true;
    }

    IEnumerator RunAutomation()
    {
        IEnumerator steps = RunSteps();
        while (true)
        {
            object next;
            try
            {
                if (!steps.MoveNext()) yield break;
                next = steps.Current;
            }
            catch (Exception exception)
            {
                completionStatus = "failed";
                completionError = exception.ToString();
                yield break;
            }
            yield return next;
        }
    }

    IEnumerator RunSteps()
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
        Button explode = buttons.Single(button => button.name == "拆解");
        Button assemble = buttons.Single(button => button.name == "组装");
        bool motionOnly = Environment.GetCommandLineArgs().Contains("-fanMotionOnly");
        float readyDeadline = Time.realtimeSinceStartup + 10;
        while (FanClarity.renderWidth == 0 && Time.realtimeSinceStartup < readyDeadline)
            yield return null;
        if (!motionOnly) yield return Capture("01-assembled");
        var pipeline = (UniversalRenderPipelineAsset)QualitySettings.renderPipeline;
        if (!FanClarity.shaderSupported || FanClarity.renderWidth <= Screen.width ||
            FanClarity.renderHeight <= viewer.viewCamera.pixelHeight || pipeline.renderScale < 2f)
            throw new InvalidOperationException("Fan supersampling/clarity pass is not active.");
        yield return new WaitForSecondsRealtime(.5f);
        explode.onClick.Invoke();
        while (viewer.IsMotionPending) yield return null;
        BeginMeasurement();
        float timeout = Time.realtimeSinceStartup + 18;
        while (viewer.IsAnimating && Time.realtimeSinceStartup < timeout) yield return null;
        EndMeasurement("first-disassembly");
        if (viewer.IsAnimating || viewer.Progress < .999f)
            throw new InvalidOperationException("First disassembly did not complete.");
        yield return new WaitForSecondsRealtime(.5f);
        assemble.onClick.Invoke();
        BeginMeasurement();
        timeout = Time.realtimeSinceStartup + 18;
        while (viewer.IsAnimating && Time.realtimeSinceStartup < timeout) yield return null;
        EndMeasurement("first-assembly");
        CheckHome();
        if (motionOnly)
        {
            completionStatus = "passed";
            reportDetails = "first full disassembly/assembly from bound buttons; no screenshots; exact home return";
            yield break;
        }
        explode.onClick.Invoke();
        yield return new WaitForSecondsRealtime(4);
        float displacement = Vector3.Distance(parts[3].position, home[3]);
        if (displacement < .02f) throw new InvalidOperationException("Explode event produced no part displacement.");
        assemble.onClick.Invoke();
        timeout = Time.realtimeSinceStartup + 18;
        while (viewer.IsAnimating && Time.realtimeSinceStartup < timeout) yield return null;
        if (viewer.IsAnimating || viewer.Progress > .001f)
            throw new InvalidOperationException("Reverse assembly did not complete.");
        float homeError = 0;
        for (int i = 0; i < parts.Length; i++)
            homeError = Mathf.Max(homeError, Vector3.Distance(parts[i].position, home[i]));
        if (homeError > .002f) throw new InvalidOperationException("Reverse assembly home error: " + homeError);
        yield return Capture("02-reassembled-after-reversal");
        explode.onClick.Invoke();
        while (viewer.Progress < .14f && viewer.IsAnimating) yield return null;
        // Infer outward from the imported mesh positions, independently of motion conversion.
        coverOutwardTravel = Vector3.Dot(parts[8].position - home[8], coverOutward);
        lidOutwardTravel = Vector3.Dot(parts[10].position - home[10], lidOutward);
        if (coverOutwardTravel < .065f || lidOutwardTravel < .22f)
            throw new InvalidOperationException("Cover/lid moved into the imported housing: " +
                coverOutwardTravel + ", " + lidOutwardTravel);
        yield return Capture("03a-cover-unseated");
        while (viewer.Progress < .22f && viewer.IsAnimating) yield return null;
        coverBoxGap = BoundsForPart(7).min.x - BoundsForPart(8).max.x;
        if (coverBoxGap < .12f)
            throw new InvalidOperationException("Cover has not moved clear of the terminal box.");
        yield return Capture("03b-cover-box-separated");
        while (viewer.Progress < .42f && viewer.IsAnimating) yield return null;
        coverVerticalGap = BoundsForPart(8).min.y - BoundsForPart(1).max.y;
        lidBoxGap = BoundsForPart(10).min.x - BoundsForPart(7).max.x;
        if (coverVerticalGap < .12f || lidBoxGap < .12f)
            throw new InvalidOperationException("Cover or terminal lid has insufficient clearance.");
        yield return Capture("03-cover-cleared");
        while (viewer.Progress < .62f && viewer.IsAnimating) yield return null;
        yield return Capture("04-cover-translated");
        timeout = Time.realtimeSinceStartup + 18;
        while (viewer.IsAnimating && Time.realtimeSinceStartup < timeout) yield return null;
        if (viewer.IsAnimating || viewer.Progress < .999f)
            throw new InvalidOperationException("Full disassembly did not complete.");
        yield return Capture("05-exploded");
        if (inspection.LabelRoots.Count(r => r.gameObject.activeInHierarchy) != parts.Length)
            throw new InvalidOperationException("Not every exploded assembly has a visible label.");
        Physics.SyncTransforms();
        Vector2 pick = Vector2.zero;
        bool found = false;
        foreach (Transform part in parts.Skip(2))
        foreach (Renderer renderer in part.GetComponentsInChildren<Renderer>()
            .OrderByDescending(r => r.bounds.size.sqrMagnitude))
        {
            Vector3 point = viewer.viewCamera.WorldToScreenPoint(renderer.bounds.center);
            if (point.z <= 0 || !viewer.viewCamera.pixelRect.Contains(point)) continue;
            if (!Physics.Raycast(viewer.viewCamera.ScreenPointToRay(point), out RaycastHit hit, 100) ||
                !hit.transform.IsChildOf(part)) continue;
            pick = point;
            found = true;
            break;
        }
        if (!found || !inspection.SelectForReview(pick))
            throw new InvalidOperationException("No exploded assembly could be selected by collider raycast.");
        yield return null;
        bool panel = FindObjectsByType<RectTransform>(FindObjectsInactive.Include,
            FindObjectsSortMode.None).Any(rect =>
                rect.name == "Part introduction" && rect.gameObject.activeInHierarchy);
        bool outline = FindObjectsByType<RawImage>(FindObjectsSortMode.None)
            .Any(image => image.name == "Selected assembly outline" && image.enabled);
        if (!panel || !outline)
            throw new InvalidOperationException("Component introduction or selection outline is not visible.");
        yield return Capture("06-component-selected");
        assemble.onClick.Invoke();
        while (viewer.Progress > .42f && viewer.IsAnimating) yield return null;
        yield return Capture("07a-cover-return-above-duct");
        while (viewer.Progress > .22f && viewer.IsAnimating) yield return null;
        yield return Capture("07c-cover-return-box-separated");
        while (viewer.Progress > .14f && viewer.IsAnimating) yield return null;
        yield return Capture("07b-cover-return-unseated");
        yield return Capture("07-cover-return-clearance");
        timeout = Time.realtimeSinceStartup + 18;
        while (viewer.IsAnimating && Time.realtimeSinceStartup < timeout) yield return null;
        CheckHome();
        yield return Capture("08-full-reassembly");
        // No screenshots, raycasts, file writes or scene searches during these runs.
        yield return new WaitForSecondsRealtime(.5f);
        explode.onClick.Invoke();
        BeginMeasurement();
        timeout = Time.realtimeSinceStartup + 18;
        while (viewer.IsAnimating && Time.realtimeSinceStartup < timeout) yield return null;
        EndMeasurement("full-disassembly");
        if (viewer.IsAnimating || viewer.Progress < .999f)
            throw new InvalidOperationException("Performance disassembly did not complete.");
        yield return new WaitForSecondsRealtime(.5f);
        assemble.onClick.Invoke();
        BeginMeasurement();
        timeout = Time.realtimeSinceStartup + 18;
        while (viewer.IsAnimating && Time.realtimeSinceStartup < timeout) yield return null;
        EndMeasurement("full-assembly");
        CheckHome();
        completionStatus = "passed";
        reportDetails = "buttons=bound onClick; mid-disassembly reverse; full disassembly; collider raycast; " +
            "outline; introduction; homeError=" + homeError.ToString("F6") +
            "; midMotionDisplacement=" + displacement.ToString("F4") +
            "; full reverse and repeated full cycle; performance excludes screenshot/telemetry work";
    }

    void CheckHome()
    {
        if (viewer.IsAnimating || viewer.Progress > .001f)
            throw new InvalidOperationException("Full assembly did not complete.");
        for (int i = 0; i < parts.Length; i++)
            if (Vector3.Distance(parts[i].position, home[i]) > .00001f)
                throw new InvalidOperationException("Full assembly left a displaced part: " + parts[i].name);
    }

    Bounds BoundsForPart(int index)
    {
        Renderer[] meshes = parts[index].GetComponentsInChildren<Renderer>();
        Bounds bounds = meshes[0].bounds;
        for (int i = 1; i < meshes.Length; i++) bounds.Encapsulate(meshes[i].bounds);
        return bounds;
    }

    void BeginMeasurement()
    {
        motionFrames.Clear();
        slowFrames.Clear();
        startCollections = GC.CollectionCount(0);
        lastAllocated = GC.GetAllocatedBytesForCurrentThread();
        motionAllocated = 0;
        skipFrames = 1;
        measuring = true;
    }

    void EndMeasurement(string phase)
    {
        measuring = false;
        float[] times = motionFrames.ToArray();
        if (times.Length == 0) throw new InvalidOperationException("No motion frame timings collected.");
        Array.Sort(times);
        performance.Add(new MotionPerformance
        {
            phase = phase,
            frames = times.Length,
            averageFps = 1000f / times.Average(),
            p50Ms = times[(int)Math.Ceiling(times.Length * .50) - 1],
            p95Ms = times[(int)Math.Ceiling(times.Length * .95) - 1],
            p99Ms = times[(int)Math.Ceiling(times.Length * .99) - 1],
            maxMs = times[times.Length - 1],
            over16ms = times.Count(t => t > 1000f / 60f),
            over25ms = times.Count(t => t > 25),
            over33ms = times.Count(t => t > 1000f / 30f),
            over50ms = times.Count(t => t > 50),
            gen0Collections = GC.CollectionCount(0) - startCollections,
            mainThreadAllocatedBytes = motionAllocated,
            slowFrames = slowFrames.ToArray()
        });
    }

    IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(outputDirectory, name + ".png"));
        yield return new WaitForSecondsRealtime(.2f);
    }

    void LateUpdate()
    {
        seconds += Time.unscaledDeltaTime;
        frames++;
        FrameTimingManager.CaptureFrameTimings();
        FrameTimingManager.GetLatestTimings(1, timing);
        if (observeInputMotion)
        {
            if (!measuring && viewer.IsAnimating) BeginMeasurement();
            else if (measuring && !viewer.IsAnimating)
                EndMeasurement(viewer.Progress > .999f ? "input-disassembly" : "input-assembly");
        }
        if (measuring)
        {
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            if (skipFrames > 0) skipFrames--;
            else
            {
                motionFrames.Add(Time.unscaledDeltaTime * 1000);
                if (Time.unscaledDeltaTime > 1f / 60f && slowFrames.Count < slowFrames.Capacity)
                    slowFrames.Add(new SlowFrame
                    {
                        milliseconds = Time.unscaledDeltaTime * 1000,
                        progress = viewer.Progress,
                        mainThreadMs = timing[0].cpuMainThreadFrameTime,
                        renderThreadMs = timing[0].cpuRenderThreadFrameTime,
                        gpuMs = timing[0].gpuFrameTime,
                        presentWaitMs = timing[0].cpuMainThreadPresentWaitTime
                    });
                motionAllocated += allocated - lastAllocated;
            }
            lastAllocated = allocated;
            return;
        }
        if (viewer.IsAnimating) return;
        if (Time.unscaledTime < nextWrite) return;
        nextWrite = Time.unscaledTime + .3f;
        Physics.SyncTransforms();
        var state = new Snapshot
        {
            width = Screen.width,
            height = Screen.height,
            progress = viewer.Progress,
            animating = viewer.IsAnimating,
            selectedPart = inspection.SelectedPart,
            automationStatus = completionStatus,
            automationError = completionError,
            parts = new PartState[parts.Length],
            averageFps = frames / Mathf.Max(.001f, seconds),
            coverBoxGap = coverBoxGap,
            coverVerticalGap = coverVerticalGap,
            lidBoxGap = lidBoxGap,
            coverOutwardTravel = coverOutwardTravel,
            lidOutwardTravel = lidOutwardTravel,
            graphicsDevice = SystemInfo.graphicsDeviceName,
            clarityRenderWidth = FanClarity.renderWidth,
            clarityRenderHeight = FanClarity.renderHeight,
            clarityShaderSupported = FanClarity.shaderSupported,
            renderScale = ((UniversalRenderPipelineAsset)QualitySettings.renderPipeline).renderScale,
            msaaSamples = ((UniversalRenderPipelineAsset)QualitySettings.renderPipeline).msaaSampleCount,
            vSyncCount = QualitySettings.vSyncCount,
            targetFrameRate = Application.targetFrameRate,
            displayRefreshHz = (float)Screen.currentResolution.refreshRateRatio.value,
            focused = Application.isFocused,
            renderReady = viewer.IsRenderReady,
            motionPending = viewer.IsMotionPending,
            renderReadySeconds = viewer.RenderReadySeconds,
            allocatedMemoryBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()
        };
        state.motionPerformance = performance.ToArray();
        for (int i = 0; i < parts.Length; i++)
        {
            state.maxHomeError = Mathf.Max(state.maxHomeError, Vector3.Distance(parts[i].position, home[i]));
            var item = new PartState { name = parts[i].name, position = parts[i].position };
            if (!state.animating && state.progress > .999f)
            {
                foreach (Renderer renderer in parts[i].GetComponentsInChildren<Renderer>()
                    .OrderByDescending(r => r.bounds.size.sqrMagnitude))
                {
                    Vector3 screen = viewer.viewCamera.WorldToScreenPoint(renderer.bounds.center);
                    if (screen.z <= 0 || !viewer.viewCamera.pixelRect.Contains(screen)) continue;
                    if (!Physics.Raycast(viewer.viewCamera.ScreenPointToRay(screen), out RaycastHit hit, 100) ||
                        !hit.transform.IsChildOf(parts[i])) continue;
                    item.pickPoint = screen;
                    item.pickable = true;
                    break;
                }
            }
            state.parts[i] = item;
        }
        Rect[] bounds = inspection.LabelRoots.Where(r => r.gameObject.activeInHierarchy).Select(r =>
        {
            var corners = new Vector3[4];
            r.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }).ToArray();
        state.visibleLabels = bounds.Length;
        for (int i = 0; i < bounds.Length; i++)
        {
            if (!viewer.viewCamera.pixelRect.Contains(bounds[i].min) ||
                !viewer.viewCamera.pixelRect.Contains(bounds[i].max)) state.labelsOutsideViewport++;
            for (int j = i + 1; j < bounds.Length; j++)
                if (bounds[i].Overlaps(bounds[j])) state.overlappingLabels++;
        }
        foreach (RectTransform line in inspection.Leaders)
            if (line.gameObject.activeInHierarchy)
                state.longestLeader = Mathf.Max(state.longestLeader, line.sizeDelta.x);
        foreach (RectTransform rect in FindObjectsByType<RectTransform>(FindObjectsInactive.Include,
            FindObjectsSortMode.None))
            if (rect.name == "Part introduction") state.descriptionVisible = rect.gameObject.activeInHierarchy;
        foreach (RawImage image in FindObjectsByType<RawImage>(FindObjectsSortMode.None))
            if (image.name == "Selected assembly outline") state.outlineVisible = image.enabled;
        string pending = Path.Combine(outputDirectory, "state.pending");
        File.WriteAllText(pending, JsonUtility.ToJson(state, true));
        File.Copy(pending, Path.Combine(outputDirectory, "state.json"), true);
        File.Delete(pending);
        if (completionStatus != "running")
            File.WriteAllText(Path.Combine(outputDirectory, "automation-result.txt"),
                completionStatus + Environment.NewLine + reportDetails + Environment.NewLine + completionError);
    }
}
