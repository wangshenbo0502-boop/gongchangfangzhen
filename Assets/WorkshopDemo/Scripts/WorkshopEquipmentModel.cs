using System.Collections.Generic;
using UnityEngine;

namespace WorkshopDemo
{
    // Authored nominal geometry. This visual assembly is not a calibrated CAD or FEM model.
    public sealed partial class WorkshopEquipmentModel : MonoBehaviour
    {
        public DeviceKind Kind { get; private set; }
        public Transform Rotor { get; private set; }
        public Transform Door { get; private set; }
        public MeshFilter DamageSurface { get; private set; }
        public bool Refined => Kind != DeviceKind.Conveyor && Kind != DeviceKind.Motor;
        public string ComponentName => Kind == DeviceKind.Pump ? "机械密封 / 联轴器" :
            Kind == DeviceKind.Tank ? "前侧罐壁 / 支撑" : Kind == DeviceKind.Fan ? "叶轮工作叶片" :
            Kind == DeviceKind.Cabinet ? "出线端子 / 柜内回路" :
            Kind == DeviceKind.Motor ? "输出端联轴器" : "输送侧梁 / 传动端";
        public float Progress { get; private set; }
        public float OpeningMm { get; private set; }
        public float DeflectionMm { get; private set; }
        public float RemainingMm { get; private set; }
        public string Stage => Progress <= 0 ? "完好外形" : Progress < .55f ? "异常发展" :
            Progress < 1 ? "损坏演化" : "末态保持";
        public string Recipe { get; private set; }
        public bool HasOpening { get; private set; }
        public Bounds NativeBounds { get; private set; }
        public int PartCount => GetComponentsInChildren<MeshRenderer>(true).Length;
        public int TriangleCount
        {
            get
            {
                int n = 0;
                foreach (var f in GetComponentsInChildren<MeshFilter>(true))
                    for (int i = 0; i < f.sharedMesh.subMeshCount; i++) n += (int)f.sharedMesh.GetIndexCount(i)/3;
                return n;
            }
        }
        readonly List<Vector3> assemblyOrigins = new List<Vector3>();
        Transform indicator, liquid;
        Quaternion doorClosed;
        WorkshopDevice owner;
        float nextFrame;

        public static WorkshopEquipmentModel Attach(WorkshopDevice device)
        {
            var existing = device.GetComponentInChildren<WorkshopEquipmentModel>();
            if (existing) return existing;
            var box = device.GetComponent<BoxCollider>();
            if (!box) return null;
            var go = new GameObject("Precision equipment assembly");
            go.transform.SetParent(device.transform,false);
            var model = go.AddComponent<WorkshopEquipmentModel>();
            model.Initialize(device.kind,device.deviceId,device);
            model.owner = device;
            model.Install(device,box);
            return model;
        }

