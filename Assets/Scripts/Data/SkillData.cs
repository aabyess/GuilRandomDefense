using System.Collections.Generic;
using UnityEngine;

// 유닛 능력(스킬) 시스템의 뼈대. 원작 조사(UPGRADE_SHOP.md 5차 ②, UNIT_SKILL_TRIGGERS.md
// 피해 715건 전수) 근거로 모양만 맞췄다 — 사장님이 "유닛별 배정은 나중에 내가 준다"고
// 하셔서 에셋은 만들지 않았고, 수치도 지어내지 않았다.
//
// ⚠️ 직렬화되는 이 파일의 enum들은 새 값을 반드시 맨 뒤에만 추가할 것 — 중간에 끼우면
// 이미 저장된 에셋의 값이 밀린다(TEAM_RULES.md 참고).

// 발동 방식. 원작에 셋 다 있다 — 평타 적중 시 확률 발동(Bash류), 쿨다운마다 자동 시전,
// 쿨다운 없이 계속 켜져 있는 오라.
public enum SkillTriggerType
{
    OnHitChance,
    CooldownAutoCast,
    Aura,

    // ⚠️ 맨 뒤에 추가 — 직렬화 순서를 지킨다.
    // 확률이 아니라 "정확히 N타째" 발동(원작 특성 26종 발동 게이트 78건 중 24건이 이
    // 방식이었다, ORIGINAL_TRAIT_PROC_CHANCE.csv — PM 지시 2026-09-05). 원작 예: 마나를
    // 카운터로 써서 평타마다 +1, 특정 값(샹크스 35·키드 50·센고쿠 75·시라호시 120·루피 160·
    // 핸콕 175)에 닿으면 그때만 발동하고 되돌린다. OnHitChance(1/N)로 근사하면 기댓값은
    // 같아도 "정확히 주기적"이라는 원작 감각이 사라져서 별도 타입으로 뗐다.
    OnHitCount,
}

// OnHitCount 전용 — 이 카운터가 원작의 어느 공유 스탯(마나/체력)을 대신하는가. 새 enum이라
// 아무 데나 둬도 되지만(기존 enum 값 순서를 안 건드리므로), 시작값은 관례대로 0=Mana.
//
// ⚠️ 원작은 이 값이 유닛 하나에 하나뿐이다(WC3 유닛은 마나 하나·체력 하나) — 그래서
// UnitAttacker는 이 카운터를 스킬(SkillData)마다 따로 안 두고, 유닛 인스턴스에 게이지
// 종류당(마나 1개·체력 1개) 딱 하나씩만 둔다. 같은 유닛의 여러 스킬이 같은 gaugeKind를
// 쓰면 그 카운터를 공유한다는 뜻이다(MULTI_SKILL_IMPACT.md ⑤-B — 사보 4개가 마나 125
// 하나를 같이 보는 게 실제 원작 사례로 확인됐다). 임계값(hitCountThreshold)·리셋값
// (resetTo)은 스킬마다 다를 수 있다 — 카운터만 공유하고 판정은 스킬별이다.
public enum SkillGaugeKind
{
    Mana,
    Life,
}

// 효과가 누구에게 가는지. 한 스킬(레벨)이 "적에게 피해 + 아군 회복"을 동시에 할 수 있어서
// (원작 예: 보스 A153 반경450 적 피해 + A11T 반경955 아군 회복) SkillLevel이 아니라
// SkillEffect마다 따로 갖는다.
public enum SkillTargetKind
{
    Self,
    Allies,
    Enemies,
    SingleTarget,
}

// 피해·효과 값이 무엇에 비례하는가. 원작 715건 전수 조사(UNIT_SKILL_TRIGGERS.md) 기준
// 여섯 갈래 중 넷만 담는다 — 연구단계(ResearchLevel)는 연구소가 만들어지는 중이라 포함했다.
// ⚠️ 2026-09-06 정정: 영웅스탯 비례 축은 사장님 01번 확정("영웅 스탯 시스템을 만들어라")으로
// 방침이 뒤집혔다 — "영웅 스탯 개념 자체가 없고 생길 계획도 없다"는 이 주석의 옛 전제는
// 더 이상 맞지 않는다. 영웅스탯 비례 basis는 아직 미구현이며 담을 예정이다(enum은 직렬화
// 순서 때문에 여기서 안 건드림).
public enum SkillEffectBasis
{
    Flat,                    // multiplier가 고정값 그 자체
    TargetMaxHpPercent,      // 대상 최대체력 × multiplier
    TargetCurrentHpPercent,  // 대상 현재체력 × multiplier
    CasterAttackPower,       // 시전자 평타 공격력 × multiplier + bonus (원작 예: atk×2.5+32500)
    ResearchLevel,           // 연구소 단계 × multiplier + bonus. 연구소(05번, 구현담당1)가
                             // 서면 그 값을 여기 잇는다 — 지금은 자리만이다.

    // ⚠️ 맨 뒤에 추가 — 직렬화 순서를 지킨다.
    // 원작 GetEventDamage() 비례(715건 중 17건) — 처음엔 "이 유닛이 받은 피해에 비례해
    // 되돌려준다"는 반응형 축으로 읽어서 "적 쪽 훅이 없어 못 담는다"고 봤었다.
    //
    // ⚠️ 2026-09-06 정정(PM 지시로 재확인): CSV의 17행 전부 게이트가 "(게이트 없음)" 아니면
    // "MANA/LIFE게이지…" — **전부 OnHitChance/OnHitCount, 즉 "평타가 맞았을 때"만 도는
    // 경로다.** GetEventDamage()는 적이 받은 피해가 아니라 **그 순간 방금 나간 그 평타
    // 자신의 피해량**이었다 — 새 아키텍처가 필요한 반응형 축이 아니라
    // UnitAttacker.TryCastOnHitSkill이 그 순간 이미 알고 있는 값(AttackDamage)이었다.
    // UnitAttacker.ResolveSkillEffectValue가 recentAttackDamage 매개변수로 그 값을 그대로
    // 받아 쓴다 — CastSkillLevel 체인을 타고 흘러온다.
    ReceivedDamage,

