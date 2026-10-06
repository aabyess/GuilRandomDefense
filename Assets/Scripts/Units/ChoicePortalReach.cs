using UnityEngine;
using UnityEngine.SceneManagement;

// 흔함 선택 포탈 판정 키우기(사장님 10-06 「선택위습 공간이 조금 좁음」). 맵 재생성 없이 판이 열릴 때 캡슐 반지름만 키운다(ShopClickBoxes와 같은 방식).
// 보이는 원판은 그대로(지름 27.1 → 반지름 13.5), 판정만 18 — 이웃 포탈 간격 72.6의 안(36 < 72.6)이라 서로 겹치지 않는다.
// ⚠️ 캡슐은 높이 150이라 구가 아니다(MapGenerator.CreatePortalObject 주석). 반지름은 지름 배율(x·z 큰 쪽)에 곱해지므로 로컬 값으로 환산한다.
static class ChoicePortalReach
{
    const float WorldRadius = 18f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        Apply();
        SceneManager.sceneLoaded += (_, __) => Apply();
    }

    static void Apply()
    {
        foreach (UnitPortal portal in Object.FindObjectsByType<UnitPortal>(FindObjectsSortMode.None))
        {
            if (!portal.name.StartsWith("흔함선택_") || !portal.TryGetComponent(out CapsuleCollider capsule)) continue;
            Vector3 scale = portal.transform.lossyScale;
            float horizontal = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            if (horizontal <= 0f) continue;
            float local = WorldRadius / horizontal;
            if (capsule.radius < local) capsule.radius = local;   // 키우기만 — 이미 크면(다시 불렸거나 생성기가 바뀜) 그대로
        }
    }
}
