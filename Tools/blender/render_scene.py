"""원작 대표 연출 「대본」 json을 Blender에서 시간 순서 스냅샷으로 재현(2026-10-09, blender).

  blender -b --factory-startup --python Tools/blender/render_scene.py -- <대본.json> <출력폴더> <t1,t2,...> [half=원작단위 시야 반폭]
render_cast_vfx.py의 MDX 지오셋·PRE2 입자 추정 시뮬레이션(원작 값, 모양·밝기는 추정)을 그대로 쓰되, 대본의 소환·이동·크기·수명을 적용한다.
UV 오프셋 애니(KTAT)도 적용(레일건 같은 번개 모델). 워크3 기본 모델(맵에 없음: 번개 줄기·전함 등)은 대략 모양 자리표시자로 그린다(주황 테두리 라벨은 PIL 합성 단계).
"""
import json, math, os, random, re, sys
import bpy
from mathutils import Vector
HERE = os.path.dirname(os.path.abspath(__file__))
args = sys.argv[sys.argv.index("--") + 1:]
SCRIPT, OUTD, TIMES = args[0], args[1], [float(x) for x in args[2].split(",")]
HALF = float(args[3]) if len(args) > 3 else 600.0
CZ = float(args[4]) if len(args) > 4 else HALF * 0.35
os.makedirs(OUTD, exist_ok=True)
H = os.path.expanduser("~"); T = H + "/GRD_orig_vfx_trial"
WORKS = [H + f"/GRD_motion_trial/{w}/work" for w in ("original_vfx", "transcend_vfx", "grade_extra", "original_skin")]
src = open(HERE + "/render_cast_vfx.py", encoding="utf8").read()
sys.argv = ["x", "--", "orig", WORKS[0], "/tmp/_rs"]
src = src[:src.index("def do_orig")]
src = src.replace("            alpha = max(0.0, min(1.0, ga * la))", "            alpha = max(0.0, min(1.0, ga * la))\n            UVO = uv_offset_fn(info, geo, gi, li, t_ms)")
src = src.replace("uv.data[loop].uv = (u, 1.0 - v)", "uv.data[loop].uv = (u + UVO[0], 1.0 - v - UVO[1])")
src = src.replace('a = sample(ga["tracks"].get("KGAO"), t_ms, (ga["alpha"],))[0]', 'tk = (ga["tracks"].get("KGAO") or {}).get("keys"); a = ga["alpha"] if (tk and t_ms < tk[0][0]) else sample(ga["tracks"].get("KGAO"), t_ms, (ga["alpha"],))[0]')
src = src.replace('a = sample(la["tracks"]["KMTA"], t_ms, (1.0,))[0]', 'tk = la["tracks"]["KMTA"].get("keys"); a = 1.0 if (tk and t_ms < tk[0][0]) else sample(la["tracks"]["KMTA"], t_ms, (1.0,))[0]')
def uv_offset_fn(info, geo, gi, li, t_ms):
    lay = geo["materials"][geo["geosets"][gi]["material"]]["layers"][li]; ta = lay.get("texanim", -1)
    if ta in (-1, 0xFFFFFFFF) or not (0 <= ta < len(info["txan"])): return 0, 0
    tr = info["txan"][ta]["tracks"].get("KTAT")
    if not tr or not tr.get("keys"): return 0, 0
    v = tr["keys"][0][1]
    for a in tr["keys"]:
        if a[0] <= t_ms: v = a[1]
    return v[0], v[1]
exec(compile(src, "render_cast_vfx", "exec"))      # MODE=orig, WORK=original_vfx/work — TEXDIR은 모델마다 바꾼다
sc = json.load(open(SCRIPT))
right = VIEW.cross(Vector((0, 0, 1))).normalized(); up = right.cross(VIEW).normalized()
CACHE = {}

def load_model(m):
    if m in CACHE: return CACHE[m]
    folder = sc["models"][m].get("folder"); r = None
    if folder:
        jd = json.load(open(f"{T}/{folder}/{folder}.json"))
        for w in WORKS:
            if os.path.exists(f"{w}/{jd['model']}"):
                data = open(f"{w}/{jd['model']}", "rb").read(); R.TEXDIR = w + "/_tex"
                r = (mdx_geo.parse(data), mdx_anim.describe(data), set(json.load(open(w + "/_approx.json"))) if os.path.exists(w + "/_approx.json") else set(), w); break
    CACHE[m] = r; return r