    // ⚠️ 맨 뒤에 추가 — 직렬화 순서를 지킨다. (2026-09-06, PM. 구현담당3 무응답으로 직접 배선)
    // 원작 "상수 x (1 + 0.01 x 대상이속 x 계수)" 꼴 4행(다섯번째 황제 2 · 니카 2)을 담는다.
    // 전개하면 `상수 + 대상이속 x (상수 x 0.01 x 계수)`라 우리 `기준값 x multiplier + bonus`
    // 패턴에 그대로 맞는다 — 데이터 쪽에서 multiplier에 (상수 x 0.01 x 계수)를 넣는다.
    // 대상 이속은 EnemyData.moveSpeed(EnemyDummy.MoveSpeed)라 새 데이터가 필요 없다.
    //
    // ⚠️ 야마토 1행은 여기 안 담긴다 — `0.83 + min(1.17, 90/(이속+0.01))`로 **반비례**이고
    // 상한이 있다(느릴수록 셈, 최대 2.00배). 개별 처리 대상이다
    // (Docs/reference/OUT_OF_AXIS_CLASSIFY.md 참고).
    TargetMoveSpeed,

    // 원작 "상수 x (1 + 0.005 x 시전자 현재 게이지)" 꼴 5행(부릉냐 h09N). 위와 같은 전개로
    // `상수 + 게이지 x (상수 x 0.005)`가 되어 같은 패턴에 담긴다.
    //
    // ⚠️ 이름을 "마나"로 짓지 않는다(PM 지시). 원작 마나는 **평타 +1 AND E 스킬 +5** 두
    // 경로인데 우리엔 E 경로가 없어 게이지가 원작보다 천천히 오른다 — 배율 상한(x1.745)에
    // 덜 도달하는 **알려진 과소**다. E를 구현하면 그때 +5를 이 게이지에 이어야 한다.
    // ⚠️ 지금은 마나 게이지만 읽는다 — 원작의 이 꼴을 쓰는 5행이 전부 부릉냐(마나)라서다.
    // 체력 게이지 사례가 나오면 그때 level.gaugeKind를 여기까지 흘려보내야 한다.
    CasterGaugeValue,

    // ⚠️ 맨 뒤에 추가 — 직렬화 순서를 지킨다. (2026-09-06, PM)
    // 원작 `GetUnitAbilityLevelSwapped('AXXX', 시전자) x k` 꼴. 원작 능력 레벨은 1 또는 2이고
    // 우리 SkillData.levels[0]/[1]과 1:1로 대응한다(UnitUpgrades.SkillLevelIndexFor +1).
    //
    // 이 축이 없어서 초월함 스킬의 「43,500 x 레벨」·「17,500 x 레벨」 같은 비례항을 통째로
    // 버리고 상수항만 옮기고 있었다 — 값을 깎는 근사 6건 중 4건이 초월함에 몰려 있었고
    // (Docs/reference/TRANSCENDENT_DAMAGE_INVESTIGATION_2026-09-06.md), 그게 「원작 초월함
    // 스킬의 1/3」의 주된 원인이었다.
    //
    // ⚠️ 같은 조사에서 나온 나머지 절반인 「영웅 능력치(STR/AGI/INT) x k」는 여기 안 담긴다 —
    // 그건 축이 없는 게 아니라 **우리 유닛에 영웅 스탯이라는 개념 자체가 없어서**다.
    // 축 하나로 안 끝나고 시스템을 만들어야 한다(사장님 판단 대기).
    CasterSkillLevel,

    // ⚠️ 맨 뒤에 추가 — 직렬화 순서를 지킨다. (2026-09-06, 사장님 결정 01번)
    // 위 CasterSkillLevel 주석에서 "시스템을 만들어야 한다"고 미뤄둔 그 축 — 원작
    // GetHeroStatBJ(영웅, STR/AGI/INT, true) x multiplier + bonus 대응. 🔴 위험 4건
    // (Zoro_enfor_3dragon 등, OUT_OF_AXIS_CLASSIFY.md — 고정항 대비 STR比가 28~71%라
    // bonus 흡수가 화력을 반토막 냈을 대상들)이 이 축으로 담긴다.
    // UnitAttacker.CurrentStrength/Agility/Intelligence가 읽는 UnitData.baseStrength 등이
    // 전부 기본값 0f인 지금은 이 값을 채운 스킬도 항상 0을 받는다 — 회귀 없음.
    CasterStrength,
    CasterAgility,
    CasterIntelligence,

    // ⚠️ 맨 뒤에 추가 — 직렬화 순서를 지킨다. (2026-09-06, A0LZ_CASTER_STACK_INVESTIGATION.md/7703d2c)
    // 원작 `GetUnitAbilityLevelSwapped('AXXX', 시전자)` 꼴이되 CasterSkillLevel과는 다르다 —
    // CasterSkillLevel은 능력 레벨이 1/2뿐이고 SkillData.levels[0]/[1]과 1:1 대응하는데, 이 축은
    // 시전자 자신이 영구히 쌓는 0~N 정수 레벨(원작 비비 A0LZ, 0~10)로, 능력 자체의 필드값은
    // 상수(장식)고 밖에서 읽는 레벨 정수 그 자체가 진짜 스탯이다 — A11S와 같은 "카운터 전용"
    // 부류다. CasterStrength/Agility/Intelligence(영웅스탯, 파티 전체 공유 성장치)와도 다르고,
    // casterBuffCountFactor(현재 버프 개수, 일시적)와도 다르다 — A0LZ는 리셋 없이 영구 누적된다.
    // UnitAttacker.SelfUpgradeLevel(유닛 인스턴스별 런타임 카운터, TryUpgradeSelf로 올림)이
    // 이 값을 들고 있다. multiplier/bonus는 다른 레벨 기반 basis와 같은 관례(level×multiplier+bonus).
    CasterSelfUpgradeLevel,
}

