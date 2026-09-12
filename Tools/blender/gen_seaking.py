"""거대 해왕류(바다뱀형, 사실적) — 메시 + 구운 질감 + 뼈대 + 숨쉬기 동작을 만들어 FBX로 내보낸다.

화면 없이 돈다(내보내기):
    blender --background --factory-startup --python Tools/blender/gen_seaking.py
사장님 창에서는 이 파일을 exec해 build_live(collection)만 부른다 — 창에 보이는 것과 내보내는 것이 같다.

사장님 확정(2026-09-12): 1단계 샘플 둘(바다뱀형·물고기형) 중 **A 바다뱀형 + 사실적 화풍**. 게임의 거대
해왕류(체력 5,400만 표적, 남쪽 먼 바다 (0,1,−650)에 고정)가 잡몹 임시 모형을 쓰던 것을 대체한다.

⚠️ 치수는 미터로 적고 내보낼 때 11.4를 곱한다(사람 키 20 = 1.75m). 몸길이 굴곡 따라 약 29m(가로 24m).
🔴 원점 = 수면 높이(z=0). 물에 잠기는 부분은 음수 높이 — 게임의 바다 판이 가려 준다.
🔴 재질 이름 약속(PM 유니티 코드가 이름으로 재질을 자동 생성): 텍스처는 Assets/Art/Monsters/Textures/
<재질이름>.png. 막(지느러미·볏)은 이름 끝 `_잎카드` → 양면 + 알파 컷. 나머지는 불투명.
🔴 동작 `Idle_Breath` — 8초·30fps·240프레임 반복, 첫 프레임 = 마지막 프레임. FBX 클립 이름은 Blender
내보내기 규칙상 「<뼈대이름>|Idle_Breath」로 나간다.

굽기(bake) 주의: `use_clear=False`, 재질을 이미지 재질로 바꾸는 건 전부 구운 뒤 + 디스크 PNG 재로드 —
아니면 같은 오브젝트의 다른 슬롯에 넣어 둔 구운 이미지가 지워진다(나무 C 샘플에서 겪음).
"""

import math
import os
import random
import sys

import bpy
import bmesh
from mathutils import Vector

UNITS_PER_METER = 11.4
PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_ROOT = os.path.join(PROJECT, "Assets", "Art", "Monsters")
TEX_DIR = os.path.join(OUT_ROOT, "Textures")
NAME = "거대해왕류"
SEED = 701
UP = Vector((0.0, 0.0, 1.0))

# 재질 — (이름, 구운 텍스처 크기 또는 None(단색), 알파 여부, 뷰포트 색)
MATERIALS = [
    ("해왕류_비늘", 1024, False, (0.20, 0.36, 0.40, 1.0)),
    ("해왕류_막_잎카드", 512, True, (0.24, 0.50, 0.52, 1.0)),
    ("해왕류_눈", 256, False, (0.90, 0.72, 0.18, 1.0)),
    ("해왕류_이빨", None, False, (0.86, 0.80, 0.60, 1.0)),
    ("해왕류_입속", None, False, (0.50, 0.13, 0.13, 1.0)),
]
SCALE, MEMBRANE, EYE, TOOTH, MOUTH = range(5)

FRAMES, FPS = 240, 30                     # 8초 한 주기(사장님 피드백: 4초는 요동쳐 보였다)


# ──────────────────────────────────────────────────────────── 곡선·고리

def frame(axis):
    axis = axis.normalized()
    ref = UP if abs(axis.z) < 0.95 else Vector((1.0, 0.0, 0.0))
    u = ref.cross(axis).normalized()          # 옆
    return u, axis.cross(u)                   # (옆, 위)


def catmull(points, samples):
    """제어점을 지나는 부드러운 곡선 — (위치, 접선) 목록."""
    pts = [points[0]] + list(points) + [points[-1]]
    out = []
    spans = len(points) - 1
    for s in range(samples + 1):
        t = s / samples * spans
        i = min(int(t), spans - 1)
        f = t - i
        p0, p1, p2, p3 = (Vector(p) for p in pts[i:i + 4])
        pos = 0.5 * ((2 * p1) + (-p0 + p2) * f + (2 * p0 - 5 * p1 + 4 * p2 - p3) * f * f + (-p0 + 3 * p1 - 3 * p2 + p3) * f ** 3)
        tan = 0.5 * ((-p0 + p2) + 2 * (2 * p0 - 5 * p1 + 4 * p2 - p3) * f + 3 * (-p0 + 3 * p1 - 3 * p2 + p3) * f * f)
        out.append((pos, tan.normalized()))
    return out


