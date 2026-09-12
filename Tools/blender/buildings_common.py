"""스토리 건물 13종 공용 — 치수 규칙·메시 도구(Builder)·간판 글자·UV·재질 연결·검사·내보내기.

건물은 gen_buildings_a.py(Story01~07)·gen_buildings_b.py(Story08~13)가 짓는다. 둘 다 이 모듈만 import하고,
텍스처(절차적 셰이더 → PNG 굽기)는 buildings_textures.py가 따로 맡는다 — 여기서는 굽지 않고 구워 둔 PNG를 잇기만 한다.

화면 없이(내보내기):
    blender --background --factory-startup --python Tools/blender/gen_buildings_a.py [-- Story01 Story02]
사장님 창에서는 gen_buildings_*.py를 exec해 groups(collection)만 부른다(show_all.py의 extra 훅) — 창 모양 = FBX 모양.

🔴 규격(사장님·PM 확정 2026-09-12) — 치수는 게임 단위(사람 키 20), 짓는 좌표도 게임 단위.
    바닥 45×45 안 · 높이 40~90 · 원점 = 바닥면 한가운데 · 정면(간판)은 Blender −Y · 삼각형 10,000 이하
    내보낼 때 1/11.4로 미터로 줄였다가 11.4배로 내보낸다(벽·나무와 같은 방식).
🔴 재질 이름 = 텍스처 파일 이름: Assets/Art/Buildings/Textures/<이름>.png. 건물 재질은 전부 `건물_` 접두(창에서 벽의
    `벽돌` 따위와 안 섞이게). 투명(알파 컷)이 필요한 것만 이름 끝 `_잎카드`. 유리는 투명 아님 — 불투명 유리색.
    FBX는 path_mode="RELATIVE". 재질은 이름으로 공유해 `.001`이 안 붙는다.
🔴 간판 글자는 메시로 바꿔 FBX에 넣는다(글꼴 파일은 유니티로 안 간다). 글꼴은 AppleSDGothicNeo.ttc —
    한글·한자(日本)·영문이 다 들어 있다(2026-09-12 확인: Hiragino GB는 한글이 빈 네모).
UV: 면마다 평면 투영. 가로축 = 밖에서 봤을 때 오른쪽, 세로축 = 면 위의 위쪽(비탈 지붕은 비탈 방향).
    재질 tile(게임 단위)로 나눠 반복, tile=None(FIT)이면 그 면 하나에 0~1로 늘인다(창문·시계판·국기).
면 방향: 모든 도구가 밖을 향하게 짓는다(유니티는 뒷면을 안 그린다). 직접 면을 만들 땐 밖에서 봐서 반시계.
"""

import glob
import importlib
import math
import os
import sys

import bpy
import bmesh
from mathutils import Matrix, Vector

UNITS_PER_METER = 11.4
PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_ROOT = os.path.join(PROJECT, "Assets", "Art", "Buildings")
TEX_DIR = os.path.join(OUT_ROOT, "Textures")
FONT_PATH = "/System/Library/Fonts/AppleSDGothicNeo.ttc"

FOOTPRINT_MAX = 45.0
HEIGHT_RANGE = (40.0, 90.0)
TRI_LIMIT = 10000
FLOOR_H = 15.0          # 한 층 높이 — 5층 본관 + 옥상 난간이 90 안에 들어가는 값. 창문은 이 비례 그대로
# 🔴 사람이 드나드는 곳(출입문·정문·도리이 기둥 사이·위병소 문)만 사람 키 20보다 크게(PM 2026-09-12) —
# 층 비례로 문을 내면 유닛이 문보다 커 보인다. 문이 있는 1층은 그만큼 높인다.
DOOR_H = 22.0
GROUND_H = 25.0         # 출입문이 있는 1층 높이(문 DOOR_H + 위 여유)
UP = Vector((0.0, 0.0, 1.0))

# 면 이름 → 바깥 방향. 정면은 "-y".
SIDES = {"-y": Vector((0, -1, 0)), "+y": Vector((0, 1, 0)), "-x": Vector((-1, 0, 0)), "+x": Vector((1, 0, 0))}

