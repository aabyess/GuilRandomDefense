"""SkillData 에셋(YAML)을 손으로 고치는 도우미 — 2026-09-30 구현담당1(구현담당2가 남긴 일 이어받으며).

생성기로 통째로 다시 쓰지 않고, 에셋 하나의 레벨 필드·효과 목록만 바꾸거나 새 에셋을 만든다.
함정 두 가지를 여기서 막는다:
  - 문자열이 「[」 등으로 시작하면 YAML 흐름 목록으로 읽혀 뒤 필드가 전부 기본값이 된다 → yaml_scalar로 감싼다.
  - 「effects: []」 인라인 빈 목록 뒤에 항목을 이어 붙이면 깨진다 → 블록 목록으로 바꿔 쓴다.
효과를 쓸 땐 randMin/randMax를 항상 1로 적는다(0,0이면 피해가 0이 되는 단골 사고).

쓰는 법(파이썬에서):
    import sys; sys.path.insert(0, 'Tools'); import skill_asset_tool as sat
    a = sat.load('SkillData_더미채널_초월_신문철_AP_79행_10000')
    sat.set_level_field(a, 0, 'triggerChance', 0.0125)
    sat.set_effects(a, 0, [sat.effect(kind=0, target=2, multiplier=47500, damageType=1, attackType=3)])
    sat.save(a)
"""
import hashlib
import os
import re
import unicodedata

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
SKILL_DIR = os.path.join(ROOT, 'Assets', 'Data', 'UnitSkills')
ROSTER_DIR = os.path.join(ROOT, 'Assets', 'Data', 'Units', 'Roster')
SKILL_SCRIPT_GUID = '9457f64cd84d34fd095791a069c0adc6'

# SkillLevel·SkillEffect 필드를 유니티가 쓰는 순서대로(없는 필드는 C# 기본값으로 읽힌다).
LEVEL_FIELDS = ['cooldown', 'triggerChance', 'range', 'hitCountThreshold', 'resetTo', 'gaugeKind',
                'requiredBuffId', 'forbiddenBuffId', 'selfBuffId', 'effects', 'requiredTargetBuffId',
                'forbiddenTargetBuffId', 'hitCountFloor', 'gaugeSpendAmount', 'aoeCenter']
EFFECT_FIELDS = ['kind', 'basis', 'target', 'damageType', 'attackType', 'multiplier', 'bonus', 'chance',
                 'hitCount', 'duration', 'casterBuffCountFactor', 'buffId', 'randMin', 'randMax',
                 'buffHitCharges', 'requiredTargetBuffId', 'forbiddenTargetBuffId', 'targetCondition',
                 'targetConditionValue', 'cascadeGroup']
STRING_FIELDS = {'requiredBuffId', 'forbiddenBuffId', 'selfBuffId', 'requiredTargetBuffId',
                 'forbiddenTargetBuffId', 'buffId'}


def yaml_scalar(value):
    """generate_unit_skills_by_gate.yaml_scalar와 같은 규칙."""
    value = '' if value is None else str(value)
    if value and (value[0] in "[]{}|>&*!%@`'\"#,?:-" or ': ' in value or ' #' in value):
        return "'" + value.replace("'", "''") + "'"
    return value


def num(x):
    if isinstance(x, bool):
        return str(int(x))
    if isinstance(x, int):
        return str(x)
    s = repr(float(x))
    return s[:-2] + '.0' if s.endswith('.0') else s


def _find(name):
    """파일명(확장자·접두사 유무 상관없이)을 NFC/NFD 차이까지 흡수해 찾는다."""
    want = unicodedata.normalize('NFC', name if name.startswith('SkillData_') else 'SkillData_' + name)
    if not want.endswith('.asset'):
        want += '.asset'
    for f in os.listdir(SKILL_DIR):
        if unicodedata.normalize('NFC', f) == want:
            return os.path.join(SKILL_DIR, f)
    raise FileNotFoundError(want)


class Asset:
    def __init__(self, path, head, levels):
        self.path, self.head, self.levels = path, head, levels   # levels: 레벨 텍스트 블록 리스트

    @property
    def text(self):
        return self.head + '  levels:\n' + ''.join(self.levels) if self.levels else self.head + '  levels: []\n'


