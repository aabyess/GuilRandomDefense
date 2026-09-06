#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""79행 이식(`71acf64`)의 잠금(triggerChance=0)을 리서치담당 진입게이트 조사(`799ef03`,
DUMMY_CHANNEL_MISSING.csv에 소환조건(로컬)·진입게이트·게이트확신도 열 추가)로 푼다.

## 왜 기존 16개 파일을 고쳐 쓰지 않고 통째로 다시 만드는가
`apply_dummy_channel_missing.py`는 "로스터당 파일 하나"로 묶었다 — 그때는 전부
triggerChance=0이라 안에 몇 개를 담든 상관없었다. 그런데 이제 로스터 하나 안에서도
능력마다 진입 확률이 다르다(예: 초월_강주혁_AP는 나미 능력 셋 중 하나만 1/9고 둘은
사실상 매 타, 조로 능력은 1/6). `SkillLevel` 하나엔 `triggerChance`가 하나뿐이라
확률이 다르면 파일을 나눠야 한다 — 그래서 "로스터당 파일 하나"가 아니라
"(로스터, 확률) 조합당 파일 하나"로 다시 짠다. 기존 16개 파일은 지우고 로스터
참조도 걷어낸 뒤 새로 쓴다(guid도 새로 낸다 — 헷갈림 방지).

## 진입게이트 → 확률 변환 규칙 (전부 description에 원문을 같이 남긴다)
`진입게이트` 필드는 `트리거명[이벤트직결]: 조건` 형태가 " | "(OR)로 여러 개 이어진다.
각 분기를 최상위 AND로 쪼개 조건별로 판정한다:
  - `(조건없음)` → 1.0
  - `GetRandomInt(1,N)==k` → 1/N (같은 분기에 여럿 AND면 곱한다)
  - `NOT(x==상수)` 또는 `x==false`(부정형) → ~1.0로 근사(그 조건이 참이 아닌 순간이
    드물다는 뜻 — PM 지시, 미호크 `NOT(마나==175)` 사례와 같은 처리)
  - 그 외 양의 등호·상태값(`Stage==N`·`버프==true`·`마나==N` 등, 부정 아님) →
    **정량화 불가**로 판정하고 그 조건은 축 밖으로 뺀다. 같은 분기에 `GetRandomInt`가
    있으면 그 값만 쓰고(양의 상태조건은 캡션에 "축 밖으로 뺌"이라 남김), 없으면 그
    분기 전체를 못 쓴다.
  - OR로 이어진 여러 분기 중 하나라도 판정 가능하면 **그중 가장 높은 확률**을
    대표로 쓴다(과소계상보다 안전한 근사, 이 프로젝트 전체에서 일관되게 쓰는 규칙).
  - 모든 분기가 정량화 불가면 **잠금 유지**(triggerChance=0, [게이트 미확인] 그대로) —
    숫자를 지어내지 않는다.

