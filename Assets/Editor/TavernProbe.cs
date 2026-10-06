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

    // 편집 모드에서 부른다(PlayerPrefs에 저장 — 다음 판부터 적용). call TavernProbe.SetStone → gameshot → call TavernProbe.SetTavern
    static string SetStone() { UiSkin.Theme = UiSkin.UiTheme.Stone; return "테마 = 돌(다음 판부터)"; }
    static string SetTavern() { UiSkin.Theme = UiSkin.UiTheme.Tavern; return "테마 = 선술집(다음 판부터)"; }

    static string Clear() { Object.FindFirstObjectByType<SelectionManager>().ClearSelection(); return "선택 해제"; }
}
