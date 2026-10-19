using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 상점 건물 클릭 판정 키우기(사장님 09-29 「건물 클릭하는 히트박스 조금만 더 키워줄래」).
// 기본 카메라에서 건물이 작아 누르기 어려웠다. 맵 재생성 없이 판이 열릴 때 각 상점의 BoxCollider를 키운다:
// 가로 최대 1.5배(옆 건물과의 간격 48%를 넘지 않게). 10-14: 높이·앞뒤는 키우지 않는다 — 앞에 선 흔함 유닛을 상자가 가렸다(높이는 오히려 절반쯤으로, 바닥 고정).
static class ShopClickBoxes
{
    const float HeightKeep = 0.4f;   // 상자 높이를 이 비율로(바닥 고정)

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Hook()
    {
        Apply();
        SceneManager.sceneLoaded += (_, __) => Apply();
    }

    static void Apply()
    {
        foreach (MonoBehaviour b in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (!(b is ILaneShop) || !b.TryGetComponent(out BoxCollider box)) continue;
            // 건물 몸통 실제 메시 경계로 좁힌다(사장님 10-14): 상자가 건물보다 넉넉해 앞에 선 유닛을 가렸다.
            // 몸통 = 형제 오브젝트 「<상점 이름>_모양」의 렌더러 합. 높이는 몸통의 HeightKeep배(바닥 고정). 모양을 못 찾으면 기존 상자의 높이만 줄인다.
            Transform t = box.transform;
            Transform look = t.parent != null ? t.parent.Find(t.name + "_모양") : null;
            bool any = false; Bounds w = default;
            if (look != null)
                foreach (Renderer r in look.GetComponentsInChildren<Renderer>())
                {
                    if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                    if (r.name.Contains("_add")) continue;   // 빛 겹은 몸통이 아니다(책 제단 빛이 상자를 가로 302로 키웠다)   // 굴뚝 연기·불 파티클은 건물 몸통이 아니다(상자가 1800으로 부풀었다)
                    if (any) w.Encapsulate(r.bounds); else { w = r.bounds; any = true; }
                }
            if (any)
            {
                float height = w.size.y * HeightKeep;
                Vector3 worldCenter = new Vector3(w.center.x, w.min.y + height * 0.5f, w.center.z);
                Vector3 ls = t.lossyScale;
                box.center = t.InverseTransformPoint(worldCenter);
                const float MaxWidth = 100f;   // 슬롯 간격(≈110) 이하
                box.size = new Vector3(Mathf.Min(w.size.x, MaxWidth) / Mathf.Abs(ls.x), height / Mathf.Abs(ls.y), Mathf.Min(w.size.z, MaxWidth) / Mathf.Abs(ls.z));
            }
            else
            {
                Vector3 size = box.size, center = box.center;
                float oldY = size.y;
                size.y *= HeightKeep;
                center.y += (size.y - oldY) * 0.5f;
                box.size = size; box.center = center;
            }
        }
    }
}