        public void Initialize(DeviceKind kind, string id, WorkshopDevice original = null)
        {
            Kind = kind;
            Palette(id);
            if (kind == DeviceKind.Pump) Pump();
            else if (kind == DeviceKind.Tank) Tank(id);
            else if (kind == DeviceKind.Fan) Fan();
            else if (kind == DeviceKind.Cabinet) Cabinet();
            else Legacy(original);
            foreach (var t in assemblies) assemblyOrigins.Add(t.localPosition);
            var renders = GetComponentsInChildren<MeshRenderer>();
            NativeBounds = new Bounds(transform.InverseTransformPoint(renders[0].bounds.center),Vector3.zero);
            foreach (var r in renders)
            {
                var b = r.bounds;
                for (int i = 0; i < 8; i++)
                    NativeBounds = Encapsulate(NativeBounds,transform.InverseTransformPoint(b.center+
                        Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            }
            SetDamage(null,0);
        }

        void Install(WorkshopDevice original, BoxCollider box)
        {
            if (Refined)
            {
                    foreach (Transform child in original.transform)
                        if (child.name.StartsWith("Source model") || child == original.rotor)
                            foreach (var r in child.GetComponentsInChildren<Renderer>()) r.enabled = false;
                    transform.localScale = Vector3.one;
                    transform.localPosition = GroundOffset(NativeBounds);
                    ConfigureEnvelope(original, NativeBounds);
                    foreach (var collider in original.GetComponentsInChildren<Collider>(true))
                        if (collider != box) collider.enabled = false;
                    if (Rotor)
                    {
                        original.rotor = Rotor;
                        original.rotorAxis = Kind == DeviceKind.Fan ? Vector3.forward : Vector3.right;
                    }
                    if (Kind == DeviceKind.Cabinet) original.statusLamp = indicator.GetComponent<Renderer>();
            }
        }

        public static Vector3 GroundOffset(Bounds bounds) =>
            new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);

        public static void ConfigureEnvelope(WorkshopDevice device, Bounds nativeBounds)
        {
            var bounds = new Bounds(nativeBounds.center + GroundOffset(nativeBounds), nativeBounds.size);
            var box = device.GetComponent<BoxCollider>();
            box.center = bounds.center;
            box.size = bounds.size + new Vector3(.08f, .05f, .08f);
            if (device.selectionRing)
            {
                var ring = device.selectionRing.transform;
                ring.localPosition = Vector3.zero;
                ring.localRotation = Quaternion.identity;
                ring.localScale = Vector3.one;
                var line = ring.GetComponent<LineRenderer>();
                line.useWorldSpace = false;
                float x = bounds.extents.x + .25f, z = bounds.extents.z + .25f;
                line.positionCount = 4;
                line.SetPositions(new[] { new Vector3(-x, .04f, -z), new Vector3(-x, .04f, z),
                    new Vector3(x, .04f, z), new Vector3(x, .04f, -z) });
            }
            if (device.worldLabel)
                device.worldLabel.transform.localPosition = new Vector3(0, bounds.max.y + .3f, 0);
            if (device.faultMarker)
                device.faultMarker.transform.localPosition = new Vector3(0, bounds.max.y + .62f, 0);
        }

        static Bounds Encapsulate(Bounds b, Vector3 p) { b.Encapsulate(p); return b; }

        void Pump()
        {
            Assembly("01 Grouted pump skid",Vector3.zero);
            Box("Channel skid",new Vector3(0,.085f,0),new Vector3(2.12f,.17f,.75f),dark);
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = new Vector3(i<2?-.86f:.86f,.18f,i%2==0?-.28f:.28f);
                Box("Anchor lug",p,new Vector3(.16f,.04f,.18f),steel);
                Bolts(p+Vector3.up*.025f,Vector3.up,0,1,.025f);
            }
            Assembly("02 Electric drive",new Vector3(-.53f,.57f,0));
            Motor(Vector3.zero,1.12f);
            Assembly("03 Coupling and guard",new Vector3(.02f,.57f,0));
            Rotor = group;
            Tube("Drive shaft",Vector3.zero,.065f,.34f,steel,Vector3.right);
            Tube("Elastomer coupling",Vector3.zero,.12f,.20f,rubber,Vector3.right);
            for (int i = 0; i < 2; i++)
                Tube("Coupling hub",new Vector3(i==0?-.12f:.12f,0,0),.13f,.075f,steel,Vector3.right);
            Assembly("04 Removable coupling guard",new Vector3(.02f,.57f,0));
            Turn("Arched perforated safety guard",Vector3.zero,new[] { new Vector2(.19f,-.21f),
                new Vector2(.205f,-.21f),new Vector2(.205f,.21f),new Vector2(.19f,.21f),
                new Vector2(.19f,-.21f) },yellow,Vector3.right,40,0,200);
            for (int i = 0; i < 12; i++)
                Box("Guard vent",new Vector3(-.16f+i*.03f,.20f,0),new Vector3(.012f,.005f,.12f),dark,.001f);
            Assembly("05 Split casing volute",new Vector3(.53f,.57f,0));
            Turn("Cast volute casing",Vector3.zero,new[] { new Vector2(0,-.20f),new Vector2(.20f,-.20f),
                new Vector2(.30f,-.14f),new Vector2(.35f,-.02f),new Vector2(.34f,.14f),
                new Vector2(.25f,.23f),new Vector2(.13f,.24f),new Vector2(0,.24f) },paint,Vector3.right,96);
            Tube("Casing split flange",Vector3.right*.12f,.36f,.045f,steel,Vector3.right,.27f,96);
            Bolts(Vector3.right*.15f,Vector3.right,.315f,12);
            Box("Cast casing foot",new Vector3(0,-.33f,0),new Vector3(.40f,.24f,.48f),paint);
            Nameplate(new Vector3(0,.06f,-.345f),.28f,"P");
            Assembly("06 Suction and discharge",new Vector3(.53f,.57f,0));
            Tube("Suction neck",Vector3.right*.37f,.125f,.30f,paint,Vector3.right,.09f);
            Flange(Vector3.right*.54f,Vector3.right,.185f,.09f);
            Tube("Discharge riser",new Vector3(0,.44f,.05f),.105f,.42f,paint,Vector3.up,.075f);
            Flange(new Vector3(0,.66f,.05f),Vector3.up,.16f,.075f);
            Gauge(new Vector3(.19f,.62f,-.10f),.075f);
            Assembly("07 Mechanical seal faces",new Vector3(.25f,.57f,-.05f));
            Tube("Seal carrier",Vector3.zero,.145f,.12f,dark,Vector3.right,.07f);
            DamageBlock("Carbon seal face",new Vector3(-.055f,-.11f,-.105f),
                new Vector3(.11f,.22f,.022f),steel);
        }

