#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""재매칭 2단계 적용(사장님 10-18 승인) — rematch_s2.py 제안표대로 로스터 능력치·스킬·originalMatchName을 S2 2.323 원작으로 바꾼다.

사용(저장소 루트, openpyxl·pyyaml 있는 python):
  Tools/rematch_s2_apply.py plan                     # 바뀔 것 미리보기 → ~/grd/GRD_rematch/apply_plan.json · apply_report.md
  Tools/rematch_s2_apply.py apply <등급…|all>        # 에셋 쓰기(등급 이름: 흔함 안흔함 특별함 희귀함 히든 변화됨 전설적인 제한됨 초월함 불멸의 영원한 랜덤전용 다른세계 특수함)

손대지 않음: 대응 없음 7 · 상붕카(브룩 그대로) · 사양 「능력치도 고정」 6기(사장님 10-18 「지금 그대로」).
능력치: attackPower · attackSpeed(1/주기) · attackType(S2 atkType1) · attackSplashRadius(msplash Farea1). 사거리·체력은 그대로(지금 규칙).
스킬(교체 대상 = 스킬만 바뀜·원작 바뀜·히든 4): 사양 아닌 스킬을 떼고 S2 툴팁에서 만든 SkillData_S2_* 를 붙인다(rematch_s2_skills.convert). 사양 스킬(SkillData_사장님_*·공용_)은 그대로.
"""
import hashlib
import json
import os
import re
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.dirname(HERE)
sys.path.insert(0, HERE)
import rematch_s2 as R  # noqa: E402
import rematch_s2_data as D  # noqa: E402
import rematch_s2_skills as S  # noqa: E402

EX = R.EX
OUT = os.path.expanduser('~/grd/GRD_rematch')
SKILL_DIR = os.path.join(ROOT, 'Assets', 'Data', 'UnitSkills')
ROSTER_DIR = os.path.join(ROOT, 'Assets', 'Data', 'Units', 'Roster')
SKILLDATA_SCRIPT = '9457f64cd84d34fd095791a069c0adc6'
ATK = {'normal': 1, 'pierce': 2, 'siege': 3, 'hero': 4, 'chaos': 5, 'magic': 6, 'spells': 7}
GRADE_SHORT = {0: '흔함', 1: '안흔함', 2: '특별함', 3: '희귀함', 4: '히든', 5: '전설', 6: '제한', 7: '초월', 8: '불멸', 9: '영원', 10: '랜덤', 11: '다른세계', 12: '특수', 14: '변화'}
SKIP = {'흔함_상붕카'}
FIXED_STATS = {'주호리얼', '보스의앞잡이', '만물통달', '구일최강의여성', '상호파와채민파의아버지', '전전교회장'}
HIDDEN4 = {'맥주만땅', '푸은서', '감탄떡볶이', '이삭토스트'}
HOLD = set(os.environ.get('REMATCH_HOLD', '').split(',')) - {''}   # PM 답 대기(원작에 피해 스킬 없음)
SLOT_OF = {'Targetart': '적중시', 'Casterart': '시전자', 'Specialart': '범위', 'Effectart': '범위', 'Areaeffectart': '범위', '트리거': '범위'}


def yq(s):
    """유니티식 따옴표 문자열(비ASCII는 \\uXXXX)."""
    s = s or ''
    out = []
    for c in s:
        if c == '"':
            out.append('\\"')
        elif c == '\\':
            out.append('\\\\')
        elif ord(c) < 32:
            out.append(' ')
        elif ord(c) < 128:
            out.append(c)
        else:
            out.append('\\u%04X' % ord(c))
    return '"' + ''.join(out) + '"'


def guid_of(name):
    return hashlib.md5(('grd-s2-rematch/' + name).encode('utf-8')).hexdigest()


def fnum(v):
    if isinstance(v, bool):
        return '1' if v else '0'
    if isinstance(v, float):
        if abs(v - round(v)) < 1e-9:
            return str(int(round(v)))
        return ('%.6f' % v).rstrip('0').rstrip('.')
    return str(v)


EFFECT_KEYS = ['kind', 'basis', 'target', 'damageType', 'attackType', 'multiplier', 'bonus', 'chance', 'hitCount', 'duration', 'casterBuffCountFactor', 'buffId',
               'randMin', 'randMax', 'buffHitCharges', 'requiredTargetBuffId', 'forbiddenTargetBuffId', 'targetCondition', 'targetConditionValue', 'cascadeGroup',
               'skipDamageTakenMultiplier', 'triggerDamageAbove', 'heroDuration', 'maxTargets', 'chainDamageStep', 'zoneTickInterval', 'zoneRadius', 'zoneSpacing',
               'zoneAtCaster', 'lineLength', 'lineStartRadius', 'lineEndRadius', 'perHighGradeUnitBonus', 'perHighGradeUnitBonusCap']
EFFECT_DEFAULT = dict(kind=0, basis=0, target=2, damageType=0, attackType=0, multiplier=0, bonus=0, chance=1, hitCount=1, duration=0, casterBuffCountFactor=0, buffId='',
                      randMin=1, randMax=1, buffHitCharges=0, requiredTargetBuffId='', forbiddenTargetBuffId='', targetCondition=0, targetConditionValue=0, cascadeGroup=0,
                      skipDamageTakenMultiplier=0, triggerDamageAbove=0, heroDuration=0, maxTargets=0, chainDamageStep=0, zoneTickInterval=0, zoneRadius=0, zoneSpacing=0,
                      zoneAtCaster=0, lineLength=0, lineStartRadius=0, lineEndRadius=0, perHighGradeUnitBonus=0, perHighGradeUnitBonusCap=0)
EFFECT_TAIL = """      summonUnits: []
      summonLifetime: 20
      summonFanDegrees: 35
      summonRadius: 70
      grantSkill: {fileID: 0}
      minAllyTier: 5
      armorIgnoreRatio: 0
      armorIgnoreRequiresBuff:
      talentStunScaled: 0
      stackPerHit: 0.01
      stackCap: 1
      stackResetSeconds: 3
      attackSpeedScale: 0
      attackSpeedScaleCap: 0
