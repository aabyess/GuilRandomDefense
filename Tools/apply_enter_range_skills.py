"""「적이 근처에 오면」(SkillTriggerType.OnEnemyEnterRange) 넣기 — 구조 칸 백로그 13번(2026-09-30 구현담당1).

원작 TriggerRegisterUnitInRangeSimple 다섯 곳(war3map_new.j 직접) 중 대응 로스터가 있는 넷:
  에넬 h05E → 제한_전법규      enel_thunder          950  표식 TurnSpeed(0.50 → 0.49)
  샹크스 h035 → 전설적인_백기현  Legend10_shanks_pegi  1049  표식 FlyHeight(≠1 → 1)
  카타쿠리 h07I → 제한_김민규    kata_04_pegi          850  표식 PropWindow(65 → 64)
  핸콕 h05C → 영원_조세민       Legend14han_petrification 950 표식 TurnSpeed(에넬과 같은 표식 — 원작도 나눠 쓴다)
  (영원 샹크스 H08Z Shanks_ET_pegi는 대응표에 없어 안 넣는다.)
갈래(에넬·카타쿠리): `PV ≥ 200 or B06B` → 센 쪽 / 아니면 1/7·1/5 → 약한 쪽 / 아니면 표식만. 에셋 셋(PV·B06B·확률)을 배타 묶음 1로 건다.
값(전부 트리거 본문):
  에넬: 적 중심 425 범위 AIsr +1 · 425,000 / 대상 8,500,000(센 쪽) · 4,250,000(1/7) — NORMAL/UNIVERSAL → AP·Spells
  샹크스: 대상 최대체력 3% + 더미 e0IU의 thunderbolt A0UE(피해 0 · 스턴 0.62 / 영웅 0.62)
  카타쿠리: 적 중심 500 범위 500,000 / 대상 4,500,000 — MAGIC/UNIVERSAL → AP·Magic / 대상 AId1 +6. 센 쪽·1/5 쪽 값이 같다
  핸콕: 대상 최대체력 3% · Aegr +5 · A0VJ +1(방어 감소 표 0 · −5 · −10의 한 칸 — EnemyDummy에 수신기가 있던 것, SkillEffectKind.A0VJStack)
%체력 식엔 A11S 인자가 없다 → skipDamageTakenMultiplier.
다시 돌려도 같은 결과(에셋이 있으면 건너뜀).
"""
import os
import sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), '..'))
sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat  # noqa: E402

ENEMIES, SINGLE = 2, 3
AP, MAGIC, SPELLS = 2, 6, 7
DAMAGE, STUN, ARMOR_BREAK, AEGR, AISR, A0VJ = 0, 1, 2, 8, 9, 15
MAXHP = 1
PV_GE = 3
ENTER = 4
TURN, PROP, FLY = '표식_TurnSpeed', '표식_PropWindow', '표식_FlyHeight'


def d(target, attack, value, **kw):
    return sat.effect(kind=DAMAGE, target=target, damageType=AP, attackType=attack, multiplier=value, **kw)


