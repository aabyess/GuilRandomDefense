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
        'moveSpeed':    8.0,   # 맵이 420 규모라 3.5로는 한 섬 건너는 데만 한참 걸린다
    }

# ⚠️ `damageType`은 **여기서 안 건드린다.** 예전엔 이 표에 `'damageType': 1`이 있어서
# 로스터를 생성할 때마다 239종이 전부 AD로 덮였다. 조합표가 `(AP)`라고 적어둔 유닛
# 40종과 `(AD+AP)` 9종까지 물리로 되돌아갔고, **파일명이 `_AP`인데 값은 AD**인 모순이
# 남았다(그 모순 덕에 뒤늦게 찾았다).
#
# damageType의 출처는 등급 곡선이 아니라 **조합표**다 — `Tools/fill_unit_damage_type.py`가
# `RECIPES.md`의 「타입」 열과 파일명 접미사에서 채운다. 이 생성기가 그걸 이기면 안 된다.

changed = 0
for path in sorted(glob.glob('Assets/Data/Units/Roster/*.asset')):
    text = open(path, encoding='utf-8').read()
    grade = int(re.search(r'^  grade: (\d+)', text, re.M).group(1))
    # 초월위습(13)은 싸우는 유닛이 아니라 조합 재료다. 전투 곡선을 씌우면 초월급 스탯의
    # 유닛이 되어버린다 — 손으로 정한 값(체력 1, 공격 없음, 이동만)을 그대로 둔다.
    if grade in (13, 14): continue   # 13 초월위습, 14 변화됨 — 둘 다 전투 곡선 대상이 아니다
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