# buildings_textures.py가 아직 없거나 목록에 없는 재질일 때만 쓰는 값(창에서 모양부터 잡을 때).
FIT_NAMES = {"건물_유리창", "건물_원룸창", "건물_시계판", "건물_국기_태극기"}
FALLBACK_TILE = 8.0
FALLBACK_COLOR = (0.6, 0.6, 0.6, 1.0)


# ──────────────────────────────────────────────────────────── 재질

def _textures():
    """buildings_textures 모듈(없으면 None). 창에서 고친 내용이 바로 반영되게 다시 읽는다."""
    here = os.path.dirname(os.path.abspath(__file__))
    if here not in sys.path:
        sys.path.insert(0, here)
    try:
        import buildings_textures
    except ImportError:
        return None
    return importlib.reload(buildings_textures)


_TEX_CACHE = {}


def material_info(name):
    """(tile, 뷰포트 색) — tile=None이면 FIT."""
    if name not in _TEX_CACHE:
        tex = _textures()
        if tex is not None and name in tex.MATERIALS:
            tile, color = tex.MATERIALS[name][0], tex.MATERIALS[name][1]
        else:
            if tex is not None:
                print(f"⚠️ buildings_textures.MATERIALS에 없는 재질: {name}")
            tile, color = (None if name in FIT_NAMES else FALLBACK_TILE), FALLBACK_COLOR
        _TEX_CACHE[name] = (tile, color)
    return _TEX_CACHE[name]


def material(name):
    """구워 둔 PNG를 잇는 이미지 재질. PNG가 아직 없으면 뷰포트 색만 칠한 재질(경고)."""
    if not name.startswith("건물_"):
        raise ValueError(f"건물 재질은 `건물_` 접두: {name}")
    tile, color = material_info(name)
    path = os.path.join(TEX_DIR, f"{name}.png")
    mat = bpy.data.materials.get(name)
    if mat is not None:
        images = [n.image for n in mat.node_tree.nodes if n.type == "TEX_IMAGE" and n.image] if mat.use_nodes else []
        if images or not os.path.exists(path):
            return mat                        # 이미 이미지 재질(창에서 구운 것 포함)이거나, 아직 이을 PNG가 없다
    else:
        mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = color
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    bsdf.inputs["Roughness"].default_value = 0.8
    nt.links.new(bsdf.outputs["BSDF"], out.inputs["Surface"])
    if os.path.exists(path):
        node = nt.nodes.new("ShaderNodeTexImage")
        node.image = bpy.data.images.load(path, check_existing=True)
        nt.links.new(node.outputs["Color"], bsdf.inputs["Base Color"])
        if name.endswith("_잎카드"):
            nt.links.new(node.outputs["Alpha"], bsdf.inputs["Alpha"])
        nt.nodes.active = node
    else:
        bsdf.inputs["Base Color"].default_value = color
        print(f"⚠️ 텍스처 없음(색만): {path}")
    return mat


# ──────────────────────────────────────────────────────────── Builder

def _face_axes(normal):
    """면 평면 위의 (오른쪽, 위) — 밖에서 봤을 때. 수평면은 (+x, ±y)."""
    if abs(normal.z) > 0.999:
        return Vector((1, 0, 0)), Vector((0, 1 if normal.z > 0 else -1, 0))
    right = UP.cross(normal).normalized()
    return right, normal.cross(right).normalized()


def side_frame(side):
    """면 이름 → (바깥 n, 오른쪽 r, 위 u)."""
    n = SIDES[side]
    return n, UP.cross(n).normalized(), UP.copy()


