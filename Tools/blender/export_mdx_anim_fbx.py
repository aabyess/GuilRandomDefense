"""원작 MDX 이펙트 모델 → 뼈 애니메이션이 구워진 FBX + 텍스처 PNG + 사이드카 JSON (2026-10-08, 초월 원작 이펙트 이식 1단계).

  blender -b --factory-startup --python Tools/blender/export_mdx_anim_fbx.py -- <작업폴더(mdx_extract 결과)> <출력폴더> <이름.mdx> [<이름.mdx> …]

산출(출력폴더):
  <모델>/<모델>.fbx      아마추어(노드=뼈, 휴식 자세 = pivot) + 지오셋×층 메시(스킨 가중치 = 행렬 그룹 균등) + 시퀀스마다 액션(30fps 구움)
  <모델>/Textures/*.png   쓰는 텍스처(맵 안 진짜 BLP→PNG. 맵 밖 워크3 기본 텍스처는 mdx_extract 근사 그림이라 `approx` 표시)
  <모델>/<모델>.json      FBX에 못 실리는 것: 시퀀스, 메시별 필터·텍스처·알파 곡선(KGAO·KMTA), UV 이동(TXAN), PRE2 파티클·리본 값

단위 워크3 1 = 0.01m. 방향은 export_mdx_parts.py와 같은 규칙(-90°Z, axis_forward=-Z, axis_up=Y).
재질 이름 끝: `_add`(가산) `_cut`(알파 컷) `_blend`(알파 혼합). 🔴 원작(블리자드·중국 모델러) 저작물 — Assets 반입은 PM·사장님 판단.
한계: 보간은 선형(헤르미트·베지어 접선 무시), 뼈 가중치는 균등, 지오셋 알파·층 알파·UV 애니·파티클은 JSON으로만.
"""
import json
import math
import os
import shutil
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.abspath(os.path.join(HERE, "..", "w3x")))
import mdx_anim                                                      # noqa: E402
import mdx_geo                                                       # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
WORK, OUT, NAMES = args[0], args[1], args[2:]
SC = 0.01
FPS = 30
SUFFIX = {"none": "cut", "transparent": "cut", "blend": "blend", "additive": "add", "addalpha": "add", "modulate": "blend", "modulate2x": "blend"}
APPROX = set(json.load(open(os.path.join(WORK, "_approx.json")))) if os.path.exists(os.path.join(WORK, "_approx.json")) else set()


def sample(tr, t_ms, default):
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


def local_delta(n, pivot, t_ms):
    """노드의 pivot 기준 변환(부모 무관) — T(piv)·T·R·S·T(-piv)."""
    tr = n["tracks"]
    T = sample(tr.get("KGTR"), t_ms, (0.0, 0.0, 0.0))
    Rq = sample(tr.get("KGRT"), t_ms, (0.0, 0.0, 0.0, 1.0))
    S = sample(tr.get("KGSC"), t_ms, (1.0, 1.0, 1.0))
    q = Quaternion((Rq[3], Rq[0], Rq[1], Rq[2]))
    q.normalize()
    return (Matrix.Translation(pivot) @ Matrix.Translation(Vector(T)) @ q.to_matrix().to_4x4()
            @ Matrix.Diagonal(Vector((S[0], S[1], S[2], 1.0))) @ Matrix.Translation(-pivot))


def keys_of(tr):
    return [[k[0], list(k[1])] for k in (tr or {}).get("keys", [])]


