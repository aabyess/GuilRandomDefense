#!/usr/bin/env python3
"""라운드 1~10의 EnemyData / WaveData 에셋을 만든다.

M4 재미 검증에는 한 판을 끝까지 돌려볼 수 있어야 하는데 Wave_Round1 하나뿐이라
2라운드부터 적이 안 나왔다. 수치는 전부 1차 추정치이고, M4에서 조정하는 게 목적이다.

- 마릿수: 15에서 시작해 라운드마다 +2 (10라운드 33마리). 원작은 라운드당 35마리지만
  처음부터 35를 넣으면 데스카운트(활성 25 초과 시 차감)에 바로 걸려 검증이 안 된다.
- 체력: 라운드마다 1.25배. 10라운드 약 75 — 안흔함 유닛 몇 기로 감당되는 선.
- 보스: 10라운드. 체력 12배, 이동속도 60%.
"""
import os, hashlib

ENEMY_SCRIPT = 'a91828e7d70cc420499707cd4f2912a5'
WAVE_SCRIPT  = '1ec64fc963a894c26aeb41f20fc037bc'
MOB_PREFAB_GUID   = '85f952ec56b9f4258b079a5873623d2b'
MOB_PREFAB_FILEID = '3685248672397471059'
WISP_COMMON_GUID  = '637456e2263b4fc495bc3cd869efd84f'   # Wisp_흔함선택

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

os.makedirs('Assets/Data/Enemies', exist_ok=True)
os.makedirs('Assets/Data/Waves', exist_ok=True)

rows = []
for rnd in range(1, 11):
    boss = (rnd % 10 == 0)
    hp    = round(10 * 1.25 ** (rnd - 1) * (12 if boss else 1), 1)
    speed = round(3.0 * (0.6 if boss else 1.0), 2)
    gold  = 5 + rnd * 2 + (50 if boss else 0)
    count = 1 if boss else 15 + 2 * (rnd - 1)
    interval = 0.0 if boss else round(24.0 / count, 2)
    # 라운드 클리어 위습. 안흔함 이상은 해당 등급 포탈이 씬에 생긴 뒤에 넣는다.
    wisps = 5 if boss else (2 if rnd <= 4 else 3)

    ename = f'Enemy_R{rnd:02d}' + ('_Boss' if boss else '')
    eguid = guid_for(ename)
    write(f'Assets/Data/Enemies/{ename}.asset',
          head(ENEMY_SCRIPT, ename) +
          f"  enemyName: {'보스' if boss else '잡몹'} R{rnd}\n"
          f"  hp: {hp}\n  moveSpeed: {speed}\n  goldReward: {gold}\n"
          f"  resourceRewards: []\n  rewardsAllPlayers: 0\n  isBoss: {1 if boss else 0}\n"
          f"  prefab: {{fileID: {MOB_PREFAB_FILEID}, guid: {MOB_PREFAB_GUID}, type: 3}}\n",
          eguid)

    wname = f'Wave_Round{rnd}'
    write(f'Assets/Data/Waves/{wname}.asset',
          head(WAVE_SCRIPT, wname) +
          f"  roundNumber: {rnd}\n  spawnList:\n"
          f"  - enemyData: {{fileID: 11400000, guid: {eguid}, type: 2}}\n"
          f"    count: {count}\n    spawnInterval: {interval}\n"
          f"  wispRewards:\n"
          f"  - wisp: {{fileID: 11400000, guid: {WISP_COMMON_GUID}, type: 2}}\n"
          f"    count: {wisps}\n",
          guid_for(wname))

    rows.append((rnd, count, hp, gold, boss))

print(f"{len(rows)}개 라운드 생성 (Assets/Data/Enemies, Assets/Data/Waves)\n")
print("  라운드  마릿수   체력    골드/마리")
for rnd, count, hp, gold, boss in rows:
    print(f"   {rnd:>3}{'(보스)' if boss else '     '} {count:>4} {hp:>8} {gold:>8}")
