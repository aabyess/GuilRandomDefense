"""워크3 MDX 모델을 Blender로 그려 PNG로 남긴다(원작 오라·날개·초월 모델 구경용, 2026-10-01).

  1) python3 Tools/w3x/mdx_extract.py <ord.mpq> <작업폴더> <이름.mdx> [...]      (MPQ에서 MDX·텍스처 PNG를 꺼낸다 — Blender 파이썬엔 mpyq·Pillow가 없다)
  2) blender -b --factory-startup --python Tools/blender/render_mdx.py -- <작업폴더> <출력폴더> <이름.mdx> [...]

- MDX는 Tools/w3x/mdx_geo.py로 읽는다(지오셋·재질 층). 텍스처는 1)이 PNG로 풀어 둔 것을 쓴다.
  **맵 안에 없는 워크3 기본 텍스처(Textures\\Blue_Glow2 등)는 비슷한 모양을 그려 대신 쓴다**(작업폴더/_approx.json에 목록) —
  원작과 모양·색이 정확히 같지는 않다.
- 층 필터: none/transparent(알파 컷)/blend(알파 혼합)/additive·addalpha(가산 — 검정=투명으로 근사)/modulate.
- 팀 색(ReplaceableTextures\\TeamColor, replaceable 1·2)은 붉은 단색.
- 카메라 셋: 앞비스듬히(모델 앞 +X) · 뒤 · 위. 워크3 모델은 +X가 앞, Z가 위.
- 안 그리는 것: 파티클(PRE2)·리본·뼈 애니메이션. 그래서 파티클이 핵심인 모델은 모습이 덜 그려진다(개수는 표시).
"""
import math
import os
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.abspath(os.path.join(HERE, "..", "w3x")))
import mdx_geo                                                       # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
WORK, OUT, NAMES = args[0], args[1], args[2:]
os.makedirs(OUT, exist_ok=True)
TEXDIR = os.path.join(WORK, "_tex")
import json as _json
APPROX = set(_json.load(open(os.path.join(WORK, "_approx.json")))) if os.path.exists(os.path.join(WORK, "_approx.json")) else set()


def load_texture(entry):
    """MDX 텍스처 항목 → bpy 이미지(없으면 None). 팀 색·빈 경로는 None → 호출부가 단색."""
    path, rid = entry["path"], entry["replaceable"]
    if rid in (1, 2) or "teamcolor" in path.lower() or "teamglow" in path.lower() or not path:
        return None
    f = os.path.join(TEXDIR, path.replace("\\", "_").replace("/", "_") + ".png")
    return bpy.data.images.load(f, check_existing=True) if os.path.exists(f) else None


def make_material(name, layer, tex_entry):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    img = load_texture(tex_entry)
    team = img is None
    if not team:
        tn = nt.nodes.new("ShaderNodeTexImage")
        tn.image = img
        tn.interpolation = "Linear"
        tn.extension = "REPEAT"
        color, alpha = tn.outputs["Color"], tn.outputs["Alpha"]
    mode = layer["filter"]
    if mode == "blend" and not team:
        # 알파가 전부 255인 텍스처를 blend로 쓰면 검은 판이 된다 — 원작에선 층 알파 애니메이션(KMTA, 안 읽음)이 숨기거나 빛 번짐용이라 가산으로 근사한다.
        import numpy as np
        px = np.empty(len(img.pixels), dtype=np.float32)
        img.pixels.foreach_get(px)
        if px[3::4].min() > 0.99:
            mode = "additive"
    if mode in ("additive", "addalpha"):
        # 가산: 검정 = 투명. 밝기(최댓값)를 섞는 비율로 쓴다. addalpha는 텍스처 알파도 곱한다.
        em = nt.nodes.new("ShaderNodeEmission")
        tr = nt.nodes.new("ShaderNodeBsdfTransparent")
        mix = nt.nodes.new("ShaderNodeMixShader")
        sep = nt.nodes.new("ShaderNodeSeparateColor")
        mx1 = nt.nodes.new("ShaderNodeMath"); mx1.operation = "MAXIMUM"
        mx2 = nt.nodes.new("ShaderNodeMath"); mx2.operation = "MAXIMUM"
        if team:
            em.inputs["Color"].default_value = (0.9, 0.1, 0.1, 1)
            mix.inputs["Fac"].default_value = 0.8
        else:
            nt.links.new(color, em.inputs["Color"])
            nt.links.new(color, sep.inputs["Color"])
            nt.links.new(sep.outputs[0], mx1.inputs[0]); nt.links.new(sep.outputs[1], mx1.inputs[1])
            nt.links.new(mx1.outputs[0], mx2.inputs[0]); nt.links.new(sep.outputs[2], mx2.inputs[1])
            fac = mx2.outputs[0]
            if mode == "addalpha":
                mul = nt.nodes.new("ShaderNodeMath"); mul.operation = "MULTIPLY"
                nt.links.new(fac, mul.inputs[0]); nt.links.new(alpha, mul.inputs[1])
                fac = mul.outputs[0]
            nt.links.new(fac, mix.inputs["Fac"])
        em.inputs["Strength"].default_value = 1.6
        nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2])
        nt.links.new(mix.outputs[0], out.inputs["Surface"])
        mat.surface_render_method = "BLENDED"
    else:
        pr = nt.nodes.new("ShaderNodeBsdfPrincipled")
        if team:
            pr.inputs["Base Color"].default_value = (0.75, 0.1, 0.1, 1)
        else:
            nt.links.new(color, pr.inputs["Base Color"])
            pr.inputs["Roughness"].default_value = 0.85
            if mode == "transparent":
                gt = nt.nodes.new("ShaderNodeMath"); gt.operation = "GREATER_THAN"; gt.inputs[1].default_value = 0.75
                nt.links.new(alpha, gt.inputs[0]); nt.links.new(gt.outputs[0], pr.inputs["Alpha"])
                mat.surface_render_method = "DITHERED"
            elif mode == "blend":
                nt.links.new(alpha, pr.inputs["Alpha"])
                mat.surface_render_method = "BLENDED"
        nt.links.new(pr.outputs[0], out.inputs["Surface"])
    mat.use_backface_culling = False
    return mat


