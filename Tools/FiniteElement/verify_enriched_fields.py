"""Validate saved tensor invariants, heat flux and the shipped refined meshes."""
import hashlib
import json
from datetime import datetime
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parents[2]


def main():
    entries = []
    for path in sorted((ROOT / "Assets/StreamingAssets/WorkshopAnalysis").glob("*.json")):
        data = json.loads(path.read_text(encoding="utf-8"))
        checks = []
        for m in data["models"]:
            faces = len(m["tri"]) // 3
            assert m["residual"] < 1e-7 and m["balance"] < 1e-6
            if data["analysis"] == "structural":
                s = np.array(m["tensor"]).reshape(faces, 6)
                p = np.array(m["principal"]).reshape(faces, 3)
                assert np.isfinite(s).all() and np.isfinite(p).all()
                vm = np.sqrt(((s[:, 0]-s[:, 1])**2+(s[:, 1]-s[:, 2])**2 +
                              (s[:, 2]-s[:, 0])**2)/2+3*(s[:, 3:]**2).sum(axis=1))
                error = float(np.max(np.abs(vm-np.array(m["stress"]))))
                assert error < 2e-6
                matrix = np.zeros((faces, 3, 3))
                matrix[:, [0, 1, 2], [0, 1, 2]] = s[:, :3]
                matrix[:, 0, 1] = matrix[:, 1, 0] = s[:, 3]
                matrix[:, 1, 2] = matrix[:, 2, 1] = s[:, 4]
                matrix[:, 0, 2] = matrix[:, 2, 0] = s[:, 5]
                eigen_error = float(np.max(np.abs(np.linalg.eigvalsh(matrix)[:, ::-1]-p)))
                assert eigen_error < 2e-6
                checks.append(dict(misesErrorMPa=error, principalErrorMPa=eigen_error))
            else:
                flux = np.array(m["flux"]).reshape(faces, 3)
                assert np.isfinite(flux).all() and np.max(np.linalg.norm(flux, axis=1)) > 0
                assert abs(m["heatOutW"]/m["powerW"]-1) < 1e-6
                checks.append(dict(maxSurfaceFluxWm2=float(np.max(np.linalg.norm(flux, axis=1)))))
        if data["code"] == "F-CRACK":
            assert data["models"][1]["elements"] == 18576
            for m, expected in zip(data["models"], (.008, .0056)):
                x = np.array(m["x"]).reshape(-1, 3)
                ids = (x[:, 0] > .24*.16+1e-6) & (x[:, 0] < .24*.20-1e-6)
                assert abs(np.ptp(x[ids, 2])-expected) < 1e-8
        if data["code"] == "TK-LEAK":
            for m, expected in zip(data["models"], (.006, .003)):
                x = np.array(m["x"]).reshape(-1, 3)
                ids = np.isclose(x[:, 0], .4) & (np.abs(x[:, 2]) < 1e-8) & (x[:, 1] > 0)
                assert abs(np.ptp(x[ids, 1])-expected) < 1e-8
        entries.append(dict(code=data["code"], sha256=hashlib.sha256(path.read_bytes()).hexdigest(), checks=checks))
    assert len(entries) == 21
    result = dict(time=datetime.now().astimezone().isoformat(), status="PASS", cases=entries,
                  scope="Saved field consistency; not Abaqus execution or actual equipment calibration.")
    out = ROOT / ("Evidence/fem-enriched-verification-" + datetime.now().strftime("%Y%m%d-%H%M%S") + ".json")
    out.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"PASS: {len(entries)} cases; tensor invariants, flux balance, refined notch and thickness geometry.")


if __name__ == "__main__":
    main()
