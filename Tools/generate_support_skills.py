#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""도움소 스킬 9종의 SupportSkillData 에셋을 만든다.

수치 근거는 Docs/reference/SUPPORT_SHOP*.md 조사 + PM 승인 내용을 그대로 옮긴 것뿐이라
여기 다시 적지 않는다. 재실행해도 guid는 이름 기반 md5라 안 바뀐다.
"""
import os, hashlib

SKILL_SCRIPT = '3dd0d10191ac4a78b909eea9d79fb207'

# SupportSkillTargetKind / SupportSkillEffect enum 순서 그대로.
GROUND, UNIT = 0, 1
DAMAGE, ROOT, BUFF, MANA_RESTORE, UNIT_DISMANTLE = 0, 1, 2, 3, 4

# UnitGrade enum 순서(그대로 참조 — 앞에 끼우면 다른 에셋들이 깨진다).
GRADE = {
    'Common': 0, 'Uncommon': 1, 'Special': 2, 'Rare': 3,
}

SKILLS = [
    dict(name='폭우', desc='좁은 범위에 비를 퍼부어 피해를 준다.', target=GROUND, effect=DAMAGE,
         mana=15, gold=0, cooldown=8, radius=10, mapWide=0,
         dmgBase=15, dmgPerRound=3, duration=0,
         refundPerHit=0, refundCap=0, restoreAmount=0, buffMult=1),
    dict(name='흡수', desc='적을 태워 피해를 주고, 맞힌 수만큼 마나를 되돌려받는다.', target=GROUND, effect=DAMAGE,
         mana=30, gold=0, cooldown=15, radius=10, mapWide=0,
         dmgBase=20, dmgPerRound=4, duration=0,
         refundPerHit=5, refundCap=30, restoreAmount=0, buffMult=1),
    dict(name='해루석', desc='적을 묶어두고 그동안 지속 마법피해를 준다.', target=GROUND, effect=ROOT,
         mana=120, gold=0, cooldown=50, radius=12, mapWide=0,
         dmgBase=35, dmgPerRound=6, duration=4,
         refundPerHit=0, refundCap=0, restoreAmount=0, buffMult=1),
    dict(name='지진', desc='넓은 땅을 흔들어 광역 피해를 준다.', target=GROUND, effect=DAMAGE,
         mana=150, gold=0, cooldown=40, radius=15, mapWide=0,
         dmgBase=60, dmgPerRound=10, duration=0,
         refundPerHit=0, refundCap=0, restoreAmount=0, buffMult=1),
    dict(name='해적선충돌', desc='해적선을 떨어뜨려 큰 피해를 주고 잠시 묶어둔다.', target=GROUND, effect=DAMAGE,
         mana=75, gold=0, cooldown=45, radius=14, mapWide=0,
         dmgBase=90, dmgPerRound=16, duration=2,
         refundPerHit=0, refundCap=0, restoreAmount=0, buffMult=1),
    dict(name='출항이다', desc='전군의 사기를 올려 한동안 공격 속도를 높인다.', target=GROUND, effect=BUFF,
         mana=100, gold=0, cooldown=60, radius=15, mapWide=1,
         dmgBase=0, dmgPerRound=0, duration=10,
         refundPerHit=0, refundCap=0, restoreAmount=0, buffMult=1.3),
    dict(name='버스터콜', desc='압도적인 화력으로 넓은 지역을 초토화한다.', target=GROUND, effect=DAMAGE,
         mana=800, gold=0, cooldown=90, radius=25, mapWide=0,
         dmgBase=150, dmgPerRound=30, duration=0,
         refundPerHit=0, refundCap=0, restoreAmount=0, buffMult=1),
    dict(name='마나포션', desc='마나를 즉시 채운다.', target=GROUND, effect=MANA_RESTORE,
         mana=0, gold=3500, cooldown=120, radius=0, mapWide=0,
         dmgBase=0, dmgPerRound=0, duration=0,
         refundPerHit=0, refundCap=0, restoreAmount=150, buffMult=1),
    dict(name='연금술', desc='낮은 등급 유닛을 분해해 마나로 되돌린다 (등가교환 — 자격 미달이면 분해 없이 마나만 돌려받는다).',
         target=UNIT, effect=UNIT_DISMANTLE,
         mana=20, gold=0, cooldown=20, radius=0, mapWide=0,
         dmgBase=0, dmgPerRound=0, duration=0,
         refundPerHit=0, refundCap=0, restoreAmount=0, buffMult=1,
         maxDismantleGrade=GRADE['Rare'],
         dismantleRefunds=[(GRADE['Common'], 10), (GRADE['Uncommon'], 20),
                           (GRADE['Special'], 35), (GRADE['Rare'], 55)]),
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
    return hashlib.md5(('guilrd/' + name).encode()).hexdigest()


def write(path, body, guid):
    open(path, 'w', encoding='utf-8').write(body)
    open(path + '.meta', 'w', encoding='utf-8').write(
        "fileFormatVersion: 2\nguid: " + guid + "\nNativeFormatImporter:\n"
        "  externalObjects: {}\n  mainObjectFileID: 11400000\n"
        "  userData: \n  assetBundleName: \n  assetBundleVariant: \n")


os.makedirs('Assets/Data/SupportSkills', exist_ok=True)

rows = []
for s in SKILLS:
    ename = f"SupportSkill_{s['name']}"
    eguid = guid_for(ename)

    refunds = ""
    if s.get('dismantleRefunds'):
        refunds = "  dismantleRefunds:\n" + "".join(
            f"  - grade: {grade}\n    manaRefund: {refund}\n" for grade, refund in s['dismantleRefunds'])
    else:
        refunds = "  dismantleRefunds: []\n"

    body = (HEAD.replace("__SCRIPT__", SKILL_SCRIPT).replace("__NAME__", ename) +
            f"  skillName: {s['name']}\n"
            f"  description: {s['desc']}\n"
            f"  targetKind: {s['target']}\n"
            f"  effect: {s['effect']}\n"
            f"  manaCost: {s['mana']}\n"
            f"  goldCost: {s['gold']}\n"
            f"  cooldownSeconds: {s['cooldown']}\n"
            f"  radius: {s['radius']}\n"
            f"  mapWide: {s['mapWide']}\n"
            f"  damageBase: {s['dmgBase']}\n"
            f"  damagePerRound: {s['dmgPerRound']}\n"
            f"  duration: {s['duration']}\n"
            f"  manaRefundPerHit: {s['refundPerHit']}\n"
            f"  manaRefundCap: {s['refundCap']}\n"
            f"  manaRestoreAmount: {s['restoreAmount']}\n"
            f"  buffAttackSpeedMultiplier: {s['buffMult']}\n"
            f"  maxDismantleGrade: {s.get('maxDismantleGrade', GRADE['Rare'])}\n"
            + refunds)

    write(f'Assets/Data/SupportSkills/{ename}.asset', body, eguid)
    rows.append((s['name'], s['mana'], s['gold'], s['cooldown'], s['radius'], s['dmgBase'], s['dmgPerRound']))

print(f"{len(rows)}개 스킬 에셋 생성 완료\n")
print("  이름          마나  골드   쿨다운  반경   데미지base  데미지/R")
for name, mana, gold, cd, radius, dbase, dper in rows:
    print(f"  {name:<12} {mana:>5} {gold:>6} {cd:>6}s {radius:>5}  {dbase:>9}  {dper:>6}")
