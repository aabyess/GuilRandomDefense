"""원작 스킨 교체 모델 Unity 반입용 재수출 (2026-10-09, blender3).

  blender -b --factory-startup --python Tools/blender/swap_refix_b3.py -- <교체 폴더(~/GRD_skin_swap/<유닛>_<원작>)> [<작업폴더 work>]

입력: <폴더>/model/<모델>.json(원본 변환 결과) · <폴더>/clip_map.json · 원본 MDX(작업폴더/<모델>.mdx — 기본 ~/GRD_motion_trial/original_skin/work, 없으면 original_vfx/work)
산출: <폴더>/model_fixed/<모델>.fbx · Textures/ · <모델>.json(원본 json + fixed 블록) · scale_audit.csv · scale_audit.json
export_mdx_anim_fbx.py와 달라진 점
  ⓐ 휴식(바인드) 포즈 = Stand 첫 프레임. 뼈 휴식 행렬 = pose0(회전+이동만), 메시 정점 = Σ(1/n)·world0·v 로 굽고,
     액션은 pose_f' = pose_f · pose0⁻¹ · 새휴식 으로 다시 굽는다(스킨 행렬 pose_f'·새휴식⁻¹ = pose_f·pose0⁻¹ 이므로 화면은 같다).
     Stand에서 스케일 <0.05(숨김)인 뼈는 그 뼈만 옛 휴식 그대로(world0 계산에서 변환 생략) — 안 그러면 정점이 한 점으로 뭉개져 되돌릴 수 없다.
  ⓑ 클립별 표시: json fixed.clipVisibility[클립][메시] (지오셋 알파 KGAO가 클립 안 내내 0이면 false) — 지오셋·층마다 이미 별도 메시.
  ⓒ 스케일 검수: 휴식·클립 프레임마다 지오셋별 경계 최대값 → scale_audit.csv, 몸(Stand 보이는 메시 합집합)의 3배 넘으면 경고.
  ⓓ 뼈 스케일: 절댓값 1e-3 아래는 1e-3(0 키가 역행렬을 깨뜨림). 몇 키가 걸렸는지 fixed.minScaleKeys에 센다.
"""
import csv
import json
import math
import os
import shutil
import sys

import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.abspath(os.path.join(HERE, "..", "w3x")))
import mdx_anim                                                      # noqa: E402
import mdx_geo                                                       # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
FOLD = os.path.abspath(args[0])
H = os.path.expanduser("~")
SC = 0.01
FPS = 30
HIDE_S = 0.05           # Stand에서 이보다 작은 스케일 축이 있으면 숨김 뼈로 본다
SUFFIX = {"none": "cut", "transparent": "cut", "blend": "blend", "additive": "add", "addalpha": "add", "modulate": "blend", "modulate2x": "blend"}

mdir = os.path.join(FOLD, "model")
J0 = json.load(open([os.path.join(mdir, f) for f in os.listdir(mdir) if f.endswith(".json")][0]))
CM = json.load(open(os.path.join(FOLD, "clip_map.json")))
mdx_name = J0["model"]
work = None
for w in ([args[1]] if len(args) > 1 else []) + [H + "/GRD_motion_trial/original_skin/work", H + "/GRD_motion_trial/original_vfx/work"]:
    if os.path.exists(os.path.join(w, mdx_name)):
        work = w
        break
assert work, "MDX 못 찾음 " + mdx_name


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


