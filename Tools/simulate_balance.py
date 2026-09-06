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
import math
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


def parse_ladder_grades():
    """UnitData.cs의 `IsLadderGrade(this UnitGrade grade)`를 파싱해 "사다리가 아닌" 등급
    이름 집합을 낸다(사장님 03번 확정 — 랜덤유닛·다른세계를 등급별 화력 사다리에서 뺀다,
    PM 지시 2026-09-06). Tier()는 그대로 둔다 — 서열(로스터 정렬·해체 상한)엔 이 둘도
    자리가 있어야 하고, 조밀한 정수열이라 빼면 유령 티어/재번호매기기 대가가 크다. 여기서
    거르는 건 "등급별 집계"(사다리 단조성 검증) 쪽뿐이다. parse_tier_mapping()과 같은
    이유로 하드코딩하지 않는다 — 값이 바뀌면 조용히 낡아지는 대신 바로 죽어야 한다."""
    text = read("Assets/Scripts/Data/UnitData.cs")
    m = re.search(r"public static bool IsLadderGrade\(this UnitGrade grade\)\s*\{(.*?)\n    \}", text, re.DOTALL)
    if not m:
        sys.exit("FATAL: UnitData.cs에서 IsLadderGrade()를 못 찾았다 — 파일 구조가 바뀐 것으로 보인다.")
    body = m.group(1)

    non_ladder = set()
    pending = []
    for token in re.finditer(r"case UnitGrade\.(\w+):|return (true|false);", body):
        grade_name, ret_val = token.group(1), token.group(2)
        if grade_name:
            pending.append(grade_name)
        elif ret_val == "false":
            non_ladder.update(pending)
            pending = []
        elif ret_val == "true":
            pending = []

    if not non_ladder:
        sys.exit("FATAL: IsLadderGrade() 파싱 결과 비사다리 등급이 0개다 — 파싱이 깨진 것 같다.")
    return non_ladder


TIER_OF = parse_tier_mapping()
GRADE_ENUM = parse_grade_enum()
NON_LADDER_GRADES = parse_ladder_grades()

KOREAN = {"Common": "흔함", "Uncommon": "안흔함", "Special": "특별함", "Rare": "희귀함",
          "Hidden": "히든", "Superior": "특수함", "Legendary": "전설적인", "Limited": "제한됨",
          "Transcendent": "초월함", "Immortal": "불멸", "Eternal": "영원함", "OtherWorld": "다른세계",
          "RandomUnit": "랜덤유닛", "TranscendentWisp": "초월위습", "Transformed": "변화됨"}

# TranscendentWisp는 등급이 아니다(UnitData.cs의 UnitGrade enum 선언 옆 주석, 사장님 확정
# 2026-09-02) — Tier()가 반환하는 값은 연금술 분해방지 전용이라 실제 등급 서열 계산(최대
# Tier·단계 수)에서 빼야 한다. 09-05 Tier 재배정으로 이 값이 8에서 99로 바뀌면서 예전에
# 여기저기 박혀 있던 "9"/"8" 하드코딩이 전부 깨졌다 — 그래서 한 곳에서 유도해서 쓴다.
NON_GRADE_TIER = TIER_OF["TranscendentWisp"]
MAX_TIER = max(t for t in TIER_OF.values() if t != NON_GRADE_TIER)
TIER_COUNT = MAX_TIER + 1


# ---------------------------------------------------------------------------
# 2. 로스터 239종 — attackPower/attackSpeed/damageType/critChance 등을 직접 읽는다.
# ---------------------------------------------------------------------------

def load_upgrade_tracks():
    """등급 연구소 트랙(Assets/Data/UnitUpgrades/*.asset) — 2026-09-06 공속 축 배선
    (구현담당2, 커밋 595dee7) 검산용. `잠금(hasOriginalResearch=0)` 트랙은 아예
    안 돌려준다 — legacyGradeLevels에 절대 안 들어와 SpeedMultiplierForGrade가
    자동으로 1을 돌려주는 런타임 동작과 같다(영원함·다른세계·흔함·안흔함 자동 제외).
    등급(int enum index) → {maxLevel, speed1, speedGrowth, bonus1, bonusGrowth}."""
    tracks = {}
    for path in glob.glob(os.path.join(ROOT, "Assets/Data/UnitUpgrades/*.asset")):
        text = open(path, encoding="utf-8").read()
        # ⚠️ 2026-09-06(구현담당2) — Unity YAML 직렬화가 가끔 "필드:  - 첫항목"을
        # 줄바꿈 없이 한 줄에 붙여 쓴다(조합식 4건에서 실제로 겪음, 그때는 재료가
        # 통째로 0개로 잘못 파싱됐었다). 아래 정규식도 같은 패턴(`^  field:\n`)에
        # 기대므로, 여기서 미리 줄바꿈 형태로 정규화해 같은 함정을 막는다.
        text = re.sub(r"^  targetGrades:  - ", "  targetGrades:\n  - ", text, flags=re.MULTILINE)
        if re.search(r"^  hasOriginalResearch: 0", text, re.MULTILINE):
            continue

        def g(field, default=0.0):
            m = re.search(rf"^  {field}: ([-+0-9.eE]+)", text, re.MULTILINE)
            return float(m.group(1)) if m else default

        grades_m = re.search(r"^  targetGrades:\n((?:  - .*\n?)*)", text, re.MULTILINE)
        grade_ids = [int(x) for x in re.findall(r"- (\d+)", grades_m.group(1))] if grades_m else []
        params = {
            "maxLevel": int(g("maxLevel")),
            "speed1": g("statLevel1Multiplier"),
            "speedGrowth": g("statGrowthPerLevel"),
            "bonus1": g("statLevel1Bonus"),
            "bonusGrowth": g("statBonusGrowthPerLevel"),
        }
        for gid in grade_ids:
            tracks[gid] = params
    return tracks


def track_speed_multiplier(track, level):
    """UnitUpgradeTrackData.SpeedMultiplierForLevel과 같은 식(구현담당2, 595dee7) —
    증가율이라 1을 더한다. level<=0이면 무조건 1(무영향)."""
    if track is None or level <= 0:
        return 1.0
    return 1.0 + track["speed1"] + track["speedGrowth"] * (level - 1)


def track_power_bonus(track, level):
    """UnitUpgradeTrackData.BonusForLevel과 같은 식. level<=0이면 무조건 0."""
    if track is None or level <= 0:
        return 0.0
    return track["bonus1"] + track["bonusGrowth"] * (level - 1)


def load_roster(research_level=0):
    """research_level: 0(기본, 회귀 없음 — 지금까지의 모든 출력과 동일) 또는 "max"
    (그 등급 트랙의 maxLevel, 잠긴 등급은 트랙이 아예 없어 그대로 0과 같다). 2026-09-06
    PM 지시 — "붕괴 라운드(연구 0)"·"붕괴 라운드(연구 max)" 두 끝을 같이 낸다(계단형
    골드-소비 모델은 창작이라 넣지 않는다, PM 확정)."""
    tracks = load_upgrade_tracks() if research_level else None
    recs = []
    for path in glob.glob(os.path.join(ROOT, "Assets/Data/Units/Roster/*.asset")):
        text = open(path, encoding="utf-8").read()
        # ⚠️ 2026-09-06(구현담당2) — "skills:  - {첫 항목}"처럼 줄바꿈 없이 한 줄에
        # 붙는 YAML 직렬화 변형이 실제로 조합식 4건에서 나왔다(그때는 재료가 통째로
        # 0개로 잘못 파싱됐다) — 아래 정규식도 같은 함정(`^  skills:\n` 기대)이라
        # 미리 정규화해둔다.
        text = re.sub(r"^  skills:  - ", "  skills:\n  - ", text, flags=re.MULTILINE)

        def g(field):
            m = re.search(rf"^  {field}: ([-+0-9.eE]+)", text, re.MULTILINE)
            return float(m.group(1)) if m else None

        grade_idx = g("grade")
        if grade_idx is None or int(grade_idx) >= len(GRADE_ENUM):
            continue
        gname = GRADE_ENUM[int(grade_idx)]
        tier = TIER_OF.get(gname)
        ap, aspd = g("attackPower"), g("attackSpeed")
        if tier is None or ap is None or aspd is None:
            continue

        if tracks is not None:
            track = tracks.get(int(grade_idx))
            level = track["maxLevel"] if (track is not None and research_level == "max") else 0
            aspd = aspd * track_speed_multiplier(track, level)
            ap = ap + track_power_bonus(track, level)

        dmgtype = int(g("damageType") or 0)
        cc = g("critChance") or 0.0
        cdm = g("critDamageMultiplier")
        cdm = 1.0 if cdm is None else cdm
        cbd = g("critBonusDamage") or 0.0
        # attackType — 필드가 YAML에 없으면 Unity가 기본값(Unassigned=0)을 생략한 것이다,
        # 값이 없다고 오류가 아니다(§12에서 이미 확인한 패턴).
        at_idx = g("attackType")
        attack_type = (ATTACK_TYPE_ENUM[int(at_idx)]
                       if at_idx is not None and int(at_idx) < len(ATTACK_TYPE_ENUM) else "Unassigned")

        # §17~§22(2026-09-05/06): 원작 능력 배정(스킬 발동형 피해). skill_dps_for_unit이
        # OnHitChance/OnHitCount가 아니거나 Damage 계열이 아니면 전부 0을 준다.
        #
        # §22: UnitData.SkillCount/SkillAt와 같은 규칙 — skills: 리스트가 하나라도
        # 있으면 그걸 전부 쓰고 skill: 단일 필드는 아예 안 본다(PM 지시, "폴백은
        # skills가 비었을 때만"). 리스트 안 중복 guid(황준석 사고, 구현담당1 수정 중)는
        # **그대로 두 번 센다** — 런타임(UnitAttacker)이 SkillAt(0)·SkillAt(1)을 각각
        # 따로 판정해서 중복이면 실제로 두 번 발동하기 때문이다. 시뮬이 그걸 한 번으로
        # 합치면 오히려 런타임과 갈린다.
        skills_m = re.search(r"^  skills:\n((?:  - .*\n?)*)", text, re.MULTILINE)
        skill_guids = re.findall(r"guid: ([0-9a-f]+)", skills_m.group(1)) if skills_m else []
        if not skill_guids:
            skill_m = re.search(r"^  skill: \{fileID: \d+, guid: ([0-9a-f]+)", text, re.MULTILINE)
            if skill_m:
                skill_guids = [skill_m.group(1)]

        skill_components = zero_skill_components()
        skill_attack_type_idx = None
        for guid in skill_guids:
            comp, at_idx = skill_dps_for_unit(guid, ap, aspd)
            skill_components = add_skill_components(skill_components, comp)
            if at_idx is not None:
                skill_attack_type_idx = at_idx
        skill_attack_type = (ATTACK_TYPE_ENUM[skill_attack_type_idx]
                              if skill_attack_type_idx is not None
                              and skill_attack_type_idx < len(ATTACK_TYPE_ENUM) else "Unassigned")

        name_m = re.search(r'^  unitName: "?(.*?)"?$', text, re.MULTILINE)
        recs.append({
            "name": name_m.group(1) if name_m else "?",
            "grade": gname, "tier": tier, "damagetype": dmgtype, "attack_type": attack_type,
            "base_dps": ap * aspd,
            # ⚠️ 2026-09-06 정정(PM 지시, 뿌리 ㉝) — `UnitAttacker.ApplyCritIfTriggered`
            # (UnitAttacker.cs:305) 원문 그대로: 평타가 맞을 때마다(aspd) critChance
            # 확률로 별도 피해 인스턴스 하나를 더 먹인다, 그 크기는
            # `AttackDamage×critDamageMultiplier+critBonusDamage`. 예전 식(`cc*cbd*aspd`)은
            # `ap×cdm` 항이 통째로 빠져 있었다 — cdm이 1.0인 유닛이 많아서(배수 없이
            # 가산만) 눈에 덜 띄었을 뿐, 완전한 근사가 아니었다. 특수 처리 없이 공식을
            # 그대로 옮기면 cdm=1.0인 유닛도 자동으로 맞다.
            "bash_dps": aspd * cc * (ap * cdm + cbd),
            "skill": skill_components, "skill_attack_type": skill_attack_type,
            "skill_guid_count": len(skill_guids),
            "skill_guid_dupes": len(skill_guids) - len(set(skill_guids)),
        })
    return recs


