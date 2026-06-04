"""
离心风机蜗壳设计

等边基元法（等环量法）—— 工程最常用的蜗壳型线设计方法。
与叶轮设计结果自动匹配，组成整机。

蜗壳参数：
  B    — 蜗壳宽度 (mm)
  A    — 蜗壳展开宽度 (mm)
  R(θ) — 外壁半径，从蜗舌到出口线性增大

建模输出：VBA 宏（SW 直接运行）+ 坐标 CSV
"""

import math
import csv
import os
from dataclasses import dataclass, field
from typing import Optional


# ═══════════════════════════════════════════════════════════════
# 蜗壳参数模型
# ═══════════════════════════════════════════════════════════════


@dataclass
class VoluteParams:
    """蜗壳全部设计参数"""
    # ── 匹配的叶轮参数 ──
    D2: float           # 叶轮外径 mm
    b2: float           # 叶片出口宽度 mm
    D0: float           # 叶轮进口直径 mm

    # ── 设计输入 ──
    Q: float            # 流量 m³/h
    P: float            # 全压 Pa

    # ── 蜗壳尺寸 ──
    B: float = 0.0      # 蜗壳宽度 mm
    A: float = 0.0      # 展开宽度 mm
    delta: float = 0.0  # 蜗舌间隙 mm
    r_tongue: float = 0.0  # 蜗舌半径 mm
    theta_start: float = 25.0  # 蜗舌起始角 °

    # ── 出口 ──
    outlet_w: float = 0.0  # 出口宽度 mm
    outlet_h: float = 0.0  # 出口高度 mm
    outlet_v: float = 0.0  # 出口风速 m/s

    # ── 壁厚 ──
    wall_thk: float = 4.0

    @property
    def R2(self) -> float:
        """叶轮半径 mm"""
        return self.D2 / 2.0

    @property
    def summary(self) -> str:
        return (
            f"  蜗壳设计（匹配叶轮 D₂={self.D2:.0f}mm）\n"
            f"    宽度 B = {self.B:.0f}mm\n"
            f"    展开 A = {self.A:.0f}mm  (A/D₂={self.A/self.D2:.2f})\n"
            f"    蜗舌间隙 δ = {self.delta:.1f}mm\n"
            f"    出口 {self.outlet_w:.0f}×{self.outlet_h:.0f}mm  "
            f"风速 {self.outlet_v:.1f}m/s\n"
            f"    壁厚 {self.wall_thk:.0f}mm"
        )


# ═══════════════════════════════════════════════════════════════
# 蜗壳设计计算
# ═══════════════════════════════════════════════════════════════


def design_volute(
    D2: float,
    b2: float,
    D0: float,
    Q: float,
    P: float,
    width_ratio: float = 0.0,
    expand_ratio: float = 0.0,
    wall_thk: float = 4.0,
    target_outlet_v: float = 12.0,
) -> VoluteParams:
    """
    蜗壳设计主入口

    自动匹配叶轮参数，计算全部蜗壳尺寸。

    Args:
        D2: 叶轮外径 mm
        b2: 叶片出口宽度 mm
        D0: 叶轮进口直径 mm
        Q: 流量 m³/h
        P: 全压 Pa
        width_ratio: 蜗壳宽度比 B/b₂（0=自动取 2.0）
        expand_ratio: 展开比 A/D₂（0=自动估算）
        wall_thk: 壁厚 mm
        target_outlet_v: 目标出口风速 m/s

    Returns:
        VoluteParams
    """
    R2 = D2 / 2.0

    # ── Step 1: 蜗壳宽度 B ──
    B = b2 * (width_ratio or 2.0)
    B = _round_step(B, 5)

    # ── Step 2: 展开宽度 A ──
    # 先按经验取 A/D₂ = 0.45
    A_est = D2 * (expand_ratio or 0.45)
    # 按出口风速校核
    v_test = Q / 3600.0 / (B / 1000.0) / (A_est / 1000.0)
    if v_test < 8:
        A_est *= v_test / target_outlet_v
    elif v_test > 16:
        A_est *= v_test / target_outlet_v

    A = _round_step(A_est, 5)
    v_out = Q / 3600.0 / (B / 1000.0) / (A / 1000.0)

    # ── Step 3: 蜗舌 ──
    delta = 0.10 * R2  # 蜗舌间隙
    r_t = 0.04 * R2    # 蜗舌半径

    # ── Step 4: 出口 ──
    outlet_w = B
    outlet_h = A
    if v_out < 6:
        # 出口风速太低，减小出口面积
        outlet_h = Q / 3600.0 / (B / 1000.0) / target_outlet_v * 1000.0
        v_out = target_outlet_v

    return VoluteParams(
        D2=D2, b2=b2, D0=D0, Q=Q, P=P,
        B=B, A=A,
        delta=delta, r_tongue=r_t,
        outlet_w=_round_step(outlet_w, 5),
        outlet_h=_round_step(outlet_h, 5),
        outlet_v=round(v_out, 1),
        wall_thk=wall_thk,
    )


