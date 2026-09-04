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

    // 특성포인트 보상(피카: 목재1 + 특성포인트1). ⚠️ 지급 코드가 없다 — 일부러다.
    // UnitUpgrades 배선이 3중으로 끊겨 있어(`MISSING_PLAYER_POWER.md` §3: UnitData에 필드 없음 /
    // Unlock을 부르는 코드 없음 / Trait 에셋 effects 전부 빔) 지금 지급 코드를 넣어도 미도달이다.
    // 필드만 두고 PirateQuestManager.HandleSuccess는 이 값을 읽지 않는다 — 그 3중 배선이
    // 살아나면 그때 GrantTraitPoints 호출을 추가할 것.
    [Header("성공 시 특성포인트 (⚠️ 미배선 — 위 주석 참고)")]
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
