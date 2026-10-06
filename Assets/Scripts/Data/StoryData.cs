using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스토리 하나.
/// 진행은 라운드가 아니라 <b>연쇄</b>다 — 앞 스토리를 깨면 다음 스토리가 바로 나온다.
/// 예외는 8번(사이버넷) 다음의 대기 구간뿐이다.
/// 보상은 전체 플레이어에게 지급된다.
/// </summary>
[CreateAssetMenu(fileName = "NewStoryData", menuName = "GuilRandomDefense/Story Data")]
public class StoryData : ScriptableObject
{
    public string storyName;
    public int order;

    [Header("등장 조건")]
    [Tooltip("앞 스토리를 깬 뒤 이만큼 기다렸다 나타난다. 0이면 곧바로")]
    public float delayAfterPreviousSeconds;

    [Tooltip("기다리는 동안 표시할 이름 (예: 백수생활). 대기가 없으면 비워 둔다")]
    public string interludeName;

    [Tooltip("신·악몽에서만 나온다(원작 13번 와노쿠니 kingnokuni). 다른 난이도에선 건너뛴다")]
    public bool godNightmareOnly;

    [Header("제한시간 — 원작 13번: 285초 안에 못 깨면 전원 패배")]
    [Tooltip("0이면 제한 없음")]
    public float timeLimitSeconds;

    [Header("건물 → 보스")]
    [Tooltip("스토리존에 서 있는 건물. 비어 있으면 이 스토리는 아직 생성할 수 없다")]
    public EnemyData building;

    [Tooltip("변신 후의 보스. 비우면 건물 데이터를 그대로 쓴다")]
    public EnemyData boss;

    [Header("클리어 보상 — 전체 플레이어에게 각각 지급")]
    public int goldReward;
    public List<EnemyResourceReward> resourceRewards;
    public List<WispReward> wispRewards;

    [Header("조기 클리어 보너스 — 원작 도전과제(클리어 시점 라운드가 이 값 미만이면 추가 지급, 생존자만)")]
    [Tooltip("0이면 없음. 원작 udg_Level < N (스토리2: 9 · 스토리10: 30)")]
    public int earlyClearBeforeRound;
    public List<EnemyResourceReward> earlyResourceRewards;
    public List<WispReward> earlyWispRewards;
    [Tooltip("그 플레이어에게만 띄우는 원작 문구")]
    public string earlyClearMessage;

    [Header("확률 아이템 드랍 — 원작 Trig_Story_reward4~9, 생존자별 독립 굴림(EnemyItemDrop: 가중치·문구·보유 시 건너뜀)")]
    [Range(0f, 1f)] public float itemDropChance;
    public List<EnemyItemDrop> itemDrops;

    // ⚠️ 맨 뒤에 추가(10-06 사장님 「스토리 가반 뒤돌고 있음 — 앞을 보게」) — 스토리 건물·보스를 놓을 때 Y축으로 돌리는 각도(도).
    //   모델마다 정면이 달라서 스토리 데이터 한 칸으로 맞춘다(모델 배선을 다시 돌리지 않아도 된다).
    public float facingYaw;

    /// <summary>변신 후에 쓸 데이터. 보스가 따로 없으면 건물 것을 그대로 쓴다.</summary>
    public EnemyData BossOrBuilding => boss != null ? boss : building;

    /// <summary>건물이 정해져야 실제로 내보낼 수 있다.</summary>
    public bool IsPlayable => building != null;
}
