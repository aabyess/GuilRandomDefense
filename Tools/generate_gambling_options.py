#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""도박소 유닛 도박 4종의 GamblingOptionData 에셋을 만든다.

수치 근거는 Docs/reference/GAMBLING.md + PM 승인 내용을 그대로 옮긴 것뿐이라
여기 다시 적지 않는다. 재실행해도 guid는 이름 기반 md5라 안 바뀐다.
돈 도박(초급/중급/고급) 비용은 아직 확정 전이라 이번엔 만들지 않는다.
"""
import os, hashlib

OPTION_SCRIPT = 'e38dcfb2166e0425695e116cb8b725bc'

# GamblingCategory enum 순서 그대로.
MONEY, UNIT = 0, 1

# ResourceType enum 순서 그대로 (뒤에 끼우면 다른 에셋들이 깨진다).
WOOD, TOKEN, LUCKY_TOKEN, MANA = 0, 1, 2, 3

# UnitGrade enum 순서 그대로.
GRADE = {
    'Common': 0, 'Uncommon': 1, 'Special': 2, 'Rare': 3, 'Hidden': 4,
    'Legendary': 5, 'Limited': 6, 'Transcendent': 7, 'Immortal': 8,
    'Eternal': 9, 'RandomUnit': 10, 'OtherWorld': 11, 'Superior': 12,
}

OPTIONS = [
    dict(name='하급도박', desc='목재를 걸고 흔함이나 안흔함 등급 유닛을 노린다.',
         category=UNIT, costType=WOOD, cost=1, chance=85,
         primary=GRADE['Common'], useSecondary=1, secondary=GRADE['Uncommon'],
         successGoldMin=0, successGoldMax=0,
         grantFailure=0, failureTokens=2, failureWood=1,
         failureGoldMin=0, failureGoldMax=0),
    dict(name='중급도박', desc='목재를 걸고 특별함 등급 유닛을 노린다.',
         category=UNIT, costType=WOOD, cost=1, chance=70,
         primary=GRADE['Special'], useSecondary=0, secondary=GRADE['Special'],
         successGoldMin=0, successGoldMax=0,
         grantFailure=0, failureTokens=2, failureWood=1,
         failureGoldMin=0, failureGoldMax=0),
    dict(name='고급도박', desc='목재를 걸고 희귀함이나 특별함 등급 유닛을 노린다. 실패해도 행운의 토큰을 돌려받는다.',
         category=UNIT, costType=WOOD, cost=4, chance=70,
         primary=GRADE['Rare'], useSecondary=1, secondary=GRADE['Special'],
         successGoldMin=0, successGoldMax=0,
         grantFailure=1, failureTokens=2, failureWood=1,
         failureGoldMin=0, failureGoldMax=0),
    dict(name='다른세계 도박', desc='목재를 크게 걸고 다른세계 등급 유닛을 노린다. 실패해도 행운의 토큰을 돌려받는다.',
         category=UNIT, costType=WOOD, cost=5, chance=17,
         primary=GRADE['OtherWorld'], useSecondary=0, secondary=GRADE['OtherWorld'],
         successGoldMin=0, successGoldMax=0,
         grantFailure=1, failureTokens=2, failureWood=1,
         failureGoldMin=0, failureGoldMax=0),
]

HEAD = """%YAML 1.1
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
  m_Script: {fileID: 11500000, guid: __SCRIPT__, type: 3}
  m_Name: __NAME__
  m_EditorClassIdentifier:
"""


def guid_for(name):
    return hashlib.md5(('guilrd/gambling/' + name).encode()).hexdigest()


def write(path, body, guid):
    open(path, 'w', encoding='utf-8').write(body)
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


os.makedirs('Assets/Data/Gambling', exist_ok=True)

rows = []
for o in OPTIONS:
    ename = f"Gambling_{o['name']}"
    eguid = guid_for(ename)

    body = (HEAD.replace("__SCRIPT__", OPTION_SCRIPT).replace("__NAME__", ename) +
            f"  optionName: {o['name']}\n"
            f"  description: {o['desc']}\n"
            f"  category: {o['category']}\n"
            f"  costResourceType: {o['costType']}\n"
            f"  cost: {o['cost']}\n"
            f"  successChancePercent: {o['chance']}\n"
            f"  primaryResultGrade: {o['primary']}\n"
            f"  useSecondaryGrade: {o['useSecondary']}\n"
            f"  secondaryResultGrade: {o['secondary']}\n"
            f"  successGoldMin: {o['successGoldMin']}\n"
            f"  successGoldMax: {o['successGoldMax']}\n"
            f"  grantFailureReward: {o['grantFailure']}\n"
            f"  failureLuckyTokens: {o['failureTokens']}\n"
            f"  failureWood: {o['failureWood']}\n"
            f"  failureGoldMin: {o['failureGoldMin']}\n"
            f"  failureGoldMax: {o['failureGoldMax']}\n")

    write(f'Assets/Data/Gambling/{ename}.asset', body, eguid)
    rows.append((o['name'], o['cost'], o['chance'], o['primary'], o['useSecondary'], o['grantFailure']))

print(f"{len(rows)}개 도박 옵션 에셋 생성 완료 (Assets/Data/Gambling/)\n")
print("  이름            목재  확률  주등급  부등급여부  실패보상")
for name, cost, chance, primary, useSecondary, grantFailure in rows:
    print(f"  {name:<14} {cost:>4} {chance:>4}%  {primary:>6}  {useSecondary:>8}  {grantFailure:>8}")
