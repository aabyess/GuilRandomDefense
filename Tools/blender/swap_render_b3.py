"""재수출 FBX 결과 렌더(blender3): Stand·Attack·Spell 각 1장(그 클립 한가운데 프레임, 그 클립에서 숨김인 메시는 감춤).
  blender -b --factory-startup --python Tools/blender/swap_render_b3.py -- <model_fixed 폴더(또는 짧은 액션 이름 사본)> <출력 접두어> [clip_map.json]
출력: <접두어>_stand.png · _attack.png · _spell.png (클립 kind는 ../clip_map.json의 첫 Idle/Attack/Spell)"""
import bpy, sys, os, glob, json
from mathutils import Vector
A = sys.argv[sys.argv.index('--') + 1:]
folder, outp = A[:2]
J = json.load(open(glob.glob(folder + '/*.json')[0])); CM = json.load(open(A[2] if len(A) > 2 else os.path.join(folder, '..', 'clip_map.json')))
vis = J['fixed']['clipVisibility']
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=glob.glob(folder + '/*.fbx')[0])
sc = bpy.context.scene
ids = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
sc.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in ids else 'BLENDER_EEVEE'
sc.render.resolution_x = sc.render.resolution_y = 512
w = bpy.data.worlds.new('w'); w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.12, 0.13, 0.16, 1); sc.world = w
for o in bpy.data.objects:
    if o.type != 'MESH': continue
    jm = next((m for m in J['meshes'] if o.name.startswith(m['mesh'])), None)
    for s in o.material_slots:
        m = s.material; m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
        tf = jm and jm.get('textureFile')
        p = os.path.join(folder, 'Textures', tf) if tf else None
        t = nt.nodes.new('ShaderNodeTexImage')
        if p and os.path.exists(p): t.image = bpy.data.images.load(p)
        e = nt.nodes.new('ShaderNodeEmission'); tr = nt.nodes.new('ShaderNodeBsdfTransparent'); mx = nt.nodes.new('ShaderNodeMixShader'); out = nt.nodes.new('ShaderNodeOutputMaterial')
        nt.links.new(t.outputs['Color'], e.inputs['Color']); nt.links.new(t.outputs['Alpha'], mx.inputs[0])
        nt.links.new(tr.outputs[0], mx.inputs[1]); nt.links.new(e.outputs[0], mx.inputs[2]); nt.links.new(mx.outputs[0], out.inputs[0])
        try: m.surface_render_method = 'BLENDED'
        except Exception: pass
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
arm.animation_data_create()
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'
def clip_for(kind):
    for c in CM['clips']:
        if c['kind'] == kind: return f"s{c['index']}_"
shots = [('stand', clip_for('Idle')), ('attack', clip_for('Attack')), ('spell', clip_for('Spell'))]
poses = []
for name, act_name in shots:
    if not act_name: continue
    a = next(x for x in bpy.data.actions if x.name.split('|')[-1].startswith(act_name))
    act_name = a.name.split('|')[-1]
    arm.animation_data.action = a
    sc.frame_set(int((a.frame_range[0] + a.frame_range[1]) / 2))
    for o in bpy.data.objects:
        if o.type == 'MESH':
            jm = next((m for m in J['meshes'] if o.name.startswith(m['mesh'])), None)
            o.hide_render = not (jm and vis[act_name].get(jm['mesh'], True))
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get(); pts = []
    for o in bpy.data.objects:
        if o.type == 'MESH' and not o.hide_render:
            ev = o.evaluated_get(dg); pts += [o.matrix_world @ v.co for v in ev.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))); hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    ext = [hi[k] - lo[k] for k in range(3)]; ax = ext.index(min(ext)); c = (lo + hi) / 2
    d = [Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))][ax]
    cam.location = c + d * max(ext) * 3 + Vector((0, 0.0001, 0)); cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
    cam.data.ortho_scale = max(ext) * 1.15 + 0.01; cam.data.clip_end = 1e5
    sc.render.filepath = f'{outp}_{name}.png'; bpy.ops.render.render(write_still=True)
    print('RENDER', name, act_name, [round(x, 2) for x in ext])