def pick_seq(info, want):
    ss = info["sequences"]
    if not ss: return dict(name="stand", start=0, end=1000, looping=True)
    if want:
        for s in ss:
            if want.lower() in s["name"].lower(): return s
    for key in ("stand",):
        for s in ss:
            if key in s["name"].lower(): return s
    return ss[0]

def uv_offset(info, geo, gi, li, t_ms):
    lay = geo["materials"][geo["geosets"][gi]["material"]]["layers"][li]; ta = lay.get("texanim", -1)
    if ta in (-1, 0xFFFFFFFF) or not (0 <= ta < len(info["txan"])): return 0, 0
    tr = info["txan"][ta]["tracks"].get("KTAT")
    if not tr or not tr.get("keys"): return 0, 0
    ks = tr["keys"]; v = ks[0][1]
    for a in ks:
        if a[0] <= t_ms: v = a[1]
    return v[0], v[1]

def actor_state(e, ev, Tn):
    """대본 이벤트(spawn)와 이후 set/ramp/kill을 Tn 시각에 적용 → (alive, 사망 중?, 로컬 시간, 위치 z, 배율%, 타임스케일)."""
    t0 = e["t"]; lt = Tn - t0
    if lt < 0: return None
    life = e.get("lifeSec"); kill = next((x["t"] for x in ev if x["op"] == "kill" and x["id"] == e["id"] and x["t"] >= t0), None)
    end = t0 + life if life is not None else None
    if kill is not None: end = kill if end is None else min(end, kill)
    dead = False; dl = 0
    if end is not None and Tn > end: dead = True; dl = Tn - end
    if dead and dl > max(e.get("deathSec", 0.1), 0.05) + 0.02: return None
    fly = e.get("flyHeight", 0.0); pct = e.get("scalePercent", 100); ts = e.get("timescale", 1.0)
    tcur = t0
    for x in sorted((x for x in ev if x.get("id") == e["id"] and x["op"] in ("set", "ramp") and x["t"] <= Tn), key=lambda x: x["t"]):
        if x["op"] == "set":
            if "scalePercent" in x: pct = x["scalePercent"]
            if "timescale" in x: ts = x["timescale"]
        elif x["prop"] == "flyHeight":
            d = x["to"] - fly; step = x["ratePerSec"] * (Tn - x["t"])
            fly = x["to"] if abs(d) <= step else fly + math.copysign(step, d)
    return dict(dead=dead, lt=lt, dl=dl, fly=fly, pct=pct, ts=ts)

def anchor_pos(e, ev, byid):
    a = e["at"]
    if "world" in a: return tuple(a["world"])
    base = {"caster": (0, 0), "target": (0, 0)}.get(a.get("anchor"), (0, 0))
    if sc["id"] == "shiki_fleet" and a.get("anchor") == "target": base = (700, 0)
    x, y = base
    if "polar" in a:
        r, ang = a["polar"]["radius"], math.radians(a["polar"]["angleDeg"]); x += r * math.cos(ang); y += r * math.sin(ang)
    if a.get("anchor") == "ship":
        s = byid[a["ship"]]; p = anchor_pos(s, ev, byid); x, y = p[0], p[1]
        mv = e.get("moveTo")
    return (x, y)