class Builder:
    """건물 하나 = bmesh 하나. 도구는 전부 재질 이름을 받는다(순서·번호는 Builder가 관리)."""

    def __init__(self):
        self.bm = bmesh.new()
        self.names = []

    def m(self, name):
        if name not in self.names:
            material_info(name)                # 이름 오타는 여기서 경고
            self.names.append(name)
        return self.names.index(name)

    def v(self, p):
        return self.bm.verts.new(Vector(p))

    def face(self, points, mat):
        """점(좌표나 BMVert) 목록으로 면 하나 — 밖에서 봐서 반시계 순서로 줄 것."""
        verts = [p if isinstance(p, bmesh.types.BMVert) else self.v(p) for p in points]
        f = self.bm.faces.new(verts)
        f.material_index = self.m(mat)
        return f

    # ── 상자·기둥

    def box_frame(self, origin, ex, ey, ez, mat, skip=()):
        """origin에서 ex·ey·ez 세 모서리 벡터로 뻗은 평행육면체. skip: "bottom"/"top"/"-y"/"+y"/"-x"/"+x" — 부르는 쪽이 준
        ex·ey·ez 기준(−y = origin 쪽 ey 면). 왼손 좌표로 줘도 알아서 뒤집어 밖을 보게 한다."""
        origin, ex, ey, ez = Vector(origin), Vector(ex), Vector(ey), Vector(ez)
        if ex.cross(ey).dot(ez) < 0:          # 왼손 좌표면 면이 안쪽을 본다 — ey를 뒤집어 바로잡는다
            origin, ey = origin + ey, -ey
            skip = tuple({"-y": "+y", "+y": "-y"}.get(s, s) for s in skip)
        c = [self.v(origin + ex * i + ey * j + ez * k)
             for i, j, k in ((0, 0, 0), (1, 0, 0), (1, 1, 0), (0, 1, 0), (0, 0, 1), (1, 0, 1), (1, 1, 1), (0, 1, 1))]
        index = {"bottom": (0, 3, 2, 1), "top": (4, 5, 6, 7), "-y": (0, 1, 5, 4), "+y": (2, 3, 7, 6),
                 "-x": (3, 0, 4, 7), "+x": (1, 2, 6, 5)}
        for name, ids in index.items():
            if name not in skip:
                self.face([c[i] for i in ids], mat)

    def box(self, x0, x1, y0, y1, z0, z1, mat, skip=()):
        """축에 맞춘 상자. 땅에 닿는 밑면은 skip=("bottom",)으로 빼면 삼각형이 준다."""
        self.box_frame((x0, y0, z0), (x1 - x0, 0, 0), (0, y1 - y0, 0), (0, 0, z1 - z0), mat, skip)

    def box_c(self, cx, cy, w, d, z0, h, mat, skip=()):
        """가운데(cx, cy)·가로 w·앞뒤 d·밑 z0·높이 h."""
        self.box(cx - w / 2, cx + w / 2, cy - d / 2, cy + d / 2, z0, z0 + h, mat, skip)

    def beam(self, p0, p1, width, height, mat):
        """두 점 사이의 각재(난간·깃대·미끄럼틀 옆판·도리이 기둥). 단면 width(가로)×height(세로)."""
        p0, p1 = Vector(p0), Vector(p1)
        d = p1 - p0
        dn = d.normalized()
        ref = UP if abs(dn.z) < 0.95 else Vector((0, -1, 0))
        s = ref.cross(dn).normalized()
        u = dn.cross(s)
        self.box_frame(p0 - s * (width / 2) - u * (height / 2), d, s * width, u * height, mat)

    def ring(self, center, radius, sides, spin=0.0, ry=None):
        cx, cy, cz = center
        return [self.v((cx + math.cos(spin + math.tau * i / sides) * radius,
                        cy + math.sin(spin + math.tau * i / sides) * (ry or radius), cz)) for i in range(sides)]

    def bridge(self, lower, upper, mat):
        n = len(lower)
        for i in range(n):
            j = (i + 1) % n
            self.face((lower[i], lower[j], upper[j], upper[i]), mat)

    def cap(self, ring, center, mat, up=True):
        c = self.v(center)
        n = len(ring)
        for i in range(n):
            j = (i + 1) % n
            self.face((c, ring[i], ring[j]) if up else (c, ring[j], ring[i]), mat)

    def cylinder(self, x, y, z0, z1, radius, sides, mat, top="cap", top_radius=None, base_cap=False, top_mat=None):
        """세로 원기둥(물탱크·게양대·굴뚝). top: cap · point · none."""
        lower = self.ring((x, y, z0), radius, sides)
        upper = self.ring((x, y, z1), radius if top_radius is None else top_radius, sides)
        self.bridge(lower, upper, mat)
        if base_cap:
            self.cap(lower, (x, y, z0), mat, up=False)
        if top == "cap":
            self.cap(upper, (x, y, z1), top_mat or mat)
        elif top == "point":
            self.cap(upper, (x, y, z1 + radius * 1.6), top_mat or mat)

    # ── 단면 뽑기·지붕

    def prism(self, profile, x0, x1, mat, closed=True, caps="fan", cap_mat=None):
        """(y, z) 단면을 x0→x1로 뽑는다. 단면은 +x에서 봐서(오른쪽 = +y) 반시계 순서.
        caps: "fan"(볼록 단면 — 가운데 한 점) · "none" · [(단면 번호, ...), ...](오목 단면을 볼록 조각으로)."""
        a = [self.v((x0, y, z)) for y, z in profile]
        b = [self.v((x1, y, z)) for y, z in profile]
        n = len(profile)
        for k in range(n if closed else n - 1):
            j = (k + 1) % n
            self.face((a[k], a[j], b[j], b[k]), mat)
        cm = cap_mat or mat
        if caps == "none":
            return
        if caps == "fan":
            cy = sum(p[0] for p in profile) / n
            cz = sum(p[1] for p in profile) / n
            for k in range(n if closed else n - 1):
                j = (k + 1) % n
                self.face((b[k], b[j], self.v((x1, cy, cz))), cm)
                self.face((a[j], a[k], self.v((x0, cy, cz))), cm)
            return
        for ids in caps:
            self.face([b[i] for i in ids], cm)
            self.face([a[i] for i in reversed(ids)], cm)

    def gable_roof(self, x0, x1, y0, y1, z0, rise, roof_mat, gable_mat=None, overhang=1.5, thickness=0.8, under_mat=None,
                   ridge="x"):
        """박공지붕 — 벽 윗면 z0(벽 사각형 x0..x1, y0..y1)에서 rise만큼 올라간다. ridge="x"면 용마루가 x축(처마가 앞뒤),
        "y"면 용마루가 y축(박공 삼각형이 정면 −y를 본다 — 동화책 집). gable_mat이 있으면 박공벽 삼각형도 채운다."""
        if ridge == "y":                       # x↔y를 바꿔 지으면 거울상이라 면 순서를 뒤집어야 밖을 본다
            a0, a1, b0, b1 = y0, y1, x0, x1
            to_world = lambda p: (p[1], p[0], p[2])
            flip = True
        else:
            a0, a1, b0, b1 = x0, x1, y0, y1
            to_world = lambda p: p
            flip = False

        def put(points, mat):
            pts = [to_world(p) for p in points]
            self.face(list(reversed(pts)) if flip else pts, mat)

        cb = (b0 + b1) / 2
        slope = rise / ((b1 - b0) / 2)
        be0, be1 = b0 - overhang, b1 + overhang
        ze = z0 - overhang * slope
        # 단면(b, z): 앞 처마 위 → 앞 처마 아래 → 용마루 아래 → 뒤 처마 아래 → 뒤 처마 위 → 용마루 위(+a에서 반시계)
        sec = [(be0, ze), (be0, ze - thickness), (cb, z0 + rise - thickness), (be1, ze - thickness), (be1, ze), (cb, z0 + rise)]
        aa, ab = a0 - overhang, a1 + overhang
        under = under_mat or roof_mat
        mats = (roof_mat, under, under, roof_mat, roof_mat, roof_mat)
        for k in range(6):
            j = (k + 1) % 6
            put(((aa, *sec[k]), (aa, *sec[j]), (ab, *sec[j]), (ab, *sec[k])), mats[k])
        for ids in ((0, 1, 2, 5), (5, 2, 3, 4)):
            put([(ab, *sec[i]) for i in ids], roof_mat)
            put([(aa, *sec[i]) for i in reversed(ids)], roof_mat)
        if gable_mat:
            apex = z0 + rise - thickness
            put(((a1, b0, z0), (a1, b1, z0), (a1, cb, apex)), gable_mat)
            put(((a0, b1, z0), (a0, b0, z0), (a0, cb, apex)), gable_mat)

    def hip_roof(self, x0, x1, y0, y1, z0, rise, mat, inset=None, bottom=True):
        """모임지붕 — 처마 사각형(x0..x1, y0..y1, z0)에서 용마루(x축)까지 네 비탈. inset = 용마루 끝이 x 끝에서
        들어온 거리(기본 앞뒤 절반 → 45° 추녀). 처마 내밀기는 부르는 쪽이 사각형을 넓혀서 준다."""
        cy = (y0 + y1) / 2
        r = (y1 - y0) / 2 if inset is None else inset
        zt = z0 + rise
        e00, e10, e11, e01 = (self.v(p) for p in ((x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0)))
        if x1 - x0 > 2 * r + 1e-4:
            r0, r1 = self.v((x0 + r, cy, zt)), self.v((x1 - r, cy, zt))
            self.face((e00, e10, r1, r0), mat)
            self.face((e11, e01, r0, r1), mat)
        else:                                  # 네모 반듯하면 피라미드
            r0 = r1 = self.v(((x0 + x1) / 2, cy, zt))
            self.face((e00, e10, r0), mat)
            self.face((e11, e01, r0), mat)
        self.face((e10, e11, r1), mat)
        self.face((e01, e00, r0), mat)
        if bottom:
            self.face((e00, e01, e11, e10), mat)

    def half_cylinder(self, x0, x1, cy, radius, z0, sides, mat, end_mat=None):
        """반원형 지붕 건물(탄약고·퀀셋) — x축으로 누운 반원통. 양 끝은 반원판(end_mat)."""
        profile = [(cy + math.cos(math.pi * k / sides) * radius, z0 + math.sin(math.pi * k / sides) * radius)
                   for k in range(sides + 1)]
        self.prism(profile, x0, x1, mat, closed=False, caps="none")
        em = end_mat or mat
        ca, cb = self.v((x0, cy, z0)), self.v((x1, cy, z0))
        for k in range(sides):
            p, q = profile[k], profile[k + 1]
            self.face((cb, self.v((x1, *p)), self.v((x1, *q))), em)
            self.face((ca, self.v((x0, *q)), self.v((x0, *p))), em)

    # ── 벽에 붙이는 것

    def panel(self, side, plane, u, z, w, h, mat, offset=0.08):
        """벽면(side, 그 면의 좌표 plane)에 붙는 사각 판 하나. u = 면 가로 위치(−y·+y면은 x, ±x면은 y), z = 밑.
        offset만큼 벽에서 띄워 깜빡임(z-fighting)을 막는다."""
        n, r, _ = side_frame(side)
        base = self._on_side(side, plane, u) + n * offset
        bl = base - r * (w / 2) + UP * z
        return self.face((bl, bl + r * w, bl + r * w + UP * h, bl + UP * h), mat)

    def _on_side(self, side, plane, u):
        return Vector((u, plane, 0.0)) if side in ("-y", "+y") else Vector((plane, u, 0.0))

    def windows(self, side, plane, us, zs, w, h, mat="건물_유리창", sill_mat=None, sill=0.5):
        """창문 격자 — us(가로 위치들) × zs(창 밑 높이들). 창 한 장 = 판 하나(FIT 텍스처). sill_mat이면 아래 턱."""
        n, r, _ = side_frame(side)
        for z in zs:
            for u in us:
                self.panel(side, plane, u, z, w, h, mat)
                if sill_mat:
                    base = self._on_side(side, plane, u)
                    self.box_frame(base - r * (w / 2 + 0.3) + UP * (z - sill), r * (w + 0.6), n * sill * 1.2, UP * sill,
                                   sill_mat, skip=("-y",))   # 로컬 −y = 벽에 붙는 면

    def railing(self, points, height, mat, post_gap=3.0, post=0.35, rail=0.3, closed=False):
        """난간 — 꺾은선을 따라 기둥(post_gap 간격)과 윗 난간대. 옥상 난간·베란다·게양대 울타리."""
        pts = [Vector(p) for p in points]
        if closed:
            pts.append(pts[0])
        for a, b in zip(pts, pts[1:]):
            length = (b - a).length
            count = max(1, round(length / post_gap))
            for k in range(count + (0 if closed or b is not pts[-1] else 1)):
                p = a + (b - a) * (k / count)
                self.box_c(p.x, p.y, post, post, p.z, height, mat, skip=("bottom",))
            self.beam(a + UP * (height - rail / 2), b + UP * (height - rail / 2), rail, rail, mat)

    # ── 글자·간판

    def _text_mesh(self, body, size, depth):
        """글꼴 곡선 → (로컬 점, 면 목록, (xmin, xmax, ymin, ymax)). 로컬: 글자 가로 +x, 위 +y, 앞 +z."""
        font = bpy.data.fonts.load(FONT_PATH, check_existing=True)
        cu = bpy.data.curves.new("_간판글자", "FONT")
        cu.body = body
        cu.font = font
        cu.size = size
        cu.align_x = "CENTER"
        cu.align_y = "CENTER"
        cu.resolution_u = 2                    # 곡선 잘게 쪼개기 — 2면 멀리서 둥글고 삼각형이 적다
        cu.fill_mode = "FRONT"
        cu.extrude = depth / 2
        ob = bpy.data.objects.new("_간판글자", cu)
        bpy.context.scene.collection.objects.link(ob)
        ev = ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
        me = ev.to_mesh()
        pts = [v.co.copy() + Vector((0, 0, depth / 2)) for v in me.vertices]   # 뒷면이 z=0에 오게
        faces = [tuple(p.vertices) for p in me.polygons]
        ev.to_mesh_clear()
        bpy.data.objects.remove(ob, do_unlink=True)
        bpy.data.curves.remove(cu)
        if not pts:
            raise ValueError(f"글자 메시가 비었다(글꼴에 없는 글자?): {body!r}")
        xs = [p.x for p in pts]
        ys = [p.y for p in pts]
        return pts, faces, (min(xs), max(xs), min(ys), max(ys))

    def text(self, body, center, side="-y", size=4.0, mat="건물_색_흰", depth=0.3, max_width=None):
        """글자 메시. center = 글자 뒷면 한가운데(월드), 글자는 side 방향을 보고 선다. 줄바꿈 "\\n"이면 여러 줄.
        max_width를 넘으면 줄여 맞춘다. 돌려주는 값: (가로, 세로) 월드 크기."""
        pts, faces, (x0, x1, y0, y1) = self._text_mesh(body, size, depth)
        scale = 1.0
        if max_width and x1 - x0 > max_width:
            scale = max_width / (x1 - x0)
        n, r, u = side_frame(side)
        rot = Matrix(((r.x, u.x, n.x), (r.y, u.y, n.y), (r.z, u.z, n.z)))   # 로컬 (x, y, z) → 월드 (r, u, n)
        c = Vector(center)
        cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
        verts = [self.v(c + rot @ (Vector((p.x - cx, p.y - cy, p.z)) * scale)) for p in pts]
        mi = self.m(mat)
        for ids in faces:
            try:
                self.bm.faces.new([verts[i] for i in ids]).material_index = mi
            except ValueError:
                pass                           # 글꼴 곡선이 드물게 겹친 면을 낸다 — 버린다
        return (x1 - x0) * scale, (y1 - y0) * scale

    def sign(self, body, side, plane, u, z, board_mat, text_mat, size=4.0, pad=1.0, board_depth=0.6, text_depth=0.3,
             max_width=None, min_width=0.0):
        """간판 = 판(board_mat 상자) + 앞에 글자(text_mat). 벽면(side, plane)의 가로 u·높이 z(판 밑)에 붙는다.
        판 크기는 글자 크기 + pad. 돌려주는 값: 판 (가로, 세로)."""
        _, _, (x0, x1, y0, y1) = self._text_mesh(body, size, text_depth)
        scale = min(1.0, max_width / (x1 - x0)) if max_width else 1.0
        tw, th = (x1 - x0) * scale, (y1 - y0) * scale
        bw, bh = max(tw + pad * 2, min_width), th + pad * 2
        n, r, _ = side_frame(side)
        base = self._on_side(side, plane, u)
        self.box_frame(base - r * (bw / 2) + UP * z, r * bw, n * board_depth, UP * bh, board_mat, skip=("-y",))
        self.text(body, base + n * (board_depth + 0.02) + UP * (z + bh / 2), side, size, text_mat, text_depth,
                  max_width=max_width)
        return bw, bh

    # ── 마무리

    def to_object(self, name, collection, meters=True):
        """bmesh → 오브젝트. 면마다 평면 투영 UV, 쓰는 재질만 슬롯에. meters=True면 게임 단위 → 미터."""
        bm = self.bm
        uv = bm.loops.layers.uv.new("UVMap")
        bm.normal_update()
        tiles = [material_info(nm)[0] for nm in self.names]
        for f in bm.faces:
            if f.normal.length < 1e-6:
                continue
            right, up = _face_axes(f.normal)
            coords = [(loop.vert.co.dot(right), loop.vert.co.dot(up)) for loop in f.loops]
            tile = tiles[f.material_index]
            if tile is None:
                u0, u1 = min(c[0] for c in coords), max(c[0] for c in coords)
                v0, v1 = min(c[1] for c in coords), max(c[1] for c in coords)
                coords = [((cu - u0) / max(u1 - u0, 1e-6), (cv - v0) / max(v1 - v0, 1e-6)) for cu, cv in coords]
            else:
                coords = [(cu / tile, cv / tile) for cu, cv in coords]
            for loop, c in zip(f.loops, coords):
                loop[uv].uv = c
        used = sorted({f.material_index for f in bm.faces})
        remap = {old: new for new, old in enumerate(used)}
        for f in bm.faces:
            f.material_index = remap[f.material_index]
        if meters:
            bmesh.ops.scale(bm, vec=Vector((1.0, 1.0, 1.0)) / UNITS_PER_METER, verts=bm.verts)
        mesh = bpy.data.meshes.new(name)
        bm.to_mesh(mesh)
        bm.free()
        obj = bpy.data.objects.new(name, mesh)
        collection.objects.link(obj)
        for old in used:
            mesh.materials.append(material(self.names[old]))
        return obj


