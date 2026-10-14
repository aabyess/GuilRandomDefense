using System.Text;
using UnityEngine;

/// <summary>
/// 레인 상점 건물 앞 유닛 클릭 점검(10-14 사장님 제보) — 플레이 중 gameshot call:ShopClickProbe.Test.
/// 상점마다: ① 클릭 상자 안쪽 앞(카메라 쪽)에 흔함 유닛을 세우고 그 몸 한가운데로 광선 → 유닛이 맞아야 ② 유닛 없이 건물 몸통(바닥 가까이 가운데)으로 광선 → 건물이 맞아야.
/// WorldPick.TryHit은 실제 클릭이 쓰는 함수다.
/// </summary>
static class ShopClickProbe
{
    static string Test()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        Camera cam = Camera.main;
        if (cam == null) return "❌ 메인 카메라 없음";
        UnitData data = null;
        foreach (UnitData u in Resources.FindObjectsOfTypeAll<UnitData>())
            if (u != null && u.prefab != null && u.name.StartsWith("흔함_") && !u.name.Contains("강재규")) { data = u; break; }
        if (data == null) return "❌ 흔함 유닛 데이터 없음";

        var sb = new StringBuilder();
        int ok = 0, total = 0;
        foreach (MonoBehaviour b in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (!(b is ILaneShop) || !b.TryGetComponent(out BoxCollider box)) continue;
            Bounds sb2 = box.bounds;
            Vector3 toCam = cam.transform.position - sb2.center; toCam.y = 0f; toCam.Normalize();

            // ② 건물 몸통: 상자 바닥 가까이 가운데 — 카메라에서 그 점으로
            Vector3 bodyPoint = sb2.center + Vector3.down * sb2.extents.y * 0.5f;
            total++;
            bool shopHit = WorldPick.TryHit(cam, cam.WorldToScreenPoint(bodyPoint), out RaycastHit h2) && h2.collider == box;
            if (shopHit) ok++;

            // ① 상자 앞쪽 안 — 상자 가장자리 안쪽 70% 지점에 유닛
            Vector3 front = sb2.center + toCam * Mathf.Min(sb2.extents.x, sb2.extents.z) * 0.7f;
            front.y = sb2.min.y;
            GameObject unit = Object.Instantiate(data.prefab, front, Quaternion.identity);
            Physics.SyncTransforms();
            bool unitHit = false; string hitName = "없음";
            if (unit.TryGetComponent(out Collider uc) || (uc = unit.GetComponentInChildren<Collider>()) != null)
            {
                Vector3 aim = uc.bounds.center;
                if (WorldPick.TryHit(cam, cam.WorldToScreenPoint(aim), out RaycastHit h1))
                {
                    hitName = h1.collider.name;
                    unitHit = h1.collider.transform.IsChildOf(unit.transform);
                }
            }
            if (unitHit) ok++;
            total++;
            sb.AppendLine($"{b.name}: 유닛 클릭 → {(unitHit ? "유닛 ✅" : "❌ " + hitName)} · 건물 몸통 클릭 → {(shopHit ? "건물 ✅" : "❌ " + (h2.collider != null ? h2.collider.name : "없음"))} · 상자 {sb2.size.x:F0}×{sb2.size.y:F0}×{sb2.size.z:F0}");
            Object.Destroy(unit);
        }
        return $"{ok}/{total} 통과\n{sb}";
    }
}