def median_dps_by_tier(roster, damagetype_filter=None, with_bash=False):
    by_tier = defaultdict(list)
    for r in roster:
        if damagetype_filter is not None and r["damagetype"] != damagetype_filter:
            continue
        # 구식 경로 — 방어력·armor_type을 아예 안 보므로 armored/unarmored 구분이
        # 의미가 없다(둘 다 그냥 더한다), gated_flat·percent는 라운드 hp가 없어서 반영
        # 불가(§18부터 문서화된 한계, 그대로 유지) — flat 두 개(AD+AP)만 더한다.
        v = (r["base_dps"] + r["skill"]["flat_ad"] + r["skill"]["flat_ap"]
             + (r["bash_dps"] if with_bash else 0.0))
        by_tier[r["tier"]].append(v)
    out = {}
    for t in range(TIER_COUNT):
        vals = by_tier.get(t)
        out[t] = statistics.median(vals) if vals else None
    return out


def median_dps_by_tier_ladder_only(roster, damagetype_filter=None, with_bash=False):
    """median_dps_by_tier와 같지만 IsLadderGrade()==false인 등급(랜덤유닛·다른세계·
    초월위습)을 등급별 집계에서 뺀다(사장님 03번 확정, PM 지시 2026-09-06). "등급별
    화력이 우상향하는가"를 재는 사다리 검증 전용 — ② 붕괴 라운드 모델(run_backlog)이
    쓰는 median_dps_by_tier는 그대로 둔다. 그건 라운드 진행에 따라 플레이어가 실제로
    확보할 수 있는 유닛 전체(랜덤유닛·다른세계도 뽑기·도박으로 실제 확보 가능)를 재는
    별개 시뮬레이션이라 03번(등급 사다리 단조성)의 대상이 아니다."""
    ladder_roster = [r for r in roster if r["grade"] not in NON_LADDER_GRADES]
    return median_dps_by_tier(ladder_roster, damagetype_filter=damagetype_filter, with_bash=with_bash)


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
        hp = float(re.search(r"^  hp: ([-+0-9.eE]+)", text, re.MULTILINE).group(1))
        armor = float(re.search(r"^  armor: ([-+0-9.eE]+)", text, re.MULTILINE).group(1))
        is_boss = int(re.search(r"^  isBoss: (\d)", text, re.MULTILINE).group(1))
        # armorType — 필드 생략 = Unassigned(0), load_roster의 attackType과 같은 이유.
        at_m = re.search(r"^  armorType: (-?\d+)", text, re.MULTILINE)
        armor_type = (ARMOR_TYPE_ENUM[int(at_m.group(1))]
                      if at_m and int(at_m.group(1)) < len(ARMOR_TYPE_ENUM) else "Unassigned")
        rounds[r] = {"hp": hp, "armor": armor, "is_boss": bool(is_boss), "armor_type": armor_type}
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
    m = re.search(rf"\[SerializeField\][^;]*?\b{field}\s*=\s*([-+0-9.eE]+)f?;", text)
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
    """§22-3(2026-09-06): 구현담당3의 라운드 길이 재정의(5f1a3f8, 2bc6d51)를 반영.
    옛 필드(newWorldRoundDuration 하나로 R61+ 전체를 덮던 구조)가 4구간으로 갈렸다 —
    RoundManager.ResolveRoundDuration 원문 그대로:
      R1        = round_duration(40.65, 원작 하드코딩)
      R2~R39    = normal_round_duration(40.67)
      R40~R60   = short_round_duration(38.67 고정) — shortRoundStartRound부터
      R61~R75   = final_round_duration(36.67 고정, 라운드마다 감소 아님) — new_world_start_round부터,
                  보스 여부와 무관하게 최우선
    그 아래에서만 보스 라운드가 bossRoundDuration으로 끼어든다."""
    rm = read("Assets/Scripts/Waves/RoundManager.cs")
    return {
        "round_duration": parse_float_field(rm, "roundDuration"),
        "normal_round_duration": parse_float_field(rm, "normalRoundDuration"),
        "boss_round_duration": parse_float_field(rm, "bossRoundDuration"),
        "short_round_duration": parse_float_field(rm, "shortRoundDuration"),
        "short_round_start_round": parse_int_field(rm, "shortRoundStartRound"),
        "final_round_duration": parse_float_field(rm, "finalRoundDuration"),
        "new_world_start_round": parse_int_field(rm, "newWorldStartRound"),
        "enemy_count_threshold": parse_int_field(rm, "enemyCountThreshold"),
    }


def load_defense_armor():
    ed = read("Assets/Scripts/Units/EnemyDummy.cs")
    m = re.search(r"public const float DefenseArmor = ([-+0-9.eE]+)f;", ed)
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
        m = re.search(rf"^  {field}: ([-+0-9.eE]+)", text, re.MULTILINE)
        return cast(m.group(1)) if m else None

    return {
        "mana_cost": g("manaCost", int),
        "damage_base": g("damageBase"),
        "cooldown": g("cooldownSeconds"),
        "buff_power_mult": g("buffAttackPowerMultiplier") or 1.0,
        "buff_duration": g("duration") or 0.0,
    }