// 무엇을 하는 효과인가.
// ⚠️ 값이 있으면 사람은 "쓰이는구나"로 읽는다(오늘 buffAttackSpeedMultiplier가 그렇게
// 오해를 샀다) — Stun/ArmorBreak/ExtraProjectile은 값을 채워도 아무 코드도 안 읽는다는 걸
// 여기 명시한다(이름만 세워둔 자리). 사장님이 실제 유닛 능력을 배정할 때 값의 의미를 정하고,
// 그때 읽는 코드를 짠다.
//
// Damage/ArmorBonus/HealOverTime 셋은 실제로 읽는 코드가 있다(EnemyDummy.ApplyAllyAuraEffect,
// UnitAttacker.DealSkillDamage) — 04번(보스 오라, 원작 A153/A11T) 근거로 추가했다.
public enum SkillEffectKind
{
    Damage,
    Stun,
    ArmorBreak,
    ExtraProjectile,

    // ⚠️ 아래 둘부터 맨 뒤에 추가한 값이다 — 직렬화 순서를 지킨다.
    // 부호 있는 값(EnemyDummy.EffectiveArmor 증가량). 양수=버프, 음수=디버프도 같은 kind로
    // 표현한다(원작 A153 Had1=+10.0). 적용: EnemyDummy.AddArmorShred(-multiplier).
    ArmorBonus,
    // 초당 체력회복 가산치(원작 A11T Uau2=350000.0, Unholy Aura류). 적용:
    // EnemyDummy.AddRegenBonus(multiplier) — data.hpRegenPerSecond와 별개로 더해진다.
    HealOverTime,

    // ⚠️ 맨 뒤에 추가 — 직렬화 순서를 지킨다.
    // 버프 레지스트리(UnitAttacker.AddBuff, 2026-09-06)에 SkillEffect.buffId를 건다 —
    // requiredBuffId/forbiddenBuffId 게이트가 실제로 조회할 대상을 만드는 자리다(PM 지시:
    // "버프를 실제로 걸 때만 게이트로 쓰라" — 안 그러면 requiredBuffId를 채운 스킬이
    // 영영 안 나간다). target(Self/Allies)에 따라 UnitAttacker.AddBuff를 부른다.
    // duration(위 필드)이 그대로 버프 지속시간이다 — 0이면 영구(RemoveBuff로만 없어짐,
    // ArmorBreak 등 기존 kind들과 같은 관례).
    ApplyBuff,

    // ⚠️ 맨 뒤에 추가(2026-09-06, B03Z/로우, PM 지시) — 발동 시 buffId로 지정한 버프를
    // 즉시 뗀다(효과 자체는 여기까지 — 버프 지속시간·재부여 조건과 무관하게 "이 순간
    // 뗀다"만 한다). 원작 예: 로우 "MANA==135(버프 오픈, 5초 창) → LIFE>=250이면
    // 즉사+버프 제거+e0RR 소환" — 같은 트리거 안에서 거는 곳과 쓰는 곳이 같아, 쓰는
    // 쪽이 버프를 직접 떼지 않으면 다음 열림 때까지 계속 남아 있다가 무한 누적된다.
    // target(Self/Allies/Enemies)에 따라 UnitAttacker.AddBuff의 반대짝(RemoveBuff)을
    // 부른다 — ApplyBuff와 정확히 대칭이다. buffId가 비어있으면 조용히 무시.
    RemoveBuff,

    // ⚠️ 맨 뒤에 추가(2026-09-06, TARGET_SIDE_AXES.md/STACK_AXES_INCREMENT_LIST.md) —
    // 대상측 3축(Aegr·AIsr·A11S) 스택. multiplier가 곧 올릴 레벨 수(int로 자른다,
    // 원작 트리거의 "+N" 그대로). EnemyDummy.AddAegrStack/AddAisrStack/AddA11SStack이
    // 각자 상한을 알아서 자른다 — 여기서 값을 다시 자르지 않는다. duration은 안 본다
    // (원작 스택은 지속시간 없이 영구 누적).
    AegrStack,
    AisrStack,
    A11SStack,

    // ⚠️ 맨 뒤에 추가(2026-09-07, PM 지시 — "Self 고정값 공격력 버프" kind 신설) — 원작
    // ANbr(배틀로어)/ACbh 계열처럼 "일정 시간 동안 공격력을 고정값만큼 올린다"는 능력을
    // 표현한다. 기존 ApplyBuff는 buffId 등록(게이트용 이름표)만 하고 실제 수치 효과가
    // 없고, AddAttackPowerBuff는 배율(%) 전용이라 이 값(예: A0JU +40,000)을 못 옮겼다.
    // 전수 census(2026-09-07) 결과 이 kind가 필요한 자산이 3건 확인돼(원작013_H099/A0JU
    // — 남겨둠, 더미채널_불멸_신지우_h04F/A05G — 이미 제거됨, 버프게이트_초월_양재모_
    // AD_B03Z/A09H — 이미 제거됨) "하나뿐이면 과함/여럿이면 만드는 게 맞다"(PM 지시)에
    // 따라 신설한다. multiplier가 더할 고정값, duration이 지속시간(0=영구, RemoveBuff로만
    // 해제), buffId가 있으면 버프 레지스트리에도 등록해 requiredBuffId 게이트가 조회할
    // 수 있게 한다(ApplyBuff와 같은 관례) — target(Self/Allies)에 따라
    // UnitAttacker.AddFlatAttackPowerBuff를 부른다. 기존 h04F/B03Z 2건은 이 kind가
    // 없어서 "제거"로 처리됐던 케이스라 지금 다시 채울지는 별도 판단(값이 늘어나는
    // 변경이라 이번 배치엔 안 건드림) — H099/A0JU만 이번에 채운다.
    AttackPowerBuffFlat,

