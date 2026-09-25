"""산출 FBX의 메시가 **뼈에 실제로 붙어 있는지** 센다(2026-09-25, PM 요청).

왜 필요한가 — R24 빈스모크 저지의 머리 앞 부품이 스킨도, 가중치 그룹도 없이 뼈대 오브젝트에만 붙어 나갈 뻔했다.
블렌더 렌더는 멀쩡하다(동작이 작으면 티가 안 난다). 그런데 유니티는 **가중치 없는 정점을 첫 뼈에 강제로 붙이므로**
몸이 움직이면 그 부품이 제자리에 남거나 늘어난다. 정지 렌더·clip_spread·mesh_clash 어느 것도 이 층을 안 본다.

세는 것(뼈대가 있는 FBX만. 뼈대 없는 정적 모델은 목록에만 적는다):
  ① 스킨없음      Armature 수정자가 없고 뼈에도 안 붙은(parent_type≠BONE) 메시
  ② 무가중치정점   스킨은 있는데 그 뼈대 뼈들의 가중치 합이 0인 정점

전수 검사 첫 결과(2026-09-25, Units+Enemies 236개): ① 0개 · ② 히든_최경범 Plug 메시 셋(224·24·24 전부 0) · 영원_조세민 5정점.

쓰는 법:
  blender -b --factory-startup --python Tools/blender/bind_check.py -- <fbx> [<fbx> ...]
  종료 코드 = 문제 있는 파일 수(0이면 통과). check_entries가 산출마다 부른다.
"""
import os
import sys

import bpy


def check(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=path)
    arms = [o for o in bpy.data.objects if o.type == "ARMATURE"]
    if not arms:
        return None                                                     # 정적 모델 — 대상 아님
    rows = []
    for m in [o for o in bpy.data.objects if o.type == "MESH"]:
        skin = next((x.object for x in m.modifiers if x.type == "ARMATURE" and x.object), None)
        if skin is None:
            if not (m.parent_type == "BONE" and m.parent_bone):
                rows.append(f"① 스킨없음 {m.name}(정점 {len(m.data.vertices)}, 그룹 {len(m.vertex_groups)})")
            continue
        bones = {b.name for b in skin.data.bones}
        gi = {g.index: g.name for g in m.vertex_groups}
        zero = sum(1 for v in m.data.vertices if sum(g.weight for g in v.groups if gi.get(g.group) in bones) <= 1e-6)
        if zero:
            rows.append(f"② 무가중치정점 {m.name} {zero}/{len(m.data.vertices)}")
    return rows


def main():
    files = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    bad = 0
    for f in files:
        rows = check(f)
        name = os.path.basename(f)
        if rows is None:
            print(f"BIND\t정적\t{name}\t뼈대 없음(대상 아님)")
        elif rows:
            bad += 1
            for r in rows:
                print(f"BIND\tFAIL\t{name}\t{r}")
        else:
            print(f"BIND\tOK\t{name}")
    sys.exit(min(bad, 100))


main()
