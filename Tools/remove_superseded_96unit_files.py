#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""96유닛 배정(ORIGINAL_UNLISTED_SKILL_EFFECTS.csv, 2a8cb51) 옛 판본 19개를 걷어낸다.

PM 확인(2026-09-06): 이 19개는 게이트별 재배정(f6fa527)이 "엄격한 부분집합"으로
대체했다 — 값 단위로 재확인(uid 19개 전부, 효과 전부 새 판본에 그대로 있음, 새 판본
쪽 identifier가 "게이트 정보 없이"라는 형식 차이만 있고 값 손실 없음) 완료. 지금
같은 원작 스킬이 두 벌(옛 96유닛 파일 + 새 게이트별 파일) 나가고 있어 화력이
과다 계상되고 있었다 — 어제 2채널 중복(32유닛→77슬롯)과 같은 종류의 사고, 이번엔
같은 작업의 신/구 판본 사이.

## 순서(PM 지시)
1. 로스터 참조를 먼저 끊는다(skill/skills에서 guid 제거) — 순서가 반대면 참조가
   깨진다(스킬 파일은 없는데 로스터가 guid를 들고 있는 상태).
2. 그 다음 에셋(.asset + .meta) 삭제.
3. uid 커버리지가 안 줄었는지 검산(104개 유지 — 새 판본이 이미 다 갖고 있으므로).

⚠️ 이 19개 파일은 96유닛 콘텐츠만 담고 있다(1채널/2채널 등 다른 채널이 같은
파일에 stack 안 됨 — 사전 확인 완료, blocks 수만 다르고 "1채널 추가 배정"/
"2차 배정" 마커 없음) — 그래서 파일 전체를 안전하게 지울 수 있다(부분 삭제 불필요).
"""
import glob
import os
import re

FILES = sorted(glob.glob('Assets/Data/UnitSkills/*.asset'))
ROSTER_DIR = 'Assets/Data/Units/Roster'
MARKER = '96유닛 배정(ORIGINAL_UNLISTED_SKILL_EFFECTS.csv)'


def remove_guid_from_roster(roster_text, guid):
    if re.search(rf'^  skill: \{{fileID: 11400000, guid: {guid}[^\n]*\}}$', roster_text, re.M):
        return re.sub(
            rf'^  skill: \{{fileID: 11400000, guid: {guid}[^\n]*\}}$',
            '  skill: {fileID: 0}', roster_text, count=1, flags=re.M)
    new_text = re.sub(rf'^  - \{{fileID: 11400000, guid: {guid}, type: 2\}}\n', '',
                       roster_text, count=1, flags=re.M)
    if new_text == roster_text:
        return None  # 이 로스터엔 없음
    return new_text


def main():
    targets = []
    for f in FILES:
        text = open(f, encoding='utf-8').read()
        desc_m = re.search(r'^  description: (.*)$', text, re.M)
        if desc_m and desc_m.group(1).startswith(MARKER):
            targets.append(f)

    print(f'대상 파일 {len(targets)}개')

    removed_refs = 0
    deleted_files = 0
    for f in targets:
        meta_text = open(f + '.meta', encoding='utf-8').read()
        guid = re.search(r'guid: (\w+)', meta_text).group(1)

        touched_rosters = []
        for rp in glob.glob(f'{ROSTER_DIR}/*.asset'):
            rtext = open(rp, encoding='utf-8').read()
            if f'guid: {guid}' not in rtext:
                continue
            new_text = remove_guid_from_roster(rtext, guid)
            if new_text is None:
                continue
            open(rp, 'w', encoding='utf-8').write(new_text)
            touched_rosters.append(rp)
            removed_refs += 1

        if not touched_rosters:
            print(f'⚠️ {f} (guid {guid}) — 참조하는 로스터를 못 찾음, 그래도 파일은 지운다')

        os.remove(f)
        os.remove(f + '.meta')
        deleted_files += 1

    print(f'로스터 참조 제거 {removed_refs}건, 파일 삭제 {deleted_files}개')


if __name__ == '__main__':
    main()