def load_mana_portal_params():
    """MapGenerator.cs의 ResourcePortal(..., ResourceType.Mana, ...) 호출 하나를 정규식으로
    찾는다. 라인 자체가 바뀌면(리팩터 등) 이 함수가 실패해서 스크립트가 죽는다 — 낡은 값을
    조용히 쓰지 않기 위함이다. 지금은 Assets/Editor/MapGenerator.cs:1612 근방이 원본이다.

    ⚠️ 2026-09-05: 다른 세션이 이 호출에 넷째 인자(UnitGrade.RandomUnit — 해왕류 퀘스트
    관련으로 보인다)를 추가하면서 예전 정규식(세 번째 숫자 뒤 바로 `)`를 기대)이 깨졌다.
    셋째 숫자 뒤에 무엇이 더 있어도 상관없게 고쳤다 — base_amount/per_round/success_pct
    세 값만 본다, 넷째 인자의 의미는 이 스크립트가 몰라도 된다."""
    mg = read("Assets/Editor/MapGenerator.cs")
    m = re.search(
        r"ResourcePortal\.Payout\.Resource,\s*ResourceType\.Mana,\s*([-+0-9.eE]+),\s*([-+0-9.eE]+)f?,\s*([-+0-9.eE]+)f?[,)]",
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


def load_mana_regen():
    """ResourceWallet.cs의 `ManaRegenPerSecond`(원작 umpr, 2026-09-06 구현담당3 추가) —
    ⚠️ 도움소의 진짜 제약은 표기 쿨다운이 아니라 이 시간 재생이다(PM 지시, 2026-09-06).
    자원교환 포탈(`load_mana_portal_params`, 랜덤유닛 등급 위습 소비)은 플레이어가
    위습을 유닛뽑기 대신 여기 쓰는 선택이 들어가 예산에 안 넣는다 — 아래에서 내는
    예산은 시간 재생만 센 **하한**이다."""
    rw = read("Assets/Scripts/Units/ResourceWallet.cs")
    m = re.search(r"const float ManaRegenPerSecond = ([-+0-9.eE]+)f;", rw)
    if not m:
        sys.exit("FATAL: ResourceWallet.cs에서 ManaRegenPerSecond를 못 찾았다.")
    return float(m.group(1))


SUPPORT_SKILL_NAMES = ("낙뢰", "능력치증가", "대지진", "독약", "버스터콜", "불비",
                       "선택위습제조", "지진", "출항이다", "폭우", "해루석", "흡수")


def load_all_support_skills():
    """도움소 12종 전부(`SupportSkill_*.asset`) — 능력별 마나 예산 최대 발동 횟수 표용."""
    out = {}
    for name in SUPPORT_SKILL_NAMES:
        out[name] = load_support_skill(name)
    return out


# ---------------------------------------------------------------------------
# 6. 백로그 시뮬레이터 — 09-04/09-05 문서와 같은 수식.
# ---------------------------------------------------------------------------

def armor_mult(armor, defense_armor):
    if armor >= 0:
        return 1.0 - (defense_armor * armor) / (1.0 + defense_armor * armor)
    return 2.0 - (1.0 - defense_armor) ** (-armor)


def make_round_length_fn(rc):
    """RoundManager.ResolveRoundDuration과 같은 우선순위(§22-3)."""
    def f(r, is_boss):
        if r >= rc["new_world_start_round"]:
            return rc["final_round_duration"]
        if is_boss:
            return rc["boss_round_duration"]
        if r <= 1:
            return rc["round_duration"]
        if r >= rc["short_round_start_round"]:
            return rc["short_round_duration"]
        return rc["normal_round_duration"]
    return f


def tier_for_round(r, total_rounds=75):
    """TIER_COUNT단계 Tier를 전체 라운드에 고르게 배분한다 — 09-04 문서가 만든 가정을 그대로
    재사용한다. 실제 뽑기 확률·라운드별 등급 분포와는 무관한 단순화다(문서 §⑤ 참고).

    09-05 Tier 확장 때 바뀐 건 "몇 단계로 나누는가"(9 → TIER_COUNT)뿐이다 — 가정 자체
    (라운드를 등급 수만큼 고르게 나눈다는 단순화)는 그대로다. 이 가정이 결과를 얼마나
    좌우하는지는 §10(tier_for_round 민감도 실험, `BALANCE_SIMULATION_2026-09-05.md`)
    에서 이미 검증했고, §15(2026-09-06)에서 칸 수·분할 모양을 더 촘촘히 흔들었다."""
    return min(MAX_TIER, (r - 1) * TIER_COUNT // total_rounds)


def tier_for_round_param(r, schedule_count, shape, total_rounds=75):
    """§15(2026-09-06, PM 지시) — `tier_for_round`의 "75라운드 균등분할" 가정을
    두 축으로 흔드는 실험 전용 함수(원본은 안 건드린다). 실제 로스터·Tier 테이블은
    그대로 두고 "라운드 r에 몇 번째 칸을 쓰는가"만 다르게 계산한다.

    schedule_count — 몇 칸으로 나누는가(원래는 TIER_COUNT=12 고정). 실제 등급 수와
    무관한 순수 페이싱 값이다 — schedule_count가 12보다 작으면 칸 하나가 더 넓어져
    저티어에 더 오래 머물고(먼저 실험한 12→10에서 R54→R8로 붕괴가 당겨진 것과 같은
    효과), 12보다 크면 반대로 고티어에 더 빨리 도달한다(도달한 뒤엔 항상
    min(MAX_TIER,...)로 클램프).

    shape — 균등분할(1.0)에서 벗어나는 정도. frac=((r-1)/total_rounds)**shape로
    라운드 진행률을 휘게 한다. shape<1(오목)이면 초반에 frac이 빨리 커져 저티어를
    빨리 지나가고 고티어 구간이 넓어진다("뒤로 갈수록 넓게" — 상위 등급을 오래
    쓴다). shape>1(볼록)이면 반대로 초반 저티어 구간이 넓어진다("앞으로 갈수록
    넓게"). shape=1이면 원래와 같은 선형 균등분할이다.

    ⚠️ 어느 조합이 "맞다"인지는 안 고른다(PM 지시) — "라운드 N에 플레이어가 실제로
    뭘 갖는가"는 원작 경제(골드 배분·가챠 확률·조합 선택)를 모델링해야 나오는데
    지금은 그 근거가 없다. 이 함수는 가정을 흔들어 결과의 흔들림 폭을 재는 용도다."""
    frac = ((r - 1) / total_rounds) ** shape
    return min(MAX_TIER, int(frac * schedule_count))


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
            for t in range(MAX_TIER):
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
            for t in range(MAX_TIER):
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
# 7. DamageTable(상성표) — Assets/Data/DamageTable.asset에서 직접 파싱한다. 하드코딩 없음
#    (PM 지시 2026-09-05, §13 — B안: 평타는 전부 물리, 마법은 스킬에만).
#    §1~§12(위)는 이 절이 존재하기 전 코드 그대로다 — 손대지 않았다, 재현성 그대로.
# ---------------------------------------------------------------------------

def parse_damage_table():
    """Assets/Data/DamageTable.asset(에셋 인스턴스, 클래스 스켈레톤이 아니라 실제 값)을
    파싱한다. 파일 구조가 바뀌면 바로 죽는다 — TIER_OF와 같은 이유."""
    text = read("Assets/Data/DamageTable.asset")
    rows = {}
    for row_name in ("normal", "pierce", "siege", "hero", "chaos", "magic", "spells"):
        m = re.search(
            rf"  {row_name}:\n    vsLarge: ([-+0-9.eE]+)\n    vsFort: ([-+0-9.eE]+)\n"
            rf"    vsNormal: ([-+0-9.eE]+)\n    vsHero: ([-+0-9.eE]+)", text)
        if not m:
            sys.exit(f"FATAL: DamageTable.asset에서 '{row_name}' 행을 못 찾았다 — 파일 구조가 바뀐 것 같다.")
        rows[row_name] = {"Large": float(m.group(1)), "Fort": float(m.group(2)),
                           "Normal": float(m.group(3)), "Hero": float(m.group(4))}
    return rows


def parse_enum(text, enum_name):
    m = re.search(rf"public enum {enum_name}\s*\{{(.*?)\n\}}", text, re.DOTALL)
    if not m:
        sys.exit(f"FATAL: EnemyData.cs에서 {enum_name} enum을 못 찾았다.")
    # ⚠️ 2026-09-06: DamageType처럼 "Name = 상수값,"(명시적 대입) 꼴도 있다 — 그냥
    # "Name,"만 기대하던 옛 정규식은 "= 값" 부분을 못 삼켜서 매치가 통째로 실패했다.
    # 값 자체은 안 쓴다(선언 순서로 인덱스를 매겨야 다른 enum들과 일관된다) — 있어도
    # 그냥 건너뛴다.
    names = re.findall(r"^\s*(\w+)\s*(?:=\s*-?\w+)?\s*,?\s*(?://.*)?$", m.group(1), re.MULTILINE)
    return [n for n in names if n]


DAMAGE_TABLE = parse_damage_table()
_ENEMY_DATA_CS = read("Assets/Scripts/Data/EnemyData.cs")
ARMOR_TYPE_ENUM = parse_enum(_ENEMY_DATA_CS, "ArmorType")
ATTACK_TYPE_ENUM = parse_enum(_ENEMY_DATA_CS, "AttackType")

# ---------------------------------------------------------------------------
# 8. 유닛 스킬(§17, 2026-09-05 — 원작 능력 100종 배정, `3cf3147`) — Assets/Data/UnitSkills/
#    *.asset에서 직접 파싱한다. PM 지시: "사거리와 달리 공간 개념이 필요 없다, 기대 DPS에
#    발동확률×(공격력×배수+추가피해)/공격간격을 더하면 된다 — dps 스칼라 안에서 끝난다."
#    그 기대값을 load_roster()가 각 유닛의 skill_dps에 더한다.
# ---------------------------------------------------------------------------

_SKILL_DATA_CS = read("Assets/Scripts/Data/SkillData.cs")
_UNIT_DATA_CS_FOR_SKILL = read("Assets/Scripts/Data/UnitData.cs")
SKILL_TRIGGER_TYPE_ENUM = parse_enum(_SKILL_DATA_CS, "SkillTriggerType")
SKILL_EFFECT_BASIS_ENUM = parse_enum(_SKILL_DATA_CS, "SkillEffectBasis")
SKILL_EFFECT_KIND_ENUM = parse_enum(_SKILL_DATA_CS, "SkillEffectKind")
SKILL_DAMAGE_TYPE_ENUM = parse_enum(_UNIT_DATA_CS_FOR_SKILL, "DamageType")
ONHIT_CHANCE_IDX = SKILL_TRIGGER_TYPE_ENUM.index("OnHitChance")
ONHIT_COUNT_IDX = SKILL_TRIGGER_TYPE_ENUM.index("OnHitCount")
CASTER_ATTACK_POWER_IDX = SKILL_EFFECT_BASIS_ENUM.index("CasterAttackPower")
FLAT_BASIS_IDX = SKILL_EFFECT_BASIS_ENUM.index("Flat")
MAX_HP_PERCENT_IDX = SKILL_EFFECT_BASIS_ENUM.index("TargetMaxHpPercent")
CUR_HP_PERCENT_IDX = SKILL_EFFECT_BASIS_ENUM.index("TargetCurrentHpPercent")
RESEARCH_LEVEL_IDX = SKILL_EFFECT_BASIS_ENUM.index("ResearchLevel")
DAMAGE_KIND_IDX = SKILL_EFFECT_KIND_ENUM.index("Damage")
SKILL_AP_IDX = SKILL_DAMAGE_TYPE_ENUM.index("AP")


def load_skill_assets():
    """guid → {trigger_type_idx, trigger_chance(레벨1), effects:[{basis_idx, kind_idx,
    attack_type_idx, multiplier, bonus, chance}]}. 레벨1(levels의 첫 항목)만 본다 —
    특성 승급 전 기본값이고, 대부분 레벨이 하나뿐이다(2026-09-05 확인, 108/123).
    파일 구조가 바뀌면(필드 순서 등) 그 파일만 건너뛴다 — 스킬 100종 중 하나가 안
    읽혀도 시뮬레이션 자체를 죽일 정도는 아니라고 판단했다(TIER_OF 등과 다른 판단,
    이유: 스킬은 아직 값이 계속 채워지는 중이라 파일 하나의 형식 오차로 전체가 죽으면
    작업에 방해가 된다)."""
    skills = {}
    for path in glob.glob(os.path.join(ROOT, "Assets/Data/UnitSkills/*.asset")):
        meta_path = path + ".meta"
        if not os.path.exists(meta_path):
            continue
        guid_m = re.search(r"guid: ([0-9a-f]+)", open(meta_path, encoding="utf-8").read())
        if not guid_m:
            continue

        text = open(path, encoding="utf-8").read()
        tt_m = re.search(r"^  triggerType: (\d+)", text, re.MULTILINE)
        if not tt_m:
            continue
        trigger_type_idx = int(tt_m.group(1))

        levels_m = re.search(r"levels:\n(.*)", text, re.DOTALL)
        if not levels_m:
            continue
        level0_m = re.search(r"-\s*cooldown:.*?(?=\n\s*- cooldown:|\Z)", levels_m.group(1), re.DOTALL)
        if not level0_m:
            continue
        level0 = level0_m.group(0)

        tc_m = re.search(r"triggerChance: ([-+0-9.eE]+)", level0)
        trigger_chance = float(tc_m.group(1)) if tc_m else 0.0

        cd_m = re.search(r"-\s*cooldown: ([-+0-9.eE]+)", level0)
        cooldown = float(cd_m.group(1)) if cd_m else 0.0

        # OnHitCount(게이지형) 전용 — ea2a579의 UnitAttacker 카운터와 같은 필드.
        threshold_m = re.search(r"hitCountThreshold: (-?\d+)", level0)
        hit_count_threshold = int(threshold_m.group(1)) if threshold_m else 0
        reset_m = re.search(r"resetTo: (-?\d+)", level0)
        reset_to = int(reset_m.group(1)) if reset_m else 0

        # §22-4(2026-09-06, 06번③) — 버프 게이트(requiredBuffId/forbiddenBuffId). 시뮬은
        # 버프 타이밍(누가 언제 거는지)을 전혀 모델하지 않는다 — **임시 조치**로 방향에
        # 따라 반대로 근사한다(06번 완료 후 재검토 필요, PM 지시 2026-09-06):
        #   requiredBuffId(보유해야 발동) → 발동률 0 (보수적 — 대부분의 시간엔 그 버프가
        #     없다고 가정). ⚠️ 첫 실측(A09E/h04G, §22-5)이 벌써 이 근사가 크게 틀릴 수
        #     있음을 보여줬다 — 실제 발동률은 0이 아니라 평타당 약 0.271이다(buffHitCharges
        #     타수형 버프 + 재중첩 가능한 상태기계라 몬테카를로로 실측). "보수적"이
        #     "안전하게 낮게"를 보장하지 않는다 — 이 unit 하나만도 3.5배 과소평가였다.
        #   forbiddenBuffId(없어야 발동) → **1.0으로 근사**(= 이 게이트를 무시하고 다른
        #     조건만 적용) — 대부분의 시간엔 그 버프가 없어서 통과하므로, 0으로 두면
        #     requiredBuffId와 반대로 가장 크게 틀린다(PM 지적, 2026-09-06). 즉
        #     forbiddenBuffId 단독으로는 rate를 깎지 않는다.
        # §22-7(06번①) — requiredTargetBuffId/forbiddenTargetBuffId(대상의 버프)도 같은
        # 방향 규칙: required는 0, forbidden은 무시. 지금은 둘 다 전 자산 공란이라
        # 회귀 없다.
        req_buff_m = re.search(r"requiredBuffId:\s*(\S+)?", level0)
        req_target_buff_m = re.search(r"requiredTargetBuffId:\s*(\S+)?", level0)
        has_required_buff_gate = bool((req_buff_m and req_buff_m.group(1))
                                       or (req_target_buff_m and req_target_buff_m.group(1)))

        effect_matches = list(re.finditer(
                r"- kind: (\d+)\s*\n\s*basis: (\d+)\s*\n\s*target: \d+\s*\n\s*damageType: (\d+)\s*\n"
                r"\s*attackType: (\d+)\s*\n\s*multiplier: ([-+0-9.eE]+)\s*\n\s*bonus: ([-+0-9.eE]+)\s*\n"
                r"\s*chance: ([-+0-9.eE]+)", level0))

        effects = []
        for idx, em in enumerate(effect_matches):
            kind_idx, basis_idx, damage_type_idx, attack_type_idx, multiplier, bonus, chance = em.groups()
            # §22-8(06번①-2, 2026-09-06) — 효과 단위 대상 버프 게이트(requiredTargetBuffId/
            # forbiddenTargetBuffId, SkillEffect 쪽)는 위 정규식이 안 잡는 뒤쪽 필드라
            # 이 효과 블록만 따로 잘라 찾는다(다음 "- kind:" 전까지, 마지막 효과면 레벨
            # 끝까지).
            block_end = effect_matches[idx + 1].start() if idx + 1 < len(effect_matches) else len(level0)
            block = level0[em.start():block_end]
            req_t_m = re.search(r"requiredTargetBuffId:\s*(\S+)?", block)
            has_required_target_buff = bool(req_t_m and req_t_m.group(1))
            effects.append({
                "kind_idx": int(kind_idx), "basis_idx": int(basis_idx),
                "damage_type_idx": int(damage_type_idx),
                "attack_type_idx": int(attack_type_idx),
                "multiplier": float(multiplier), "bonus": float(bonus), "chance": float(chance),
                "has_required_target_buff": has_required_target_buff,
            })

        skills[guid_m.group(1)] = {
            "trigger_type_idx": trigger_type_idx,
            "trigger_chance": trigger_chance,
            "cooldown": cooldown,
            "hit_count_threshold": hit_count_threshold,
            "reset_to": reset_to,
            "has_required_buff_gate": has_required_buff_gate,
            "effects": effects,
        }
    return skills


SKILLS = load_skill_assets()


def skill_proc_rate(trigger_chance, attack_speed, cooldown):
    """§19(2026-09-05, PM 지시): OnHitChance 스킬이 발동하면 `cooldown`초 동안
    "절대쿨" 잠금이 걸린다 — 그 사이엔 평타가 맞아도 발동확률 자체를 안 굴린다
    (구현담당3이 UnitAttacker에 넣는 중인 것과 같은 규칙).

    타격 간격 Δ=1/공격속도, 발동확률 p, 잠금 L일 때:
      잠금 동안 흘려보내는 타수 = ceil(L/Δ)  (그동안은 확률을 안 굴린다)
      발동 1회당 평균 주기 = ceil(L/Δ)·Δ + Δ/p   (뒤 항은 잠금이 풀린 뒤 "몇 번째
      타격에서 성공하는가"의 기하분포 기댓값)
      초당 발동 = 1 / 위 주기
    L=0이면 ceil(0/Δ)=0이라 주기가 Δ/p로 줄어 **기존 식(p×공격속도)과 정확히
    같다** — 회귀 없음(2026-09-05 확인, 현재 자산 184개 전부 cooldown=0이라
    이 함수가 실제로 옛 값과 다른 값을 낸 사례는 아직 없다).
    """
    if attack_speed <= 0 or trigger_chance <= 0:
        return 0.0
    if cooldown <= 0:
        return trigger_chance * attack_speed

    delta = 1.0 / attack_speed
    locked_ticks = math.ceil(cooldown / delta)
    avg_period = locked_ticks * delta + delta / trigger_chance
    return 1.0 / avg_period if avg_period > 0 else 0.0


SKILL_COMPONENT_KEYS = ("flat_ad", "flat_ap", "gated_flat_ad", "gated_flat_ap", "percent_ad", "percent_ap")


def zero_skill_components():
    return {k: 0.0 for k in SKILL_COMPONENT_KEYS}


def add_skill_components(a, b):
    return {k: a[k] + b[k] for k in SKILL_COMPONENT_KEYS}


def scale_skill_components(a, factor):
    return {k: a[k] * factor for k in SKILL_COMPONENT_KEYS}


def skill_dps_for_unit(skill_guid, attack_power, attack_speed):
    """이 유닛의 스킬이 기대 화력에 얼마를 더하는지 — §17(CasterAttackPower)·§18(Flat·
    %체력)·§19(OnHitChance 절대쿨)·§20(OnHitCount 게이지형)에 §21(피해 공식 미러링,
    EnemyDummy.MitigatedDamage 원문 직접 대조)을 더했다.

    ⚠️ 2026-09-06: `bypassPhysicalArmor = isAbilityDamage && type == DamageType.AP`
    (능력 피해 + AP면 숫자 방어력·마법배율을 **둘 다** 건너뛴다) — 스킬 피해는 전부
    `isAbilityDamage=true`라 **damageType 하나로 armored/unarmored가 갈린다.** 한
    스킬 안에서도 효과마다 damageType이 다를 수 있다(예: 핸콕 h05C — Flat/%체력
    효과 둘은 AP, ResearchLevel 효과 하나는 AD, 셋이 같은 스킬 안에 있다) — 그래서
    "스킬 하나에 대표값 하나"가 아니라 **효과 하나하나를 AD/AP로 갈라 합산**한다.

    %체력(TargetMaxHpPercent/TargetCurrentHpPercent)은 적 HP에 비례해서 고정 dps
    스칼라로 못 접는다 — 그래서 세 축(하나가 아니라 셋)으로 쪼개 AD/AP 각각 반환한다
    (총 6개):
      flat        = 초당발동 × Σ(효과확률×(공격력×배수+추가피해 또는 배수 또는
                    ResearchLevel의 bonus)) — Flat·CasterAttackPower·ResearchLevel
                    성분. **항상** 더해진다(보스든 아니든), armored 쪽만 방어력을 탄다.
      gated_flat  = 초당발동 × Σ(효과확률×%체력효과의 bonus) — hp와 무관하지만
                    **%체력 basis 소속이라 TakesPercentDamage 게이트를 같이 탄다**
                    (0c144e8: %체력·연구 basis가 이제 bonus도 더한다 — 거프 h04C의
                    600만이 여기 해당). 원작 ResolveSkillEffectValue가
                    `target.TakesPercentDamage ? hp*mult+bonus : 0f`로 **곱셈항과
                    상수항을 통째로 게이트 안에** 두는 것과 같은 구조다.
      percent     = 초당발동 × Σ(효과확률×%체력효과의 배수) — hp에 안 곱한 채로
                    반환한다("초당 죽이는 대상의 비율"), TakesPercentDamage 게이트도
                    같이 탄다.
    ResearchLevel은 `CountResearchLevel()×배수+bonus`다. ⚠️ 2026-09-06 — 연구소(05번)가
    실제로 서서 `CountResearchLevel()`이 더는 하드코딩 0이 아니다(플레이어가 연구소에서
    그 등급을 실제로 올린 만큼 값이 나온다, `UnitUpgrades.LevelForGrade` 참고). 이
    시뮬레이터는 플레이어 진행 상태(연구소를 얼마나 올렸는지)를 아예 모델하지 않으므로
    **여전히 "연구 0단계(아무것도 안 삼)" 기준으로 `bonus`만 더한다** — 이건 근사가
    아니라 시뮬의 목적(배정 직후 기준 화력)에 맞는 의도적 바닥값이다. 실제 플레이가
    진행되면 이 항이 시뮬 값보다 커진다는 뜻이니, 여기서 잰 숫자는 "이 정도보다는
    세다"는 하한으로 읽을 것(게이트 없음, %체력과 다른 축이다).

    "초당발동(rate)"은 트리거 타입에 따라 갈린다(§19·§20 그대로, §22-2에서 OnHitCount에
    2차 확률을 추가):
      OnHitChance → skill_proc_rate(절대쿨 포함)
      OnHitCount  → 게이지가 임계에 닿는 주기(1/((threshold-resetTo)×Δ))에 triggerChance를
                    곱한다 — 절대쿨은 안 얹음(§20 그대로).
    그 외(CooldownAutoCast·Aura)는 전부 0.

    ⚠️ §22-2(2026-09-06, 구현담당1 추가): 원작에 "게이지 AND 확률" 조합이 있다
    (`UnitAttacker.TryCastOnHitSkill` — 게이지가 임계에 닿아도 그걸로 끝이 아니라
    `SkillLevel.triggerChance`로 2차 확률 판정을 한 번 더 한다). 리셋은 이 확률과
    무관하게(별도 블록으로) 매 주기 일어나므로 — 판정에 실패해도 게이지 주기 자체는
    그대로 돈다 — "주기당 발동 확률"이 단순 곱으로 들어간다(주기끼리 독립이라 평균
    발동률 = 주기율×triggerChance, 실패가 다음 주기로 누적되지 않는다). 실제 자산
    47종 중 13종이 triggerChance≠1(0.003~0.2)이라 이 항을 안 넣으면 그 13종의
    발동률을 최대 300배까지 과대평가한다 — 나머지 34종은 기본값 1.0이라 이 곱이
    항등이라 회귀 없음.

    ⚠️ `gaugeKind`(Mana/Life) 자체는 시뮬이 안 읽는다 — 게이지가 유닛당 하나를
    여러 스킬이 공유해도(같은 게이지를 쓰는 스킬들의 threshold·resetTo가 실제
    데이터에서 전수 일치함, 2026-09-06 확인) 스킬 하나 입장에서 보는 "내 카운터가
    한 타에 1씩 오른다"는 공유든 아니든 동일해서, 평균 dps 기준으로는 독립
    카운터로 계산해도 공유 카운터와 수치가 같다(동시발동 자체는 dps 총합에 영향
    없음). threshold·resetTo가 갈리는 실제 사례가 생기면 이 가정이 깨진다 —
    그때 gaugeKind별 그룹핑이 필요하다.

    반환: (dict[SKILL_COMPONENT_KEYS], 대표 attack_type_idx 또는 None).
    """
    zero = zero_skill_components()
    skill = SKILLS.get(skill_guid) if skill_guid else None
    if skill is None or skill["trigger_type_idx"] not in (ONHIT_CHANCE_IDX, ONHIT_COUNT_IDX):
        return zero, None

    if skill["has_required_buff_gate"]:
        # §22-4 — **임시 조치**(위 load_skill_assets 주석 참고, 06번 완료 후 재검토
        # 필요) — requiredBuffId만 rate=0으로 근사한다. forbiddenBuffId는 반대 방향
        # (대부분 통과)이라 별도 처리 없이 그대로 둔다(rate를 안 깎는다).
        rate = 0.0
    elif skill["trigger_type_idx"] == ONHIT_CHANCE_IDX:
        rate = skill_proc_rate(skill["trigger_chance"], attack_speed, skill["cooldown"])
    else:
        period_hits = skill["hit_count_threshold"] - skill["reset_to"]
        rate = ((1.0 / (period_hits / attack_speed)) * skill["trigger_chance"]
                 if period_hits > 0 and attack_speed > 0 else 0.0)

    out = zero_skill_components()
    attack_type_idx = None
    for eff in skill["effects"]:
        if eff["kind_idx"] != DAMAGE_KIND_IDX:
            continue
        # §22-8 — 효과 단위 대상 버프 게이트(requiredTargetBuffId, SkillEffect 쪽).
        # forbiddenTargetBuffId와 같은 방향 규칙(§22-6): required만 보수적으로 0(이
        # 효과를 통째로 건너뜀), forbidden은 안 봐도 된다(따로 안 걸러도 그 효과가
        # 그대로 들어간다 = 1.0 근사와 같은 효과).
        if eff["has_required_target_buff"]:
            continue
        is_ap = eff["damage_type_idx"] == SKILL_AP_IDX
        suffix = "ap" if is_ap else "ad"
        attack_type_idx = eff["attack_type_idx"]

        if eff["basis_idx"] == CASTER_ATTACK_POWER_IDX:
            out["flat_" + suffix] += eff["chance"] * (attack_power * eff["multiplier"] + eff["bonus"])
        elif eff["basis_idx"] == FLAT_BASIS_IDX:
            out["flat_" + suffix] += eff["chance"] * eff["multiplier"]
        elif eff["basis_idx"] == RESEARCH_LEVEL_IDX:
            out["flat_" + suffix] += eff["chance"] * eff["bonus"]  # CountResearchLevel()==0 항상, §21 확인
        elif eff["basis_idx"] in (MAX_HP_PERCENT_IDX, CUR_HP_PERCENT_IDX):
            out["percent_" + suffix] += eff["chance"] * eff["multiplier"]
            out["gated_flat_" + suffix] += eff["chance"] * eff["bonus"]

    return scale_skill_components(out, rate), attack_type_idx


def dt_multiplier(attack_type_name, armor_type_name):
    """DamageTable.Multiplier(AttackType, ArmorType)의 파이썬 재현(DamageTable.cs 원문
    그대로) — 한쪽이라도 Unassigned면 1.0."""
    if not attack_type_name or attack_type_name == "Unassigned":
        return 1.0
    if not armor_type_name or armor_type_name == "Unassigned":
        return 1.0
    row = DAMAGE_TABLE.get(attack_type_name.lower())
    if row is None:
        return 1.0
    return row.get(armor_type_name, row.get("Normal", 1.0))


def median_dps_by_tier_vs_armor(roster, armor_type_name):
    """median_dps_by_tier와 같은 모양이지만, 유닛별 attack_type과 armor_type_name의
    DamageTable 배율을 먼저 곱한 뒤 중앙값을 낸다. §18: hp-무관 성분(flat)과 hp-비례
    성분(percent)을 따로 낸다 — 후자는 라운드별 실제 hp를 몰라서 여기선 중앙값만
    내고, hp를 곱하는 건 run_backlog_dt가 라운드마다 한다.

    §21: flat/percent 각각이 다시 AD/AP(armored/unarmored)로 갈린다 — 반환값은
    Tier→SKILL_COMPONENT_KEYS 6개짜리 dict. 평타(base_dps)는 항상 물리라
    flat_ad에만 들어간다(§12-1에서 확인한 "평타는 전부 물리" 그대로).

    §17: 평타(attack_type)와 스킬(skill_attack_type)은 원작 데이터상 서로 다른 공격
    타입을 쓸 수 있어서(스킬 쪽은 CSV에 마법 여부가 없어 평타 타입을 그대로 물려받았을
    뿐 — 3cf3147 커밋 메시지 참고) 각자 자기 attack_type으로 상성표 배율을 따로
    곱한다 — 이건 armored/unarmored 구분과는 별개 축이다(상성표는 방어 무시 여부와
    무관하게 항상 탄다, EnemyDummy.MitigatedDamage 확인).

    ⚠️ 2026-09-06 추가(PM 지시, 뿌리 ㉝ — 크리티컬이 계산은 되는데 이 경로가 아예
    안 읽고 있었다) — `bash_dps`(치명타 기대 dps)도 base_dps와 같은 취급이다:
    `UnitAttacker.ApplyCritIfTriggered`가 평타와 같은 DamageType/AttackType·
    isAbilityDamage=false로 때리므로(UnitAttacker.cs 주석 확인) `flat_ad`에
    `base_mult`(평타와 같은 attack_type 상성 배율)로 더한다 — 별도 취급 없음."""
    by_tier = defaultdict(list)
    for r in roster:
        skill_mult = dt_multiplier(r["skill_attack_type"], armor_type_name)
        base_mult = dt_multiplier(r["attack_type"], armor_type_name)
        v = scale_skill_components(r["skill"], skill_mult)
        v["flat_ad"] += (r["base_dps"] + r["bash_dps"]) * base_mult
        by_tier[r["tier"]].append(v)
    out = {}
    for t in range(TIER_COUNT):
        vals = by_tier.get(t)
        if not vals:
            out[t] = None
            continue
        out[t] = {k: statistics.median(v[k] for v in vals) for k in SKILL_COMPONENT_KEYS}
    return out


ARMOR_TYPES_REAL = ("Normal", "Large", "Fort", "Hero")


def build_median_by_armor(roster, all_median_fallback):
    """4개 실제 방어타입 전부에 대해 Tier별 6성분 표를 만든다. 표본이 없는 Tier는
    (기존 ad_median/ap_median과 같은 관례로) flat_ad에 전체 중앙값을 채우고 나머지
    다섯은 0으로 둔다(all_median_fallback엔 그 다섯 개념이 없다 — §17 이전 경로라서).
    armorType이 Unassigned인 라운드는 dt_multiplier가 이미 1.0을 주므로
    undifferentiated 중앙값과 같다."""
    tables = {}
    for at in ARMOR_TYPES_REAL:
        computed = median_dps_by_tier_vs_armor(roster, at)
        table = {}
        for t in range(TIER_COUNT):
            if computed[t] is not None:
                table[t] = computed[t]
            else:
                fallback = zero_skill_components()
                fallback["flat_ad"] = all_median_fallback[t]
                table[t] = fallback
        tables[at] = table
    unassigned = {}
    for t in range(TIER_COUNT):
        z = zero_skill_components()
        z["flat_ad"] = all_median_fallback[t]
        unassigned[t] = z
    tables["Unassigned"] = unassigned
    return tables


def fixed10_dps_fn_dt(median_by_armor):
    def f(r, armor_type):
        return scale_skill_components(median_by_armor[armor_type][tier_for_round(r)], 10)
    return f


def no_combine_dps_fn_dt(median_by_armor):
    owned = [tier_for_round(1)] * 5

    def f(r, armor_type):
        owned.append(tier_for_round(r))
        owned.append(tier_for_round(r))
        table = median_by_armor[armor_type]
        total = zero_skill_components()
        for t in owned:
            total = add_skill_components(total, table[t])
        return total
    return f


def greedy_combine_dps_fn_dt(median_by_armor):
    counts = Counter({tier_for_round(1): 5})

    def f(r, armor_type):
        table = median_by_armor[armor_type]
        counts[tier_for_round(r)] += 2
        changed = True
        while changed:
            changed = False
            for t in range(MAX_TIER):
                while counts[t] >= 2:
                    counts[t] -= 2
                    counts[t + 1] += 1
                    changed = True
        total = zero_skill_components()
        for t, n in counts.items():
            total = add_skill_components(total, scale_skill_components(table[t], n))
        return total
    return f


def local_optimal_dps_fn_dt(median_by_armor):
    counts = Counter({tier_for_round(1): 5})

    def f(r, armor_type):
        table = median_by_armor[armor_type]
        counts[tier_for_round(r)] += 2
        changed = True
        while changed:
            changed = False
            for t in range(MAX_TIER):
                # 조합 판단은 그 Tier의 flat_ad 성분만 본다(다른 다섯은 라운드별 hp가
                # 있어야 비교가 되거나 AP 몫이라 armor_mult 앞뒤가 달라 단순 비교가
                # 안 된다) — §17까지와 같은 한계다, 새로 생긴 문제가 아니다.
                if (counts[t] >= 2 and table.get(t + 1)
                        and table[t + 1]["flat_ad"] >= table[t]["flat_ad"] * 2):
                    counts[t] -= 2
                    counts[t + 1] += 1
                    changed = True
        total = zero_skill_components()
        for t, n in counts.items():
            total = add_skill_components(total, scale_skill_components(table[t], n))
        return total
    return f


MODELS_DT = {
    "고정 10기/레인": fixed10_dps_fn_dt,
    "안 조합": no_combine_dps_fn_dt,
    "최대 조합(그리디)": greedy_combine_dps_fn_dt,
    "국소최적 조합": local_optimal_dps_fn_dt,
}


# ---------------------------------------------------------------------------
# §⑤(2026-09-06) — "A안"(제한): 영원함(Tier6)·초월함(Tier9)·불멸(Tier11)은
# `war3map.j` 확정(`udg_Tech_Onedill`, `WISP_BUDGET_TIER_CEILING_2026-09-06.md`
# §④)대로 "플레이어당 평생 1기"가 원작 상한이다. 위 MODELS_DT는 이 세 등급도
# 다른 등급과 똑같이 "몇 기든 보유 가능"으로 취급한다 — 그 전제가 틀렸다는 게
# 확정됐으니, 세 등급에 도달하는 슬롯을 "공유 1개"로 캡을 씌운 변형을 따로 둔다.
# 원본 MODELS_DT는 그대로 둔다(B안은 폐기됐지만, 캡을 안 씌운 원본 자체는 다른
# 용도(§13/§14/§15)에 계속 쓰인다).
# ---------------------------------------------------------------------------

CAPPED_GRADE_TIERS = {6, 9, 11}  # 영원함·초월함·불멸 — 셋이 공유하는 슬롯 1개


def fixed10_dps_fn_dt_capped(median_by_armor):
    def f(r, armor_type):
        t = tier_for_round(r)
        n = 1 if t in CAPPED_GRADE_TIERS else 10
        return scale_skill_components(median_by_armor[armor_type][t], n)
    return f


def no_combine_dps_fn_dt_capped(median_by_armor):
    owned = [tier_for_round(1)] * 5
    slot_used = [False]  # 영원함·초월함·불멸 공유 슬롯 — 한 번 채워지면 그 뒤로는 버려진다

    def f(r, armor_type):
        for _ in range(2):
            t = tier_for_round(r)
            if t in CAPPED_GRADE_TIERS:
                if slot_used[0]:
                    continue
                slot_used[0] = True
            owned.append(t)
        table = median_by_armor[armor_type]
        total = zero_skill_components()
        for t in owned:
            total = add_skill_components(total, table[t])
        return total
    return f


def greedy_combine_dps_fn_dt_capped(median_by_armor):
    counts = Counter({tier_for_round(1): 5})
    slot_used = [False]

    def f(r, armor_type):
        table = median_by_armor[armor_type]
        counts[tier_for_round(r)] += 2
        changed = True
        while changed:
            changed = False
            for t in range(MAX_TIER):
                target = t + 1
                while counts[t] >= 2:
                    if target in CAPPED_GRADE_TIERS:
                        if slot_used[0]:
                            break  # 이 이상은 못 올라간다 — 재료만 그 등급에 묶인다
                        slot_used[0] = True
                    counts[t] -= 2
                    counts[target] += 1
                    changed = True
        total = zero_skill_components()
        for t, n in counts.items():
            total = add_skill_components(total, scale_skill_components(table[t], n))
        return total
    return f


def local_optimal_dps_fn_dt_capped(median_by_armor):
    counts = Counter({tier_for_round(1): 5})
    slot_used = [False]

    def f(r, armor_type):
        table = median_by_armor[armor_type]
        counts[tier_for_round(r)] += 2
        changed = True
        while changed:
            changed = False
            for t in range(MAX_TIER):
                target = t + 1
                if not (counts[t] >= 2 and table.get(target)
                        and table[target]["flat_ad"] >= table[t]["flat_ad"] * 2):
                    continue
                if target in CAPPED_GRADE_TIERS:
                    if slot_used[0]:
                        continue
                    slot_used[0] = True
                counts[t] -= 2
                counts[target] += 1
                changed = True
        total = zero_skill_components()
        for t, n in counts.items():
            total = add_skill_components(total, scale_skill_components(table[t], n))
        return total
    return f


MODELS_DT_CAPPED = {
    "고정 10기/레인": fixed10_dps_fn_dt_capped,
    "안 조합": no_combine_dps_fn_dt_capped,
    "최대 조합(그리디)": greedy_combine_dps_fn_dt_capped,
    "국소최적 조합": local_optimal_dps_fn_dt_capped,
}


def run_backlog_dt(enemies, wave_counts, round_length_fn, defense_armor,
                    team_dps_fn, threshold, total_rounds=75):
    """run_backlog과 같지만 매 라운드 적의 armor_type을 읽어 team_dps_fn(r, armor_type)로
    넘긴다 — DamageTable을 반영한 §13 전용.

    §18: team_dps_fn이 hp-무관 성분(flat)과 hp-비례 성분(percent)을 나눠 돌려준다.
    percent는 "초당 죽이는 대상 비율"이라 이번 라운드의 실제 hp를 곱해야 flat과 같은
    단위가 된다 — dps = flat + percent×hp로 합치면 kills_capacity = dps×mult×rl/hp가
    자동으로 flat×mult×rl/hp + percent×mult×rl로 풀려서, %체력 성분이 hp와 무관한
    "초당 킬 수"로 정확히 떨어진다.

    §21(2026-09-06): team_dps_fn이 이제 6성분 dict(SKILL_COMPONENT_KEYS)를 돌려준다.
    `EnemyDummy.MitigatedDamage` 원문 그대로 — `isAbilityDamage && type==AP`면 숫자
    방어력(armor_mult)도 마법배율도 둘 다 건너뛴다(우리 스킬 피해는 전부
    isAbilityDamage=true이므로 damageType 하나로 armored/unarmored가 갈린다).
    그래서 AD 몫(armored)에만 `mult`를 곱하고, AP 몫(unarmored)은 그대로 더한다 —
    상성표(dt_multiplier)는 이미 team_dps_fn을 만들 때 양쪽 다 곱해뒀다(방어 무시
    여부와 무관하게 항상 탄다, 확인 완료).

    ⚠️ %체력 분기 게이트(EnemyData.takesPercentDamage, 원작 GetUnitPointValue<200)는
    percent와 gated_flat(%체력 basis의 bonus, 0c144e8) 둘 다에 적용된다 — 원작
    `target.TakesPercentDamage ? hp*mult+bonus : 0f`가 곱셈항·상수항을 통째로 게이트
    안에 두는 것과 같다. 보스(is_boss)는 이 둘을 아예 안 받는다. 라운드 보스 9종만
    걸린다(스토리 건물 등 다른 보스 판정은 이 라운드 시퀀스에 안 들어온다)."""
    backlog = 0.0
    collapse = None
    for r in range(1, total_rounds + 1):
        e = enemies[r]
        cnt = wave_counts[r]
        mult = armor_mult(e["armor"], defense_armor)
        c = team_dps_fn(r, e["armor_type"])
        takes_percent = not e["is_boss"]
        armored = c["flat_ad"] + (takes_percent * (c["gated_flat_ad"] + c["percent_ad"] * e["hp"]))
        unarmored = c["flat_ap"] + (takes_percent * (c["gated_flat_ap"] + c["percent_ap"] * e["hp"]))
        dps = armored * mult + unarmored
        rl = round_length_fn(r, e["is_boss"])
        incoming = backlog + cnt
        kills_capacity = dps * rl / e["hp"]
        killed = min(incoming, kills_capacity)
        backlog = incoming - killed
        if backlog >= threshold and collapse is None:
            collapse = r
    return collapse


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
    for t in range(TIER_COUNT):
        if ap_median[t] is None:
            ap_median[t] = all_median[t]
        if ad_median[t] is None:
            ad_median[t] = all_median[t]

    print("=== ① 등급별 median DPS (Tier(), UnitData.cs에서 직접 파싱) ===")
    print(f"{'Tier':6s}{'등급':20s}{'ALL':>10s}{'AD':>10s}{'AP':>10s}")
    for t in range(TIER_COUNT):
        grades = ",".join(KOREAN[g] for g in GRADE_ENUM if TIER_OF.get(g) == t)
        print(f"T{t:<5d}{grades:20s}{all_median[t]:>10.0f}{ad_median[t]:>10.0f}{ap_median[t]:>10.0f}")

    mono_breaks = [t for t in range(1, TIER_COUNT) if all_median[t] < all_median[t - 1]]
    print(f"\n⚠️ 단조성 깨지는 지점(ALL 기준): T{mono_breaks}" if mono_breaks else "\n단조 증가 확인됨(ALL 기준)")
    print("이건 시뮬레이션 가정이 아니라 로스터 실값이다 — 2026-09-05 구현담당2/구현담당1 교차 확인.")
    print(f"⚠️ 이 표는 사다리가 아닌 2등급({','.join(KOREAN[g] for g in sorted(NON_LADDER_GRADES) if g in KOREAN)})을 "
          f"포함해 집계된 것이다 — 03번 검산은 바로 아래 ①' 표를 본다(PM 지시 2026-09-06).")

    ladder_tiers = sorted({t for g, t in TIER_OF.items() if g not in NON_LADDER_GRADES})
    ad_median_l = median_dps_by_tier_ladder_only(roster, damagetype_filter=1)
    ap_median_l = median_dps_by_tier_ladder_only(roster, damagetype_filter=2)
    all_median_l = median_dps_by_tier_ladder_only(roster)
    for t in ladder_tiers:
        if ap_median_l[t] is None:
            ap_median_l[t] = all_median_l[t]
        if ad_median_l[t] is None:
            ad_median_l[t] = all_median_l[t]

    print("\n=== ①' 등급별 median DPS — 사다리 전용(랜덤유닛·다른세계 제외, 03번 검산) ===")
    print(f"{'Tier':6s}{'등급':20s}{'ALL':>10s}{'AD':>10s}{'AP':>10s}")
    for t in ladder_tiers:
        grades = ",".join(KOREAN[g] for g in GRADE_ENUM if TIER_OF.get(g) == t and g not in NON_LADDER_GRADES)
        print(f"T{t:<5d}{grades:20s}{all_median_l[t]:>10.0f}{ad_median_l[t]:>10.0f}{ap_median_l[t]:>10.0f}")

    mono_breaks_l = [ladder_tiers[i] for i in range(1, len(ladder_tiers))
                      if all_median_l[ladder_tiers[i]] < all_median_l[ladder_tiers[i - 1]]]
    print(f"\n⚠️ 단조성 깨지는 지점(사다리 전용 ALL 기준): T{mono_breaks_l}"
          if mono_breaks_l else "\n단조 증가 확인됨(사다리 전용 ALL 기준) — 랜덤유닛·다른세계를 빼도 사다리가 무너지지 않는다.")

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

    # -----------------------------------------------------------------
    # ③ 도움소 — 2026-09-06 재설계(PM 지시). 표기 쿨다운이 아니라 마나 재생
    # (`ManaRegenPerSecond`, 원작 umpr=0.30/초, 오늘 구현담당3이 추가)이 진짜 제약이다.
    # ⚠️ 예전엔 이 절이 자원교환 포탈(마나 포탈, 랜덤유닛 등급 위습 소비)을 "성공률 100%
    # 자동 지급"으로 가정해 사실상 공짜 마나 취급했다 — 그 포탈은 위습을 유닛뽑기 대신
    # 여기 쓰는 선택이 들어가는 자원교환이라 이제 모델에서 뺀다(PM 지시, 2026-09-06 —
    # "최적 배분을 지어내지 마세요"). 시간 재생만으로 예산을 다시 낸다 — 그래서 이 값은
    # **하한**이다(포탈까지 쓰면 더 늘어난다).
    # -----------------------------------------------------------------
    mana_regen = load_mana_regen()
    mana_cap, mana_start = load_mana_cap_and_start()
    total_round_seconds = sum(round_length_fn(r, enemies[r]["is_boss"]) for r in range(1, 76))
    mana_budget = mana_start + total_round_seconds * mana_regen

    print("\n=== ③ 도움소 — 마나 예산(시간 재생만, 하한) · 능력별 최대 발동 횟수 ===")
    print(f"75라운드 총 길이 {total_round_seconds:.1f}초 × 재생 {mana_regen}/초 + 시작 {mana_start} "
          f"= 예산 {mana_budget:.1f} (상한 {mana_cap} — 예산이 상한 밑이라 저축해도 안 넘친다)")
    print("⚠️ 자원교환 포탈(20+라운드×1.5, 랜덤유닛 위습 소비)은 안 셌다 — 위습 배분은 창작이라 제외.")
    print("⚠️ 표기 쿨다운 기준 '이론상 최대'는 진짜 제약이 아니다 — 마나가 항상 먼저 바닥난다.")
    print(f"{'능력':10s}{'마나':>6s}{'쿨(초)':>8s}{'예산 기준 최대':>14s}{'쿨다운 기준 이론상':>18s}")
    all_skills = load_all_support_skills()
    for name, sk in all_skills.items():
        if not sk["mana_cost"]:
            print(f"{name:10s}{'-':>6s}{sk['cooldown'] or 0:>8.0f}{'(마나 안 씀)':>14s}{'-':>18s}")
            continue
        budget_max = int(mana_budget // sk["mana_cost"])
        cd_max = int(total_round_seconds // sk["cooldown"]) if sk["cooldown"] else None
        cd_s = f"{cd_max}" if cd_max is not None else "-"
        print(f"{name:10s}{sk['mana_cost']:>6d}{sk['cooldown'] or 0:>8.0f}{budget_max:>14d}{cd_s:>18s}")

    # 붕괴 라운드 재측정 — 피해를 직접 주는 능력만(damageBase>0). 스턴·버프·소환·즉사
    # 등(지진·출항이다·흡수·능력치증가·선택위습제조)은 "배틀로그를 얼마나 줄이는가"를
    # 만들어내야 해서(창작) 이번엔 안 낸다. 능력 하나만 쓴다고 가정한다(다른 능력과
    # 마나를 나눠 쓰는 배분은 안 지어낸다, PM 지시) — 각 능력을 독립으로 딱 한 번씩만 잰다.
    print("\n=== ③-보강 붕괴 라운드 — 능력 하나만 단독 사용(마나 재생 기준), 고정10기 모델 ===")
    print("⚠️ 2026-09-06 PM 지적 — '한 방 킬' 게이트는 도움소 전용 근사다(grep 확인,")
    print("   skill_dps_for_unit/DealSkillDamage 등 일반 스킬 피해 경로엔 이 게이트가 없다 —")
    print("   범위 스킬 전체가 과소 계상되는 문제는 아니다). 다만 도움소 자체는 '한 방에")
    print("   못 죽이면 기여 0'이 실제보다 비관적이라 세 가지를 나란히 낸다.")

    def run_with_support(median_table, use_armor, skill, credit_mode):
        """credit_mode:
        'oneshot'  — 예전 모델(그대로 보존, 비교용). e.hp<=damage_base일 때만 그 라운드
                     cnt 전체를 죽인 것으로 친다. damage_base가 hp에 못 미치면 기여 0 —
                     '한 방 못 죽이면 안 쓴다'가 아니라 '이 모델이 부분 기여를 못 센다'는
                     한계였다(PM 지적).
        'aoe_all'  — 부분 기여, 상한: 범위 안 모든 적(cnt마리)이 각자 damage_base를 맞는다고
                     본다(설명문 '넓은 범위'). coverage=min(1, damage_base/hp)를 cnt에 곱해
                     그 라운드 킬 수에 더한다 — 몇 마리가 실제로 맞는지는 모르니(원작 라운드당
                     35마리가 어떻게 퍼져 있는지 불명, 지어내지 않는다) '전부 맞는다'는
                     상한이다.
        'single'   — 부분 기여, 하한: 단일 대상 1마리만 맞는다고 본다(coverage×1).
        세 값을 나란히 내면 실제는 그 사이라는 게 보인다(PM 지시, 오늘 연구0/max와 같은 방식).
        """
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
            mana = min(mana_cap, mana + mana_regen * rl)
            incoming = backlog + cnt
            kills_capacity = dps * mult * rl / e["hp"]
            bonus = 0
            can_afford = mana >= skill["mana_cost"] and cd_remaining <= rl
            if credit_mode == "oneshot":
                worth_casting = e["hp"] <= skill["damage_base"]
            else:
                worth_casting = skill["damage_base"] > 0  # 부분 기여는 항상 뭔가는 깎는다
            if incoming > kills_capacity and can_afford and worth_casting:
                mana -= skill["mana_cost"]
                casts += 1
                cd_remaining = skill["cooldown"]
                if credit_mode == "oneshot":
                    bonus = min(incoming, cnt)
                else:
                    coverage = min(1.0, skill["damage_base"] / e["hp"])
                    hit_count = cnt if credit_mode == "aoe_all" else 1
                    bonus = min(incoming, coverage * hit_count)
            cd_remaining = max(0.0, cd_remaining - rl)
            remaining = incoming - bonus
            killed = min(remaining, kills_capacity)
            backlog = remaining - killed
            if backlog >= rc["enemy_count_threshold"] and collapse is None:
                collapse = r
        return collapse, casts

    for name, sk in all_skills.items():
        if not sk["damage_base"]:
            continue
        print(f"  [{name}] mana={sk['mana_cost']} dmg={sk['damage_base']:.0f} cd={sk['cooldown']:.0f}s")
        for label, mtable, use_armor in [("AD(고정10기)", ad_median, True), ("AP(고정10기)", ap_median, False)]:
            base = run_backlog(enemies, wave_counts, round_length_fn, defense_armor,
                                fixed10_dps_fn(mtable), use_armor=use_armor, threshold=rc["enemy_count_threshold"])
            base_s = f"R{base}" if base else "완주"
            results = []
            for mode, mode_label in [("oneshot", "한방킬(기존)"), ("aoe_all", "부분기여-전부맞음(상한)"),
                                       ("single", "부분기여-한마리만(하한)")]:
                sup, casts = run_with_support(mtable, use_armor, sk, mode)
                sup_s = f"R{sup}" if sup else "완주"
                results.append(f"{mode_label}={sup_s}({casts}회)")
            print(f"    {label}: 기본 {base_s} -> " + " / ".join(results))

    # ⚠️ 2026-09-06 PM 지시 — 출항이다(피해 0이라 위 damage_base 루프에서 빠짐)는 팀
    # 전체 공격력을 map-wide로 10초간 ×1.5 올린다(buffAttackPowerMultiplier·duration,
    # SupportSkill_출항이다.asset). 범위 안 마릿수를 몰라도 되는 축이라(map-wide라
    # "몇 마리가 맞는가"라는 추측이 아예 없다) 정확하게 낼 수 있다 — 라운드 길이가
    # 전부 10초보다 훨씬 길어(37~40초) 지속시간이 항상 그 라운드 안에 다 들어간다고
    # 본다(잘려나가는 경우 없음).
    print("\n=== ③-보강② 출항이다(공격력 버프, 피해 0) — map-wide라 마릿수 추측 불필요 ===")
    sail = load_support_skill("출항이다")
    print(f"  [출항이다] mana={sail['mana_cost']} cd={sail['cooldown']:.0f}s "
          f"buff=×{sail['buff_power_mult']} {sail['buff_duration']:.0f}초")

    def run_with_buff(median_table, use_armor, skill):
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
            mana = min(mana_cap, mana + mana_regen * rl)
            incoming = backlog + cnt
            kills_capacity = dps * mult * rl / e["hp"]
            bonus = 0
            if incoming > kills_capacity and mana >= skill["mana_cost"] and cd_remaining <= rl:
                mana -= skill["mana_cost"]
                casts += 1
                cd_remaining = skill["cooldown"]
                # 버프 구간(duration초)만 dps가 (buff_power_mult-1)배 더 나온다 — 그만큼
                # 킬 능력치가 늘어난 걸 그 라운드 kills_capacity에 얹는다(위 도움소들처럼
                # "즉시 몇 마리를 지운다"가 아니라 "이번 라운드 dps가 잠깐 더 세진다").
                extra_dps = dps * mult * (skill["buff_power_mult"] - 1.0)
                bonus = min(incoming, extra_dps * skill["buff_duration"] / e["hp"])
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
        sup, casts = run_with_buff(mtable, use_armor, sail)
        base_s = f"R{base}" if base else "완주"
        sup_s = f"R{sup}" if sup else "완주"
        print(f"    {label}: 기본 {base_s} -> 도움소 {sup_s} (캐스트 {casts}회)")

    # -----------------------------------------------------------------
    # §13: DamageTable(상성표) 반영 — B안 이후 실제 배정. PM 지시 2026-09-05.
    # 배정 전(=attackType 전부 Unassigned와 동치, all_median) → 후(실제 배정, 로스터
    # attackType 그대로 읽음) 비교. AD/AP 팀 구분 없음 — B안 이후 평타가 전부 물리다.
    # -----------------------------------------------------------------
    print("\n=== §13: DamageTable(상성표) 반영 — 배정 전/후, 팀 구분 없음(전부 물리) ===")

    def flat_ad_only_table():
        table = {}
        for t in range(TIER_COUNT):
            z = zero_skill_components()
            z["flat_ad"] = all_median[t]
            table[t] = z
        return table

    before_by_armor = {at: flat_ad_only_table() for at in ARMOR_TYPES_REAL + ("Unassigned",)}
    after_by_armor = build_median_by_armor(roster, all_median)

    print("--- ① 배정 전 → 후 붕괴 라운드 (방어력 항상 적용) ---")
    print(f"{'모델':20s}{'배정 전':14s}{'배정 후':14s}")
    for name, fn in MODELS_DT.items():
        before_r = run_backlog_dt(enemies, wave_counts, round_length_fn, defense_armor,
                                   fn(before_by_armor), threshold=rc["enemy_count_threshold"])
        after_r = run_backlog_dt(enemies, wave_counts, round_length_fn, defense_armor,
                                  fn(after_by_armor), threshold=rc["enemy_count_threshold"])
        before_s = f"붕괴 R{before_r}" if before_r else "완주(75R)"
        after_s = f"붕괴 R{after_r}" if after_r else "완주(75R)"
        print(f"{name:20s}{before_s:14s}{after_s:14s}")

    def effective_dps(table, armor_type, tier, hp, is_boss, mult):
        # §21: armored(AD, mult 적용) + unarmored(AP, mult 안 적용) — run_backlog_dt와
        # 같은 결합식. %체력 게이트(보스는 percent·gated_flat을 아예 안 받음)도 같이 본다.
        c = table[armor_type][tier]
        takes_percent = not is_boss
        armored = c["flat_ad"] + (takes_percent * (c["gated_flat_ad"] + c["percent_ad"] * hp))
        unarmored = c["flat_ap"] + (takes_percent * (c["gated_flat_ap"] + c["percent_ap"] * hp))
        return armored * mult + unarmored

    def weighted_pct_change(rounds_range):
        total_w = acc_before = acc_after = 0.0
        for r in rounds_range:
            e = enemies[r]
            w = wave_counts[r] * e["hp"]
            t = tier_for_round(r)
            mult = armor_mult(e["armor"], defense_armor)
            total_w += w
            acc_before += w * effective_dps(before_by_armor, e["armor_type"], t, e["hp"], e["is_boss"], mult)
            acc_after += w * effective_dps(after_by_armor, e["armor_type"], t, e["hp"], e["is_boss"], mult)
        return (acc_after - acc_before) / acc_before * 100 if acc_before else float("nan")

    print("\n--- ② 구간별 실제 배정 화력 변화 (HP×마릿수 가중, tier_for_round 스케줄 기준) ---")
    for label, rng in (("R1~R30", range(1, 31)), ("R31~R60", range(31, 61)), ("R61~R75", range(61, 76))):
        print(f"{label}: {weighted_pct_change(rng):+.1f}%")

    print("\n--- ③ 지금 배정(원작 등급별 분포) vs 전원 normal(이론상 최고 비교군) ---")
    roster_all_normal = [dict(u, attack_type="Normal") for u in roster]
    normal_by_armor = build_median_by_armor(roster_all_normal, all_median)
    print(f"{'모델':20s}{'전원 normal':14s}{'지금 배정':14s}")
    for name, fn in MODELS_DT.items():
        normal_r = run_backlog_dt(enemies, wave_counts, round_length_fn, defense_armor,
                                   fn(normal_by_armor), threshold=rc["enemy_count_threshold"])
        current_r = run_backlog_dt(enemies, wave_counts, round_length_fn, defense_armor,
                                    fn(after_by_armor), threshold=rc["enemy_count_threshold"])
        normal_s = f"붕괴 R{normal_r}" if normal_r else "완주(75R)"
        current_s = f"붕괴 R{current_r}" if current_r else "완주(75R)"
        print(f"{name:20s}{normal_s:14s}{current_s:14s}")

    # -----------------------------------------------------------------
    # §14: 연구소 공속 축(2026-09-06, 구현담당2, 595dee7) — 두 끝만 낸다. "라운드가
    # 오를수록 연구 단계가 얼마나 오르는가"의 완만한 곡선은 골드를 연구에 얼마나
    # 우선순위 두는지에 달려 있는데, 그 배분 비율은 원작에도 우리에도 근거가 없어
    # 창작이다(PM 확정 2026-09-06 — 계단형 근사 넣지 않는다). 대신 "연구 0"(하한,
    # 지금까지의 모든 §1~§13 출력과 동일 — 이 섹션 이전 코드는 전부 load_roster()
    # 기본값 그대로라 회귀 없음)과 "연구 max"(상한, 그 등급 트랙의 원작 maxLevel)
    # 두 끝을 같이 낸다 — 잠긴 트랙(영원함·다른세계·흔함·안흔함)은 load_upgrade_tracks가
    # 아예 안 돌려줘서 "연구 max"에서도 자동으로 0(무변화)과 같다.
    # -----------------------------------------------------------------
    print("\n=== §14: 연구소 공속 축 — 붕괴 라운드(연구 0) vs (연구 max) ===")

    roster_max = load_roster(research_level="max")
    all_median_max = median_dps_by_tier(roster_max)
    after_by_armor_max = build_median_by_armor(roster_max, all_median_max)

    print(f"{'모델':20s}{'연구 0':14s}{'연구 max':14s}")
    for name, fn in MODELS_DT.items():
        r0 = run_backlog_dt(enemies, wave_counts, round_length_fn, defense_armor,
                             fn(after_by_armor), threshold=rc["enemy_count_threshold"])
        rmax = run_backlog_dt(enemies, wave_counts, round_length_fn, defense_armor,
                               fn(after_by_armor_max), threshold=rc["enemy_count_threshold"])
        r0_s = f"붕괴 R{r0}" if r0 else "완주(75R)"
        rmax_s = f"붕괴 R{rmax}" if rmax else "완주(75R)"
        print(f"{name:20s}{r0_s:14s}{rmax_s:14s}")

    # ⚠️ 아래 세 줄은 "연구가 싸다"는 결론이 아니다 — 원작 비용 필드(gglb/gglm) 두
    # 숫자가 둘 다 골드인지, 하나가 목재인지 아직 리서치담당 확인 대기다(PM 지시
    # 2026-09-06). 목재라면 결론이 뒤집힌다(목재가 우리 게임에서 훨씬 귀하다) — 그래서
    # "킬 골드 기준" 전제를 명시하고 꼬리표를 단다. UnitUpgradeTrackData.CostForLevel
    # (레벨1만 costBase, 이후 매레벨 costGrowthPerLevel 정액)과 Enemy_R*.asset의
    # goldReward × 라운드당 마릿수로 계산했다 — 도움소·조합·특성강화 등 다른 지출은
    # 전혀 없다고 가정한 상한값이다.
    print("\n--- 참고: 킬 골드 기준 연구비용 도달 라운드 (⚠️ 비용 단위 gglb/gglm 확인 대기, 목재면 결론 뒤집힘) ---")
    print("R4  누적 1,190엔   초월함 트랙 하나(1,075엔) 만렙 가능")
    print("R16 누적 7,215엔   8개 트랙 전부(6,605엔) 만렙 가능")
    print("8트랙 만렙 비용 = 전 게임 총수입(≈99,630엔)의 6.6% — 다른 지출 0 가정, 골드 단위 확인 전 참고치")

    # -----------------------------------------------------------------
    # §15(2026-09-06, PM 지시) — tier_for_round 신뢰구간. "75라운드 균등분할"
    # 가정을 두 축(칸 수·분할 모양)으로 흔들어 붕괴 라운드가 얼마나 움직이는지 잰다.
    # ⚠️ 어느 조합이 맞는지 고르지 않는다 — 그건 원작 경제(골드 배분·가챠 확률)를
    # 몰라서 지금은 창작이다. 로스터·Tier 테이블은 그대로 두고 tier_for_round만
    # 일시적으로 바꿔치기한 뒤 되돌린다(§10, BALANCE_SIMULATION_2026-09-05.md와 같은
    # 방법 — 그때는 "항상 T0/T4/T8" 극단값 3개, 이번엔 칸 수·모양을 촘촘히 스캔한다).
    # -----------------------------------------------------------------
    print("\n=== §15: tier_for_round 신뢰구간 — 칸 수 × 분할 모양 스윕 (4모델×연구0/max) ===")
    print("⚠️ 어느 조합이 맞는지 고르지 않는다 — 결과의 흔들림 폭만 잰다(PM 지시).")

    schedule_counts = [8, 10, 12, 14]
    shapes = [(0.5, "뒤가 넓음(고티어 오래)"), (1.0, "균등(원래)"), (2.0, "앞이 넓음(저티어 오래)")]

    global tier_for_round
    original_tier_for_round = tier_for_round

    def sweep_run(fn, after_armor):
        return run_backlog_dt(enemies, wave_counts, round_length_fn, defense_armor,
                               fn(after_armor), threshold=rc["enemy_count_threshold"])

    all_results = {name: [] for name in MODELS_DT}  # name -> list of (collapse_or_None) across every combo×연구단계
    print(f"\n--- 고정 10기/레인 상세 그리드 (가장 많이 인용된 모델) ---")
    print(f"{'칸수':6s}{'모양':22s}{'연구0':>10s}{'연구max':>10s}")
    fixed_fn = MODELS_DT["고정 10기/레인"]
    for sc in schedule_counts:
        for shape, shape_label in shapes:
            tier_for_round = lambda r, total_rounds=75, _sc=sc, _sh=shape: tier_for_round_param(r, _sc, _sh, total_rounds)
            r0 = sweep_run(fixed_fn, after_by_armor)
            rmax = sweep_run(fixed_fn, after_by_armor_max)
            all_results["고정 10기/레인"].append(r0)
            all_results["고정 10기/레인"].append(rmax)
            r0_s = f"R{r0}" if r0 else "완주"
            rmax_s = f"R{rmax}" if rmax else "완주"
            print(f"{sc:<6d}{shape_label:22s}{r0_s:>10s}{rmax_s:>10s}")

    print("\n--- 나머지 3모델 — 같은 12개 조합(칸수4×모양3)을 돌려 범위만 요약 ---")
    for name, fn in MODELS_DT.items():
        if name == "고정 10기/레인":
            continue
        for sc in schedule_counts:
            for shape, _ in shapes:
                tier_for_round = lambda r, total_rounds=75, _sc=sc, _sh=shape: tier_for_round_param(r, _sc, _sh, total_rounds)
                all_results[name].append(sweep_run(fn, after_by_armor))
                all_results[name].append(sweep_run(fn, after_by_armor_max))

    tier_for_round = original_tier_for_round  # 반드시 원상복구 — 이 아래로는 원래 함수를 쓴다

    print(f"\n{'모델':20s}{'붕괴 라운드 범위(12개 조합×연구0/max, 완주 제외)':45s}")
    for name in MODELS_DT:
        collapses = [c for c in all_results[name] if c is not None]
        completed = len(all_results[name]) - len(collapses)
        if collapses:
            rng = f"R{min(collapses)}~R{max(collapses)} (24개 중 {completed}개는 완주)"
        else:
            rng = "24개 조합 전부 완주"
        print(f"{name:20s}{rng:45s}")

    print("\n⚠️ 이 가정을 닫으려면 필요한 데이터(§10-5, BALANCE_SIMULATION_2026-09-05.md에서 이미 지목함):")
    print("  GachaTable 등급별 weight(지금 등급4+ 전부 0 — 사장님 콘텐츠 필요)")
    print("  WaveData.wispRewards(라운드별 위습 보상)")
    print("  조합식 204개(재료→결과 등급)")
    print("  + 라운드별 킬 골드(이미 있음, 964 계산에 씀)와 유닛 가격표")
    print("  → '그 골드/위습을 유닛 확보에 즉시 쓰는지 저축하는지' 배분 비율만 정해지면")
    print("    라운드별 실제 등급 도달 시점이 창작 없이 계산으로 나온다 — 그 배분 비율은")
    print("    사장님께 여쭐 자리다.")

    # -----------------------------------------------------------------
    # §⑤(2026-09-06) — A안(제한) 붕괴 스윕. `war3map.j` 확정(`udg_Tech_Onedill`,
    # WISP_BUDGET_TIER_CEILING_2026-09-06.md §④)대로 영원함(T6)·초월함(T9)·
    # 불멸(T11)을 "플레이어당 공유 1기"로 캡을 씌운 MODELS_DT_CAPPED로 §15와
    # 똑같은 12개 조합(칸수4×모양3)을 다시 돈다. B안(현재/무제한 취급)은 §15
    # 결과가 그대로 그 값이라 다시 안 돌린다(PM 지시 — B안 폐기, A안만).
    # -----------------------------------------------------------------
    print("\n=== §⑤: A안(영원함·초월함·불멸 = 플레이어당 공유 1기) 붕괴 스윕 ===")
    print("⚠️ B안(무제한 취급, §15의 값)은 폐기 — Tech_Onedill이 '제한'으로 확정됐다.")

    all_results_capped = {name: [] for name in MODELS_DT_CAPPED}
    print(f"\n--- 고정 10기/레인 상세 그리드(A안) ---")
    print(f"{'칸수':6s}{'모양':22s}{'연구0':>10s}{'연구max':>10s}")
    fixed_fn_capped = MODELS_DT_CAPPED["고정 10기/레인"]
    for sc in schedule_counts:
        for shape, shape_label in shapes:
            tier_for_round = lambda r, total_rounds=75, _sc=sc, _sh=shape: tier_for_round_param(r, _sc, _sh, total_rounds)
            r0 = sweep_run(fixed_fn_capped, after_by_armor)
            rmax = sweep_run(fixed_fn_capped, after_by_armor_max)
            all_results_capped["고정 10기/레인"].append(r0)
            all_results_capped["고정 10기/레인"].append(rmax)
            r0_s = f"R{r0}" if r0 else "완주"
            rmax_s = f"R{rmax}" if rmax else "완주"
            print(f"{sc:<6d}{shape_label:22s}{r0_s:>10s}{rmax_s:>10s}")

    print("\n--- 나머지 3모델(A안) — 같은 12개 조합을 돌려 범위만 요약 ---")
    for name, fn in MODELS_DT_CAPPED.items():
        if name == "고정 10기/레인":
            continue
        for sc in schedule_counts:
            for shape, _ in shapes:
                tier_for_round = lambda r, total_rounds=75, _sc=sc, _sh=shape: tier_for_round_param(r, _sc, _sh, total_rounds)
                all_results_capped[name].append(sweep_run(fn, after_by_armor))
                all_results_capped[name].append(sweep_run(fn, after_by_armor_max))

    tier_for_round = original_tier_for_round  # 반드시 원상복구

    print(f"\n{'모델':20s}{'A안 범위':30s}{'B안 범위(§15, 참고)':30s}")
    for name in MODELS_DT_CAPPED:
        collapses_a = [c for c in all_results_capped[name] if c is not None]
        completed_a = len(all_results_capped[name]) - len(collapses_a)
        rng_a = (f"R{min(collapses_a)}~R{max(collapses_a)}({completed_a}완주)"
                 if collapses_a else "전부 완주")
        collapses_b = [c for c in all_results[name] if c is not None]
        completed_b = len(all_results[name]) - len(collapses_b)
        rng_b = (f"R{min(collapses_b)}~R{max(collapses_b)}({completed_b}완주)"
                 if collapses_b else "전부 완주")
        print(f"{name:20s}{rng_a:30s}{rng_b:30s}")


if __name__ == "__main__":
    main()
