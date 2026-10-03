"""고유 동작 FBX의 클립마다 5개 프레임(0·25·50·75·100%)을 **뒷면 컬링을 켠** 정면 렌더로 뽑아 한 장에 깐다(2026-10-03, 5묶음 검수).
   skin-verify-procedure ① — 유니티 URP는 뒷면을 버리므로 컬링을 켜야 뒤집힌 면이 보인다. Workbench 텍스처 색 그대로.

쓰는 법:
  blender -b --factory-startup --python Tools/blender/clip_sheet.py -- <유닛.fbx> <출력.png> [--side]
  --side : 정면 대신 옆(+X에서) 렌더.
각 줄 = 클립 하나(위에서부터 FBX 안 순서). 칸 = 그 클립의 25% 간격 다섯 프레임. 줄 제목은 stdout에 클립 이름·프레임 수로 찍는다.
"""
import math
import sys

import bpy
from mathutils import Vector

CELL = 220
FRACS = (0.0, 0.25, 0.5, 0.75, 1.0)


def main():
    args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    side = "--side" in args
    only = None
    if "--only" in args:                                              # --only Idle,Move,Attack : 이 이름의 클립만(이름 일치, 쉼표 구분)
        i = args.index("--only")
        only = set(args[i + 1].split(","))
        del args[i:i + 2]
    args = [a for a in args if a != "--side"]
    fbx, out = args[0], args[1]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx)
    scene = bpy.context.scene
    arm = next(o for o in scene.objects if o.type == "ARMATURE")
    meshes = [o for o in scene.objects if o.type == "MESH"]
    actions = sorted({a for a in bpy.data.actions}, key=lambda a: a.name)
    # FBX 안 순서를 지키려고 임포트 직후 목록을 그대로 쓴다(이름 정렬이 아니라 range 순서가 필요하면 아래 표를 본다)
    actions = [x for x in bpy.data.actions if len(x.slots) == 0 or any(sl.target_id_type == 'OBJECT' for sl in x.slots)]   # 모양 키 액션(KESlot)은 뺀다
    if only:
        actions = [x for x in actions if x.name.split("|")[-1] in only]
    scene.render.engine = "BLENDER_WORKBENCH"
    sh = scene.display.shading
    sh.light = "FLAT"
    sh.color_type = "TEXTURE"
    sh.show_backface_culling = True
    scene.render.resolution_x = scene.render.resolution_y = CELL
    scene.render.film_transparent = False
    w = bpy.data.worlds.new("w")
    scene.world = w
    w.color = (0.45, 0.5, 0.55)
    cam_d = bpy.data.cameras.new("c")
    cam_d.type = "ORTHO"
    cam = bpy.data.objects.new("c", cam_d)
    scene.collection.objects.link(cam)
    scene.camera = cam
    # 크기 = 쉬는 자세 메시 경계
    pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    zmax = max(p.z for p in pts)
    cam_d.ortho_scale = max(zmax * 1.55, 2.2)
    cx = 0.0
    if side:
        cam.location = (zmax * 4, 0, zmax * 0.5)
        cam.rotation_euler = (math.radians(90), 0, math.radians(90))
    else:
        cam.location = (cx, -zmax * 4, zmax * 0.5)
        cam.rotation_euler = (math.radians(90), 0, 0)
    import os
    tmp = os.path.join(os.path.dirname(out) or ".", "_cs")
    os.makedirs(tmp, exist_ok=True)
    rows = []
    for act in actions:
        arm.animation_data_create()
        arm.animation_data.action = act
        if hasattr(arm.animation_data, 'action_slot') and len(act.slots):
            arm.animation_data.action_slot = next((sl for sl in act.slots if sl.target_id_type == 'OBJECT'), act.slots[0])
        f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
        row = []
        for i, fr in enumerate(FRACS):
            f = round(f0 + (f1 - f0) * fr)
            scene.frame_set(f)
            p = os.path.join(tmp, f"{len(rows)}_{i}.png")
            scene.render.filepath = p
            bpy.ops.render.render(write_still=True)
            row.append(p)
        rows.append(row)
        print(f"ROW {len(rows) - 1} {act.name} {f0}-{f1} ({f1 - f0 + 1}f)")
    W, H = CELL * len(FRACS), CELL * len(rows)
    img = bpy.data.images.new("sheet", W, H, alpha=False)
    import numpy as np
    buf = np.zeros((H, W, 4), dtype=np.float32)
    for r, row in enumerate(rows):
        for c, p in enumerate(row):
            im = bpy.data.images.load(p)
            a = np.asarray(im.pixels[:], dtype=np.float32).reshape(CELL, CELL, 4)
            y0 = (len(rows) - 1 - r) * CELL
            buf[y0:y0 + CELL, c * CELL:(c + 1) * CELL] = a
            bpy.data.images.remove(im)
    img.pixels = buf.ravel()
    img.filepath_raw = out
    img.file_format = "PNG"
    img.save()
    import shutil
    shutil.rmtree(tmp, ignore_errors=True)


main()
