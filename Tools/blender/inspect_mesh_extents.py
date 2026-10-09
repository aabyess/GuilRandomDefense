import bpy,sys,json
from mathutils import Vector
f=sys.argv[sys.argv.index('--')+1]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.fbx(filepath=f)
arm=[o for o in bpy.data.objects if o.type=='ARMATURE'][0]
print('ARM scale',tuple(arm.scale),'loc',tuple(arm.location),'rot',tuple(round(x,2) for x in arm.rotation_euler),'dims', [round(x,2) for x in arm.dimensions])
for o in bpy.data.objects:
    if o.type=='MESH':
        vs=[v.co for v in o.data.vertices]
        print(o.name,'raw local bbox',[round(max(v[i] for v in vs)-min(v[i] for v in vs),2) for i in range(3)],'obj scale',tuple(round(x,3) for x in o.scale),'parent',o.parent.name if o.parent else None,'mods',[m.type for m in o.modifiers],'vgroups',len(o.vertex_groups))
arm.animation_data_create(); mx={}
for a in bpy.data.actions:
    arm.animation_data.action=a
    for fr in range(int(a.frame_range[0]),int(a.frame_range[1])+1,3):
        bpy.context.scene.frame_set(fr); dg=bpy.context.evaluated_depsgraph_get()
        for o in bpy.data.objects:
            if o.type=='MESH':
                ev=o.evaluated_get(dg); pts=[o.matrix_world@Vector(c) for c in ev.bound_box]; e=max(max(p[i] for p in pts)-min(p[i] for p in pts) for i in range(3))
                k=(a.name.split('|')[-1],o.name[-9:]); mx[k]=max(mx.get(k,0),e)
print('MAXEXT',json.dumps({f'{k[0]}/{k[1]}':round(v,2) for k,v in mx.items()},ensure_ascii=False))
