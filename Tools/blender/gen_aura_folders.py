"""원작 상시 오라(Sphere 능력 대상 아트) MDX → 유니티용 폴더(2026-10-01). 시스템 python으로:

  SCR=<mdx·_tex·_approx.json이 있는 작업폴더(mdxwork)> python3 Tools/blender/gen_aura_folders.py
산출: ~/GRD_motion_trial/초월_상시오라/<모델>/{설명.md, <모델>_all.fbx, Textures/}  (+ _맵에없는7.md)
오라 모델은 통째가 이펙트라 지오셋 전부를 뜯는다. FBX 원점 = 모델 원점(= 유닛 발밑 origin 붙임).
"""
import csv, json, os, re, shutil, subprocess, sys, collections
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "w3x"))
import mdx_anim, mdx_geo
SC = os.environ["SCR"]
REPO = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.expanduser("~/GRD_motion_trial/초월_상시오라")
APPROX = set(json.load(open(os.path.join(SC, "_approx.json")))) if os.path.exists(os.path.join(SC, "_approx.json")) else set()
rows = list(csv.DictReader(open(os.path.join(REPO, "Docs/research/SPHERE_ART_BY_ROSTER.csv"))))
use = collections.defaultdict(list)                                   # 모델(소문자 파일) → [(로스터, 능력, 붙는곳)]
missing = collections.defaultdict(list)
for r in rows:
    for art in r["아트(atat)"].split(","):
        art = art.strip()
        if not art: continue
        key = art.replace("\\", "/").split("/")[-1]
        if not art.lower().endswith(".mdx") or "/" in art.replace("\\", "/") and not art.lower().startswith("model"):
            missing[art].append((r["로스터"], r["능력"], r["붙는곳0"]))
        elif os.path.exists(os.path.join(SC, key)) or os.path.exists(os.path.join(SC, key.lower())):
            use[key].append((r["로스터"], r["능력"], r["붙는곳0"], r["붙는곳1"]))
        else:
            missing[art].append((r["로스터"], r["능력"], r["붙는곳0"]))
os.makedirs(OUT, exist_ok=True)
for key, users in sorted(use.items()):
    path = os.path.join(SC, key) if os.path.exists(os.path.join(SC, key)) else os.path.join(SC, key.lower())
    data = open(path, "rb").read()
    m = mdx_geo.parse(data); info = mdx_anim.describe(data)
    tag = os.path.splitext(key)[0]
    d = os.path.join(OUT, tag); shutil.rmtree(d, ignore_errors=True); os.makedirs(os.path.join(d, "Textures"))
    L = [f"# {tag} — 원작 상시 오라 모델", "", "단위: 워크3 1 = 0.01m, 앞 = +X(FBX는 유니티 규약으로 돌려 둠). **원점 = 유닛 발밑(origin)**. 캐릭터 키 워크3 약 150~200(=1.5~2m) 기준 비율로 크기를 맞출 것.",
         "재질 끝 `_add`=가산(검정=투명) `_cut`=알파컷 `_blend`=알파혼합.", "", "## 쓰는 로스터(SPHERE_ART_BY_ROSTER.csv)"]
    by = collections.defaultdict(list)
    for r, ab, at0, at1 in users: by[(ab, at0 + ("," + at1 if at1 else ""))].append(r)
    for (ab, at), rs in by.items(): L.append(f"- 능력 {ab} · 붙는 곳 `{at}` — {len(set(rs))}명: {', '.join(sorted(set(rs)))}")
    L += ["", "## 모양(지오셋)"]
    vmax = [0, 0, 0]; vmin = [1e9] * 3
    for gi, g in enumerate(m["geosets"]):
        for v in g["verts"]:
            for i in range(3): vmax[i] = max(vmax[i], v[i]); vmin[i] = min(vmin[i], v[i])
        ls = [(l["filter"], m["textures"][l["tex"]]["path"] or "team") for l in m["materials"][g["material"]]["layers"]]
        L.append(f"- g{gi}: 정점 {len(g['verts'])} · 층 {ls}")
    if m["geosets"]: L.append(f"- 크기(워크3) 가로 {vmax[0]-vmin[0]:.0f} × {vmax[1]-vmin[1]:.0f} × 높이 {vmax[2]-vmin[2]:.0f} (z {vmin[2]:.0f}~{vmax[2]:.0f})")
    else: L.append("- 지오셋 없음 — 파티클만")
    L += ["", "## 움직임·파티클·리본 값(FBX에 안 담김)"] + ["- " + t for t in mdx_anim.text(info, m["materials"])]
    L += ["", "## 텍스처"]
    for t in m["textures"]:
        p = t["path"]
        if not p or t["replaceable"] in (1, 2): L.append(f"- (팀색/빈) {p}"); continue
        fn = p.replace("\\", "_").replace("/", "_") + ".png"; s = os.path.join(SC, "_tex", fn)
        ap = p in APPROX
        if os.path.exists(s): shutil.copy(s, os.path.join(d, "Textures", fn)); L.append(f"- `Textures/{fn}`" + (" **(근사 — 맵에 없는 워크3 기본 텍스처, 비슷하게 그린 것)**" if ap else " (맵 내장)"))
        else: L.append(f"- {p} — 파일 없음")
    if m["geosets"]:
        env = dict(os.environ); env.pop("ANALYSIS", None)
        r = subprocess.run(["blender", "-b", "--factory-startup", "--python", os.path.join(REPO, "Tools/blender/export_mdx_parts.py"), "--", SC, d, os.path.basename(path), ",".join(str(i) for i in range(len(m["geosets"])))], env=env, capture_output=True, text=True)
        L.insert(2, "**FBX**: " + ", ".join(f"`{f}`" for f in os.listdir(d) if f.endswith(".fbx")) + ("" if r.returncode == 0 else "  ⚠ 내보내기 실패: " + r.stderr[-200:]))
    open(os.path.join(d, "설명.md"), "w").write("\n".join(L))
    print(tag, len(m["geosets"]), "geo", m["counts"])
M = ["# 맵에 없는 상시 오라 7종 — 유니티에서 기존 이펙트로 대신", "", "워크3 기본 모델(맵 안에 없음) 또는 확장팩 모델이라 못 꺼낸다. 어떤 로스터가 쓰는지만 적는다.", ""]
for art, us in sorted(missing.items()): M.append(f"- `{art}` — {', '.join(sorted({u[0] for u in us}))} (능력 {', '.join(sorted({u[1] for u in us}))} · 붙는 곳 {', '.join(sorted({u[2] for u in us}))})")
open(os.path.join(OUT, "_맵에없는것.md"), "w").write("\n".join(M))
print("missing", list(missing))
