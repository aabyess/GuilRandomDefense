"""고유 동작 산출 FBX 결함 검사 한 번에(2026-10-03, 5묶음) — skin-verify-procedure · skin-import-traps의 넷을 숫자로.
   blender -b --factory-startup --python Tools/blender/verify_clips5.py -- <유닛.fbx> [...]
찍는 것(줄마다 OK/주의):
  KEY      키(z 최대) — 1.8 언저리(사람형·적). 크게 벗어나면 주의.
  FLIP     면 법선을 재계산했을 때 **뒤집히는 면 비율**(메시별). 0.75↑는 컬링에서 사라진다.  ※ 닫힌 껍데기 기준 — 얇은 판·옷자락은 값이 크게 나올 수 있다.
  TEX      재질별 텍스처 파일 · **전부 순흑색인 작은 PNG**(64×64류) · 텍스처 없는 재질(= 기본색 그대로)
  EMIT     재질 Emission 색이 검지 않은 것(몸 전체가 허옇게 빛나는 함정)
  BONE     뼈 꼬리: 다리 뼈 사슬이 위를 향하는지(머리 z < 꼬리 z인 Thigh/Leg)
  ARM      아마추어 최상위 개수(1) · Hips 부모 없음
"""
import os
import sys

import bpy
import bmesh
import numpy as np
from mathutils import Vector


def check(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    sc = bpy.context.scene
    name = os.path.basename(path)
    arms = [o for o in sc.objects if o.type == "ARMATURE"]
    meshes = [o for o in sc.objects if o.type == "MESH"]
    tops = [o for o in sc.objects if o.parent is None]
    pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
    zmax, zmin = max(p.z for p in pts), min(p.z for p in pts)
    print(f"[{name}] KEY zmax {zmax:.3f} zmin {zmin:.3f}  메시 {len(meshes)}  최상위 {[o.name for o in tops]}")
    for o in meshes:
        bm = bmesh.new()
        bm.from_mesh(o.data)
        bm.faces.ensure_lookup_table()
        before = [f.normal.copy() for f in bm.faces]
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        flipped = sum(1 for f, n in zip(bm.faces, before) if f.normal.dot(n) < 0)
        r = flipped / max(len(bm.faces), 1)
        bm.free()
        tag = "주의" if r > 0.75 else "OK"
        print(f"  FLIP {tag} {o.name}: {flipped}/{len(before)} = {r:.2f}")
    seen = set()
    for o in meshes:
        for slot in o.material_slots:
            m = slot.material
            if m is None or m.name in seen:
                continue
            seen.add(m.name)
            imgs = [n.image for n in (m.node_tree.nodes if m.node_tree else []) if n.type == "TEX_IMAGE" and n.image]
            info = []
            for im in imgs:
                px = np.empty(len(im.pixels), dtype=np.float32)
                im.pixels.foreach_get(px)
                px = px.reshape(-1, 4)
                black = px[:, :3].max() < 0.02
                info.append(f"{os.path.basename(im.filepath)} {im.size[0]}x{im.size[1]} 평균{px[:, :3].mean():.2f}{' ★순흑색' if black else ''}")
            tag = "OK" if imgs and not any("순흑색" in s for s in info) else "주의"
            print(f"  TEX {tag} {m.name}: {info or '텍스처 없음(기본색)'}")
            if m.node_tree:
                for n in m.node_tree.nodes:
                    if n.type == "BSDF_PRINCIPLED":
                        ec = n.inputs["Emission Color"].default_value
                        st = n.inputs["Emission Strength"].default_value
                        if st > 0 and max(ec[:3]) > 0.05:
                            print(f"  EMIT 주의 {m.name}: 색 {tuple(round(c, 2) for c in ec[:3])} 세기 {st}")
    for a in arms:
        bad = []
        for b in a.data.bones:
            if any(k in b.name for k in ("UpLeg", "Thigh", "thigh", "upleg")):
                if b.head_local.z < b.tail_local.z - 1e-4 and b.tail_local.z > b.head_local.z:
                    bad.append(b.name)
        hips = next((b for b in a.data.bones if b.name.endswith("Hips")), None)
        print(f"  BONE {'주의 다리 뼈 위향 ' + str(bad) if bad else 'OK'}  ARM {len(arms)}개 Hips부모 {hips.parent.name if hips and hips.parent else None}")


for p in sys.argv[sys.argv.index("--") + 1:]:
    check(p)
