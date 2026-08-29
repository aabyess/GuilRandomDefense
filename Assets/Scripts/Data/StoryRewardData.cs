using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WispReward
{
    public WispData wisp;
    public int count = 1;
}

[CreateAssetMenu(fileName = "NewStoryRewardData", menuName = "GuilRandomDefense/Story Reward Data")]
public class StoryRewardData : ScriptableObject
{
    public string storyName;
    public int goldReward;
    public List<EnemyResourceReward> resourceRewards;
    public List<WispReward> wispRewards;
}
