#!/usr/bin/python3
# 전설 스킬 신규 대응 — 평타 트리거 블록과 유닛 필드(2026-10-06, 구현담당1). 상시 오라는 Tools/apply_uabi_passives.py가 MASTER_UID_ROSTER_MAP.csv로 채운다.
#   드래곤 h02W → 김용태 : Legend3(평타 1/10 stomp A07D 500 범위 180,000 · 스턴 2.75초/영웅 0.41초)
#   시저  h038 → 노태현 : A0DG(ANpi 0.4초마다 33,333 · 반경 1000 상시 지대)
#   킹    h0AH → 양재모 : Legend33(평타 1/7 acidbomb A10Q: 반경 450 · 방어 −22 · 85,000 + 초당 42,500 · 2.7초 · 같은 자리 e0NC A12R −12)
#   슈가  h037 → 구주호 : A0IX 마나 오라(유닛 필드) · 평타 타입
import os, re, sys
sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat

SELF, ALLIES, ENEMIES, SINGLE = 0, 1, 2, 3
DAMAGE, STUN, ARMOR_BONUS = 0, 1, 4
ON_HIT, COOLDOWN, AURA = 0, 1, 2
AP, SPELLS = 2, 7

def unit_fields(roster, **fields):
    p = os.path.join(sat.ROSTER_DIR, roster + '.asset')
    t = open(p, encoding='utf-8').read()
    for k, v in fields.items():
        pat = re.compile(r'^(  %s: ).*$' % re.escape(k), re.M)
        assert pat.search(t), (roster, k)
        t = pat.sub(lambda m: m.group(1) + (str(v) if isinstance(v, (int, str)) else ('%g' % v)), t, count=1)
    open(p, 'w', encoding='utf-8').write(t)

def make(stem, name, desc, trig, roster, level):
    path = os.path.join(sat.SKILL_DIR, 'SkillData_' + stem + '.asset')
    if not os.path.exists(path):
        sat.new_asset(stem, name, desc, trig, [level])
    sat.add_skill_to_unit(roster, sat.guid_of(path))
    return path

out = []
# ── 드래곤 → 김용태
make('원작능력_전설적인_김용태_Legend3', 'Legend3 1/10 (드래곤 스톰프)',
     '원작 몽키.D.드래곤 h02W Legend3: GetRandomInt(1,10)==6 → 대상 위치에 e007 → stomp A07D(AOws): 반경 500 · Wrs1 180,000 · 스턴 adur 2.75초(영웅·저항 ahdu 0.41초). 피해는 Legend7 스톰프와 같은 능력 피해(AP/Spells) 환산. 2026-10-06 구현담당1(Tools/apply_legend_blocks.py).',
     ON_HIT, '전설적인_김용태',
     sat.level_block(triggerChance=0.1, range=500.0, effects=[
         sat.effect(kind=DAMAGE, target=ENEMIES, damageType=AP, attackType=SPELLS, multiplier=180000.0),
         sat.effect(kind=STUN, target=ENEMIES, duration=2.75, heroDuration=0.41)]))
unit_fields('전설적인_김용태', attackType=1, critChance=0, critBonusDamage=0)
out.append('김용태: Legend3 + 평타 normal · 크리 0')

# ── 시저 → 노태현
make('원작능력_전설적인_노태현_A0DG', 'A0DG 가스가스 열매 — 0.4초마다 33,333(반경 1000 지대)',
     '원작 시저 크라운 h038 uabi A0DG(ANpi, Eim1 33,333 · adur 0.4초 · aare 1000 · atar air,enemies,neutral,ground). 상시 주기 피해 — 유닛 곁 반경 1000 적에게 0.4초마다 33,333(능력 피해 AP/Spells 환산). 2026-10-06 구현담당1.',
     COOLDOWN, '전설적인_노태현',
     sat.level_block(cooldown=0.4, range=1000.0, effects=[
         sat.effect(kind=DAMAGE, target=ENEMIES, damageType=AP, attackType=SPELLS, multiplier=33333.0)]))
unit_fields('전설적인_노태현', attackType=1, critChance=0, critBonusDamage=0)
out.append('노태현: A0DG 지대 + 평타 normal · 크리 0')

# ── 킹 → 양재모
make('원작능력_전설적인_양재모_Legend33', 'Legend33 1/7 (킹 산성탄 A10Q)',
     '원작 킹 h0AH Legend33: GetRandomInt(1,7)==4 → King_skill_1sc: 대상 위치 e0NK acidbomb A10Q(ANab lv2: 반경 450 · Nab3 방어 −22 · Nab4 85,000 즉시 · Nab5 42,500 초당 · adur 2.7초 · B05Z) + e0NC 더미 A12R(AHad −12 · 반경 475, 같은 자리 · B06R — 수명 원문 없어 산성탄과 같은 2.7초). 즉시 피해 + 2.7초 동안 초당 42,500 지대 + 방어 감소 둘. 2026-10-06 구현담당1.',
     ON_HIT, '전설적인_양재모',
     sat.level_block(triggerChance=round(1 / 7, 6), range=450.0, effects=[
         sat.effect(kind=DAMAGE, target=ENEMIES, damageType=AP, attackType=SPELLS, multiplier=85000.0),
         sat.effect(kind=DAMAGE, target=ENEMIES, damageType=AP, attackType=SPELLS, multiplier=42500.0, duration=2.7, zoneTickInterval=1.0, zoneRadius=450.0, hitCount=1),
         sat.effect(kind=ARMOR_BONUS, target=ENEMIES, multiplier=-22.0, duration=2.7, buffId='B05Z'),
         sat.effect(kind=ARMOR_BONUS, target=ENEMIES, multiplier=-12.0, duration=2.7, buffId='B06R')]))
unit_fields('전설적인_양재모', attackType=1, critChance=0, critBonusDamage=0)
out.append('양재모: Legend33 + 평타 normal · 크리 0')

# ── 슈가 → 구주호
unit_fields('전설적인_구주호', attackType=1, critChance=0, critBonusDamage=0, manaAuraRegenPerSecond=1.25, manaAuraRange=700, manaAuraBuffId='B02H', manaAuraIncludesSelf=1)
out.append('구주호: A0IX 마나 오라 1.25/700 + 평타 normal · 크리 0')
print('\n'.join(out))
