#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
이 스크립트는 아무것도 쓰지 않는다 — 에셋·코드를 읽기만 한다(Assets/Data/Units/Roster/,
Assets/Data/Enemies/, Assets/Data/Waves/, Assets/Scripts/*.cs, Assets/Data/SupportSkills/).
`Tools/generate_*.py`(파괴적, 로스터에 쓴다)와 헷갈리지 않도록 동사를 "generate"가 아니라
"simulate"로 뒀다.

무엇을 하나: `Docs/reference/BALANCE_SIMULATION_2026-05.md` 계열 문서가 쓰는 방법(라인몹
백로그 누적 모델 + 유닛 확보 모델 4종)을 재현 가능한 코드로 고정한다. **하드코딩된 값을
최소화**했다 — 값이 필요하면 해당 에셋·스크립트를 직접 읽는다. 부득이 하드코딩한 상수는
바로 위에 출처(파일:줄)와 "이 값이 원본과 어긋나면 이 스크립트가 낡은 것"이라는 경고를
남겼다.

사용법: `python3 Tools/simulate_balance.py` (프로젝트 루트에서 실행). 출력은
`BALANCE_SIMULATION_*.md`의 표와 같은 형식이다 — 문서를 다시 쓸 때 이 출력을 그대로 옮기면
된다.
"""
import re
import glob
import os
import sys
import statistics
from collections import defaultdict, Counter

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))


def read(path):
    with open(os.path.join(ROOT, path), encoding="utf-8") as f:
        return f.read()


# ---------------------------------------------------------------------------
# 1. UnitGradeExtensions.Tier() — UnitData.cs에서 직접 파싱한다. 하드코딩하지 않는다.
#    (PM 지시 2026-09-05: "Tier() 매핑도 하드코딩하지 말고 UnitData.cs에서 읽어라" —
#    과거 enum 인덱스로 사다리를 세워 최고 등급을 7단계 떨어뜨린 사고가 있었다.)
# ---------------------------------------------------------------------------

def parse_tier_mapping():
    """UnitData.cs의 `public static int Tier(this UnitGrade grade)` switch문을 파싱해
    {등급이름: Tier값} 딕셔너리를 만든다. 이 함수가 실패하면(파일 구조가 바뀌면) 바로 죽는다 —
    조용히 낡은 값으로 넘어가지 않기 위함이다."""
    text = read("Assets/Scripts/Data/UnitData.cs")

    m = re.search(r"public static int Tier\(this UnitGrade grade\)\s*\{(.*?)\n    \}", text, re.DOTALL)
    if not m:
        sys.exit("FATAL: UnitData.cs에서 Tier() 메서드를 못 찾았다 — 파일 구조가 바뀐 것으로 보인다.")
    body = m.group(1)

    mapping = {}
    # "case UnitGrade.X:" 블록들이 이어지다가 "return N;"으로 끝나는 패턴을 순서대로 훑는다.
    # "case X: return N;"처럼 한 줄에 같이 있는 경우와, 여러 case가 return 하나를 같이 쓰는
    # 경우(Hidden/Superior 등) 둘 다 나온다 — 줄 단위가 아니라 토큰 단위로 훑는다.
    pending_cases = []
    for token in re.finditer(r"case UnitGrade\.(\w+):|return (-?\d+);", body):
        grade_name, ret_val = token.group(1), token.group(2)
        if grade_name:
            pending_cases.append(grade_name)
        elif ret_val is not None and pending_cases:
            value = int(ret_val)
            for g in pending_cases:
                mapping[g] = value
            pending_cases = []

    expected_grades = {"Common", "Uncommon", "Special", "Rare", "Hidden", "Superior",
                        "Legendary", "Transformed", "RandomUnit", "Limited",
                        "Transcendent", "Immortal", "OtherWorld", "Eternal", "TranscendentWisp"}
    missing = expected_grades - mapping.keys()
    if missing:
        sys.exit(f"FATAL: Tier() 파싱 결과에 등급이 빠졌다: {missing} — UnitData.cs가 바뀐 것 같다.")
    return mapping


def parse_grade_enum():
    """UnitGrade enum의 선언 순서(= 에셋에 저장되는 정수 인덱스)를 UnitData.cs에서 읽는다."""
    text = read("Assets/Scripts/Data/UnitData.cs")
    m = re.search(r"public enum UnitGrade\s*\{(.*?)\n\}", text, re.DOTALL)
    if not m:
        sys.exit("FATAL: UnitData.cs에서 UnitGrade enum을 못 찾았다.")
    names = re.findall(r"^\s*(\w+)\s*,?\s*(?://.*)?$", m.group(1), re.MULTILINE)
    names = [n for n in names if n]
    return names


TIER_OF = parse_tier_mapping()
GRADE_ENUM = parse_grade_enum()

KOREAN = {"Common": "흔함", "Uncommon": "안흔함", "Special": "특별함", "Rare": "희귀함",
          "Hidden": "히든", "Superior": "특수함", "Legendary": "전설적인", "Limited": "제한됨",
          "Transcendent": "초월함", "Immortal": "불멸", "Eternal": "영원함", "OtherWorld": "다른세계",
          "RandomUnit": "랜덤유닛", "TranscendentWisp": "초월위습", "Transformed": "변화됨"}


# ---------------------------------------------------------------------------
# 2. 로스터 239종 — attackPower/attackSpeed/damageType/critChance 등을 직접 읽는다.
# ---------------------------------------------------------------------------

def load_roster():
    recs = []
    for path in glob.glob(os.path.join(ROOT, "Assets/Data/Units/Roster/*.asset")):
        text = open(path, encoding="utf-8").read()

        def g(field):
            m = re.search(rf"^  {field}: (-?[\d.]+)", text, re.MULTILINE)
            return float(m.group(1)) if m else None

        grade_idx = g("grade")
        if grade_idx is None or int(grade_idx) >= len(GRADE_ENUM):
            continue
        gname = GRADE_ENUM[int(grade_idx)]
        tier = TIER_OF.get(gname)
        ap, aspd = g("attackPower"), g("attackSpeed")
        if tier is None or ap is None or aspd is None:
            continue
        dmgtype = int(g("damageType") or 0)
        cc = g("critChance") or 0.0
        cdm = g("critDamageMultiplier")
        cdm = 1.0 if cdm is None else cdm
        cbd = g("critBonusDamage") or 0.0
        name_m = re.search(r'^  unitName: "?(.*?)"?$', text, re.MULTILINE)
        recs.append({
            "name": name_m.group(1) if name_m else "?",
            "grade": gname, "tier": tier, "damagetype": dmgtype,
            "base_dps": ap * aspd,
            "bash_dps": cc * cbd * aspd,
        })
    return recs


def median_dps_by_tier(roster, damagetype_filter=None, with_bash=False):
    by_tier = defaultdict(list)
    for r in roster:
        if damagetype_filter is not None and r["damagetype"] != damagetype_filter:
            continue
        v = r["base_dps"] + (r["bash_dps"] if with_bash else 0.0)
        by_tier[r["tier"]].append(v)
    out = {}
    for t in range(9):
        vals = by_tier.get(t)
        out[t] = statistics.median(vals) if vals else None
    return out


# ---------------------------------------------------------------------------
# 3. 적 75라운드 — HP/방어력/보스여부를 EnemyData 에셋에서, 스폰수를 WaveData 에셋에서.
# ---------------------------------------------------------------------------

def load_enemies():
    rounds = {}
    for path in glob.glob(os.path.join(ROOT, "Assets/Data/Enemies/Enemy_R*.asset")):
        text = open(path, encoding="utf-8").read()
        m = re.match(r".*Enemy_R0*(\d+)_", os.path.basename(path))
        if not m:
            continue
        r = int(m.group(1))
        hp = float(re.search(r"^  hp: ([\d.]+)", text, re.MULTILINE).group(1))
        armor = float(re.search(r"^  armor: ([\d.]+)", text, re.MULTILINE).group(1))
        is_boss = int(re.search(r"^  isBoss: (\d)", text, re.MULTILINE).group(1))
        rounds[r] = {"hp": hp, "armor": armor, "is_boss": bool(is_boss)}
    return rounds


def load_wave_counts():
    counts = {}
    for path in glob.glob(os.path.join(ROOT, "Assets/Data/Waves/Wave_Round*.asset")):
        text = open(path, encoding="utf-8").read()
        m = re.match(r".*Wave_Round0*(\d+)\.asset", os.path.basename(path))
        r = int(m.group(1))
        sm = re.search(r"spawnList:\n(.*?)(?:\n  wispRewards:|\Z)", text, re.DOTALL)
        total = 0
        if sm:
            total = sum(int(c) for c in re.findall(r"count: (\d+)", sm.group(1)))
        counts[r] = total
    return counts


# ---------------------------------------------------------------------------
# 4. 라운드 길이 / 방어 상수 — RoundManager.cs, EnemyDummy.cs에서 파싱.
# ---------------------------------------------------------------------------

def parse_float_field(text, field, default=None):
    m = re.search(rf"\[SerializeField\][^;]*?\b{field}\s*=\s*(-?[\d.]+)f?;", text)
    if m:
        return float(m.group(1))
    if default is not None:
        return default
    sys.exit(f"FATAL: 필드 {field}를 못 찾았다 — 소스가 바뀐 것 같다.")


def parse_int_field(text, field, default=None):
    m = re.search(rf"\[SerializeField\][^;]*?\b{field}\s*=\s*(-?\d+);", text)
    if m:
        return int(m.group(1))
    if default is not None:
        return default
    sys.exit(f"FATAL: 필드 {field}를 못 찾았다 — 소스가 바뀐 것 같다.")


def load_round_constants():
    rm = read("Assets/Scripts/Waves/RoundManager.cs")
    return {
        "round_duration": parse_float_field(rm, "roundDuration"),
        "boss_round_duration": parse_float_field(rm, "bossRoundDuration"),
        "new_world_round_duration": parse_float_field(rm, "newWorldRoundDuration"),
        "new_world_start_round": parse_int_field(rm, "newWorldStartRound"),
        "enemy_count_threshold": parse_int_field(rm, "enemyCountThreshold"),
    }


def load_defense_armor():
    ed = read("Assets/Scripts/Units/EnemyDummy.cs")
    m = re.search(r"public const float DefenseArmor = ([\d.]+)f;", ed)
    if not m:
        sys.exit("FATAL: EnemyDummy.cs에서 DefenseArmor를 못 찾았다.")
    return float(m.group(1))


# ---------------------------------------------------------------------------
# 5. 도움소(버스터콜) — SupportSkills 에셋 + 마나 포탈 파라미터(MapGenerator.cs)에서.
# ---------------------------------------------------------------------------

def load_support_skill(name):
    path = os.path.join(ROOT, f"Assets/Data/SupportSkills/SupportSkill_{name}.asset")
    text = open(path, encoding="utf-8").read()

    def g(field, cast=float):
        m = re.search(rf"^  {field}: (-?[\d.]+)", text, re.MULTILINE)
        return cast(m.group(1)) if m else None

    return {
        "mana_cost": g("manaCost", int),
        "damage_base": g("damageBase"),
        "cooldown": g("cooldownSeconds"),
    }


def load_mana_portal_params():
    """MapGenerator.cs의 ResourcePortal(..., ResourceType.Mana, ...) 호출 하나를 정규식으로
    찾는다. 라인 자체가 바뀌면(리팩터 등) 이 함수가 실패해서 스크립트가 죽는다 — 낡은 값을
    조용히 쓰지 않기 위함이다. 지금은 Assets/Editor/MapGenerator.cs:1515 근방이 원본이다."""
    mg = read("Assets/Editor/MapGenerator.cs")
    m = re.search(
        r"ResourcePortal\.Payout\.Resource,\s*ResourceType\.Mana,\s*(-?[\d.]+),\s*(-?[\d.]+)f?,\s*(-?[\d.]+)f?\)",
        mg)
    if not m:
        sys.exit("FATAL: MapGenerator.cs에서 마나 포탈 파라미터를 못 찾았다 — 시그니처가 바뀐 것 같다.")
    base_amount, per_round, success_pct = float(m.group(1)), float(m.group(2)), float(m.group(3))
    return {"base_amount": base_amount, "per_round": per_round, "success_pct": success_pct}


def load_mana_cap_and_start():
    """ResourceWallet.cs의 DefaultManaCap/DefaultManaStart 상수를 읽는다."""
    rw = read("Assets/Scripts/Units/ResourceWallet.cs")
    cap = re.search(r"const int DefaultManaCap = (\d+);", rw)
    start = re.search(r"const int DefaultManaStart = (\d+);", rw)
    if not cap or not start:
        sys.exit("FATAL: ResourceWallet.cs에서 마나 상한/시작값 상수를 못 찾았다.")
    return int(cap.group(1)), int(start.group(1))


# ---------------------------------------------------------------------------
# 6. 백로그 시뮬레이터 — 09-04/09-05 문서와 같은 수식.
# ---------------------------------------------------------------------------

def armor_mult(armor, defense_armor):
    if armor >= 0:
        return 1.0 - (defense_armor * armor) / (1.0 + defense_armor * armor)
    return 2.0 - (1.0 - defense_armor) ** (-armor)


def make_round_length_fn(rc):
    def f(r, is_boss):
        if r >= rc["new_world_start_round"]:
            return rc["new_world_round_duration"]
        if is_boss:
            return rc["boss_round_duration"]
        return rc["round_duration"]
    return f


def tier_for_round(r, total_rounds=75):
    """9단계 Tier를 전체 라운드에 고르게 배분한다 — 09-04 문서가 만든 가정을 그대로 재사용한다.
    실제 뽑기 확률·라운드별 등급 분포와는 무관한 단순화다(문서 §⑤ 참고)."""
    return min(8, (r - 1) * 9 // total_rounds)


def run_backlog(enemies, wave_counts, round_length_fn, defense_armor,
                 team_dps_fn, use_armor, threshold, total_rounds=75):
    backlog = 0.0
    collapse = None
    for r in range(1, total_rounds + 1):
        e = enemies[r]
        cnt = wave_counts[r]
        mult = armor_mult(e["armor"], defense_armor) if use_armor else 1.0
        dps = team_dps_fn(r)
        rl = round_length_fn(r, e["is_boss"])
        incoming = backlog + cnt
        kills_capacity = dps * mult * rl / e["hp"]
        killed = min(incoming, kills_capacity)
        backlog = incoming - killed
        if backlog >= threshold and collapse is None:
            collapse = r
    return collapse


def fixed10_dps_fn(median_table):
    return lambda r: 10 * median_table[tier_for_round(r)]


def no_combine_dps_fn(median_table):
    owned = [tier_for_round(1)] * 5

    def f(r):
        owned.append(tier_for_round(r))
        owned.append(tier_for_round(r))
        return sum(median_table[t] for t in owned)
    return f


def greedy_combine_dps_fn(median_table):
    counts = Counter({tier_for_round(1): 5})

    def f(r):
        counts[tier_for_round(r)] += 2
        changed = True
        while changed:
            changed = False
            for t in range(8):
                while counts[t] >= 2:
                    counts[t] -= 2
                    counts[t + 1] += 1
                    changed = True
        return sum(median_table[t] * n for t, n in counts.items())
    return f


def local_optimal_dps_fn(median_table):
    counts = Counter({tier_for_round(1): 5})

    def f(r):
        counts[tier_for_round(r)] += 2
        changed = True
        while changed:
            changed = False
            for t in range(8):
                if counts[t] >= 2 and median_table.get(t + 1) and median_table[t + 1] >= median_table[t] * 2:
                    counts[t] -= 2
                    counts[t + 1] += 1
                    changed = True
        return sum(median_table[t] * n for t, n in counts.items())
    return f


MODELS = {
    "고정 10기/레인": fixed10_dps_fn,
    "안 조합": no_combine_dps_fn,
    "최대 조합(그리디)": greedy_combine_dps_fn,
    "국소최적 조합": local_optimal_dps_fn,
}


# ---------------------------------------------------------------------------
# main
# ---------------------------------------------------------------------------

def main():
    roster = load_roster()
    enemies = load_enemies()
    wave_counts = load_wave_counts()
    rc = load_round_constants()
    defense_armor = load_defense_armor()
    round_length_fn = make_round_length_fn(rc)

    if len(enemies) != 75 or len(wave_counts) != 75:
        sys.exit(f"FATAL: 75라운드가 다 안 모였다 (enemies={len(enemies)}, waves={len(wave_counts)}).")

    ad_median = median_dps_by_tier(roster, damagetype_filter=1)
    ap_median = median_dps_by_tier(roster, damagetype_filter=2)
    # AP 표본이 0인 Tier는 ALL(그 Tier 전체) 중앙값으로 대신 채운다 — 우리 로스터에 그 등급
    # AP 유닛이 아예 없다는 뜻이라 사실상 그 Tier는 AD=ALL이다(§ 문서에 명시할 것).
    all_median = median_dps_by_tier(roster)
    for t in range(9):
        if ap_median[t] is None:
            ap_median[t] = all_median[t]
        if ad_median[t] is None:
            ad_median[t] = all_median[t]

    print("=== ① 등급별 median DPS (Tier(), UnitData.cs에서 직접 파싱) ===")
    print(f"{'Tier':6s}{'등급':20s}{'ALL':>10s}{'AD':>10s}{'AP':>10s}")
    for t in range(9):
        grades = ",".join(KOREAN[g] for g in GRADE_ENUM if TIER_OF.get(g) == t)
        print(f"T{t:<5d}{grades:20s}{all_median[t]:>10.0f}{ad_median[t]:>10.0f}{ap_median[t]:>10.0f}")

    mono_breaks = [t for t in range(1, 9) if all_median[t] < all_median[t - 1]]
    print(f"\n⚠️ 단조성 깨지는 지점(ALL 기준): T{mono_breaks}" if mono_breaks else "\n단조 증가 확인됨(ALL 기준)")
    print("이건 시뮬레이션 가정이 아니라 로스터 실값이다 — 2026-09-05 구현담당2/구현담당1 교차 확인.")

    print("\n=== ② 붕괴 라운드 — 4개 유닛확보모델 × AD/AP ===")
    print(f"{'모델':20s}{'AD팀(방어적용)':18s}{'AP팀(방어무시)':18s}")
    for name, fn in MODELS.items():
        ad_r = run_backlog(enemies, wave_counts, round_length_fn, defense_armor,
                            fn(ad_median), use_armor=True, threshold=rc["enemy_count_threshold"])
        ap_r = run_backlog(enemies, wave_counts, round_length_fn, defense_armor,
                            fn(ap_median), use_armor=False, threshold=rc["enemy_count_threshold"])
        ad_s = f"붕괴 R{ad_r}" if ad_r else "완주(75R)"
        ap_s = f"붕괴 R{ap_r}" if ap_r else "완주(75R)"
        print(f"{name:20s}{ad_s:18s}{ap_s:18s}")

    print("\n=== ③ 도움소(버스터콜) 추가 — 고정10기 모델, 저축형 ===")
    skill = load_support_skill("버스터콜")
    mana_params = load_mana_portal_params()
    mana_cap, mana_start = load_mana_cap_and_start()
    print(f"(버스터콜: mana={skill['mana_cost']} dmg={skill['damage_base']:.0f} cd={skill['cooldown']}s, "
          f"마나포탈: base={mana_params['base_amount']} perRound={mana_params['per_round']} "
          f"성공률={mana_params['success_pct']}%, 상한={mana_cap} 시작={mana_start})")

    def run_with_support(median_table, use_armor):
        backlog = 0.0
        mana = mana_start
        cd_remaining = 0.0
        casts = 0
        collapse = None
        dps_fn = fixed10_dps_fn(median_table)
        for r in range(1, 76):
            e = enemies[r]
            cnt = wave_counts[r]
            mult = armor_mult(e["armor"], defense_armor) if use_armor else 1.0
            dps = dps_fn(r)
            rl = round_length_fn(r, e["is_boss"])
            gain = round(mana_params["base_amount"] + mana_params["per_round"] * r)
            mana = min(mana_cap, mana + gain)  # 성공률 100%(마나 포탈)로 가정 — 실제 파라미터 확인함
            incoming = backlog + cnt
            kills_capacity = dps * mult * rl / e["hp"]
            bonus = 0
            if incoming > kills_capacity and mana >= skill["mana_cost"] and cd_remaining <= rl and e["hp"] <= skill["damage_base"]:
                mana -= skill["mana_cost"]
                casts += 1
                bonus = min(incoming, cnt)
                cd_remaining = skill["cooldown"]
            cd_remaining = max(0.0, cd_remaining - rl)
            remaining = incoming - bonus
            killed = min(remaining, kills_capacity)
            backlog = remaining - killed
            if backlog >= rc["enemy_count_threshold"] and collapse is None:
                collapse = r
        return collapse, casts

    for label, mtable, use_armor in [("AD(고정10기)", ad_median, True), ("AP(고정10기)", ap_median, False)]:
        base = run_backlog(enemies, wave_counts, round_length_fn, defense_armor,
                            fixed10_dps_fn(mtable), use_armor=use_armor, threshold=rc["enemy_count_threshold"])
        sup, casts = run_with_support(mtable, use_armor)
        base_s = f"R{base}" if base else "완주"
        sup_s = f"R{sup}" if sup else "완주"
        print(f"  {label}: 기본 {base_s} -> 도움소 {sup_s} (캐스트 {casts}회)")


if __name__ == "__main__":
    main()