def load(name):
    path = name if os.path.exists(name) else _find(os.path.basename(name))
    t = open(path, encoding='utf-8').read()
    head, sep, rest = t.partition('  levels:\n')
    if not sep:
        head, sep, rest = t.partition('  levels: []\n')
        return Asset(path, head, [])
    levels = re.split(r'(?=^  - cooldown:)', rest, flags=re.M)
    assert levels[0] == '', f'levels 앞에 예상 못 한 텍스트: {path}'
    return Asset(path, head, levels[1:])


def save(asset):
    open(asset.path, 'w', encoding='utf-8').write(asset.text)


def guid_of(asset_or_path):
    p = asset_or_path.path if isinstance(asset_or_path, Asset) else asset_or_path
    return re.search(r'guid: (\w+)', open(p + '.meta', encoding='utf-8').read()).group(1)


def set_head_field(asset, field, value):
    v = yaml_scalar(value) if field in ('skillName', 'description') else num(value)
    new, n = re.subn(r'^(  %s:) ?.*$' % field, lambda m: m.group(1) + ' ' + v, asset.head, count=1, flags=re.M)
    assert n == 1, field
    asset.head = new


def get_level_field(asset, i, field):
    m = re.search(r'^(?:  - |    )%s: ?(.*)$' % field, asset.levels[i], flags=re.M)
    return m.group(1) if m else None


def set_level_field(asset, i, field, value):
    """레벨 i의 필드를 바꾼다. 없으면 LEVEL_FIELDS 순서에 맞춰 끼운다."""
    L = asset.levels[i]
    v = yaml_scalar(value) if field in STRING_FIELDS else num(value)
    prefix = '  - ' if field == 'cooldown' else '    '
    new, n = re.subn(r'^(%s%s:) ?.*$' % (re.escape(prefix), field), lambda m: m.group(1) + ' ' + v, L, count=1, flags=re.M)
    if n == 0:
        # 뒤따르는 필드 중 이미 있는 첫 번째 앞에 끼운다. 없으면 맨 끝.
        after = LEVEL_FIELDS[LEVEL_FIELDS.index(field) + 1:]
        pos = len(L)
        for f in after:
            m = re.search(r'^    %s:' % f, L, flags=re.M)
            if m:
                pos = m.start(); break
        new = L[:pos] + '    %s: %s\n' % (field, v) + L[pos:]
    asset.levels[i] = new


def effect(**kw):
    """효과 하나(dict). 주지 않은 필드는 C# 기본값과 같은 값으로 채운다(randMin/randMax=1)."""
    e = dict(kind=0, basis=0, target=2, damageType=0, attackType=0, multiplier=0.0, bonus=0.0, chance=1,
             hitCount=1, duration=0.0, casterBuffCountFactor=0.0, buffId='', randMin=1.0, randMax=1.0,
             buffHitCharges=0, requiredTargetBuffId='', forbiddenTargetBuffId='', targetCondition=0,
             targetConditionValue=0.0, cascadeGroup=0)
    unknown = set(kw) - set(e)
    assert not unknown, unknown
    e.update(kw)
    return e


def effect_block(e):
    lines = []
    for k in EFFECT_FIELDS:
        v = e[k]
        s = yaml_scalar(v) if k in STRING_FIELDS else num(v)
        lines.append(('    - ' if k == 'kind' else '      ') + '%s: %s' % (k, s) if s != '' else ('      %s:' % k))
    return '\n'.join(lines) + '\n'


_EFFECTS_RE = re.compile(r'^    effects:(?: \[\])?\n((?:    - .*\n(?:      .*\n)*)*)', re.M)


def get_effect_blocks(asset, i):
    m = _EFFECTS_RE.search(asset.levels[i])
    if not m:
        return []
    return [b for b in re.split(r'(?=^    - kind:)', m.group(1), flags=re.M) if b]


