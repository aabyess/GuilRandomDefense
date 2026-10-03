"""한 클립을 일정 간격 프레임으로 한 장에 깐다(구간 자르기용, 2026-10-03). 뒷면 컬링 켠 Workbench 옆/정면.
   blender -b --factory-startup --python Tools/blender/frames_sheet.py -- <fbx> <클립이름 일부> <간격> <출력.png> [--side|--front] [--from N --to M] [--cols C]
   stdout: 프레임 번호 목록과 뼈 높이(pelvis·head) 곡선.
"""
import math, os, sys
import bpy, numpy as np
from mathutils import Vector

a = sys.argv[sys.argv.index("--") + 1:]
fbx, key, step, out = a[0], a[1], int(a[2]), a[3]
side = "--front" not in a
get = lambda k, d: int(a[a.index(k) + 1]) if k in a else d
cols = get("--cols", 8)
CELL = 200
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
sc = bpy.context.scene
arm = next(o for o in sc.objects if o.type == "ARMATURE")
act = next(x for x in bpy.data.actions if key in x.name and any(sl.target_id_type == 'OBJECT' for sl in x.slots))
arm.animation_data_create(); arm.animation_data.action = act
if hasattr(arm.animation_data, 'action_slot') and len(act.slots): arm.animation_data.action_slot = next((sl for sl in act.slots if sl.target_id_type == 'OBJECT'), act.slots[0])
f0, f1 = int(act.frame_range[0]), int(act.frame_range[1])
lo, hi = get("--from", f0), get("--to", f1)
meshes = [o for o in sc.objects if o.type == "MESH"]
sc.render.engine = "BLENDER_WORKBENCH"
sh = sc.display.shading; sh.light = "FLAT"; sh.color_type = "TEXTURE"; sh.show_backface_culling = True
sc.render.resolution_x = sc.render.resolution_y = CELL
w = bpy.data.worlds.new("w"); sc.world = w; w.color = (0.45, 0.5, 0.55)
cd = bpy.data.cameras.new("c"); cd.type = "ORTHO"
cam = bpy.data.objects.new("c", cd); sc.collection.objects.link(cam); sc.camera = cam
sc.frame_set(f0)
pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
H = (float(a[a.index('--H') + 1]) if '--H' in a else max(p.z for p in pts)) or 1.8
cd.ortho_scale = H * (float(a[a.index('--zoom') + 1]) if '--zoom' in a else 2.2); cd.clip_start = H * 0.01; cd.clip_end = H * 50
if side:
    cam.location = (H * 4, 0, H * 0.5); cam.rotation_euler = (math.radians(90), 0, math.radians(90))
else:
    cam.location = (0, -H * 4, H * 0.5); cam.rotation_euler = (math.radians(90), 0, 0)
frames = list(range(lo, hi + 1, step))
tmp = os.path.join(os.path.dirname(out), "_fs"); os.makedirs(tmp, exist_ok=True)
files = []
for f in frames:
    sc.frame_set(f)
    pv = next(b for b in arm.pose.bones if b.name.lower().endswith(("hips", "pelvis")))
    pw = arm.matrix_world @ pv.head                                   # 카메라가 골반을 따라간다(돌진 클립이 화면 밖으로 나간다)
    cam.location = (H * 4, pw.y, H * 0.5) if side else (pw.x, -H * 4, H * 0.5)
    p = os.path.join(tmp, f"{f}.png"); sc.render.filepath = p
    bpy.ops.render.render(write_still=True); files.append(p)
    bones = arm.pose.bones
    hp = [b for b in bones if b.name.lower().endswith(("hips", "pelvis"))]
    hd = [b for b in bones if "head" in b.name.lower()]
    z = lambda b: (arm.matrix_world @ b.head).z
    print(f"F {f} pelvis_z {z(hp[0]):.3f} head_z {z(hd[0]):.3f}" if hp and hd else f"F {f}")
rows = math.ceil(len(files) / cols)
buf = np.zeros((rows * CELL, cols * CELL, 4), np.float32)
for i, p in enumerate(files):
    im = bpy.data.images.load(p)
    px = np.asarray(im.pixels[:], np.float32).reshape(CELL, CELL, 4)
    r, c = divmod(i, cols)
    buf[(rows - 1 - r) * CELL:(rows - r) * CELL, c * CELL:(c + 1) * CELL] = px
    bpy.data.images.remove(im)
img = bpy.data.images.new("s", cols * CELL, rows * CELL, alpha=False)
img.pixels = buf.ravel(); img.filepath_raw = out; img.file_format = "PNG"; img.save()
import shutil; shutil.rmtree(tmp, ignore_errors=True)
