using System.Collections.Generic;
using UnityEngine;

// 리서치담당 3차 조사(Docs/reference/UPGRADE_SHOP.md "3차 조사") 표본 22종에서 뽑은 11개 유형.
// 표본이 로스터의 9%뿐이라 여기서 끝났다고 보지 말 것 — 새 유형이 나오면 여기 추가한다.
//
// 지금 전투에 반영되는 건 DamageIncrease·ArmorShred·SlowOnHit 셋이다(UnitAttacker 참고).
// ArmorShred는 2026-09-03 방어력 시스템이 들어오면서 살아났다 — EnemyDummy.AddArmorShred로 쌓인다.
// SlowOnHit는 2026-09-07 EnemyDummy.AddMoveSpeedShred(MoveSpeedFloor=220 하한)로 살아났다.
// 나머지는 데이터 자리만 있고 아직 아무 시스템도 안 읽는다 — Summon/MechanismChange/
// UtilityBuff/CastMethodChange는 유닛 전용 코드(Tier B)가 필요하다.
public enum TraitEffectKind
{
    DamageIncrease,        // 딜증가 — 표본에서 가장 흔함(22종 중 10). 유일하게 지금 반영됨.
    SlowOnHit,              // 이감부여 — EnemyDummy.AddMoveSpeedShred로 쌓인다(2026-09-07)
    Summon,                 // 소환(서브유닛) — Tier B 전용
    MovementAbilityGrant,   // 이동능력부여(공중이동 등)
    StatusAilment,          // 상태이상(스턴 등)
    ArmorShred,             // 방깎 — EnemyDummy.EffectiveArmor를 깎는다. 하한 -20
    DamageTypeChange,       // 판정변경(물뎀→마뎀, 고정뎀 전환 등)
    MechanismChange,        // 메커니즘변경(소환수 제한 변경 등) — Tier B
    UtilityBuff,            // 버프(공속·자원회복 등, 대개 조건부 트리거) — Tier B
    AreaDamage,             // 범위피해
    AccuracyIncrease,       // 명중률/집탄율상승 — 명중 시스템 자체가 없음
    CastMethodChange,       // 시전방식변경(캐스트→즉발 등) — Tier B

    // 마방깍 — EnemyDummy.EffectiveMagicMultiplier를 올린다(마법 피해를 더 받게 한다).
    // ⚠️ **맨 뒤에 붙일 것.** 중간에 끼우면 이미 직렬화된 kind 값이 전부 한 칸씩 밀려
    // 다른 효과로 읽힌다. (2026-09-04에 실제로 ArmorShred 뒤에 끼웠다가 되돌렸다 —
    // 마침 effects가 전부 비어 있어 피해는 없었지만, 값이 있었으면 조용히 틀렸을 것이다.)
    MagicArmorShred,
}

[System.Serializable]
public class TraitEffect
{
    public TraitEffectKind kind;

    // 의미는 kind마다 다르다. DamageIncrease는 배율 가산치(0.1 = 공격력 +10%) — UnitAttacker가
    // 이렇게 읽는다. 그 외 kind는 아직 아무도 안 읽으므로 단위를 여기서 확정하지 않는다.
    public float value;
}

// 특성강화 1건 = 유닛 1종. 원작 사례 대부분 효과를 1~3개 동시에 받는다
// (조로 = 딜증가 + 방깎 + 이감, 한 유닛이 kind 하나만 갖는 게 아니다).
[CreateAssetMenu(fileName = "NewUnitTraitData", menuName = "GuilRandomDefense/Unit Trait Data")]
public class UnitTraitData : ScriptableObject
{
    public UnitData targetUnit;
    public string traitName;
    [TextArea] public string description;

    // 원작 맵(war3map.w3a) 직접 확인 결과 1~3개, 최빈값 2개다(2026-09-04, UPGRADE_SHOP.md 5차
    // 조사) — "대부분 4개"였던 이전 조사는 정정됐다.
    public int costTraitPoints = 2;

    public List<TraitEffect> effects = new List<TraitEffect>();

