#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""다른세계 강화소·영원함 강화소의 UnitUpgradeTrackData 에셋(각 1개)을 만든다.

유닛강화소(Tools/generate_unit_upgrade_tracks.py)와 같은 틀. 수치는 리서치담당의
[제안](Docs/reference/UPGRADE_SHOP.md "2차 조사")을 기반으로, UnitUpgradeTrackData가
엔 단일 통화라 목재 비용 부분만 뺐다 — 구현담당1과 합의. 상한 5레벨·레벨당 +15%로
유닛강화소(10레벨·+10%)보다 적게, 대신 세게 — 상위 등급 강화소라는 취지.

재실행해도 guid는 안 바뀐다.
"""
import os, hashlib

TRACK_SCRIPT = 'af9413764dc9047d893ae0a4d3fd02fa'  # UnitUpgradeTrackData.cs

# UnitData.cs의 UnitGrade enum 순서 그대로.
GRADE = dict(Common=0, Uncommon=1, Special=2, Rare=3, Hidden=4, Legendary=5,
             Limited=6, Transcendent=7, Immortal=8, Eternal=9, RandomUnit=10, OtherWorld=11)

TRACKS = [
    # cost는 UnitUpgradeTrackData.CostForLevel(0)에 그대로 들어가는 costBase다 —
    # 즉 "1레벨 비용" = cost 그 자체다(유닛강화소 스크립트와 같은 관례).
    dict(id='다른세계', name='다른세계 강화', grades=['OtherWorld'],
         cost=10000, color=(0.6, 0.25, 0.75)),
    dict(id='영원함', name='영원함 강화', grades=['Eternal'],
         cost=20000, color=(0.95, 0.9, 0.6)),
]

MAX_LEVEL = 5
COST_GROWTH = 2.0
STAT_GROWTH = 1.15

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
    return hashlib.md5(('guilrd/unitupgrades/' + name).encode()).hexdigest()


def write(path, body, guid):
    open(path, 'w', encoding='utf-8').write(body)
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


os.makedirs('Assets/Data/UnitUpgrades', exist_ok=True)

rows = []
for t in TRACKS:
    ename = f"UnitUpgrade_{t['name']}"
    eguid = guid_for(f"UnitUpgrade_{t['id']}")

    grades_yaml = "\n".join(f"  - {GRADE[g]}" for g in t['grades'])
    r, g, b = t['color']

    body = (HEAD.replace("__SCRIPT__", TRACK_SCRIPT).replace("__NAME__", ename) +
            f"  trackName: {t['name']}\n"
            f"  description: {t['name']} — 해당 등급 유닛 전체의 공격력을 영구히 올립니다.\n"
            f"  targetGrades:\n{grades_yaml}\n"
            f"  maxLevel: {MAX_LEVEL}\n"
            f"  costBase: {t['cost']}\n"
            f"  costGrowthPerLevel: {COST_GROWTH}\n"
            f"  statGrowthPerLevel: {STAT_GROWTH}\n"
            f"  slotColor: {{r: {r}, g: {g}, b: {b}, a: 1}}\n")

    write(f'Assets/Data/UnitUpgrades/{ename}.asset', body, eguid)
    rows.append((t['name'], t['cost']))

print(f"{len(rows)}개 트랙 에셋 생성 완료 (Assets/Data/UnitUpgrades/)\n")
for name, cost in rows:
    print(f"  {name:<12} 1레벨 {cost}엔 (레벨5 {round(cost * COST_GROWTH**4)}엔)")