def build(model, tag, only=None):
    objs = []
    for gi, g in enumerate(model["geosets"]):
        if only is not None and gi not in only:
            continue
        mdef = model["materials"][g["material"]]
        for li, layer in enumerate(mdef["layers"]):
            me = bpy.data.meshes.new(f"{tag}_{gi}_{li}")
            me.from_pydata(g["verts"], [], g["tris"])
            uv = me.uv_layers.new(name="UV")
            for poly in me.polygons:
                for vi, loop in zip(poly.vertices, poly.loop_indices):
                    u, v = g["uvs"][vi] if vi < len(g["uvs"]) else (0, 0)
                    uv.data[loop].uv = (u, 1.0 - v)                     # MDX는 v가 아래로
            me.update()
            ob = bpy.data.objects.new(me.name, me)
            bpy.context.scene.collection.objects.link(ob)
            ob.data.materials.append(make_material(f"{tag}_m{gi}_{li}", layer, model["textures"][layer["tex"]]))
            objs.append(ob)
    return objs


def bounds(objs):
    pts = [o.matrix_world @ v.co for o in objs for v in o.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def render_views(objs, base, view_names=None):
    scn = bpy.context.scene
    try:
        scn.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x = scn.render.resolution_y = 700
    scn.render.film_transparent = False
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (0.04, 0.045, 0.06, 1)
    bg.inputs[1].default_value = 1.0
    scn.world = w
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 3.0
    scn.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(55), 0, math.radians(35))
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scn.collection.objects.link(cam)
    scn.camera = cam
    cam.data.type = "ORTHO"
    lo, hi = bounds(objs)
    c = (lo + hi) / 2
    size = max((hi - lo).length, 1e-3) * 0.82
    cam.data.ortho_scale = size
    cam.data.clip_start = size * 0.01
    cam.data.clip_end = size * 20
    views = (("앞비스듬히", Vector((1.0, -0.9, 0.55))), ("뒤", Vector((-1.0, 0.25, 0.25))), ("위", Vector((0.001, 0.0, 1.0))))
    for name, d in views:
        if view_names and name not in view_names:
            continue
        d = d.normalized()
        cam.location = c + d * size * 3
        cam.rotation_euler = (-d).to_track_quat("-Z", "Y").to_euler()
        scn.render.filepath = os.path.join(OUT, f"{base}_{name}.png")
        bpy.ops.render.render(write_still=True)
    return lo, hi


report = []
ANALYSIS = _json.load(open(os.environ["ANALYSIS"])) if os.environ.get("ANALYSIS") else {}
PART_CLASSES = {"effect", "teamglow", "blendplane", "conditional"}
for name in NAMES:
    fpath = os.path.join(WORK, name)
    data = open(fpath, "rb").read() if os.path.exists(fpath) else None
    if data is None:
        report.append((name, "작업폴더에 없음"))
        print("MDX", name, "없음")
        continue
    model = mdx_geo.parse(data)
    tag = os.path.splitext(name)[0]
    an = next((v for v in ANALYSIS.values() if v.get("file") == name), None)
    if an:
        part_idx = {g["index"] for g in an["geosets"] if g["cls"] in PART_CLASSES}
        has_parts = bool(part_idx or an.get("particles") or an.get("ribbons"))
        bpy.ops.wm.read_factory_settings(use_empty=True)
        objs = build(model, tag)
        lo, hi = render_views(objs, tag + "_몸+부품", ("앞비스듬히", "뒤"))
        if part_idx:
            bpy.ops.wm.read_factory_settings(use_empty=True)
            objs = build(model, tag, part_idx)
            render_views(objs, tag + "_부품만", ("앞비스듬히", "위"))
        print("MDX", name, "parts", sorted(part_idx), "ext", tuple(round(v, 1) for v in (hi - lo)))
        report.append((name, sorted(part_idx)))
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    objs = build(model, tag)
    lo, hi = render_views(objs, tag)
    ext = tuple(round(v, 1) for v in (hi - lo))
    layers = sorted({(l["filter"], model["textures"][l["tex"]]["path"] or "team") for m in model["materials"] for l in m["layers"]})
    print("MDX", name, "ext", ext, "counts", model["counts"], "approx", sorted(k for k in APPROX))
    report.append((name, ext, model["counts"], layers))
import json
json.dump(report, open(os.path.join(OUT, "_report.json"), "w"), ensure_ascii=False, indent=1)
