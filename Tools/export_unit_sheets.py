#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""사장님용 엑셀 두 장을 만든다(드라이브에 올려 두고 배포마다 최신화하는 표). 읽기 전용 — Assets는 건드리지 않는다.

  1) 「G.R.D 미구현 스킬 유닛.xlsx」 — 전설적인·제한됨·초월함·불멸·영원함·랜덤·다른세계 중 스킬이 안 도는 유닛
  2) 「G.R.D 구랜디-원랜디 매칭.xlsx」 — 우리 유닛과 원작 유닛의 대응 + 스킬이 하는 일 한 줄

사용(저장소 루트): <openpyxl·pyyaml 있는 python> Tools/export_unit_sheets.py [출력폴더]
  기본 출력폴더 ~/grd/GRD_unit_sheets/ . openpyxl이 없으면 `python3 -m venv ~/grd/GRD_unit_sheets/venv && venv/bin/pip install openpyxl pyyaml`.
같은 에셋이면 같은 내용이 나온다(정렬 고정, 시각 대신 커밋 해시를 맨 위에 적는다).

판정 기준(1번 표): 로스터 skill/skills가 가리키는 SkillData 중 levels에 effects가 하나라도 있으면 「구현」.
  - 스킬 연결 0 → 「스킬 연결 0」 / 연결은 있으나 effects 전부 빔 → 「스킬은 있으나 효과 빔」.
  - 위 둘에 안 걸려도 효과가 전부 값 0(피해 0·버프 0)이면 「효과가 전부 0값」으로 넣는다.
  - 사양 스킬(SkillData_사장님_*)이라도 effects가 비고 코드(UnitAttacker 등)에 이름 전용 처리가 없으면 미구현으로 본다.