    // Tier B — 범용 표로 못 담는 유닛 전용 로직(키자루 분신 개수, 브룩 9타 트리거 등).
    // 비어있으면 없음. 그 유닛이 실제로 콘텐츠에 들어갈 때 이 키를 보고 코드를 짠다.
    public string specialEffectId;

    // ⚠️ 2026-09-05(06번①): 위 effects(TraitEffectKind)는 "스탯을 얼마나 올리는가"고,
    // 이건 완전히 다른 축이다 — 원작 특성강화는 흔함~초월함엔 하나도 없고 불멸·영원 등급
    // 26종에만 있다(`Docs/reference/ORIGINAL_TRAIT_BRANCHES.md`, war3map.j 전수). 처음엔
    // "26개 전부 스킬 레벨 1→2"로 알려졌었는데 **틀렸다** — 실제로는 26개가 네 종류로
    // 갈린다: 스킬승급 15 · 능력교체 8(아래 replacementSkill) · 변신 2(아직 미착수) ·
    // 순수스탯 1(아직 미착수). 이 필드는 그중 스킬승급 15개 전용이다(예: 후지토라
    // !중력장 A0GR — 레벨2는 레벨1 수치 그대로 + "1/4 확률 운석낙하"가 새로 생긴다.
    // 배율 상승이 아니다). SkillLevel.effects가 레벨마다 독립 리스트인 이유가 정확히 이거다.
    //
    // 0(기본)이면 스킬승급형이 아니다. 1 이상이면 targetUnit.skill(SkillData)의
    // levels[skillLevelUnlockIndex]를 쓰라는 뜻이고, UnitUpgrades.Unlock으로 이 트레잇이
    // 풀리면 UnitAttacker.CurrentSkillLevel이 그 인덱스를 읽는다
    // (UnitUpgrades.SkillLevelIndexFor 참고). targetUnit.skill이 null이면 아무 효과도
    // 없다 — 이 값만으론 스킬을 만들어내지 못한다.
    //
    // 원작 26명은 우리 로스터에 이름으로 없다(이름 매핑 불가 원칙) — 등급별 순위 배정으로
    // 우리 유닛에 옮긴다. 어느 원작 유닛 자리인지는 이 asset 파일명이나 위 description에
    // 원작 유닛ID·능력ID(예: `H08X`/`A0GR`)를 남길 것 — 사장님이 실제 콘텐츠를 배정할 때의 연결점이다.
    public int skillLevelUnlockIndex;

    // 능력교체형(위 26분기 중 8개) 전용 — 레벨을 올리는 게 아니라 능력 자체를 통째로
    // 갈아끼운다(원작: `UnitRemoveAbilityBJ`(구) + `UnitAddAbilityBJ`(신)). 이 값이 있으면
    // 이 트레잇을 언락했을 때 UnitAttacker.Skill이 targetUnit.skill 대신 이 SkillData를
    // 통째로 쓴다(UnitUpgrades.ReplacementSkillFor 참고). skillLevelUnlockIndex와는
    // 한 트레잇에 동시에 안 쓴다 — 원작 26분기 중 한 유닛이 스킬승급과 능력교체를 같이
    // 갖는 사례가 없다.
    public SkillData replacementSkill;

    // 06번③(변신, 26분기 중 2개) 대상 유닛. 실행 메커니즘은 GameHud.ExecuteTransform이다
    // (2026-09-05 완성 — CombineSystem.TryCombine의 "재료 Consume() 후 UnitSpawner로 결과
    // 생성" 패턴을 자기 자신 하나만 소모하는 형태로 재사용했다, 그 주석에 무엇을 잇고
    // 무엇을 버리는지 적어뒀다).
    //
    // 원작 변신 목적지(H097→H0B1 아오키지, H091→H093 쵸파)가 우리 로스터의 어떤 유닛이 될지는
    // 아직 안 정했다(사장님 배정 대기) — 이 필드를 null로 비워두고 원작 유닛ID만
    // description에 적어 억지로 짝짓지 않는다. **null인 동안은 메커니즘이 있어도 절대 안
    // 불린다** — GameHud가 구매 자체를 막는다(아래 isTransformType 코멘트 참고).
    public UnitData transformIntoUnit;

