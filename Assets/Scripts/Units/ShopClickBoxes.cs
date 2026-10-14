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
            Renderer[] rs = look != null ? look.GetComponentsInChildren<Renderer>() : null;
            if (rs != null && rs.Length > 0)
            {
                Bounds w = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++) w.Encapsulate(rs[i].bounds);
                float height = w.size.y * HeightKeep;
                Vector3 worldCenter = new Vector3(w.center.x, w.min.y + height * 0.5f, w.center.z);
                Vector3 ls = t.lossyScale;
                box.center = t.InverseTransformPoint(worldCenter);
                box.size = new Vector3(w.size.x / Mathf.Abs(ls.x), height / Mathf.Abs(ls.y), w.size.z / Mathf.Abs(ls.z));
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
