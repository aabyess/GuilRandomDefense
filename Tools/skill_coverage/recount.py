"""「시작 → 지금」 재집계 표 — 시작 시점 CSV(expected/coverage_start_2026-09-30.csv, 반영 405)와 지금 CSV를 같은 식으로 센다.
항목 수 = 「우리_반영」·「빠짐」 칸을 ' | '로 나눈 개수. 「있나 없나」만 세는 집계다(값·블록 정확성은 등급별 FILL_LIST).
사용(저장소 루트에서): python3 Tools/skill_coverage/recount.py  → 마크다운 표
"""
import collections
import csv
import re

GO = ['영원한', '불멸의', '초월함', '제한됨', '랜덤전용', '히든', '변화된', '특수함', '전설적인', '희귀함', '특별함', '안흔함', '흔함']
n = lambda s: len([x for x in s.split(' | ') if x.strip()])
g0 = lambda r: re.sub(r'\[.*', '', r['원작등급'])


def load(p):
    return list(csv.DictReader(open(p, encoding='utf-8-sig')))


def main():
    old, new = load('Tools/skill_coverage/expected/coverage_start_2026-09-30.csv'), load('Docs/research/SKILL_COVERAGE_BY_GRADE.csv')
    T = collections.defaultdict(lambda: [0] * 7)
    for rows, k in ((old, 0), (new, 1)):
        for r in rows:
            t = T[g0(r)]
            if k:
                t[0] += 1
                t[1] += bool(r['우리로스터'])
                t[2] += int(r['원작항목수'] or 0)
            t[3 + k] += n(r['우리_반영'])
            t[5 + k] += n(r['빠짐'])
    print('| 원작 등급 | 유닛(대응 있음) | 원작 항목 | 반영: 시작 → 지금 | 빠짐: 시작 → 지금 |')
    print('|---|---|---|---|---|')
    S = [0] * 7
    for g in GO:
        t = T[g]
        S = [a + b for a, b in zip(S, t)]
        print('| %s | %d(%d) | %d | %d → %d | %d → %d |' % (g, t[0], t[1], t[2], t[3], t[4], t[5], t[6]))
    print('| **합계** | %d | %d | **%d → %d** | **%d → %d** |' % (S[0], S[2], S[3], S[4], S[5], S[6]))


if __name__ == '__main__':
    main()