def volute_profile(v: VoluteParams, n_points: int = 72) -> list[dict]:
    """
    生成蜗壳外壁型线（极坐标）

    等边基元法：
      R(θ) = R₂ + A × θ / 360°
      θ 从蜗舌起始角到 360°+起始角

    Returns:
        [{theta, R, x, y}, ...]
    """
    R2 = v.R2
    A = v.A

    points = []
    for i in range(n_points):
        t = i / n_points
        theta_deg = v.theta_start + t * 360.0
        theta = math.radians(theta_deg)

        # 展开半径
        R = R2 + A * t
        x = R * math.cos(theta)
        y = R * math.sin(theta)

        points.append({
            "theta": round(theta_deg, 2),
            "R": round(R, 1),
            "x": round(x, 2),
            "y": round(y, 2),
        })

    return points


def volute_inner_profile(v: VoluteParams, n_points: int = 36) -> list[dict]:
    """
    蜗壳内壁型线（叶轮外径去除区域）

    内壁就是叶轮外径圆弧 + 蜗舌区域
    """
    R2 = v.R2
    theta_start = v.theta_start
    points = []

    # 叶轮外径圆弧（从蜗舌到 360°）
    for i in range(n_points):
        t = i / n_points
        theta_deg = theta_start + t * (360.0 - theta_start)
        theta = math.radians(theta_deg)
        x = R2 * math.cos(theta)
        y = R2 * math.sin(theta)
        points.append({
            "theta": round(theta_deg, 2), "R": round(R2, 1),
            "x": round(x, 2), "y": round(y, 2),
        })

    return points


# ═══════════════════════════════════════════════════════════════
# 与叶轮设计自动匹配
# ═══════════════════════════════════════════════════════════════


def match_impeller(design_result) -> VoluteParams:
    """
    从叶轮设计结果自动计算蜗壳参数

    Args:
        design_result: ImpellerDesignResult

    Returns:
        VoluteParams
    """
    p = design_result
    return design_volute(
        D2=p.D2,
        b2=p.b2,
        D0=p.D0,
        Q=p.input_params.Q,
        P=p.input_params.P,
    )


# ═══════════════════════════════════════════════════════════════
# VBA 宏（SW 建模）
# ═══════════════════════════════════════════════════════════════


def generate_vba_macro(v: VoluteParams, full_profile: list[dict]) -> str:
    """
    生成蜗壳 VBA 宏

    建模步骤：
    1. 在顶视基准面画蜗壳外壁型线（样条曲线）
    2. 拉伸 B 宽度 → 蜗壳体
    3. 切出叶轮内腔
    4. 切出进口孔
    """
    R2 = v.R2

    # 型线点转 VBA 数组
    n = len(full_profile)

    def m(val):
        return f"{val / 1000.0:.6f}"

    # 生成外壁样条点
    spline_pts = []
    for idx, pt in enumerate(full_profile):
        spline_pts.append(
            f"  sp({idx * 2}) = {m(pt['x'])}: sp({idx * 2 + 1}) = {m(pt['y'])}"
        )
    spline_block = "\n".join(spline_pts)

    macro = f"""' SolidWorks VBA Macro - Centrifugal Fan Volute
' Matching impeller: D2={v.D2:.0f}mm  b2={v.b2:.0f}mm
' Volute: B={v.B:.0f}mm  A={v.A:.0f}mm  delta={v.delta:.1f}mm
' ============================================================

Dim swApp As Object
Dim Part As Object
Dim skMgr As Object
Dim featMgr As Object
Dim boolStat As Boolean

Sub main()
    Set swApp = Application.SldWorks
    Set Part = swApp.NewDocument("", 0, 0, 0)
    swApp.Visible = True
    Set skMgr = Part.SketchManager
    Set featMgr = Part.FeatureManager

    ' ============================================================
    ' Step 1: Volute outer wall profile (spline, Top Plane)
    ' ============================================================
    ' Switch to Top Plane
    Part.Extension.SelectByID2 "Front Plane", "PLANE", 0, 0, 0, False, 0, Nothing, 0
    skMgr.InsertSketch True

    ' Select the plane (already in sketch mode on Front)
    
    ' Draw outer profile as spline
    Dim sp(0 To {n * 2 - 1}) As Double
{spline_block}

    Dim splineWall As Object
    Set splineWall = skMgr.CreateSpline(sp)
    skMgr.InsertSketch True
    
    ' Draw impeller clearance circle (R2 + delta)
    skMgr.InsertSketch True
    Part.CreateCircle2 0, 0, 0, {m(R2)}, 0, 0
    skMgr.InsertSketch True

    ' ============================================================
    ' Step 2: Extrude volute body (width B={v.B:.0f}mm)
    ' ============================================================
    Part.ClearSelection2 True
    ' Select the outer profile area for extrusion
    ' (In practice: select the bounded region between outer wall and inner circle)
    
    boolStat = featMgr.FeatureExtrusion2( _
        True, False, False, 0#, 0, 1, {m(v.B)}, 0, 0, 0, 0, True, _
        0, 0#, 0#, 0, False, False)

    ' ============================================================
    ' Step 3: Cut inlet hole (diameter D0={v.D0:.0f}mm)
    ' ============================================================
    ' Select side face for sketch
    ' Draw inlet circle
    ' Cut-extrude through
    
    Part.ViewZoomtofit2
    MsgBox "Fan Volute D2={v.D2:.0f}mm  B={v.B:.0f}mm  A={v.A:.0f}mm", _
           vbInformation, "solidworks-parametric"
End Sub
"""
    return macro


