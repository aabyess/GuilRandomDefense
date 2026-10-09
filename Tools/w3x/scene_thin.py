"""「검토 필요」 자동 대본 후처리(2026-10-09, blender). /usr/bin/python3 Tools/w3x/scene_thin.py
- THIN: 같은 모델·같은 자리에서 폭주하는 스폰을 최소 간격(초)으로 솎는다(빠진 id의 set/ramp/kill/damage도 같이 뺀다).
- OK_BY_EYE: 렌더 시트를 눈으로 보고 쓸 만하다고 판단한 것 → 품질을 「양호(눈 확인)」로.
- NO_VISUAL: 더미 모델만 있어 보이는 게 없는 것 → 「연출 없음(더미 모델만)」."""
import json, os, glob
H = os.path.expanduser("~"); S = H + "/GRD_scenes/auto"; R = H + "/GRD_scenes/auto_render"
THIN = {"랜덤_한마_바키__Byakuya_E": 0.15, "랜덤_한마_바키__Byakuya_R": 0.15, "불멸_김용태__roger_Mana": 0.6, "영원_조세민__hancock_skill_Mana": 0.5}
NO_VISUAL = {"제한_이유범__Sinobu_Skill_4"}
OK_BY_EYE = """랜덤_카마도_탄지로__Ryougi_Skill_3 변화됨_강재규__carrot_skill_2 변화됨_박은석__Transpom_AceMana 변화됨_박은석__Transpom_Ace_bulRemake
불멸_고도현__Ed_Skill_1 불멸_고도현__Ed_Skill_1_Item 불멸_박은석__Z_Skill_Mana 전설적인_최상호__Legend51 제한_박성호__Marco_S2 제한_최영민__Marco_S2
제한_이충민__Ain_Skill_1 제한_임준성__King_skill_2 제한_임준성__King_skill_2_tr 초월_김민준_AP__Tasigi_03 초월_두유찬_AD__Sabo_Skill_1 초월_박기찬_AD__Franky_Skill_Mana1 초월_황준석_ADAP__Sabo_Skill_1""".split()
def save(i, d): json.dump(d, open(f"{S}/{i}.json", "w"), ensure_ascii=False, indent=1)
for i, gap in THIN.items():
    d = json.load(open(f"{S}/{i}.json")); last = {}; drop = set(); keep = []
    for e in sorted(d["timeline"], key=lambda e: e["t"]):
        if e["op"] == "spawn":
            k = (e["model"], tuple(e["at"]["offset"]), e["at"]["anchor"])
            if k in last and e["t"] - last[k] < gap: drop.add(e["id"]); continue
            last[k] = e["t"]
        elif e.get("id") in drop: continue
        keep.append(e)
    n0 = sum(1 for e in d["timeline"] if e["op"] == "spawn"); n1 = sum(1 for e in keep if e["op"] == "spawn")
    d["timeline"] = keep; d["thinned"] = f"같은 모델·자리 스폰 {gap}초 간격으로 솎음 {n0}→{n1}"
    d["quality"] = "양호(솎음)"; d["unresolvedConditions"] = d["unresolvedConditions"]; save(i, d); print(i, d["thinned"])
    for f in glob.glob(f"{R}/{i}_*.png"): os.remove(f)
for i in OK_BY_EYE:
    d = json.load(open(f"{S}/{i}.json")); d["quality"] = "양호(눈 확인)"; save(i, d)
    for f in glob.glob(f"{R}/{i}_sheet.png"): os.remove(f)
for i in NO_VISUAL:
    d = json.load(open(f"{S}/{i}.json")); d["quality"] = "연출 없음(더미 모델만)"; save(i, d)
