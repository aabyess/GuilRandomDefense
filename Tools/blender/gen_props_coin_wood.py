"""소품 둘 — 금화 더미 · 통나무 다발(목재). 뽑기 섬 특수지급 칸(돈+목재)과 조합표 줄 왼쪽 비용 아이콘용. PM 지시 2026-10-02(사장님 「목재·금화도 만들 수 있지?」).

화면 없이:  blender -b --factory-startup --python Tools/blender/gen_props_coin_wood.py -- [--out DIR] [--render DIR]
사장님 창(보여 주기):  exec 후 ns["show_in_window"]()

규격: 삼각형 1500 이하(각) · 원점 바닥 가운데 · 정면 −Y · 바닥 지름 약 1m(배율은 코드가 맞춘다) · 텍스처 512 이하 · 재질 이름 = 텍스처 이름 · 발광 없음.
  단위는 **미터 그대로**(1.0 = 1m = 유니티 1). 벽·건물처럼 11.4를 곱하지 않는다 — 코드가 크기를 맞추므로 비율만 중요하다.
  굽기(Cycles) 없이 **UV를 직접 짓고 텍스처를 numpy로 그린다** — gen_props.py 방식(면마다 UV 0~1 겹침 + 절차 셰이더 굽기)은
  동전 각인·나이테처럼 UV 자리가 정해진 그림에 못 쓴다.
금화    동전 20닢 안팎 — 가운데 쌓인 기둥(9닢, 살짝 어긋남) + 기대어 기운 동전 4 + 바닥에 눕거나 굴러 나온 동전. 면 각인(테두리·안쪽 원·가운데 원판)·옆면 어두운 금.
목재    통나무 5개(아래 3·위 2 피라미드, 길이 0.9 · Y축 → 잘린 면이 정면 −Y) · 끝면 = 껍질 고리 + 나이테 원판 · 끈 둘로 묶음.
재질    소품_금화 · 소품_금화옆 · 소품_통나무껍질 · 소품_통나무단면 · 소품_끈
"""
import math
import os
import random
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector

OUT = os.path.expanduser("~/GRD_motion_trial/소품_금화목재")
TRI_LIMIT = 1500


# ──────────────────────────────────────────────────────────── 텍스처(numpy → PNG, sRGB 표시값)

def _wrap_noise(n, rng, k=8):
    """가로로 이어붙여도 끊기지 않는 1차원 값 노이즈(길이 n)."""
    pts = rng.random(k)
    xs = np.linspace(0, k, n, endpoint=False)
    i0 = np.floor(xs).astype(int) % k
    i1 = (i0 + 1) % k
    t = xs - np.floor(xs)
    t = t * t * (3 - 2 * t)
    return pts[i0] * (1 - t) + pts[i1] * t


def tex_bark(n=256):
    rng = np.random.default_rng(3)
    base = np.array([0.55, 0.36, 0.19])
    dark = np.array([0.30, 0.17, 0.09])
    light = np.array([0.72, 0.50, 0.29])
    streak = _wrap_noise(n, rng, 10) * 0.6 + _wrap_noise(n, rng, 31) * 0.4
    img = np.zeros((n, n, 3))
    y = np.linspace(0, 1, n)[:, None]
    wob = 0.5 + 0.5 * np.sin(y * 18 + streak[None, :] * 12)
    t = np.clip(streak[None, :] * 0.8 + wob * 0.35, 0, 1)
    img[:] = dark + (base - dark) * np.clip(t * 1.6, 0, 1)[..., None]
    img = img + (light - base) * (np.clip(t - 0.75, 0, 1) * 3)[..., None]
    for c in rng.choice(n, 14, replace=False):          # 깊은 세로 홈
        w = rng.integers(2, 4)
        img[:, c:c + w] = dark * 0.7
    for _ in range(5):                                  # 옹이
        cx, cy, r = rng.integers(0, n), rng.integers(20, n - 20), rng.integers(6, 11)
        yy, xx = np.ogrid[:n, :n]
        d = np.sqrt(((xx - cx) / 0.8) ** 2 + (yy - cy) ** 2)
        img[d < r] = dark * 0.8
        img[(d >= r) & (d < r + 3)] = light * 0.9
    return np.clip(img, 0, 1)