def _round_step(v: float, step: float) -> float:
    return round(v / step) * step


# ═══════════════════════════════════════════════════════════════
# 导出
# ═══════════════════════════════════════════════════════════════


def export_csv(points: list[dict], path: str):
    """导出蜗壳型线 CSV（SW 曲线文件格式）"""
    with open(path, "w", newline="") as f:
        w = csv.DictWriter(f, fieldnames=["x", "y", "theta", "R"], extrasaction="ignore")
        w.writeheader()
        for pt in points:
            w.writerow(pt)
    print(f"  📄 {path} ({len(points)} pts)")


def export_sw_curve(points_3d: list[dict], path: str):
    """导出 SW 曲线"""
    with open(path, "w") as f:
        f.write(f"# Volute curve — {len(points_3d)} pts\nx,y,z\n")
        for pt in points_3d:
            f.write(f"{pt['x']},{pt['y']},{pt.get('z', 0)}\n")
    print(f"  📄 {path}")


# ═══════════════════════════════════════════════════════════════
# 完整 Pipeline
# ═══════════════════════════════════════════════════════════════


def design_and_output(
    Q: float, P: float, n: float,
    blade_type: str = "backward",
    material: str = "Q235B",
    output_dir: str = ".",
    gen_macro: bool = True,
) -> dict:
    """
    完整流程：叶轮设计 → 蜗壳匹配 → 输出

    Returns:
        {"impeller": ..., "volute": ..., "summary": ..., ...}
    """
    from .design import design_impeller
    from .params import ImpellerDesignInput

    inp = ImpellerDesignInput(Q=Q, P=P, n=n, blade_type=blade_type, material=material)
    imp = design_impeller(inp)
    vol = match_impeller(imp)

    result = {
        "impeller": imp,
        "volute": vol,
        "summary": f"{imp.summary}\n\n{vol.summary}",
        "macro_path": None,
        "profile_path": None,
    }

    os.makedirs(output_dir, exist_ok=True)
    safe = f"Fan_{Q:.0f}m3h_{P:.0f}Pa_{n:.0f}rpm"

    # 蜗壳型线
    profile = volute_profile(vol)
    csv_path = os.path.join(output_dir, f"{safe}_volute.csv")
    export_csv(profile, csv_path)
    result["profile_path"] = csv_path

    if gen_macro:
        macro = generate_vba_macro(vol, profile)
        mp = os.path.join(output_dir, f"{safe}.bas")
        with open(mp, "w", encoding="utf-8") as f:
            f.write(macro)
        result["macro_path"] = mp
        print(f"  ✅ VBA Macro: {mp}")

    return result


# ═══════════════════════════════════════════════════════════════
# 测试
# ═══════════════════════════════════════════════════════════════

if __name__ == "__main__":
    from .design import design_impeller
    from .params import ImpellerDesignInput

    # 用之前的叶轮案例
    inp = ImpellerDesignInput(Q=5000, P=2500, n=1450)
    imp = design_impeller(inp)

    print("=" * 60)
    print("  叶轮设计")
    print("=" * 60)
    print(imp.summary)

    print("\n" + "=" * 60)
    print("  蜗壳匹配")
    print("=" * 60)
    vol = match_impeller(imp)
    print(vol.summary)

    profile = volute_profile(vol)
    print(f"\n  蜗壳型线: {len(profile)} 个点")
    print(f"    进口 (θ={vol.theta_start}°): R={profile[0]['R']:.0f}mm")
    print(f"    出口 (θ={profile[-1]['theta']}°): R={profile[-1]['R']:.0f}mm")

    macro = generate_vba_macro(vol, profile)
    print(f"\n  VBA Macro: {len(macro)} chars")
