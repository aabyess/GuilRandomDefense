#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""PM 지시(2026-09-06) — 초월함 스킬 기여가 전설적인의 1/3인 원인 추적.
읽기 전용. 초월함 로스터 25종 각각의 스킬(들)을 열어:
  - 참조한 원작 uid
  - levels[0].effects의 multiplier+bonus 합
  - 0/빈 효과 건수
  - "축 밖"/근사 꼬리표 건수
를 뽑아 Docs/reference로 남긴다.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, 'Tools')
import check_assignment_invariants as cai

ROOT = cai.ROOT
APPROX_MARKERS = [
    '축 밖', '근사', '추정', '미확인', '스톡 필드 값 그대로', '피해 0',
    '가장 낮은 발동률', '보수적으로',
]


def first_level_effects(text):
    """levels[0]의 effects 블록들만(두 번째 레벨이 있으면 거기서 끊는다)."""
    m = re.search(r'  levels:\n', text)
    if not m:
        return []
    rest = text[m.end():]
    level_starts = [mm.start() for mm in re.finditer(r'^  - cooldown: ', rest, re.M)]
    level0_end = level_starts[1] if len(level_starts) > 1 else len(rest)
    level0_text = rest[:level0_end]
    effects = []
    for em in re.finditer(r'^    - kind: \d+\n((?:      .*\n)+)', level0_text, re.M):
        block = em.group(1)
        mult_m = re.search(r'multiplier: ([-\d.]+)', block)
        bonus_m = re.search(r'bonus: ([-\d.]+)', block)
        mult = float(mult_m.group(1)) if mult_m else 0.0
        bonus = float(bonus_m.group(1)) if bonus_m else 0.0
        effects.append((mult, bonus))
    return effects


def extract_uids(desc):
    return sorted(set(m.upper() for m in re.findall(r'\(([Hh]0[0-9A-Za-z]{2})\)', desc)))


def main():
    roster_assets = cai.glob("Assets/Data/Units/Roster/*.asset")
    guid_to_path = cai.build_guid_index()

    rows = []
    for rp in roster_assets:
        text = cai.read(rp)
        gm = re.search(r'^  grade: (\d+)', text, re.M)
        if not gm or gm.group(1) != '7':
            continue
        base = rp.stem
        guids = cai.resolve_skill_guids(text)
        unit_total = 0.0
        unit_zero_effects = 0
        unit_total_effects = 0
        unit_approx = 0
        uids = set()
        skill_files = []
        for g in guids:
            sp = guid_to_path.get(g)
            if not sp:
                continue
            stext = cai.read(Path(sp))
            desc_m = re.search(r'^  description: (.*)$', stext, re.M)
            desc = desc_m.group(1) if desc_m else ''
            uids |= set(extract_uids(desc))
            effs = first_level_effects(stext)
            unit_total_effects += len(effs)
            for mult, bonus in effs:
                unit_total += mult + bonus
                if mult == 0.0 and bonus == 0.0:
                    unit_zero_effects += 1
            for marker in APPROX_MARKERS:
                if marker in desc:
                    unit_approx += 1
                    break
            skill_files.append(Path(sp).name)
        rows.append(dict(base=base, uids=sorted(uids), total=unit_total,
                          zero_effects=unit_zero_effects, total_effects=unit_total_effects,
                          approx=unit_approx, skill_files=skill_files, has_skill=bool(guids)))

    rows.sort(key=lambda r: -r['total'])

    out = ROOT / 'Docs/reference/TRANSCENDENT_DAMAGE_INVESTIGATION_2026-09-06.md'
    lines = []
    lines.append('# 초월함 스킬 기여 추적 (2026-09-06, 구현담당1)\n')
    lines.append('PM 지시 — 초월함 시뮬 스킬 기여가 전설적인의 1/3인 원인. 읽기 전용 조사.\n')
    lines.append(f'\n총 초월함 로스터 {len(rows)}종, 스킬 있음 {sum(1 for r in rows if r["has_skill"])}종\n')
    lines.append('\n| 로스터 | 원작 uid | 합계(mult+bonus) | 0/빈 효과 | 전체 효과 | 근사 태그 있는 파일 수 | 스킬 파일 |\n')
    lines.append('|---|---|---:|---:|---:|---:|---|\n')
    for r in rows:
        lines.append(
            f"| {r['base']} | {', '.join(r['uids']) or '-'} | {r['total']:.1f} | "
            f"{r['zero_effects']} | {r['total_effects']} | {r['approx']} | "
            f"{', '.join(r['skill_files']) or '-'} |\n"
        )

    total_zero = sum(r['zero_effects'] for r in rows)
    total_effects = sum(r['total_effects'] for r in rows)
    total_approx_units = sum(1 for r in rows if r['approx'] > 0)
    lines.append(f"\n## 요약\n")
    lines.append(f"- 0/빈 효과: 전체 {total_effects}개 효과 중 {total_zero}개\n")
    lines.append(f"- 근사 꼬리표가 붙은 스킬을 가진 유닛: {total_approx_units}/{len(rows)}종\n")
    lines.append(f"- 스킬 없는 유닛: {sum(1 for r in rows if not r['has_skill'])}종\n")

    out.write_text(''.join(lines), encoding='utf-8')
    print(f'{out} 작성 완료')
    print(f'0/빈 효과: {total_zero}/{total_effects}, 근사 태그 있는 유닛: {total_approx_units}/{len(rows)}')


if __name__ == '__main__':
    main()
