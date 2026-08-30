#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""라운드 1~75의 EnemyData / WaveData 에셋을 만든다.

이름은 Tools/enemy_roster.py (사용자 제공). 보스 라운드에는 잡몹이 나오지 않는다.
수치는 1차 추정치이고, 조정하려면 아래 상수만 고치면 된다.
"""
import os, sys, hashlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from enemy_roster import ENEMIES, BOSSES, TOTAL_ROUNDS

ENEMY_SCRIPT = 'a91828e7d70cc420499707cd4f2912a5'
WAVE_SCRIPT  = '1ec64fc963a894c26aeb41f20fc037bc'
MOB_PREFAB_GUID   = '85f952ec56b9f4258b079a5873623d2b'
MOB_PREFAB_FILEID = '3685248672397471059'

WISPS = {
    '흔함':   '637456e2263b4fc495bc3cd869efd84f',
    '안흔함': 'e610498c4baf4cc88aa036f9ce7b0799',
    '특별함': '81eac83fa14e4fa8a0fefc992a32453b',
    '희귀함': 'e343898df3db474d85f1c7d1582a1334',
    '전설':   'b6f67a2135764203900043ae518e8a0a',
}

# --- 밸런스 상수 ---
BASE_COUNT, COUNT_STEP, MAX_COUNT = 15, 3, 35   # 3라운드마다 +1, 원작 상한 35
BASE_HP, HP_GROWTH = 10.0, 1.09                 # 라운드마다 9%
BOSS_HP_MULTIPLIER = 15.0
MOB_SPEED, BOSS_SPEED = 5.0, 3.5
SPAWN_WINDOW = 24.0                             # 28초 라운드 중 스폰에 쓰는 시간

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

def head(script, name):
    return HEAD.replace("__SCRIPT__", script).replace("__NAME__", name)

def guid_for(name):
    return hashlib.md5(('guilrd/' + name).encode()).hexdigest()

def write(path, body, guid):
    open(path, 'w', encoding='utf-8').write(body)
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")

def wisp_rewards(rnd, boss):
    """라운드 클리어 위습. 보스 라운드에는 한 단계 위 위습을 얹어준다."""
    rewards = [('흔함', 2 if rnd <= 20 else 3)]
    if boss:
        if   rnd <= 20: rewards.append(('안흔함', 1))
        elif rnd <= 40: rewards.append(('특별함', 1))
        elif rnd <= 60: rewards.append(('희귀함', 1))
        else:           rewards.append(('전설', 1))
    return rewards

for folder in ('Assets/Data/Enemies', 'Assets/Data/Waves'):
    os.makedirs(folder, exist_ok=True)
    for f in os.listdir(folder):
        os.remove(os.path.join(folder, f))

rows = []
for rnd in range(1, TOTAL_ROUNDS + 1):
    boss = rnd in BOSSES
    name = BOSSES[rnd][0] if boss else ENEMIES[rnd]
    stage = BOSSES[rnd][1] if boss else ''

    hp = round(BASE_HP * HP_GROWTH ** (rnd - 1) * (BOSS_HP_MULTIPLIER if boss else 1), 1)
    speed = BOSS_SPEED if boss else MOB_SPEED
    gold = 100 + rnd * 5 if boss else 5 + rnd
    count = 1 if boss else min(MAX_COUNT, BASE_COUNT + (rnd - 1) // COUNT_STEP)
    interval = 0.0 if boss else round(SPAWN_WINDOW / count, 2)

    safe = name.replace(' ', '_').replace('(', '').replace(')', '')
    ename = f"Enemy_R{rnd:02d}_{safe}"
    eguid = guid_for(ename)
    write(f'Assets/Data/Enemies/{ename}.asset',
          head(ENEMY_SCRIPT, ename) +
          f"  enemyName: {name}\n  hp: {hp}\n  moveSpeed: {speed}\n  goldReward: {gold}\n"
          f"  resourceRewards: []\n  rewardsAllPlayers: 0\n  isBoss: {1 if boss else 0}\n"
          f"  prefab: {{fileID: {MOB_PREFAB_FILEID}, guid: {MOB_PREFAB_GUID}, type: 3}}\n",
          eguid)

    rewards = "".join(
        f"  - wisp: {{fileID: 11400000, guid: {WISPS[grade]}, type: 2}}\n    count: {n}\n"
        for grade, n in wisp_rewards(rnd, boss))

    wname = f'Wave_Round{rnd:02d}'
    write(f'Assets/Data/Waves/{wname}.asset',
          head(WAVE_SCRIPT, wname) +
          f"  roundNumber: {rnd}\n  spawnList:\n"
          f"  - enemyData: {{fileID: 11400000, guid: {eguid}, type: 2}}\n"
          f"    count: {count}\n    spawnInterval: {interval}\n"
          f"  wispRewards:\n{rewards}",
          guid_for(wname))

    rows.append((rnd, name, stage, count, hp, gold, boss))

print(f"{len(rows)}개 라운드 생성\n")
print("  라운드  이름                마릿수    체력      골드")
for rnd, name, stage, count, hp, gold, boss in rows:
    if boss or rnd % 10 == 1 or rnd in (5, 35, 55):
        tag = f"  ★보스 — {stage}" if boss else ""
        print(f"   {rnd:>3}  {name:<16} {count:>4} {hp:>10} {gold:>6}{tag}")
