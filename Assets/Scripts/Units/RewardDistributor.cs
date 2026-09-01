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
        if (!GameAuthority.IsServer) return;
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
        // 비어 있는 플레이어 슬롯에도 물범 목재 등이 쌓이는 걸 막는다 (rewardsAllPlayers 전체 지급 경로).
        if (context == null || !context.IsOccupied) return;

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
    public void GrantStoryReward(StoryData storyReward)
    {
        if (!GameAuthority.IsServer) return;
        if (storyReward == null) return;

        foreach (PlayerContext context in PlayerContext.All)
        {
            // 처치 보상과 같은 이유로 빈 슬롯은 건너뛴다 — 아무도 없는 자리에 자원이 쌓인다.
            if (context == null || !context.IsOccupied) continue;

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

        // 위습은 자기 등급 칸 안에서 생긴다. 칸은 막혀 있어 다른 등급 포탈로 새지 않는다.
        // 칸이 아직 없는 등급이면 플레이어 위치에 떨어뜨린다.
        WispCell cell = WispCell.Get(wispData.targetGrade);
        Vector3 origin = cell != null ? cell.transform.position : context.transform.position;

        for (int i = 0; i < count; i++)
        {
            // 여러 개를 같은 점에 겹쳐 놓으면 서로 밀어내느라 흩어진다. 살짝 벌려 놓는다.
            Vector3 offset = count > 1
                ? Quaternion.Euler(0f, 360f / count * i, 0f) * Vector3.forward * 1.6f
                : Vector3.zero;

            GameObject instance = Instantiate(wispData.prefab, origin + offset, Quaternion.identity);

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
