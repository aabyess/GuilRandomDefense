#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""유닛 하나(로스터 에셋)가 가진 여러 스킬(SkillData.skills)이 실전에서 **각자 게이트로**
도는지 확인하는 드라이런 시뮬레이터. 읽기 전용 — 아무것도 고치지 않는다.

⚠️ 왜 필요한가: 지금까지 로스터의 모든 스킬 보유 유닛이 SkillCount==1이라
UnitAttacker.TryCastOnHitSkill의 "여러 스킬" 경로가 실전에서 한 번도 안 돌았다
(2026-09-05 밤 발견 — 그래서 공유 게이지 리셋 버그(4aade90)를 그때까지 못 잡고 있었다).
구현담당1이 원작 능력을 게이트 하나당 SkillData 하나로 다시 쪼개는 중이라(PM 지시,
2026-09-06) 유닛당 2~6개짜리 데이터가 곧 들어온다 — Unity 에디터를 열지 않고도 그
유닛의 실제 스킬 데이터를 즉석에서 검증할 방법이 필요했다.

이 스크립트는 **UnitAttacker.TryCastOnHitSkill/UpdateSkillCooldown과 같은 알고리즘**을
Python으로 그대로 재현한다(공유 게이지 지연 리셋 — 4aade90, OnHitCount 2차 확률
게이트 — e23cfd8 포함). C# 코드 자체를 실행하는 게 아니므로 **C# 쪽을 고치면 이
시뮬레이터도 같이 고쳐야 한다** — "값 계산 스크립트"이지 "테스트 러너"가 아니다.
그래서 스크립트 맨 위에 재현 대상 커밋을 적어둔다: 4aade90(지연 리셋) · e23cfd8
(2차 확률 게이트) 기준.

사용법:
  python3 Tools/simulate_multiskill_gates.py <로스터_에셋_경로_또는_이름_일부> [--hits N] [--attack-interval 초] [--seed N]

예:
  python3 Tools/simulate_multiskill_gates.py 황준석
  python3 Tools/simulate_multiskill_gates.py Assets/Data/Units/Roster/희귀함_황준석.asset --hits 500
