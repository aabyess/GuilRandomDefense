import bpy,sys,glob,json
from mathutils import Vector
f=sys.argv[sys.argv.index('--')+1]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=f)
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
def bb():
    dg=bpy.context.evaluated_depsgraph_get(); r={}
    for o in bpy.data.objects:
        if o.type=='MESH':
            ev=o.evaluated_get(dg); pts=[o.matrix_world@Vector(c) for c in ev.bound_box]
            r[o.name]=[round(max(p[i] for p in pts)-min(p[i] for p in pts),2) for i in range(3)]
    return r
arm.animation_data_create(); arm.animation_data.action=None; bpy.context.view_layer.update()
print('REST',json.dumps(bb(),ensure_ascii=False))
for a in bpy.data.actions:
    if 'stand' in a.name.lower() and 'ready' not in a.name.lower():
        arm.animation_data.action=a; bpy.context.scene.frame_set(int(a.frame_range[0])); print('STAND',a.name,json.dumps(bb(),ensure_ascii=False)); break
print('bones scale0:',[b.name for b in arm.data.bones if b.matrix_local.to_scale().length<1e-3][:5])
