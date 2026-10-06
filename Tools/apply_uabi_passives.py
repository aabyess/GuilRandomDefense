"""원작 uabi 상시 능력(오라·상시 공속/평타 가산·강타) 가운데 우리 로스터에 없는 것을 대응표 전체에서 찾아 채운다 — 2026-09-30 구현담당1.

등급별 FILL_LIST의 「빠진 상시 오라·상시 효과」 절을 한 도구로 묶은 것(Blender 조사와 같은 그물: uabi → w3a 필드). 손으로 옮기다 빠뜨리지 않게 데이터에서 유도한다.
대상 기반 능력과 옮기는 법(apply_eternal_auras.py·apply_immortal_auras.py와 같은 규칙):
  AOae  Oae1 < 0 → 적 이속 감소(Slow, 남는 비율 1+Oae1) / Oae2 > 0 → 공속 %(atar에 self면 자기, friend면 아군)
  AHad  Had1 → 적 방어 가감(버프 ID = abuf)
  ACac  Cac1 ≤ 1 → 공격력 %, > 1 → 공격력 고정 가산(atar self/friend)
  Aasl  Slo1 < 0 → 적 이속 감소
  AIsx  Isx1 → 자기 공속 %(상시) · AIfb Idam → 자기 평타 고정 가산 — 버프 ID가 없어 능력 코드를 키로
  ACbh  Hbh1(빈칸 = 스톡 15%)·Hbh2(평타 배수)·Hbh3(추가 피해)·adur(스턴, 빈칸이면 미반영). atar ancient → PV==200 · nonancient → PV≠200 · sapper → PV≥200
  반경 aare(빈칸 = 스톡 900). atar none은 껍데기라 제외.
안 넣는 것(EXCLUDE):
  - 소환체·형태·얻는 길 없는 유닛의 능력(그 유닛이 서 있을 때만 도는 것): h04Y 광분 빅맘 · h0AE 히그마 · h0BH·h0BI·h0BJ 시키 부유물 · h088 우타 토트 · A179(불사조 마르코 형태 전용 공속 +225%)
  - 우리 축에 안 담기는 것: A0EI·A0EH(흰수염 — PV 조건이 붙은 방깎 오라). (A12P 야마토 적 이속 +15% 손해 오라는 09-30 밤부터 담긴다 — Slow 배수가 1 초과를 받는다.)
  - 값이 0인 것(A03K Cac1 0 · Hbh1 0으로 명시된 강타)
「이미 있음」 판정: 그 로스터에 연결된 SkillData의 이름·설명·효과 어디에든 능력 코드가 적혀 있으면 건너뛴다(강타는 같은 배수·가산 값의 효과가 있어도 건너뛴다 — 이중 계상 방지).
사용: python3 Tools/apply_uabi_passives.py [--apply] [접두사…]   (기본은 목록만 출력)
"""
import collections
import csv
import glob
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
sys.path.insert(0, os.path.join(os.path.dirname(__file__), 'w3x'))
import skill_asset_tool as sat  # noqa: E402
import w3a  # noqa: E402
import w3u  # noqa: E402

SELF, ALLIES, ENEMIES, SINGLE = 0, 1, 2, 3
DAMAGE, STUN, ARMOR_BONUS, FLAT, SPEED, SLOW, PERCENT = 0, 1, 4, 11, 12, 13, 14
ATK = 3
PV_LT, PV_EQ, PV_GE, PV_NE = 1, 2, 3, 4
ON_HIT, AURA = 0, 2
STOCK_AURA_RADIUS = 900
STOCK_ACBH_HBH1 = 15.0
EXCLUDE_UIDS = {'h04Y', 'h0AE', 'h0BH', 'h0BI', 'h0BJ', 'h088'}
EXCLUDE_ABILITIES = {'A179'}   # A0EI·A0EH(흰수염 PV 조건 방깎 오라)는 2026-10-06부터 담는다 — 오라가 효과별 PV 조건을 본다
# 흔함 로스터에 대응된 원작 uid는 「안흔함 한 등급 어긋남」(NEXT_SESSION §3-5) 미해결이라 건너뛴다.
SKIP_ROSTER_PREFIXES = ('흔함_',)
BASES = ('AOae', 'AHad', 'ACac', 'Aasl', 'AIsx', 'AIfb', 'ACbh')


