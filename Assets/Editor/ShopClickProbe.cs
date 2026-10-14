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
            Vector3 bodyPoint = new Vector3(sb2.center.x, sb2.min.y + sb2.size.y * 0.3f, sb2.center.z);
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
            Object.Destroy(unit);

            // 흔함 칸 안 유닛(사장님 10-14 재제보): 상점 상자 밖 — 앞쪽 왼·가운데·오른쪽, 옆쪽 왼쪽 — 을 눌러도 건물이 아니라 유닛이어야 한다.
            string cells = "";
            Vector3 e = sb2.extents;
            Vector3 right = Vector3.Cross(Vector3.up, toCam).normalized;
            foreach ((string label, Vector3 offset) in new[] {
                ("앞왼", toCam * e.z * 1.5f - right * e.x * 0.8f), ("앞가운데", toCam * e.z * 1.5f), ("앞오른", toCam * e.z * 1.5f + right * e.x * 0.8f),
                ("옆왼", -right * e.x * 1.2f + toCam * e.z * 0.3f) })
            {
                Vector3 pos = sb2.center + offset; pos.y = sb2.min.y;
                GameObject cellUnit = Object.Instantiate(data.prefab, pos, Quaternion.identity);
                Physics.SyncTransforms();
                Collider cc = cellUnit.GetComponentInChildren<Collider>();
                bool cellHit = false;
                if (cc != null)
                {
                    // 실제 클릭 경로(SelectionManager.TrySelectAtCursor)와 같게: TryHit → 상점이면 커서 둘레 유닛 우선. 가운데와 몸 옆 18px 두 점 모두 유닛이어야 한다.
                    cellHit = true;
                    foreach (Vector2 shift in new[] { Vector2.zero, new Vector2(-18f, 0f), new Vector2(18f, 0f) })
                    {
                        Vector2 click = (Vector2)cam.WorldToScreenPoint(cc.bounds.center) + shift;
                        Selectable picked = null;
                        if (WorldPick.TryHit(cam, click, out RaycastHit hc))
                        {
                            hc.collider.TryGetComponent(out picked);
                            if (picked != null && picked.TryGetComponent(out ILaneShop _)) { Selectable near = WorldPick.NearestUnitOnScreen(cam, click, 48f); if (near != null) picked = near; }
                        }
                        if (picked == null || !picked.transform.IsChildOf(cellUnit.transform)) cellHit = false;
                    }
                }
                total++; if (cellHit) ok++;
                cells += $" {label}{(cellHit ? "✅" : "❌")}";
                Object.Destroy(cellUnit);
            }
            sb.AppendLine($"  └ 칸 안 유닛:{cells}");
            sb.AppendLine($"{b.name}: 유닛 클릭 → {(unitHit ? "유닛 ✅" : "❌ " + hitName)} · 건물 몸통 클릭 → {(shopHit ? "건물 ✅" : "❌ " + (h2.collider != null ? h2.collider.name : "없음"))} · 상자 {sb2.size.x:F0}×{sb2.size.y:F0}×{sb2.size.z:F0}");
        }
        return $"{ok}/{total} 통과\n{sb}";
    }
}
