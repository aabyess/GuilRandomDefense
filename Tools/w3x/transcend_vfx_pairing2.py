"""초월 스킬 ↔ 원작 이펙트 짝 2차(2026-10-09, PM 지시 「나머지 초월 스킬도 짝 찾기」).

  /usr/bin/python3 Tools/w3x/transcend_vfx_pairing2.py

1차(transcend_vfx_pairing.py)의 짝 13(이름·능력코드·문턱)은 그대로 「강한 짝」. 남은 스킬은 사장님 신규 사양이라 원작에 같은 스킬이 없다. 그래서
  - 후보(유닛) : 그 초월 유닛의 원작 캐릭터(survey.json의 families = 공격 트리거 + 그 사슬의 AddSpecialEffect·더미 유닛 모델)가 쓰는 이펙트 전부를 「후보」로 단다.
                 어느 스킬에 어느 모델인지는 원작에 없는 대응이라 고르지 않는다(사장님/PM 몫). 근거 = survey 트리거 이름·깊이·모델 출처(w3u 더미·war3map.j 줄).
  - 없음(패시브·수치) : 스킬 이름에 패시브·오라·증가·증폭·감소·방깍·방어 같은 수치 말머리만 있고 시전 연출이 없는 것.
  - 없음(원작 대응 유닛 없음) : 원작 캐릭터(families)가 없는 초월 유닛.
산출: ~/GRD_motion_trial/transcend_vfx/pairing2.csv(+Docs/research/TRANSCEND_VFX_PAIRING2_2026-10-09.csv) · 후보 모델 목록(candidates.json)
"""
import csv, json, os, re

H = os.path.expanduser("~")
SURVEY = json.load(open(H + "/GRD_cast_vfx_trial/survey.json"))
HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(os.path.dirname(HERE))
P1 = list(csv.DictReader(open(os.path.join(ROOT, "Docs/research/TRANSCEND_VFX_PAIRING_2026-10-08.csv"), encoding="utf-8-sig")))
strong = {}
for r in P1:
    if "짝 없음" not in r["짝 근거"]:
        strong.setdefault((r["초월 유닛(로스터)"], r["우리 스킬 에셋"]), set()).add(re.split(r"[\\/]", r["원작 이펙트 모델"])[-1])
PASSIVE = re.compile(r"패시브|오라|증가|증폭|감소|방깍|방어|저항|면역|회복|재생|보너스|획득|강화\)|\(패시브")
tag = lambda m: os.path.splitext(re.split(r"[\\/]", m)[-1])[0]
rows, cand, nstrong, ncand, nnone = [], {}, 0, 0, 0
for u in SURVEY:
    models = {}
    for f in u["families"]:
        for t in f["triggers"]:
            for e in t["effects"]:
                if e.get("inmap") and tag(e["model"]).lower() not in ("robin_dres9", "hy10", "tashigi17", "zoro14"):     # 유닛 몸 모델 제외
                    models.setdefault(tag(e["model"]), []).append(f"{t['trigger']}(깊이{t['depth']})·{e['src'][:40]}")
    cand[u["roster"]] = sorted(models)
    for s in u["ourSkills"]:
        key = (u["roster"], s["asset"])
        if key in strong:
            rows.append([u["roster"], s["asset"], s["skillName"], "강한 짝(1차)", "; ".join(sorted(strong[key]))]); nstrong += 1
        elif not models:
            rows.append([u["roster"], s["asset"], s["skillName"], "없음(원작 대응 유닛 없음)", ""]); nnone += 1
        elif PASSIVE.search(s["skillName"]) and not s.get("gates") and "발동" not in s["skillName"]:
            rows.append([u["roster"], s["asset"], s["skillName"], "없음(패시브·수치)", ""]); nnone += 1
        else:
            rows.append([u["roster"], s["asset"], s["skillName"], "후보(유닛 단위)", "; ".join(sorted(models))]); ncand += 1
head = ["초월 유닛", "우리 스킬 에셋", "우리 스킬 이름", "구분", "원작 이펙트 모델(후보 포함)"]
for out in (H + "/GRD_motion_trial/transcend_vfx/pairing2.csv", os.path.join(ROOT, "Docs/research/TRANSCEND_VFX_PAIRING2_2026-10-09.csv")):
    with open(out, "w", newline="", encoding="utf-8-sig") as fh:
        w = csv.writer(fh); w.writerow(head); w.writerows(rows)
json.dump(cand, open(H + "/GRD_motion_trial/transcend_vfx/candidates.json", "w"), ensure_ascii=False, indent=1)
print("skills", len(rows), "강한", nstrong, "후보", ncand, "없음", nnone, "후보 모델 종", len({m for v in cand.values() for m in v}))
