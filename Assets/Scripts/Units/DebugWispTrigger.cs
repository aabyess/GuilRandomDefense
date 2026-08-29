using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 임시 디버그용 트리거: G키로 위습 1개 지급. 정식 UI(포탈 진입 등)가 붙으면 제거 예정.
public class DebugWispTrigger : MonoBehaviour
{
    [SerializeField] RewardDistributor rewardDistributor;
    [SerializeField] WispData testWisp;

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.gKey.wasPressedThisFrame) return;

        RewardDistributor distributor = rewardDistributor != null ? rewardDistributor : RewardDistributor.Instance;
        PlayerContext localContext = PlayerContext.Local;

        if (distributor == null || testWisp == null || localContext == null) return;

        distributor.GrantWisps(localContext, new List<WispReward> { new WispReward { wisp = testWisp, count = 1 } });
    }
}
