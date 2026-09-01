#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""도박소 돈 도박 2종(10엔 도박/500엔 도박)의 GamblingOptionData 에셋을 만든다.

사장님 확정 사양(2026-09-01) 그대로 옮긴 것뿐이라 여기 다시 적지 않는다.
초급도박은 없다 — 사장님이 두 개만 주셨다. 재실행해도 guid는 안 바뀐다.

이름은 원래 "중급도박(돈)"/"고급도박(돈)"이었다가 유닛 도박과 이름이 겹쳐서
사장님이 "10엔도박"/"500엔도박"으로 다시 정했다(2026-09-01). 파일은 git mv로
옮기고 guid는 그대로 보존했다 — 'id'가 그 원래 식별자다. 이름이 또 바뀌어도
'id'는 고정해서 guid가 안 흔들리게 한다. 'name'만 표시용으로 바뀐다.
"""
import os, hashlib

OPTION_SCRIPT = 'e38dcfb2166e0425695e116cb8b725bc'

MONEY = 0  # GamblingCategory enum 순서 그대로.
WOOD = 0   # ResourceType enum 순서 그대로 — Money 카테고리는 이 필드를 안 쓰지만 기본값을 채워둔다.

OPTIONS = [
    dict(id='중급도박(돈)', name='10엔 도박',
         desc='10엔을 걸고 0~100엔을 받는다. 평생 10번만 돌릴 수 있다.',
         cost=10, goldMin=0, goldMax=100,
         maxUses=10, requiresUnlock=0, unlockHint='', unlockRound=0),
    dict(id='고급도박(돈)', name='500엔 도박',
         desc='500엔을 걸고 0~4500엔을 받는다. 10라운드 보스를 처치해야 열린다.',
         cost=500, goldMin=0, goldMax=4500,
         maxUses=0, requiresUnlock=1, unlockHint='10라운드 보스 처치 후 해금', unlockRound=10),
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
    return hashlib.md5(('guilrd/gambling/money/' + name).encode()).hexdigest()


def write(path, body, guid):
    open(path, 'w', encoding='utf-8').write(body)
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


os.makedirs('Assets/Data/Gambling', exist_ok=True)

rows = []
for o in OPTIONS:
    ename = f"Gambling_{o['name']}"              # 지금 표시 이름 = 파일명
    eguid = guid_for(f"Gambling_{o['id']}")       # guid는 원래 id로 고정 — 이름이 또 바뀌어도 안 변한다

    body = (HEAD.replace("__SCRIPT__", OPTION_SCRIPT).replace("__NAME__", ename) +
            f"  optionName: {o['name']}\n"
            f"  description: {o['desc']}\n"
            f"  category: {MONEY}\n"
            f"  costResourceType: {WOOD}\n"
            f"  cost: {o['cost']}\n"
            f"  successChancePercent: 100\n"
            f"  primaryResultGrade: 0\n"
            f"  useSecondaryGrade: 0\n"
            f"  secondaryResultGrade: 0\n"
            f"  successGoldMin: {o['goldMin']}\n"
            f"  successGoldMax: {o['goldMax']}\n"
            f"  grantFailureReward: 0\n"
            f"  failureLuckyTokens: 0\n"
            f"  failureWood: 0\n"
            f"  maxUses: {o['maxUses']}\n"
            f"  requiresUnlock: {o['requiresUnlock']}\n"
            f"  unlockHint: {o['unlockHint']}\n"
            f"  unlockRound: {o['unlockRound']}\n")

    write(f'Assets/Data/Gambling/{ename}.asset', body, eguid)
    rows.append((o['name'], o['cost'], o['goldMin'], o['goldMax'], o['maxUses'], o['requiresUnlock']))

print(f"{len(rows)}개 돈 도박 옵션 에셋 생성 완료 (Assets/Data/Gambling/)\n")
print("  이름            비용   결과범위      최대사용  해금필요")
for name, cost, gmin, gmax, maxUses, requiresUnlock in rows:
    print(f"  {name:<14} {cost:>4}엔  {gmin}~{gmax}엔{'':<4}  {maxUses:>8}  {requiresUnlock:>8}")
