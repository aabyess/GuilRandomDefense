"""고유 동작 전수조사 1단계(2026-10-01): 구랜디스킨모음의 유닛·적 원본을 풀어 **모델 파일 목록(jobs.json)**을 만든다. 시스템 python.

  python3 Tools/blender/survey_extract.py   →  ~/GRD_motion_trial/_work/survey/{x/…(풀린 것), jobs.json}
유닛 이름(Assets/Art/Units/*)이 상대 경로에 들어 있는 파일을 그 유닛의 원본으로 본다(+ 90_적유닛 전부는 경로 이름으로). 압축은 bsdtar로 두 겹까지 푼다.
"""
import hashlib, json, os, subprocess, sys
R = os.path.expanduser("~/Desktop/구랜디스킨모음")
PROJECT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
W = os.path.expanduser("~/GRD_motion_trial/_work/survey")
MODEL = (".fbx", ".glb", ".gltf")
ARCH = (".zip", ".rar", ".7z")
SKIP_DIRS = ("/00_다운로드원본", "/99_")


def extract(path, depth=0):
    d = os.path.join(W, "x", hashlib.md5(path.encode()).hexdigest()[:10])
    if not os.path.isdir(d):
        os.makedirs(d)
        r = subprocess.run(["bsdtar", "-xf", path, "-C", d], capture_output=True)
        if r.returncode != 0:
            subprocess.run(["unar", "-q", "-f", "-o", d, path], capture_output=True)
    models = []
    for dp, dn, fn in os.walk(d):
        for f in fn:
            p = os.path.join(dp, f)
            low = f.lower()
            if low.endswith(MODEL) and "__MACOSX" not in p:
                models.append(p)
            elif low.endswith(ARCH) and depth < 2:
                models += extract(p, depth + 1)
    return models


def models_of(path):
    low = path.lower()
    if low.endswith(MODEL):
        return [path]
    if low.endswith(ARCH):
        return extract(path)
    return []


def main():
    units = sorted(os.path.basename(d) for d in os.listdir(os.path.join(PROJECT, "Assets/Art/Units")) if os.path.isdir(os.path.join(PROJECT, "Assets/Art/Units", d)))
    files = []
    for dp, dn, fn in os.walk(R):
        if any(s in dp + "/" for s in SKIP_DIRS):
            continue
        for f in fn:
            files.append(os.path.join(dp, f))
    jobs, nosrc = [], []
    seen = set()
    for u in units:
        cands = [f for f in files if u in os.path.relpath(f, R) and not os.path.basename(f).startswith("._")
                 and f.lower().endswith(MODEL + ARCH)]
        if not cands:
            nosrc.append(u); continue
        for c in cands:
            for m in models_of(c):
                if (u, m) in seen: continue
                seen.add((u, m)); jobs.append(dict(kind="유닛", name=u, src=os.path.relpath(c, R), model=m))
    # 적·보스: 90_/91_ 아래 전부 — 이름은 경로에서
    for dp, dn, fn in os.walk(os.path.join(R, "90_적유닛")):
        for f in fn:
            if f.startswith("._") or not f.lower().endswith(MODEL + ARCH): continue
            p = os.path.join(dp, f)
            for m in models_of(p):
                jobs.append(dict(kind="적", name=os.path.splitext(f)[0], src=os.path.relpath(p, R), model=m))
    for dp, dn, fn in os.walk(os.path.join(R, "91_보스")):
        for f in fn:
            if f.startswith("._") or not f.lower().endswith(MODEL + ARCH): continue
            p = os.path.join(dp, f)
            for m in models_of(p):
                jobs.append(dict(kind="보스", name=os.path.splitext(f)[0], src=os.path.relpath(p, R), model=m))
    json.dump(dict(jobs=jobs, no_source=nosrc), open(os.path.join(W, "jobs.json"), "w"), ensure_ascii=False, indent=1)
    print("유닛", len(units), "원본 못 찾음", len(nosrc), nosrc, "| 작업", len(jobs))


main()
