using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// 선술집 UI 촬영용(10-06) — gameshot: call:TavernProbe.Setup → wait:1 → snap: → call:TavernProbe.Single → wait:1 → snap: → call:TavernProbe.Multi → wait:1 → snap:
//   Setup = 흔함·안흔함·특별함·희귀함 유닛을 레인 가운데에 줄 세움 · Single = 첫 유닛만 선택 · Multi = 전부 선택(단일 → 다중 전환 확인) · Clear = 선택 해제
static class TavernProbe
{
    static readonly List<Selectable> units = new List<Selectable>();
    static UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

    static string Setup()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        if (spawner == null || lane == null) return "❌ 준비 안 됨";
        units.Clear();
        string[] names = { "흔함_강재규", "안흔함_강재규", "안흔함_황정기", "흔함_박민수" };
        for (int i = 0; i < names.Length; i++)
        {
            UnitData d = Roster(names[i]);
            if (d == null) continue;
            GameObject go = spawner.Spawn(d, lane.LaneCenter + new Vector3(i * 40f, 0f, 0f), 0);
            if (go != null && go.TryGetComponent(out Selectable s)) units.Add(s);
        }
        Object.FindFirstObjectByType<RtsCameraController>()?.MoveTo(lane.LaneCenter);
        return $"세움 {units.Count}기";
    }

    static string Single()
    {
        var sel = Object.FindFirstObjectByType<SelectionManager>();
        sel.SelectOnly(units[0]);
        return $"단일 선택: {units[0].name}";
    }

    static string Multi()
    {
        var sel = Object.FindFirstObjectByType<SelectionManager>();
        MethodInfo add = typeof(SelectionManager).GetMethod("AddToSelection", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (Selectable s in units) add.Invoke(sel, new object[] { s });
        return $"다중 선택: {sel.Selected.Count}기";
    }

    // 적 3마리 체력 100% · 50% · 10% (체력바 배경 확인용)
    static string Hp3()
    {
        LaneMarker lane = LaneMarker.Get(0);
        EnemyData ed = System.Linq.Enumerable.FirstOrDefault(
            System.Linq.Enumerable.Select(AssetDatabase.FindAssets("t:EnemyData", new[] { "Assets/Data/Enemies" }), g => AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(g))),
            e => e != null && !e.isBoss && e.prefab != null && e.name.Contains("R2"));
        if (lane == null || ed == null) return "❌ 준비 안 됨";
        float[] ratios = { 1f, 0.5f, 0.1f };
        FieldInfo hpField = typeof(EnemyDummy).GetField("hp", BindingFlags.Instance | BindingFlags.NonPublic);
        for (int i = 0; i < 3; i++)
        {
            GameObject go = Object.Instantiate(ed.prefab, lane.LaneCenter + new Vector3(-40f + i * 40f, 0f, 90f), Quaternion.Euler(0f, 180f, 0f));
            if (go.TryGetComponent(out WaypointMover m)) m.enabled = false;
            var e = go.GetComponent<EnemyDummy>();
            e.Initialize(ed, 1e3f);
            e.SetLane(0);
            hpField.SetValue(e, e.MaxHp * ratios[i]);
        }
        Object.FindFirstObjectByType<RtsCameraController>()?.MoveTo(lane.LaneCenter + new Vector3(0f, 0f, 90f));
        return "적 3마리 체력 100/50/10%";
    }

    // 명령 카드 칸 채움 순서 촬영 — call:TavernProbe.PickCommon / PickTranscend / PickMany
    static string Pick(string rosterName)
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        UnitData d = Roster(rosterName);
        if (spawner == null || lane == null || d == null) return "❌ 준비 안 됨 " + rosterName;
        GameObject go = spawner.Spawn(d, lane.LaneCenter, 0);
        Object.FindFirstObjectByType<SelectionManager>().SelectOnly(go.GetComponent<Selectable>());
        return $"선택: {d.DisplayName}";
    }
    static string PickCommon() => Pick("흔함_강재규");
    static string PickTranscend() => Pick("초월_신문철_AP");
    static string PickMany() => Pick("초월_김민준_AP");

    static string Clear() { Object.FindFirstObjectByType<SelectionManager>().ClearSelection(); return "선택 해제"; }
}