"""
import argparse
import glob
import re
import random
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent


def read(path):
    return Path(path).read_text(encoding="utf-8")


# ── guid → SkillData 경로 (simulate_balance.py의 .meta 기반 guid 추출과 같은 패턴) ──
def build_guid_index():
    index = {}
    for pattern in ("Assets/Data/UnitSkills/*.asset.meta", "Assets/Data/EnemySkills/*.asset.meta"):
        for meta_path in glob.glob(str(ROOT / pattern)):
            guid_m = re.search(r"guid: ([0-9a-f]+)", read(meta_path))
            if guid_m:
                index[guid_m.group(1)] = meta_path[:-len(".meta")]
    return index


def find_roster_asset(query):
    direct = ROOT / query
    if direct.exists():
        return direct
    candidates = [p for p in glob.glob(str(ROOT / "Assets/Data/Units/Roster/*.asset")) if query in Path(p).name]
    if not candidates:
        print(f"'{query}'와 일치하는 로스터 에셋을 못 찾았습니다.", file=sys.stderr)
        sys.exit(2)
    if len(candidates) > 1:
        print(f"'{query}'가 여러 개와 일치합니다 — 더 구체적으로 지정하세요:", file=sys.stderr)
        for c in candidates:
            print("  ", Path(c).relative_to(ROOT), file=sys.stderr)
        sys.exit(2)
    return Path(candidates[0])


# ── UnitData.SkillCount/SkillAt과 같은 폴백: skills가 비어 있으면 skill 하나짜리 목록 ──
def resolve_skill_guids(roster_text):
    skill_m = re.search(r"^  skill: \{fileID: (\d+)(?:, guid: ([0-9a-f]+))?", roster_text, re.MULTILINE)
    has_skill = skill_m is not None and skill_m.group(1) != "0"
    skill_guid = skill_m.group(2) if has_skill else None

    skills_m = re.search(r"^  skills:(.*)$", roster_text, re.MULTILINE)
    guids = []
    if skills_m is not None:
        rest = skills_m.group(1).strip()
        tail = roster_text[skills_m.end():]
        block_lines = []
        if rest.startswith("- "):
            block_lines.append(rest)
        for line in tail.split("\n"):
            if line.startswith("  - "):
                block_lines.append(line[4:])
            elif line.startswith("  ") and not line.startswith("    ") and line.strip() != "":
                break
        for line in block_lines:
            g = re.search(r"guid: ([0-9a-f]+)", line)
            if g:
                guids.append(g.group(1))

    if guids:
        return guids  # skills가 하나라도 있으면 skill은 안 본다(UnitData.SkillCount 규칙)
    return [skill_guid] if skill_guid else []


# ── SkillData 에셋 파싱 — levels[0]만 쓴다(CurrentSkillLevel이 항상 index 0을 쓴다) ──
def parse_skill(path):
    text = read(path)
    trigger_m = re.search(r"^  triggerType: (\d+)", text, re.MULTILINE)
    trigger_type = int(trigger_m.group(1)) if trigger_m else 1  # 기본값 CooldownAutoCast

    level_bodies = re.split(r"\n  - cooldown: ", text)[1:]
    if not level_bodies:
        return {"triggerType": trigger_type, "hasEffects": False}
    level_body = level_bodies[0]

    def num(pattern, default, cast=float):
        m = re.search(pattern, level_body)
        return cast(m.group(1)) if m else default

    effects_match = re.search(r"    effects:(.*?)(?=\n  - cooldown: |\Z)", level_body, re.S)
    effects_body = effects_match.group(1) if effects_match else ""
    has_effects = re.search(r"^\s*- kind:", effects_body, re.MULTILINE) is not None

    cooldown_m = re.match(r"([\d.]+)", level_body)
    cooldown = float(cooldown_m.group(1)) if cooldown_m else 0.0

    return {
        "triggerType": trigger_type,
        "hasEffects": has_effects,
        "cooldown": cooldown,
        "triggerChance": num(r"\n {4}triggerChance: ([\d.]+)", 1.0),
        "hitCountThreshold": int(num(r"\n {4}hitCountThreshold: (-?\d+)", 0, int)),
        "resetTo": int(num(r"\n {4}resetTo: (-?\d+)", 0, int)),
        "gaugeKind": int(num(r"\n {4}gaugeKind: (\d+)", 0, int)),
    }


TRIGGER_NAMES = {0: "OnHitChance", 1: "CooldownAutoCast", 2: "Aura", 3: "OnHitCount"}
GAUGE_NAMES = {0: "Mana", 1: "Life"}


def simulate(unit_name, skills, hits, attack_interval, seed):
    rng = random.Random(seed)

    # 스킬별 런타임 상태(UnitAttacker.SkillRuntimeState와 대응) — 인스턴스별이라 여기선
    # 스킬 dict마다 독립 변수로 둔다.
    for s in skills:
        s["lockedUntil"] = 0.0
        s["fireCount"] = 0

    mana_counter = 0
    mana_initialized = False
    life_counter = 0
    life_initialized = False

    onhit_skills = [s for s in skills if s["triggerType"] in (0, 3) and s["hasEffects"]]
    other_skills = [s for s in skills if s["triggerType"] not in (0, 3)]

    print(f"=== {unit_name} — 스킬 {len(skills)}개 ===")
    for i, s in enumerate(skills):
        tname = TRIGGER_NAMES.get(s["triggerType"], "?")
        if s["triggerType"] in (0, 3) and not s["hasEffects"]:
            print(f"  [{i}] {s['path'].name} — {tname}, effects 비어있음(아직 안 돎)")
        elif s["triggerType"] == 3:
            print(f"  [{i}] {s['path'].name} — {tname}, gauge={GAUGE_NAMES.get(s['gaugeKind'],'?')}, "
                  f"threshold={s['hitCountThreshold']}, resetTo={s['resetTo']}, "
                  f"2차확률={s['triggerChance']}")
        elif s["triggerType"] == 0:
            print(f"  [{i}] {s['path'].name} — {tname}, 확률={s['triggerChance']}, 절대쿨={s['cooldown']}s")
        else:
            print(f"  [{i}] {s['path'].name} — {tname} (이 스크립트는 시간 기반 발동방식은 "
                  f"카운트만 하고 히트 시뮬레이션은 안 함)")
    print()

    # ⚠️ 같은 게이지·다른 임계값 조합은 원작에 0건으로 확인됐지만(리서치담당), 데이터
    # 사고로 생길 수 있으니 미리 경고한다.
    for kind, kind_name in ((0, "Mana"), (1, "Life")):
        thresholds = {s["hitCountThreshold"] for s in onhit_skills
                      if s["triggerType"] == 3 and s["gaugeKind"] == kind}
        if len(thresholds) > 1:
            print(f"  ⚠️  {kind_name} 게이지를 공유하는 스킬들의 hitCountThreshold가 서로 다릅니다: "
                  f"{sorted(thresholds)} — 원작에 이런 사례가 없다고 확인됐는데 지금 이 유닛엔 있습니다.")

    time = 0.0
    co_fire_log = []  # 한 타에 2개 이상 같이 나가면 기록

    for hit in range(1, hits + 1):
        time += attack_interval
        mana_incremented = False
        life_incremented = False
        mana_should_reset = False
        mana_reset_value = 0
        life_should_reset = False
        life_reset_value = 0
        fired_this_hit = []

        for s in onhit_skills:
            if s["triggerType"] == 0:
                if s["cooldown"] > 0.0 and time < s["lockedUntil"]:
                    continue
                if rng.random() >= s["triggerChance"]:
                    continue
                if s["cooldown"] > 0.0:
                    s["lockedUntil"] = time + s["cooldown"]
            else:
                if s["gaugeKind"] == 0:
                    if not mana_initialized:
                        mana_counter = s["resetTo"]
                        mana_initialized = True
                    if not mana_incremented:
                        mana_counter += 1
                        mana_incremented = True
                    if mana_counter < s["hitCountThreshold"]:
                        continue
                    mana_should_reset = True
                    mana_reset_value = s["resetTo"]
                else:
                    if not life_initialized:
                        life_counter = s["resetTo"]
                        life_initialized = True
                    if not life_incremented:
                        life_counter += 1
                        life_incremented = True
                    if life_counter < s["hitCountThreshold"]:
                        continue
                    life_should_reset = True
                    life_reset_value = s["resetTo"]

                # OnHitCount 2차 확률 게이트(e23cfd8) — 실패해도 리셋 예약은 살아있다.
                if rng.random() >= s["triggerChance"]:
                    continue

            s["fireCount"] += 1
            fired_this_hit.append(s["path"].name)

        if mana_should_reset:
            mana_counter = mana_reset_value
        if life_should_reset:
            life_counter = life_reset_value

        if len(fired_this_hit) >= 2:
            co_fire_log.append((hit, list(fired_this_hit)))
        if hit <= 30 and fired_this_hit:
            print(f"  hit {hit:>4} (t={time:6.1f}s): {', '.join(fired_this_hit)}")

    print()
    print("=== 요약 ===")
    for s in onhit_skills:
        rate = s["fireCount"] / hits * 100
        print(f"  {s['path'].name}: {s['fireCount']}/{hits}회 발동 ({rate:.1f}%)")
    if co_fire_log:
        print(f"\n  같은 타에 2개 이상 동시발동: {len(co_fire_log)}회 (처음 5개)")
        for hit, names in co_fire_log[:5]:
            print(f"    hit {hit}: {', '.join(names)}")
    else:
        print("\n  같은 타에 2개 이상 동시발동한 적 없음.")
    if other_skills:
        print(f"\n  시간기반(CooldownAutoCast/Aura) 스킬 {len(other_skills)}개는 히트 시뮬레이션 대상이 "
              f"아닙니다(별도로 시간 루프가 돕니다) — 트리거타입만 위에 표시.")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("unit", help="로스터 에셋 경로 또는 파일명에 포함된 문자열")
    parser.add_argument("--hits", type=int, default=1000, help="시뮬레이션할 평타 횟수(기본 1000)")
    parser.add_argument("--attack-interval", type=float, default=1.0, help="평타 간격 초(기본 1.0 — 절대쿨 계산용)")
    parser.add_argument("--seed", type=int, default=0, help="난수 시드(기본 0, 재현 가능하게)")
    args = parser.parse_args()

    roster_path = find_roster_asset(args.unit)
    roster_text = read(roster_path)
    guids = resolve_skill_guids(roster_text)
    if not guids:
        print(f"{roster_path.name}: 스킬이 하나도 배선돼 있지 않습니다(skill/skills 둘 다 비어있음).")
        return

    # ⚠️ 2026-09-06 실전에서 실제로 발견한 사고 꼴 — 같은 SkillData guid가 skills 리스트에
    # 두 번 이상 들어가면 그 스킬이 한 타에 두 번 판정된다(예: 확률 12.5% 스킬이 사실상
    # ~23%가 됨). 조용히 지나가면 사람이 못 알아채니 여기서 바로 소리친다.
    dup_guids = {g for g in guids if guids.count(g) > 1}
    if dup_guids:
        print(f"⚠️⚠️  {roster_path.name}: skills 리스트에 같은 SkillData가 중복 배선돼 있습니다 — "
              f"그 스킬은 한 타에 여러 번 판정됩니다(발동확률·게이지 증가가 왜곡됩니다).")
        for g in dup_guids:
            print(f"    guid {g} × {guids.count(g)}회")
        print()

    guid_index = build_guid_index()
    skills = []
    for g in guids:
        path = guid_index.get(g)
        if path is None:
            print(f"⚠️  guid {g}에 해당하는 SkillData 에셋을 못 찾았습니다 — 배선이 끊어져 있습니다.")
            continue
        parsed = parse_skill(path)
        parsed["path"] = Path(path)
        skills.append(parsed)

    simulate(roster_path.stem, skills, args.hits, args.attack_interval, args.seed)


if __name__ == "__main__":
    main()
