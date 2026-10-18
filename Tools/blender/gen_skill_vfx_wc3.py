"""워크3 기본 이펙트 모델(맵 밖이라 꺼낼 수 없는 것)을 원작 모습대로 베껴 만든다 — 재매칭 ③ 제작 묶음(2026-10-18, PM 지시).

  blender -b --factory-startup --python Tools/blender/gen_skill_vfx_wc3.py -- [이름 …] [--out <루트>] [--sheet]
  산출: <루트>/<이름>/<이름>.fbx · Textures/*.png · <이름>.json  (루트 기본 ~/grd/GRD_orig_vfx_trial — OriginalVfxBuilder 반입 원본 자리)
        json = export_mdx_anim_fbx 형식 + "unity"(OriginalVfxBuilder) + "pre2Unity"(Pre2Builder 입자, Tools/w3x/pre2_unity.py와 같은 원소 구조)
  미리보기: ~/grd/GRD_skill_vfx/3_make/sheet_3_make.png (산출물엔 안 넣음 — PM 10-18)

방식: 메시(가산 판·기둥·구)는 오브젝트 애니(FBX 테이크 = 클립 이름), 층 알파는 unity.layers[].alphaCurveGlobalSec,
      흩날리는 것(불티·연기·반짝이·꼬리)은 원작처럼 PRE2 입자로 json pre2Unity에 쓴다 → 유니티가 진짜 ParticleSystem으로 세운다.
      텍스처는 원작 기본 텍스처(GenericGlow·Flare·Lightning·Smoke 등) 모양을 numpy로 근사해 그림(approxTexture=true).
단위: 메시는 미터(워크3 1 = 0.01m), pre2Unity 값은 워크3 단위(Pre2Builder가 ×0.01). 루트 −90°Z·axis_forward=-Z·axis_up=Y = export_mdx_anim_fbx와 같은 방향.
투사체(…Missile)는 +X(워크3 앞)를 향하고 클립이 루프, 꼬리 입자는 월드 공간이라 날아가면 뒤에 남는다.
DNCLordaeronTerrain/Unit은 이펙트가 아니라 워크3 낮밤 조명 모델(SetDayNightModels)이라 만들지 않는다 — 우리 조명으로 대체.
"""
import json
import math
import os
import random
import sys

import bpy
import numpy as np

H = os.path.expanduser("~")
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.expanduser(ARGS[ARGS.index("--out") + 1]) if "--out" in ARGS else H + "/grd/GRD_orig_vfx_trial"
PREV = H + "/grd/GRD_skill_vfx/3_make"
FPS = 30


# ============================== 텍스처 ==============================
def _g(n):
    y, x = np.mgrid[0:n, 0:n].astype(np.float32)
    return (x + 0.5) / n * 2 - 1, (y + 0.5) / n * 2 - 1


def _noise(n, scale, seed, octaves=4):
    rng = np.random.default_rng(seed)
    out = np.zeros((n, n), np.float32)
    amp, tot = 1.0, 0.0
    for o in range(octaves):
        k = max(2, int(scale * 2 ** o))
        g = rng.random((k + 1, k + 1)).astype(np.float32)
        yi = np.linspace(0, k, n, endpoint=False)
        y0 = yi.astype(int)
        fy = (yi - y0)[:, None]
        fx = (yi - y0)[None, :]
        fy = fy * fy * (3 - 2 * fy)
        fx = fx * fx * (3 - 2 * fx)
        a = g[y0][:, y0]
        b = g[y0][:, y0 + 1]
        c = g[y0 + 1][:, y0]
        d = g[y0 + 1][:, y0 + 1]
        out += amp * (a * (1 - fx) * (1 - fy) + b * fx * (1 - fy) + c * (1 - fx) * fy + d * fx * fy)
        tot += amp
        amp *= 0.5
    return out / tot


def rgba(v, col=(1, 1, 1), a=None):
    v = np.clip(v, 0, 1)
    c = np.dstack([v * col[0], v * col[1], v * col[2]])
    al = np.ones_like(v) if a is None else np.clip(a, 0, 1)
    return np.dstack([np.clip(c, 0, 1), al])


TEX = {}


def T_glow(n=128):
    x, y = _g(n)
    r = np.hypot(x, y)
    return rgba(np.exp(-(r / 0.38) ** 2) + 0.25 * np.exp(-(r / 0.8) ** 2) * (r < 1))


def T_flare(n=128):           # 4갈래 별빛(Flare·Star4)
    x, y = _g(n)
    r = np.hypot(x, y) + 1e-6
    a = np.arctan2(y, x)
    return rgba(np.exp(-(r / 0.12) ** 2) + np.abs(np.cos(2 * a)) ** 80 * np.clip(1 - r, 0, 1) ** 2 + 0.3 * np.exp(-(r / 0.4) ** 2))


def T_ring(n=256, w=0.06, rr=0.8):
    x, y = _g(n)
    r = np.hypot(x, y)
    return rgba(np.exp(-((r - rr) / w) ** 2) + 0.25 * np.clip((r - 0.3) / (rr - 0.3), 0, 1) ** 3 * (r < rr))


def T_smoke(n=128, seed=1):
    x, y = _g(n)
    r = np.hypot(x, y)
    nz = _noise(n, 3, seed)
    m = np.clip((1 - r) * 1.4 + (nz - 0.5) * 1.2, 0, 1) ** 1.5
    v = 0.55 + 0.45 * nz
    return np.dstack([v, v, v, m])        # 알파 혼합(blend)용 연기


def T_flame(n=128, seed=2):
    x, y = _g(n)
    r = np.hypot(x, y)
    nz = _noise(n, 4, seed)
    d = np.clip((1 - r) * 1.3 + (nz - 0.5) * 1.1, 0, 1)
    col = np.dstack([np.clip(d * 2.0, 0, 1), np.clip(d * 1.4 - 0.25, 0, 1), np.clip(d * 1.6 - 0.9, 0, 1)])
    return np.dstack([col, np.ones_like(d)])


