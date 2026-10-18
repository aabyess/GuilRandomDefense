using UnityEngine;

/// <summary>S2 미션판·개인 퀘스트 보상 위습 참조(Resources/S2/S2Rewards). 씬을 안 건드리려고 Resources 에셋으로 둔다.</summary>
[CreateAssetMenu(menuName = "GRD/S2 Reward Table")]
public class S2RewardTable : ScriptableObject
{
    public WispData randomWisp;    // e0IX 랜덤위습
    public WispData choiceWisp;    // e018 흔함선택위습
    public WispData uncommonWisp;  // e017 안흔함위습
    public WispData rareWisp;      // e019 희귀함위습(S2 「b5」)

    static S2RewardTable cached;
    public static S2RewardTable Get() => cached != null ? cached : (cached = Resources.Load<S2RewardTable>("S2/S2Rewards"));
}
