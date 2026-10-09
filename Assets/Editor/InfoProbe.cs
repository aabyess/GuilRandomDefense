using UnityEditor;
using UnityEngine;

/// <summary>정보창 촬영(10-09): ClaudeBridge/g2_info.txt의 로스터 유닛을 세우고 고른다. gameshot x.png 2 1920x1080 click?:보통 wait:2 call:InfoProbe.Select wait:1</summary>
static class InfoProbe
{
    static string Select()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        string name = System.IO.File.ReadAllText("ClaudeBridge/g2_info.txt").Trim();
        var data = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var sel = Object.FindFirstObjectByType<SelectionManager>();
        if (data == null || spawner == null || sel == null) return "❌ 유닛·스포너·선택 관리자 없음";
        GameObject go = spawner.Spawn(data, LaneMarker.Get(0).LaneCenter, 0);
        var s = go.GetComponent<Selectable>();
        sel.SelectOnly(s);
        return $"{name} · 원작 표시 「{data.OriginalMatchLabel}」";
    }
}
