#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""재매칭 1단계 — 우리 로스터 240기를 S2 2.323 원작 유닛에 다시 매칭하는 「제안표」를 만든다(사장님 10-18 「전체 유닛을 능력치+스킬이 같은 원작 유닛으로」).
읽기 전용: Assets를 건드리지 않는다. 적용은 2단계(사장님 승인 뒤).

사용(저장소 루트): <openpyxl·pyyaml 있는 python> Tools/rematch_s2.py [출력폴더=~/grd/GRD_rematch]
  → 재매칭_제안.xlsx(시트: 제안 · 원작 중복·부족 · 이펙트 작업 목록) · 재매칭_요약.md · rematch.json

규칙(PM 지시):
  · 기본은 지금 능력치 원작(SKILL_STAT_SOURCE_AUDIT·MASTER_UID)을 그대로. 그 원작이 S2에 없거나 등급이 안 맞거나 다른 로스터가 같이 쓰면
    같은 등급 안에서 「아직 아무도 안 쓰는」 S2 원작으로 새로 매칭(DPS가 가장 가까운 것). 한 원작은 한 로스터가 원칙, 모자라면 표시.
  · 스킬은 새 원작의 능력(abilList)에서 온다 — S2 툴팁(Ubertip)이 한 줄 설명. 사장님 사양 스킬(SkillData_사장님_*)은 그대로.
  · 수치(공격력·공속·사거리·공격타입·마나)는 S2 슬크 값, 옛 11.089 값과 대조해 바뀐 값을 표시.