이 규칙으로 56행 중 **49행이 풀리고 7행은 그대로 잠긴다**(마르코 1행 — 순수 상태
조건뿐이라 확률 없음, 핸콕 6행 — `버프 B03M==true`가 양의 필요조건이라 근사가 위험함,
GATE_FIX_9의 고도현/Z 사례와 같은 유형이라 여기서도 지어내지 않는다).
"""
import csv
import hashlib
import re
import sys
from collections import defaultdict

sys.path.insert(0, 'Tools')
import apply_dummy_channel_missing as base_mod

SKILL_DIR = base_mod.SKILL_DIR
ROSTER_DIR = base_mod.ROSTER_DIR
SKILL_SCRIPT_GUID = base_mod.SKILL_SCRIPT_GUID
CSV_PATH = base_mod.CSV_PATH
HEAD = base_mod.HEAD

RANDINT_RE = re.compile(r'GetRandomInt\(1,\s*([\w.\[\]]+)\)==')

# ── 71acf64가 만든 옛 파일(로스터당 1개) — 지우고 로스터 참조도 뗀다 ────────────
OLD_ROSTERS = [
    '변화됨_박은석', '변화됨_최상호', '불멸_고도현', '불멸_박은석', '불멸_정윤식',
    '영원_조세민', '영원_최상호', '전설적인_임채민', '제한_최영민',
    '초월_강주혁_AP', '초월_구주호_AD', '초월_신문철_AP', '초월_임채민_AP', '초월_최상호_AP',
    '초월_황준석_ADAP', '히든_호치킨',
]


def split_ands(cond):
    parts, depth, cur = [], 0, ''
    for t in re.split(r'(\(|\)|\sAND\s)', cond):
        if t == '(':
            depth += 1; cur += t
        elif t == ')':
            depth -= 1; cur += t
        elif t == ' AND ' and depth == 0:
            parts.append(cur); cur = ''
        else:
            cur += t
    parts.append(cur)
    return [p.strip() for p in parts if p.strip() not in ('', '()')]


def conjunct_prob(c):
    c = c.strip()
    if '조건없음' in c:
        return 1.0
    m = RANDINT_RE.search(c)
    if m:
        try:
            return 1.0 / int(m.group(1))
        except ValueError:
            return None
    if ('NOT(' in c and '==' in c) or re.search(r'==\s*false\b', c):
        return 1.0
    return None


def branch_prob(cond):
    prob, any_none, any_rand = 1.0, False, False
    for c in split_ands(cond):
        p = conjunct_prob(c)
        if p is None:
            any_none = True
            continue
        if p < 1.0:
            any_rand = True
        prob *= p
    if any_none and not any_rand:
        return None
    return prob


def entry_prob(gate_text):
    best = None
    for b in gate_text.split(' | '):
        m = re.match(r'^.*?\[이벤트직결\]:\s*(.*)$', b.strip())
        cond = m.group(1) if m else b.strip()
        p = branch_prob(cond)
        if p is not None and (best is None or p > best):
            best = p
    return best


def remove_old_wiring():
    """71acf64가 만든 로스터당-1파일(`_79행.asset`)과, 이 스크립트를 이미 한 번 돌렸다면
    그때 만든 (로스터,확률)별 파일(`_79행_*.asset`)까지 전부 지우고 로스터 참조도 뗀다 —
    재실행해도 안전하게(idempotent) 처음부터 다시 쓴다."""
    import glob
    for base in OLD_ROSTERS:
        path = f'{ROSTER_DIR}/{base}.asset'
        text = open(path, encoding='utf-8').read()
        for old_asset in glob.glob(f'{SKILL_DIR}/SkillData_더미채널_{base}_79행*.asset'):
            meta_m = re.search(r'guid: ([0-9a-f]+)', open(old_asset + '.meta', encoding='utf-8').read())
            if meta_m:
                old_guid = meta_m.group(1)
                text = re.sub(rf'  - \{{fileID: 11400000, guid: {old_guid}, type: 2\}}\n', '', text)
            for suffix in ('', '.meta'):
                p = old_asset + suffix
                import os
                if os.path.exists(p):
                    os.remove(p)
        open(path, 'w', encoding='utf-8').write(text)


def guid_for(name):
    return hashlib.md5(('guilrd/dummychannel79v2/' + name).encode()).hexdigest()


def main():
    remove_old_wiring()

    rows = list(csv.DictReader(open(CSV_PATH, encoding='utf-8-sig')))
    by_group = defaultdict(list)  # (base, prob_or_None) -> [(row, char, grade)]

    for row in rows:
        did = row['더미유닛ID']
        if did == 'h05J':
            base, char, grade = '불멸_정윤식', '레일리', '불멸'
        elif did == 'h0BH':
            base, char, grade = '불멸_고도현', '시키', '불멸'
        else:
            base, char, grade = base_mod.resolve_row(row)
        if not base:
            continue
        prob = entry_prob(row['진입게이트'])
        by_group[(base, prob)].append((row, char, grade))

    made = 0
    locked = 0
    unlock_report = []
    for gi, ((base, prob), entries) in enumerate(sorted(
            by_group.items(), key=lambda kv: (kv[0][0], -1 if kv[0][1] is None else kv[0][1]))):
        at = base_mod.roster_attack_type(base)
        locked_now = prob is None
        suffix = 'lock' if locked_now else f'{prob:.4f}'.replace('.', '')
        name = f'SkillData_더미채널_{base}_79행_{suffix}'
        guid = guid_for(name)
        path = f'{SKILL_DIR}/{name}.asset'

        if locked_now:
            head_desc = (
                "⚠️ [게이트 미확인]이라 triggerChance=0으로 잠갔다. 피해 없음이 아니다 — "
                "이 그룹은 진입게이트 조사(799ef03) 뒤에도 전 분기가 양의 상태조건(Stage/"
                "버프/마나 등)뿐이라 확률을 못 냈다(지어내지 않음). 값·범위·대상·타입은 "
                "원작 그대로 채워뒀다."
            )
            tc = 0.0
        else:
            head_desc = (
                f"진입게이트 확정(2026-09-06, 리서치담당 799ef03) — triggerChance={prob:.4f}"
                f"로 잠금 해제. 양의 상태조건(스테이지·버프 보유 등)은 축 밖이라 뺐다 — "
                f"실제보다 자주 발동할 수 있다."
            )
            tc = prob
            unlock_report.append((base, prob, len(entries)))

        desc_lines = [head_desc]
        effect_blocks = []
        for row, char, grade in entries:
            value = float(row['값'].replace(',', ''))
            desc_lines.append(
                f"  · {row['더미유닛ID']}({row['더미이름']}) {row['능력']} {row['능력이름']} "
                f"[{row['필드의미']}]: {value:.0f} (원작 {char}·{grade}) 진입게이트: "
                f"{row['진입게이트']}"
            )
            effect_blocks.append(
                "    - kind: 0\n"
                "      basis: 0\n"
                "      target: 3\n"
                "      damageType: 1\n"
                f"      attackType: {at}\n"
                f"      multiplier: {value}\n"
                "      bonus: 0.0\n"
                "      chance: 1\n"
                "      hitCount: 1\n"
                "      duration: 0\n"
                "      casterBuffCountFactor: 0\n"
            )

        description = ''.join(desc_lines)
        body = (
            HEAD.replace('__SCRIPT__', SKILL_SCRIPT_GUID).replace('__NAME__', name)
            + "  m_EditorClassIdentifier:\n"
            + f"  skillName: {entries[0][0]['능력']} {entries[0][0]['능력이름']}\n"
            + f"  description: {description}\n"
            + "  triggerType: 0\n"
            + "  levels:\n"
            + "  - cooldown: 0\n"
            + f"    triggerChance: {tc}\n"
            + "    range: 0\n"
            + "    hitCountThreshold: 0\n"
            + "    resetTo: 0\n"
            + "    gaugeKind: 0\n"
            + "    effects:\n"
            + ''.join(effect_blocks)
        )
        open(path, 'w', encoding='utf-8').write(body)
        base_mod.write_meta(path, guid)
        base_mod.add_to_skills(f'{ROSTER_DIR}/{base}.asset', guid)
        made += len(entries)
        if locked_now:
            locked += len(entries)
        print(f'{base}: prob={prob} n={len(entries)} -> {path}')

    print(f'\n총 {made}행 -> 잠금 해제 {made - locked}행 / 잠금 유지 {locked}행')

    # GATE_LOCKED_ROWS.csv를 "여전히 잠긴 것"만 남도록 갱신
    with open('Docs/reference/GATE_LOCKED_ROWS.csv', 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(['더미유닛ID', '능력', '능력이름', '값', '부모트리거', '원작캐릭터', '등급',
                    '배선로스터', '잠금사유'])
        for (base, prob), entries in sorted(by_group.items(), key=lambda kv: kv[0][0]):
            if prob is not None:
                continue
            for row, char, grade in entries:
                w.writerow([row['더미유닛ID'], row['능력'], row['능력이름'], row['값'],
                            row['부모트리거'], char, grade, base,
                            '전 분기 양의 상태조건뿐(정량화 불가)'])

    return unlock_report


if __name__ == '__main__':
    main()
