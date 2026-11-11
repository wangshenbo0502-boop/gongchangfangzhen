"""Reproducible reference-component FEM, SI inputs, no proprietary solver claims."""
import argparse
import hashlib
import json
import math
from datetime import datetime
from functools import lru_cache
from pathlib import Path

import numpy as np
import scipy
import skfem
from skfem import Basis, FacetBasis, ElementTetP1, ElementTetP2, ElementVector, MeshTet
from skfem import BilinearForm, LinearForm, asm, condense, solve
from skfem.helpers import dot, grad, sym_grad, trace, ddot
from skfem.models.elasticity import linear_elasticity
from scipy.sparse import diags
from scipy.sparse.linalg import splu

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets/StreamingAssets/WorkshopAnalysis"
E, NU, K = 210e9, .3, 45.
LAM, MU = E * NU / ((1 + NU) * (1 - 2 * NU)), E / (2 * (1 + NU))


def accurate_matvec(matrix, vector):
    csr = matrix.tocsr()
    products = csr.data * vector[csr.indices]
    return np.fromiter((math.fsum(products[csr.indptr[i]:csr.indptr[i+1]])
                        for i in range(csr.shape[0])), dtype=float, count=csr.shape[0])


def block(length, width, height, defect=0., refinement=1):
    # Defect is an open, finite-width notch, not a propagating mathematical crack.
    xs = np.unique(np.r_[np.linspace(0, length, 17 * refinement),
                         length * .16, length * .20])
    ys = np.linspace(-width / 2, width / 2, 5 * refinement)
    zs = np.unique(np.r_[np.linspace(-height / 2, height / 2, 5 * refinement),
                         height / 2 - defect])
    m = MeshTet.init_tensor(xs, ys, zs)
    if defect > 0:
        c = m.p[:, m.t].mean(axis=1)
        m = m.remove_elements(np.flatnonzero((c[0] > length * .16) &
                              (c[0] < length * .20) & (c[2] > height / 2 - defect)))
    return m


def tube(length, radius, thickness, thinning=0., refinement=1):
    # A periodic mapped tetrahedral mesh; merge coincident angular seam nodes.
    m = MeshTet.init_tensor(np.linspace(0, length, 13 * refinement),
                           np.linspace(0, 1, 3 * refinement),
                           np.linspace(0, 2 * np.pi, 33 * refinement))
    x, r, a = m.p
    local = np.exp(-((x - length * .5) / (length * .16)) ** 4 -
                   ((np.arctan2(np.sin(a), np.cos(a))) / .5) ** 4)
    inner = radius - thickness + thinning * local
    radial = inner + r * (radius - inner)
    pts = np.array([x, radial * np.cos(a), radial * np.sin(a)])
    _, unique, inverse = np.unique(np.round(pts.T, 11), axis=0,
                                   return_index=True, return_inverse=True)
    return MeshTet(pts[:, unique], inverse[m.t])


def boundaries(m, length):
    return m.facets_satisfying(lambda x: np.isclose(x[0], length)), \
        m.facets_satisfying(lambda x: np.isclose(x[0], 0))


def surface(m, displacement, stress, temperature, quadratic=False, tensor=None, flux=None):
    faces = m.boundary_facets()
    tri = m.facets[:, faces].T.copy()
    owner = m.f2t[0, faces]
    # Consistent outward orientation, used for normals and readable wireframes.
    p = m.p.T
    normals = np.cross(p[tri[:, 1]] - p[tri[:, 0]], p[tri[:, 2]] - p[tri[:, 0]])
    inside = p[m.t[:, owner]].mean(axis=0) - p[tri].mean(axis=1)
    flip = (normals * inside).sum(axis=1) > 0
    tri[flip] = tri[flip][:, [0, 2, 1]]
    if quadratic:
        scalar = Basis(m, ElementTetP2())
        edge_map = {tuple(sorted(e)): int(scalar.edge_dofs[0, i])
                    for i, e in enumerate(m.edges.T)}
        children = []
        for a, b, c in tri:
            ab, bc, ca = [edge_map[tuple(sorted(e))] for e in ((a,b),(b,c),(c,a))]
            children.extend(((a,ab,ca),(ab,b,bc),(ca,bc,c),(ab,bc,ca)))
        tri = np.array(children)
        owner = np.repeat(owner, 4)
        p = scalar.doflocs.T
    result = dict(x=np.round(p.reshape(-1), 10).tolist(), tri=tri.reshape(-1).tolist(),
                u=np.round(displacement.T.reshape(-1), 12).tolist(),
                stress=np.round(stress[owner] / 1e6, 6).tolist(),
                temperature=np.round(temperature, 5).tolist(),
                nodes=int(len(p)), elements=int(m.nelements),
                elementType="Tet10 / 二次位移" if quadratic else "Tet4 / 一次温度")
    if tensor is not None:
        # Engineering xyz order, symmetric stress in MPa, one tensor per boundary triangle.
        components = tensor[[0, 1, 2, 0, 1, 0], [0, 1, 2, 1, 2, 2]].T / 1e6
        principal = np.linalg.eigvalsh(np.moveaxis(tensor, 2, 0))[:, ::-1] / 1e6
        result["tensor"] = np.round(components[owner].reshape(-1), 7).tolist()
        result["principal"] = np.round(principal[owner].reshape(-1), 7).tolist()
    if flux is not None:
        result["flux"] = np.round(flux[:, owner].T.reshape(-1), 6).tolist()
    return result