        void Tank(string id)
        {
            float r = .67f;
            Assembly("01 Vessel legs and anchor plates",Vector3.zero);
            for (int i = 0; i < 4; i++)
            {
                float a = (45+i*90)*Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Cos(a)*.55f,.38f,Mathf.Sin(a)*.55f);
                if (i == 2)
                {
                    DamageBlock("Front support fracture section",new Vector3(0,-.06f,-.06f),
                        new Vector3(.70f,.12f,.12f),dark);
                    DamageSurface.transform.localPosition = p+new Vector3(0,-.35f,0);
                    DamageSurface.transform.localRotation = Quaternion.Euler(0,0,90);
                }
                else Box("Structural leg",p,new Vector3(.12f,.70f,.12f),dark);
                Box("Anchored baseplate",new Vector3(p.x,.035f,p.z),new Vector3(.31f,.07f,.29f),steel);
                Bolts(new Vector3(p.x,.08f,p.z),Vector3.up,.11f,4,.019f);
            }
            Assembly("02 Rolled shell",new Vector3(0,1.88f,0));
            Material shell = id == "TK-01" ? steel : paint;
            Turn("Welded shell excluding inspection panel",Vector3.zero,new[] {
                new Vector2(r-.012f,-1.05f),new Vector2(r,-1.05f),new Vector2(r,1.05f),
                new Vector2(r-.012f,1.05f),new Vector2(r-.012f,-1.05f) },shell,Vector3.up,112,-64,308);
            // The missing 52-degree front sector is filled by a thick, deformable shell panel.
            DamageBlock("Continuous curved inspection sector",new Vector3(-.294f,-1.05f,-.67f),
                new Vector3(.588f,2.1f,.012f),shell,true);
            for (int i = 0; i < 4; i++)
                Tube("Circumferential weld bead",new Vector3(0,-.95f+i*.63f,0),r+.003f,.009f,steel,Vector3.up,r-.005f,96);
            Assembly("03 Ellipsoidal end heads",new Vector3(0,1.88f,0));
            var profile = new List<Vector2> { new Vector2(0,-1.40f) };
            for (int i = 0; i <= 16; i++)
            {
                float a = i*Mathf.PI/32;
                profile.Add(new Vector2(r*Mathf.Sin(a),-1.05f-.35f*Mathf.Cos(a)));
            }
            Turn("Lower formed head",Vector3.zero,profile.ToArray(),shell,Vector3.up,96);
            profile.Clear();
            for (int i = 0; i <= 16; i++)
            {
                float a = i*Mathf.PI/32;
                profile.Add(new Vector2(r*Mathf.Cos(a),1.05f+.35f*Mathf.Sin(a)));
            }
            Turn("Upper formed head",Vector3.zero,profile.ToArray(),shell,Vector3.up,96);
            Assembly("04 Flanged process connections",Vector3.zero);
            Tube("Top nozzle",new Vector3(0,3.40f,0),.10f,.30f,steel,Vector3.up,.07f);
            Flange(new Vector3(0,3.56f,0),Vector3.up,.17f,.07f);
            Tube("Outlet spool",new Vector3(.65f,.9f,0),.095f,.50f,steel,Vector3.right,.065f);
            Flange(new Vector3(.92f,.9f,0),Vector3.right,.155f,.065f);
            Tube("Manway collar",new Vector3(0,2.25f,-.70f),.235f,.10f,steel,Vector3.back,.18f);
            Tube("Manway blind cover",new Vector3(0,2.25f,-.78f),.255f,.045f,shell,Vector3.back);
            Bolts(new Vector3(0,2.25f,-.81f),Vector3.back,.215f,12,.021f);
            Gauge(new Vector3(.32f,3.10f,-.41f),.105f);
            Tube("Safety valve stem",new Vector3(-.30f,3.35f,0),.047f,.32f,copper,Vector3.up);
            Box("Valve bonnet",new Vector3(-.30f,3.51f,0),new Vector3(.14f,.07f,.12f),red);
            Assembly("05 Level instrumentation",Vector3.zero);
            for (int i = 0; i < 2; i++)
                Tube("Level gauge takeoff",new Vector3(.44f,i==0?1.02f:2.90f,-.57f),.022f,.20f,steel,Vector3.back);
            Tube("Protected level column",new Vector3(.44f,1.96f,-.68f),.031f,1.86f,dark,Vector3.up);
            liquid = Box("Visible level column",new Vector3(.44f,1.65f,-.71f),new Vector3(.023f,1.14f,.014f),green);
            Nameplate(new Vector3(-.25f,1.60f,-.68f),.30f,id);
            Assembly("06 Service ladder",Vector3.zero);
            for (int i = 0; i < 2; i++)
                Rod("Ladder rail",new Vector3(-.83f,.13f,i==0?-.22f:.22f),
                    new Vector3(-.83f,3.35f,i==0?-.22f:.22f),.023f,steel);
            for (int i = 0; i < 12; i++)
                Rod("Anti-slip ladder rung",new Vector3(-.83f,.25f+i*.26f,-.22f),
                    new Vector3(-.83f,.25f+i*.26f,.22f),.017f,steel);
        }

