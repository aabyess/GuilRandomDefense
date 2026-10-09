"""S2 날개 Unity용 묶음(blender, 2026-10-09): ~/GRD_wings/_raw/<이름>/(export_mdx_anim_fbx 산출) → ~/GRD_wings/<이름>/{<이름>.fbx, Textures/, wing.json, preview.png}
  blender -b --factory-startup --python Tools/blender/wing_pack.py -- <이름> [<이름> …]
wing.json: 재질별 필터(additive 등)·텍스처·UV 이동·알파 곡선(원본 json의 meshes), 클립(이름·프레임·30fps·반복), 크기·부착(원작 chest) 정보.
미리보기: Stand 한가운데 프레임, 뒤(+Y)·앞(-Y)에서 두 컷(검은 바탕, 가산은 발광으로)."""
import bpy, sys, os, glob, json, shutil
from mathutils import Vector
H = os.path.expanduser('~'); RAW = H + '/GRD_wings/_raw'; OUT = H + '/GRD_wings'
names = sys.argv[sys.argv.index('--') + 1:]


def render(folder, fbx, J, png):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=fbx); sc = bpy.context.scene
    ids = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
    sc.render.engine = 'BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in ids else 'BLENDER_EEVEE'
    sc.render.resolution_x, sc.render.resolution_y = 640, 480
    w = bpy.data.worlds.new('w'); w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (0.05, 0.055, 0.07, 1); sc.world = w
    for o in bpy.data.objects:
        if o.type != 'MESH': continue
        jm = next((m for m in J['meshes'] if o.name.startswith(m['mesh'])), None)
        for s in o.material_slots:
            m = s.material; m.use_nodes = True; nt = m.node_tree; nt.nodes.clear()
            p = os.path.join(folder, 'Textures', jm['textureFile']) if jm and jm.get('textureFile') else None
            t = nt.nodes.new('ShaderNodeTexImage')
            if p and os.path.exists(p): t.image = bpy.data.images.load(p)
            e = nt.nodes.new('ShaderNodeEmission'); tr = nt.nodes.new('ShaderNodeBsdfTransparent'); out = nt.nodes.new('ShaderNodeOutputMaterial')
            nt.links.new(t.outputs['Color'], e.inputs['Color'])
            f = (jm or {}).get('filter', 'none')
            if f == 'additive':
                ad = nt.nodes.new('ShaderNodeAddShader'); nt.links.new(tr.outputs[0], ad.inputs[0]); nt.links.new(e.outputs[0], ad.inputs[1]); nt.links.new(ad.outputs[0], out.inputs[0])
            else:
                mx = nt.nodes.new('ShaderNodeMixShader'); nt.links.new(t.outputs['Alpha'], mx.inputs[0]); nt.links.new(tr.outputs[0], mx.inputs[1]); nt.links.new(e.outputs[0], mx.inputs[2]); nt.links.new(mx.outputs[0], out.inputs[0])
            try: m.surface_render_method = 'BLENDED'
            except Exception: pass
    arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]; arm.animation_data_create()
    a = next((x for x in bpy.data.actions if 'Death' not in x.name), bpy.data.actions[0]); arm.animation_data.action = a
    sc.frame_set(int((a.frame_range[0] + a.frame_range[1]) / 2)); bpy.context.view_layer.update()
    pts = []
    dg = bpy.context.evaluated_depsgraph_get()
    for o in bpy.data.objects:
        if o.type == 'MESH':
            ev = o.evaluated_get(dg); pts += [o.matrix_world @ v.co for v in ev.data.vertices]
    lo = Vector([min(p[k] for p in pts) for k in range(3)]); hi = Vector([max(p[k] for p in pts) for k in range(3)]); c = (lo + hi) / 2
    cam = bpy.data.objects.new('c', bpy.data.cameras.new('c')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.clip_end = 1e5
    cam.data.ortho_scale = max(hi.x - lo.x, (hi.z - lo.z) * 4 / 3) * 1.15 + 0.01
    for side, sgn in (('back', 1), ('front', -1)):
        cam.location = c + Vector((0, sgn * 20, 0)); cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = png.replace('.png', f'_{side}.png'); bpy.ops.render.render(write_still=True)
    return [round(x, 3) for x in lo], [round(x, 3) for x in hi], a


for n in names:
    src = f'{RAW}/{n}'; dst = f'{OUT}/{n}'; os.makedirs(dst, exist_ok=True)
    shutil.copy(f'{src}/{n}.fbx', dst); shutil.rmtree(dst + '/Textures', ignore_errors=True); shutil.copytree(src + '/Textures', dst + '/Textures')
    J = json.load(open(f'{src}/{n}.json'))
    lo, hi, a = render(src, f'{src}/{n}.fbx', J, f'{dst}/preview.png')
    clip = dict(name=a.name.split('|')[-1], frameStart=round(a.frame_range[0]), frameEnd=round(a.frame_range[1]), fps=30, loop=True, note='날갯짓(Stand 반복). FBX 액션 이름이 길면 Blender가 63자에서 자르므로 모델당 클립 하나뿐')
    wing = dict(name=n, source='S2 대시보드 날개 모델(원작 저작물 — Assets 반입은 PM·사장님 판단)', unitScale='FBX 1 = 1m = 원작 100단위(WC3 1단위=0.01m)', clip=clip,
                bboxMeters=dict(min=lo, max=hi, size=[round(hi[i] - lo[i], 3) for i in range(3)]),
                attach=dict(original='chest', note='원작은 유닛의 chest 부착점에 이 모델의 원점을 맞췄다. 원점(0,0,0)=chest 부착점, +Y=등 쪽(뒤)·+Z=위·±X=좌우. 몸통 안쪽으로는 파고들지 않고 날개는 원점 위쪽·뒤쪽으로 펼쳐진다(bboxMeters 참고)',
                            sizeRef='모델 크기 그대로(스케일 1.0)가 원작 기준. 원작 영웅 모델 키는 대략 1.2~1.9m라 날개 폭은 키의 2~3배 — 우리 유닛에는 유닛 키 비율로 맞출 것: 날개 폭 ≈ 유닛 키 × 2.2 권장(조정 필요)'),
                materials=[{k: m.get(k) for k in ('mesh', 'filter', 'suffix', 'texture', 'textureFile', 'staticAlpha', 'geosetAlphaKeys', 'layerAlphaKeys', 'uvAnim', 'uvOffsetClip0', 'approxTexture')} for m in J['meshes']],
                filterGuide={'additive': 'Unity: 가산(Additive) 파티클/URP Unlit Blend One One', 'addalpha': '알파×가산(Premultiplied/Additive, 알파 곱)', 'blend': '알파 혼합(Transparent)', 'transparent': '알파 컷(Cutout, clip 0.75)', 'none': '불투명'},
                emitters='없음 — 이 6개 MDX에는 PRE2/PREM/RIBB(파티클·리본) 청크가 하나도 없다(청크 목록: GEOS·BONE·HELP·PIVT·TXAN·SEQS…). 흰/검/왜곡 날개도 지오셋(판)과 뼈 애니메이션이다',
                sequences=J['sequences'], nodes=J['nodes'])
    json.dump(wing, open(f'{dst}/wing.json', 'w'), ensure_ascii=False, indent=1)
    print('PACK', n, wing['bboxMeters']['size'])
