#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""h04F(스코퍼 가반) 더미 복구 — 뿌리 ⑱ 형제.

fill_dummy_channel_damage.py(d6499ef)가 출력 파일명을 입력 키(uid)가 아니라
대상(로스터 base)으로 지었다 — h04F와 h07M이 둘 다 불멸_신지우를 타깃해서
같은 파일(SkillData_더미채널_불멸_신지우.asset)에 h07M이 나중에 써서 h04F의
750,000짜리 Trig_Gaban_Attack 더미(e055)가 조용히 사라졌다(입력 41 uid ≠
출력 40 파일로 발견).

재발 방지: 이 스크립트는 파일명에 uid(h04F)를 박아 넣는다 — 로스터
불멸_신지우가 이미 다른 uid(h07M)의 더미 파일(SkillData_더미채널_불멸_
신지우_1.asset)을 갖고 있어도 절대 안 겹친다.

게이트는 DUMMY_CHANNEL_GATES.csv(aa1c1bc) 35행 e055: GetRandomInt(1,22)==6
확정 → triggerChance=1/22(X가 몇이든 1/N, apply_dummy_channel_gates.py와
같은 규칙).
"""
import hashlib

SKILL_DIR = 'Assets/Data/UnitSkills'
ROSTER_DIR = 'Assets/Data/Units/Roster'
SKILL_SCRIPT_GUID = '9457f64cd84d34fd095791a069c0adc6'
BASE = '불멸_신지우'
UID = 'h04F'
NAME = f'SkillData_더미채널_{BASE}_{UID}'
VALUE = 750000.0
ATTACK_TYPE = 3  # 불멸_신지우 로스터 attackType 그대로(다른 더미채널 파일들과 동일 fallback)

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
    return hashlib.md5(('guilrd/dummygate/' + name).encode()).hexdigest()


def write_meta(path, guid):
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


def main():
    guid = guid_for(NAME)
    path = f'{SKILL_DIR}/{NAME}.asset'
    description = (
        f"더미 채널 게이트 확정(2026-09-06, DUMMY_CHANNEL_GATES.csv, aa1c1bc) — {BASE}. "
        "뿌리 ⑱ 형제 복구: d6499ef에서 h07M(카이도)의 같은-대상 파일 쓰기가 h04F(가반)의 "
        "이 더미를 조용히 덮어써서 없어졌다(파일명이 uid가 아니라 로스터 base였던 게 "
        "원인) — 이 파일은 파일명에 uid(h04F)를 박아 재발을 막는다. "
        "· Trig_Gaban_Attack → e055(#불멸 가반 공증 더미) → A05G: 750000"
    )
    body = (
        HEAD.replace('__SCRIPT__', SKILL_SCRIPT_GUID).replace('__NAME__', NAME)
        + f"  skillName: {NAME}\n"
        + f"  description: {description}\n"
        + "  triggerType: 0\n"
        + "  levels:\n"
        + "  - cooldown: 0\n"
        + f"    triggerChance: {round(1 / 22, 6)}\n"
        + "    range: 0\n"
        + "    hitCountThreshold: 0\n"
        + "    resetTo: 0\n"
        + "    gaugeKind: 0\n"
        + "    effects:\n"
        + "    - kind: 0\n"
        + "      basis: 0\n"
        + "      target: 3\n"
        + "      damageType: 1\n"
        + f"      attackType: {ATTACK_TYPE}\n"
        + f"      multiplier: {VALUE}\n"
        + "      bonus: 0.0\n"
        + "      chance: 1\n"
        + "      hitCount: 1\n"
        + "      duration: 0\n"
        + "      casterBuffCountFactor: 0\n"
    )
    open(path, 'w', encoding='utf-8').write(body)
    write_meta(path, guid)

    roster_path = f'{ROSTER_DIR}/{BASE}.asset'
    text = open(roster_path, encoding='utf-8').read()
    assert f'guid: {guid},' not in text, 'already applied'
    import re
    m = re.search(r'^  skills:\n((?:  - .*\n)*)', text, __import__('re').M)
    text = text[:m.end(1)] + f"  - {{fileID: 11400000, guid: {guid}, type: 2}}\n" + text[m.end(1):]
    open(roster_path, 'w', encoding='utf-8').write(text)
    print(f'복구 완료: {path} (guid {guid})')


if __name__ == '__main__':
    main()