    // ⚠️ 맨 뒤에 추가(2026-09-07, PM 지시 — "Allies/Self 고정 배율 공격속도 버프" kind
    // 신설) — 원작 AOae(Endurance Aura) 계열의 "범위 내 아군/자신 공격속도 +N%" 능력을
    // 표현한다. 기존 ApplyBuff는 이름표만, AddAttackSpeedBuff(attackSpeedBuffs 리스트)는
    // 수동 관리(호출부가 직접 Remove) 전용이라 SkillEffect 기반 자동 만료(duration/
    // buffHitCharges)와 안 맞는다. 전수 census(2026-09-07) 결과 4건 확인 — 원작009_H094/
    // A0WK(Self+12%, 채움) · 원작018_H09I/A0QZ(Allies+15%, 소환된 더미가 오라를 내는
    // 구조라 이 kind와는 별개로 "지속 오라를 내는 더미" 축이 여전히 없어 미완성 유지,
    // 채우지 않음) · 원작007_H08V/A107(Self+7%, 채움) · 하네카와 츠바사/A0L8(Allies+20%,
    // 이 원작 캐릭터 자체가 아직 로스터 어디에도 배정 안 됨 — ROSTER_DEFICIT 백로그
    // 항목이라 별개, 채우지 않음). "여럿이면 만드는 게 맞다"(PM 지시)에 따라 신설.
    // multiplier는 원작 그대로 raw 퍼센트(0.15=15%, 배율 아님) — UnitAttacker가
    // (1+multiplier)로 변환한다. duration/buffId/buffHitCharges 관례는 AttackPowerBuffFlat과
    // 동일. AttackSpeedMultiplier 체인에 배율로 곱해진다(HeroAttackSpeedMultiplier와
    // 같은 성격 — 공속은 애초에 배율 축이다, AttackDamage의 가산 축과 다르다).
    AttackSpeedBuffPercent,
}

// ⚠️ 2026-09-06 신설(PM 지시, "대상 조건 게이트") — SkillEffect 전용. 원작 조사(리서치담당,
// chance:1 다중효과 62파일 JASS 기계추적)에서 확률처럼 보이던 게 실은 **대상이 누구냐로
// 갈리는 결정론적 분기**인 사례가 25건 나왔다 — 대부분(19건)이 `GetUnitPointValue(대상)`을
// 문턱(200·300 등)과 비교하는 3단 분기(<·==·>=)다. `chance`로 옮기면 평균은 맞아도 분산이
// 틀린다(원작은 보스에게 "항상" B식이 나가는데 확률로 옮기면 절반만 나간다).
//
// ⚠️ 버프 조건(나머지 6건 중 2건, B06B 보유 여부)은 이미 SkillEffect.requiredTargetBuffId/
// forbiddenTargetBuffId(06번①-2)로 담을 수 있어 이 enum에 안 넣는다 — 그 필드가 정확히
// "긍정/부정 짝"을 이미 표현하므로 중복 축을 안 만든다. 이 enum은 포인트값 비교 전용이다.
//
// ⚠️ 임계값(200/300 등)은 이 enum이 아니라 SkillEffect.targetConditionValue(자산 필드)에
// 담는다 — 코드에 상수로 박지 않는다(원작에 200과 300이 둘 다 실재해서 어느 효과가 어느
// 문턱을 쓰는지는 리서치담당 조사가 끝나야 안다).
public enum SkillEffectTargetCondition
{
    None,
    TargetPointValueLessThan,
    TargetPointValueEqual,
    TargetPointValueAtLeast,
}

// 효과 하나. 레벨 하나가 이걸 여러 개 가질 수 있다 — "레벨2에 효과가 하나 더 생긴다"(원작
// 중력장: Lv2에 25% 확률 운석낙하 추가)를 "레벨마다 독립된 리스트"로 자연스럽게 담기 위함이다.
[System.Serializable]
public class SkillEffect
{
    public SkillEffectKind kind = SkillEffectKind.Damage;
    public SkillEffectBasis basis = SkillEffectBasis.Flat;
    public SkillTargetKind target = SkillTargetKind.Enemies;

    // 이 피해가 물리(AD)인지 마법(AP)인지 + 상성표 어느 행을 타는지. 원작 구조 확정(2026-09-05,
    // PM/사장님 B안): **평타는 항상 물리다(원작 플레이어 유닛 431종 평타 공격타입 전수조사 —
    // normal 127·siege 89·hero 28·pierce 25·chaos 1·magic 0). 마법은 오직 스킬(트리거) 피해에만
    // 있다**(715건 중 62건, 9%). 즉 우리 유닛이 마법 피해를 낼 수 있는 유일한 자리가 여기다 —
    // `UnitData.damageType`(평타용)은 이제 못 건드린다. 기본값은 AD/Unassigned — 스킬도 대개는
    // 물리이고, 마법 스킬만 여기서 AP로 명시한다.
    //
    // ⚠️ `DamageTable.RowMatches(damageType, attackType)`가 둘의 짝을 검사한다(AP엔 Magic/Spells
    // 행, 물리엔 Normal/Pierce/Siege/Hero/Chaos 행) — 짝이 안 맞으면 옛 버그(마법이 물리 상성을
    // 타던 것)가 돌아온다. `UnitAttacker.DealSkillDamage`가 이 값을 그대로 `EnemyDummy.TakeDamage`에
    // 넘겨서 그 검사를 자동으로 탄다(별도 검사 코드를 여기 새로 안 만들었다 — MitigatedDamage가
    // 이미 하는 일을 중복할 이유가 없다).
    public DamageType damageType = DamageType.AD;
    public AttackType attackType = AttackType.Unassigned;

    // Flat이면 고정값 그 자체. 비례 basis면 배율(원작 "atk×2.5+32500"의 2.5).
    public float multiplier;
    // 비례식의 +상수항(위 예의 32500). Flat이거나 상수항이 없으면 0.
    public float bonus;

    // 이 효과 자체의 발동확률(0~1). 기본 1 = 항상 발동. 원작 예: 중력장 Lv2 운석낙하 0.25.
    [Range(0f, 1f)] public float chance = 1f;

    // 다단히트 — SupportSkillData.waveCount/duration과 같은 관례다: hitCount<=1이면 즉발
    // 1회(duration은 그때 지속시간 용도, 예: 스턴). hitCount>1이면 duration에 걸쳐 나눠
    // 때린다(간격 = duration/hitCount) — 원작 예: 보스 A153의 "22만 데미지 × 3".
    public int hitCount = 1;
    public float duration;

