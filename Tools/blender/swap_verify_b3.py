"""재수출 FBX 검증(blender3): 다시 읽어 휴식 vs Stand 첫 프레임 메시 크기와 중간 프레임 크기를 잰다.
  blender -b --factory-startup --python Tools/blender/swap_verify_b3.py -- <model_fixed 폴더>"""
import bpy, sys, glob, json, os, re
from mathutils import Vector
d = sys.argv[sys.argv.index('--') + 1]
f = glob.glob(d + '/*.fbx')[0]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=f)
REST = json.load(open(glob.glob(d + '/*.json')[0]))['fixed']['restClip']
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
def bb():
    dg = bpy.context.evaluated_depsgraph_get(); r = {}
    for o in bpy.data.objects:
        if o.type == 'MESH':
            ev = o.evaluated_get(dg); pts = [o.matrix_world @ Vector(c) for c in ev.bound_box]
            r[re.search(r'_g(\d+)_L(\d+)', o.name).group(0)] = round(max(max(p[i] for p in pts) - min(p[i] for p in pts) for i in range(3)), 3)
    return r
arm.animation_data_create(); arm.animation_data.action = None
for pb in arm.pose.bones:       # 진짜 바인드: 포즈 전부 항등(임포터가 마지막 키 값을 남겨 둔다)
    pb.location = (0, 0, 0); pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0); pb.scale = (1, 1, 1)
bpy.context.view_layer.update()
rest = bb(); print('REST', rest)
worst = 0
for a in bpy.data.actions:
    arm.animation_data.action = a
    bpy.context.scene.frame_set(int(a.frame_range[0])); first = bb()
    if a.name.endswith('|' + REST):
        worst = max(abs(first[k] - rest[k]) for k in rest); print('STAND0', a.name, first, 'rest-vs-stand max diff', worst)
    mid = int((a.frame_range[0] + a.frame_range[1]) / 2); bpy.context.scene.frame_set(mid)
    print(a.name.split('|')[-1], 'mid', bb())