def tex_rings(n=256):
    base = np.array([0.86, 0.66, 0.40])
    ring = np.array([0.60, 0.40, 0.20])
    core = np.array([0.42, 0.26, 0.12])
    yy, xx = np.mgrid[:n, :n]
    dx, dy = (xx + 0.5) / n - 0.5, (yy + 0.5) / n - 0.5
    ang = np.arctan2(dy, dx)
    r = np.sqrt(dx ** 2 + dy ** 2) * (1 + 0.05 * np.sin(ang * 3 + 1.0) + 0.03 * np.sin(ang * 7))   # 살짝 찌그러진 나이테
    img = np.zeros((n, n, 3))
    img[:] = base
    band = (np.sin(r * 2 * math.pi * 9.0) > 0.55)
    img[band] = ring
    img[r < 0.05] = core
    img[(r >= 0.05) & (r < 0.075)] = ring
    img[r > 0.47] = ring * 0.8                          # 가장자리 한 줄(껍질 쪽 경계)
    # 갈라진 금 하나
    a = np.abs(ang - 0.4) < 0.025
    img[a & (r > 0.06) & (r < 0.34)] = core
    return np.clip(img, 0, 1)


def tex_rope(n=64):
    rng = np.random.default_rng(5)
    yy, xx = np.mgrid[:n, :n]
    base = np.array([0.80, 0.68, 0.42])
    dark = np.array([0.52, 0.40, 0.20])
    twist = (np.sin((xx + yy) / n * 2 * math.pi * 6) > 0.1)
    img = np.zeros((n, n, 3))
    img[:] = base
    img[twist] = dark
    img += rng.normal(0, 0.015, img.shape)
    return np.clip(img, 0, 1)


def tex_coin(n=256):
    """금화 면(원판 UV: 중심 0.5·반지름 0.5). 테두리 띠 · 안쪽 원 홈 · 가운데 돋을새김 원판 + 십자 무늬."""
    gold = np.array([1.00, 0.80, 0.16])
    rim = np.array([1.00, 0.93, 0.50])
    deep = np.array([0.78, 0.52, 0.08])
    yy, xx = np.mgrid[:n, :n]
    dx, dy = (xx + 0.5) / n - 0.5, (yy + 0.5) / n - 0.5
    r = np.sqrt(dx ** 2 + dy ** 2)
    img = np.zeros((n, n, 3))
    img[:] = gold
    sheen = np.clip(0.5 - (dx * 0.7 + dy * 0.7), 0, 1) * 0.18        # 한쪽이 밝은 광택
    img += sheen[..., None]
    img[(r > 0.40) & (r <= 0.50)] = rim
    img[(r > 0.385) & (r <= 0.40)] = deep                            # 테두리 안쪽 홈
    img[r <= 0.26] = gold * 0.93
    img[(r > 0.26) & (r <= 0.285)] = deep                            # 가운데 원판 둘레 홈
    # 가운데 무늬: 굵은 십자 + 가운데 점(원피스 베리풍 심볼 대신 단순 각인)
    cross = ((np.abs(dx) < 0.035) & (np.abs(dy) < 0.17)) | ((np.abs(dy) < 0.035) & (np.abs(dx) < 0.17))
    img[cross & (r <= 0.26)] = rim
    img[r < 0.045] = deep
    img[r > 0.5] = gold                                              # 원판 밖(UV 구석)
    return np.clip(img, 0, 1)


def tex_flat(color, n=16):
    img = np.zeros((n, n, 3))
    img[:] = color
    return img


TEXTURES = {
    "소품_금화": (tex_coin, 256),
    "소품_금화옆": (lambda: tex_flat((0.95, 0.68, 0.12)), 16),
    "소품_통나무껍질": (tex_bark, 256),
    "소품_통나무단면": (tex_rings, 256),
    "소품_끈": (tex_rope, 64),
}
ROUGH = {"소품_금화": 0.35, "소품_금화옆": 0.45, "소품_통나무껍질": 0.9, "소품_통나무단면": 0.85, "소품_끈": 0.9}
METAL = {"소품_금화": 0.0, "소품_금화옆": 0.0}      # 발광·금속 없음(유니티에서 어두워지지 않게 금속 0)