    // ⚠️ 맨 뒤에 추가 — 직렬화 순서를 지킨다.
    // 원작 realD = 0.03 × 시전자가 가진 버프 개수(원작 예: 거프 — `UnitCountBuffsExBJ`
    // 원문으로 확인됨) — 최종 피해에 (1 + 이 값×버프개수)를 곱하는 배율이다. **basis가
    // 아니라 이 필드로 뗀 이유**: 다른 basis는 전부 "피해량 자체를 정하는 근거"인데 이건
    // "다른 basis가 정한 값에 사후에 곱해지는 배율"이라 성격이 다르다(PM 지시 2026-09-05) —
    // basis 자리에 넣으면 "이 값이 곧 피해량"으로 읽혀 실제 계산(원작 (6,000,000+maxHP×
    // 0.05)×(1+0.03×버프개수)의 뒤쪽 절반)과 안 맞는다.
    //
    // ⚠️ 2026-09-05 PM 정정: 처음엔 "realD=버프개수"를 715건 중 29건 전체에 일반화했는데
    // 틀렸다 — realD는 스테이지 머신의 3번 레지스터라 계열마다 담기는 값이 다르다(46건
    // 재추적 결과: 버프개수 6건 · 영웅 능력치 12건 · 캐릭터 전용 누적변수 8건 · 다른
    // 레지스터 경유 9건 · 추적 불가 13건 등). 거프 하나만 보고 일반화한 것이 원인 —
    // **이 필드는 "realD=버프개수"로 확인된 건에만 쓸 것.** 나머지 계열은 이 필드가 아니라
    // 각자 basis(예: CasterAttackPower 등)로 담아야 한다.
    //
    // 기본 0 = 무효(배율 1.0로 계산되어 곱해도 결과가 그대로다) — 필드 자체가 빠진 기존
    // 227개 효과도 C# 기본값 0f로 읽혀 똑같이 무효, 회귀 없다.
    //
    // ⚠️ 2026-09-06 갱신: UnitAttacker.CountCasterBuffs()가 예전엔 항상 0을 돌려주는
    // 자리만 만든 자리였는데, 이제 버프 레지스트리(UnitAttacker.activeBuffs)를 실제로
    // 센다 — attackSpeedBuffs/attackPowerBuffs(SupportShop 버프)도 이 레지스트리에
    // 같이 등록되므로 포함된다. 원작이 시전자의 워크3 버프 전부(자기 스킬이 건 것·
    // 오라·적이 건 디버프까지)를 센다는 것과 완전히 같지는 않다 — 우리 레지스트리에
    // 아직 등록되는 종류가 한정적이다(SupportShop 버프·OnHitChance 절대쿨 중
    // selfBuffId를 채운 것뿐, 적이 거는 디버프 같은 건 없다). 다만 최소한 "0으로 죽어
    // 있진 않다."
    //
    // ⚠️ 2026-09-06 정정(구현담당3): 아래엔 "이 필드는 실질적으로 쓸 데가 없다"고
    // 적었었는데 **거프 Garp_AttackDamage #1·#3에서 실제로 틀렸다는 게 확인됐다**
    // (`b07e545`, 리서치담당). 그 두 행은 `GetEventDamage()×(1.10/1+realD)` 꼴이라
    // **ReceivedDamage(축) × (1+factor×버프개수)** — 이 필드가 정확히 모델하는 형태다
    // (실제로 factor=0.25가 들어갔다). 아래 결론은 **`(1,000,000+realD)×버프개수`
    // 꼴에만 적용된다** — "basis 값 자체에 버프개수가 곱해지는" 경우는 여전히 이
    // 필드가 아니라 개별 처리가 맞다. 즉 이 필드가 맞는 꼴인지는 "결과 = basis값 ×
    // (1+factor×count)"로 정확히 분해되는지를 매번 확인해야 한다 — 이름이 같다고
    // (realD=버프개수) 무조건 이 필드에 넣으면 또 뿌리 ⑨를 밟는다.
    public float casterBuffCountFactor;

    // ⚠️ 맨 뒤에 추가 — 직렬화 순서를 지킨다.
    // SkillEffectKind.ApplyBuff 전용 — 부여할 버프의 id. 원작 버프 ID를 문자열로 그대로
    // 적는다(예: "B00J"). 다른 스킬의 SkillLevel.requiredBuffId/forbiddenBuffId가 이
    // 문자열로 조회한다 — 철자를 맞추는 건 데이터 작성자 책임이다(코드가 뜻을 검증 안 함).
    public string buffId = "";

    // ⚠️ 맨 뒤에 추가 — 직렬화 순서를 지킨다.
    // 원작 RRD(시전자, 대상, c, min, max, 공격타입, 피해타입) — 실제 피해 = c ×
    // GetRandomReal(min, max). 지금까지 c(=이 클래스의 multiplier/bonus로 만드는 값)만
    // 옮기고 4·5번째 인자(난수 배율)를 통째로 무시해왔다(2026-09-06 리서치담당 전수:
    // RRD 649건 중 152건(23.4%)이 배율≠1 — 난수 134건(1.0~1.5 86건 등) + 고정 배율≠1
    // 18건). 원작 피해의 4분의 1이 틀린 값이었다.
    //
    // ⚠️ 기본값이 반드시 "배율 없음"(=1.0)이어야 한다 — 새 필드가 0으로 읽히면 기존
    // 302개 효과의 피해가 전부 0이 된다(이 프로젝트 단골 사고: acceptedGrades·range 0·
    // takesPercentDamage·casterBuffCountFactor와 같은 꼴). 필드 기본값 자체를 1f로
    // 선언했고(신규 에셋·에디터에서 항목을 새로 추가해도 1,1로 보인다), **거기에 더해**
    // UnitAttacker의 적용부에서 "둘 다 정확히 0"이면 필드가 없던 걸로 보고 1.0으로
    // 읽는 방어를 한 번 더 건다(직렬화가 어떤 경로로 0,0을 만들어도 안전하게) — PM 지시.
    // 실제로 0..X 범위(예: 0~5배)를 원작이 쓴다면 0이 아닌 아주 작은 값으로 min을 잡을
    // 것 — "정확히 0,0"만 안전장치에 걸린다.
    public float randMin = 1f;
    public float randMax = 1f;

