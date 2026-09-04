#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""적 에셋에 `armorType` 한 줄을 끼워 넣는다. **원작 `war3map.w3u`의 분류 그대로다**
(근거: `Docs/reference/UNIT_STATS_RESEARCH.md` §②).

⚠️ `Tools/generate_*.py`와 달리 **파일을 다시 쓰지 않는다.** `prefab:` 줄 앞에 한 줄을
끼우기만 하고, 이미 `armorType`이 있으면 건드리지 않는다. 그래서 두 번 돌려도 안전하다.

원작 대응은 이름이 아니라 **칸**으로 선다 — 보스 라운드 9곳·스토리 13종의 순서·크립 이름이
원작과 같다. 체력 같은 수치는 우리 값이 따로라 넘어오지 않는다.
"""
import re, sys, pathlib

UNASSIGNED, NORMAL, LARGE, FORT, HERO = 0, 1, 2, 3, 4

BOSS_ROUNDS = {10, 20, 30, 40, 50, 60, 65, 70, 75}   # 원작 [보스] 11종이 전부 large
FORT_ROUNDS = {64, 67, 72}                            # 원작 레벨 64·67·72가 fort
HERO_ROUNDS = {68, 69, 74}                            # 원작 레벨 68·69·74가 hero
# 원작 레벨 66·71·73은 `udty`를 안 적어 베이스(ogru) 값을 상속한다 — 우리 파일로는 못 읽는다.
# 근거 없는 값을 86종의 진짜 근거에 섞지 않으려고 **비워 둔다.**
SKIP_ROUNDS = {66, 71, 73}


def armor_type_for(name):
    if name == 'Enemy_Seal':                       # 원작 「1단계 크립 물범」 — 이름까지 같다
        return NORMAL, '크립 (원작 1단계 크립 물범)'
    if name.startswith('Enemy_Story'):             # 원작 스토리 섬 13종이 전부 fort
        return FORT, '스토리 섬 (원작 13/13 fort)'
    m = re.match(r'^Enemy_R(\d\d)_', name)
    if not m:
        return None, '알 수 없는 이름 꼴'
    r = int(m.group(1))
    if r in SKIP_ROUNDS:
        return None, '원작이 방어 타입을 안 적었다 (베이스 상속) — 비워 둔다'
    if r in BOSS_ROUNDS:
        return LARGE, '보스 (원작 11/11 large)'
    if r in FORT_ROUNDS:
        return FORT, f'원작 레벨 {r} fort'
    if r in HERO_ROUNDS:
        return HERO, f'원작 레벨 {r} hero'
    if r <= 63:
        return NORMAL, '라인몹 (원작 레벨 1~63 55/55 normal)'
    return None, f'라운드 {r}에 대응하는 원작 근거가 없다'


def main():
    root = pathlib.Path('Assets/Data/Enemies')
    written = skipped = already = 0
    for path in sorted(root.glob('*.asset')):
        text = path.read_text(encoding='utf-8')
        if 'armorType:' in text:
            already += 1
            continue
        value, why = armor_type_for(path.stem)
        if value is None:
            print(f'  건너뜀  {path.stem:34} — {why}')
            skipped += 1
            continue
        if '\n  prefab:' not in text:
            print(f'  ⚠ 실패  {path.stem} — prefab 줄을 못 찾았다', file=sys.stderr)
            skipped += 1
            continue
        path.write_text(text.replace('\n  prefab:', f'\n  armorType: {value}\n  prefab:', 1),
                        encoding='utf-8')
        written += 1
    print(f'\n기록 {written}개 / 비움 {skipped}개 / 이미 있음 {already}개')


if __name__ == '__main__':
    main()
