using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 상점 건물 클릭 판정 키우기(사장님 09-29 「건물 클릭하는 히트박스 조금만 더 키워줄래」).
// 기본 카메라에서 건물이 작아 누르기 어려웠다. 맵 재생성 없이 판이 열릴 때 각 상점의 BoxCollider를 키운다:
// 가로 최대 1.5배(옆 건물과의 간격 48%를 넘지 않게) · 높이 1.3배(바닥은 그대로, 위로만).
static class ShopClickBoxes
{
    const float MaxWiden = 1.5f;
    const float Taller = 1.3f;

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
            size.x *= widen; size.z *= widen; size.y *= Taller;
            center.y += (size.y - oldY) * 0.5f;   // 바닥 고정, 위로만 늘린다
            box.size = size;
            box.center = center;
        }
    }
}
