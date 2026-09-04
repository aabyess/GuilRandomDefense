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

    // 도전 횟수가 늘수록 미니보스가 세진다(스모커). 원작은 능력 레벨(`A0NR`)로 올리는데
    // 그 레벨별 실제 HP 증가폭을 이번 조사로 못 구해서, 시도마다 배율을 곱하는 근사치로 대신한다.
    // ⚠️ 배율 자체가 창작이다 — 원작 수치가 아니다.
    public bool scalesWithAttempts;
    public float hpIncreasePerAttempt = 0.5f;

    [Header("발동 가능 라운드 — 0이면 제한 없음")]
    public int minRound;
    public int maxRound;

    [Header("성공 보상 — 판매(발동)한 플레이어에게 지급. 처치자가 아니다(원작 그대로)")]
    public int successGold;
    public List<EnemyResourceReward> successResources;
    public WispData successWisp;
    public int successWispCount;

    // 처치 성공 시 스토리 건물/보스에 추가로 주는 보너스 피해(원작 와포루: "스토리에 450만의
    // 마법데미지를 줍니다"). 방어력을 무시하는 마법(Spells 행)으로 들어간다 — StoryManager가
    // EnemyDummy.TakeDamage(DamageType.AP, AttackType.Spells)로 적용한다. 0이면 이 축이 없는 퀘스트.
    [Header("성공 시 스토리 보너스 피해 (원작에만 있는 퀘스트에서만 사용)")]
    public float storyDamage;

    [Header("실패 페널티 — 시간 안에 못 죽이면")]
    public int failWispBlockRounds = 2;
}
