#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""PM 지시(2026-09-06) — "스킬은 붙어 있는데 levels[0].effects가 빈 유닛"을 감사한다.
06번① 특성 9개(수치 미상, 사장님 콘텐츠 배정 대기)와 같은 원인인지, 아니면 다른
이유(진짜 구멍)인지를 가른다. 읽기 전용.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, 'Tools')
import check_assignment_invariants as cai

ROOT = cai.ROOT
GRADE_NAMES = {0: '흔함', 1: '안흔함', 2: '특별함', 3: '희귀함', 4: '히든', 5: '전설적인',
               6: '제한됨', 7: '초월함', 8: '불멸의', 9: '영원한', 10: '랜덤전용',
               11: '다른세계', 12: '특수함', 14: '변화됨'}

TRAIT_STUB_MARKER = '수치 미상 — 두 레벨 모두 effects가 비어'


def level0_effect_count(text):
    m = re.search(r'  levels:\n', text)
    if not m:
        return 0
    rest = text[m.end():]
    level_starts = [mm.start() for mm in re.finditer(r'^  - cooldown: ', rest, re.M)]
    level0_end = level_starts[1] if len(level_starts) > 1 else len(rest)
    level0_text = rest[:level0_end]
    return len(re.findall(r'^    - kind: \d+\n', level0_text, re.M))


def main():
    guid_to_path = cai.build_guid_index()
    roster_assets = cai.glob("Assets/Data/Units/Roster/*.asset")

    rows = []
    for rp in roster_assets:
        text = cai.read(rp)
        gm = re.search(r'^  grade: (\d+)', text, re.M)
        if not gm:
            continue
        grade_num = int(gm.group(1))
        grade = GRADE_NAMES.get(grade_num)
        if grade not in ('전설적인', '초월함'):
            continue  # PM이 비교 중인 두 등급만(전체 로스터로 넓히면 65% 무배정 잡음이 묻힌다)

        guids = cai.resolve_skill_guids(text)

        if not guids:
            # 스킬 참조 자체가 아예 없다 — PM이 센 "효과 0개"엔 이 경우도 들어간다
            # (김건_AP·박민석_ADAP 등, ⑤-2에서 채널우선순위에 져 스킬 전체를 다른
            # 초월함 유닛으로 옮긴 자리). "특성 대기"와는 다른 카테고리(no_skill)다.
            rows.append(dict(base=rp.stem, grade=grade, files=[], category='no_skill'))
            continue

        total_effects = 0
        is_trait_stub_only = True
        file_names = []
        for g in guids:
            sp = guid_to_path.get(g)
            if not sp:
                continue
            stext = cai.read(Path(sp))
            n = level0_effect_count(stext)
            total_effects += n
            file_names.append(Path(sp).name)
            if n == 0 and TRAIT_STUB_MARKER not in stext:
                is_trait_stub_only = False

        if total_effects > 0:
            continue  # 정상 — 감사 대상 아님

        rows.append(dict(base=rp.stem, grade=grade, files=file_names,
                          category='trait_stub' if is_trait_stub_only else 'other'))

    by_grade = {}
    for r in rows:
        by_grade.setdefault(r['grade'], []).append(r)

    lines = []
    lines.append('# 효과 0개 유닛 감사 (2026-09-06, 구현담당1)\n')
    lines.append('PM 지시 — 스킬 기여가 0인 유닛(스킬 파일은 있는데 levels[0].effects가 비었거나,\n')
    lines.append('스킬 참조 자체가 없는 경우 둘 다 포함). 읽기 전용.\n\n')
    lines.append(f'전체 {len(rows)}종 발견.\n\n')

    trait_stub = [r for r in rows if r['category'] == 'trait_stub']
    no_skill = [r for r in rows if r['category'] == 'no_skill']
    other = [r for r in rows if r['category'] == 'other']

    lines.append(f"## ① 06번① 특성 스텁만 원인(사장님 콘텐츠 배정 대기, 손댈 자리 아님) — {len(trait_stub)}종\n\n")
    lines.append('| 등급 | 로스터 | 스킬 파일 |\n|---|---|---|\n')
    for r in sorted(trait_stub, key=lambda x: (x['grade'], x['base'])):
        lines.append(f"| {r['grade']} | {r['base']} | {', '.join(r['files'])} |\n")

    lines.append(f"\n## ② 스킬 참조 자체가 없음(claimed 아님) — {len(no_skill)}종\n\n")
    lines.append("⚠️ 이 중 초월함 일부(김건_AP·박민석_ADAP·엄태웅_AD·이재윤_AD)는 새로 뚫린 구멍이\n")
    lines.append("아니다 — `cb2b003`(⑤-2)에서 채널우선순위(06번①)에 져 스킬 전체를 같은 등급 안의\n")
    lines.append("다른 유닛에게 그대로 옮겼다(등급 합계엔 손실 없음, 재배치일 뿐). 나머지는 애초에\n")
    lines.append("아무 채널에서도 못 받은 유닛이다.\n\n")
    lines.append('| 등급 | 로스터 |\n|---|---|\n')
    for r in sorted(no_skill, key=lambda x: (x['grade'], x['base'])):
        lines.append(f"| {r['grade']} | {r['base']} |\n")

    lines.append(f"\n## 🔴 ③ 다른 이유로 빈 것 — 진짜 구멍 — {len(other)}종\n\n")
    lines.append('| 등급 | 로스터 | 스킬 파일 |\n|---|---|---|\n')
    for r in sorted(other, key=lambda x: (x['grade'], x['base'])):
        lines.append(f"| {r['grade']} | {r['base']} | {', '.join(r['files'])} |\n")

    lines.append('\n## 등급별 요약\n\n| 등급 | 전체 | ①특성 스텁 | ②스킬 없음 | ③진짜 구멍 |\n|---|---:|---:|---:|---:|\n')
    for grade in sorted(by_grade, key=lambda g: -len(by_grade[g])):
        rs = by_grade[grade]
        t = sum(1 for r in rs if r['category'] == 'trait_stub')
        n = sum(1 for r in rs if r['category'] == 'no_skill')
        o = sum(1 for r in rs if r['category'] == 'other')
        lines.append(f"| {grade} | {len(rs)} | {t} | {n} | {o} |\n")

    out = ROOT / 'Docs/reference/EMPTY_EFFECTS_AUDIT.md'
    out.write_text(''.join(lines), encoding='utf-8')
    print(f'{out} 작성 완료 — 전체 {len(rows)}종 (①특성스텁 {len(trait_stub)} / ②스킬없음 '
          f'{len(no_skill)} / ③진짜구멍 {len(other)})')


if __name__ == '__main__':
    main()
