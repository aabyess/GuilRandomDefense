"""원작 시전 이펙트 MDX를 시간 순서 스냅샷으로 그린다(2026-10-08, 초월 시전 이펙트 시범).

  1) /usr/bin/python3 Tools/w3x/mdx_extract.py <ord.mpq> <작업폴더> <이름.mdx> …     (MDX·텍스처 PNG 추출)
  2) blender -b --factory-startup --python Tools/blender/render_cast_vfx.py -- orig <작업폴더> <출력폴더> <이름.mdx> <크기배수> [snap수=4]
     blender -b --factory-startup --python Tools/blender/render_cast_vfx.py -- ours <작업폴더> <출력폴더> <FBX이름 VFX_초승달_검기> <대표치수(워크3 단위)> <텍스처 png>
  3) /usr/bin/python3 Tools/blender/compose_cast_vfx.py …                         (나란히 놓은 비교 그림)

orig: 지오셋 메시(정적 — 뼈 변환·지오셋 알파 애니는 안 읽는다) + **PRE2 파티클 추정 시뮬레이션**. 추정 규칙(워크3 MDX 형식 지식 + 파일 값):
  - 방출률 = KP2E 키(없으면 rate) × KP2V 가시성(≥0.5). 시퀀스 길이 동안 방출, 스냅샷 시각 T에 살아 있는 입자를 그린다.
  - 위치 = 방출점 pivot + 방출 면(width×length 무작위) + 속도(방향 = +Z 둘레 latitude° 원뿔, 크기 speed×(1±variation)) × 나이 + 중력.
  - 크기 = scale(시작·중간·끝)을 수명 비율(mid 지점 기준)로 보간 · 색 = colors 3점 · 알파 = alpha 3점 · 플립북은 나이에 따라 칸 순서 재생.
  - 모든 입자는 카메라를 보는 판(빌보드). filter 1(가산)은 가산, 나머지는 알파 혼합. 헤드/테일·UV 애니·「squirt」·모델 입자(PREM)·리본은 안 그린다.
  **추정**이다 — 모양·밝기가 원작과 정확히 같다고 보지 말 것. 워크3의 정확한 난수·정렬·배치와 다르다.
ours: 우리 VFX_*.fbx(Assets/Art/Effects/Meshes)를 같은 카메라·같은 가산 재질로 그린다(정적, 대표 치수 고정).
"""
import math
import os
import random
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.abspath(os.path.join(HERE, "..", "w3x")))
import mdx_anim                                                      # noqa: E402
import mdx_geo                                                       # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
MODE, WORK, OUT = args[0], args[1], args[2]
os.makedirs(OUT, exist_ok=True)
sys.argv = ["x", "--", WORK, OUT]                                    # render_mdx를 부품으로 쓴다(NAMES 빈 채로 import)
sys.path.insert(0, HERE)
import render_mdx as R                                               # noqa: E402

VIEW = Vector((0.9, -1.0, 0.8)).normalized()                         # 앞비스듬히 위에서
REF_H = 90.0                                                          # 기준 사람 키(워크3 단위)


def track_value(tr, t_ms, default, step=True):
    """KP2E/KP2V 같은 한 값 트랙을 t_ms에서 읽는다(키 시각은 파일 전체 ms)."""
    if not tr or not tr.get("keys"):
        return default
    ks = tr["keys"]
    if t_ms <= ks[0][0]:
        return ks[0][1][0]
    for a, b in zip(ks, ks[1:]):
        if a[0] <= t_ms < b[0]:
            if step or tr.get("interp", 0) == 0:
                return a[1][0]
            f = (t_ms - a[0]) / max(b[0] - a[0], 1)
            return a[1][0] + (b[1][0] - a[1][0]) * f
    return ks[-1][1][0]


