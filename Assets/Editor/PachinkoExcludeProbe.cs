using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>파칭코 보상 위습의 솔·성탄·뻬꼼 제외 실측(10-14): 전설·히든 포탈에 위습을 N번 넣어 나온 유닛 이름 집계 — 제외 표식 켠/끈 두 줄.
///   gameshot x 1 1280x720 mode:쉬움 wait:3 call:PachinkoExcludeProbe.Run  (결과는 로그 한 줄)</summary>
static class PachinkoExcludeProbe
{
    const int N = 400;

    static string Run()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        var data = AssetDatabase.LoadAssetAtPath<WispData>("Assets/Data/Wisps/Wisp_전설히든.asset");
        MethodInfo accepts = typeof(UnitPortal).GetMethod("Accepts", BindingFlags.NonPublic | BindingFlags.Instance);
        MethodInfo consume = typeof(UnitPortal).GetMethod("TryConsume", BindingFlags.NonPublic | BindingFlags.Instance);
        UnitPortal portal = Object.FindObjectsByType<UnitPortal>(FindObjectsSortMode.None)
            .FirstOrDefault(p => p.SpecificUnit == null && (bool)accepts.Invoke(p, new object[] { data.targetGrade }));
        if (portal == null) return "❌ 전설 받는 포탈 없음";
        var sb = new System.Text.StringBuilder($"포탈 {portal.name} · 위습 {data.wispName} · {N}회\n");
        foreach (bool exclude in new[] { true, false })
        {
            var counts = new Dictionary<string, int>();
            for (int i = 0; i < N; i++)
            {
                var before = new HashSet<UnitIdentity>(UnitIdentity.Active);
                GameObject go = Object.Instantiate(data.prefab, portal.transform.position, Quaternion.identity);
                Wisp wisp = go.GetComponent<Wisp>() ?? go.AddComponent<Wisp>();
                wisp.SetData(data); wisp.ExcludeRestrictedUnits = exclude;
                (go.GetComponent<OwnedByPlayer>() ?? go.AddComponent<OwnedByPlayer>()).SetOwner(0);
                consume.Invoke(portal, new object[] { go.GetComponent<Collider>() });
                foreach (UnitIdentity u in UnitIdentity.Active.ToList())
                {
                    if (before.Contains(u)) continue;
                    string n = u.Data != null ? u.Data.unitName : "?";
                    counts[n] = counts.TryGetValue(n, out int c) ? c + 1 : 1;
                    Object.Destroy(u.gameObject);
                }
                if (go != null) Object.Destroy(go);
            }
            int sol = counts.GetValueOrDefault("솔"), sung = counts.GetValueOrDefault("성탄"), bbe = counts.GetValueOrDefault("뻬꼼");
            sb.AppendLine($"제외 표식 {(exclude ? "켬" : "끔")}: 총 {counts.Values.Sum()}기 · 솔 {sol} · 성탄 {sung} · 뻬꼼 {bbe} · 종류 {counts.Count}");
        }
        return sb.ToString();
    }
}