def convert(name):
    data = open(os.path.join(WORK, name), "rb").read()
    geo = mdx_geo.parse(data)
    info = mdx_anim.describe(data)
    tag = os.path.splitext(name)[0].replace("\\", "__").replace("/", "__")        # war3mapImported\\x.mdx 같은 경로 이름은 폴더 이름에 못 쓴다
    odir = os.path.join(OUT, tag)
    os.makedirs(os.path.join(odir, "Textures"), exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scn = bpy.context.scene
    scn.render.fps = FPS

    nodes = {n["id"]: n for n in info["nodes"]}
    order = []                                                       # 부모 먼저
    seen = set()

    def visit(nid):
        if nid in seen or nid not in nodes:
            return
        seen.add(nid)
        p = nodes[nid]["parent"]
        if p in nodes and p != nid:
            visit(p)
        order.append(nid)
    for nid in sorted(nodes):
        visit(nid)
    piv = {nid: Vector(info["pivots"][nid]) if nid < len(info["pivots"]) else Vector((0, 0, 0)) for nid in nodes}

    # 아마추어
    arm_d = bpy.data.armatures.new(tag + "_arm")
    arm = bpy.data.objects.new(tag, arm_d)
    scn.collection.objects.link(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode="EDIT")
    bname = {}
    for nid in order:
        n = nodes[nid]
        nm = f"n{nid}_" + "".join(c if (c.isascii() and (c.isalnum() or c in "_-")) else "" for c in n["name"])[:30]   # 원작 이름은 GBK 깨짐이 있어 ASCII만, 번호로 유일
        bname[nid] = nm
        eb = arm_d.edit_bones.new(nm)
        eb.head = piv[nid] * SC
        eb.tail = piv[nid] * SC + Vector((0, 0, 0.05))
        p = n["parent"]
        if p in nodes and p != nid:
            eb.parent = arm_d.edit_bones[bname[p]]
    bpy.ops.object.mode_set(mode="OBJECT")
    if not order:                                                    # 뼈 없는 모델: 뼈 하나
        bpy.ops.object.mode_set(mode="EDIT")
        eb = arm_d.edit_bones.new("root")
        eb.head, eb.tail = (0, 0, 0), (0, 0, 0.05)
        bpy.ops.object.mode_set(mode="OBJECT")

    # 메시
    side_meshes = []
    meshes = []
    for gi, g in enumerate(geo["geosets"]):
        mdef = geo["materials"][g["material"]]
        groups, pos = [], 0
        for c in g["mtgc"]:
            groups.append(g["mats"][pos:pos + c])
            pos += c
        for li, layer in enumerate(mdef["layers"]):
            tex = geo["textures"][layer["tex"]]
            team = tex["replaceable"] in (1, 2) or not tex["path"] or "team" in tex["path"].lower()
            if team:
                continue
            suf = SUFFIX.get(layer["filter"], "blend")
            mname = f"{tag}_g{gi}_L{li}_{suf}"
            me = bpy.data.meshes.new(mname)
            me.from_pydata([tuple(Vector(v) * SC) for v in g["verts"]], [], g["tris"])
            uv = me.uv_layers.new(name="UV")
            for poly in me.polygons:
                for vi, loop in zip(poly.vertices, poly.loop_indices):
                    u, v = g["uvs"][vi] if vi < len(g["uvs"]) else (0, 0)
                    uv.data[loop].uv = (u, 1.0 - v)
            me.update()
            ob = bpy.data.objects.new(mname, me)
            scn.collection.objects.link(ob)
            ob.parent = arm
            vgs = {}
            for vi in range(len(g["verts"])):
                gix = g["gndx"][vi] if vi < len(g["gndx"]) else 0
                bones = [b for b in (groups[gix] if gix < len(groups) else []) if b in bname]
                if not bones:
                    bones = [order[0]] if order else []
                for b in bones:
                    nm = bname.get(b, "root")
                    vg = vgs.get(nm) or ob.vertex_groups.new(name=nm)
                    vgs[nm] = vg
                    vg.add([vi], 1.0 / len(bones), "REPLACE")
            if not order:
                vg = ob.vertex_groups.new(name="root")
                vg.add(list(range(len(g["verts"]))), 1.0, "REPLACE")
            md = ob.modifiers.new("Armature", "ARMATURE")
            md.object = arm
            # 재질
            mat = bpy.data.materials.new(mname)
            mat.use_nodes = True
            bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
            f = os.path.join(WORK, "_tex", tex["path"].replace("\\", "_").replace("/", "_") + ".png")
            texfile = None
            if os.path.exists(f):
                dst = os.path.join(odir, "Textures", os.path.basename(f))
                shutil.copy(f, dst)
                texfile = os.path.basename(f)
                tn = mat.node_tree.nodes.new("ShaderNodeTexImage")
                tn.image = bpy.data.images.load(dst, check_existing=True)
                mat.node_tree.links.new(tn.outputs["Color"], bsdf.inputs["Base Color"])
                if suf != "cut":
                    mat.node_tree.links.new(tn.outputs["Alpha"], bsdf.inputs["Alpha"])
            mat.use_backface_culling = False
            me.materials.append(mat)
            meshes.append(ob)
            ga = next((x for x in info["geoa"] if x["geoset"] == gi), None)
            la = next((x for x in info["layer_anims"] if x["material"] == g["material"] and x["layer"] == li), None)
            ta = next((x for x in info["txan"] if x.get("id") == layer.get("texanim")), None) if layer.get("texanim", 0xFFFFFFFF) not in (0xFFFFFFFF, -1) else None
            side_meshes.append(dict(
                mesh=mname, geoset=gi, layer=li, filter=layer["filter"], suffix=suf, texture=tex["path"], textureFile=texfile,
                approxTexture=tex["path"] in APPROX, staticAlpha=layer["alpha"],
                geosetAlphaStatic=(ga or {}).get("alpha"), geosetAlphaKeys=keys_of(((ga or {}).get("tracks") or {}).get("KGAO")),
                layerAlphaKeys=keys_of(((la or {}).get("tracks") or {}).get("KMTA")),
                uvAnim=(json.loads(json.dumps(ta, default=list)) if ta else None)))

    # 애니메이션 — 시퀀스마다 액션
    arm.animation_data_create()
    pbs = {nid: arm.pose.bones[bname[nid]] for nid in order}
    rest = {nid: arm_d.bones[bname[nid]].matrix_local.copy() for nid in order}
    actions = []
    seqs = info["sequences"] or [dict(name="stand", start=0, end=1000, looping=True)]
    for si, seq in enumerate(seqs):
        seq["name"] = f"s{si}_" + "".join(c if (c.isascii() and (c.isalnum() or c in "_-")) else "" for c in seq["name"].encode("latin1", "ignore").decode("gbk", "ignore") or "")[:24]
        dur = max(seq["end"] - seq["start"], 1)
        nfr = max(2, int(math.ceil(dur / 1000 * FPS)) + 1)
        act = bpy.data.actions.new(f"{tag}|{seq['name']}")
        arm.animation_data.action = act
        for fi in range(nfr):
            t_ms = seq["start"] + min(dur, fi * 1000 / FPS)
            world = {}
            for nid in order:
                n = nodes[nid]
                tr = n["tracks"]
                T = Vector(sample(tr.get("KGTR"), t_ms, (0.0, 0.0, 0.0))) * SC
                Rq = sample(tr.get("KGRT"), t_ms, (0.0, 0.0, 0.0, 1.0))
                S = [max(abs(x), 1e-3) for x in sample(tr.get("KGSC"), t_ms, (1.0, 1.0, 1.0))]   # 0 스케일(숨김)은 역행렬이 없다 → 아주 작게
                q = Quaternion((Rq[3], Rq[0], Rq[1], Rq[2]))
                q.normalize()
                p0 = piv[nid] * SC
                loc = (Matrix.Translation(p0) @ Matrix.Translation(T) @ q.to_matrix().to_4x4()
                       @ Matrix.Diagonal(Vector((S[0], S[1], S[2], 1.0))) @ Matrix.Translation(-p0))
                par = n["parent"]
                world[nid] = (world[par] @ loc) if (par in world and par != nid) else loc
            pose = {nid: world[nid] @ rest[nid] for nid in order}   # 아마추어 공간 포즈 행렬
            for nid in order:
                par = nodes[nid]["parent"]
                if par in pose and par != nid:
                    basis = (rest[par].inverted() @ rest[nid]).inverted() @ pose[par].inverted() @ pose[nid]
                else:
                    basis = rest[nid].inverted() @ pose[nid]
                l, r, s = basis.decompose()
                pb = pbs[nid]
                pb.rotation_mode = "QUATERNION"
                pb.location, pb.rotation_quaternion, pb.scale = l, r, s
                pb.keyframe_insert("location", frame=fi + 1, group=bname[nid])
                pb.keyframe_insert("rotation_quaternion", frame=fi + 1, group=bname[nid])
                pb.keyframe_insert("scale", frame=fi + 1, group=bname[nid])
        act.use_fake_user = True
        actions.append(dict(name=seq["name"], action=act.name, frames=nfr, fps=FPS, seconds=round(dur / 1000, 3), looping=seq["looping"]))
    arm.animation_data.action = bpy.data.actions[actions[0]["action"]]

    # 방향 규칙 — 아마추어를 -90°Z (메시는 자식)
    arm.rotation_euler = (0, 0, math.radians(-90))

    bpy.ops.object.select_all(action="DESELECT")
    arm.select_set(True)
    for ob in meshes:
        ob.select_set(True)
    dst = os.path.join(odir, tag + ".fbx")
    bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
                             path_mode="STRIP", embed_textures=False, bake_anim=True, bake_anim_use_all_actions=True,
                             bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
                             add_leaf_bones=False)
    side = dict(
        model=name, scaleNote="워크3 1 = 0.01m", sequences=actions, meshes=side_meshes,
        pre2=[dict(node=p["node"]["name"], speed=p["speed"], variation=p["variation"], latitude=p["latitude"], gravity=p["gravity"],
                   lifespan=p["lifespan"], rate=p["rate"], width=p["width"], length=p["length"], filter=p["filter"], rows=p["rows"], cols=p["cols"],
                   mid=p["mid"], colors=[list(c) for c in p["colors"]], alpha=p["alpha"], scale=list(p["scale"]),
                   texture=(geo["textures"][p["tex"]]["path"] if p["tex"] < len(geo["textures"]) else ""),
                   emissionKeys=keys_of(p["tracks"].get("KP2E")), visKeys=keys_of(p["tracks"].get("KP2V"))) for p in info["pre2"]],
        prem=[dict(node=p["node"]["name"], model=p["model"], rate=p["rate"], lifespan=p["lifespan"], speed=p["speed"]) for p in info["prem"]],
        ribbons=len(info["ribbons"]), nodes=[dict(name=bname[nid], parent=bname.get(nodes[nid]["parent"])) for nid in order])
    json.dump(side, open(os.path.join(odir, tag + ".json"), "w"), ensure_ascii=False, indent=1, default=list)
    print("OK", name, "bones", len(order), "meshes", len(meshes), "seqs", [a["name"] for a in actions], "pre2", len(info["pre2"]), "fbx", dst)


for nm in NAMES:
    try:
        convert(nm)
    except Exception as e:                                           # noqa: BLE001
        import traceback
        traceback.print_exc()
        print("FAIL", nm, repr(e))
