"""ourSkill 빈 대본 채우기(10-14, blender). 근거: Docs/research/REPRESENTATIVE_OURSKILL_REVIEW_2026-10-09.csv 「피해 스킬 없음 — 후보 그대로」(구현담당1 검수) 14건의 blender 후보.
사장님 사양 스킬은 고정 — 효과·수치는 안 건드리고 연출만 붙는 자리 표시. 김정래 Uta_skill_1/2는 후보 글자가 깨져(색코드 조각) 같은 로스터의 마나 스킬 「최고의연설」로.
  /usr/bin/python3 Tools/w3x/scene_fill_ourskill.py   (auto/ · add_rel_26/out · vivi4_rel 모두 같은 값으로 — scene_tone 등 재컴파일 원본이 빈칸을 되살리지 않게)"""
import json, os
H = os.path.expanduser("~"); ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
FILL = {
    "영원_김정래__Uta_skill_1": "사장님_영원_김정래_최고의연설", "영원_김정래__Uta_skill_2": "사장님_영원_김정래_최고의연설", "영원_김정래__tots_R": "사장님_영원_김정래_최고의연설",
    "영원_조세민__arrow_h2": "사장님_영원_조세민_라이브콘서트", "영원_조세민__hancock_skill_Mana": "사장님_영원_조세민_라이브콘서트",
    "제한_이충민__Ain_Skill_1": "사장님_제한_이충민_발명품제작", "제한_이충민__Ain_Skill_2": "사장님_제한_이충민_발명품제작",
    "초월_박기찬_AD__Franky_Skill_1": "사장님_초월_박기찬_AD_벗어야흥이나지_암브", "초월_박기찬_AD__Franky_Skill_Mana1": "사장님_초월_박기찬_AD_벗어야흥이나지_암브",
    "초월_유재헌_ADAP__Sirahoshi_skill_Mana": "사장님_초월_유재헌_ADAP_토사장의어둠의수하들",
    "초월_임장혁_AD__Luchi_Skill_2_ZiGun": "사장님_초월_임장혁_AD_영혼없는칭찬_공속", "초월_임장혁_AD__Luchi_Skill_3_6kinggun": "사장님_초월_임장혁_AD_영혼없는칭찬_공속",
    "초월_최상호_AD__Jimbe": "사장님_초월_최상호_AD_상호파집결", "초월_최상호_AD__Jimbe_jingak": "사장님_초월_최상호_AD_상호파집결",
}
n = 0
for d in (H + "/GRD_scenes/auto", H + "/GRD_scenes/add_rel_26/out", H + "/GRD_scenes/vivi4_rel"):
    for sid, sk in FILL.items():
        p = f"{d}/{sid}.json"
        if not os.path.exists(p): continue
        assert os.path.exists(f"{ROOT}/Assets/Data/UnitSkills/SkillData_{sk}.asset"), sk
        j = json.load(open(p)); j["ourSkill"] = "SkillData_" + sk; j["ourSkillName"] = sk
        json.dump(j, open(p, "w"), ensure_ascii=False, indent=1); n += 1
print("채움", n)
