"""영원한 채우기 — 본체 결정(사장님 09-30 「합친 채로 둔다」) 뒤 풀린 것(구현담당1, PM 지시).

미호크 A0G3(h058 → 영원_최상호): ACbh, Hbh1 100(%)·adur/ahdu 1.2·Hbh3 0·atar air,enemies,ground, 트리거 참조 0 = uabi 상시 강타.
  매 평타 100%로 대상 스턴 1.2초(피해 없음). 조건·절대쿨 없음 — 미호크 ua1c 0.93 < 1.2라 원작도 잡은 대상은 계속 스턴이다
  (툴팁 A0HP 「공격시 100% 확률로 단일 적 1.2초 스턴」). 영원_김영원의 효과 0개짜리 「A0G3」 껍데기는 그대로.
버기 A0M8(h05A → 영원_김정래): ACbh, Hbh1 7.77·Hbh2 7.77·Hbh3 3333333·adur 7.77, 트리거 참조 0.
  있던 묶음 에셋(원작능력_영원_김정래, 이름표 「A0OU」)은 09-05 생성 때부터 확률 0 — 생성기가 묶음 첫 능력(A0OU, Hbh1 빈칸)의
  발동을 0으로 읽은 것이지 일부러 잠근 흔적은 없다(git log -S). 쓰레기 효과(20·1)가 섞여 있어 그 에셋은 그대로 두고 새 에셋으로.
버기 오라: A0RE(Aasl Slo1 −0.25, 800) · A0M7(AHad −30, 800, B038) · A04E(ACac 고정 +10000 레벨 1, 825, 자기 포함, B032) ·
  A03L(AOae 공속 +0.60 레벨 1, 825, 자기 포함, B037). 레벨(3,333골드 버튼 Bugi_money, 최대 6)은 축 없음 — 레벨 1.
버기 하울(Bugi_Attack 1/22 → 더미 e0AJ A0M5 ANht): Roa1 −0.33을 아군에 = 공격력 +33%, 4.5초, 900, B036.
  원작 더미는 Player(4)라 동맹 전원이 받지만 우리는 같은 주인만.
한 몸에 합쳐진 두 캐릭터의 버프 ID는 서로 다르다(우타 B07W ↔ 버기 B037·B032·B036·B038, 미호크 B03L ↔ 카벤딧슈 B01K) → 서로 안 막는다.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402

SELF, ALLIES, ENEMIES, SINGLE = 0, 1, 2, 3
ARMOR, FLAT, SPEED, SLOW, PERCENT = 4, 11, 12, 13, 14
NOTE = ' 2026-09-30 구현담당1(apply_eternal_unlocked).'
TAG = '[09-30 본체 결정]'


def note(name, text):
    a = sat.load(name)
    m = re.search(r'^  description: (.*)$', a.head, re.M)
    old = m.group(1)
    if TAG in old:
        return None
    add = ' %s %s' % (TAG, text)
    new = old[:-1] + add.replace("'", "''") + "'" if old.startswith("'") and old.endswith("'") else sat.yaml_scalar(old + add)
    a.head = a.head.replace(m.group(0), '  description: ' + new)
    sat.save(a)
    return a.path


def main():
    changed = []

    def new(roster, stem, name, desc, trigger, level):
        path = os.path.join(sat.SKILL_DIR, stem + '.asset')
        if not os.path.exists(path):
            sat.new_asset(stem, name, desc + NOTE, trigger, [level])
            changed.extend([path, path + '.meta'])
        if sat.add_skill_to_unit(roster, sat.guid_of(path)):
            changed.append(os.path.join(sat.ROSTER_DIR, roster + '.asset'))

    new('영원_최상호', 'SkillData_원작능력_영원_최상호_A0G3', 'A0G3 !최강의 참격 — 매 타 스턴 1.2초',
        '원작 미호크 h058 uabi A0G3(ACbh Hbh1 100·adur 1.2·Hbh3 0, atar air,enemies,ground, 트리거 참조 0): 평타마다 100%로 대상 스턴 1.2초, 피해 없음. 조건·절대쿨 없음.',
        0, sat.level_block(triggerChance=1.0, effects=[sat.effect(kind=1, target=SINGLE, duration=1.2)]))
    p = note('SkillData_원작능력_영원_김영원', '이 에셋은 효과 0개 껍데기다 — A0G3(미호크)의 값은 영원_최상호의 SkillData_원작능력_영원_최상호_A0G3에 있다.')
    if p:
        changed.append(p)

    new('영원_김정래', 'SkillData_원작능력_영원_김정래_A0M8', 'A0M8 !해적파견조직총수 — 7.77% · 7.77배 + 3333333 · 스턴 7.77초',
        '원작 버기 h05A uabi A0M8(ACbh Hbh1 7.77·Hbh2 7.77·Hbh3 3333333·adur 7.77, atar air,enemies,neutral,ground, 트리거 참조 0).',
        0, sat.level_block(triggerChance=0.0777, effects=[
            sat.effect(kind=0, basis=3, target=SINGLE, damageType=1, attackType=1, multiplier=7.77, bonus=3333333.0),
            sat.effect(kind=1, target=SINGLE, duration=7.77)]))
    p = note('SkillData_원작능력_영원_김정래', '이 묶음은 생성 때부터 확률 0(묶음 첫 능력 A0OU의 Hbh1 빈칸을 0으로 읽음)이라 안 돈다. 이름표 「A0OU 웨스턴 액션」은 비비 능력이고 '
             '안에 든 7.77·3333333은 버기 A0M8 — 그 값은 SkillData_원작능력_영원_김정래_A0M8로 따로 살렸다. 이 에셋은 그대로 둔다.')
    if p:
        changed.append(p)

    auras = [
        ('A0RE', 'A0RE 버기 이동속도 감소 — 적 이속 −25% 오라', '원작 버기 h05A uabi A0RE(Aasl Slo1 −0.25, aare 800).', 800,
         [sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.75)]),
        ('A0M7', 'A0M7 전설을 살아가는 사나이 — 적 방어 −30 오라', '원작 버기 h05A uabi A0M7(AHad Had1 −30, aare 800, atar air,enemies,ground, abuf B038).', 800,
         [sat.effect(kind=ARMOR, target=ENEMIES, multiplier=-30.0, buffId='B038')]),
        ('A04E', 'A04E 언변 — 아군 공격력 +10000 오라', '원작 버기 h05A uabi A04E(ACac Cac1 10000 고정값(Ear4), aare 825, atar …self…friend, abuf B032). 레벨 1 기준(레벨 6 = 15000, Bugi_money 버튼 — 레벨 축 없음).', 825,
         [sat.effect(kind=FLAT, target=SELF, multiplier=10000.0, buffId='B032'), sat.effect(kind=FLAT, target=ALLIES, multiplier=10000.0, buffId='B032')]),
        ('A03L', 'A03L 허세 — 아군 공속 +60% 오라', '원작 버기 h05A uabi A03L(AOae Oae2 +0.60, aare 825, atar …self…friend, abuf B037). 레벨 1 기준(레벨 6 = 0.65). ⚠️ 이 능력의 툴팁은 다른 효과(1/22 하울)를 적고 있다 — 필드가 기준.', 825,
         [sat.effect(kind=SPEED, target=SELF, multiplier=0.6, buffId='B037'), sat.effect(kind=SPEED, target=ALLIES, multiplier=0.6, buffId='B037')]),
    ]
    for tail, name, desc, radius, effects in auras:
        new('영원_김정래', 'SkillData_원작오라_영원_김정래_%s' % tail, name, desc, 2, sat.level_block(range=float(radius), effects=effects))

    new('영원_김정래', 'SkillData_원작트리거_영원_김정래_Bugi_Attack_하울', 'Bugi_Attack 1/22 — 허세 하울(아군 공격력 +33% 4.5초)',
        '원작 버기 h05A Trig_Bugi_Attack GetRandomInt(1,22)==3 → 더미 e0AJ howlofterror(A0M5 ANht Roa1 −0.33, adur 4.5, aare 900, atar …self…friend, abuf B036) = 주변 아군 공격력 +33%. '
        '원작 더미는 Player(4)(동맹 전원), 우리는 같은 주인만.',
        0, sat.level_block(triggerChance=1.0 / 22.0, range=900.0, effects=[
            sat.effect(kind=PERCENT, target=SELF, multiplier=0.33, duration=4.5, buffId='B036'),
            sat.effect(kind=PERCENT, target=ALLIES, multiplier=0.33, duration=4.5, buffId='B036')]))

    print('바꾼 파일 %d' % len(set(changed)))
    for c in sorted(set(changed)):
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
