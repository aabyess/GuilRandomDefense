#!/usr/bin/python3
# 전설 스킬 「진짜 빠진 9항목·5구조」(Docs/research/LEGEND_PARTIAL_MISSING_2026-10-06.csv) + 슈가·킹의 구조 항목 — 2026-10-06 구현담당1.
#   소환체 (SummonUnit + 소환 유닛 데이터): 슈가 h06N·h07H · 시키 h07F · 시노부 분신 h085
#   시전 능력 (CooldownAutoCast 근사): 슈가 A0J0 장난감화 · 로우 A0GA 외과수술 · 카르가라 A08G 샨디아의 창
#   PV 조건 크리: 울티 A0FC  ·  주기 지대: 샹크스 A0PA  ·  평타 DoT: 레이쥬 독 A08M · 킹 화재 A10U  ·  LIFE 재생 오라: 히루루크 A0RW(유닛 필드)
# 다시 돌려도 안전(있으면 건너뜀). 수치 근거는 각 description에 w3a/w3u 원문을 적었다.
import os, re, sys, hashlib
sys.path.insert(0, os.path.dirname(__file__))
import skill_asset_tool as sat

SELF, ALLIES, ENEMIES, SINGLE = 0, 1, 2, 3
DAMAGE, STUN, SLOW, SPEED = 0, 1, 13, 12
ON_HIT, COOLDOWN, AURA = 0, 1, 2
AP, SPELLS, AD = 2, 7, 1
ATK = 3
PV_LT, PV_GE = 1, 3
RANGE_RATIO = 5.5   # 로스터 사거리 ≈ 원작 사거리 ÷ 5.5(전설 33기 로스터/원작 비율 중앙값 5.2~5.8)
ROOT = sat.ROOT
BASE_UNIT = os.path.join(ROOT, 'Assets/Data/Units/Roster/흔함_강재규.asset')
SUMMON_DIR = os.path.join(ROOT, 'Assets/Data/Units/Summons')

def unit_fields(roster, **fields):
    p = os.path.join(sat.ROSTER_DIR, roster + '.asset')
    t = open(p, encoding='utf-8').read()
    for k, v in fields.items():
        pat = re.compile(r'^(  %s: ).*$' % re.escape(k), re.M)
        if not pat.search(t):
            # 새 필드(유닛 에셋에 아직 없음) — manaAuraIncludesSelf 뒤에 붙인다(필드 순서는 유니티가 다시 쓸 때 정리)
            t = t.replace('  manaAuraIncludesSelf: ', '  %s: %s\n  manaAuraIncludesSelf: ' % (k, v if isinstance(v, (int, str)) else '%g' % v), 1)
            continue
        t = pat.sub(lambda m: m.group(1) + (str(v) if isinstance(v, (int, str)) else ('%g' % v)), t, count=1)
    open(p, 'w', encoding='utf-8').write(t)

def summon_unit(stem, unit_name, attack, speed_per_sec, range_orig, atk_type, splash=0):
    """소환체 UnitData(흔함 로스터 에셋을 본떠 복사). 소환수는 시스템 유닛(isSystemUnit) — 인벤토리·판매·획득 알림 밖."""
    os.makedirs(SUMMON_DIR, exist_ok=True)
    path = os.path.join(SUMMON_DIR, stem + '.asset')
    if not os.path.exists(path):
        t = open(BASE_UNIT, encoding='utf-8').read()
        def setf(k, v):
            nonlocal t
            t = re.sub(r'^(  %s: ).*$' % k, lambda m: m.group(1) + str(v), t, count=1, flags=re.M)
        t = re.sub(r'^  m_Name: .*$', '  m_Name: %s' % stem, t, count=1, flags=re.M)
        setf('unitName', unit_name); setf('grade', 0); setf('attackType', atk_type); setf('hp', 100)
        setf('attackPower', attack); setf('attackRange', round(range_orig / RANGE_RATIO, 2)); setf('attackSpeed', round(speed_per_sec, 4)); setf('moveSpeed', 0)
        setf('skinAlias', ''); setf('isSystemUnit', 1); setf('attackSplashRadius', splash)
        setf('sellRewardEveryNSells', 0); setf('sellRewardEveryNWisp', '{fileID: 0}'); setf('sellRewardEveryNWood', 0)
        open(path, 'w', encoding='utf-8').write(t)
        guid = hashlib.md5(('guilrd/summon/' + stem).encode()).hexdigest()
        open(path + '.meta', 'w', encoding='utf-8').write(
            'fileFormatVersion: 2\nguid: %s\nNativeFormatImporter:\n  externalObjects: {}\n  mainObjectFileID: 11400000\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' % guid)
    return sat.guid_of(path)