data = open(os.path.join(work, mdx_name), "rb").read()
geo = mdx_geo.parse(data)
info = mdx_anim.describe(data)
tag = os.path.splitext(mdx_name)[0].replace("\\", "__").replace("/", "__")
odir = os.path.join(FOLD, "model_fixed")
shutil.rmtree(odir, ignore_errors=True)
os.makedirs(os.path.join(odir, "Textures"), exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
scn = bpy.context.scene
scn.render.fps = FPS

nodes = {n["id"]: n for n in info["nodes"]}
order, seen = [], set()


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
bname = {}
for nid in order:
    n = nodes[nid]
    bname[nid] = f"n{nid}_" + "".join(c if (c.isascii() and (c.isalnum() or c in "_-")) else "" for c in n["name"])[:30]


MINS_KEYS = [0]
BIG = [0]
MAXSTEP = [0.0]      # 한 프레임 사이 최대 회전각(도) — 60° 넘으면 FBX를 1/4프레임 간격으로 구워 오일러 보간이 최단 경로에 가깝게 한다


def local_m(nid, t_ms, skip=False):
    n = nodes[nid]
    p0 = piv[nid] * SC
    if skip:
        return Matrix.Identity(4)
    tr = n["tracks"]
    T = Vector(sample(tr.get("KGTR"), t_ms, (0.0, 0.0, 0.0))) * SC
    Rq = sample(tr.get("KGRT"), t_ms, (0.0, 0.0, 0.0, 1.0))
    Sraw = sample(tr.get("KGSC"), t_ms, (1.0, 1.0, 1.0))
    S = []
    for x in Sraw:
        if abs(x) < 1e-3:
            MINS_KEYS[0] += 1
        S.append(max(abs(x), 1e-3))
    q = Quaternion((Rq[3], Rq[0], Rq[1], Rq[2]))
    q.normalize()
    return (Matrix.Translation(p0) @ Matrix.Translation(T) @ q.to_matrix().to_4x4()
            @ Matrix.Diagonal(Vector((S[0], S[1], S[2], 1.0))) @ Matrix.Translation(-p0))


def world_all(t_ms, skipset=()):
    w = {}
    for nid in order:
        loc = local_m(nid, t_ms, nid in skipset)
        par = nodes[nid]["parent"]
        w[nid] = (w[par] @ loc) if (par in w and par != nid) else loc
    return w


# 시퀀스 이름 정리(원본 exporter와 같은 규칙)
seqs = info["sequences"] or [dict(name="stand", start=0, end=1000, looping=True)]
for si, seq in enumerate(seqs):
    seq["name"] = f"s{si}_" + "".join(c if (c.isascii() and (c.isalnum() or c in "_-")) else "" for c in seq["name"].encode("latin1", "ignore").decode("gbk", "ignore") or "")[:24]
stand_i = next((i for i, s in enumerate(seqs) if "stand" in s["name"].lower() and "ready" not in s["name"].lower() and "ready" not in s["name"]), 0)
T0 = seqs[stand_i]["start"]

# Stand에서 숨김(스케일 ~0) 뼈
hidden_nodes = set()
for nid in order:
    S = sample(nodes[nid]["tracks"].get("KGSC"), T0, (1.0, 1.0, 1.0))
    if min(abs(x) for x in S) < HIDE_S:
        hidden_nodes.add(nid)
MINS_KEYS[0] = 0
world0 = world_all(T0, hidden_nodes)

# 아마추어 — 먼저 옛 휴식(pivot)으로 만들어 rest_old를 얻고, 뼈 행렬을 pose0로 바꾼다
arm_d = bpy.data.armatures.new(tag + "_arm")
arm = bpy.data.objects.new(tag, arm_d)
scn.collection.objects.link(arm)
bpy.context.view_layer.objects.active = arm
bpy.ops.object.mode_set(mode="EDIT")
for nid in order:
    eb = arm_d.edit_bones.new(bname[nid])
    eb.head = piv[nid] * SC
    eb.tail = piv[nid] * SC + Vector((0, 0, 0.05))
    p = nodes[nid]["parent"]
    if p in nodes and p != nid:
        eb.parent = arm_d.edit_bones[bname[p]]
bpy.ops.object.mode_set(mode="OBJECT")
rest_old = {nid: arm_d.bones[bname[nid]].matrix_local.copy() for nid in order}
pose0 = {nid: world0[nid] @ rest_old[nid] for nid in order}
bpy.ops.object.mode_set(mode="EDIT")
for nid in order:
    M = pose0[nid]
    loc, rot, _s = M.decompose()
    arm_d.edit_bones[bname[nid]].matrix = Matrix.Translation(loc) @ rot.to_matrix().to_4x4()
bpy.ops.object.mode_set(mode="OBJECT")
rest = {nid: arm_d.bones[bname[nid]].matrix_local.copy() for nid in order}
for nid in order:                                  # 새 휴식이 의도한 행렬과 같은지(roll·길이 변형 확인)
    loc, rot, _s = pose0[nid].decompose()
    want = Matrix.Translation(loc) @ rot.to_matrix().to_4x4()
    err = max(abs(rest[nid][i][j] - want[i][j]) for i in range(4) for j in range(4))
    assert err < 5e-3, ("휴식 행렬 어긋남", bname[nid], err)

# 메시 — 정점은 world0로 굽는다
meshes, side_meshes, geoset_meshes = [], [], {}
audit_pre = {}          # geoset → (verts np, gndx, groups)
tex_src = {}
for gi, g in enumerate(geo["geosets"]):
    mdef = geo["materials"][g["material"]]
    groups, pos = [], 0
    for c in g["mtgc"]:
        groups.append(g["mats"][pos:pos + c])
        pos += c
    nv = len(g["verts"])
    gx = [g["gndx"][vi] if vi < len(g["gndx"]) else 0 for vi in range(nv)]
    vb = []
    for vi in range(nv):
        bones = [b for b in (groups[gx[vi]] if gx[vi] < len(groups) else []) if b in bname] or ([order[0]] if order else [])
        v = Vector(g["verts"][vi]) * SC
        if bones:
            acc = Vector((0, 0, 0))
            for b in bones:
                acc += world0[b] @ v
            vb.append(acc / len(bones))
        else:
            vb.append(v)
    audit_pre[gi] = (np.array([tuple(Vector(v) * SC) for v in g["verts"]]), gx, groups)
    for li, layer in enumerate(mdef["layers"]):
        tex = geo["textures"][layer["tex"]]
        team = tex["replaceable"] in (1, 2) or not tex["path"] or "team" in tex["path"].lower()
        if team:
            continue
        suf = SUFFIX.get(layer["filter"], "blend")
        mname = f"{tag}_g{gi}_L{li}_{suf}"
        me = bpy.data.meshes.new(mname)
        me.from_pydata([tuple(v) for v in vb], [], g["tris"])
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
        for vi in range(nv):
            bones = [b for b in (groups[gx[vi]] if gx[vi] < len(groups) else []) if b in bname] or ([order[0]] if order else [])
            for b in bones:
                nm = bname[b]
                vg = vgs.get(nm) or ob.vertex_groups.new(name=nm)
                vgs[nm] = vg
                vg.add([vi], 1.0 / len(bones), "REPLACE")
        md = ob.modifiers.new("Armature", "ARMATURE")
        md.object = arm
        mat = bpy.data.materials.new(mname)
        mat.use_nodes = True
        bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
        bn = tex["path"].replace("\\", "_").replace("/", "_") + ".png"
        cand = [os.path.join(mdir, "Textures", bn), os.path.join(work, "_tex", bn)]
        f = next((c for c in cand if os.path.exists(c)), None)
        texfile = None
        if f:
            dst = os.path.join(odir, "Textures", bn)
            shutil.copy(f, dst)
            texfile = bn
            tn = mat.node_tree.nodes.new("ShaderNodeTexImage")
            tn.image = bpy.data.images.load(dst, check_existing=True)
            mat.node_tree.links.new(tn.outputs["Color"], bsdf.inputs["Base Color"])
            if suf != "cut":
                mat.node_tree.links.new(tn.outputs["Alpha"], bsdf.inputs["Alpha"])
        mat.use_backface_culling = False
        me.materials.append(mat)
        meshes.append(ob)
        geoset_meshes.setdefault(gi, []).append(mname)
        ga = next((x for x in info["geoa"] if x["geoset"] == gi), None)
        side_meshes.append(dict(mesh=mname, geoset=gi, layer=li, texture=tex["path"], textureFile=texfile, ga=ga))

# 애니메이션 — 새 휴식 기준으로 다시 굽는다. 동시에 지오셋별 경계도 잰다.
arm.animation_data_create()
pbs = {nid: arm.pose.bones[bname[nid]] for nid in order}
pose0_inv = {nid: pose0[nid].inverted() for nid in order}
actions = []
audit = []          # (clip, frame, geoset, maxdim, bbox)
bind_verts = {gi: audit_pre[gi][0] for gi in audit_pre}
clipvis = {}
clipvis_any = {}
for si, seq in enumerate(seqs):
    dur = max(seq["end"] - seq["start"], 1)
    nfr = max(2, int(math.ceil(dur / 1000 * FPS)) + 1)
    act = bpy.data.actions.new(f"{tag}|{seq['name']}")
    arm.animation_data.action = act
    prevq_raw = {}
    prevq = {}                                       # 뼈별 직전 프레임 쿼터니언 — 부호를 맞춰(내적 ≥ 0) 최단 경로 보간이 되게 한다(10-09 가프 머리 뒤집힘 조사)
    for fi in range(nfr):
        t_ms = seq["start"] + min(dur, fi * 1000 / FPS)
        world = world_all(t_ms)
        pose = {nid: (world[nid] @ rest_old[nid]) @ pose0_inv[nid] @ rest[nid] for nid in order}
        for nid in order:
            par = nodes[nid]["parent"]
            if par in pose and par != nid:
                basis = (rest[par].inverted() @ rest[nid]).inverted() @ pose[par].inverted() @ pose[nid]
            else:
                basis = rest[nid].inverted() @ pose[nid]
            l, r, s = basis.decompose()
            pb = pbs[nid]
            pb.rotation_mode = "QUATERNION"
            if nid in prevq and prevq[nid].dot(r) < 0:
                r = Quaternion((-r.w, -r.x, -r.y, -r.z))
            if nid in prevq_raw:
                MAXSTEP[0] = max(MAXSTEP[0], math.degrees(2 * math.acos(min(1.0, abs(prevq_raw[nid].dot(r))))))
            prevq_raw[nid] = r.copy()
            prevq[nid] = r.copy()
            BIG[0] = max(BIG[0], 0)
            pb.location, pb.rotation_quaternion, pb.scale = l, r, s
            pb.keyframe_insert("location", frame=fi + 1, group=bname[nid])
            pb.keyframe_insert("rotation_quaternion", frame=fi + 1, group=bname[nid])
            pb.keyframe_insert("scale", frame=fi + 1, group=bname[nid])
        # 경계(원본 정점 → 이 프레임; 새 FBX와 같은 화면)
        for gi, (v0, gx, groups) in audit_pre.items():
            mats = []
            for grp in groups:
                bl = [b for b in grp if b in world]
                mats.append(sum((np.array(world[b]) for b in bl), np.zeros((4, 4))) / len(bl) if bl else np.eye(4))
            vh = np.hstack([v0, np.ones((len(v0), 1))])
            Mv = np.array([mats[x] if x < len(mats) else np.eye(4) for x in gx])
            vp = np.einsum("nij,nj->ni", Mv, vh)[:, :3]
            lo, hi = vp.min(0), vp.max(0)
            audit.append((seq["name"], fi, gi, float((hi - lo).max()), [round(float(x), 3) for x in (hi - lo)]))
    act.use_fake_user = True
    actions.append(dict(name=seq["name"], action=act.name, frames=nfr, fps=FPS, seconds=round(dur / 1000, 3), looping=seq["looping"]))
    # 표시 여부 — vis: 시퀀스 시작 시각 알파(지오셋×층, skin_swap_pack과 같은 규칙 = clip_map.hiddenMeshes) · vis_any: 클립 안 어느 때라도 보임
    def alpha_at(keys, t, default):
        if not keys or t < keys[0][0]:
            return default
        v = keys[0][1][0]
        for k in keys:
            if k[0] <= t:
                v = k[1][0]
        return v
    vis, vis_any = {}, {}
    for sm in side_meshes:
        jm = next((m for m in J0["meshes"] if m["mesh"] == sm["mesh"]), None) or {}
        gst = jm.get("geosetAlphaStatic") if jm.get("geosetAlphaStatic") is not None else 1.0
        vis[sm["mesh"]] = bool(alpha_at(jm.get("geosetAlphaKeys"), seq["start"], gst) * alpha_at(jm.get("layerAlphaKeys"), seq["start"], 1.0) >= 0.05)
        ga = sm["ga"]
        inside = [k[1][0] for k in ((ga or {}).get("tracks") or {}).get("KGAO", {}).get("keys", []) if seq["start"] <= k[0] <= seq["end"]]
        vis_any[sm["mesh"]] = bool(vis[sm["mesh"]] or (inside and max(inside) >= 0.05))
    clipvis_any[seq["name"]] = vis_any
    clipvis[seq["name"]] = vis
arm.animation_data.action = bpy.data.actions[actions[0]["action"]]

# 검수 표
body_dims = [a[3] for a in audit if a[0] == seqs[stand_i]["name"] and a[1] == 0 and any(clipvis[a[0]][m] for m in geoset_meshes.get(a[2], []))]
body_union = None
lo_all, hi_all = [], []
bodymax = 0.0
# 몸 크기 = Stand 첫 프레임 보이는 지오셋 합집합의 최대 치수
sv = {}
w0 = world_all(T0)
for gi, (v0, gx, groups) in audit_pre.items():
    if not any(clipvis[seqs[stand_i]["name"]][m] for m in geoset_meshes.get(gi, [])):
        continue
    mats = [sum((np.array(w0[b]) for b in grp if b in w0), np.zeros((4, 4))) / max(len([b for b in grp if b in w0]), 1) for grp in groups]
    vh = np.hstack([v0, np.ones((len(v0), 1))])
    Mv = np.array([mats[x] if x < len(mats) else np.eye(4) for x in gx])
    vp = np.einsum("nij,nj->ni", Mv, vh)[:, :3]
    lo_all.append(vp.min(0))
    hi_all.append(vp.max(0))
bodymax = float((np.max(hi_all, 0) - np.min(lo_all, 0)).max()) if lo_all else 1.0
warn = []
per_clip_max = {}
for (cl, fi, gi, md_, bb) in audit:
    vis = any(clipvis[cl][m] for m in geoset_meshes.get(gi, []))
    key = (cl, gi)
    cur = per_clip_max.get(key, (0, 0))
    if md_ > cur[0]:
        per_clip_max[key] = (md_, fi)
rows = []
for (cl, gi), (mx, fi) in sorted(per_clip_max.items(), key=lambda x: (x[0][0], x[0][1])):
    vis = any(clipvis[cl][m] for m in geoset_meshes.get(gi, []))
    ratio = mx / bodymax
    flag = "경고" if (ratio > 3 and vis) else ("숨김중 큼" if ratio > 3 else "")
    rows.append([cl, f"g{gi}", round(mx, 3), fi, round(ratio, 2), "보임" if vis else "숨김", flag])
    if flag == "경고":
        warn.append(f"{cl} g{gi} {mx:.2f}m = 몸의 {ratio:.1f}배")
restrows = []
for gi, (v0, gx, groups) in audit_pre.items():
    ww = world_all(T0)
    pass
with open(os.path.join(odir, "scale_audit.csv"), "w", newline="") as fh:
    wr = csv.writer(fh)
    wr.writerow(["클립", "지오셋", "프레임 중 최대 치수(m)", "그 프레임", f"몸 대비(몸={bodymax:.3f}m)", "그 클립 표시", "경고"])
    wr.writerows(rows)

bpy.ops.object.select_all(action="DESELECT")
arm.rotation_euler = (0, 0, math.radians(-90))
arm.select_set(True)
for ob in meshes:
    ob.select_set(True)
dst = os.path.join(odir, tag + ".fbx")
BAKE_STEP = 0.25 if MAXSTEP[0] > 60 else 1.0
bpy.ops.export_scene.fbx(filepath=dst, use_selection=True, bake_anim_step=BAKE_STEP, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
                         apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
                         path_mode="STRIP", embed_textures=False, bake_anim=True, bake_anim_use_all_actions=True,
                         bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0,
                         add_leaf_bones=False)

# 검증·렌더용 사본: Blender 가져오기는 이름 63자 제한으로 긴 모델의 액션 이름을 자른다 → 액션 이름을 짧게 바꾼 사본을 scratch에 낸다(납품본은 원본과 같은 이름 규칙 그대로)
SHORT = os.environ.get("B3_SHORT_DIR")
if SHORT:
    os.makedirs(SHORT, exist_ok=True)
    for a, sq in zip([bpy.data.actions[x["action"]] for x in actions], actions):
        a.name = sq["name"]
    bpy.ops.export_scene.fbx(filepath=os.path.join(SHORT, tag + ".fbx"), use_selection=True, bake_anim_step=BAKE_STEP, object_types={"ARMATURE", "MESH"}, apply_unit_scale=True,
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE",
                             path_mode="STRIP", embed_textures=False, bake_anim=True, bake_anim_use_all_actions=True,
                             bake_anim_use_nla_strips=False, bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, add_leaf_bones=False)

# json: 원본 json + fixed 블록
J = dict(J0)
J["sequences"] = actions
J["fixed"] = dict(
    note="휴식 포즈 = Stand 첫 프레임 · 액션 재굽기 · 숨김 뼈(Stand 스케일<0.05)는 옛 휴식 유지",
    restClip=seqs[stand_i]["name"], hiddenBonesAtStand=[bname[n] for n in sorted(hidden_nodes)],
    clipVisibility=clipvis, clipVisibilityAnytime=clipvis_any, bodyMaxDimM=round(bodymax, 3), minScaleKeys=MINS_KEYS[0], maxBoneStepDeg=round(MAXSTEP[0], 1), bakeStep=BAKE_STEP, warnings=warn,
    clipMapHidden={c["fbxAction"]: c["hiddenMeshes"] for c in CM["clips"]})
json.dump(J, open(os.path.join(odir, tag + ".json"), "w"), ensure_ascii=False, indent=1, default=list)
# clip_map과 대조
diff = []
for c in CM["clips"]:
    mine = sorted(m for m, v in clipvis.get(c["fbxAction"], {}).items() if not v)
    if sorted(c["hiddenMeshes"]) != mine:
        diff.append((c["fbxAction"], c["hiddenMeshes"], mine))
print("OK", tag, "bones", len(order), "meshes", len(meshes), "hiddenBones", len(hidden_nodes), "bodyMax", round(bodymax, 3),
      "minScaleKeys", MINS_KEYS[0], "WARN", warn, "clipmap_diff", diff)
if SHORT:
    shutil.copy(os.path.join(odir, tag + ".json"), os.path.join(SHORT, tag + ".json"))
    shutil.copytree(os.path.join(odir, "Textures"), os.path.join(SHORT, "Textures"), dirs_exist_ok=True)