def main(dry):
    changed = []

    def make(roster, stem, title, why, **level):
        path = os.path.join(sat.SKILL_DIR, 'SkillData_' + stem + '.asset')
        if not os.path.exists(path):
            print('%-16s %s' % (roster, stem))
            if dry:
                return
            sat.new_asset(stem, title, why + ' [근접 09-30] 2026-09-30 구현담당1(Tools/apply_enter_range_skills.py).', ENTER, [sat.level_block(**level)])
            changed.extend([path, path + '.meta'])
        if not dry and sat.add_skill_to_unit(roster, sat.guid_of(path)):
            changed.append(os.path.join(sat.ROSTER_DIR, roster + '.asset'))

    def branches(roster, base, name, why, enter, mark, chance, strong, weak, rng):
        """PV ≥ 200 → strong / B06B → strong / 확률 → weak. 목록 순서 = if/elseif 순서."""
        make(roster, base + '_PV200', '%s — 근접 %g(PV 200 이상)' % (name, enter), why, enterRange=enter, range=rng, forbiddenTargetBuffId=mark,
             primaryTargetCondition=PV_GE, primaryTargetConditionValue=200.0, exclusiveGroup=1, effects=strong)
        make(roster, base + '_B06B', '%s — 근접 %g(B06B 대상)' % (name, enter), why, enterRange=enter, range=rng, forbiddenTargetBuffId=mark,
             requiredTargetBuffId='B06B', exclusiveGroup=1, effects=strong)
        make(roster, base + '_확률', '%s — 근접 %g(그 밖 %s)' % (name, enter, chance[1]), why, enterRange=enter, range=rng, forbiddenTargetBuffId=mark,
             triggerChance=round(chance[0], 6), exclusiveGroup=1, effects=weak)

    enel = lambda big: [sat.effect(kind=AISR, target=ENEMIES, multiplier=1.0), d(ENEMIES, SPELLS, 425000.0), d(SINGLE, SPELLS, big)]
    branches('제한_전법규', '원작트리거_제한_전법규_enel_thunder', '에넬 뇌격',
             '원작 에넬 h05E enel_thunder: 적이 950 안에 들어오면(TurnSpeed 0.50일 때 한 번) PV≥200 or B06B → 425 범위 AIsr +1·425000 + 대상 8500000 / 아니면 1/7로 대상 4250000.',
             950.0, TURN, (1 / 7, '1/7'), enel(8500000.0), enel(4250000.0), 425.0)

    make('전설적인_백기현', '원작트리거_전설적인_백기현_shanks_pegi', '샹크스 패왕색 — 근접 1049(대상 최대체력 3% · 스턴 0.62초)',
         '원작 샹크스 h035 Legend10_shanks_pegi: 적이 1049 안에 들어오면(FlyHeight ≠ 1일 때 한 번) 대상 최대체력 3% + 더미 e0IU thunderbolt A0UE(스턴 0.62 / 영웅 0.62).',
         enterRange=1049.0, forbiddenTargetBuffId=FLY,
         effects=[sat.effect(kind=DAMAGE, basis=MAXHP, target=SINGLE, damageType=AP, attackType=SPELLS, multiplier=0.03, skipDamageTakenMultiplier=1),
                  sat.effect(kind=STUN, target=SINGLE, duration=0.62, heroDuration=0.62)])

    kata = [d(ENEMIES, MAGIC, 500000.0), d(SINGLE, MAGIC, 4500000.0), sat.effect(kind=ARMOR_BREAK, target=SINGLE, multiplier=6.0)]
    branches('제한_김민규', '원작트리거_제한_김민규_kata_04_pegi', '카타쿠리 패왕색',
             '원작 카타쿠리 h07I kata_04_pegi: 적이 850 안에 들어오면(PropWindow 65일 때 한 번) PV≥200 or B06B, 아니면 1/5로 → 500 범위 500000 + 대상 4500000 + AId1 +6.',
             850.0, PROP, (1 / 5, '1/5'), kata, kata, 500.0)

    make('영원_조세민', '원작트리거_영원_조세민_han_petrification', '핸콕 석화 — 근접 950(대상 최대체력 3% · Aegr +5 · 방어 −5)',
         '원작 핸콕 h05C Legend14han_petrification: 적이 950 안에 들어오면(TurnSpeed 0.50일 때 한 번) 대상 최대체력 3% + Aegr +5 + A0VJ +1(방어 −5).',
         enterRange=950.0, forbiddenTargetBuffId=TURN,
         effects=[sat.effect(kind=DAMAGE, basis=MAXHP, target=SINGLE, damageType=AP, attackType=SPELLS, multiplier=0.03, skipDamageTakenMultiplier=1),
                  sat.effect(kind=AEGR, target=SINGLE, multiplier=5.0),
                  sat.effect(kind=A0VJ, target=SINGLE, multiplier=1.0)])

    print('바꾼 파일 %d' % len(set(changed)))


if __name__ == '__main__':
    main('--dry' in sys.argv)
