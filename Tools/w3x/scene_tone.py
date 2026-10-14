"""연출 대본 겉모습 조정(10-14, blender): 자동검사(Tools/cinematics/auto_check.py) 「화면 덮음·검은 판」 실패 3건.
원작 해석(타임라인·위치·시각)은 그대로, 대체 모델의 크기·수명만 줄인다. 원본은 add_rel_26/out·vivi4_rel에서 다시 읽어 덮는다(여러 번 돌려도 같음).
  /usr/bin/python3 Tools/w3x/scene_tone.py
- 대체 SuperBigExplosion(원작 이펙트 모델이 맵에 없어 범용 폭발로 갈음한 것): baseScale 0.3 · lifeSec 1.2 — 수명 10초의 어두운 연기가 판을 덮던 원인
- Franky_Skill_Mana1: 전 스폰 baseScale ×0.8(빔 판이 화면 너비를 넘음)"""
import json, os
H = os.path.expanduser("~"); A = H + "/GRD_scenes/auto"; O = H + "/GRD_scenes/add_rel_26/out"
SRC = {"영원_문필환__vivi_Skill_4_Mana": H + "/GRD_scenes/vivi4_rel", "랜덤_카마도_탄지로__Ryougi_Skill_3": O, "초월_박기찬_AD__Franky_Skill_Mana1": O}
for sid, d0 in SRC.items():
    d = json.load(open(f"{d0}/{sid}.json")); n = 0
    for e in d["timeline"]:
        if e["op"] != "spawn": continue
        if e.get("substitute") and e["model"] == "SuperBigExplosion":
            e["baseScale"] = 0.3; e["lifeSec"] = 1.2; e["baseScaleNote"] = "10-14 자동검사: 대체 폭발 0.3배·수명 1.2초(덮음·검은 판)"; n += 1
        elif sid.endswith("Franky_Skill_Mana1"):
            e["baseScale"] = round(e["baseScale"] * 0.8, 3); n += 1
    d["toneNote"] = f"10-14 겉모습 조정 {n}건(scene_tone.py)"
    json.dump(d, open(f"{A}/{sid}.json", "w"), ensure_ascii=False, indent=1); print(sid, n)
