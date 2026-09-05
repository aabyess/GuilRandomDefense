using System.Collections.Generic;
using UnityEngine;

// 해적단류 퀘스트 하나의 설정(와포루·스모커 등). war3map.j의 Trig_Quest_* 트리거 하나에 대응한다.
// "수치는 에셋에, 코드는 공식만" — 흐름은 PirateQuestManager, 수치는 여기.
[CreateAssetMenu(fileName = "NewPirateQuest", menuName = "GuilRandomDefense/Pirate Quest")]
public class PirateQuestData : ScriptableObject
{
    public string questName;

    [Header("판매 대상 — 이 유닛을 UnitSellPortal에 넣으면 발동")]
    public UnitData sellUnit;

    [Header("미니보스 — EnemyDummy를 그대로 재사용한다(SetLane(-1))")]
    public EnemyData miniboss;
    public float timerSeconds = 60f;

    // 도전 횟수가 늘수록 미니보스가 세진다(스모커·해적단, 둘 다 원작 능력 `A0NR`/`Ilif` 계열로
    // 올라간다). 레벨별 실제 HP 증가폭을 이번 조사로 못 구해서, 시도마다 배율을 곱하는 근사치로
    // 대신한다. ⚠️ 배율 자체가 창작이다 — 원작 수치가 아니다(해적단의 `Ilif`는 부호가 툴팁과
    // 반대로 나와 리서치담당이 무리하게 숫자로 바꾸지 않기로 하고 스모커와 같은 근사치를 그대로 썼다).
    //
    // 🔴 미도달(2026-09-05, 전 구간 손 추적으로 발견). 토큰이 시작에 1회만 지급되고 재획득
    // 경로가 어디에도 없다(전수 grep — sellUnit을 참조하는 곳은 GrantStartingTokens 하나뿐).
    // 그래서 PirateQuestManager.NextAttempt는 모든 플레이어에게 항상 1을 돌려주고, 이 배율은
    // 실전에서 `1 + (1-1)*hpIncreasePerAttempt = 1.0`으로 고정돼 절대 안 움직인다. 원작은
    // 상점 재고가 시간이 지나면 보충되는 구조(`AddUnitToStockBJ`)라 재도전이 설계의 일부다 —
    // 스모커 툴팁 "도전 횟수가 증가할수록 체력이 증가합니다"가 그 증거. **필드는 지우지 않는다
    // — 죽은 게 아니라 살릴 예정인 기능이다.** 재고 보충 간격이 확인되면(리서치담당 조사 중)
    // 토큰 재획득 경로부터 만들 것 — 그전엔 이 배율을 만질 이유가 없다.
    public bool scalesWithAttempts;
    public float hpIncreasePerAttempt = 0.5f;

    // ⚠️ 원작 판매 가능 라운드는 툴팁이 아니라 "판매 유닛을 들고 있는 상점 건물"의
    // 생성/제거 트리거로 정해진다 — 둘이 어긋난다. 와포루 툴팁은 "21~30라운드까지만"이라
    // 적지만 토큰은 게임 시작부터 있고 31라운드에만 제거된다(리서치담당, 2026-09-05) —
    // 실제로는 1~30. 바제스는 Level==40에 제거되어 1~39. 해적단·거프·모리아·피카·스모커
    // 다섯은 전부 같은 상점 건물(`h07A`, 맵 유닛명 "도전과제-퇴치")의 「판매 유닛」 정적 필드에
    // 박혀 있어서(스크립트가 아니라 오브젝트 데이터라 트리거 검색으론 안 잡혔다) 5개가 같은
    // 생애주기를 공유한다 — 게임 시작에 4레인 각각 생성, `Trig_Round_10ver_Actions`
    // 스테이지11(60라운드-신세계 진입, 신세계 대기 화면이 뜨는 바로 그 지점)에서 4개 전부
    // KillUnit → 1~59. **셋(30/40/59)이 서로 다른 마감선을 갖는 게 원작이다 — 통일하지 말 것.**
    [Header("발동 가능 라운드 — 0이면 제한 없음")]
    public int minRound;
    public int maxRound;

    [Header("성공 보상 — 판매(발동)한 플레이어에게 지급. 처치자가 아니다(원작 그대로)")]
    public int successGold;
    public List<EnemyResourceReward> successResources;
    public WispData successWisp;
    public int successWispCount;

    // 특성포인트 보상(피카: 목재1 + 특성포인트1, 사장님 확정 2026-09-05 07번 — 4갈래 중 넷째).
    // ⚠️ 코드 경로는 이어졌지만(`PirateQuestManager.HandleSuccess`가 0보다 크면
    // `UnitUpgrades.GrantPirateQuestPoint()`를 부른다) **지금 당장은 도달하지 않는다** —
    // `PlayerContext.unitUpgrades`가 씬에서 여전히 null이다(`MapGenerator`가 그 컴포넌트를
    // 안 붙인다, `WIRING_AUDIT.md` §1). 그리고 §1이 풀려도 **받은 포인트를 쓸 상점이 아직
    // 없다**(`UnitTraitData.costTraitPoints`를 읽어 `Unlock()`을 부르는 코드 0건). 값 자체는
    // 항상 1로 취급된다 — `GrantPirateQuestPoint()`가 1회 한정 bool 플래그라 필드의 정확한
    // 수치는 안 읽는다.
    [Header("성공 시 특성포인트 (배선은 됐으나 §1·상점 둘 다 막혀 미도달 — 위 주석 참고)")]
    public int successTraitPoints;

    // 처치 성공 시 스토리 건물/보스에 추가로 주는 보너스 피해. 방어력을 무시하는 마법(Spells 행)으로
    // 들어간다 — StoryManager가 EnemyDummy.TakeDamage(DamageType.AP, AttackType.Spells)로 적용한다.
    //
    // ⚠️ 와포루 툴팁은 "스토리에 450만의 마법데미지를 줍니다"라고 약속하지만, `war3map.j`의
    // `Trig_Quest_waporu_Actions` 성공 분기(Stage=1)를 끝까지 읽어도 피해 호출이 없다(리서치담당,
    // 2026-09-05). 450만이라는 상수 자체는 다른 스킬 트리거 5곳에 있으나 이 콜백과 안 이어져 있다 —
    // **원작이 툴팁으로만 약속하고 실제로는 배선하지 않은 것**이다. 그래서 0이 원작과 같은 상태다.
    // 이 필드가 죽은 게 아니라 원작 재현이 정확히 0이라는 뜻 — 값을 채우기 전에 반드시 그 퀘스트의
    // 원작 트리거에서 실제 피해 호출을 확인할 것. 지어내지 말 것.
    [Header("성공 시 스토리 보너스 피해 (원작 트리거에 실제로 있을 때만 채운다)")]
    public float storyDamage;

    [Header("실패 페널티 — 시간 안에 못 죽이면")]
    public int failWispBlockRounds = 2;
}
