#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""⑤-2 파트 A(PM 지시, 2026-09-06): check_assignment_invariants ①의 8건 중
06번①이 게이트/회수를 이기는 5건 — 06번①(사장님 확정 26명 특성)이 항상 승자
(CHANNEL_UNIFICATION_DESIGN 원칙 1). 패자(게이트/회수)의 스킬을 승자 로스터의
skills로 옮긴다. 이 5건은 게이트 파일이 다른 채널과 안 얽혀 있어(단일 목적
파일) 단순 참조 이동으로 끝난다 — entangled 파일 수술이 필요한 나머지 3건
(2채널 vs 게이트/회수)은 별도 스크립트.

파일명 리네임은 여기서 안 한다 — 원칙 7대로 별도 커밋(다음 스크립트).
"""
import re
import sys

sys.path.insert(0, 'Tools')
import generate_unit_skills_by_gate as gen_gate

ROSTER_DIR = 'Assets/Data/Units/Roster'
UNIT_SKILLS_DIR = 'Assets/Data/UnitSkills'

# (원작 키, 승자 base, 패자 base, [패자 파일의 guid들])
CASES = [
    ('G.O.D', '초월_조성진_AD', '초월_김건_AP',
     ['e8a6a7d07a8822719ed09949df64884c', '80b19512c56e0d2781726ce3e4de611f']),
    ('CP.Zero', '초월_임장혁_AD', '초월_노태현_AP',
     ['d9a138308d25036229ce09052dcc9064']),
    ('무적의 아이언 파이러츠', '초월_박기찬_AD', '초월_박민석_ADAP',
     ['54418a4764142eddb47de816f4715ad3', 'b3ea98873f4ea6c4f787d399340111f0']),
    ('지진과 어둠의 검은수염', '초월_임채민_AP', '초월_엄태웅_AD',
     ['80e30c07c24a88e0f065a0017ed77a28', '6d36c081c8ad68bca247685057670b91']),
    ('"어둠의 조커"드레스로자의 악몽', '초월_최상호_AP', '초월_이재윤_AD',
     ['55075be6a8d950d4b1654fc7cb9dd037', 'f139cc7b42a126cf8dc48b15369f699e',
      '53ae542edf555fdcf9a4eb38716a6321']),
]


def remove_guid_from_skills(roster_text, guid):
    new_text = re.sub(rf'^  - \{{fileID: 11400000, guid: {guid}, type: 2\}}\n', '',
                       roster_text, count=1, flags=re.M)
    if new_text == roster_text:
        raise SystemExit(f'guid {guid}를 skills에서 못 찾음')
    return new_text


def main():
    moved = 0
    for key, winner_base, loser_base, guids in CASES:
        winner_path = f'{ROSTER_DIR}/{winner_base}.asset'
        loser_path = f'{ROSTER_DIR}/{loser_base}.asset'
        winner_text = open(winner_path, encoding='utf-8').read()
        loser_text = open(loser_path, encoding='utf-8').read()

        for guid in guids:
            loser_text = remove_guid_from_skills(loser_text, guid)
            if f'guid: {guid},' in winner_text:
                continue  # 이미 반영됨 — 재실행 멱등성
            winner_text = gen_gate.add_to_skills_list(winner_text, guid)
            moved += 1

        open(winner_path, 'w', encoding='utf-8').write(winner_text)
        open(loser_path, 'w', encoding='utf-8').write(loser_text)
        print(f'[{key}] {loser_base} -> {winner_base}: guid {len(guids)}개 이동')

    print(f'총 {moved}개 guid 이동 완료')


if __name__ == '__main__':
    main()
