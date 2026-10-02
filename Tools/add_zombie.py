#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""원작 h00H 「좀비 - 안흔함」을 새 안흔함 유닛으로, A029(좀비×3 + 나미 = 압살롬)를 조합식으로 넣는다 (2026-10-02, PM 지시).

근거(war3map_new.w3u / w3a / j 직접 디코드):
  - h00H: ua1b 98 · ua1c 0.61(공속 1/0.61=1.6393) · ua1r 450 · uabi A029,Avul → 전투 스킬 없음·판매/창고 능력도 없음. 스탯 쌍둥이 = 안흔함_황정기(98/1.6393).
  - A029(AHwe 껍데기) aub1 「좀비 + 좀비 + 좀비 + 나미 = 압살롬」 · ORIGINAL_FUSION_RECIPES.csv:104 (재료 h00H×3, h001×1).
  - h001 나미 = 우리 흔함_강재규(원작 ua1b 20·쿨 0.98 → 22/1.0204, fa005c34가 같은 근거로 22로 맞춤).
  - 좀비는 가챠 풀 밖(원작 Ran_1 밖 배치, ROSTER_DENOMINATOR_GACHA_POOL §②) → MainGachaTable에 안 넣는다. 획득 경로(모리아 A113)는 보류.
재실행 안전. prefab은 UnitPrefab 템플릿(모델은 PM이 배선).
"""
import hashlib, os, re

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
os.chdir(ROOT)
TWIN = 'Assets/Data/Units/Roster/안흔함_황정기.asset'
UNIT_PATH = 'Assets/Data/Units/Roster/안흔함_좀비.asset'
RECIPE_PATH = 'Assets/Data/Recipes/특별함_압살롬_조합.asset'
TEMPLATE_PREFAB = '{fileID: 1000000000000000001, guid: 4e2d5e92014e4a2e9ec20862d26bf3b8, type: 3}'  # Assets/Prefabs/UnitPrefab.prefab
NAMI_GUID = '4938133f679e4543997bf1cb25005f90'   # 흔함_강재규


def guid_for(name):
    return hashlib.md5(('guilrd/zombie/' + name).encode()).hexdigest()


def meta_of(path):
    return re.search(r'^guid: (\w+)', open(path + '.meta', encoding='utf-8').read(), re.M).group(1)


def write_meta(path, guid):
    open(path + '.meta', 'w', encoding='utf-8').write(
        'fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n'
        '  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % guid)


def yq(s):
    return '"' + ''.join(c if ord(c) < 128 else '\\u%04X' % ord(c) for c in s) + '"'


def setf(t, key, val):
    t, n = re.subn(r'^  %s: .*$' % key, lambda m: '  %s: %s' % (key, val), t, count=1, flags=re.M)
    assert n == 1, key
    return t


unit_guid = guid_for('unit')
if not os.path.exists(UNIT_PATH):
    t = open(TWIN, encoding='utf-8').read()
    t = setf(t, 'm_Name', yq('안흔함_좀비'))
    t = setf(t, 'unitName', yq('좀비'))
    t = setf(t, 'prefab', TEMPLATE_PREFAB)
    t = setf(t, 'skill', '{fileID: 0}')
    # 원작 h00H는 판매·창고 능력이 없다(uabi A029,Avul) — 판매 보상 0
    t = setf(t, 'sellRewardWisp', '{fileID: 0}')
    t = setf(t, 'sellRewardWispChance', 0)
    t = setf(t, 'sellRewardWood', 0)
    t = setf(t, 'sellRewardWoodChance', 0)
    open(UNIT_PATH, 'w', encoding='utf-8').write(t)
    write_meta(UNIT_PATH, unit_guid)
    print('유닛 만듦', UNIT_PATH)
else:
    unit_guid = meta_of(UNIT_PATH)

if not os.path.exists(RECIPE_PATH):
    t = open('Assets/Data/Recipes/특별함_주영호_조합.asset', encoding='utf-8').read()
    head = t[:t.index('  commandId:')]
    head = re.sub(r'^  m_Name: .*$', lambda m: '  m_Name: 특별함_압살롬_조합', head, count=1, flags=re.M)
    absalom = meta_of('Assets/Data/Units/Roster/특별함_압살롬.asset')
    ing = lambda g, c: ('  - kind: 0\n    unit: {fileID: 11400000, guid: %s, type: 2}\n    item: {fileID: 0}\n'
                        '    wildcardGrade: 0\n    count: %d\n' % (g, c))
    body = (head + '  commandId: 압살롬\n  result: {fileID: 11400000, guid: %s, type: 2}\n  ingredients:\n' % absalom
            + ing(unit_guid, 3) + ing(NAMI_GUID, 1)
            + '  goldCost: 0\n  resourceCosts: []\n  minRound: 0\n  maxRound: 0\n  requiredSaveCount: 0\n')
    open(RECIPE_PATH, 'w', encoding='utf-8').write(body)
    write_meta(RECIPE_PATH, guid_for('recipe'))
    print('조합식 만듦', RECIPE_PATH)
