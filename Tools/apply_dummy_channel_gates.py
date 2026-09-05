#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""더미 채널 52행에 리서치담당이 찾은 실제 "진입 게이트"(더미를 소환하는 호출자
쪽 조건 — 트리거 내부 Stage 단계가 아니라)를 적용한다
(Docs/reference/DUMMY_CHANNEL_GATES.csv, `진입게이트`/`진입확신도` 열,
04516dd → aa1c1bc로 추정 5건 중 4건이 확정으로 좁혀짐. 방법론: 소환 지점이
여럿이면 "누가 소환하나"가 아니라 "이 행의 유닛(HashAttack 등록 평타 트리거)은
누구인가"로 좁힌다).

⚠️ 뿌리 ⑱ 회피 — 이 스크립트는 에셋 트리를 다시 스캔해 원작→로스터 대응표를
새로 만들지 않는다. f67cc59가 실제로 건드린 40개 파일만 고정 목록으로 읽어
그 안의 effect들을 CSV의 "출처표기" 문자열로 정확히 매칭해서 게이트만 채운다
(배정 자체는 절대 안 건드림).

## 분류 (52행 중 이 스크립트 대상은 51행 — e055/h04F는 d6499ef 때 h07M에
덮여 사라져서 ②(h04F 복구)로 미룬다. 51행 확정 50 · 추정 1(e0H5), 불가 0)
- GetRandomInt(1,N)==X 또는 리터럴 "1/N" → triggerChance=1/N(X가 몇이든 상관없다
  — 처음에 X/N으로 잘못 짜서 최대 22배까지 과다 계상될 뻔했다, 커밋 전에 잡음).
  복합(AND)이면 곱한다.
- 시전자마나==NNN / 시전자 마나 == NNN(공백 있는 표기도 있다) → OnHitCount(Mana,
  threshold=NNN, resetTo=0).
- 능력레벨(...)/NOT(능력레벨...)/버프보유(...)/GetUnitUserData(...)/체력>NN 같이
  우리 축에 없는 조건이 AND로 섞여 있으면 그 조건만 버리고 나머지(확률·게이지)는
  그대로 채운다 — description에 "축 밖" 표시. 버프 조건 단독(대체 확률 없음)이면
  triggerChance=1.0으로 근사.
- e0H5(카이도)만 유일한 추정 — HashAttack 등록 평타 트리거는 Kaido_Attack으로
  확정됐지만 stage0→4 분기 조건은 미확인이라 매 타 발동(1.0)으로 근사하고
  [추정] 꼬리표를 유지한다.

## 파일 분할
한 로스터 유닛에 붙은 더미 여러 개가 서로 다른 게이트를 쓰면(예: h030 바솔로뮤
쿠마 — 1/10과 마나115가 섞여 있음), "게이트 하나=SkillData 하나" 원칙(오늘 밤
내내 쓴 것)에 따라 파일을 쪼갠다. 같은 게이트를 쓰는 것끼리는 한 파일에 묶는다.
"""
import csv
import hashlib
import re
import subprocess
from collections import defaultdict

SKILL_DIR = 'Assets/Data/UnitSkills'
ROSTER_DIR = 'Assets/Data/Units/Roster'
SKILL_SCRIPT_GUID = '9457f64cd84d34fd095791a069c0adc6'

HEAD = """%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {fileID: 0}
  m_PrefabInstance: {fileID: 0}
  m_PrefabAsset: {fileID: 0}
  m_GameObject: {fileID: 0}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {fileID: 11500000, guid: __SCRIPT__, type: 3}
  m_Name: __NAME__
  m_EditorClassIdentifier:
"""

def guid_for(name):
    return hashlib.md5(('guilrd/dummygate/' + name).encode()).hexdigest()


def write_meta(path, guid):
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def resolve_gate(row):
    """returns dict(kind='chance'|'gauge', triggerChance=, gaugeKind=, hitCountThreshold=,
    resetTo=, dropped=[...], estimated=bool) or None(unresolved)."""
    dummy_id = row['더미유닛ID']
    gate = row['진입게이트']
    confidence = row['진입확신도']
    estimated = confidence.strip() != '확정'

    # e0H5(카이도)만 유일하게 진짜 특수 케이스다 — "평타 1회 → Kaido_Attack stage4"
    # 처럼 자연어라 아래 정규식 어느 것도 못 잡는다. HashAttack 등록 평타 트리거
    # 자체는 aa1c1bc로 확정됐지만 stage0→4 분기 조건은 여전히 미확인이라 매 타
    # 발동(1.0)으로 근사한다.
    if dummy_id == 'e0H5':
        return dict(kind='chance', triggerChance=1.0,
                    dropped=['stage0→4 분기 조건 미확인 — 매 타로 근사'], estimated=True,
                    estimated_note='HashAttack 등록 평타 트리거는 Kaido_Attack으로 확정됐지만 '
                                    'stage0→4 내부 분기 조건은 못 찾아 매 타 발동으로 근사했다.')

    dropped = []

    # 리터럴 "1/N" 표기
    m = re.match(r'^1/(\d+)$', gate.strip())
    if m:
        return dict(kind='chance', triggerChance=round(1 / int(m.group(1)), 6), dropped=[], estimated=estimated)

    # 마나/체력 게이지(정확한 값, GetRandomInt와 안 섞인 경우) — "시전자마나==115.00"
    # (붙여쓰기+소수점)과 "시전자 마나 == 115"(공백+정수) 두 표기가 다 나온다.
    m = re.search(r'시전자\s*마나\s*==\s*([\d.]+)', gate)
    if m and 'GetRandomInt' not in gate:
        return dict(kind='gauge', gaugeKind=0, hitCountThreshold=int(float(m.group(1))), resetTo=0, dropped=[], estimated=estimated)
    m = re.search(r'시전자\s*체력\s*==\s*([\d.]+)', gate)
    if m and 'GetRandomInt' not in gate:
        return dict(kind='gauge', gaugeKind=1, hitCountThreshold=int(float(m.group(1))), resetTo=1, dropped=[], estimated=estimated)

    # 확률(복합 가능) — GetRandomInt(1,N)==X는 X가 몇이든 1..N 중 하나를 정확히
    # 맞히는 것이라 확률은 항상 1/N이다(X/N이 아니다 — 처음에 이걸 거꾸로 짜서
    # 22배까지 과다 계상될 뻔했다, 커밋 전에 잡음). 축 밖 조건(능력레벨·버프보유·
    # GetUnitUserData·체력>N)은 버리고 기록.
    probs = [1 / int(a) for a, b in re.findall(r'GetRandomInt\(1,(\d+)\)==(\d+)', gate)]
    # ⚠️ 괄호가 중첩된다(예: 버프보유(GetAttacker(),'B017')==false — GetAttacker()
    # 자체가 괄호 쌍이라 [^)]*로는 첫 ')'에서 멈춘다) — non-greedy .*? + DOTALL로
    # 다음 "==" 마커까지 통째로 삼킨다(오늘 밤 검사기에서 같은 함정을 이미 한 번
    # 겪었다 — 괄호 깊이를 안 세면 잘못 끊긴다).
    for pat, label in [
        (r"능력레벨\(.*?\)==\d+", '능력레벨 조건'),
        (r"NOT\(\(능력레벨\(.*?\)==\d+\)\)", '능력레벨 부정 조건'),
        (r"버프보유\(.*?\)==false", '버프 미보유 조건'),
        (r'GetUnitUserData\(.*?\)==\d+', 'GetUnitUserData 조건'),
        (r'시전자체력>[\d.]+', '체력>N 조건(거의 항상 참)'),
    ]:
        if re.search(pat, gate):
            dropped.append(label)

    if probs:
        chance = 1.0
        for p in probs:
            chance *= p
        return dict(kind='chance', triggerChance=round(chance, 6), dropped=dropped, estimated=estimated)

    if dropped:
        # 확률 없이 조건(버프 등)만 있는 경우 — 항상 발동으로 근사.
        return dict(kind='chance', triggerChance=1.0, dropped=dropped, estimated=estimated)

    raise ValueError(f'게이트 파싱 실패: {dummy_id} {gate!r}')


def main():
    rows = list(csv.DictReader(open('Docs/reference/DUMMY_CHANNEL_GATES.csv', encoding='utf-8-sig')))
    by_source = {r['출처표기']: r for r in rows}

    out = subprocess.run(
        ['git', '-c', 'core.quotepath=false', 'show', '--name-only', 'f67cc59'],
        cwd='.', capture_output=True, text=True, check=True,
    ).stdout
    files = sorted(set(
        line for line in out.splitlines()
        if line.startswith('Assets/Data/UnitSkills/SkillData_더미채널_') and line.endswith('.asset')
    ))
    print(f'대상 파일 {len(files)}개')

    resolved_count = 0
    unresolved_count = 0
    estimated_count = 0

    for path in files:
        text = open(path, encoding='utf-8').read()
        # bullet 목록에서 "출처: 값[태그]" 항목을 순서대로 뽑는다(원본 순서 = effect 순서)
        # 매칭 키(source_key)는 콜론 앞 "출처"만, 재삽입용(full_bullet)은 원문 전체를 쓴다.
        bullet_matches = re.findall(r'(  · ([^:]+): [\d.]+(?: \[[^\]]+\])?)', text)
        bullets = [(full, key) for full, key in bullet_matches]
        # effect 블록들을 순서대로 파싱
        blocks = list(re.finditer(r'    - kind: 0\n(?:      .*\n)+', text))
        assert len(bullets) == len(blocks), (path, len(bullets), len(blocks))

        base = path.split('/')[-1][len('SkillData_더미채널_'):-len('.asset')]
        roster_path = f'{ROSTER_DIR}/{base}.asset'
        roster_text = open(roster_path, encoding='utf-8').read()
        old_guid_m = re.search(rf'guid: (\w+)', open(path + '.meta', encoding='utf-8').read())
        old_guid = old_guid_m.group(1)

        groups = defaultdict(list)  # gate signature -> [(bullet_text, block_text, csv_row)]
        for (full_bullet, source_key), block in zip(bullets, blocks):
            bullet = full_bullet
            csv_row = by_source.get(source_key)
            if csv_row is None:
                print(f'⚠️ 매칭 실패: {path} :: {source_key}')
                groups[('unresolved',)].append((bullet, block.group(0), None))
                unresolved_count += 1
                continue
            g = resolve_gate(csv_row)
            if g is None:
                groups[('unresolved',)].append((bullet, block.group(0), csv_row))
                unresolved_count += 1
                continue
            if g['estimated']:
                estimated_count += 1
            else:
                resolved_count += 1
            sig = ('chance', g['triggerChance']) if g['kind'] == 'chance' else \
                  ('gauge', g['gaugeKind'], g['hitCountThreshold'], g['resetTo'])
            groups[sig].append((bullet, block.group(0), csv_row, g))

        # unresolved 그룹은 옛 파일 그대로 남긴다(값 안 건드림) — 별도 파일로.
        new_guids = []
        idx = 0
        for sig, items in groups.items():
            idx += 1
            if sig == ('unresolved',):
                # 원래 파일을 그대로 유지(게이트 미확인 태그 이미 있음) — 이름 유지.
                keep_blocks = ''.join(b for _, b, _ in items)
                keep_bullets = [bt for bt, _, _ in items]
                _rewrite_file(path, old_guid, keep_bullets, keep_blocks, text, trigger_type=0,
                              cooldown=0, trigger_chance=1.0, hit_threshold=0, reset_to=0, gauge_kind=0)
                new_guids.append(old_guid)
                continue

            kind = sig[0]
            name = f'SkillData_더미채널_{base}_{idx}'
            guid = guid_for(name)
            new_path = f'{SKILL_DIR}/{name}.asset'
            blocks_text = ''.join(b for _, b, _, _ in items)
            # 이제 게이트가 확정됐으니 f67cc59가 붙였던 "[근사: 원작 게이트 미확인]"
            # 꼬리표는 지운다 — 안 지우면 방금 확정한 값 옆에 낡은 "미확인" 표시가
            # 남아 다음 사람을 헷갈리게 한다.
            bullets_list = [re.sub(r' \[근사: 원작 게이트 미확인\]', '', bt) for bt, _, _, _ in items]
            dropped_all = sorted(set(d for _, _, _, g in items for d in g['dropped']))
            estimated_any = any(g['estimated'] for _, _, _, g in items)
            estimated_notes = sorted(set(g.get('estimated_note') for _, _, _, g in items if g.get('estimated_note')))

            if kind == 'chance':
                tc = sig[1]
                trigger_type, cooldown, hit_th, reset_to, gauge_kind = 0, 0, 0, 0, 0
            else:
                trigger_type, cooldown = 3, 0
                gauge_kind, hit_th, reset_to = sig[1], sig[2], sig[3]
                tc = 1.0

            _write_new_file(new_path, guid, base, bullets_list, blocks_text,
                             trigger_type, cooldown, tc, hit_th, reset_to, gauge_kind,
                             dropped_all, estimated_any, estimated_notes)
            new_guids.append(guid)

        # 로스터 skills 리스트: 옛 guid 제거 + 새 guid(들) 추가
        roster_text2 = re.sub(rf'  - \{{fileID: 11400000, guid: {old_guid}, type: 2\}}\n', '', roster_text, count=1)
        m = re.search(r'^  skills:\n((?:  - .*\n)*)', roster_text2, re.M)
        block = m.group(1)
        add_lines = ''.join(f"  - {{fileID: 11400000, guid: {g}, type: 2}}\n" for g in new_guids
                             if f'guid: {g},' not in block)
        roster_text2 = roster_text2[:m.end(1)] + add_lines + roster_text2[m.end(1):]
        open(roster_path, 'w', encoding='utf-8').write(roster_text2)

        # 원래 단일 파일(다중 게이트로 쪼개졌다면) 삭제
        if len(groups) > 1 or ('unresolved',) not in groups:
            import os
            if new_guids != [old_guid]:
                os.remove(path)
                os.remove(path + '.meta')

    print(f'확정 적용: {resolved_count} / 추정 적용: {estimated_count} / 미확인 유지: {unresolved_count}')


def _rewrite_file(path, guid, bullets, blocks_text, orig_text, trigger_type, cooldown,
                   trigger_chance, hit_threshold, reset_to, gauge_kind):
    desc_m = re.search(r'^  description: (.*)$', orig_text, re.M)
    desc = desc_m.group(1)
    # 기존 bullet 라인 전부 제거하고 남긴 것만 다시 붙인다
    desc = re.sub(r'(  · [^:]+: [\d.]+(?: \[[^\]]+\])?)+', '', desc)
    desc += ''.join(bullets)
    body = (
        HEAD.replace('__SCRIPT__', SKILL_SCRIPT_GUID).replace('__NAME__', path.split('/')[-1][:-6])
        + re.search(r'  skillName: .*\n', orig_text).group(0)
        + f"  description: {desc}\n"
        + f"  triggerType: {trigger_type}\n"
        + "  levels:\n"
        + f"  - cooldown: {cooldown}\n"
        + f"    triggerChance: {trigger_chance}\n"
        + "    range: 0\n"
        + f"    hitCountThreshold: {hit_threshold}\n"
        + f"    resetTo: {reset_to}\n"
        + f"    gaugeKind: {gauge_kind}\n"
        + "    effects:\n"
        + blocks_text
    )
    open(path, 'w', encoding='utf-8').write(body)


def _write_new_file(path, guid, base, bullets, blocks_text, trigger_type, cooldown,
                     trigger_chance, hit_threshold, reset_to, gauge_kind, dropped, estimated,
                     estimated_notes=()):
    name = path.split('/')[-1][:-6]
    tag = ''
    if dropped:
        tag = f" ⚠️ 축 밖이라 뺀 조건: {', '.join(dropped)} — 실제보다 자주 발동할 수 있다."
    if estimated:
        # 행마다 추정 사유가 다르므로(카이도=내부 분기 미확인, 그 외=소환 지점 복수)
        # 있으면 그 사유를 그대로 쓰고, 없으면 "소환 지점 복수" 기본 문구로 대체한다 —
        # 한 번 확정된 사유를 다른 행에도 뭉뚱그려 붙이면 그 자체가 또 낡은 꼬리표가 된다.
        note = ' '.join(estimated_notes) if estimated_notes else \
            '같은 더미가 여러 곳에서 소환돼 확정 못함 — 후보 중 가장 낮은 발동률/가장 대표적인 게이지를 썼다.'
        tag += f' [추정] {note}'
    description = (
        f"더미 채널 게이트 확정(2026-09-06, DUMMY_CHANNEL_GATES.csv, a1c34bc) — {base}." +
        tag + ''.join(bullets)
    )
    body = (
        HEAD.replace('__SCRIPT__', SKILL_SCRIPT_GUID).replace('__NAME__', name)
        + f"  skillName: {name}\n"
        + f"  description: {description}\n"
        + f"  triggerType: {trigger_type}\n"
        + "  levels:\n"
        + f"  - cooldown: {cooldown}\n"
        + f"    triggerChance: {trigger_chance}\n"
        + "    range: 0\n"
        + f"    hitCountThreshold: {hit_threshold}\n"
        + f"    resetTo: {reset_to}\n"
        + f"    gaugeKind: {gauge_kind}\n"
        + "    effects:\n"
        + blocks_text
    )
    open(path, 'w', encoding='utf-8').write(body)
    write_meta(path, guid)


if __name__ == '__main__':
    main()
