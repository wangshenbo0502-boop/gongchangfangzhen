using System.Collections.Generic;
using UnityEngine;

namespace WorkshopDemo
{
    public sealed partial class WorkshopEquipmentModel
    {
        sealed class DamagePatch
        {
            public MeshFilter Filter;
            public Vector3[] Rest, Current;
            public int[] Side;
            public Vector3 Size;
            public Material Material;
            public Color Color;
            public int LeftLip, RightLip;
        }
        readonly List<DamagePatch> patches = new List<DamagePatch>();
        WorkshopFault lastFault;
        public const float CrackStart = .55f;
        public float CrackLengthMm { get; private set; }
        public float ThicknessMm { get; private set; }
        public bool Detached => HasOpening && Progress >= .9999f;
        public int AssemblyCount => assemblies.Count;
        public string AssemblyName(int i) => assemblies[i].name;
        public Vector3 AssemblyAnchor(int i) => assemblies[i].position;
        public Transform AssemblyTransform(int i) => assemblies[i];
        public string AssemblyLabel(int i)
        {
            string[] labels = Kind == DeviceKind.Pump ? new[] {"灌浆底座 / 地脚","散热电机 / 接线盒","轴 / 弹性联轴器",
                "可拆安全护罩","剖分泵壳 / 紧固圈","吸排管口 / 压力表","机械密封副"} :
                Kind == DeviceKind.Tank ? new[] {"支腿 / 锚固底板","卷制筒体 / 环焊缝","成形上下封头",
                    "法兰 / 人孔 / 安全阀","液位柱 / 铭牌","检修爬梯"} :
                Kind == DeviceKind.Fan ? new[] {"减振底座 / 支架","卷制风筒 / 法兰","扭曲叶轮 / 轮毂",
                    "可拆进风防护网","后置电机 / 导流支撑"} :
                Kind == DeviceKind.Cabinet ? new[] {"钣金柜体 / 密封","导轨 / 断路器 / PLC","端子排 / 接地排","铰链门 / HMI"} :
                new[] {"原有设备外形","可观察传动连接"};
            return (i+1).ToString("D2")+"  "+labels[i];
        }

        // Two watertight solids share an initially closed, irregular interface.
        // The interface vertices are independent, so propagation opens a genuine hole.
        void DamageBlock(string name, Vector3 origin, Vector3 size, Material material,
            bool curved = false, bool blade = false, bool dynamic = true)
        {
            var vertices = new List<Vector3>();
            var sides = new List<int>();
            var outside = new List<int>();
            var inside = new List<int>();
            var leftLip = -1;
            var rightLip = -1;
            for (int half = 0; half < 2; half++)
            {
                int side = half == 0 ? -1 : 1;
                for (int face = 0; face < 6; face++)
                {
                    int start = vertices.Count;
                    const int resolution = 18;
                    for (int j = 0; j <= resolution; j++)
                        for (int i = 0; i <= resolution; i++)
                        {
                            float u = (float)i/resolution, v = (float)j/resolution;
                            Vector3 t;
                            if (face < 2) t = new Vector3(face,u,v);
                            else if (face < 4) t = new Vector3(u,face-2,v);
                            else t = new Vector3(u,v,face-4);
                            float seam = .43f + .024f*Mathf.Sin(t.y*21) +
                                .012f*Mathf.Sin(t.y*47) + .009f*Mathf.Sin(t.z*13);
                            t.x = half == 0 ? t.x*seam : seam+t.x*(1-seam);
                            Vector3 p = Vector3.Scale(t,size);
                            if (blade)
                            {
                                p.y = (t.y-.5f)*size.y*(1-.37f*t.x)+size.y*.5f;
                                p.z += (t.y-.5f)*size.y*(.55f-.85f*t.x);
                            }
                            p += origin;
                            if (curved) p.z += .67f-Mathf.Sqrt(Mathf.Max(.001f,.67f*.67f-p.x*p.x));
                            vertices.Add(p); sides.Add(side);
                            bool interfaceFace = half == 0 ? face == 1 : face == 0;
                            if (interfaceFace && j == resolution && i == resolution)
                            {
                                if (half == 0) leftLip = vertices.Count-1;
                                else rightLip = vertices.Count-1;
                            }
                        }
                    var indices = (half == 0 ? face == 1 : face == 0) ? inside : outside;
                    // The parameter directions are Y/Z, X/Z, X/Y respectively.
                    bool reverse = face == 0 || face == 3 || face == 4;
                    for (int j = 0; j < resolution; j++)
                        for (int i = 0; i < resolution; i++)
                        {
                            int a = start+j*(resolution+1)+i, b = a+resolution+1;
                            if (reverse) indices.AddRange(new[] {a,b,a+1,a+1,b,b+1});
                            else indices.AddRange(new[] {a,a+1,b,a+1,b+1,b});
                        }
                }
            }
            var mesh = MakeMesh(name,vertices,outside);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(inside,1);
            mesh.RecalculateNormals();
            var mat = Finish(name+" surface",material.GetColor("_BaseColor"),
                material.GetFloat("_Metallic"),material.GetFloat("_Smoothness"));
            var cut = Finish(name+" exposed fracture",new Color(.29f,.30f,.28f),.55f,.18f);
            var part = Part(name,mesh,Vector3.zero,mat,Quaternion.identity);
            part.GetComponent<MeshRenderer>().sharedMaterials = new[] {mat,cut};
            if (!dynamic) return;
            DamageSurface = part.GetComponent<MeshFilter>();
            patches.Add(new DamagePatch { Filter = DamageSurface, Rest = vertices.ToArray(),
                Current = vertices.ToArray(), Side = sides.ToArray(), Size = size,
                Material = mat, Color = mat.GetColor("_BaseColor"), LeftLip = leftLip, RightLip = rightLip });
        }

