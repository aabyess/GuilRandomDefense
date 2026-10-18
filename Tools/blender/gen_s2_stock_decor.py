"""S2 맵 장식 중 워크3 기본 모델(맵에 없어 못 꺼낸다)을 Blender로 베껴 만든다 (2026-10-19, 사장님 「구랜디 맵을 원랜디 신작처럼 최대한 카피」).
  blender -b --factory-startup --python Tools/blender/gen_s2_stock_decor.py -- [이름…] [--out 루트]
  산출: ~/grd/GRD_s2_models/map/gen_<이름>/ <이름>.fbx · Textures/*.png · <이름>.json · preview.png ; 끝에 sheet_stock.png
이름: CityLowWall0 CityLowWall90 TallEndCap Waterfall PotofGold Blacksmith PowerGenerator MagicVault Seaweed AshenShrooms CatTail LilyPad AshenRock Rowboat RuinsFlower Battleship BubbleGeyser CircleOfPower
원작 정확한 모양은 모르니 워크3 기본 모델의 실루엣(원작 크기 감각)으로 만든 어림판이다 — json approx:true. 단위: 워크3 1 = 0.01m(다른 반입 FBX와 같음).
FBX 방향 규칙은 export_mdx_anim_fbx와 같음(axis_forward=-Z, axis_up=Y). 정적 메시, 뼈·클립 없음."""
import json, math, os, sys
import bpy
import numpy as np

H = os.path.expanduser("~")
ROOT = H + "/grd/GRD_s2_models/map"
if "--out" in sys.argv:
    ROOT = sys.argv[sys.argv.index("--out") + 1]
S = 0.01


def save_png(arr, path):
    h, w = arr.shape[:2]
    im = bpy.data.images.new(os.path.basename(path), w, h, alpha=True)
    im.pixels.foreach_set(np.ascontiguousarray(arr[::-1]).astype(np.float32).ravel())
    im.filepath_raw = path; im.file_format = "PNG"; im.save(); bpy.data.images.remove(im)