def lerp3(c, m, f):
    """3점(시작·중간·끝) 보간. m = 중간 지점(수명 비율)."""
    if f < m:
        k = f / max(m, 1e-4)
        return [c[0][i] + (c[1][i] - c[0][i]) * k for i in range(len(c[0]))] if isinstance(c[0], (list, tuple)) else c[0] + (c[1] - c[0]) * k
    k = (f - m) / max(1 - m, 1e-4)
    return [c[1][i] + (c[2][i] - c[1][i]) * k for i in range(len(c[1]))] if isinstance(c[1], (list, tuple)) else c[1] + (c[2] - c[1]) * k


def particle_material(name, tex_path, additive, use_tex_alpha=False):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    tn = nt.nodes.new("ShaderNodeTexImage")
    f = os.path.join(R.TEXDIR, (tex_path or "").replace("\\", "_").replace("/", "_") + ".png")
    if tex_path and os.path.exists(f):
        tn.image = bpy.data.images.load(f, check_existing=True)
    vc = nt.nodes.new("ShaderNodeVertexColor")
    vc.layer_name = "Col"
    mul = nt.nodes.new("ShaderNodeMix")
    mul.data_type = "RGBA"
    mul.blend_type = "MULTIPLY"
    mul.inputs[0].default_value = 1.0
    nt.links.new(tn.outputs["Color"], mul.inputs[6])
    nt.links.new(vc.outputs["Color"], mul.inputs[7])
    em = nt.nodes.new("ShaderNodeEmission")
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mix = nt.nodes.new("ShaderNodeMixShader")
    nt.links.new(mul.outputs[2], em.inputs["Color"])
    if additive:
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(mul.outputs[2], sep.inputs["Color"])
        m1 = nt.nodes.new("ShaderNodeMath"); m1.operation = "MAXIMUM"
        m2 = nt.nodes.new("ShaderNodeMath"); m2.operation = "MAXIMUM"
        nt.links.new(sep.outputs[0], m1.inputs[0]); nt.links.new(sep.outputs[1], m1.inputs[1])
        nt.links.new(m1.outputs[0], m2.inputs[0]); nt.links.new(sep.outputs[2], m2.inputs[1])
        a = nt.nodes.new("ShaderNodeMath"); a.operation = "MULTIPLY"
        nt.links.new(m2.outputs[0], a.inputs[0]); nt.links.new(vc.outputs["Alpha"], a.inputs[1])
        if use_tex_alpha:                                            # addalpha: 텍스처 알파도 곱한다
            a2 = nt.nodes.new("ShaderNodeMath"); a2.operation = "MULTIPLY"
            nt.links.new(a.outputs[0], a2.inputs[0]); nt.links.new(tn.outputs["Alpha"], a2.inputs[1])
            nt.links.new(a2.outputs[0], mix.inputs["Fac"])
        else:
            nt.links.new(a.outputs[0], mix.inputs["Fac"])
    else:
        # 맵 밖 기본 텍스처는 mdx_extract가 알파 255짜리 근사 그림을 대신 깐다 → 알파 대신 텍스처 밝기(검정=투명)를 쓴다
        sp = nt.nodes.new("ShaderNodeSeparateColor")
        nt.links.new(tn.outputs["Color"], sp.inputs["Color"])
        b1 = nt.nodes.new("ShaderNodeMath"); b1.operation = "MAXIMUM"
        b2 = nt.nodes.new("ShaderNodeMath"); b2.operation = "MAXIMUM"
        nt.links.new(sp.outputs[0], b1.inputs[0]); nt.links.new(sp.outputs[1], b1.inputs[1])
        nt.links.new(b1.outputs[0], b2.inputs[0]); nt.links.new(sp.outputs[2], b2.inputs[1])
        a = nt.nodes.new("ShaderNodeMath"); a.operation = "MULTIPLY"
        src = tn.outputs["Alpha"] if use_tex_alpha else b2.outputs[0]    # 맵 안 진짜 텍스처는 알파를, 근사 그림은 밝기를
        nt.links.new(src, a.inputs[0]); nt.links.new(vc.outputs["Alpha"], a.inputs[1])
        nt.links.new(a.outputs[0], mix.inputs["Fac"])
    em.inputs["Strength"].default_value = 1.6 if additive else 1.0
    nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2])
    nt.links.new(mix.outputs[0], out.inputs["Surface"])
    mat.surface_render_method = "BLENDED"
    mat.use_backface_culling = False
    return mat



