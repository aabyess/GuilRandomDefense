using System.Collections.Generic;
using UnityEngine;

// 강화소(유닛/다른세계/영원함 공용) 트랙 1개 = 등급 묶음 1개를 강화하는 버튼 1칸.
// 유닛강화소는 이 에셋 9개(흔함&안흔함 묶음 포함, 히든 포함), 다른세계·영원함 강화소는
// 각각 1개만 물린다(구현담당2와 합의 — UnitUpgradeShop.cs 참고).
//
// ⚠️ 2026-09-05, 사장님 결정(05번 "원작 연구소로 만들어라")으로 수치가 원작 실값으로
// 교체됐다. 그전엔 리서치담당의 [제안](Docs/reference/UPGRADE_SHOP.md "2차 조사")을 옮긴
// 가제였다("등급 전체 강화" 시스템 자체가 원작에 없다고 알려져 있었기 때문) — 그 판단은
// 리서치담당이 `.w3q`(연구소 원본 파일)를 직접 파싱해내면서 뒤집혔다. 원작은 등급별로
// 정확히 이 모양(레벨당 공격력 배율 + 레벨당 골드)의 연구소를 갖고 있었다.
//
// ⚠️ 비용·배율 공식이 **둘 다 지수식에서 선형식으로 바뀌었다**(2026-09-05). 원작은
// "레벨1 값 + 레벨당 증분×(레벨-1)"이고, 옛 코드는 "밑^레벨"이었다 — 두 곡선은 근본적으로
// 다른 함수라 필드 값만 바꿔서는 재현할 수 없었다(예: 초월함을 지수식에 끼우면 레벨21에서
// 2.15^21로 폭주한다, 원작은 3.35다). 그래서 `statLevel1Multiplier` 필드를 새로 추가했다 —
// "새 스키마를 만들지 말라"는 지시는 병렬 시스템을 새로 짓지 말라는 뜻이었고(PM 확인,
// 2026-09-05), 기존 타입에 필드 하나 더하는 건 여기 해당하지 않는다.
[CreateAssetMenu(fileName = "NewUnitUpgradeTrackData", menuName = "GuilRandomDefense/Unit Upgrade Track Data")]
public class UnitUpgradeTrackData : ScriptableObject
{
    public string trackName;
    [TextArea] public string description;

    // 이 트랙이 강화하는 등급들. 흔함&안흔함처럼 여러 등급을 한 트랙에 묶을 수 있다.
    public List<UnitGrade> targetGrades = new List<UnitGrade>();

    public int maxLevel = 10;

    // 비용 — 원작 실측(리서치담당, `.w3q` `gglb`/`gglm` 필드, 2026-09-05): **레벨1(레벨0→1)만
    // costBase를 내고, 그 뒤(레벨1→2, 2→3, ...)는 전부 costGrowthPerLevel로 동일하다(증가하지
    // 않는 정액).** "레벨당 골드"라는 이름과 달리 지수/선형 증가가 아니라 딱 두 단계 요금이다.
    // 예전엔 costGrowthPerLevel이 "배수"(costBase×growth^레벨)였다 — 필드 이름은 그대로 두고
    // 의미만 바뀌었다(스키마 필드 수를 안 늘리려고).
    public int costBase = 100;                // 레벨1 비용(레벨0→1 전용), 단위는 엔
    public float costGrowthPerLevel = 1.5f;   // 레벨2 이후 매 레벨 정액 비용(레벨1 비용과 무관)

    // ⚠️ 초월함·불멸·랜덤유닛의 costBase는 근사치다(리서치담당, 2026-09-05) — 원작 맵 파일에
    // 그 셋의 레벨1 기본비용 필드(`gglb`)가 아예 없다(엔진 내장 스톡값을 그대로 상속하는
    // 경우라 맵 파일 파싱만으로는 못 뽑는다). 나머지 확정 셋(전설70·히든60·제한90)의 범위
    // 안에서 초월·불멸=75, 랜덤유닛=50으로 잡았다 — 지어낸 숫자라는 걸 여기 남긴다. 정확한
    // 값이 필요해지면 워크3 스톡 업그레이드 비용표를 별도로 대조해야 한다.

    // 공격력 배율 = 레벨<=0이면 1(강화 안 한 상태) · 레벨>=1이면
    // statLevel1Multiplier + statGrowthPerLevel × (레벨−1). 원작이 이 선형식이다 —
    // "레벨1 배수"가 등급마다 달라서(예: 희귀함 0.70, 초월함 2.15) 이 필드가 따로 필요했다.
    public float statLevel1Multiplier = 1f;
    public float statGrowthPerLevel = 1.1f;   // 레벨당 공격력 배율 증분(가산, 더 이상 밑이 아니다)

    // ⚠️ 랜덤유닛의 statLevel1Multiplier도 근사치다 — 원작 필드값이 0으로 읽혔는데, 다른
    // 트랙(특별함 등)에서 필드가 비면 엔진 스톡 기본값(1.0)을 상속하는 패턴이 확인돼서
    // 0이 아니라 1.0으로 해석했다(리서치담당, 2026-09-05). 레벨당 증분(+0.14)만 확정값이다.

    public Color slotColor = Color.white;

    // 원작 연구소 8종(특별함·희귀함·히든·제한됨·전설적인·불멸·초월함·랜덤전용) 중 하나에
    // 대응하면 true. 흔함·안흔함(묶음)·다른세계·영원함은 원작에 대응하는 연구소가 없다 —
    // 지우지 않고 이 플래그로 잠근다(사장님 결정 05번, 2026-09-05: "사장님이 나중에 '이
    // 등급도 강화하고 싶다'고 하실 때 정보가 사라지면 안 된다"). false인 트랙은
    // `ResearchLabImplemented`가 true여도 계속 잠긴 채로 남는다 — UnitUpgradeShop 참고.
    public bool hasOriginalResearch = true;

    public int CostForLevel(int level) =>
        level <= 0 ? Mathf.Max(0, costBase) : Mathf.Max(0, Mathf.RoundToInt(costGrowthPerLevel));

    public float MultiplierForLevel(int level) =>
        level <= 0 ? 1f : statLevel1Multiplier + statGrowthPerLevel * (level - 1);
}