def noise(n, seed, sc=8):
    r = np.random.RandomState(seed); a = r.rand(n // sc + 2, n // sc + 2)
    xi = np.linspace(0, a.shape[0] - 1.001, n); i = xi.astype(int); f = xi - i
    A = a[i][:, i]; B = a[i + 1][:, i]; C = a[i][:, i + 1]; D = a[i + 1][:, i + 1]
    fx = f[:, None]; fy = f[None, :]
    return A * (1 - fx) * (1 - fy) + B * fx * (1 - fy) + C * (1 - fx) * fy + D * fx * fy


def flat(rgb, n=32, jit=0.0, seed=0):
    img = np.ones((n, n, 4), np.float32)
    g = 1 + (noise(n, seed, 4) - 0.5) * jit
    img[..., :3] = np.array(rgb)[None, None] * g[..., None]
    return np.clip(img, 0, 1)


def tex_stone(n=256, base=(0.62, 0.6, 0.55)):
    y, x = np.mgrid[0:n, 0:n]; bh, bw = n // 6, n // 3; row = y // bh
    xx = (x + (row % 2) * bw // 2) % bw
    line = ((y % bh) < 3) | (xx < 3)
    g = 0.82 + 0.18 * noise(n, 2) + 0.08 * noise(n, 3, 3)
    img = np.ones((n, n, 4), np.float32); img[..., :3] = np.array(base)[None, None] * g[..., None]
    img[line, :3] *= 0.55
    return img


def tex_wood(n=128, base=(0.45, 0.3, 0.16)):
    y, x = np.mgrid[0:n, 0:n]; img = np.ones((n, n, 4), np.float32)
    g = 0.8 + 0.2 * np.sin(x / 3.0 + noise(n, 4, 4) * 6) * 0.5 + 0.1 * noise(n, 5, 3)
    img[..., :3] = np.array(base)[None, None] * g[..., None]; img[(y % 32) < 2, :3] *= 0.5
    return np.clip(img, 0, 1)


def tex_metal(n=128, base=(0.5, 0.55, 0.62)):
    img = np.ones((n, n, 4), np.float32); g = 0.8 + 0.25 * noise(n, 6, 5)
    img[..., :3] = np.array(base)[None, None] * g[..., None]
    y, x = np.mgrid[0:n, 0:n]; img[(x % 64 < 2) | (y % 64 < 2), :3] *= 0.55
    return np.clip(img, 0, 1)


def tex_water(n=128):
    y, x = np.mgrid[0:n, 0:n]; img = np.ones((n, n, 4), np.float32)
    streak = 0.5 + 0.5 * np.sin(x / 2.0 + noise(n, 7, 6) * 8)
    img[..., 0] = 0.55 + 0.35 * streak; img[..., 1] = 0.78 + 0.2 * streak; img[..., 2] = 0.95
    img[..., 3] = 0.65 + 0.2 * streak
    return img


def tex_glow(n=64, rgb=(0.4, 0.8, 1.0)):
    y, x = np.mgrid[0:n, 0:n].astype(np.float32); d = np.hypot(x - n / 2, y - n / 2) / (n / 2)
    v = np.clip(1.2 - d, 0, 1) ** 1.5; img = np.ones((n, n, 4), np.float32)
    for k in range(3): img[..., k] = rgb[k] * (0.4 + 0.6 * v)
    return img


class Ctx:
    def __init__(self, name):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        self.name = name; self.dir = f"{ROOT}/gen_{name}"; os.makedirs(self.dir + "/Textures", exist_ok=True); self.mats = {}; self.objs = []

    def mat(self, key, arr=None, emit=0.0, alpha=False):
        if key in self.mats: return self.mats[key]
        p = f"{self.dir}/Textures/{self.name}_{key}.png"; save_png(arr, p)
        m = bpy.data.materials.new(f"{self.name}_{key}"); m.use_nodes = True; nt = m.node_tree
        bs = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED"); tn = nt.nodes.new("ShaderNodeTexImage")
        tn.image = bpy.data.images.load(p); nt.links.new(tn.outputs["Color"], bs.inputs["Base Color"]); bs.inputs["Roughness"].default_value = 0.85
        if emit:
            for k in ("Emission Color", "Emission"):
                if k in bs.inputs: nt.links.new(tn.outputs["Color"], bs.inputs[k]); break
            if "Emission Strength" in bs.inputs: bs.inputs["Emission Strength"].default_value = emit
        if alpha:
            nt.links.new(tn.outputs["Alpha"], bs.inputs["Alpha"])
            try: m.surface_render_method = "BLENDED"
            except Exception: pass
        m.use_backface_culling = False; self.mats[key] = m; return m

    def prim(self, kind, loc, size, m, verts=8, rot=(0, 0, 0), uvs=1.0):
        """loc·size는 워크3 단위(위치는 바닥 중심 기준 X·Y·Z). 크기는 박스 X·Y·Z / 원기둥·원뿔 지름X·지름Y·높이."""
        x, y, z = loc; sx, sy, sz = size
        if kind == "box": bpy.ops.mesh.primitive_cube_add(size=1, location=(x * S, y * S, z * S))
        elif kind == "cone": bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=0.5, radius2=0, depth=1, location=(x * S, y * S, z * S))
        elif kind == "cyl": bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=0.5, depth=1, location=(x * S, y * S, z * S))
        elif kind == "sphere": bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=10, radius=0.5, location=(x * S, y * S, z * S))
        elif kind == "torus": bpy.ops.mesh.primitive_torus_add(major_radius=0.4, minor_radius=0.1, major_segments=24, minor_segments=8, location=(x * S, y * S, z * S))
        o = bpy.context.active_object; o.scale = (sx * S, sy * S, sz * S); o.rotation_euler = rot
        bpy.ops.object.transform_apply(scale=True, rotation=True)
        o.data.materials.append(m)
        bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.uv.cube_project(cube_size=1.0 / uvs); bpy.ops.object.mode_set(mode="OBJECT")
        bpy.ops.object.shade_flat(); self.objs.append(o); return o

    def export(self, note="", approx=True):
        for o in self.objs: o.select_set(True)
        bpy.context.view_layer.objects.active = self.objs[0]
        bpy.ops.export_scene.fbx(filepath=f"{self.dir}/{self.name}.fbx", use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                                 apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", path_mode="STRIP",
                                 embed_textures=False, bake_anim=False)
        pts = [o.matrix_world @ __import__("mathutils").Vector(c) for o in self.objs for c in o.bound_box]
        ext = [round((max(p[i] for p in pts) - min(p[i] for p in pts)) / S) for i in range(3)]
        json.dump(dict(model=f"{self.name}(워크3 기본 모델 베껴 만듦)", scaleNote="워크3 1 = 0.01m", sizeWc3=ext, parts=len(self.objs),
                       textures=sorted(os.listdir(self.dir + "/Textures")), note=note, approx=approx), open(f"{self.dir}/{self.name}.json", "w"), ensure_ascii=False, indent=1)
        return ext

    def preview(self, w=420, h=380, az=35):
        sc = bpy.context.scene
        for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
            try: sc.render.engine = eng; break
            except TypeError: pass
        sc.render.resolution_x, sc.render.resolution_y = w, h; sc.view_settings.view_transform = "Standard"
        wd = bpy.data.worlds.new("w"); wd.use_nodes = True; wd.node_tree.nodes["Background"].inputs["Color"].default_value = (0.5, 0.6, 0.75, 1); sc.world = wd
        V = __import__("mathutils").Vector
        pts = [o.matrix_world @ V(c) for o in self.objs for c in o.bound_box]
        lo = V((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))); hi = V((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
        c = (lo + hi) / 2; r = max((hi - lo).length, 1e-3)
        cd = bpy.data.cameras.new("cam"); cd.lens = 40; cam = bpy.data.objects.new("cam", cd); sc.collection.objects.link(cam); sc.camera = cam
        a = math.radians(az); d = r * 1.9
        cam.location = c + V((d * math.sin(a) * 0.8, -d * math.cos(a) * 0.8, d * 0.55)); cam.rotation_euler = (c - cam.location).to_track_quat("-Z", "Y").to_euler()
        for e, rx, rz in ((4, 50, 30), (1.2, 70, -120)):
            ld = bpy.data.lights.new("s", "SUN"); ld.energy = e; so = bpy.data.objects.new("s", ld); so.rotation_euler = (math.radians(rx), 0, math.radians(rz)); sc.collection.objects.link(so)
        sc.render.filepath = f"{self.dir}/preview.png"; bpy.ops.render.render(write_still=True)


# ---------- 모델 ----------
def m_CityLowWall0(c):
    st = c.mat("stone", tex_stone()); c.prim("box", (0, 0, 28), (128, 32, 56), st, uvs=1.5); c.prim("box", (0, 0, 60), (136, 40, 8), st, uvs=1.5)
    c.note = "도시 낮은 벽 X방향 한 칸(128×32×64)"


def m_CityLowWall90(c):
    st = c.mat("stone", tex_stone()); c.prim("box", (0, 0, 28), (32, 128, 56), st, uvs=1.5); c.prim("box", (0, 0, 60), (40, 136, 8), st, uvs=1.5)
    c.note = "도시 낮은 벽 Y방향 한 칸(32×128×64)"


def m_TallEndCap(c):
    st = c.mat("stone", tex_stone()); c.prim("box", (0, 0, 8), (56, 56, 16), st); c.prim("box", (0, 0, 64), (40, 40, 100), st, uvs=1.5)
    c.prim("box", (0, 0, 120), (52, 52, 12), st); c.prim("cone", (0, 0, 140), (44, 44, 30), c.mat("roof", tex_stone(base=(0.35, 0.3, 0.5))), verts=4)
    c.note = "낮은 벽 끝의 키 큰 기둥"


def m_Waterfall(c):
    w = c.mat("water", tex_water(), alpha=True); st = c.mat("stone", tex_stone(base=(0.45, 0.45, 0.42)))
    c.prim("box", (0, 0, 100), (160, 12, 200), w, uvs=1.0); c.prim("box", (-86, 4, 100), (16, 36, 200), st); c.prim("box", (86, 4, 100), (16, 36, 200), st)
    c.prim("cyl", (0, -20, 8), (150, 40, 16), c.mat("foam", tex_glow(rgb=(0.9, 0.97, 1.0)), alpha=False), verts=16)
    c.note = "절벽 폭포(세로 물판 + 바위 양옆 + 아래 물보라)"


def m_PotofGold(c):
    pot = c.mat("pot", flat((0.12, 0.1, 0.09), jit=0.3)); gold = c.mat("gold", flat((1.0, 0.78, 0.2), jit=0.4), emit=0.4)
    c.prim("sphere", (0, 0, 42), (110, 110, 80), pot); c.prim("cyl", (0, 0, 78), (86, 86, 10), pot, verts=16); c.prim("sphere", (0, 0, 84), (84, 84, 36), gold)
    for i in range(8):
        a = i * 0.8; c.prim("cyl", (math.cos(a) * 40, math.sin(a) * 40, 90 + (i % 3) * 5), (22, 22, 5), gold, verts=10)
    c.note = "금화 항아리(도박소 모델 h062)"


def m_Blacksmith(c):
    st = c.mat("stone", tex_stone()); rf = c.mat("roof", tex_wood(base=(0.4, 0.2, 0.15))); mt = c.mat("metal", tex_metal(base=(0.3, 0.3, 0.33)))
    c.prim("box", (0, 0, 40), (180, 150, 80), st, uvs=2); c.prim("cone", (0, 0, 110), (230, 200, 70), rf, verts=4, rot=(0, 0, math.pi / 4))
    c.prim("box", (60, 30, 130), (30, 30, 80), st); c.prim("box", (-70, -95, 24), (50, 24, 24), mt); c.prim("box", (-70, -95, 40), (30, 14, 12), mt)
    c.prim("box", (0, -78, 30), (36, 6, 56), c.mat("door", tex_wood(base=(0.3, 0.2, 0.12))))
    c.note = "대장간(강화소 h06Q) — 돌 몸체·붉은 지붕·굴뚝·모루"


def m_PowerGenerator(c):
    mt = c.mat("metal", tex_metal()); gl = c.mat("glow", tex_glow(rgb=(0.3, 0.9, 1.0)), emit=2.5)
    c.prim("box", (0, 0, 22), (150, 150, 44), mt, uvs=2); c.prim("cyl", (-38, 0, 66), (60, 60, 56), mt, verts=12); c.prim("cyl", (38, 0, 66), (60, 60, 56), mt, verts=12)
    c.prim("sphere", (-38, 0, 106), (44, 44, 44), gl); c.prim("sphere", (38, 0, 106), (44, 44, 44), gl); c.prim("box", (0, -52, 32), (70, 30, 40), c.mat("panel", tex_metal(base=(0.25, 0.3, 0.4))))
    c.note = "발전기(강화소 h06R) — 금속 상자·원통 둘·빛나는 구"


def m_MagicVault(c):
    st = c.mat("stone", tex_stone(base=(0.55, 0.5, 0.6))); dm = c.mat("dome", tex_metal(base=(0.45, 0.4, 0.7))); gl = c.mat("gold", flat((1.0, 0.78, 0.2), jit=0.3), emit=0.3)
    c.prim("cyl", (0, 0, 40), (190, 190, 80), st, verts=16, uvs=2); c.prim("sphere", (0, 0, 86), (190, 190, 100), dm); c.prim("cyl", (0, 0, 80), (200, 200, 8), gl, verts=16)
    c.prim("box", (0, -92, 34), (50, 12, 60), c.mat("door", tex_wood(base=(0.3, 0.2, 0.12))))
    c.note = "마법 금고(항해일지 H0C4) — 돌 원통·보랏빛 돔"


def m_Seaweed(c):
    g = c.mat("leaf", flat((0.2, 0.55, 0.3), jit=0.4))
    for i, (x, y, hh) in enumerate(((0, 0, 90), (14, 6, 70), (-12, 8, 60), (6, -12, 80), (-8, -10, 50))):
        c.prim("cone", (x, y, hh / 2), (10, 10, hh), g, verts=5)
    c.note = "해초"


def m_AshenShrooms(c):
    cap = c.mat("cap", flat((0.7, 0.3, 0.25), jit=0.4)); stm = c.mat("stem", flat((0.85, 0.8, 0.7), jit=0.2))
    for x, y, hh, r in ((0, 0, 40, 36), (30, 10, 24, 22), (-24, 14, 30, 26)):
        c.prim("cyl", (x, y, hh / 2), (r * 0.3, r * 0.3, hh), stm, verts=8); c.prim("sphere", (x, y, hh), (r, r, r * 0.6), cap)
    c.note = "버섯 무리"


def m_CatTail(c):
    st = c.mat("stalk", flat((0.35, 0.5, 0.2), jit=0.3)); hd = c.mat("head", flat((0.35, 0.2, 0.1), jit=0.3))
    for x, y, hh in ((0, 0, 100), (12, 8, 80), (-10, 6, 90)):
        c.prim("cyl", (x, y, hh / 2), (3, 3, hh), st, verts=5); c.prim("cyl", (x, y, hh - 10), (7, 7, 22), hd, verts=6)
    c.note = "부들"


def m_LilyPad(c):
    g = c.mat("pad", flat((0.25, 0.6, 0.25), jit=0.3)); c.prim("cyl", (0, 0, 2), (80, 80, 4), g, verts=20); c.prim("sphere", (14, 10, 6), (14, 14, 10), c.mat("bud", flat((0.95, 0.7, 0.8), jit=0.2)))
    c.note = "수련 잎"


def m_AshenRock(c):
    r = c.mat("rock", tex_stone(base=(0.5, 0.5, 0.48)))
    c.prim("sphere", (0, 0, 28), (100, 80, 60), r); c.prim("sphere", (34, 16, 20), (50, 44, 40), r); c.prim("sphere", (-30, -14, 14), (40, 36, 28), r)
    c.note = "바위"


def m_Rowboat(c):
    w = c.mat("wood", tex_wood()); c.prim("box", (0, 0, 14), (150, 52, 14), w); c.prim("box", (0, 26, 28), (150, 4, 22), w); c.prim("box", (0, -26, 28), (150, 4, 22), w)
    c.prim("cone", (80, 0, 18), (52, 40, 20), w, verts=4, rot=(0, math.pi / 2, 0)); c.prim("box", (-30, 0, 24), (6, 46, 4), w); c.prim("box", (20, 0, 24), (6, 46, 4), w)
    c.note = "노 젓는 배"


def m_RuinsFlower(c):
    c.prim("cyl", (0, 0, 28), (4, 4, 56), c.mat("stem", flat((0.25, 0.55, 0.25))), verts=5)
    for i in range(6):
        a = i * math.pi / 3; c.prim("sphere", (math.cos(a) * 14, math.sin(a) * 14, 58), (16, 10, 6), c.mat("petal", flat((0.9, 0.5, 0.75), jit=0.3)))
    c.prim("sphere", (0, 0, 58), (10, 10, 8), c.mat("core", flat((1.0, 0.85, 0.2))))
    c.note = "유적 꽃"


def m_Battleship(c):
    w = c.mat("wood", tex_wood(base=(0.4, 0.28, 0.18))); mt = c.mat("metal", tex_metal(base=(0.4, 0.42, 0.45))); sl = c.mat("sail", flat((0.9, 0.88, 0.8), jit=0.1))
    c.prim("box", (0, 0, 40), (520, 130, 70), w, uvs=1); c.prim("cone", (300, 0, 40), (130, 130, 70), w, verts=4, rot=(0, math.pi / 2, math.pi / 4))
    c.prim("box", (-170, 0, 100), (130, 110, 60), w); c.prim("cyl", (40, 0, 170), (10, 10, 260), w, verts=6); c.prim("box", (40, 0, 190), (8, 130, 120), sl)
    for i in range(5): c.prim("cyl", (-120 + i * 70, 66, 76), (18, 18, 40), mt, verts=8, rot=(math.pi / 2, 0, 0))
    c.note = "전함(북쪽 바다)"


def m_BubbleGeyser(c):
    st = c.mat("stone", tex_stone(base=(0.45, 0.5, 0.5))); gl = c.mat("bubble", tex_glow(rgb=(0.7, 0.9, 1.0)), emit=0.8)
    c.prim("torus", (0, 0, 14), (160, 160, 50), st, verts=8)
    for x, y, z, r in ((0, 0, 50, 24), (10, 6, 80, 18), (-8, -4, 108, 14), (6, -8, 130, 10)): c.prim("sphere", (x, y, z), (r, r, r), gl)
    c.note = "물거품 분출구"


def m_CircleOfPower(c):
    gl = c.mat("ring", tex_glow(rgb=(0.9, 0.9, 1.0)), emit=1.5); c.prim("torus", (0, 0, 3), (180, 180, 14), gl); c.prim("cyl", (0, 0, 1), (150, 150, 2), c.mat("floor", flat((0.4, 0.45, 0.8), jit=0.2), emit=0.3), verts=24)
    c.note = "배치 더미 마법진(색만 다른 4종 — 분홍·빨강·노랑·파랑은 Unity에서 틴트)"


ALL = ["CityLowWall0", "CityLowWall90", "TallEndCap", "Waterfall", "PotofGold", "Blacksmith", "PowerGenerator", "MagicVault", "Seaweed", "AshenShrooms",
       "CatTail", "LilyPad", "AshenRock", "Rowboat", "RuinsFlower", "Battleship", "BubbleGeyser", "CircleOfPower"]

if __name__ == "__main__":
    a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    names = [x for x in a if x in ALL] or ALL
    for nm in names:
        c = Ctx(nm); c.note = ""; globals()["m_" + nm](c); ext = c.export(c.note); c.preview(); print("OK", nm, ext)
