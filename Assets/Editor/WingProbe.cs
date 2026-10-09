using System.Collections;
using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>날개 점검(10-09): 초월·불멸·영원 한 기씩 나란히 세우고 카메라를 당긴다. ClaudeBridge/g1n_wing.txt = front|back.
///   gameshot x.png 1 1280x720 click?:쉬움 mode:쉬움 wait:2 call:WingProbe.Setup wait:6</summary>
static class WingProbe
{
    static string Setup()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        bool front = File.ReadAllText("ClaudeBridge/g1n_wing.txt").Trim() == "front";
        var spawner = Object.FindFirstObjectByType<UnitSpawner>();
        var rts = Object.FindFirstObjectByType<RtsCameraController>();
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        foreach (var other in Object.FindObjectsByType<UnitAttacker>(FindObjectsSortMode.None)) other.gameObject.SetActive(false);
        string[] names = { "초월_노태현_AP", "불멸_박은석", "영원_이지원" };
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < names.Length; i++)
        {
            var d = AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{names[i]}.asset");
            if (d == null) { sb.AppendLine("❌ " + names[i]); continue; }
            var go = spawner.Spawn(d, c + new Vector3((i - 1) * 34f, 0f, 0f), 0);
            if (go == null) continue;
            if (go.TryGetComponent(out UnityEngine.AI.NavMeshAgent a)) a.enabled = false;
            if (go.TryGetComponent(out UnitFacing f)) f.enabled = false;
            go.transform.rotation = Quaternion.Euler(0f, front ? 180f : 0f, 0f);
            sb.AppendLine($"{names[i]} 등급 {d.grade} 날개 {UnitWings.WingNameFor(d.grade)}");
        }
        rts.FlyTo(c, 100f);
        return sb.ToString();
    }

    static string Report()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var w in Object.FindObjectsByType<WingModel>(FindObjectsSortMode.None))
        {
            var rs = w.GetComponentsInChildren<Renderer>();
            Bounds b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            sb.AppendLine($"{w.transform.parent.name}/{w.name} 위치 {w.transform.position} 크기 {b.size} 부모 {w.transform.parent.GetComponentInChildren<SkinnedMeshRenderer>()?.bounds.size}");
        }
        return sb.ToString();
    }
}
