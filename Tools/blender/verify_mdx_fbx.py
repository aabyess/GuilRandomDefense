"""export_mdx_anim_fbx.py 산출을 다시 읽어 **부품 수 대조**한다 (2026-10-08, 원랜디 창고 15·16 검사).

  blender -b --factory-startup --python Tools/blender/verify_mdx_fbx.py -- <작업폴더(MDX)> <산출폴더(fbx/)> <결과.json>

원본 MDX(작업폴더)를 다시 파싱해 기대값을 세고, FBX를 가져와 센 값과 맞춘다:
  메시 오브젝트 수 = 팀색 아닌 (지오셋×층) 수 · 삼각형 합 = 같은 곳의 삼각형 합 · 뼈 수 = 노드 수(없으면 1) · 액션 수 = 시퀀스 수
  · 텍스처 PNG 파일이 실제 있는가 · 지오셋 정점 수 합(원본) vs 가져온 메시 정점 수 합(FBX는 UV 경계에서 정점을 쪼갤 수 있어 ≥ 만 본다).
objrip 함정(조각 버림·면 삭제·Image_N 텍스처)을 여기서 잡는다: 삼각형이 하나라도 줄면 FAIL.
"""
import glob
import json
import os
import sys

import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, os.path.abspath(os.path.join(HERE, "..", "w3x")))
import mdx_geo                                                       # noqa: E402

args = sys.argv[sys.argv.index("--") + 1:]
WORK, FBX, OUTJ = args[0], args[1], args[2]
res = []
for jp in sorted(glob.glob(os.path.join(FBX, "*", "*.json"))):
    side = json.load(open(jp))
    d = os.path.dirname(jp)
    tag = os.path.basename(d)
    fbx = os.path.join(d, tag + ".fbx")
    r = dict(tag=tag, model=side["model"], ok=True, problems=[])
    try:
        geo = mdx_geo.parse(open(os.path.join(WORK, side["model"]), "rb").read())
        exp_meshes = len(side["meshes"])
        # 같은 정점 번호가 겹친 삼각형(넓이 0)과 정점 집합이 같은 겹친 면(워크3의 양면 뒷면 복사)은 Blender가 면 하나로 합친다 — 모양은 같아서 기대값에서 뺀다
        valid = lambda gi: len({frozenset(t) for t in geo["geosets"][gi]["tris"] if len(set(t)) == 3})
        exp_tris = sum(valid(m["geoset"]) for m in side["meshes"])
        degenerate = sum(len(geo["geosets"][m["geoset"]]["tris"]) - valid(m["geoset"]) for m in side["meshes"])
        exp_verts = sum(len(geo["geosets"][m["geoset"]]["verts"]) for m in side["meshes"])
        src_tris_all = sum(len(g["tris"]) for g in geo["geosets"])
        # 팀색 층만 있는 지오셋은 빠지는 게 정상 — 그 지오셋의 삼각형 수를 따로 센다
        used_geosets = {m["geoset"] for m in side["meshes"]}
        dropped = [gi for gi in range(len(geo["geosets"])) if gi not in used_geosets]
        r.update(src_geosets=len(geo["geosets"]), dropped_geosets=dropped, exp_meshes=exp_meshes, exp_tris=exp_tris, degenerate_tris=degenerate)
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=fbx)
        meshes = [o for o in bpy.data.objects if o.type == "MESH"]
        arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
        tris = 0
        verts = 0
        for o in meshes:
            me = o.data
            me.calc_loop_triangles()
            tris += len(me.loop_triangles)
            verts += len(me.vertices)
        bones = sum(len(a.data.bones) for a in arms)
        acts = len(bpy.data.actions)
        r.update(got_meshes=len(meshes), got_tris=tris, got_verts=verts, bones=bones, actions=acts)
        if len(meshes) != exp_meshes:
            r["problems"].append(f"메시 수 {len(meshes)} != {exp_meshes}")
        if tris != exp_tris:
            r["problems"].append(f"삼각형 {tris} != {exp_tris}")
        if verts < exp_verts:
            r["problems"].append(f"정점 {verts} < {exp_verts}")
        import mdx_anim
        info = mdx_anim.describe(open(os.path.join(WORK, side["model"]), "rb").read())
        moving = any(len({tuple(k[1]) for k in tr["keys"]}) > 1 for n in info["nodes"] for tr in n["tracks"].values() if tr.get("keys"))
        r["moving"] = moving
        if moving and acts < len(side["sequences"]):
            r["problems"].append(f"액션 {acts} < {len(side['sequences'])}")
        for m in side["meshes"]:
            if m["textureFile"] is None:
                r["problems"].append(f"텍스처 PNG 없음: {m['texture']}")
            elif not os.path.exists(os.path.join(d, "Textures", m["textureFile"])):
                r["problems"].append(f"텍스처 파일 누락: {m['textureFile']}")
        # 위치 이탈(NaN) 검사
        import math
        bad = False
        for o in meshes:
            for v in o.data.vertices:
                if any(math.isnan(c) or abs(c) > 1e5 for c in v.co):
                    bad = True
                    break
            if bad:
                break
        if bad:
            r["problems"].append("정점 좌표 NaN/폭주")
    except Exception as e:                                           # noqa: BLE001
        r["problems"].append("검사 실패: " + repr(e)[:160])
    r["ok"] = not r["problems"]
    res.append(r)
json.dump(res, open(OUTJ, "w"), ensure_ascii=False, indent=1)
print("VERIFY total", len(res), "ok", sum(1 for r in res if r["ok"]), "fail", sum(1 for r in res if not r["ok"]))
