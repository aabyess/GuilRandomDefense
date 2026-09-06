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
// ⚠️ 공속 증가율(gba1=gmo1, "레벨1값=레벨당증분" 관례 — 등급트랙과 같은 선형식)은
// 이번 작업 범위 밖이다(PM 지시, `SkillEffectBasis.ResearchLevel` 연결만). 필드는
// 미리 담아두되(다음에 붙일 때 새로 만들지 않도록) 아직 아무 코드도 안 읽는다.
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

    // 비용 — 마스터 버튼 4개 전부 골드3000+목재500, 매 레벨(구매 시도) 동일하다
    // (등급트랙처럼 "레벨1만 다르다"가 아니다 — war3map.w3q gglb/gglm 원문 확인,
    // ATTACKTYPE_UPGRADE_RAW_DUMP.md ④). "강화소 3" 건물 자체의 건설 비용은
    // [미확인](원문에서 못 찾음, 리서치담당).
    public int costGold = 3000;
    public int costWood = 500;

    // 공속 증가율(gba1=gmo1) — 일반·공성·관통 3%, 패기 4%. 이번 작업(ResearchLevel
    // 연결)은 이 값을 안 쓴다 — 나중에 AttackSpeedMultiplier에 붙일 때를 위해 필드만
    // 미리 둔다(직렬화 추가, PM 지시).
    public float speedPercentPerLevel;

    public int CostForLevel(int level) => Mathf.Max(0, costGold);
    public int WoodCostForLevel(int level) => Mathf.Max(0, costWood);
}
