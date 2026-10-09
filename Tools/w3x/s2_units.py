"""최신 원랜디(S2 2.323) 유닛 → 등급·이름·모델·구버전 대비 NEW 표(2026-10-09, blender).
  /usr/bin/python3 Tools/w3x/s2_units.py → ~/GRD_motion_trial/s2_skin/units.json
- S2는 슬크 저장본(이름·wts 지워짐) → 이름은 구버전 같은 ID 이름으로 대체(「추정」), 신규 ID는 모델 캐릭터 이름 토큰으로 구버전 등급 추정.
- 등급: skin_grade_build.grade_of(구버전 이름) 그대로. 신규 ID는 같은 캐릭터 토큰을 쓰는 구버전 ID의 등급(없으면 14_신규).
- NEW 판정: 구버전 모델과 S2 모델의 (지오셋 수, 정점 합, 삼각형 합)이 다르면 NEW, 같으면 「동일(이름만 바뀜)」."""
import json, os, re, sys, importlib.util
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.dirname(os.path.dirname(HERE)); sys.path.insert(0, HERE)
import slk, mdx_geo, w3u
from mpqread import Archive
H = os.path.expanduser("~")
spec = importlib.util.spec_from_file_location("sgb", HERE + "/skin_grade_build.py"); sys.argv = ["x", "none"]; sgb = importlib.util.module_from_spec(spec); spec.loader.exec_module(sgb)
new = Archive(H + "/GRD_motion_trial/_work/s2.mpq"); old = Archive(H + "/GRD_motion_trial/_work/ord.mpq")
_, ui = slk.parse(new.read("units\\unitui.slk"))
U = sgb.U; clean = sgb.clean
def rd(a, n):
    try: return a.read(n)
    except Exception: return None
def sig(a, path):
    p = path.replace("\\\\", "\\"); cands = [re.sub(r"\.md[lx]$", "", p, flags=re.I) + ".mdx", os.path.basename(p.replace("\\", "/")).rsplit(".", 1)[0] + ".mdx"]
    for c in cands:
        d = rd(a, c)
        if d:
            try:
                g = mdx_geo.parse(d)["geosets"]; return (len(g), sum(len(x["verts"]) for x in g), sum(len(x["tris"]) for x in g)), c
            except Exception: return None, c
    return None, None
grade_of = {}; 
for g, ids in sgb.UNITS.items():
    for k in ids: grade_of[k] = g
tok = lambda f: (re.search(r"!\d+_([A-Za-z]+)", f or "") or [None, None])[1]
tok_grade = {}
for k, g in grade_of.items():
    t = tok(ui.get(k, {}).get("file", ""))
    if t: tok_grade.setdefault(t.lower(), []).append(g)
out = []
for k, r in ui.items():
    f = r.get("file", "")
    if not re.match(r"^[A-Za-z][0-9][0-9A-Za-z]{2}$", k) or k[0] in "eonEON" or f in ("", "_"): continue
    isold = k in U; g = grade_of.get(k); est = False
    if not g:
        t = tok(f); gs = tok_grade.get((t or "").lower())
        g = max(set(gs), key=gs.count) if gs else "14"; est = True
    if not isold and not re.search(r"!\d{4}_", f): continue          # 이펙트 더미(Effect …) 제외
    nm = clean(U[k].get("unam", "")) if isold else f"(신규) {os.path.basename(f.replace(chr(92), '/'))}"
    ns, nfile = sig(new, f + ".mdx" if not f.endswith(".mdl") else f); os_, ofile = sig(old, U[k].get("umdl") or "") if isold else (None, None)
    status = "신규 ID" if not isold else ("NEW(구버전과 다른 모델)" if ns and os_ and ns != os_ else ("동일 모델(이름만 바뀜)" if ns and os_ else ("S2에만 모델 있음(구버전엔 맵에 없음)" if ns and not os_ else "S2에도 모델 없음")))
    out.append(dict(id=k, name=nm, nameEstimated=not isold or not nm, grade=g, gradeEstimated=est, s2Model=f, s2File=nfile, oldModel=U[k].get("umdl") if isold else None, oldFile=ofile, s2Sig=ns, oldSig=os_, status=status))
os.makedirs(H + "/GRD_motion_trial/s2_skin", exist_ok=True); json.dump(out, open(H + "/GRD_motion_trial/s2_skin/units.json", "w"), ensure_ascii=False, indent=1)
import collections
print(len(out), collections.Counter(x["grade"] for x in out)); print(collections.Counter(x["status"] for x in out))
print("구버전 「맵에 없음」이던 곳:", sum(1 for x in out if x["status"].startswith("S2에만")), [x["oldModel"] for x in out if x["status"].startswith("S2에만")][:12])
