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

    // ⚠️ 2026-09-06 정정(구현담당2 발견, PM 확인) — 이 필드는 이름과 달리 "공격력 배율"이
    // 아니라 **원작 공속 증가율(`gba1`/`gmo1`)**이다. 6개 트랙(불멸·전설적인·제한됨·히든·
    // 초월함·희귀함) 전부에서 이 값이 리서치담당이 별도로 뽑은 공속 표(초월함 +215%/+6%,
    // 불멸 +210%/+6% 등)와 정확히 일치했다 — 진짜 공격력 가산치는 아래 statLevel1Bonus
    // (gba2/gmo2)가 이미 맞게 들고 있다. 필드 이름은 직렬화 키라 못 바꾼다(바꾸면 이미
    // 채워진 값이 전부 날아간다, PM 지시) — 이름은 낡았지만 값은 공속으로 취급한다.
    // 원작 뜻이 "레벨1부터 +210%"(증가율)라 배수가 아니다 — 실제 배수는
    // 1 + statLevel1Multiplier + statGrowthPerLevel×(레벨−1)이어야 한다(SpeedMultiplierForLevel
    // 참고). 레벨 0(강화 안 한 상태)은 증가율 0 → 배수 1로 원작과 같다.
    public float statLevel1Multiplier = 1f;
    public float statGrowthPerLevel = 1.1f;   // 레벨당 공속 증가율 증분(가산)

    // ⚠️ 랜덤유닛의 statLevel1Multiplier도 근사치다 — 원작 필드값이 0으로 읽혔는데, 다른
    // 트랙(특별함 등)에서 필드가 비면 엔진 스톡 기본값(1.0)을 상속하는 패턴이 확인돼서
    // 0이 아니라 1.0으로 해석했다(리서치담당, 2026-09-05). 레벨당 증분(+0.14)만 확정값이다.
    // ⚠️ 위 "1.0 상속" 해석은 이제 공속 축으로 다시 봐야 한다 — 랜덤전용·특별함은 리서치담당
    // 공속 표에서도 공속 기본이 0(원작 실값, 결측 아님)이라 이 필드도 0이 맞다. 아래
    // SpeedMultiplierForLevel은 statLevel1Multiplier를 "1.0을 상속한 배수"가 아니라 "0인
    // 증가율"로 다룬다 — 랜덤유닛 트랙만 이 필드가 여전히 0/+0.14로 남아 있다면 재확인 필요.

    public Color slotColor = Color.white;

    // 원작 연구소 8종(특별함·희귀함·히든·제한됨·전설적인·불멸·초월함·랜덤전용) 중 하나에
    // 대응하면 true. 흔함·안흔함(묶음)·다른세계·영원함은 원작에 대응하는 연구소가 없다 —
    // 지우지 않고 이 플래그로 잠근다(사장님 결정 05번, 2026-09-05: "사장님이 나중에 '이
    // 등급도 강화하고 싶다'고 하실 때 정보가 사라지면 안 된다"). false인 트랙은
    // `ResearchLabImplemented`가 true여도 계속 잠긴 채로 남는다 — UnitUpgradeShop 참고.
    public bool hasOriginalResearch = true;

    // 절대 가산치(`gba2`/`gmo2`, 리서치담당 2026-09-05 확정) — 진짜 공격력(power) 데이터다.
    // 위 statLevel1Multiplier(공속 증가율)와 **완전히 별개 필드**로 원작에 저장돼 있다.
    // 전설적인·히든·불멸·초월함·제한됨 5개만 이 값이 있다(나머지 특별함·희귀함·랜덤전용은
    // 0 — 필드 기본값 그대로 두면 된다). UnitAttacker.AttackDamage에 그대로 더해진다(공속
    // 배율과는 안 곱해진다). 선형식(레벨1값 + 레벨당증분×(레벨−1))이다 — 레벨 0은 가산 없음(0).
    public float statLevel1Bonus;
    public float statBonusGrowthPerLevel;

    public int CostForLevel(int level) =>
        level <= 0 ? Mathf.Max(0, costBase) : Mathf.Max(0, Mathf.RoundToInt(costGrowthPerLevel));

    // 2026-09-06 정정 — 예전 이름 MultiplierForLevel, "공격력 배율"로 잘못 쓰였다(구현담당2
    // 발견, PM 확인). 메서드는 직렬화되지 않으니 이름을 바로 고쳤다. statLevel1Multiplier가
    // 원작 "증가율"(+210% 등)이라 실제 배수는 1을 더해야 한다 — 레벨 0은 증가율 0이라
    // 자연히 배수 1(무영향)이 된다.
    public float SpeedMultiplierForLevel(int level) =>
        level <= 0 ? 1f : 1f + statLevel1Multiplier + statGrowthPerLevel * (level - 1);

    public float BonusForLevel(int level) =>
        level <= 0 ? 0f : statLevel1Bonus + statBonusGrowthPerLevel * (level - 1);
}
