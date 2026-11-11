using System.Collections.Generic;
using UnityEngine;

namespace WorkshopDemo
{
    public sealed partial class WorkshopEquipmentModel
    {
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Transform> assemblies = new List<Transform>();
        Material paint, steel, dark, rubber, yellow, copper, white, green, red, glass;
        Transform group;

        Material Finish(string name, Color color, float metallic, float gloss)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", gloss);
            ownedMaterials.Add(m);
            return m;
        }

        void Palette(string id)
        {
            paint = Finish("Epoxy enamel", id == "P-02" || id == "TK-02" ?
                new Color(.16f,.42f,.35f) : new Color(.045f,.31f,.43f), .32f,.37f);
            steel = Finish("Machined stainless", new Color(.63f,.68f,.70f), .78f,.48f);
            dark = Finish("Powder coat graphite", new Color(.12f,.14f,.15f), .35f,.28f);
            rubber = Finish("Elastomer", new Color(.028f,.035f,.037f), .02f,.19f);
            yellow = Finish("Safety enamel", new Color(.95f,.64f,.08f), .28f,.34f);
            copper = Finish("Copper busbar", new Color(.58f,.25f,.105f), .75f,.48f);
            white = Finish("Porcelain and markings", new Color(.82f,.86f,.84f), .05f,.3f);
            green = Finish("Indicator green", new Color(.09f,.7f,.38f), .12f,.5f);
            red = Finish("Indicator red", new Color(.84f,.09f,.055f), .16f,.4f);
            glass = Finish("Display glass", new Color(.015f,.065f,.075f), .3f,.68f);
        }

        Transform Assembly(string name, Vector3 position)
        {
            group = new GameObject(name).transform;
            group.SetParent(transform,false);
            group.localPosition = position;
            assemblies.Add(group);
            return group;
        }

        Mesh MakeMesh(string name, List<Vector3> v, List<int> indices)
        {
            var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(v);
            mesh.SetTriangles(indices,0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            ownedMeshes.Add(mesh);
            return mesh;
        }

        Transform Part(string name, Mesh mesh, Vector3 position, Material material, Quaternion rotation)
        {
            var t = new GameObject(name).transform;
            t.SetParent(group,false);
            t.localPosition = position;
            t.localRotation = rotation;
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = t.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            return t;
        }

        // A revolved engineering section, including its bore, chamfers and end faces.
        Transform Turn(string name, Vector3 position, Vector2[] section, Material material,
            Vector3 axis, int segments = 64, float start = 0, float sweep = 360)
        {
            var v = new List<Vector3>();
            var tri = new List<int>();
            for (int j = 0; j < section.Length; j++)
                for (int i = 0; i <= segments; i++)
                {
                    float a = (start+sweep*i/segments)*Mathf.Deg2Rad;
                    v.Add(new Vector3(Mathf.Cos(a)*section[j].x,section[j].y,Mathf.Sin(a)*section[j].x));
                }
            for (int j = 0; j < section.Length-1; j++)
                for (int i = 0; i < segments; i++)
                {
                    int n = j*(segments+1)+i, b = n+segments+1;
                    tri.Add(n); tri.Add(b); tri.Add(n+1);
                    tri.Add(n+1); tri.Add(b); tri.Add(b+1);
                }
            return Part(name,MakeMesh(name,v,tri),position,material,Quaternion.FromToRotation(Vector3.up,axis));
        }

        Transform Tube(string name, Vector3 p, float radius, float length, Material mat,
            Vector3 axis, float bore = 0, int segments = 64)
        {
            float b = Mathf.Min(radius*.07f,length*.10f);
            return Turn(name,p,new[] { new Vector2(bore,-length/2),new Vector2(radius-b,-length/2),
                new Vector2(radius,-length/2+b),new Vector2(radius,length/2-b),
                new Vector2(radius-b,length/2),new Vector2(bore,length/2),new Vector2(bore,-length/2) },
                mat,axis,segments);
        }

        Transform Box(string name, Vector3 p, Vector3 size, Material mat, float bevel = .012f)
        {
            bevel = Mathf.Min(bevel,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.2f);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            // Three octagonal sections give small readable highlights on folded or cast edges.
            for (int k = 0; k < 4; k++)
            {
                float z = k == 0 ? -size.z/2 : k == 1 ? -size.z/2+bevel :
                    k == 2 ? size.z/2-bevel : size.z/2;
                float inset = k == 0 || k == 3 ? bevel : 0;
                float x = size.x/2-inset, y = size.y/2-inset, r = Mathf.Max(.00001f,bevel);
                vertices.AddRange(new[] { new Vector3(-x+r,-y,z),new Vector3(x-r,-y,z),
                    new Vector3(x,-y+r,z),new Vector3(x,y-r,z),new Vector3(x-r,y,z),
                    new Vector3(-x+r,y,z),new Vector3(-x,y-r,z),new Vector3(-x,-y+r,z) });
            }
            for (int k = 0; k < 3; k++)
                for (int i = 0; i < 8; i++)
                {
                    int a = k*8+i,b = k*8+(i+1)%8;
                    triangles.AddRange(new[] { a,b,a+8,b,b+8,a+8 });
                }
            for (int i = 1; i < 7; i++)
                triangles.AddRange(new[] { 0,i+1,i,24,24+i,24+i+1 });
            return Part(name,MakeMesh(name,vertices,triangles),p,mat,Quaternion.identity);
        }

        void Rod(string name, Vector3 a, Vector3 b, float radius, Material mat)
            => Tube(name,(a+b)/2,radius,Vector3.Distance(a,b),mat,b-a,0,16);

        void Bolts(Vector3 p, Vector3 axis, float pitchRadius, int count, float radius = .022f)
        {
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up,axis);
            for (int i = 0; i < count; i++)
            {
                float a = i*Mathf.PI*2/count;
                Vector3 at = p+rotation*new Vector3(Mathf.Cos(a)*pitchRadius,0,Mathf.Sin(a)*pitchRadius);
                Tube("Bolt washer",at,radius*1.4f,radius*.25f,steel,axis,0,20);
                Tube("Hex fastener",at+axis.normalized*radius*.5f,radius,radius,steel,axis,0,6);
            }
        }