def placeholder(kind, pos, scale):
    if kind == "Lightningbolt":
        bpy.ops.mesh.primitive_cylinder_add(radius=18 * scale / 15, depth=1500, location=(pos[0], pos[1], 750))
        col = (0.6, 0.7, 1.0, 1)
    elif kind == "HumanBattleship":                                   # 우리 해적선 FBX(길이 380 워크3 단위로 맞춤)
        ship = os.path.join(os.path.dirname(os.path.dirname(HERE)), ".check_entries_out", "해적선.fbx")
        before = set(bpy.data.objects)
        bpy.ops.import_scene.fbx(filepath=ship)
        new = [o for o in bpy.data.objects if o not in before]
        meshes = [o for o in new if o.type == "MESH"]
        pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
        lo = Vector((min(q.x for q in pts), min(q.y for q in pts), min(q.z for q in pts))); hi = Vector((max(q.x for q in pts), max(q.y for q in pts), max(q.z for q in pts)))
        k = 380.0 / max((hi - lo).x, (hi - lo).y, 1e-6)
        root = bpy.data.objects.new("shiproot", None); scn.collection.objects.link(root)
        for o in new:
            if o.parent is None: o.parent = root
        root.scale = (k, k, k); root.location = (pos[0] - (lo.x + hi.x) / 2 * k, pos[1] - (lo.y + hi.y) / 2 * k, pos[2] - lo.z * k)
        for o in meshes:
            for mt in o.data.materials:
                if mt and mt.use_nodes:
                    bs = next((n for n in mt.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
                    if bs: bs.inputs["Emission Strength"].default_value = 0.25 if "Emission Strength" in bs.inputs else 0
        return root
    else:
        bpy.ops.mesh.primitive_torus_add(major_radius=60 * scale, minor_radius=6, location=(pos[0], pos[1], 10)); col = (0.3, 1.0, 0.5, 1)
    ob = bpy.context.active_object; ob.data.materials.append(emit("ph", col)); return ob

def emit(name, col):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    o = nt.nodes.new("ShaderNodeOutputMaterial"); e = nt.nodes.new("ShaderNodeEmission"); e.inputs["Color"].default_value = col; e.inputs["Strength"].default_value = 1.2
    nt.links.new(e.outputs[0], o.inputs[0]); return m

scn, cam = setup_scene(); add_reference(scn)
ev = sc["timeline"]; spawns = [e for e in ev if e["op"] == "spawn"]; byid = {e["id"]: e for e in spawns}
for ti, Tn in enumerate(TIMES):
    objs = []
    for e in spawns:
        st = actor_state(e, ev, Tn)
        if not st: continue
        x, y = anchor_pos(e, ev, byid)[:2]
        tp = [q for q in ev if q["op"] == "teleport" and q.get("id") == e["id"] and q["t"] <= Tn]
        if tp: x, y = tp[-1]["to"]["world"]
        if e.get("moveTo"):                                            # 투사체: 선형 이동
            tx, ty = (700, 0) if sc["id"] == "shiki_fleet" else (0, 0); d = max(math.hypot(tx - x, ty - y), 1); f = min(st["lt"] * e["moveTo"]["speedPerSec"] / d, 1.0)
            if f >= 1.0 and st["lt"] > 0.05: continue
            x, y = x + (tx - x) * f, y + (ty - y) * f
        pos = Vector((x, y, st["fly"]))
        mdl = sc["models"][e["model"]]; lm = load_model(e["model"]) if mdl.get("folder") else None
        if lm is None and (e.get("substitute", {}).get("ship") or e["model"] == "HumanBattleship"): e = dict(e, model="HumanBattleship")
        scale = e["baseScale"] * st["pct"] / 100.0
        if lm is None:
            objs.append(placeholder(e["model"], pos, scale)); continue
        geo, info, approx, w = lm
        seq = pick_seq(info, "death" if st["dead"] else e.get("anim"))
        dur = max((seq["end"] - seq["start"]) / 1000.0, 0.001); lt = (st["dl"] if st["dead"] else st["lt"]) * st["ts"]
        if seq["looping"] and not st["dead"]: lt = lt % dur
        else: lt = min(lt, dur)
        t_ms = seq["start"] + lt * 1000
        R.TEXDIR = w + "/_tex"
        for o in build_geosets(geo, info, t_ms, scale, approx):
            for m_ in o.data.materials: pass
            for poly in o.data.polygons: pass
            o.location = pos; objs.append(o)
        rnd = random.Random(7 + hash(e["id"]) % 1000)
        for p in info["pre2"]:
            if p["tex"] >= len(geo["textures"]): continue
            parts = simulate(p, info, geo, seq, lt, scale, rnd)
            if not parts: continue
            parts = [(pp + pos, sz, rgba, fr) for pp, sz, rgba, fr in parts]
            tex = geo["textures"][p["tex"]]["path"]; additive = p["filter"] in (1, 3)
            ob = quad_mesh(f"p_{ti}_{e['id']}", parts, p, right, up); ob.data.materials.append(particle_material(f"pm_{len(objs)}", tex, additive)); scn.collection.objects.link(ob); objs.append(ob)
    for o in objs:
        if o.name not in scn.collection.objects: scn.collection.objects.link(o)
    xs = [anchor_pos(e, ev, byid)[0] for e in spawns] or [0]
    cx = 350 if sc["id"] == "shiki_fleet" else (sum(xs) / len(xs) if "world" in (spawns[0]["at"] if spawns else {}) else 0)
    frame_and_render(scn, cam, HALF, Vector((cx, 0, CZ)), f"{OUTD}/{sc['id']}_{ti}.png")
    for o in objs:
        for ch in list(getattr(o, "children_recursive", [])):
            bpy.data.objects.remove(ch)
        bpy.data.objects.remove(o)
print("done", sc["id"], len(TIMES))