def mechanical(m, length, force, axis=2, pressure=0., radius=0., thickness=0.):
    b = Basis(m, ElementVector(ElementTetP2()))
    a = asm(linear_elasticity(LAM, MU), b)
    end, fixed = boundaries(m, length)
    fb = FacetBasis(m, b.elem, facets=end)
    area = float(np.sum(fb.dx))
    direction = np.eye(3)[axis] * force / area

    @LinearForm
    def traction(v, w):
        return dot(direction, v)

    f = asm(traction, fb)
    if pressure:
        # Pressure on the inner cylindrical wall, including the locally thinned patch.
        fs = m.boundary_facets()
        mid = m.p[:, m.facets[:, fs]].mean(axis=1)
        # Classify the actual face vertices: a polygonal outer-face centroid
        # lies inside the nominal radius and must never receive internal pressure.
        rr = np.sqrt(m.p[1, m.facets[:, fs]] ** 2 +
                     m.p[2, m.facets[:, fs]] ** 2).max(axis=0)
        inner = fs[(mid[0] > 1e-8) & (mid[0] < length - 1e-8) &
                   (rr < radius - thickness * .15)]
        pfb = FacetBasis(m, b.elem, facets=inner)

        @LinearForm
        def internal(v, w):
            return -pressure * dot(w.n, v)

        f += asm(internal, pfb)
    d = b.get_dofs(facets=fixed).all()
    reduced, rhs, u, free = condense(a, f, D=d)
    # Equilibrate the thin-component system and refine the direct solution.
    scaling = 1 / np.sqrt(reduced.diagonal())
    scale = diags(scaling)
    lu = splu((scale @ reduced @ scale).tocsc())
    solution = scaling * lu.solve(scaling * rhs)
    for _ in range(3):
        correction = rhs - accurate_matvec(reduced, solution)
        solution += scaling * lu.solve(scaling * correction)
    u[free] = solution
    residual = accurate_matvec(a, u) - f
    relative = float(np.linalg.norm(residual[free]) / max(np.linalg.norm(f), 1e-20))
    reactions = np.array([residual[d[d % 3 == i]].sum() for i in range(3)])
    applied = np.array([f[i::3].sum() for i in range(3)])
    balance = float(np.linalg.norm(reactions + applied) / max(np.linalg.norm(f), 1e-20))
    strain = sym_grad(b.interpolate(u)).mean(axis=-1)
    sigma = 2 * MU * strain + LAM * np.einsum("ii...->...", strain)[None, None] * np.eye(3)[:, :, None]
    dev = sigma - np.einsum("ii...->...", sigma)[None, None] * np.eye(3)[:, :, None] / 3
    mises = np.sqrt(1.5 * np.einsum("ij...,ij...->...", dev, dev))
    disp = u.reshape(-1, 3).T
    result = surface(m, disp, mises, np.zeros(disp.shape[1]), quadratic=True, tensor=sigma)
    result.update(maxStress=float(mises.max() / 1e6), maxDisplacement=float(
        np.linalg.norm(disp, axis=0).max() * 1000), maxTemperature=0.,
        residual=relative, balance=balance, energy=float(u @ (a @ u) / 2),
        reactionN=reactions.tolist(), appliedN=applied.tolist(), volumeM3=float(b.dx.sum()))
    assert relative < 1e-7 and balance < 1e-6, (relative, balance, m.nelements)
    return result