def summon_effect(guid, lifetime, chance=1.0, fan=0.0, radius=60.0):
    e = sat.effect(kind=SUMMON, target=SELF, chance=chance)
    return sat.effect_block(e) + '      summonUnits:\n      - {fileID: 11400000, guid: %s, type: 2}\n      summonLifetime: %g\n      summonFanDegrees: %g\n      summonRadius: %g\n' % (guid, lifetime, fan, radius)

SUMMON = 16   # SkillEffectKind.SummonUnit(= A0VJStack 15 다음) · DamageOverTime = 26(SkillData.cs 열거 순서 — 바뀌면 여기도)

def make(stem, name, desc, trig, roster, level):
    path = os.path.join(sat.SKILL_DIR, 'SkillData_' + stem + '.asset')
    if not os.path.exists(path):
        sat.new_asset(stem, name, desc, trig, [level])
    sat.add_skill_to_unit(roster, sat.guid_of(path))

report = []
R = lambda n: '전설적인_' + n

# ── 소환체 유닛
h06N = summon_unit('Summon_슈가_장난감콜렉션no31', '장난감 콜렉션 no.31', 185000, 1 / 0.39, 460, 3)
h07H = summon_unit('Summon_슈가_망가진장난감', '망가진 장난감', 250000, 1 / 0.38, 450, 3)
h07F = summon_unit('Summon_시키_전설돌떨구기', '시키 전설 돌 떨구기', 172500 + 6000, (1 / 0.48) * (1 + 2.85), 485, 1)   # +ACbh A14N 20%×30,000 평균 6,000 · 공속 +285%(A0T2)
h085 = summon_unit('Summon_시노부_분신', '시노부 분신', 21500 + 50000, 1 / 0.73, 425, 1, splash=325)                # 평타 21,500 + A0U8(AIfb Idam 50,000 · 반경 325)

# 슈가 Legend27 1/178 장난감 콜렉션(4.5초) + 그중 1/3 망가진 장난감
make('원작능력_전설적인_구주호_Legend27', 'Legend27 1/178 (슈가 장난감 콜렉션 no.31)',
     '원작 슈가 h037 Legend27: GetRandomInt(1,178)==4 → h06N 「장난감 콜렉션 no.31」(평타 185,000·주기 0.39·사거리 460) 4.5초 소환 + 그중 GetRandomInt(1,3)==1이면 A0QF(Aspt Sod1 1 Sod2 h07H)로 「망가진 장난감」(평타 250,000·주기 0.38·사거리 450) 생성 — h07H 수명은 원문 없음(시스템 유닛, 판매 버튼 A080뿐)이라 4.5초로 제안. 1/12 h06P 「장난감」(selfdestruct)은 폭발 피해 수치 원문 없음(A0J1은 아트용 스톡 Asph)이라 미반영. 소환체 사거리는 원작÷5.5. 2026-10-06 구현담당1.',
     ON_HIT, R('구주호'),
     sat.level_block(triggerChance=round(1 / 178, 6), effects=[summon_effect(h06N, 4.5), summon_effect(h07H, 4.5, chance=round(1 / 3, 4), fan=35.0)]))
# 슈가 A0J0 장난감화(AHtb 폭풍망치 계열: Htb1 1.0 피해 + 스턴 3.0초, 사거리 1150, 쿨 105초, PV<200)
make('원작능력_전설적인_구주호_A0J0', 'A0J0 장난감화 — 스턴 3초(쿨 105초·사거리 1150)',
     '원작 슈가 h037 uabi A0J0(AHtb, Htb1 1.0 · adur 3.0 · acdn 105 · aran 1150 · atar nonancient,nonsapper,organic = PV<200). 대상 지정 시전 능력 — 쿨다운마다 사거리 1150 안 가장 가까운 PV<200 적 한 기를 스턴 3초(+피해 1). 자동 시전으로 근사. 2026-10-06 구현담당1.',
     COOLDOWN, R('구주호'),
     sat.level_block(cooldown=105.0, range=1150.0, effects=[
         sat.effect(kind=DAMAGE, target=SINGLE, damageType=AP, attackType=SPELLS, multiplier=1.0, targetCondition=PV_LT, targetConditionValue=200.0),
         sat.effect(kind=STUN, target=SINGLE, duration=3.0, targetCondition=PV_LT, targetConditionValue=200.0)]))