# ──────────────────────────────────────────────────────────── 검사·내보내기

def triangles(obj):
    return sum(len(p.vertices) - 2 for p in obj.data.polygons)


def check(obj, meters=True):
    """규격 검사 — 문제 문장 목록(비면 통과). 크기는 게임 단위로 잰다."""
    k = UNITS_PER_METER if meters else 1.0
    xs = [v.co.x * k for v in obj.data.vertices]
    ys = [v.co.y * k for v in obj.data.vertices]
    zs = [v.co.z * k for v in obj.data.vertices]
    w, d, h = max(xs) - min(xs), max(ys) - min(ys), max(zs) - min(zs)
    problems = []
    if w > FOOTPRINT_MAX + 1e-3 or d > FOOTPRINT_MAX + 1e-3:
        problems.append(f"바닥 {w:.1f}×{d:.1f} > {FOOTPRINT_MAX:.0f}")
    if not HEIGHT_RANGE[0] <= h <= HEIGHT_RANGE[1]:
        problems.append(f"높이 {h:.1f} (규격 {HEIGHT_RANGE[0]:.0f}~{HEIGHT_RANGE[1]:.0f})")
    if abs(min(zs)) > 0.05:
        problems.append(f"바닥이 z={min(zs):.2f} (원점은 바닥면)")
    if abs((max(xs) + min(xs)) / 2) > 1.5 or abs((max(ys) + min(ys)) / 2) > 3.0:
        problems.append(f"원점이 가운데가 아님 (중심 {(max(xs) + min(xs)) / 2:.1f}, {(max(ys) + min(ys)) / 2:.1f})")
    tris = triangles(obj)
    if tris > TRI_LIMIT:
        problems.append(f"삼각형 {tris} > {TRI_LIMIT}")
    bad = [m.name for m in obj.data.materials if not m.name.startswith("건물_") or "." in m.name]
    if bad:
        problems.append(f"재질 이름 규칙 위반: {bad}")
    return problems, (w, d, h, tris)