"""
import csv
import glob
import importlib.util
import json
import os
import re
import sys
from collections import Counter, defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, HERE)
import rematch_s2_data as D  # noqa: E402

_spec = importlib.util.spec_from_file_location('ex', os.path.join(HERE, 'export_unit_sheets.py'))
EX = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(EX)

OUT = os.path.expanduser(sys.argv[1] if len(sys.argv) > 1 else '~/grd/GRD_rematch')

# 로스터 등급 → 받을 수 있는 S2 등급 태그(이름 뒤 「- 태그」). 태그 없는 대문자 H 영웅은 옛 판 이름 태그로 등급을 따른다.
ALLOWED = {
    0: {'흔함'}, 1: {'안흔함'}, 2: {'특별'}, 3: {'희귀'}, 4: {'히든', '왜곡'}, 5: {'전설'}, 6: {'제한'}, 7: {'초월'},
    8: {'불멸'}, 9: {'영원'}, 10: {'랜덤전용', '신비'}, 11: {'신비'}, 12: {'특수'}, 14: {'변화', '왜곡'},
}
# 새로 고를 때의 우선순위(앞 묶음이 다 차면 다음 묶음). 대문자 H 영웅(사람 이름 변형)은 초월·불멸·영원 풀에만 넣는다.
TIERS = {4: [{'히든'}, {'왜곡'}], 10: [{'랜덤전용'}, {'신비'}], 14: [{'변화'}, {'왜곡'}]}
HERO_GRADES = {7, 8, 9}
OLD_TAG = {'초월함': '초월', '불멸의': '불멸', '영원한': '영원', '전설적인': '전설', '희귀함': '희귀', '특별함': '특별', '제한됨': '제한', '특수함': '특수',
           '변화된': '변화', '변화됨': '변화', '랜덤전용': '랜덤전용', '히든': '히든', '흔함': '흔함', '안흔함': '안흔함'}


CODE_TAG = {'01': '흔함', '02': '안흔함', '03': '특별', '04': '희귀', '05': '전설', '06': '히든', '07': '변화', '08': '랜덤전용', '09': '제한', '10': '특수',
            '11': '초월', '12': '불멸', '13': '영원'}
PREFIX_TAG = [('흔함_', '흔함'), ('안흔함_', '안흔함'), ('특별함_', '특별'), ('희귀함_', '희귀'), ('전설적인_', '전설'), ('히든_', '히든'), ('변화됨_', '변화'),
              ('랜덤_', '랜덤전용'), ('다른세계_', '신비'), ('제한_', '제한'), ('특수함_', '특수'), ('초월_', '초월'), ('불멸_', '불멸'), ('영원_', '영원')]
S2_UNITS_JSON = os.path.expanduser('~/grd/GRD_motion_trial/s2_skin/units.json')   # Tools/w3x/s2_units.py 산출(모델 토큰으로 등급 추정)


def s2_grade(u, old, master_u, s2json):
    """S2 유닛의 등급 태그와 근거. 이름 태그 → (태그 없는 대문자 H 영웅) 우리 대응표 접두 → s2_units.py 추정. 소문자 무태그는 소환·더미라 제외."""
    t = u['tag']
    if t in ('전설-소환됨', '불멸 소환됨', '제한 (소환됨)', '신비 분신'):
        return '(소환·분신)', '이름 태그'
    if t and re.match(r'^[0-9]+$', t):
        return '(PUNK)', '이름 태그'
    if t in ALL_TAGS:
        return t, '이름 태그'
    if t:
        return '(기타:%s)' % t, '이름 태그'
    if u['id'][0] != 'H':
        return '(무태그 소환·더미)', '-'
    for r in master_u.get(u['id'].upper(), []):
        for pre, tag in PREFIX_TAG:
            if r.startswith(pre):
                return tag, '대응표 접두(%s)' % r
    j = s2json.get(u['id'])
    if j and j.get('grade') in CODE_TAG:
        return CODE_TAG[j['grade']], ('모델 토큰 추정' if j.get('gradeEstimated') else '옛 판 등급')
    return '(미분류)', '-'


ALL_TAGS = {'흔함', '안흔함', '특별', '희귀', '히든', '왜곡', '전설', '제한', '초월', '불멸', '영원', '랜덤전용', '신비', '특수', '변화', '세라핌'}


def parse_audit():
    fs = sorted(glob.glob(ROOT + '/Docs/reference/SKILL_STAT_SOURCE_AUDIT_*.csv'))
    rows = {}
    for x in list(csv.reader(open(fs[-1], encoding='utf-8-sig')))[1:]:
        ids = [i.upper() for i in re.findall(r'(?:^|/)([HEhe][0-9A-Za-z]{3}) ', x[5])]
        owners = [i.upper() for i in re.findall(r'(?<![0-9A-Za-z])([HEhe][0-9A-Za-z]{3})(?![0-9A-Za-z])', x[8])]
        rows[x[1]] = dict(grade=x[0], atk=x[2], spd=x[3], how=x[4], stat_ids=ids, skill_owner_ids=owners, verdict=x[9])
    return rows




# ───────────────────────── 스킬(능력) 한 줄 ─────────────────────────

TAG_ONLY_BASES = {'AIde', 'Aloc', 'Avul', 'Abun', 'Amov'}


def ability_view(aid, AB, AS):
    a = D.ability_info(aid, AB, AS)
    nm = re.sub(r'\s*::\s*Lv\.?\s*\d+\s*$', '', a['tip']).strip() or re.sub(r'^[!@#$]+\d*_?', '', a['name']).strip()
    uber = a['uber']
    parts = [p.strip(' /') for p in re.findall(r'◎([^◎]+)', uber)]
    head = re.split(r'◎', uber)[0].strip(' /')
    # ◎ 앞의 굵은 소제목(「패기 발산」 등)이 있으면 그것도 살린다
    desc = ' · '.join(p.replace(' / ', ' ') for p in parts) if parts else head
    ui_only = nm.startswith(('창고로 이동', '특성강화', '유닛 판매', '판매')) or uber.startswith('사용시 유닛을 창고')
    tag_only = ui_only or a['name'].startswith('[') or a['name'] in ('판매',) or uber.startswith('◈') or (not uber and not a['tip']) or (a['name'].startswith(('@', '#')) and not uber) or (a['base'] in TAG_ONLY_BASES and not uber)
    if not desc and not tag_only:
        desc = '설명 미확정'
    return dict(id=aid, name=nm or aid, desc=desc, base=a['base'], tag_only=tag_only, raw_name=a['name'], art=a['art'], data=a['data'])


# ───────────────────────── 이펙트(원작 아트) ─────────────────────────

ART_KEYS = ('Casterart', 'Targetart', 'Specialart', 'Effectart', 'Areaeffectart', 'Missileart')
DEFAULT_ROOTS = ('abilities\\', 'objects\\', 'units\\', 'doodads\\', 'environment\\', 'sharedmodels\\', 'ui\\', 'buildings\\')


def split_models(v):
    return [m.strip().strip('"') for m in re.split(r',', v or '') if m.strip() and m.strip() not in (',', '_', '-') and re.search(r'\.md[lx]$', m.strip(), re.I)]


def j_index(units_s2):
    """S2 war3map.j(난독화)에서 함수마다 문자열 모델·능력 rawcode·유닛 rawcode를 모아 능력→모델, 유닛→모델 표를 만든다(같은 함수 안 = 근사)."""
    j = open(os.path.join(HERE, 'w3x', '원본_S2_2.323', '풀린것', 'war3map.j'), encoding='utf-8', errors='replace').read()
    amap, umap = defaultdict(set), defaultdict(set)
    dec = lambda h: bytes.fromhex(h).decode('latin1')
    for _name, body in re.findall(r'function (\w+) takes [^\n]*\n(.*?)\nendfunction', j, re.S):
        models = set(m.replace('\\\\', '\\') for m in re.findall(r'"([^"]+\.md[lx])"', body, re.I))
        for e in re.findall(r'\$(65[0-9A-F]{6})\b', body):   # 더미 유닛(e…)의 모델
            eu = units_s2.get(dec(e))
            if eu and eu.get('model'):
                models.add(eu['model'])
        if not models:
            continue
        for a in re.findall(r'\$(41[0-9A-F]{6})\b', body):
            amap[dec(a)] |= models
        for u in re.findall(r'\$((?:68|48)[0-9A-F]{6})\b', body):
            umap[dec(u)] |= models
    return amap, umap


def model_status_factory(arch):
    ours = set()
    base = os.path.join(ROOT, 'Assets', 'Art', 'Effects', 'Original')
    if os.path.isdir(base):
        ours = {n.lower() for n in os.listdir(base) if not n.endswith('.meta')}
    wh = {}
    whp = os.path.expanduser('~/Desktop/구랜디스킨모음/16_원랜디스킬/index.csv')
    if os.path.exists(whp):
        for r in csv.DictReader(open(whp, encoding='utf-8-sig')):
            wh[os.path.splitext(r['원작 모델 파일'].lower())[0]] = r
    cache = {}

    def status(path):
        p = path.replace('\\\\', '\\')
        stem = os.path.splitext(os.path.basename(p.replace('\\', '/')))[0]
        key = stem.lower()
        if key in cache:
            return cache[key]
        in_map = False
        for c in (re.sub(r'\.md[lx]$', '.mdx', p, flags=re.I), stem + '.mdx', 'war3mapImported\\' + stem + '.mdx'):
            try:
                if arch.read(c):
                    in_map = True
                    break
            except Exception:
                pass
        default = p.lower().startswith(DEFAULT_ROOTS) and not in_map
        w = wh.get(key)
        if key in ours:
            st = '이미 들어와 있음'
        elif default:
            st = '없음(기본 War3 모델·맵 밖 → blender 새로 제작)'
        elif w and w.get('변환') == '변환됨' and w.get('부품 수 검사') == '통과' and (w.get('파티클 줄기(PRE2, JSON만)') in ('0', '')) and (w.get('리본(미변환)') in ('0', '')):
            st = '꺼내기만 하면 됨'
        elif w:
            why = []
            if w.get('변환') != '변환됨':
                why.append('변환 ' + (w.get('변환') or '?'))
            if w.get('부품 수 검사') not in ('통과', None):
                why.append('부품 검사 ' + (w.get('부품 수 검사') or '?'))
            if w.get('파티클 줄기(PRE2, JSON만)') not in ('0', '', None):
                why.append('파티클 %s' % w['파티클 줄기(PRE2, JSON만)'])
            if w.get('리본(미변환)') not in ('0', '', None):
                why.append('리본 %s' % w['리본(미변환)'])
            st = '꺼내도 깨짐(blender 손질: %s)' % ', '.join(why)
        elif in_map:
            st = '꺼내기만 하면 됨(S2 신규 모델·미검사)'
        else:
            st = '없음(blender 새로 제작)'
        cache[key] = (stem, st, 'S2 맵 내장' if in_map else ('기본 War3' if p.lower().startswith(DEFAULT_ROOTS) else '맵에 없음'))
        return cache[key]
    return status


def our_vfx_state():
    """SkillVfxTable: 우리 스킬마다 붙은 이펙트의 출처(원작/Kenney/팩/기본)."""
    p = os.path.join(ROOT, 'Assets', 'Resources', 'Effects', 'SkillVfxTable.asset')
    t = open(p, encoding='utf-8').read()
    pre = re.search(r'  prefabs:\n((?:  - .*\n)+)', t)
    prefabs = re.findall(r'guid: (\w+)', pre.group(1)) if pre else []
    path_of = [EX.GUID.get(g) or '' for g in prefabs]
    prefab_paths = []
    gi = {}
    for m in glob.glob(ROOT + '/Assets/**/*.prefab.meta', recursive=True):
        g = re.search(r'guid: (\w+)', open(m, encoding='utf-8').read())
        if g:
            gi[g.group(1)] = m[:-5]
    prefab_paths = [gi.get(g, '') for g in prefabs]

    def src(i):
        pp = prefab_paths[i] if 0 <= i < len(prefab_paths) else ''
        if '/Original/' in pp or '/Effects/Original' in pp:
            return '원작'
        if 'Kenney' in pp:
            return 'Kenney 대체'
        if pp:
            return '팩 대체(' + pp.split('/')[2] + ')'
        return '?'
    out = {}
    for m in re.finditer(r'  - skill: \{fileID: \d+, guid: (\w+), type: 2\}\n((?:    .*\n)+)', t):
        kinds = []
        for slot in re.finditer(r'    (\w+):\n      prefab: (-?\d+)\n      kind: (-?\d+)', m.group(2)):
            pi, ki = int(slot.group(2)), int(slot.group(3))
            if pi >= 0:
                kinds.append(src(pi))
            elif ki >= 0:
                kinds.append('기본 이펙트(SkillVfx)')
        out[m.group(1)] = sorted(set(kinds))
    return out


# ───────────────────────── 본체 ─────────────────────────

NO_ORIGINAL_KEEP = True   # 지금 원작이 없는 로스터(사장님 창작·특수 지급)는 새 원작을 붙이지 않고 「대응 없음」 유지 — 사장님 확인 대상


def fmt(v):
    return '{:,.0f}'.format(v) if abs(v) >= 1000 else ('%.3g' % v)


def main():
    os.makedirs(OUT, exist_ok=True)
    U, AB, AS, US = D.load_s2()
    O = D.load_old()
    arch = D.open_s2()
    Uu = {k.upper(): k for k in U}
    Ou = {k.upper(): k for k in O}
    master_u = defaultdict(list)
    for r in csv.DictReader(open(ROOT + '/Docs/reference/MASTER_UID_ROSTER_MAP.csv', encoding='utf-8')):
        master_u[r['유닛ID'].upper()].append(r['로스터'])
    s2json = {x['id']: x for x in json.load(open(S2_UNITS_JSON))} if os.path.exists(S2_UNITS_JSON) else {}
    for u in U.values():
        u['grade'], u['grade_src'] = s2_grade(u, O, master_u, s2json)
    roster = EX.load_roster()
    audit = parse_audit()
    vfx_now = our_vfx_state()
    amap, umap = j_index(D.load_s2_models())
    mstatus = model_status_factory(arch)

    def uname(i):
        k = Uu.get(i.upper()) if i else None
        if k:
            return '%s(%s)' % (U[k]['name_full'] or U[k]['name'], k)
        k = Ou.get(i.upper()) if i else None
        return '%s(%s, S2에 없음)' % (O[k]['name_full'], k) if k else (i or '')

    # ── 로스터별 현재 정보 ──
    recs = []
    for d in roster:
        f = d['_file']
        a = audit.get(f, {})
        guids = EX.skill_guids(d)
        skills = [s for s in (EX.skill_of(g) for g in guids) if s]
        sguid = [g for g in guids if EX.skill_of(g)]
        spec = [s for s in skills if s['_file'].startswith(('SkillData_사장님_', 'SkillData_공용_'))]
        ids = [i for i in a.get('stat_ids', []) if i in Uu]
        cur_vfx = sorted({k for g in sguid for k in vfx_now.get(g, [])})
        recs.append(dict(
            file=f, name=d['unitName'], grade=d['grade'], grade_name=EX.GRADE.get(d['grade'], '초월위습' if d['grade'] == 13 else str(d['grade'])),
            atk=d['attackPower'], spd=d['attackSpeed'], dps=d['attackPower'] * d['attackSpeed'], rng=d['attackRange'],
            stat_ids=a.get('stat_ids', []), s2_stat_ids=ids, owner_ids=a.get('skill_owner_ids', []), how=a.get('how', ''),
            skills=[EX.clean_skill_name(s['skillName']) for s in skills], spec=len(spec), spec_names=[EX.clean_skill_name(s['skillName']) for s in spec],
            vfx_now=cur_vfx, n_vfx=sum(1 for g in sguid if vfx_now.get(g)), n_skills=len(skills)))

    for r in recs:
        best = None
        for i in r['s2_stat_ids']:
            o = O.get(Ou.get(i, ''))
            dps = (o['atk'] / o['cool']) if o and o['cool'] else 0
            gap = abs(dps - r['dps']) / max(1.0, r['dps'])
            fits = U[Uu[i]]['grade'] in ALLOWED.get(r['grade'], ())   # 합친 대응(여러 원작)이면 등급 맞는 원작 먼저
            key = (0 if fits else 1, gap)
            if best is None or key < best[0]:
                best = (key, i)
        r['cur'] = best[1] if best else None
        r['cur_s2'] = Uu.get(r['cur']) if r['cur'] else None

    pool = defaultdict(list)
    for u in sorted(U.values(), key=lambda u: u['id']):
        for g, tags in ALLOWED.items():
            combat = any(not ability_view(a, AB, AS)['tag_only'] for a in u['abil']) or u['atk'] > 100
            if u['grade'] in tags and (u['id'][0] == 'h' or g in HERO_GRADES) and combat and '선물상자' not in u['name']:
                pool[g].append(u['id'])
    claimed, result = {}, {}
    by_cur = defaultdict(list)
    for r in recs:
        if r['cur_s2'] and (U[r['cur_s2']]['grade'] in ALLOWED.get(r['grade'], ()) or U[r['cur_s2']]['grade'] == '(무태그 소환·더미)'):
            by_cur[r['cur_s2']].append(r)

    def priority(r):
        # S2 「신비」(옛 「랜덤전용[제한됨]」)는 우리 다른세계 쪽이 먼저(랜덤 쪽은 「랜덤전용」 풀로 간다)
        mystic = 0 if (U[r['cur_s2']]['grade'] == '신비' and r['grade'] == 11) else 1
        own = 0 if r['cur'] in r['owner_ids'] else 1
        csvm = 0 if r['how'] == '값일치=csv' else 1
        return (mystic, own, csvm, r['spec'] > 0, r['file'])
    for s2id, rs in by_cur.items():
        rs.sort(key=priority)
        claimed[s2id] = rs[0]['file']
        result[rs[0]['file']] = (s2id, '유지', '')
        for loser in rs[1:]:
            loser['lost_to'] = rs[0]['file']
    s2dps = lambda i: (U[i]['atk'] / U[i]['cool']) if U[i]['cool'] else 0.0
    rest = [r for r in recs if r['file'] not in result]
    rest.sort(key=lambda r: (-r['dps'], r['file']))
    for r in rest:
        if not r['stat_ids'] and NO_ORIGINAL_KEEP:
            result[r['file']] = (None, '대응 없음', '지금도 원작 대응 없음(사장님 창작·특수 지급) — 새로 붙일지 사장님 확인')
            continue
        if r['grade'] not in ALLOWED:
            result[r['file']] = (None, '대응 없음', '등급 대응 없음')
            continue
        why = ('같은 원작을 %s가 씀' % r['lost_to']) if r.get('lost_to') else (
            '지금 원작이 S2 등급 「%s」라 이 등급과 안 맞음' % U[r['cur_s2']]['grade'] if r['cur_s2'] else '지금 원작이 S2에 없음')
        free = []
        for tier in TIERS.get(r['grade'], [ALLOWED[r['grade']]]):
            free = [i for i in pool[r['grade']] if i not in claimed and U[i]['grade'] in tier]
            if free:
                break
        if not free:
            if r['cur_s2']:
                result[r['file']] = (r['cur_s2'], '중복', why + ' · 같은 등급에 남는 S2 원작이 없어 지금 원작을 그대로 둠(중복·사장님 판단)')
            else:
                result[r['file']] = (None, '대응 없음', why + ' · 같은 등급에 남는 S2 원작 없음')
            continue
        pick = min(free, key=lambda i: (abs(s2dps(i) - r['dps']), i))
        claimed[pick] = r['file']
        result[r['file']] = (pick, '새로', why + ' · 미사용 원작 중 DPS가 가장 가까운 것')

    # ── 행 만들기 ──
    rows, skill_rows, fx = [], [], defaultdict(lambda: dict(users=set(), status=None, where=None))
    for r in sorted(recs, key=lambda r: (EX.GRADE_ORDER.index(r['grade']) if r['grade'] in EX.GRADE_ORDER else 99, r['file'])):
        new, how, why = result.get(r['file'], (None, '대응 없음', ''))
        u = U.get(new) if new else None
        o = O.get(Ou.get(new.upper(), '')) if new else None
        abil = [ability_view(a, AB, AS) for a in (u['abil'] if u else [])]
        real = [a for a in abil if not a['tag_only']]
        owners_txt = ', '.join(uname(i) for i in r['owner_ids']) if r['owner_ids'] else ('사양 스킬뿐' if r['spec'] else '스킬 없음')
        if not u:
            change = '대응 없음'
        elif how == '유지':
            other = [i for i in r['owner_ids'] if i.upper() != (r['cur'] or '').upper() and not i.upper().startswith('E')]
            change = '스킬만 바뀜' if (other or not r['owner_ids']) and r['spec'] < r['n_skills'] else '유지'
        else:
            change = '원작 바뀜'
        if r['spec']:
            spec_txt = '사양 스킬 %d개 유지(%s)' % (r['spec'], ', '.join(r['spec_names'][:6]))
            spec_txt += ' · 능력치는 원작 추종' if r['how'].startswith('값일치') else ' · 능력치도 지금 값 고정(원작과 다름: %s)' % (r['how'] or '출처 불명')
        else:
            spec_txt = '-'
        diff = ''
        if u and o:
            ch = []
            if abs(u['atk'] - o['atk']) > 0.5:
                ch.append('공격력 %s→%s(×%.2f)' % (fmt(o['atk']), fmt(u['atk']), u['atk'] / max(1, o['atk'])))
            if abs(u['cool'] - o['cool']) > 0.005:
                ch.append('공속주기 %.2f→%.2f' % (o['cool'], u['cool']))
            if abs(u['range'] - o['range']) > 0.5:
                ch.append('사거리 %d→%d' % (o['range'], u['range']))
            if (u['atk_type'] or 'normal') != (o['atk_type'] or 'normal'):
                ch.append('공격타입 %s→%s' % (o['atk_type'], u['atk_type']))
            if abs(u['mana'] - o['mana']) > 0.5:
                ch.append('마나 %d→%d' % (o['mana'], u['mana']))
            diff = ' · '.join(ch) if ch else '수치 같음'
        elif u:
            diff = 'S2 신규 원작(옛 판에 없음)'
        new_dps = s2dps(new) if u else 0.0
        if u and u['atk'] < 100 and r['atk'] > 1000:
            why = (why + ' · ' if why else '') + 'S2에서 평타 %s(스킬로 싸우는 유닛) — 공격력이 사실상 0이 됨' % fmt(u['atk'])
        stat_txt = ('%s / 공속 %.3f(주기 %.2f) / %s%s · 사거리 %d · 마나 %d' % (fmt(u['atk']), 1 / u['cool'] if u['cool'] else 0, u['cool'], u['atk_type'],
                    (' · 광역 %d' % u['splash']) if u['weap'] == 'msplash' and u['splash'] else '', u['range'], u['mana'])) if u else ''
        skills_txt = '\n'.join('· %s: %s' % (a['name'], a['desc']) for a in real) if u else ''
        if u and not real:
            skills_txt = '(원작 스킬 없음 — 꼬리표 능력만)'
        # 이펙트
        unit_models = set(umap.get(new, set())) if u else set()
        fx_rows = []
        for a in real:
            models = []
            for k in ART_KEYS:
                models += [(k, m) for m in split_models(a['art'].get(k))]
            models += [('트리거', m) for m in sorted(amap.get(a['id'], set()))]
            seen = set()
            for k, m in models:
                stem, st, where = mstatus(m)
                if (stem, k) in seen or 'BTN' in stem.upper()[:3]:
                    continue
                seen.add((stem, k))
                fx_rows.append((a['name'], k, stem, where, st))
                fx[stem]['users'].add('%s/%s' % (r['name'], a['name']))
                fx[stem]['status'], fx[stem]['where'] = st, where
            skill_rows.append(dict(grade=r['grade_name'], name=r['name'], file=r['file'], orig=uname(new), aid=a['id'], sname=a['name'], desc=a['desc'], base=a['base'],
                                   fx='; '.join('%s:%s(%s)' % (k, s, st) for (sn, k, s, w, st) in fx_rows if sn == a['name']) or '없음'))
        for m in sorted(unit_models):
            stem, st, where = mstatus(m)
            fx[stem]['users'].add('%s/(유닛 트리거)' % r['name'])
            fx[stem]['status'], fx[stem]['where'] = st, where
        fx_summary = Counter(st.split('(')[0] for (_, _, _, _, st) in fx_rows)
        fx_txt = ' · '.join('%s %d' % (k, v) for k, v in fx_summary.items()) if fx_rows else ('원작 아트 필드 없음' if u else '')
        rows.append(dict(
            grade=r['grade_name'], roster=r['name'], file=r['file'], cur_stat=uname(r['cur']) if r['cur'] else ('원작 대응 없음' if not r['stat_ids'] else ', '.join(r['stat_ids'])),
            cur_skill=owners_txt + (' + 사양 %d' % r['spec'] if r['spec'] and r['owner_ids'] else ''), new=uname(new) if u else '-',
            new_stat=stat_txt, new_skills=skills_txt, change=change, how=how, spec=spec_txt, note=why, diff=diff,
            cur_atk=r['atk'], cur_dps=r['dps'], new_atk=u['atk'] if u else 0.0, new_dps=new_dps, new_grade_src=(u['grade'] + ' · ' + u['grade_src']) if u else '',
            fx=fx_txt, vfx_now=', '.join(r['vfx_now']) or '없음', n_real=len(real)))

    json.dump(rows, open(os.path.join(OUT, 'rematch.json'), 'w'), ensure_ascii=False, indent=1)
    write_xlsx(rows, skill_rows, fx, pool, claimed, U)
    write_md(rows, pool, claimed, U)
    print(json.dumps({'rows': len(rows), 'skills': len(skill_rows), 'fx_models': len(fx), 'change': Counter(r['change'] for r in rows),
                      'how': Counter(r['how'] for r in rows)}, ensure_ascii=False, default=str))


def write_xlsx(rows, skill_rows, fx, pool, claimed, U):
    from openpyxl import Workbook
    wb = Workbook()
    ws = wb.active
    ws.title = '제안'
    head = 'S2 2.323 재매칭 제안(1단계, 읽기 전용) · 기준 커밋 %s · 로스터 %d기' % (EX.git_head(), len(rows))
    EX.write_sheet(ws, head, ['등급', '로스터(우리 이름)', '에셋 파일', '지금 능력치 원작', '지금 스킬 원작', '새 매칭 원작(id·이름)', '새 공격력·공속·공격타입(S2)',
                              '새 스킬 목록(S2 툴팁 한 줄)', '변화 요약', '매칭 방식', '사양 고정 여부', '11.089→S2 바뀐 값', '지금 공격력', '새 공격력', 'DPS 배율(새/지금)',
                              '원작 이펙트(새 스킬)', '우리 지금 이펙트', '비고(새 매칭 이유·등급 근거)'],
                   [(r['grade'], r['roster'], r['file'], r['cur_stat'], r['cur_skill'], r['new'], r['new_stat'], r['new_skills'], r['change'], r['how'], r['spec'],
                     r['diff'], round(r['cur_atk']), round(r['new_atk']), round(r['new_dps'] / r['cur_dps'], 2) if r['cur_dps'] and r['new_dps'] else '',
                     r['fx'], r['vfx_now'], (r['note'] + (' · ' if r['note'] else '') + r['new_grade_src']).strip(' ·'))
                    for r in rows], [9, 18, 22, 30, 30, 30, 34, 70, 11, 9, 34, 40, 12, 12, 10, 30, 22, 40])
    ws2 = wb.create_sheet('새 스킬 상세')
    EX.write_sheet(ws2, head + ' · 새 원작 능력 하나당 한 줄(꼬리표 능력 제외)', ['등급', '로스터', '에셋 파일', '새 원작', '능력 id', '스킬 이름', 'S2 툴팁 요약', '기반 코드', '원작 이펙트(필드:모델(상태))'],
                   [(s['grade'], s['name'], s['file'], s['orig'], s['aid'], s['sname'], s['desc'], s['base'], s['fx']) for s in skill_rows], [9, 18, 22, 30, 8, 22, 80, 8, 60])
    ws3 = wb.create_sheet('이펙트 작업 목록')
    order = {'이미': 0, '꺼내기만': 1, '꺼내도': 2, '없음': 3}
    fxr = sorted(fx.items(), key=lambda kv: (order.get(kv[1]['status'].split(' ')[0].split('(')[0], 9), kv[0].lower()))
    EX.write_sheet(ws3, head + ' · 새 스킬에 쓰이는 원작 이펙트 모델(blender 작업 목록)', ['모델 파일명', '어디에 있나', '상태', '쓰는 스킬·유닛'],
                   [(k, v['where'], v['status'], '; '.join(sorted(v['users']))) for k, v in fxr], [36, 14, 44, 90])
    ws4 = wb.create_sheet('등급별 S2 원작 풀')
    prow = []
    for g, ids in sorted(pool.items(), key=lambda kv: EX.GRADE_ORDER.index(kv[0]) if kv[0] in EX.GRADE_ORDER else 99):
        for i in ids:
            u = U[i]
            prow.append((EX.GRADE[g], i, u['name_full'] or u['name'], u['grade'] + ' · ' + u['grade_src'], fmt(u['atk']), u['cool'], claimed.get(i, '(미사용)')))
    EX.write_sheet(ws4, head + ' · 등급마다 받을 수 있는 S2 원작과 쓰는 로스터', ['우리 등급', 'S2 id', 'S2 이름', 'S2 등급·근거', '공격력', '공격 주기', '쓰는 로스터'], prow, [9, 8, 34, 30, 12, 10, 26])
    EX.save(wb, os.path.join(OUT, '재매칭_제안.xlsx'))


def write_md(rows, pool, claimed, U):
    import math
    L = ['# S2 2.323 재매칭 제안 요약 (1단계 · 읽기 전용)', '', '기준 커밋 %s · 정본: Tools/w3x/원본_S2_2.323(슬크·툴팁) · 표: 재매칭_제안.xlsx' % EX.git_head(), '']
    # 0) 먼저 볼 것 — 대응 없음·중복·평타 사실상 0
    none_ = [r for r in rows if r['how'] == '대응 없음']
    dup = [r for r in rows if r['how'] == '중복']
    weak = [r for r in rows if 'S2에서 평타' in r['note']]
    L += ['## 0. 먼저 볼 것(사장님 판단)', '', '### 대응 없음 %d — 지금도 원작이 없는 유닛(새 원작을 붙일지)' % len(none_), '']
    L += ['- [%s] %s (%s)' % (r['grade'], r['roster'], r['file']) for r in none_]
    L += ['', '### 중복 %d — 같은 등급에 남는 S2 원작이 없어 지금 원작을 그대로 둔 것' % len(dup), '']
    L += ['- [%s] %s → %s : %s' % (r['grade'], r['roster'], r['new'], r['note']) for r in dup]
    L += ['', '### 랜덤 %d기 — S2 「랜덤전용」 원작은 평타가 18 안팎(스킬로 싸우는 유닛)이라 공격력이 사실상 0이 됨' % len(weak), '',
          'S2에서 옛 「랜덤전용[제한됨]」은 「신비」 등급이 됐다. 같은 신비 원작을 우리 랜덤·다른세계가 나눠 쓰고 있어, 한 원작 = 한 로스터 원칙으로 **다른세계에 신비를 주고 랜덤은 남는 「랜덤전용」 원작으로** 보냈다.',
          '다른 길: (가) 랜덤 쪽이 지금처럼 신비를 쓰고 다른세계가 비켜 줌(다른세계 원작 부족) (나) 랜덤은 중복을 허용해 지금 원작 유지 (다) 이 표대로.', '']
    L += ['- %s: 지금 공격력 %s → %s(%s)' % (r['roster'], fmt(r['cur_atk']), fmt(r['new_atk']), r['new']) for r in weak]
    L += ['']
    # ① 등급별 공격력·DPS 배율
    L += ['## ① 등급별로 공격력이 몇 배 바뀌나 (새 원작 S2 값 ÷ 지금 우리 값, 기하평균)', '', '| 등급 | 로스터 | 공격력 배율 | DPS 배율 | 범위(DPS 최소~최대) |', '|---|---:|---:|---:|---|']
    by = defaultdict(list)
    for r in rows:
        if r['new_atk'] and r['cur_atk'] and r['cur_dps'] and r['new_dps']:
            by[r['grade']].append(r)
    gm = lambda xs: math.exp(sum(math.log(x) for x in xs) / len(xs)) if xs else 0
    for g in [EX.GRADE[k] for k in EX.GRADE_ORDER]:
        rs = by.get(g)
        if not rs:
            continue
        a = gm([r['new_atk'] / r['cur_atk'] for r in rs])
        dd = [r['new_dps'] / r['cur_dps'] for r in rs]
        L.append('| %s | %d | ×%.2f | ×%.2f | ×%.2f ~ ×%.2f |' % (g, len(rs), a, gm(dd), min(dd), max(dd)))
    # ② 상위 20
    L += ['', '## ② 크게 바뀌는 유닛 상위 20 (DPS 배율 기준, 커지는·작아지는 쪽 모두)', '', '| 등급 | 로스터 | 지금 공격력 | 새 공격력 | DPS 배율 | 매칭 | 새 원작 |', '|---|---|---:|---:|---:|---|---|']
    big = sorted([r for rs in by.values() for r in rs], key=lambda r: -abs(math.log(r['new_dps'] / r['cur_dps'])))[:20]
    for r in big:
        L.append('| %s | %s | %s | %s | ×%.2f | %s | %s |' % (r['grade'], r['roster'], fmt(r['cur_atk']), fmt(r['new_atk']), r['new_dps'] / r['cur_dps'], r['how'], r['new']))
    # ③ 밸런스 위험
    hp = 10600000 * 8.25
    L += ['', '## ③ 밸런스 위험(대략): 「신」 난이도 R50 보스(체력 10,600,000 × 8.25 = %s) 를 등급 평균 1기가 평타만으로 잡는 시간' % fmt(hp), '',
          '스킬·방어·연구·강화·가동률을 뺀 맨 평타 기준이라 절대값이 아니라 **지금 대비 몇 배 빨라지나/느려지나**만 본다.', '',
          '| 등급 | 지금 평균 DPS | 새 평균 DPS | 지금 → 새 (초) |', '|---|---:|---:|---|']
    for g in ['희귀함', '히든', '전설적인', '변화됨', '제한됨', '초월함', '불멸의', '영원한', '랜덤전용', '다른세계']:
        rs = by.get(g)
        if not rs:
            continue
        c = sum(r['cur_dps'] for r in rs) / len(rs)
        n = sum(r['new_dps'] for r in rs) / len(rs)
        L.append('| %s | %s | %s | %.0f → %.0f |' % (g, fmt(c), fmt(n), hp / c, hp / n))
    # 매칭 개요
    L += ['', '## 매칭 개요', '', '| 등급 | 유지 | 스킬만 바뀜 | 원작 바뀜 | 중복 | 대응 없음 |', '|---|---:|---:|---:|---:|---:|']
    cc = defaultdict(Counter)
    for r in rows:
        cc[r['grade']][r['change'] if r['how'] != '중복' else '중복'] += 1
    for g in [EX.GRADE[k] for k in EX.GRADE_ORDER] + ['초월위습']:
        if g in cc:
            c = cc[g]
            L.append('| %s | %d | %d | %d | %d | %d |' % (g, c['유지'], c['스킬만 바뀜'], c['원작 바뀜'], c['중복'], c['대응 없음']))
    L += ['', '## 사장님 판단이 필요한 것', '']
    for r in rows:
        if r['how'] in ('중복', '대응 없음') or r['change'] == '원작 바뀜':
            L.append('- [%s] %s(%s): %s — %s' % (r['grade'], r['roster'], r['file'], r['how'] if r['how'] != '새로' else '원작 바뀜 → ' + r['new'], r['note']))
    noskill = [r for r in rows if r['new_skills'].startswith('(원작 스킬 없음')]
    L += ['', '## 새 원작에 원작 스킬이 없는 로스터(%d)' % len(noskill), ''] + ['- [%s] %s → %s' % (r['grade'], r['roster'], r['new']) for r in noskill]
    open(os.path.join(OUT, '재매칭_요약.md'), 'w', encoding='utf-8').write('\n'.join(L) + '\n')


if __name__ == '__main__':
    main()