# ───────── 뼈 애니메이션 평가(지오셋을 시각 t_ms에서 변형) ─────────
from mathutils import Matrix, Quaternion


def sample(tr, t_ms, default):
    """트랙 → t_ms 값(선형. 헤르미트·베지어도 선형으로 근사). 키가 없으면 default."""
    if not tr or not tr.get("keys"):
        return default
    ks = tr["keys"]
    if t_ms <= ks[0][0]:
        return ks[0][1]
    if t_ms >= ks[-1][0]:
        return ks[-1][1]
    for a, b in zip(ks, ks[1:]):
        if a[0] <= t_ms <= b[0]:
            if tr.get("interp", 0) == 0:
                return a[1]
            f = (t_ms - a[0]) / max(b[0] - a[0], 1)
            return tuple(x + (y - x) * f for x, y in zip(a[1], b[1]))
    return ks[-1][1]


def node_matrices(info, t_ms):
    """모든 노드의 월드 변환 행렬 {id: Matrix} — pivot 기준 이동·회전·스케일을 부모부터 합성."""
    nodes = {n["id"]: n for n in info["nodes"]}
    cache = {}

    def world(nid):
        if nid in cache:
            return cache[nid]
        n = nodes.get(nid)
        if n is None:
            return Matrix.Identity(4)
        piv = Vector(info["pivots"][nid]) if nid < len(info["pivots"]) else Vector((0, 0, 0))
        tr = n["tracks"]
        T = sample(tr.get("KGTR"), t_ms, (0.0, 0.0, 0.0))
        Rq = sample(tr.get("KGRT"), t_ms, (0.0, 0.0, 0.0, 1.0))
        S = sample(tr.get("KGSC"), t_ms, (1.0, 1.0, 1.0))
        q = Quaternion((Rq[3], Rq[0], Rq[1], Rq[2]))
        q.normalize()
        local = Matrix.Translation(piv) @ Matrix.Translation(Vector(T)) @ q.to_matrix().to_4x4() @ Matrix.Diagonal(Vector((S[0], S[1], S[2], 1.0))) @ Matrix.Translation(-piv)
        par = n["parent"]
        m = (world(par) @ local) if par not in (-1, 0xFFFFFFFF) and par in nodes and par != nid else local
        cache[nid] = m
        return m

    return {nid: world(nid) for nid in nodes}


def skinned_geoset(g, mats_by_node):
    """지오셋 정점을 매트릭스 그룹(뼈들의 평균)으로 변형 → [Vector]."""
    groups, pos = [], 0
    for c in g["mtgc"]:
        groups.append(g["mats"][pos:pos + c])
        pos += c
    out = []
    for vi, v in enumerate(g["verts"]):
        gi = g["gndx"][vi] if vi < len(g["gndx"]) else 0
        bones = groups[gi] if gi < len(groups) and groups[gi] else []
        vv = Vector(v)
        if not bones:
            out.append(vv)
            continue
        acc = Vector((0, 0, 0))
        for b in bones:
            acc += mats_by_node.get(b, Matrix.Identity(4)) @ vv
        out.append(acc / len(bones))
    return out


def geoset_alpha(info, gi, t_ms):
    a = 1.0
    for ga in info["geoa"]:
        if ga["geoset"] == gi:
            a = sample(ga["tracks"].get("KGAO"), t_ms, (ga["alpha"],))[0]
    return a


def layer_alpha(info, mi, li, t_ms):
    a = 1.0
    for la in info["layer_anims"]:
        if la["material"] == mi and la["layer"] == li and "KMTA" in la["tracks"]:
            a = sample(la["tracks"]["KMTA"], t_ms, (1.0,))[0]
    return a


