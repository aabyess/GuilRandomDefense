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
arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
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
    head = arm.pose.bones.get("mixamorig:Head")
    h = arm.matrix_world @ head.head
    t = arm.matrix_world @ head.tail
    return h, t


def setup(res_x, res_y):
    scn = bpy.context.scene
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


def spin(name, deg, axis="Y"):
    """믹사모 뼈를 armature 공간 월드축(X·Y·Z) 둘레로 deg° 돌린다(관절=뼈 머리 기준). T포즈에서 시작하는 시안용 근사."""
    from mathutils import Matrix
    pb = arm.pose.bones.get("mixamorig:" + name)
    if not pb:
        return
    m = pb.matrix.copy()
    hd = m.to_translation()
    pb.matrix = Matrix.Translation(hd) @ Matrix.Rotation(math.radians(deg), 4, axis) @ Matrix.Translation(-hd) @ m
    bpy.context.view_layer.update()


def reset_pose():
    from mathutils import Matrix
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()


# 정면 = -Y, 캐릭터의 왼팔 = +X(sg=+1), 오른팔 = -X(sg=-1). 팔 방향 벡터가 Y축 둘레로 +θ 돌면 +X팔은 아래로, -X팔은 위로 간다.
def pose_hips():                 # 허리에 손(자신감)
    for side, sg in (("Left", 1), ("Right", -1)):
        spin(f"{side}Arm", sg * 60)
        spin(f"{side}ForeArm", sg * 105)


def pose_crossed():              # 팔짱
    for side, sg in (("Left", 1), ("Right", -1)):
        spin(f"{side}Arm", sg * 85)
        spin(f"{side}ForeArm", -90, "X")
        spin(f"{side}ForeArm", -sg * 62, "Z")
        spin(f"{side}Hand", -sg * 20, "Z")


def pose_point():                # 손가락질(오른팔 앞으로 쭉, 왼손 허리)
    spin("RightArm", 90, "Z")
    spin("RightArm", -18, "X")
    spin("LeftArm", 60)
    spin("LeftForeArm", 105)


def pose_fist():                 # 주먹 쥐고 들기(오른팔 위로 접음, 왼팔 내림)
    spin("RightArm", 40)
    spin("RightForeArm", 110)
    spin("LeftArm", 78)
    spin("LeftForeArm", 14)


def pose_calm():                 # 차분한 정면(팔 내리고 살짝 굽힘) — C안(프롤로그 자기소개)
    for side, sg in (("Left", 1), ("Right", -1)):
        spin(f"{side}Arm", sg * 72)
        spin(f"{side}ForeArm", sg * 18)


POSES = {"calm": pose_calm, "hips": pose_hips, "crossed": pose_crossed, "point": pose_point, "fist": pose_fist}


def render(path):
    bpy.context.scene.render.filepath = path
    bpy.ops.render.render(write_still=True)


# ───────── A: 얼굴 클로즈업, 노란 외곽선 ─────────
apply_toon((1.0, 0.82, 0.05), 0.007)
h, t = head_info()
top = max((o.matrix_world @ v.co).z for o in meshes for v in o.data.vertices)
center = Vector((h.x, h.y, top - 0.14))
scn, cam = setup(1024, 1280)
aim(cam, center, 3.0, 0.5)
render(os.path.join(OUT, f"A_{NAME}_face.png"))
print("head", tuple(round(v, 2) for v in h), tuple(round(v, 2) for v in t))

# ───────── B: 상반신 — 포즈별 ─────────
for o in meshes:                                                    # 외곽선만 검정으로 바꾼다
    for m in o.data.materials:
        if m and m.name.startswith("outline"):
            em = next(n for n in m.node_tree.nodes if n.type == "EMISSION")
            em.inputs["Color"].default_value = (0.04, 0.03, 0.05, 1)
    for md in o.modifiers:
        if md.type == "SOLIDIFY":
            md.thickness = 0.006
want = (os.environ.get("POSES") or "calm,hips,crossed,point,fist").split(",")
for pn in want:
    reset_pose()
    POSES[pn]()
    scn, cam = setup(1024, 1280)
    bust_c = Vector((h.x, h.y, top - 0.55))
    aim(cam, bust_c, 3.0, 1.1)
    render(os.path.join(OUT, f"B_{NAME}_bust_{pn}.png"))
    if pn == "hips":
        render(os.path.join(OUT, f"B_{NAME}_bust.png"))

# ───────── C: 허벅지까지 상반신(차분한 정면) — 프롤로그 자기소개 컷인 ─────────
reset_pose()
POSES[os.environ.get("C_POSE", "calm")]()
scn, cam = setup(1024, 1536)
aim(cam, Vector((h.x, h.y, top - 0.72)), 3.0, 1.5)
render(os.path.join(OUT, f"C_{NAME}_thigh.png"))