    // ⚠️ 맨 뒤에 추가(2026-09-06, PM 지시) — SkillEffectKind.ApplyBuff 전용. 원작
    // 제트 A09E 조사에서 나온 사례: 버프 B00M의 "0.6초 창"은 시간이 아니라 **타수**였다
    // (원작 실효 공격간격이 연구로 크게 빨라져 0.6초 안에 약 3대가 들어간다 — 우리는
    // 그 공격속도 스케일을 안 쓰므로 "0.6초"를 시간 그대로 옮기면 뜻이 사라진다,
    // BALANCE_SIMULATION §22-4). **0이면(기본값) 기존처럼 `duration`초 뒤 시간 만료**
    // (회귀 없음) — 1 이상이면 시간이 아니라 **이 버프를 가진 유닛이 평타를 N번 더
    // 날릴 때까지** 유지되고 그 뒤 만료된다(`duration`은 이 경우 무시). 카운트다운은
    // `UnitAttacker.TryCastOnHitSkill`이 매 평타 끝에 한 번씩 깎는다(게이지 리셋과
    // 같은 자리) — 버프를 새로 거는 그 평타 자체는 아직 안 깎인 채로 게이트를 통과하지
    // 못한다(연다-쓴다가 같은 타에 안 겹친다, h04G와 같은 이유).
    public int buffHitCharges;

    // ⚠️ 맨 뒤에 추가(06번①-2, 2026-09-06, PM 지시) — SkillLevel.requiredTargetBuffId/
    // forbiddenTargetBuffId는 "스킬 전체"를 막거나 통과시킬 뿐이라, **한 스킬 안에서
    // 효과마다 다른 대상 버프 조건**을 못 담는다(원작006 A10S 감마나이프: 효과A는 항상,
    // 효과B는 "대상이 B06B 보유"일 때만 — SkillLevel 게이트 하나로는 A까지 같이 막히거나
    // B까지 같이 나가버린다, 둘 다 원작과 다르다). 그래서 같은 이름의 게이트를 효과
    // 단위에도 둔다 — `UnitAttacker.ApplyToEnemy`가 대상(target) 하나하나에 대해 이
    // 효과를 적용하기 직전에 검사한다. **SkillLevel 쪽 게이트는 안 지운다** — 두 층이
    // 다른 일을 한다(스킬 자체를 막는 것 vs 효과 하나를 막는 것), 원작006처럼 같은
    // 스킬 안에 무조건 효과와 조건부 효과가 같이 있으면 레벨 게이트는 비워두고 이
    // 필드만 쓴다. 기본값 ""(둘 다 빈 문자열) = 조건 없음 — 기존 자산 전부 회귀 0이다.
    public string requiredTargetBuffId = "";
    public string forbiddenTargetBuffId = "";

    // ⚠️ 맨 뒤에 추가(2026-09-06, PM 지시, "대상 조건 게이트") — 대상의 원작 포인트값
    // (EnemyData.pointValue)을 targetConditionValue와 비교해 이 효과를 걸지 말지 정한다.
    // 위 requiredTargetBuffId 게이트와 같은 자리(UnitAttacker.ApplyToEnemy)에서, 같은
    // 방식(대상 하나하나마다 따로 평가 — AoE면 적마다 갈린다)으로 검사한다.
    // 기본값 None = 조건 없음, 항상 통과(기존 365개 에셋 전부 이 필드가 없어 C# 기본값
    // None으로 읽히므로 회귀 없음). targetConditionValue 기본 0은 None일 때 안 쓰인다.
    public SkillEffectTargetCondition targetCondition = SkillEffectTargetCondition.None;
    public float targetConditionValue;

    // ⚠️ 맨 뒤에 추가(2026-09-06, PM 지시, "캐스케이드 그룹") — 원작 if/elseif/else 사슬
    // 대응. 대상 조건 게이트(targetCondition, "누가 대상이냐"로 갈리는 결정론)와는 다른
    // 축이다 — 이건 "앞 단계가 이미 발동했느냐"로 갈리는 결정론이다(원작 예:
    // Cavendish_Attack 1단계 10% → 실패시에만 2단계 1/9, Legend4 1단계 10% → 실패시에만
    // 2단계 1/17, Nami_Skill_2 33%/67%). 지금은 SkillEffect.chance가 효과마다 독립
    // 판정이라 이 셋에서 "원작에 없는 동시발동"과 "원작에 없는 둘다실패"가 생긴다
    // (강주혁 파일 실측 각 22.11%).
    //
    // 기본값 0 = 그룹 없음(오늘까지의 완전 독립 판정, 회귀 없음 — 기존 365개 에셋 전부
    // 이 필드가 없어 0으로 읽힌다). 0이 아닌 같은 값을 공유하는 효과들은 **같은 레벨의
    // effects 리스트 안에서, 선언 순서 그대로** if/elseif/else 사슬을 이룬다 — ⚠️ 자산을
    // 배선하는 쪽은 반드시 원작 if/elseif 순서와 같은 순서로 effects를 나열할 것(순서가
    // 틀리면 컴파일 에러 없이 다른 캐스케이드가 나온다). 각 효과의 chance는 원작의 그
    // 단계 확률(예: 10%, 1/9, 1/17) 그대로 넣는다 — 합성확률(예: "1단계 실패 후 1/9"의
    // 최종 기댓값)을 미리 계산해서 넣지 않는다. 같은 그룹의 앞선 효과가 이미 발동했으면
    // (같은 시전(CastSkillLevel 한 번) · 같은 대상 기준) 뒤 효과는 chance를 굴리지도
    // 않고 건너뛴다(UnitAttacker.ApplyToEnemy/ApplyToAlly 참고) — "정확히 하나만"이
    // 구조적으로 강제된다.
    //
    // chance==0인 효과가 그룹 안에 있으면: 그 효과 자체는 절대 안 발동하고(당연히) 그룹을
    // "발동됨"으로 표시하지도 않는다 — 뒤 효과들에게 영향이 없다. 다만 앞 단계가 이미
    // 발동해서 건너뛰어진 경우엔 chance==0 여부와 무관하게 건너뛴다(순서가 여전히
    // 우선한다) — 그룹 맨 앞에 chance==0을 두면 그 효과는 사실상 죽은 자리가 된다(의도한
    // 설계라면 그렇게 두어도 안전하다, 다만 보통은 실수일 가능성이 높다).
    public int cascadeGroup;
}

