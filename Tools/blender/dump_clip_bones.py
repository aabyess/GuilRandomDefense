"""클립의 뼈별 프레임 로컬 값 덤프(blender, 2026-10-09): 유니티 임포트 보간 문제(오일러 뒤집힘) 비교용.
  blender -b --factory-startup --python Tools/blender/dump_clip_bones.py -- <FBX> <액션 앞부분(s4_)> <출력 json>
각 뼈·프레임: 로컬 변환 matrix_basis에서 q(w,x,y,z)·loc, 그리고 FBX에 실린 원시 오일러 곡선 값(도). 프레임 사이 회전각(쿼터니언 간)이 90° 넘으면 jumps에 표시."""
import bpy, sys, json, math
A = sys.argv[sys.argv.index('--') + 1:]; fbx, pre, outp = A[:3]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=fbx)
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; arm.animation_data_create()
a = next(x for x in bpy.data.actions if x.name.split('|')[-1].startswith(pre)); arm.animation_data.action = a
f0, f1 = [int(x) for x in a.frame_range]
fcs = a.fcurves if hasattr(a, 'fcurves') else [fc for l in a.layers for st in l.strips for cb in st.channelbags for fc in cb.fcurves]
eul = {}
for fc in fcs:
    if 'pose.bones' in fc.data_path and ('rotation_euler' in fc.data_path or 'rotation_quaternion' in fc.data_path):
        bn = fc.data_path.split('"')[1]; eul.setdefault(bn, {}).setdefault(fc.data_path.split('.')[-1], {})[fc.array_index] = fc
res = dict(action=a.name.split('|')[-1], frames=[f0, f1], fps=30, bones={}, jumps=[])
for pb in arm.pose.bones: res['bones'][pb.name] = dict(parent=pb.parent.name if pb.parent else None, frames=[])
prev = {}
for fr in range(f0, f1 + 1):
    bpy.context.scene.frame_set(fr); bpy.context.view_layer.update()
    for pb in arm.pose.bones:
        loc, q, sc = pb.matrix_basis.decompose(); e = eul.get(pb.name, {}).get('rotation_euler')
        raw = [round(math.degrees(e[i].evaluate(fr)), 2) for i in range(3)] if e and len(e) == 3 else None
        pe = [round(math.degrees(x), 2) for x in q.to_euler('XYZ')]                    # 연속성 보정 없는 XYZ 오일러(유닛이 FBX 오일러를 이렇게 풀면 보이는 값)
        if raw is None: raw = pe
        rec = dict(f=fr, q=[round(x, 5) for x in q], loc=[round(x, 5) for x in loc], scale=[round(x, 4) for x in sc], eulerDeg=raw, qSignFlip=bool(pb.name in prev and prev[pb.name][0].dot(q) < 0))
        res['bones'][pb.name]['frames'].append(rec)
        if pb.name in prev:
            d = abs(prev[pb.name][0].dot(q)); ang = math.degrees(2 * math.acos(min(1, d)))
            de = max(abs(raw[i] - prev[pb.name][1][i]) for i in range(3)) if raw and prev[pb.name][1] else 0
            if ang > 90 or de > 90: res['jumps'].append(dict(bone=pb.name, f=fr, quatAngleDeg=round(ang, 1), maxEulerStepDeg=round(de, 1)))
        prev[pb.name] = (q.copy(), raw)
json.dump(res, open(outp, 'w'), ensure_ascii=False)
print('DUMP', res['action'], len(res['bones']), 'bones', 'jumps', len(res['jumps']), res['jumps'][:6])
