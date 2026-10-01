"""고유 동작 전수조사 2단계(2026-10-01): 원본 모델 파일을 열어 **자체 클립·뼈대·메시**를 잰다. Blender 안에서.

  blender -b --factory-startup --python Tools/blender/survey_clips.py -- <jobs.json> <조각번호> <조각수> <출력.jsonl>
jobs.json = survey_extract.py가 만든 것. 클립마다: 이름·프레임·fps·길이 + (최대 24개) 첫↔끝 이음새·움직임 크기·뿌리 이동·최저 뼈 높이(모델 키 대비 비율).
읽기 전용 — 에셋·저장소 안 건드린다.
"""
import json, math, os, sys, traceback
import bpy
from mathutils import Vector

jobs_path, k, n, out = sys.argv[sys.argv.index("--") + 1:][:4]
k, n = int(k), int(n)
jobs = json.load(open(jobs_path))["jobs"][k::n]
fo = open(out, "w")


def clear():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def do_import(path):
    low = path.lower()
    if low.endswith((".glb", ".gltf")):
        bpy.ops.import_scene.gltf(filepath=path)
    else:
        bpy.ops.import_scene.fbx(filepath=path)


def height():
    pts = [o.matrix_world @ v.co for o in bpy.context.scene.objects if o.type == "MESH" for v in o.data.vertices]
    if not pts:
        return 0.0, 0
    return max(p.z for p in pts) - min(p.z for p in pts), len(pts)


def hips_of(arm):
    roots = [b for b in arm.pose.bones if b.parent is None]
    best = None
    for b in arm.pose.bones:
        nm = b.name.lower()
        if any(t in nm for t in ("hips", "pelvis", "hip")) and "twist" not in nm:
            best = b
            break
    return best or (roots[0] if roots else None)


for job in jobs:
    rec = dict(job)
    try:
        clear()
        do_import(job["model"])
        sc = bpy.context.scene
        arms = [o for o in sc.objects if o.type == "ARMATURE"]
        meshes = [o for o in sc.objects if o.type == "MESH"]
        H, nv = height()
        rec.update(ok=True, height=round(H, 3), verts=nv, fps=sc.render.fps, armatures=len(arms),
                   bones=sum(len(a.data.bones) for a in arms),
                   bone_sample=[b.name for a in arms for b in a.data.bones][:6],
                   meshes=sorted(((o.name, len(o.data.vertices)) for o in meshes), key=lambda x: -x[1])[:60], mesh_count=len(meshes),
                   objects_other=[o.name for o in sc.objects if o.type not in ("MESH", "ARMATURE")][:20])
        acts = []
        arm = arms[0] if arms else None
        # 🔴 FBX 가져오기는 테이크마다 **노드별 액션**으로 쪼갠다(「<노드이름>|<테이크>|Base Layer」, 노드 224개면 액션 수천 개) —
        #   뼈대 오브젝트 몫만 골라야 한다(안 거르면 다른 노드의 액션을 뼈대에 입혀 「움직임 0」으로 나온다). glb는 이름에 접두가 없어 전부.
        mine = [a for a in bpy.data.actions if arm and a.name.startswith(arm.name + "|")]
        pool = mine or list(bpy.data.actions)
        rec["actions_total"] = len(bpy.data.actions)
        for a in pool:
            f0, f1 = a.frame_range
            acts.append(dict(name=a.name, f0=round(f0), f1=round(f1), frames=round(f1 - f0) + 1))
        rec["actions"] = acts
        if arm and acts and H > 1e-6 and not os.environ.get("NOACT"):
            if arm.animation_data is None:
                arm.animation_data_create()
            hb = hips_of(arm)
            todo = [a for a in pool if (a.frame_range[1] - a.frame_range[0]) >= 3][:40]
            for a in todo:
                ent = next(e for e in acts if e["name"] == a.name)
                try:
                    arm.animation_data.action = a
                    f0, f1 = int(a.frame_range[0]), int(a.frame_range[1])
                    snaps = []
                    for f in (f0, f0 + (f1 - f0) // 4, f0 + (f1 - f0) // 2, f0 + 3 * (f1 - f0) // 4, f1):
                        sc.frame_set(f)
                        bpy.context.view_layer.update()
                        snaps.append([(arm.matrix_world @ pb.head).copy() for pb in arm.pose.bones])
                    first, last = snaps[0], snaps[-1]
                    seam = math.sqrt(sum((p - q).length_squared for p, q in zip(first, last)) / len(first))
                    motion = max(max((p - q).length for p, q in zip(first, s)) for s in snaps)
                    minz = min(min(p.z for p in s) for s in snaps)
                    ent.update(seam=round(seam / H, 4), motion=round(motion / H, 3), minbone_z=round(minz / H, 3))
                    if hb is not None:
                        i = [pb.name for pb in arm.pose.bones].index(hb.name)
                        d = snaps[-1][i] - snaps[0][i]
                        ent["root_drift_xy"] = round(math.hypot(d.x, d.y) / H, 3)
                        ent["root_z_range"] = round((max(s[i].z for s in snaps) - min(s[i].z for s in snaps)) / H, 3)
                except Exception as e:                                           # 한 클립이 터져도 나머지는 잰다
                    ent["err"] = str(e)[:80]
            arm.animation_data.action = None
    except Exception as e:
        rec.update(ok=False, err=(str(e) or traceback.format_exc())[-300:])
    fo.write(json.dumps(rec, ensure_ascii=False) + "\n")
    fo.flush()
fo.close()
