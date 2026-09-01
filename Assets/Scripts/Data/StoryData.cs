using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스토리 하나. 이름·순서는 확정됐고 마감 라운드·적·보상은 아직 미정이다.
/// 보상은 전체 플레이어에게 지급된다(사용자 확정).
/// </summary>
[CreateAssetMenu(fileName = "NewStoryData", menuName = "GuilRandomDefense/Story Data")]
public class StoryData : ScriptableObject
{
    public string storyName;
    public int order;

    [Header("진행 조건 — 0이면 아직 정해지지 않음")]
    [Tooltip("이 라운드까지 클리어하지 못하면 전원 패배")]
    public int deadlineRound;

    [Tooltip("스토리존에 나타나는 적. 비어 있으면 이 스토리는 아직 생성되지 않는다")]
    public EnemyData enemy;

    [Header("클리어 보상 — 전체 플레이어에게 각각 지급")]
    public int goldReward;
    public List<EnemyResourceReward> resourceRewards;
    public List<WispReward> wispRewards;

    /// <summary>적과 마감 라운드가 정해져야 실제로 진행할 수 있다.</summary>
    public bool IsPlayable => enemy != null && deadlineRound > 0;
}
