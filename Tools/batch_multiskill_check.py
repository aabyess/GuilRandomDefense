#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""PM 지시(2026-09-06) — 지수표기 정규식 버그(7e1f63a) 수정 뒤 simulate_multiskill_gates.py
"전수 통과"를 다시 돌린다. 이전 통과들(9834a31 등)은 전부 이 버그가 있는 상태로 돈 것이라
무효일 수 있다 — 950%짜리가 섞여 있으면 그 게이지 그룹은 무조건 "정상"으로 보였을 것이다.

multi-skill(skills 2개 이상) 로스터 전체에 대해 --quiet로 재실행해 RESULT 줄만 모은다.
읽기 전용 — 데이터를 안 고친다.
"""
import glob
import re
import subprocess
import sys

ROOT = 'Assets/Data/Units/Roster'


def resolve_skill_count(text):
    skill_m = re.search(r'^  skill: \{fileID: (\d+)', text, re.M)
    has_skill = skill_m is not None and skill_m.group(1) != '0'
    skills_m = re.search(r'^  skills:\n((?:  - .*\n)*)', text, re.M)
    n_skills = len(re.findall(r'^  - ', skills_m.group(1), re.M)) if skills_m else 0
    if n_skills > 0:
        return n_skills
    return 1 if has_skill else 0


def main():
    targets = []
    for rp in sorted(glob.glob(f'{ROOT}/*.asset')):
        text = open(rp, encoding='utf-8').read()
        n = resolve_skill_count(text)
        if n >= 2:
            targets.append(rp)

    print(f'multi-skill(2개 이상) 로스터 {len(targets)}종 재검증')

    results = []
    for rp in targets:
        out = subprocess.run(
            [sys.executable, 'Tools/simulate_multiskill_gates.py', rp, '--quiet'],
            capture_output=True, text=True,
        )
        line = None
        for l in out.stdout.splitlines():
            if l.startswith('RESULT '):
                line = l
                break
        if line is None:
            results.append((rp, 'NO_RESULT', out.stdout + out.stderr))
            continue
        results.append((rp, line, None))

    ok = [r for r in results if r[1] not in ('NO_RESULT',) and 'status=OK' in r[1]]
    anomaly = [r for r in results if 'status=ANOMALY' in (r[1] or '')]
    other = [r for r in results if r not in ok and r not in anomaly]

    print(f'OK: {len(ok)} / ANOMALY: {len(anomaly)} / 기타(EMPTY 등): {len(other)}')
    if anomaly:
        print('\n=== 이상 발견 ===')
        for rp, line, _ in anomaly:
            print(rp)
            print(' ', line)
            # 상세 로그를 한 번 더 non-quiet로 뽑아 보여준다
            detail = subprocess.run(
                [sys.executable, 'Tools/simulate_multiskill_gates.py', rp],
                capture_output=True, text=True,
            ).stdout
            for l in detail.splitlines():
                if 'RESULT' in l or '⚠' in l:
                    print('   ', l)
    if other:
        print('\n=== 기타(EMPTY/NO_RESULT) ===')
        for rp, line, extra in other:
            print(rp, line, extra)


if __name__ == '__main__':
    main()
