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

    [Header("건물 → 보스")]
    [Tooltip("스토리존에 서 있는 건물. 비어 있으면 이 스토리는 아직 생성할 수 없다")]
    public EnemyData building;

    [Tooltip("변신 후의 보스. 비우면 건물 데이터를 그대로 쓴다")]
    public EnemyData boss;

    [Header("클리어 보상 — 전체 플레이어에게 각각 지급")]
    public int goldReward;
    public List<EnemyResourceReward> resourceRewards;
    public List<WispReward> wispRewards;

    /// <summary>변신 후에 쓸 데이터. 보스가 따로 없으면 건물 것을 그대로 쓴다.</summary>
    public EnemyData BossOrBuilding => boss != null ? boss : building;

    /// <summary>건물이 정해져야 실제로 내보낼 수 있다.</summary>
    public bool IsPlayable => building != null;
}