def clear_objects():
    """물체·메시만 지운다 — 재질·이미지는 건물 사이에 재사용한다(재질 이름에 .001이 안 붙게)."""
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for mesh in list(bpy.data.meshes):
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)


def export(obj, name):
    os.makedirs(OUT_ROOT, exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT_ROOT, f"{name}.fbx"), use_selection=True, global_scale=UNITS_PER_METER,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", mesh_smooth_type="FACE",
        use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False,
        path_mode="RELATIVE")                # ../Textures 상대경로 — AUTO는 경로가 깨지고 STRIP은 형제 폴더를 못 찾는다


def catalogs():
    """gen_buildings_*.py 전부의 CATALOG를 번호 순으로 — SOURCE.txt를 두 스크립트가 서로 덮어쓰지 않게 한 번에 쓴다."""
    here = os.path.dirname(os.path.abspath(__file__))
    if here not in sys.path:
        sys.path.insert(0, here)
    entries = []
    for path in sorted(glob.glob(os.path.join(here, "gen_buildings_*.py"))):
        name = os.path.splitext(os.path.basename(path))[0]
        module = sys.modules.get(name)
        if module is None or getattr(module, "__file__", None) != path:
            module = importlib.import_module(name)
        elif module.__name__ != "__main__":
            module = importlib.reload(module)  # 창에서 여러 번 돌리므로 고친 내용이 바로 반영되게
        entries += module.CATALOG
    return sorted(entries, key=lambda e: e[0])