        void Flange(Vector3 p, Vector3 axis, float radius, float bore)
        {
            Tube("Bolted flange",p,radius,.055f,steel,axis,bore);
            Tube("Gasket witness",p-axis.normalized*.033f,radius*.92f,.012f,rubber,axis,bore);
            Bolts(p+axis.normalized*.032f,axis,radius*.79f,8,radius*.065f);
        }

        void Gauge(Vector3 p, float radius = .095f)
        {
            Rod("Gauge stem",p-Vector3.up*.13f,p,.015f,steel);
            Tube("Gauge bezel",p,radius,.065f,steel,Vector3.back);
            Tube("Dial face",p+Vector3.back*.036f,radius*.88f,.006f,white,Vector3.back);
            for (int i = 0; i < 13; i++)
            {
                float a = (-220+i*260f/12)*Mathf.Deg2Rad;
                Vector3 r = new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                Rod("Gauge graduation",p+r*radius*.68f+Vector3.back*.042f,
                    p+r*radius*.80f+Vector3.back*.042f,.0025f,dark);
            }
            Rod("Gauge pointer",p+Vector3.back*.047f,
                p+new Vector3(.45f,.5f,0)*radius+Vector3.back*.047f,.004f,red);
        }

        void Nameplate(Vector3 p, float width, string id)
        {
            Box("Riveted rating plate",p,new Vector3(width,.14f,.012f),steel,.003f);
            for (int i = 0; i < 4; i++)
                Tube("Plate rivet",p+new Vector3((i%2==0?-1:1)*(width/2-.018f),
                    (i<2?-1:1)*.05f,-.01f),.007f,.006f,dark,Vector3.back,0,10);
            for (int i = 0; i < 4; i++)
                Box("Etched rating line",p+new Vector3(-width*.05f,.04f-i*.025f,-.009f),
                    new Vector3(width*(i==0?.64f:.45f),.006f,.003f),dark,.001f);
        }

        void Motor(Vector3 p, float scale)
        {
            var previous = group;
            var t = new GameObject("Finned drive assembly").transform;
            t.SetParent(group,false); t.localPosition = p; t.localScale = Vector3.one*scale;
            group = t;
            Tube("Motor barrel",Vector3.zero,.235f,.58f,paint,Vector3.right);
            for (int i = 0; i < 28; i++)
            {
                float a = i*Mathf.PI*2/28;
                var fin = Box("Cast cooling fin",new Vector3(0,Mathf.Cos(a)*.25f,Mathf.Sin(a)*.25f),
                    new Vector3(.49f,.047f,.012f),paint,.003f);
                fin.localRotation = Quaternion.Euler(a*Mathf.Rad2Deg,0,0);
            }
            Tube("Bearing end shield",new Vector3(.31f,0,0),.245f,.07f,paint,Vector3.right);
            Bolts(new Vector3(.35f,0,0),Vector3.right,.19f,6,.017f);
            Tube("Ventilated fan cap",new Vector3(-.34f,0,0),.248f,.15f,dark,Vector3.right);
            for (int i = 0; i < 12; i++)
                Rod("Fan cap slots",new Vector3(-.42f,-.17f+i*.03f,-.11f),
                    new Vector3(-.42f,-.17f+i*.03f,.11f),.005f,steel);
            Box("Terminal box",new Vector3(-.015f,.29f,0),new Vector3(.26f,.15f,.24f),paint);
            Box("Terminal lid seam",new Vector3(-.015f,.37f,0),new Vector3(.27f,.018f,.25f),steel,.004f);
            Tube("Cable gland",new Vector3(-.015f,.30f,-.155f),.042f,.08f,dark,Vector3.back,0,6);
            for (int i = 0; i < 2; i++)
                Box("Motor mounting foot",new Vector3(i==0?-.2f:.2f,-.25f,0),
                    new Vector3(.14f,.10f,.56f),paint);
            group = previous;
        }

        void OnDestroy()
        {
            foreach (var mesh in ownedMeshes) if (mesh) Destroy(mesh);
            foreach (var mat in ownedMaterials) if (mat) Destroy(mat);
        }
    }
}
