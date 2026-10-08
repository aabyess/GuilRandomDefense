import bpy,sys,os,json,glob
from mathutils import Vector
a=sys.argv[sys.argv.index('--')+1:]; folder,out,startms=a[0],a[1],float(a[2])
tag=os.path.basename(folder); fbx=glob.glob(folder+'/*.fbx')[0]; J=json.load(open(glob.glob(folder+'/*.json')[0]))
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=fbx)
sc=bpy.context.scene
ids=[e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
sc.render.engine='BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in ids else 'BLENDER_EEVEE'
sc.render.resolution_x=sc.render.resolution_y=360
w=bpy.data.worlds.new('w'); w.use_nodes=True; w.node_tree.nodes['Background'].inputs[0].default_value=(0.12,0.13,0.16,1); sc.world=w
MAP={}
for o in list(bpy.data.objects):
    if o.type!='MESH': continue
    jm=next((m for m in J['meshes'] if m['mesh']==o.name or o.name.startswith(m['mesh'])),None)
    if not jm: bpy.data.objects.remove(o); continue
    for s in o.material_slots:
        m=s.material; m.use_nodes=True; nt=m.node_tree; nt.nodes.clear()
        img=bpy.data.images.load(folder+'/Textures/'+jm['textureFile']) if jm.get('textureFile') and os.path.exists(folder+'/Textures/'+jm['textureFile']) else None
        t=nt.nodes.new('ShaderNodeTexImage'); t.image=img; mp=nt.nodes.new('ShaderNodeMapping'); tc=nt.nodes.new('ShaderNodeTexCoord')
        nt.links.new(tc.outputs['UV'],mp.inputs[0]); nt.links.new(mp.outputs[0],t.inputs[0])
        e=nt.nodes.new('ShaderNodeEmission'); tr=nt.nodes.new('ShaderNodeBsdfTransparent'); add=nt.nodes.new('ShaderNodeAddShader'); o_=nt.nodes.new('ShaderNodeOutputMaterial')
        nt.links.new(t.outputs[0],e.inputs[0]); nt.links.new(tr.outputs[0],add.inputs[0]); nt.links.new(e.outputs[0],add.inputs[1]); nt.links.new(add.outputs[0],o_.inputs[0])
        try: m.surface_render_method='BLENDED'
        except: pass
        li=J['meshes'].index(jm); uo=(J.get('unity',{}).get('layers') or [{}]*len(J['meshes']))[li].get('uvOffsetClip0')
        keys=[[int(k[0]*1000),(k[1],k[2])] for k in uo['keys']] if uo else None      # unity.layers[].uvOffsetClip0(첫 클립 기준 초) 우선 — 폴더마다 meshes[].uvAnim이 없는 것이 있었다(10-09 레일건)
        if keys is None:
            ua=jm.get('uvAnim'); keys=((ua or {}).get('tracks') or {}).get('KTAT',{}).get('keys') if ua else None
            if keys: keys=[[k[0]-startms,k[1]] for k in keys]
        MAP[mp.name+str(id(mp))]=(mp,keys)
arm=[o for o in bpy.data.objects if o.type=='ARMATURE']
act=None
if arm and bpy.data.actions:
    act=bpy.data.actions[0]; arm[0].animation_data.action=act
cam=bpy.data.objects.new('c',bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera=cam; cam.data.type='ORTHO'
dur=J['sequences'][0]['seconds']
def sample(keys,t):
    k=[x for x in keys if x[0]<=t]; return k[-1][1] if k else keys[0][1]
frames=[]
NF=int(os.environ.get('NF','4'))
for i in range(NF):
    sec=dur*(i+0.5)/NF
    if act: sc.frame_set(int(act.frame_range[0]+sec*30))
    for mp,keys in MAP.values():
        if keys:
            u,v=sample(keys,sec*1000)[:2]; mp.inputs[1].default_value=(u,-v,0)
    bpy.context.view_layer.update()
    pts=[]
    dg=bpy.context.evaluated_depsgraph_get()
    for o in bpy.data.objects:
        if o.type=='MESH': ev=o.evaluated_get(dg); pts+=[o.matrix_world@v.co for v in ev.data.vertices]
    lo=Vector((min(p.x for p in pts),min(p.y for p in pts),min(p.z for p in pts)));hi=Vector((max(p.x for p in pts),max(p.y for p in pts),max(p.z for p in pts)))
    ext=[hi[k]-lo[k] for k in range(3)]; ax=ext.index(min(ext)); c=(lo+hi)/2
    d=[Vector((1,0,0)),Vector((0,1,0)),Vector((0,0,1))][ax]
    cam.location=c+d*max(ext)*3+Vector((0,0.0001,0)); cam.rotation_euler=(c-cam.location).to_track_quat('-Z','Y').to_euler()
    cam.data.ortho_scale=max(ext)*1.2+0.01; cam.data.clip_end=1e5
    sc.render.filepath=f'{out}_{tag}_{i}.png'; bpy.ops.render.render(write_still=True)
