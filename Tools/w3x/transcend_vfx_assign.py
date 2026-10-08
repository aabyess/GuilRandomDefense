"""초월 스킬 후보 → 한 스킬=한 모델 PM 초안 배정(2026-10-09, blender).

  /usr/bin/python3 Tools/w3x/transcend_vfx_assign.py
입력: TRANSCEND_VFX_PAIRING2 표(후보 68) · SkillData(첫 레벨의 효과 대상·반경·직선·장판) · 창고 16 index.csv(모양 묶음·원작 역할) · 후보 모델 json(메시 없음=입자 전용 제외)
규칙: ①같은 초월 유닛의 원작 캐릭터 후보 모델 안에서만 ②스킬 모양(단일·직선·범위·장판·연쇄·버프)과 모델 모양(묶음·원작 역할·이름 힌트) 점수 ③한 유닛 안 모델 중복 금지(모자라면 중복·표시) ④입자 전용 제외.
산출: ~/GRD_orig_vfx_trial/assignment.csv (스킬·모델·모양 근거·확신)
"""
import csv, glob, json, os, re, yaml

H = os.path.expanduser("~")
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
T = H + "/GRD_orig_vfx_trial"
cand = json.load(open(H + "/GRD_motion_trial/transcend_vfx/candidates.json"))
idx = {r["폴더 이름"]: r for r in csv.DictReader(open(H + "/Desktop/구랜디스킨모음/16_원랜디스킬/index.csv", encoding="utf-8-sig"))}
rows = [r for r in csv.DictReader(open(ROOT + "/Docs/research/TRANSCEND_VFX_PAIRING2_2026-10-09.csv", encoding="utf-8-sig")) if r["구분"] == "후보(유닛 단위)"]


import difflib
FILES = [os.path.basename(p)[10:-6] for p in glob.glob(ROOT + "/Assets/Data/UnitSkills/SkillData_*.asset")]
FIXED = {}


def load(asset):
    """에셋 이름이 survey 이후 바뀐 것은 같은 유닛 이름 안에서 가장 닮은 파일로(바뀐 것은 FIXED에 모아 보고)."""
    if asset not in FILES:
        core = re.search(r"(초월_[^_]+_[A-Z]+)", asset)
        pool = [f for f in FILES if core and core.group(1) in f]
        hit = difflib.get_close_matches(asset, pool, 1, 0.5)
        if not hit: raise FileNotFoundError(asset)
        FIXED[asset] = hit[0]; asset = hit[0]
    t = open(f"{ROOT}/Assets/Data/UnitSkills/SkillData_{asset}.asset", encoding="utf-8").read().split("--- !u!114 &11400000\n", 1)[1]
    return yaml.safe_load(t)["MonoBehaviour"]


def skill_shape(d, name):
    """(모양, 근거). target: 0 Self 1 Allies 2 Enemies 3 Single 4 RandomEnemy 5 Chain 6 DesignatedAlly"""
    L = d["levels"][0]; ef = L["effects"]
    tg = [e["target"] for e in ef]; rng = L["range"]
    if any(e["lineLength"] > 0 for e in ef): return "직선", f"직선 길이 {max(e['lineLength'] for e in ef):g}"
    if any(e["zoneRadius"] > 0 for e in ef): return "장판", f"지속 장판 반경 {max(e['zoneRadius'] for e in ef):g}"
    if 5 in tg: return "연쇄", "연쇄 튕김"
    if 2 in tg or 4 in tg: return "범위", f"범위 반경 {rng:g}" if rng else "범위(적 전체 반경)"
    if 3 in tg: return "단일", "단일 대상" + (f"(사거리 {rng:g})" if rng else "")
    if all(t in (0, 1, 6) for t in tg): return "버프", "자기·아군 버프"
    return "단일", "기타 → 단일로 간주"