"""


def effect_yaml(e, atk_type):
    d = dict(EFFECT_DEFAULT)
    d.update({k: v for k, v in e.items() if k in EFFECT_DEFAULT})
    if d['kind'] in (0, 27) and not d['attackType']:
        d['attackType'] = atk_type
    lines = []
    for i, k in enumerate(EFFECT_KEYS):
        v = d[k]
        s = (yq(v) if v else '') if isinstance(v, str) else fnum(v)
        lines.append(('    - ' if i == 0 else '      ') + '%s: %s' % (k, s))
    txt = '\n'.join(lines) + '\n' + EFFECT_TAIL
    if e.get('fixedDamage'):
        txt += '      fixedDamage: 1\n'
    if e.get('explosive'):
        txt += '      explosive: 1\n'
    return txt


def skill_yaml(name, skill_name, desc, blk, atk_type):
    trig = {0: 0, 1: 1, 2: 2, 3: 3, 5: 5}[blk.trigger]
    thr, gk = (blk.gauge[1], 0 if blk.gauge[0] == 'mana' else 1) if blk.gauge else (0, 0)
    chance = blk.chance if blk.trigger == 0 else 1
    body = """%YAML 1.1
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
  m_Script: {fileID: 11500000, guid: %s, type: 3}
  m_Name: %s
  m_EditorClassIdentifier:
  skillName: %s
  description: %s
  triggerType: %d
  levels:
  - cooldown: %s
    triggerChance: %s
    range: %s
    hitCountThreshold: %d
    resetTo: 0
    gaugeKind: %d
    requiredBuffId:
    forbiddenBuffId:
    selfBuffId:
    effects:
""" % (SKILLDATA_SCRIPT, yq(name), yq(skill_name), yq(desc), trig, fnum(float(blk.cooldown)), fnum(float(chance)), fnum(float(blk.range)), thr, gk)
    body += ''.join(effect_yaml(e, atk_type) for e in blk.effects)
    body += """    requiredTargetBuffId:
    forbiddenTargetBuffId:
    hitCountFloor: 0
    gaugeSpendAmount: 0
    aoeCenter: 0
    targetArmorBreakAbove: 0
    primaryTargetCondition: 0
    primaryTargetConditionValue: 0
    exclusiveGroup: 0
    enterRange: 0
    requireNormalEnemyInRange: 0
    needsTargetClick: 0
"""
    meta = """fileFormatVersion: 2
guid: %s
NativeFormatImporter:
  externalObjects: {}
  mainObjectFileID: 11400000
  userData:
  assetBundleName:
  assetBundleVariant:
""" % guid_of(name)
    return body, meta


def set_field(text, key, value):
    pat = re.compile(r'(?m)^  %s: .*$' % re.escape(key))
    if not pat.search(text):
        raise KeyError(key)
    return pat.sub(lambda m: '  %s: %s' % (key, value), text, count=1)


def build_plan():
    rows = json.load(open(os.path.join(OUT, 'rematch.json')))
    U, AB, AS, US = D.load_s2()
    plan = []
    amap, _umap = R.j_index(D.load_s2_models())
    for r in rows:
        if r['how'] == '대응 없음' or r['file'] in SKIP or r['roster'] in FIXED_STATS or r['file'] in HOLD:
            continue
        sid = r['new'].rsplit('(', 1)[1].rstrip(')')
        u = U[sid]
        path = os.path.join(ROSTER_DIR, r['file'] + '.asset')
        d = EX.load_asset(path)
        atk_type = ATK.get((u['atk_type'] or 'normal').lower(), 1)
        stats = dict(attackPower=fnum(round(u['atk'], 2)), attackSpeed=fnum(round(1.0 / u['cool'], 4)) if u['cool'] else None, attackType=atk_type,
                     attackSplashRadius=fnum(u['splash'] if u['weap'] == 'msplash' else 0.0))
        stats = {k: v for k, v in stats.items() if v is not None}
        before = dict(attackPower=d['attackPower'], attackSpeed=d['attackSpeed'], attackType=d['attackType'], attackSplashRadius=d.get('attackSplashRadius', 0))
        omn = '%s %s' % (GRADE_SHORT.get(d['grade'], ''), u['name'])
        item = dict(file=r['file'], roster=r['roster'], grade=r['grade'], s2=sid, s2name=u['name_full'] or u['name'], stats=stats, before=before,
                    omn=omn, replace=False, skills=[], patch={}, lost=[], keep=[], remove=[])
        if r['change'] in ('스킬만 바뀜', '원작 바뀜', '스킬 추가') or r['roster'] in HIDDEN4:
            item['replace'] = True
            for g in EX.skill_guids(d):
                s = EX.skill_of(g)
                if s and s['_file'].startswith(('SkillData_사장님_', 'SkillData_공용_')):
                    item['keep'].append(s['_file'])
                elif s:
                    item['remove'].append(s['_file'])
            mana_need = 0
            for aid in u['abil']:
                v = R.ability_view(aid, AB, AS)
                if v['tag_only']:
                    continue
                blocks, patch, lost = S.convert(AS.get(aid, {}).get('Ubertip', ''), aid, v['name'])
                item['patch'].update(patch)
                item['lost'] += ['%s(%s): %s' % (v['name'], aid, x) for x in lost]
                models = {}
                for k in R.ART_KEYS:
                    if k == 'Missileart':
                        continue
                    for m in R.split_models(v['art'].get(k)):
                        models.setdefault(SLOT_OF[k], os.path.splitext(os.path.basename(m.replace('\\\\', '/').replace('\\', '/')))[0])
                for m in sorted(amap.get(aid, set())):
                    models.setdefault('범위', os.path.splitext(os.path.basename(m.replace('\\\\', '/').replace('\\', '/')))[0])
                for i, b in enumerate(blocks):
                    nm = 'SkillData_S2_%s_%s_%d' % (r['file'], aid, i + 1)
                    sk_name = v['name'] if (len(blocks) == 1 or b.name == v['name']) else '%s — %s' % (v['name'], b.name)
                    desc = 'S2 2.323 원작 %s(%s) %s — %s' % (u['name_full'] or u['name'], sid, aid, v['desc'])
                    item['skills'].append(dict(name=nm, skill_name=sk_name, desc=desc, block=b.as_dict(), aid=aid, vfx=models))
                    if b.gauge and b.gauge[0] == 'mana':
                        mana_need = max(mana_need, b.gauge[1])
                    if b.gauge and b.gauge[0] == 'life':
                        item['patch']['lifeGaugeMax'] = max(item['patch'].get('lifeGaugeMax', 0), b.gauge[1])
                        item['patch']['lifeGaugeCustomHitGain'] = 1
            if mana_need:
                item['patch']['manaMax'] = max(mana_need, u['mana'])
                item['patch']['manaGaugePerMana'] = 1
        plan.append(item)
    return plan


def report(plan):
    chg = [p for p in plan if any(str(p['before'][k]) != str(v) and not (isinstance(p['before'][k], (int, float)) and abs(float(p['before'][k]) - float(v)) < 1e-3) for k, v in p['stats'].items())]
    rep = [p for p in plan if p['replace']]
    L = ['# 재매칭 2단계 적용 계획', '', '능력치 바뀌는 로스터 %d / 대상 %d · 스킬 교체 로스터 %d · 새 SkillData %d · 떼는 스킬 %d · 남기는 사양 스킬 %d' % (
        len(chg), len(plan), len(rep), sum(len(p['skills']) for p in rep), sum(len(p['remove']) for p in rep), sum(len(p['keep']) for p in rep)), '']
    L += ['## 스킬 교체 로스터별(새 스킬 · 미옮김)', '']
    for p in rep:
        L.append('### [%s] %s (%s) ← %s(%s)' % (p['grade'], p['roster'], p['file'], p['s2name'], p['s2']))
        L.append('- 새 스킬 %d: %s' % (len(p['skills']), ' / '.join('%s[%s]' % (s['skill_name'], {0: '평타 %.1f%%' % (s['block']['chance'] * 100), 1: '쿨 %gs' % s['block']['cooldown'], 2: '오라', 3: '게이지 %s' % (s['block']['gauge'],), 5: '누름 쿨 %gs' % s['block']['cooldown']}[s['block']['trigger']]) for s in p['skills'])))
        if p['patch']:
            L.append('- 로스터 필드: %s' % ', '.join('%s=%s' % kv for kv in p['patch'].items()))
        if p['keep']:
            L.append('- 사양 스킬 유지 %d' % len(p['keep']))
        for x in p['lost']:
            L.append('- 미옮김: %s' % x)
        L.append('')
    open(os.path.join(OUT, 'apply_report.md'), 'w', encoding='utf-8').write('\n'.join(L) + '\n')
    return len(chg), len(rep)


def write_vfx_csv(plan, grades):
    """새 스킬의 원작 이펙트 → Docs/research/ORIGINAL_VFX_ASSIGN_S2재매칭.csv(SkillVfxTableBuilder·OriginalVfxBuilder가 읽는 「원작:모델」 칸)."""
    import csv
    p = os.path.join(ROOT, 'Docs', 'research', 'ORIGINAL_VFX_ASSIGN_S2재매칭.csv')
    rows = []
    for it in plan:
        if not it['replace'] or ('all' not in grades and it['grade'] not in grades):
            continue
        for s in it['skills']:
            v = s.get('vfx') or {}
            if not v:
                continue
            hit = v.get('적중시') or (v.get('범위') if not s['block']['range'] else '')
            area = v.get('범위') if s['block']['range'] else ''
            cast = v.get('시전자', '')
            rows.append(['Assets/Data/UnitSkills/%s.asset' % s['name'], ('원작:' + hit) if hit else '', ('원작:' + area) if area else '', ('원작:' + cast) if cast else '',
                         '맞은 적 몸' if hit else ('범위 중심' if area else '시전자 위치'), '적중시' if hit else ('범위' if area else '시전자'), '자동(S2 아트 필드·트리거)',
                         'S2 %s %s' % (it['s2'], s['aid'])])
    old = []
    if os.path.exists(p):
        old = [r for r in csv.reader(open(p, encoding='utf-8')) if r and r[0] != 'asset_path']
    keep = {r[0] for r in rows}
    allrows = [r for r in old if r[0] not in keep] + rows
    with open(p, 'w', encoding='utf-8', newline='') as f:
        w = csv.writer(f)
        w.writerow(['asset_path', '적중시_프리팹', '범위_지면_프리팹', '시전자_프리팹', '발동지점', '권장칸', '검수', '근거'])
        w.writerows(sorted(allrows))
    return 'Docs/research/ORIGINAL_VFX_ASSIGN_S2재매칭.csv'


def write_s2_map(plan):
    """Docs/reference/S2_ROSTER_MAP_2026-10-18.csv — 재매칭 정본 대응표(export_unit_sheets가 먼저 읽는다)."""
    import csv
    rows = json.load(open(os.path.join(OUT, 'rematch.json')))
    planned = {p['file']: p for p in plan}
    out = []
    for r in rows:
        sid = r['new'].rsplit('(', 1)[1].rstrip(')') if r['new'] not in ('-', '') and '(' in r['new'] else ''
        name = r['new'].rsplit('(', 1)[0] if sid else ''
        if r['file'] in planned:
            st = '적용(능력치+스킬)' if planned[r['file']]['replace'] else '적용(능력치·스킬 주인 같음)'
        elif r['file'] in HOLD:
            st, sid, name = '보류(PM 답 대기)', '', ''
        elif r['roster'] in FIXED_STATS:
            st = '사양 고정(능력치·스킬 그대로)'
        elif r['file'] in SKIP:
            st = '그대로(지금 원작 유지)'
        else:
            st, sid, name = '원작 대응 없음', '', ''
        out.append([r['file'], r['roster'], r['grade'], sid, name, st])
    path = os.path.join(ROOT, 'Docs', 'reference', 'S2_ROSTER_MAP_2026-10-18.csv')
    with open(path, 'w', encoding='utf-8-sig', newline='') as f:
        w = csv.writer(f)
        w.writerow(['로스터', '우리 이름', '등급', 'S2 원작 id', 'S2 원작 이름', '상태'])
        w.writerows(out)
    return 'Docs/reference/S2_ROSTER_MAP_2026-10-18.csv'


def apply(plan, grades):
    written = []
    for p in plan:
        if 'all' not in grades and p['grade'] not in grades:
            continue
        path = os.path.join(ROSTER_DIR, p['file'] + '.asset')
        t = open(path, encoding='utf-8').read()
        for k, v in p['stats'].items():
            t = set_field(t, k, v)
        t = set_field(t, 'originalMatchName', yq(p['omn']))
        if p['replace']:
            keep_g = []
            for f in p['keep']:
                keep_g.append(re.search(r'guid: (\w+)', open(os.path.join(SKILL_DIR, f + '.asset.meta'), encoding='utf-8').read()).group(1))
            new_g = []
            for s in p['skills']:
                blk = S.Block(s['block']['name'])
                for k, v in s['block'].items():
                    setattr(blk, k, tuple(v) if k == 'gauge' and v else v)
                body, meta = skill_yaml(s['name'], s['skill_name'], s['desc'], blk, p['stats']['attackType'])
                open(os.path.join(SKILL_DIR, s['name'] + '.asset'), 'w', encoding='utf-8').write(body)
                open(os.path.join(SKILL_DIR, s['name'] + '.asset.meta'), 'w', encoding='utf-8').write(meta)
                new_g.append(guid_of(s['name']))
                written.append('Assets/Data/UnitSkills/%s.asset' % s['name'])
                written.append('Assets/Data/UnitSkills/%s.asset.meta' % s['name'])
            guids = keep_g + new_g
            block = '  skills:\n' + ''.join('  - {fileID: 11400000, guid: %s, type: 2}\n' % g for g in guids) if guids else '  skills: []\n'
            t, n = re.subn(r'(?m)^  skills:(?: \[\])?\n(?:  - \{.*\}\n)*', lambda m: block, t, count=1)
            assert n == 1, p['file']
            t = set_field(t, 'skill', '{fileID: 0}')
            for k, v in p['patch'].items():
                t = set_field(t, k, fnum(float(v)) if not isinstance(v, str) else v)
        open(path, 'w', encoding='utf-8').write(t)
        written.append('Assets/Data/Units/Roster/%s.asset' % p['file'])
    return written


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else 'plan'
    plan = build_plan()
    json.dump(plan, open(os.path.join(OUT, 'apply_plan.json'), 'w'), ensure_ascii=False, indent=1)
    n_chg, n_rep = report(plan)
    print('계획: 대상 %d · 능력치 바뀜 %d · 스킬 교체 %d · 새 스킬 %d' % (len(plan), n_chg, n_rep, sum(len(p['skills']) for p in plan)))
    if cmd == 'apply':
        grades = set(sys.argv[2:]) or {'all'}
        w = apply(plan, grades)
        w.append(write_vfx_csv(plan, grades))
        w.append(write_s2_map(plan))
        open(os.path.join(OUT, 'apply_written.txt'), 'w', encoding='utf-8').write('\n'.join(w) + '\n')
        print('썼음 %d파일 → %s' % (len(w), os.path.join(OUT, 'apply_written.txt')))


if __name__ == '__main__':
    main()