class Mesh:
    """bmesh + UV 레이어 묶음. 고리는 u = 둘레(0 옆 → 0.25 위 → 0.75 아래), v = 길이(m)/6."""

    def __init__(self):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")

    def ring(self, center, axis, sides, rx, rz, basis=None):
        """basis=(옆, 위)를 주면 그 방향으로 고리를 놓는다 — 경로를 따라 평행 이동한 프레임을 넘겨
        등·배가 목에서도 안 뒤집히게 한다. 안 주면 UP 기준."""
        u, w = basis or frame(axis)
        return [self.bm.verts.new(center + u * (math.cos(math.tau * i / sides) * rx)
                                  + w * (math.sin(math.tau * i / sides) * rz)) for i in range(sides)]

    def bridge(self, lower, upper, m, v0, v1):
        n = len(lower)
        faces = []
        for i in range(n):
            j = (i + 1) % n
            f = self.bm.faces.new((lower[i], lower[j], upper[j], upper[i]))
            f.material_index = m
            for loop, uu, vv in zip(f.loops, (i / n, (i + 1) / n, (i + 1) / n, i / n), (v0, v0, v1, v1)):
                loop[self.uv].uv = (uu, vv)
            faces.append(f)
        return faces

    def fan(self, ring, center, m, v, outward=True):
        c = self.bm.verts.new(center)
        n = len(ring)
        for i in range(n):
            j = (i + 1) % n
            f = self.bm.faces.new((c, ring[i], ring[j]) if outward else (c, ring[j], ring[i]))
            f.material_index = m
            for loop, uu in zip(f.loops, ((i + 0.5) / n, i / n, (i + 1) / n) if outward else ((i + 0.5) / n, (i + 1) / n, i / n)):
                loop[self.uv].uv = (uu, v)

    def quad(self, a, b, c, d, m, uvs):
        f = self.bm.faces.new([self.bm.verts.new(p) for p in (a, b, c, d)])
        f.material_index = m
        for loop, coord in zip(f.loops, uvs):
            loop[self.uv].uv = coord
        return f

    def disc(self, center, normal, radius, m, sides=8):
        """표면에 붙이는 얇은 원반(콧구멍)."""
        ring = self.ring(center, normal, sides, radius, radius)
        self.fan(ring, center + normal.normalized() * 0.01, m, 0.5)

    def cone(self, base, direction, radius, length, m, sides=5):
        ring = self.ring(base, direction, sides, radius, radius)
        tip = self.bm.verts.new(base + direction.normalized() * length)
        for i in range(sides):
            f = self.bm.faces.new((ring[i], ring[(i + 1) % sides], tip))
            f.material_index = m
            for loop in f.loops:
                loop[self.uv].uv = (0.5, 0.5)
        self.fan(ring, base, m, 0.5, outward=False)

    def eyeball(self, center, outward, radius, m):
        """눈알 — 바깥 축을 따라 고리를 쌓은 구. v = 0(뒤) → 1(앞, 동공 쪽)."""
        rings, vs = [], []
        steps = 6
        for k in range(1, steps):
            a = math.pi * k / steps
            c = center - outward * (radius * math.cos(a))
            rings.append(self.ring(c, outward, 10, radius * math.sin(a), radius * math.sin(a)))
            vs.append(k / steps)
        self.fan(rings[0], center - outward * radius, m, 0.0, outward=False)
        for (r0, v0), (r1, v1) in zip(zip(rings, vs), zip(rings[1:], vs[1:])):
            self.bridge(r0, r1, m, v0, v1)
        self.fan(rings[-1], center + outward * radius, m, 1.0)

    def to_object(self, name, collection):
        mesh = bpy.data.meshes.new(name)
        self.bm.to_mesh(mesh)
        self.bm.free()
        obj = bpy.data.objects.new(name, mesh)
        collection.objects.link(obj)
        for mat_name, *_ in MATERIALS:
            mesh.materials.append(material(mat_name))
        return obj


# ──────────────────────────────────────────────────────────── 몸

# 굴곡과 목이 만나는 곳이 각지지 않게 제어점 간격을 고르게(사장님 피드백 7).
SPINE = [(-11.0, 0.3, -2.6), (-8.8, 0.1, -0.4), (-6.8, -0.2, 1.3), (-4.6, -0.4, -0.4), (-2.4, -0.2, -2.0),
         (-0.2, 0.2, -0.4), (1.9, 0.4, 1.6), (3.9, 0.3, 0.4), (5.5, 0.0, -1.3), (6.9, -0.1, 0.6),
         (7.9, -0.2, 2.8), (8.7, -0.1, 4.8), (9.3, 0.0, 6.4)]
SAMPLES, SIDES = 140, 20

# (t, 반지름) — 꼬리 0.3 → 몸통 1.25 → 목 밑동 0.85 → 목 끝 0.6. 코사인 보간이라 어디서도 안 꺾인다.
# 🔴 예전 식은 t=0.75에서 sin(π)=0으로 떨어졌다 되살아나 "목 밑동에 끼운 토시"가 생겼다(사장님 피드백 1).
RADIUS_KEYS = ((0.0, 0.30), (0.12, 0.75), (0.30, 1.25), (0.50, 1.20), (0.68, 1.05), (0.80, 0.85), (0.90, 0.70), (1.0, 0.60))


def radius_at(t):
    for (t0, r0), (t1, r1) in zip(RADIUS_KEYS, RADIUS_KEYS[1:]):
        if t <= t1:
            f = (1 - math.cos(math.pi * (t - t0) / (t1 - t0))) / 2
            return r0 + (r1 - r0) * f
    return RADIUS_KEYS[-1][1]