def thermal(m, power, convection, ambient=25.):
    b = Basis(m, ElementTetP1())
    fb = FacetBasis(m, b.elem)
    volume = float(b.dx.sum())
    # Localized heat source; normalized to specified total power by quadrature.
    span = np.ptp(m.p[0])
    center = m.p.mean(axis=1)

    def shape(x):
        return np.exp(-((x[0] - span * .55) / (span * .15)) ** 2)

    weight = float(np.sum(shape(b.global_coordinates()) * b.dx))

    @BilinearForm
    def conduction(u, v, w):
        return K * dot(grad(u), grad(v))

    @BilinearForm
    def film(u, v, w):
        return convection * u * v

    @LinearForm
    def source(v, w):
        return power / weight * shape(w.x) * v

    @LinearForm
    def air(v, w):
        return convection * ambient * v

    a = asm(conduction, b) + asm(film, fb)
    f = asm(source, b) + asm(air, fb)
    t = solve(a, f)
    relative = float(np.linalg.norm(a @ t - f) / np.linalg.norm(f))
    heat_out = float(np.sum(convection * (fb.interpolate(t) - ambient) * fb.dx))
    balance = abs(heat_out - power) / power
    assert relative < 1e-7 and balance < 1e-7
    result = surface(m, np.zeros_like(m.p), np.zeros(m.nelements), t,
                     flux=-K * grad(b.interpolate(t)).mean(axis=-1))
    result.update(maxStress=0., maxDisplacement=0., maxTemperature=float(t.max()),
                  residual=relative, balance=balance, energy=0., powerW=power,
                  heatOutW=heat_out, volumeM3=volume)
    return result


CASES = [
    # code, component, shape, mechanism, variable, normal value, fault value, load axis
    ("CV-JAM", "输送机承载梁", "beam", "卡阻引起附加横向载荷的静力包络", "force", 300, 1200, 2),
    ("CV-TRACK", "输送机承载梁", "beam", "跑偏引起附加侧向载荷的静力包络", "force", 100, 450, 1),
    ("CV-BEAR", "滚筒轴承座参考套筒", "shaft", "摩擦热源增大；未求解轴承滚动接触", "heat", 8, 35, 2),
    ("M-OVER", "电机轴套参考件", "shaft", "给定损耗热源；未求解电磁场或绕组", "heat", 15, 60, 2),
    ("M-BEAR", "电机轴套参考件", "shaft", "磨损区壁厚减薄 40%，同载荷对比", "thin", 0, .004, 2),
    ("M-PHASE", "电机轴套参考件", "shaft", "缺相后假设损耗热源；不是缺相电磁诊断", "heat", 15, 75, 2),
    ("M-ALIGN", "电机轴套参考件", "shaft", "不对中附加横向力；未求解动态振幅", "force", 150, 650, 2),
    ("P-HOT", "泵出口管参考件", "pipe", "同热源、散热系数降低的稳态导热", "film", 30, 6, 2),
    ("P-CAV", "泵出口管参考件", "pipe", "给定压力包络下结构响应；未求解汽蚀或流场", "pressure", 2e5, 6e5, 2),
    ("P-SEAL", "密封支撑参考梁", "beam", "有限宽开口缺口，同载荷；未求解密封接触或漏率", "notch", 0, .008, 2),
    ("P-BEAR", "泵轴套参考件", "shaft", "磨损区壁厚减薄 50%，同载荷对比", "thin", 0, .005, 2),
    ("TK-PRESS", "罐壁参考圆筒段", "tank", "内压结构响应；无封头、焊缝与塑性", "pressure", 3e5, 9e5, 2),
    ("TK-LEAK", "罐壁参考圆筒段", "tank", "局部腐蚀减薄 50%；壁面尚未贯穿，不预测漏率", "thin", 0, .003, 2),
    ("TK-LEVEL", "罐壁参考圆筒段", "tank", "给定均匀静水压上包络；不求解液位控制", "pressure", 5e4, 9e4, 2),
    ("TK-SUPPORT", "储罐支座参考梁", "support", "支座开口缺口深 12 mm，同载荷对比", "notch", 0, .012, 2),
    ("F-IMBAL", "风机叶片参考件", "blade", "等效附加弯曲载荷；不是转子动力学振幅", "force", 15, 65, 2),
    ("F-BLOCK", "风机叶片参考件", "blade", "风阻等效叶尖载荷包络；不计算风量", "force", 15, 45, 2),
    ("F-BEAR", "风机轴套参考件", "shaft", "摩擦热源增大；未求解抱死接触", "heat", 8, 45, 2),
    ("F-CRACK", "风机叶片参考件", "blade", "有限宽裂口深 2.4 mm，同载荷；无裂纹扩展、J积分或寿命", "notch", 0, .0024, 2),
    ("PLC-HOT", "柜内钢制散热板参考件", "plate", "给定热源、对流换热退化；非整柜流热耦合", "film", 15, 3, 2),
    ("PLC-CONTACT", "端子散热板参考件", "plate", "给定局部焦耳损耗热源；未求解接触电阻", "heat", 5, 20, 2),
]
GEOM = {
    "beam": (.5, .06, .04), "support": (.3, .08, .06),
    "blade": (.24, .06, .008), "plate": (.2, .12, .006),
    "shaft": (.24, .04, .01), "pipe": (.3, .06, .008),
    "tank": (.8, .3, .006),
}


