"""클립 도중 알파가 바뀌는 지오셋·레이어 목록(2026-10-09, blender): /usr/bin/python3 Tools/w3x/clip_alpha_changes.py
원본 MDX의 지오셋 알파(KGAO)·재질 레이어 알파(KMTA) 키를 시퀀스 구간과 겹쳐, 한 시퀀스 안(시작 뒤~끝 전)에서 값이 바뀌는 것을 찾는다.
→ ~/GRD_skin_swap/클립중_알파변화.json  (팩 → 메시 → 시퀀스 → {startAlpha, changes:[{t(초, 시퀀스 시작 기준), alpha}]})
알파 값 규약: 시작 알파 = 시퀀스 시작 시각 이전(포함) 마지막 키의 값(없으면 정적 알파), 키는 선형 보간이 아니라 계단으로 읽는다(보수적 — 실제 보간 종류는 MDX 키 헤더 참고)."""
import csv, glob, json, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import mdx_anim
H = os.path.expanduser("~"); rows = list(csv.DictReader(open(H + "/GRD_skin_swap/교체목록.csv", encoding="utf-8-sig")))
res = {}; summary = []
for r in rows:
    ro = r["우리 로스터"]; nm = ro.split("_")[1] + "_" + r["원작 이름"].split()[0]; pk = f"{H}/GRD_skin_swap/{nm}"
    if not os.path.isdir(pk): c = glob.glob(f"{H}/GRD_skin_swap/{ro.split('_')[1]}_*"); pk = c[0]
    sd = json.load(open(glob.glob(pk + "/model_fixed/*.json")[0]))
    mdx = next((p for w in ("original_skin", "original_vfx", "s2_skin") for p in glob.glob(f"{H}/GRD_motion_trial/{w}/work/{sd['model']}")), None)
    if not mdx: summary.append((ro, "MDX 없음")); continue
    seqs = mdx_anim.describe(open(mdx, "rb").read())["sequences"]
    out = {}
    for m in sd["meshes"]:
        for kind, keys in (("geoset", m.get("geosetAlphaKeys") or []), ("layer", m.get("layerAlphaKeys") or [])):
            if not keys: continue
            static = m.get("geosetAlphaStatic") if kind == "geoset" else m.get("staticAlpha")
            for sq in seqs:
                s0, s1 = sq["start"], sq["end"]
                before = [k for k in keys if k[0] <= s0]; start = before[-1][1][0] if before else (static if static is not None else (keys[0][1][0]))
                inside = [(k[0], k[1][0]) for k in keys if s0 < k[0] < s1]
                ch = [dict(t=round((t - s0) / 1000, 3), alpha=round(v, 3)) for t, v in inside if abs(v - start) > 0.01]
                if ch:
                    out.setdefault(m["mesh"], {}).setdefault(kind, {})[sq["name"]] = dict(startAlpha=round(start, 3), seqSeconds=round((s1 - s0) / 1000, 3), changes=ch)
    if out: res[ro] = dict(pack=os.path.basename(pk), model=sd["model"], meshes=out); summary.append((ro, f"{len(out)}개 메시"))
json.dump(res, open(H + "/GRD_skin_swap/클립중_알파변화.json", "w"), ensure_ascii=False, indent=1)
for s in summary: print(*s)
print("클립 도중 알파가 바뀌는 기:", len(res), "/", len(rows))
