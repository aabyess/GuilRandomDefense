using UnityEngine;

// 원작 비비 A0LZ류 "자가시전 영구 강화" 능력의 설정값. SkillEffect가 아니다 — 대상이 없고
// 피해도 없는 유닛 자신의 액션이라 SkillData의 kind/target 모델에 안 맞는다(A0LZ_CASTER_
// STACK_INVESTIGATION.md/7703d2c 참고). 성공률·자원비용을 코드에 상수로 박지 않고 여기
// 자산 필드로 뺀다 — 원작에 이미 200/300처럼 여러 문턱이 실재해서 다음 캐릭터가 다른
// 값을 쓸 수 있다(뿌리: 오늘 하루 "상수로 박았다가 다른 값이 실재해서 못 담은" 사고가
// 여러 번 났다).
[CreateAssetMenu(fileName = "NewSelfUpgradeAbilityData", menuName = "GuilRandomDefense/Self Upgrade Ability Data")]
public class SelfUpgradeAbilityData : ScriptableObject
{
    // 원작 alev와 같은 뜻 — 이 레벨에 도달하면 성공확률 공식과 무관하게 더 안 오른다.
    // A0LZ는 성공확률 공식 자체가 레벨10에서 자연히 0%가 되지만(90-9*10=0), 식이 나중에
    // 바뀌어도 상한이 남아 있도록 별도로 잘라둔다(PM 지시 — "식이 바뀌면 상한이 사라진다").
    public int maxLevel = 10;

    // 성공확률(%) = baseChancePercent - 현재레벨 × chancePerLevelPercent. A0LZ 원작값:
    // 90 · 9(레벨0=90%, 레벨9=9%, 레벨10=0%).
    public float baseChancePercent = 90f;
    public float chancePerLevelPercent = 9f;

    // 시도할 때마다(성공/실패 무관) 나가는 자원. A0LZ 원작값: 목재2 + 랜덤위습(e0IX) 3기.
    public int woodCost = 2;
    // ⚠️ 2026-09-07 정정(PM 지시, "필드만·아직 없다" 뼈대 구멍 점검) — e0IX 매핑 자체는
    // 풀렸다: `Wisp_흔함.asset`(isPlayerChoice=0·targetGrade=흔함, 2026-09-07 신설, A09G
    // 흔함 판매 누적형이 이미 이 자산을 쓴다)이 정확히 이 자리다. **다만 이 클래스의
    // 자산 인스턴스 자체가 아직 하나도 없다**(A0LZ가 어느 로스터 유닛에 배정됐는지도
    // 미정) — wispCurrency를 채우기 전에 먼저 SelfUpgradeAbilityData 자산 생성 + 대상
    // 유닛 배정이 필요하다(둘 다 이번 점검 범위 밖, 콘텐츠 배정 문제). 그때까지 비어
    // 있으면 TryUpgradeSelf가 안전하게 실패한다(아래 참고), 조용히 무제한 통과하지 않는다.
    public WispData wispCurrency;
    public int wispCost = 3;
}
