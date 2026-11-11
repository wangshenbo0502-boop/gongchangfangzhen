using UnityEngine;

namespace WorkshopDemo
{
    public sealed partial class WorkshopAnalysisView
    {
        public const float FractureInitiation = .55f;
        public const float FractureDuration = 18;
        public bool FractureActive { get; private set; }
        public bool FracturePlaying { get; set; }
        public float FractureProgress { get; private set; }
        public float FractureGrowth => Mathf.SmoothStep(0, 1,
            Mathf.InverseLerp(FractureInitiation, 1, FractureProgress));
        public float FractureOpeningMm => fractureBounds.size.x * .04f * FractureGrowth * 1000;
        public float FractureDepthMm => fractureDepth * FractureGrowth * 1000;
        public float FractureRemainingMm => fractureBounds.size.y * 1000 - FractureDepthMm;
        public float FractureVisualScale { get; private set; }
        public string FractureStage => FractureProgress >= 1 ? "最终裂口 · 保持" :
            FractureProgress < FractureInitiation ? "逐渐受载 · 弯曲变形" :
            FractureProgress < FractureInitiation + .04f ? "预设起裂点 · 裂口萌生" : "裂口扩展 · 两侧张开";

        Mesh fractureMesh;
        Vector3[] fracturePoints, fractureNodes, fractureVertices, fractureNormals;
        Vector3 fractureEndDisplacement;
        int[] fractureVertexNodes;
        Bounds fractureBounds;
        float fractureDepth, fractureNormalization;
        GameObject fractureDimensions;
        LineRenderer fractureMouthLine, fractureDepthLine;

        public bool BeginFracture()
        {
            if (Result.variable != "notch" || Result.analysis != "structural") return false;
            if (!fractureMesh) PrepareFractureMesh();
            ClearProbes();
            FractureActive = true;
            FracturePlaying = true;
            surfaceCollider.enabled = false;
            wireRenderer.enabled = false;
            body.GetComponent<MeshFilter>().sharedMesh = fractureMesh;
            material.SetFloat("_Contours", 0);
            material.SetFloat("_SurfaceShade", 1);
            material.SetFloat("_Ambient", .32f);
            ResetView();
            SetFractureProgress(0);
            return true;
        }

