#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""PM 지시(2026-09-06) — GATE_FIX 14+6(20건) 적용 후 "우리" 쪽 발동률(방식 A/B)을
Docs/reference/PROC_RATE_METHOD.md의 계산 순서(⑥) 그대로 재측정한다. 원작 쪽 A는
그 문서에 이미 확정된 상수(전설 0.1443·제한 0.1324·초월 0.0919·영원 0.0354·불멸
0.0253)라 다시 안 잰다 — 우리 쪽만 다시 낸다.

⚠️ PM이 새로 지적한 보정 — UnitAttacker.cs의 절대쿨은 triggerType==0(OnHitChance)
에만 걸리고(OnHitCount 분기엔 없다, 코드 직접 확인), 걸리면
    effective_rate/sec = min(triggerChance × 초당타수, 1/cooldown)
이라 "평타당 확률"로 환산하면
    f_effective = min(triggerChance, ua1c/cooldown)   (ua1c = 1/초당타수 = 평타 간격)
문서 ⑥의 "우리" 식엔 이 보정이 없었다 — 여기서 추가한다(triggerType==3엔 코드상
쿨다운 게이트가 없어 안 건드림).

읽기 전용 — 데이터를 안 고친다.
"""
import re
import statistics
import sys
from pathlib import Path

sys.path.insert(0, 'Tools')
import check_assignment_invariants as cai

ROOT = cai.ROOT
GRADE_NAMES = {5: '전설적인', 6: '제한됨', 7: '초월함', 8: '불멸의', 9: '영원한'}

# 원작 쪽 A(고정 상수, PROC_RATE_METHOD.md 확정치 — 이번에 다시 안 잰다)
ORIGINAL_A = {'전설적인': 0.1443, '제한됨': 0.1324, '초월함': 0.0919, '영원한': 0.0354, '불멸의': 0.0253}
# 직전 측정 A비(우리/원작, GATE_FIX_14 적용 직후 값 — PROC_RATE_METHOD.md②)
LAST_RATIO = {'전설적인': 1.88, '제한됨': 3.93, '초월함': 0.33, '영원한': 5.50, '불멸의': 5.13}


def level0_block(text):
    """levels[0] 블록 텍스트(다음 레벨 시작 전까지)를 돌려준다."""
    m = re.search(r'  levels:\n', text)
    if not m:
        return None
    rest = text[m.end():]
    level_starts = [mm.start() for mm in re.finditer(r'^  - cooldown: ', rest, re.M)]
    level0_end = level_starts[1] if len(level_starts) > 1 else len(rest)
    return rest[:level0_end]


def parse_skill_file(text):
    tt_m = re.search(r'^  triggerType: (\d+)', text, re.M)
    trigger_type = int(tt_m.group(1)) if tt_m else 0
    level0 = level0_block(text)
    if level0 is None:
        return None

    tc_m = re.search(r'triggerChance: ([-+0-9.eE]+)', level0)
    trigger_chance = float(tc_m.group(1)) if tc_m else 1.0
    cd_m = re.search(r'^    cooldown: ([-+0-9.eE]+)', level0, re.M)
    cooldown = float(cd_m.group(1)) if cd_m else 0.0
    th_m = re.search(r'hitCountThreshold: (\d+)', level0)
    threshold = int(th_m.group(1)) if th_m else 0
    rt_m = re.search(r'resetTo: (\d+)', level0)
    reset_to = int(rt_m.group(1)) if rt_m else 0

    if trigger_type == 3:
        denom = threshold - reset_to
        f = trigger_chance / denom if denom > 0 else 0.0
    else:
        f = trigger_chance

    raw = 0.0
    for em in re.finditer(r'^    - kind: \d+\n((?:      .*\n)+)', level0, re.M):
        block = em.group(1)
        mult_m = re.search(r'multiplier: ([-+0-9.eE]+)', block)
        bonus_m = re.search(r'bonus: ([-+0-9.eE]+)', block)
        mult = float(mult_m.group(1)) if mult_m else 0.0
        bonus = float(bonus_m.group(1)) if bonus_m else 0.0
        raw += mult + bonus

    return dict(trigger_type=trigger_type, f=f, cooldown=cooldown, raw=raw)


def main():
    guid_to_path = cai.build_guid_index()
    roster_assets = cai.glob("Assets/Data/Units/Roster/*.asset")

    by_grade = {g: [] for g in GRADE_NAMES.values()}
    for rp in roster_assets:
        text = cai.read(rp)
        gm = re.search(r'^  grade: (\d+)', text, re.M)
        if not gm or int(gm.group(1)) not in GRADE_NAMES:
            continue
        grade = GRADE_NAMES[int(gm.group(1))]

        ap_m = re.search(r'^  attackPower: ([\d.]+)', text, re.M)
        as_m = re.search(r'^  attackSpeed: ([\d.]+)', text, re.M)
        if not ap_m or not as_m:
            continue
        attack_speed = float(as_m.group(1))
        if attack_speed <= 0:
            continue
        ua1c = 1.0 / attack_speed  # 평타 간격(초)

        guids = cai.resolve_skill_guids(text)
        if not guids:
            continue

        raw_total = 0.0
        dps_total = 0.0
        b_total = 0.0
        n_skills = 0
        for g in guids:
            sp = guid_to_path.get(g)
            if not sp:
                continue
            stext = cai.read(Path(sp))
            parsed = parse_skill_file(stext)
            if parsed is None or parsed['raw'] == 0.0:
                continue
            n_skills += 1
            f = parsed['f']
            # ⚠️ PM 지적 보정 — OnHitChance + cooldown>0이면 평타당 확률을 절대쿨로 clamp.
            if parsed['trigger_type'] == 0 and parsed['cooldown'] > 0:
                f = min(f, ua1c / parsed['cooldown'])
            raw_total += parsed['raw']
            dps_total += parsed['raw'] * f * (1.0 / ua1c)
            b_total += f * (1.0 / ua1c)

        if raw_total == 0.0:
            continue

        a_unit = dps_total / raw_total
        by_grade[grade].append(dict(base=rp.stem, a=a_unit, b=b_total, n_skills=n_skills))

    print(f"{'등급':6s}{'n':>4s}{'A(우리, 재측정)':>16s}{'A비(재측정)':>12s}"
          f"{'A비(직전)':>10s}{'변화':>8s}{'B(우리)':>12s}")
    rows_for_doc = []
    for grade in GRADE_NAMES.values():
        units = by_grade[grade]
        if not units:
            print(f"{grade:6s}{'0':>4s}  (유닛 없음)")
            continue
        a_med = statistics.median(u['a'] for u in units)
        b_med = statistics.median(u['b'] for u in units)
        ratio = a_med / ORIGINAL_A[grade]
        last = LAST_RATIO[grade]
        delta_pct = (ratio - last) / last * 100
        print(f"{grade:6s}{len(units):>4d}{a_med:>16.4f}{ratio:>12.2f}{last:>10.2f}"
              f"{delta_pct:>+7.1f}%{b_med:>12.4f}")
        rows_for_doc.append((grade, len(units), a_med, ratio, last, delta_pct, b_med))

    return rows_for_doc


if __name__ == '__main__':
    main()
