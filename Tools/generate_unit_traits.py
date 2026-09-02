#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""로스터 전 유닛(235종)에 빈 UnitTraitData 에셋을 하나씩 만든다.

PM 승인된 5단계 설계(Docs/design/TRAIT_UPGRADE.md) 중 5번 — "235개 스캐폴딩, 전부 효과 없음".
전부 costTraitPoints=4(원작 사례 대부분 4개 [원작]), effects=[](효과 없음), specialEffectId=""로
시작한다 — 나중에 사장님이 실제 수치를 주시는 유닛부터 이 에셋 하나씩만 갈아끼우면 된다.

재실행해도 guid는 안 바뀐다(로스터 파일명 기준으로 고정). 로스터에 유닛이 추가/제거되면
다시 돌리면 된다 — 이미 있는 파일은 덮어쓰되 guid는 그대로 유지된다(파일명이 같으면).
"""
import os, re, glob, hashlib

TRAIT_SCRIPT = '8e410fbbb611c449cbce6ef8552b5f07'  # UnitTraitData.cs

ROSTER_DIR = 'Assets/Data/Units/Roster'
OUT_DIR = 'Assets/Data/Traits'

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
    return hashlib.md5(('guilrd/traits/' + name).encode()).hexdigest()


def write(path, body, guid):
    open(path, 'w', encoding='utf-8').write(body)
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


os.makedirs(OUT_DIR, exist_ok=True)

roster_paths = sorted(glob.glob(os.path.join(ROSTER_DIR, '*.asset')))
made = 0

for roster_path in roster_paths:
    base = os.path.splitext(os.path.basename(roster_path))[0]  # 예: 다른세계_고죠_사토루

    meta_text = open(roster_path + '.meta', encoding='utf-8').read()
    roster_guid = re.search(r'guid: (\w+)', meta_text).group(1)

    ename = f'Trait_{base}'
    eguid = guid_for(ename)

    body = (HEAD.replace('__SCRIPT__', TRAIT_SCRIPT).replace('__NAME__', ename) +
            f'  targetUnit: {{fileID: 11400000, guid: {roster_guid}, type: 2}}\n'
            f'  traitName: {base} 특성강화\n'
            f'  description: \n'
            f'  costTraitPoints: 4\n'
            f'  effects: []\n'
            f'  specialEffectId: \n')

    write(f'{OUT_DIR}/{ename}.asset', body, eguid)
    made += 1

print(f"{made}개 특성강화 스캐폴딩 에셋 생성 완료 ({OUT_DIR}/) — 전부 효과 없음, costTraitPoints=4")