def make_material(name, tdir):
    """텍스처를 그려 PNG로 저장하고 Principled+이미지 재질을 짓는다(재질 이름 = 텍스처 이름)."""
    fn, n = TEXTURES[name]
    arr = fn()
    os.makedirs(tdir, exist_ok=True)
    path = os.path.join(tdir, name + ".png")
    img = bpy.data.images.get(name) or bpy.data.images.new(name, arr.shape[1], arr.shape[0], alpha=False)
    rgba = np.ones((arr.shape[0], arr.shape[1], 4))
    rgba[..., :3] = arr[::-1]                                         # PNG는 위가 첫 줄 → Blender 아래가 첫 줄
    img.pixels = rgba.ravel()
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    b = nt.nodes.new("ShaderNodeBsdfPrincipled")
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = img
    t.interpolation = "Linear"
    nt.links.new(t.outputs["Color"], b.inputs["Base Color"])
    nt.links.new(b.outputs["BSDF"], out.inputs["Surface"])
    b.inputs["Roughness"].default_value = ROUGH[name]
    b.inputs["Metallic"].default_value = METAL.get(name, 0.0)
    return mat


# ──────────────────────────────────────────────────────────── 기하 도구

class Builder:
    def __init__(self, mat_names):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.mi = {n: i for i, n in enumerate(mat_names)}

    def face(self, pts, uvs, mat, flip=False):
        vs = [self.bm.verts.new(p) for p in pts]
        if flip:
            vs.reverse()
            uvs = list(reversed(uvs))
        f = self.bm.faces.new(vs)
        f.material_index = self.mi[mat]
        for loop, uv in zip(f.loops, uvs):
            loop[self.uv].uv = uv
        return f

    def disk(self, center, u, v, normal, radius, sides, mat, spin=0.0, inner=None, outer_mat=None):
        """원판 면(부채꼴). UV = 원판 좌표 → (0.5+cos*0.5, 0.5+sin*0.5)."""
        ring = [(math.cos(spin + math.tau * i / sides), math.sin(spin + math.tau * i / sides)) for i in range(sides)]
        for i in range(sides):
            a, b = ring[i], ring[(i + 1) % sides]
            pts = [center, center + (u * a[0] + v * a[1]) * radius, center + (u * b[0] + v * b[1]) * radius]
            uvs = [(0.5, 0.5), (0.5 + a[0] * 0.5, 0.5 + a[1] * 0.5), (0.5 + b[0] * 0.5, 0.5 + b[1] * 0.5)]
            # 법선이 normal 쪽을 보게
            nrm = (pts[1] - pts[0]).cross(pts[2] - pts[0])
            self.face(pts, uvs, mat, flip=nrm.dot(normal) < 0)

    def tube(self, c0, c1, u, v, r0, r1, sides, mat, spin=0.0, vrange=(0.0, 1.0), ucount=1.0, wob=None):
        """옆면 — c0→c1 축, u·v 단면 기저. UV: u 둘레(0~ucount), v 길이."""
        def at(c, r, i):
            a = spin + math.tau * i / sides
            k = 1.0 if wob is None else wob[i % len(wob)]
            return c + (u * math.cos(a) + v * math.sin(a)) * r * k
        axis = (c1 - c0)
        for i in range(sides):
            p = [at(c0, r0, i), at(c0, r0, i + 1), at(c1, r1, i + 1), at(c1, r1, i)]
            uu0, uu1 = ucount * i / sides, ucount * (i + 1) / sides
            uvs = [(uu0, vrange[0]), (uu1, vrange[0]), (uu1, vrange[1]), (uu0, vrange[1])]
            nrm = (p[1] - p[0]).cross(p[3] - p[0])
            mid = (p[0] + p[2]) / 2 - (c0 + c1) / 2
            self.face(p, uvs, mat, flip=nrm.dot(mid) < 0)


def basis(normal):
    ref = Vector((1, 0, 0)) if abs(normal.z) < 0.9 else Vector((0, 1, 0))
    u = normal.cross(ref).normalized()
    v = normal.cross(u).normalized()
    return u, v