def build_geosets(geo, info, t_ms, scale, approx):
    """시각 t_ms의 지오셋 메시 오브젝트들(재질: 정점색 알파 = 지오셋 알파 × 층 알파)."""
    mats = node_matrices(info, t_ms)
    objs = []
    for gi, g in enumerate(geo["geosets"]):
        gmat = geo["materials"][g["material"]]
        vs = skinned_geoset(g, mats)
        ga = geoset_alpha(info, gi, t_ms)
        for li, layer in enumerate(gmat["layers"]):
            la = layer_alpha(info, g["material"], li, t_ms)
            tex = geo["textures"][layer["tex"]]
            if tex["replaceable"] in (1, 2) or "team" in (tex["path"] or "").lower():
                continue                                                # 팀 색 판은 안 그린다
            alpha = max(0.0, min(1.0, ga * la))
            if alpha <= 0.002:
                continue
            me = bpy.data.meshes.new(f"g{gi}_{li}")
            me.from_pydata([tuple(v * scale) for v in vs], [], g["tris"])
            uv = me.uv_layers.new(name="UV")
            for poly in me.polygons:
                for vi, loop in zip(poly.vertices, poly.loop_indices):
                    u, v = g["uvs"][vi] if vi < len(g["uvs"]) else (0, 0)
                    uv.data[loop].uv = (u, 1.0 - v)
            ca = me.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
            for loop in me.loops:
                ca.data[loop.index].color = (1, 1, 1, alpha)
            me.update()
            filt = layer["filter"]
            additive = filt in ("additive", "addalpha")
            real = tex["path"] not in approx
            m = particle_material(f"gm_{gi}_{li}", tex["path"], additive, use_tex_alpha=(real and filt in ("addalpha", "blend", "transparent")))
            me.materials.append(m)
            ob = bpy.data.objects.new(me.name, me)
            bpy.context.scene.collection.objects.link(ob)
            objs.append(ob)
    return objs


def simulate(p, info, geo, seq, T, scale, rnd):
    """PRE2 하나를 시각 T(초, 시퀀스 시작 기준)에서 → [(pos, size, rgba, frame)] 목록."""
    s0 = seq["start"]
    pivot = info["pivots"][p["node"]["id"]] if p["node"]["id"] < len(info["pivots"]) else (0, 0, 0)
    life = max(p["lifespan"], 0.05)
    out = []
    dt = 0.01
    acc = 0.0
    t = 0.0
    nmax = 1200
    emis = p["tracks"].get("KP2E")
    vis = p["tracks"].get("KP2V")
    lat = math.radians(min(p["latitude"], 180.0))
    while t <= T and len(out) < nmax:
        rate = track_value(emis, s0 + t * 1000, p["rate"]) if emis else p["rate"]
        v = track_value(vis, s0 + t * 1000, 1.0) if vis else 1.0
        if v >= 0.5 and rate > 0:
            acc += rate * dt
            while acc >= 1.0:
                acc -= 1.0
                age = T - t
                if age >= life:
                    continue
                f = age / life
                a1 = rnd.random() * lat
                a2 = rnd.random() * math.tau
                sp = p["speed"] * (1 + p["variation"] * (rnd.random() * 2 - 1))
                d = Vector((math.sin(a1) * math.cos(a2), math.sin(a1) * math.sin(a2), math.cos(a1)))
                off = Vector(((rnd.random() - 0.5) * p["width"], (rnd.random() - 0.5) * p["length"], 0))
                pos = Vector(pivot) + off + d * sp * age + Vector((0, 0, -0.5 * p["gravity"] * age * age))
                sz = lerp3(p["scale"], p["mid"], f)
                col = lerp3(p["colors"], p["mid"], f)
                al = lerp3(p["alpha"], p["mid"], f) / 255.0
                nf = max(p["rows"] * p["cols"], 1)
                frame = min(int(f * nf), nf - 1)
                out.append((pos * scale, sz * scale, (col[0], col[1], col[2], al), frame))
        t += dt
    return out


