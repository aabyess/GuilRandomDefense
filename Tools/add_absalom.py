#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""원작 h010 「압살롬 투명인간 - 특별함」을 새 특별함 유닛으로, h069 압살롬 도박을 새 도박 옵션으로 넣는다 (2026-10-02, PM 지시).

근거(war3map_new.j / w3u 직접 디코드):
  - 도박 j:13141 `GetRandomPercentageBJ()<=45` 이면 h010 지급(13143), 아니면 실패 문구 — 성공 45%. h069 w3u: 500골드·목재1·utub「45%확률로 압살롬」·ureq Rhde(R20 보스 처치 해금, j:84872).
  - h010 능력 A0JE·A07Y·A080·A01V·Avul = 모으기·창고·판매·조합뿐(전투 스킬 없음). atk 300 · 쿨 0.6 · 사거리 450은 기존 특별함 33행에 이미 들어 있어
    스탯 쌍둥이(특별함_배성령: 300 / 1.6667 / 123.25)의 값을 그대로 쓴다.
  - 원작 풀 소속: 중급도박(Random3, Ran_3 렉트 안 h010 배치) → MainGachaTable 특별함 풀에 추가.
재실행 안전(이미 있으면 건너뜀). 모델은 PM이 ArtBinder로 배선 — prefab은 UnitPrefab 템플릿.
"""
import hashlib, os, re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
os.chdir(ROOT)
TWIN = 'Assets/Data/Units/Roster/특별함_배성령.asset'
UNIT_PATH = 'Assets/Data/Units/Roster/특별함_압살롬.asset'
OPT_PATH = 'Assets/Data/Gambling/Gambling_압살롬 도박.asset'
GACHA = 'Assets/Data/MainGachaTable.asset'
TEMPLATE_PREFAB = '{fileID: 1000000000000000001, guid: 4e2d5e92014e4a2e9ec20862d26bf3b8, type: 3}'  # Assets/Prefabs/UnitPrefab.prefab


def guid_for(name):
    return hashlib.md5(('guilrd/absalom/' + name).encode()).hexdigest()


def write_meta(path, guid):
    open(path + '.meta', 'w', encoding='utf-8').write(
        'fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n'
        '  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % guid)


def yq(s):
    return '"' + ''.join(c if ord(c) < 128 else '\\u%04X' % ord(c) for c in s) + '"'


unit_guid = guid_for('unit')
if not os.path.exists(UNIT_PATH):
    t = open(TWIN, encoding='utf-8').read()
    t = re.sub(r'^  m_Name: .*$', lambda m: '  m_Name: ' + yq('특별함_압살롬'), t, count=1, flags=re.M)
    t = re.sub(r'^  unitName: .*$', lambda m: '  unitName: ' + yq('압살롬'), t, count=1, flags=re.M)
    t = re.sub(r'^  prefab: .*$', lambda m: '  prefab: ' + TEMPLATE_PREFAB, t, count=1, flags=re.M)
    open(UNIT_PATH, 'w', encoding='utf-8').write(t)
    write_meta(UNIT_PATH, unit_guid)
    print('유닛 만듦', UNIT_PATH)

if not os.path.exists(OPT_PATH):
    t = open('Assets/Data/Gambling/Gambling_고급도박.asset', encoding='utf-8').read()
    def setf(t, key, val):
        t, n = re.subn(r'^  %s: .*$' % key, '  %s: %s' % (key, val), t, count=1, flags=re.M)
        assert n == 1, key
        return t
    t = re.sub(r'^  m_Name: .*$', lambda m: '  m_Name: Gambling_압살롬 도박', t, count=1, flags=re.M)
    for k, v in [
        ('optionName', '압살롬 도박'),
        ('description', '500엔과 목재 1개를 걸고 45% 확률로 압살롬을 얻는다. 20라운드 보스를 잡으면 열린다.'),
        ('cost', 1), ('goldCost', 500), ('successChancePercent', 45),
        ('grantFailureReward', 0), ('scalesWithGamblerNavigation', 0), ('grantsUniqueRerollOnGenericSuccess', 0),
        ('announceFailLabel', '☆압★살☆롬★'), ('announceSuccessLabel', '☆압★살☆롬★'),
        ('requiresUnlock', 1), ('unlockHint', '20라운드 보스 처치 후 해금'), ('unlockAtRound', 0),
    ]:
        t = setf(t, k, v)
    if re.search(r'^  unlockRound: ', t, re.M):
        t = setf(t, 'unlockRound', 20)
    else:
        t = t.rstrip('\n') + '\n  unlockRound: 20\n'
    # 보너스 유닛 = 압살롬, 100% (성공하면 항상 이 유닛)
    for k, v in [('bonusUnit', '{fileID: 11400000, guid: %s, type: 2}' % unit_guid), ('bonusChancePercent', 100)]:
        if re.search(r'^  %s: ' % k, t, re.M):
            t = setf(t, k, v)
        else:
            t = t.rstrip('\n') + '\n  %s: %s\n' % (k, v)
    open(OPT_PATH, 'w', encoding='utf-8').write(t)
    write_meta(OPT_PATH, guid_for('option'))
    print('도박 만듦', OPT_PATH)

g = open(GACHA, encoding='utf-8').read()
ref = '    - {fileID: 11400000, guid: %s, type: 2}\n' % unit_guid
if unit_guid not in g:
    m = re.search(r'(  - grade: 2\n    weight: [\d.]+\n    pool:\n(?:    - .*\n)*)', g)
    g = g[:m.end(1)] + ref + g[m.end(1):]
    open(GACHA, 'w', encoding='utf-8').write(g)
    print('가챠 특별함 풀에 추가')
