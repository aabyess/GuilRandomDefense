#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""특별함_임채준을 원작 모리아(h00B) 자리로 옮긴다 (2026-10-02, 사장님 결정 → PM 지시). 버기(h015) 자리는 비운다.

근거(war3map_new.w3u / w3a 직접 디코드):
  - h00B: ua1b 400 · ua1c 1.0(공속 1.0) · ua1r 600 · ua1t normal(→ AttackType.Normal) · uabi A0JE,A07Y,A0BA,A028,A016,Avul,A113.
    전투 능력은 A113(ANba 블랙애로 「그림자그림자 열매」)뿐 — 나머지는 모으기·창고·판매·조합 버튼.
  - 특별함 33행 수열의 (400 / 1.0 / 600) 행은 이미 사거리 112.12로 옮겨져 있다(특별함_박기찬·노태현) → 같은 값을 쓴다.
  - 원래 임채준의 525/1.3158/134.39는 원작 h01A(크로커다일 525/0.76/600) 행의 순위 배정값이었다(버기 값이 아니었다) — 이 행은 holder를 잃는다.
  - 버기 스킬(A05E 마기탄) 연결을 뺀다. 그 SkillData 에셋 파일은 남긴다(참조만 끊음).
  - A113: 임채준이 평타로 죽인 적은 좀비(안흔함_좀비)로 부활 — 코드는 UnitAttacker.TryRaiseOnKill.
재실행 안전.
"""
import os, re
ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
os.chdir(ROOT)
P = 'Assets/Data/Units/Roster/특별함_임채준.asset'
ZOMBIE = 'Assets/Data/Units/Roster/안흔함_좀비.asset'
MAP = 'Docs/reference/MASTER_UID_ROSTER_MAP.csv'


def guid(path):
    return re.search(r'^guid: (\w+)', open(path + '.meta', encoding='utf-8').read(), re.M).group(1)


def setf(t, key, val):
    t, n = re.subn(r'^  %s: .*$' % key, lambda m: '  %s: %s' % (key, val), t, count=1, flags=re.M)
    assert n == 1, key
    return t


t = open(P, encoding='utf-8').read()
for k, v in [('attackPower', 400), ('attackSpeed', 1), ('attackRange', 112.12), ('attackType', 1), ('skill', '{fileID: 0}')]:
    t = setf(t, k, v)
add = {
    'raiseOnKillUnit': '{fileID: 11400000, guid: %s, type: 2}' % guid(ZOMBIE),
    'raiseOnKillChancePercent': 10,
    'raiseOnKillLifetimeSeconds': 0,
}
for k, v in add.items():
    if re.search(r'^  %s: ' % k, t, re.M):
        t = setf(t, k, v)
    else:
        t = t.rstrip('\n') + '\n  %s: %s\n' % (k, v)
open(P, 'w', encoding='utf-8').write(t)

m = open(MAP, encoding='utf-8').read()
if 'h015,특별함_임채준' in m:
    m = m.replace('h015,특별함_임채준,1채널', 'h00B,특별함_임채준,1채널')
    open(MAP, 'w', encoding='utf-8').write(m)
print('완료')
