using System.Collections.Generic;
using UnityEngine;

// TODO(멀티플레이): 지급 로직은 반드시 서버 권위로 옮겨야 한다.
// 지금처럼 각 클라이언트가 이 메서드를 각자 실행하면(특히 rewardsAllPlayers=true인 물범류나
// GrantStoryReward의 전체 지급) 자원·위습이 접속자 수만큼 중복 지급된다.
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

    // 스토리 클리어 보상: 전체 플레이어에게 골드 + 자원 + 위습 지급.
    // 보스 최다 데미지·도전과제 보상은 기여도 추적 시스템이 없어 아직 만들지 않았다(별도 작업).
    public void GrantStoryReward(StoryRewardData storyReward)
    {
        if (storyReward == null) return;

        foreach (PlayerContext context in PlayerContext.All)
        {
            if (storyReward.goldReward > 0 && context.GoldWallet != null)
            {
                context.GoldWallet.Add(storyReward.goldReward);
            }

            if (storyReward.resourceRewards != null && context.ResourceWallet != null)
            {
                foreach (EnemyResourceReward reward in storyReward.resourceRewards)
                {
                    context.ResourceWallet.Add(reward.type, reward.amount);
                }
            }

            GrantWisps(context, storyReward.wispRewards);
        }
    }

    public void GrantWisps(PlayerContext context, List<WispReward> wispRewards)
    {
        if (context == null || wispRewards == null) return;

        foreach (WispReward reward in wispRewards)
        {
            if (reward == null || reward.wisp == null) continue;
            SpawnWisp(context, reward.wisp, Mathf.Max(1, reward.count));
        }
    }

    void SpawnWisp(PlayerContext context, WispData wispData, int count)
    {
        if (wispData.prefab == null)
        {
            Debug.LogWarning($"RewardDistributor: {wispData.wispName} WispData에 prefab이 없어 위습을 생성하지 못했습니다.");
            return;
        }

        for (int i = 0; i < count; i++)
        {
            GameObject instance = Instantiate(wispData.prefab, context.transform.position, Quaternion.identity);

            if (!instance.TryGetComponent(out Wisp wisp))
            {
                wisp = instance.AddComponent<Wisp>();
            }
            wisp.SetData(wispData);

            if (!instance.TryGetComponent(out OwnedByPlayer owner))
            {
                owner = instance.AddComponent<OwnedByPlayer>();
            }
            owner.SetOwner(context.PlayerId);
        }
    }
}
