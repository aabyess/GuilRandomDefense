using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;

// 희귀함 오라 넷(강주혁 A064·김정래 A0PF·강보명 A0E0·김기연 A132+A133) 발동 확인(구현담당2, 10-15)
// gameshot: ... call:RareAuraProbe.Run wait:3 call:RareAuraProbe.Report
static class RareAuraProbe
{
    static readonly BindingFlags NP = BindingFlags.NonPublic | BindingFlags.Instance;
    static EnemyDummy enemy;
    static float armor0;
    static readonly string[] Units = { "희귀함_강주혁", "희귀함_김정래", "희귀함_강보명", "희귀함_김기연" };
    static int step;

    static string Run()
    {
        if (!Application.isPlaying) return "❌ 플레이 중에만";
        KillAll();
        EnemyData d = AssetDatabase.LoadAssetAtPath<EnemyData>("Assets/Data/Enemies/Enemy_R01_박진웅.asset");
        Vector3 c = LaneMarker.Get(0).LaneCenter;
        GameObject go = Object.Instantiate(d.prefab, c + new Vector3(0f, 0f, 15f), Quaternion.identity);
        if (go.TryGetComponent(out WaypointMover m)) m.enabled = false;
        enemy = go.GetComponent<EnemyDummy>(); enemy.Initialize(d, 1f); enemy.SetLane(0);
        armor0 = enemy.EffectiveArmor;
        var sp = Object.FindFirstObjectByType<UnitSpawner>();
        foreach (string n in Units)
            sp.Spawn(AssetDatabase.LoadAssetAtPath<UnitData>($"Assets/Data/Units/Roster/{n}.asset"), c, 0);
        return $"세움 · 적 이속배수 {enemy.EffectiveSlowMultiplier:F2} 방어 {armor0:F1}";
    }

    static void KillAll() { foreach (var u in UnitIdentity.Active.ToArray()) if (u != null && u.Data != null && System.Array.IndexOf(Units, u.Data.name) >= 0) Object.Destroy(u.gameObject); }

    // 넷이 한꺼번에 서 있으면 합쳐 보이니 하나씩 지우며 읽는다: 넷 → 강주혁 빼고 → …
    static string Report()
    {
        var sb = new StringBuilder($"넷 모두: 이속배수 {enemy.EffectiveSlowMultiplier:F2} · 방어 {enemy.EffectiveArmor:F1}(기본 {armor0:F1})");
        sb.Append("\n기대: 이속 min(0.75,0.85)=0.75(가장 센 하나) · 방어 −10(정래)·−10(보명)·−5(기연) 중 같은 id 아님 → 합 −25 → 방어 +25");
        return sb.ToString();
    }
}