unit_fields(R('구주호'), attackType=1)

# 시키 Legend4 elseif 1/17 h07F(1.05초) — 앞 1/10 stomp가 안 터졌을 때만이라 0.9/17
make('원작능력_전설적인_신문철_Legend4_h07F', 'Legend4 1/17 (시키 전설 돌 떨구기 h07F)',
     '원작 시키 h039 Legend4: GetRandomInt(1,10)==5(바닷물 가두기)이 아니면(elseif) GetRandomInt(1,17)==3 → h07F 「시키 전설 돌 떨구기」(평타 172,500·주기 0.48·사거리 485, A0T2 공속 +285%, A14N 20%×30,000 추가)를 1.05초 소환해 대상을 공격 — 확률은 0.9/17. 소환체 평타 = 172,500 + 평균 추가 6,000. 2026-10-06 구현담당1.',
     ON_HIT, R('신문철'),
     sat.level_block(triggerChance=round(0.9 / 17, 6), effects=[summon_effect(h07F, 1.05)]))

# 시노부 A0U7 인술 분신(1/14, 2초) — 이일중·김민규(복제)
for owner in ('이일중', '김민규'):
    make('원작능력_전설적인_%s_A0U7' % owner, 'A0U7 인술 분신 — 1/14 (시노부 분신 h085 2초)',
         '원작 시노부 h042 uabi A0U7(Aroc→소환 h085): 평타 1/14 확률로 분신 h085(평타 21,500 + A0U8 AIfb +50,000 · 반경 325 · 주기 0.73)를 2초 소환(대상 B06B면 1/4 추가 굴림은 미반영). 분신 평타 = 71,500 · 스플래시 325. 2026-10-06 구현담당1.',
         ON_HIT, R(owner),
         sat.level_block(triggerChance=round(1 / 14, 6), effects=[summon_effect(h085, 2.0)]))

# 로우 A0GA 외과수술(Auhf, 아군 하나 공속 +150% 12초, 쿨 50초, 사거리 750) → 신지우
make('원작능력_전설적인_신지우_A0GA', 'A0GA 외과수술 — 아군 한 기 공속 +150% 12초(쿨 50초)',
     '원작 로우 h03C uabi A0GA(Auhf, Uhf1 1.5 · adur 12 · acdn 50 · aran 750 · abuf B023 · atar friend,self). 쿨다운마다 사거리 750 안 아군 한 기(시전자에서 가장 가까운)에게 공속 +150% 12초. 자동 시전으로 근사. 2026-10-06 구현담당1.',
     COOLDOWN, R('신지우'),
     sat.level_block(cooldown=50.0, range=750.0, effects=[
         sat.effect(kind=SPEED, target=ALLIES, multiplier=1.5, duration=12.0, buffId='B023', maxTargets=1)]))

# 카르가라 A08G 샨디아의 창(Ablo 자기 공속 +400% 5.25초, 마나 100·재생 6/초 → 쿨 16.67초) → 박민석
make('원작능력_전설적인_박민석_A08G', 'A08G 샨디아의 창 — 자기 공속 +400% 5.25초',
     '원작 카르가라 h03F uabi A08G(Ablo, Blo1 4.0 · adur 5.25 · 마나 비용 100 · 마나 재생 6/초 → 약 16.7초마다). 자기 공속 +400% 5.25초. 마나 소비 자동 시전은 쿨다운 16.67초로 근사. 2026-10-06 구현담당1.',
     COOLDOWN, R('박민석'),
     sat.level_block(cooldown=16.67, effects=[
         sat.effect(kind=SPEED, target=SELF, multiplier=4.0, duration=5.25, buffId='B00E')]))

