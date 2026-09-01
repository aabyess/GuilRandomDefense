#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""스토리 13개의 건물 EnemyData 에셋을 만들고, 각 Assets/Data/Stories/*.asset의
building 필드를 그 에셋으로 연결한다.

스토리는 라운드 몹과 달리 "누적 피해가 쌓이는 오래 버티는 표적"이다.
건물 최대 체력(hp)을 정하는 게 이 스크립트의 핵심 계산이다 — 근거와 가정은 아래에 그대로 남긴다.

--- 체력 곡선을 정한 방법 ---

1. 라운드 잡몹 체력(Tools/generate_waves.py와 동일 공식): mob_hp(r) = BASE_HP * HP_GROWTH^(r-1)
   라운드 보스 체력 = mob_hp(r) * BOSS_HP_MULTIPLIER(15) — 이 스크립트도 그대로 재사용한다.
   (스토리 보스도 "그 라운드에 상대할 만한 세기"가 되도록, 변신 시점의 mob_hp에 같은 15배를 곱한다.)

2. "그 시점 플레이어 화력"을 추정하는 기준값이 없어서, 이미 검증된 라운드 밸런스 데이터에서
   역산했다: 한 레인을 28초 라운드 안에 정리하려면 필요한 총 DPS를
   team_dps(r) = mob_hp(r) * count(r) / ROUND_SECONDS 로 잡는다.
   (count(r)도 generate_waves.py와 동일 공식: BASE_COUNT + (r-1)//COUNT_STEP, 상한 MAX_COUNT)
   → 이건 "한 레인을 방어하는 데 필요한 화력"이지 "스토리를 때리는 화력"이 아니다.
     레인 방어가 우선이므로, 그중 일부만 스토리에 새어나간다고 가정한다:
     STORY_DPS_SHARE(0.2) = 팀 전체가 스토리에 돌릴 수 있는 여유 화력의 비율(보수적으로 낮게 잡음
     — 방치해도 거저 깎이면 "미리 때려둘 이유"가 안 생긴다).

   참고용 교차검증: 유닛 공격력(흔함 5 ~ 초월 1247, generate_unit_stats.py)과 비교했을 때
   story_dps(r)를 그 라운드에 기대되는 유닛 등급의 공격력으로 나누면 "동시에 몇 기가 때리는 셈"이
   나오는데, 대체로 라운드가 올라갈수록 한 자릿수~수십 기 규모로 나와 비현실적이지 않다.

3. 스토리 13개가 정확히 몇 라운드에 상대되는지는 알 수 없다(스토리는 라운드와 안 엮여 있음).
   13개를 75라운드에 걸쳐 대략 고르게 겪는다고 가정하고, 각 스토리의 "변신 라운드"를
   i번째 스토리 → round(i/13 * 75)를 5의 배수로 스냅해서 잡았다(끝자리 0/5만 변신 가능하므로).
   이전 스토리의 변신 라운드부터 이번 변신 라운드까지의 라운드 수(window)가
   "이 건물이 맞고 있었던 기간"이 된다.

4. 최종 공식:
   pre_damage(i)  = story_dps(변신라운드) * window(라운드 수) * ROUND_SECONDS   ← 변신 전까지 누적 피해
   boss_hp(i)     = mob_hp(변신라운드) * BOSS_HP_MULTIPLIER                      ← 변신 후 남아있어야 할 체력
   building_hp(i) = pre_damage(i) + boss_hp(i)                                  ← EnemyData.hp에 넣을 값

   즉 "미리 안 때리면 grief" 구조: 건물 hp에서 변신 시점까지 누적 피해를 빼면 정확히
   설계한 보스 체력(라운드 보스와 동급 세기)이 남는다.

이 가정들(스토리-라운드 대응, STORY_DPS_SHARE)은 전부 추정치다. 실측 밸런스가 나오면
아래 상수만 고치고 재실행하면 된다 — guid는 이름 기반이라 재실행해도 안 바뀐다.
"""
import os, re, sys, glob, hashlib

ENEMY_SCRIPT = 'a91828e7d70cc420499707cd4f2912a5'
MOB_PREFAB_GUID = '85f952ec56b9f4258b079a5873623d2b'
MOB_PREFAB_FILEID = '3685248672397471059'

# --- 라운드 밸런스 (generate_waves.py와 동일 공식·값) ---
BASE_HP, HP_GROWTH = 10.0, 1.09
BASE_COUNT, COUNT_STEP, MAX_COUNT = 15, 3, 35
BOSS_HP_MULTIPLIER = 15.0
ROUND_SECONDS = 28.0
TOTAL_ROUNDS = 75
STORY_COUNT = 13

# --- 이 스크립트만의 가정 ---
STORY_DPS_SHARE = 0.2   # 레인 방어 화력 중 스토리로 새는 비율 (보수적으로 낮게)


def mob_hp(rnd):
    return BASE_HP * HP_GROWTH ** (rnd - 1)


def mob_count(rnd):
    return min(MAX_COUNT, BASE_COUNT + (rnd - 1) // COUNT_STEP)


def team_dps(rnd):
    return mob_hp(rnd) * mob_count(rnd) / ROUND_SECONDS


def transform_rounds(n):
    """i번째 스토리(1-based)의 가정 변신 라운드. 0/5 끝자리로 스냅하고, 겹치면 +5씩 민다."""
    rounds = []
    for i in range(1, n + 1):
        raw = i / n * TOTAL_ROUNDS
        snapped = max(5, round(raw / 5) * 5)
        rounds.append(snapped)
    for i in range(1, len(rounds)):
        if rounds[i] <= rounds[i - 1]:
            rounds[i] = rounds[i - 1] + 5
    return rounds


def guid_for(name):
    return hashlib.md5(('guilrd/' + name).encode()).hexdigest()


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


def write(path, body, guid):
    open(path, 'w', encoding='utf-8').write(body)
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


# --- 스토리 목록: Assets/Data/Stories/*.asset에서 storyName/order를 읽는다 ---
stories = []  # (order, storyName, path)
for path in glob.glob('Assets/Data/Stories/*.asset'):
    text = open(path, encoding='utf-8').read()
    name = re.search(r'^  storyName: (.*)$', text, re.M).group(1).strip()
    order = int(re.search(r'^  order: (\d+)', text, re.M).group(1))
    stories.append((order, name, path))
stories.sort(key=lambda s: s[0])

if len(stories) != STORY_COUNT:
    print(f"경고: 스토리 에셋 {len(stories)}개 발견, {STORY_COUNT}개를 기대했습니다.")

os.makedirs('Assets/Data/Enemies', exist_ok=True)

rounds = transform_rounds(len(stories))

rows = []
prev_round = 0
for (order, name, path), rnd in zip(stories, rounds):
    window = rnd - prev_round
    prev_round = rnd

    boss_hp = mob_hp(rnd) * BOSS_HP_MULTIPLIER
    pre_damage = team_dps(rnd) * STORY_DPS_SHARE * window * ROUND_SECONDS
    building_hp = round(pre_damage + boss_hp, 1)

    safe = name.replace(' ', '_').replace('(', '').replace(')', '')
    ename = f"Enemy_Story{order:02d}_{safe}"
    eguid = guid_for(ename)

    write(f'Assets/Data/Enemies/{ename}.asset',
          head(ENEMY_SCRIPT, ename) +
          f"  enemyName: {name}\n  hp: {building_hp}\n  moveSpeed: 0\n  goldReward: 0\n"
          f"  resourceRewards: []\n  rewardsAllPlayers: 0\n  isBoss: 1\n"
          f"  prefab: {{fileID: {MOB_PREFAB_FILEID}, guid: {MOB_PREFAB_GUID}, type: 3}}\n",
          eguid)

    # building 필드만 갈아끼운다 — boss/goldReward/resourceRewards/wispRewards는 손대지 않는다.
    story_text = open(path, encoding='utf-8').read()
    new_story_text = re.sub(
        r'^  building: \{.*\}$',
        f"  building: {{fileID: 11400000, guid: {eguid}, type: 2}}",
        story_text, count=1, flags=re.M)
    open(path, 'w', encoding='utf-8').write(new_story_text)

    rows.append((order, name, rnd, window, boss_hp, pre_damage, building_hp))

print(f"{len(rows)}개 스토리 건물 생성 및 연결 완료\n")
print("  순서  이름                변신라운드  누적기간  보스체력      누적피해      건물최대체력")
for order, name, rnd, window, boss_hp, pre_damage, building_hp in rows:
    print(f"   {order:>3}  {name:<16} {rnd:>6}  {window:>6}라운드 {boss_hp:>10.1f} {pre_damage:>12.1f} {building_hp:>14.1f}")
