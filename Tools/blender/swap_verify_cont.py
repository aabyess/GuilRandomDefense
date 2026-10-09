"""재수출 연속성 검증(blender, 2026-10-09): 새 model_fixed FBX를 (a) FBX 오일러 곡선 키 간 점프(>180°·>90°) 세고 (b) 이전 model_fixed(_model_fixed_prev)와 정수 프레임마다 뼈 자세(쿼터니언 각 차)를 비교한다.
  blender -b --factory-startup --python Tools/blender/swap_verify_cont.py -- <새 FBX> <이전 FBX>
출력 한 줄: CONT <이름> eulerOver180 N eulerOver90 N maxEulerStep X maxPoseDiffDeg Y (Y<1° 이어야 통과)"""
import bpy, sys, math
from io_scene_fbx import parse_fbx
A = sys.argv[sys.argv.index('--') + 1:]; new, old = A[:2]


def euler_stats(f):
    root, ver = parse_fbx.parse(f); objs = {}; conn = []
    for el in root.elems:
        if el.id == b'Objects':
            for o in el.elems: objs[o.props[0]] = o
        if el.id == b'Connections': conn = list(el.elems)
    cn_of, cn_t = {}, {}
    for c in conn:
        if c.props[0] == b'OP' and objs.get(c.props[1]) is not None:
            o = objs[c.props[1]]
            if o.id == b'AnimationCurve': cn_of[c.props[1]] = c.props[2]
            if o.id == b'AnimationCurveNode':
                t = objs.get(c.props[2])
                if t is not None and t.id == b'Model': cn_t[c.props[1]] = c.props[3]
    o180 = o90 = 0; mx = 0.0
    for cid, cn in cn_of.items():
        pr = cn_t.get(cn)
        if not pr or b'Rotation' not in (pr if isinstance(pr, bytes) else pr.encode()): continue
        vals = None
        for e in objs[cid].elems:
            if e.id == b'KeyValueFloat': vals = list(e.props[0])
        if not vals: continue
        for i in range(len(vals) - 1):
            d = abs(vals[i + 1] - vals[i]); mx = max(mx, d); o180 += d > 180; o90 += d > 90
    return o180, o90, round(mx, 1)


def poses(f):
    bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=f)
    arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; arm.animation_data_create(); res = {}
    for a in bpy.data.actions:
        arm.animation_data.action = a; f0, f1 = [int(round(x)) for x in a.frame_range]; fr = {}
        for k in range(f0, f1 + 1):
            bpy.context.scene.frame_set(k); bpy.context.view_layer.update()
            fr[k] = {pb.name: pb.matrix_basis.to_quaternion() for pb in arm.pose.bones}
        res[a.name] = fr
    return res


e180, e90, mxe = euler_stats(new)
pn = poses(new); po = poses(old); worst = 0.0; missing = 0
for name, frames in pn.items():
    if name not in po: missing += 1; continue
    for k, bones in frames.items():
        if k not in po[name]: continue
        for b, q in bones.items():
            d = abs(po[name][k][b].dot(q)); worst = max(worst, math.degrees(2 * math.acos(min(1.0, d))))
print('CONT', new.split('/')[-1], 'eulerOver180', e180, 'eulerOver90', e90, 'maxEulerStep', mxe, 'maxPoseDiffDeg', round(worst, 3), 'unmatchedActions', missing)
