"""원작 MDX 모델의 부가 이펙트 지오셋을 FBX로 뜯는다(2026-10-01, 초월 부가 이펙트).

  ANALYSIS=<analysis.json> blender -b --factory-startup --python Tools/blender/export_mdx_parts.py -- <작업폴더> <출력폴더> <모델.mdx> <지오셋번호,번호…>

- 작업폴더 = mdx_extract/mdx_effect_parts가 MDX·_tex/를 풀어 둔 곳. 출력폴더/<모델>_g<번호>.fbx + Textures/*.png.
- 지오셋마다 메시 하나(층이 여럿이면 층마다 오브젝트 `<모델>_g<번호>_L<층>_<필터>`). **오브젝트 원점 = 붙는 뼈의 pivot** — 날개짓처럼 뼈 둘레로 돌릴 때 그대로 돌리면 된다.
- 단위: 워크3 1 → 0.01m (몸 키 209 → 2.09m). 방향: 워크3 +X(앞)·Z(위) → 유니티 FBX 규약(-90°Z 돌려 앞=-Y, axis_forward=-Z, axis_up=Y).
- 재질 이름 끝: `_add`(가산: 검정=투명, 유니티 Particles/Additive 또는 URP Unlit 가산) · `_cut`(알파 컷) · `_blend`(알파 혼합) · `_team`(팀색 판 — 버릴 것).
- 뼈 애니메이션·파티클·리본은 못 담는다(설명 md에 값으로).
"""
import json
import math
import os
import shutil
import sys

import bpy
from mathutils import Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.abspath(os.path.join(HERE, "..", "w3x")))
import mdx_geo                                                       # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
WORK, OUT, NAME, IDX = args[0], args[1], args[2], [int(x) for x in args[3].split(",")]
os.makedirs(os.path.join(OUT, "Textures"), exist_ok=True)
SC = 0.01
ANALYSIS = json.load(open(os.environ["ANALYSIS"]))
an = next(v for v in ANALYSIS.values() if v.get("file") == NAME)
tag = os.path.splitext(NAME)[0]
model = mdx_geo.parse(open(os.path.join(WORK, NAME), "rb").read())
SUFFIX = {"none": "cut", "transparent": "cut", "blend": "blend", "additive": "add", "addalpha": "add", "modulate": "blend"}

bpy.ops.wm.read_factory_settings(use_empty=True)
rot = __import__("mathutils").Matrix.Rotation(math.radians(-90), 4, "Z")
made = []
for gi in IDX:
    g = model["geosets"][gi]
    pivot = Vector(next(x for x in an["geosets"] if x["index"] == gi)["bone_pivot"])
    mdef = model["materials"][g["material"]]
    for li, layer in enumerate(mdef["layers"]):
        tex = model["textures"][layer["tex"]]
        team = tex["replaceable"] in (1, 2) or not tex["path"] or "team" in tex["path"].lower()
        suf = "team" if team else SUFFIX.get(layer["filter"], "blend")
        name = f"{tag}_g{gi}_L{li}_{suf}"
        me = bpy.data.meshes.new(name)
        verts = [(Vector(v) - pivot) * SC for v in g["verts"]]
        me.from_pydata([tuple(v) for v in verts], [], g["tris"])
        uv = me.uv_layers.new(name="UV")
        for poly in me.polygons:
            for vi, loop in zip(poly.vertices, poly.loop_indices):
                u, v = g["uvs"][vi] if vi < len(g["uvs"]) else (0, 0)
                uv.data[loop].uv = (u, 1.0 - v)
        me.update()
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        ob.location = rot @ (pivot * SC)
        ob.rotation_euler = (0, 0, math.radians(-90))
        mat = bpy.data.materials.new(name)
        mat.use_nodes = True
        bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        if not team:
            f = os.path.join(WORK, "_tex", tex["path"].replace("\\", "_").replace("/", "_") + ".png")
            if os.path.exists(f):
                dst = os.path.join(OUT, "Textures", os.path.basename(f))
                shutil.copy(f, dst)
                tn = mat.node_tree.nodes.new("ShaderNodeTexImage")
                tn.image = bpy.data.images.load(dst, check_existing=True)
                mat.node_tree.links.new(tn.outputs["Color"], bsdf.inputs["Base Color"])
                if suf != "cut":
                    mat.node_tree.links.new(tn.outputs["Alpha"], bsdf.inputs["Alpha"])
        mat.use_backface_culling = False
        me.materials.append(mat)
        made.append(ob)
        print("PART", name, "tex", tex["path"], "pivot", [round(x, 1) for x in pivot], "verts", len(verts))

bpy.ops.object.select_all(action="DESELECT")
for ob in made:
    ob.select_set(True)
dst = os.path.join(OUT, f"{tag}_g{'-'.join(map(str, IDX))}.fbx")
bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
                         mesh_smooth_type="FACE", path_mode="STRIP", embed_textures=False, bake_anim=False)
print("FBX", dst)