        void PrepareFractureMesh()
        {
            var m = Result.models[1];
            fractureBounds = new Bounds(Result.models[0].Point(0), Vector3.zero);
            for (int i = 0; i < Result.models[0].nodes; i++)
                fractureBounds.Encapsulate(Result.models[0].Point(i));
            fractureNormalization = 2.8f / fractureBounds.size.magnitude;
            fractureDepth = Result.inputValues[1];
            FractureVisualScale = Mathf.Clamp(.14f * fractureBounds.size.x /
                Mathf.Max(1e-9f, m.maxDisplacement / 1000), 1, 20000);
            fracturePoints = new Vector3[m.nodes];
            fractureNodes = new Vector3[m.nodes];
            int endNodes = 0;
            for (int i = 0; i < m.nodes; i++)
            {
                fracturePoints[i] = m.Point(i);
                if (Mathf.Abs(fracturePoints[i].x-fractureBounds.max.x) < 1e-7f)
                {
                    fractureEndDisplacement += m.Displacement(i);
                    endNodes++;
                }
            }
            fractureEndDisplacement /= Mathf.Max(1,endNodes);
            int count = m.tri.Length;
            fractureVertexNodes = new int[count];
            fractureVertices = new Vector3[count];
            fractureNormals = new Vector3[count];
            var colors = new Color[count];
            var indices = new int[count];
            float left = fractureBounds.min.x + fractureBounds.size.x * .16f;
            float right = fractureBounds.min.x + fractureBounds.size.x * .20f;
            float floor = fractureBounds.max.y - fractureDepth;
            for (int i = 0; i < count; i += 3)
            {
                bool leftWall = true, rightWall = true;
                for (int c = 0; c < 3; c++)
                {
                    int n = m.tri[i + (c == 0 ? 0 : 3 - c)];
                    fractureVertexNodes[i+c] = n;
                    Vector3 p = fracturePoints[n];
                    leftWall &= Mathf.Abs(p.x - left) < 1e-7f && p.y >= floor - 1e-7f;
                    rightWall &= Mathf.Abs(p.x - right) < 1e-7f && p.y >= floor - 1e-7f;
                }
                Color color = leftWall || rightWall ? new Color(.96f, .37f, .16f) : new Color(.63f, .71f, .75f);
                for (int c = 0; c < 3; c++) { colors[i+c] = color; indices[i+c] = i+c; }
            }
            fractureMesh = new Mesh { name = "Kinematic opening crack - not a fracture solution",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            fractureMesh.MarkDynamic();
            fractureMesh.vertices = fractureVertices;
            fractureMesh.colors = colors;
            fractureMesh.triangles = indices;
            fractureDimensions = new GameObject("Opening crack dimensions");
            fractureDimensions.transform.SetParent(body.transform, false);
            Stroke(fractureDimensions.transform, "Mouth opening", Color.yellow, Vector3.zero, Vector3.zero);
            Stroke(fractureDimensions.transform, "Crack depth", new Color(1,.5f,.2f), Vector3.zero, Vector3.zero);
            fractureMouthLine = fractureDimensions.transform.GetChild(0).GetComponent<LineRenderer>();
            fractureDepthLine = fractureDimensions.transform.GetChild(1).GetComponent<LineRenderer>();
            fractureMouthLine.startWidth = fractureMouthLine.endWidth = .003f;
            fractureDepthLine.startWidth = fractureDepthLine.endWidth = .003f;
        }

        // Collapse the premeshed slot onto a seam, then separate its lips into a V.
        // Shared nodes follow the same map; the lower ligament stays connected.
        Vector3 CrackPoint(Vector3 point, float growth)
        {
            float xmin = fractureBounds.min.x, xmax = fractureBounds.max.x;
            float a = xmin + fractureBounds.size.x * .16f, b = xmin + fractureBounds.size.x * .20f;
            float mid = (a+b)/2, floor = fractureBounds.max.y - fractureDepth;
            float upper = Mathf.Clamp01((point.y-floor)/fractureDepth);
            float ligament = Mathf.Clamp01((point.y-fractureBounds.min.y) /
                (floor-fractureBounds.min.y));
            float opening = (b-a) * growth * upper;
            float x = point.x <= a ? Mathf.Lerp(xmin, mid-opening/2, (point.x-xmin)/(a-xmin)) :
                point.x >= b ? Mathf.Lerp(mid+opening/2, xmax, (point.x-b)/(xmax-b)) :
                Mathf.Lerp(mid-opening/2, mid+opening/2, (point.x-a)/(b-a));
            point.x = Mathf.Lerp(point.x, x, ligament);
            float currentFloor = fractureBounds.max.y - fractureDepth*growth;
            point.y = point.y >= floor ? Mathf.Lerp(currentFloor, fractureBounds.max.y, upper) :
                Mathf.Lerp(fractureBounds.min.y, currentFloor, ligament);
            return point;
        }

        public void FractureLandmarks(out Vector3 left, out Vector3 right, out Vector3 tip)
        {
            float x = fractureBounds.min.x, length = fractureBounds.size.x;
            float y = fractureBounds.max.y, z = fractureBounds.min.z;
            left = CrackPoint(new Vector3(x+length*.16f,y,z), FractureGrowth);
            right = CrackPoint(new Vector3(x+length*.20f,y,z), FractureGrowth);
            tip = CrackPoint(new Vector3(x+length*.18f,y-fractureDepth,z), FractureGrowth);
        }

        public void SetFractureProgress(float progress)
        {
            if (!FractureActive) return;
            FractureProgress = Mathf.Clamp01(progress);
            if (FractureProgress >= 1) FracturePlaying = false;
            float growth = FractureGrowth;
            float bend = Mathf.SmoothStep(0, 1, FractureProgress / FractureInitiation) * (.7f + .3f*growth);
            for (int i = 0; i < fracturePoints.Length; i++)
            {
                Vector3 p = CrackPoint(fracturePoints[i], growth);
                fractureNodes[i] = (p - fractureBounds.center + FractureBendOffset(p,bend)) * fractureNormalization;
            }
            for (int i = 0; i < fractureVertices.Length; i++)
                fractureVertices[i] = fractureNodes[fractureVertexNodes[i]];
            for (int i = 0; i < fractureVertices.Length; i += 3)
            {
                Vector3 n = Vector3.Cross(fractureVertices[i+1]-fractureVertices[i],
                    fractureVertices[i+2]-fractureVertices[i]).normalized;
                fractureNormals[i] = fractureNormals[i+1] = fractureNormals[i+2] = n;
            }
            fractureMesh.vertices = fractureVertices;
            fractureMesh.normals = fractureNormals;
            fractureMesh.RecalculateBounds();
            float mid = fractureBounds.min.x + fractureBounds.size.x*.18f;
            var at = new Vector3(mid, fractureBounds.max.y-fractureDepth, fractureBounds.min.z);
            Vector3 localBend = FractureBendOffset(CrackPoint(at,growth),bend);
            detailPoint = (CrackPoint(at, growth) + Vector3.up*fractureDepth*growth*.45f -
                fractureBounds.center + localBend) * fractureNormalization;
            detailDirection = new Vector3(.65f,.55f,-1).normalized;
            detailDistance = Mathf.Clamp(fractureBounds.size.x*.18f*fractureNormalization,.40f,.85f);
            FractureLandmarks(out var left, out var right, out var tip);
            Vector3 offset = -fractureBounds.center + Vector3.back * .001f;
            fractureMouthLine.SetPosition(0,(left+FractureBendOffset(left,bend)+offset)*fractureNormalization);
            fractureMouthLine.SetPosition(1,(right+FractureBendOffset(right,bend)+offset)*fractureNormalization);
            Vector3 mouth = (left+right)/2;
            fractureDepthLine.SetPosition(0,(mouth+FractureBendOffset(mouth,bend)+offset)*fractureNormalization);
            fractureDepthLine.SetPosition(1,(tip+FractureBendOffset(tip,bend)+offset)*fractureNormalization);
            fractureDimensions.SetActive(ShowAnnotations && growth > .001f);
        }

        Vector3 FractureBendOffset(Vector3 point, float bend)
        {
            float t = Mathf.Clamp01((point.x-fractureBounds.min.x)/fractureBounds.size.x);
            return fractureEndDisplacement * (t*t*(3-t)/2 * bend * FractureVisualScale);
        }

        public void AdvanceFracture(float seconds)
        {
            if (FractureActive && FracturePlaying)
                SetFractureProgress(FractureProgress + Mathf.Max(0,seconds)/FractureDuration);
        }

        public void EndFracture()
        {
            if (!FractureActive) return;
            FractureActive = FracturePlaying = false;
            body.GetComponent<MeshFilter>().sharedMesh = mesh;
            surfaceCollider.enabled = true;
            if (fractureDimensions) fractureDimensions.SetActive(false);
            Rebuild();
        }

        void DisposeFracture()
        {
            if (fractureMesh) Destroy(fractureMesh);
        }
    }
}