def coin(b, center, radius, thick, normal, sides=12, spin=0.0):
    """동전 한 닢 — center는 아래 면 중심, normal이 위. 옆면(소품_금화옆) + 위·아래 면(소품_금화)."""
    n = normal.normalized()
    u, v = basis(n)
    top = center + n * thick
    b.disk(top, u, v, n, radius, sides, "소품_금화", spin)
    b.disk(center, u, v, -n, radius, sides, "소품_금화", spin)
    b.tube(center, top, u, v, radius, radius, sides, "소품_금화옆", spin, vrange=(0.1, 0.9))


def lift_to_floor(bm, start_index=0):
    """start_index 이후 정점의 최저점이 z=0이 되게 올린다(기운 동전이 바닥을 뚫지 않게)."""
    vs = [v for v in bm.verts if v.index >= start_index]
    lo = min(v.co.z for v in vs)
    for v in vs:
        v.co.z -= lo


# ──────────────────────────────────────────────────────────── 금화 더미

def build_coins(tdir):
    names = ["소품_금화", "소품_금화옆"]
    b = Builder(names)
    rng = random.Random(7)
    R, T = 0.20, 0.075                       # 동전 반지름·두께(굵고 투박하게 — 카툰)
    # 가운데 쌓인 기둥: 9닢, 살짝 어긋나고 돌아가 있다
    z = 0.0
    for i in range(9):
        off = Vector((rng.uniform(-0.018, 0.018), rng.uniform(-0.018, 0.018), 0))
        coin(b, Vector((0, 0, z)) + off, R, T, Vector((rng.uniform(-0.02, 0.02), rng.uniform(-0.02, 0.02), 1)), 12, rng.uniform(0, 1))
        z += T
    # 기둥에 기대어 기운 동전 4닢(밑이 바닥에 닿고 윗변이 기둥에 기댐)
    for k, ang in enumerate((0.3, 1.9, 3.4, 4.9)):
        d = Vector((math.cos(ang), math.sin(ang), 0))
        tilt = math.radians(rng.uniform(52, 68))                       # 수평에서 기운 각
        n = (Vector((0, 0, 1)) * math.cos(tilt) + d * math.sin(tilt))   # 위 법선이 바깥(−d 아님)을 향하게: 동전 윗면이 바깥을 봄
        n = Vector((-d.x * math.sin(tilt), -d.y * math.sin(tilt), math.cos(tilt)))
        c = d * (R + 0.14 + rng.uniform(0, 0.03))
        start = len(b.bm.verts)
        b.bm.verts.index_update()
        coin(b, Vector((c.x, c.y, 0.0)), R, T, -n if False else n, 12, rng.uniform(0, 1))
        b.bm.verts.index_update()
        lift_to_floor(b.bm, start)
    # 바닥에 눕거나 굴러 나온 동전 4닢
    for ang, rad in ((0.9, 0.42), (2.6, 0.40), (4.1, 0.43), (5.6, 0.38)):
        c = Vector((math.cos(ang) * rad, math.sin(ang) * rad, 0.0))
        tilt = rng.uniform(0.0, 0.18)
        n = Vector((math.sin(tilt) * math.cos(ang + 1.2), math.sin(tilt) * math.sin(ang + 1.2), math.cos(tilt)))
        start = len(b.bm.verts)
        b.bm.verts.index_update()
        coin(b, c, R * rng.uniform(0.9, 1.0), T, n, 12, rng.uniform(0, 1))
        b.bm.verts.index_update()
        lift_to_floor(b.bm, start)
    return finish(b, "금화", names, tdir)


# ──────────────────────────────────────────────────────────── 통나무 다발

