#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""uid 단위 등급 안 순위매칭 공용 헬퍼 — 뿌리 ⑮ 방지(⑤-0, 2026-09-06).

세 생성기(by_gate·2채널·1채널)가 각자 독립적으로 "행 단위로 순위를 매겨 로스터와
짝짓는다"를 구현했다가 전부 같은 실수를 냈다 — 원작 유닛 하나(uid)의 행이
여럿이면 그 행 개수만큼 서로 다른 로스터 유닛에 쪼개지거나(1채널·2채널) 넘치는
uid가 조용히 드롭됐다(by_gate). CHANNEL_UNIFICATION_DESIGN_2026-09-06.md의
"같은 실수를 세 번 냈다는 것 자체가 신호다" — 이 로직을 한 곳에 두고 새로
만드는 생성기는 반드시 이걸 쓴다.

원칙: uid로 먼저 묶고, 그룹 단위로 점수를 매겨 등급 안 로스터와 1:1로 짝짓는다.
넘치는 uid는 조용히 버리지 않고 dropped로 돌려준다.
"""
import glob
import re
from collections import defaultdict


def group_by_uid(rows, uid_key='유닛ID'):
    """rows를 uid로 묶는다 — 그룹 내부는 입력 순서를 그대로 보존한다."""
    groups = defaultdict(list)
    for r in rows:
        groups[r[uid_key]].append(r)
    return dict(groups)


def match_uid_groups_to_roster(uid_groups, eligible, score_fn):
    """uid_groups: {uid: [rows...]}. eligible: 등급 안 가용 로스터
    (DPS 내림차순 정렬, claimed 제외 — 이미 호출자가 걸러서 넘긴다).
    score_fn(rows) -> float, 클수록 먼저 배정(1등 uid ↔ 1등 로스터).

    반환: (matched, dropped)
      matched = [(uid, roster_unit, rows), ...] — 순위 순서.
      dropped = 등급 슬롯이 모자라 못 들어간 uid 목록 — 호출자가 반드시 보고할 것
                (뿌리 ⑮ by_gate 사고가 이걸 조용히 넘겨서 났다).
    """
    ranked_uids = sorted(uid_groups.keys(), key=lambda u: -score_fn(uid_groups[u]))
    n = min(len(ranked_uids), len(eligible))
    matched = [(ranked_uids[i], eligible[i], uid_groups[ranked_uids[i]]) for i in range(n)]
    dropped = ranked_uids[n:]
    return matched, dropped


# ⚠️ 뿌리 ⑱ 형제급 발견: skill/skills 필드 중 skill만 보고 claimed를 판정하면
# skills 리스트만 채워진 유닛(예: 06번① 능력교체형, 게이트·회수·더미채널이
# add_to_skills_list로 넣은 유닛)을 "안 찜됨"으로 잘못 본다 — 2채널
# regroup_skill_damage_by_unit.py 주석에서 실제로 7건 걸렸다고 기록됨. 이후
# 만드는 로더는 전부 이 함수를 쓴다.
def is_claimed(roster_text):
    has_skill = not re.search(r'^  skill: \{fileID: 0\}', roster_text, re.M)
    has_skills = bool(re.search(r'^  skills:\n  - ', roster_text, re.M))
    return has_skill or has_skills


def load_roster(roster_dir):
    """등급·DPS·평타타입·claimed(skill+skills 둘 다 보는 정정판)를 채운
    로스터 목록. path 오름차순(안정적인 재실행을 위해 glob 결과를 정렬)."""
    roster = []
    for path in sorted(glob.glob(f'{roster_dir}/*.asset')):
        text = open(path, encoding='utf-8').read()
        grade_m = re.search(r'^  grade: (\d+)', text, re.M)
        ap_m = re.search(r'^  attackPower: ([\d.]+)', text, re.M)
        aspd_m = re.search(r'^  attackSpeed: ([\d.]+)', text, re.M)
        if not (grade_m and ap_m and aspd_m):
            continue
        attack_type_m = re.search(r'^  attackType: (\d+)', text, re.M)
        attack_type = int(attack_type_m.group(1)) if attack_type_m else 0
        roster.append(dict(
            path=path, text=text, grade=int(grade_m.group(1)),
            dps=float(ap_m.group(1)) * float(aspd_m.group(1)),
            attack_type=attack_type, claimed=is_claimed(text),
        ))
    return roster