        public void SetDamage(WorkshopFault fault, float progress)
        {
            Progress = fault == null ? 0 : Mathf.Clamp01(progress);
            string code = fault?.Code ?? "";
            HasOpening = code == "F-CRACK" || code == "P-SEAL" || code == "TK-LEAK" ||
                code == "TK-SUPPORT" || code == "TK-PRESS" || code == "CV-JAM";
            Recipe = HasOpening ? "预设加载：弯曲 → 起裂 → 扩展 → 张开分离" :
                fault?.Visual == FaultVisual.Heat ? "局部热变色示意" :
                fault?.Visual == FaultVisual.Vibration ? "轴端偏移 / 周期摆振示意" :
                fault?.Visual == FaultVisual.Electrical ? "控制指示状态变化，不产生结构裂口" : "工艺异常示意";
            OpeningMm = DeflectionMm = CrackLengthMm = RemainingMm = ThicknessMm = 0;
            for (int k = 0; k < patches.Count; k++)
            {
                var patch = patches[k];
                bool selected = Kind != DeviceKind.Tank ||
                    (code == "TK-SUPPORT" ? k == 0 : k == 1);
                float p = selected ? Progress : 0;
                float bend = Mathf.SmoothStep(0,1,Mathf.Min(p/CrackStart,1));
                float propagation = HasOpening ? Mathf.Clamp01((p-CrackStart)/(1-CrackStart)) : 0;
                float maxBend = patch.Size.x*(HasOpening ? .13f :
                    fault?.Visual == FaultVisual.Vibration ? .018f : 0);
                float gap = patch.Size.x*.19f*propagation*propagation;
                float worn = code == "TK-LEAK" ? Mathf.Lerp(1,.50f,p) : 1;
                Bounds b = new Bounds(patch.Rest[0],Vector3.zero);
                foreach (var point in patch.Rest) b.Encapsulate(point);
                for (int i = 0; i < patch.Rest.Length; i++)
                {
                    Vector3 v = patch.Rest[i];
                    float x = Mathf.Clamp01((v.x-b.min.x)/patch.Size.x);
                    float y = Mathf.Clamp01((v.y-b.min.y)/patch.Size.y);
                    float opening = Mathf.SmoothStep(0,1,
                        Mathf.Clamp01((y-1+propagation*1.14f)/.14f));
                    v.z += maxBend*bend*x*x;
                    if (HasOpening)
                    {
                        v.x += patch.Side[i]*gap*.5f*opening;
                        v.z += patch.Side[i]*gap*.23f*opening*x;
                    }
                    else if (fault?.Visual == FaultVisual.Vibration)
                        v.y += .006f*p*Mathf.Sin(p*40)*x;
                    v.z -= (patch.Rest[i].z-b.center.z)*(1-worn);
                    patch.Current[i] = v;
                }
                patch.Filter.sharedMesh.vertices = patch.Current;
                patch.Filter.sharedMesh.RecalculateNormals();
                patch.Filter.sharedMesh.RecalculateBounds();
                patch.Material.SetColor("_BaseColor",Color.Lerp(patch.Color,
                    fault?.Visual == FaultVisual.Heat ? new Color(.27f,.11f,.055f) : patch.Color,p));
                if (!selected) continue;
                DamageSurface = patch.Filter;
                OpeningMm = Vector3.Distance(patch.Current[patch.LeftLip],patch.Current[patch.RightLip])*1000;
                DeflectionMm = maxBend*bend*1000;
                CrackLengthMm = patch.Size.y*Mathf.Min(1,propagation*1.14f)*1000;
                RemainingMm = patch.Size.y*(1-Mathf.Min(1,propagation*1.14f))*1000;
                ThicknessMm = patch.Size.z*worn*1000;
            }
            if (liquid)
            {
                float level = code == "TK-LEAK" ? 1-.7f*Progress : code == "TK-LEVEL" ? 1+.5f*Progress : 1;
                liquid.localScale = new Vector3(1,level,1);
            }
            if (indicator)
            {
                var block = new MaterialPropertyBlock();
                block.SetColor("_BaseColor",fault == null ? new Color(.09f,.7f,.38f) :
                    code == "PLC-LOW" ? new Color(.07f,.10f,.08f) : new Color(.9f,.2f,.04f));
                indicator.GetComponent<Renderer>().SetPropertyBlock(block);
            }
        }

        public void CrackLips(out Vector3 left, out Vector3 right)
        {
            var p = patches.Find(t => t.Filter == DamageSurface);
            left = p.Current[p.LeftLip]; right = p.Current[p.RightLip];
        }

        public void SetDisassembly(float step, bool openDoor, bool removeGuard)
        {
            for (int i = 0; i < assemblies.Count; i++)
            {
                float amount = Mathf.SmoothStep(0,1,Mathf.Clamp01(step-i));
                Vector3 direction = assemblyOrigins[i]-NativeBounds.center;
                if (direction.sqrMagnitude < .01f) direction = Vector3.forward;
                direction.Normalize();
                float separation = NativeBounds.size.magnitude*.19f;
                assemblies[i].localPosition = assemblyOrigins[i]+direction*amount*separation;
                if (Kind == DeviceKind.Fan && i == 3)
                    assemblies[i].localPosition += Vector3.back*(removeGuard ? .9f : 0);
            }
            if (Door) Door.localRotation = doorClosed*Quaternion.Euler(0,openDoor?-112:0,0);
        }
    }
}