def make_mesh(shape, defect, var, ref=1):
    dim = GEOM[shape]
    if shape in ("shaft", "pipe", "tank"):
        return tube(*dim, thinning=defect if var == "thin" else 0., refinement=ref)
    return block(*dim, defect=defect if var == "notch" else 0., refinement=ref)


def compute(case, value, ref=1):
    code, _, shape, _, var, _, _, axis = case
    return compute_component(shape, var, value, axis, ref)


@lru_cache(maxsize=64)
def compute_component(shape, var, value, axis, ref):
    m = make_mesh(shape, value, var, ref)
    length, width, depth = GEOM[shape]
    if var in ("heat", "film"):
        return m, thermal(m, value if var == "heat" else 12.,
                          value if var == "film" else 12.)
    force = value if var == "force" else (30. if shape == "blade" else 250.)
    pressure = value if var == "pressure" else (3e5 if shape == "tank" else 0.)
    if pressure:
        force = 0.
    return m, mechanical(m, length, force, axis, pressure, width, depth)


def abaqus_input(path, m, case, value):
    # Same mechanical model for an independently licensed Abaqus/Standard run.
    _, _, shape, _, var, _, _, axis = case
    if var in ("heat", "film"):
        return
    length, radius, thickness = GEOM[shape]
    scalar = Basis(m, ElementTetP2())
    lines = ["*Heading", "Reference component; SI m N Pa. Not actual equipment CAD.",
             "*Node"]
    lines += [f"{i+1}, {p[0]:.11g}, {p[1]:.11g}, {p[2]:.11g}"
              for i, p in enumerate(scalar.doflocs.T)]
    lines += ["*Element, type=C3D10, elset=SOLID"]
    connectivity = scalar.element_dofs.T.copy()
    corners = m.p[:, m.t]
    negative = np.linalg.det(np.moveaxis(corners[:, 1:] - corners[:, :1], 2, 0)) < 0
    # Abaqus requires positive tetrahedron orientation, including matching edge nodes.
    connectivity[negative] = connectivity[negative][:, [1, 0, 2, 3, 4, 6, 5, 8, 7, 9]]
    lines += [f"{i+1}, " + ", ".join(str(j+1) for j in t) for i, t in enumerate(connectivity)]
    lines += ["*Material, name=STEEL", "*Elastic", f"{E}, {NU}",
              "*Solid Section, elset=SOLID, material=STEEL", ",",
              "*Nset, nset=FIXED"]
    fixed = np.flatnonzero(np.isclose(scalar.doflocs[0], 0))
    lines += [", ".join(str(j+1) for j in fixed[i:i+12]) for i in range(0, len(fixed), 12)]
    # Use the identical assembled equivalent nodal loads, including pressure.
    b = Basis(m, ElementVector(ElementTetP2()))
    end, _ = boundaries(m, length)
    fb = FacetBasis(m, b.elem, facets=end)
    force = value if var == "force" else (30. if shape == "blade" else 250.)
    pressure = value if var == "pressure" else (3e5 if shape == "tank" else 0.)
    direction = np.eye(3)[axis] * (0. if pressure else force) / fb.dx.sum()

    @LinearForm
    def load(v, w):
        return dot(direction, v)

    f = asm(load, fb)
    if pressure:
        fs = m.boundary_facets()
        mid = m.p[:, m.facets[:, fs]].mean(axis=1)
        rr = np.sqrt(m.p[1, m.facets[:, fs]] ** 2 +
                     m.p[2, m.facets[:, fs]] ** 2).max(axis=0)
        faces = fs[(mid[0] > 1e-8) & (mid[0] < length - 1e-8) & (rr < radius - thickness * .15)]

        @LinearForm
        def p_load(v, w):
            return -pressure * dot(w.n, v)

        f += asm(p_load, FacetBasis(m, b.elem, facets=faces))
    lines += ["*Boundary", "FIXED, 1, 3", "*Step, name=STATIC_REFERENCE",
              "*Static", "1., 1.", "*Cload"]
    lines += [f"{i//3+1}, {i%3+1}, {x:.12g}" for i, x in enumerate(f) if abs(x) > 1e-15]
    lines += ["*Output, field", "*Node Output", "U, RF", "*Element Output",
              "S, E", "*End Step"]
    path.write_text("\n".join(lines) + "\n", encoding="ascii")


