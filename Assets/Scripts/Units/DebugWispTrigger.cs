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
        // 2026-09-25 베타 빌드: 친구 테스트판에서 G로 위습을 공짜로 받으면 안 된다 — 에디터·개발 빌드에서만.
        if (!Application.isEditor && !Debug.isDebugBuild) return;
        if (Keyboard.current == null || !Keyboard.current.gKey.wasPressedThisFrame) return;
        // MP: 멀티 클라에서 누르면 호스트 모르게 진짜 위습이 생긴다(GrantWisps에 가드가 없다) — 디버그 키는 호스트만.
        if (!GameAuthority.IsServer) return;

        RewardDistributor distributor = rewardDistributor != null ? rewardDistributor : RewardDistributor.Instance;
        PlayerContext localContext = PlayerContext.Local;

        if (distributor == null || testWisp == null || localContext == null) return;

        distributor.GrantWisps(localContext, new List<WispReward> { new WispReward { wisp = testWisp, count = 1 } });
    }
}
