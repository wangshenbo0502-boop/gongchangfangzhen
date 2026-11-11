using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class PumpViewer
{
    const float MaxLeader = 48;
    Text[] tags;
    RectTransform[] tagRects, leaderLines, leaderDots;
    Renderer[] anchors;
    Vector3[][] surfaceVertices;
    Vector3[][] surfaceNormals;
    Rect[] labelBoxes, projectedBodies;
    Vector2[] lineStarts, lineEnds;
    Quaternion labelCameraRotation;
    Vector3 labelCameraPosition;
    float labelCameraSize = -1, labelProgress = -1;
    Vector2 labelCanvasSize;

    void BuildLabels()
    {
        int count = parts.Length;
        tags = new Text[count]; tagRects = new RectTransform[count];
        leaderLines = new RectTransform[count]; leaderDots = new RectTransform[count];
        anchors = new Renderer[count];
        surfaceVertices = new Vector3[count][]; surfaceNormals = new Vector3[count][];
        labelBoxes = new Rect[count]; projectedBodies = new Rect[count];
        lineStarts = new Vector2[count]; lineEnds = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            anchors[i] = parts[i].GetComponentsInChildren<Renderer>()
                .First(r => r.name == manifest.parts[i].anchor);
            Mesh mesh = anchors[i].GetComponent<MeshFilter>().sharedMesh;
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            int stride = Mathf.Max(1,vertices.Length/160);
            int[] samples = Enumerable.Range(0,vertices.Length).Where(n => n%stride == 0).ToArray();
            surfaceVertices[i] = samples.Select(n => vertices[n]).ToArray();
            surfaceNormals[i] = samples.Select(n => normals[n]).ToArray();
            leaderLines[i] = Rect("Part leader",canvas.transform,Vector2.zero,Vector2.zero,
                Vector2.zero,Vector2.one);
            Panel(leaderLines[i],new Color(.52f,.74f,.70f,.85f)).raycastTarget = false;
            leaderLines[i].pivot = new Vector2(0,.5f);
            leaderDots[i] = Rect("Part anchor",canvas.transform,Vector2.zero,Vector2.zero,
                Vector2.zero,new Vector2(4,4));
            Panel(leaderDots[i],new Color(.65f,.86f,.80f)).raycastTarget = false;
            leaderDots[i].pivot = new Vector2(.5f,.5f);
        }
        for (int i = 0; i < count; i++)
        {
            tags[i] = TextAt((i+1).ToString("00")+"  "+manifest.parts[i].label,canvas.transform,
                Vector2.zero,Vector2.zero,Vector2.zero,new Vector2(160,26),
                14,new Color(.94f,.97f,.96f));
            tagRects[i] = tags[i].rectTransform;
            tagRects[i].sizeDelta = new Vector2(Mathf.Ceil(tags[i].preferredWidth)+16,26);
            tagRects[i].pivot = new Vector2(0,.5f);
            RectTransform backing = Rect("Label backing",tagRects[i],Vector2.zero,Vector2.one,
                new Vector2(-6,0),Vector2.zero);
            Panel(backing,new Color(.075f,.095f,.095f,.93f)).raycastTarget = false;
            backing.SetAsFirstSibling();
            // A separate child text keeps the opaque backing behind the glyphs.
            TextAt(tags[i].text,backing,Vector2.zero,Vector2.one,
                new Vector2(8,0),new Vector2(-4,0),14,tags[i].color);
            tags[i].text = "";
        }
    }

    Rect ProjectBounds(Bounds bounds)
    {
        Vector2 low = new Vector2(float.MaxValue,float.MaxValue), high = -low;
        foreach (int x in new[] {-1,1}) foreach (int y in new[] {-1,1}) foreach (int z in new[] {-1,1})
        {
            Vector3 point = viewCamera.WorldToScreenPoint(bounds.center+
                Vector3.Scale(bounds.extents,new Vector3(x,y,z)));
            Vector2 p = new Vector2(point.x,point.y)/canvas.scaleFactor;
            low = Vector2.Min(low,p); high = Vector2.Max(high,p);
        }
        return UnityEngine.Rect.MinMaxRect(low.x,low.y,high.x,high.y);
    }

    static float OverlapArea(Rect a, Rect b)
    {
        return Mathf.Max(0,Mathf.Min(a.xMax,b.xMax)-Mathf.Max(a.xMin,b.xMin))*
            Mathf.Max(0,Mathf.Min(a.yMax,b.yMax)-Mathf.Max(a.yMin,b.yMin));
    }
    static float Cross(Vector2 a, Vector2 b) { return a.x*b.y-a.y*b.x; }
    static bool Crosses(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        return Cross(b-a,c-a)*Cross(b-a,d-a) < -.01f &&
            Cross(d-c,a-c)*Cross(d-c,b-c) < -.01f;
    }

    void UpdateLabels()
    {
        bool show = progress > .96f && !detail && !jointReview;
        for (int i = 0; i < tags.Length; i++)
        {
            tags[i].gameObject.SetActive(show);
            leaderLines[i].gameObject.SetActive(show);
            leaderDots[i].gameObject.SetActive(show);
        }
        if (!show) { labelCameraSize = -1; return; }
        Vector2 size = ((RectTransform)canvas.transform).rect.size;
        if (size == labelCanvasSize && Mathf.Abs(labelProgress-progress) < .00001f &&
            Quaternion.Angle(labelCameraRotation,viewCamera.transform.rotation) < .04f &&
            Vector3.Distance(labelCameraPosition,viewCamera.transform.position) < .0002f &&
            Mathf.Abs(labelCameraSize-viewCamera.orthographicSize) < .0001f) return;
        labelCanvasSize = size; labelProgress = progress;
        labelCameraRotation = viewCamera.transform.rotation;
        labelCameraPosition = viewCamera.transform.position;
        labelCameraSize = viewCamera.orthographicSize;
        var projected = new List<Vector2>[parts.Length];
        var anchorBounds = new Rect[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            projectedBodies[i] = ProjectBounds(ComponentBounds(parts[i]));
            anchorBounds[i] = ProjectBounds(anchors[i].bounds);
            projected[i] = new List<Vector2>();
            for (int v = 0; v < surfaceVertices[i].Length; v++)
            {
                Transform t = anchors[i].transform;
                if (Vector3.Dot(t.TransformDirection(surfaceNormals[i][v]),-viewCamera.transform.forward) < .05f)
                    continue;
                Vector3 p = viewCamera.WorldToScreenPoint(t.TransformPoint(surfaceVertices[i][v]));
                projected[i].Add(new Vector2(p.x,p.y)/canvas.scaleFactor);
            }
            if (projected[i].Count == 0) projected[i].Add(anchorBounds[i].center);
            labelBoxes[i] = new Rect(-1000,-1000,0,0);
        }
        int[] order = Enumerable.Range(0,parts.Length)
            .OrderBy(i => anchorBounds[i].width*anchorBounds[i].height).ToArray();
        // Place small internal components first, then refine against all labels.
        for (int pass = 0; pass < 3; pass++)
        foreach (int i in order)
        {
            Rect body = anchorBounds[i];
            Vector2 dimensions = tagRects[i].sizeDelta;
            float best = float.MaxValue;
            Rect chosen = default;
            Vector2 chosenFrom = default, chosenTo = default;
            foreach (float gap in new[] {10f,24f,40f})
            foreach (int direction in Enumerable.Range(0,8))
            foreach (float shift in new[] {0f,-32f,32f,-64f,64f})
            {
                bool vertical = direction < 4;
                float x, y;
                if (vertical)
                {
                    x = body.center.x+shift+(direction%2 == 0 ? 0 : dimensions.x*.45f);
                    y = direction < 2 ? body.yMax+gap+dimensions.y/2 : body.yMin-gap-dimensions.y/2;
                }
                else
                {
                    x = direction < 6 ? body.xMin-gap-dimensions.x/2 : body.xMax+gap+dimensions.x/2;
                    y = body.center.y+shift+(direction%2 == 0 ? 0 : dimensions.y*.65f);
                }
                Rect box = new Rect(x-dimensions.x/2,y-dimensions.y/2,dimensions.x,dimensions.y);
                if (box.xMin < 12 || box.xMax > size.x-12 || box.yMin < 140 || box.yMax > size.y-106) continue;
                Vector2 from = default, to = default;
                float shortest = float.MaxValue;
                foreach (Vector2 point in projected[i])
                {
                    Vector2 edge = new Vector2(Mathf.Clamp(point.x,box.xMin-6,box.xMax),
                        Mathf.Clamp(point.y,box.yMin,box.yMax));
                    float distance = (point-edge).sqrMagnitude;
                    if (distance < shortest) { shortest = distance; from = point; to = edge; }
                }
                if (shortest > MaxLeader*MaxLeader) continue;
                float score = shortest*.08f+Mathf.Abs(shift)*.05f+gap*.04f;
                for (int other = 0; other < parts.Length; other++)
                {
                    if (other == i) continue;
                    Rect padded = labelBoxes[other];
                    padded.xMin -= 8; padded.xMax += 8; padded.yMin -= 5; padded.yMax += 5;
                    score += OverlapArea(box,padded)*1000;
                    score += OverlapArea(box,projectedBodies[other])*.8f;
                    if (Crosses(from,to,lineStarts[other],lineEnds[other])) score += 4000;
                }
                score += OverlapArea(box,body)*4;
                if (score >= best) continue;
                best = score; chosen = box; chosenFrom = from; chosenTo = to;
            }
            if (best == float.MaxValue)
            {
                // A compact local name remains attached when the user zooms past the frame.
                Vector2 point = projected[i][0];
                chosen = new Rect(point.x+8,point.y+8,dimensions.x,dimensions.y);
                chosenFrom = point; chosenTo = new Vector2(chosen.xMin,chosen.yMin);
            }
            labelBoxes[i] = chosen; lineStarts[i] = chosenFrom; lineEnds[i] = chosenTo;
        }
        for (int i = 0; i < parts.Length; i++)
        {
            tagRects[i].anchoredPosition = new Vector2(labelBoxes[i].xMin,labelBoxes[i].center.y);
            Vector2 delta = lineEnds[i]-lineStarts[i];
            leaderLines[i].anchoredPosition = lineStarts[i];
            leaderLines[i].sizeDelta = new Vector2(delta.magnitude,1);
            leaderLines[i].localRotation = Quaternion.Euler(0,0,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
            leaderDots[i].anchoredPosition = lineStarts[i];
        }
    }

    float MaxLeaderPixels() { return leaderLines.Max(l => l.sizeDelta.x)*canvas.scaleFactor; }
    int LabelOverlapCount()
    {
        int count = 0;
        for (int a = 0; a < parts.Length; a++)
        for (int b = a+1; b < parts.Length; b++)
            if (OverlapArea(labelBoxes[a],labelBoxes[b]) > 1) count++;
        return count;
    }
    int LeaderCrossingCount()
    {
        int count = 0;
        for (int a = 0; a < parts.Length; a++)
        for (int b = a+1; b < parts.Length; b++)
            if (Crosses(lineStarts[a],lineEnds[a],lineStarts[b],lineEnds[b])) count++;
        return count;
    }
    bool LabelsInFrame()
    {
        Vector2 size = ((RectTransform)canvas.transform).rect.size;
        return labelBoxes.All(b => b.xMin >= 6 && b.xMax <= size.x-6 &&
            b.yMin >= 134 && b.yMax <= size.y-100);
    }
}
