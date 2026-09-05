#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""③ 19행 회수 — generate_unit_skills_by_gate.py의 등급별 min(원작,가용) 자르기 때문에
경고 없이 통째로 빠졌던 13개 원작 유닛(불멸의5·초월함7·영원한1)을 되살린다.

PM 지시(2026-09-06): 「원작 유닛ID로 먼저 매칭 — 이미 어딘가에 배정된 원작
유닛(06번① 특성, 1·2채널 포함)이 96프로젝트에도 있으면, 순위 배정을 건너뛰고
그 로스터 유닛의 skills에 추가하세요.」

## 1단계 — 이미 배정된 9개 uid는 직접 라우팅
06번①(SkillData_원작0NN_*.asset) 또는 1·2채널(SkillData_원작능력_*.asset 설명에
uid가 언급됨) 어디선가 이미 살아있는 로스터 참조를 가진 uid들 — 같은 캐릭터의
다른 능력이니 같은 유닛에 쌓는다(정체성 혼선 아니라 오히려 일치, PM 확인).

## 2단계 — 남은 4개는 이번 런의 등급 안 매칭 결과(가용 슬롯 전부 소진)에
1:다로 붙이되, 게이지 충돌 검사를 반드시 거친다(PM 경고: 공유 카운터가 다른
임계값으로 리셋되면 먼저 찬 스킬이 영영 못 닿는다).
  - H09I(브룩, 1/7 순수확률×2, 게이지 없음) → 게이지 무관, 아무 데나 안전
  - H096(로우, 버프드롭+1/20, 게이지 없음) → 게이지 무관, 아무 데나 안전
  - H0B5(키자루, 1/17, 게이지 없음) → 게이지 무관, 아무 데나 안전
  - h04B(시키, 3개 서브게이트: MANA125 게이지 1개 + 순수확률 2개) → 게이지가
    있는 서브게이트만 대상 유닛의 기존 게이지와 종류·임계값이 맞아야 한다.
    불멸_신지우(Mana45)·불멸_김용태(Mana150)는 종류는 같은데 임계값이 달라
    충돌(공유 카운터가 45/150에서 먼저 리셋돼 125가 영영 안 닿는다) — 제외.
    불멸_고도현(Life115)은 게이지 "종류" 자체가 달라(Life vs Mana) 카운터가
    아예 분리돼 있어 충돌 없음 — 여기로 붙인다.

대상 선정(1:다 붙일 로스터 유닛)은 이번 런에서 이미 매칭된 유닛 중 skills
개수가 가장 적은 쪽을 우선했다(과부하 분산) — 이후 h04B의 게이지 제약으로
불멸의 쪽은 고도현으로 고정.
"""
import re
import sys
from collections import defaultdict

sys.path.insert(0, 'Tools')
import generate_unit_skills_by_gate as gen

DIRECT_ROUTE = {
    # 불멸의 4명 — 06번① 특성 유닛에 96프로젝트 나머지 능력을 합류
    'h04C': '불멸_이승우',
    'h049': '불멸_정윤식',
    'h04G': '불멸_박은석',
    'h04E': '불멸_이이삭',
    # 초월함 4명 — 1·2채널에서 이미 같은 캐릭터를 받은 유닛
    'H095': '초월_김만경_AD',
    'H09E': '초월_최상호_AP',
    'H0BL': '초월_구주호_AD',
    'H0BT': '초월_강주혁_AP',
    # 영원한 1명
    'h057': '영원_문필환',
}

# 2단계 — 게이지 무관 3명은 이번 런의 최소부하 유닛에, h04B는 게이지 호환
# 유닛(고도현, Life≠Mana라 충돌 없음)에 고정 배정.
OVERFLOW_ROUTE = {
    'H09I': '초월_노태현_AP',
    'H096': '초월_양재모_AD',
    'H0B5': '초월_구주호_AD',
    'h04B': '불멸_고도현',
}


def main():
    by_unit, _ = gen.load_confirmed_groups()
    roster = gen.load_roster()
    roster_by_base = {u['base']: u for u in roster}

    all_routes = {**DIRECT_ROUTE, **OVERFLOW_ROUTE}
    total_gates = 0
    total_rows = 0

    for uid, target_base in all_routes.items():
        rows = by_unit.get(uid)
        if not rows:
            print(f'⚠️ {uid}: 확정 행을 찾지 못했습니다 — 건너뜁니다')
            continue
        unit = roster_by_base.get(target_base)
        if unit is None:
            print(f'⚠️ {target_base}: 로스터에서 찾지 못했습니다 — 건너뜁니다')
            continue

        unit_name = rows[0]['유닛이름']
        by_gate = defaultdict(list)
        for r in rows:
            by_gate[r['gate']].append(r)

        roster_text = unit['text']
        gate_count = 0
        for gate_str, gate_rows in by_gate.items():
            try:
                gate_resolved, dropped = gen.resolve_gate(gate_str)
            except ValueError as e:
                print(f'⚠️ {uid} 게이트 파싱 실패: {gate_str!r} — {e}')
                continue

            _, range_note = gen.resolve_range(gate_rows)
            gate_hash = __import__('hashlib').md5(gate_str.encode()).hexdigest()[:8]
            name = f'SkillData_회수_{target_base}_{gate_hash}'

            guid = gen.build_skill_asset(name, gate_str, gate_resolved, dropped, gate_rows,
                                          unit_name, uid, range_note)
            roster_text = gen.add_to_skills_list(roster_text, guid)
            gate_count += 1
            total_rows += len(gate_rows)

        open(unit['path'], 'w', encoding='utf-8').write(roster_text)
        total_gates += gate_count
        print(f'{uid}({unit_name}) → {target_base}: 게이트 {gate_count}개, 행 {sum(len(v) for v in by_gate.values())}개')

    print()
    print(f'총 {len(all_routes)}개 원작 유닛 회수, 게이트 {total_gates}개, 행 {total_rows}개')


if __name__ == '__main__':
    main()