# 울티 A0FC 파키케팔로(AIcs 크리 33% ×10 +330,000, PV≥200 대상만) → 임채현·김건
for owner in ('임채현', '김건'):
    make('원작능력_전설적인_%s_A0FC' % owner, 'A0FC 파키케팔로 — 33% 크리 ×10 +330,000 (PV≥200 대상만)',
         '원작 울티 h03S uabi A0FC(AIcs, Ocr1 33% · Ocr2 ×10 · Ocr3 +330,000 · atar sapper = PV≥200). 평타 33% 확률로 평타 피해의 ×10(추가분 9배) + 330,000. 평타 자체는 따로 들어가므로 추가 피해만 낸다. 2026-10-06 구현담당1.',
         ON_HIT, R(owner),
         sat.level_block(triggerChance=0.33, primaryTargetCondition=PV_GE, primaryTargetConditionValue=200.0, effects=[
             sat.effect(kind=DAMAGE, basis=ATK, target=SINGLE, damageType=AD, attackType=1, multiplier=9.0, bonus=330000.0)]))

# 샹크스 A0PA 패왕색의 패기(ANpi 0.2초마다 12,500 · 반경 1051) → 백기현
make('원작능력_전설적인_백기현_A0PA', 'A0PA 패왕색의 패기 — 0.2초마다 12,500(반경 1051)',
     '원작 샹크스 h035 uabi A0PA(ANpi, Eim1 12,500 · adur 0.2초 · aare 1051 · atar organic = 기계 제외 — 우리는 구분 없음). 상시 주기 피해 지대 — 유닛 곁 반경 1051 적에게 0.2초마다 12,500. 2026-10-06 구현담당1.',
     COOLDOWN, R('백기현'),
     sat.level_block(cooldown=0.2, range=1051.0, effects=[
         sat.effect(kind=DAMAGE, target=ENEMIES, damageType=AP, attackType=SPELLS, multiplier=12500.0)]))

# 레이쥬 A08M 포이즌 핑크(Aspo 초당 200,000 · 이속 −15% · 6초) → 임건웅
make('원작능력_전설적인_임건웅_A08M', 'A08M 포이즌 핑크 — 평타 독 초당 200,000·이속 −15%·6초',
     '원작 레이쥬 h033 uabi A08M(Aspo, Spo1 200,000/초 · Spo2 0.15 이속 감소 · adur 6초 · Spo4 9). 평타마다 대상에게 6초 독 + 이속 −15%. 같은 대상에 다시 걸면 끝 시각만 늘린다(중첩 없음). 2026-10-06 구현담당1.',
     ON_HIT, R('임건웅'),
     sat.level_block(triggerChance=1.0, effects=[
         sat.effect(kind=26, target=SINGLE, damageType=AP, attackType=SPELLS, multiplier=200000.0, duration=6.0),
         sat.effect(kind=SLOW, target=SINGLE, multiplier=0.85, duration=6.0)]))

# 킹 A10U 화재(Aliq 초당 250,000 · liq2 0.45) → 양재모
make('원작능력_전설적인_양재모_A10U', 'A10U 화재 — 평타 DoT 초당 250,000·이속 −45%',
     '원작 킹 h0AH uabi A10U(Aliq, liq1 250,000/초 · liq2 0.45(이속 감소로 읽음) · atar structure,air,enemies,neutral,ground · abuf B062). 지속시간 필드가 원문에 없어(Aliq 스톡 값) 5초로 제안. 평타마다 대상에게 DoT + 이속 −45%. 2026-10-06 구현담당1.',
     ON_HIT, R('양재모'),
     sat.level_block(triggerChance=1.0, effects=[
         sat.effect(kind=26, target=SINGLE, damageType=AP, attackType=SPELLS, multiplier=250000.0, duration=5.0),
         sat.effect(kind=SLOW, target=SINGLE, multiplier=0.55, duration=5.0)]))

# 히루루크 A0RW LIFE 재생 오라(AUau Uau2 1.6 · 반경 850) → 이재윤 (유닛 필드)
unit_fields(R('이재윤'), lifeAuraRegenPerSecond=1.6, lifeAuraRange=850, lifeAuraBuffId='B04A', lifeAuraIncludesSelf=1)
print('ok')