def main(apply, prefixes):
    U = {u['id']: u for u in w3u.parse(os.path.join(sat.ROOT, 'Tools/w3x/원본/war3map_new.w3u'))}
    A = {a['id']: a for a in w3a.parse(os.path.join(sat.ROOT, 'Tools/w3x/원본/war3map_new.w3a'))}
    guid_to_asset = {re.search(r'guid: (\w+)', open(p + '.meta').read()).group(1): p for p in glob.glob(os.path.join(sat.SKILL_DIR, '*.asset'))}
    done, changed, rows = set(), [], []
    for r in csv.DictReader(open(os.path.join(sat.ROOT, 'Docs/reference/MASTER_UID_ROSTER_MAP.csv'), encoding='utf-8')):
        uid, ro = r['유닛ID'], r['로스터']
        if uid not in U or uid in EXCLUDE_UIDS or ro.startswith(SKIP_ROSTER_PREFIXES) or (prefixes and not any(ro.startswith(p) for p in prefixes)):
            continue
        rp = os.path.join(sat.ROSTER_DIR, ro + '.asset')
        if not os.path.exists(rp):
            continue
        text = open(rp, encoding='utf-8').read()
        linked = ''.join(open(guid_to_asset[g], encoding='utf-8').read() for g in re.findall(r'guid: (\w+), type: 2', text.split('  trait:')[0]) if g in guid_to_asset)
        attack_type = int((re.search(r'^  attackType: (\d+)', text, re.M) or [0, 1])[1])
        for ab in str(U[uid]['mods'].get('uabi', '')).split(','):
            if ab not in A or A[ab]['base'] not in BASES or ab in EXCLUDE_ABILITIES or (ro, ab) in done:
                continue
            done.add((ro, ab))
            base, f = A[ab]['base'], {}
            for m in A[ab]['mods']:
                if m.get('level') in (0, 1):
                    f.setdefault(m['field'], m['value'])
            atar = str(f.get('atar', ''))
            if 'none' in atar or ab in linked:
                continue
            name = re.sub(r'^[!#$/AB^]+', '', str(f.get('anam') or '')).strip()
            radius = float(f.get('aare') or STOCK_AURA_RADIUS)
            buff = str(f.get('abuf') or '') or ab
            who = [t for t, key in ((SELF, 'self'), (ALLIES, 'friend')) if key in atar.split(',')]
            effects, kind, label, trigger, level_kw = [], 'aura', '', AURA, {}
            if base == 'AOae':
                if float(f.get('Oae1') or 0) != 0 and 'enemies' in atar.split(','):
                    # 음수 = 이감, 양수 = 적 이속 증가(손해 오라 — 야마토 A12P). 둘 다 Slow 효과의 배수(남는/늘어난 비율)로.
                    effects.append(sat.effect(kind=SLOW, target=ENEMIES, multiplier=round(1.0 + float(f['Oae1']), 4)))
                    label = '적 이속 %+d%% 오라%s' % (round(float(f['Oae1']) * 100), '(손해 오라)' if float(f['Oae1']) > 0 else '')
                if float(f.get('Oae2') or 0) > 0:
                    effects += [sat.effect(kind=SPEED, target=t, multiplier=round(float(f['Oae2']), 4), buffId=buff) for t in who]
                    label = '%s 공속 +%d%%' % ('아군' if ALLIES in who else '자기', round(float(f['Oae2']) * 100))
            elif base == 'AHad' and float(f.get('Had1') or 0) != 0:
                # 2026-10-06: atar의 PV 제한(sapper = PV≥200 · ancient = PV 200 · nonancient = PV≠200 · nonsapper = PV<200)을 효과 조건으로 옮긴다
                # (오라 적용 때 UnitAttacker가 효과별 targetCondition을 본다 — ApplyPersistentAuraEffectsToEnemy).
                tags_had = atar.split(',')
                # sapper + nonancient(드래곤 A0W8) = PV≥200이면서 PV 200이 아닌 것 = PV>200(스토리·신세계, 보스 제외) → 「≥201」로 옮긴다.
                c_had = PV_LT if 'nonsapper' in tags_had else PV_EQ if 'ancient' in tags_had else PV_NE if 'nonancient' in tags_had and 'sapper' not in tags_had else PV_GE if 'sapper' in tags_had else 0
                cond_had = dict(targetCondition=c_had, targetConditionValue=201.0 if ('sapper' in tags_had and 'nonancient' in tags_had) else 200.0) if c_had else {}
                effects.append(sat.effect(kind=ARMOR_BONUS, target=ENEMIES, multiplier=float(f['Had1']), buffId=buff, **cond_had))
                label = '적 방어 %+d 오라%s' % (float(f['Had1']), {PV_LT: ' (PV<200 적만)', PV_EQ: ' (PV 200 대상만)', PV_NE: ' (PV 200 아닌 적)', PV_GE: (' (PV>200 대상만: 스토리·신세계)' if cond_had.get('targetConditionValue') == 201.0 else ' (PV≥200 대상만)')}.get(c_had, ''))
            elif base == 'ACac' and float(f.get('Cac1') or 0) != 0:
                v = float(f['Cac1'])
                effects += [sat.effect(kind=PERCENT if abs(v) <= 1 else FLAT, target=t, multiplier=round(v, 4), buffId=buff) for t in who]
                label = '%s 공격력 %s' % ('아군' if ALLIES in who else '자기', ('%+d%%' % round(v * 100)) if abs(v) <= 1 else '%+d' % v)
            elif base == 'Aasl' and float(f.get('Slo1') or 0) < 0:
                effects.append(sat.effect(kind=SLOW, target=ENEMIES, multiplier=round(1.0 + float(f['Slo1']), 4)))
                label = '적 이속 %+d%% 오라' % round(float(f['Slo1']) * 100)
            elif base == 'AIsx' and float(f.get('Isx1') or 0) != 0:
                effects.append(sat.effect(kind=SPEED, target=SELF, multiplier=round(float(f['Isx1']), 4), buffId=ab))
                label, radius = '자기 공속 %+d%%(상시)' % round(float(f['Isx1']) * 100), 0.0
            elif base == 'AIfb' and float(f.get('Idam') or 0) > 0:
                effects.append(sat.effect(kind=FLAT, target=SELF, multiplier=float(f['Idam']), buffId=ab))
                label, radius = '자기 평타 +%d' % float(f['Idam']), 0.0
            elif base == 'ACbh':
                chance = float(f['Hbh1']) if 'Hbh1' in f else STOCK_ACBH_HBH1
                mult, bonus, stun = round(float(f.get('Hbh2') or 0), 4), float(f.get('Hbh3') or 0), round(float(f.get('adur') or 0), 4)
                if chance <= 0 or not (mult or bonus or stun):
                    continue
                if (mult or bonus) and re.search(r'basis: 3\n(?:      .*\n)*?      multiplier: %s\n      bonus: %s\n' % (re.escape(sat.num(mult)), re.escape(sat.num(bonus))), linked):
                    continue   # 같은 값의 강타가 이미 있다(이름표만 다른 에셋)
                tags = atar.split(',')
                c = PV_EQ if 'ancient' in tags else PV_NE if 'nonancient' in tags else PV_GE if 'sapper' in tags else 0
                cond = dict(targetCondition=c, targetConditionValue=200.0) if c else {}
                if mult or bonus:
                    effects.append(sat.effect(kind=DAMAGE, basis=ATK, target=SINGLE, damageType=1, attackType=attack_type, multiplier=mult, bonus=bonus, **cond))
                if stun:
                    effects.append(sat.effect(kind=STUN, target=SINGLE, duration=stun, **cond))
                kind, trigger, radius = 'bash', ON_HIT, 0.0
                level_kw = dict(triggerChance=round(chance / 100.0, 6))
                label = '%g%%%s%s%s%s' % (chance, ' ×%g' % mult if mult else '', ' +%d' % bonus if bonus else '', ' · 스턴 %g초' % stun if stun else '',
                                          {PV_EQ: ' (PV 200 대상만)', PV_NE: ' (PV 200 아닌 적)', PV_GE: ' (PV≥200 대상만)'}.get(c, ''))
            if not effects:
                continue
            fields = ' · '.join('%s %s' % (k, ('%g' % v) if isinstance(v, float) else v) for k, v in f.items()
                                if k in ('Oae1', 'Oae2', 'Had1', 'Cac1', 'Slo1', 'Isx1', 'Idam', 'Hbh1', 'Hbh2', 'Hbh3', 'adur', 'ahdu', 'aare', 'abuf') and v not in ('', None))
            rows.append((ro, uid, ab, base, label, fields))
            if not apply:
                continue
            stem = 'SkillData_%s_%s_%s' % ('원작오라' if kind == 'aura' else '원작능력', ro, ab)
            path = os.path.join(sat.SKILL_DIR, stem + '.asset')
            if not os.path.exists(path):
                desc = '원작 %s uabi %s(%s, %s, atar %s). %s2026-09-30 구현담당1(Tools/apply_uabi_passives.py).' % (
                    uid, ab, base, fields, atar or '빈칸',
                    '반경 빈칸 = 스톡 900. ' if kind == 'aura' and 'aare' not in f and radius else
                    ('Hbh1 빈칸 = 스톡 15%. ' if base == 'ACbh' and 'Hbh1' not in f else ''))
                sat.new_asset(stem, '%s %s — %s' % (ab, name, label), desc, trigger, [sat.level_block(range=float(radius), effects=effects, **level_kw)])
                changed += [path, path + '.meta']
            if sat.add_skill_to_unit(ro, sat.guid_of(path)):
                changed.append(rp)
    grade = collections.Counter(r[0].split('_')[0] for r in rows)
    print('%s %d건 (%s)' % ('넣음' if apply else '넣을 것', len(rows), ' · '.join('%s %d' % kv for kv in sorted(grade.items()))))
    for r in rows:
        print('  %-24s %s %s %s  %s  [%s]' % r)
    if apply:
        print('바꾼 파일 %d' % len(set(changed)))


if __name__ == '__main__':
    main('--apply' in sys.argv, [a for a in sys.argv[1:] if not a.startswith('--')])
