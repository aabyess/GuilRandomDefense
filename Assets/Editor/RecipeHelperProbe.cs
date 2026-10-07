using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// 조합 도우미 창 촬영 — gameshot: call:RecipeHelperProbe.Own → call:RecipeHelperProbe.Open → wait:2 → snap:
//   Own = 흔함·안흔함·특별함 유닛 몇 기를 세움(진행률이 오르게) · Open = 스위치 켜고 창 열기 · Sort/Filter = 정렬·필터 확인
static class RecipeHelperProbe
{
    static UnitData Roster(string n) => AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset");

    static string Own()
    {
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        LaneMarker lane = LaneMarker.Get(0);
        if (spawner == null || lane == null) return "❌ 준비 안 됨";
        string[] names = { "흔함_강재규", "흔함_박민수", "흔함_임장혁", "안흔함_강재규", "안흔함_황정기", "안흔함_박준희", "흔함_상붕카" };
        int n = 0;
        foreach (string name in names)
        {
            UnitData d = Roster(name);
            if (d == null) continue;
            spawner.Spawn(d, lane.LaneCenter + new Vector3(n * 30f, 0f, 0f), 0);
            n++;
        }
        return $"{n}기 세움";
    }

    static string Open()
    {
        RecipeHelperPanel.Show("", null);
        return $"열림 {RecipeHelperPanel.IsOpen}";
    }

    static string Sort()
    {
        var panel = Object.FindFirstObjectByType<RecipeHelperPanel>();
        typeof(RecipeHelperPanel).GetField("sort", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, System.Enum.ToObject(typeof(RecipeHelperPanel).GetNestedType("SortMode", BindingFlags.NonPublic), 1));
        typeof(RecipeHelperPanel).GetField("dirty", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, true);
        return "정렬 = 퍼센트 높은순";
    }

    static string Filter()
    {
        var panel = Object.FindFirstObjectByType<RecipeHelperPanel>();
        typeof(RecipeHelperPanel).GetField("filter", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, SkillTag.Stun);
        typeof(RecipeHelperPanel).GetField("dirty", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(panel, true);
        return "필터 = 스턴";
    }
}

static class RecipeHelperProbe2
{
    static string Tab1() { var p = Object.FindFirstObjectByType<RecipeHelperPanel>(); typeof(RecipeHelperPanel).GetField("tab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(p, 1); typeof(RecipeHelperPanel).GetMethod("LayoutColumns", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(p, null); return "탭 1"; }
    static string Tab2() { var p = Object.FindFirstObjectByType<RecipeHelperPanel>(); typeof(RecipeHelperPanel).GetField("tab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(p, 2); typeof(RecipeHelperPanel).GetMethod("LayoutColumns", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(p, null); return "탭 2"; }
    static string Popup() { var p = Object.FindFirstObjectByType<RecipeHelperPanel>(); typeof(RecipeHelperPanel).GetMethod("ToggleFilterPopup", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(p, null); return "필터 팝업 토글"; }
    static string Drawer()
    {
        var d = Object.FindFirstObjectByType<RecipeSearchDrawer>();
        typeof(RecipeSearchDrawer).GetMethod("SetOpen", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(d, new object[] { true });
        return "서랍 열림";
    }
}
