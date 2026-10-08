"""등급 조사(survey.json)가 가리키는 맵 안 이펙트 모델을 창고(15·16)에서 평평한 ~/GRD_orig_vfx_trial/<모델>/로 복사(2026-10-09, blender).
  GRADE=불멸 /usr/bin/python3 Tools/w3x/vfx_prep_models.py     (그 뒤 W/fbx → 평평한 폴더 심볼릭 링크도 만든다)
"""
import glob, json, os, re, shutil
H = os.path.expanduser("~"); GRADE = os.environ.get("GRADE", "불멸"); TAG = {"초월": "transcend", "불멸": "immortal", "영원": "eternal"}[GRADE]
T = H + "/GRD_orig_vfx_trial"; W = H + f"/GRD_motion_trial/{TAG}_vfx"
os.makedirs(W, exist_ok=True)
if not os.path.exists(W + "/fbx"): os.symlink(T, W + "/fbx")
wh = {}
for g in ("16_원랜디스킬", "15_원랜디스킨"):
    for p in glob.glob(f"{H}/Desktop/구랜디스킨모음/{g}/*/*"):
        if os.path.isdir(p): wh.setdefault(os.path.basename(p).lower(), p)
tag = lambda m: os.path.splitext(re.split(r"[\\/]", m)[-1])[0]
s = json.load(open(H + f"/GRD_cast_vfx_{GRADE}/survey.json"))
ms = {}
for u in s:
    for f in u["families"]:
        for t in f["triggers"]:
            for e in t["effects"]: ms[tag(e["model"])] = e
        for e in f.get("own", []): ms[tag(e["model"])] = e
add = miss = have = 0; missing = []
for m, e in sorted(ms.items()):
    if not e.get("inmap"): continue
    if os.path.isdir(f"{T}/{m}"): have += 1; continue
    src = wh.get(m.lower())
    if not src: miss += 1; missing.append(m); continue
    shutil.copytree(src, f"{T}/{m}"); add += 1
    pv = f"{T}/{m}/preview.png"
    if os.path.exists(pv): os.makedirs(T + "/_photos", exist_ok=True); shutil.move(pv, f"{T}/_photos/{m}.png")
print(GRADE, "이미", have, "추가", add, "창고에 없음", missing)
