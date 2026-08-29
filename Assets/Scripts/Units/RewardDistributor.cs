using UnityEngine;

// TODO(멀티플레이): 지급 로직은 반드시 서버 권위로 옮겨야 한다.
// 지금처럼 각 클라이언트가 이 메서드를 각자 실행하면(특히 rewardsAllPlayers=true인 물범류)
// 자원이 접속자 수만큼 중복 지급된다.
public class RewardDistributor : MonoBehaviour
{
    public static RewardDistributor Instance { get; private set; }

    void OnEnable()
    {
        Instance = this;
    }

    void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void GrantKillReward(EnemyData data, int killerPlayerId)
    {
        if (data == null) return;

        if (data.rewardsAllPlayers)
        {
            foreach (PlayerContext context in PlayerContext.All)
            {
                GrantTo(context, data);
            }
        }
        else
        {
            PlayerContext context = PlayerContext.Get(killerPlayerId);
            if (context != null)
            {
                GrantTo(context, data);
            }
        }
    }

    void GrantTo(PlayerContext context, EnemyData data)
    {
        if (data.goldReward > 0 && context.GoldWallet != null)
        {
            context.GoldWallet.Add(data.goldReward);
        }

        if (data.resourceRewards == null || context.ResourceWallet == null) return;

        foreach (EnemyResourceReward reward in data.resourceRewards)
        {
            context.ResourceWallet.Add(reward.type, reward.amount);
        }
    }
}
