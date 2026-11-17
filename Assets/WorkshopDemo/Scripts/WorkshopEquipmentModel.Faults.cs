using System.Collections.Generic;
using UnityEngine;

namespace WorkshopDemo
{
    public sealed partial class WorkshopEquipmentModel
    {
        Dictionary<string, WorkshopFaultBinding> faultBindings;
        WorkshopFaultPresenter faultPresenter;
        WorkshopPumpFaultPresenter pumpFaultPresenter;
        WorkshopLocalFaultPresenter localFaultPresenter;
        internal WorkshopFaultFrame? FaultFrame { get; private set; }
        internal WorkshopFaultPresenter FaultPresenter => faultPresenter;
        internal WorkshopPumpFaultPresenter PumpFaultPresenter => pumpFaultPresenter;
        internal WorkshopLocalFaultPresenter LocalFaultPresenter => localFaultPresenter;

        internal WorkshopFaultProfile ProfileFor(WorkshopFault fault)
        {
            return fault?.Code switch
            {
                "TK-LEAK" when ReviewedTankModel => WorkshopFaultProfile.TankLeak,
                "P-SEAL" when reviewedParts != null => WorkshopFaultProfile.PumpSeal,
                "P-CAV" when reviewedParts != null => WorkshopFaultProfile.PumpCavitation,
                "F-CRACK" when ReviewedFanModel => WorkshopFaultProfile.FanCrack,
                _ => faultBindings != null && fault != null ? WorkshopFaultCase.Find(fault.Code)?.Profile : null
            };
        }

        internal bool TryGetFaultBinding(string id, out WorkshopFaultBinding binding)
        {
            binding = null;
            return faultBindings != null && faultBindings.TryGetValue(id, out binding);
        }

        void InitializeFaultBindings(string deviceId)
        {
            string resource = ReviewedTankModel ? "silo" : reviewedParts != null ? "water-pump" :
                ReviewedFanModel ? "industrial-fan" : ReviewedCabinetModel ? "electrical-cabinet" : null;
            if (resource == null) return;
            var root = transform.Find(resource + "(Clone)");
            if (!root)
            {
                Debug.LogWarning(deviceId + ": 故障模型根节点缺失 (" + resource + ")");
                return;
            }
            faultBindings = WorkshopFaultBinding.Resolve(root, resource, deviceId);
            localFaultPresenter = new WorkshopLocalFaultPresenter(this, faultBindings);
            if (reviewedParts != null)
            {
                if (TryGetFaultBinding("pump.seal.chamber", out var chamber) && chamber.Settings.anchorCalibrated &&
                    TryGetFaultBinding("pump.seal.faces", out var faces) && faces.Renderers.Length == 2 &&
                    TryGetFaultBinding("pump.inlet.cover", out var inlet) && inlet.Settings.anchorCalibrated &&
                    TryGetFaultBinding("pump.impeller.inlet", out _) &&
                    TryGetFaultBinding("pump.discharge.gauge", out var meter) && meter.Renderers.Length == 2)
                    pumpFaultPresenter = new WorkshopPumpFaultPresenter(this, chamber, faces, inlet, meter, pumpImpeller);
                else Debug.LogWarning(deviceId + ": 水泵故障部件绑定缺失");
                return;
            }
            if (!ReviewedTankModel) return;
            if (TryGetFaultBinding(WorkshopFaultProfile.TankLeak.BindingId, out var leak) &&
                leak.Settings.anchorCalibrated && leak.Settings.normalLocal.sqrMagnitude > .99f &&
                leak.Settings.focusSizeM.x > 0 && leak.Settings.focusSizeM.y > 0 && leak.Settings.focusSizeM.z > 0 &&
                TryGetFaultBinding("tank.instruments.level", out var gauge) && gauge.Renderers.Length == 2)
                faultPresenter = new WorkshopFaultPresenter(this, leak, gauge);
            else Debug.LogWarning(deviceId + ": TK-LEAK 故障部件绑定缺失或漏点未标定");
        }

        bool ApplyFaultProfile(WorkshopFault fault, float seconds, WorkshopDevice live)
        {
            var profile = ProfileFor(fault);
            if (profile == null)
            {
                FaultFrame = null;
                faultPresenter?.ResetVisual();
                pumpFaultPresenter?.ResetVisual();
                localFaultPresenter?.ResetVisual();
                if (ReviewedFanModel) ReviewedFanDamage("");
                return false;
            }
            var frame = profile.Evaluate(Progress, seconds, live, NominalRpm);
            FaultFrame = frame;
            HasOpening = false;
            OpeningMm = DeflectionMm = CrackLengthMm = RemainingMm = ThicknessMm = 0;
            if (profile == WorkshopFaultProfile.TankLeak)
            {
                localFaultPresenter?.ResetVisual();
                faultPresenter?.Apply(frame);
            }
            else faultPresenter?.ResetVisual();
            if (profile == WorkshopFaultProfile.PumpSeal || profile == WorkshopFaultProfile.PumpCavitation)
            {
                localFaultPresenter?.ResetVisual();
                pumpFaultPresenter?.Apply(frame, live != null);
            }
            else
            {
                pumpFaultPresenter?.ResetVisual();
                if (profile != WorkshopFaultProfile.TankLeak) localFaultPresenter?.Apply(frame, live != null);
            }
            if (ReviewedFanModel)
            {
                ReviewedFanDamage(fault.Code);
                HasOpening = fault.Code == "F-CRACK" && Progress >= .2f;
            }
            Recipe = FaultPresentationAvailable ? profile.Recipe : "故障部件绑定缺失";
            return true;
        }

        internal bool FaultPresentationAvailable => FaultFrame?.Code switch {
            "TK-LEAK" => faultPresenter != null,
            "P-SEAL" or "P-CAV" => pumpFaultPresenter != null,
            _ => localFaultPresenter?.Available ?? false
        };

        void LateUpdate()
        {
            if (!owner) return;
            localFaultPresenter?.SyncRotorPhase();
            pumpFaultPresenter?.SyncLiveRotation();
            pumpFaultPresenter?.UpdateAudio(owner.IsRunning);
        }

        internal bool TryGetFaultFocus(out Bounds bounds, out Vector3 normal)
        {
            if (FaultFrame.HasValue)
            {
                if (FaultFrame.Value.Code != "TK-LEAK" && FaultFrame.Value.Code != "P-SEAL" &&
                    FaultFrame.Value.Code != "P-CAV")
                {
                    bounds = localFaultPresenter?.Available == true ? localFaultPresenter.FocusBounds : default;
                    normal = localFaultPresenter?.Available == true ? localFaultPresenter.Normal : Vector3.zero;
                    return localFaultPresenter?.Available == true;
                }
                if (FaultFrame.Value.Code != "TK-LEAK" && pumpFaultPresenter != null)
                {
                    bounds = pumpFaultPresenter.FocusBounds;
                    normal = pumpFaultPresenter.Normal;
                    return true;
                }
                bounds = faultPresenter != null ? faultPresenter.FocusBounds : default;
                normal = faultPresenter != null ? faultPresenter.Normal : Vector3.zero;
                return faultPresenter != null;
            }
            if (DamageSurface && DamageSurface.TryGetComponent<Renderer>(out var surface))
            {
                bounds = surface.bounds;
                normal = Vector3.zero;
                return true;
            }
            bounds = default;
            normal = Vector3.zero;
            return false;
        }
    }
}
