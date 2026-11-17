using UnityEngine;

namespace WorkshopDemo
{
    internal sealed partial class WorkshopLocalFaultPresenter
    {
        // Pooled teaching glyphs share one material. They never modify a device measurement.
        readonly LineRenderer[] mechanismLines = new LineRenderer[12];
        Material mechanismMaterial;
        Vector3 surfacePoint, surfaceNormal;
        bool surfaceAvailable;
        Vector3 raceFrontLocal;
        float raceInnerRadiusM;
        int usedLines;
        static readonly Color Reference = new Color(.18f, .86f, .76f);
        static readonly Color Abnormal = new Color(1, .56f, .12f);
        static readonly Color Danger = new Color(1, .18f, .12f);
        public int ActiveMechanismLines => usedLines;
        public bool TeachingVisible = true;

        void CacheInletRestriction()
        {
            if (inletScreen) return;
            if (!bindings.TryGetValue("pump.suction.bore", out suction))
                throw new System.InvalidOperationException("Pump suction bore binding is missing");
            var filter = suction.Surface.GetComponent<MeshFilter>();
            var mesh = filter.sharedMesh;
            if (!mesh.isReadable) throw new System.InvalidOperationException("Pump suction bore mesh is unreadable");
            var center = suction.Surface.TransformPoint(mesh.bounds.center);
            var axis = model.transform.right.normalized;
            float scale = Mathf.Abs(model.transform.lossyScale.x);
            float radius = float.PositiveInfinity, halfDepth = 0;
            foreach (var vertex in mesh.vertices)
            {
                var offset = suction.Surface.TransformPoint(vertex) - center;
                radius = Mathf.Min(radius, Vector3.ProjectOnPlane(offset, axis).magnitude);
                halfDepth = Mathf.Max(halfDepth, Mathf.Abs(Vector3.Dot(offset, axis)));
            }
            if (radius < .01f * scale || float.IsInfinity(radius))
                throw new System.InvalidOperationException("Pump suction bore dimensions are invalid");
            inletRadiusM = radius / scale * .97f;
            // The added teaching strainer sits behind the existing raised flange face, inside its real bore.
            center += axis * (halfDepth + .004f * scale);
            inletCenterLocal = model.transform.InverseTransformPoint(center);
            var shader = suction.Renderers[0].sharedMaterial.shader;
            screenMaterial = new Material(shader) { name = "Teaching inlet strainer steel" };
            screenMaterial.SetColor("_BaseColor", new Color(.39f, .43f, .42f));
            screenMaterial.SetFloat("_Metallic", .7f);
            screenMaterial.SetFloat("_Smoothness", .35f);
            screenMaterial.SetFloat("_Cull", 0);
            depositMaterial = new Material(shader) { name = "Teaching inlet retained debris" };
            depositMaterial.SetColor("_BaseColor", new Color(.22f, .28f, .12f));
            depositMaterial.SetFloat("_Smoothness", .15f);
            depositMaterial.SetFloat("_Cull", 0);
            var vertices = new System.Collections.Generic.List<Vector3>();
            var triangles = new System.Collections.Generic.List<int>();
            float wireHalfWidth = .00065f;
            for (int i = -9; i <= 9; i++)
            {
                float across = inletRadiusM * i / 10;
                float length = Mathf.Sqrt(inletRadiusM * inletRadiusM -
                    Mathf.Pow(Mathf.Abs(across) + wireHalfWidth, 2));
                for (int orientation = 0; orientation < 2; orientation++)
                {
                    int first = vertices.Count;
                    for (int corner = 0; corner < 4; corner++)
                    {
                        float a = across + (corner == 0 || corner == 3 ? -wireHalfWidth : wireHalfWidth);
                        float b = corner < 2 ? -length : length;
                        vertices.Add(orientation == 0 ? new Vector3(a, b, 0) : new Vector3(b, a, -.0002f));
                    }
                    triangles.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                }
            }
            screenMesh = new Mesh { name = "Teaching inlet strainer, original bore dimensions" };
            screenMesh.SetVertices(vertices);
            screenMesh.SetTriangles(triangles, 0);
            screenMesh.RecalculateNormals();
            screenMesh.RecalculateBounds();
            depositMesh = new Mesh { name = "Teaching inlet obstruction, preset sector" };
            var depositTriangles = new int[64 * 3];
            for (int i = 0; i < 64; i++)
            {
                depositTriangles[i * 3] = 0;
                depositTriangles[i * 3 + 1] = i + 1;
                depositTriangles[i * 3 + 2] = i + 2;
            }
            depositMesh.vertices = depositVertices;
            depositMesh.triangles = depositTriangles;
            inletScreen = InletPart("Teaching inlet strainer insert", screenMesh, screenMaterial, center);
            inletDeposit = InletPart("Teaching inlet retained debris", depositMesh, depositMaterial, center);
        }

