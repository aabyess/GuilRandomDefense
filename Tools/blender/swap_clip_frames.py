"""교체 모델의 한 클립을 프레임별로 찍는 시트(blender, 2026-10-09): 머리 사라짐·망토 뒤집힘 같은 클립 도중 문제 확인용.
  blender -b --factory-startup --python Tools/blender/swap_clip_frames.py -- <model_fixed 폴더> <액션 앞부분(s4_)> <출력 png> [카메라 고도(도, 기본 62)] [프레임 간격 기본 2]"""
import bpy, sys, os, glob, json, math
from mathutils import Vector
A = sys.argv[sys.argv.index('--') + 1:]; folder, pre, outp = A[:3]; elev = float(A[3]) if len(A) > 3 else 62; step = int(A[4]) if len(A) > 4 else 2
J = json.load(open(glob.glob(folder + '/*.json')[0])); vis = J['fixed']['clipVisibility']
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=glob.glob(folder + '/*.fbx')[0]); sc = bpy.context.scene
ids = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
sc.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in ids else 'BLENDER_EEVEE'; sc.render.resolution_x = sc.render.resolution_y = 256
w = bpy.data.worlds.new('w'); w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.2, 0.4, 0.2, 1); sc.world = w
for o in bpy.data.objects:
    if o.type != 'MESH': continue
    jm = next((m for m in J['meshes'] if o.name.startswith(m['mesh'])), None)
    for s in o.material_slots:
        m = s.material; m.use_nodes = True; nt = m.node_tree; nt.nodes.clear(); tf = jm and jm.get('textureFile'); p = os.path.join(folder, 'Textures', tf) if tf else None
        t = nt.nodes.new('ShaderNodeTexImage')
        if p and os.path.exists(p): t.image = bpy.data.images.load(p)
        e = nt.nodes.new('ShaderNodeEmission'); tr = nt.nodes.new('ShaderNodeBsdfTransparent'); mx = nt.nodes.new('ShaderNodeMixShader'); out = nt.nodes.new('ShaderNodeOutputMaterial')
        nt.links.new(t.outputs['Color'], e.inputs['Color']); nt.links.new(t.outputs['Alpha'], mx.inputs[0]); nt.links.new(tr.outputs[0], mx.inputs[1]); nt.links.new(e.outputs[0], mx.inputs[2]); nt.links.new(mx.outputs[0], out.inputs[0])
        try: m.surface_render_method = 'BLENDED'
        except Exception: pass
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; arm.animation_data_create()
a = next(x for x in bpy.data.actions if x.name.split('|')[-1].startswith(pre)); an = a.name.split('|')[-1]; arm.animation_data.action = a
for o in bpy.data.objects:
    if o.type == 'MESH':
        jm = next((m for m in J['meshes'] if o.name.startswith(m['mesh'])), None); o.hide_render = not (jm and vis[an].get(jm['mesh'], True))
cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.clip_end = 1e5
f0, f1 = [int(x) for x in a.frame_range]; files = []
sc.frame_set(f0); bpy.context.view_layer.update(); dg = bpy.context.evaluated_depsgraph_get(); pts = []
for o in bpy.data.objects:
    if o.type == 'MESH' and not o.hide_render: pts += [o.matrix_world @ v.co for v in o.evaluated_get(dg).data.vertices]
c = Vector([(min(p[k] for p in pts) + max(p[k] for p in pts)) / 2 for k in range(3)]); sz = max(max(p[k] for p in pts) - min(p[k] for p in pts) for k in range(3)) * 1.5
cam.data.ortho_scale = sz; e = math.radians(elev); cam.location = c + Vector((0, -20 * math.cos(e), 20 * math.sin(e))); cam.rotation_euler = (math.radians(90 - elev), 0, 0)
for fr in range(f0, f1 + 1, step):
    sc.frame_set(fr); sc.render.filepath = f'/tmp/_scf_{fr:03d}.png'; bpy.ops.render.render(write_still=True); files.append(sc.render.filepath)
print('FRAMES', an, len(files))
open('/tmp/_scf_list.txt', 'w').write('\n'.join(files))
