"""불멸의 등급 빠진 원작 상시 오라 채우기(2026-09-30 구현담당1 — Docs/research/IMMORTAL_FILL_LIST.md §1, Blender §V 확인 + w3a 직접 재디코드).

원작 uabi 상시 오라 14 + 카이도 이감 오라(A14G)를 Aura 발동방식 SkillData로 만들어 대응 로스터에 붙인다. 모양은 apply_eternal_auras.py와 같다.
다시 돌려도 같은 결과(이미 있으면 건너뜀). 능력마다 주인 uabi·필드·j 참조 수(A0ES만 2 — 레벨 분기용, 나머지 0)를 확인했다.

반경 빈칸(A0ES·A0VE·A04L·A0DY) = 스톡 900(apply_eternal_auras.py 머리 주석의 맵 안 근거, slk 미확인).
A14G: Slo1 빈칸 = 스톡. 맵 안 Aasl 명시값(−0.5 셋 · −0.25 둘 · −0.1 · −0.15)에 −0.6만 0건 + 툴팁 「60% 감소」 → −0.6(확신 보통, slk 미확인).
손해 오라 둘은 원작대로 넣는다(PM 지시): A0VE 적 방어 +15(거프) · A14F 같은 주인 아군 공격력 −75%(카이도, 자기 제외).
A142 「해군원수-자신만적용」은 이름과 달리 atar에 self가 없다 → 필드대로 자기 제외 아군 +22%(확신 낮음).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402

SELF, ALLIES, ENEMIES = 0, 1, 2
ARMOR, SPEED, SLOW, PERCENT = 4, 12, 13, 14
STOCK_AURA_RADIUS = 900   # ⚠️ slk 미확인
STOCK_AASL_SLO1 = -0.6    # ⚠️ slk 미확인, 위 주석


def both(kind, value, buff):
    return [sat.effect(kind=kind, target=SELF, multiplier=value, buffId=buff), sat.effect(kind=kind, target=ALLIES, multiplier=value, buffId=buff)]


AURAS = [
    # (로스터, 파일 꼬리, 이름, 설명, 반경, 효과들)
    ('불멸_정윤식', 'A03I', 'A03I 패기수련 — 아군 공속 +45% 오라',
     '원작 레일리 h049 uabi A03I(AOae, Oae2 +0.45, aare 825, atar …self…friend, abuf B001). 자기 포함.',
     825, both(SPEED, 0.45, 'B001')),
    ('불멸_정윤식', 'A0ES', 'A0ES 명왕의 패기 — 적 방어 −20 오라',
     '원작 레일리 h049 uabi A0ES(AHad, Had1 −20(레벨 1)/−25(레벨 2), atar air,enemies,ground, abuf B01S). 반경 빈칸 = 스톡 900. 레벨 2(T특성강화)는 레벨 축이 없어 미반영.',
     STOCK_AURA_RADIUS, [sat.effect(kind=ARMOR, target=ENEMIES, multiplier=-20.0, buffId='B01S')]),
    ('불멸_고도현', 'A0FP', 'A0FP 대해적의 위압감 — 적 방어 −45 오라',
     '원작 흰수염 h04A uabi A0FP(AHad, Had1 −45, aare 850, atar air,enemies,ground, abuf B01O).',
     850, [sat.effect(kind=ARMOR, target=ENEMIES, multiplier=-45.0, buffId='B01O')]),
    ('불멸_이승우', 'A0VE', 'A0VE 영웅 — 적 방어 +15 오라(손해 오라)',
     '원작 거프 h04C uabi A0VE(AHad, Had1 +15, atar air,enemies,ground, abuf B073). 적 방어를 올리는 불리한 오라 — 원작대로(툴팁 「적군의 방어력을 15증가시키는 불리한 버프」). 반경 빈칸 = 스톡 900.',
     STOCK_AURA_RADIUS, [sat.effect(kind=ARMOR, target=ENEMIES, multiplier=15.0, buffId='B073')]),
    ('불멸_정준영', 'A04L', 'A04L 드래곤-혁명가 — 아군 공속 +20% 오라',
     '원작 드래곤 h04D uabi A04L(AOae, Oae2 +0.20, atar …self…friend, abuf B006). 자기 포함. 반경 빈칸 = 스톡 900.',
     STOCK_AURA_RADIUS, both(SPEED, 0.20, 'B006')),
    ('불멸_이이삭', 'A062', 'A062 해군원수 — 맵 전체 아군 공격력 +11% 오라(자기 제외)',
     '원작 센고쿠 h04E uabi A062(ACac, Cac1 0.11, aare 99999, atar에 self 없음, abuf B00A).',
     99999, [sat.effect(kind=PERCENT, target=ALLIES, multiplier=0.11, buffId='B00A')]),
    ('불멸_이이삭', 'A142', 'A142 해군원수-자신만적용 — 아군 공격력 +22% 오라(자기 제외)',
     '원작 센고쿠 h04E uabi A142(ACac, Cac1 0.22, aare 850, atar에 self 없음, abuf B06W). ⚠️ 이름은 「자신만적용」인데 필드는 자기 제외 — 필드대로(확신 낮음).',
     850, [sat.effect(kind=PERCENT, target=ALLIES, multiplier=0.22, buffId='B06W')]),
    ('불멸_신지우', 'A04F', 'A04F 천왕의 검술 — 자기 공격력 +25%',
     '원작 가반 h04F uabi A04F(ACac, Cac1 0.25, aare 200, atar invulnerable,self = 자기만, abuf B003).',
     200, [sat.effect(kind=PERCENT, target=SELF, multiplier=0.25, buffId='B003')]),
    ('불멸_박은석', 'A0DY', 'A0DY 신념 — 적 이속 −35% 오라',
     '원작 제트 h04G uabi A0DY(AOae, Oae1 −0.35, atar air,enemies,ground, abuf B01G). 반경 빈칸 = 스톡 900.',
     STOCK_AURA_RADIUS, [sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.65)]),
    ('불멸_김용태', 'A0FO', 'A0FO 만물을 듣는 힘 — 아군 공격력 +60% 오라',
     '원작 로저 h04J uabi A0FO(ACac, Cac1 0.60, aare 850, atar …self…friend, abuf B021). 자기 포함.',
     850, both(PERCENT, 0.60, 'B021')),
    ('불멸_김용태', 'A0FD', 'A0FD 해적왕의 위엄 — 적 방어 −60 오라',
     '원작 로저 h04J uabi A0FD(AHad, Had1 −60, aare 850, atar air,enemies,ground, abuf B01W).',
     850, [sat.effect(kind=ARMOR, target=ENEMIES, multiplier=-60.0, buffId='B01W')]),
    ('불멸_김용태', 'A0AR', 'A0AR 로저 이동속도 감소 — 적 이속 −50% 오라',
     '원작 로저 h04J uabi A0AR(Aasl, Slo1 −0.5, aare 825, abuf B00Z).',
     825, [sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.5)]),
    ('불멸_정준영', 'A0DT', 'A0DT 흉포한 광기 — 적 이속 −70% 오라',
     '원작 빅맘 h04Q uabi A0DT(AOae, Oae1 −0.7, aare 888, atar air,enemies,ground, abuf B01B).',
     888, [sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.3)]),
    ('불멸_신지우', 'A14F', 'A14F 꺼지지 않는 공포 — 아군 공격력 −75% 오라(손해 오라, 자기 제외)',
     '원작 카이도 h07M uabi A14F(ACac, Cac1 −0.75, aare 1025, atar …player(self 없음), abuf B071). 같은 주인 아군의 공격력을 깎는 불리한 오라 — 원작대로(툴팁 「플레이어의 아군 공격력 75% 감소 디버프」).',
     1025, [sat.effect(kind=PERCENT, target=ALLIES, multiplier=-0.75, buffId='B071')]),
    ('불멸_정준영', 'A14F', 'A14F 꺼지지 않는 공포 — 아군 공격력 −75% 오라(손해 오라, 자기 제외)',
     '원작 카이도 용형 h0AD uabi A14F(ACac, Cac1 −0.75, aare 1025, atar …player(self 없음), abuf B071). 신지우(h07M) 것과 같은 버프 — 겹치면 하나만.',
     1025, [sat.effect(kind=PERCENT, target=ALLIES, multiplier=-0.75, buffId='B071')]),
    ('불멸_신지우', 'A14G', 'A14G 카이도 이동속도 감소 — 적 이속 −60% 오라',
     '원작 카이도 h07M uabi A14G(Aasl, aare 1025, abuf B04B, Slo1 빈칸 = 스톡 −0.6 — 맵 안 Aasl 명시값에 −0.6만 0건 + 툴팁 「60% 감소」, slk 미확인).',
     1025, [sat.effect(kind=SLOW, target=ENEMIES, multiplier=1.0 + STOCK_AASL_SLO1)]),
    ('불멸_정준영', 'A14G', 'A14G 카이도 이동속도 감소 — 적 이속 −60% 오라',
     '원작 카이도 용형 h0AD uabi A14G(Aasl, aare 1025, abuf B04B, Slo1 빈칸 = 스톡 −0.6 — 신지우 것과 같은 근거).',
     1025, [sat.effect(kind=SLOW, target=ENEMIES, multiplier=1.0 + STOCK_AASL_SLO1)]),
]


def main():
    changed = []
    for roster, tail, name, desc, radius, effects in AURAS:
        stem = 'SkillData_원작오라_%s_%s' % (roster, tail)
        path = os.path.join(sat.SKILL_DIR, stem + '.asset')
        if not os.path.exists(path):
            sat.new_asset(stem, name, desc + ' 2026-09-30 구현담당1(IMMORTAL_FILL_LIST §1).', 2,
                          [sat.level_block(range=float(radius), effects=effects)])
            changed += [path, path + '.meta']
        if sat.add_skill_to_unit(roster, sat.guid_of(path)):
            changed.append(os.path.join(sat.ROSTER_DIR, roster + '.asset'))
    print('바꾼 파일 %d' % len(changed))
    for c in changed:
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
