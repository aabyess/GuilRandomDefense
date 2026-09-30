"""영원한 등급 빠진 원작 오라 채우기(2026-09-30 구현담당1, PM 승인 — Docs/research/ETERNAL_FILL_LIST.md §1·§3).

원작 uabi 상시 오라(트리거 참조 0) 다섯 + 오뎅 둘을 Aura 발동방식 SkillData로 만들어 대응 로스터에 붙인다.
값은 war3map_new.w3a 직접 디코드. 다시 돌려도 같은 결과(이미 있으면 건너뜀).

반경 빈칸(A0HP·A0FF·A0EC) = 스톡 900: 스톡 slk는 저장소·맵에 없다. 맵 안 근거 — AOae 파생 96개의 aare 명시값에
915·925·875·850·825·805·800·700·500은 있는데 900만 0건, AHad 파생 64개도 1000·950·925·851·850·825·800은 있고 900만 0건
(에디터는 기본값과 같은 값을 저장하지 않는다). 툴팁의 「500범위」(에이스)·「850범위」(카벤딧슈)는 필드가 아니다.
겹침: 이감은 EnemyDummy가 가장 강한 하나만. 방깎 오라·아군 수치 오라는 버프 ID(원작 abuf)가 같으면 큰 것 하나, 다르면 합
— 엔진 지식, 맵 미확정. 방깎 오라(AHad)는 스킬 방깎(AId1, 상한 −75)과 다른 능력이라 그 상한 밖.
보류: 버기 오라(A0RE·A0M7·A04E·A03L) — 영원_김정래 본체 미정(사장님 결정 대기).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402

SELF, ALLIES, ENEMIES = 0, 1, 2
ARMOR, FLAT, SPEED, SLOW, PERCENT = 4, 11, 12, 13, 14
STOCK_AURA_RADIUS = 900   # ⚠️ slk 미확인, 위 주석

AURAS = [
    # (로스터, 파일 꼬리, 이름, 설명, 반경, 효과들)
    ('영원_최상호', 'A0HP', 'A0HP 매의눈 — 적 이속 −45% 오라',
     '원작 미호크 h058 uabi A0HP(AOae, Oae1 −0.45, atar air,enemies,neutral,ground, 트리거 참조 0 = 상시 오라). 반경 빈칸 = 스톡 900(맵 안 근거, slk 미확인).',
     STOCK_AURA_RADIUS, [sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.55)]),
    ('영원_최상호', 'A0EC', 'A0EC 미공자 — 적 방어 −35 오라',
     '원작 카벤딧슈 h05B uabi A0EC(AHad, Had1 −35, atar air,enemies,ground, abuf B01K). 반경 빈칸 = 스톡 900(툴팁 「850범위」는 필드가 아님).',
     STOCK_AURA_RADIUS, [sat.effect(kind=ARMOR, target=ENEMIES, multiplier=-35.0, buffId='B01K')]),
    ('영원_윤현모', 'A0FF', 'A0FF 대염계 — 적 이속 −45% 오라',
     '원작 에이스 h059 uabi A0FF(AOae, Oae1 −0.45, atar air,enemies,ground). 반경 빈칸 = 스톡 900(툴팁 「500범위」는 필드가 아님).',
     STOCK_AURA_RADIUS, [sat.effect(kind=SLOW, target=ENEMIES, multiplier=0.55)]),
    ('영원_조세민', 'A0IM', 'A0IM 석화-핸영 — 적 방어 −45 오라',
     '원작 핸콕 h05C uabi A0IM(AHad, Had1 −45, aare 850, atar air,enemies,ground, abuf B02P).',
     850, [sat.effect(kind=ARMOR, target=ENEMIES, multiplier=-45.0, buffId='B02P')]),
    ('영원_김정래', 'A16U', 'A16U 가희 — 아군 공속 +15% 오라',
     '원작 우타 h067 uabi A16U(AOae, Oae2 +0.15, aare 1250, atar …self…friend, abuf B07W). 자기 포함.',
     1250, [sat.effect(kind=SPEED, target=SELF, multiplier=0.15, buffId='B07W'),
            sat.effect(kind=SPEED, target=ALLIES, multiplier=0.15, buffId='B07W')]),
    ('영원_서민성', 'A0Y2', 'A0Y2 와노쿠니의 혼 — 자신 제외 아군 공격력 +50% 오라',
     '원작 오뎅 h08R uabi A0Y2(ACac, Cac1 0.5 퍼센트형, aare 850, atar에 self 없음 = 자신 제외, abuf B05H). %는 기본 공격력에만(SkillEffectKind.AttackPowerBuffPercent 주석).',
     850, [sat.effect(kind=PERCENT, target=ALLIES, multiplier=0.5, buffId='B05H')]),
    ('영원_서민성', 'A0YC', 'A0YC 오뎅 이도류 — 자기 평타 +115000',
     '원작 오뎅 h08R uabi A0YC(AIfb 오브, Idam 115000 = 평타 추가 피해). 툴팁(A0Y2) 「자신의 공격력 115000증가」. 자기 고정 공격력 가산으로 옮김(오브의 다른 필드 aare 400·Iob5는 미반영). 버프 ID가 없어 능력 코드를 키로 씀.',
     0, [sat.effect(kind=FLAT, target=SELF, multiplier=115000.0, buffId='A0YC')]),
]


def main():
    changed = []
    for roster, tail, name, desc, radius, effects in AURAS:
        stem = 'SkillData_원작오라_%s_%s' % (roster, tail)
        path = os.path.join(sat.SKILL_DIR, stem + '.asset')
        if not os.path.exists(path):
            sat.new_asset(stem, name, desc + ' 2026-09-30 구현담당1(ETERNAL_FILL_LIST).', 2,
                          [sat.level_block(range=float(radius), effects=effects)])
            changed += [path, path + '.meta']
        if sat.add_skill_to_unit(roster, sat.guid_of(path)):
            changed.append(os.path.join(sat.ROSTER_DIR, roster + '.asset'))
    print('바꾼 파일 %d' % len(changed))
    for c in changed:
        print(os.path.relpath(c, sat.ROOT))


if __name__ == '__main__':
    main()
