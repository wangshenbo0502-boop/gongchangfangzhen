using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WorkshopDemo
{
    public sealed partial class WorkshopAnalysisView
    {
        sealed class Note
        {
            public Vector3 position;
            public Text text;
        }
        sealed class Sample
        {
            public int face;
            public Vector3 position, displacement;
            public Vector3 displayed;
            public float temperature;
        }
        readonly List<Note> notes = new List<Note>();
        readonly List<Sample> samples = new List<Sample>();
        readonly List<Object> ownedAnnotationObjects = new List<Object>();
        GameObject annotations, probeObjects;
        Material annotationMaterial;
        Vector3 cameraFocus;
        Font annotationFont;
        public bool ShowAnnotations = true;

        void InitializeAnnotations()
        {
            annotationFont = GetComponentInParent<Canvas>().GetComponentInChildren<Text>().font;
            annotationMaterial = new Material(Resources.Load<Shader>("WorkshopField"));
            ownedAnnotationObjects.Add(annotationMaterial);
        }

        void Stroke(Transform parent, string name, Color color, params Vector3[] points)
        {
            var g = new GameObject(name) { layer = 29 };
            g.transform.SetParent(parent, false);
            var line = g.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = annotationMaterial;
            line.startColor = line.endColor = color;
            line.startWidth = line.endWidth = .006f;
            line.positionCount = points.Length;
            line.SetPositions(points);
        }

        void Arrow(Transform parent, Vector3 tip, Vector3 direction, Color color)
        {
            direction.Normalize();
            Vector3 side = Vector3.Cross(direction, Vector3.forward).normalized;
            if (side.sqrMagnitude < .1f) side = Vector3.right;
            Stroke(parent, "Boundary arrow", color, tip - direction * .32f, tip,
                tip - direction * .075f + side * .035f, tip,
                tip - direction * .075f - side * .035f);
        }

        void AddNote(Vector3 position, string text, Color color)
        {
            var g = new GameObject("Field annotation", typeof(RectTransform));
            g.transform.SetParent(transform, false);
            var r = g.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = Vector2.zero;
            r.sizeDelta = new Vector2(238, 24);
            var label = g.AddComponent<Text>();
            label.font = annotationFont;
            label.fontSize = 12;
            label.color = color;
            label.text = text;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            notes.Add(new Note { position = position, text = label });
        }

        void RefreshAnnotations(Bounds bounds, float normalization)
        {
            if (annotations) { annotations.SetActive(false); Destroy(annotations); }
            foreach (var n in notes) { n.text.gameObject.SetActive(false); Destroy(n.text.gameObject); }
            notes.Clear();
            annotations = new GameObject("Loads constraints and dimensions") { layer = 29 };
            annotations.transform.SetParent(body.transform, false);
            Vector3 min = (bounds.min - center) * normalization, max = (bounds.max - center) * normalization;
            var white = new Color(.8f,.87f,.9f);
            var green = new Color(.25f, .95f, .68f);
            Vector3 p = new Vector3(min.x, min.y - .20f, min.z);
            Vector3 q = new Vector3(max.x, min.y - .20f, min.z);
            Stroke(annotations.transform, "Length dimension", white, p, q);
            foreach (var point in new[] { p, q })
                Stroke(annotations.transform, "Dimension extension", white, point + Vector3.up * .07f, point - Vector3.up * .07f);
            AddNote((p+q)/2 - Vector3.up * .08f, $"L = {bounds.size.x*1000:F2} mm", white);
            bool round = Result.shape == "tank" || Result.shape == "shaft" || Result.shape == "pipe";
            AddNote(new Vector3(max.x,max.y+.38f,max.z), round ?
                $"外径 {Result.dimensionsMm[1]*2:F2} / 原壁厚 {Result.dimensionsMm[2]:F2} mm" :
                $"宽 {Result.dimensionsMm[1]:F2} / 原厚 {Result.dimensionsMm[2]:F2} mm",white);
            if (Result.analysis == "structural")
            {
                Stroke(annotations.transform, "Fixed face", green, new Vector3(min.x, min.y-.07f, min.z),
                    new Vector3(min.x, max.y+.07f, min.z));
                for (int i = 0; i < 6; i++)
                {
                    Vector3 at = new Vector3(min.x, Mathf.Lerp(min.y,max.y,i/5f), min.z);
                    Stroke(annotations.transform, "Clamp hatch", green, at, at + new Vector3(-.10f,-.07f,0));
                }
                AddNote(new Vector3(min.x, max.y+.15f,min.z), "固定端  Ux = Uy = Uz = 0", green);
                var m = Result.models[Model];
                Vector3 force = m.appliedN == null ? Vector3.zero :
                    new Vector3(m.appliedN[0], m.appliedN[2], m.appliedN[1]);
                var orange = new Color(1,.65f,.2f);
                bool pressure = Result.variable == "pressure" || Result.shape == "tank";
                if (pressure)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 radial = new Vector3(0, Mathf.Cos(i*Mathf.PI/2), Mathf.Sin(i*Mathf.PI/2));
                        Arrow(annotations.transform, new Vector3(max.x*.6f,0,0) + radial * max.y, radial, orange);
                    }
                    float pa = Result.variable == "pressure" ? Result.inputValues[Model] : 300000;
                    AddNote(new Vector3(max.x*.5f,max.y+.20f,0), $"内压 p = {pa/1000000*Factor:F3} MPa", orange);
                }
                else if (force.magnitude > .001f)
                {
                    for (int i = 0; i < 3; i++)
                        Arrow(annotations.transform, new Vector3(max.x, Mathf.Lerp(min.y,max.y,i/2f), 0),
                            force.normalized, orange);
                    AddNote(new Vector3(max.x, min.y-.40f, 0), $"端面合力 F = {force.magnitude*Factor:F2} N", orange);
                }
            }
            else
                AddNote(new Vector3(0,max.y+.18f,0), $"热源 {Result.models[Model].powerW*Factor:F2} W  /  环境 25 °C", green);
            int best = PeakFace();
            var data = Result.models[Model];
            Vector3 peak = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                int n = data.tri[best*3+i];
                peak += (data.Point(n) - center + (Deformed ? data.Displacement(n)*Factor*Magnification : Vector3.zero)) * normalization / 3;
            }
            float value = data.FaceValue(Field, best, data.tri[best*3]);
            for (int i = 1; i < 3; i++)
            {
                float candidate = data.FaceValue(Field, best, data.tri[best*3+i]);
                if (Mathf.Abs(candidate) > Mathf.Abs(value)) value = candidate;
            }
            AddNote(peak + Vector3.up * .12f, $"极值 {Scaled(value):G5} {WorkshopAnalysisResult.Unit(Field)}", new Color(1,.85f,.35f));
            UpdateAnnotationPositions();
        }

        float Scaled(float value) => Field == 2 ? 25 + (value-25)*Factor : value*Factor;

        int PeakFace()
        {
            var m = Result.models[Model];
            int best = 0; float peak = -1;
            foreach (int f in displayedFaces)
                for (int i = 0; i < 3; i++)
                {
                    float v = Mathf.Abs(m.FaceValue(Field,f,m.tri[f*3+i]));
                    if (v > peak) { peak = v; best = f; }
                }
            return best;
        }

        public void FocusPeak()
        {
            if (FractureActive) { FocusDefect(); return; }
            var m = Result.models[Model];
            int face = PeakFace();
            Vector3 point = Vector3.zero;
            for (int i = 0; i < 3; i++)
            {
                int node = m.tri[face*3+i];
                point += (m.Point(node)-center + (Deformed ? m.Displacement(node)*Factor*Magnification : Vector3.zero)) * (2.8f/size/3);
            }
            cameraFocus = scene.transform.InverseTransformPoint(body.transform.TransformPoint(point));
            zoom = 1.5f;
            PositionCamera();
        }

        public void ResetView()
        {
            cameraFocus = Vector3.zero; zoom = DefaultZoom;
            body.transform.localRotation = FractureActive ? Quaternion.Euler(-32,-30,-10) : Quaternion.Euler(22,-32,-12);
            PositionCamera();
        }

        public void Preset(int axis)
        {
            cameraFocus = Vector3.zero; zoom = DefaultZoom;
            body.transform.localRotation = axis == 0 ? Quaternion.identity :
                axis == 1 ? Quaternion.Euler(90,0,0) : Quaternion.Euler(0,90,0);
        }

        void UpdateAnnotationPositions()
        {
            if (annotations) annotations.SetActive(ShowAnnotations && !FractureActive);
            foreach (var n in notes)
            {
                Vector3 vp = viewCamera.WorldToViewportPoint(body.transform.TransformPoint(n.position));
                bool shown = ShowAnnotations && !FractureActive && vp.z > 0 && vp.x > 0 && vp.x < 1 && vp.y > 0 && vp.y < 1;
                n.text.enabled = shown;
                var r = n.text.rectTransform;
                r.anchoredPosition = new Vector2(Mathf.Clamp(vp.x*rect.rect.width,119,rect.rect.width-119),
                    Mathf.Clamp(vp.y*rect.rect.height,12,rect.rect.height-12));
            }
        }

        void AddProbe(int face, Vector3 position, Vector3 displayedPosition, Vector3 displacement, float temperature)
        {
            if (samples.Count == 3) ClearProbes();
            if (Measuring && samples.Count > 0 && probeObjects)
                Stroke(probeObjects.transform,"Measurement span",Color.white,samples[samples.Count-1].displayed,displayedPosition);
            samples.Add(new Sample { face = face, position = position, displacement = displacement, temperature = temperature, displayed = displayedPosition });
            if (!probeObjects)
            {
                probeObjects = new GameObject("Measurement probes");
                probeObjects.transform.SetParent(body.transform, false);
            }
            float d = .026f;
            Stroke(probeObjects.transform, "Probe cross", Color.white, displayedPosition-Vector3.right*d,displayedPosition+Vector3.right*d);
            Stroke(probeObjects.transform, "Probe cross", Color.white, displayedPosition-Vector3.up*d,displayedPosition+Vector3.up*d);
            var m = Result.models[Model];
            string values = "";
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                values += $"P{i+1} xyz=({s.position.x*1000:F1},{s.position.z*1000:F1},{s.position.y*1000:F1}) mm  ";
                values += Result.analysis == "thermal" ? $"T={25+(s.temperature-25)*Factor:F2} °C  " :
                    $"S={m.stress[s.face]*Factor:F3} MPa  U={s.displacement.magnitude*1000*Factor:F5} mm  ";
                if (i < samples.Count-1) values += "\n";
            }
            if (Measuring && samples.Count >= 2)
            {
                var s = samples[samples.Count-2];
                float distance = Vector3.Distance(s.position,position)*1000;
                values += $"\n两点原始几何距离 = {distance:F4} mm";
            }
            Probe?.Invoke(values);
        }

        public void ClearProbes()
        {
            samples.Clear();
            if (probeObjects) { probeObjects.SetActive(false); Destroy(probeObjects); }
            Probe?.Invoke("探针：—");
        }

        public string ProbeCsv()
        {
            if (FractureActive) return "# Kinematic fracture preview has no solved field samples.\n";
            var csv = new System.Text.StringBuilder("model,factor,point,x_mm,y_mm,z_mm,mises_MPa,ux_mm,uy_mm,uz_mm,temperature_C\n");
            var m = Result.models[Model];
            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];
                csv.Append(System.FormattableString.Invariant(
                    $"{Model},{Factor},{i+1},{s.position.x*1000},{s.position.z*1000},{s.position.y*1000},"));
                csv.AppendLine(Result.analysis == "thermal" ?
                    System.FormattableString.Invariant($",,,,{25+(s.temperature-25)*Factor}") :
                    System.FormattableString.Invariant(
                        $"{m.stress[s.face]*Factor},{s.displacement.x*1000*Factor},{s.displacement.z*1000*Factor},{s.displacement.y*1000*Factor},"));
            }
            return csv.ToString();
        }
    }
}