    // ⚠️ transformIntoUnit==null만으로는 "변신형인데 목적지가 아직 없다"와 "애초에 변신형이
    // 아니다"를 못 가른다 — 그래서 따로 뒀다. GameHud의 특성강화 버튼이 `isTransformType &&
    // transformIntoUnit == null`이면 "변신 대상 미정"으로 구매 자체를 막는다(2026-09-05
    // 사장님 지시로 조건이 바뀌었다 — 예전엔 "메커니즘이 없어서" 막았지만 이제 메커니즘은
    // 있고 "대상이 없어서" 막힌 것이다). 언락은 HashSet.Add라 되돌릴 수 없어서, 대상이
    // 정해지기 전엔 포인트를 아예 못 쓰게 막는 게 맞다 — 다른 23개(effects가 비어도 구조는
    // 다 이어져 값이 오면 바로 동작)와 이 둘만 다르다. transformIntoUnit이 채워지면 이
    // 필드는 그대로 두고 그 값만 넣으면 된다 — 코드를 더 지울 것 없다.
    public bool isTransformType;

    // 06번⑥(순수스탯형, 26분기 중 유일하게 1개 — 타시기 전용). 스킬 레벨업이 전혀 없고
    // 경험치+스탯만 오른다(원작: AddHeroXPSwapped(5000) + STR/AGI/INT 각 +3). 0(기본)이면
    // 순수스탯형이 아니라는 뜻 — 스킬승급형·능력교체형·변신형과 동시에 안 쓴다(26분기
    // 중 한 유닛이 이 넷을 겹쳐 갖는 사례가 없다). 실행은 GameHud.OnTraitButtonClicked가
    // 구매 시 선택된 그 유닛 인스턴스(UnitAttacker.AddHeroXp/AddPurchasedStat)에 즉시
    // 적용한다 — effects(TraitEffectKind)와 달리 "언락 상태를 매 프레임 읽는" 지속 효과가
    // 아니라 ExecuteTransform과 같은 "구매 시 1회 실행" 패턴이다.
    //
    // ⚠️ 5000은 "레벨"이 아니라 "경험치 포인트"다(UnitAttacker.AddHeroXp가 누적 경험치에
    // 그대로 더하는 값과 같은 단위) — 레벨로 착각하지 말 것.
    public int heroXpGrant;

    // STR/AGI/INT 세 스탯 각각에 이만큼(원작 +3). heroXpGrant와 항상 같이 채워진다(원작에
    // 경험치만 주고 스탯은 안 주는, 또는 그 반대인 사례가 없다) — 그래도 필드는 독립으로
    // 둔다(둘의 의미가 다르므로 하나로 묶으면 "0이 무슨 뜻인지"가 모호해진다).
    public int purchasedStatGrantEach;

    // 06번⑤(반복구매형, 26분기 중 유일하게 1개 — 아카이누 전용). 다른 25개는 "언락=끝"이라
    // UnitUpgrades.unlockedTraits(HashSet)의 1회잠금이 정확히 맞는데, 이 하나만 원작이
    // 몇 번이든 다시 살 수 있다(UserData=구매횟수 카운터, TRAIT_UPGRADE_26_HEROES_FULL.md).
    // ⚠️ HashSet은 안 건드린다 — 그 하나 때문에 나머지 25개의 "재구매 불가" 동작을 바꾸면
    // 안 된다(PM 지시). 대신 UnitUpgrades.repeatablePurchaseCounts(별도 저장소)가 구매
    // 횟수를 센다 — false(기본)면 이 카운터 자체가 안 쓰인다(회귀 없음).
    //
    // skillLevelUnlockIndex는 "1회차 구매가 도달하는 인덱스"고, 그 뒤로는 구매할 때마다
    // +1씩 더 간다(UnitUpgrades.SkillLevelIndexFor 참고) — 원작 "능력 레벨 = 구매 횟수,
    // 반복 횟수(hitCount)=5+레벨"과 같은 모양이다. 인덱스가 SkillData.levels 범위를
    // 넘으면 UnitAttacker.CurrentSkillLevel이 이미 마지막 레벨로 clamp한다(안전).
    //
    // ⚠️ 구매 횟수 상한 없음 — 원작 원문에 상한 비교가 없다("몇 번이든"). 상한을 넣는 건
    // 지어내는 것이라 넣지 않았다.
    public bool isRepeatablePurchase;