def verify():
    # Exact 3D uniaxial tension with nu=0 and matching end traction.
    m = block(.2, .04, .03)
    b = Basis(m, ElementVector(ElementTetP1()))
    end, fixed = boundaries(m, .2)
    fb = FacetBasis(m, b.elem, facets=end)

    @LinearForm
    def load(v, w):
        return 1e6 * v[0]

    a = asm(linear_elasticity(0., E / 2), b)
    f = asm(load, fb)
    u = solve(*condense(a, f, D=b.get_dofs(facets=fixed).all()))
    expected = 1e6 * .2 / E
    actual = u[b.nodal_dofs[0, np.isclose(m.p[0], .2)]].mean()
    error = float(abs(actual / expected - 1))
    assert error < 1e-8
    return dict(axialExactM=expected, axialFemM=float(actual), axialRelativeError=error)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--only", default="")
    parser.add_argument("--verify-existing", action="store_true",
                        help="Verify saved fields and recompute independent reference checks.")
    args = parser.parse_args()
    OUT.mkdir(parents=True, exist_ok=True)
    inputs = ROOT / "Tools/FiniteElement/AbaqusInputs"
    inputs.mkdir(exist_ok=True)
    report = dict(time=datetime.now().astimezone().isoformat(),
                  solver=f"scikit-fem {skfem.__version__}; scipy {scipy.__version__}",
                  verification=verify(), cases=[])
    for c in ([] if args.verify_existing else CASES):
        code, component, shape, mechanism, var, v0, v1, axis = c
        if args.only and args.only != code:
            continue
        models = []
        for label, value in (("完好 / 基准", v0), ("故障 / 缺陷", v1)):
            m, result = compute(c, value, ref=2 if code in ("F-CRACK", "PLC-HOT") else 1)
            result = dict(result)
            result["label"] = label
            models.append(result)
            abaqus_input(inputs / f"{code}-{'normal' if value == v0 else 'fault'}.inp", m, c, value)
        dim = GEOM[shape]
        units = "W" if var == "heat" else "W/(m²·K)" if var == "film" else \
            "Pa" if var == "pressure" else "m" if var in ("notch", "thin") else "N"
        names = dict(heat="给定损耗功率", film="对流换热系数", pressure="内壁压力",
                     force="端面总载荷", notch="开口缺口深度", thin="局部壁厚损失")
        detail = f"{names[var]}：{v0:g} → {v1:g} {units}"
        if var == "notch":
            detail += f"；开口宽 {dim[0]*.04*1000:g} mm，位于长度 16%–20%"
        if var == "thin":
            detail += f"；原壁厚 {dim[2]*1000:g} → 最小 {(dim[2]-v1)*1000:g} mm，位于轴向中段"
        if var in ("notch", "thin"):
            detail += f"；两模型载荷相同：{'内压 0.3 MPa' if shape == 'tank' else '端载荷 '+str(30 if shape == 'blade' else 250)+' N'}"
        metadata = dict(schema=1, code=code, component=component, solver=report["solver"],
                        source="真实数值求解 / 参数化参考件 / 非 Abaqus",
                        analysis="thermal" if var in ("heat", "film") else "structural",
                        geometry=f"{'长 / 外半径 / 原壁厚' if shape in ('shaft','pipe','tank') else '长 / 宽 / 厚'}："
                                 + " / ".join(f"{d*1000:g}" for d in dim) + " mm",
                        material=f"参考钢：E=210 GPa，ν=0.30，k=45 W/(m·K)；常数材料",
                        boundary="全表面对流，环境 25 °C，局部体热源" if var in ("heat", "film")
                                 else "x=0 端面全部平移固定；其余自由；小变形线弹性",
                        change=detail, mechanism=mechanism,
                        limitation="未标定实际设备；未求解接触、塑性、裂纹扩展或疲劳寿命。"
                                   "应力按二次四面体单元平均应力张量的 Mises 显示；尖角峰值依赖网格，不作工程验收。",
                        shape=shape, dimensionsMm=[d * 1000 for d in dim],
                        variable=var, inputValues=[v0, v1], models=models)
        # Common comparison scale, never normalize each case independently.
        metadata["stressRange"] = max(r["maxStress"] for r in models)
        metadata["displacementRange"] = max(r["maxDisplacement"] for r in models)
        metadata["temperatureRange"] = max(r["maxTemperature"] for r in models)
        path = OUT / f"{code}.json"
        path.write_text(json.dumps(metadata, ensure_ascii=False, separators=(",", ":")),
                        encoding="utf-8")
        report["cases"].append(dict(code=code, sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                                   normal={k: models[0][k] for k in ("maxStress","maxDisplacement","maxTemperature","residual","balance")},
                                   fault={k: models[1][k] for k in ("maxStress","maxDisplacement","maxTemperature","residual","balance")}))
        print(code, component, [(r["nodes"], r["elements"], round(r["maxStress"],3),
                                round(r["maxDisplacement"],5), round(r["maxTemperature"],2)) for r in models], flush=True)
    if args.verify_existing:
        for c in CASES:
            path = OUT / f"{c[0]}.json"
            data = json.loads(path.read_text(encoding="utf-8"))
            assert data["code"] == c[0] and len(data["models"]) == 2
            for model in data["models"]:
                assert model["residual"] < 1e-7 and model["balance"] < 1e-6
            report["cases"].append(dict(code=c[0], sha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                normal={k: data["models"][0][k] for k in ("maxStress","maxDisplacement","maxTemperature","residual","balance")},
                fault={k: data["models"][1][k] for k in ("maxStress","maxDisplacement","maxTemperature","residual","balance")}))
    # A meaningful mesh sensitivity check on the visible notch, using displacement/compliance.
    if not args.only:
        c = next(c for c in CASES if c[0] == "F-CRACK")
        _, coarse = compute(c, c[6])
        _, fine = compute(c, c[6], ref=2)
        report["meshSensitivity"] = dict(code="F-CRACK", coarseElements=coarse["elements"],
            fineElements=fine["elements"], coarseDisplacementMm=coarse["maxDisplacement"],
            fineDisplacementMm=fine["maxDisplacement"],
            displacementChange=float(abs(fine["maxDisplacement"]/coarse["maxDisplacement"]-1)),
            coarsePeakMPa=coarse["maxStress"], finePeakMPa=fine["maxStress"],
            fineResidual=fine["residual"], fineBalance=fine["balance"],
            note="Peak at sharp notch not certified converged; no fracture-life claim.")
        normal = compute(c, c[5])[1]
        length, width, depth = GEOM["blade"]
        analytical = 30 * length ** 3 / (3 * E * width * depth ** 3 / 12) * 1000
        report["verification"]["cantileverBeamMm"] = analytical
        report["verification"]["cantileverFemMm"] = normal["maxDisplacement"]
        report["verification"]["cantileverRelativeDifference"] = abs(normal["maxDisplacement"]/analytical-1)
        assert report["verification"]["cantileverRelativeDifference"] < .05
        assert report["meshSensitivity"]["displacementChange"] < .08
        tank = next(c for c in CASES if c[0] == "TK-PRESS")
        _, cylinder = compute(tank, tank[5])
        length, outer, thick = GEOM["tank"]
        inner = outer - thick
        exact = 2 * tank[5] * inner ** 2 * outer / (E * (outer ** 2 - inner ** 2)) * 1000
        points = np.array(cylinder["x"]).reshape(-1, 3)
        disp = np.array(cylinder["u"]).reshape(-1, 3)
        ids = np.isclose(points[:, 0], length) & np.isclose(np.linalg.norm(points[:, 1:], axis=1), outer)
        radial = np.sum(disp[ids, 1:] * points[ids, 1:], axis=1) / outer * 1000
        report["verification"]["lameOuterDisplacementMm"] = exact
        report["verification"]["cylinderFemOuterMm"] = float(radial.mean())
        report["verification"]["cylinderRelativeDifference"] = float(abs(radial.mean() / exact - 1))
        assert report["verification"]["cylinderRelativeDifference"] < .08
    evidence = ROOT / "Evidence/fem-reference-verification-20261109.json"
    evidence.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print("VERIFIED", evidence, flush=True)


if __name__ == "__main__":
    main()