// 스킬 레벨 하나. 특성강화(UnitTraitData)가 이 레벨을 올린다 — 원작이 `atp1` 표시 이름에
// `.lv2`/`.Lv3`를 붙여 레벨을 구분하는 것과 같은 구조(UPGRADE_SHOP.md 5차 ②).
[System.Serializable]
public class SkillLevel
{
    // CooldownAutoCast 전용. OnHitChance에선 절대쿨(발동 후 잠금 초) — 원작 AUfa 기반
    // 「N절대쿨」 버프 21종(2026-09-05, 리서치담당)이 그 자리다: 평타가 맞을 때마다 시전을
    // 시도하되(triggerChance는 그대로 굴러간다) 시전에 성공하면 이 값만큼 다시 못 쏜다.
    // 0이면 잠금 없음(기존 OnHitChance 동작 그대로). OnHitCount에선 아직 쓰지 않는다
    // (게이지+절대쿨 동시 사례가 있는지 리서치담당 확인 전 — check_required_fields.py #10
    // 참고).
    public float cooldown;
    // 발동확률(0~1), 기본 1f(=항상 발동). OnHitChance에선 평타 적중마다의 1차(유일) 판정.
    // ⚠️ 2026-09-06부터 OnHitCount에서도 쓴다 — 원작에 「게이지 AND 확률」 조합이 있다
    // (구현담당1 발견, PM 지시): 게이지가 임계에 닿아도 그걸로 끝이 아니라 이 값으로 2차
    // 확률 판정을 한 번 더 한다. UnitAttacker.TryCastOnHitSkill 참고 — 게이지 리셋은 이
    // 확률 판정과 별개 블록이라(원작도 그렇다) 확률에 실패해도 게이지는 리셋된다. 기존
    // OnHitCount 자산은 전부 기본값 1f라 이 판정이 항상 통과해 회귀가 없다.
    [Range(0f, 1f)] public float triggerChance = 1f;
    // 시전·오라 반경.
    public float range;

    // ⚠️ 맨 뒤에 추가 — 직렬화 순서를 지킨다.
    // OnHitCount 전용 — 카운터가 이 값에 닿으면 발동한다. 다른 발동방식이면 0(안 씀).
    // 0으로 두면 "닿을 수 없다"가 아니라 정반대로 매 타 발동한다(카운터가 1로 증가한 순간
    // 1>=0이 항상 참) — Tools/check_required_fields.py가 OnHitCount인데 이 값이 0인
    // 경우를 잡는다.
    //
    // ⚠️ 2026-09-05 정정: 카운터는 SkillData/UnitAttacker 인스턴스가 아니라 **유닛의
    // gaugeKind별 공유 카운터**(UnitAttacker.manaGaugeCounter/lifeGaugeCounter)다 — 같은
    // 유닛의 다른 스킬이 같은 gaugeKind를 쓰면 이 값도 그 공유 카운터와 비교된다(gaugeKind
    // 주석·MULTI_SKILL_IMPACT.md ⑤-B 참고). 임계값 자체는 스킬마다 달라도 된다.
    public int hitCountThreshold;

    // OnHitCount 전용 — 발동한 뒤 (공유) 카운터를 되돌릴 값. 원작이 정확히 0으로 돌리는지
    // 다른 값으로 돌리는지 리서치담당이 아직 일부(샹크스·쵸파 등)만 확인했다 — 지금은 0
    // 기본값으로 두고 필드로 빼둔다. 확인되면 이 값만 바꾸면 된다(코드는 안 건드림).
    public int resetTo;

    // OnHitCount 전용 — 이 카운터가 원작의 어느 공유 스탯을 대신하는지(SkillGaugeKind 주석
    // 참고). 다른 발동방식이면 안 쓴다. 기본값 Mana(0) — 지금 있는 OnHitCount 자산 4개는
    // 전부 스킬이 하나뿐인 유닛이라 어느 쪽이든 결과가 같다(공유할 다른 스킬이 없다).
    public SkillGaugeKind gaugeKind;

    // ⚠️ 2026-09-06 추가(버프 레지스트리, PM 지시) — 유닛이 지금 가진 버프를 게이트로
    // 쓴다. 원작 예: 드래곤 "버프 B00J 미보유", 루피 "버프 B06Y 미보유 AND 1/80". 원작
    // 버프 ID를 문자열로 그대로 적는다(예: "B00M") — 우리 코드가 그 뜻을 몰라도 데이터
    // 작성자가 같은 문자열만 맞추면 게이트가 성립한다. **둘 다 기본값 ""(빈 문자열) =
    // 조건 없음** — 기존 102개 게이트는 전부 비어있어 회귀 0이다
    // (UnitAttacker.PassesBuffGate 참고).
    public string requiredBuffId = "";   // 이 버프가 있어야만 판정을 계속한다("보유" 게이트).
    public string forbiddenBuffId = "";  // 이 버프가 있으면 판정을 막는다("미보유" 게이트).

    // ⚠️ 2026-09-06 추가 — OnHitChance 절대쿨(위 cooldown 필드)이 실제로 흉내내는 버프의
    // ID. 지금까지 절대쿨은 SkillRuntimeState.onHitChanceLockedUntil이라는 익명 타이머로만
    // 존재해서 **다른 스킬의 requiredBuffId/forbiddenBuffId가 그걸 가리킬 방법이 없었다**
    // (드래곤 "B00J 미보유"가 못 담기던 이유). 이 값을 채우면 절대쿨이 걸릴 때 같은 이름의
    // 버프가 버프 레지스트리에도 등록돼 다른 스킬이 조회할 수 있다. 기본값 ""(안 등록) —
    // 기존 게이트의 절대쿨 동작(state.onHitChanceLockedUntil 기반)은 이 필드와 무관하게
    // 그대로 돈다, 이 필드는 "그 잠금을 남에게도 보이게 할지"만 결정한다.
    public string selfBuffId = "";

