"""교체 전후 비교 렌더(blender, 2026-10-09). 한 판에서 「교체 전 FBX(Assets 옛 판, 보통 기본 자세)」와 「교체 후 model_fixed(Stand 한가운데 프레임)」를 같은 방향·같은 눈금으로 각각 렌더.
  blender -b --factory-startup --python Tools/blender/swap_before_after.py -- <옛 폴더> <model_fixed 폴더> <clip_map.json> <출력 접두어>
출력: <접두어>_before.png · _after.png (512x768 세로, 투명 배경). 눈금 = 두 모델 중 큰 쪽 키 기준 한 값. 보는 방향 = 블렌더 앞(-Y)에서."""
import bpy, sys, os, glob, json
from mathutils import Vector
OLD, NEW, CMP, OUTP = sys.argv[sys.argv.index('--') + 1:][:4]
J = json.load(open(glob.glob(NEW + '/*.json')[0])); CM = json.load(open(CMP)); vis = J['fixed']['clipVisibility']


def fresh():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    ids = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
    sc.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in ids else 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 512, 768
    sc.render.film_transparent = True
    return sc


def emission(m, img):
    m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
    t = nt.nodes.new('ShaderNodeTexImage'); t.image = img
    e = nt.nodes.new('ShaderNodeEmission'); tr = nt.nodes.new('ShaderNodeBsdfTransparent'); mx = nt.nodes.new('ShaderNodeMixShader'); out = nt.nodes.new('ShaderNodeOutputMaterial')
    nt.links.new(t.outputs['Color'], e.inputs['Color']); nt.links.new(t.outputs['Alpha'], mx.inputs[0])
    nt.links.new(tr.outputs[0], mx.inputs[1]); nt.links.new(e.outputs[0], mx.inputs[2]); nt.links.new(mx.outputs[0], out.inputs[0])
    try: m.surface_render_method = 'BLENDED'
    except Exception: pass


def load(side):
    sc = fresh()
    folder = OLD if side == 'before' else NEW
    bpy.ops.import_scene.fbx(filepath=glob.glob(folder + '/*.fbx')[0])
    for o in bpy.data.objects:
        if o.type != 'MESH': continue
        jm = next((m for m in J['meshes'] if o.name.startswith(m['mesh'])), None) if side == 'after' else None
        for s in o.material_slots:
            m = s.material
            if side == 'after':
                tf = jm and jm.get('textureFile'); p = os.path.join(NEW, 'Textures', tf) if tf else None
                img = bpy.data.images.load(p) if p and os.path.exists(p) else None
            else:
                img = next((n.image for n in m.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image), None) if m.node_tree else None
            if img: emission(m, img)
    if side == 'after':
        arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; arm.animation_data_create()
        idx = next(c['index'] for c in CM['clips'] if c['kind'] == 'Idle')
        a = next((x for x in bpy.data.actions if x.name.split('|')[-1].startswith(f's{idx}_')), None)
        if a is None:       # 액션 이름이 63자에서 잘려 번호가 사라진 판(롭): 움직임이 가장 적은 액션 = Stand로 본다
            def motion(x):
                t = 0.0
                fcs = x.fcurves if hasattr(x, 'fcurves') else [fc for l in x.layers for st in l.strips for cb in st.channelbags for fc in cb.fcurves]
                for fc in fcs:
                    v = [k.co[1] for k in fc.keyframe_points]
                    t += (max(v) - min(v)) if v else 0
                return t
            a = min((x for x in bpy.data.actions if 'Walk' not in x.name and 'Move' not in x.name), key=motion)
        an0 = a.name.split('|')[-1]
        if an0 not in vis: an0 = next(k for k in vis if k.startswith(f's{idx}_'))
        arm.animation_data.action = a; sc.frame_set(int((a.frame_range[0] + a.frame_range[1]) / 2))
        an = an0
        for o in bpy.data.objects:
            if o.type == 'MESH':
                jm = next((m for m in J['meshes'] if o.name.startswith(m['mesh'])), None)
                o.hide_render = not (jm and vis[an].get(jm['mesh'], True))
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get(); pts = []
    for o in bpy.data.objects:
        if o.type == 'MESH' and not o.hide_render:
            ev = o.evaluated_get(dg); pts += [o.matrix_world @ v.co for v in ev.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))); hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return sc, lo, hi


def shoot(sc, lo, hi, scale, path):
    c = (lo + hi) / 2; base = Vector((c.x, c.y, lo.z + scale * 0.46))     # 발바닥을 화면 아래에 맞춘다(눈금 스케일의 반 높이)
    cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'
    cam.location = base + Vector((0, -scale * 4, 0)); cam.rotation_euler = (1.5707963, 0, 0)
    cam.data.ortho_scale = scale * 768 / 512 if False else scale * 1.0; cam.data.clip_end = 1e5
    sc.render.filepath = path; bpy.ops.render.render(write_still=True)


ext = {}
for side in ('before', 'after'):
    sc, lo, hi = load(side); ext[side] = (lo, hi)
H = max(max(hi.z - lo.z, (hi.x - lo.x) * 1.3, (hi.y - lo.y) * 1.3) for lo, hi in ext.values()) * 1.12
for side in ('before', 'after'):
    sc, lo, hi = load(side); shoot(sc, lo, hi, H * 512 / 768 if False else H, f'{OUTP}_{side}.png')
    print('RENDER', side, round(hi.z - lo.z, 3), round(H, 3))