ROLE = lambda r: (r or "")
AFF = {   # 스킬 모양 → {모델 모양: 점수}
    "단일": {"투사체": 3, "번개": 3, "베기": 2, "폭발": 2, "원소": 2, "미분류": 1, "고리": 1, "땅폭발": 1, "오라": 0},
    "직선": {"투사체": 3, "베기": 2, "번개": 2, "원소": 1, "미분류": 1, "폭발": 1, "고리": 1, "땅폭발": 1, "오라": 0},
    "범위": {"고리": 3, "폭발": 3, "땅폭발": 3, "원소": 2, "번개": 2, "베기": 1, "미분류": 1, "투사체": 0, "오라": 1},
    "장판": {"땅폭발": 3, "오라": 3, "고리": 2, "원소": 2, "폭발": 2, "번개": 1, "미분류": 1, "베기": 0, "투사체": 0},
    "연쇄": {"번개": 3, "투사체": 1, "원소": 1, "폭발": 1, "고리": 0, "땅폭발": 0, "베기": 0, "미분류": 1, "오라": 0},
    "버프": {"오라": 3, "고리": 2, "폭발": 1, "원소": 1, "땅폭발": 1, "번개": 1, "미분류": 1, "베기": 0, "투사체": 0},
}
GROUP = {"고리·충격파": "고리", "땅폭발·가시": "땅폭발", "투사체": "투사체", "섬광·번개": "번개", "초승달·베기": "베기", "오라·버프": "오라", "불·얼음·바람": "원소", "미분류": "미분류"}


def model_shape(m):
    r = idx.get(m, {}); g = GROUP.get(r.get("모양 묶음", ""), "미분류"); role = r.get("역할", "")
    n = m.lower(); 
    if re.search(r"boom|explo|burst|blast|nova", n): g = "폭발" if g in ("미분류", "번개", "원소") else g
    if "투사체" in role and g in ("미분류", "번개", "베기"): g = "투사체" if g == "미분류" else g
    return g, role


KW = [(r"번개|낙뢰|전기|썬더|라이트닝", r"thunder|lightning|zeus|bolt|laser|spark"), (r"불|화염|염|화재|폭발", r"fire|flame|burn|boom|explo"), (r"얼음|빙|냉", r"ice|frost|snow"),
      (r"검|참|베기|도|베", r"slash|sword|blade|cut|moon"), (r"바람|돌풍|폭풍", r"wind|tornado|storm|cyclone"), (r"독|안개|가스", r"poison|gas|smoke|toxic")]
out = []
for u in sorted({r["초월 유닛"] for r in rows}):
    sk = [r for r in rows if r["초월 유닛"] == u]
    ms = [m for m in cand[u] if os.path.exists(f"{T}/{m}/{m}.json") and json.load(open(f"{T}/{m}/{m}.json"))["meshes"]]
    if not ms:
        for r in sk: out.append([u, r["우리 스킬 이름"], r["우리 스킬 에셋"], "", "", "(메시 있는 후보 없음)", "하", ""]); 
        continue
    S = []
    for r in sk:
        d = load(r["우리 스킬 에셋"]); sh, why = skill_shape(d, r["우리 스킬 이름"]); S.append((r, sh, why))
    def score(si, m):
        _, sh, _ = S[si]; g, role = model_shape(m); s = AFF[sh].get(g, 0) * 10
        nm = S[si][0]["우리 스킬 이름"]
        for kn, km in KW:
            if re.search(kn, nm) and re.search(km, m.lower()): s += 4
        if "투사체" in role and sh in ("직선", "단일"): s += 2
        return s
    used = {}; assign = {}
    order = sorted(range(len(S)), key=lambda i: -max(score(i, m) for m in ms))
    for rnd in (0, 1, 2):                                            # 한 바퀴 안엔 중복 금지, 모델이 모자라면 다음 바퀴(중복 표시)
        used_round = set()
        for i in order:
            if i in assign: continue
            c = [m for m in ms if m not in used_round]
            if not c: continue
            best = max(c, key=lambda m: (score(i, m), -ms.index(m)))
            assign[i] = (best, rnd > 0); used_round.add(best)
    for i, (r, sh, why) in enumerate(S):
        m, dup = assign[i]; g, role = model_shape(m); a = AFF[sh].get(g, 0)
        conf = "상" if a >= 3 else ("중" if a == 2 else "하")
        reason = f"스킬 {sh}({why}) ↔ 모델 {g}" + (f"·원작역할 {role}" if role else "") + ("; ⚠️ 이 유닛 안 모델 중복" if dup else "")
        if dup and conf == "상": conf = "중"
        out.append([u, r["우리 스킬 이름"], r["우리 스킬 에셋"], sh, m, reason, conf, "중복" if dup else ""])
with open(T + "/assignment.csv", "w", newline="", encoding="utf-8-sig") as f:
    w = csv.writer(f); w.writerow(["초월 유닛", "스킬 이름", "스킬 에셋", "스킬 모양", "배정 모델", "모양 근거", "확신", "중복"]); w.writerows(out)
import collections
print(len(out), collections.Counter(o[6] for o in out), "중복", sum(1 for o in out if o[7]))

print("에셋 이름 보정", FIXED)
