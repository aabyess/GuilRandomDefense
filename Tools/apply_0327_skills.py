#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""0.3.27 「유지 56기 정밀 대조」 적용(사장님 승인 10-18) — 원작능력_·원작오라_·게이트_·더미채널_·원작트리거_ 스킬 수치를 S2 2.323 툴팁 값으로.
규칙(사장님): ① SkillData_사장님_*·공용_*는 안 건드린다 ② skillName·표시 이름은 그대로(수치만) ③ 새로 만드는 스킬만 S2 능력 이름을 쓴다.
사용: ~/grd/GRD_unit_sheets/venv/bin/python Tools/apply_0327_skills.py [dry|apply]  → 변경 로그 ~/grd/GRD_rematch/적용_0327_로그.json
"""
import json, os, re, sys
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.dirname(HERE)
sys.argv_saved = sys.argv; sys.argv = sys.argv[:1]
sys.path.insert(0, HERE)
import rematch_s2_apply as A   # noqa: E402
import rematch_s2_skills as S   # noqa: E402
sys.argv = sys.argv_saved
SK = os.path.join(ROOT, 'Assets/Data/UnitSkills'); RO = os.path.join(ROOT, 'Assets/Data/Units/Roster')
KIND = S.KIND
LOG = []
STORE = {}   # 경로 → 편집 중 텍스트(같은 파일을 여러 번 고칠 때)
NEWFILES = {}   # 경로 → 새 파일 텍스트
DELETE = []     # 지울 에셋

def rd(p): return open(p, encoding='utf-8').read()
def wr(p, t): open(p, 'w', encoding='utf-8').write(t)
def fmt(v):
    if isinstance(v, bool): return '1' if v else '0'
    if isinstance(v, float) and abs(v - round(v)) < 1e-9: return str(int(round(v)))
    if isinstance(v, float): return ('%.6f' % v).rstrip('0').rstrip('.')
    return str(v)

def level0(t):
    i = t.index('  levels:\n  - cooldown:')
    m = re.search(r'(?m)^(?:  - cooldown:|  [A-Za-z])', t[i + 12:])
    end = i + 12 + m.start() if m else len(t)
    return i, end

def effect_spans(t):
    i, end = level0(t)
    body = t[i:end]
    es = body.index('    effects:\n') + len('    effects:\n')
    # effects 끝 = 4칸 키(「    requiredTargetBuffId:」 등)
    m = re.search(r'(?m)^    [A-Za-z]', body[es:])
    ee = es + m.start() if m else len(body)
    starts = [x.start() for x in re.finditer(r'(?m)^    - kind:', body[es:ee])]
    spans = [(es + s, es + (starts[n + 1] if n + 1 < len(starts) else ee - es)) for n, s in enumerate(starts)]
    return i, body, es, ee, spans

def set_level(t, key, val):
    i, end = level0(t)
    body = t[i:end]
    es = body.index('    effects:\n'); m = re.search(r'(?m)^    [A-Za-z]', body[es + 12:])
    pat = re.compile(r'(?m)^    %s: .*$' % re.escape(key))
    mm = pat.search(body)
    assert mm, key
    body = body[:mm.start()] + '    %s: %s' % (key, fmt(val)) + body[mm.end():]
    return t[:i] + body + t[end:]

def set_effect(t, idx, key, val):
    i, body, es, ee, spans = effect_spans(t)
    s, e = spans[idx]
    blk = body[s:e]
    if key == 'kind': pat = re.compile(r'(?m)^    - kind: .*$'); rep = '    - kind: %s' % fmt(val)
    else: pat = re.compile(r'(?m)^      %s: .*$' % re.escape(key)); rep = '      %s: %s' % (key, fmt(val))
    mm = pat.search(blk)
    if mm: blk = blk[:mm.start()] + rep + blk[mm.end():]
    else: blk = blk.rstrip('\n') + '\n' + rep + '\n'
    body = body[:s] + blk + body[e:]
    return t[:i] + body + t[level0(t)[1]:]

def effect_text(e, atk_type=1):
    return A.effect_yaml(e, atk_type)

def add_effects(t, effs, atk_type=1):
    i, body, es, ee, spans = effect_spans(t)
    add = ''.join(effect_text(e, atk_type) for e in effs)
    body = body[:ee] + add + body[ee:]
    return t[:i] + body + t[level0(t)[1]:]

def replace_effects(t, effs, atk_type=1):
    i, body, es, ee, spans = effect_spans(t)
    add = ''.join(effect_text(e, atk_type) for e in effs)
    body = body[:es] + add + body[ee:]
    return t[:i] + body + t[level0(t)[1]:]

def remove_effect(t, idx):
    i, body, es, ee, spans = effect_spans(t)
    s, e = spans[idx]
    body = body[:s] + body[e:]
    return t[:i] + body + t[level0(t)[1]:]

def eff(kind, target, mult, **kw):
    return S.eff(kind, target, mult, **kw)

def summary(t):
    import yaml
    tt = re.sub(r'^%.*\n', '', t, flags=re.M); tt = re.sub(r'^--- !u!\d+ &\d+', '---', tt, flags=re.M)
    d = yaml.load(tt, Loader=yaml.CSafeLoader)['MonoBehaviour']
    lv = d['levels'][0]
    s = '발동%s 확률%g 쿨%g 범위%g 게이지%g' % (d['triggerType'], lv['triggerChance'], lv['cooldown'], lv['range'], lv['hitCountThreshold'])
    for e in lv['effects']:
        s += ' | k%d b%d t%d m%g+%g d%g' % (e['kind'], e.get('basis', 0), e.get('target', 0), e.get('multiplier', 0), e.get('bonus', 0), e.get('duration', 0))
        for k in ('damageType', 'randMin', 'randMax', 'maxTargets', 'targetCondition', 'targetConditionValue', 'requiredTargetBuffId', 'fixedDamage', 'explosive'):
            v = e.get(k)
            if v not in (0, '', None, 1) or (k in ('fixedDamage', 'explosive') and v): s += ' %s=%s' % (k, v)
    return s

def edit(unit, skill, level=None, effects=None, add=None, replace=None, remove=None, atk_type=1):
    p = os.path.join(SK, 'SkillData_%s.asset' % skill)
    assert not skill.startswith(('사장님_', '공용_')), skill
    t = STORE.get(p) or rd(p); before = summary(t)
    for k, v in (level or {}).items(): t = set_level(t, k, v)
    for idx, kv in (effects or {}).items():
        for k, v in kv.items(): t = set_effect(t, idx, k, v)
    if remove is not None:
        for idx in sorted(remove, reverse=True): t = remove_effect(t, idx)
    if replace: t = replace_effects(t, replace, atk_type)
    if add: t = add_effects(t, add, atk_type)
    after = summary(t)
    LOG.append(dict(unit=unit, skill=skill, before=before, after=after, kind='수정'))
    STORE[p] = t
    return p, t

def new_skill(unit, aid, sname, desc, blk, atk_type=1, caster_center=False, idx=1):
    name = 'SkillData_S2_%s_%s_%d' % (unit, aid, idx)
    body, meta = A.skill_yaml(name, sname, desc, blk, atk_type)
    if caster_center: body = body.replace('    aoeCenter: 0\n', '    aoeCenter: 1\n', 1)
    return name, body, meta

def block(trigger, chance=1.0, rng=0.0, cooldown=0.0, effects=()):
    b = S.Block('x'); b.trigger = trigger; b.chance = chance; b.range = rng; b.cooldown = cooldown; b.effects = list(effects)
    return b

def add_to_roster(unit, guid):
    p = os.path.join(RO, unit + '.asset'); t = STORE.get(p) or rd(p)
    line = '  - {fileID: 11400000, guid: %s, type: 2}\n' % guid
    m = re.search(r'(?m)^  skills:(?: \[\])?\n(?:  - \{.*\}\n)*', t)
    cur = m.group(0)
    new = '  skills:\n' + (cur.split('\n', 1)[1] if cur.rstrip() != '  skills: []' else '') + line
    assert line not in cur, unit
    t = t[:m.start()] + new + t[m.end():]
    STORE[p] = t
    return p, t

def remove_from_roster(unit, skillname):
    meta = os.path.join(SK, 'SkillData_%s.asset.meta' % skillname)
    g = re.search(r'guid: (\w+)', rd(meta)).group(1)
    p = os.path.join(RO, unit + '.asset'); t = STORE.get(p) or rd(p)
    line = '  - {fileID: 11400000, guid: %s, type: 2}\n' % g
    assert line in t, (unit, skillname)
    t = t.replace(line, '', 1); STORE[p] = t
    return p, t

if __name__ == '__main__':
    mode = sys.argv[1] if len(sys.argv) > 1 else 'dry'
    exec(open(os.path.join(HERE, 'apply_0327_ops.py'), encoding='utf-8').read())