def quad_mesh(name, parts, p, right, up):
    me = bpy.data.meshes.new(name)
    vs, fs, uvs, cols = [], [], [], []
    rows, cols_n = max(p["rows"], 1), max(p["cols"], 1)
    for pos, sz, rgba, frame in parts:
        h = sz / 2
        i = len(vs)
        for dx, dy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
            vs.append(tuple(pos + right * dx * h + up * dy * h))
        fs.append((i, i + 1, i + 2, i + 3))
        r, c = divmod(frame, cols_n)
        u0, v0 = c / cols_n, 1 - (r + 1) / rows
        for (uu, vv) in ((0, 0), (1, 0), (1, 1), (0, 1)):
            uvs.append((u0 + uu / cols_n, v0 + vv / rows))
            cols.append(rgba)
    me.from_pydata(vs, [], fs)
    uv = me.uv_layers.new(name="UV")
    for li, (u, v) in enumerate(uvs):
        uv.data[li].uv = (u, v)
    ca = me.color_attributes.new("Col", "FLOAT_COLOR", "CORNER")
    for li, c in enumerate(cols):
        ca.data[li].color = c
    me.update()
    return bpy.data.objects.new(name, me)


def setup_scene():
    scn = bpy.context.scene
    try:
        scn.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x = scn.render.resolution_y = 640
    scn.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (0.07, 0.075, 0.09, 1)
    scn.world = w
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scn.collection.objects.link(cam)
    scn.camera = cam
    cam.data.type = "ORTHO"
    return scn, cam


def add_reference(scn, scale_unit=1.0):
    """기준 사람(키 90) 반투명 기둥 + 바닥 격자 점 — 크기 가늠용."""
    me = bpy.data.meshes.new("ref")
    bpy.ops.mesh.primitive_cylinder_add(radius=12, depth=REF_H, location=(0, 0, REF_H / 2))
    ob = bpy.context.active_object
    ob.name = "ref"
    m = bpy.data.materials.new("ref")
    m.use_nodes = True
    nt = m.node_tree
    nt.nodes.clear()
    o = nt.nodes.new("ShaderNodeOutputMaterial")
    em = nt.nodes.new("ShaderNodeEmission")
    em.inputs["Color"].default_value = (0.35, 0.5, 0.9, 1)
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mx = nt.nodes.new("ShaderNodeMixShader")
    mx.inputs["Fac"].default_value = 0.35
    nt.links.new(tr.outputs[0], mx.inputs[1]); nt.links.new(em.outputs[0], mx.inputs[2]); nt.links.new(mx.outputs[0], o.inputs["Surface"])
    m.surface_render_method = "BLENDED"
    ob.data.materials.append(m)
    return ob


def frame_and_render(scn, cam, half, center, path):
    cam.data.ortho_scale = half * 2.0
    cam.data.clip_start = half * 0.01
    cam.data.clip_end = half * 40
    cam.location = center + VIEW * half * 6
    cam.rotation_euler = (-VIEW).to_track_quat("-Z", "Y").to_euler()
    scn.render.filepath = path
    bpy.ops.render.render(write_still=True)


