using UnityEngine;

// "강화소 3"(원작) — 공격타입×등급 업그레이드. 우리가 이미 만든 "강화소"(등급트랙,
// UnitUpgradeTrackData, 원작 "강화소 1")와 이름은 같지만 원작에서 다른 건물이다
// (리서치담당 war3map.w3q 원문 전수, ATTACKTYPE_UPGRADE_RAW_DUMP.md 2026-09-06 —
// 강화소 1=R00F,R008,R019,R002,R00E,R003,R004,R000 / 강화소(번호없음)=캐릭터 전용
// 고유보너스 5종 / **강화소 3=R00G,R00H,R00I,R01V**, 이번 트랙이 대응하는 것).
//
// 원작 구조: 마스터 버튼(공격타입당 하나, R00G 일반·R00H 공성·R00I 관통·R01V 패기) 4개만
// 플레이어가 직접 산다. 사면 트리거가 하위 자식 3개(버킷별 고정값)를 그 즉시 공짜로
// 부여한다 — 등급트랙(R000 등)이 부속 8개를 공짜로 부여하는 것과 같은 패턴이라, 우리도
// 자식을 별도 데이터로 안 담는다(그 값을 쓰는 스킬은 자기 자신의 multiplier/bonus를
// 이미 갖고 있어 자식의 절대값을 또 읽으면 이중 계상이다 — 필요한 건 "몇 번 샀는가"
// 뿐이다). 마스터 자체도 `glvl=3`이라 최대 3레벨까지 반복 구매가 가능한 것으로 보인다
// (자식이 "count+1"로 누적되는 트리거 구조와 일치) — 다만 이 레벨 자체가 자식의
// 절대값에 곱해지는지, 매 구매가 그냥 "1회성 완전 해금"인지는 원문에서 확정 못 했다
// [미확인] — 우리는 `SkillEffectBasis.ResearchLevel` 소비 스킬들이 이미 자기
// multiplier×level+bonus 공식을 갖고 있으므로 "레벨 그대로"만 필요해 안전하다.
//
// 공속 증가율(gba1=gmo1)은 2026-09-07 연결 완료 — 아래 speedPercentPerLevel 필드
// 주석 참고(UnitUpgrades.SpeedMultiplierForAttackType → UnitAttacker.AttackSpeedMultiplier).
[CreateAssetMenu(fileName = "NewAttackTypeUpgradeTrackData", menuName = "GuilRandomDefense/Attack Type Upgrade Track Data")]
public class AttackTypeUpgradeTrackData : ScriptableObject
{
    public string trackName;
    [TextArea] public string description;

    // 이 트랙이 담당하는 공격타입 하나(마스터 버튼 하나 = 공격타입 하나, 등급트랙의
    // targetGrades 리스트와 달리 여기는 항상 단일값이다 — 원작 마스터 버튼 4개가
    // 정확히 4개 공격타입에 1:1 대응한다).
    public AttackType attackType;

    public int maxLevel = 3;

    // 비용 — 2026-09-25 정정(war3map.w3q 직접 디코드): R00G/R00H/R00I/R01V 모두
    // gglb 3000 · gglm 500 · glmb 1 · glmm 1. 워크3 연구 비용은 「기본 + 증가×(지금 레벨)」이라
    // 0→1 3000엔·목재1, 1→2 3500엔·목재2, 2→3 4000엔·목재3이다.
    // ⚠️ 09-06엔 gglm(골드 증가 500)을 목재로 읽어 「매 레벨 목재 500」이 들어가 있었다 — 사실상 못 샀다.
    public int costGold = 3000;
    public int costGoldPerLevel = 500;
    public int costWood = 1;
    public int costWoodPerLevel = 1;

    // 공속 증가율(gba1=gmo1) — 일반·공성·관통 3%, 패기 4%. 2026-09-07 연결 완료(PM 지시,
    // "필드만·아직 없다"류 뼈대 구멍 전수 점검) — UnitUpgrades.SpeedMultiplierForAttackType이
    // 이 값을 읽고 UnitAttacker.AttackSpeedMultiplier가 곱한다. "레벨당 곱해 누적되는지
    // 1회성 완전해금인지"는 원문 미확인(위 costGold 옆 주석 참고)이라, 다른 ResearchLevel
    // 소비 스킬들과 같은 원칙("레벨 그대로"만 넘기고 배율은 스킬 쪽 multiplier×level 공식이
    // 이미 담당)에 맞춰 여기서도 **레벨에 선형 비례**(1+speedPercentPerLevel×level)로
    // 다룬다 — 레벨 0은 자연히 배수 1(무영향).
    public float speedPercentPerLevel;

    // level = 지금 레벨(0부터). 다음 레벨로 올리는 값이다.
    public int CostForLevel(int level) => Mathf.Max(0, costGold + costGoldPerLevel * Mathf.Max(0, level));
    public int WoodCostForLevel(int level) => Mathf.Max(0, costWood + costWoodPerLevel * Mathf.Max(0, level));

    // 공격력 가산 — 2026-09-25 신설(war3map.w3q 자식 업그레이드 디코드). 마스터를 사면 트리거가
    // 자식 셋(등급 묶음별)을 같은 레벨로 올리고, 자식은 `ratx`(공격력 +gba2, 레벨당 +gmo2)다:
    //   일반 R01K/L/M · 공성 R01O/P/N · 관통 R01S/R/Q = 1500 · 2000 · 3000 (레벨당 같은 값)
    //   패기 R01W/U/T = 2000 · 3000 · 4000
    // 어느 유닛이 어느 자식을 받는지는 원작 w3u `upgr` 배정 분포로 묶었다:
    //   전설·히든(칭호형)·랜덤전용 → 첫째 묶음 / 제한됨·특수함·변화된 → 둘째 / 초월·불멸·영원 → 셋째.
    //   흔함~희귀함은 어느 자식에도 없다(0).
    // 예전엔 자식 절대값을 「스킬이 ResearchLevel로 이미 가진다」며 안 옮겼는데, 그건 스킬 쪽
    // 얘기고 평타 공격력 가산(ratx)은 어디에도 없었다.
    public int attackBonusTier1 = 1500;
    public int attackBonusTier2 = 2000;
    public int attackBonusTier3 = 3000;

    public float AttackBonusForLevel(UnitGrade grade, int level)
    {
        if (level <= 0) return 0f;
        switch (grade)
        {
            case UnitGrade.Legendary:
            case UnitGrade.Hidden:
            case UnitGrade.RandomUnit:
            case UnitGrade.OtherWorld:
                return attackBonusTier1 * level;
            case UnitGrade.Limited:
            case UnitGrade.Superior:
            case UnitGrade.Transformed:
                return attackBonusTier2 * level;
            case UnitGrade.Transcendent:
            case UnitGrade.Immortal:
            case UnitGrade.Eternal:
                return attackBonusTier3 * level;
            default:
                return 0f;
        }
    }

    // 2026-09-07 신설(PM 지시) — 등급트랙 SpeedMultiplierForLevel과 같은 자리·같은 관례.
    public float SpeedMultiplierForLevel(int level) => 1f + speedPercentPerLevel * level;
}