        void Fan()
        {
            Assembly("01 Isolated mounting skid",Vector3.zero);
            for (int i = 0; i < 2; i++)
            {
                float x = i==0?-.72f:.72f;
                Box("Foot channel",new Vector3(x,.10f,0),new Vector3(.38f,.14f,.85f),dark);
                Box("Anti-vibration pad",new Vector3(x,.025f,0),new Vector3(.34f,.05f,.72f),rubber);
                Box("Fan pedestal",new Vector3(x,.42f,0),new Vector3(.13f,.64f,.16f),yellow);
                Bolts(new Vector3(x,.19f,0),Vector3.up,.23f,2,.026f);
            }
            Assembly("02 Axial flow casing",new Vector3(0,1.22f,0));
            Turn("Rolled ventilation barrel",Vector3.zero,new[] { new Vector2(.84f,-.32f),
                new Vector2(.87f,-.32f),new Vector2(.87f,.32f),new Vector2(.84f,.32f),
                new Vector2(.84f,-.32f) },paint,Vector3.forward,112);
            for (int i = 0; i < 2; i++)
            {
                float z = i==0?-.34f:.34f;
                Tube("Mounting ring",new Vector3(0,0,z),.94f,.045f,steel,Vector3.forward,.83f,96);
                Bolts(new Vector3(0,0,z-.03f),Vector3.back,.895f,16,.022f);
            }
            Nameplate(new Vector3(.54f,.55f,-.37f),.30f,"F");
            Assembly("03 Six-blade twisted impeller",new Vector3(0,1.22f,0));
            Rotor = group;
            Tube("Balanced rotor hub",Vector3.zero,.22f,.29f,steel,Vector3.forward);
            Tube("Hub nose",new Vector3(0,0,-.19f),.15f,.15f,dark,Vector3.forward);
            Bolts(new Vector3(0,0,-.28f),Vector3.back,.115f,6,.018f);
            for (int i = 0; i < 6; i++)
            {
                var anchor = new GameObject("Blade root "+(i+1)).transform;
                anchor.SetParent(group,false);
                anchor.localRotation = Quaternion.Euler(0,0,i*60);
                var previous = group; group = anchor;
                if (i == 0)
                    DamageBlock("Twisted working blade",new Vector3(.18f,-.13f,-.018f),
                        new Vector3(.60f,.26f,.018f),steel,false,true);
                else Blade();
                Tube("Root clamp",new Vector3(.24f,0,0),.045f,.05f,dark,Vector3.forward);
                Bolts(new Vector3(.24f,0,-.03f),Vector3.back,.025f,2,.009f);
                group = previous;
            }
            Assembly("04 Removable inlet guard",new Vector3(0,1.22f,-.39f));
            for (int i = 1; i <= 7; i++)
                Tube("Concentric guard wire",Vector3.zero,.11f*i,.008f,steel,Vector3.forward,.11f*i-.006f,96);
            for (int i = 0; i < 12; i++)
            {
                float a = i*Mathf.PI/6;
                Rod("Guard spoke",Vector3.zero,new Vector3(Mathf.Cos(a)*.83f,Mathf.Sin(a)*.83f,0),.004f,steel);
            }
            Assembly("05 Drive and supports",new Vector3(0,1.22f,.43f));
            Tube("Rear drive motor",Vector3.zero,.23f,.42f,dark,Vector3.forward);
            for (int i = 0; i < 4; i++)
            {
                float a = i*Mathf.PI/2;
                Rod("Motor support vane",new Vector3(Mathf.Cos(a)*.21f,Mathf.Sin(a)*.21f,0),
                    new Vector3(Mathf.Cos(a)*.84f,Mathf.Sin(a)*.84f,-.12f),.025f,steel);
            }
        }

