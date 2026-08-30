#!/usr/bin/env python3
"""로스터 234종의 스탯을 등급 곡선으로 채운다.

전부 0이라 유닛이 공격도 이동도 못 하는 상태였다. 실제 밸런스 수치는
사용자가 나중에 제공하므로, 여기서는 '등급이 오르면 확실히 세진다'만
보장하는 임시 곡선을 넣는다. 등급 내 개체차는 두지 않았다 — 가짜 정밀도라
조합 전후 차이를 읽기만 어려워진다.

UnitGradeExtensions.Tier()와 같은 등급→티어 대응을 쓴다.
"""
import re, glob, os

TIER = {0:0, 1:1, 2:2, 3:3, 4:3, 12:4, 5:5, 6:6, 7:7, 8:7, 9:7, 11:7, 10:3}
#      흔함 안흔함 특별함 희귀함 히든 특수함 전설 제한 초월 불멸 영원 다른세계 랜덤(중간값)

def stats(tier):
    return {
        'hp':           round(100 * 1.8 ** tier, 1),
        'attackPower':  round(5 * 2.2 ** tier, 1),
        'attackRange':  round(6 + 0.5 * tier, 1),
        'attackSpeed':  round(1.0 + 0.08 * tier, 2),
        'moveSpeed':    3.5,
        'damageType':   1,   # AD — 실제 타입은 콘텐츠 확정 후 교체
    }

changed = 0
for path in sorted(glob.glob('Assets/Data/Units/Roster/*.asset')):
    text = open(path, encoding='utf-8').read()
    grade = int(re.search(r'^  grade: (\d+)', text, re.M).group(1))
    values = stats(TIER[grade])
    for key, value in values.items():
        text = re.sub(rf'^  {key}: .*$', f'  {key}: {value}', text, count=1, flags=re.M)
    open(path, 'w', encoding='utf-8').write(text)
    changed += 1

print(f"{changed}개 유닛 스탯 갱신\n")
seen = set()
for grade in [0,1,2,3,4,12,5,6,7]:
    t = TIER[grade]
    if t in seen: continue
    seen.add(t)
    s = stats(t)
    print(f"  티어{t}: hp={s['hp']:>8} 공격력={s['attackPower']:>7} 사거리={s['attackRange']:>4} 공속={s['attackSpeed']}")