def write_source():
    with open(os.path.join(OUT_ROOT, "SOURCE.txt"), "w", encoding="utf-8") as f:
        f.write(
            "출처: 직접 생성 (Tools/blender/buildings_common.py + gen_buildings_a.py·gen_buildings_b.py,"
            " 텍스처 buildings_textures.py)\n"
            "만든 날: 2026-09-12\n"
            f"Blender {bpy.app.version_string}, 사장님 창에서 짓고 확인한 뒤 화면 없이 내보냄\n\n"
            f"크기 기준: 1m = {UNITS_PER_METER} 게임 단위(사람 키 20). 치수는 게임 단위. 한 층 {FLOOR_H:.0f}.\n"
            f"규격: 바닥 {FOOTPRINT_MAX:.0f}×{FOOTPRINT_MAX:.0f} 안 · 높이 {HEIGHT_RANGE[0]:.0f}~{HEIGHT_RANGE[1]:.0f} · "
            f"삼각형 {TRI_LIMIT} 이하 · 원점 바닥면 한가운데 · 정면(간판) −Y(유니티 −Z)\n"
            "재질: 텍스처 Textures/<재질 이름>.png, 전부 `건물_` 접두. `_잎카드`만 알파 컷. 간판 글자는 메시"
            "(글꼴 AppleSDGothicNeo).\n\n"
            "만들어진 것:\n" + "".join(f"  {e[0]:22} {e[3]}\n" for e in catalogs()))