        void Blade()
        {
            DamageBlock("Twisted cast blade",new Vector3(.18f,-.13f,-.018f),new Vector3(.60f,.26f,.018f),
                steel,false,true,false);
        }

        void Cabinet()
        {
            Assembly("01 Plinth and sheetmetal enclosure",Vector3.zero);
            Box("Bolted plinth",new Vector3(0,.10f,0),new Vector3(1.32f,.20f,.63f),dark);
            Box("Back panel",new Vector3(0,1.24f,.28f),new Vector3(1.28f,2.08f,.04f),white);
            for (int i = 0; i < 2; i++)
            {
                float x = i==0?-.65f:.65f;
                Box("Folded sidewall",new Vector3(x,1.26f,0),new Vector3(.045f,2.12f,.60f),white);
                Box("Seal rail",new Vector3(x*.96f,1.26f,-.30f),new Vector3(.025f,2.04f,.03f),rubber);
            }
            Box("Top rain lip",new Vector3(0,2.34f,0),new Vector3(1.35f,.06f,.66f),white);
            Assembly("02 DIN rail control backplate",new Vector3(0,1.25f,.21f));
            Box("Galvanized mounting plate",Vector3.zero,new Vector3(1.12f,1.86f,.025f),steel);
            for (int row = 0; row < 3; row++)
            {
                float y = .58f-row*.54f;
                Box("DIN rail",new Vector3(0,y,-.05f),new Vector3(.99f,.035f,.035f),steel);
                Box("Slotted cable duct",new Vector3(0,y-.20f,-.05f),new Vector3(1.02f,.11f,.085f),white);
                for (int i = 0; i < 24; i++)
                    Box("Duct slot",new Vector3(-.48f+i*.042f,y-.20f,-.098f),new Vector3(.012f,.075f,.004f),dark,.001f);
                for (int i = 0; i < 6; i++)
                {
                    Vector3 p = new Vector3(-.44f+i*.17f,y,-.11f);
                    Box(row==0?"Circuit breaker":"PLC module",p,new Vector3(.135f,.25f,.13f),row==0?white:dark);
                    Box("Module label",p+new Vector3(0,.055f,-.068f),new Vector3(.10f,.035f,.004f),steel,.001f);
                    Box("Module actuator",p+new Vector3(0,-.025f,-.085f),new Vector3(.055f,.04f,.028f),row==0?dark:green,.002f);
                    Rod("Wiring core",p+new Vector3(-.03f,-.13f,0),p+new Vector3(-.03f,-.19f,0),.008f,row==2?yellow:paint);
                }
            }
            Assembly("03 Terminal strip and earth bar",new Vector3(0,.39f,.07f));
            for (int i = 0; i < 15; i++)
            {
                var p = new Vector3(-.49f+i*.07f,0,0);
                Box("Terminal block",p,new Vector3(.06f,.12f,.11f),i>11?green:dark,.003f);
                Tube("Terminal screw",p+new Vector3(0,.025f,-.063f),.012f,.008f,steel,Vector3.back,0,16);
                Rod("Outgoing conductor",p+Vector3.down*.06f,p+Vector3.down*.17f,.006f,i>11?yellow:paint);
            }
            DamageBlock("Copper terminal contact",new Vector3(-.48f,-.01f,-.07f),
                new Vector3(.13f,.025f,.018f),copper);
            Assembly("04 Hinged gasketed door",new Vector3(-.64f,.24f,-.335f));
            Door = group; doorClosed = Door.localRotation;
            Box("Folded steel door",new Vector3(.64f,1,0),new Vector3(1.26f,2.00f,.045f),white,.015f);
            Box("Operator display bezel",new Vector3(.65f,1.52f,-.034f),new Vector3(.43f,.28f,.035f),dark);
            Box("HMI glass",new Vector3(.65f,1.52f,-.055f),new Vector3(.36f,.21f,.009f),glass,.006f);
            for (int i = 0; i < 3; i++)
                Box("HMI reading",new Vector3(.58f,1.58f-i*.055f,-.062f),new Vector3(.16f,.012f,.003f),i==0?green:steel,.001f);
            for (int i = 0; i < 3; i++)
            {
                Tube("Pushbutton bezel",new Vector3(.47f+i*.18f,1.22f,-.045f),.043f,.035f,steel,Vector3.back);
                var lens = Tube("Control indicator",new Vector3(.47f+i*.18f,1.22f,-.070f),
                    .032f,.022f,i==0?green:i==1?yellow:red,Vector3.back);
                if (i==0) indicator = lens;
            }
            Box("Door lock handle",new Vector3(1.12f,1.02f,-.07f),new Vector3(.035f,.22f,.05f),dark);
            for (int i = 0; i < 3; i++)
                Tube("Captive hinge",new Vector3(0,.22f+i*.75f,0),.035f,.11f,steel,Vector3.up);
            for (int i = 0; i < 9; i++)
                Box("Filter louvre",new Vector3(.62f,.25f+i*.037f,-.03f),new Vector3(.72f,.019f,.025f),dark,.003f);
            Nameplate(new Vector3(.65f,.77f,-.031f),.32f,"PLC");
        }

