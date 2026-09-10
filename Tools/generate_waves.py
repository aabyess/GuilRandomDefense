#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""라운드 1~75의 EnemyData / WaveData 에셋을 만든다.

이름은 Tools/enemy_roster.py (사용자 제공). 대부분의 보스 라운드(10~60)는 잡몹이
나오지 않는다 — 65/70/75(신세계 보스)는 예외다(아래 참고). 수치는 1차 추정치이고,
조정하려면 아래 상수만 고치면 된다.

⚠️ 이 스크립트는 매번 Assets/Data/Enemies·Waves 폴더를 통째로 지우고 새로 만든다
(아래 for folder 루프). **armor·armorType·percentDamageTaken·pointValue 필드는
여기서 안 쓴다** — 이 필드들은 다른(위치 미확인) 패치 과정이 나중에 채워 넣은 것으로
보인다(de63a85·696c815·69738c9 커밋, 2026-09-11 확인 — 이 스크립트 자체는 그 커밋들
에서 수치 상수만 바뀌었지 필드 작성 코드는 안 늘었다). 즉 **이 스크립트를 지금 다시
돌리면 그 네 필드가 89종 전부에서 사라진다** — 65/70/75 라인몹 둘(아래 MOB_*_OVERRIDE)
은 이 스크립트가 직접 쓰므로 예외지만, 나머지는 재실행 전에 그 패치 과정을 먼저
찾아 이 스크립트에 합치거나 실행 순서를 맞춰야 한다(이번 작업 범위 밖, PM에게 보고).
그 재작업 자체는 여기서 하지 않는다 — 대신 그 사고가 실제로 나지 않게 아래에서
기본을 "손대지 않음"으로 막는다.

⚠️ 유니티 메뉴(맵 생성 등)는 이 스크립트를 안 부른다(2026-09-11, PM 확인 — Assets/Editor
전체에 이 스크립트를 실행하는 코드 0건). 위험은 **사람이 손으로 이 스크립트를 다시
돌릴 때**뿐이다 — 그래도 그 순간 위 필드 소실 사고가 조용히 나면 안 되므로, 기존
자산이 있으면 기본은 아무것도 안 쓰고 멈춘다. 정말 다시 만들고 싶으면
`--force`를 붙인다(2026-09-11, PM 지시).
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
BASE_HP, HP_GROWTH = 135.0, 1.1712273519960352  # 원작 몹 HP 그대로(R1 135 → R75 16,210,000).
                                                 # growth = (16210000/135)**(1/74). 2026-09-04.
# 원작 보스 HP는 라인몹처럼 공식으로 안 나온다 — 원작자가 라운드마다 손으로 적어넣은 값이고,
# 후반으로 갈수록 오히려 라인몹보다 얇아진다(R75가 직전 라인몹의 0.84배). 상수 배수로는
# 이 무너지는 곡선을 못 따라가서(2026-09-04, `Docs/reference/UNIT_STATS_RESEARCH.md` §⑦)
# 9개 값을 그대로 하드코딩한다. `Trig_Enemy_Boss_create`가 `Round_UnitType[udg_Level+50]`을
# 읽는 구조라 R65/70/75는 배열 65/70/75번(라인몹)이 아니라 95/100/105번이 진짜 보스 값이다 —
# 처음 나온 R75=14,080,000은 그 혼동에서 나온 오독이었다.
BOSS_HP = {
    10: 14500, 20: 85000, 30: 750000, 40: 2900000, 50: 10600000,
    60: 21800000, 65: 12297000, 70: 12750000, 75: 13595000,
}
MOB_SPEED, BOSS_SPEED = 10.0, 7.0   # 두 배 (사장님 확정 2026-09-01) — 라운드가 너무 늘어져서
SPAWN_WINDOW = 24.0                             # 28초 라운드 중 스폰에 쓰는 시간

# ⚠️ 2026-09-11 추가(PM 지시, 원문 직접 대조) — 65/70/75(신세계 보스)는 원작에서 보스와
# 별개로 라인몹이 같이 나온다(o01S/o02G/o02H, Tools/w3x/w3u.py로 war3map_new.w3u 직접
# 파싱). 위 지수곡선(BASE_HP·HP_GROWTH)은 R1·R75 두 점만 앵커링한 근사라 중간이 크게
# 어긋난다 — 이 세 라운드는 그 근사를 안 쓰고 원작 실측으로 바로 넣는다(R65 근사값
# 3,337,113 vs 실측 8,690,000 = -61.6%, R70 근사 7,354,904 vs 실측 12,450,000 = -40.9%,
# R75는 우연히 곡선의 앵커점이라 근사=실측). armorType은 원작 udty 그대로
# (o01S·o02G=normal, o02H=hero) — ArmorType enum 값(Assets/Scripts/Data/EnemyData.cs):
# Unassigned=0·Normal=1·Large=2·Fort=3·Hero=4.
MOB_HP_OVERRIDE = {65: 8690000.0, 70: 12450000.0, 75: 16210000.0}
MOB_ARMOR_OVERRIDE = {65: 73.0, 70: 70.0, 75: 81.0}
MOB_ARMOR_TYPE_OVERRIDE = {65: 1, 70: 1, 75: 4}

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

