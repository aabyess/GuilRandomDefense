"""scene_survey.py 산출 → 대표 연출 표 문서(Docs/research/REPRESENTATIVE_SCENES_2026-10-09.md, blender).
  /usr/bin/python3 Tools/w3x/scene_table_md.py
우리 쪽 붙일 후보 스킬 = 그 로스터 스킬 중 (a) 설명·에셋 이름에 그 트리거 이름이 든 것, 없으면 (b) 상시 패시브(Aura) 아닌 것 중 효과 수가 가장 큰 것."""
import csv, glob, json, os, re, yaml
H = os.path.expanduser("~"); ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
res = json.load(open(H + "/GRD_scenes/scene_table.json"))
g2p = {}
for mp in glob.glob(ROOT + "/Assets/Data/**/*.asset.meta", recursive=True):
    m = re.search(r"guid: (\w+)", open(mp, encoding="utf8").read()); g2p[m.group(1)] = mp[:-5]
def roster_skills(r):
    rp = f"{ROOT}/Assets/Data/Units/Roster/{r}.asset"
    if not os.path.exists(rp): return []
    blk = re.search(r"skills:\n((?:  - .*\n)+)", open(rp, encoding="utf8").read())
    return [g2p[g] for g in re.findall(r"guid: (\w+)", blk.group(1)) if g in g2p] if blk else []
def info(p):
    t = open(p, encoding="utf8").read(); d = yaml.safe_load(t.split("--- !u!114 &11400000\n", 1)[1])["MonoBehaviour"]
    return d, t
def ours(roster, trig):
    best = None; cand = []
    for p in roster_skills(roster):
        try: d, t = info(p)
        except Exception: continue
        nm = os.path.basename(p)[10:-6]
        if re.search(r"(?<![a-z0-9])" + re.escape(trig.lower()) + r"(?![a-z0-9])", (t + nm).lower()): return nm + " (트리거 이름 일치)"
        if d["triggerType"] != 2:
            L = d["levels"][0]; cand.append((len(L["effects"]), nm, d["skillName"]))
    if cand: n, nm, sn = max(cand); return f"{nm} (효과 {n}개 최대)"
    return "(없음 — 액티브/게이트 스킬 없음)"
TOP = [("초월_황준석_ADAP", "Shanks_skill_5", "초월 샹크스 「패기 폭발」(하늘패기)", "샹크스 마린포드 정상해전 종결자(h04U=H08Z)"), ("불멸_고도현", "Shiki_Attack", "불멸 시키 「함대 소환」(A0T4 !함대 — 평타 1/33, 전함 2척)", "금사자 시키(h04B)"),
       ("제한_전법규", "Enel_Mana", "제한됨 에넬 「엘토르」(뇌영, 마나 145)", "에넬(h05E)"), ("불멸_정준영", "Dragon_Skill_Mana", "불멸 드래곤 「폭풍」(마나스킬 번개구름)", "몽키.D.드래곤(h04D)")]
rows = []; seen = set()
def cell(x):
    return x
for roster, trig, nick, who in TOP:
    x = next((q for q in res if q["roster"] == roster and q["trigger"] == trig), None) or next((q for q in res if q["trigger"] == trig), None)
    if x:
        x = dict(x, roster=roster, name=who); rows.append((nick, x)); seen.add((x["roster"], x["trigger"])); seen.add((x["roster"] if False else "", trig))
seen_trig = {t for _, t in seen}
for x in res:
    if (x["roster"], x["trigger"]) in seen or x["trigger"] in {t for _, t in seen if _ == ""}: continue
    if x["score"] < 8: continue
    rows.append((x["trigger"], x)); seen.add((x["roster"], x["trigger"]))
nm = lambda s: re.sub(r"\|c[0-9a-fA-F]{8}|\|r", "", s)
out = ["# 원작 대표 스킬 연출 후보 표 (2026-10-09, blender)\n", "원작 캐릭터마다 j에서 「큰 스킬 연출」 트리거를 점수로 찾았다(더미 유닛 종류×3 + 이펙트 + 대기·타이머×2 + 번개×2 + 반복×2 + 입자 모델). 생성: `Tools/w3x/scene_survey.py` → `scene_table_md.py`. 맨 위 4개는 사장님이 말한 이름으로 고른 것.\n",
       "| # | 우리 유닛 | 원작 캐릭터 | 스킬 이름/트리거 | j 줄 | 더미 유닛(이동·소환) | 이펙트 모델 | 대기·번개·반복 | 입자 모델 | 우리 쪽 후보 스킬 |", "|---|---|---|---|---|---|---|---|---|---|"]
for i, (nick, x) in enumerate(rows, 1):
    d = "<br>".join(f"{k}: {nm(v)}" for k, v in list(x["dummies"].items())[:6]) + (f"<br>…(+{len(x['dummies']) - 6})" if len(x["dummies"]) > 6 else "")
    out.append(f"| {i} | {x['roster']} | {nm(x['name'])[:24]} ({x['uid']}) | **{nick}** `{x['trigger']}`(깊이{x['depth']}) | {x['jline']} | {d} | {', '.join(x['effects'][:5])} | {x['sleeps']}·{x['lightnings']}·{x['loops']} | {x['particle_models']} | {ours(x['roster'], x['trigger'])} |")
open(ROOT + "/Docs/research/REPRESENTATIVE_SCENES_2026-10-09.md", "w", encoding="utf8").write("\n".join(out) + "\n")
print(len(rows), "행")