        void Legacy(WorkshopDevice original)
        {
            Assembly("01 Existing equipment appearance",Vector3.zero);
            if (original)
            {
                foreach (Transform child in original.transform)
                    if (child.name.StartsWith("Source model") || child == original.rotor)
                        foreach (var r in child.GetComponentsInChildren<MeshRenderer>())
                        {
                            if (!r.enabled || !r.GetComponent<MeshFilter>()) continue;
                            var t = new GameObject(r.name).transform;
                            t.SetParent(group,false);
                            t.localPosition = original.transform.InverseTransformPoint(r.transform.position);
                            t.localRotation = Quaternion.Inverse(original.transform.rotation)*r.transform.rotation;
                            var s = original.transform.lossyScale;
                            t.localScale = Vector3.Scale(r.transform.lossyScale,new Vector3(1/s.x,1/s.y,1/s.z));
                            t.gameObject.AddComponent<MeshFilter>().sharedMesh = r.GetComponent<MeshFilter>().sharedMesh;
                            t.gameObject.AddComponent<MeshRenderer>().sharedMaterials = r.sharedMaterials;
                        }
                // In the workshop the source already exists; this copy is only used in the inspection viewer.
                if (transform.parent == original.transform) group.gameObject.SetActive(false);
            }
            Assembly("02 Serviceable drive connection",Vector3.zero);
            if (Kind == DeviceKind.Conveyor)
                DamageBlock("Side rail inspection segment",new Vector3(-.6f,.82f,-.66f),
                    new Vector3(1.2f,.12f,.055f),steel);
            else
                DamageBlock("Coupling inspection segment",new Vector3(.10f,.45f,-.20f),
                    new Vector3(.25f,.085f,.085f),steel);
        }

        void Update()
        {
            if (!owner || Time.unscaledTime < nextFrame) return;
            nextFrame = Time.unscaledTime+.05f;
            float progress = owner.Fault == null ? 0 : Mathf.Clamp01(owner.FaultAge/18);
            if (owner.Fault != lastFault || Mathf.Abs(progress-Progress) > .00001f)
            {
                SetDamage(owner.Fault,progress);
                lastFault = owner.Fault;
            }
        }
    }
}
