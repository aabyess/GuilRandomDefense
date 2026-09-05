#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""PM 지시(2026-09-06) — 전설적인 33종·초월함 25종의 스킬 게이트를 우리 파일에 직접
박혀 있는 원작 게이트 문자열(description의 `게이트 "..."` 자리 — 오늘 밤 게이트별
배정·더미채널 생성기들이 원본 그대로 남겨둔 것)과 실제 필드값(triggerType·
triggerChance·gaugeKind·hitCountThreshold)을 대조한다. 어긋난 것만 CSV로.

⚠️ "원작 표와 대조"라고 했지만 실제로는 파일 자체가 이미 원작 게이트 문자열을
그대로 인용하고 있다(생성기들이 추적 가능하게 항상 원문을 남겼다) — 그래서 외부
CSV를 다시 매칭하는 대신 파일 자기 자신의 인용문과 필드를 비교한다. 이게 더
정확하다: 매칭 실수 없이 "이 파일이 자기가 인용한 게이트를 실제로 반영했는가"를
직접 검증한다.
"""
import csv
import re
import sys

sys.path.insert(0, 'Tools')
import check_assignment_invariants as cai

GRADE_NAMES = {5: '전설적인', 6: '제한됨', 7: '초월함', 8: '불멸의', 9: '영원한'}
GATE_RE = re.compile(r'게이트 "([^"]*)"')

# 축 밖(우리 스키마에 없음) 조건들 — 확률/게이지 판정에서 빼고 기록만 한다.
OFF_AXIS_PATTERNS = [
    (re.compile(r'버프[A-Za-z0-9]*==true'), '버프 보유 조건'),
    (re.compile(r'버프[A-Za-z0-9]*==false'), '버프 미보유 조건'),
]


def parse_gate_string(gate):
    """게이트 문자열 -> dict(kind='chance'|'gauge'|'none', chance=, gauge_kind=,
    threshold=, dropped=[...]) 이거나 파싱 실패 시 None."""
    if gate.strip() == '(게이트 없음)':
        return dict(kind='none', chance=1.0, gauge_kind=None, threshold=None, dropped=[])

    dropped = []
    remaining = gate
    for pat, label in OFF_AXIS_PATTERNS:
        if pat.search(remaining):
            dropped.append(label)
            remaining = pat.sub('', remaining)

    parts = [p.strip() for p in re.split(r'\bAND\b', remaining) if p.strip()]

    chance = 1.0
    gauge_kind = None
    threshold = None
    any_term = False
    for p in parts:
        m = re.match(r'^1/(\d+)$', p)
        if m:
            chance *= 1 / int(m.group(1))
            any_term = True
            continue
        m = re.match(r'^MANA게이지([\d.]+)$', p)
        if m:
            gauge_kind = 0
            threshold = int(float(m.group(1)))
            any_term = True
            continue
        m = re.match(r'^LIFE게이지([\d.]+)$', p)
        if m:
            gauge_kind = 1
            threshold = int(float(m.group(1)))
            any_term = True
            continue
        if p:
            return None  # 못 알아보는 조건 — 비교 보류(모른다고 틀렸다고 하지 않는다)

    if gauge_kind is not None:
        return dict(kind='gauge', chance=None, gauge_kind=gauge_kind, threshold=threshold, dropped=dropped)
    if any_term or dropped:
        return dict(kind='chance', chance=chance, gauge_kind=None, threshold=None, dropped=dropped)
    return None


def main():
    guid_to_path = cai.build_guid_index()
    roster_assets = cai.glob("Assets/Data/Units/Roster/*.asset")

    rows_out = []
    checked = 0
    skipped_no_gate = 0
    skipped_unparsed = 0

    for rp in roster_assets:
        text = cai.read(rp)
        gm = re.search(r'^  grade: (\d+)', text, re.M)
        if not gm or int(gm.group(1)) not in GRADE_NAMES:
            continue
        grade = GRADE_NAMES[int(gm.group(1))]
        base = rp.stem
        guids = cai.resolve_skill_guids(text)

        for g in guids:
            sp = guid_to_path.get(g)
            if not sp:
                continue
            stext = cai.read(sp)
            fname = str(sp).split('/')[-1]
            desc_m = re.search(r'^  description: (.*)$', stext, re.M)
            desc = desc_m.group(1) if desc_m else ''

            our_tt_m = re.search(r'^  triggerType: (\d+)', stext, re.M)
            our_tc_m = re.search(r'triggerChance: ([\d.eE+-]+)', stext)
            our_gk_m = re.search(r'gaugeKind: (\d+)', stext)
            our_th_m = re.search(r'hitCountThreshold: (\d+)', stext)
            our_tt = our_tt_m.group(1) if our_tt_m else None
            our_tc = float(our_tc_m.group(1)) if our_tc_m else None
            our_gk = our_gk_m.group(1) if our_gk_m else None
            our_th = int(our_th_m.group(1)) if our_th_m else None
            n_effects = len(re.findall(r'^    - kind: \d+\n', stext, re.M))

            gate_matches = GATE_RE.findall(desc)
            if not gate_matches:
                skipped_no_gate += 1
                # 원작 게이트 인용 자체가 없다 — 2채널(피해표) CSV는 애초에 게이트
                # 컬럼이 없어 전부 triggerChance=1.0(항상 발동)으로 근사했다는
                # 사실이 밤새 여러 커밋에 이미 문서화돼 있다. 효과가 실제로 있는
                # (n_effects>0) 파일만 "화력에 영향을 주는 헐거움"으로 잡는다 —
                # 06번① 빈 스텁(n_effects==0)은 어차피 화력 0이라 게이트가 헐거워도
                # 영향이 없다(이미 EMPTY_EFFECTS_AUDIT에서 처리됨, 여기서 또 안 셈).
                if our_tt == '0' and our_tc == 1.0 and n_effects > 0 and '2차 배정' in desc:
                    rows_out.append(dict(
                        등급=grade, 유닛=base, 스킬파일=fname, 원작게이트='(2채널 CSV에 게이트 컬럼 없음)',
                        불일치=f'원작 발동조건 미상 — 우리는 triggerChance=1.0(매 타)으로 근사, 효과 {n_effects}개 보유',
                        방향='헐거움(원작 게이트 정보 자체가 없어 100%로 근사 — 2채널 구조적 한계)',
                        축밖조건='-',
                    ))
                continue

            for gate_str in set(gate_matches):
                parsed = parse_gate_string(gate_str)
                if parsed is None:
                    skipped_unparsed += 1
                    continue
                checked += 1

                mismatch = None
                direction = None
                if parsed['kind'] == 'gauge':
                    if our_tt != '3':
                        mismatch = f"원작=게이지({['MANA','LIFE'][parsed['gauge_kind']]}{parsed['threshold']}) 우리=triggerType {our_tt}(게이지 아님)"
                        direction = '종류 다름'
                    elif our_gk != str(parsed['gauge_kind']) or our_th != parsed['threshold']:
                        mismatch = f"원작 threshold={parsed['threshold']}(종류{parsed['gauge_kind']}) 우리 threshold={our_th}(종류{our_gk})"
                        direction = '헐거움(임계값 낮음)' if (our_th or 0) < parsed['threshold'] else '빡빡함(임계값 높음)'
                else:  # chance 또는 none
                    if our_tt != '0':
                        mismatch = f"원작=확률({parsed['chance']:.4f}) 우리=triggerType {our_tt}(확률형 아님)"
                        direction = '종류 다름'
                    elif our_tc is None or round(our_tc, 6) != round(parsed['chance'], 6):
                        mismatch = f"원작 확률={parsed['chance']:.4f} 우리 확률={our_tc}"
                        direction = '헐거움(우리가 더 자주 발동)' if (our_tc or 0) > parsed['chance'] else '빡빡함(우리가 덜 자주 발동)'

                if mismatch:
                    rows_out.append(dict(
                        등급=grade, 유닛=base, 스킬파일=fname, 원작게이트=gate_str,
                        불일치=mismatch, 방향=direction,
                        축밖조건=', '.join(parsed['dropped']) or '-',
                    ))

    out_path = 'Docs/reference/GATE_MISMATCH_2026-09-06.csv'
    with open(out_path, 'w', encoding='utf-8', newline='') as f:
        w = csv.DictWriter(f, fieldnames=['등급', '유닛', '스킬파일', '원작게이트', '불일치', '방향', '축밖조건'])
        w.writeheader()
        for r in rows_out:
            w.writerow(r)

    print(f'대조 {checked}건, 불일치 {len(rows_out)}건 -> {out_path}')
    print(f'게이트 인용 없어 대조 불가 {skipped_no_gate}건, 못 알아본 게이트 표현 {skipped_unparsed}건')

    from collections import Counter
    grade_counter = Counter(r['등급'] for r in rows_out)
    dir_counter = Counter((r['등급'], r['방향']) for r in rows_out)
    print('등급별 불일치:', dict(grade_counter))
    print('등급×방향:', dict(dir_counter))


if __name__ == '__main__':
    main()
