#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""S2 2.323 툴팁(Ubertip) → SkillData 효과 변환기(재매칭 2단계, 사장님 10-18 「스킬은 툴팁 수치 그대로」).

툴팁은 사람이 쓴 문장이라 정해진 꼴만 옮긴다. 못 옮긴 문장은 버리지 않고 「미옮김」으로 돌려준다(지어내지 않음).

한 능력(ability) → 스킬 블록 여러 개:
  · 「기본효과/추가효과」(발동조건 없는 문단) → 상시 오라(방깎·이속 감소·아군 공속/공격력·자기 공격력) 또는 평타 부속(매 공격 범위 피해·크리티컬)
  · 「발동조건」 문단마다 → 확률(공격시 N%)/게이지(마나·체력 N)/쿨(쿨타임 N초) 발동 스킬 하나
"""
import re

COLOR = re.compile(r'\|[cC][0-9A-Fa-f]{8}|\|[rR]')


def first_level(raw):
    """Ubertip="lv1","lv2" → lv1 원문."""
    raw = raw or ''
    if raw.startswith('"'):
        m = re.match(r'"((?:[^"]|"")*)"', raw)
        if m:
            return m.group(1)
    return raw


def kor_num(s):
    s = (s or '').replace(',', '')
    total = 0.0
    for v, unit in re.findall(r'(\d+(?:\.\d+)?)\s*(억|만)?', s):
        total += float(v) * (1e8 if unit == '억' else 1e4 if unit == '만' else 1)
    return total


def paragraphs(raw):
    """원문 → [(제목, [(키, 값)…])…]. 제목 없는 첫 문단은 '' ."""
    text = first_level(raw)
    out = []
    for para in text.split('|n|n'):
        lines = [COLOR.sub('', ln).strip() for ln in para.split('|n')]
        lines = [ln for ln in lines if ln]
        if not lines:
            continue
        title = ''
        items = []
        for ln in lines:
            m = re.match(r'^◎\s*([^:]+?)\s*:\s*(.*)$', ln)
            if m:
                items.append([m.group(1).strip(), m.group(2).strip()])
            elif ln.startswith('▶') or ln.startswith('　▶'):
                if items:
                    items[-1][1] += ' ▶ ' + ln.lstrip('　▶ ').strip()
            elif not items:
                title = ln
            elif items:
                items[-1][1] += ' ' + ln
        out.append((title, items))
    # 제목만 있는 문단(「패기 발산」)은 다음 문단의 제목으로
    merged = []
    pend = ''
    for title, items in out:
        if not items:
            pend = title
            continue
        merged.append((title if items and title and not items else (pend or title), items))
        pend = ''
    return merged


def dmg_type(txt):
    if '물리' in txt:
        return 1
    if '마법' in txt or '폭발형' in txt:
        return 2
    return 0


class Block:
    def __init__(self, name):
        self.name = name
        self.trigger = None       # 0 평타 확률 · 1 쿨 자동 · 2 오라 · 3 게이지
        self.chance = 1.0
        self.cooldown = 0.0
        self.gauge = None         # ('mana'|'life', N)
        self.range = 0.0
        self.effects = []
        self.lost = []

    def as_dict(self):
        return dict(name=self.name, trigger=self.trigger, chance=self.chance, cooldown=self.cooldown, gauge=self.gauge, range=self.range, effects=self.effects)


def eff(kind, target, mult, **kw):
    d = dict(kind=kind, target=target, multiplier=mult)
    d.update(kw)
    return d


KIND = dict(Damage=0, Stun=1, ArmorBreak=2, ArmorBonus=4, ApplyBuff=6, AttackPowerBuffFlat=11, AttackSpeedBuffPercent=12, Slow=13,
            AttackPowerBuffPercent=14, DamageOverTime=26, GrantRandomWisp=52)
T_SELF, T_ALLY, T_ENEMIES, T_SINGLE = 0, 1, 2, 3


def parse_range(v):
    m = re.search(r'(\d+)\s*범위', v) or re.search(r'^(\d+)', v.strip()) or re.search(r'(\d+)', v)
    return float(m.group(1)) if m else 0.0


def parse_damage(v, blk, unit_atk_type):
    """「데미지 : …」 값 → 효과들. 못 읽는 부분은 lost."""
    out = []
    main, *extras = re.split(r'\s*▶\s*', v)
    tick = re.match(r'^\s*([\d.]+)초(?:당|마다)\s*(.*)$', main)
    if tick:
        # 「0.2초당 400 마법데미지」 → 1초마다 5배(같은 초당 피해)
        n = kor_num(tick.group(2).split('데미지')[0])
        if n > 0:
            if blk.trigger is None:
                blk.trigger, blk.cooldown = 1, 1.0
            per = 1.0 / max(0.01, float(tick.group(1)))
            return [eff(KIND['Damage'], T_ENEMIES if blk.range else T_SINGLE, n * per if blk.trigger == 1 else n, damageType=dmg_type(main) or 2)]
    per_sec = '초당' in main or '타수당' in main
    rnd = re.search(r'\(x([\d.]+)~([\d.]+)\)', main)
    for part in re.split(r'\s+\+\s+|\s\+', main):
        part = part.strip()
        if not part:
            continue
        hp = re.search(r'(전체|최대|현재)체력\s*(\d+(?:\.\d+)?)%', part)
        strm = re.search(r'힘\s*x\s*([\d만억,\s]+)', part)
        dt = dmg_type(part) or dmg_type(main)
        fixed = '고정' in part
        explosive = '폭발형' in part
        if hp:
            basis = 2 if hp.group(1) == '현재' else 1
            out.append(eff(KIND['Damage'], T_ENEMIES if blk.range else T_SINGLE, float(hp.group(2)) / 100, basis=basis, damageType=dt or 2, fixedDamage=fixed, explosive=explosive))
            continue
        if strm:
            out.append(eff(KIND['Damage'], T_ENEMIES if blk.range else T_SINGLE, kor_num(strm.group(1)), basis=9, damageType=dt or 2))
            part = part[:strm.start()]
        n = kor_num(re.sub(r'\(x[\d.]+~[\d.]+\)', '', part.split('데미지')[0]).replace('초당', '').replace('타수당', ''))
        if n <= 0:
            if part and not strm:
                blk.lost.append('데미지 「%s」' % part)
            continue
        e = eff(KIND['Damage'], T_ENEMIES if blk.range else T_SINGLE, n, damageType=dt or 2, fixedDamage=fixed, explosive=explosive)
        if rnd:
            e['randMin'], e['randMax'] = float(rnd.group(1)), float(rnd.group(2))
        if per_sec:
            e['perSec'] = True
        out.append(e)
    for x in extras:
        m = re.search(r'단일 적(?:에게|은)?\s*(?:추가\s*)?([\d만억,\s]+)\s*(?:추가\s*)?(물리|마법)?\s*데미지', x)
        pct = re.search(r'단일 적(?:에게는|에게)?\s*(\d+)%\s*추가\s*(물리|마법)?\s*데미지', x)
        hpx = re.search(r'(전체|최대|현재)체력\s*(\d+(?:\.\d+)?)%\s*추가', x)
        if pct and out:
            base = next((o for o in out if o['basis'] == 0 if 'basis' in o), None) if False else next((o for o in out if o.get('basis', 0) == 0), None)
            if base:
                out.append(eff(KIND['Damage'], T_SINGLE, base['multiplier'] * float(pct.group(1)) / 100, damageType=2 if pct.group(2) != '물리' else 1))
                continue
        if m and kor_num(m.group(1)) > 0:
            out.append(eff(KIND['Damage'], T_SINGLE, kor_num(m.group(1)), damageType=2 if (m.group(2) or '마법') == '마법' else 1))
            continue
        if hpx:
            e = eff(KIND['Damage'], T_SINGLE, float(hpx.group(2)) / 100, basis=2 if hpx.group(1) == '현재' else 1, damageType=2, explosive='폭발형' in x)
            if '라인' in x:
                e['targetCondition'], e['targetConditionValue'] = 1, 200   # 라인 몬스터 = 포인트값 200 미만
            elif '광폭화' in x:
                e['requiredTargetBuffId'] = 'B06B'
            out.append(e)
            continue
        blk.lost.append('추가 데미지 「%s」' % x.strip())
    return out


def parse_extra(v, blk, quiet=False):
    """「추가효과」(발동 스킬 안) → 스턴·이속감소·방깎·아머브레이크. 나머지는 lost."""
    out = []
    area = '해당 범위' in v or '범위' in v and '단일' not in v
    tgt = T_ENEMIES if (area and blk.range) else T_SINGLE
    used = False
    m = re.search(r'(\d+(?:\.\d+)?)초\s*스턴(?:\s*\(보스\s*(\d+(?:\.\d+)?)초\))?', v)
    if m:
        e = eff(KIND['Stun'], T_SINGLE if '단일' in v.split('스턴')[0][-15:] else tgt, 0, duration=float(m.group(1)))
        if m.group(2):
            e['heroDuration'] = float(m.group(2))
        out.append(e)
        used = True
    vv = v.replace('이동 속도', '이동속도')
    m = re.search(r'(\d+(?:\.\d+)?)초간\s*이동속도\s*(\d+(?:\.\d+)?)%\s*감소', vv) or re.search(r'이동속도(?:를)?\s*(\d+(?:\.\d+)?)초간\s*(\d+(?:\.\d+)?)%\s*감소', vv)
    m2 = None if m else re.search(r'이동속도\s*(\d+(?:\.\d+)?)%\s*감소.*?(\d+(?:\.\d+)?)초', vv)
    if m or m2:
        mm = m or m2
        a, b = float(mm.group(1)), float(mm.group(2))
        dur, pct = (a, b) if m else (b, a)
        out.append(eff(KIND['Slow'], tgt, 1 - pct / 100, duration=dur))
        used = True
    m = re.search(r'(\d+)의\s*아머브레이크', v)
    if m:
        out.append(eff(KIND['ArmorBreak'], tgt, float(m.group(1))))
        used = True
    m = re.search(r'방어력\s*(\d+)\s*(?:추가\s*)?감소', v)
    if m and '아머브레이크' not in v and '보스' not in v:
        out.append(eff(KIND['ArmorBreak'], tgt, float(m.group(1))))
        used = True
    m = re.search(r'마법\s*데미지\s*(\d+)%\s*증폭', v.replace('마법데미지', '마법 데미지')) or re.search(r'(\d+)%의\s*마법\s*데미지\s*증폭', v.replace('마법데미지', '마법 데미지'))
    if m:
        out.append(eff(9, T_ENEMIES if ('해당 범위' in v and blk.range) else T_SINGLE, float(m.group(1))))   # AisrStack — 마뎀증폭 1레벨 = 1%
        used = True
    m = re.search(r'폭발형\s*데미지\s*(\d+)%\s*증폭', v)
    if m:
        out.append(eff(10, T_ENEMIES if ('해당 범위' in v and blk.range) else T_SINGLE, round(float(m.group(1)) / 5)))   # A11SStack — 1레벨 = 5%
        used = True
    m = re.search(r'([\d만억][\d만억,\s]*)\s*추가\s*(물리|마법)\s*데미지', v)
    if m and '보스' not in v and '스토리' not in v:
        out.append(eff(KIND['Damage'], T_SINGLE, kor_num(m.group(1)), damageType=1 if m.group(2) == '물리' else 2))
        used = True
    m = re.search(r'(\d+(?:\.\d+)?)초간\s*초당\s*([\d만억,\s]+)\s*(물리|마법)?\s*데미지', v)
    if m:
        out.append(eff(KIND['DamageOverTime'], T_SINGLE, kor_num(m.group(2)), duration=float(m.group(1)), damageType=1 if m.group(3) == '물리' else 2))
        used = True
    rest = re.sub(r'\d+(?:\.\d+)?초\s*스턴(\s*\([^)]*\))?|\d+(?:\.\d+)?초간\s*이동속도\s*\d+(?:\.\d+)?%\s*감소|이동속도(?:를)?\s*\d+(?:\.\d+)?초간\s*\d+(?:\.\d+)?%\s*감소|\d+의\s*아머브레이크\s*(\(최대[^)]*\))?|방어력\s*\d+\s*(?:추가\s*)?감소|[\d만억][\d만억,\s]*\s*추가\s*(?:물리|마법)\s*데미지|\d+(?:\.\d+)?초간\s*초당\s*[\d만억,\s]+\s*(?:물리|마법)?\s*데미지|마법\s*데미지\s*\d+%\s*증폭|\d+%의\s*마법\s*데미지\s*증폭|폭발형\s*데미지\s*\d+%\s*증폭|스킬에 적중당한 적들의|해당 범위|단일 적|및|\+|,|에게|공격시|적들의|의', '', vv.replace('마법데미지', '마법 데미지')).strip()
    if rest and len(rest) > 1 and not quiet:
        blk.lost.append('추가효과 「%s」' % v.strip())
    return out


def onhit_block(v, name):
    """「공격시 15% 확률 단일 적 2만 5000 물리데미지 및 0.9초 스턴」·「매 공격시 2000 고정데미지」·「공격시 375범위 기본 공격 피해량의 100% 마법데미지」 → 평타 발동 블록."""
    if '크리티컬' in v or not re.search(r'공격\s*시|공격시마다', v):
        return None
    m = re.search(r'(\d+(?:\.\d+)?)%\s*확률', v)
    b = Block(name)
    b.trigger, b.chance = 0, (float(m.group(1)) / 100 if m else 1.0)
    rg = re.search(r'(\d+)\s*범위', v)
    b.range = float(rg.group(1)) if rg else 0.0
    tgt = T_ENEMIES if b.range else T_SINGLE
    pct = re.search(r'기본\s*공격\s*피해량의\s*(\d+)%(?:\s*\+\s*([\d만억,\s]+)\s*추가)?', v)
    dot = re.search(r'(\d+(?:\.\d+)?)초간\s*초당\s*([\d만억,\s]+)\s*(물리|마법)?', v)
    if pct:
        e = eff(KIND['Damage'], tgt, float(pct.group(1)) / 100, basis=3, damageType=dmg_type(v) or 2)
        if pct.group(2):
            e['bonus'] = kor_num(pct.group(2))
        b.effects.append(e)
    elif dot:
        b.effects.append(eff(KIND['DamageOverTime'], T_SINGLE, kor_num(dot.group(2)), duration=float(dot.group(1)), damageType=1 if dot.group(3) == '물리' else 2))
    else:
        flat = re.search(r'(?:범위\s*)?(?:단일 적(?:에게)?\s*)?([\d만억][\d만억,\s]*)\s*(?:추가\s*)?(물리|마법|고정)\s*데미지', v)
        if flat:
            e = eff(KIND['Damage'], tgt, kor_num(flat.group(1)), damageType=1 if flat.group(2) == '물리' else 2, fixedDamage=flat.group(2) == '고정')
            b.effects.append(e)
    b.effects += parse_extra(re.sub(r'[\d만억,\s]+\s*(?:추가\s*)?(?:물리|마법|고정)\s*데미지', '', v), b, quiet=True)
    return b if b.effects else None


def parse_passive(key, v, ability_id, blk_list, roster_patch, lost):
    """발동조건 없는 「기본효과/추가효과」 한 줄 → 오라 블록·로스터 패치."""
    m = re.search(r'(\d+)\s*범위\s*적\s*방어력\s*(\d+)\s*감소', v)
    handled = False
    if m:
        b = Block('오라: 적 방어 −%s' % m.group(2))
        b.trigger, b.range = 2, float(m.group(1))
        b.effects.append(eff(KIND['ArmorBonus'], T_ENEMIES, -float(m.group(2)), buffId=ability_id + 'D'))
        blk_list.append(b)
        handled = True
    m = re.search(r'(\d+)\s*범위\s*(?:적\s*)?(?:유닛\s*)?이동속도\s*(\d+(?:\.\d+)?)%\s*감소', v)
    if m:
        b = Block('오라: 적 이속 −%s%%' % m.group(2))
        b.trigger, b.range = 2, float(m.group(1))
        b.effects.append(eff(KIND['Slow'], T_ENEMIES, 1 - float(m.group(2)) / 100))
        blk_list.append(b)
        handled = True
    m = re.search(r'(\d+)\s*범위\s*(?:내\s*)?아군(?:\s*유닛)?\s*공격속도\s*(\d+(?:\.\d+)?)%\s*증가', v)
    if m:
        b = Block('오라: 아군 공속 +%s%%' % m.group(2))
        b.trigger, b.range = 2, float(m.group(1))
        p = float(m.group(2)) / 100
        b.effects += ([] if '자신 제외' in v else [eff(KIND['AttackSpeedBuffPercent'], T_SELF, p, buffId=ability_id + 'S')]) + [eff(KIND['AttackSpeedBuffPercent'], T_ALLY, p, buffId=ability_id + 'S')]
        blk_list.append(b)
        handled = True
    m = re.search(r'(\d+)\s*범위\s*아군(?:\s*유닛)?\s*공격력\s*(\d+(?:\.\d+)?)%\s*증가', v)
    if m:
        b = Block('오라: 아군 공격력 +%s%%' % m.group(2))
        b.trigger, b.range = 2, float(m.group(1))
        p = float(m.group(2)) / 100
        b.effects += [eff(KIND['AttackPowerBuffPercent'], T_SELF, p, buffId=ability_id + 'P'), eff(KIND['AttackPowerBuffPercent'], T_ALLY, p, buffId=ability_id + 'P')]
        blk_list.append(b)
        handled = True
    m = re.search(r'(?:자신의?\s*)?공격력\s*([\d만억][\d만억,\s]*)\s*증가', v)
    if m:
        b = Block('자기 공격력 +%s' % m.group(1).strip())
        b.trigger = 2
        b.effects.append(eff(KIND['AttackPowerBuffFlat'], T_SELF, kor_num(m.group(1)), buffId=ability_id + 'F'))
        blk_list.append(b)
        handled = True
    m = re.search(r'(\d+)\s*범위\s*아군의?\s*마나\s*초당\s*([\d.]+)\s*증가', v)
    if m:
        roster_patch['manaAuraRegenPerSecond'] = float(m.group(2))
        roster_patch['manaAuraRange'] = float(m.group(1))
        roster_patch['manaAuraIncludesSelf'] = 1
        handled = True
    m = re.search(r'(\d+(?:\.\d+)?)%\s*확률(?:로)?\s*(\d+(?:\.\d+)?)배(?:의)?\s*크리티컬(?:\s*발동)?(?:\s*(?:및|\+)\s*([\d만억][\d만억,\s]*)\s*(?:물리|마법)?\s*데미지)?(?:.*?(\d+(?:\.\d+)?)초\s*스턴)?', v)
    if m and re.search(r'(보스|스토리)[^%]*에게[^%]*확률', v):
        m = None
    if m:
        roster_patch['critChance'] = float(m.group(1)) / 100
        roster_patch['critDamageMultiplier'] = round(float(m.group(2)) - 1, 4)   # 평타는 이미 들어갔으니 추가분(Y배 = 평타 + (Y−1)배)
        if m.group(3):
            roster_patch['critBonusDamage'] = kor_num(m.group(3))
        if m.group(4):
            roster_patch['critStunDuration'] = float(m.group(4))
        handled = True
    m = re.search(r'기본\s*공격시\s*(\d+)명의\s*적\s*동시\s*공격', v)
    if m:
        roster_patch['attackExtraTargets'] = int(m.group(1)) - 1
        roster_patch['attackExtraTargetRadius'] = 700
        handled = True
    m = re.search(r'(?:(\d+)\s*범위\s*)?아군(?:의)?\s*체력\s*초당\s*([\d.]+)\s*(?:회복|증가)', v)
    if m:
        roster_patch['lifeAuraRegenPerSecond'] = float(m.group(2))
        roster_patch['lifeAuraRange'] = float(m.group(1) or 800)
        roster_patch['lifeAuraIncludesSelf'] = 1
        handled = True
    if not handled and '크리티컬' not in v:
        ob = onhit_block(v, '평타 부속')
        if ob:
            blk_list.append(ob)
            handled = True
    if re.search(r'공중이동|스플래시|기본 공격력 매우 높음|평타 공격', v):
        handled = True
    if not handled:
        lost.append('%s 「%s」' % (key, v))


def convert(raw_uber, ability_id, name):
    """능력 하나 → (블록들, 로스터 패치, 미옮김 문장들)."""
    blocks, patch, lost = [], {}, []
    for title, items in paragraphs(raw_uber):
        keys = [k for k, _ in items]
        if '발동조건' in keys or '쿨타임' in keys and '사용효과' not in keys:
            b = Block(title if title and not title.rstrip().endswith(('다.', '다', '.')) else name)
            for k, v in items:
                if k == '발동조건':
                    m = re.search(r'공격시\s*(\d+(?:\.\d+)?)%\s*확률', v)
                    g = re.search(r'(마나|체력)\s*(\d+)', v)
                    if m:
                        b.trigger, b.chance = 0, float(m.group(1)) / 100
                    elif g:
                        b.trigger, b.gauge = 3, ('mana' if g.group(1) == '마나' else 'life', int(g.group(2)))
                    else:
                        b.lost.append('발동조건 「%s」' % v)
                    if '또는' in v:
                        b.lost.append('발동조건 둘 중 첫 것만 옮김 「%s」' % v)
                elif k in ('쿨타임', '절대쿨타임'):
                    b.cooldown = kor_num(v)
                    if b.trigger is None:
                        b.trigger = 1
                elif k == '범위':
                    b.range = parse_range(v)
                elif k in ('데미지', '추가데미지'):
                    b.effects += parse_damage(v, b, 0)
                elif k in ('추가효과', '여진', '현기증'):
                    pm = re.search(r'단일 적(?:에게)?\s*(\d+)%\s*추가\s*(물리|마법)?\s*데미지', v)
                    base = next((e for e in b.effects if e['kind'] == KIND['Damage'] and e.get('basis', 0) == 0), None)
                    if pm and base:
                        b.effects.append(eff(KIND['Damage'], T_SINGLE, base['multiplier'] * float(pm.group(1)) / 100, damageType=1 if pm.group(2) == '물리' else 2,
                                             **({'randMin': base['randMin'], 'randMax': base['randMax']} if 'randMin' in base else {})))
                        v = v[:pm.start()] + v[pm.end():]
                    b.effects += parse_extra(v, b)
                elif k == '지속시간':
                    dur = kor_num(v)
                    for e in b.effects:
                        if e.pop('perSec', False):
                            e['multiplier'] *= max(1.0, dur)
                elif k in ('기본효과',):
                    parse_passive(k, v, ability_id, blocks, patch, lost)
                elif k in ('시전시간', '발동대상', '유성개수', '추가설명'):
                    b.lost.append('%s 「%s」(무시)' % (k, v))
                else:
                    b.lost.append('%s 「%s」' % (k, v))
            for e in b.effects:
                e.pop('perSec', None)
            if any(re.search(r'보스\s*/\s*스토리\s*적용\s*X', vv) for _, vv in items):
                # 「보스 / 스토리 적용 X」 → 포인트값 200 미만(일반 적)만
                for e in b.effects:
                    if e['kind'] == KIND['Damage']:
                        e['targetCondition'], e['targetConditionValue'] = 1, 200
                b.lost = [x for x in b.lost if '적용 X' not in x]
            if b.trigger is None:
                b.lost.append('발동조건을 못 읽음')
            if b.effects and b.trigger is not None:
                blocks.append(b)
            elif not b.effects:
                b.lost.append('효과 없음')
            lost += ['%s: %s' % (b.name, x) for x in b.lost]
        else:
            for k, v in items:
                if k in ('기본효과', '추가효과', '추가효과2'):
                    if k == '추가효과2' or re.search(r'\d+(?:\.\d+)?%\s*확률로', v) and '크리티컬' not in v:
                        # 「35% 확률로 단일 적 36만 추가 물리데미지 및 1.5초 스턴」 같은 평타 부속 발동
                        m = re.search(r'(\d+(?:\.\d+)?)%\s*확률로\s*(.*)', v)
                        if m:
                            b = Block('%s(%s%%)' % (name, m.group(1)))
                            b.trigger, b.chance = 0, float(m.group(1)) / 100
                            dm = re.search(r'([\d만억,\s]+)\s*(?:추가\s*)?(물리|마법)\s*데미지', m.group(2))
                            if dm:
                                b.effects.append(eff(KIND['Damage'], T_SINGLE, kor_num(dm.group(1)), damageType=1 if dm.group(2) == '물리' else 2))
                            b.effects += parse_extra(m.group(2), b)
                            if b.effects:
                                blocks.append(b)
                                lost += ['%s: %s' % (b.name, x) for x in b.lost if '추가효과' not in x]
                                continue
                    parse_passive(k, v, ability_id, blocks, patch, lost)
                elif k == '사용효과' and re.search(r'[\d만억]\s*(?:물리|마법|고정)\s*데미지', v):
                    b = Block('%s(누르는 스킬)' % name)
                    cd = next((kor_num(v2) for k2, v2 in items if k2 == '쿨타임'), 0.0)
                    if not cd:
                        mcd = re.search(r'쿨타임\s*:?\s*(\d+(?:\.\d+)?)초', ' '.join(v2 for _, v2 in items))
                        cd = float(mcd.group(1)) if mcd else 0.0
                    b.trigger, b.cooldown = 5, cd
                    rg = re.search(r'(\d+)\s*범위', v)
                    b.range = float(rg.group(1)) if rg else 0.0
                    b.effects += parse_damage(v.split('및')[0], b, 0)
                    b.effects += parse_extra(v, b, quiet=True)
                    if b.effects:
                        blocks.append(b)
                elif k == '사용효과' and re.search(r'랜덤한\s*위습\s*1개\s*획득', v):
                    # 「떼쓰기」: 쿨마다 랜덤~희귀 위습 1개 → 쿨 자동 GrantRandomWisp(풀: 랜덤·흔함·안흔함·특별함·희귀함)
                    b = Block(name)
                    b.trigger = 1
                    b.cooldown = next((kor_num(v2) for k2, v2 in items if k2 == '쿨타임'), 90.0)
                    b.effects.append(eff(KIND['GrantRandomWisp'], T_SELF, 0, wispPool=['6ad4fde3d2a94d59be7d5d7665a73d2a', '78f2f9cf08054763ab23ce26a1020667',
                                                                                   'e610498c4baf4cc88aa036f9ce7b0799', '81eac83fa14e4fa8a0fefc992a32453b', 'e343898df3db474d85f1c7d1582a1334']))
                    blocks.append(b)
                elif k in ('사용효과', '쿨타임', '시전시간', '코스트', '제한사항'):
                    if k == '쿨타임' and any(k2 == '사용효과' and re.search(r'랜덤한\s*위습', v2) for k2, v2 in items):
                        continue
                    if k == '쿨타임' and any(k2 == '사용효과' and re.search(r'[\d만억]\s*(?:물리|마법|고정)\s*데미지', v2) for k2, v2 in items):
                        continue
                    lost.append('%s 「%s」(액티브·유틸 — 안 옮김)' % (k, v))
                else:
                    lost.append('%s 「%s」' % (k, v))
    return blocks, patch, lost