def T_lightning(n=256, seed=3):   # 세로 번개 줄기(위→아래), 가산
    rng = np.random.default_rng(seed)
    img = np.zeros((n, n), np.float32)
    xs = np.cumsum(rng.normal(0, 0.03, n))
    xs = xs - np.linspace(xs[0], xs[-1], n)
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32) / n * 2 - 1
    cx = xs[:, None]
    img += np.exp(-((xx - cx) / 0.02) ** 2) + 0.4 * np.exp(-((xx - cx) / 0.09) ** 2)
    for b in range(3):                     # 곁가지
        y0 = rng.integers(n // 5, n * 3 // 4)
        dx = np.cumsum(rng.normal(0.006 * (1 if b % 2 else -1), 0.02, n - y0))
        cxb = xs[y0] + dx
        seg = np.zeros((n, n), np.float32)
        seg[y0:] = np.exp(-((xx[y0:] - cxb[:, None]) / 0.012) ** 2) * np.linspace(1, 0, n - y0)[:, None]
        img += seg * 0.7
    return rgba(img, (0.75, 0.85, 1.0))


def T_rune(n=256):           # 마법진(MassTeleport·Levelup 바닥)
    x, y = _g(n)
    r = np.hypot(x, y)
    a = np.arctan2(y, x)
    v = (np.exp(-((r - 0.9) / 0.02) ** 2) + np.exp(-((r - 0.72) / 0.015) ** 2)
         + (np.abs(np.sin(6 * a)) > 0.97) * ((r > 0.72) & (r < 0.9)) * 0.8
         + np.exp(-((np.abs(np.sin(3 * a + 0.5)) * r - 0.0) / 0.02) ** 2) * (r < 0.72) * 0.6
         + 0.25 * np.clip(1 - r, 0, 1))
    return rgba(v)


def T_note(n=128):           # 음표(우타 오라)
    img = np.zeros((n, n), np.float32)
    x, y = _g(n)
    head = np.exp(-(((x + 0.15) / 0.28) ** 2 + ((y - 0.45) / 0.2) ** 2) * 3)
    stem = ((np.abs(x - 0.1) < 0.05) & (y > -0.65) & (y < 0.45)).astype(np.float32)
    flag = np.exp(-(((x - 0.3) / 0.18) ** 2 + ((y + 0.5 - 0.3 * (x - 0.1)) / 0.08) ** 2) * 2) * (x > 0.1)
    img = np.clip(head * 2 + stem + flag * 1.5, 0, 1)
    return rgba(img)


def T_streak(n=128):         # 가로로 긴 빛줄기(투사체 몸통·화살)
    x, y = _g(n)
    return rgba(np.exp(-(y / 0.12) ** 2) * np.clip(1 - np.abs(x) ** 3, 0, 1) + 0.5 * np.exp(-(np.hypot(x - 0.5, y) / 0.25) ** 2))


def T_rock(n=128, seed=4):   # 돌덩이(투석 미사일·포탄) 불투명
    nz = _noise(n, 6, seed)
    v = 0.25 + 0.5 * nz
    return np.dstack([v * 0.9, v * 0.8, v * 0.7, np.ones_like(v)])


def T_metal(n=64):
    nz = _noise(n, 3, 9)
    v = 0.35 + 0.3 * nz
    return np.dstack([v, v, v * 1.05, np.ones_like(v)])


def T_swirl(n=256, seed=5):  # 회오리(텔레포트·블링크 기둥)
    x, y = _g(n)
    u = (x + 1) / 2
    v = (y + 1) / 2
    s = 0.5 + 0.5 * np.sin((u * 6 + v * 3) * math.pi * 2)
    nz = _noise(n, 4, seed)
    edge = np.sin(v * math.pi)
    return rgba(np.clip(s * 0.8 + nz * 0.4 - 0.2, 0, 1) * edge)


DRAW = dict(glow=T_glow, flare=T_flare, ring=T_ring, smoke=T_smoke, flame=T_flame, lightning=T_lightning, rune=T_rune,
            note=T_note, streak=T_streak, rock=T_rock, metal=T_metal, swirl=T_swirl)


def save_png(arr, path):
    h, w = arr.shape[:2]
    im = bpy.data.images.new(os.path.basename(path), w, h, alpha=True)
    im.pixels.foreach_set(np.ascontiguousarray(arr[::-1]).astype(np.float32).ravel())
    im.filepath_raw = path
    im.file_format = "PNG"
    im.save()
    bpy.data.images.remove(im)


def tex(mdir, key, tint=(1, 1, 1)):
    """Textures/wc3_<key>[_<색>].png 를 그려 두고 파일 이름을 돌려준다. 메시 층 색은 텍스처에 구워 넣는다
    (OriginalVfxBuilder는 층 색을 안 읽는다 — 입자 색은 pre2Unity colorsRGB01로 따로 간다)."""
    tag = "" if tuple(tint) == (1, 1, 1) else "_" + "".join("%02x" % int(round(c * 255)) for c in tint)
    fn = f"wc3_{key}{tag}.png"
    p = os.path.join(mdir, "Textures", fn)
    if not os.path.exists(p):
        os.makedirs(os.path.dirname(p), exist_ok=True)
        arr = DRAW[key]().copy()
        arr[..., :3] *= np.array(tint, np.float32)
        save_png(arr, p)
    return fn


# ============================== 메시 ==============================
def _obj(name, verts, faces, uvs):
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    uvl = me.uv_layers.new(name="UVMap")
    li = 0
    for f, fu in zip(me.polygons, uvs):
        for k, l in enumerate(f.loop_indices):
            uvl.data[l].uv = fu[k]
    me.update()
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    return o


def m_ground(name, size):
    s = size / 2
    return _obj(name, [(-s, -s, 0.02), (s, -s, 0.02), (s, s, 0.02), (-s, s, 0.02)], [(0, 1, 2, 3)], [[(0, 0), (1, 0), (1, 1), (0, 1)]])


def m_cross(name, w, h, n=3, z0=0.0):
    """세로 판 n장을 엇갈려 세움(어느 각도에서도 보이는 빛·불꽃)."""
    vs, fs, us = [], [], []
    for i in range(n):
        a = math.pi * i / n
        c, s = math.cos(a) * w / 2, math.sin(a) * w / 2
        b = len(vs)
        vs += [(-c, -s, z0), (c, s, z0), (c, s, z0 + h), (-c, -s, z0 + h)]
        fs.append((b, b + 1, b + 2, b + 3))
        us.append([(0, 0), (1, 0), (1, 1), (0, 1)])
    return _obj(name, vs, fs, us)


def m_cross_center(name, w, h, n=3):
    """가운데 기준 엇갈린 판(투사체·구름)."""
    o = m_cross(name, w, h, n, -h / 2)
    return o


def m_xplanes(name, length, width):
    """+X 방향으로 긴 십자 판 2장(화살·빛줄기 투사체)."""
    L, W = length / 2, width / 2
    vs = [(-L, -W, 0), (L, -W, 0), (L, W, 0), (-L, W, 0), (-L, 0, -W), (L, 0, -W), (L, 0, W), (-L, 0, W)]
    return _obj(name, vs, [(0, 1, 2, 3), (4, 5, 6, 7)], [[(0, 0), (1, 0), (1, 1), (0, 1)]] * 2)


def m_column(name, r, h, seg=24, r_top=None):
    r_top = r if r_top is None else r_top
    vs, fs, us = [], [], []
    for i in range(seg + 1):
        a = 2 * math.pi * i / seg
        vs += [(math.cos(a) * r, math.sin(a) * r, 0), (math.cos(a) * r_top, math.sin(a) * r_top, h)]
    for i in range(seg):
        b = i * 2
        fs.append((b, b + 2, b + 3, b + 1))
        us.append([(i / seg, 0), ((i + 1) / seg, 0), ((i + 1) / seg, 1), (i / seg, 1)])
    return _obj(name, vs, fs, us)


def m_sphere(name, r, seg=20, ring=12, stretch=(1, 1, 1)):
    vs, fs, us = [], [], []
    for j in range(ring + 1):
        th = math.pi * j / ring
        for i in range(seg + 1):
            ph = 2 * math.pi * i / seg
            vs.append((r * stretch[0] * math.sin(th) * math.cos(ph), r * stretch[1] * math.sin(th) * math.sin(ph), r * stretch[2] * math.cos(th)))
    for j in range(ring):
        for i in range(seg):
            a = j * (seg + 1) + i
            fs.append((a, a + seg + 1, a + seg + 2, a + 1))
            us.append([(i / seg, 1 - j / ring), (i / seg, 1 - (j + 1) / ring), ((i + 1) / seg, 1 - (j + 1) / ring), ((i + 1) / seg, 1 - j / ring)])
    o = _obj(name, vs, fs, us)
    for p in o.data.polygons:
        p.use_smooth = True
    return o


def m_rocket(name, length, r):
    """+X로 향한 로켓: 원기둥 몸통 + 원뿔 머리."""
    seg = 12
    vs, fs, us = [], [], []
    L = length
    for i in range(seg + 1):
        a = 2 * math.pi * i / seg
        y, z = math.cos(a) * r, math.sin(a) * r
        vs += [(-L / 2, y, z), (L * 0.25, y, z)]
    for i in range(seg):
        b = i * 2
        fs.append((b, b + 2, b + 3, b + 1))
        us.append([(i / seg, 0), ((i + 1) / seg, 0), ((i + 1) / seg, 0.7), (i / seg, 0.7)])
    tip = len(vs)
    vs.append((L / 2, 0, 0))
    for i in range(seg):
        b = i * 2 + 1
        fs.append((b, b + 2, tip))
        us.append([(i / seg, 0.7), ((i + 1) / seg, 0.7), ((i + 0.5) / seg, 1)])
    o = _obj(name, vs, fs, us)
    for p in o.data.polygons:
        p.use_smooth = True
    return o


# ============================== 재질 ==============================
def mat(name, png, blend="additive", tint=(1, 1, 1), strength=1.0):
    """미리보기(EEVEE)용 재질. FBX엔 이미지 텍스처 이름이 실리고, 유니티 재질은 unity.layers[].blend로 OriginalVfxBuilder가 정한다.
    오브젝트 color.a = 층 알파 곡선(미리보기)."""
    suf = {"additive": "add", "blend": "blend", "none": "cut"}[blend]
    m = bpy.data.materials.new(f"{name}_{suf}")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = bpy.data.images.load(png, check_existing=True)
    oi = nt.nodes.new("ShaderNodeObjectInfo")
    mul = nt.nodes.new("ShaderNodeVectorMath")
    mul.operation = "MULTIPLY"
    nt.links.new(tn.outputs["Color"], mul.inputs[0])
    mul.inputs[1].default_value = tint
    if blend == "none":
        b = nt.nodes.new("ShaderNodeBsdfPrincipled")
        nt.links.new(mul.outputs["Vector"], b.inputs["Base Color"])
        b.inputs["Roughness"].default_value = 0.6
        nt.links.new(b.outputs[0], out.inputs["Surface"])
    else:
        em = nt.nodes.new("ShaderNodeEmission")
        nt.links.new(mul.outputs["Vector"], em.inputs["Color"])
        st = nt.nodes.new("ShaderNodeMath")
        st.operation = "MULTIPLY"
        nt.links.new(oi.outputs["Alpha"], st.inputs[0])
        st.inputs[1].default_value = strength
        if blend == "blend":
            st2 = nt.nodes.new("ShaderNodeMath")
            st2.operation = "MULTIPLY"
            nt.links.new(st.outputs[0], st2.inputs[0])
            nt.links.new(tn.outputs["Alpha"], st2.inputs[1])
            fac = st2.outputs[0]
            tr = nt.nodes.new("ShaderNodeBsdfTransparent")
            mix = nt.nodes.new("ShaderNodeMixShader")
            nt.links.new(fac, mix.inputs[0])
            nt.links.new(tr.outputs[0], mix.inputs[1])
            nt.links.new(em.outputs[0], mix.inputs[2])
            nt.links.new(mix.outputs[0], out.inputs["Surface"])
        else:
            nt.links.new(st.outputs[0], em.inputs["Strength"])
            tr = nt.nodes.new("ShaderNodeBsdfTransparent")
            ad = nt.nodes.new("ShaderNodeAddShader")
            nt.links.new(tr.outputs[0], ad.inputs[0])
            nt.links.new(em.outputs[0], ad.inputs[1])
            nt.links.new(ad.outputs[0], out.inputs["Surface"])
        try:
            m.surface_render_method = "BLENDED"
        except Exception:
            pass
    m.use_backface_culling = False
    return m


# ============================== 입자(pre2Unity) ==============================
def pre2(node, texfile, *, speed=0.0, variation=0.0, latitude=0.0, gravity=0.0, life=1.0, rate=10.0, width=0.0, length=0.0,
         filt="additive", colors=((1, 1, 1),) * 3, alpha=(255, 255, 0), scale=(20, 20, 20), mid=0.5, pivot=(0, 0, 0),
         rows=1, cols=1, tail=False, tail_len=0.0, squirt=False, rate_keys=None, vis_keys=None):
    """Tools/w3x/pre2_unity.pre2_entries 원소와 같은 구조(값은 워크3 단위·ms)."""
    tracks = {}
    if rate_keys:
        tracks["KP2E"] = dict(interp=0, keysMsAbs=[[int(t), [float(v)]] for t, v in rate_keys])
    if vis_keys:
        tracks["KP2V"] = dict(interp=0, keysMsAbs=[[int(t), [float(v)]] for t, v in vis_keys])
    return dict(textureFile=texfile, textureSourcePath="(blender 근사) " + texfile, sequenceStartMs=0, node=node, nodeFlags=0, parentId=None,
                pivot=list(pivot), speed=speed, variation=variation, latitude=latitude, gravity=gravity, lifespanSec=life,
                emissionRate=rate, width=width, length=length, filterMode=filt, rows=rows, cols=cols,
                headOrTail="tail" if tail else "head", tailLength=tail_len, midTime=mid, colorsRGB01=[list(c) for c in colors],
                alpha255=list(alpha), scale3=list(scale), squirt=squirt, textureIndex=0, tracks=tracks)


# ============================== 애니 도우미 ==============================
def key(o, t, loc=None, rot=None, scale=None, alpha=None):
    f = 1 + round(t * FPS)
    if loc is not None:
        o.location = loc
        o.keyframe_insert("location", frame=f)
    if rot is not None:
        o.rotation_euler = [math.radians(a) for a in rot]
        o.keyframe_insert("rotation_euler", frame=f)
    if scale is not None:
        o.scale = (scale,) * 3 if isinstance(scale, (int, float)) else scale
        o.keyframe_insert("scale", frame=f)
    if alpha is not None:
        o.color = (1, 1, 1, alpha)
        o.keyframe_insert("color", frame=f)


def linear_all():
    for o in bpy.data.objects:
        ad = o.animation_data
        if not (ad and ad.action):
            continue
        try:
            for layer in ad.action.layers:
                for strip in layer.strips:
                    for cb in strip.channelbags:
                        for fc in cb.fcurves:
                            for kp in fc.keyframe_points:
                                kp.interpolation = "LINEAR"
        except Exception:
            pass


# ============================== 효과 정의 ==============================
class FX:
    def __init__(self, name, dur, loop, clip):
        self.name, self.dur, self.loop, self.clip = name, dur, loop, clip
        self.mdir = os.path.join(OUT, name)
        self.layers = []       # (obj, blend, texfile, alpha_curve[[sec,a]])
        self.parts = []
        self.desc = ""

    def add(self, o, key_, blend="additive", tint=(1, 1, 1), strength=1.0, alpha_curve=None):
        fn = tex(self.mdir, key_, tint)
        o.data.materials.append(mat(o.name, os.path.join(self.mdir, "Textures", fn), blend, (1, 1, 1), strength))
        o.color = (1, 1, 1, 1)
        if alpha_curve:
            for t, a in alpha_curve:
                key(o, t, alpha=a)
        self.layers.append((o, blend, fn, alpha_curve, tint))
        return o

    def p(self, node, key_, **kw):
        fn = tex(self.mdir, key_)
        self.parts.append(pre2(node, fn, **kw))


def ring_burst(fx, nm, r0, r1, t0, t1, a=1.0, tint=(1, 1, 1), key_="ring"):
    o = m_ground(nm, 1.0)
    key(o, t0, scale=r0 * 2)
    key(o, t1, scale=r1 * 2)
    fx.add(o, key_, tint=tint, alpha_curve=[(t0, 0.0), (t0 + 0.05, a), (t0 + (t1 - t0) * 0.5, a * 0.7), (t1, 0.0)])
    return o


# --- 각 효과 (원작 모습 메모는 docstring) ---
def fx_BlinkTarget():
    """블링크 도착: 보라·청록 빛이 땅에서 확 솟고 반짝이가 흩어짐, 약 1초."""
    f = FX("BlinkTarget", 1.0, False, "Birth")
    col = (0.55, 0.45, 1.0)
    c = m_cross("blink_pillar", 1.2, 2.2)
    key(c, 0.0, scale=(0.3, 0.3, 0.2))
    key(c, 0.15, scale=(1.0, 1.0, 1.0))
    key(c, 1.0, scale=(1.3, 1.3, 1.2))
    f.add(c, "swirl", tint=col, strength=1.4, alpha_curve=[(0, 0), (0.08, 1), (0.5, 0.6), (1.0, 0)])
    ring_burst(f, "blink_ring", 0.2, 1.4, 0.0, 0.6, 1.0, (0.6, 0.85, 1.0))
    f.p("sparkle", "flare", speed=280, variation=0.5, latitude=60, gravity=150, life=0.8, rate=0, colors=((0.8, 0.9, 1), (0.6, 0.5, 1), (0.4, 0.3, 1)),
        alpha=(255, 200, 0), scale=(22, 18, 6), mid=0.4, pivot=(0, 0, 40), squirt=True, rate_keys=[(0, 60), (33, 0)])
    f.p("glow", "glow", speed=0, life=0.6, rate=0, colors=((0.7, 0.6, 1),) * 3, alpha=(200, 120, 0), scale=(80, 140, 160), pivot=(0, 0, 60),
        squirt=True, rate_keys=[(0, 3), (33, 0)])
    return f


def fx_MassTeleportTarget():
    """집단 순간이동 대상: 파란 마법진 + 하늘로 솟는 회오리 기둥, 약 1.5초."""
    f = FX("MassTeleportTarget", 1.5, False, "Birth")
    g = m_ground("mt_rune", 2.4)
    key(g, 0, scale=0.4, rot=(0, 0, 0))
    key(g, 0.3, scale=1.0)
    key(g, 1.5, scale=1.05, rot=(0, 0, 120))
    f.add(g, "rune", tint=(0.45, 0.7, 1.0), alpha_curve=[(0, 0), (0.2, 1), (1.1, 0.8), (1.5, 0)])
    col = m_column("mt_column", 0.6, 3.2, r_top=0.35)
    key(col, 0, scale=(1, 1, 0.1), rot=(0, 0, 0))
    key(col, 0.4, scale=(1, 1, 1.0))
    key(col, 1.5, scale=(0.8, 0.8, 1.1), rot=(0, 0, 360))
    f.add(col, "swirl", tint=(0.5, 0.75, 1.0), strength=1.2, alpha_curve=[(0, 0), (0.3, 1), (1.0, 0.7), (1.5, 0)])
    f.p("rise", "flare", speed=320, variation=0.3, latitude=8, life=0.9, rate=40, width=110, length=110, colors=((0.8, 0.9, 1), (0.5, 0.7, 1), (0.3, 0.4, 1)),
        alpha=(0, 255, 0), scale=(14, 20, 8), mid=0.3, rate_keys=[(0, 40), (1100, 0)])
    return f


def fx_LevelupCaster():
    """레벨 업: 금빛 기둥이 솟고 금 반짝이가 위로 흩날리며 바닥 금고리, 약 2초."""
    f = FX("LevelupCaster", 2.0, False, "Birth")
    gold = (1.0, 0.82, 0.35)
    col = m_column("lv_column", 0.55, 3.6, r_top=0.7)
    key(col, 0, scale=(1, 1, 0.05))
    key(col, 0.35, scale=(1, 1, 1))
    key(col, 2.0, scale=(1.15, 1.15, 1.1))
    f.add(col, "swirl", tint=gold, strength=1.3, alpha_curve=[(0, 0), (0.2, 1), (1.2, 0.7), (2.0, 0)])
    ring_burst(f, "lv_ring", 0.3, 1.6, 0.0, 1.0, 1.0, gold)
    g = m_ground("lv_rune", 2.0)
    key(g, 0, rot=(0, 0, 0))
    key(g, 2.0, rot=(0, 0, 90))
    f.add(g, "rune", tint=gold, alpha_curve=[(0, 0), (0.25, 0.9), (1.4, 0.6), (2.0, 0)])
    f.p("goldspark", "flare", speed=380, variation=0.5, latitude=15, gravity=60, life=1.2, rate=55, width=90, length=90,
        colors=((1, 1, 0.8), (1, 0.85, 0.3), (1, 0.6, 0.1)), alpha=(255, 220, 0), scale=(18, 16, 6), mid=0.4, rate_keys=[(0, 55), (1300, 0)])
    return f


def fx_MirrorImageCaster():
    """미러 이미지 시전: 회청색 섬광이 번쩍하며 잔상 고리가 퍼짐, 약 0.9초."""
    f = FX("MirrorImageCaster", 0.9, False, "Birth")
    c = m_cross("mi_flash", 1.6, 2.2)
    key(c, 0, scale=(0.4, 0.4, 0.6))
    key(c, 0.12, scale=(1.1, 1.1, 1.05))
    key(c, 0.9, scale=(1.6, 1.6, 1.2))
    f.add(c, "glow", tint=(0.6, 0.75, 1.0), strength=1.5, alpha_curve=[(0, 0), (0.06, 1), (0.35, 0.4), (0.9, 0)])
    ring_burst(f, "mi_ring", 0.3, 1.8, 0.0, 0.7, 0.9, (0.7, 0.8, 1.0))
    f.p("shimmer", "flare", speed=150, variation=0.6, latitude=90, life=0.7, rate=0, width=60, length=60, colors=((0.9, 0.95, 1),) * 3,
        alpha=(255, 160, 0), scale=(16, 12, 4), pivot=(0, 0, 80), squirt=True, rate_keys=[(0, 40), (33, 0)])
    return f


def fx_PolyMorphTarget():
    """변이 대상: 분홍·보라 연기가 펑 피어오르고 별 반짝이, 약 1초."""
    f = FX("PolyMorphTarget", 1.0, False, "Birth")
    c = m_cross_center("pm_puff", 1.8, 1.8)
    c.location.z = 0.8
    key(c, 0, scale=0.3)
    key(c, 0.25, scale=1.0)
    key(c, 1.0, scale=1.3)
    f.add(c, "smoke", blend="blend", tint=(1.0, 0.6, 0.95), strength=1.0, alpha_curve=[(0, 0), (0.1, 1), (0.6, 0.6), (1.0, 0)])
    f.p("puffs", "smoke", speed=120, variation=0.5, latitude=90, life=0.9, rate=0, width=60, length=60, filt="blend",
        colors=((1, 0.7, 1), (0.85, 0.5, 1), (0.6, 0.4, 0.9)), alpha=(220, 160, 0), scale=(60, 110, 140), mid=0.35, pivot=(0, 0, 70),
        squirt=True, rate_keys=[(0, 14), (33, 0)])
    f.p("stars", "flare", speed=200, variation=0.5, latitude=90, gravity=80, life=0.8, rate=0, colors=((1, 0.9, 1), (1, 0.6, 1), (0.8, 0.4, 1)),
        alpha=(255, 200, 0), scale=(16, 14, 5), pivot=(0, 0, 80), squirt=True, rate_keys=[(0, 30), (33, 0)])
    return f


def fx_MonsoonBoltTarget():
    """몬순 낙뢰: 하늘에서 푸른 번개가 꽂히고 바닥 섬광·충격 고리, 약 0.8초."""
    f = FX("MonsoonBoltTarget", 0.8, False, "Birth")
    b = m_cross("ms_bolt", 1.0, 9.0, n=2)
    key(b, 0, scale=(1, 1, 1))
    key(b, 0.8, scale=(0.6, 0.6, 1))
    f.add(b, "lightning", tint=(0.8, 0.9, 1.0), strength=2.0, alpha_curve=[(0, 1), (0.08, 0.4), (0.12, 1), (0.3, 0.7), (0.5, 0.2), (0.8, 0)])
    g = m_cross("ms_flash", 2.2, 1.2)
    f.add(g, "glow", tint=(0.7, 0.85, 1.0), strength=1.8, alpha_curve=[(0, 1), (0.25, 0.5), (0.8, 0)])
    ring_burst(f, "ms_ring", 0.3, 2.2, 0.0, 0.5, 1.0, (0.75, 0.9, 1.0))
    f.p("sparks", "flare", speed=420, variation=0.5, latitude=70, gravity=600, life=0.6, rate=0, colors=((1, 1, 1), (0.7, 0.85, 1), (0.4, 0.6, 1)),
        alpha=(255, 200, 0), scale=(12, 10, 3), pivot=(0, 0, 10), squirt=True, rate_keys=[(0, 50), (33, 0)])
    return f


def fx_BIsvTarget():
    """무적(포션) 대상: 몸을 감싸는 금빛 반투명 보호막이 은은히 일렁임(루프)."""
    f = FX("BIsvTarget", 2.0, True, "Stand")
    s = m_sphere("isv_shell", 0.85, stretch=(1, 1, 1.25))
    s.location.z = 0.9
    key(s, 0, scale=1.0, rot=(0, 0, 0))
    key(s, 1.0, scale=1.05)
    key(s, 2.0, scale=1.0, rot=(0, 0, 360))
    f.add(s, "glow", tint=(1.0, 0.85, 0.45), strength=0.9, alpha_curve=[(0, 0.45), (1.0, 0.7), (2.0, 0.45)])
    f.p("glint", "flare", speed=40, variation=0.4, latitude=90, life=0.8, rate=6, width=120, length=120, colors=((1, 1, 0.8), (1, 0.85, 0.4), (1, 0.7, 0.2)),
        alpha=(0, 255, 0), scale=(14, 18, 6), pivot=(0, 0, 90))
    return f


def fx_Dashboard_UtaExAuraEff():
    """우타 오라(S2 꾸미기 오라, 원본 없음): 분홍·흰 바닥 고리가 돌고 음표가 떠오름(루프). 원작 Dashboard 오라들처럼 바닥 고리+오르는 입자 구성."""
    f = FX("Dashboard_UtaExAuraEff", 2.0, True, "Stand")
    pink = (1.0, 0.55, 0.85)
    g = m_ground("uta_ring", 2.2)
    key(g, 0, rot=(0, 0, 0))
    key(g, 2.0, rot=(0, 0, 180))
    f.add(g, "rune", tint=pink, alpha_curve=[(0, 0.8), (1.0, 1.0), (2.0, 0.8)])
    g2 = m_ground("uta_ring2", 2.6)
    key(g2, 0, rot=(0, 0, 0))
    key(g2, 2.0, rot=(0, 0, -90))
    f.add(g2, "ring", tint=(1, 0.85, 0.95), alpha_curve=[(0, 0.5), (1.0, 0.3), (2.0, 0.5)])
    f.p("notes", "note", speed=90, variation=0.4, latitude=20, gravity=-20, life=1.8, rate=4, width=150, length=150,
        colors=((1, 0.9, 1), (1, 0.6, 0.9), (1, 0.5, 0.8)), alpha=(0, 255, 0), scale=(24, 28, 22), mid=0.3)
    f.p("sparkle", "flare", speed=60, variation=0.5, latitude=30, life=1.2, rate=8, width=160, length=160, colors=((1, 1, 1), (1, 0.7, 0.9), (1, 0.5, 0.8)),
        alpha=(0, 220, 0), scale=(10, 12, 4), mid=0.4)
    return f


def missile(name, desc, *, body, trail_parts, dur=1.0):
    f = FX(name, dur, True, "Stand")
    f.desc = desc
    body(f)
    for nm, k, kw in trail_parts:
        f.p(nm, k, **kw)
    return f


def fx_BlackArrowMissile():
    def body(f):
        a = m_xplanes("ba_arrow", 1.1, 0.22)
        f.add(a, "streak", tint=(0.35, 0.15, 0.5), strength=1.2)
        g = m_cross_center("ba_glow", 0.5, 0.5)
        g.location.x = 0.45
        f.add(g, "glow", tint=(0.5, 0.2, 0.7), strength=1.2)
    return missile("BlackArrowMissile", "검은 화살: 보랏빛 어둠 화살 + 검보라 연기 꼬리", body=body, trail_parts=[
        ("smoke", "smoke", dict(speed=10, variation=0.5, latitude=180, life=0.6, rate=45, filt="blend", colors=((0.25, 0.1, 0.35), (0.15, 0.05, 0.2), (0.05, 0, 0.08)),
                                alpha=(200, 120, 0), scale=(22, 34, 44), mid=0.3, pivot=(-40, 0, 0)))])


def fx_BloodElfSpellThiefMISSILE():
    def body(f):
        g = m_cross_center("bs_orb", 0.6, 0.6)
        f.add(g, "glow", tint=(0.55, 1.0, 0.35), strength=1.6)
        s = m_xplanes("bs_streak", 0.9, 0.3)
        f.add(s, "streak", tint=(0.4, 1.0, 0.3), strength=1.0)
    return missile("BloodElfSpellThiefMISSILE", "블러드 엘프 주문도둑 탄: 연두 마법 구슬 + 초록 꼬리", body=body, trail_parts=[
        ("trail", "glow", dict(speed=5, variation=0.3, latitude=180, life=0.4, rate=60, colors=((0.7, 1, 0.5), (0.3, 1, 0.2), (0.1, 0.5, 0.1)),
                               alpha=(200, 120, 0), scale=(26, 18, 6), mid=0.4, pivot=(-25, 0, 0)))])


def fx_BoatMissile():
    def body(f):
        s = m_sphere("bm_ball", 0.16)
        f.add(s, "metal", blend="none", tint=(0.35, 0.35, 0.38))
    return missile("BoatMissile", "배 대포알: 검은 쇠 포탄 + 회색 연기 꼬리", body=body, trail_parts=[
        ("smoke", "smoke", dict(speed=8, variation=0.5, latitude=180, life=0.7, rate=35, filt="blend", colors=((0.6, 0.6, 0.6), (0.45, 0.45, 0.45), (0.3, 0.3, 0.3)),
                                alpha=(170, 110, 0), scale=(16, 30, 44), mid=0.3, pivot=(-15, 0, 0)))])


def fx_DemolisherMissile():
    def body(f):
        s = m_sphere("dm_rock", 0.28, stretch=(1.1, 0.9, 0.95))
        key(s, 0, rot=(0, 0, 0))
        key(s, 1.0, rot=(360, 180, 0))
        f.add(s, "rock", blend="none")
        g = m_cross_center("dm_fire", 0.9, 0.9)
        f.add(g, "flame", tint=(1, 0.8, 0.6), strength=1.3, alpha_curve=[(0, 0.9), (0.5, 1.0), (1.0, 0.9)])
    return missile("DemolisherMissile", "투석기 불붙은 돌덩이 + 불꽃·연기 꼬리", body=body, trail_parts=[
        ("fire", "flame", dict(speed=10, variation=0.4, latitude=180, life=0.45, rate=50, colors=((1, 0.9, 0.5), (1, 0.5, 0.1), (0.5, 0.1, 0)),
                               alpha=(255, 180, 0), scale=(40, 30, 10), mid=0.3, pivot=(-20, 0, 0))),
        ("smoke", "smoke", dict(speed=10, variation=0.5, latitude=180, life=0.9, rate=20, filt="blend", colors=((0.35, 0.3, 0.28), (0.25, 0.22, 0.2), (0.15, 0.15, 0.15)),
                                alpha=(150, 100, 0), scale=(30, 50, 70), mid=0.3, pivot=(-35, 0, 0)))])


def fx_MortarMissile():
    def body(f):
        s = m_sphere("mo_shell", 0.12, stretch=(1.4, 1, 1))
        f.add(s, "metal", blend="none", tint=(0.4, 0.38, 0.35))
        g = m_cross_center("mo_fire", 0.45, 0.45)
        g.location.x = -0.12
        f.add(g, "flame", strength=1.3)
    return missile("MortarMissile", "박격포탄: 작은 포탄 + 불꽃·연기 꼬리", body=body, trail_parts=[
        ("fire", "flame", dict(speed=5, variation=0.4, latitude=180, life=0.3, rate=45, colors=((1, 0.9, 0.5), (1, 0.5, 0.1), (0.5, 0.1, 0)),
                               alpha=(255, 160, 0), scale=(22, 16, 6), mid=0.3, pivot=(-15, 0, 0))),
        ("smoke", "smoke", dict(speed=8, variation=0.5, latitude=180, life=0.8, rate=22, filt="blend", colors=((0.55, 0.55, 0.55), (0.4, 0.4, 0.4), (0.3, 0.3, 0.3)),
                                alpha=(150, 90, 0), scale=(18, 32, 46), mid=0.3, pivot=(-25, 0, 0)))])


def fx_RocketMissile():
    def body(f):
        r = m_rocket("rk_body", 0.7, 0.08)
        f.add(r, "metal", blend="none", tint=(0.7, 0.7, 0.72))
        g = m_cross_center("rk_flame", 0.5, 0.35)
        g.location.x = -0.45
        key(g, 0, scale=(1, 1, 1))
        key(g, 0.5, scale=(1.2, 1.2, 0.9))
        key(g, 1.0, scale=(1, 1, 1))
        f.add(g, "flame", strength=1.5)
    return missile("RocketMissile", "로켓: 쇠 몸통 + 뒤 불꽃 + 흰 연기 꼬리", body=body, trail_parts=[
        ("exhaust", "flame", dict(speed=60, variation=0.3, latitude=10, life=0.25, rate=60, colors=((1, 1, 0.7), (1, 0.6, 0.1), (0.6, 0.1, 0)),
                                  alpha=(255, 180, 0), scale=(20, 14, 4), mid=0.3, pivot=(-40, 0, 0))),
        ("smoke", "smoke", dict(speed=10, variation=0.5, latitude=180, life=1.0, rate=30, filt="blend", colors=((0.85, 0.85, 0.85), (0.7, 0.7, 0.7), (0.5, 0.5, 0.5)),
                                alpha=(170, 100, 0), scale=(16, 34, 52), mid=0.3, pivot=(-50, 0, 0)))])


def fx_ZigguratFrostMissile():
    def body(f):
        g = m_cross_center("zf_orb", 0.6, 0.6)
        key(g, 0, rot=(0, 0, 0))
        key(g, 1.0, rot=(0, 0, 360))
        f.add(g, "flare", tint=(0.7, 0.9, 1.0), strength=1.6)
        s = m_sphere("zf_core", 0.13)
        f.add(s, "glow", tint=(0.6, 0.85, 1.0), strength=1.4)
    return missile("ZigguratFrostMissile", "서리 지구라트 탄: 얼음빛 구슬 + 서리 반짝이 꼬리", body=body, trail_parts=[
        ("frost", "flare", dict(speed=15, variation=0.6, latitude=180, gravity=30, life=0.5, rate=50, colors=((1, 1, 1), (0.6, 0.85, 1), (0.3, 0.5, 1)),
                                alpha=(255, 180, 0), scale=(14, 10, 4), mid=0.3, pivot=(-20, 0, 0))),
        ("mist", "glow", dict(speed=5, variation=0.4, latitude=180, life=0.5, rate=25, colors=((0.7, 0.9, 1), (0.5, 0.7, 1), (0.3, 0.4, 0.8)),
                              alpha=(120, 70, 0), scale=(30, 40, 50), mid=0.3, pivot=(-25, 0, 0)))])


def fx_ZigguratMissile():
    def body(f):
        g = m_cross_center("zg_orb", 0.65, 0.65)
        f.add(g, "glow", tint=(0.45, 0.55, 1.0), strength=1.7)
        s = m_xplanes("zg_streak", 1.0, 0.35)
        f.add(s, "streak", tint=(0.4, 0.45, 1.0), strength=1.0)
    return missile("ZigguratMissile", "지구라트 탄: 남보라 기운 구슬 + 푸른 꼬리", body=body, trail_parts=[
        ("trail", "glow", dict(speed=5, variation=0.3, latitude=180, life=0.45, rate=55, colors=((0.6, 0.7, 1), (0.35, 0.4, 1), (0.2, 0.1, 0.6)),
                               alpha=(220, 130, 0), scale=(30, 20, 6), mid=0.4, pivot=(-25, 0, 0)))])


ALL = [fx_BIsvTarget, fx_BlackArrowMissile, fx_BlinkTarget, fx_BloodElfSpellThiefMISSILE, fx_BoatMissile, fx_Dashboard_UtaExAuraEff,
       fx_DemolisherMissile, fx_LevelupCaster, fx_MassTeleportTarget, fx_MirrorImageCaster, fx_MonsoonBoltTarget, fx_MortarMissile,
       fx_PolyMorphTarget, fx_RocketMissile, fx_ZigguratFrostMissile, fx_ZigguratMissile]


# ============================== 내보내기 ==============================
def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    sc.render.fps = FPS
    return sc


def export(f):
    sc = bpy.context.scene
    N = max(2, round(f.dur * FPS) + 1)
    sc.frame_start, sc.frame_end = 1, N
    clipname = f"s0_{f.clip}"
    sc.name = clipname                                  # FBX 테이크 이름 = 장면 이름 → 유니티 클립 이름
    linear_all()
    root = bpy.data.objects.new(f.name, None)
    sc.collection.objects.link(root)
    meshes = [l[0] for l in f.layers]
    for o in meshes:
        o.parent = root
    root.rotation_euler = (0, 0, math.radians(-90))
    bpy.ops.object.select_all(action="DESELECT")
    for o in [root] + meshes:
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    fbx = os.path.join(f.mdir, f.name + ".fbx")
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={"MESH", "EMPTY"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
                             path_mode="STRIP", embed_textures=False, bake_anim=True, bake_anim_use_all_actions=False,
                             bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0)
    BL = {"additive": "Additive", "blend": "AlphaBlend", "none": "Opaque"}
    MF = {"additive": "additive", "blend": "blend", "none": "none"}
    side_meshes, layers = [], []
    for o, blend, fn, curve, tint in f.layers:
        mn = o.data.materials[0].name
        side_meshes.append(dict(mesh=o.name, material=mn, filter=MF[blend], suffix=mn.rsplit("_", 1)[-1], texture="(blender 근사)", textureFile=fn,
                                approxTexture=True, staticAlpha=1.0, tint=list(tint)))
        L = dict(mesh=o.name, blend=BL[blend], mdxFilter=MF[blend], texture=fn, approxTexture=True, staticAlpha=1.0, tint=list(tint))
        if curve:
            L["alphaCurveClip0Sec"] = [[round(t, 3), a] for t, a in curve]
            L["alphaCurveGlobalSec"] = [[round(t, 3), a] for t, a in curve]
        else:
            L["alphaCurveGlobalSec"] = None
        layers.append(L)
    texdir = os.path.join(f.mdir, "Textures")
    unity = dict(durationSec=round(f.dur, 3), loop=f.loop, clips=[dict(name=clipname, sec=round(f.dur, 3), loop=f.loop, mdxStartMs=0, mdxEndMs=int(f.dur * 1000))],
                 units="초·미터(FBX 크기 그대로; 워크3 1=0.01m)", textureBytes=sum(os.path.getsize(os.path.join(texdir, t)) for t in os.listdir(texdir)),
                 particlesPRE2Excluded=0, layers=layers)
    side = dict(model=f.name + "(blender 재제작)", source="워크3 기본 모델(맵 밖) — 원작 모습을 보고 Tools/blender/gen_skill_vfx_wc3.py로 베껴 만듦",
                desc=f.desc or (globals()["fx_" + f.name].__doc__ or "").strip(), scaleNote="워크3 1 = 0.01m",
                sequences=[dict(name=clipname, action=f"{f.name}|{clipname}", frames=N, fps=FPS, seconds=round(f.dur, 3), looping=f.loop)],
                meshes=side_meshes, pre2=[], prem=[], ribbons=0, nodes=[],
                unity=unity, pre2Unity=f.parts, tintNote="layers[].tint = 텍스처에 이미 구워 넣은 색(참고용)",
                pre2UnityNote="Tools/w3x/pre2_unity.py와 같은 원소 구조. 값은 원작 모습에 맞춰 손으로 정함(근사)")
    json.dump(side, open(os.path.join(f.mdir, f.name + ".json"), "w"), ensure_ascii=False, indent=1)
    return root


# ============================== 미리보기(입자 흉내 포함, 산출물 밖) ==============================
def sim_particles(f, t, rng):
    """pre2Unity를 대충 흉내 내 시각 t의 입자 위치를 돌려줌(미리보기 전용)."""
    pts = []
    for p in f.parts:
        rk = p["tracks"].get("KP2E", {}).get("keysMsAbs")
        life = p["lifespanSec"]
        emits = []
        if p["squirt"] and rk:
            n = int(rk[0][1][0])
            emits = [0.0] * n
        else:
            rate = p["emissionRate"]
            end = (rk[-1][0] / 1000.0) if rk and rk[-1][1][0] == 0 else 99
            tt = max(0.0, t - life)
            k = 0
            while tt <= t and k < 400:
                if tt <= end:
                    emits.append(tt)
                tt += 1.0 / max(rate, 0.1)
                k += 1
        for e in emits:
            age = t - e
            if age < 0 or age > life:
                continue
            u = age / life
            sp = p["speed"] * (1 + rng.uniform(-p["variation"], p["variation"])) * 0.01
            lat = math.radians(rng.uniform(0, p["latitude"]))
            az = rng.uniform(0, 2 * math.pi)
            d = (math.sin(lat) * math.cos(az), math.sin(lat) * math.sin(az), math.cos(lat))
            x0 = (p["pivot"][0] * 0.01 + rng.uniform(-0.5, 0.5) * p["width"] * 0.01, p["pivot"][1] * 0.01 + rng.uniform(-0.5, 0.5) * p["length"] * 0.01, p["pivot"][2] * 0.01)
            if f.loop and f.name.endswith(("Missile", "MISSILE")):
                x0 = (x0[0] - 6.0 * age, x0[1], x0[2])       # 날아가는 투사체: 꼬리가 뒤로 남게
            pos = [x0[i] + d[i] * sp * age for i in range(3)]
            pos[2] -= 0.5 * p["gravity"] * 0.01 * age * age
            m = p["midTime"]
            sc = (p["scale3"][0] + (p["scale3"][1] - p["scale3"][0]) * u / m) if u < m else (p["scale3"][1] + (p["scale3"][2] - p["scale3"][1]) * (u - m) / (1 - m))
            al = (p["alpha255"][0] + (p["alpha255"][1] - p["alpha255"][0]) * u / m) if u < m else (p["alpha255"][1] + (p["alpha255"][2] - p["alpha255"][1]) * (u - m) / (1 - m))
            c = p["colorsRGB01"]
            col = [c[0][i] + (c[1][i] - c[0][i]) * min(u / m, 1) if u < m else c[1][i] + (c[2][i] - c[1][i]) * (u - m) / (1 - m) for i in range(3)]
            pts.append((pos, sc * 0.01, al / 255.0, col, p["textureFile"], p["filterMode"]))
    return pts


def preview(f, root, frames, cam_dist):
    sc = bpy.context.scene
    for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            pass
    sc.render.resolution_x, sc.render.resolution_y = 360, 360
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    w.node_tree.nodes["Background"].inputs["Color"].default_value = (0.05, 0.05, 0.07, 1)
    sc.world = w
    sun = bpy.data.lights.new("sun", "SUN")
    sun.energy = 3
    so = bpy.data.objects.new("sun", sun)
    so.rotation_euler = (math.radians(40), 0, math.radians(30))
    sc.collection.objects.link(so)
    cd = bpy.data.cameras.new("cam")
    cd.lens = 40
    cam = bpy.data.objects.new("cam", cd)
    sc.collection.objects.link(cam)
    is_m = f.name.endswith(("Missile", "MISSILE"))
    tz = 0.0 if is_m else 1.0
    cam.location = (cam_dist * 0.75 if is_m else 0, -cam_dist, tz + cam_dist * 0.35)
    import mathutils
    d = mathutils.Vector((0 if not is_m else -1.0, 0, tz)) - cam.location
    cam.rotation_euler = d.to_track_quat("-Z", "Y").to_euler()
    sc.camera = cam
    if is_m:
        root.rotation_euler = (0, 0, 0)     # 미리보기는 +X가 화면 오른쪽
    rng = random.Random(1)
    pmats = {}
    shots = []
    for fr in frames:
        t = (fr - 1) / FPS
        sc.frame_set(fr)
        tmp = []
        for pos, s, a, col, tf, fm in sim_particles(f, t, rng):
            k = (tf, fm)
            if k not in pmats:
                pmats[k] = mat("pp_" + tf, os.path.join(f.mdir, "Textures", tf), "blend" if fm == "blend" else "additive")
            o = m_cross_center("pp", max(s, 0.02), max(s, 0.02), n=2)
            o.location = pos
            o.data.materials.append(pmats[k])
            o.color = (1, 1, 1, a)
            o.hide_render = False
            tmp.append(o)
            o.rotation_euler = (0, 0, math.atan2(cam.location.y - pos[1], cam.location.x - pos[0]) + math.pi / 2)
            # 색 곱: 재질 공용이라 미리보기에선 흰빛(입자 색은 json에) — 대략 모양만
        p = os.path.join(PREV, f"_{f.name}_{fr:02d}.png")
        sc.render.filepath = p
        bpy.ops.render.render(write_still=True)
        shots.append(p)
        for o in tmp:
            bpy.data.objects.remove(o, do_unlink=True)
    return shots


def main():
    names = [a for a in ARGS if not a.startswith("--") and (ARGS.index(a) == 0 or ARGS[ARGS.index(a) - 1] != "--out")]
    os.makedirs(PREV, exist_ok=True)
    results = []
    for fn in ALL:
        nm = fn.__name__[3:]
        if names and nm not in names:
            continue
        reset()
        f = fn()
        os.makedirs(f.mdir, exist_ok=True)
        root = export(f)
        N = max(2, round(f.dur * FPS) + 1)
        frames = [2, max(3, N // 3), max(4, N * 2 // 3)]
        shots = preview(f, root, frames, 6.0 if not f.name.endswith(("Missile", "MISSILE")) else 3.0)
        results.append(dict(name=f.name, dur=f.dur, loop=f.loop, meshes=len(f.layers), pre2=len(f.parts), shots=shots))
        print("OK", f.name, "메시", len(f.layers), "입자", len(f.parts))
    json.dump(results, open(os.path.join(PREV, "_shots.json"), "w"), ensure_ascii=False, indent=1)


main()