def run(catalog):
    """화면 없이: -- 뒤 접두로 골라(없으면 전부) 짓고 검사하고 내보낸다."""
    prefixes = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    picked = [e for e in catalog if not prefixes or any(e[0].startswith(p) for p in prefixes)]
    if not picked:
        raise SystemExit(f"이름이 맞는 게 없다: {prefixes}")
    made = []
    for name, label, maker, note in picked:
        clear_objects()
        obj = maker().to_object(name, bpy.context.scene.collection)
        problems, (w, d, h, tris) = check(obj)
        export(obj, name)
        made.append((name, w, d, h, tris, problems))
    write_source()
    print("=" * 60)
    for name, w, d, h, tris, problems in made:
        print(f"만듦  {name:22} {w:5.1f}×{d:5.1f}×{h:5.1f}  삼각형 {tris:5d}  " + ("통과" if not problems else "⚠️ " + " / ".join(problems)))
    print(f"총 {len(made)}개 → {OUT_ROOT}")


def groups(catalog, collection, meters=True):
    """사장님 창: CATALOG를 짓고 showcase 무리 목록으로 돌려준다(건물 하나 = 무리 하나, 이름표 = label)."""
    here = os.path.dirname(os.path.abspath(__file__))
    if here not in sys.path:
        sys.path.insert(0, here)
    import showcase
    out = []
    for name, label, maker, note in catalog:
        obj = maker().to_object(name, collection, meters=meters)
        problems, (w, d, h, tris) = check(obj, meters=meters)
        print(f"{name:22} {w:5.1f}×{d:5.1f}×{h:5.1f}  삼각형 {tris:5d}  " + ("통과" if not problems else "⚠️ " + " / ".join(problems)))
        out.append(showcase.group(label, [(obj, 0.0, 0.0)]))
    return out


def story_rows(collection, meters=True):
    """판_스토리(show_all의 extra 훅) — 생활 순서 두 줄: 앞줄 01~07, 뒷줄 08~13. 두 스크립트의 CATALOG를 합쳐 짓는다."""
    entries = catalogs()
    number = lambda e: int(e[0][len("Story"):len("Story") + 2])
    rows = [("01~07", groups([e for e in entries if number(e) <= 7], collection, meters))]
    back = [e for e in entries if number(e) > 7]
    if back:
        rows.append(("08~13", groups(back, collection, meters)))
    return rows
