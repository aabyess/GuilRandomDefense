using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 상점 건물 클릭 판정 키우기(사장님 09-29 「건물 클릭하는 히트박스 조금만 더 키워줄래」).
// 기본 카메라에서 건물이 작아 누르기 어려웠다. 맵 재생성 없이 판이 열릴 때 각 상점의 BoxCollider를 키운다:
// 가로 최대 1.5배(옆 건물과의 간격 48%를 넘지 않게). 10-14: 높이·앞뒤는 키우지 않는다 — 앞에 선 흔함 유닛을 상자가 가렸다(높이는 오히려 절반쯤으로, 바닥 고정).
static class ShopClickBoxes
{
    const float MaxWiden = 1.5f;
    const float HeightKeep = 0.55f;   // 상자 높이를 이 비율로(바닥 고정)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        Apply();
        SceneManager.sceneLoaded += (_, __) => Apply();
    }

    static void Apply()
    {
        List<BoxCollider> boxes = new List<BoxCollider>();
        foreach (MonoBehaviour b in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            if (b is ILaneShop && b.TryGetComponent(out BoxCollider box) && !boxes.Contains(box)) boxes.Add(box);

        foreach (BoxCollider box in boxes)
        {
            Bounds world = box.bounds;
            // 가장 가까운 이웃 상점까지의 수평 거리 — 그 절반 조금 못 미치게까지만 넓힌다.
            float nearest = float.MaxValue;
            foreach (BoxCollider other in boxes)
            {
                if (other == box) continue;
                Vector3 d = other.bounds.center - world.center; d.y = 0f;
                nearest = Mathf.Min(nearest, d.magnitude);
            }
            float halfWidth = Mathf.Max(world.extents.x, world.extents.z);
            float widen = nearest < float.MaxValue && halfWidth > 0f
                ? Mathf.Clamp(nearest * 0.48f / halfWidth, 1f, MaxWiden)
                : MaxWiden;

            Vector3 size = box.size;
            Vector3 center = box.center;
            float oldY = size.y;
            size.x *= widen; size.y *= HeightKeep;   // 앞뒤(z)는 안 넓힌다 — 카메라 쪽으로 튀어나온 상자가 유닛을 가렸다
            center.y += (size.y - oldY) * 0.5f;   // 바닥 고정
            box.size = size;
            box.center = center;
        }
    }
}
