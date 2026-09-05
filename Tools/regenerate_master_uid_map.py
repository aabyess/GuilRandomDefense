#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""uid→로스터 대응표 CSV를 다시 스캔해서 얼린다 — 뿌리 ⑱ 근본 해법.

translate_hidden_recipes.build_master_uid_map()은 이제 이 스크립트가 만든
Docs/reference/MASTER_UID_ROSTER_MAP.csv를 읽기만 하고 절대 라이브 스캔을
안 한다. 여러 세션이 동시에 커밋하는 동안 라이브 스캔 생성기를 재실행하면
결과가 흔들리는 사고(2026-09-06 새벽, h00R·h029 등 중복/소실 — 되돌림)가
재발하는 걸 막기 위해서다.

## 사용법
1. git status가 깨끗한지 확인한다(재실행 전 확인 — 규칙).
2. 이 스크립트를 손으로 실행한다: python3 Tools/regenerate_master_uid_map.py
3. git diff로 바뀐 행만 리뷰한다 — 대응이 왜 바뀌었는지 설명 없이 바뀐 행이
   있으면 원인(다른 세션의 커밋? 생성기 로직 변경?)을 먼저 확인한다.
4. CSV만 커밋한다. 이 CSV를 읽는 스크립트(translate_hidden_recipes.py,
   fill_dummy_channel_damage.py 등)는 따로 안 건드려도 된다 —
   build_master_uid_map()이 이미 CSV를 읽는 쪽이라 자동으로 새 대응을 본다.

## 언제 다시 돌리나
"대응이 바뀔 만한 커밋"이 들어왔을 때만 — 06번①/게이트·회수/1채널/2채널
스킬 배정 파일이 새로 생기거나 바뀌었을 때. 이 CSV를 소비하는 스크립트
자체를 고치는 건 재실행 사유가 아니다.
"""
import csv
import sys

sys.path.insert(0, 'Tools')
import translate_hidden_recipes as th


def main():
    mapping = th._scan_master_uid_map()
    rows = sorted(mapping.items())

    with open(th.MASTER_UID_MAP_CSV, 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(['유닛ID', '로스터', '채널'])
        for uid, (base, channel) in rows:
            w.writerow([uid, base, channel])

    print(f'{th.MASTER_UID_MAP_CSV} — uid {len(rows)}개 기록')


if __name__ == '__main__':
    main()
