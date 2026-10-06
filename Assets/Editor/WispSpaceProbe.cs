using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 선택 위습 공간 점검(10-06 「선택위습 공간이 조금 좁음」) — gameshot call:로 판 안에서 부른다.
///   call:WispSpaceProbe.Grant   내 0번에 흔함 선택 위습 6기 추가(칸에 쌓이는 모습 확인)
///   call:WispSpaceProbe.Focus   카메라를 흔함 선택 줄 가운데로
///   call:WispSpaceProbe.Report  위습 칸·포탈·부스 벽·위습 자리의 실측(수치)
/// </summary>
public static class WispSpaceProbe
{
    public static string Grant()
    {
        RewardDistributor rd = RewardDistributor.Instance;
        PlayerContext ctx = PlayerContext.Get(0);
        WispData data = AssetDatabase.LoadAssetAtPath<WispData>("Assets/Data/Wisps/Wisp_흔함선택.asset");
        if (rd == null || ctx == null || data == null) return "❌ RewardDistributor/PlayerContext/WispData 없음";
        rd.GrantWisps(ctx, new System.Collections.Generic.List<WispReward> { new WispReward { wisp = data, count = 6 } });
        return "흔함 선택 위습 6기 지급";
    }

    public static string Focus()
    {
        WispCell cell = WispCell.Get(UnitGrade.Common);
        Camera cam = Camera.main;
        RtsCameraController rts = cam != null ? cam.GetComponent<RtsCameraController>() : null;
        if (cell == null || rts == null) return "❌ 흔함 위습 칸/카메라 없음";
        rts.MoveTo(new Vector3(cell.transform.position.x, 0f, cell.transform.position.z));
        return $"카메라 → 칸 {cell.transform.position:F0}";
    }

    public static string Report()
    {
        StringBuilder sb = new StringBuilder();
        WispCell cell = WispCell.Get(UnitGrade.Common);
        if (cell == null) return "❌ 흔함 위습 칸 없음";
        Vector3 c = cell.transform.position;
        sb.AppendLine($"   위습 칸(흔함) 위치 {c:F1}");
        var portals = Object.FindObjectsByType<UnitPortal>(FindObjectsSortMode.None).Where(p => p.name.StartsWith("흔함선택_")).OrderBy(p => p.transform.position.x).ToList();
        if (portals.Count > 0)
        {
            float rowZ = portals[0].transform.position.z;
            float dx = portals.Count > 1 ? portals[1].transform.position.x - portals[0].transform.position.x : 0f;
            SphereCollider sc = portals[0].GetComponent<SphereCollider>();
            CapsuleCollider cc = portals[0].GetComponent<CapsuleCollider>();
            sb.AppendLine($"   포탈 {portals.Count}개 · 줄 z {rowZ:F1} · 칸 폭(포탈 간격) {dx:F1} · 지름 {portals[0].transform.localScale.x:F1} · 판정 {(sc != null ? $"구 r{sc.radius * portals[0].transform.lossyScale.x:F1}" : cc != null ? $"캡슐 r{cc.radius * portals[0].transform.lossyScale.x:F1} h{cc.height * portals[0].transform.lossyScale.y:F1}" : portals[0].GetComponent<Collider>()?.GetType().Name)}");
            sb.AppendLine($"   위습 칸 ↔ 포탈 줄 거리 {rowZ - c.z:F1}");
        }
        foreach (var w in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None).Where(g => g.name == "흔함구역_아래벽"))
            sb.AppendLine($"   아래벽 z {w.transform.position.z:F1} (칸 중심에서 {c.z - w.transform.position.z:F1} 아래)");
        var wisps = Object.FindObjectsByType<Wisp>(FindObjectsSortMode.None).Where(w => w.Data != null && (w.Data.wispName ?? "").Contains("흔함 선택")).ToList();
        sb.AppendLine($"   흔함 선택 위습 {wisps.Count}기");
        foreach (Wisp w in wisps)
            sb.AppendLine($"     {w.name} 위치 {w.transform.position:F1} · 칸 중심에서 ({w.transform.position.x - c.x:F1}, {w.transform.position.z - c.z:F1})");
        float minD = float.MaxValue;
        for (int i = 0; i < wisps.Count; i++)
            for (int j = i + 1; j < wisps.Count; j++)
                minD = Mathf.Min(minD, Vector3.Distance(wisps[i].transform.position, wisps[j].transform.position));
        if (wisps.Count > 1) sb.AppendLine($"   위습끼리 최소 거리 {minD:F1} (위습 지름 {(wisps[0].GetComponent<Collider>() != null ? wisps[0].GetComponent<Collider>().bounds.size.x : 0):F1})");
        return sb.ToString();
    }
}
