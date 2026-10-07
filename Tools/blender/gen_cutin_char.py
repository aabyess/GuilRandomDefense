"""상위 등급 뽑기 컷인용 캐릭터 렌더(2026-10-08, 사장님 시안). 3D 스킨 → 만화풍(셀 셰이딩 + 외곽선) 투명 배경 PNG.

  blender -b --factory-startup --python Tools/blender/gen_cutin_char.py -- <스킨.fbx> <출력폴더> <이름> [front=-Y|+Y]

산출(출력폴더):
  A_<이름>_face.png   논파 컷인용 얼굴 클로즈업(노란 외곽선, 정면 살짝 위, 이미지 안에서 기울임은 합성 단계가 한다)
  B_<이름>_bust.png   소개 카드용 상반신(검정 외곽선, 허리에 손 얹은 포즈 근사)
셀 셰이딩: 텍스처 색 × 3단 명암(Shader to RGB → 상수 램프). 외곽선: 뒤집은 껍데기(Solidify 법선 반전, 앞면 컬링).
Assets엔 안 쓴다. 정면 방향이 모델마다 달라 front 인자로 고른다(기본 -Y).
"""
import math
import os
import sys

import bpy
from mathutils import Euler, Vector

args = sys.argv[sys.argv.index("--") + 1:]
FBX, OUT, NAME = args[0], args[1], args[2]
FRONT = args[3] if len(args) > 3 else "-Y"
os.makedirs(OUT, exist_ok=True)

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=FBX)
arm = next((o for o in bpy.data.objects if o.type == "ARMATURE"), None)
meshes = [o for o in bpy.data.objects if o.type == "MESH"]