"""
import csv
import glob
import json
import os
import re
import subprocess
import sys

import yaml
import datetime
from openpyxl import Workbook
from openpyxl.styles import Alignment, Font, PatternFill
from openpyxl.utils import get_column_letter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
OUT = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else '~/grd/GRD_unit_sheets')

GRADE = {0: '흔함', 1: '안흔함', 2: '특별함', 3: '희귀함', 4: '히든', 5: '전설적인', 6: '제한됨', 7: '초월함', 8: '불멸의',
         9: '영원한', 10: '랜덤전용', 11: '다른세계', 12: '특수함', 14: '변화됨'}
GRADE_ORDER = [0, 1, 2, 3, 4, 14, 5, 6, 7, 8, 9, 10, 11, 12]
TOP7 = (5, 6, 7, 8, 9, 10, 11)

# ───────────────────────── 로딩 ─────────────────────────


def load_asset(p):
    t = open(p, encoding='utf-8').read()
    t = re.sub(r'^%.*\n', '', t, flags=re.M)
    t = re.sub(r'^--- !u!\d+ &\d+', '---', t, flags=re.M)
    return yaml.load(t, Loader=yaml.CSafeLoader)['MonoBehaviour']


GUID = {}
for _m in glob.glob(ROOT + '/Assets/Data/**/*.asset.meta', recursive=True):
    _g = re.search(r'guid: (\w+)', open(_m, encoding='utf-8').read())
    if _g:
        GUID[_g.group(1)] = _m[:-5]
_skill_cache = {}


def skill_of(g):
    if g not in _skill_cache:
        p = GUID.get(g)
        d = load_asset(p) if p else None
        if d is not None:
            d['_file'] = os.path.basename(p)[:-6]
        _skill_cache[g] = d
    return _skill_cache[g]


def skill_guids(d):
    out = [s['guid'] for s in (d.get('skills') or []) if s.get('guid')]
    if not out and d.get('skill') and d['skill'].get('guid'):
        out = [d['skill']['guid']]
    return out


def load_roster():
    rs = []
    for p in sorted(glob.glob(ROOT + '/Assets/Data/Units/Roster/*.asset')):
        d = load_asset(p)
        d['_file'] = os.path.basename(p)[:-6]
        if d.get('isSystemUnit'):
            continue
        rs.append(d)
    return rs


def enum_names(enum):
    src = open(ROOT + '/Assets/Scripts/Data/SkillData.cs', encoding='utf-8').read()
    body = re.search(r'public enum ' + enum + r'\s*\{(.*?)\n\}', src, re.S).group(1)
    body = re.sub(r'//.*', '', body)
    return [n.strip() for n in body.split(',') if n.strip()]


KIND = enum_names('SkillEffectKind')


def git_head():
    try:
        return subprocess.check_output(['git', '-C', ROOT, 'log', '-1', '--format=%h %ad', '--date=short'], text=True).strip()
    except Exception:
        return '?'


# ───────────────────────── 숫자·문장 ─────────────────────────


def num(v):
    if v is None:
        return '0'
    if isinstance(v, float):
        if abs(v - round(v)) < 1e-9:
            v = int(round(v))
        else:
            s = ('%.4f' % v).rstrip('0').rstrip('.')
            return s
    if isinstance(v, int) and abs(v) >= 10000:
        return '{:,}'.format(v)
    return str(v)


def pct(v):
    p = v * 100
    return num(round(p, 2)) + '%'


def secs(v):
    return num(v) + '초'


DMG = {1: '물리', 2: '마법'}
TARGET = {0: '자신', 1: '범위 안 아군', 2: '범위 안 적 전체', 3: '맞은 적 1기', 4: '범위 안 무작위 적 1기', 5: '연쇄로 이어지는 적', 6: '지정한 아군'}
TRIGGER = {0: 'OnHitChance', 1: 'CooldownAutoCast', 2: 'Aura', 3: 'OnHitCount', 4: 'OnEnemyEnterRange', 5: 'ActiveButton'}


def basis_text(e):
    b = e.get('basis', 0)
    m = e.get('multiplier', 0) or 0
    bonus = e.get('bonus', 0) or 0
    if b == 0:
        return num(m)
    if b == 1:
        return '대상 최대체력의 ' + pct(m)
    if b == 2:
        return '대상 현재체력의 ' + pct(m)
    if b == 13:
        return '대상이 잃은 체력의 ' + pct(m)
    if b == 3:
        return '공격력×' + num(m) + ('+' + num(bonus) if bonus else '')
    if b == 4:
        return '연구소 단계×' + num(m) + ('+' + num(bonus) if bonus else '')
    if b == 5:
        return '방금 평타 피해×' + num(m)
    if b == 6:
        return '대상 이동속도 비례(×' + num(m) + ')'
    if b == 7:
        return '게이지 값×' + num(m)
    if b == 8:
        return '스킬 레벨 비례(×' + num(m) + ')'
    if b in (9, 10, 11):
        return {9: '힘', 10: '민첩', 11: '지능'}[b] + '×' + num(m)
    if b == 12:
        return '자기강화 단계×' + num(m)
    return num(m)


def effect_text(e, lv):
    k = KIND[e['kind']] if e['kind'] < len(KIND) else str(e['kind'])
    m = e.get('multiplier', 0) or 0
    bonus = e.get('bonus', 0) or 0
    dur = e.get('duration', 0) or 0
    tgt = TARGET.get(e.get('target', 0), '')
    rng = lv.get('range', 0) or 0
    where = tgt
    if e.get('target') == 2 and rng:
        where = '주변 %s 안 적 전체' % num(rng)
    if e.get('target') == 1 and rng:
        where = '주변 %s 안 아군' % num(rng)
    if e.get('target') == 5 and e.get('maxTargets'):
        where = '연쇄로 최대 %d기' % e['maxTargets']
    chance = e.get('chance', 1)
    cs = '' if chance is None or chance >= 1 else '(%s 확률) ' % pct(chance)
    if k == 'Damage':
        kindtxt = ('폭발형 ' if e.get('explosive') else '') + ('고정 ' if e.get('fixedDamage') else '') + DMG.get(e.get('damageType', 0), '')
        amount = basis_text(e)
        flat = e.get('basis', 0) == 0
        body = '%s피해 %s' % (kindtxt + ' ' if kindtxt else '', amount) if flat else '%s피해(%s)' % (kindtxt + ' ' if kindtxt else '', amount)
        base = '%s%s%s' % (cs, where + '에게 ' if where else '', body)
        extra = []
        if e.get('hitCount', 1) and e['hitCount'] > 1:
            extra.append('%d회' % e['hitCount'])
        if e.get('zoneTickInterval'):
            extra.append('반경 %s 지대에서 %s마다 반복' % (num(e.get('zoneRadius', 0)), secs(e['zoneTickInterval'])))
        if e.get('lineLength'):
            extra.append('직선 길이 %s' % num(e['lineLength']))
        if e.get('requiredTargetBuffId'):
            extra.append('조건: 대상이 「%s」 상태' % e['requiredTargetBuffId'])
        if e.get('targetCondition'):
            extra.append('대상 포인트값 조건 %s' % num(e.get('targetConditionValue', 0)))
        return base + (' (' + ', '.join(extra) + ')' if extra else '')
    if k == 'Stun':
        return '%s%s 기절 %s' % (cs, where, secs(dur or e.get('heroDuration', 0)))
    if k == 'ArmorBreak':
        return '%s%s 방어력 %s 깎기%s' % (cs, where, num(m), '(%s)' % secs(dur) if dur else '')
    if k == 'ArmorBonus':
        return '%s%s 방어력 %s%s' % (cs, where, ('+' if m >= 0 else '') + num(m), '(%s)' % secs(dur) if dur else '')
    if k == 'Slow':
        return '%s%s 이동속도 %s로 감소 %s' % (cs, where, pct(m), secs(dur))
    if k == 'ExtraProjectile':
        return '%s추가 투사체 %s발' % (cs, num(m))
    if k == 'HealOverTime':
        return '%s%s 초당 체력 %s 회복' % (cs, where, num(m))
    if k == 'HealPercentOverTime':
        return '%s%s 초당 최대체력의 %s 회복' % (cs, where, pct(m))
    if k == 'AttackPowerBuffFlat':
        return '%s%s 공격력 +%s%s' % (cs, where, num(m), '(%s)' % secs(dur) if dur else '')
    if k == 'AttackPowerBuffPercent':
        if e.get('perHighGradeUnitBonus'):
            return '%s전설 이상 유닛 1기당 %s 공격력 %s(최대 %s)' % (cs, where, '+' + pct(e['perHighGradeUnitBonus']), pct(e.get('perHighGradeUnitBonusCap', 0)))
        return '%s%s 공격력 %s%s%s' % (cs, where, '+' if m >= 0 else '', pct(m), '(%s)' % secs(dur) if dur else '')
    if k == 'AttackSpeedBuffPercent':
        return '%s%s 공격속도 +%s%s' % (cs, where, pct(m), '(%s)' % secs(dur) if dur else '')
    if k in ('AegrStack', 'AisrStack', 'A11SStack', 'A0VJStack'):
        return '%s%s 대상 약화 표식 %s단계 누적(%s)' % (cs, where, num(m), {'AegrStack': '방어', 'AisrStack': '마법저항', 'A11SStack': '스킬 피해 감수', 'A0VJStack': '핸콕 석화 방깎'}[k])
    if k == 'ApplyBuff':
        return '%s%s에 「%s」 상태 부여%s' % (cs, where, e.get('buffId', ''), ' (%s)' % secs(dur) if dur else '')
    if k == 'RemoveBuff':
        return '%s「%s」 상태 해제' % (cs, e.get('buffId', ''))
    if k == 'SummonUnit':
        return '%s소환수 소환 (%s 유지)' % (cs, secs(e.get('summonLifetime', 20)))
    if k == 'GrantSkillToAllies':
        return '주변 아군에게 스킬을 빌려줌(오라)'
    if k == 'BossDamageMultiplier':
        return '보스에게 피해 ×%s' % num(m)
    if k == 'AllyMoveSpeedDebuff':
        return '주변 아군(자신 제외) 이동속도 −%s' % pct(m)
    if k == 'SplashDamageMultiplier':
        return '평타 광역(스플래시) 피해 ×%s' % num(m)
    if k == 'AttackSpeedStack':
        return '평타마다 공속 +%s 누적(최대 %s)' % (pct(e.get('stackPerHit', 0.01)), pct(e.get('stackCap', 1)))
    if k == 'AttackDamageStack':
        return '평타마다 피해 +%s 누적(최대 %s)' % (pct(e.get('stackPerHit', 0.01)), pct(e.get('stackCap', 1)))
    if k == 'DamagePerAllyDebuff':
        return '받는 아군발 디버프 1개당 피해 +%s(최대 +%s)' % (pct(m), pct(bonus))
    if k == 'SelfStunRefillLifeGauge':
        return '자신이 %s 기절, 끝나면 체력 게이지 가득' % secs(dur)
    if k == 'KillNormalEnemies':
        return '주변 %s 안 가장 가까운 일반 적 1기 즉사(보스 제외)' % num(rng)
    if k == 'DamageGrowthOverTime':
        return '%s마다 피해 +%s(최대 +%s)' % (secs(dur), pct(m), pct(bonus))
    if k == 'DamageOverTime':
        return '%s에게 %s 동안 %s마다 %s 지속 피해' % (where or '대상', secs(dur), secs(e.get('zoneTickInterval') or 1), num(m))
    if k == 'AllySkillDamageBonus':
        return '주변 아군 스킬 피해 ×%s' % num(1 + m)
    if k == 'DispelAllyDebuffs':
        return '주변 아군이 받는 아군발 디버프 무효'
    if k == 'Knockback':
        return '%s대상 %s 뒤로 밀어냄(보스 면역)' % (cs, num(m))
    if k == 'FormChange':
        return '형태 변환 %s(유닛삭제 횟수만큼 연장)' % secs(dur)
    if k == 'GoldPlusBonus':
        return '살아 있는 동안 처치 골드 배율 +%s' % num(m)
    if k == 'StoryDamageMultiplier':
        return '스토리 적에게 피해 ×%s' % num(m)
    if k == 'SkillDamagePerHighGradeUnit':
        return '전설 이상 유닛 1기당 스플래시 피해 +%s(최대 +%s)' % (pct(m), pct(bonus))
    if k == 'TeleportToPoint':
        return '지정한 지점으로 순간이동'
    if k == 'GrantLoot':
        return '노획물 지급'
    if k == 'DamagePerTargetArmorShred':
        return '대상 방깎 1당 피해 +%s(최대 +%s)' % (pct(m), pct(bonus))
    if k == 'RecruitEnemy':
        return '일반 적 1기를 아군 유닛으로 회유(동시 최대 %s기)' % num(bonus or 5)
    if k == 'DamagePerRecruit':
        return '회유 유닛 1기당 피해 +%s' % pct(m)
    if k == 'DamageVsTargetBuff':
        return '「%s」 상태 적에게 피해 ×%s' % (e.get('buffId', ''), num(1 + m))
    if k == 'GambleRoll':
        return '%s 확률로 베팅 성공(이어지는 효과가 성공/실패로 갈림)' % pct(m)
    if k == 'SkillDamageAfterKill':
        return '스킬로 일반 적 처치 시 %s 동안 스킬 피해 +%s' % (secs(dur), pct(m))
    if k == 'SlowRewardBonus':
        return '이감 1%%당 처치 보상 +%s' % pct(m)
    if k == 'GrantInvention':
        return '발명품 지급(금화·목재·위습·소환수·유닛 중 하나)'
    if k == 'SkillTriggerChanceBonus':
        return '받는 유닛의 확률 스킬 발동 확률 ×%s' % num(1 + m)
    if k == 'RevealTreasure':
        return '주변 %s 안 숨은 보물 위치 공개(%s)' % (num(rng), secs(dur))
    if k in ('ManaRegenBuff', 'LifeRegenBuff'):
        return '%s 초당 %s 게이지 재생 +%s' % (where, '마나' if k == 'ManaRegenBuff' else '체력', num(m))
    if k == 'DesignateAlly':
        return '아군 1기를 지정'
    if k == 'GrantRandomWisp':
        return '랜덤·흔함·안흔함·특별함·희귀함 위습 중 1개 획득'
    if k == 'LaneExplosiveAmp':
        return '내 라인 적이 받는 폭발형 피해 증폭 %s단계' % num(m)
    return '%s(%s)' % (k, num(m))


def trigger_text(s, lv, d):
    t = s.get('triggerType', 0)
    ch = lv.get('triggerChance', 1)
    cd = lv.get('cooldown', 0)
    rng = lv.get('range', 0)
    if t == 0:
        return '평타 적중 시 ' + ('매번' if ch is None or ch >= 1 else pct(ch) + ' 확률로')
    if t == 1:
        return secs(cd) + '마다 자동' + ('' if ch is None or ch >= 1 else ' (%s 확률)' % pct(ch))
    if t == 2:
        return '상시 효과'
    if t == 3:
        g = '마나' if lv.get('gaugeKind', 0) == 0 else '체력'
        return '%s 게이지가 %s에 닿으면' % (g, num(lv.get('hitCountThreshold', 0)))
    if t == 4:
        return '적이 %s 안에 들어오면 한 번' % num(lv.get('enterRange', 0) or rng)
    if t == 5:
        return '버튼으로 직접 시전' + (' (쿨 %s)' % secs(cd) if cd else '')
    return ''


def skill_sentence(s):
    lv = (s.get('levels') or [{}])[0]
    effs = lv.get('effects') or []
    if not effs:
        return ''
    parts = []
    for e in effs:
        x = effect_text(e, lv)
        if x and x not in parts:  # 아군용·자신용 효과가 같은 문장이면 한 번만
            parts.append(x)
    sent = trigger_text(s, lv, None) + ': ' + ', '.join(p for p in parts if p)
    if len(s.get('levels') or []) > 1:
        sent += ' (레벨 %d단계까지 강화)' % len(s['levels'])
    return sent


def clean_skill_name(name):
    n = (name or '').strip()
    n = re.sub(r'^(?:[A-Za-z][0-9A-Za-z]{3})\s+', '', n)
    return n


def all_zero(s):
    """효과는 있으나 모든 값이 0(피해 0·버프 0·소환 없음)이면 참 — 도는 것처럼 보여도 실제 변화가 없다."""
    effs = [e for l in (s.get('levels') or []) for e in (l.get('effects') or [])]
    if not effs:
        return False
    for e in effs:
        gs = e.get('grantSkill') or {}
        if (e.get('multiplier') or 0) or (e.get('bonus') or 0) or (e.get('duration') or 0) or e.get('buffId') \
                or e.get('summonUnits') or e.get('lootItems') or e.get('rewardWispPool') or (isinstance(gs, dict) and gs.get('guid')):
            return False
    return True


# ───────────────────────── 시트 1: 미구현 ─────────────────────────


def unimplemented_rows(roster):
    rows = []
    for d in roster:
        if d['grade'] not in TOP7:
            continue
        gs = skill_guids(d)
        sks = [skill_of(g) for g in gs]
        sks = [s for s in sks if s]
        live = [s for s in sks if any(l.get('effects') for l in (s.get('levels') or []))]
        if not gs:
            reason = '스킬 연결 0'
        elif not live:
            reason = '스킬은 있으나 효과 빔 (스킬 %d개: %s)' % (len(sks), ', '.join(clean_skill_name(s['skillName']) for s in sks))
        elif all(all_zero(s) for s in live):
            reason = '기타: 효과가 전부 값 0'
        else:
            continue
        rows.append((d['grade'], d['unitName'], d['_file'], reason))
    return rows


def partial_rows(roster):
    """참고: 일부 스킬만 효과가 빈 유닛(전체 미구현은 아님)."""
    rows = []
    for d in roster:
        if d['grade'] not in TOP7:
            continue
        gs = skill_guids(d)
        sks = [s for s in (skill_of(g) for g in gs) if s]
        live = [s for s in sks if any(l.get('effects') for l in (s.get('levels') or []))]
        empty = [s for s in sks if s not in live]
        if live and empty:
            rows.append((d['grade'], d['unitName'], d['_file'], len(live), len(empty),
                         ', '.join(clean_skill_name(s['skillName']) for s in empty)))
    return rows


# ───────────────────────── 시트 2: 매칭 ─────────────────────────


def load_audit():
    fs = sorted(glob.glob(ROOT + '/Docs/reference/SKILL_STAT_SOURCE_AUDIT_*.csv'))
    out = {}
    if not fs:
        return out
    for x in list(csv.reader(open(fs[-1], encoding='utf-8-sig')))[1:]:
        out[x[1]] = x
    return out


def load_master_map():
    by = {}
    p = ROOT + '/Docs/reference/MASTER_UID_ROSTER_MAP.csv'
    for r in csv.DictReader(open(p, encoding='utf-8')):
        by.setdefault(r['로스터'], []).append(r['유닛ID'].upper())
    return by


def unit_names():
    sys.path.insert(0, ROOT + '/Tools/w3x')
    import w3u
    out = {}
    for u in w3u.parse(ROOT + '/Tools/w3x/원본/war3map_new.w3u'):
        out[u['id'].upper()] = (u['mods'].get('unam') or '').strip()
    return out


COLOR = re.compile(r'\|[cC][0-9A-Fa-f]{8}|\|[rR]')
UNIT_ID = re.compile(r'\b[HhEe][0-9A-Za-z]{3}\b')


def clean_unit_name(n):
    return COLOR.sub('', n or '').strip()


def orig_units(d, audit_row, master, names):
    """능력치 가져온 원작 유닛 [(id, 이름)] — 감사표의 「스탯 원작유닛」 우선, 없으면 MASTER_UID 대응표."""
    ids = []
    if audit_row and audit_row[5]:
        ids = [i.upper() for i in re.findall(r'(?:^|/)([HEhe][0-9A-Za-z]{3}) ', audit_row[5])]
    if not ids:
        ids = master.get(d['_file'], [])
    return [(i, clean_unit_name(names.get(i, ''))) for i in ids]


def skill_source(s, stat_ids, names):
    f = s['_file']
    nm = s.get('skillName') or ''
    desc = s.get('description') or ''
    if f.startswith('SkillData_사장님_'):
        return '사장님 사양', True
    if f.startswith('SkillData_공용_'):
        return '사장님 사양(공용 에셋)', True
    kind = '원작 능력' if f.startswith(('SkillData_원작능력', 'SkillData_원작오라', 'SkillData_더미채널', 'SkillData_회수')) else (
        '원작 트리거' if f.startswith(('SkillData_원작트리거', 'SkillData_게이트', 'SkillData_절대쿨')) else '원작')
    code = re.match(r'^([A-Za-z][0-9A-Za-z]{3})\b', nm)
    code = code.group(1) if code else ''
    if not code:
        m = re.search(r'\buabi (A[0-9A-Za-z]{3})\b', desc) or re.search(r'\b(A[0-9][0-9A-Z]{2})\b', desc)
        code = m.group(1) if m else ''
    txt = kind + (' ' + code if code else '')
    mentioned = {i.upper() for i in UNIT_ID.findall(desc)}
    mentioned = {i for i in mentioned if i in names}
    stat = set(stat_ids)
    if mentioned & stat:
        return txt + ' · 능력치 원작 유닛(%s)의 것' % '/'.join(sorted(mentioned & stat)), True
    if mentioned:
        return txt + ' · 다른 원작 유닛 것(%s)' % '/'.join('%s %s' % (i, names[i]) for i in sorted(mentioned)), True
    if stat and any(clean_unit_name(names.get(i, '')) and clean_unit_name(names[i]).split(' ')[0] in desc for i in stat):
        return txt + ' · 능력치 원작 유닛의 것', True
    return txt + ' · 원작 주인 확인 못 함', False


def unit_field_state(name, d):
    """효과 칸이 빈 스킬이 유닛 데이터 필드로 처리되는지 — 확인되면 설명을 돌려준다."""
    if '스플래시' in name and (d.get('attackSplashRadius') or d.get('attackCleaveRadius')):
        return '평타 광역(유닛 데이터 attackSplash/Cleave: 반경 %s)' % num(d.get('attackSplashRadius') or d.get('attackCleaveRadius'))
    if '멀티샷' in name and d.get('attackExtraTargets'):
        return '평타가 추가로 %s기를 더 때림(유닛 데이터)' % d['attackExtraTargets']
    if ('마젠' in name or '체젠' in name) and (d.get('manaAuraRegenPerSecond') or d.get('lifeAuraRegenPerSecond')):
        return '주변 아군 게이지 재생 오라(유닛 데이터: 마나 %s · 체력 %s /초, 반경 %s)' % (
            num(d.get('manaAuraRegenPerSecond', 0)), num(d.get('lifeAuraRegenPerSecond', 0)), num(d.get('manaAuraRange') or d.get('lifeAuraRange')))
    if ('방무뎀' in name) and d.get('attackArmorIgnoreRatio'):
        return '평타가 방어를 %s 무시(유닛 데이터)' % pct(d['attackArmorIgnoreRatio'])
    if ('이동' in name or '비행' in name) and d.get('movementAbility'):
        return '이동 방식 변경(유닛 데이터 movementAbility=%s)' % d['movementAbility']
    return ''


def load_s2_map():
    """재매칭(10-18) 정본 대응표 — 있으면 이것을 먼저 쓴다(S2 2.323 원작)."""
    fs = sorted(glob.glob(ROOT + '/Docs/reference/S2_ROSTER_MAP_*.csv'))
    if not fs:
        return {}
    return {r['로스터']: r for r in csv.DictReader(open(fs[-1], encoding='utf-8-sig'))}


def vfx_state():
    """스킬 guid → 이펙트 상태(원작 이펙트 적용 / 대체 이펙트 / blender 제작 대기 / 없음)."""
    t = open(ROOT + '/Assets/Resources/Effects/SkillVfxTable.asset', encoding='utf-8').read()
    pre = re.search(r'  prefabs:\n((?:  - .*\n)+)', t)
    pg = re.findall(r'guid: (\w+)', pre.group(1)) if pre else []
    gi = {}
    for m in glob.glob(ROOT + '/Assets/**/*.prefab.meta', recursive=True):
        g = re.search(r'guid: (\w+)', open(m, encoding='utf-8').read())
        if g:
            gi[g.group(1)] = m[:-5]
    paths = [gi.get(g, '') for g in pg]
    out = {}
    for m in re.finditer(r'  - skill: \{fileID: \d+, guid: (\w+), type: 2\}\n((?:    .*\n)+)', t):
        kinds = set()
        for s in re.finditer(r'      prefab: (-?\d+)\n      kind: (-?\d+)', m.group(2)):
            pi, ki = int(s.group(1)), int(s.group(2))
            if pi >= 0:
                kinds.add('원작 이펙트 적용' if '/Original/' in (paths[pi] if pi < len(paths) else '') else '대체 이펙트')
            elif ki >= 0:
                kinds.add('대체 이펙트')
        if kinds:
            out[m.group(1)] = '원작 이펙트 적용' if '원작 이펙트 적용' in kinds else '대체 이펙트'
    pend = set()
    for f in glob.glob(ROOT + '/Docs/research/ORIGINAL_VFX_ASSIGN_*.csv'):
        for r in csv.reader(open(f, encoding='utf-8')):
            if r and r[0].startswith('Assets/') and any(c.startswith('원작:') for c in r[1:4]):
                pend.add(os.path.basename(r[0])[:-6])
    return out, pend


def match_rows(roster, audit, master, names):
    rows = []
    unverified = 0
    s2map = load_s2_map()
    vfx, vfx_pend = vfx_state()
    for d in sorted(roster, key=lambda x: (GRADE_ORDER.index(x['grade']) if x['grade'] in GRADE_ORDER else 99, x['_file'])):
        a = audit.get(d['_file'])
        ou = orig_units(d, a, master, names)
        sm = s2map.get(d['_file'])
        if sm and sm.get('상태', '').startswith('원작 없음'):
            orig = '원작 없음(사장님 창작)'
            ou = []
        elif sm and sm.get('S2 원작 id'):
            orig = '%s(%s) · S2 2.323' % (sm['S2 원작 이름'], sm['S2 원작 id'])
            ou = [(sm['S2 원작 id'].upper(), sm['S2 원작 이름'])]
        elif ou:
            orig = ' / '.join('%s(%s)' % (n or '이름 미상', i) for i, n in ou)
        elif d.get('originalMatchName'):
            orig = d['originalMatchName'] + ' (원작 id 미상)'
        else:
            orig = '원작 대응 없음'
        gs = skill_guids(d)
        sks = [s for s in (skill_of(g) for g in gs) if s]
        if not sks:
            rows.append((GRADE.get(d['grade']), d['unitName'], orig, '(스킬 없음)', '이 유닛은 스킬이 붙어 있지 않다', '-', '스킬 없음', '-'))
            continue
        for g, s in [(g, skill_of(g)) for g in gs if skill_of(g)]:
            lv = (s.get('levels') or [{}])[0]
            has = bool(lv.get('effects'))
            sent = skill_sentence(s)
            src, ok = skill_source(s, [i for i, _ in ou], names)
            nm = clean_skill_name(s['skillName'])
            if nm.startswith('SkillData_'):
                nm = '(이름 없는 원작 더미 능력)'
            desc = (s.get('description') or '')
            if has:
                state = '구현'
            else:
                state = '미구현(효과 빔)'
                fs = unit_field_state(nm, d)
                if '이름만' in desc or '미정' in desc:
                    sent = '효과 사양이 없다(이름만 달린 스킬)'
                elif fs:
                    sent, state = fs, '구현(유닛 데이터)'
                else:
                    sent = '효과 칸이 비어 있다(이름·표시만. 수치가 유닛 데이터나 다른 스킬에서 처리되는지 확인 못 함)'
            if s['_file'].startswith('SkillData_S2_'):
                m = re.search(r'_(A[0-9A-Za-z]{3})_\d+$', s['_file'])
                src = '원작 능력 %s · S2 2.323 툴팁' % (m.group(1) if m else '')
            fx = vfx.get(g) or ('원작 이펙트 대기(blender·반입 전)' if s['_file'] in vfx_pend else '없음')
            rows.append((GRADE.get(d['grade']), d['unitName'], orig, nm or s['_file'], sent, src, state, fx))
    return rows


# ───────────────────────── 엑셀 쓰기 ─────────────────────────

HEAD_FILL = PatternFill('solid', fgColor='1F3864')
HEAD_FONT = Font(bold=True, color='FFFFFF')
TOP_FONT = Font(bold=True)


def write_sheet(ws, headline, header, rows, widths, merge_cols=()):
    ws.append([headline])
    ws['A1'].font = TOP_FONT
    ws.append(header)
    for c in range(1, len(header) + 1):
        cell = ws.cell(row=2, column=c)
        cell.fill = HEAD_FILL
        cell.font = HEAD_FONT
        cell.alignment = Alignment(horizontal='center', vertical='center', wrap_text=True)
    for r in rows:
        ws.append(list(r))
    for i, w in enumerate(widths, 1):
        ws.column_dimensions[get_column_letter(i)].width = w
    for row in ws.iter_rows(min_row=3, max_row=ws.max_row):
        for cell in row:
            cell.alignment = Alignment(vertical='top', wrap_text=True)
    ws.freeze_panes = 'A3'
    ws.auto_filter.ref = 'A2:%s%d' % (get_column_letter(len(header)), max(ws.max_row, 2))
    # 같은 (등급,이름) 연속 행은 병합
    if merge_cols:
        start = 3
        for r in range(4, ws.max_row + 2):
            same = r <= ws.max_row and all(ws.cell(row=r, column=c).value == ws.cell(row=start, column=c).value for c in merge_cols[:2])
            if not same:
                if r - 1 > start:
                    for c in merge_cols:
                        ws.merge_cells(start_row=start, start_column=c, end_row=r - 1, end_column=c)
                start = r


def fixed_props(wb):
    # 같은 에셋이면 같은 파일이 나오게 문서 속성의 시각을 고정한다
    wb.properties.created = wb.properties.modified = datetime.datetime(2026, 1, 1)
    wb.properties.creator = 'G.R.D'


def save(wb, path):
    fixed_props(wb)
    wb.save(path)
    # zip 안 파일 시각도 고정
    import zipfile
    tmp = path + '.tmp'
    with zipfile.ZipFile(path) as zi, zipfile.ZipFile(tmp, 'w', zipfile.ZIP_DEFLATED) as zo:
        for info in zi.infolist():
            ni = zipfile.ZipInfo(info.filename, (2026, 1, 1, 0, 0, 0))
            ni.compress_type = zipfile.ZIP_DEFLATED
            data = zi.read(info.filename)
            if info.filename == 'docProps/core.xml':  # openpyxl이 저장 시각을 지금으로 덮어쓴다
                data = re.sub(rb'(<dcterms:(?:created|modified)[^>]*>)[^<]*', rb'\g<1>2026-01-01T00:00:00Z', data)
            zo.writestr(ni, data)
    os.replace(tmp, path)


def main():
    os.makedirs(OUT, exist_ok=True)
    head = git_head()
    roster = load_roster()
    base = '기준: 커밋 %s · 로스터 %d기(시스템 유닛 제외)' % (head, len(roster))

    # 1) 미구현
    ur = unimplemented_rows(roster)
    ur.sort(key=lambda r: (GRADE_ORDER.index(r[0]), r[2]))
    wb = Workbook()
    ws = wb.active
    ws.title = '미구현 스킬 유닛'
    write_sheet(ws, base + ' · 스킬이 안 도는 유닛 %d기 (스킬 effects가 하나도 없음)' % len(ur),
                ['등급', '우리 이름(unitName)', '에셋 파일', '사유'],
                [(GRADE[g], n, f, r) for g, n, f, r in ur], [12, 22, 28, 70])
    pr = partial_rows(roster)
    pr.sort(key=lambda r: (GRADE_ORDER.index(r[0]), r[2]))
    ws2 = wb.create_sheet('참고-일부 스킬만 빔')
    write_sheet(ws2, base + ' · 참고: 스킬 중 일부만 효과가 비어 있는 유닛 %d기 (유닛 전체 미구현은 아님)' % len(pr),
                ['등급', '우리 이름(unitName)', '에셋 파일', '효과 있는 스킬 수', '효과 빈 스킬 수', '효과 빈 스킬 이름'],
                [(GRADE[g], n, f, a, b, c) for g, n, f, a, b, c in pr], [12, 22, 28, 12, 12, 70])
    p1 = os.path.join(OUT, 'G.R.D 미구현 스킬 유닛.xlsx')
    save(wb, p1)

    # 2) 매칭
    audit = load_audit()
    master = load_master_map()
    names = unit_names()
    mr = match_rows(roster, audit, master, names)
    wb = Workbook()
    ws = wb.active
    ws.title = '전체'
    write_sheet(ws, base + ' · 구랜디-원랜디 매칭 (원작 유닛 = 능력치 가져온 원작 유닛 기준)',
                ['등급', '우리 이름', '원작 유닛 이름(원작 id)', '스킬 이름', '스킬 설명', '스킬 출처', '구현 상태', '이펙트'],
                mr, [12, 20, 38, 30, 80, 34, 16, 22])
    p2 = os.path.join(OUT, 'G.R.D 구랜디-원랜디 매칭.xlsx')
    save(wb, p2)

    unv_eff = sum(1 for r in mr if '확인 못 함' in r[4])
    unv_src = sum(1 for r in mr if '확인 못 함' in r[5])
    unv_orig = sum(1 for r in mr if '원작 대응 없음' in r[2] or 'id 미상' in r[2])
    print(json.dumps({'file1': p1, 'rows1': len(ur), 'partial': len(pr), 'file2': p2, 'rows2': len(mr),
                      'units2': len(roster), 'unverified_effect_cells': unv_eff, 'unverified_source_cells': unv_src, 'no_original_rows': unv_orig}, ensure_ascii=False))


if __name__ == '__main__':
    main()
