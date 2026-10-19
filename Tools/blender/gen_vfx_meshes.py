"""VFX Graph용 저폴리 메시 3종(blender, 2026-10-19): 링(납작 토러스)·반구·원뿔. 긴 변 1 단위로 정규화, 피벗=경계 중앙, UV 0~1 펼침, 자작(출처 없음·CC0 취급).
  blender -b --factory-startup --python Tools/blender/gen_vfx_meshes.py -- [출력 폴더]
산출: <out>/VfxRing.fbx · VfxHemisphere.fbx · VfxCone.fbx  (Y-up·m 단위, 크기 1)"""
import bpy, bmesh, sys, os, math
a = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = os.path.expanduser(a[0] if a else "~/grd/GRD_vfx_graph/재료/메시"); os.makedirs(OUT, exist_ok=True)


def fresh():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def finish(o, name):
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    bpy.ops.object.origin_set(type="ORIGIN_GEOMETRY", center="BOUNDS"); o.location = (0, 0, 0)
    m = max(o.dimensions); o.scale = (1 / m,) * 3; bpy.ops.object.transform_apply(location=True, scale=True, rotation=True)
    o.name = name; o.data.name = name; bpy.ops.object.shade_smooth()
    bpy.ops.export_scene.fbx(filepath=f"{OUT}/{name}.fbx", use_selection=True, object_types={"MESH"}, apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", mesh_smooth_type="FACE", path_mode="STRIP", bake_anim=False)
    print("OK", name, [round(x, 3) for x in o.dimensions], len(o.data.polygons), "면")


def uv_unwrap_box_free(o):
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.uv.smart_project(angle_limit=1.2, island_margin=0.02); bpy.ops.object.mode_set(mode="OBJECT")


# 링: 납작 토러스(바깥 지름 1, 두께 약 0.12). UV는 토러스 기본(고리 방향 U 0~1·단면 V 0~1)
fresh(); bpy.ops.mesh.primitive_torus_add(major_radius=0.42, minor_radius=0.08, major_segments=32, minor_segments=8)
o = bpy.context.active_object; o.scale = (1, 1, 0.45); bpy.ops.object.transform_apply(scale=True)
bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT"); bpy.ops.uv.unwrap(method="ANGLE_BASED"); bpy.ops.object.mode_set(mode="OBJECT")
if not o.data.uv_layers: uv_unwrap_box_free(o)
finish(o, "VfxRing")

# 반구: 위 반쪽 UV 구(지름 1). V 0~1을 위 반쪽에 다시 펼침, 바닥은 닫지 않음(돔 안쪽 보임)
fresh(); bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=12, radius=0.5); o = bpy.context.active_object
bm = bmesh.new(); bm.from_mesh(o.data); bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.calc_center_median().z < -1e-4], context="FACES")
bm.to_mesh(o.data); bm.free()
for v in o.data.vertices: pass
uvl = o.data.uv_layers.active
if uvl:
    for loop in o.data.loops:
        u, v = uvl.data[loop.index].uv; uvl.data[loop.index].uv = (u, min(1.0, max(0.0, (v - 0.5) * 2)))
finish(o, "VfxHemisphere")

# 원뿔: 지름 0.5·높이 1 → 긴 변(높이) 1. 옆면 UV는 기본(0~1)
fresh(); bpy.ops.mesh.primitive_cone_add(vertices=24, radius1=0.5, radius2=0, depth=1.0); o = bpy.context.active_object
uv_unwrap_box_free(o); finish(o, "VfxCone")
