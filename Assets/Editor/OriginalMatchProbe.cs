using System.Linq;
using UnityEngine;

/// <summary>원작 대응 이름 표시 촬영(10-07) — gameshot call:OriginalMatchProbe.Show : 초월 최상호 AD(징베)를 세워 획득 알림이 뜨고 정보창에 선택.</summary>
static class OriginalMatchProbe
{
    static string Show()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        UnitSpawner spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        UnitData d = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/초월_최상호_AD.asset");
        UnitData d2 = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>("Assets/Data/Units/Roster/전설적인_최상호.asset");
        GameObject go = spawner.Spawn(d, lane.LaneCenter, 0);
        spawner.Spawn(d2, lane.LaneCenter + Vector3.right * 30f, 0);
        var sel = Object.FindFirstObjectByType<SelectionManager>();
        if (sel != null && go.TryGetComponent(out Selectable s)) sel.SelectOnly(s);
        return $"세움 {d.name}(원작 표시 「{d.OriginalMatchLabel}」) · {d2.name}(「{d2.OriginalMatchLabel}」)";
    }
}
