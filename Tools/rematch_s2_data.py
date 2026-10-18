#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""재매칭(사장님 10-18 「처음부터 다시 매칭」) 자료층 — 최신 원랜디 S2 2.323을 정본으로 읽는다. 읽기 전용.

S2 2.323은 W3x2lni 슬크 저장본이다:
  · 유닛 수치 = MPQ units\\unitweapons·unitbalance·unitabilities·unitui·unitdata.slk (유닛 ID는 옛 판과 같음, 대소문자 구분: h03A 일반 / H05N 영웅)
  · 이름·툴팁 = units\\campaignunitstrings.txt · campaignabilitystrings.txt (한글 이름·「Ubertip」 설명에 발동조건·확률·범위·데미지가 적혀 있다)
  · 능력 슬크 = units\\abilitydata.slk (base 코드·Data 필드) · 트리거(war3map.j)는 난독화(함수·rawcode 변조)라 설명엔 툴팁을 쓴다
옛 판(11.089)은 Tools/w3x/원본/war3map_new.w3u(+.w3a)로 대조용으로만 읽는다.
S2 w3x는 HM3W 512바이트 머리를 떼면 MPQ다(첫 사용 때 ~/grd/GRD_rematch/s2.mpq로 풀어 둔다).
"""
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, os.path.join(HERE, 'w3x'))
import slk  # noqa: E402
import w3u  # noqa: E402
from mpqread import Archive  # noqa: E402

S2_W3X = os.path.join(HERE, 'w3x', '원본_S2_2.323', 'ORDR_S2_2.323[C].w3x')
WORK = os.path.expanduser('~/grd/GRD_rematch')
COLOR = re.compile(r'\|[cC][0-9A-Fa-f]{8}|\|[rR]')


def clean(s):
    s = COLOR.sub('', s or '')
    return s.replace('|n', ' / ').strip()


def open_s2():
    os.makedirs(WORK, exist_ok=True)
    p = os.path.join(WORK, 's2.mpq')
    if not os.path.exists(p):
        with open(S2_W3X, 'rb') as f:
            f.seek(512)
            data = f.read()
        with open(p, 'wb') as g:
            g.write(data)
    return Archive(p)


def parse_ini(text):
    d, cur = {}, None
    for ln in text.split('\n'):
        ln = ln.rstrip('\r')
        m = re.match(r'^\[(\w{4})\]$', ln)
        if m:
            cur = m.group(1)
            d[cur] = {}
        elif cur and '=' in ln:
            k, v = ln.split('=', 1)
            d[cur][k] = v
    return d


def num(v, default=0.0):
    try:
        return float(str(v).strip().rstrip('.') or default)
    except ValueError:
        return default


def load_s2():
    a = open_s2()
    t = {n: slk.parse(a.read('units\\%s.slk' % n))[1] for n in ('unitdata', 'unitbalance', 'unitui', 'unitweapons', 'unitabilities')}
    ability = slk.parse(a.read('units\\abilitydata.slk'))[1]
    ustr = parse_ini(a.read('units\\campaignunitstrings.txt').decode('utf-8', 'replace'))
    astr = parse_ini(a.read('units\\campaignabilitystrings.txt').decode('utf-8', 'replace'))
    for extra in ('commonabilitystrings',):
        try:
            for k, v in parse_ini(a.read('units\\%s.txt' % extra).decode('utf-8', 'replace')).items():
                astr.setdefault(k, v)
        except Exception:
            pass
    units = {}
    for k, ui in t['unitui'].items():
        if k[0] not in 'hH' or not re.match(r'^[hH][0-9A-Za-z]{3}$', k):
            continue
        w = t['unitweapons'].get(k, {})
        b = t['unitbalance'].get(k, {})
        ab = t['unitabilities'].get(k, {})
        s = ustr.get(k, {})
        name_full = clean(s.get('Name', ''))
        tag = name_full.rsplit(' - ', 1)[-1] if ' - ' in name_full else ''
        base_name = name_full.rsplit(' - ', 1)[0] if ' - ' in name_full else name_full
        dice, sides, plus = num(w.get('dice1', 1), 1), num(w.get('sides1', 1), 1), num(w.get('dmgplus1', 0))
        units[k] = dict(
            id=k, name=base_name, tag=tag, name_full=name_full, model=ui.get('file', ''),
            atk=plus + dice * (sides + 1) / 2, cool=num(w.get('cool1', 0)), range=num(w.get('rangeN1', 0)),
            atk_type=w.get('atkType1', ''), weap=w.get('weapTp1', ''), splash=num(w.get('Farea1', 0)),
            mana=num(b.get('manaN', 0)), def_type=b.get('defType', ''), level=b.get('level', ''),
            abil=[x for x in (ab.get('abilList') or '').split(',') if x and x not in ('Avul', '_', '-')],
            hero_abil=[x for x in (ab.get('heroAbilList') or '').split(',') if x and x not in ('_', '-')],
            art=s.get('Art', ''), tip=clean(s.get('Ubertip', '')),
        )
    return units, ability, astr, ustr


def load_s2_models():
    """모든 유닛(더미 e… 포함)의 모델 경로 — 트리거가 더미 유닛으로 이펙트를 낼 때 쓴다."""
    a = open_s2()
    return {k: dict(model=v.get('file', '')) for k, v in slk.parse(a.read('units\\unitui.slk'))[1].items()}


def load_old():
    out = {}
    for u in w3u.parse(os.path.join(HERE, 'w3x', '원본', 'war3map_new.w3u')):
        m = u['mods']
        if u['id'][0] not in 'hH' or m.get('ua1b') is None:
            continue
        name = clean(m.get('unam'))
        out[u['id']] = dict(
            id=u['id'], name_full=name, name=name.rsplit(' - ', 1)[0] if ' - ' in name else name,
            tag=name.rsplit(' - ', 1)[-1] if ' - ' in name else '',
            atk=m['ua1b'] + (m.get('ua1d') or 1) * ((m.get('ua1s') or 1) + 1) / 2,
            cool=m.get('ua1c') or 0.0, range=m.get('ua1r') or 0.0, atk_type=m.get('ua1t') or 'normal',
            weap=m.get('ua1w') or '', splash=m.get('ua1f') or 0.0, mana=m.get('umpm') or 0.0,
            abil=[x for x in (m.get('uabi') or '').split(',') if x and x != 'Avul'],
        )
    return out


def ability_info(aid, ability, astr):
    """한 능력의 이름·툴팁·아트·기반 코드(S2)."""
    s = astr.get(aid, {})
    r = ability.get(aid, {})
    return dict(
        id=aid, name=clean(s.get('Name', '')), tip=clean(s.get('Tip', '')), uber=clean(s.get('Ubertip', '')),
        base=r.get('code', ''), art={k: s[k] for k in ('Art', 'Casterart', 'Targetart', 'Specialart', 'Effectart', 'Areaeffectart', 'Missileart', 'Animnames', 'Buffart') if s.get(k) and s.get(k) != ','},
        data={k: r[k] for k in r if k.startswith(('Data', 'Dur', 'Cool', 'Area', 'Rng', 'targs')) and r[k] not in ('-', '_', '')},
    )


if __name__ == '__main__':
    U, AB, AS, US = load_s2()
    O = load_old()
    print('S2 H유닛', len(U), '옛 H유닛', len(O), '공통', len(set(U) & set(O)))