def build_logs(tdir):
    names = ["소품_통나무껍질", "소품_통나무단면", "소품_끈"]
    b = Builder(names)
    rng = random.Random(11)
    Rl, L, sides = 0.17, 0.90, 14
    # 아래 셋 · 위 둘(피라미드). 통나무는 Y축으로 누워 있고 잘린 면이 −Y(정면)을 본다.
    spots = [(-0.34, Rl), (0.0, Rl), (0.34, Rl), (-0.17, Rl + 0.294), (0.17, Rl + 0.294)]
    circles = []
    for x, z in spots:
        r = Rl * rng.uniform(0.94, 1.04)
        y0 = -L / 2 + rng.uniform(-0.04, 0.04)
        y1 = L / 2 + rng.uniform(-0.04, 0.04)
        wob = [rng.uniform(0.93, 1.06) for _ in range(sides)]
        c0, c1 = Vector((x, y0, z)), Vector((x, y1, z))
        u, v = Vector((1, 0, 0)), Vector((0, 0, 1))
        spin = rng.uniform(0, 1)
        b.tube(c0, c1, u, v, r, r, sides, "소품_통나무껍질", spin, vrange=(0.0, 1.0), ucount=1.0, wob=wob)
        # 끝면: 껍질 고리(바깥 → 안쪽 0.86) + 나이테 원판. 앞(−Y) 끝은 살짝 안으로 들어간 원판(껍질 두께가 보이게)
        for c, nrm, flip in ((c0, Vector((0, -1, 0)), False), (c1, Vector((0, 1, 0)), True)):
            inner = 0.84
            for i in range(sides):
                a0, a1 = spin + math.tau * i / sides, spin + math.tau * (i + 1) / sides
                k0, k1 = wob[i % sides], wob[(i + 1) % sides]
                po0 = c + (u * math.cos(a0) + v * math.sin(a0)) * r * k0
                po1 = c + (u * math.cos(a1) + v * math.sin(a1)) * r * k1
                pi0 = c + (u * math.cos(a0) + v * math.sin(a0)) * r * k0 * inner + nrm * 0.012 * -1
                pi1 = c + (u * math.cos(a1) + v * math.sin(a1)) * r * k1 * inner + nrm * 0.012 * -1
                quad = [po0, po1, pi1, pi0]
                n_ = (quad[1] - quad[0]).cross(quad[3] - quad[0])
                b.face(quad, [(0.02 + 0.96 * i / sides, 0.02), (0.02 + 0.96 * (i + 1) / sides, 0.02), (0.02 + 0.96 * (i + 1) / sides, 0.18), (0.02 + 0.96 * i / sides, 0.18)],
                       "소품_통나무껍질", flip=n_.dot(nrm) < 0)
                # 원판 부채꼴(안쪽 고리 안)
                ctr = c - nrm * 0.012
                tri = [ctr, pi0, pi1]
                nn = (tri[1] - tri[0]).cross(tri[2] - tri[0])
                b.face(tri, [(0.5, 0.5), (0.5 + 0.5 * k0 * math.cos(a0) * 0 + 0.5 * math.cos(a0), 0.5 + 0.5 * math.sin(a0)),
                             (0.5 + 0.5 * math.cos(a1), 0.5 + 0.5 * math.sin(a1))], "소품_통나무단면", flip=nn.dot(nrm) < 0)
        circles.append((x, z, r))
    # 끈 두 줄 — 다발 윤곽(원들의 볼록 껍질)을 따라 두른 납작한 띠
    def hull_point(theta):
        d = (math.cos(theta), math.sin(theta))
        best = max(circles, key=lambda c: c[0] * d[0] + c[1] * d[1] + c[2])
        return (best[0] + d[0] * (best[2] + 0.014), best[1] + d[1] * (best[2] + 0.014)), d
    N = 36
    for yc in (-0.27, 0.27):
        w = 0.07
        pts = [hull_point(math.tau * i / N) for i in range(N)]
        for i in range(N):
            (p0, d0), (p1, d1) = pts[i], pts[(i + 1) % N]
            out0 = [Vector((p0[0], yc - w / 2, p0[1])), Vector((p1[0], yc - w / 2, p1[1])), Vector((p1[0], yc + w / 2, p1[1])), Vector((p0[0], yc + w / 2, p0[1]))]
            mid = (out0[0] + out0[2]) / 2
            nrm = (out0[1] - out0[0]).cross(out0[3] - out0[0])
            outward = Vector((d0[0] + d1[0], 0, d0[1] + d1[1]))
            b.face(out0, [(0, 0), (1, 0), (1, 1), (0, 1)], "소품_끈", flip=nrm.dot(outward) < 0)
            # 띠 두께(바깥 0.012 위로 얇은 옆면 둘)
            t = 0.012
            for sgn, yy in ((-1, yc - w / 2), (1, yc + w / 2)):
                a = Vector((p0[0], yy, p0[1]))
                bq = Vector((p1[0], yy, p1[1]))
                a2 = Vector((p0[0] - d0[0] * t, yy, p0[1] - d0[1] * t))
                b2 = Vector((p1[0] - d1[0] * t, yy, p1[1] - d1[1] * t))
                q = [a, bq, b2, a2]
                n_ = (q[1] - q[0]).cross(q[3] - q[0])
                b.face(q, [(0, 0), (1, 0), (1, 0.2), (0, 0.2)], "소품_끈", flip=n_.dot(Vector((0, sgn, 0))) < 0)
    return finish(b, "목재", names, tdir)