def set_effects(asset, i, effects):
    """레벨 i의 효과 목록을 통째로 바꾼다. effects: effect() dict 또는 이미 있는 블록 문자열의 리스트."""
    body = ''.join(x if isinstance(x, str) else effect_block(x) for x in effects)
    L = asset.levels[i]
    m = _EFFECTS_RE.search(L)
    head = '    effects:\n' if body else '    effects: []\n'
    if m:
        L = L[:m.start()] + head + body + L[m.end():]
    else:
        set_level_field(asset, i, 'effects', 0)  # 자리만 잡고
        L = asset.levels[i].replace('    effects: 0\n', head + body)
    asset.levels[i] = L


def level_block(**kw):
    """새 레벨 텍스트. effects는 effect() 리스트."""
    effects = kw.pop('effects', [])
    d = dict(cooldown=0, triggerChance=1.0, range=0.0, hitCountThreshold=0, resetTo=0, gaugeKind=0,
             requiredBuffId='', forbiddenBuffId='', selfBuffId='', requiredTargetBuffId='',
             forbiddenTargetBuffId='', hitCountFloor=0, gaugeSpendAmount=0, aoeCenter=0)
    unknown = set(kw) - set(d)
    assert not unknown, unknown
    d.update(kw)
    out = []
    for k in LEVEL_FIELDS:
        if k == 'effects':
            out.append('    effects:\n' + ''.join(e if isinstance(e, str) else effect_block(e) for e in effects) if effects else '    effects: []\n')
            continue
        s = yaml_scalar(d[k]) if k in STRING_FIELDS else num(d[k])
        pre = '  - ' if k == 'cooldown' else '    '
        out.append(pre + ('%s: %s\n' % (k, s) if s != '' else '%s:\n' % k))
    return ''.join(out)


def new_asset(file_stem, skill_name, description, trigger_type, levels):
    """새 SkillData 에셋 + .meta. guid는 파일 이름에서 결정적으로 만든다(다시 돌려도 같다)."""
    stem = unicodedata.normalize('NFC', file_stem if file_stem.startswith('SkillData_') else 'SkillData_' + file_stem)
    path = os.path.join(SKILL_DIR, stem + '.asset')
    assert not os.path.exists(path), path
    guid = hashlib.md5(('guilrd/skill_asset_tool/' + stem).encode()).hexdigest()
    head = ('%%YAML 1.1\n%%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n'
            '  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n'
            '  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n'
            '  m_Script: {fileID: 11500000, guid: %s, type: 3}\n  m_Name: %s\n  m_EditorClassIdentifier:\n'
            '  skillName: %s\n  description: %s\n  triggerType: %d\n') % (
        SKILL_SCRIPT_GUID, stem, yaml_scalar(skill_name), yaml_scalar(description), trigger_type)
    a = Asset(path, head, list(levels))
    save(a)
    open(path + '.meta', 'w', encoding='utf-8').write(
        'fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n'
        '  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % guid)
    return a


def add_skill_to_unit(roster_name, skill_guid):
    """로스터 유닛의 skills 목록 끝에 붙인다(이미 있으면 그대로)."""
    p = os.path.join(ROSTER_DIR, roster_name + '.asset')
    t = open(p, encoding='utf-8').read()
    if skill_guid in t:
        return False
    line = '  - {fileID: 11400000, guid: %s, type: 2}\n' % skill_guid
    # skills가 비어 있고 단일 skill만 쓰던 유닛이면, skills가 생기는 순간 skill이 무시되므로
    # (UnitData.SkillCount — skills 우선) 기존 skill을 skills 맨 앞으로 옮기고 skill은 비운다.
    single = re.search(r'^  skill: \{fileID: 11400000, guid: (\w+), type: 2\}\n', t, flags=re.M)
    if single and re.search(r'^  skills: \[\]\n', t, flags=re.M):
        t = t.replace(single.group(0), '  skill: {fileID: 0}\n', 1)
        line = '  - {fileID: 11400000, guid: %s, type: 2}\n' % single.group(1) + line
    m = re.search(r'^  skills:\n((?:  - .*\n)*)', t, flags=re.M)
    if m:
        t = t[:m.end()] + line + t[m.end():]
    else:
        t, n = re.subn(r'^  skills: \[\]\n', '  skills:\n' + line, t, count=1, flags=re.M)
        assert n == 1, p
    open(p, 'w', encoding='utf-8').write(t)
    return True