# ⚠️ 2026-09-11 추가(PM 지시) — 기존 자산이 있으면 기본은 손대지 않고 멈춘다. 이 스크립트가
# armor·armorType·percentDamageTaken·pointValue를 전혀 모르는 채로 89종을 전부 지우고 다시
# 쓰면 그 네 필드가 조용히 사라진다(위 docstring 참고) — "그럴 리 없겠지"가 아니라 실제로
# 벌어지는 사고라 기본값 자체를 안전한 쪽으로 둔다. `--force`를 주면 예전처럼 그대로 돈다
# (배포 실태에 맞추는 전면 재작업은 이 플래그로 하는 게 아니다 — 그건 별도로 배정될 작업).
FORCE = '--force' in sys.argv
_existing = [f for folder in ('Assets/Data/Enemies', 'Assets/Data/Waves')
             if os.path.isdir(folder) for f in os.listdir(folder) if f.endswith('.asset')]
if _existing and not FORCE:
    print(f"기존 자산 {len(_existing)}개가 있어 아무것도 안 쓰고 멈췄다(예: {_existing[0]}).")
    print("이 스크립트가 모르는 필드: armor·armorType·percentDamageTaken·pointValue —")
    print("덮어쓰면 89종에서 사라진다. 정말 다시 만들려면 --force를 붙여라.")
    sys.exit(1)

for folder in ('Assets/Data/Enemies', 'Assets/Data/Waves'):
    os.makedirs(folder, exist_ok=True)
    for f in os.listdir(folder):
        os.remove(os.path.join(folder, f))

rows = []
for rnd in range(1, TOTAL_ROUNDS + 1):
    boss = rnd in BOSSES
    name = BOSSES[rnd][0] if boss else ENEMIES[rnd]
    stage = BOSSES[rnd][1] if boss else ''

    hp = float(BOSS_HP[rnd]) if boss else round(BASE_HP * HP_GROWTH ** (rnd - 1), 1)
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

    spawn_entries = [
        f"  - enemyData: {{fileID: 11400000, guid: {eguid}, type: 2}}\n"
        f"    count: {count}\n    spawnInterval: {interval}\n"
    ]

    # ⚠️ 2026-09-11 추가 — 65/70/75만 해당(ENEMIES에 이 셋만 "라인몹"이 채워져 있다,
    # enemy_roster.py 참고). 위 보스 EnemyData와 별개로 두 번째 EnemyData(라인몹)를
    # 만들어 spawnList 맨 앞에 끼운다 — 순서는 무관하지만 사람이 읽을 때 라인몹이
    # 먼저 보이는 쪽이 자연스럽다.
    if boss and rnd in ENEMIES:
        # ⚠️ count/interval을 위 BASE_COUNT 점증 공식으로 계산하지 않는다 — 배포된 실제
        # 자산을 R01부터 R74까지 전수 대조해보니(2026-09-11) 이 공식은 이미 죽어 있다.
        # 지금 살아있는 라운드는 전부(점증 곡선과 무관하게) count=35·interval=0.65로
        # 고정돼 있다(다른 위치 미확인 패치가 바꿔놓은 것으로 보인다 — 위 파일 docstring의
        # "네 필드가 사라진다" 경고와 같은 종류의 드리프트). 공식값(예: R11=18마리·1.33초)
        # 대신 이웃 라운드와 실제로 같은 값을 그대로 맞춘다.
        mob_hp = MOB_HP_OVERRIDE[rnd]
        mob_count = MAX_COUNT
        mob_interval = 0.65
        mob_name = ENEMIES[rnd]
        mob_safe = mob_name.replace(' ', '_').replace('(', '').replace(')', '')
        mob_ename = f"Enemy_R{rnd:02d}_{mob_safe}"
        mob_guid = guid_for(mob_ename)
        write(f'Assets/Data/Enemies/{mob_ename}.asset',
              head(ENEMY_SCRIPT, mob_ename) +
              f"  enemyName: {mob_name}\n  hp: {mob_hp}\n  moveSpeed: {MOB_SPEED}\n"
              f"  goldReward: {5 + rnd}\n  resourceRewards: []\n  rewardsAllPlayers: 0\n"
              f"  isBoss: 0\n  armorType: {MOB_ARMOR_TYPE_OVERRIDE[rnd]}\n"
              f"  armor: {MOB_ARMOR_OVERRIDE[rnd]}\n"
              f"  prefab: {{fileID: {MOB_PREFAB_FILEID}, guid: {MOB_PREFAB_GUID}, type: 3}}\n"
              f"  percentDamageTaken: 0.9\n  pointValue: 100.0\n",
              mob_guid)
        spawn_entries.insert(0,
            f"  - enemyData: {{fileID: 11400000, guid: {mob_guid}, type: 2}}\n"
            f"    count: {mob_count}\n    spawnInterval: {mob_interval}\n")

    rewards = "".join(
        f"  - wisp: {{fileID: 11400000, guid: {WISPS[grade]}, type: 2}}\n    count: {n}\n"
        for grade, n in wisp_rewards(rnd, boss))

    wname = f'Wave_Round{rnd:02d}'
    write(f'Assets/Data/Waves/{wname}.asset',
          head(WAVE_SCRIPT, wname) +
          f"  roundNumber: {rnd}\n  spawnList:\n" + "".join(spawn_entries) +
          f"  wispRewards:\n{rewards}",
          guid_for(wname))

    rows.append((rnd, name, stage, count, hp, gold, boss))

print(f"{len(rows)}개 라운드 생성\n")
print("  라운드  이름                마릿수    체력      골드")
for rnd, name, stage, count, hp, gold, boss in rows:
    if boss or rnd % 10 == 1 or rnd in (5, 35, 55):
        tag = f"  ★보스 — {stage}" if boss else ""
        print(f"   {rnd:>3}  {name:<16} {count:>4} {hp:>10} {gold:>6}{tag}")