def do_orig():
    name, scale = args[3], float(args[4])
    nsnap = int(args[5]) if len(args) > 5 else 4
    data = open(os.path.join(WORK, name), "rb").read()
    geo = mdx_geo.parse(data)
    info = mdx_anim.describe(data)
    seq = max(info["sequences"], key=lambda s: s["end"] - s["start"])
    dur = (seq["end"] - seq["start"]) / 1000.0
    tag = os.path.splitext(name)[0]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scn, cam = setup_scene()
    right = VIEW.cross(Vector((0, 0, 1))).normalized()
    up = right.cross(VIEW).normalized()
    approx = set(__import__("json").load(open(os.path.join(WORK, "_approx.json")))) if os.path.exists(os.path.join(WORK, "_approx.json")) else set()
    pts = []
    # 스냅샷 시각 — 시작·중반·끝 구간(파티클 수명이 시퀀스보다 길면 시퀀스 끝 + 수명까지)
    horizon = 0.0                                                    # 마지막 방출이 끝난 시각 + 입자 수명 = 입자가 다 사라지는 때
    for pe in info["pre2"]:
        keys = (pe["tracks"].get("KP2E") or {}).get("keys") or []
        zero = [k[0] for k in keys if k[1][0] == 0 and k[0] > keys[0][0]]
        end = ((min(zero) - seq["start"]) / 1000.0) if zero else (dur if (pe["rate"] > 0 or keys) else 0.0)
        horizon = max(horizon, end + pe["lifespan"])
    horizon = min(max(horizon, dur if (info["nodes"] or info["geoa"]) else 0.0, 0.3), 8.0)
    fr = {1: [0.4], 2: [0.2, 0.7], 3: [0.12, 0.4, 0.85], 4: [0.1, 0.3, 0.55, 0.9]}.get(nsnap) or [(i + 0.5) / nsnap for i in range(nsnap)]
    times = [horizon * f for f in fr]
    for T in times:                                                  # 틀 잡기: 시각마다 변형된 메시 점을 모은다
        tmp = build_geosets(geo, info, seq["start"] + T * 1000, scale, approx)
        bpy.context.view_layer.update()
        pts += [o.matrix_world @ v.co for o in tmp for v in o.data.vertices]
        for o in tmp:
            bpy.data.objects.remove(o)
    sims = []
    pp = []
    for T in times:
        rnd = random.Random(7)
        per = []
        for p in info["pre2"]:
            if p["tex"] < len(geo["textures"]):
                per.append((p, simulate(p, info, geo, seq, T, scale, rnd)))
        sims.append(per)
        for p, parts in per:
            pp += [pos for pos, *_ in parts]
    if pp:                                                           # 튀어나간 입자 몇 개가 화면을 늘리지 않게 5~95% 분위로 틀을 잡는다
        for ax in range(3):
            v = sorted(q[ax] for q in pp)
            pts += [Vector((v[int(len(v) * 0.05)] if ax == 0 else 0, v[int(len(v) * 0.05)] if ax == 1 else 0, v[int(len(v) * 0.05)] if ax == 2 else 0)),
                    Vector((v[int(len(v) * 0.95)] if ax == 0 else 0, v[int(len(v) * 0.95)] if ax == 1 else 0, v[int(len(v) * 0.95)] if ax == 2 else 0))]
    pts.append(Vector((0, 0, REF_H)))
    lo = Vector((min(q.x for q in pts), min(q.y for q in pts), min(q.z for q in pts)))
    hi = Vector((max(q.x for q in pts), max(q.y for q in pts), max(q.z for q in pts)))
    half = max((hi - lo).length * 0.5, 40.0) * 0.8
    center = (lo + hi) / 2
    add_reference(scn)
    mats = {}
    for i, (T, per) in enumerate(zip(times, sims)):
        added = []
        for p, parts in per:
            if not parts:
                continue
            tex = geo["textures"][p["tex"]]["path"]
            additive = p["filter"] in (1, 3)
            key = (tex, additive)
            if key not in mats:
                mats[key] = particle_material(f"pm_{len(mats)}", tex, additive)
            ob = quad_mesh(f"p_{i}_{p['node']['id']}", parts, p, right, up)
            ob.data.materials.append(mats[key])
            scn.collection.objects.link(ob)
            added.append(ob)
        added += build_geosets(geo, info, seq["start"] + T * 1000, scale, approx)
        frame_and_render(scn, cam, half, center, os.path.join(OUT, f"{tag}_orig_s{i}.png"))
        for ob in added:
            bpy.data.objects.remove(ob)
        print("snap", i, round(T, 2), "particles", sum(len(pp) for _, pp in per))
    open(os.path.join(OUT, f"{tag}_orig_info.txt"), "w").write(
        f"{name} scale={scale} seq={seq['name']} {dur:.2f}s horizon={horizon:.2f}s times={[round(t, 2) for t in times]} pre2={len(info['pre2'])} extent={[round(v) for v in (hi - lo)]}\n")