def build(collection):
    """메시 하나를 만든다. 뼈대용 정보(척추 점·머리·턱·볏 위치, 로컬 좌표)를 같이 돌려준다."""
    rng = random.Random(SEED)
    M = Mesh()
    path = catmull(SPINE, SAMPLES)

    # 몸통 튜브 — 1단계보다 촘촘하게(20면·120마디). 배 색은 재질(텍스처 안 그라데이션)이라 면을 안 가른다.
    rings, vs, radii, bases = [], [], [], []
    dist = 0.0
    side = frame(path[0][1])[0]
    for k, (pos, tan) in enumerate(path):
        t = k / SAMPLES
        if k:
            dist += (pos - path[k - 1][0]).length
        # 🔴 평행 이동: 옆 방향에서 접선 성분만 빼 이어 간다 — UP 기준으로 다시 잡으면 수직 목에서
        # 옆·위가 뒤집혀 배 색이 목 앞으로 돌아갔다(1단계 렌더의 흰 띠).
        side = (side - tan * side.dot(tan)).normalized()
        # 머리 쪽 15%는 옆 방향을 수평(UP×접선)으로 서서히 되돌린다 — 평행 이동만 하면 S자를 따라
        # 조금씩 비틀려 머리에서 눈이 정수리로 올라갔다. 목은 완전 수직이 아니라 수평 옆이 정의된다.
        if t > 0.85 and abs(tan.z) < 0.95:
            level = UP.cross(tan).normalized()
            if level.dot(side) < 0:
                level = -level
            side = side.lerp(level, (t - 0.85) / 0.15).normalized()
        basis = (side, tan.cross(side))
        bases.append(basis)
        r = radius_at(t)
        rings.append(M.ring(pos, tan, SIDES, r, r * 1.08, basis))
        vs.append(dist / 6.0)
        radii.append(r)
    for k in range(SAMPLES):
        M.bridge(rings[k], rings[k + 1], SCALE, vs[k], vs[k + 1])
    M.fan(rings[0], path[0][0] - path[0][1] * 0.5, SCALE, vs[0], outward=False)

    # 머리 — 목 끝 고리에서 한 덩어리로 이어진다. 원랜디·원피스 해왕류 얼굴(사장님 피드백):
    # 길게 찢어진 큰 입, 두툼한 주둥이와 콧구멍, 머리 위쪽 옆의 큰 눈과 눈썹 뼈, 뺨의 지느러미 귀, 턱 수염.
    hpos, htan = path[-1]
    hu = bases[-1][0]
    fwd = (htan * 0.8 + Vector((0.6, 0, -0.15))).normalized()
    hu = (hu - fwd * hu.dot(fwd)).normalized()
    hw = fwd.cross(hu)
    r0 = radii[-1]
    H = r0 * 1.55                              # 머리 기준 반지름 — 목보다 확실히 크게
    prev, v = rings[-1], vs[-1]
    head_faces = []
    # (앞으로 거리, 옆 반지름/H, 위 반지름/H, 위로 올림/H) — 두개골에서 넓어졌다가 주둥이 끝은 뭉툭하게
    profile = ((0.7, 0.95, 0.95, 0.15), (1.6, 1.25, 1.05, 0.35), (2.6, 1.30, 0.95, 0.35), (3.7, 1.18, 0.78, 0.25),
               (4.8, 1.05, 0.62, 0.12), (5.7, 0.95, 0.52, 0.02), (6.3, 0.72, 0.40, -0.05))
    head_rings = [prev]
    for dist_f, rx, rz, lift in profile:
        c = hpos + fwd * dist_f + hw * (lift * H)
        ring = M.ring(c, fwd, SIDES, H * rx, H * rz, (hu, hw))
        v2 = v + 0.9 / 6.0
        head_faces += M.bridge(prev, ring, SCALE, v, v2)
        prev, v = ring, v2
        head_rings.append(ring)
    snout_tip = hpos + fwd * 6.55 - hw * 0.05 * H
    M.fan(prev, snout_tip, SCALE, v)
    for f in head_faces:                       # 입천장(위턱 아랫면)은 짙은 붉은색
        f.normal_update()
        if f.normal.dot(hw) < -0.45:
            f.material_index = MOUTH

    # 아래턱 — 입꼬리가 눈 뒤(두개골 밑)에서 시작해 길게 찢어지고, 크게 벌어진다
    jaw_root = hpos + fwd * 0.5 - hw * H * 0.75
    jaw_dir = (fwd - hw * 0.72).normalized()
    ju = (hu - jaw_dir * hu.dot(jaw_dir)).normalized()
    jw = jaw_dir.cross(ju)
    jaw_profile = ((0.0, 1.22, 0.42), (1.6, 1.28, 0.40), (3.2, 1.18, 0.36), (4.6, 0.98, 0.30), (5.6, 0.70, 0.22))
    jaw_rings = [M.ring(jaw_root + jaw_dir * d, jaw_dir, 14, H * rx, H * rz, (ju, jw)) for d, rx, rz in jaw_profile]
    for a, b in zip(jaw_rings, jaw_rings[1:]):
        for f in M.bridge(a, b, SCALE, v, v + 0.2):
            f.normal_update()
            if f.normal.dot(jw) > 0.35:        # 아래턱 윗면(입 안)
                f.material_index = MOUTH
    M.fan(jaw_rings[-1], jaw_root + jaw_dir * 5.85, SCALE, v)
    M.fan(jaw_rings[0], jaw_root - jaw_dir * 0.2, MOUTH, v, outward=False)

    # 이빨 — 위턱 아래 가장자리에서 아래로, 아래턱 위 가장자리에서 위로. 앞니가 크고 뒤로 갈수록 작다
    for k in range(8):
        along = 1.2 + k * 0.62
        size = 0.62 - k * 0.045
        for side in (-1, 1):
            base = hpos + fwd * along + hu * side * H * (1.16 - k * 0.03) - hw * H * 0.32
            M.cone(base, (-hw + fwd * 0.2).normalized(), 0.13, size, TOOTH)
            base = jaw_root + jaw_dir * (along - 0.5) + ju * side * H * (1.12 - k * 0.05) + jw * H * 0.3
            M.cone(base, (jw + jaw_dir * 0.1).normalized(), 0.12, size * 0.9, TOOTH)

    # 콧구멍 — 주둥이 끝 위에 짙은 원반 둘
    for side in (-1, 1):
        c = hpos + fwd * 5.9 + hu * side * H * 0.42 + hw * H * 0.50
        M.disc(c, (hw + fwd * 0.5).normalized(), 0.17, MOUTH)

    # 눈 — 머리 위쪽 옆, 두개골에 박힌 채(중심이 표면 안쪽) 크고 둥글게. 그 위에 두꺼운 눈썹 뼈 능선.
    eyes, eye_r = [], H * 0.42
    for side in (-1, 1):
        c = hpos + fwd * 2.0 + hu * side * H * 1.02 + hw * H * 0.52
        outward = (hu * side * 0.8 + hw * 0.35 + fwd * 0.3).normalized()
        M.eyeball(c, outward, eye_r, EYE)
        eyes.append(c)
        brow = [M.ring(hpos + fwd * d + hu * side * H * 1.0 + hw * H * (lift), fwd, 8, H * 0.42, H * 0.24, (hu, hw))
                for d, lift in ((1.1, 1.02), (2.0, 1.0), (3.0, 0.9))]        # 앞으로 갈수록 내려와 화난 눈썹
        for a, b in zip(brow, brow[1:]):
            M.bridge(a, b, SCALE, v, v + 0.1)
        M.fan(brow[-1], hpos + fwd * 3.35 + hu * side * H * 1.0 + hw * H * 0.86, SCALE, v)
        M.fan(brow[0], hpos + fwd * 0.8 + hu * side * H * 1.0 + hw * H * 1.02, SCALE, v, outward=False)

    # 지느러미 귀(프릴) — 뺨·턱 옆에서 뒤로 뾰족하게 펼친 막 한 쌍(막 재질, 볏 뼈가 움직인다)
    crest = []
    for side in (-1, 1):
        base_f = hpos + fwd * 1.4 + hu * side * H * 1.05 + hw * H * 0.1
        base_b = hpos + fwd * 0.2 + hu * side * H * 0.95 - hw * H * 0.15
        tip = hpos - fwd * 1.6 + hu * side * H * 2.6 + hw * H * 0.55
        mid = hpos - fwd * 0.3 + hu * side * H * 2.2 - hw * H * 0.35
        M.quad(base_b, base_f, tip, mid, MEMBRANE, ((0, 0), (0.8, 0), (0.8, 1), (0, 1)))
        crest.append((base_b, tip))
    # 머리 볏 — 두개골 정수리에서 뒤로 넘어가는 막 돛
    for k, (d0, d1, h0, h1) in enumerate(((0.3, 1.2, 1.1, 1.9), (1.2, 2.2, 1.9, 1.5))):
        a = hpos + fwd * d0 + hw * H * 1.02
        b = hpos + fwd * d1 + hw * H * 1.05
        M.quad(a, b, b - fwd * 0.9 + hw * H * h1, a - fwd * 1.4 + hw * H * h0, MEMBRANE,
               ((k * 0.5, 0), (k * 0.5 + 0.5, 0), (k * 0.5 + 0.5, 1), (k * 0.5, 1)))

    # 수염 — 턱 밑 굵은 촉수 두 가닥
    for side in (-1, 1):
        base = jaw_root + jaw_dir * 3.4 + ju * side * H * 0.55 - jw * H * 0.28
        pts_w = [base, base + ju * side * 0.5 - jw * 0.9 - jaw_dir * 0.3, base + ju * side * 1.1 - jw * 1.9 - jaw_dir * 1.0]
        wr = None
        for i, (a, b) in enumerate(zip(pts_w, pts_w[1:])):
            d = (b - a).normalized()
            ring_a = M.ring(a, d, 6, 0.17 - i * 0.06, 0.17 - i * 0.06) if wr is None else wr
            ring_b = M.ring(b, d, 6, 0.11 - i * 0.06, 0.11 - i * 0.06)
            M.bridge(ring_a, ring_b, SCALE, v, v + 0.1)
            wr = ring_b
        M.fan(wr, pts_w[-1] - jw * 0.15, SCALE, v)

    # 등 막 — 꼬리에서 목까지 등마루를 따라 한 장으로 이어지는 띠(알파 텍스처가 위 가장자리를 가시로 자른다)
    for k in range(3, SAMPLES - 4, 2):
        p0, t0 = path[k]
        p1, t1 = path[k + 2]
        w0, w1 = bases[k][1], bases[k + 2][1]
        h0 = radii[k] * 1.25 * (0.55 + 0.45 * math.sin(math.pi * k / SAMPLES))
        h1 = radii[k + 2] * 1.25 * (0.55 + 0.45 * math.sin(math.pi * (k + 2) / SAMPLES))
        base0, base1 = p0 + w0 * radii[k] * 1.05, p1 + w1 * radii[k + 2] * 1.05
        u0, u1 = k / 6.0, (k + 2) / 6.0
        M.quad(base0, base1, base1 + w1 * h1, base0 + w0 * h0, MEMBRANE, ((u0, 0), (u1, 0), (u1, 1), (u0, 1)))
    # 꼬리 막 — 세로로 펼친 부채(위·아래)
    tail, ttan = path[0]
    tw = bases[0][1]
    for sign in (1, -1):
        M.quad(tail, tail - ttan * 2.4 + tw * sign * 0.2, tail - ttan * 3.0 + tw * sign * 2.6, tail - ttan * 0.6 + tw * sign * 2.0,
               MEMBRANE, ((0, 0), (0.6, 0), (0.6, 1), (0, 1)))

    head_end = snout_tip
    jaw_tip = jaw_root + jaw_dir * 5.85

    obj = M.to_object(NAME, collection)
    info = {
        "spine": [path[k][0].copy() for k in range(0, SAMPLES + 1, SAMPLES // 8)],   # 9점 → 척추 8마디
        "head_end": head_end,
        "jaw_tip": jaw_tip,
        "jaw_root": jaw_root,
        "crest": crest,
        "tail_dir": ttan,
        "humps": [k / SAMPLES for k in range(SAMPLES + 1) if 0.12 < k / SAMPLES < 0.6
                  and path[k][0].z > 0.8 and all(path[k][0].z >= path[j][0].z for j in range(max(0, k - 6), min(SAMPLES, k + 6) + 1))],
    }
    return obj, info


# ──────────────────────────────────────────────────────────── 재질(절차적 → 굽기)

def material(name):
    mat = bpy.data.materials.get(name)
    if mat is None:
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
    color = next(c for n, _, _, c in MATERIALS if n == name)
    mat.diffuse_color = color
    mat.use_backface_culling = False
    return mat


def _new_tree(mat):
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    return nt, bsdf, out


def _math(nt, op, a, b=None, clamp=False):
    n = nt.nodes.new("ShaderNodeMath")
    n.operation = op
    n.use_clamp = clamp
    for k, val in enumerate((a, b)):
        if val is None:
            continue
        if isinstance(val, (int, float)):
            n.inputs[k].default_value = val
        else:
            nt.links.new(val, n.inputs[k])
    return n.outputs[0]


def _ramp(nt, fac, stops):
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.elements[0].position, ramp.color_ramp.elements[0].color = stops[0]
    ramp.color_ramp.elements[1].position, ramp.color_ramp.elements[1].color = stops[-1]
    for pos, col in stops[1:-1]:
        e = ramp.color_ramp.elements.new(pos)
        e.color = col
    nt.links.new(fac, ramp.inputs["Fac"])
    return ramp.outputs["Color"]


def procedural_scales():
    """비늘 — 보로노이 셀 경계가 비늘 틈, 노이즈로 얼룩. 등(u=0.25)은 짙고 배(u=0.75)는 밝게, 경계는 부드럽게."""
    mat = material("해왕류_비늘")
    nt, bsdf, _ = _new_tree(mat)
    bsdf.inputs["Roughness"].default_value = 0.55
    coord = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(coord.outputs["UV"], sep.inputs["Vector"])
    mapping = nt.nodes.new("ShaderNodeMapping")
    mapping.inputs["Scale"].default_value = (1.0, 1.15, 1.0)
    nt.links.new(coord.outputs["UV"], mapping.inputs["Vector"])
    vor = nt.nodes.new("ShaderNodeTexVoronoi")
    vor.feature = "DISTANCE_TO_EDGE"
    vor.inputs["Scale"].default_value = 46.0
    vor.inputs["Randomness"].default_value = 0.55
    nt.links.new(mapping.outputs["Vector"], vor.inputs["Vector"])
    edge = _math(nt, "MULTIPLY", vor.outputs["Distance"], 14.0, clamp=True)     # 0 틈 → 1 비늘 가운데
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 7.0
    noise.inputs["Detail"].default_value = 5.0
    nt.links.new(mapping.outputs["Vector"], noise.inputs["Vector"])
    # 위아래 그라데이션: 0.5 − 0.5·sin(2πu) → 등 0, 배 1
    band = _math(nt, "SUBTRACT", 0.5, _math(nt, "MULTIPLY", _math(nt, "SINE", _math(nt, "MULTIPLY", sep.outputs["X"], math.tau)), 0.5))
    band = _math(nt, "ADD", band, _math(nt, "MULTIPLY", _math(nt, "SUBTRACT", noise.outputs["Fac"], 0.5), 0.25), clamp=True)
    base = _ramp(nt, band, ((0.0, (0.07, 0.19, 0.25, 1)), (0.45, (0.16, 0.36, 0.40, 1)), (0.7, (0.42, 0.56, 0.52, 1)), (1.0, (0.70, 0.74, 0.64, 1))))
    tint = _ramp(nt, edge, ((0.0, (0.35, 0.35, 0.35, 1)), (0.35, (0.8, 0.8, 0.8, 1)), (1.0, (1.08, 1.08, 1.05, 1))))
    mix = nt.nodes.new("ShaderNodeMixRGB")
    mix.blend_type = "MULTIPLY"
    mix.inputs["Fac"].default_value = 1.0
    nt.links.new(base, mix.inputs["Color1"])
    nt.links.new(tint, mix.inputs["Color2"])
    nt.links.new(mix.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def procedural_membrane():
    """막 — 위 가장자리를 가시(삼각) 모양으로 자르는 알파, 가시 뼈대 선은 짙게, 사이 막은 반투명 느낌의 청록."""
    mat = material("해왕류_막_잎카드")
    nt, bsdf, _ = _new_tree(mat)
    bsdf.inputs["Roughness"].default_value = 0.5
    mat.surface_render_method = "DITHERED"
    coord = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(coord.outputs["UV"], sep.inputs["Vector"])
    u, v = sep.outputs["X"], sep.outputs["Y"]
    saw = _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", _math(nt, "MULTIPLY", _math(nt, "FRACT", _math(nt, "MULTIPLY", u, 2.4)), 2.0), 1.0))  # 0 가시 끝 … 1 사이
    profile = _math(nt, "SUBTRACT", 1.0, _math(nt, "MULTIPLY", _math(nt, "POWER", saw, 0.7), 0.55))                                        # 가시 끝 1.0, 사이 0.45
    mask = _math(nt, "LESS_THAN", v, profile)
    rib = _math(nt, "LESS_THAN", saw, 0.12)
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 6.0
    nt.links.new(coord.outputs["UV"], noise.inputs["Vector"])
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", noise.outputs["Fac"], 0.5), _math(nt, "MULTIPLY", v, 0.4), clamp=True)
    tone = _math(nt, "SUBTRACT", tone, _math(nt, "MULTIPLY", rib, 0.6), clamp=True)
    color = _ramp(nt, tone, ((0.0, (0.06, 0.16, 0.20, 1)), (0.5, (0.18, 0.44, 0.48, 1)), (1.0, (0.46, 0.70, 0.66, 1))))
    nt.links.new(color, bsdf.inputs["Base Color"])
    nt.links.new(mask, bsdf.inputs["Alpha"])
    return mat


def procedural_eye():
    """눈 — 노란 홍채, 앞 극(v→1)에 둥근 검은 동공, 위쪽에 젖은 흰 반점(하이라이트)."""
    mat = material("해왕류_눈")
    nt, bsdf, _ = _new_tree(mat)
    bsdf.inputs["Roughness"].default_value = 0.15
    coord = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(coord.outputs["UV"], sep.inputs["Vector"])
    u, v = sep.outputs["X"], sep.outputs["Y"]
    pupil = _math(nt, "GREATER_THAN", v, 0.84)
    iris = _math(nt, "GREATER_THAN", v, 0.45)
    spot = _math(nt, "MULTIPLY", _math(nt, "LESS_THAN", _math(nt, "ABSOLUTE", _math(nt, "SUBTRACT", u, 0.25)), 0.07),
                 _math(nt, "MULTIPLY", _math(nt, "GREATER_THAN", v, 0.66), _math(nt, "LESS_THAN", v, 0.78)))
    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.inputs["Scale"].default_value = 12.0
    nt.links.new(coord.outputs["UV"], noise.inputs["Vector"])
    tone = _math(nt, "ADD", _math(nt, "MULTIPLY", iris, 0.45), _math(nt, "MULTIPLY", noise.outputs["Fac"], 0.12))
    tone = _math(nt, "SUBTRACT", tone, _math(nt, "MULTIPLY", pupil, 0.7))
    tone = _math(nt, "ADD", tone, spot, clamp=True)
    color = _ramp(nt, tone, ((0.0, (0.04, 0.03, 0.02, 1)), (0.25, (0.50, 0.36, 0.08, 1)), (0.55, (0.96, 0.78, 0.16, 1)), (1.0, (1.0, 1.0, 0.98, 1))))
    nt.links.new(color, bsdf.inputs["Base Color"])
    return mat


def procedural_flat(name, color, roughness):
    mat = material(name)
    nt, bsdf, _ = _new_tree(mat)
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Roughness"].default_value = roughness
    return mat


def make_materials():
    procedural_scales()
    procedural_membrane()
    procedural_eye()
    procedural_flat("해왕류_이빨", (0.86, 0.80, 0.60, 1.0), 0.45)
    procedural_flat("해왕류_입속", (0.50, 0.13, 0.13, 1.0), 0.8)


def _bake(obj, mat, image, bake_type):
    scene = bpy.context.scene
    nt = mat.node_tree
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    nt.nodes.active = tex
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    prev = (scene.render.engine, scene.cycles.samples, scene.cycles.use_denoising, scene.cycles.seed)
    scene.render.engine = "CYCLES"
    scene.cycles.samples, scene.cycles.use_denoising, scene.cycles.seed = 4, False, 0
    scene.render.bake.use_pass_direct = scene.render.bake.use_pass_indirect = False
    scene.render.bake.use_pass_color = True
    scene.render.bake.margin = 6
    scene.render.bake.use_clear = False       # 🔴 다른 슬롯의 구운 이미지를 지우지 않게
    bpy.ops.object.bake(type=bake_type)
    scene.render.engine, scene.cycles.samples, scene.cycles.use_denoising, scene.cycles.seed = prev
    nt.nodes.remove(tex)


def _image(name, size, alpha=False):
    old = bpy.data.images.get(name)
    if old:
        bpy.data.images.remove(old)
    return bpy.data.images.new(name, size, size, alpha=alpha)


def _to_image_material(mat, image, alpha):
    nt, bsdf, _ = _new_tree(mat)
    bsdf.inputs["Roughness"].default_value = 0.55
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = image
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    if alpha:
        nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
        mat.surface_render_method = "DITHERED"
    nt.nodes.active = tex


def bake_textures(obj, out_dir=None):
    out_dir = out_dir or TEX_DIR             # 기본값을 여기서 읽어야 시험용으로 OUT_ROOT를 바꿔도 따라온다
    """구운 뒤 재질을 이미지 재질로 바꾼다. 텍스처 파일 이름 = 재질 이름(유니티 약속)."""
    os.makedirs(out_dir, exist_ok=True)
    import numpy as np
    finals = []
    for name, size, alpha, _ in MATERIALS:
        if size is None:
            continue
        mat = bpy.data.materials[name]
        nt = mat.node_tree
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        color_img = _image(name + "_color", size)
        alpha_link = next((l for l in nt.links if l.to_socket == bsdf.inputs["Alpha"]), None)
        mask_out = alpha_link.from_socket if alpha_link else None
        if alpha_link:
            nt.links.remove(alpha_link)       # 색을 구울 땐 알파를 끊는다(투명 부분이 검게 안 구워지게)
        _bake(obj, mat, color_img, "DIFFUSE")
        pixels = np.array(color_img.pixels[:], dtype=np.float32).reshape(-1, 4)
        if alpha:
            emit = nt.nodes.new("ShaderNodeEmission")
            out = next(n for n in nt.nodes if n.type == "OUTPUT_MATERIAL")
            nt.links.new(mask_out, emit.inputs["Color"])
            nt.links.remove(next(l for l in nt.links if l.to_socket == out.inputs["Surface"]))
            nt.links.new(emit.outputs["Emission"], out.inputs["Surface"])
            mask_img = _image(name + "_mask", size)
            _bake(obj, mat, mask_img, "EMIT")
            mask = np.array(mask_img.pixels[:], dtype=np.float32).reshape(-1, 4)
            pixels[:, 3] = (mask[:, 0] > 0.5).astype(np.float32)
            bpy.data.images.remove(mask_img)
        final = _image(name, size, alpha=alpha)
        final.pixels = pixels.ravel().tolist()
        final.filepath_raw = os.path.join(out_dir, f"{name}.png")
        final.file_format = "PNG"
        final.alpha_mode = "STRAIGHT"
        final.save()
        bpy.data.images.remove(color_img)
        finals.append((mat, final, alpha))
    for mat, image, alpha in finals:
        image.reload()                        # 디스크 PNG가 정본
        _to_image_material(mat, image, alpha)
    return [os.path.join(out_dir, f"{n}.png") for n, s, _, _ in MATERIALS if s]


# ──────────────────────────────────────────────────────────── 뼈대 + 숨쉬기

def _edit_bone(arm, name, head, tail, parent=None, connect=False):
    b = arm.data.edit_bones.new(name)
    b.head, b.tail = Vector(head), Vector(tail)
    b.inherit_scale = "NONE"                  # 🔴 가슴 마디를 부풀려도 머리·턱이 같이 커지지 않게
    if parent is not None:
        b.parent = parent
        b.use_connect = connect
    return b


def rig(obj, info):
    """Generic 뼈대: Root(수면 원점)·Tail·Spine_01~08·Head·Jaw·Crest_L/R. 자동 가중치로 붙인다."""
    arm_data = bpy.data.armatures.new(NAME + "_뼈대")
    arm = bpy.data.objects.new(NAME + "_뼈대", arm_data)
    for col in obj.users_collection:
        col.objects.link(arm)
    arm.location = obj.location
    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")

    pts = info["spine"]
    root = _edit_bone(arm, "Root", (0, 0, 0), (0, -1.0, 0))
    tail = _edit_bone(arm, "Tail", pts[0] - info["tail_dir"] * 3.0, pts[0], root)
    prev = tail
    for i in range(len(pts) - 1):
        prev = _edit_bone(arm, f"Spine_{i + 1:02d}", pts[i], pts[i + 1], prev, connect=True)
    head = _edit_bone(arm, "Head", pts[-1], info["head_end"], prev, connect=True)
    _edit_bone(arm, "Jaw", info["jaw_root"], info["jaw_tip"], head)
    for side, (base, tip) in zip(("L", "R"), info["crest"]):
        _edit_bone(arm, f"Crest_{side}", base, tip, head)
    bpy.ops.object.mode_set(mode="OBJECT")
    skin(obj, arm)
    return arm


def skin(obj, arm, blend=0.6):
    """가중치를 직접 준다 — 자동 가중치(bone heat)는 막·눈알처럼 떨어진 조각이 있으면 조용히
    실패해 뼈가 메시를 못 움직였다(첫 시험: 뼈를 움직여도 정점이 그대로). 정점마다 가장 가까운
    뼈 두 개(뼈를 선분으로 보고 거리)에 거리 반비례로 나눠 준다 — 튜브 몸엔 이쪽이 더 매끈하다."""
    bones = [(b.name, Vector(b.head_local), Vector(b.tail_local)) for b in arm.data.bones if b.name != "Root"]

    def seg_dist(p, a, b):
        ab = b - a
        t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared)) if ab.length_squared > 1e-9 else 0.0
        return (p - (a + ab * t)).length

    groups = {name: obj.vertex_groups.new(name=name) for name, _, _ in bones}
    for v in obj.data.vertices:
        p = v.co
        near = sorted(((seg_dist(p, a, b), name) for name, a, b in bones))[:2]
        (d0, n0), (d1, n1) = near
        w0 = 1.0 if d1 < 1e-6 else max(0.0, min(1.0, 0.5 + (d1 - d0) / (blend * (d0 + d1) + 1e-6) * 0.5))
        groups[n0].add([v.index], w0, "REPLACE")
        if w0 < 1.0:
            groups[n1].add([v.index], 1.0 - w0, "REPLACE")
    obj.parent = arm
    mod = obj.modifiers.new("Armature", "ARMATURE")
    mod.object = arm


def _fcurves(action):
    if hasattr(action, "fcurves"):
        return list(action.fcurves)
    return [fc for layer in action.layers for strip in layer.strips for bag in strip.channelbags for fc in bag.fcurves]


def breath(arm, info, swell=0.02, rise=0.12, keys=16):
    """Idle_Breath — 8초·240프레임 한 주기 반복. 모든 채널이 한 주기에 딱 한 번 갔다 온다(0→1→0 곡선),
    첫 프레임 = 마지막 프레임. 키는 정수 프레임 16개, 자동 클램프 베지어라 사인 모양이고 오버슈트가 없다.

    사장님 피드백(요동친다)으로 정한 폭: 몸 오르내림 0.12m, 가슴 부풂 2%, 턱 2.5°, 볏 4°, 꼬리 3.5°,
    굴곡 굽힘 2.5°. 머리·목은 거의 고정, 뒤로 갈수록 위상을 조금씩 늦춰 물결이 천천히 뒤로 흘러간다."""
    scene = bpy.context.scene
    scene.render.fps = FPS
    scene.frame_start, scene.frame_end = 1, FRAMES
    if arm.animation_data is None:
        arm.animation_data_create()
    action = bpy.data.actions.new("Idle_Breath")
    arm.animation_data.action = action
    bones = arm.pose.bones
    for b in bones:
        b.rotation_mode = "XYZ"
    spine = [b for b in bones if b.name.startswith("Spine_")]
    n = len(spine)
    lag = {b.name: (n - 1 - i) / (n - 1) * 0.35 * math.pi for i, b in enumerate(spine)}   # 꼬리 쪽이 가장 늦다
    lag["Tail"] = 0.4 * math.pi
    chest = spine[5:] + [bones["Head"]]
    hump_pairs = []
    for t in info["humps"]:
        k = min(n - 2, max(1, int(t * n)))
        hump_pairs.append((spine[k - 1], spine[k + 1], lag[spine[k].name]))

    def pulse(phase, shift=0.0):
        return (1 - math.cos(phase - shift)) / 2          # 0 → 1 → 0, 한 주기에 한 번

    def key(bone, frame, path, values):
        setattr(bone, path, values)
        bone.keyframe_insert(path, frame=frame)

    for s in range(keys + 1):
        frame = 1 + round((FRAMES - 1) * s / keys)
        phase = math.tau * s / keys
        for b in chest:
            p = pulse(phase, lag.get(b.name, 0.0))
            key(b, frame, "scale", (1 + swell * p, 1.0, 1 + swell * p))
        key(bones["Root"], frame, "location", (0.0, 0.0, rise * pulse(phase)))
        key(bones["Jaw"], frame, "rotation_euler", (-math.radians(2.5) * pulse(phase), 0.0, 0.0))
        for name, sign in (("Crest_L", 1), ("Crest_R", -1)):
            key(bones[name], frame, "rotation_euler", (0.0, sign * math.radians(4) * pulse(phase, 0.15 * math.pi), 0.0))
        for before, after, shift in hump_pairs:
            key(before, frame, "rotation_euler", (math.radians(2.5) * pulse(phase, shift), 0.0, 0.0))
            key(after, frame, "rotation_euler", (-math.radians(2.5) * pulse(phase, shift), 0.0, 0.0))
        key(bones["Tail"], frame, "rotation_euler", (0.0, 0.0, math.radians(3.5) * pulse(phase, lag["Tail"])))
    for fc in _fcurves(action):
        for kp in fc.keyframe_points:
            kp.interpolation = "BEZIER"
            kp.handle_left_type = kp.handle_right_type = "AUTO_CLAMPED"
    return action


def measure(arm):
    """PM이 재는 방식 — 채널별 값 폭과 방향 전환 횟수(1이면 한 번 갔다 온 것)."""
    action = arm.animation_data.action
    rows = []
    for fc in _fcurves(action):
        vals = [fc.evaluate(f) for f in range(1, FRAMES + 1)]
        diffs = [b - a for a, b in zip(vals, vals[1:]) if abs(b - a) > 1e-7]
        turns = sum(1 for a, b in zip(diffs, diffs[1:]) if (a > 0) != (b > 0))
        bone = fc.data_path.split('"')[1] if '"' in fc.data_path else fc.data_path
        rows.append((bone, fc.data_path.rsplit(".", 1)[-1], fc.array_index, round(max(vals) - min(vals), 4), turns,
                     round(abs(vals[0] - vals[-1]), 6)))
    return rows


def check_loop(arm):
    scene = bpy.context.scene
    def pose_at(f):
        scene.frame_set(f)
        return [(b.name, tuple(round(v, 5) for v in b.matrix.translation)) for b in arm.pose.bones]
    return pose_at(1) == pose_at(FRAMES)


# ──────────────────────────────────────────────────────────── 조립

def assemble(collection, tex_dir=None):
    """메시 → 재질 굽기 → 뼈대 → 동작. 창과 화면 없는 실행이 같은 길을 탄다."""
    obj, info = build(collection)
    make_materials()
    textures = bake_textures(obj, tex_dir)
    arm = rig(obj, info)
    action = breath(arm, info)
    return obj, arm, action, textures, info


def export(obj, arm, folder=None):
    folder = folder or OUT_ROOT
    os.makedirs(folder, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(folder, f"{NAME}.fbx"),
        use_selection=True,
        global_scale=UNITS_PER_METER,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",
        mesh_smooth_type="FACE",
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=True,
        bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=True,
        bake_anim_step=1.0,
        bake_anim_simplify_factor=0.0,
        path_mode="STRIP",                   # 경로를 아예 안 넣는다 — 유니티는 재질 이름으로 PNG를 찾는다(PM 약속)
    )


def rows(collection):
    """show_all의 extra 훅 — 판_해왕류에 완성본을 올린다(창에서 짓고 굽고 뼈대·동작까지, 내보내기만 빼고).
    자리는 뼈대(부모)를 옮긴다 — 메시는 자식이라 따라온다."""
    here = os.path.dirname(os.path.abspath(__file__))
    if here not in sys.path:
        sys.path.insert(0, here)
    import showcase
    obj, arm, action, textures, info = assemble(collection)
    group = showcase.group("SeaKing_Serpent", [(obj, 0.0, 0.0)])
    group["parts"] = [(arm, 0.0, 0.0)]
    return [("해왕류", [group])]


def play_in_window():
    """사장님 창의 타임라인을 재생한다(숨 쉬는 게 보이게)."""
    for window in bpy.context.window_manager.windows:
        for area in window.screen.areas:
            if area.type == "VIEW_3D":
                with bpy.context.temp_override(window=window, screen=window.screen, area=area, region=area.regions[-1]):
                    if not bpy.context.screen.is_animation_playing:
                        bpy.ops.screen.animation_play()
                return True
    return False


def clear_scene():
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for block in (bpy.data.meshes, bpy.data.materials, bpy.data.objects, bpy.data.armatures, bpy.data.actions, bpy.data.images):
        for item in list(block):
            if item.users == 0:
                block.remove(item)


def main():
    clear_scene()
    obj, arm, action, textures, info = assemble(bpy.context.scene.collection)
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    export(obj, arm)
    os.makedirs(OUT_ROOT, exist_ok=True)
    with open(os.path.join(OUT_ROOT, "SOURCE.txt"), "w", encoding="utf-8") as f:
        f.write(
            "출처: 직접 생성 (Tools/blender/gen_seaking.py)\n"
            "만든 날: 2026-09-12\n"
            f"Blender {bpy.app.version_string}, 사장님 창에서 짓고 확인한 뒤 화면 없이 내보냄\n\n"
            f"크기 기준: 1m = {UNITS_PER_METER} 게임 단위(사람 키 20 = 1.75m). 몸길이 굴곡 따라 약 29m, 가로 24m.\n"
            "원점: 수면(z=0). 물에 잠기는 부분은 음수 높이 — 게임 바다 판이 가린다.\n"
            f"재질: {', '.join(n for n, *_ in MATERIALS)} — 텍스처는 Textures/<재질이름>.png, "
            "`_잎카드`는 양면+알파 컷(막), 나머지 불투명.\n"
            f"동작: Idle_Breath 8초·{FPS}fps·{FRAMES}프레임 반복(첫=끝). FBX 클립 이름은 「{NAME}_뼈대|Idle_Breath」.\n"
            f"삼각형 {tris}개.\n\n"
            "진행 상태(2026-09-12, 사장님 지시로 여기서 일단 멈춤 — 유니티 연결은 아직):\n"
            "  [됨] 얼굴 — 원작 해왕류 느낌(길게 찢어진 입·위아래 이빨 줄·두툼한 주둥이·콧구멍·박힌 눈+눈썹 뼈·지느러미 귀·수염)\n"
            "  [됨] 목 토시 이음새(반지름 코사인 보간) · 까만 조각(눈 뒷면, 눈을 박아 해결) · 굴곡 꺾임(제어점 고르게)\n"
            "  [됨] 애니메이션 차분하게 — 8초·240프레임·30fps, 폭 축소(오르내림 0.12·부풂 2%·턱 2.5°·볏 4°·꼬리 3.5°), 첫=끝\n"
            "  [남음] 사장님·PM 최종 검수 뒤 세부 조정(위상 지연을 둘지, 눈 크기·이빨 수 등) · 유니티 프리팹 교체(PM)\n")
    print("=" * 60)
    print(f"만듦  {NAME}  삼각형 {tris}  뼈 {len(arm.data.bones)}  텍스처 {len(textures)}  루프 {check_loop(arm)}")


if __name__ == "__main__":
    main()