def toon_material(src, outline=False, color=(1, 0.85, 0.1)):
    """원래 재질의 텍스처를 받아 3단 셀 재질로."""
    mat = bpy.data.materials.new(src.name + "_toon" if src else "outline")
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    if outline:
        em = nt.nodes.new("ShaderNodeEmission")
        em.inputs["Color"].default_value = color + (1,)
        em.inputs["Strength"].default_value = 1.0
        nt.links.new(em.outputs[0], out.inputs["Surface"])
        mat.use_backface_culling = True
        return mat
    tex = None
    base = (0.8, 0.8, 0.8, 1)
    if src and src.use_nodes:
        for n in src.node_tree.nodes:
            if n.type == "TEX_IMAGE" and n.image:
                tex = n.image
                break
        pr = next((n for n in src.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if pr:
            base = tuple(pr.inputs["Base Color"].default_value)
    diff = nt.nodes.new("ShaderNodeBsdfDiffuse")
    s2r = nt.nodes.new("ShaderNodeShaderToRGB")
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.color_ramp.interpolation = "CONSTANT"
    ramp.color_ramp.elements[0].position = 0.0
    ramp.color_ramp.elements[0].color = (0.62, 0.58, 0.72, 1)           # 그림자: 살짝 보랏빛
    e1 = ramp.color_ramp.elements.new(0.30)
    e1.color = (0.86, 0.84, 0.9, 1)
    e2 = ramp.color_ramp.elements.new(0.62)
    e2.color = (1.08, 1.06, 1.04, 1)
    nt.links.new(diff.outputs[0], s2r.inputs[0])
    sep = nt.nodes.new("ShaderNodeSeparateColor")
    nt.links.new(s2r.outputs[0], sep.inputs[0])
    nt.links.new(sep.outputs[0], ramp.inputs[0])
    mul = nt.nodes.new("ShaderNodeMix")
    mul.data_type = "RGBA"
    mul.blend_type = "MULTIPLY"
    mul.inputs[0].default_value = 1.0
    nt.links.new(ramp.outputs[0], mul.inputs[6])
    alpha_src = None
    if tex:
        tn = nt.nodes.new("ShaderNodeTexImage")
        tn.image = tex
        tn.interpolation = "Linear"
        nt.links.new(tn.outputs["Color"], mul.inputs[7])
        alpha_src = tn.outputs["Alpha"]
    else:
        mul.inputs[7].default_value = base
    em = nt.nodes.new("ShaderNodeEmission")
    nt.links.new(mul.outputs[2], em.inputs["Color"])
    if alpha_src is not None and src and src.blend_method != "OPAQUE":
        tr = nt.nodes.new("ShaderNodeBsdfTransparent")
        mx = nt.nodes.new("ShaderNodeMixShader")
        nt.links.new(alpha_src, mx.inputs["Fac"])
        nt.links.new(tr.outputs[0], mx.inputs[1])
        nt.links.new(em.outputs[0], mx.inputs[2])
        nt.links.new(mx.outputs[0], out.inputs["Surface"])
        mat.surface_render_method = "BLENDED"
    else:
        nt.links.new(em.outputs[0], out.inputs["Surface"])
    return mat


def apply_toon(outline_color, outline_w):
    for o in meshes:
        srcs = list(o.data.materials)
        o.data.materials.clear()
        for s in srcs:
            o.data.materials.append(toon_material(s))
        n = len(srcs)
        o.data.materials.append(toon_material(None, outline=True, color=outline_color))
        for m in list(o.modifiers):
            if m.type == "SOLIDIFY":
                o.modifiers.remove(m)
        sd = o.modifiers.new("outline", "SOLIDIFY")
        sd.thickness = outline_w
        sd.offset = 1.0
        sd.use_flip_normals = True
        sd.use_rim = False
        sd.material_offset = n


def head_info():
    """머리뼈(mixamorig:Head) 위치. 믹사모가 아니거나 뼈가 없으면 None(→ 메시 경계로 틀을 잡는다)."""
    if arm is None:
        return None
    head = arm.pose.bones.get("mixamorig:Head")
    if head is None:
        return None
    return arm.matrix_world @ head.head, arm.matrix_world @ head.tail


_SETUP = {}


def setup(res_x, res_y):
    scn = bpy.context.scene
    if _SETUP:
        scn.render.resolution_x, scn.render.resolution_y = res_x, res_y
        return scn, _SETUP["cam"]
    try:
        scn.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        scn.render.engine = "BLENDER_EEVEE"
    scn.render.resolution_x, scn.render.resolution_y = res_x, res_y
    scn.render.film_transparent = True
    scn.view_settings.view_transform = "Standard"
    w = bpy.data.worlds.new("w")
    w.use_nodes = True
    scn.world = w
    scn.render.image_settings.color_mode = "RGBA"
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    scn.collection.objects.link(sun)
    sun.data.energy = 4.0
    sx = -1 if FRONT == "-Y" else 1
    sun.rotation_euler = Euler((math.radians(50), 0, math.radians(35 * sx)))
    sun.rotation_euler = Vector((-0.5 * sx * 0, 0, 0)).to_track_quat("-Z", "Y").to_euler() if False else sun.rotation_euler
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
    scn.collection.objects.link(cam)
    scn.camera = cam
    _SETUP["cam"] = cam
    return scn, cam


def aim(cam, target, dist, fovscale, roll=0.0, side=0.0, up=0.0):
    f = Vector((0, -1, 0)) if FRONT == "-Y" else Vector((0, 1, 0))
    pos = target + f * dist + Vector((side, 0, up))
    cam.location = pos
    d = (target - pos).normalized()
    q = d.to_track_quat("-Z", "Y")
    cam.rotation_euler = q.to_euler()
    cam.data.type = "ORTHO"
    cam.data.ortho_scale = fovscale
    cam.data.clip_start = 0.01
    cam.data.clip_end = 20


def aim_bone(name, direction):
    """믹사모 뼈를 armature 공간 방향(앞 = -Y, 캐릭터 왼쪽 = +X, 위 = +Z)으로 겨눈다. 관절(뼈 머리) 기준 회전 —
    쉬는 자세(T·A·차렷)와 상관없이 같은 모양이 된다. 뼈가 없으면 건너뛴다(믹사모 아닌 스킨)."""
    from mathutils import Matrix
    if arm is None:
        return
    pb = arm.pose.bones.get("mixamorig:" + name)
    if not pb:
        return
    cur = (pb.tail - pb.head)
    if cur.length < 1e-6:
        return
    q = cur.normalized().rotation_difference(Vector(direction).normalized())
    hd = pb.head.copy()
    pb.matrix = Matrix.Translation(hd) @ q.to_matrix().to_4x4() @ Matrix.Translation(-hd) @ pb.matrix
    bpy.context.view_layer.update()


def reset_pose():
    from mathutils import Matrix
    if arm is None:
        return
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()


def mirror(d, sg):
    return (d[0] * sg, d[1], d[2])


def arm_chain(side, sg, upper, fore, hand=None):
    aim_bone(f"{side}Arm", mirror(upper, sg))
    aim_bone(f"{side}ForeArm", mirror(fore, sg))
    aim_bone(f"{side}Hand", mirror(hand or fore, sg))


# 방향은 왼팔(+X) 기준으로 적고 오른팔은 X를 뒤집는다.
def pose_calm():                 # 차분한 정면(팔 내리고 살짝 굽힘) — C안
    for side, sg in (("Left", 1), ("Right", -1)):
        arm_chain(side, sg, (0.28, 0.04, -1), (0.12, -0.22, -1))


def pose_hips():                 # 허리에 손(자신감)
    for side, sg in (("Left", 1), ("Right", -1)):
        arm_chain(side, sg, (0.62, 0.12, -0.78), (-0.72, -0.2, -0.62), (-0.5, -0.3, -0.8))


def pose_crossed():              # 팔짱
    for side, sg in (("Left", 1), ("Right", -1)):
        arm_chain(side, sg, (0.22, -0.18, -1), (-0.92, -0.38, 0.12), (-1, -0.2, 0.15))


def pose_point():                # 손가락질(오른팔 앞으로 쭉 + 왼손 허리)
    arm_chain("Right", 1, (-0.12, -1, 0.22), (-0.06, -1, 0.26))
    arm_chain("Left", 1, (0.62, 0.12, -0.78), (-0.72, -0.2, -0.62), (-0.5, -0.3, -0.8))


def pose_fist():                 # 주먹 쥐고 들기(오른팔 위로 접음 + 왼팔 내림)
    arm_chain("Right", 1, (-0.75, -0.15, 0.4), (0.15, -0.2, 1))
    arm_chain("Left", 1, (0.28, 0.04, -1), (0.12, -0.22, -1))


POSES = {"calm": pose_calm, "hips": pose_hips, "crossed": pose_crossed, "point": pose_point, "fist": pose_fist}


def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


ONLY = set((os.environ.get("ONLY") or "A,B,C").split(","))
apply_toon((1.0, 0.82, 0.05), 0.007)
allv = [(o.matrix_world @ v.co) for o in meshes for v in o.data.vertices]
top = max(p.z for p in allv)
hi = head_info()
if hi:
    h, t = hi
else:                                                               # 믹사모 아닌 스킨(영원_서민성 Root/Body 등) — 경계 상자 가운데
    h = Vector(((max(p.x for p in allv) + min(p.x for p in allv)) / 2, (max(p.y for p in allv) + min(p.y for p in allv)) / 2, top - 0.14))
    t = h
H = top - min(p.z for p in allv)                                    # 키(로스터 FBX는 1.8로 맞춰져 있지만 서민성처럼 다른 것도 있다)

# ───────── A: 얼굴 클로즈업, 노란 외곽선 ─────────
if "A" in ONLY:
    center = Vector((h.x, h.y, top - 0.14 * H / 1.8))
    scn, cam = setup(1024, 1280)
    aim(cam, center, 3.0, 0.5 * H / 1.8)
    render(os.path.join(OUT, f"A_{NAME}_face.png"))
print("head", tuple(round(v, 2) for v in h), "top", round(top, 2), "H", round(H, 2), "mixamo-head", bool(hi))

# ───────── B: 상반신 — 포즈별 ─────────
for o in meshes:                                                    # 외곽선만 검정으로 바꾼다
    for m in o.data.materials:
        if m and m.name.startswith("outline"):
            em = next(n for n in m.node_tree.nodes if n.type == "EMISSION")
            em.inputs["Color"].default_value = (0.04, 0.03, 0.05, 1)
    for md in o.modifiers:
        if md.type == "SOLIDIFY":
            md.thickness = 0.006
if "B" in ONLY:
    want = (os.environ.get("POSES") or "calm,hips,crossed,point,fist").split(",")
    for pn in want:
        reset_pose()
        POSES[pn]()
        scn, cam = setup(1024, 1280)
        aim(cam, Vector((h.x, h.y, top - 0.55 * H / 1.8)), 3.0, 1.1 * H / 1.8)
        render(os.path.join(OUT, f"B_{NAME}_bust_{pn}.png"))
        if pn == "hips":
            render(os.path.join(OUT, f"B_{NAME}_bust.png"))

# ───────── C: 허벅지까지 — 공격 클립의 가장 크게 벌어진 프레임을 포즈로(사장님 10-08) ─────────
SHARED_ATTACK = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets/Art/Characters/attack.fbx"))


def eval_points(step=7):
    dg = bpy.context.evaluated_depsgraph_get()
    pv = []
    for o in meshes:
        ev = o.evaluated_get(dg)
        me = ev.to_mesh()
        mw = ev.matrix_world
        pv += [mw @ me.vertices[i].co for i in range(0, len(me.vertices), step)]
        ev.to_mesh_clear()
    return pv


def attack_action():
    """유닛 자체 클립(FBX 안 Attack*) 우선, 없으면 공용 휴머노이드 attack.fbx(믹사모 뼈 이름 그대로 얹는다). (액션, 출처) 또는 (None, 사유)."""
    own = [a for a in bpy.data.actions if "attack" in a.name.lower() and "lunge" not in a.name.lower() and "_b" not in a.name.lower()]
    own.sort(key=lambda a: (a.name.split("|")[-1] != "Attack", a.name))
    if own:
        return own[0], "자체:" + own[0].name.split("|")[-1]
    if arm is None or not arm.pose.bones.get("mixamorig:Hips"):
        return None, "클립 없음(믹사모 아님)"
    before_o, before_a = set(bpy.data.objects), set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=SHARED_ATTACK)
    for o in set(bpy.data.objects) - before_o:
        bpy.data.objects.remove(o)
    new = [a for a in bpy.data.actions if a not in before_a]
    return (new[0], "공용:attack.fbx") if new else (None, "공용 클립 못 읽음")


def set_frame(act, f):
    ad = arm.animation_data or arm.animation_data_create()
    if ad.action != act:
        ad.action = act
        try:
            if act.slots:
                ad.action_slot = act.slots[0]
        except AttributeError:
            pass
    bpy.context.scene.frame_set(int(round(f)))
    bpy.context.view_layer.update()


def render_c(path):
    pv = eval_points(1)
    ptop = max(p.z for p in pv)
    pbot = ptop - H * 0.62
    span_v = (ptop - pbot) * 1.06
    band = [p for p in pv if p.z >= pbot]
    span_h = (max(p.x for p in band) - min(p.x for p in band)) * 1.04
    cx = (max(p.x for p in band) + min(p.x for p in band)) / 2
    cy = (max(p.y for p in band) + min(p.y for p in band)) / 2
    span = max(span_v, span_h)
    scn, cam = setup(1536, 1536)
    aim(cam, Vector((cx, cy, ptop - span_v / 2 + 0.02)), 3.0, span)
    render(path)
    return dict(top=round(ptop, 3), span=round(span, 3))


if "C" in ONLY:
    import json
    info = dict(unit=NAME, candidates=[])
    act, src = (None, "")
    if arm is not None:
        reset_pose()
        act, src = attack_action()
    info["source"] = src
    if act is not None:
        f0, f1 = act.frame_range
        samples = [f0 + (f1 - f0) * k / 15 for k in range(16)]
        scored = []
        for f in samples:                                          # 벌어짐 = 앞에서 본 넓이 × 높이(허벅지 위만)
            set_frame(act, f)
            pv = eval_points(9)
            ptop = max(p.z for p in pv)
            up = [p for p in pv if p.z >= ptop - H * 0.62]
            wdt = max(p.x for p in up) - min(p.x for p in up)
            hgt = max(p.z for p in up) - min(p.z for p in up)
            # 웅크리거나 눕거나(정수리가 키의 82% 아래) 뒤로 멀어진(머리 깊이 이동) 프레임은 뒤로 미룬다 — 41기 1차에서 조성진·고도현·박은석이 그랬다
            stand = ptop >= H * 0.82
            scored.append((wdt * hgt * (1.0 if stand else 0.25), f))
        scored.sort(reverse=True)
        picks = []
        for sc, f in scored:                                       # 서로 클립 길이 25% 이상 떨어진 시점 3개
            if all(abs(f - g) >= (f1 - f0) * 0.25 for _, g in picks):
                picks.append((sc, f))
            if len(picks) == 3:
                break
        for k, (sc, f) in enumerate(picks, 1):
            set_frame(act, f)
            fn = f"C_{NAME}_thigh_c{k}.png"
            r = render_c(os.path.join(OUT, fn))
            info["candidates"].append(dict(id=f"c{k}", file=fn, frame=round(f, 1), score=round(sc, 3), **r))
        if arm.animation_data:
            arm.animation_data.action = None
    # 프리셋 포즈(믹사모만) — 클립이 밋밋할 때 사장님이 고를 수 있게
    if arm is not None and arm.pose.bones.get("mixamorig:LeftArm"):
        for pn in ("hips", "crossed", "point", "fist"):
            reset_pose()
            POSES[pn]()
            fn = f"C_{NAME}_thigh_p_{pn}.png"
            r = render_c(os.path.join(OUT, fn))
            info["candidates"].append(dict(id="p_" + pn, file=fn, **r))
    if not info["candidates"]:                                     # 클립도 프리셋도 없다(믹사모 아닌 스킨 + 클립 없음) → 쉬는 자세
        reset_pose()
        fn = f"C_{NAME}_thigh_rest.png"
        r = render_c(os.path.join(OUT, fn))
        info["candidates"].append(dict(id="rest", file=fn, **r))
    pick = os.environ.get("C_PICK") or info["candidates"][0]["id"]  # 기본 = 가장 벌어진 공격 프레임(c1)
    chosen = next((c for c in info["candidates"] if c["id"] == pick), info["candidates"][0])
    info["chosen"] = chosen["id"]
    import shutil
    shutil.copyfile(os.path.join(OUT, chosen["file"]), os.path.join(OUT, f"C_{NAME}_thigh.png"))
    json.dump(info, open(os.path.join(OUT, f"C_{NAME}_cands.json"), "w"), ensure_ascii=False, indent=1)
    print("C", NAME, src, "후보", [c["id"] for c in info["candidates"]], "고름", info["chosen"])