def ours_material(texpng, tint):
    img = bpy.data.images.load(texpng, check_existing=True)
    mat = bpy.data.materials.new("ours")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    tn = nt.nodes.new("ShaderNodeTexImage")
    tn.image = img
    em = nt.nodes.new("ShaderNodeEmission")
    tr = nt.nodes.new("ShaderNodeBsdfTransparent")
    mx = nt.nodes.new("ShaderNodeMixShader")
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    m1 = nt.nodes.new("ShaderNodeMath"); m1.operation = "MAXIMUM"
    m2 = nt.nodes.new("ShaderNodeMath"); m2.operation = "MAXIMUM"
    nt.links.new(tn.outputs["Color"], sep.inputs["Color"])
    nt.links.new(sep.outputs[0], m1.inputs[0]); nt.links.new(sep.outputs[1], m1.inputs[1])
    nt.links.new(m1.outputs[0], m2.inputs[0]); nt.links.new(sep.outputs[2], m2.inputs[1])
    nt.links.new(m2.outputs[0], mx.inputs["Fac"])
    mixc = nt.nodes.new("ShaderNodeMix"); mixc.data_type = "RGBA"; mixc.blend_type = "MULTIPLY"; mixc.inputs[0].default_value = 1
    nt.links.new(tn.outputs["Color"], mixc.inputs[6]); mixc.inputs[7].default_value = tint + (1,)
    nt.links.new(mixc.outputs[2], em.inputs["Color"])
    em.inputs["Strength"].default_value = 1.6
    nt.links.new(tr.outputs[0], mx.inputs[1]); nt.links.new(em.outputs[0], mx.inputs[2]); nt.links.new(mx.outputs[0], out.inputs["Surface"])
    mat.surface_render_method = "BLENDED"
    mat.use_backface_culling = False
    return mat


def do_ours():
    """args: ours <작업> <출력> <출력이름> <fbx|대표치수|텍스처png|r,g,b> …  (여러 층을 한 장에 겹친다 — 게임의 Layered 이펙트 흉내, 정적·최대 크기)"""
    label, specs = args[3], args[4:]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scn, cam = setup_scene()
    base = os.path.join(os.path.abspath(os.path.join(HERE, "..", "..")), "Assets/Art/Effects/Meshes")
    allobjs = []
    for sp in specs:
        fbx, size, texpng, tint = sp.split("|")
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=os.path.join(base, fbx + ".fbx"))
        new = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
        mat = ours_material(texpng, tuple(float(x) for x in tint.split(",")))
        for o in new:
            o.data.materials.clear()
            o.data.materials.append(mat)
            o.scale = (float(size),) * 3
        allobjs += new
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ v.co for o in allobjs for v in o.data.vertices]
    pts.append(Vector((0, 0, REF_H)))
    lo = Vector((min(q.x for q in pts), min(q.y for q in pts), min(q.z for q in pts)))
    hi = Vector((max(q.x for q in pts), max(q.y for q in pts), max(q.z for q in pts)))
    half = max((hi - lo).length * 0.5, 40.0) * 0.8
    add_reference(scn)
    frame_and_render(scn, cam, half, (lo + hi) / 2, os.path.join(OUT, f"{label}_ours.png"))
    open(os.path.join(OUT, f"{label}_ours_info.txt"), "w").write(f"{label} specs={specs} extent={[round(v, 1) for v in (hi - lo)]}\n")


{"orig": do_orig, "ours": do_ours}[MODE]()
