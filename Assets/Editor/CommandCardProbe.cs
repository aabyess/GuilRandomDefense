using System.Linq;
using UnityEngine;

/// <summary>명령 카드 원작 배치 촬영(10-07) — gameshot call:CommandCardProbe.Pick(unit) / .Multi / .Patrol. 로그에 칸 번호별 내용.</summary>
static class CommandCardProbe
{
    static UnitIdentity Spawn(string name, Vector3 pos)
    {
        UnitData d = UnityEditor.AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{name}.asset");
        return Object.FindFirstObjectByType<UnitSpawner>().Spawn(d, pos, 0).GetComponent<UnitIdentity>();
    }

    static void Select(params UnitIdentity[] units)
    {
        var sel = Object.FindFirstObjectByType<SelectionManager>();
        sel.SelectOnly(units[0].GetComponent<Selectable>());
    }

    static string Transcend() { var u = Spawn("초월_임채민_AP", LaneMarker.Get(0).LaneCenter); Select(u); return "초월 임채민(스킬 칸 5·6 · 조합 초상 8~)"; }
    static string Common() { var u = Spawn("흔함_최상호", LaneMarker.Get(0).LaneCenter); Select(u); return "흔함 최상호(조합 초상)"; }
    static string Legendary() { var u = Spawn("전설적인_최상호", LaneMarker.Get(0).LaneCenter); Select(u); return "전설 최상호(판매 칸 비어야)"; }

    static UnitIdentity patroller; static Vector3 a, b; static readonly System.Collections.Generic.List<string> log = new System.Collections.Generic.List<string>();
    static string PatrolSetup()
    {
        log.Clear();
        a = LaneMarker.Get(0).LaneCenter + new Vector3(-200f, 0f, -200f);
        b = a + new Vector3(420f, 0f, 0f);
        patroller = Spawn("초월_박민수_AD", a);
        var sel = Object.FindFirstObjectByType<SelectionManager>();
        sel.SelectOnly(patroller.GetComponent<Selectable>());
        int n = UnitCommands.Patrol(sel.Selected, b);
        UnityEditor.EditorApplication.update -= Sample; UnityEditor.EditorApplication.update += Sample;
        nextSample = Time.time;
        return $"반복 명령 {n}기: {a:F0} ↔ {b:F0}";
    }
    static float nextSample;
    static void Sample()
    {
        if (!Application.isPlaying || patroller == null) { UnityEditor.EditorApplication.update -= Sample; return; }
        if (Time.time < nextSample) return;
        nextSample += 2f;
        var combat = patroller.GetComponent<UnitCombat>();
        log.Add($"{Time.time:0}s x {patroller.transform.position.x:0} (A {a.x:0} ↔ B {b.x:0}) 표적 {(combat != null && combat.CurrentTarget != null ? combat.CurrentTarget.name : "-")}");
    }
    static string PatrolReport() => string.Join("\n   ", log);
}
