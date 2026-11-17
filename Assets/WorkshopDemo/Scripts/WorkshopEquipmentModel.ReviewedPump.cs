using System;
using System.Linq;
using UnityEngine;

namespace WorkshopDemo
{
    public sealed partial class WorkshopEquipmentModel
    {
        [Serializable] class PumpMotion { public float time; public Vector3 offset; }
        [Serializable] class PumpPart { public string name, label; public PumpMotion[] path; }
        [Serializable] class PumpParts { public PumpPart[] parts; }
        PumpParts reviewedParts;
        Transform reviewedPump;
        Transform pumpImpeller;

        bool ReviewedPump()
        {
            var prefab = Resources.Load<GameObject>("ReviewedEquipment/water-pump");
            var contract = Resources.Load<TextAsset>("ReviewedEquipment/pump-parts");
            if (!prefab || !contract) return false;
            reviewedParts = JsonUtility.FromJson<PumpParts>(contract.text);
            reviewedPump = Instantiate(prefab, transform, false).transform;
            var nodes = reviewedPump.GetComponentsInChildren<Transform>();
            foreach (var part in reviewedParts.parts)
                assemblies.Add(nodes.Single(t => t.name == part.name));
            Rotor = nodes.Single(t => t.name == "PMP_02_Coupling_PumpShaft");
            pumpImpeller = nodes.Single(t => t.name == "water-pump_rotor");
            DamageSurface = nodes.Single(t => t.name == "PMP_06_SealBearing_BearingHousing").GetComponent<MeshFilter>();
            return true;
        }

        static Vector3 PumpCoordinates(Vector3 p) => new Vector3(p.x, p.z, -p.y);

        void ReviewedDisassembly(float step)
        {
            float progress = Mathf.Clamp01(step / assemblies.Count);
            for (int i = 0; i < assemblies.Count; i++)
            {
                PumpMotion[] path = reviewedParts.parts[i].path;
                Vector3 offset = path[path.Length - 1].offset;
                for (int k = 1; k < path.Length; k++)
                {
                    if (progress > path[k].time) continue;
                    float t = Mathf.Clamp01((progress - path[k - 1].time) / (path[k].time - path[k - 1].time));
                    t = t * t * t * (10 + t * (-15 + 6 * t));
                    offset = Vector3.LerpUnclamped(path[k - 1].offset, path[k].offset, t);
                    break;
                }
                assemblies[i].localPosition = assemblyOrigins[i] + PumpCoordinates(offset);
            }
        }
    }
}
