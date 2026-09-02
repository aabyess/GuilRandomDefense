#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""유닛강화소 8칸의 UnitUpgradeTrackData 에셋을 만든다.

수치는 리서치담당의 [제안](Docs/reference/UPGRADE_SHOP.md "2차 조사") 그대로다 — 원작에
"등급 전체 강화" 시스템 자체가 없어서 사용자 확정 전까지 가제다. 문서가 명시한 5개
(흔함&안흔함/특별함/희귀함/전설적인/제한됨)는 그대로 옮기고, 문서 밖 3개(초월/불멸/랜덤유닛)는
같은 비율(x2.5 안팎)로 이어서 채웠다 — 이 3개는 순수 추정치라 사장님 확인 필요.

공통: 레벨당 공격력 x1.1(10레벨 누적 +159%), 비용은 costBase * 1.5^레벨, 최대 10레벨.

재실행해도 guid는 안 바뀐다.
"""
import os, hashlib

TRACK_SCRIPT = 'af9413764dc9047d893ae0a4d3fd02fa'  # UnitUpgradeTrackData.cs

# UnitData.cs의 UnitGrade enum 순서 그대로.
GRADE = dict(Common=0, Uncommon=1, Special=2, Rare=3, Hidden=4, Legendary=5,
             Limited=6, Transcendent=7, Immortal=8, Eternal=9, RandomUnit=10, OtherWorld=11)

TRACKS = [
    dict(id='흔함안흔함', name='흔함·안흔함 강화', grades=['Common', 'Uncommon'],
         cost=100, color=(0.3, 0.7, 0.35)),
    dict(id='특별함', name='특별함 강화', grades=['Special'],
         cost=300, color=(0.9, 0.85, 0.2)),
    dict(id='희귀함', name='희귀함 강화', grades=['Rare'],
         cost=800, color=(0.6, 0.3, 0.85)),
    dict(id='전설적인', name='전설적인 강화', grades=['Legendary'],
         cost=2000, color=(0.85, 0.2, 0.2)),
    dict(id='제한됨', name='제한됨 강화', grades=['Limited'],
         cost=5000, color=(0.9, 0.5, 0.15)),
    # 아래 3개는 문서 밖 추정치(x2.5 비율 연장) — 사장님 확인 전 가제.
    dict(id='초월', name='초월 강화', grades=['Transcendent'],
         cost=12000, color=(0.2, 0.85, 0.85)),
    dict(id='불멸', name='불멸 강화', grades=['Immortal'],
         cost=30000, color=(0.95, 0.95, 0.95)),
    dict(id='랜덤유닛', name='랜덤유닛 강화', grades=['RandomUnit'],
         cost=75000, color=(0.5, 0.5, 0.5)),
]

MAX_LEVEL = 10
COST_GROWTH = 1.5
STAT_GROWTH = 1.1

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

print(f"{len(rows)}개 유닛강화 트랙 에셋 생성 완료 (Assets/Data/UnitUpgrades/)\n")
for name, cost in rows:
    print(f"  {name:<12} 1레벨 {cost}엔")
