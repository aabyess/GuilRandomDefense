"""구랜디 현재 스킨 격자용 목록: Assets/Prefabs/Generated/Unit_<등급>_<이름>.prefab이 쓰는 FBX(프리팹 변형의 SourcePrefab guid → fbx.meta).
  /usr/bin/python3 Tools/blender/ours_grid_items.py <출력 items.json>   (Assets는 읽기만) — 등급 「고대의배」·「해적선」은 유닛이 아니라 뺀다."""
import re, glob, os, sys, json
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
os.chdir(ROOT)
g = {}
for m in glob.glob("Assets/**/*.fbx.meta", recursive=True):
    r = re.search(r"guid: (\w+)", open(m).read()); g[r.group(1)] = m[:-5]
items = []
for f in sorted(glob.glob("Assets/Prefabs/Generated/Unit_*.prefab")):
    grade, _, name = os.path.basename(f)[5:-7].partition("_")
    if grade in ("고대의배", "해적선"): continue
    t = open(f).read()
    gs = set(re.findall(r"m_SourcePrefab: \{fileID: \d+, guid: (\w+)", t)) | set(re.findall(r"guid: (\w+), type: 3\}\n\s+propertyPath", t))
    fb = sorted(g[x] for x in gs if x in g)
    items.append(dict(grade=grade, name=name, fbx=os.path.abspath(fb[0]) if fb else None, prefab=f))
json.dump(items, open(sys.argv[1], "w"), ensure_ascii=False, indent=1); print(len(items), "기", sum(1 for i in items if not i["fbx"]), "기 모델 없음")
