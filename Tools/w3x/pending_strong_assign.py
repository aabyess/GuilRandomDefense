"""초월 원작 평타 피해 문(구현담당1 d28c48c9b) 8스킬 → 원작 이펙트 강한 짝 배정(2026-10-09, blender).
  /usr/bin/python3 Tools/w3x/pending_strong_assign.py   → Docs/research/ORIGINAL_VFX_ASSIGN_초월평타문.csv
입력: ~/GRD_orig_vfx_trial/pending_strong_pairs.csv(원작 트리거별 이펙트 모델) · SkillData(효과 대상) · 실렌더 커버리지(/tmp/ra/cov.json) · 창고 16 index."""
import csv, json, os, re, yaml
H = os.path.expanduser("~"); T = H + "/GRD_orig_vfx_trial"; ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
MAP = {"SkillData_원작017_H095": "Akainu_01", "SkillData_회수_초월_김만경_AD_0ac0451e": "Akainu_02", "SkillData_원작트리거_초월_김만경_AD_Akainu_03": "Akainu_03",
       "SkillData_게이트_초월_김경현_AP_4266b6e3": "Snake_3_Kingkobra", "SkillData_게이트_초월_김경현_AP_b7ae9b41": "Snake_2",
       "SkillData_원작트리거_초월_황준석_ADAP_Kid_Skill_Mana": "Kid_Skill_Mana", "SkillData_더미채널_초월_황준석_ADAP_79행_01000": "Kid_Skill_3",
       "SkillData_더미채널_초월_황준석_ADAP_79행_10000": "Kid_Skill_Life2"}
cov = json.load(open("/tmp/ra/cov.json"))
idx = {r["폴더 이름"]: r for r in csv.DictReader(open(H + "/Desktop/구랜디스킨모음/16_원랜디스킬/index.csv", encoding="utf-8-sig"))}
GROUP = {"고리·충격파": "고리", "땅폭발·가시": "땅폭발", "투사체": "투사체", "섬광·번개": "번개", "초승달·베기": "베기", "오라·버프": "오라", "불·얼음·바람": "원소", "미분류": "미분류"}
SLOT = {"적중시": {"투사체": 3, "번개": 3, "베기": 2, "폭발": 2, "원소": 2, "미분류": 1, "땅폭발": 1, "고리": 1, "오라": 0},
        "범위": {"고리": 3, "폭발": 3, "땅폭발": 3, "원소": 2, "번개": 2, "베기": 1, "미분류": 1, "오라": 1, "투사체": 0}}
def shape(m):
    r = idx.get(m, {}); g = GROUP.get(r.get("모양 묶음", ""), "미분류")
    if re.search(r"boom|explo|burst|blast|nova", m.lower()) and g in ("미분류", "번개", "원소"): g = "폭발"
    return g
def ok(m):
    jp = f"{T}/{m}/{m}.json"
    if not os.path.exists(jp) or cov.get(m, 0) < 1: return False
    d = json.load(open(jp)); return bool(d["meshes"]) and not any(re.search(r"walk|attack|spell", x["name"], re.I) for x in d["sequences"])
pend = {}
for r in csv.DictReader(open(T + "/pending_strong_pairs.csv", encoding="utf-8-sig")): pend.setdefault(r["원작 트리거"], []).append(r["이펙트 모델"])
out = []
for stem, trig in MAP.items():
    t = open(f"{ROOT}/Assets/Data/UnitSkills/{stem}.asset", encoding="utf-8").read().split("--- !u!114 &11400000\n", 1)[1]
    ef = yaml.safe_load(t)["MonoBehaviour"]["levels"][0]["effects"]; tg = {e["target"] for e in ef}
    slots = (["적중시"] if 3 in tg else []) + (["범위"] if (2 in tg or 4 in tg) else []) or ["범위"]
    pool = [m for m in dict.fromkeys(pend.get(trig, [])) if ok(m)]
    cells = {}; used = set()
    for k in slots:
        best = next((m for m in sorted(pool, key=lambda m: (-SLOT[k].get(shape(m), 0), m)) if m not in used and SLOT[k].get(shape(m), 0) >= 1), None)
        if best: used.add(best); cells[k] = "원작:" + best
        else: cells[k] = "유지"
    out.append([f"Assets/Data/UnitSkills/{stem}.asset", cells.get("적중시", ""), cells.get("범위", ""), ""])
    print(trig, "← 후보", len(pool), "→", cells)
with open(ROOT + "/Docs/research/ORIGINAL_VFX_ASSIGN_초월평타문.csv", "w", newline="", encoding="utf-8") as f:
    w = csv.writer(f); w.writerow(["asset_path", "적중시_프리팹", "범위_지면_프리팹", "시전자_프리팹"]); w.writerows(out)
