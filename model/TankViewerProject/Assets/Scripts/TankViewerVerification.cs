using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

// Runs only with -tankReviewOutput <directory>.
public sealed class TankViewerVerification : MonoBehaviour
{
    TankViewer viewer;
    string folder;
    readonly System.Collections.Generic.List<string> errors = new();

    public static void Install(TankViewer viewer)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(arguments, "-tankReviewOutput");
        if (index < 0 || index + 1 >= arguments.Length) return;
        var check = viewer.gameObject.AddComponent<TankViewerVerification>();
        check.viewer = viewer;
        check.folder = Path.GetFullPath(arguments[index + 1]);
    }

    IEnumerator Start()
    {
        Directory.CreateDirectory(folder);
        Application.logMessageReceived += RecordError;
        var steps = Verify();
        while (true)
        {
            object next;
            try { if (!steps.MoveNext()) break; next = steps.Current; }
            catch (Exception e) { errors.Add(e.ToString()); break; }
            yield return next;
        }
        Application.logMessageReceived -= RecordError;
        File.AppendAllLines(Path.Combine(folder, "verification.txt"),
            errors.Select(error => "FAIL " + error).Append("Result: " + (errors.Count == 0 ? "PASS" : "FAIL")));
        Application.Quit(errors.Count == 0 ? 0 : 1);
    }

    void RecordError(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception) errors.Add(message + "\n" + stack);
    }

    IEnumerator Verify()
    {
        float deadline = Time.realtimeSinceStartup + 12;
        while (TankClarity.renderWidth == 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Check(TankClarity.shaderSupported && TankClarity.renderWidth > Screen.width,
            "Supersampling and detail pass at " + Screen.width + " × " + Screen.height);
        var buttons = FindObjectsByType<Button>(FindObjectsSortMode.None);
        Button explode = buttons.Single(button => button.name == "拆解");
        Button assemble = buttons.Single(button => button.name == "组装");
        var homes = Enumerable.Range(0, 12).Select(i => viewer.PartAt(i).position).ToArray();
        TankViewer.ResolveBasis(viewer.tank, out var x, out var y, out var z);
        Check(Vector3.Dot(z.normalized, Vector3.up) > .999f &&
              Mathf.Abs(Vector3.Dot(x.normalized, y.normalized)) < .001f, "FBX motion probes agree with imported up axis");
        yield return Capture("01-assembled");
        viewer.ReviewView(5, 9, .52f, new Vector3(0, .35f, 0));
        yield return Capture("02-instruments");
        viewer.ReviewView(20, 30, .47f, new Vector3(0, 2.39f, 0));
        yield return Capture("03-roof-lifting-lugs");
        viewer.ReviewView(26, 15, 1, Vector3.zero);
        explode.onClick.Invoke();
        while (viewer.Progress < .039f && viewer.IsAnimating) yield return null;
        Vector3 travel = viewer.PartAt(10).position - homes[10];
        Check(Vector3.Dot(travel, -y) > .74f && Vector3.ProjectOnPlane(travel, y).magnitude < .001f,
            "Manway cover clears straight along the flange axis");
        yield return Capture("04-manway-cover-release");
        assemble.onClick.Invoke();
        while (viewer.IsAnimating) yield return null;
        CheckHome(homes, "Mid-disassembly reversal");
        explode.onClick.Invoke();
        while (viewer.Progress < .80f && viewer.IsAnimating) yield return null;
        yield return Capture("05-lower-assembly-clearance");
        while (viewer.IsAnimating) yield return null;
        Check(viewer.Progress > .999f && viewer.TagCount == 12, "All 12 assembly labels are shown");
        yield return Capture("06-exploded");
        bool picked = false;
        foreach (var part in Enumerable.Range(0, 12).Select(viewer.PartAt))
        {
            foreach (var renderer in part.GetComponentsInChildren<Renderer>())
            {
                Vector3 point = viewer.viewCamera.WorldToScreenPoint(renderer.bounds.center);
                if (!viewer.viewCamera.pixelRect.Contains(point) || !viewer.SelectPart(point)) continue;
                picked = true;
                break;
            }
            if (picked) break;
        }
        Check(picked && viewer.Selected >= 0, "Triangle raycast selects an assembly and shows its description");
        yield return Capture("07-selected");
        assemble.onClick.Invoke();
        while (viewer.IsAnimating) yield return null;
        CheckHome(homes, "Full reverse assembly");
        yield return Capture("08-reassembled");
    }

    void CheckHome(Vector3[] home, string label) =>
        Check(viewer.Progress == 0 &&
              home.Select((position, i) => Vector3.Distance(position, viewer.PartAt(i).position)).Max() < .00001f,
              label + " restores all assembly positions (progress " + viewer.Progress + ")");

    void Check(bool ok, string result)
    {
        if (!ok) throw new InvalidOperationException(result);
        File.AppendAllText(Path.Combine(folder, "verification.txt"), "PASS " + result + "\n");
    }

    IEnumerator Capture(string name)
    {
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(folder, name + ".png"));
        yield return new WaitForSecondsRealtime(.18f);
    }
}
