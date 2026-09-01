#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""'백수생활' 구간 전용 선택 위습 1종의 WispData 에셋을 만든다.

스토리 8 클리어 직후 백수생활 구간에서 딱 1개만 나오고, 플레이어는 이걸로
세 특수 칸(돈+목재 / 박은석 초월위습 / 레일리+배) 중 하나에만 넣을 수 있다.
한 번만 고르는 강제는 위습을 1개만 주는 것 + Wisp.IsConsumed로 이미 보장되고,
InterludeGate.choiceTrackedWispData가 이 에셋을 가리키게 하면 "이미 골랐다"
표시도 로컬 플레이어 기준으로 동작한다(InterludeGate.cs 참고).

targetGrade는 실제 등급 의미가 아니라 WispCell이 이 위습을 스폰할 때 쓰는
라우팅 키다. 기존 픽업 위습들이 Common/Uncommon/Special/Rare/Legendary/
RandomUnit을 이미 쓰고 있어서 안 겹치는 Transcendent를 썼다.

재실행해도 guid는 안 바뀐다.
"""
import os, hashlib

WISP_SCRIPT = '358064a02780a445eb0fe8cf185289d0'  # WispData.cs

# 기존 위습들이 전부 재사용하는 공유 위습 프리팹.
PREFAB_GUID = 'a9ce2567fa0e4e1ca4f49506a74c90ae'
PREFAB_FILEID = '2000000000000000001'

TRANSCENDENT = 7  # UnitGrade enum 순서 그대로 (UnitData.cs) — 라우팅 키로만 쓴다.

NAME = 'Wisp_백수생활선택'
WISP_NAME = '백수생활 선택 위습'

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
    return hashlib.md5(('guilrd/wisps/' + name).encode()).hexdigest()


def write(path, body, guid):
    open(path, 'w', encoding='utf-8').write(body)
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


os.makedirs('Assets/Data/Wisps', exist_ok=True)

guid = guid_for(NAME)
body = (HEAD.replace("__SCRIPT__", WISP_SCRIPT).replace("__NAME__", NAME) +
        f"  wispName: {WISP_NAME}\n"
        f"  targetGrade: {TRANSCENDENT}\n"
        f"  isPlayerChoice: 1\n"
        f"  prefab: {{fileID: {PREFAB_FILEID}, guid: {PREFAB_GUID}, type: 3}}\n")

path = f'Assets/Data/Wisps/{NAME}.asset'
write(path, body, guid)

print(f"생성 완료: {path}")
print(f"  guid: {guid}")
print(f"  targetGrade: Transcendent({TRANSCENDENT}) — MapGenerator에서 WispCell 라우팅 키로 사용")