        GameObject InletPart(string name, Mesh mesh, Material material, Vector3 center)
        {
            var node = new GameObject(name);
            node.transform.SetParent(model.transform, false);
            node.transform.SetPositionAndRotation(center,
                Quaternion.LookRotation(-model.transform.right, model.transform.up));
            node.layer = model.gameObject.layer;
            node.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.AddComponent<MeshRenderer>().sharedMaterial = material;
            node.SetActive(false);
            return node;
        }

        void ApplyInletRestriction(float activity)
        {
            inletScreen.SetActive(true);
            inletDeposit.SetActive(activity > 0);
            if (Mathf.Approximately(inletActivity, activity)) return;
            inletActivity = activity;
            depositVertices[0] = new Vector3(0, 0, .001f);
            for (int i = 0; i <= 64; i++)
            {
                float angle = Mathf.Lerp(-150, -150 + 234 * activity, i / 64f) * Mathf.Deg2Rad;
                float radius = inletRadiusM * (.94f + .035f * Mathf.Sin(i * 1.7f));
                depositVertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, .001f);
            }
            depositMesh.vertices = depositVertices;
            depositMesh.RecalculateNormals();
            depositMesh.RecalculateBounds();
        }

        void CacheBladeWear()
        {
            if (wornBlade) return;
            wornBladeFilter = binding.Surface.GetComponent<MeshFilter>();
            intactBlade = wornBladeFilter.sharedMesh;
            if (!intactBlade.isReadable) throw new System.InvalidOperationException("Pump blade mesh is not readable");
            wornBlade = Object.Instantiate(intactBlade);
            wornBlade.name = "P-IMPELLER preset outer-edge loss, maximum 2 mm";
            bladeVertices = intactBlade.vertices;
            bladeWear = new Vector3[bladeVertices.Length];
            bladeOutput = new Vector3[bladeVertices.Length];
            var axle = binding.Surface.parent.position;
            float outerRadius = 0;
            var axis = model.transform.right.normalized;
            foreach (var v in bladeVertices)
                outerRadius = Mathf.Max(outerRadius, Vector3.ProjectOnPlane(
                    binding.Surface.TransformPoint(v) - axle, axis).magnitude);
            float scale = Mathf.Abs(model.transform.lossyScale.x);
            for (int i = 0; i < bladeVertices.Length; i++)
            {
                var radial = Vector3.ProjectOnPlane(binding.Surface.TransformPoint(bladeVertices[i]) - axle, axis);
                float weight = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(outerRadius * .78f, outerRadius, radial.magnitude));
                bladeWear[i] = binding.Surface.InverseTransformVector(-radial.normalized * (.002f * scale * weight));
            }
        }

        void ApplyBladeWear(float activity)
        {
            if (!wornBlade || Mathf.Approximately(bladeActivity, activity)) return;
            bladeActivity = activity;
            wornBladeFilter.sharedMesh = activity <= 0 ? intactBlade : wornBlade;
            if (activity <= 0) return;
            for (int i = 0; i < bladeOutput.Length; i++)
                bladeOutput[i] = bladeVertices[i] + bladeWear[i] * activity;
            wornBlade.vertices = bladeOutput;
            wornBlade.RecalculateNormals();
            wornBlade.RecalculateBounds();
        }

        void CacheRaceWear()
        {
            if (wornRace) return;
            foreach (var renderer in pumpContext)
            {
                if (!renderer.name.StartsWith("PMP_06_SealBearing_BearingOuterRace",
                    System.StringComparison.Ordinal)) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                if (!raceFilter || model.transform.InverseTransformPoint(filter.transform.position).x <
                    model.transform.InverseTransformPoint(raceFilter.transform.position).x) raceFilter = filter;
            }
            if (!raceFilter || !raceFilter.sharedMesh.isReadable)
                throw new System.InvalidOperationException("Pump bearing outer race is missing or unreadable");
            intactRace = raceFilter.sharedMesh;
            wornRace = Object.Instantiate(intactRace);
            wornRace.name = "P-BEAR preset local race surface loss, maximum 1 mm";
            raceVertices = intactRace.vertices;
            raceWear = new Vector3[raceVertices.Length];
            raceOutput = new Vector3[raceVertices.Length];
            var center = raceFilter.transform.TransformPoint(intactRace.bounds.center);
            var axis = model.transform.right.normalized;
            var direction = -model.transform.forward.normalized;
            float innerRadius = float.PositiveInfinity;
            foreach (var v in raceVertices)
                innerRadius = Mathf.Min(innerRadius, Vector3.ProjectOnPlane(
                    raceFilter.transform.TransformPoint(v) - center, axis).magnitude);
            float scale = Mathf.Abs(model.transform.lossyScale.x);
            float halfWidth = 0;
            foreach (var v in raceVertices)
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(Vector3.Dot(
                    raceFilter.transform.TransformPoint(v) - center, axis)));
            raceFrontLocal = raceFilter.transform.InverseTransformPoint(center - axis * (halfWidth + .0006f * scale));
            raceInnerRadiusM = innerRadius / scale;
            for (int i = 0; i < raceVertices.Length; i++)
            {
                var radial = Vector3.ProjectOnPlane(
                    raceFilter.transform.TransformPoint(raceVertices[i]) - center, axis);
                float angleWeight = Mathf.SmoothStep(0, 1,
                    Mathf.InverseLerp(Mathf.Cos(18 * Mathf.Deg2Rad), 1, Vector3.Dot(radial.normalized, direction)));
                float surfaceWeight = 1 - Mathf.SmoothStep(0, 1,
                    Mathf.InverseLerp(innerRadius + .0005f * scale, innerRadius + .002f * scale, radial.magnitude));
                raceWear[i] = raceFilter.transform.InverseTransformVector(
                    radial.normalized * (.001f * scale * angleWeight * surfaceWeight));
            }
        }

        void ApplyRaceWear(float activity)
        {
            if (!wornRace || Mathf.Approximately(raceActivity, activity)) return;
            raceActivity = activity;
            raceFilter.sharedMesh = activity <= 0 ? intactRace : wornRace;
            if (activity <= 0) return;
            for (int i = 0; i < raceOutput.Length; i++)
                raceOutput[i] = raceVertices[i] + raceWear[i] * activity;
            wornRace.vertices = raceOutput;
            wornRace.RecalculateNormals();
            wornRace.RecalculateBounds();
        }

        void DrawRaceInspection(WorkshopFaultFrame frame, float scale)
        {
            var center = raceFilter.transform.TransformPoint(raceFrontLocal);
            var front = -model.transform.right.normalized;
            var radial = -model.transform.forward.normalized;
            var up = model.transform.up.normalized;
            float radius = raceInnerRadiusM * scale;
            float width = .00045f * scale;
            var reference = Stroke(Reference, 25, width);
            var defect = Stroke(Abnormal, 25, width * 1.5f);
            for (int i = 0; i < 25; i++)
            {
                float angle = Mathf.Lerp(-18, 18, i / 24f) * Mathf.Deg2Rad;
                var direction = radial * Mathf.Cos(angle) + up * Mathf.Sin(angle);
                float weight = Mathf.SmoothStep(0, 1,
                    Mathf.InverseLerp(Mathf.Cos(18 * Mathf.Deg2Rad), 1, Mathf.Cos(angle)));
                reference.SetPosition(i, center + direction * radius);
                defect.SetPosition(i, center + direction * (radius + .001f * scale * weight * frame.Activity));
            }
            var point = center + radial * (radius + .001f * scale * frame.Activity);
            var leader = (front * .018f + radial * .018f) * scale;
            Arrow(point + leader, -leader, up, Abnormal, width * 1.5f);
        }

        void CreateMechanismOverlays()
        {
            mechanismMaterial = new Material(Shader.Find("Sprites/Default")) { name = "Local teaching vectors" };
            for (int i = 0; i < mechanismLines.Length; i++)
            {
                var node = new GameObject("Local teaching vector " + i);
                node.transform.SetParent(model.transform, false);
                var line = node.AddComponent<LineRenderer>();
                line.sharedMaterial = mechanismMaterial;
                line.useWorldSpace = true;
                line.numCapVertices = 2;
                line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                line.receiveShadows = false;
                node.SetActive(false);
                mechanismLines[i] = line;
            }
        }

        void CacheSurface()
        {
            surfacePoint = Vector3.zero;
            surfaceNormal = Vector3.back;
            surfaceAvailable = false;
            if (binding.Settings.surfaceCached)
            {
                surfacePoint = binding.Settings.surfaceLocalM;
                surfaceNormal = binding.Settings.surfaceNormalLocal;
                surfaceAvailable = true;
                return;
            }
            var filter = binding.Surface.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh || !filter.sharedMesh.isReadable) return;
            var mesh = filter.sharedMesh;
            var vertices = mesh.vertices;
            var normals = mesh.normals;
            Vector3 desired = binding.Surface.InverseTransformDirection(
                Normal == Vector3.zero ? model.transform.TransformDirection(Vector3.back) : Normal).normalized;
            // Derive a visible surface location from actual vertices, never the solid's center.
            float best = float.NegativeInfinity;
            for (int i = 0; i < vertices.Length; i++)
            {
                float score = Vector3.Dot(vertices[i] - mesh.bounds.center, desired);
                if (score <= best) continue;
                best = score;
                surfacePoint = vertices[i];
                surfaceNormal = normals.Length == vertices.Length ? normals[i] : desired;
                surfaceAvailable = true;
            }
        }

        void ResetMechanismOverlays()
        {
            usedLines = 0;
            foreach (var line in mechanismLines)
            {
                if (!line) continue;
                line.gameObject.SetActive(false);
                line.positionCount = 0;
            }
        }

        LineRenderer Stroke(Color color, int points, float width)
        {
            var line = mechanismLines[usedLines++];
            line.gameObject.SetActive(true);
            line.startColor = line.endColor = color;
            line.widthMultiplier = width;
            line.positionCount = points;
            return line;
        }

        void Segment(Vector3 a, Vector3 b, Color color, float width)
        {
            var line = Stroke(color, 2, width);
            line.SetPosition(0, a); line.SetPosition(1, b);
        }

        void Arrow(Vector3 start, Vector3 vector, Vector3 side, Color color, float width)
        {
            var end = start + vector;
            var line = Stroke(color, 5, width);
            line.SetPosition(0, start); line.SetPosition(1, end);
            line.SetPosition(2, end - vector * .25f + side * vector.magnitude * .14f);
            line.SetPosition(3, end);
            line.SetPosition(4, end - vector * .25f - side * vector.magnitude * .14f);
        }

        void Ring(Vector3 center, Vector3 right, Vector3 up, float radius, Color color, float width)
        {
            var line = Stroke(color, 25, width);
            for (int i = 0; i < 25; i++)
            {
                float a = i * Mathf.PI / 12;
                line.SetPosition(i, center + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius);
            }
        }

        void ApplyMechanismOverlays(WorkshopFaultFrame f)
        {
            ResetMechanismOverlays();
            if (f.Progress <= 0 || !TeachingVisible || !surfaceAvailable ||
                (spec.Effect == "thermal" && !ThermalVisible)) return;
            float scale = Mathf.Abs(model.transform.lossyScale.x);
            if (SectionOpen && f.Code == "P-BEAR")
            {
                DrawRaceInspection(f, scale);
                return;
            }
            float radius = Mathf.Clamp(binding.Renderers[0].bounds.size.magnitude * .32f, .018f * scale, .16f * scale);
            float width = Mathf.Max(.0008f * scale, radius * .025f);
            Vector3 normal = binding.Settings.anchorCalibrated ? binding.Normal :
                binding.Surface.TransformDirection(surfaceNormal).normalized;
            if (normal.sqrMagnitude < .5f) normal = model.transform.TransformDirection(Vector3.back);
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
            if (up.sqrMagnitude < .5f) up = Vector3.ProjectOnPlane(model.transform.forward, normal).normalized;
            Vector3 right = Vector3.Cross(up, normal).normalized;
            Vector3 anchor = binding.Settings.anchorCalibrated ? binding.Anchor : binding.Surface.TransformPoint(surfacePoint);
            anchor += normal * width * 2;
            if (f.Code == "P-INLET")
            {
                normal = -model.transform.right.normalized;
                up = model.transform.up.normalized;
                right = Vector3.Cross(up, normal).normalized;
                anchor = model.transform.TransformPoint(inletCenterLocal) +
                    normal * (.008f * scale) + right * (.13f * scale);
                radius = .035f * scale;
                width = .0008f * scale;
            }
            else if (SectionOpen && f.Code == "P-IMPELLER")
            {
                // Inspect the actual blade from the inlet side after removing the front cover.
                normal = -model.transform.right.normalized;
                up = model.transform.up.normalized;
                right = Vector3.Cross(up, normal).normalized;
                var b = binding.Renderers[0].bounds;
                float depth = Vector3.Dot(b.extents, new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z)));
                anchor = b.center + normal * (depth + .0015f * scale);
                radius = .038f * scale;
                width = .0008f * scale;
            }
            else if (f.Code == "P-ALIGN")
            {
                normal = Normal.normalized;
                var b = binding.Renderers[0].bounds;
                foreach (var renderer in couplingContext)
                    b.Encapsulate(renderer.bounds);
                float depth = Vector3.Dot(b.extents, new Vector3(Mathf.Abs(normal.x),
                    Mathf.Abs(normal.y), Mathf.Abs(normal.z)));
                anchor = b.center + normal * (depth + .02f * scale);
                radius = Mathf.Clamp(b.size.magnitude * .4f, .04f * scale, .16f * scale);
                width = Mathf.Max(.0008f * scale, radius * .025f);
                right = Vector3.ProjectOnPlane(model.transform.right, normal).normalized;
                up = Vector3.Cross(normal, right).normalized;
            }
            else if (f.Code == "TK-VENT")
                anchor += normal * radius * .75f;
            float p = f.Activity;

            if (spec.Effect == "restriction" || f.Code == "PLC-HOT" || f.Code == "P-DRY")
            {
                // Paired same-scale flow arrows distinguish inlet resistance from shaft speed.
                for (int i = -1; i <= 1; i++)
                {
                    var start = anchor + right * radius * i * .5f - up * radius;
                    Arrow(start, up * radius * 1.6f, right, Reference, width);
                    Arrow(start + normal * radius * .18f, up * radius * Mathf.Lerp(1.6f, .25f, p),
                        right, Abnormal, width * 1.5f);
                }
                if (f.Code != "P-DRY")
                    for (int i = 0; i < 3; i++)
                        Segment(anchor + right * radius * (-.8f + i * .65f) - up * radius * .3f,
                            anchor + right * radius * (-.4f + i * .65f) + up * radius * .3f,
                            Abnormal, width * 2 * p);
            }
            else if (spec.Effect == "signal")
            {
                // Cyan = independent reference/command; orange = current indication/feedback.
                float actual = f.Code == "PLC-LOW" ? f.Voltage / 24 :
                    f.Code == "PLC-COMM" ? f.Communication / 100 :
                    f.Code == "PLC-SENSOR" ? Mathf.Lerp(.5f, .85f, p) :
                    f.Code == "TK-SENSOR" ? Mathf.Lerp(.65f, .91f, p) :
                    f.Code == "PLC-OVERLOAD" ? Mathf.Lerp(.65f, 1, p) : 1 - p;
                float reference = f.Code == "PLC-SENSOR" ? .5f : f.Code == "TK-SENSOR" ? .65f :
                    f.Code == "PLC-OVERLOAD" ? .65f : 1;
                Segment(anchor - right * radius, anchor + right * radius, Reference, width);
                Arrow(anchor - right * radius * .45f, up * radius * reference, right, Reference, width * 1.6f);
                Arrow(anchor + right * radius * .45f, up * radius * Mathf.Max(.025f, actual),
                    right, f.Code == "PLC-OVERLOAD" ? Danger : Abnormal, width * 1.6f);
                if ((f.Code == "PLC-COMM" || f.Code == "PLC-DRIVE" || f.Code == "PLC-IO") && p > .6f)
                {
                    Segment(anchor - right * radius * .2f, anchor + right * radius * .2f + up * radius * .4f, Danger, width * 2);
                    Segment(anchor + right * radius * .2f, anchor - right * radius * .2f + up * radius * .4f, Danger, width * 2);
                }
            }
            else if (spec.Effect == "orbit" || spec.Effect == "offset")
            {
                Ring(anchor, right, up, radius * .7f, Reference, width);
                if (f.Code == "P-ALIGN")
                {
                    Segment(anchor - right * radius, anchor + right * radius, Reference, width);
                    var offset = up * radius * .25f * p;
                    Segment(anchor - right * radius + offset, anchor + right * radius + offset, Abnormal, width * 1.8f);
                }
                else if (spec.Effect == "offset")
                    Arrow(anchor, -up * radius * p, right, Abnormal, width * 2);
                else
                {
                    float phase = f.Code == "F-IMBAL" ? rotorPhase :
                        f.Seconds * Mathf.PI * 2 * Mathf.Min(f.Rpm / 60, 8);
                    var direction = f.Code == "F-IMBAL" ?
                        moving.parent.TransformVector(rotorHome *
                            new Vector3(Mathf.Cos(phase), Mathf.Sin(phase) * .7f, 0)).normalized :
                        right * Mathf.Sin(phase) + up * Mathf.Cos(phase);
                    var vector = f.Running ? direction * radius * p * .6f : Vector3.zero;
                    Arrow(anchor, vector, up, Abnormal, width * 1.8f);
                }
            }
            else if (spec.Effect == "corrosion" || spec.Effect == "wear" || spec.Effect == "erosion")
            {
                // Local inspection hatching marks a region, never a computed thickness or crack.
                for (int i = 0; i < 5; i++)
                {
                    float x = (i - 2) * radius * .22f;
                    Segment(anchor + right * x - up * radius * .35f,
                        anchor + right * (x + radius * .3f) + up * radius * .35f,
                        Abnormal, width * (1 + p));
                }
            }
            else if (spec.Effect == "pressure")
            {
                Ring(anchor, right, up, radius * .8f, Reference, width);
                float angle = Mathf.Lerp(-120, 120, Mathf.InverseLerp(0, 1, f.Pressure));
                Vector3 needle = right * Mathf.Sin(angle * Mathf.Deg2Rad) + up * Mathf.Cos(angle * Mathf.Deg2Rad);
                Arrow(anchor, needle * radius * .7f, right, Abnormal, width * 1.8f);
            }
            else if (spec.Effect == "reverse")
            {
                Ring(anchor, right, up, radius, Reference, width);
                Arrow(anchor + right * radius, -up * radius * .75f, right, Reference, width);
                Arrow(anchor - right * radius, -up * radius * .75f, right, Abnormal, width * 2);
            }
            else if (spec.Effect == "leak")
                Ring(anchor, right, up, radius * .25f * p, new Color(.08f, .56f, .72f), width * 3);
            else if (spec.Effect == "thermal" && ThermalVisible)
                for (int i = 0; i < 3; i++)
                    Ring(anchor + normal * width * i, right, up, radius * (.35f + .2f * i),
                        Color.Lerp(Abnormal, Danger, Mathf.InverseLerp(35, 100, f.Temperature)), width);
        }
    }
}