    // 레벨마다 독립된 리스트다 — 통째로 갈아끼운다는 뜻이다. "레벨업 = 값이 커진다"로만
    // 설계했다면 레벨2에서 효과가 하나 더 늘어나는 원작 사례(중력장: Lv2에 25% 확률
    // 운석낙하가 새로 생김)를 못 담는다. 그래서 레벨2의 effects는 레벨1 effects를 고친 게
    // 아니라 그 레벨이 갖는 전체 효과 목록을 처음부터 다시 적는 것이다.
    public List<SkillEffect> effects = new List<SkillEffect>();

    // ⚠️ 맨 뒤에 추가(06번①, 2026-09-06, PM 승인) — requiredBuffId/forbiddenBuffId는
    // 캐스터 자신의 버프만 본다. 원작엔 **대상(적)의 버프**를 게이트로 쓰는 경우도 있다
    // (B06B "신세계 광폭화 몬스터 전용" — 대상이 그 상태일 때만 발동, 캐스터와 무관).
    // `EnemyDummy`엔 이미 독립된 버프 레지스트리와 `ApplyBuff` kind로 거는 경로가 있어서
    // (04번 오라 경로) 여기선 "읽는 쪽"만 추가한다 — `UnitAttacker.PassesBuffGate`가
    // 대상의 `EnemyDummy.HasBuff`를 조회한다(캐스터의 `HasBuff`와는 별개 레지스트리).
    // 원작 버프 ID 문자열을 그대로 적는다(캐스터 쪽과 이름공간이 같다 — 같은 문자열이면
    // 같은 버프라는 원작 전제 그대로).
    //
    // ⚠️ "대상 없음"일 때의 규칙 — **게이트 통과 안 함**(PM 지시). Aura·CooldownAutoCast는
    // 게이트를 검사하는 시점에 아직 대상을 안 골랐고(대상은 CastSkillLevel 안에서 나중에
    // 정해진다), Self 대상 스킬은 애초에 "적 대상"이 없다 — 이런 경로에서 이 필드가
    // 채워져 있으면 조건 없이 다 나가버리는 게 아니라 **항상 막혀야 안전하다**(반대로
    // "대상 없음=통과"로 두면 오라가 조건 없이 나간다). 둘 다 기본값 ""(빈 문자열) =
    // 조건 없음 — 기존 자산 전부 회귀 0이다.
    public string requiredTargetBuffId = "";   // 대상이 이 버프가 있어야만 판정을 계속한다.
    public string forbiddenTargetBuffId = "";  // 대상이 이 버프가 있으면 판정을 막는다.

    // ⚠️ 맨 뒤에 추가(2026-09-06, PM 지시) — OnHitCount 전용, "바닥 조건" 모드. 원작
    // 카타쿠리(B045) "1/7 AND LIFE > 36"은 hitCountThreshold의 "정확히 N타째"(도달 시
    // 자동으로 resetTo까지 되돌림)가 아니라 **"게이지가 N을 넘는 동안 계속" + 별도 확률
    // 판정**이다 — 카운터가 자동으로 리셋되지 않고, 넘긴 채로 있으면 매 타 triggerChance를
    // 굴린다. **0이면(기본값) 기존 hitCountThreshold 경로 그대로**(회귀 없음) — 1 이상이면
    // hitCountThreshold/resetTo 대신 이 값을 쓴다: 카운터가 이 값보다 **크면**(같으면 X)
    // triggerChance를 굴리고, 실패해도 카운터는 그대로 둔다(리셋 없음 — 아래
    // gaugeSpendAmount가 대신한다). 캐스터의 게이지 공유 규칙(gaugeKind)은 그대로다.
    public int hitCountFloor;

    // ⚠️ 맨 뒤에 추가(2026-09-06, PM 지시) — hitCountFloor와 짝. 원작 "발동 시 LIFE−17"처럼
    // **성공적으로 발동했을 때만** 게이지를 임의값만큼 깎는다 — hitCountThreshold의
    // resetTo(항상 정해진 값으로 되돌림, 확률과 무관하게 적용)와 다르다: 이건 (a) 확률
    // 판정까지 통과했을 때만 적용되고(원작 "발동 시"), (b) 고정값이 아니라 현재 카운터에서
    // 이 값만큼 뺀 값이다(0 아래로는 안 내려간다). **0이면(기본값) 아무것도 안 깎는다** —
    // 기존 자산 전부 0이라 회귀 없음. hitCountFloor==0(구식 경로)에서도 이 필드를 채우면
    // 똑같이 "발동 성공 시에만 차감"이 적용된다(리셋과는 별개로 추가 차감).
    //
    // ⚠️ [차감 시점 원작 확인 대기, 2026-09-06] — 위 triggerChance 주석은 "확률 판정과
    // 별개 블록이라(원작도 그렇다) 확률에 실패해도 게이지는 리셋된다"고 적혀 있다 — 기존
    // hitCountThreshold의 resetTo는 확률 실패에도 항상 적용된다는 뜻이다. 그런데 이
    // 필드는 방향이 반대로 "성공 시에만" 깎는다(B045/카타쿠리 조사 원문 "발동 시
    // LIFE−17"을 그렇게 읽었다). 둘 중 하나가 원작과 다를 수 있어 리서치담당 확인
    // 대기 중이다 — 답이 오기 전엔 이 동작을 바꾸지 말 것.
    public int gaugeSpendAmount;
}

[CreateAssetMenu(fileName = "NewSkillData", menuName = "GuilRandomDefense/Skill Data")]
public class SkillData : ScriptableObject
{
    public string skillName;
    [TextArea] public string description;

    public SkillTriggerType triggerType = SkillTriggerType.CooldownAutoCast;

    // [0] = 레벨1, [1] = 레벨2 ... 지금은 어디서도 레벨을 올리는 코드가 없어 전부 레벨1(index 0)만
    // 쓴다 — 특성 배선(06번)이 UnitUpgrades에서 실제 레벨을 읽어오면 그 자리를 바꾼다.
    public List<SkillLevel> levels = new List<SkillLevel>();
}