    // 우솝(G.O.D, H09B) 전용 — 26명 중 유일하게 targetUnit이 아니라 "전체 플레이어 공용
    // 건물"(도움소)에 거는 특성이다(2026-09-07, 뿌리 ㊽). 언락 순간이 캐스터/targetUnit
    // 자신에게는 원작에도 아무 효과가 없다(위 skillLevelUnlockIndex/replacementSkill 등
    // 기존 축과 동시에 켜질 일이 없다) — GameHud.OnTraitButtonClicked이 이 플래그를 보고
    // UsoppDockhouseTrait.Activate()를 부른다. false(기본값)면 기존 25개는 완전히 무영향.
    public bool triggersUsoppDockhouseBoost;

    // 로빈(H098) 전용 — 26명 중 유일하게 대상이 targetUnit(구매자 자신)이 아니라 플레이어이
    // 다음 클릭으로 직접 찍는 다른 유닛이다(원작 `Trig_T_Ability_hero_Actions`,
    // `GetSpellTargetUnit()` 확인, `Tools/w3x/원본/war3map_new.j`, 2026-09-07). true면
    // GameHud.OnTraitButtonClicked가 즉시 적용하지 않고 대상 지정 모드로 들어간다
    // (RefreshTraitTargeting, WorldPick.TryHit 재사용 — 새 인프라 안 만듦).
    //
    // 원작 확인 내용:
    // ⓐ 대상 선택 — 플레이어가 고른다(WC3 표준 대상지정 스킬 캐스트, 자동 아님).
    // ⓑ 부여물 둘 — `A0FL`("로빈의 손날개", 실제 게임플레이 능력: 영구 블링크, 사거리
    //    1500·쿨다운 10초) + `A0ZP`(같은 이름, base=AItc 부착물 능력 — 순수 시각효과,
    //    가슴에 `wing-robin3.mdx` 부착, 스탯·효과 없음).
    // ⓒ "영구화"(`UnitMakeAbilityPermanent`) — 부여된 능력이 제거되지 않는다는 뜻일 뿐,
    //    재구매·반복과는 무관하다. 구매 자체는 다른 25개와 같은 1회성(HashSet 잠금).
    // ⓓ 대상 제한 — 없음. 로빈의 실제 구매 능력(`A0Q2`, `Arsg`가 아니라 `AHtb` 기반,
    //    툴팁 "어떠한 유닛이든") 확인 결과 `atar`='air,invulnerable,organic,ground'로
    //    거의 모든 유닛 카테고리를 허용한다 — 소유주 제한도 못 찾았다. 우리도 안 건다.
    //
    // 🔴 [미확정으로 남긴 것] `A0FL`의 실제 블링크 효과는 배선하지 않았다 — 우리 엔진에
    // "플레이어가 지점을 클릭해 순간이동" 로직 자체가 없다(`UnitData.MovementAbility.
    // Teleport`가 이미 "필드만, 로직은 나중에 구현"으로 못박혀 있다 — 새 기반시설이 필요한
    // 사안이라 이 작업 범위 밖으로 판단했다). 대신 대상에게 `UnitIdentity.
    // hasRobinWingBlessing` 표시만 남긴다(구매·소비·대상지정은 전부 실제로 동작) —
    // 텔레포트 로직이 생기면 그 표시를 읽어 켜면 된다. `A0ZP`(순수 시각효과)는 우리에
    // 부착물 시스템이 없어 대응 없이 생략 — 지어낼 게 없는 자리다(값 자체가 없다).
    public bool targetsOtherUnit;
}