# ──────────────────────────────────────────────────────────── 마무리·내보내기·렌더

def finish(b, name, mat_names, tdir):
    bm = b.bm
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    lo_z = min(v.co.z for v in bm.verts)
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    cx, cy = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2
    for v in bm.verts:                                       # 원점 = 바닥 가운데
        v.co.x -= cx
        v.co.y -= cy
        v.co.z -= lo_z
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for n in mat_names:
        me.materials.append(make_material(n, tdir))
    for p in me.polygons:
        p.use_smooth = False
    obj = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def stats(obj):
    vs = [v.co for v in obj.data.vertices]
    dims = [max(v[i] for v in vs) - min(v[i] for v in vs) for i in range(3)]
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    return dims, tris, min(v.z for v in vs)


def export(obj, out):
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(filepath=os.path.join(out, obj.name + ".fbx"), use_selection=True, object_types={"MESH"},
                             global_scale=1.0, apply_unit_scale=True, mesh_smooth_type="FACE", add_leaf_bones=False,
                             bake_anim=False, path_mode="RELATIVE")


def render(objs, odir):
    os.makedirs(odir, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.view_settings.view_transform = "Standard"      # 기본 AgX는 금색을 올리브로 죽인다
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x = sc.render.resolution_y = 700
    cam = bpy.data.objects.new("c", bpy.data.cameras.new("c"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = 1.5
    for o in objs:
        for p in objs:
            p.hide_render = p is not o
        for name, loc, rot in (("앞", (0, -5, 0.45), (90, 0, 0)), ("옆", (5, 0, 0.45), (90, 0, 90)), ("위", (0, 0, 5), (0, 0, 0)),
                               ("비스듬", (3.2, -3.2, 2.4), (62, 0, 45))):
            cam.location = loc
            cam.rotation_euler = [math.radians(x) for x in rot]
            sc.render.filepath = os.path.join(odir, f"{o.name}_{name}.png")
            bpy.ops.render.render(write_still=True)
        # 무음영(FLAT) 한 장 — 스튜디오 조명이 색을 어둡게 눌러 보이므로 텍스처 본색 확인용
        sc.display.shading.light = "FLAT"
        cam.location, cam.rotation_euler = (3.2, -3.2, 2.4), [math.radians(x) for x in (62, 0, 45)]
        sc.render.filepath = os.path.join(odir, f"{o.name}_본색.png")
        bpy.ops.render.render(write_still=True)
        sc.display.shading.light = "STUDIO"
    for p in objs:
        p.hide_render = False


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    out, rdir = OUT, os.path.join(OUT, "검증")
    it = iter(args)
    for a in it:
        if a == "--out":
            out = next(it)
        elif a == "--render":
            rdir = next(it)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    tdir = os.path.join(out, "Textures")
    os.makedirs(out, exist_ok=True)
    made = [build_coins(tdir), build_logs(tdir)]
    for o in made:
        dims, tris, lo = stats(o)
        ok = tris <= TRI_LIMIT and abs(lo) < 1e-4
        print(f"만듦 {o.name} 치수(m) {[round(d, 3) for d in dims]} 삼각형 {tris} 최저z {lo:.4f} 재질 {[m.name for m in o.data.materials]} {'통과' if ok else '⚠️'}")
        export(o, out)
    render(made, rdir)


if __name__ == "__main__":
    main()
