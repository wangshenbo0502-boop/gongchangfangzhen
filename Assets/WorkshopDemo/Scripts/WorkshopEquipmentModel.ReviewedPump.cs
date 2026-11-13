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
        Renderer[] reviewedSealRenderers;
        GameObject reviewedDamage;

        bool ReviewedPump()
        {
            var prefab = Resources.Load<GameObject>("ReviewedEquipment/water-pump");
            var contract = Resources.Load<TextAsset>("ReviewedEquipment/pump-parts");
            if (!prefab || !contract) return false;
            reviewedParts = JsonUtility.FromJson<PumpParts>(contract.text);
            reviewedPump = Instantiate(prefab, transform, false).transform;
            foreach (var part in reviewedParts.parts)
                assemblies.Add(reviewedPump.GetComponentsInChildren<Transform>().Single(t => t.name == part.name));
            group = assemblies[6];
            reviewedSealRenderers = group.GetComponentsInChildren<Renderer>();
            Vector3 at = group.InverseTransformPoint(reviewedPump.TransformPoint(new Vector3(-.17f, .66f, 0)));
            DamageBlock("Seal inspection fracture insert", at + new Vector3(-.03f, -.06f, -.085f),
                new Vector3(.06f, .12f, .008f), steel);
            reviewedDamage = DamageSurface.gameObject;
            reviewedDamage.SetActive(false);
            // Rotate only the authored shaft, never the entire coupling group or its support.
            Rotor = reviewedPump.GetComponentsInChildren<Transform>()
                .FirstOrDefault(t => t.name == "PMP_02_Coupling_PumpShaft");
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
